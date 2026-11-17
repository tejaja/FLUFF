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
// 2026-11-17 てじゃ「向きは上固定、下の部分を丸くして底を手にとる」「竹とんぼは上昇下降のみ、移動はスティックで」→ 案C：
// ・軸の下のにぎり玉の中心を持つ（root の原点＝玉の中心）。見た目（visual）は毎フレーム玉の中心から真上に立て直す（水平の向きだけ手に合わせる）
// ・起き上がりこぼしみたいにゆらゆら：動いている方向へ少し前のめり、手を急に動かすと上が遅れてついてくる。ばねで真上に戻る
// ・トリガー中は真上に上昇＋スティックで横移動。竹とんぼの傾きは飛ぶ向きに関係しない
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaBambooCopter : UdonSharpBehaviour
{
    public FuwaRespawnManager manager;
    [Tooltip("回る羽（local の +Y が軸）")]
    public Transform blades;
    [Tooltip("見た目（玉・軸・羽）。原点＝にぎり玉の中心。毎フレーム真上に立て直す")]
    public Transform visual;
    [Header("ゆらゆら")]
    [Tooltip("動く速さ 1m/s あたりの前のめり（rad）")]
    public float leanPerSpeed = 0.035f;
    [Tooltip("手（玉）の加速で上が遅れる強さ")]
    public float lagGain = 0.012f;
    [Tooltip("ばねの強さ・減衰")]
    public float spring = 70f, damping = 7f;
    [Tooltip("いちばん傾く角度（rad）")]
    public float maxTilt = 0.33f;
    [Header("飛び方")]
    [Tooltip("トリガー中にプロペラの向きへ進む速さ(m/s)")]
    public float speed = 7f;
    [Tooltip("持っていて空中、トリガーを押していない時（ホバリング）に降りる速さ(m/s)")]
    public float hoverFall = 0.6f;   // 2026-11-08 てじゃ「あとちょっぴり速くても」0.2→0.35、2026-11-11「落下速度もうちょいアップ」→0.6
    [Tooltip("ホバリング中の移動の速さ（走る速さに対する倍率）")]
    public float moveScale = 1f;
    [Tooltip("VR で持っている時の横の速さの倍率（2026-11-17 てじゃ「持ってるとちょっぴり最高速度アップ」。デスクトップは Shift の時だけ shiftBoost、押さなければふつう）")]
    public float vrBoost = 1.2f;
    [Tooltip("デスクトップで Shift を押している間の横の速さの倍率（てじゃ「シフト押しながらで最高速度アップ」）")]
    public float shiftBoost = 1.6f;
    [Tooltip("トリガーを離してからホバリングになるまでの秒数")]
    public float brakeTime = 0.18f;
    // 2026-11-17 てじゃ「自然下降なし、VRでは右スティックの上下で上昇下降、ジャンプでも上昇。デスクトップはスペースで上昇、ctrlで下降。下降時はプロペラが逆回転」
    [Tooltip("下降の速さ(m/s)")]
    public float descendSpeed = 5f;
    [Tooltip("上下の速さが変わる早さ(m/s²)")]
    public float verticalAccel = 22f;
    [Tooltip("VR の右スティックの上下の遊び")]
    public float stickDead = 0.3f;
    private float _lookV, _vy;
    private bool _jumpHeld;
    [Header("羽")]
    public float spinUse = 1500f, spinIdle = 120f, spinAccel = 2400f;   // spinIdle：いつものゆっくり回転（度/秒）

    private bool _mine, _using, _braking;
    private float _gravity = 1f, _brakeStart, _liftUntil, _spin;
    private Vector3 _brakeFrom, _homePos;
    private Quaternion _homeRot;
    private float _moveH, _moveV;
    private bool _hovering;
    private VRCPickup _pickup;
    // 2026-11-16 てじゃ「ステージ１に放置した竹とんぼの影がステージ２までとどいてた」：ステージ1の真下にステージ2があるので、
    //  影は自分から近い時（shadowNear m 以内）だけ落とす
    [Tooltip("自分（カメラ）からこの距離(m)より遠いと影を落とさない")]
    public float shadowNear = 40f;
    [Tooltip("影を落とすか（2026-11-17 なしに）")]
    public bool castShadows = false;
    private Renderer[] _rends;
    private bool _shadowOn = true;
    private float _shadowCheckAt;
    // ゆらゆら：傾き（上向きからのずれ、水平の x・z）とその速さ。玉の位置と速さの前の値
    private Vector3 _tilt, _tiltVel, _prevPos, _prevVel;
    private bool _hasPrev;

    private void Start()
    {
        _pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        _homePos = transform.position; _homeRot = transform.rotation;
        _rends = GetComponentsInChildren<Renderer>(true);
    }

    private void UpdateShadow()
    {
        if (Time.time < _shadowCheckAt) return;
        _shadowCheckAt = Time.time + 0.5f;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null || _rends == null) return;
        bool on = (lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position - transform.position).sqrMagnitude < shadowNear * shadowNear;
        if (!castShadows) on = false;   // 2026-11-17 てじゃ「竹とんぼ（の影）いらんとおもう」
        if (on == _shadowOn) return;
        _shadowOn = on;
        for (int i = 0; i < _rends.Length; i++) if (_rends[i] != null) _rends[i].shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // 竹とんぼ立てをクリックした（FuwaCopterStand）：持っていたら手放して、立てに戻す
    public void ReturnHome()
    {
        if (_mine && _pickup != null) _pickup.Drop();
        transform.SetPositionAndRotation(_homePos, _homeRot);
    }

    public override void InputMoveHorizontal(float value, UdonInputEventArgs args) { _moveH = value; }
    public override void InputMoveVertical(float value, UdonInputEventArgs args) { _moveV = value; }
    public override void InputLookVertical(float value, UdonInputEventArgs args) { _lookV = value; }
    public override void InputJump(bool value, UdonInputEventArgs args) { _jumpHeld = value; }

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
        _using = true;   // 上昇の入力として Update で使う
    }

    public override void OnPickupUseUp()
    {
        if (!_mine) return;
        _using = false;
    }

    private void EndFlight()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p != null && (_using || _braking || _hovering)) p.SetGravityStrength(_gravity);
        _using = false; _braking = false; _hovering = false;
    }

    private void Update()
    {
        UpdateShadow();
        // 上下の入力：+1 上昇、−1 下降（トリガーも上昇）
        float vin = 0f;
        VRCPlayerApi lp0 = _mine ? Networking.LocalPlayer : null;
        if (lp0 != null)
        {
            if (lp0.IsUserInVR())
            {
                float a = Mathf.Abs(_lookV);
                if (a > stickDead) vin = Mathf.Sign(_lookV) * Mathf.InverseLerp(stickDead, 1f, a);
                if (_jumpHeld) vin = 1f;
            }
            else
            {
                if (Input.GetKey(KeyCode.Space)) vin = 1f;
                else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) vin = -1f;
            }
            if (_using) vin = 1f;
        }
        // 羽：持っている間はゆっくり、上昇中は速く、下降中は逆回転
        float want = !_mine ? 0f : (vin > 0.01f ? spinUse : (vin < -0.01f ? -spinUse * 0.7f : spinIdle));
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
        bool grounded = p.IsPlayerGrounded();
        if (vin > 0.01f || !grounded)
        {
            // 空中（か上昇の入力）：重力を切って、自然には下がらない。上下は入力どおり、横はふつうの移動
            if (!_hovering) { _hovering = true; p.SetGravityStrength(0f); _vy = Mathf.Max(0f, p.GetVelocity().y); }
            float target = vin > 0f ? vin * speed : vin * descendSpeed;
            _vy = Mathf.MoveTowards(_vy, target, verticalAccel * Time.deltaTime);
            Vector3 v = Hover(p); v.y = _vy;
            p.SetVelocity(v);
        }
        else if (_hovering) { _hovering = false; _vy = 0f; p.SetGravityStrength(_gravity); }   // 地面に着いた
    }

    // 見た目を真上に立て直す＋ゆらゆら。手（pickup）の位置が決まった後に動かす
    public override void PostLateUpdate()
    {
        if (visual == null) return;
        float dt = Time.deltaTime;
        Vector3 pos = transform.position;
        Vector3 vel = Vector3.zero, acc = Vector3.zero;
        if (_hasPrev && dt > 1e-4f)
        {
            vel = (pos - _prevPos) / dt;
            acc = (vel - _prevVel) / dt;
            if (acc.sqrMagnitude > 400f * 400f) acc = acc.normalized * 400f;   // ワープ・持ちかえの一瞬で飛ばないように
        }
        if (vel.sqrMagnitude > 30f * 30f) { vel = Vector3.zero; acc = Vector3.zero; }   // ワープした
        _prevPos = pos; _prevVel = vel; _hasPrev = true;
        vel.y = 0f; acc.y = 0f;
        // 目標：動く方向へ前のめり。手の加速には上が遅れる（加速と逆向き）
        Vector3 target = vel * leanPerSpeed;
        if (target.magnitude > maxTilt) target = target.normalized * maxTilt;
        Vector3 force = (target - _tilt) * spring - _tiltVel * damping - acc * lagGain;
        if (dt > 0f && dt < 0.1f)
        {
            _tiltVel += force * dt;
            _tilt += _tiltVel * dt;
        }
        if (_tilt.magnitude > maxTilt) { _tilt = _tilt.normalized * maxTilt; _tiltVel *= 0.5f; }
        // 水平の向きは手（root）に合わせる
        Vector3 f = transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 1e-4f) { f = transform.up; f.y = 0f; }
        if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
        Quaternion yaw = Quaternion.LookRotation(f.normalized, Vector3.up);
        Vector3 up = (Vector3.up + _tilt).normalized;
        visual.SetPositionAndRotation(pos, Quaternion.FromToRotation(Vector3.up, up) * yaw);
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
        float boost = p.IsUserInVR() ? vrBoost : ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? shiftBoost : 1f);
        Vector3 v = mv * p.GetRunSpeed() * moveScale * boost;
        v.y = 0f;   // 2026-11-17 自然下降なし（上下は Update で）
        return v;
    }
}
