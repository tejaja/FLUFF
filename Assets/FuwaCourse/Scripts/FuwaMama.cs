using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// まもののすの奥の大きい巣で寝ている「ママけだま」（自分の画面だけ、同期なし）。
// 近くに来ると起きて、巣の中をめっちゃゆっくりふわふわを追いかけてくる（運んでない時はプレイヤー）。
// ふわふわにぶつかると大きめに吹っ飛ばす（2026-10-09 ちびけだまと同じくふわふわ狙いに）。人も通り道にいれば吹っ飛ぶ。
// エアガンはチャージ撃ちだけ少し押せる（ノンチャージは効かない）。倒せないので急いで抜ける。
// （useSneeze をONにすると、前の「くしゃみで押し戻す」も使える）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaMama : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("見た目（呼吸・ためでふくらむ）")]
    public Transform visual;
    [Tooltip("目（寝てる時・ためてる時は細く）")]
    public Transform eyes;
    [Tooltip("くしゃみの風が出る所（口）。なければこのオブジェクト")]
    public Transform mouth;

    [Header("黒目（動く）")]
    [Tooltip("左右の黒目（白目の前の面に沿って動く）")]
    public Transform pupilL;
    public Transform pupilR;
    [Tooltip("黒目が中心から動ける距離（目のローカル）")]
    public float pupilReach = 0.036f;
    [Tooltip("白目の前の面のふくらみ（黒目を白目の面に沿わせる）")]
    public float pupilDepth = 0.04f;

    [Header("風の当たり判定")]
    [Tooltip("エアガンの風が当たる体の半径(m)。風の円すいがこの球のどこかにかすれば当たり（2026-10-07 前は中心の1点だけだった）")]
    public float hitRadius = 1.3f;

    [Header("追いかける")]
    [Tooltip("ON：運んでいる最中のふわふわを追いかけて吹っ飛ばす。OFF：前と同じくプレイヤーを追いかける")]
    public bool targetBall = false;
    [Tooltip("追いかける速さ(m/s)。めっちゃ遅い（ジャンプしない時）")]
    public float chaseSpeed = 0.55f;
    [Tooltip("ゆったりジャンプで近づく（OFFなら這う）")]
    public bool hop = true;
    public float hopDistance = 1.3f;
    public float hopHeight = 0.7f;
    [Tooltip("ジャンプしている秒数")]
    public float hopTime = 0.9f;
    [Tooltip("着地してから次のジャンプまでの秒数")]
    public float hopRest = 1.4f;
    [Tooltip("巣の中心（ここから moveRadius の中だけ動く）。なければ最初の位置")]
    public Transform nestCenter;
    public float moveRadius = 3.4f;
    [Tooltip("体の半径(m)。ぶつかり判定に使う")]
    public float bodyRadius = 0.8f;
    [Tooltip("ぶつかった時に人を吹っ飛ばす強さ")]
    public float knockback = 8.5f;
    public float knockUp = 4f;
    [Tooltip("ふわふわに触れた時に押しのける強さ（運んでない・待ってる時）")]
    public float ballBump = 3.5f;
    [Tooltip("運んでいる最中のふわふわにぶつかった時に吹っ飛ばす速さ（横・上）")]
    public float ballKnockback = 6.5f;
    public float ballKnockUp = 1.8f;
    [Tooltip("一度吹っ飛ばしてから次に当たるまでの秒数")]
    public float ballHitCooldown = 1.2f;
    public Color ballHitGlow = new Color(0.75f, 0.35f, 1f);
    [Tooltip("チャージ撃ちで押された時の倍率（ふつうのけだまより重い）")]
    public float pushScale = 0.35f;
    public float pushDamping = 2.5f;
    public AudioSource bumpSound;

    [Header("くしゃみ（useSneeze の時だけ）")]
    public bool useSneeze = false;
    [Tooltip("くしゃみの間の秒数（ため時間は含まない）")]
    public float interval = 3.2f;
    public float intervalJitter = 0.8f;
    [Tooltip("「は…は…」とためる秒数（予告）")]
    public float telegraph = 1.4f;
    [Tooltip("ふわふわか自分がこの距離にいる時だけくしゃみする(m)")]
    public float activeRange = 13f;

    [Header("くしゃみの風")]
    public float range = 7.5f;
    [Tooltip("風の円すいの半分の角度(度)")]
    public float coneAngle = 38f;
    public float ballPush = 4.2f;
    public float ballUp = 1.6f;
    public float playerPush = 4.5f;
    public float playerUp = 2.2f;

    [Header("見た目・音")]
    public Transform zzz;
    public Transform warnText;
    public Transform sneezeText;
    public ParticleSystem sneezeFx;
    public AudioSource inhaleSound;
    public AudioSource sneezeSound;

    // 0=寝てる 1=待ち 2=ため 3=くしゃみ直後
    private int _state;
    private float _t;
    private float _wait;
    private Vector3 _visualScale = Vector3.one;
    private Vector3 _visualBase;
    private Vector3 _eyesScale = Vector3.one;
    private Vector3 _zzzBase;
    private Vector3 _zzzScale = Vector3.one;
    private Vector3 _startPos;
    private Quaternion _startRot;
    private Vector3 _home;
    private Vector3 _push;
    private float _bumpUntil;
    private float _ballHitUntil;
    private float _squashUntil;
    private float _hopClock;
    private Vector3 _hopDir;
    private bool _inHop;
    private float _crouch;

    private bool _inited;

    private void Start()
    {
        Init();
    }

    // まもののすは普段隠してあって Start がまだのうちに ResetBoss が来ることがある。
    // その時に最初の位置(_startPos)が (0,0,0) のままだと原点へ飛んでしまうので、先にここで覚える
    private void Init()
    {
        if (_inited) return;
        _inited = true;
        if (visual != null) { _visualScale = visual.localScale; _visualBase = visual.localPosition; }
        if (eyes != null) _eyesScale = eyes.localScale;
        InitPupils();
        if (zzz != null) { _zzzBase = zzz.localPosition; _zzzScale = zzz.localScale; }
        _startPos = transform.position; _startRot = transform.rotation;
        if (warnText != null) warnText.gameObject.SetActive(false);
        if (sneezeText != null) sneezeText.gameObject.SetActive(false);
        _wait = interval;
        _home = nestCenter != null ? nestCenter.position : transform.position;
        _home.y = transform.position.y;
    }

    // エアガンから（FuwaBlower）。charged=false（ノンチャージ）は効かない
    public void Blown(Vector3 impulse, bool charged)
    {
        if (!charged || _state == 0) return;
        Vector3 v = impulse * pushScale; v.y = 0f;
        _push += v;
        _squashUntil = Time.time + 0.3f;
    }

    private void SetState(int s) { _state = s; _t = 0f; }

    private void Update()
    {
        float dt = Time.deltaTime;
        _t += dt;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null || !local.IsValid()) return;
        bool near = (local.GetPosition() - transform.position).magnitude < activeRange
                 || (ball != null && ball.gameObject.activeInHierarchy && (ball.transform.position - transform.position).magnitude < activeRange);

        UpdatePupils(_state != 0, BallLive() ? ball.transform.position : local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position, dt);
        float swell = 0f, squint = 1f, shake = 0f;
        // ゴールしたら：巣のまん中（最初の場所）へ戻って、また眠る（ゴールしている間は起きない）
        bool finished = ball != null && ball.IsFinished();
        if (finished && _state != 0 && _state != 4)
        {
            if (warnText != null) warnText.gameObject.SetActive(false);
            if (sneezeText != null) sneezeText.gameObject.SetActive(false);
            _inHop = false; _hopClock = 0f; _push = Vector3.zero;
            SetState(4);
        }
        if (_state == 0)
        {
            squint = 0.03f;
            if (near && !finished) { _wait = interval * 0.6f; SetState(1); }
        }
        else if (_state == 4)
        {
            squint = 0.5f;
            Vector3 p = transform.position; Vector3 to = _startPos - p; to.y = 0f;
            float d = to.magnitude;
            if (d < 0.05f)
            {
                transform.position = new Vector3(_startPos.x, _home.y, _startPos.z);
                transform.rotation = Quaternion.Slerp(transform.rotation, _startRot, 1f - Mathf.Exp(-4f * dt));
                if (_t > 0.6f && Quaternion.Angle(transform.rotation, _startRot) < 2f) { transform.rotation = _startRot; SetState(0); }
            }
            else
            {
                // ぴょんぴょん跳ねて戻る（少し速め）
                _hopClock += dt;
                float x = Mathf.Repeat(_hopClock / (hopTime * 0.8f), 1f);
                Vector3 step = to.normalized * Mathf.Min(d, (hopDistance / (hopTime * 0.8f)) * 1.3f * dt);
                p += step; p.y = _home.y + hopHeight * 0.6f * 4f * x * (1f - x);
                transform.position = p;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), 1f - Mathf.Exp(-4f * dt));
                _t = 0f;
            }
        }
        else if (_state == 1)
        {
            squint = 1f;   // 起きたら目をぱっちり
            if (!near && _t > 2f) SetState(0);
            else if (useSneeze && _t > _wait)
            {
                SetState(2);
                if (warnText != null) warnText.gameObject.SetActive(true);
                if (inhaleSound != null) inhaleSound.Play();
            }
        }
        else if (_state == 2)
        {
            float k = Mathf.Clamp01(_t / telegraph);
            swell = 0.16f * k;                 // 息を吸ってふくらむ
            shake = 0.02f * k;
            squint = Mathf.Lerp(0.55f, 0.08f, k);
            if (warnText != null) warnText.localScale = Vector3.one * (0.85f + 0.3f * k);
            if (_t > telegraph) Sneeze();
        }
        else if (_state == 3)
        {
            float k = Mathf.Clamp01(_t / 0.5f);
            swell = -0.12f * Mathf.Sin(k * Mathf.PI);   // くしゃみでぺこっ
            squint = 0.08f;
            if (_t > 0.9f)
            {
                if (sneezeText != null) sneezeText.gameObject.SetActive(false);
                _wait = interval + Random.Range(-intervalJitter, intervalJitter);
                SetState(1);
            }
        }

        if (_state != 0 && _state != 4) Move(local, dt);
        float sq = Time.time < _squashUntil ? Mathf.Sin((_squashUntil - Time.time) / 0.3f * Mathf.PI) * 0.12f : 0f;
        swell -= sq;
        swell -= _crouch * 0.1f;

        // 見た目：呼吸＋ため
        float br = Mathf.Sin(Time.time * (_state == 0 ? 1.2f : 2f)) * 0.03f;
        if (visual != null)
        {
            float s = 1f + swell + br;
            visual.localScale = new Vector3(_visualScale.x * s, _visualScale.y * (1f + swell * 0.7f - br), _visualScale.z * s);
            visual.localPosition = _visualBase + (shake > 0f ? Random.insideUnitSphere * shake : Vector3.zero);
        }
        if (eyes != null) eyes.localScale = new Vector3(_eyesScale.x, _eyesScale.y * squint, _eyesScale.z);
        if (zzz != null)
        {
            // 鼻ちょうちん：息を吐くとふくらんで、吸うとしぼむ
            zzz.gameObject.SetActive(_state == 0);
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.2f);
            float g = Mathf.Lerp(0.2f, 1f, k * k);
            zzz.localScale = _zzzScale * g;
            zzz.localPosition = _zzzBase + Vector3.forward * 0.5f * _zzzScale.z * g;
        }
    }

    private void Sneeze()
    {
        SetState(3);
        if (warnText != null) warnText.gameObject.SetActive(false);
        if (sneezeText != null) sneezeText.gameObject.SetActive(true);
        if (sneezeFx != null) sneezeFx.Play();
        if (sneezeSound != null) sneezeSound.Play();
        Transform m = mouth != null ? mouth : transform;
        Vector3 fwd = m.forward; fwd.y = 0f; fwd.Normalize();

        if (ball != null && ball.gameObject.activeInHierarchy)
        {
            float f = Falloff(m.position, fwd, ball.transform.position);
            if (f > 0f) ball.Blow(fwd * ballPush * f + Vector3.up * ballUp * f);
        }
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local != null && local.IsValid())
        {
            float f = Falloff(m.position, fwd, local.GetPosition() + Vector3.up * 0.8f);
            if (f > 0f) local.SetVelocity(local.GetVelocity() * 0.3f + fwd * playerPush * f + Vector3.up * playerUp * f);
        }
    }

    // 円すいの中なら、近いほど強い（1〜0.45）。外なら0
    private float Falloff(Vector3 origin, Vector3 fwd, Vector3 p)
    {
        Vector3 to = p - origin;
        Vector3 flat = to; flat.y = 0f;
        float d = flat.magnitude;
        if (d > range || d < 0.01f) return d < 0.01f ? 1f : 0f;
        if (Mathf.Abs(to.y) > 2.5f) return 0f;
        if (Vector3.Angle(fwd, flat) > coneAngle) return 0f;
        return Mathf.Lerp(1f, 0.45f, d / range);
    }

    private Vector3 _pupilBaseL, _pupilBaseR;
    private Vector2 _look, _lookGoal;
    private float _nextGlance;
    private bool _glancing;
    private Vector2 _glanceGoal;

    private void InitPupils()
    {
        if (pupilL != null) _pupilBaseL = pupilL.localPosition;
        if (pupilR != null) _pupilBaseR = pupilR.localPosition;
    }

    // 黒目：見る相手がいればそっち、いなければときどき、きょろきょろ
    private void UpdatePupils(bool watch, Vector3 target, float dt)
    {
        if (pupilL == null || pupilR == null) return;
        Transform basis = pupilL.parent;
        // 体ごとこっちを向くので、見つめるだけだと黒目がほぼ真ん中で止まって見える。
        // 見ている時も、ときどき目をそらして（きょろっ）すぐ戻す。上下の差は大きめに出す
        if (Time.time > _nextGlance)
        {
            _glancing = !watch || Random.value < 0.45f;
            _glanceGoal = Random.insideUnitCircle.normalized * Random.Range(0.6f, 1f);
            _nextGlance = Time.time + (_glancing ? Random.Range(0.35f, 0.8f) : Random.Range(0.8f, 2.0f));
        }
        if (watch && !_glancing && basis != null)
        {
            Vector3 d = basis.InverseTransformDirection(target - pupilL.position);
            Vector2 g = new Vector2(d.x, d.y) / Mathf.Max(0.05f, Mathf.Abs(d.z) + 0.3f);
            _lookGoal = Vector2.ClampMagnitude(g * 2.5f, 1f);
        }
        else _lookGoal = _glanceGoal;
        _look = Vector2.Lerp(_look, _lookGoal, 1f - Mathf.Exp(-18f * dt));
        SetPupil(pupilL, _pupilBaseL);
        SetPupil(pupilR, _pupilBaseR);
    }

    private void SetPupil(Transform p, Vector3 b)
    {
        float r = Mathf.Clamp01(_look.magnitude);
        p.localPosition = b + new Vector3(_look.x * pupilReach, _look.y * pupilReach, -pupilDepth * (1f - Mathf.Sqrt(Mathf.Max(0f, 1f - r * r * 0.8f))));
    }

    // 狙うのは運んでいる最中のふわふわ（待ってる・かくれてる・持ってる・ゴール後はプレイヤー）
    private bool BallLive()
    {
        return targetBall && ball != null && ball.gameObject.activeInHierarchy && !ball.IsWaitingOrHidden() && !ball.IsCarried() && !ball.IsFinished();
    }

    // ゆっくり追いかける（巣の外には出ない）。押された分はだんだん止まる
    private void Move(VRCPlayerApi local, float dt)
    {
        Vector3 pos = transform.position;
        bool ballLive = BallLive();
        Vector3 to = (ballLive ? ball.transform.position : local.GetPosition()) - pos; to.y = 0f;
        Vector3 step = Vector3.zero;
        float lift = 0f;
        _crouch = 0f;
        if (hop)
        {
            // ゆったりジャンプ：休む→ちょっとかがむ→ぴょーん→着地でむにっ
            _hopClock += dt;
            if (!_inHop)
            {
                float rest = hopRest;
                if (_hopClock > rest - 0.35f) _crouch = Mathf.Clamp01((_hopClock - (rest - 0.35f)) / 0.35f);
                if (_hopClock > rest && to.magnitude > bodyRadius * 0.9f)
                {
                    _inHop = true; _hopClock = 0f;
                    _hopDir = to.normalized;
                }
            }
            else
            {
                float x = Mathf.Clamp01(_hopClock / hopTime);
                step = _hopDir * (hopDistance / hopTime) * dt;
                lift = hopHeight * 4f * x * (1f - x);
                if (x >= 1f) { _inHop = false; _hopClock = 0f; _squashUntil = Time.time + 0.35f; }
            }
        }
        else if (to.magnitude > bodyRadius * 0.9f) step = to.normalized * chaseSpeed * dt;
        step += _push * dt;
        _push *= Mathf.Exp(-pushDamping * dt);
        pos += step;
        Vector3 off = pos - _home; off.y = 0f;
        if (off.magnitude > moveRadius) pos = _home + off.normalized * moveRadius;
        pos.y = _home.y + lift;
        transform.position = pos;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), 1f - Mathf.Exp(-3f * dt));

        // 人にぶつかったら大きめに吹っ飛ばす
        Vector3 c = pos + Vector3.up * bodyRadius;
        Vector3 feet = local.GetPosition();
        Vector3 flat = feet - pos; flat.y = 0f;
        if (Time.time > _bumpUntil && flat.magnitude < bodyRadius + 0.35f && feet.y < c.y + bodyRadius)
        {
            Vector3 away = flat.sqrMagnitude > 0.0001f ? flat.normalized : -transform.forward;
            local.SetVelocity(away * knockback + Vector3.up * knockUp);
            _bumpUntil = Time.time + 0.6f;
            _squashUntil = Time.time + 0.3f;
            if (bumpSound != null) bumpSound.Play();
        }
        // ふわふわ：運んでいる最中なら大きめに吹っ飛ばす（道の外まで飛べばアウト）。それ以外はやさしく押しのける
        if (ball != null && ball.gameObject.activeInHierarchy)
        {
            Vector3 bd = ball.transform.position - c;
            if (bd.magnitude < bodyRadius + 0.3f)
            {
                Vector3 bf = bd; bf.y = 0f;
                bf = bf.sqrMagnitude > 0.0001f ? bf.normalized : transform.forward;
                if (ballLive)
                {
                    if (Time.time > _ballHitUntil)
                    {
                        ball.LaunchTinted(bf * ballKnockback + Vector3.up * ballKnockUp, ballHitGlow);
                        _ballHitUntil = Time.time + ballHitCooldown;
                        _squashUntil = Time.time + 0.3f;
                        if (bumpSound != null) bumpSound.Play();
                    }
                }
                else ball.Blow(bf * ballBump * dt * 8f + Vector3.up * 0.5f * dt * 8f);
            }
        }
    }

    // アウト・リスポーン・コース開始の時（FuwaBall から）：最初の場所で寝ている状態に戻す
    public void ResetBoss()
    {
        Init();
        transform.position = _startPos;
        transform.rotation = _startRot;
        _push = Vector3.zero;
        _inHop = false; _hopClock = 0f;
        if (warnText != null) warnText.gameObject.SetActive(false);
        if (sneezeText != null) sneezeText.gameObject.SetActive(false);
        SetState(0);
    }
}
