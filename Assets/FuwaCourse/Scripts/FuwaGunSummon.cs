using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

// 自分のエアガンをどこからでも呼び出す（自分だけ）。
// ・デスクトップ：Eキーで目の前に出てくる（あとはいつも通りクリックで持つ）
// ・VR：右スティックを下にしばらく倒すと、右手の中に銃が出てきて手についてくる。そのままグリップを握れば持てる
//   （VRChatはスクリプトから「持たせる」ことはできないので、握るまで手の位置に置き続ける。
//     waitSeconds 秒たっても握らなければ、元の場所に戻す）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGunSummon : UdonSharpBehaviour
{
    public FuwaRespawnManager manager;
    [Tooltip("デスクトップで呼び出すキー")]
    public KeyCode desktopKey = KeyCode.E;
    [Tooltip("デスクトップ：目の前のどれくらい先に出すか(m)")]
    public float desktopDistance = 0.6f;
    [Tooltip("デスクトップ：目線からどれくらい下に出すか(m)")]
    public float desktopDrop = 0.25f;

    [Header("VR：右スティックの下入力で呼び出す")]
    [Tooltip("スティックをこれ以上下に倒したら「下入力」（-1が一番下）")]
    public float stickThreshold = -0.7f;
    [Tooltip("下入力をこの秒数続けたら呼び出す（うっかり防止）")]
    public float stickHoldSeconds = 0.4f;
    [Tooltip("呼び出してから握るのを待つ秒数。過ぎたら元の場所に戻す")]
    public float waitSeconds = 5f;

    [Tooltip("VR：掴んだ時の銃の向き（手の向きからの回転。右手の値）")]
    public Vector3 gripEuler = new Vector3(90f, 0f, 0f);
    [Tooltip("（左手用・今は使っていない）左右反転したあとに追加で回す角度")]
    public Vector3 leftExtraEuler = new Vector3(0f, 0f, 180f);
    [Tooltip("VR：最後に銃自身の向き基準で足す傾き。X＋で銃口が下がる/グリップが前に倒れる向き")]
    public Vector3 gripTilt = new Vector3(45f, 0f, 0f);
    [Tooltip("VR：手の位置からのずらし（銃自身の向き基準 m。Y＋で銃が上＝手がグリップの下の方を握る）")]
    public Vector3 gripOffset = new Vector3(0f, 0.03f, 0f);
    [Tooltip("VR：握るのを待つ間、何秒ごとに手の位置へ置き直すか")]
    public float followInterval = 0.02f;

    [Tooltip("銃を置く位置として毎回動かす空のオブジェクト（Udonでは新しく作れないので用意しておく）")]
    public Transform summonSpot;
    [Tooltip("（今は使っていない）見た目だけの銃")]
    public GameObject decoy;

    [Header("VRで呼び出す時の、手元の丸いゲージ（なくてもOK）")]
    [Tooltip("FuwaCourse/ProgressRing の Quad。スティックを倒している間だけ右手の上に出て、たまったら呼び出し")]
    public Renderer gauge;
    [Tooltip("ゲージの大きさ(m)")]
    public float gaugeSize = 0.11f;
    [Tooltip("手の甲からどれだけ浮かせるか(m)")]
    public float gaugeLift = 0.09f;
    private Material _gaugeMat;
    private float _gaugeShown = -1f;
    private float _gaugeFlashUntil;

    private void Start()
    {
        if (gauge != null) { _gaugeMat = gauge.material; gauge.enabled = false; }
    }

    // ゲージを出す（f=0〜1、負なら消す）。右手の上に、顔の方を向けて置く
    private void ShowGauge(VRCPlayerApi p, float f)
    {
        if (gauge == null) return;
        if (f < 0f)
        {
            if (gauge.enabled) gauge.enabled = false;
            _gaugeShown = -1f;
            return;
        }
        VRCPlayerApi.TrackingData hand = p.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
        VRCPlayerApi.TrackingData head = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 pos = hand.position + Vector3.up * gaugeLift;
        Vector3 toHead = head.position - pos;
        if (toHead.sqrMagnitude < 0.0001f) toHead = Vector3.back;
        // Quad の表（-Z 側から見える面）を顔へ向ける
        gauge.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-toHead.normalized, Vector3.up));
        gauge.transform.localScale = Vector3.one * gaugeSize;
        if (!gauge.enabled) gauge.enabled = true;
        if (Mathf.Abs(f - _gaugeShown) > 0.002f) { _gaugeShown = f; _gaugeMat.SetFloat("_Fill", f); }
    }

    private float _nextFollow;
    private bool _summoning;
    private float _summonUntil;
    private Vector3 _returnPos;
    private Quaternion _returnRot;
    private float _stickY;
    private float _downSince = -1f;
    private bool _waitRelease;   // 呼び出したあと、一度スティックを戻すまで次の呼び出しをしない

    public override void InputLookVertical(float value, UdonInputEventArgs args)
    {
        _stickY = value;
    }

    private void Update()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || !p.IsValid() || manager == null || summonSpot == null) return;
        FuwaBlower gun = manager.GetMyBlower();
        if (gun == null) return;
        if (decoy != null && decoy.activeSelf) decoy.SetActive(false);
        if (gun.IsHeld())
        {
            _summoning = false;
            _dockFollow = 0;
            ShowGauge(p, -1f);   // 銃を持っている時は出さない
            return;
        }
        if (!_summoning) FollowDocked(p, gun);

        VRCPlayerApi.TrackingData head = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        if (!p.IsUserInVR())
        {
            ShowGauge(p, -1f);
            if (!Input.GetKeyDown(desktopKey)) return;
            Vector3 fwd = head.rotation * Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            summonSpot.SetPositionAndRotation(head.position + fwd * desktopDistance + Vector3.down * desktopDrop, Quaternion.LookRotation(fwd, Vector3.up));
            gun.MoveToStand(summonSpot);
            return;
        }

        // VR：右スティックの下入力を見張る
        bool down = _stickY <= stickThreshold;
        if (!down) { _downSince = -1f; _waitRelease = false; }
        else if (_downSince < 0f) _downSince = Time.time;
        // 手元の丸いゲージ：倒している間たまっていく。呼び出した瞬間は満タンで少しだけ残す
        if (down && !_waitRelease && !_summoning && _downSince >= 0f) ShowGauge(p, Mathf.Clamp01((Time.time - _downSince) / Mathf.Max(0.01f, stickHoldSeconds)));
        else if (Time.time < _gaugeFlashUntil) ShowGauge(p, 1f);
        else ShowGauge(p, -1f);
        if (down && !_waitRelease && !_summoning && Time.time - _downSince >= stickHoldSeconds)
        {
            _gaugeFlashUntil = Time.time + 0.25f;
            _waitRelease = true;
            _summoning = true;
            _summonUntil = Time.time + waitSeconds;
            _returnDock = gun.GetDock();   // 台に乗っていたら、時間切れの時は台に戻す
            gun.Undock();
            _dockFollow = 0;
            _returnPos = gun.transform.position;
            _returnRot = gun.transform.rotation;
            gun.SetSummonHidden(false);
            _nextFollow = 0f;
        }
        if (!_summoning) return;

        // 握らないまま時間切れ：元の場所に戻す
        if (Time.time > _summonUntil)
        {
            _summoning = false;
            if (_returnDock != null) { gun.Dock(_returnDock); return; }
            summonSpot.SetPositionAndRotation(_returnPos, _returnRot);
            gun.MoveToStand(summonSpot);
            return;
        }

        if (Time.time < _nextFollow) return;
        _nextFollow = Time.time + followInterval;
        SetGripPose(p, false);
        gun.MoveToStand(summonSpot);
    }

    // summonSpot を、手で握った時の銃の位置・向きにする。
    // 手の向きのままだと銃口が親指の方（上）を向くので、銃口を手の先・グリップを小指側へ回す。
    // 左手は右手の握り方を左右反転して（手の左右の軸＝X で鏡に映す）、leftExtraEuler を足す
    private void SetGripPose(VRCPlayerApi p, bool left)
    {
        VRCPlayerApi.TrackingData hand = p.GetTrackingData(left ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand);
        Quaternion g = Quaternion.Euler(gripEuler) * Quaternion.Euler(gripTilt);
        Vector3 off = gripOffset;
        if (left)
        {
            g = new Quaternion(g.x, -g.y, -g.z, g.w) * Quaternion.Euler(leftExtraEuler);
            off.x = -off.x;
        }
        Quaternion rot = hand.rotation * g;
        summonSpot.SetPositionAndRotation(hand.position + rot * off, rot);
    }

    [Header("台に乗っている銃（VR）")]
    [Tooltip("台の上の銃にこの距離(m)まで手を近づけると、本物の銃が握る位置で手についてくる（銃の大きさに合わせて伸び縮み）")]
    public float dockReach = 0.35f;
    [Tooltip("手が離れてから台に戻すまでの距離（少し広めにして、境目でちらつかないように）")]
    public float dockRelease = 0.5f;
    private Transform _returnDock;
    private int _dockFollow;   // 0=台の上、1=右手、2=左手

    // 台に乗っている間：VRなら近い方の手に本物の銃（見えない）をついてこさせる。手が近くにない時は台の上に置いて、掴めないようにする
    private void FollowDocked(VRCPlayerApi p, FuwaBlower gun)
    {
        Transform dock = gun.GetDock();
        if (dock == null) { _dockFollow = 0; return; }
        if (!p.IsUserInVR()) { gun.SetDockGrabbable(true); return; }   // デスクトップはクリックで取る（向きは持った時に視線へ合わせる）
        if (Time.time < _nextFollow) return;
        _nextFollow = Time.time + followInterval;
        Vector3 c = gun.GetDockPoint();
        float s = gun.GetAutoScale();
        float dr = (p.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position - c).magnitude;
        float dl = (p.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position - c).magnitude;
        int want = 0;
        if (_dockFollow == 1 && dr < dockRelease * s) want = 1;
        else if (_dockFollow == 2 && dl < dockRelease * s) want = 2;
        else if (dr < dockReach * s || dl < dockReach * s) want = dr <= dl ? 1 : 2;
        if (want == 0)
        {
            if (_dockFollow != 0) gun.MoveDocked(c, dock.rotation);
            _dockFollow = 0;
            gun.SetDockGrabbable(false);
            return;
        }
        _dockFollow = want;
        SetGripPose(p, want == 2);
        gun.MoveDocked(summonSpot.position, summonSpot.rotation);
        gun.SetDockGrabbable(true);
    }
}
