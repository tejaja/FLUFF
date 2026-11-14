using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

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
    private Material[] _mats;
    private int _active = -2;

    private void Start()
    {
        _mats = new Material[holes.Length];
        for (int i = 0; i < holes.Length; i++) if (holes[i] != null) { _mats[i] = holes[i].material; _mats[i].SetFloat("_Active", 0f); }
        if (camL != null) camL.enabled = false;
        if (camR != null) camR.enabled = false;
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
        }
        bool on = best >= 0;
        if (camL.enabled != on) camL.enabled = on;
        if (camR != null && camR.enabled != (on && vr)) camR.enabled = on && vr;
        if (!on) return;

        Transform en = entries[best], ex = exits[best];
        Matrix4x4 m = ex.localToWorldMatrix * Matrix4x4.Rotate(Quaternion.Euler(0f, 180f, 0f)) * en.worldToLocalMatrix;
        Vector3 target = m.MultiplyPoint3x4(holes[best].bounds.center);
        if (vr && camR != null)
        {
            float ipd = ipdBase * Mathf.Max(0.2f, p.GetAvatarEyeHeightAsMeters()) / 1.6f;
            Vector3 right = hr * Vector3.right;
            Matrix4x4 vl = Place(camL, m.MultiplyPoint3x4(head - right * (ipd * 0.5f)), target, ex, m);
            Matrix4x4 vrm = Place(camR, m.MultiplyPoint3x4(head + right * (ipd * 0.5f)), target, ex, m);
            _mats[best].SetMatrix("_VPL", vl); _mats[best].SetMatrix("_VPR", vrm);
        }
        else
        {
            Matrix4x4 vl = Place(camL, m.MultiplyPoint3x4(head), target, ex, m);
            _mats[best].SetMatrix("_VPL", vl); _mats[best].SetMatrix("_VPR", vl);
        }
    }

    // カメラを置いて、穴のシェーダーに渡す行列（入口側のワールド → このカメラのクリップ）を返す
    private Matrix4x4 Place(Camera cam, Vector3 pos, Vector3 target, Transform ex, Matrix4x4 m)
    {
        Vector3 fwd = target - pos; float dist = fwd.magnitude;
        if (dist < 0.001f) { fwd = ex.forward; dist = 0.001f; }
        cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd / dist, ex.up));
        cam.ResetProjectionMatrix();
        cam.fieldOfView = Mathf.Clamp(2f * Mathf.Atan(holeRadius / Mathf.Max(dist, 0.05f)) * Mathf.Rad2Deg * 1.1f, 4f, 170f);
        // 斜めの近クリップ：出口の面（少し手前）より奥を切る
        Matrix4x4 w2c = cam.worldToCameraMatrix;
        float sgn = Mathf.Sign(Vector3.Dot(ex.forward, ex.position - pos));
        Vector3 n = w2c.MultiplyVector(ex.forward) * sgn;
        Vector3 cp = w2c.MultiplyPoint(ex.position + ex.forward * 0.02f);
        if (Mathf.Abs(Vector3.Dot(ex.position - pos, ex.forward)) > 0.05f)
            cam.projectionMatrix = cam.CalculateObliqueMatrix(new Vector4(n.x, n.y, n.z, -Vector3.Dot(cp, n)));
        return cam.projectionMatrix * w2c * m;
    }
}
