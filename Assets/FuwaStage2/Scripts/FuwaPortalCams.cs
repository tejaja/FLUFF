using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Rendering;

// 2026-11-14 カメラ式ポータル（てじゃ「ワープ前にカーテンとかクモの巣とかみえない」「明るさ」→ A：カメラ式）
//  入口（entries[i]）の穴に、出口（exits[i]）側から見た本物の景色を映す。空・遠景・巣・カーテン・影の明るさもそのまま
//  ・カメラは左目・右目の2台（デスクトップは左だけ、頭のまん中）。自分の頭（目）を 入口→出口 の変換で出口側へ移した所に置く
//  ・カメラは出口の穴の中心を向いて、穴がちょうど入るくらいの画角（解像度をむだにしない）。出口の面より奥は斜めの近クリップで切る（出口のうしろの壁・裏の円盤が映らない）
//  ・穴のシェーダー（Fuwa/PortalCam）は、穴の面のワールド座標を「カメラの投影 × view × 入口→出口」で投影して映像を読む（_VPL/_VPR）
//  ・負荷対策：近くて手前にいて、ある程度そっちを見ているポータル1つだけカメラを動かす。ほかの穴は待機のうず（_Active=0）
//  人・ふわふわを運ぶのは今まで通り FuwaPortal
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPortalCams : UdonSharpBehaviour
{
    public Transform[] entries;
    public Transform[] exits;
    public MeshRenderer[] holes;
    public Camera camL, camR;
    [Tooltip("これより遠いポータルは映さない")]
    public float range = 70f;
    [Tooltip("穴の半径（画角の計算用、少し大きめ）")]
    public float holeRadius = 1.9f;
    [Tooltip("頭の向きと穴の方向のなす角の cos がこれより小さい（見ていない）なら映さない（近い時はいつも映す）")]
    public float viewCos = 0.0f;
    public float ipdBase = 0.064f;
    [Tooltip("穴に入る直前（穴に合わせる画角がこれを超える）は、頭の向きのままの画角 closeFov に切りかえる（2026-11-15 てじゃ「ポータル入る瞬間、こんなかんじに」→ 170°の画角で引きのばされていた）")]
    public float closeSwitchFov = 100f;
    public float closeFov = 130f;
    [Tooltip("出口の穴（自分の円盤）をこのレイヤーに移して、ポータルのカメラには映さない（入る直前に出口の円盤が目の前に大きく映っていた）")]
    public int hideLayer = 25;
    // 2026-11-15 てじゃ「ポータルの軽量化」「近寄ったほうがボケてみえるのなんで？」
    //  ・映像の大きさ（解像度）を、穴が画面に映る大きさに合わせる：レンダーテクスチャは1組（最大 rtSize）のまま、カメラは左下の一部（rect）だけに描く。穴のシェーダーは _VS 倍の所を読む
    //    （前は 1024 固定：遠いと余り、近いと足りずにボケた）
    //  ・入る直前は 130° ではなく、画面のカメラの横の画角に合わせる（余分に広く描くと 1 ピクセルがうすくなる）
    //  ・遠い時（monoDist より遠い）は VR でもカメラ1台（両目に同じ映像）。halfRateDist より遠いと 2 フレームに 1 回だけ描く
    //  ・小さい物（layerCull のレイヤー）はポータルのカメラでは近くだけ（layerCullDist）
    [Tooltip("映像のくっきりさ（1＝画面のピクセルと同じ。少し下げると軽い）")]
    public float quality = 0.85f;
    public int minPixels = 256;
    public float monoDist = 9f, halfRateDist = 16f;
    [Tooltip("ポータルのカメラで近くだけ映すレイヤーと、その距離（m）")]
    public int[] layerCull;
    public float layerCullDist = 25f;
    private float _vs = 1f;
    private bool _mono;
    private Material[] _mats;
    private int _active = -2;
    private int[] _exitHole;
    private int _hidHole = -1, _hidLayer0;

    private void Start()
    {
        _mats = new Material[holes.Length];
        for (int i = 0; i < holes.Length; i++) if (holes[i] != null) { _mats[i] = holes[i].material; _mats[i].SetFloat("_Active", 0f); }
        if (camL != null) { camL.enabled = false; camL.cullingMask &= ~(1 << hideLayer); }
        if (camR != null) { camR.enabled = false; camR.cullingMask &= ~(1 << hideLayer); }
        if (layerCull != null && layerCull.Length > 0)
        {
            float[] cd = new float[32];
            for (int k = 0; k < layerCull.Length; k++) if (layerCull[k] >= 0 && layerCull[k] < 32) cd[layerCull[k]] = layerCullDist;
            if (camL != null) { camL.layerCullDistances = cd; camL.layerCullSpherical = true; }
            if (camR != null) { camR.layerCullDistances = cd; camR.layerCullSpherical = true; }
        }
        _exitHole = new int[entries.Length];
        for (int i = 0; i < entries.Length; i++)
        {
            _exitHole[i] = -1;
            if (exits[i] == null) continue;
            for (int j = 0; j < holes.Length; j++) if (holes[j] != null && holes[j].transform.IsChildOf(exits[i])) { _exitHole[i] = j; break; }
        }
    }

    public override void PostLateUpdate()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || camL == null) return;
        VRCPlayerApi.TrackingData hd = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 head = hd.position; Quaternion hr = hd.rotation; Vector3 hf = hr * Vector3.forward;
        int best = -1; float bd = range;
        for (int i = 0; i < holes.Length; i++)
        {
            if (holes[i] == null || entries[i] == null || exits[i] == null) continue;
            if (!holes[i].gameObject.activeInHierarchy) continue;
            Vector3 c = holes[i].bounds.center; Vector3 d = c - head; float dist = d.magnitude;
            if (dist >= bd) continue;
            if (Vector3.Dot(head - entries[i].position, entries[i].forward) <= 0f) continue;   // 入口の裏にいる
            if (dist > 4f && Vector3.Dot(hf, d / dist) < viewCos) continue;                     // 見ていない
            best = i; bd = dist;
        }
        bool vr = p.IsUserInVR();
        if (best != _active)
        {
            for (int i = 0; i < _mats.Length; i++) if (_mats[i] != null) _mats[i].SetFloat("_Active", i == best ? 1f : 0f);
            _active = best;
            // 出口の円盤をポータルのカメラから隠す（前に隠したのは元のレイヤーへ）
            if (_hidHole >= 0 && holes[_hidHole] != null) holes[_hidHole].gameObject.layer = _hidLayer0;
            _hidHole = best >= 0 ? _exitHole[best] : -1;
            if (_hidHole >= 0 && holes[_hidHole] != null) { _hidLayer0 = holes[_hidHole].gameObject.layer; holes[_hidHole].gameObject.layer = hideLayer; }
        }
        bool on = best >= 0;
        if (!on)
        {
            if (camL.enabled) camL.enabled = false;
            if (camR != null && camR.enabled) camR.enabled = false;
            return;
        }

        Transform en = entries[best], ex = exits[best];
        Matrix4x4 m = ex.localToWorldMatrix * Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f)) * en.worldToLocalMatrix;
        Quaternion camRot = ex.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(en.rotation) * hr;   // 頭の向きを出口側へ
        Vector3 target = m.MultiplyPoint3x4(holes[best].bounds.center);

        // 画面のカメラ（縦の画角・ピクセル数）
        VRCCameraSettings sc = VRCCameraSettings.ScreenCamera;
        float vFov = 60f; float pxW = 1920f, pxH = 1080f;
        if (sc != null) { vFov = Mathf.Clamp(sc.FieldOfView, 20f, 150f); pxW = Mathf.Max(64f, sc.PixelWidth); pxH = Mathf.Max(64f, sc.PixelHeight); }
        float aspect = pxW / pxH;
        float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f * Mathf.Deg2Rad) * aspect) * Mathf.Rad2Deg;
        // 穴が画面にどのくらいの大きさで映るか → 映像の解像度
        float ang = 2f * Mathf.Atan(holeRadius / Mathf.Max(bd, 0.05f)) * Mathf.Rad2Deg;
        float fitFov = ang * 1.1f;
        bool close = fitFov > Mathf.Min(closeSwitchFov, hFov);
        float camFov = close ? Mathf.Min(hFov * 1.04f, closeFov) : fitFov;
        float pxPerDeg = pxH / vFov;
        float need = camFov * pxPerDeg * quality;                 // この画角をこの密度で描くのに要るピクセル
        // 遠い時：VR でもカメラ1台・2フレームに1回（描かないフレームは前の映像・行列・大きさのまま）
        bool skip = !close && bd > halfRateDist && (Time.frameCount & 1) == 1;
        if (skip)
        {
            if (camL.enabled) camL.enabled = false;
            if (camR != null && camR.enabled) camR.enabled = false;
            return;
        }
        int rtSz = camL.targetTexture != null ? camL.targetTexture.width : 1024;
        float vs = Mathf.Clamp(need / rtSz, (float)minPixels / rtSz, 1f);
        if (vs > _vs || vs < _vs * 0.8f) _vs = Mathf.Min(1f, vs * 1.1f);   // ちらつかないように少し余裕・のりしろ
        Rect rc = new Rect(0f, 0f, _vs, _vs);
        _mono = !vr || camR == null || (!close && bd > monoDist);
        if (!camL.enabled) camL.enabled = true;
        if (camR != null && camR.enabled != !_mono) camR.enabled = !_mono;
        Material mat = _mats[best];
        mat.SetFloat("_VS", _vs); mat.SetFloat("_Mono", _mono ? 1f : 0f);
        camL.rect = rc; if (camR != null) camR.rect = rc;
        if (!_mono)
        {
            float ipd = ipdBase * Mathf.Max(0.2f, p.GetAvatarEyeHeightAsMeters()) / 1.6f;
            Vector3 right = hr * Vector3.right;
            Matrix4x4 vl = Place(camL, m.MultiplyPoint3x4(head - right * (ipd * 0.5f)), target, ex, m, camRot, close, camFov);
            Matrix4x4 vrm = Place(camR, m.MultiplyPoint3x4(head + right * (ipd * 0.5f)), target, ex, m, camRot, close, camFov);
            mat.SetMatrix("_VPL", vl); mat.SetMatrix("_VPR", vrm);
        }
        else
        {
            Matrix4x4 vl = Place(camL, m.MultiplyPoint3x4(head), target, ex, m, camRot, close, camFov);
            mat.SetMatrix("_VPL", vl); mat.SetMatrix("_VPR", vl);
        }
    }

    // カメラを置いて、穴のシェーダーに渡す行列（入口側のワールド → このカメラのクリップ）を返す
    private Matrix4x4 Place(Camera cam, Vector3 pos, Vector3 target, Transform ex, Matrix4x4 m, Quaternion headRot, bool close, float camFov)
    {
        Vector3 fwd = target - pos; float dist = fwd.magnitude;
        if (dist < 0.001f) { fwd = ex.forward; dist = 0.001f; }
        cam.ResetProjectionMatrix();
        if (close)
        {
            // 穴のすぐ前：頭の向きのまま、画面と同じくらいの画角（見えている所だけ映せばいい）
            cam.transform.SetPositionAndRotation(pos, headRot);
        }
        else
        {
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd / dist, ex.up));
        }
        cam.fieldOfView = Mathf.Clamp(camFov, 4f, 170f);
        // 出口の面より奥（カメラ側）を切る：前は斜めの近クリップ（CalculateObliqueMatrix）だったが、クリップの奥行きがゆがんで霧（遠景のぼかし）が効かなくなった
        //  （2026-11-14 てじゃ「ポータル越しだと遠景にかかるぼかしがちゃんときかない、火山がはっきり」）
        //  → ふつうの近クリップを、出口の穴の四すみのうちいちばん近い所の手前まで出す（穴のふち付近で出口の裏が少し見えることがあるだけ）
        Vector3 cf = cam.transform.forward; float nearD = 1e9f;
        for (int k = 0; k < 4; k++)
        {
            Vector3 q = target + ex.right * ((k & 1) == 0 ? -holeRadius : holeRadius) + ex.up * ((k & 2) == 0 ? -holeRadius : holeRadius);
            nearD = Mathf.Min(nearD, Vector3.Dot(q - pos, cf));
        }
        cam.nearClipPlane = Mathf.Clamp(nearD * 0.97f, 0.05f, 500f);
        Matrix4x4 w2c = cam.worldToCameraMatrix;
        return cam.projectionMatrix * w2c * m;
    }
}
