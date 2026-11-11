using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

// 自分のエアガンをどこからでも呼び出す（自分だけ）。
// ・デスクトップ：Fキーで目の前に出てくる（あとはいつも通りクリックで持つ）
// ・VR：右スティックを下にしばらく倒すと、目の前に台の上と同じ浮いて回る銃が出る（見えない台 vrFrontSpot に乗せる）。
//   手を近づけると近い方の手に握る向きでついてくるので、そのまま握れば持てる（台から取る時と同じ FollowDocked）。
//   （2026-11-11 前は右手の中に出して、握らなければ waitSeconds 秒で戻していた。左利きの人のために目の前へ。自動で戻すのはなし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGunSummon : UdonSharpBehaviour
{
    public FuwaRespawnManager manager;
    [Tooltip("デスクトップで呼び出すキー")]
    public KeyCode desktopKey = KeyCode.F;   // 2026-10-14 E→F（EはVRChatのカメラの上下移動とかぶる。てじゃ）
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
        VRCPlayerApi.TrackingData head = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 pos = FrontPos(p);   // 銃が出てくる所（2026-11-11 右手の上→目の前）
        Vector3 toHead = head.position - pos;
        if (toHead.sqrMagnitude < 0.0001f) toHead = Vector3.back;
        // Quad の表（-Z 側から見える面）を顔へ向ける
        gauge.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-toHead.normalized, Vector3.up));
        gauge.transform.localScale = new Vector3(-gaugeSize, gaugeSize, gaugeSize);   // 左右反転：見る人から見て上から時計回りに満ちる（ポータルのゲージと同じ）
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

    // 掴むボタン（トリガー／グリップ）を押した手：台の銃がもう一方の手についていたら、すぐこの手へ乗りかえる
    // （VRChat が掴む前にここが呼ばれれば、押した手の向きで持てる。後だった時のために、手の動きでも選んでいる）
    public override void InputUse(bool value, UdonInputEventArgs args) { if (value) OnGrabPress(args.handType == HandType.LEFT ? 2 : 1); }
    public override void InputGrab(bool value, UdonInputEventArgs args) { if (value) OnGrabPress(args.handType == HandType.LEFT ? 2 : 1); }

    private void OnGrabPress(int hand)
    {
        _pressedHand = hand;
        _pressedUntil = Time.time + 0.3f;
        if (_dockFollow == 0 || _dockFollow == hand) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || !p.IsValid() || !p.IsUserInVR() || manager == null || summonSpot == null) return;
        FuwaBlower gun = manager.GetMyBlower();
        if (gun == null || gun.IsHeld() || gun.GetDock() == null) return;
        Vector3 c = gun.GetDockPoint();
        float d = (p.GetTrackingData(hand == 2 ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand).position - c).magnitude;
        if (d > dockRelease * gun.GetAutoScale()) return;
        _dockFollow = hand;
        SetGripPose(p, hand == 2);
        gun.MoveDocked(summonSpot.position, summonSpot.rotation);
    }

    // デスクトップ・スマホ：目の前に銃を出す（Fキー、スマホの画面のボタン FuwaMobileGunButton から）
    public void SummonInFront()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || !p.IsValid() || manager == null || summonSpot == null) return;
        FuwaBlower gun = manager.GetMyBlower();
        if (gun == null || gun.IsHeld()) return;
        VRCPlayerApi.TrackingData head = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 fwd = head.rotation * Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();
        summonSpot.SetPositionAndRotation(head.position + fwd * desktopDistance + Vector3.down * desktopDrop, Quaternion.LookRotation(fwd, Vector3.up));
        gun.MoveToStand(summonSpot);
        gun.PlaySpawnSound();
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
            if (Input.GetKeyDown(desktopKey)) SummonInFront();
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
        if (down && !_waitRelease && !_summoning && Time.time - _downSince >= stickHoldSeconds && vrFrontSpot != null)
        {
            // 2026-11-11 てじゃ「目の前召喚のほうがいいかも、左利きの人もおるし」「握った時の向きは今のまま」「自動で戻るのはいらん」
            //  → 目の前に台と同じ浮いて回る銃を出す（見えない台＝vrFrontSpot に乗せる）。手を近づけると近い方の手に握る向きでついてくる（台と同じ FollowDocked）。時間切れで戻すのはなし
            _gaugeFlashUntil = Time.time + 0.25f;
            _waitRelease = true;
            vrFrontSpot.SetPositionAndRotation(FrontPos(p), Quaternion.LookRotation(FlatForward(p), Vector3.up));
            gun.Dock(vrFrontSpot);
            _dockFollow = 0;
            gun.PlaySpawnSound();
            return;
        }
    }

    [Header("VR：目の前に出す")]
    [Tooltip("VRで呼び出した時に銃を乗せる見えない台（空のオブジェクト。Udonでは新しく作れないので用意しておく）")]
    public Transform vrFrontSpot;
    [Tooltip("VR：目の前のどれくらい先に出すか(m)")]
    public float vrDistance = 0.45f;
    [Tooltip("VR：目線からどれくらい下に出すか(m)")]
    public float vrDrop = 0.3f;

    private Vector3 FlatForward(VRCPlayerApi p)
    {
        Vector3 fwd = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        return fwd.normalized;
    }

    // 目の前の出す位置（アバターの大きさで伸び縮み）
    private Vector3 FrontPos(VRCPlayerApi p)
    {
        float s = 1f;
        FuwaBlower gun = manager != null ? manager.GetMyBlower() : null;
        if (gun != null) s = gun.GetAutoScale();
        return p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position + FlatForward(p) * (vrDistance * s) + Vector3.down * (vrDrop * s);
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
    [Tooltip("両手とも近い時、反対の手がこれ(m)以上近くなったら乗りかえる")]
    public float dockSwitchMargin = 0.05f;
    [Tooltip("手についてきている間の、掴む箱の大きさ（銃の大きさ基準。呼び出しの時の summonGrabSize より小さく）")]
    public float dockFollowGrabSize = 0.2f;
    [Tooltip("手の近づく速さから、何秒先の距離で比べるか")]
    public float dockLookAhead = 0.2f;
    private float _lastDr, _lastDl, _lastDistAt, _approachR, _approachL;
    private int _pressedHand;      // 1=右 2=左
    private float _pressedUntil;
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
        // 台へ近づいている速さ（なめらかに）。置いているだけの手より、伸ばしている手を優先する
        float dt = Mathf.Max(0.001f, Time.time - _lastDistAt);
        if (_lastDistAt > 0f && dt < 0.5f)
        {
            float k = 1f - Mathf.Exp(-dt / 0.08f);
            _approachR = Mathf.Lerp(_approachR, (_lastDr - dr) / dt, k);
            _approachL = Mathf.Lerp(_approachL, (_lastDl - dl) / dt, k);
        }
        else { _approachR = 0f; _approachL = 0f; }
        _lastDr = dr; _lastDl = dl; _lastDistAt = Time.time;
        // 少し先の距離（近づいている手ほど小さい）で比べる
        float pr = dr - Mathf.Max(0f, _approachR) * dockLookAhead, pl = dl - Mathf.Max(0f, _approachL) * dockLookAhead;
        // 掴むボタンを押した手（InputUse / InputGrab）は、少しの間それを優先
        if (Time.time < _pressedUntil) { if (_pressedHand == 1) pr -= 1f; else if (_pressedHand == 2) pl -= 1f; }
        // 両手とも近い時は、今ついている手より反対の手の方がはっきり近くなったら乗りかえる
        // （前は一度ついた手から離れるまで乗りかえなかったので、左手が台の近くにあると、右手で掴んでも左手用の向きで持っていた。2026-10-14 てじゃ）
        bool rIn = dr < (_dockFollow == 1 ? dockRelease : dockReach) * s;
        bool lIn = dl < (_dockFollow == 2 ? dockRelease : dockReach) * s;
        int want = 0;
        if (rIn && lIn)
        {
            want = _dockFollow != 0 ? _dockFollow : (pr <= pl ? 1 : 2);
            if (want == 1 && pl + dockSwitchMargin * s < pr) want = 2;
            else if (want == 2 && pr + dockSwitchMargin * s < pl) want = 1;
        }
        else if (rIn) want = 1;
        else if (lIn) want = 2;
        if (want == 0)
        {
            if (_dockFollow != 0) gun.MoveDocked(c, dock.rotation);
            _dockFollow = 0;
            gun.SetDockGrabbable(false);
            gun.SetGrabBoxSize(-1f);
            return;
        }
        _dockFollow = want;
        SetGripPose(p, want == 2);
        gun.MoveDocked(summonSpot.position, summonSpot.rotation);
        gun.SetDockGrabbable(true);
        // ついている手のまわりだけで掴めるように、掴む箱を小さく（反対の手で掴むと、ついている手用の向きで持ってしまうので）
        gun.SetGrabBoxSize(dockFollowGrabSize);
    }
}
