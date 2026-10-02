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
            return;
        }

        VRCPlayerApi.TrackingData head = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        if (!p.IsUserInVR())
        {
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
        if (down && !_waitRelease && !_summoning && Time.time - _downSince >= stickHoldSeconds)
        {
            _waitRelease = true;
            _summoning = true;
            _summonUntil = Time.time + waitSeconds;
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
            summonSpot.SetPositionAndRotation(_returnPos, _returnRot);
            gun.MoveToStand(summonSpot);
            return;
        }

        if (Time.time < _nextFollow) return;
        _nextFollow = Time.time + followInterval;
        // 手の向きのままだと銃口が親指の方（上）を向くので、銃口を手の先・グリップを小指側へ回す
        VRCPlayerApi.TrackingData hand = p.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
        Quaternion rot = hand.rotation * Quaternion.Euler(gripEuler) * Quaternion.Euler(gripTilt);
        summonSpot.SetPositionAndRotation(hand.position + rot * gripOffset, rot);
        gun.MoveToStand(summonSpot);
    }
}
