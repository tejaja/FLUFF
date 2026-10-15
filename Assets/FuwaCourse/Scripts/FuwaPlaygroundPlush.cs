using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

// あそびばのまもののぬいぐるみ（コース4 まもののす を誰かがクリアすると出てくる）。
// ふわふわ（FuwaSharedBall）がぶつかると、ぬいぐるみがぽよんとつぶれて、ふわふわを跳ね返す（それぞれの画面で同じように Nudge）。
// 持って好きな所に置ける。当たり判定はトリガー（持つため）で、跳ね返しはこのスクリプト。
// 2026-10-13 てじゃ「ペットみたいに追いかける。ただ止めれるように」：
// ・最初は寝ている（目を閉じて鼻ちょうちん）。持ってトリガー（撃つボタン）で起きる⇄寝るを切り替え。持っている間も寝ている見た目のまま
// ・起きていると、島の中でふわふわを見つけて、ぷるぷる溜めてからぴょんと体当たり（割らない、押すだけ）。着地した所が新しい居場所（戻って歩かない）
// ・追いかける相手がいないまま sleepAfter 秒たったら勝手に寝る
// ・エアガンで撃つと押し返せる（FuwaBlower → Blown）。島の外へ落ちたら元の場所へ戻って寝る
// ・イバラ（bramble）に押し込まれたら、パチンと倒れて少ししたら元の場所で寝て復活（今のあそびばにはイバラはない）
// 同期（2026-10-14）：位置は VRCObjectSync ではなく「動きの区切り」を送って、みんなの画面で同じ動きを計算する。
// 決めるのは持ち主の画面（追いかける・跳ぶ・着地）。撃った人は持ち主を待たずに自分の画面ですぐ吹っ飛ばし（先読み）、同じ区切りを持ち主に送って配ってもらう
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class FuwaPlaygroundPlush : UdonSharpBehaviour
{
    public FuwaPlaygroundSpawner spawner;
    public Transform visual;
    public float radius = 0.4f;
    public float bounce = 1.25f;
    public float minOut = 1.6f;
    public AudioSource boing;

    [Header("寝ている見た目（コース4の寝ているまものと同じ）")]
    public Transform eyes;
    public Transform sleepFx;

    [Header("追いかける")]
    [Tooltip("地面から体の中心（このオブジェクトの位置）までの高さ")]
    public float standUp = 0.2f;
    [Tooltip("体（毛の芯）の中心から下はしまで(m)。つぶれた時に足元が浮かないよう、見た目をこの分だけ下げる")]
    public float bodyBottom = 0.175f;
    public float detectRange = 4.5f;
    [Tooltip("置いた場所からこれ以上離れたふわふわは追わない（areaRadius が 0 の時）")]
    public float leashRange = 6f;
    [Tooltip("動き回れる範囲（島）。areaRadius > 0 なら、置いた場所ではなくこの円の中なら追いかける（2026-10-13 てじゃ「ふわふわ飛ばしながら追いかけると感知範囲超えちゃって戻っちゃう」）")]
    public Vector3 areaCenter;
    public float areaRadius = 0f;
    [Tooltip("一度追いかけ始めたふわふわは、この距離までは見失わない（見つけるのは detectRange）")]
    public float keepRange = 9f;
    public float windupTime = 0.6f;
    public float dashSpeed = 4f;
    public float dashTime = 0.55f;
    public float hopHeight = 0.45f;
    public float cooldown = 1.1f;
    public float returnSpeed = 1.2f;
    [Tooltip("体当たり中にふわふわへ足す押しの強さ（体の速さにかける倍率）")]
    public float dashPush = 1f;
    [Tooltip("追いかける相手がいないまま、この秒数で寝る")]
    public float sleepAfter = 30f;
    [Tooltip("ふわふわは、動ける範囲（areaRadius）よりこれだけ外まで追う（柵ぎわのふわふわも追えるように。体は areaRadius で止まる）")]
    public float targetMargin = 0.6f;
    [Tooltip("追いかけている途中でも、今の相手よりこの倍率より近いふわふわが来たら乗りかえる（0.6＝今の相手の6割の距離より近ければ）")]
    [Range(0.1f, 1f)] public float switchRatio = 0.6f;

    [Header("起きていて相手がいない時：ときどきぴょんと跳ねる（2026-10-15 てじゃ「柵付近くると静止しちゃうの寂しい」「ランダムで跳ねるのもあり」）")]
    public float wanderMin = 1.5f;
    public float wanderMax = 4f;
    public float wanderLenMin = 0.6f;
    public float wanderLenMax = 1.5f;
    [Tooltip("跳ねる高さ（hopHeight にかける）")]
    public float wanderHop = 0.6f;
    private float _nextWander;

    [Header("撃たれた時・落ちた時")]
    public float pushScale = 1.2f;
    public float gravity = 12f;
    public LayerMask ground = 1 << 11;
    public float maxDrop = 6f;
    [Tooltip("ここより下へ落ちたら元の場所へ戻す（ワールドの高さ）")]
    public float fallY = 80f;

    [Header("イバラ（押し込まれたら倒れる）")]
    public Transform bramble;
    [Tooltip("イバラの判定の半径(m)（水平）")]
    public float brambleRadius = 0.6f;
    public float reviveDelay = 3f;
    public ParticleSystem popEffect;
    public AudioSource popSound;

    [UdonSynced] private bool _awake;
    [UdonSynced] private bool _windup;
    [UdonSynced] private bool _down;
    // 動きの区切り。kind 0＝その場に立つ 2＝体当たり（ぴょん） 4＝吹っ飛び（重力つき）。時刻はサーバー時刻
    [UdonSynced] private int _sKind;
    [UdonSynced] private Vector3 _sFrom;
    [UdonSynced] private Vector3 _sVec;     // 体当たり：向き（水平・長さ1）／吹っ飛び：最初の速さ
    [UdonSynced] private float _sLen;
    [UdonSynced] private float _sHop;
    [UdonSynced] private float _sDur;
    [UdonSynced] private double _sAt;
    [UdonSynced(UdonSyncMode.Smooth)] private Quaternion _sRot = Quaternion.identity;
    // 持たれている間：持っている人（＝持ち主）が位置を送る
    [UdonSynced] private bool _held;
    [UdonSynced(UdonSyncMode.Smooth)] private Vector3 _sHoldPos;

    // 今使っている区切り（届いた区切り、または自分が撃った時の先読み）
    private int _aKind;
    private Vector3 _aFrom, _aVec;
    private float _aLen, _aHop, _aDur;
    private double _aAt;
    // 届くのが遅れた区切り（体当たり・吹っ飛び）は、その分だけ遅らせて最初から動かす（途中が抜けて「跳ねたのが省略」に見えないように）
    private float _aShift;
    private bool _pendingSeg;     // 今の動きが終わるまで、届いた「立つ」区切りを待たせている
    private float _predUntil;        // 自分が撃った先読みを、届いた区切りより優先する期限
    private double _predAt;
    // 吹っ飛びの計算（毎回はじめからではなく、続きから細かい刻みで進める。みんなの画面で同じ刻み）
    private Vector3 _flyPos, _flyVel;
    private float _flyT;
    private bool _flyLanded;
    private double _flyFor = -1.0;
    private double _landSquashFor = -1.0;
    private bool _dashNoGround;
    private Vector3 _spawnPos;
    private Quaternion _spawnRot;
    private const float FlyStep = 0.02f;
    [Tooltip("体当たりの区切りを、跳ぶ何秒前に配るか（通信の遅れの分）")]
    public float dashLead = 0.35f;
    [Tooltip("区切りが変わった時の位置のずれを寄せる速さ（大きいほど早く寄る）")]
    public float errDecay = 10f;
    private Vector3 _err;
    private double _errSeg = -1.0;

    private Vector3 _baseScale = Vector3.one;
    private Vector3 _basePos;
    private Vector3 _eyesScale = Vector3.one;
    private Vector3 _fxBase, _fxScale = Vector3.one;
    private float _squashAt = -10f;
    private float _squashK;
    private float[] _hitAt = new float[0];
    private Vector3 _prevPos;
    private Vector3 _vel;
    private float _phase;
    private bool _shownDown;

    // 持ち主の画面だけで使う
    private int _state;          // 0 待つ 1 溜め 2 体当たり 3 ひと休み 4 吹っ飛び（撃たれた・落ちる） 5 倒れている
    private float _stateAt;
    private float _lastSeen;
    private VRCPickup _pickup;

    private void Start()
    {
        if (visual != null) { _baseScale = visual.localScale; _basePos = visual.localPosition; }
        if (eyes != null) _eyesScale = eyes.localScale;
        if (sleepFx != null) { _fxBase = sleepFx.localPosition; _fxScale = sleepFx.localScale; }
        _pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        _prevPos = transform.position;
        _phase = Random.Range(0f, 6.28f);
        _spawnPos = transform.position; _spawnRot = transform.rotation;
        // まだ何も届いていない時は置いてある場所に立つ（届いたら OnDeserialization で上書き）
        if (_sAt == 0.0 && _sFrom == Vector3.zero) { _sFrom = _spawnPos; _sRot = _spawnRot; }
        CopyToActive();
    }

    private double Now() { return Networking.GetServerTimeInSeconds(); }
    private bool Held() { return _pickup != null && _pickup.IsHeld; }

    // 持っている人がトリガー：起きる⇄寝る
    public override void OnPickupUseDown()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _awake = !_awake;
        _windup = false;
        _lastSeen = Time.time;
        _state = 0;
        Squash(0.6f);
        RequestSerialization();
    }

    public override void OnPickup()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _state = 0; _windup = false; _held = true;
        _sHoldPos = transform.position; _sRot = transform.rotation;
        RequestSerialization();
    }

    // 手を離したら、その場の地面に立てる
    public override void OnDrop()
    {
        Vector3 p = transform.position;
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out hit, maxDrop + 0.3f, ground, QueryTriggerInteraction.Ignore)) p = hit.point + Vector3.up * standUp;
        Vector3 f = transform.forward; f.y = 0f;
        _sRot = f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f.normalized) : transform.rotation;
        transform.SetPositionAndRotation(p, _sRot);
        _held = false;
        SetSeg(0, p, Vector3.zero, 0f, 0f, 0f, Now());
        _state = 0; _stateAt = Time.time; _lastSeen = Time.time;
    }

    // エアガンから（撃った人の画面で）。持ち主ならそのまま、そうでなければ自分の画面ですぐ吹っ飛ばして（先読み）、同じ区切りを持ち主に送る
    public void Blown(Vector3 velocity)
    {
        if (_held || Held() || _down) return;
        Vector3 v0 = new Vector3(velocity.x, Mathf.Max(1.2f, velocity.y), velocity.z) * pushScale;
        Vector3 from = transform.position;
        double at = Now();
        if (Networking.IsOwner(gameObject)) { StartFly(from, v0, at); return; }
        SetActive(4, from, v0, 0f, 0f, 0f, at);
        _predUntil = Time.time + 1.5f; _predAt = at;
        Squash(0.8f);
        SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RemoteBlown), from, v0, at);
    }

    [NetworkCallable(10)]
    public void RemoteBlown(Vector3 from, Vector3 v0, double at)
    {
        if (!Networking.IsOwner(gameObject) || _held || Held() || _down) return;
        StartFly(from, v0, at);
        _aShift = Mathf.Clamp((float)(Now() - at), 0f, 0.6f);   // 撃った人より遅れて知った分、自分の画面ではずらして最初から
    }

    private void StartFly(Vector3 from, Vector3 v0, double at)
    {
        SetSeg(4, from, v0, 0f, 0f, 0f, at);
        _state = 4; _stateAt = Time.time;
        _windup = false;
        Squash(0.8f);
    }

    // 持ち主：区切りを決めて配る
    private void SetSeg(int kind, Vector3 from, Vector3 vec, float len, float hop, float dur, double at)
    {
        _sKind = kind; _sFrom = from; _sVec = vec; _sLen = len; _sHop = hop; _sDur = dur; _sAt = at;
        CopyToActive();
        RequestSerialization();
    }

    private void SetActive(int kind, Vector3 from, Vector3 vec, float len, float hop, float dur, double at)
    {
        _aKind = kind; _aFrom = from; _aVec = vec; _aLen = len; _aHop = hop; _aDur = dur; _aAt = at;
        _dashNoGround = false; _aShift = 0f;
    }

    private void CopyToActive()
    {
        Vector3 from = _sFrom;
        if (from == Vector3.zero) from = _spawnPos != Vector3.zero ? _spawnPos : transform.position;   // まだ何も届いていない
        SetActive(_sKind, from, _sVec, _sLen, _sHop, _sDur, _sAt);
        // ほかの人の画面：届くのが遅れた動きは、遅れた分だけ遅らせて最初から見せる（最大0.6秒）
        if (!Networking.IsOwner(gameObject) && (_sKind == 2 || _sKind == 4)) _aShift = Mathf.Clamp((float)(Now() - _sAt), 0f, 0.6f);
    }

    // 今の動き（体当たり・吹っ飛び）がまだ途中か
    private bool Busy()
    {
        // 吹っ飛び：着地するまで。ただし島の外へ落ちて着地しないまま（持ち主が元の場所へ戻した知らせが待たされっぱなしになる）なので、3秒で打ち切る
        //（2026-10-16 てじゃ「まものを外にふっとばしてリスポンしたとき、一人しか見えてなかった」）
        if (_aKind == 4) return !(_flyLanded && _flyFor == _aAt) && Now() - _aAt < 3.0;
        if (_aKind == 2) return Now() - _aAt - _aShift < _aDur;
        return false;
    }

    public override void OnDeserialization()
    {
        if (Networking.IsOwner(gameObject)) return;
        // 自分が撃った先読みが、まだ持ち主に反映されていない（届いたのはそれより前の区切り）
        if (Time.time < _predUntil && _sAt < _predAt - 0.02) return;
        _predUntil = 0f;
        if (_sAt == _aAt && _sKind == _aKind && _sFrom == _aFrom) return;
        // 跳んでいる・吹っ飛んでいる途中に「立つ」が届いた：自分の画面の動きが終わってから切り替える
        if (_sKind == 0 && Busy()) { _pendingSeg = true; return; }
        _pendingSeg = false;
        CopyToActive();
    }

    // 持ち主が入れ替わった：今の区切りの続きから自分が決める
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        _sKind = _aKind; _sFrom = _aFrom; _sVec = _aVec; _sLen = _aLen; _sHop = _aHop; _sDur = _aDur; _sAt = _aAt;
        _state = _aKind == 2 ? 2 : (_aKind == 4 ? 4 : 0);
        _stateAt = Time.time; _lastSeen = Time.time;
        _predUntil = 0f;
        RequestSerialization();
    }

    private void FixedUpdate()
    {
        Vector3 c = transform.position;
        Vector3 v = (c - _prevPos) / Mathf.Max(0.0001f, Time.fixedDeltaTime);
        _prevPos = c;
        _vel = Vector3.Lerp(_vel, v, 0.5f);
        if (_down || spawner == null || spawner.balls == null) return;
        FuwaSharedBall[] balls = spawner.balls;
        if (_hitAt.Length != balls.Length) _hitAt = new float[balls.Length];
        for (int i = 0; i < balls.Length; i++)
        {
            FuwaSharedBall b = balls[i];
            if (b == null || !b.IsFlying() || Time.time - _hitAt[i] < 0.25f) continue;
            Vector3 rel = b.transform.position - c;
            float rr = radius + b.transform.lossyScale.x * 0.5f;
            float d = rel.magnitude;
            if (d > rr || d < 0.0001f) continue;
            Vector3 n = rel / d;
            Rigidbody rb = b.GetComponent<Rigidbody>();
            Vector3 bv = rb != null ? rb.velocity : Vector3.zero;
            float vn = Vector3.Dot(bv, n);
            float pv = Mathf.Max(0f, Vector3.Dot(_vel, n));   // ぬいぐるみが向かってきた速さ（体当たり）。位置の変わり方から出すので全員の画面で同じ
            if (vn - pv > 0.2f) continue;   // もう離れていく所
            _hitAt[i] = Time.time;
            // 法線方向の速さを跳ね返す（少し強め）＋最低これだけは外へ＋体当たりの勢い
            float outSpeed = Mathf.Max(minOut, -vn * bounce) + pv * dashPush;
            b.Nudge(n * (outSpeed - vn) + Vector3.up * (0.6f + pv * 0.25f));
            Squash(Mathf.Clamp01((pv - vn) / 4f + 0.35f));
        }
    }

    private void Squash(float k)
    {
        _squashAt = Time.time; _squashK = k;
        if (boing != null && boing.clip != null) { boing.pitch = Random.Range(0.95f, 1.15f); boing.PlayOneShot(boing.clip, 0.4f + 0.6f * k); }
    }

    private void Update()
    {
        bool owner = Networking.IsOwner(gameObject);
        if (Held())
        {
            // 自分が持っている：位置を送る（Continuous なので変えておけば届く）
            if (owner) { _sHoldPos = transform.position; _sRot = transform.rotation; }
        }
        else if (_held)
        {
            // ほかの人が持っている
            if (_sHoldPos != Vector3.zero) transform.SetPositionAndRotation(_sHoldPos, SafeRot());
        }
        else
        {
            if (owner) Think();
            else if (_aKind == 4 && Now() - _aAt > 4.0 && transform.position.y < fallY)
            {
                // 島の外へ落ちたのに持ち主が戻さない（持ち主があそびばにいなくて、この子が隠れて動いていない時など）：自分が持ち主になって元の場所へ戻す
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
                ResetHome();
            }
            if (!_down)
            {
                Vector3 p = EvalPos();
                if (_pendingSeg && !Busy()) { _pendingSeg = false; CopyToActive(); p = EvalPos(); }
                if (p != Vector3.zero)
                {
                    // 区切りが変わった瞬間の位置のずれ（撃たれた知らせ・体当たりの知らせが通信の分だけ遅れて届いた時など）は、
                    // ワープさせずに少しずつ寄せる（2026-10-14 てじゃ「オーナー以外が撃ったあとワープして見える」）
                    if (_aAt != _errSeg)
                    {
                        _errSeg = _aAt;
                        Vector3 e = transform.position - p;
                        _err = e.magnitude < 3f ? e : Vector3.zero;   // 遠すぎる（元の場所へ戻した等）時はそのまま
                    }
                    _err = Vector3.Lerp(_err, Vector3.zero, 1f - Mathf.Exp(-errDecay * Time.deltaTime));
                    transform.position = p + _err;
                }
                transform.rotation = SafeRot();
            }
        }
        UpdateLook();
    }

    // 同期した向き。届く前・補間の途中で長さ0の回転になると表示が壊れるので、その時は置いてあった向き
    private Quaternion SafeRot()
    {
        Quaternion q = _sRot;
        float m = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
        if (m < 0.5f) return _spawnRot;
        return q;
    }

    // ---------- 区切りから今の位置を出す（全員の画面で同じ計算） ----------
    private Vector3 EvalPos()
    {
        float t = Mathf.Max(0f, (float)(Now() - _aAt) - _aShift);
        if (_aKind == 2)
        {
            float x = Mathf.Clamp01(t / Mathf.Max(0.05f, _aDur));
            Vector3 q = _aFrom + _aVec * _aLen * x;
            // 動ける範囲から出ない（島の中／置いた場所のまわり）
            Vector3 c = areaRadius > 0f ? areaCenter : _aFrom; float lim = areaRadius > 0f ? areaRadius : leashRange;
            Vector3 fromC = q - c; fromC.y = 0f;
            if (fromC.magnitude > lim) q = new Vector3(c.x, q.y, c.z) + fromC.normalized * lim;
            RaycastHit hit;
            if (!Physics.Raycast(q + Vector3.up * 1.0f, Vector3.down, out hit, 2.5f, ground, QueryTriggerInteraction.Ignore))
            {
                _dashNoGround = true;   // 足元に地面がない：持ち主が吹っ飛び（落ちる）に切り替える
                return new Vector3(q.x, transform.position.y, q.z);
            }
            return new Vector3(q.x, hit.point.y + standUp + _aHop * Mathf.Sin(x * Mathf.PI), q.z);
        }
        if (_aKind == 4)
        {
            if (_flyFor != _aAt) { _flyFor = _aAt; _flyPos = _aFrom; _flyVel = _aVec; _flyT = 0f; _flyLanded = false; }
            t = Mathf.Min(t, 8f);
            int guard = 0;
            while (!_flyLanded && _flyT + FlyStep <= t && guard++ < 400)
            {
                _flyVel += Vector3.down * gravity * FlyStep;
                float drag = Mathf.Clamp01(1f - 0.8f * FlyStep);
                _flyVel.x *= drag; _flyVel.z *= drag;
                Vector3 q = _flyPos + _flyVel * FlyStep;
                RaycastHit hit;
                if (_flyVel.y <= 0f && Physics.Raycast(q + Vector3.up * 0.5f, Vector3.down, out hit, 0.5f + standUp, ground, QueryTriggerInteraction.Ignore))
                {
                    _flyPos = new Vector3(q.x, hit.point.y + standUp, q.z);
                    _flyLanded = true;
                }
                else _flyPos = q;
                _flyT += FlyStep;
            }
            if (_flyLanded && _landSquashFor != _aAt) { _landSquashFor = _aAt; Squash(0.5f); }
            return _flyPos;
        }
        return _aFrom;
    }

    // ---------- 持ち主の画面：どう動くか決める ----------
    private void Think()
    {
        Vector3 p = transform.position;
        if (_state == 5)
        {
            // 倒れている：少ししたら元の場所で寝て復活
            if (Time.time - _stateAt >= reviveDelay) ResetHome();
            return;
        }
        // 落ちすぎたら元の場所へ戻して寝かせる
        if (p.y < fallY) { ResetHome(); return; }
        // イバラに押し込まれた（体当たり中・吹っ飛び中だけ）
        if ((_state == 2 || _state == 4) && TouchingBramble(p))
        {
            _state = 5; _stateAt = Time.time;
            _down = true; _awake = false; _windup = false;
            RequestSerialization();
            return;
        }
        if (_aKind == 4)
        {
            // 吹っ飛び中：着地したらその場に立つ（そこが新しい居場所）
            if (_flyLanded && _flyFor == _aAt) { SetSeg(0, _flyPos, Vector3.zero, 0f, 0f, 0f, Now()); _state = 3; _stateAt = Time.time; }
            return;
        }
        if (!_awake) return;

        FuwaSharedBall target = FindTarget(p);
        if (target != null) _lastSeen = Time.time;
        else if (_state == 0 && Time.time - _lastSeen > sleepAfter)
        {
            _awake = false; _windup = false; RequestSerialization();
            return;
        }

        float t = Time.time - _stateAt;
        if (_state == 0)
        {
            // 置いた場所へ歩いて戻ることはしない（2026-10-14 てじゃ「ジャンプしてから戻ろうとしちゃう」）
            if (target != null && t > 0.2f)
            {
                Face(target.transform.position - p);
                _state = 1; _stateAt = Time.time; _windup = true; RequestSerialization();
            }
            else if (target == null)
            {
                if (_nextWander <= 0f) _nextWander = Time.time + Random.Range(wanderMin, wanderMax);
                if (Time.time >= _nextWander) { _nextWander = 0f; Wander(p); }
            }
        }
        else if (_state == 1)
        {
            if (target != null) Face(target.transform.position - p);
            // 体当たりの区切りは、溜めが終わる dashLead 秒前に「この時刻に跳ぶ」と先に配る（通信の遅れで、ほかの人の画面でジャンプの途中が抜けないように。2026-10-14 てじゃ「ジャンプ中が見えない」）
            if (t >= windupTime - dashLead)
            {
                if (target == null) { _windup = false; _state = 0; _stateAt = Time.time; RequestSerialization(); return; }
                Vector3 tp = target.transform.position;
                Vector3 d = tp - p; d.y = 0f;
                Vector3 dir = d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
                // 跳び方をふわふわに合わせる：ふわふわの少し先に着地して、ちょうどふわふわの所で体がふわふわの高さに来る弧にする
                float dist = d.magnitude;
                float len = Mathf.Clamp(dist + 0.5f, 0.8f, dashSpeed * dashTime * 1.6f);
                float xHit = Mathf.Clamp01(dist / len);
                float lift = tp.y - p.y;
                float hop = Mathf.Clamp(lift / Mathf.Max(0.35f, Mathf.Sin(xHit * Mathf.PI)), 0.1f, hopHeight * 3f);
                SetSeg(2, p, dir, len, hop, len / Mathf.Max(0.1f, dashSpeed), Now() + Mathf.Max(0f, windupTime - t));
                _state = 2; _stateAt = Time.time;
            }
        }
        else if (_state == 2)
        {
            if (_aKind != 2) { _state = 0; _stateAt = Time.time; return; }
            if (_windup && Now() >= _aAt) { _windup = false; RequestSerialization(); }   // 跳び始めたら溜めの見た目をやめる
            if (_dashNoGround)
            {
                // 足元に地面がない所へ跳んだ：そのまま落ちる
                SetSeg(4, p, _aVec * dashSpeed * 0.5f, 0f, 0f, 0f, Now()); _state = 4; _stateAt = Time.time; return;
            }
            if (Now() - _aAt >= _aDur) { SetSeg(0, p, Vector3.zero, 0f, 0f, 0f, Now()); _state = 3; _stateAt = Time.time; }
        }
        else if (_state == 3)
        {
            if (t >= cooldown) { _state = 0; _stateAt = Time.time; }
        }
        else if (_state == 4)
        {
            _state = 0; _stateAt = Time.time;   // 吹っ飛びは上で扱う（ここに来たら区切りはもう立っている）
        }
    }

    // 相手がいない時のぴょん：ランダムな向きへ少しだけ。動ける範囲のふちに近い時は内側へ向かう
    private void Wander(Vector3 p)
    {
        Vector3 c = areaRadius > 0f ? areaCenter : _aFrom; float lim = areaRadius > 0f ? areaRadius : leashRange;
        Vector3 toC = c - p; toC.y = 0f;
        float a = Random.Range(0f, 360f);
        Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
        if (toC.magnitude > lim - 1.8f && toC.sqrMagnitude > 0.0001f)
            dir = Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * toC.normalized;   // ふちの近く：内側へ
        float len = Random.Range(wanderLenMin, wanderLenMax);
        _sRot = Quaternion.LookRotation(dir);
        SetSeg(2, p, dir, len, hopHeight * wanderHop, len / Mathf.Max(0.1f, dashSpeed * 0.6f), Now() + dashLead);
        _state = 2; _stateAt = Time.time;
    }

    private bool TouchingBramble(Vector3 p)
    {
        if (bramble == null || !bramble.gameObject.activeInHierarchy) return false;
        Vector3 d = p - bramble.position;
        if (d.y > radius + 0.6f || d.y < -radius - 0.3f) return false;
        d.y = 0f;
        return d.magnitude < brambleRadius + radius * 0.6f;
    }

    // 元の場所へ戻して寝かせる（落ちた時・「おもちゃを戻す」ボタン FuwaHandFanReset から。呼ぶ前に持ち主になっておく）
    public void ResetHome()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (_pickup != null && _pickup.IsHeld) _pickup.Drop();
        _held = false;
        _sRot = _spawnRot;
        SetSeg(0, _spawnPos, Vector3.zero, 0f, 0f, 0f, Now());
        _state = 0;
        _awake = false; _windup = false; _down = false;
        RequestSerialization();
    }

    private void Face(Vector3 d)
    {
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return;
        _sRot = Quaternion.Slerp(_sRot, Quaternion.LookRotation(d.normalized), 0.25f);
    }

    private FuwaSharedBall FindTarget(Vector3 p)
    {
        if (spawner == null || spawner.balls == null) return null;
        // 追いかけている最中のふわふわは、遠くへ飛ばしても（keepRange まで・動ける範囲の中なら）見失わない。
        // ただし近くに別のふわふわが来たら（今の相手の switchRatio 倍より近い）そっちへ乗りかえる（2026-10-16 てじゃ「近くに別のがあるときにターゲットうつりやすく」）
        float keepD = -1f;
        if (_chase != null && _chase.IsFlying() && InArea(_chase.transform.position))
        {
            keepD = (_chase.transform.position - p).magnitude;
            if (keepD >= keepRange) keepD = -1f;
        }
        FuwaSharedBall cur = keepD >= 0f ? _chase : null;
        _chase = null;
        FuwaSharedBall best = null;
        float bestD = detectRange;
        FuwaSharedBall[] balls = spawner.balls;
        for (int i = 0; i < balls.Length; i++)
        {
            FuwaSharedBall b = balls[i];
            if (b == null || !b.IsFlying()) continue;
            Vector3 bp = b.transform.position;
            if (!InArea(bp)) continue;
            float d = (bp - p).magnitude;
            if (d < bestD) { bestD = d; best = b; }
        }
        // 今の相手がいる時：いちばん近いのが今の相手より switchRatio 倍以上近い時だけ乗りかえる（すぐ行ったり来たりしないように）
        if (cur != null && (best == null || best == cur || bestD > keepD * switchRatio)) best = cur;
        _chase = best;
        return best;
    }

    private FuwaSharedBall _chase;
    private bool InArea(Vector3 v)
    {
        Vector3 c = areaRadius > 0f ? areaCenter : _aFrom; float lim = (areaRadius > 0f ? areaRadius : leashRange) + targetMargin;
        Vector3 d = v - c; d.y = 0f;
        return d.magnitude <= lim;
    }


    // 寝ている目「︶」：寝ている間は白目・黒目を隠して、弧の線（Eyes の子の SleepEye、FuwaSleepEyesSetup で付ける）を出す。
    // SleepEye が無ければ前のように目を上下につぶして、黒目だけ隠す（2026-10-13 てじゃ「うっすら黒目みえてこわい」「😴こんな目に」）
    private Transform[] _eyeParts = new Transform[0];
    private bool _hasSleepEye;
    private bool _eyesInited;
    private bool _eyesClosed;
    // 寝ている間は、目のまわりの毛を短くする仕組み（毛のシェーダーの _EyeMinLen）を弱めて、目の所がハゲて見えないように（2026-10-13 てじゃ）
    private Renderer[] _furs = new Renderer[0];
    private float[] _furEyeMin = new float[0];
    private MaterialPropertyBlock _furMpb;
    private const float SleepFurEyeMin = 0.75f;
    private void InitEyes()
    {
        if (_eyesInited) return;
        _eyesInited = true;
        if (visual != null)
        {
            Renderer[] rs = visual.GetComponentsInChildren<Renderer>(true);
            int n = 0;
            for (int i = 0; i < rs.Length; i++) if (rs[i].sharedMaterial != null && rs[i].sharedMaterial.HasProperty("_EyeMinLen")) n++;
            _furs = new Renderer[n]; _furEyeMin = new float[n]; n = 0;
            for (int i = 0; i < rs.Length; i++) if (rs[i].sharedMaterial != null && rs[i].sharedMaterial.HasProperty("_EyeMinLen")) { _furs[n] = rs[i]; _furEyeMin[n] = rs[i].sharedMaterial.GetFloat("_EyeMinLen"); n++; }
            _furMpb = new MaterialPropertyBlock();
        }
        if (eyes == null) return;
        _eyeParts = new Transform[eyes.childCount];
        for (int i = 0; i < eyes.childCount; i++) { _eyeParts[i] = eyes.GetChild(i); if (_eyeParts[i].name == "SleepEye") _hasSleepEye = true; }
    }
    private void SetEyesClosed(bool closed)
    {
        InitEyes();
        if (eyes == null) return;
        if (closed == _eyesClosed && _eyesInited && (closed || eyes.localScale == _eyesScale)) return;
        _eyesClosed = closed;
        for (int i = 0; i < _eyeParts.Length; i++)
        {
            Transform t = _eyeParts[i];
            if (t == null) continue;
            bool on;
            if (t.name == "SleepEye") on = closed;
            else if (t.name == "Pupil") on = !closed;
            else on = !closed || !_hasSleepEye;   // 白目：︶ がある時は隠す、無い時はつぶして見せる
            if (t.gameObject.activeSelf != on) t.gameObject.SetActive(on);
        }
        eyes.localScale = closed && !_hasSleepEye ? new Vector3(_eyesScale.x, _eyesScale.y * 0.03f, _eyesScale.z) : _eyesScale;
        if (_hasSleepEye && _furMpb != null)
            for (int i = 0; i < _furs.Length; i++)
            {
                if (_furs[i] == null) continue;
                _furs[i].GetPropertyBlock(_furMpb);
                _furMpb.SetFloat("_EyeMinLen", closed ? Mathf.Max(SleepFurEyeMin, _furEyeMin[i]) : _furEyeMin[i]);
                _furs[i].SetPropertyBlock(_furMpb);
            }
    }

    // ---------- 全員の画面：見た目 ----------
    private void UpdateLook()
    {
        if (visual == null) return;
        // 倒れた：パチンと消える（同期した _down で全員）
        if (_down != _shownDown)
        {
            _shownDown = _down;
            visual.gameObject.SetActive(!_down);
            if (_down)
            {
                if (popEffect != null) { popEffect.transform.position = transform.position; popEffect.Play(); }
                if (popSound != null) popSound.Play();
            }
        }
        if (_down) return;
        float t = Time.time - _squashAt;
        Vector3 s = _baseScale;
        if (!_awake)
        {
            // 寝てる：すーすー（コース4の寝ているまものと同じ）。持っている間も寝たまま（2026-10-14 てじゃ「寝てる時につかんだら寝たままで」。前は持つと目を開けた）
            float br = Mathf.Sin(Time.time * 1.6f + _phase);
            s = new Vector3(_baseScale.x * (1.08f + 0.04f * br), _baseScale.y * (0.84f - 0.04f * br), _baseScale.z * (1.08f + 0.04f * br));
            SetEyesClosed(true);
            if (sleepFx != null)
            {
                if (!sleepFx.gameObject.activeSelf) sleepFx.gameObject.SetActive(true);
                float k = 0.5f + 0.5f * br;
                sleepFx.localScale = _fxScale * Mathf.Lerp(0.2f, 1f, k * k);
                sleepFx.localPosition = _fxBase + Vector3.forward * 0.5f * _fxScale.z * Mathf.Lerp(0.2f, 1f, k * k);
            }
        }
        else
        {
            SetEyesClosed(false);
            if (sleepFx != null && sleepFx.gameObject.activeSelf) sleepFx.gameObject.SetActive(false);
            if (_windup)
            {
                // 溜め：ぷるぷる震えながら低くなる
                float w = 0.06f * Mathf.Sin(Time.time * 55f);
                s = new Vector3(_baseScale.x * (1.12f + w), _baseScale.y * (0.82f - w), _baseScale.z * (1.12f + w));
            }
        }
        if (t < 1f)
        {
            float w = _squashK * 0.3f * Mathf.Exp(-t * 6f) * Mathf.Cos(t * 22f);
            s = new Vector3(s.x * (1f + w * 0.6f), s.y * (1f - w), s.z * (1f + w * 0.6f));
        }
        if (visual.localScale != s) visual.localScale = s;
        // つぶれて低くなった分だけ下げて、足元を地面につけたままにする（寝息・溜め・ぽよん）（2026-10-13 てじゃ「もうちょい接地」）
        Vector3 lp = _basePos + Vector3.down * bodyBottom * (1f - s.y / Mathf.Max(0.0001f, _baseScale.y));
        if (visual.localPosition != lp) visual.localPosition = lp;
    }
}
