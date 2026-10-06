using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

// ふわふわ追っかけカメラ（自分にだけ、同期なし）。自分の視界はそのままで、VRChatの手持ちカメラ（写真・ストリーム）にだけ映す。
// ワールドからはVRChatカメラを動かせないので、追っかけカメラの映像をテクスチャ（feedTexture）に描いて、
// それを VRChatカメラのレンズの前に置いた板（feed、Fuwa/PhotoCamFeed）で「VRChatカメラで描く時だけ画面いっぱいに」貼る。
// 画角と縦横比はVRChatカメラに合わせるので、カメラ側のズームもそのまま効く。
// 設定ボタン（CycleMode）か、デスクトップならキー（toggleKey）で OFF → 後ろ → 横 → 上 と切り替え。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaFollowCam : UdonSharpBehaviour
{
    [Tooltip("追っかけ用のカメラ（Target Texture に feedTexture を入れておく）")]
    public Camera cam;
    [Tooltip("VRChatカメラのレンズの前に置く板（Fuwa/PhotoCamFeed のマテリアル）")]
    public Transform feed;
    public Renderer feedRenderer;
    [Tooltip("板をレンズからどれだけ前に置くか(m)")]
    public float feedDistance = 0.3f;

    [Tooltip("追いかける物（ひとりで用のふわふわ、みんなで用のふわふわ…）。見えていて近いものを追う")]
    public Transform[] targets;
    [Tooltip("targets と同じ順で、その物の見た目。消えている（隠れている）時は追わない")]
    public Renderer[] targetRenderers;
    [Tooltip("これより遠いふわふわは追わない（自分だけ映す）")]
    public float maxTargetDistance = 25f;

    [Header("切り替え")]
    public KeyCode toggleKey = KeyCode.B;
    [Tooltip("ボタンの今のモードの文字（各ロビーのボタン）")]
    public TextMeshProUGUI[] modeTexts;
    [Tooltip("modeTexts と同じ順で、ONの時に文字の後ろに出す白い札")]
    public UnityEngine.UI.Image[] modePills;
    [Tooltip("ONの時の文字の色（ボタンの色）")]
    public Color activeTextColor = new Color(0.45f, 0.78f, 0.62f, 1f);
    [Tooltip("OFFの時の文字の色")]
    public Color idleTextColor = new Color(1f, 1f, 1f, 0.75f);

    [Header("カメラの動き")]
    [Tooltip("位置の追いつく時間(秒)。大きいほどふわっと遅れてついてくる")]
    public float posSmoothTime = 0.35f;
    [Tooltip("向きの追いつく速さ")]
    public float lookSpeed = 6f;
    [Tooltip("進む向き（自分→ふわふわ）の変わる速さ")]
    public float dirSpeed = 2.5f;
    [Tooltip("自分とふわふわの間のどこを見るか（0=自分、1=ふわふわ）")]
    [Range(0f, 1f)] public float focusBlend = 0.55f;
    [Tooltip("カメラと見る所の間に地面や壁があったら手前に寄せる")]
    public LayerMask obstacleMask = (1 << 0) | (1 << 11);
    [Tooltip("テスト用：VRChatカメラがなくても（ClientSimでも）追っかけカメラを動かす")]
    public bool runWithoutPhotoCamera = false;

    private int _mode;   // 0=OFF 1=後ろ 2=横 3=上
    private Vector3 _dir = Vector3.forward;
    private Vector3 _focus;
    private Vector3 _vel;
    private bool _snap;
    private bool _running;

    private void Start()
    {
        if (cam != null) cam.enabled = false;
        if (feedRenderer != null) feedRenderer.enabled = false;
        UpdateLabels();
    }

    // 設定のカメラボタン（レーザーで押すUIボタン）から呼ばれる。自分だけ
    public void CycleMode()
    {
        _mode = (_mode + 1) % 4;
        _snap = true;
        UpdateLabels();
    }

    public void TurnOff()
    {
        _mode = 0;
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        string s = _mode == 1 ? "BACK" : _mode == 2 ? "SIDE" : _mode == 3 ? "TOP" : "OFF";
        if (modeTexts != null)
            foreach (TextMeshProUGUI t in modeTexts) if (t != null) { t.text = s; t.color = _mode != 0 ? activeTextColor : idleTextColor; }
        if (modePills != null)
            foreach (UnityEngine.UI.Image im in modePills) if (im != null) im.enabled = _mode != 0;
    }

    private void Update()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        if (!lp.IsUserInVR() && Input.GetKeyDown(toggleKey)) CycleMode();
    }

    // VRChatカメラが動いた後に、板をレンズの前に置き直す。追っかけカメラもここで動かす
    public override void PostLateUpdate()
    {
        if (cam == null) return;
        VRCCameraSettings photo = VRCCameraSettings.PhotoCamera;
        bool photoOn = photo != null && photo.Active;
        bool run = _mode != 0 && (photoOn || runWithoutPhotoCamera);
        if (run != _running)
        {
            _running = run;
            cam.enabled = run;
            if (run) _snap = true;
        }
        if (feedRenderer != null && feedRenderer.enabled != (run && photoOn)) feedRenderer.enabled = run && photoOn;
        if (!run) return;

        if (photoOn)
        {
            cam.fieldOfView = photo.FieldOfView;
            if (photo.Aspect > 0.01f) cam.aspect = photo.Aspect;
            if (feed != null)
            {
                feed.position = photo.Position + photo.Forward * feedDistance;
                feed.rotation = photo.Rotation;
            }
        }
        MoveCamera();
    }

    private void MoveCamera()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;

        float eye = lp.GetAvatarEyeHeightAsMeters();
        Vector3 p = lp.GetPosition() + Vector3.up * (eye * 0.6f);

        Transform tgt = FindTarget(p);
        Vector3 b;
        if (tgt != null) b = tgt.position;
        else
        {
            Vector3 hf = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
            hf.y = 0f;
            if (hf.sqrMagnitude < 0.0001f) hf = _dir;
            b = p + hf.normalized * 1.5f;
        }

        // 進む向き：自分→ふわふわ（横だけ）。急に振り向かないようにゆっくり回す
        Vector3 d = b - p;
        d.y = 0f;
        if (d.sqrMagnitude > 0.04f)
        {
            d.Normalize();
            _dir = _snap ? d : Vector3.Slerp(_dir, d, 1f - Mathf.Exp(-dirSpeed * Time.deltaTime)).normalized;
        }
        float sep = Vector3.Distance(p, b);
        Vector3 focus = Vector3.Lerp(p, b, focusBlend);
        Vector3 right = Vector3.Cross(Vector3.up, _dir).normalized;

        Vector3 want;
        if (_mode == 1) want = p - _dir * (2.6f + sep * 0.25f) + Vector3.up * (1.2f + sep * 0.12f);
        else if (_mode == 2) want = focus + right * (3.0f + sep * 0.6f) + Vector3.up * 0.7f - _dir * 0.5f;
        else want = focus - _dir * (2.0f + sep * 0.3f) + Vector3.up * (5.0f + sep * 0.4f);

        // 地面や壁にめり込まないように、見る所からカメラまでの間に何かあれば手前に
        Vector3 toCam = want - focus;
        float len = toCam.magnitude;
        RaycastHit hit;
        if (len > 0.01f && Physics.Raycast(focus, toCam / len, out hit, len, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            want = hit.point + hit.normal * 0.25f;
        }

        Transform ct = cam.transform;
        if (_snap)
        {
            ct.position = want;
            _focus = focus;
            _vel = Vector3.zero;
            ct.rotation = Quaternion.LookRotation(focus - want, Vector3.up);
            _snap = false;
            return;
        }
        // SmoothDamp と同じばね（Udon で ref 引数を避けるため自前で）
        float omega = 2f / Mathf.Max(0.01f, posSmoothTime);
        float x = omega * Time.deltaTime;
        float ex = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
        Vector3 change = ct.position - want;
        Vector3 temp = (_vel + omega * change) * Time.deltaTime;
        _vel = (_vel - omega * temp) * ex;
        ct.position = want + (change + temp) * ex;
        _focus = Vector3.Lerp(_focus, focus, 1f - Mathf.Exp(-lookSpeed * 1.5f * Time.deltaTime));
        Vector3 look = _focus - ct.position;
        if (look.sqrMagnitude > 0.0001f)
        {
            Quaternion r = Quaternion.LookRotation(look, Vector3.up);
            ct.rotation = Quaternion.Slerp(ct.rotation, r, 1f - Mathf.Exp(-lookSpeed * Time.deltaTime));
        }
    }

    private Transform FindTarget(Vector3 p)
    {
        if (targets == null) return null;
        Transform best = null;
        float bestD = maxTargetDistance;
        for (int i = 0; i < targets.Length; i++)
        {
            Transform t = targets[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            if (targetRenderers != null && i < targetRenderers.Length)
            {
                Renderer r = targetRenderers[i];
                if (r != null && !r.enabled) continue;
            }
            float dd = Vector3.Distance(p, t.position);
            if (dd < bestD) { bestD = dd; best = t; }
        }
        return best;
    }
}
