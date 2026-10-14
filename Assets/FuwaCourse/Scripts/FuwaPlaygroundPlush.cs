using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

// あそびばのまもののぬいぐるみ（コース4 まもののす を誰かがクリアすると出てくる）。
// ふわふわ（FuwaSharedBall）がぶつかると、ぬいぐるみがぽよんとつぶれて、ふわふわを跳ね返す（それぞれの画面で同じように Nudge）。
// 持って好きな所に置ける（VRCObjectSync）。当たり判定はトリガー（持つため）で、跳ね返しはこのスクリプト。
// 2026-10-13 てじゃ「ペットみたいに追いかける。ただ止めれるように」：
// ・最初は寝ている（目を閉じて鼻ちょうちん）。持ってトリガー（撃つボタン）で起きる⇄寝るを切り替え
// ・起きていると、置いた場所のまわりでふわふわを見つけて、ぷるぷる溜めてからぴょんと体当たり（割らない、押すだけ）
// ・追いかける相手がいないまま sleepAfter 秒たったら勝手に寝る
// ・エアガンで撃つと押し返せる（FuwaBlower → Blown）。島の外へ落ちたら元の場所へ戻って寝る
// ・イバラ（bramble）に押し込まれたら、パチンと倒れて少ししたら元の場所で寝て復活
// 動かすのは持ち主の画面だけ（位置は VRCObjectSync が配る）。起きている・溜めている・倒れているは同期
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

    // 持ち主の画面だけで使う動き
    private int _state;          // 0 待つ・戻る 1 溜め 2 体当たり 3 ひと休み 4 吹っ飛び（撃たれた・落ちる） 5 倒れている
    private float _stateAt;
    private Vector3 _home;
    private bool _homeSet;
    private float _lastSeen;
    private Vector3 _dashFrom, _dashDir;
    private float _dashLen, _dashHop, _dashDur;
    private Vector3 _air;        // 吹っ飛んでいる時の速さ
    private VRCPickup _pickup;

    private void Start()
    {
        if (visual != null) { _baseScale = visual.localScale; _basePos = visual.localPosition; }
        if (eyes != null) _eyesScale = eyes.localScale;
        if (sleepFx != null) { _fxBase = sleepFx.localPosition; _fxScale = sleepFx.localScale; }
        _pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        _prevPos = transform.position;
        _phase = Random.Range(0f, 6.28f);
    }

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

    public override void OnPickup() { _state = 0; if (_windup) { _windup = false; RequestSerialization(); } }

    // 手を離したら、その場の地面に立てる。ここが新しい「おうち」
    public override void OnDrop()
    {
        Vector3 p = transform.position;
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out hit, maxDrop + 0.3f, ground, QueryTriggerInteraction.Ignore)) p = hit.point + Vector3.up * standUp;
        Vector3 f = transform.forward; f.y = 0f;
        transform.SetPositionAndRotation(p, f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f.normalized) : transform.rotation);
        _home = p; _homeSet = true;
        _state = 0; _stateAt = Time.time; _lastSeen = Time.time;
    }

    // エアガンから（撃った人の画面で）。持ち主の画面で動かす
    public void Blown(Vector3 velocity)
    {
        if (Held() || _down) return;
        if (Networking.IsOwner(gameObject)) ApplyBlown(velocity);
        else SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RemoteBlown), velocity);
    }

    [NetworkCallable(10)]
    public void RemoteBlown(Vector3 velocity)
    {
        if (!Networking.IsOwner(gameObject) || Held() || _down) return;
        ApplyBlown(velocity);
    }

    private void ApplyBlown(Vector3 velocity)
    {
        _air = new Vector3(velocity.x, Mathf.Max(1.2f, velocity.y), velocity.z) * pushScale;
        _state = 4; _stateAt = Time.time;
        if (_windup) { _windup = false; RequestSerialization(); }
        Squash(0.8f);
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
        if (Networking.IsOwner(gameObject) && !Held()) Think();
        UpdateLook();
    }

    // ---------- 持ち主の画面：動き ----------
    private void Think()
    {
        if (!_homeSet) { _home = transform.position; _homeSet = true; }
        float dt = Time.deltaTime;
        Vector3 p = transform.position;
        if (_state == 5)
        {
            // 倒れている：少ししたら元の場所で寝て復活
            if (Time.time - _stateAt >= reviveDelay) ResetHome();
            return;
        }
        // 落ちすぎたら元の場所へ（VRCObjectSync の最初の位置）、寝かせる
        if (p.y < fallY) { ResetHome(); return; }
        // イバラに押し込まれた（体当たり中・吹っ飛び中だけ。歩いて戻る時は避ける）
        if ((_state == 2 || _state == 4) && TouchingBramble(p))
        {
            _state = 5; _stateAt = Time.time;
            _down = true; _awake = false; _windup = false;
            RequestSerialization();
            return;
        }
        if (_state == 4) { Fly(dt); return; }
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
            if (target != null && Time.time - _stateAt > 0.2f)
            {
                Face(target.transform.position - p);
                _state = 1; _stateAt = Time.time; _windup = true; RequestSerialization();
                return;
            }
            // おうちへ戻る（ぴょこぴょこ歩き。イバラは回り込む）
            Vector3 h = _home - p; h.y = 0f;
            if (h.magnitude > 0.15f)
            {
                Vector3 step = h.normalized * Mathf.Min(h.magnitude, returnSpeed * dt);
                if (TouchingBramble(p + step * 8f)) step = new Vector3(step.z, 0f, -step.x);
                Face(step);
                Place(p + step, 0.08f * Mathf.Abs(Mathf.Sin(Time.time * 9f)));
            }
        }
        else if (_state == 1)
        {
            if (target != null) Face(target.transform.position - p);
            if (t >= windupTime)
            {
                _windup = false;
                if (target == null) { _state = 0; _stateAt = Time.time; RequestSerialization(); return; }
                Vector3 tp = target.transform.position;
                Vector3 d = tp - p; d.y = 0f;
                _dashDir = d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
                _dashFrom = p;
                // 跳び方をふわふわに合わせる：ふわふわの少し先に着地して、ちょうどふわふわの所で体がふわふわの高さに来る弧にする
                // （前はいつも同じ高さ・同じ距離で、床にいる小さいふわふわの頭上を越えてしまっていた。2026-10-13 てじゃ）
                float dist = d.magnitude;
                _dashLen = Mathf.Clamp(dist + 0.5f, 0.8f, dashSpeed * dashTime * 1.6f);
                float xHit = Mathf.Clamp01(dist / _dashLen);
                float lift = tp.y - p.y;   // 体の中心から見たふわふわの高さ（床のふわふわならマイナス）
                _dashHop = Mathf.Clamp(lift / Mathf.Max(0.35f, Mathf.Sin(xHit * Mathf.PI)), 0.1f, hopHeight * 3f);
                _dashDur = _dashLen / Mathf.Max(0.1f, dashSpeed);
                _state = 2; _stateAt = Time.time;
                RequestSerialization();
            }
        }
        else if (_state == 2)
        {
            float x = Mathf.Clamp01(t / Mathf.Max(0.05f, _dashDur));
            Vector3 q = _dashFrom + _dashDir * _dashLen * x;
            // 動ける範囲から出ない（島の中／おうちのまわり）
            Vector3 c = areaRadius > 0f ? areaCenter : _home; float lim = areaRadius > 0f ? areaRadius : leashRange;
            Vector3 fromHome = q - c; fromHome.y = 0f;
            if (fromHome.magnitude > lim) q = new Vector3(c.x, q.y, c.z) + fromHome.normalized * lim;
            if (!Place(q, _dashHop * Mathf.Sin(x * Mathf.PI)))
            {
                // 足元に地面がない所へ跳んだ：そのまま落ちる
                _air = _dashDir * dashSpeed * 0.5f; _state = 4; _stateAt = Time.time; return;
            }
            // 着地した所を新しいおうちにする（前は跳ぶたびに置いた場所へ歩いて戻ろうとした。2026-10-14 てじゃ「ジャンプ後に戻らないように」）
            if (x >= 1f) { _state = 3; _stateAt = Time.time; _home = transform.position; }
        }
        else if (_state == 3)
        {
            if (t >= cooldown) { _state = 0; _stateAt = Time.time; }
        }
    }

    private bool TouchingBramble(Vector3 p)
    {
        if (bramble == null || !bramble.gameObject.activeInHierarchy) return false;
        Vector3 d = p - bramble.position;
        if (d.y > radius + 0.6f || d.y < -radius - 0.3f) return false;
        d.y = 0f;
        return d.magnitude < brambleRadius + radius * 0.6f;
    }

    // 元の場所（VRCObjectSync の最初の位置）へ戻して寝かせる
    private void ResetHome()
    {
        VRCObjectSync s = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        if (s != null) s.Respawn();
        _homeSet = false; _state = 0;
        _awake = false; _windup = false; _down = false;
        RequestSerialization();
    }

    // 地面の上の q へ置く（lift＝地面からの浮き）。下に地面がなければ false
    private bool Place(Vector3 q, float lift)
    {
        RaycastHit hit;
        if (!Physics.Raycast(q + Vector3.up * 1.0f, Vector3.down, out hit, 2.5f, ground, QueryTriggerInteraction.Ignore)) return false;
        transform.position = new Vector3(q.x, hit.point.y + standUp + lift, q.z);
        return true;
    }

    // 撃たれて吹っ飛ぶ・落ちる
    private void Fly(float dt)
    {
        Vector3 p = transform.position;
        _air += Vector3.down * gravity * dt;
        _air.x *= Mathf.Clamp01(1f - 0.8f * dt); _air.z *= Mathf.Clamp01(1f - 0.8f * dt);
        Vector3 q = p + _air * dt;
        RaycastHit hit;
        if (_air.y <= 0f && Physics.Raycast(q + Vector3.up * 0.5f, Vector3.down, out hit, 0.5f + standUp, ground, QueryTriggerInteraction.Ignore))
        {
            transform.position = new Vector3(q.x, hit.point.y + standUp, q.z);
            Squash(0.5f);
            _state = 3; _stateAt = Time.time;
            _home = transform.position;   // 吹っ飛ばされて着地した所がおうち（戻って歩かない）
            return;
        }
        transform.position = q;
    }

    private void Face(Vector3 d)
    {
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d.normalized), 0.25f);
    }

    private FuwaSharedBall FindTarget(Vector3 p)
    {
        if (spawner == null || spawner.balls == null) return null;
        // 追いかけている最中のふわふわは、遠くへ飛ばしても（keepRange まで・動ける範囲の中なら）見失わない
        if (_chase != null && _chase.IsFlying() && InArea(_chase.transform.position) && (_chase.transform.position - p).magnitude < keepRange) return _chase;
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
        _chase = best;
        return best;
    }

    private FuwaSharedBall _chase;
    private bool InArea(Vector3 v)
    {
        Vector3 c = areaRadius > 0f ? areaCenter : _home; float lim = areaRadius > 0f ? areaRadius : leashRange;
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
