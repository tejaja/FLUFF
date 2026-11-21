using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

// スマホの画面に出す竹とんぼの ↑↓ ボタン（2026-11-21 てじゃ「竹とんぼがスマホ版だと下降ができない」「スマホ用の上昇下降ボタンもありね」→ B案）
// ・スマホ（Android/iOS版で VR でない人）が竹とんぼを持っている間だけ、画面の右に ↑ と ↓ を出す
// ・1タップで FuwaBambooCopter.tapStep m だけスーッと上がる／下がる。連打でたくさん（UI のボタンは「押している間」が取れないので）
// ・見た目・タップの受け方・画面への置き方はエアガン呼び出しボタン（FuwaMobileGunButton）と同じ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaMobileCopterButtons : UdonSharpBehaviour
{
    public FuwaBambooCopter[] copters;
    [Tooltip("↑ の見た目（Fuwa/ScreenButton の板）と、タップを受ける物（Canvas＋ボタンの親。毎フレーム同じ所へ動かす）")]
    public Renderer upVisual;
    public Transform upRoot;
    public GameObject upUi;
    public Renderer downVisual;
    public Transform downRoot;
    public GameObject downUi;
    // 置き場所は縦画面・横画面で分ける（VRChat の手のボタン・ジャンプと重ならないように。2026-11-21 てじゃのスクショ）
    //  横画面：手のボタン2つ（右の真ん中あたり）の少し左に縦に並べる／縦画面：右下の手のボタンの上に縦に並べる
    [Tooltip("横画面：ボタンの中心の、画面の右はしからの距離（短い辺に対する割合）")]
    public float landRight = 0.62f;
    [Tooltip("横画面：↑ の中心の、画面の上はしからの距離（画面の高さに対する割合）")]
    public float landUpFromTop = 0.32f;
    [Tooltip("縦画面：ボタンの中心の、画面の右はしからの距離（短い辺に対する割合）")]
    public float portRight = 0.12f;
    [Tooltip("縦画面：↑ の中心の、画面の上はしからの距離（画面の高さに対する割合）")]
    public float portUpFromTop = 0.56f;
    [Tooltip("↑ と ↓ の中心の間（短い辺に対する割合）")]
    public float gap = 0.2f;
    [Tooltip("ボタンの直径（短い辺に対する割合）")]
    public float diameter = 0.16f;
    [Tooltip("カメラからの距離(m)")]
    public float distance = 0.35f;
    [Tooltip("テスト用：PCでも（竹とんぼを持っている時に）出す")]
    public bool forceShow = false;

    private bool _mobile;
    private bool _shown = true;
    private FuwaBambooCopter _mine;

    private void Start()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        bool vr = lp != null && lp.IsUserInVR();
#if UNITY_ANDROID || UNITY_IOS
        _mobile = !vr;
#else
        _mobile = false;
#endif
        Show(false);
    }

    private void Show(bool on)
    {
        if (on == _shown) return;
        _shown = on;
        if (upVisual != null) upVisual.enabled = on;
        if (downVisual != null) downVisual.enabled = on;
        if (upUi != null) upUi.SetActive(on);
        if (downUi != null) downUi.SetActive(on);
    }

    public void OnTapUp() { if (_mine != null) _mine.MobileUp(); }
    public void OnTapDown() { if (_mine != null) _mine.MobileDown(); }

    public override void PostLateUpdate()
    {
        if (!_mobile && !forceShow) { Show(false); return; }
        _mine = null;
        if (copters != null) for (int i = 0; i < copters.Length; i++) if (copters[i] != null && copters[i].IsMine()) { _mine = copters[i]; break; }
        if (_mine == null) { Show(false); return; }
        VRCCameraSettings cam = VRCCameraSettings.ScreenCamera;
        if (cam == null) { Show(false); return; }
        float w = cam.PixelWidth, h = cam.PixelHeight;
        if (w < 1f || h < 1f) { Show(false); return; }
        float s = Mathf.Min(w, h);
        float tanV = Mathf.Tan(cam.FieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * (w / h);
        float d = Mathf.Max(distance, cam.NearClipPlane * 2f);
        Quaternion rot = cam.Rotation;
        float size = diameter * s * (2f * d * tanV / h);   // ピクセル → m
        Vector3 sc = new Vector3(size, size, size);
        bool land = w >= h;
        float nx = (w - (land ? landRight : portRight) * s) / w * 2f - 1f;
        float yUp = h * (1f - (land ? landUpFromTop : portUpFromTop));
        Place(upRoot, upVisual, cam.Position, rot, nx, yUp / h * 2f - 1f, tanH, tanV, d, sc);
        Place(downRoot, downVisual, cam.Position, rot, nx, (yUp - gap * s) / h * 2f - 1f, tanH, tanV, d, sc);
        Show(true);
    }

    private void Place(Transform root, Renderer vis, Vector3 camPos, Quaternion rot, float nx, float ny, float tanH, float tanV, float d, Vector3 sc)
    {
        Vector3 pos = camPos + rot * new Vector3(nx * tanH * d, ny * tanV * d, d);
        if (root != null) { root.SetPositionAndRotation(pos, rot); root.localScale = sc; }
        if (vis != null) { Transform vt = vis.transform; vt.SetPositionAndRotation(pos, rot); vt.localScale = sc; }
    }
}
