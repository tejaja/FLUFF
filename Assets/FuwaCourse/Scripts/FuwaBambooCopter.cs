using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon.Common;

// 見学用の竹とんぼ（2026-11-08 てじゃ「傘を指すのはなんかちがう」「竹とんぼで良い気がしてきた」→ 傘の代わり）
// ・ロビーの竹とんぼ立てに1本ずつ。自分の画面だけの物（同期なし）：みんなそれぞれ取れて、ほかの人には見えない
// ・どの向きでも持てる（PickupOrientation.Any。デスクトップは持ち物を回すキーで向きを変えられる）。立てから取るとまっすぐ上向きで手に来る
// ・使う（トリガー）を押している間、手の所で羽が回る
//   2026-11-08 てじゃ「トリガーでプロペラの向きに移動、つまりデフォだとトリガーorクリックで真上に上昇」→ プロペラの向き（transform.up）へ speed
// ・持っていて空中でトリガーを押していない間：ホバリング（かなりゆっくり降りる hoverFall）＋ふつうの移動（WASD・スティック）
// ・トリガーを離すと素早くホバリングの速さに（brakeTime）
// ・手放してもその場に残る（2026-11-08 てじゃ「離しても自動で戻らなくする　元の台座をクリックで戻す」→ FuwaCopterStand）。エアガンを持ったら手放す
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaBambooCopter : UdonSharpBehaviour
{
    public FuwaRespawnManager manager;
    [Tooltip("回る羽（local の +Y が軸）")]
    public Transform blades;
    [Header("飛び方")]
    [Tooltip("トリガー中にプロペラの向きへ進む速さ(m/s)")]
    public float speed = 7f;
    [Tooltip("持っていて空中、トリガーを押していない時（ホバリング）に降りる速さ(m/s)")]
    public float hoverFall = 0.35f;   // 2026-11-08 てじゃ「あとちょっぴり速くても」0.2→0.35
    [Tooltip("ホバリング中の移動の速さ（走る速さに対する倍率）")]
    public float moveScale = 1f;
    [Tooltip("トリガーを離してからホバリングになるまでの秒数")]
    public float brakeTime = 0.18f;
    [Header("羽")]
    public float spinUse = 1500f, spinIdle = 0f, spinAccel = 2400f;

    private bool _mine, _using, _braking;
    private float _gravity = 1f, _brakeStart, _liftUntil, _spin;
    private Vector3 _brakeFrom, _homePos;
    private Quaternion _homeRot;
    private float _moveH, _moveV;
    private bool _hovering;
    private VRCPickup _pickup;

    private void Start()
    {
        _pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        _homePos = transform.position; _homeRot = transform.rotation;
    }

    // 竹とんぼ立てをクリックした（FuwaCopterStand）：持っていたら手放して、立てに戻す
    public void ReturnHome()
    {
        if (_mine && _pickup != null) _pickup.Drop();
        transform.SetPositionAndRotation(_homePos, _homeRot);
    }

    public override void InputMoveHorizontal(float value, UdonInputEventArgs args) { _moveH = value; }
    public override void InputMoveVertical(float value, UdonInputEventArgs args) { _moveV = value; }

    public override void OnPickup()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        _mine = true; _using = false; _braking = false;
        _gravity = p.GetGravityStrength();
    }

    public override void OnDrop()
    {
        if (!_mine) return;
        EndFlight();
        _mine = false;
    }

    public override void OnPickupUseDown()
    {
        if (!_mine) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        _using = true; _braking = false;
        p.SetGravityStrength(0f); _hovering = true;   // _hovering＝重力を切っている（地面についたら戻す）
        if (p.IsPlayerGrounded()) _liftUntil = Time.time + 0.25f;   // 地面に立っていると横の速さが消されるので、まず少し浮かせる
    }

    public override void OnPickupUseUp()
    {
        if (!_mine) return;
        _using = false;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        _braking = true; _brakeStart = Time.time; _brakeFrom = p.GetVelocity();
    }

    private void EndFlight()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p != null && (_using || _braking || _hovering)) p.SetGravityStrength(_gravity);
        _using = false; _braking = false; _hovering = false;
    }

    private void Update()
    {
        // 羽
        float want = _using ? spinUse : (_mine ? spinIdle : 0f);
        _spin = Mathf.MoveTowards(_spin, want, spinAccel * Time.deltaTime);
        if (blades != null && _spin != 0f) blades.Rotate(0f, _spin * Time.deltaTime, 0f, Space.Self);

        if (!_mine) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        // エアガンを持ったら手放す
        if (manager != null)
        {
            FuwaBlower gun = manager.GetMyBlower();
            if (gun != null && gun.IsHeld()) { if (_pickup != null) _pickup.Drop(); return; }
        }
        if (_using)
        {
            // トリガー中：プロペラの向きへまっすぐ（ふつうに持てば真上）
            Vector3 v = transform.up * speed;
            if (Time.time < _liftUntil) v.y = Mathf.Max(v.y, 2f);
            p.SetVelocity(v);
        }
        else if (_braking)
        {
            float t = (Time.time - _brakeStart) / Mathf.Max(0.01f, brakeTime);
            if (t >= 1f) _braking = false;
            else { float e = 1f - t; p.SetVelocity(_brakeFrom * (e * e) + Hover(p) * (1f - e * e)); }
        }
        else if (!p.IsPlayerGrounded() && (_hovering || p.GetVelocity().y < 0f))
        {
            // 持っていて空中：ホバリング（かなりゆっくり降りる）＋ふつうの移動。ジャンプは上がりきってから（落ちはじめたら）
            if (!_hovering) { _hovering = true; p.SetGravityStrength(0f); }
            p.SetVelocity(Hover(p));
        }
        else if (_hovering && p.IsPlayerGrounded()) { _hovering = false; p.SetGravityStrength(_gravity); }
    }

    // ホバリングの速さ：見ている向き（水平）で移動の入力どおり、下へ hoverFall
    private Vector3 Hover(VRCPlayerApi p)
    {
        Vector3 f = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward; f.y = 0f;
        if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
        f.Normalize();
        Vector3 r = new Vector3(f.z, 0f, -f.x);
        Vector3 mv = f * _moveV + r * _moveH;
        if (mv.sqrMagnitude > 1f) mv.Normalize();
        Vector3 v = mv * p.GetRunSpeed() * moveScale;
        v.y = -hoverFall;
        return v;
    }
}
