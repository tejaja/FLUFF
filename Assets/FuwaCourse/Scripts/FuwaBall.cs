using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 風で運ぶ「ふわふわ」本体。同期しないので、各プレイヤーのPCに1個ずつ存在する。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[RequireComponent(typeof(Rigidbody))]
public class FuwaBall : UdonSharpBehaviour
{
    [Header("ふわふわの動き")]
    [Tooltip("落ちる加速度（重力の代わり）")]
    public float fallAcceleration = 1.5f;
    [Tooltip("落下速度の上限")]
    public float maxFallSpeed = 0.7f;
    [Tooltip("全体の速度上限")]
    public float maxSpeed = 7f;
    [Tooltip("空気抵抗")]
    public float airDrag = 1.2f;

    [Header("柵の上で止まらない（2026-10-12 てじゃ「柵にちょうど乗っかって止まる時がある」）")]
    [Tooltip("名前にこれを含む物（FenceCollider など）の上に乗って止まったら、近くの道（島）の方へ転がす")]
    public string perchName = "Fence";
    [Tooltip("乗ったまま、この秒数ほとんど動かなかったら転がす")]
    public float perchDelay = 0.35f;
    [Tooltip("転がす速さ(m/s)")]
    public float perchPush = 0.9f;
    [Tooltip("道を探す時に横へずらす距離(m)")]
    public float perchProbe = 0.8f;
    private float _perchSeen = -1f, _perchStart = -1f;

    [Header("リスポーン")]
    public Transform startPoint;
    [Tooltip("この高さより下に落ちたらアウト（ステージ1）")]
    public float killHeight = -3f;
    [Tooltip("ステージ2（つぎのしま、ステージ1の真下170m）でのアウトの高さ。2026-10-17")]
    public float stage2KillHeight = -200f;
    private int _stage = 1;
    [Tooltip("コースアウト判定：真下に道が無く、最後に通った道の高さよりこれだけ下がったらアウト(m)")]
    public float offCourseDrop = 0.8f;
    private float _lastGroundY = -10000f;
    [Tooltip("コースアウト判定：真下に道が無くても、再開位置よりこれだけ下ならどこでもアウト(m)")]
    public float maxDrop = 3f;
    [Tooltip("この範囲の中ではコースアウト判定をしない（崖ダイブの谷の上など。底のアウトは別のトゲ等で）")]
    public Collider[] gapZones;
    [Tooltip("この範囲の中では「再開位置より maxDrop 下でアウト」を見ない（最後に通った道より offCourseDrop 下でアウトだけ）。崖の下の広い岩場など、はみ出しても沈むまでは押し戻せるように")]
    public Collider[] lowZones;
    [Tooltip("真下の道を探す距離(m)")]
    public float groundProbe = 12f;
    private float _nextOffCourseCheck;
    [Tooltip("アウトになってから戻るまでの秒数")]
    public float resetDelay = 0.8f;
    [Tooltip("ゴール後、スタートに戻るまでの秒数")]
    public float restartDelay = 5f;
    [Tooltip("タイムを計るか（ロビーではOFFになる）")]
    public bool timerEnabled = true;
    [Tooltip("このレイヤーの物（コースの道など）に触ったらアウト。11=Environment")]
    public int groundLayer = 11;

    [Header("表示（なくてもOK）")]
    public TextMeshPro statusText;
    public TextMeshPro timerText;

    [Header("効果音（自分にだけ聞こえる。なくてもOK）")]
    public AudioSource sfxCheckpoint;
    public AudioSource sfxGoal;
    public AudioSource sfxOut;
    public AudioSource sfxFell;
    [Tooltip("アウトの時に弾け飛ぶ毛（なくてもOK）")]
    public ParticleSystem popEffect;
    public AudioSource sfxPop;
    [Tooltip("クモの巣にくっついた瞬間の音")]
    public AudioSource sfxStick;
    [Tooltip("撃たれた時の「ポヨン」（強く撃つほど低く大きく）")]
    public AudioSource sfxBoing;
    [Tooltip("弱い時の音程 / 強い時の音程")]
    public float boingPitchWeak = 1.35f;
    public float boingPitchStrong = 0.8f;
    [Tooltip("弱い時の音量 / 強い時の音量")]
    public float boingVolumeWeak = 0.35f;
    public float boingVolumeStrong = 1f;

    [Tooltip("プレイヤーがやられた・リスポーンした時に復活させるモンスター")]
    public FuwaEnemy[] enemies;
    [Tooltip("アウト・リスポーンの時に元に戻すボス（ママけだま）")]
    public FuwaMama[] bosses;

    [Tooltip("最速タイムの看板（なくてもOK）")]
    public FuwaRecords records;
    [Tooltip("ゴールでファンファーレを鳴らすため（なくてもOK）")]
    public FuwaBgm bgm;

    [Header("ミスした時のプレイヤーの戻し方と暗転")]
    [Tooltip("暗転（なくてもOK）")]
    public FuwaFade fade;
    [Tooltip("玉の再開位置から見て、どれだけ後ろに人を戻すか(m)")]
    public float playerBackDistance = 1.2f;
    [Tooltip("玉の再開位置から足元までの高さ(m)")]
    public float playerHeightBelow = 1.3f;
    [Tooltip("ふわふわがアウトになった時にプレイヤーもチェックポイントへ戻す（タイムを計るコースだけ）")]
    public bool movePlayerOnOut = true;
    [Tooltip("タイムを計らないコースでも、このスタートから始めた時はアウトでプレイヤーも戻す（れんしゅうの最後の本番の道）")]
    public Transform[] returnPlayerStarts;
    private bool _untimedRun;   // タイムを計らない道での挑戦中（IsInRun）
    private bool _practice;   // れんしゅうの「ためし撃ちの場所」にいる間（プレイヤーは動かさない）
    private bool _teleportOnReset;

    [HideInInspector] public Transform respawnPoint;
    [HideInInspector] public bool english;   // FuwaLanguageが切り替える
    private bool _softStick;

    private Rigidbody _rb;
    private bool _waiting;   // 吹かれるまで空中で止まって待ってる
    private bool _running;   // タイム計測中
    private bool _finished;
    private float _startTime;
    private float _resetAt = -1f;
    private float _messageUntil;
    private float _gravityScale;

    [Header("回転")]
    [Tooltip("1回吹かれた時の転がる回転の強さ(rad/s)")]
    public float spinPerPuff = 4f;
    [Tooltip("1回吹かれた時のランダムなひねり(rad/s)")]
    public float spinRandom = 1.5f;
    [Tooltip("回転の減り方（小さいほど長く回る）")]
    public float spinDamping = 0.6f;
    [Tooltip("待機中にゆっくり回る速さ(度/秒)")]
    public float idleSpin = 25f;

    [Header("毛のなびき（毛玉シェーダー用、なくてもOK）")]
    public Renderer furRenderer;
    [Tooltip("速度に対して毛先が後ろに流れる量")]
    public float furDragAmount = 0.025f;
    public float furDragMax = 0.12f;
    private Material _furMat;
    private Vector3 _furDrag;

    [Header("風などの影響を受けている間の光（毛玉シェーダー用）")]
    public Color windGlowColor = new Color(0.15f, 0.6f, 1f, 1f);   // 空の水色に溶けないよう濃いめの青
    public Color gravityGlowColor = new Color(0.7f, 0.3f, 1f, 1f);
    public Color launchGlowColor = new Color(1f, 0.6f, 0.2f, 1f);
    [Tooltip("横風がこの強さ(加速度)以上なら一番明るく光る")]
    public float windGlowFull = 3f;
    [Tooltip("チェックポイントで浮いて待ってる間だけ、ふわふわの上に出す▼（なくてもOK。ふわふわの子にはしない）")]
    public Transform waitMarker;
    [Tooltip("▼の高さ（ふわふわの大きさの何倍上か）")]
    public float waitMarkerHeight = 1.2f;
    [Tooltip("発射台で飛ばされてから光っている秒数")]
    public float launchGlowTime = 0.9f;
    private Color _glowColor;
    private bool _launchGlow;   // 今の光が打ち出しの光か
    private Vector3 _glowDir = Vector3.up;
    private float _glowAmount;       // 今の光の強さ（なめらかに変化）
    private float _glowTarget;       // 目標の強さ
    private float _glowUntil;        // この時刻まで目標を保つ
    private float _glowLongUntil;    // 長く保つ指定（0.15秒より長い）の光がこの時刻まで残っている
    private int _glowFrame = -1;     // 最後に光を指定したフレーム

    [Header("スタートのキノコ")]
    [Tooltip("コースのスタートでは胞子をキノコの中に隠し、キノコを撃つと飛び出す")]
    public bool hideAtStart = true;
    [Tooltip("飛び出す時、撃たれた向きへの勢い(m/s)")]
    public float releasePush = 2.2f;
    [Tooltip("飛び出す時、上への勢い(m/s)")]
    public float releaseUp = 2.0f;
    private bool _hidden;
    private FuwaGoal _goalZone;   // 今いるゴールのリングの中（着地したらゴール）
    private bool _stuck;          // クモの巣にくっついている間
    private bool _carried;        // 上昇気流などに運ばれている間（位置は運ぶ側が決める）

    [Header("アウトで破裂する時")]
    [Tooltip("ぷくーっと膨らんでから弾けるまでの秒数（0なら即破裂）")]
    public float inflateTime = 0.18f;
    [Tooltip("弾ける直前の大きさ（倍）")]
    public float inflateScale = 1.45f;
    private float _inflateStart = -1f;
    private Vector3 _baseScale;
    private Vector3 _inflatePos;     // 膨らみ始めた時の中心
    private float _inflateRadius;    // 膨らむ前の半径(m)
    private Vector3 _anchorDir = Vector3.down;   // 膨らむ時に動かさない側（中心から見た向き）。普段は下＝接地面
    private Vector3 _inflateAnchor = Vector3.down;

    // 次の破裂で膨らむ時に、どちら側を動かさないか（クモの巣なら巣に当たっている側）
    public void SetInflateAnchor(Vector3 dirFromCenter)
    {
        if (dirFromCenter.sqrMagnitude > 0.0001f) _anchorDir = dirFromCenter.normalized;
    }

    private void Start()
    {
        _baseScale = transform.localScale;
        if (furRenderer != null) _furMat = furRenderer.material;
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.drag = airDrag;
        _rb.angularDrag = spinDamping;
        _rb.maxAngularVelocity = 12f;
        respawnPoint = startPoint != null ? startPoint : transform;
        ResetToRespawn();
        _ballPosId = VRCShader.PropertyToID("_UdonFuwaBallPos");
    }

    // 自分のふわふわの位置をシェーダーへ（チュートリアルの「ふわふわだけ止める壁」が、ふわふわの近くだけ浮かび上がるのに使う）。
    // w は見えている時 1、隠れている時 0
    private int _ballPosId;
    private void ShareBallPos()
    {
        Vector3 p = transform.position;
        VRCShader.SetGlobalVector(_ballPosId, new Vector4(p.x, p.y, p.z, (_hidden || _resetAt > 0f) ? 0f : 1f));
    }

    // 直前の物理の1歩での速さ（当たった瞬間の向きを見る。OnCollisionEnter の時点の速さは当たったあとの値）
    private Vector3 _fixedVel;
    private void FixedUpdate()
    {
        _fixedVel = _rb.velocity;
        if (_waiting || _stuck || _carried) return;

        // 重くなるゾーンから届いた倍率（届いてなければ1倍）
        float g = _gravityScale > 0f ? _gravityScale : 1f;
        _gravityScale = 0f;

        _rb.AddForce(Vector3.down * fallAcceleration * g, ForceMode.Acceleration);
        UpdatePerch();
        // エアガンを溜めている間のささやかな引き寄せ（FuwaBlower.SetChargePull から毎フレーム届く）
        if (Time.time < _pullUntil) _rb.AddForce(_pullAcc, ForceMode.Acceleration);

        // 発射台の「遠くへ飛ばす」打ち出しの間は、空気抵抗を弱くして速さの上限もかけない
        bool flying = Time.time < _flightUntil;
        _rb.drag = flying ? _flightDrag : airDrag;
        if (flying) return;

        Vector3 v = _rb.velocity;
        if (v.y < -maxFallSpeed * g) v.y = -maxFallSpeed * g;
        if (v.magnitude > maxSpeed) v = v.normalized * maxSpeed;
        _rb.velocity = v;
    }

    private void Update()
    {
        if (_furMat != null)
        {
            // 動きと反対向きに毛先を流す（なめらかに追従）
            Vector3 target = _waiting ? Vector3.zero : Vector3.ClampMagnitude(-_rb.velocity * furDragAmount, furDragMax);
            _furDrag = Vector3.Lerp(_furDrag, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            _furMat.SetVector("_Drag", new Vector4(_furDrag.x, _furDrag.y, _furDrag.z, 0f));
            UpdateGlow();
        }
        if (_stuck) UpdateStuckShake();
        UpdateAppear();
        ShareBallPos();
        UpdateInflate();
        UpdateSquash();
        // 待機中（空中で止まってる間）もゆっくり回して、ふわふわ感を出す
        if (_waiting) transform.Rotate(new Vector3(0.3f, 1f, 0.15f), idleSpin * Time.deltaTime, Space.World);
        // チェックポイントで浮いて待ってる間だけ、上に▼を出す（スタートのキノコに隠れてる時は出さない）
        if (waitMarker != null)
        {
            bool show = _waiting && !_hidden && _resetAt < 0f;
            if (waitMarker.gameObject.activeSelf != show) waitMarker.gameObject.SetActive(show);
            if (show) waitMarker.position = transform.position + Vector3.up * (transform.lossyScale.y * waitMarkerHeight);
        }
        if (_resetAt > 0f && Time.time >= _resetAt)
        {
            StopInflate();
            if (_teleportOnReset) TeleportPlayerToRespawn();
            _teleportOnReset = false;
            ResetToRespawn(); ReviveEnemies();   // アウトで戻ったら、モンスターも家に戻す
        }
        if (!_waiting && !_carried && _resetAt < 0f && transform.position.y < (_stage == 2 ? stage2KillHeight : killHeight)) Fail();
        CheckOffCourse();

        if (timerText != null)
        {
            if (_running) timerText.text = Mono(FormatTime(Time.time - _startTime));
            else if (!_finished) timerText.text = "";
        }
        if (statusText != null && _messageUntil > 0f && Time.time > _messageUntil)
        {
            statusText.text = "";
            _messageUntil = 0f;
        }
    }

    // 風の道具から呼ばれる（瞬間的な速度変化）
    public void Blow(Vector3 velocityChange)
    {
        if (_resetAt > 0f || _hidden || _stuck || _spiking) return;
        if (_waiting)
        {
            _waiting = false;
            _rb.isKinematic = false;
            // 浮いて待っている玉を撃ったら（チェックポイントからの再開など）倒したモンスターを復活させる。
            // 玉を置いたまま先にモンスターを掃除しておく、ができないように
            ReviveEnemies();
            if (timerEnabled && !_running && !_finished && respawnPoint == startPoint)
            {
                _running = true;
                _startTime = Time.time;
            }
            // タイムを計らない道（チュートリアルの本番）でも「挑戦中」は覚えておく（名場面のフォトポイント用。2026-10-12 てじゃ「チュートリアル後半の写真とれてない」）
            else if (!timerEnabled && !_untimedRun && !_finished && respawnPoint == startPoint)
            {
                _untimedRun = true;
                _startTime = Time.time;
            }
        }
        // 落ちてる最中に下から吹かれたら、落下をいったん止めてから持ち上げる（ふわっと受け止める感じ）
        Vector3 v = _rb.velocity;
        if (v.y < 0f && velocityChange.y > 0f)
        {
            v.y = 0f;
            _rb.velocity = v;
        }
        _rb.AddForce(velocityChange, ForceMode.VelocityChange);

        StartSquash(velocityChange);
        EmitShotPuff(velocityChange);

        // 吹かれた向きに転がるように回して、少しだけランダムにひねる
        Vector3 flat = new Vector3(velocityChange.x, 0f, velocityChange.z);
        Vector3 axis = flat.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.zero;
        _rb.AddTorque(axis * spinPerPuff + Random.insideUnitSphere * spinRandom, ForceMode.VelocityChange);
    }

    // エアガンを溜めている間、銃口の方へごく弱く引き寄せる（届かなくなったら0.1秒で切れる）
    private Vector3 _pullAcc;
    private float _pullUntil;
    public void SetChargePull(Vector3 acceleration)
    {
        if (_waiting || _hidden || _stuck || _resetAt > 0f) return;
        _pullAcc = acceleration;
        _pullUntil = Time.time + 0.1f;
    }

    // ぽよんと跳ねる物（FuwaBounce）に当たった時。表面から離れる向きの速さを bounceSpeed 以上にする
    private float _nextBounceSound;
    private void BounceOff(FuwaBounce b)
    {
        BounceOffNormal(b, b.GetNormal(transform.position));
    }

    private void BounceOffNormal(FuwaBounce b, Vector3 n)
    {
        if (_resetAt > 0f || _hidden || _stuck || _waiting) return;
        if (n.sqrMagnitude < 0.0001f) n = Vector3.up;
        n.Normalize();
        Vector3 v = _rb.velocity;
        float vn = Vector3.Dot(v, n);
        Vector3 tangent = (v - n * vn) * b.keepTangent;
        Vector3 nv = tangent + n * Mathf.Max(b.bounceSpeed, -vn * 0.7f);
        _rb.velocity = nv;
        Vector3 change = nv - v;
        StartSquash(change);
        if (sfxBoing != null && sfxBoing.clip != null && Time.time >= _nextBounceSound)
        {
            _nextBounceSound = Time.time + 0.15f;
            sfxBoing.pitch = boingPitchWeak * Random.Range(0.96f, 1.04f);
            sfxBoing.PlayOneShot(sfxBoing.clip, boingVolumeWeak);
        }
    }

    [Header("撃った直後は風の効きが弱まる（つむじ風から抜けやすく）")]
    [Tooltip("ふつう撃ち／フルチャージで、風の効きを何割弱めるか")]
    [Range(0f, 1f)] public float shotWindDampMin = 0.35f;
    [Range(0f, 1f)] public float shotWindDampMax = 0.85f;
    [Tooltip("フルチャージで、弱めたまま保つ秒数（溜め具合の2乗で伸びる。ふつう撃ちは0）")]
    public float shotWindDampHoldMax = 0.9f;
    [Tooltip("ふつう撃ち／フルチャージで、保ったあと元に戻るまでの秒数")]
    public float shotWindDampTimeMin = 0.35f;
    public float shotWindDampTimeMax = 0.8f;
    private float _windDampStart = -10f;
    private float _windDampHold = 0f;
    private float _windDampTime = 0f;
    private float _windDampAmount = 0f;
    private Vector3 _windDampDir = Vector3.zero;
    [Tooltip("風の効きが弱まっている間の光の色（溜めて撃った時だけ。ふつう撃ちは光らない）")]
    public Color resistGlowColor = new Color(1f, 0.78f, 0.15f, 1f);
    [Tooltip("フルチャージで撃った時の光の強さ（溜め具合に比例）")]
    [Range(0f, 1f)] public float resistGlowMax = 1f;
    [Tooltip("光が消えるまでの秒数（撃った瞬間が一番明るく、2乗のカーブでなめらかに消える。体感は6割くらいの時間で消える。風の弱まり自体はもっと長く続く）")]
    public float resistGlowTime = 0.9f;
    private float _windDampGlow = 0f;

    // エアガンで撃たれた時に呼ばれる（charge 0〜1、shotDir＝飛ばした向き）。しばらく風の効きを弱め（溜めるほど長く保つ）、だんだん元に戻す
    public void ShotWindDamp(float charge, Vector3 shotDir)
    {
        _windDampStart = Time.time;
        _windDampAmount = Mathf.Lerp(shotWindDampMin, shotWindDampMax, charge);
        _windDampHold = shotWindDampHoldMax * charge * charge;
        _windDampTime = Mathf.Lerp(shotWindDampTimeMin, shotWindDampTimeMax, charge);
        _windDampDir = shotDir.sqrMagnitude > 0.0001f ? shotDir.normalized : Vector3.zero;
        _windDampGlow = resistGlowMax * charge;
        _lastShotCharge = charge;
    }

    // 最後に撃たれた時の溜め具合・経過秒数・向き（つむじ風が「溜めて撃たれたら放す」のに使う。2026-10-13）
    private float _lastShotCharge;
    public float LastShotCharge() { return _lastShotCharge; }
    public float LastShotAge() { return Time.time - _windDampStart; }
    public Vector3 LastShotDir() { return _windDampDir; }

    // 風の弱まりの残り（保っている間は1、戻る間に1→0）
    private float WindDampLeft()
    {
        float t = Time.time - _windDampStart - _windDampHold;
        if (_windDampTime <= 0f || t >= _windDampTime) return 0f;
        return 1f - Mathf.Max(0f, t) / _windDampTime;
    }

    // 撃った向きと同じ向きの風（追い風など）はそのまま、横向き・逆向きの分だけ弱める
    private Vector3 DampWind(Vector3 acc)
    {
        float left = WindDampLeft();
        if (left <= 0f) return acc;
        float k = 1f - _windDampAmount * left;
        float along = Vector3.Dot(acc, _windDampDir);
        Vector3 keep = along > 0f ? _windDampDir * along : Vector3.zero;
        return keep + (acc - keep) * k;
    }

    // 風ゾーンから毎フレーム呼ばれる（加速度）
    // 今いるステージ（ワープ FuwaCourseWarp.stage から）。アウトの高さを切り替える
    public void SetStage(int stage) { if (stage > 0) _stage = stage; }

    // 水（FuwaWater）から：浮力・水の抵抗・流れの加速度。風と違って撃った直後も弱めない、光らせない
    public void AddWater(Vector3 acceleration)
    {
        if (_waiting || _resetAt > 0f || _stuck) return;
        _rb.AddForce(acceleration, ForceMode.Acceleration);
        _waterAt = Time.time;
    }
    // 最後に水に浮いていた時刻（水に浮いたまま道のふちに寄った時はアウトにしない）
    private float _waterAt = -10f;
    private bool InWaterNow() { return Time.time - _waterAt < 0.2f; }
    public Vector3 GetVelocity() { return _rb != null ? _rb.velocity : Vector3.zero; }
    public float GetFallAcceleration() { return fallAcceleration; }
    public float GetDrag() { return _rb != null ? _rb.drag : airDrag; }

    public void AddWind(Vector3 acceleration)
    {
        if (_waiting || _resetAt > 0f || _stuck) return;
        _rb.AddForce(DampWind(acceleration), ForceMode.Acceleration);
        float mag = acceleration.magnitude;
        if (mag > 0.05f) SetGlow(windGlowColor, acceleration / mag, WindGlowCurve(mag / windGlowFull), 0.1f);
    }

    // 風の光の強さ：弱い風でも見えるように平方根で持ち上げる（0.2→0.45、0.5→0.71、1→1）
    private float WindGlowCurve(float x)
    {
        return Mathf.Sqrt(Mathf.Clamp01(x));
    }

    // 風の力はそのままかけて、光る向き・強さだけ指定する（つむじ風に捕まっている時など、力の向きがころころ変わる時用）
    public void AddWindGlowDir(Vector3 acceleration, Vector3 glowDir, float glowAmount)
    {
        if (_waiting || _resetAt > 0f || _stuck) return;
        _rb.AddForce(DampWind(acceleration), ForceMode.Acceleration);
        if (glowDir.sqrMagnitude > 0.0001f) SetGlow(windGlowColor, glowDir.normalized, WindGlowCurve(glowAmount), 0.1f);
    }

    // 発射台から呼ばれる（速度をそのまま上書き）
    public void Launch(Vector3 velocity)
    {
        LaunchTinted(velocity, launchGlowColor);
    }

    // 光る色を指定して打ち出す（風の穴は緑、転がる岩はふつうのオレンジ）
    public void LaunchTinted(Vector3 velocity, Color glow)
    {
        if (_waiting || _resetAt > 0f || _finished || _stuck) return;
        _rb.velocity = velocity;
        _flightUntil = 0f;
        if (velocity.sqrMagnitude > 0.0001f) { _launchGlow = false; SetGlow(glow, velocity.normalized, 1f, launchGlowTime); _launchGlow = true; }
    }

    // 発射台から呼ばれる（遠くへ飛ばす版）。seconds 秒のあいだ空気抵抗を drag にして、速さの上限もかけない
    private float _flightUntil;
    private float _flightDrag;
    public void LaunchFlight(Vector3 velocity, float seconds, float drag)
    {
        LaunchFlightTinted(velocity, seconds, drag, launchGlowColor);
    }

    public void LaunchFlightTinted(Vector3 velocity, float seconds, float drag, Color glow)
    {
        if (_waiting || _resetAt > 0f || _finished || _stuck) return;
        LaunchTinted(velocity, glow);
        _flightUntil = Time.time + seconds;
        _flightDrag = drag;
    }

    // 重くなるゾーンから毎フレーム呼ばれる（次の物理更新だけ落下が scale 倍になる）
    public void SetGravityScale(float scale)
    {
        if (scale > _gravityScale) _gravityScale = scale;
        if (scale > 1f && !_waiting && !_hidden && _resetAt < 0f) SetGlow(gravityGlowColor, Vector3.down, 1f, 0.1f);
    }

    // 滝（FuwaWaterfall）から：重くするのは同じ、光る色だけ水色に（重くなるゾーンの紫と見分ける）
    public void SetGravityScaleTint(float scale, Color tint)
    {
        if (scale > _gravityScale) _gravityScale = scale;
        if (scale > 1f && !_waiting && !_hidden && _resetAt < 0f) SetGlow(tint, Vector3.down, 0.8f, 0.1f);
    }

    // ほかの物から光らせる（貝の泡の無敵時間の点滅など、2026-10-25）
    public void Glow(Color color, Vector3 dir, float amount, float hold)
    {
        if (_waiting || _hidden || _resetAt > 0f) return;
        SetGlow(color, dir, amount, hold);
    }

    // 光らせる（色・向き・強さ・保つ秒数）。発射台の光っている間は、弱い横風などで上書きしない
    private void SetGlow(Color color, Vector3 dir, float amount, float hold)
    {
        // 打ち出しの光（_launchGlow）が残っている間は、ほかの弱い光で上書きしない
        if (_launchGlow && Time.time < _glowUntil) return;
        _launchGlow = false;
        _glowColor = color;
        _glowDir = dir;
        // 同じフレームの中では一番強いものを使う。毎フレーム呼ばれる弱まっていく風（強風の終わりなど）は、
        // 次のフレームで弱い値に下げてよい（長く保つ指定の光が残っている間は下げない）
        bool newFrame = _glowFrame != Time.frameCount;
        if (Time.time >= _glowUntil || amount >= _glowTarget || (newFrame && Time.time >= _glowLongUntil)) _glowTarget = amount;
        _glowFrame = Time.frameCount;
        _glowUntil = Mathf.Max(_glowUntil, Time.time + hold);
        if (hold > 0.15f) _glowLongUntil = Mathf.Max(_glowLongUntil, Time.time + hold);
    }

    // 影響がある間はすっと光り、終わったらゆっくり消える
    private void UpdateGlow()
    {
        float target = Time.time < _glowUntil && !_hidden ? _glowTarget : 0f;
        // 溜めて撃って風の効きが弱まっている間は、黄色く光る（撃った向きに光の帯が流れる）。
        // 撃った瞬間が一番明るく、(1-u)^2 でなめらかに消える（終わりぎわがパッと消えないように）
        Color gc = _glowColor; Vector3 gd = _glowDir;
        float u = resistGlowTime > 0f ? Mathf.Clamp01((Time.time - _windDampStart) / resistGlowTime) : 1f;
        float resist = _hidden ? 0f : _windDampGlow * (1f - u) * (1f - u);
        if (resist > 0.001f)
        {
            Vector3 rd = _windDampDir.sqrMagnitude > 0.0001f ? _windDampDir : _glowDir;
            if (target <= 0.01f)
            {
                // ほかに光っていない：黄色のまま消える（消えたあとも色を残す）
                _glowColor = resistGlowColor; _glowDir = rd; gc = _glowColor; gd = rd;
            }
            else
            {
                // 風の中：黄色が弱まるにつれて風の色へなめらかに移る
                float mix = Mathf.Clamp01(resist / target);
                gc = Color.Lerp(_glowColor, resistGlowColor, mix); gd = mix > 0.5f ? rd : _glowDir;
            }
            target = Mathf.Max(target, resist);
        }
        float speed = target > _glowAmount ? 8f : 3f;
        _glowAmount = Mathf.MoveTowards(_glowAmount, target, speed * Time.deltaTime);
        _furMat.SetColor("_GlowColor", new Color(gc.r, gc.g, gc.b, _glowAmount));
        _furMat.SetVector("_GlowDir", new Vector4(gd.x, gd.y, gd.z, 0f));
    }

    // クモの巣にくっつく：その場でピタッと止まり、少ししたら破裂してアウト
    public void Stick(float seconds)
    {
        if (_waiting || _finished || _resetAt > 0f || _stuck) return;
        _stuck = true;
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
        _stuckPos = transform.position;
        _stuckStart = Time.time;
        _stuckDuration = Mathf.Max(0.05f, seconds);
        PlaySfx(sfxStick);
        SendCustomEventDelayedSeconds(nameof(StickPop), seconds);
    }

    public void StickPop()
    {
        if (!_stuck) return;
        _stuck = false;
        transform.position = _stuckPos;
        if (_softStick)
        {
            // アイテムのクモの糸：破裂しないではがれる
            _softStick = false;
            _rb.isKinematic = false;
            return;
        }
        Fail();
    }

    // アイテムのクモの糸：少しの間くっつくだけ（破裂しない）
    public void StickSoft(float seconds)
    {
        if (_waiting || _finished || _resetAt > 0f || _stuck || _hidden) return;
        Stick(seconds);
        _softStick = _stuck;
    }

    public bool IsWaitingOrHidden() { return _waiting || _hidden || _resetAt > 0f; }

    // アイテムなどの一言（普通の大きさ）
    public void ShowInfo(string ja, string en, float seconds)
    {
        ShowMessage(english ? en : ja, seconds);
    }

    [Header("撃たれた時のポヨン（見た目だけ。自分の玉だけ）")]
    [Tooltip("いちばん強く撃った時の潰れ具合（0.3 = 30%潰れる）")]
    public float squashAmount = 0.4f;
    [Tooltip("この速度変化で最大の潰れになる")]
    public float squashFullAt = 6f;
    [Tooltip("揺れ戻りの速さ（1秒に何回ぽよぽよするか）")]
    public float squashFrequency = 2.6f;
    [Tooltip("揺れのおさまる速さ（大きいほど早くおさまる）")]
    public float squashDamping = 2.6f;
    private float _squashStart = -10f;
    private float _squashPower;
    private Vector3 _squashDir = Vector3.up;
    private bool _squashActive;

    private void StartSquash(Vector3 velocityChange)
    {
        float m = velocityChange.magnitude;
        if (m < 0.01f) return;
        // 揺れている最中にまた撃たれたら、今の揺れより強い時だけ上書き（連射でガタガタしないように）
        // 弱い連射でも半分はポヨンとする（実機だと小さい変形は毛に埋もれて見えなかった）
        float p = squashAmount * (0.5f + 0.5f * Mathf.Clamp01(m / Mathf.Max(0.01f, squashFullAt)));
        float left = _squashActive ? _squashPower * Mathf.Exp(-squashDamping * (Time.time - _squashStart)) : 0f;
        if (p < left) return;
        // 撃った人はだいたい玉の後ろから、撃つ向きに見ているので、撃った向きに潰すと視線の方向に潰れて
        // ほとんど見えない（実機で確認）。どこから見ても分かるように、縦にむにっと潰してぽよんと戻す
        _squashDir = Vector3.up;
        _squashPower = p;
        _squashStart = Time.time;
        _squashActive = true;
    }

    private void UpdateSquash()
    {
        if (!_squashActive || _furMat == null) return;
        float t = Time.time - _squashStart;
        float env = Mathf.Exp(-squashDamping * t);
        if (env < 0.01f)
        {
            _squashActive = false;
            _furMat.SetVector("_Squash", Vector4.zero);
            return;
        }
        float w = _squashPower * env * Mathf.Cos(t * squashFrequency * 2f * Mathf.PI);
        _furMat.SetVector("_Squash", new Vector4(_squashDir.x, _squashDir.y, _squashDir.z, w));
    }

    [Tooltip("クモの巣で破裂直前の震えの大きさ(m)")]
    public float stuckShakeMax = 0.012f;
    [Tooltip("震えの速さ（大きいほど小刻み）")]
    public float stuckShakeSpeed = 115f;
    private Vector3 _stuckPos;
    private float _stuckStart;
    private float _stuckDuration = 1f;

    // クモの巣にくっついている間、プルプル震える（破裂が近づくほど激しく）
    private void UpdateStuckShake()
    {
        float k = Mathf.Clamp01((Time.time - _stuckStart) / _stuckDuration);
        float amp = stuckShakeMax * (0.15f + 0.85f * k * k);
        float t = Time.time * stuckShakeSpeed;
        Vector3 off = new Vector3(Mathf.Sin(t), Mathf.Sin(t * 0.86f + 1.3f), Mathf.Sin(t * 1.1f + 2.1f)) * amp;
        transform.position = _stuckPos + off;
    }

    [Header("トゲに刺さった時")]
    [Tooltip("「グサッ」の音")]
    public AudioSource sfxSpike;
    [Tooltip("刺さってから弾けるまで止まる秒数")]
    public float spikeFreezeTime = 0.3f;
    private bool _spiking;

    // トゲから呼ばれる：グサッと刺さって一瞬止まってから、弾けてアウト
    public void FailBySpike()
    {
        if (_waiting || _finished || _resetAt > 0f || _spiking || _hidden) return;
        _spiking = true;
        PlaySfx(sfxSpike);
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
        SendCustomEventDelayedSeconds(nameof(SpikePop), spikeFreezeTime);
    }

    public void SpikePop()
    {
        if (!_spiking) return;
        _spiking = false;
        Fail();
    }

    // 弾けるけど、プレイヤーは戻さない（チュートリアルの区切りの壁。2026-10-12 てじゃ「自分はリスポンなし」）
    private bool _stayOnFail;
    public void FailStay()
    {
        _stayOnFail = true;
        Fail();
        _stayOnFail = false;
    }

    public void Fail()
    {
        if (_waiting || _finished || _resetAt > 0f || _spiking) return;
        float inflate = popEffect != null ? Mathf.Max(0f, inflateTime) : 0f;
        _resetAt = Time.time + resetDelay + inflate;
        // れんしゅうのためし撃ちの場所では「アウト！」は出さない（弾けて戻るだけ。2026-10-09 てじゃ）
        if (!_practice) { ShowMessage(english ? "Out!" : "アウト！", resetDelay + inflate + 0.5f); PlaySfx(sfxOut); }
        // その場で止めて、ぷくーっと膨らんでからパッと弾けて消える（戻る時にResetToRespawnで元に戻る）
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
        if (inflate > 0f)
        {
            _inflateStart = Time.time;
            _inflatePos = transform.position;
            SphereCollider sc = GetComponent<SphereCollider>();
            _inflateRadius = (sc != null ? sc.radius : 0.5f) * _baseScale.x;
            _inflateAnchor = _anchorDir;
            SendCustomEventDelayedSeconds(nameof(PopNow), inflate);
        }
        else PopNow();
        // コース中なら、破裂を見せてから暗転して、真っ暗の間にプレイヤーも一緒に戻す
        if (movePlayerOnOut && !_stayOnFail && (timerEnabled || ReturnPlayerUntimed()))
        {
            _teleportOnReset = true;
            if (fade != null) fade.Play(Mathf.Max(0f, resetDelay + inflate - 0.35f), 0.3f, 0.15f, 0.35f);
        }
    }

    // 膨らみきったところで弾ける
    public void PopNow()
    {
        if (_resetAt < 0f || _hidden) { StopInflate(); return; }   // その間にリセットされていたら何もしない
        StopInflate();
        if (popEffect == null) return;
        popEffect.transform.position = transform.position;
        popEffect.Play();
        PlaySfx(sfxPop);
        SetHidden(true);
    }

    // ぷくーっ：だんだん速く膨らんで、弾ける直前は小刻みにふるえる
    private void UpdateInflate()
    {
        if (_inflateStart < 0f) return;
        float t = Mathf.Clamp01((Time.time - _inflateStart) / Mathf.Max(0.01f, inflateTime));
        float k = 1f + (inflateScale - 1f) * t * t;
        k += Mathf.Sin(Time.time * 70f) * 0.04f * t;
        transform.localScale = _baseScale * k;
        // 球なので回っていても関係なく、接地面（クモの巣なら巣に当たっている側）を動かさないように中心をずらす
        transform.position = _inflatePos - _inflateAnchor * _inflateRadius * (k - 1f);
    }

    private void StopInflate()
    {
        _inflateStart = -1f;
        _anchorDir = Vector3.down;
        if (_baseScale.sqrMagnitude > 0f) transform.localScale = _baseScale;
    }

    public void SetCheckpoint(Transform point)
    {
        if (point == null || point == respawnPoint || _finished) return;
        respawnPoint = point;
        ShowMessage(english ? "Checkpoint!" : "チェックポイント！", 2f);
        PlaySfx(sfxCheckpoint);
    }

    [HideInInspector] public float lastGoalTime;   // 最後にゴールした時のタイム（計らないコースは0）。FuwaGoal の CLEAR 表示用

    public string FormatTimeText(float t) { return FormatTime(t); }

    // returnPoint：ゴールした後に落ちたりした時に戻る、ゴール手前の地点。
    // あれば、ゴール後もチェックポイントの光はそのまま（スタートに戻すのは、ワープで移った時・手動リスポーン・スタートのキノコを撃った時）。
    // 無ければ（対戦レースも）昔どおり、少ししてからスタートに戻す
    public void ReachGoal(Transform returnPoint)
    {
        if (_finished || _resetAt > 0f) return;
        _finished = true;
        float t = _running ? Time.time - _startTime : 0f;
        lastGoalTime = timerEnabled ? t : 0f;
        _running = false; _untimedRun = false;
        if (timerText != null) timerText.text = timerEnabled ? Mono(FormatTime(t)) : "";   // 計らないコース（チュートリアルの本番の道）は 0:00.00 を出さない
        ShowMessage(english ? "Goal!" : "ゴール！", restartDelay);   // タイムは下のタイマーに出ているので文字には入れない
        PlaySfx(sfxGoal);
        // ゴールしたら、ふわふわがパンッと弾けて胞子を撒く（次のスタートでキノコに戻る）
        if (popEffect != null)
        {
            popEffect.transform.position = transform.position;
            popEffect.Play();
        }
        PlaySfx(sfxPop);
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
        SetHidden(true);
        if (records != null && timerEnabled && t > 0f) records.ReportTime(startPoint, t);
        if (bgm != null && (timerEnabled || ReturnPlayerUntimed())) bgm.PlayFanfare();   // れんしゅうの本番の道（タイムなし）のゴールでも鳴らす
        if (returnPoint != null) respawnPoint = returnPoint;
        else SendCustomEventDelayedSeconds(nameof(RestartFromStart), restartDelay);
        _goalReturn = returnPoint;
    }

    // ゴールした後（コースに残って見ている時）に落ちたら、今いるコースのチェックポイントとゴール前のうち、落ちた所にいちばん近い所へ戻す
    //（2026-10-19 てじゃ「先にクリアした人がまだの人を近くで見てるケースが割とあって、そのほうが追いかけやすい」。前はいつもゴール前）。
    // 今いるコース＝スタート地点と同じ一番上の親（FuwaCourse〜）。近さは横の距離（落ちている途中で高さは当てにならない）
    private Transform _goalReturn;
    private Vector3 _missPos;
    private bool _hasMissPos;
    private Transform NearestAfterGoal(Vector3 at)
    {
        if (_goalReturn == null) return null;
        Transform best = _goalReturn;
        float bd = FlatDist(_goalReturn.position, at);
        Transform courseRoot = startPoint != null ? startPoint.root : null;
        if (checkpoints == null || courseRoot == null) return best;
        for (int i = 0; i < checkpoints.Length; i++)
        {
            FuwaCheckpoint cp = checkpoints[i];
            if (cp == null || cp.transform.root != courseRoot) continue;
            Transform p = cp.respawnPoint != null ? cp.respawnPoint : cp.transform;
            float d = FlatDist(p.position, at);
            if (d < bd) { bd = d; best = p; }
        }
        return best;
    }
    private float FlatDist(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    private float _fallLockUntil;

    // プレイヤーがミスした（穴に落ちた・トゲに触った）瞬間に呼ばれる。
    // メッセージと音を出して暗転し、真っ暗になったらチェックポイントへ戻す。
    // すでに処理中ならfalse（複数の判定に続けて触れても一回だけにする）
    public bool BeginPlayerMiss(string messageJa, string messageEn)
    {
        if (Time.time < _fallLockUntil) return false;
        _fallLockUntil = Time.time + 1.5f;
        // ワープまでの間に落ち続けて、VRChatの「下に落ちたらスポーンへ戻す」高さ(-20)を割らないように、落ちる速さを止める
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player != null) { player.SetVelocity(Vector3.zero); _missPos = player.GetPosition(); _hasMissPos = true; }
        ShowMessage(english ? messageEn : messageJa, 1.8f);
        PlaySfx(sfxFell);
        if (fade != null) fade.Play(0.1f, 0.3f, 0.15f, 0.35f);
        SendCustomEventDelayedSeconds(nameof(FinishPlayerMiss), 0.45f);
        return true;
    }

    // 自分がトゲに刺さった時：「グサッ」と鳴って、その場で一瞬止まってから暗転してチェックポイントへ
    public bool BeginPlayerSpike(string messageJa, string messageEn)
    {
        if (Time.time < _fallLockUntil) return false;
        _fallLockUntil = Time.time + 1.7f;
        ShowMessage(english ? messageEn : messageJa, 2f);
        PlaySfx(sfxSpike);
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player != null) { _missPos = player.GetPosition(); _hasMissPos = true; }
        // 動けなくするのは暗転側にまとめる（戻し忘れを防ぐため。暗転が無い時だけ自分で）
        if (fade != null) fade.FreezeFor(spikeFreezeTime + 0.45f);
        else if (player != null)
        {
            player.SetVelocity(Vector3.zero);
            player.Immobilize(true);
            _freezeUntil = Time.time + spikeFreezeTime + 0.45f;
            SendCustomEventDelayedSeconds(nameof(ReleaseRespawnFreeze), spikeFreezeTime + 0.45f);
        }
        // 止まってる時間(spikeFreezeTime)ぶん、暗転を遅らせる
        if (fade != null) fade.Play(spikeFreezeTime, 0.3f, 0.15f, 0.35f);
        SendCustomEventDelayedSeconds(nameof(FinishPlayerMiss), spikeFreezeTime + 0.35f);
        return true;
    }

    public void FinishPlayerMiss()
    {
        TeleportPlayerToRespawn();
        if (_finished) return;
        _teleportOnReset = false;
        ReviveEnemies();
        ResetToRespawn();
    }

    // 自分を、今の再開位置（チェックポイント）の少し後ろの道の上へ戻す
    // タイムなしでも、returnPlayerStarts のスタートから始めたコースなら、アウトでプレイヤーも戻す（ためし撃ちの場所にいる時は戻さない）
    private bool ReturnPlayerUntimed()
    {
        if (_practice || returnPlayerStarts == null) return false;
        for (int i = 0; i < returnPlayerStarts.Length; i++) if (returnPlayerStarts[i] != null && returnPlayerStarts[i] == startPoint) return true;
        return false;
    }

    public void TeleportPlayerToRespawn()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null) return;
        Transform p = respawnPoint != null ? respawnPoint : startPoint;
        if (_finished)
        {
            Transform n = NearestAfterGoal(_hasMissPos ? _missPos : player.GetPosition());
            if (n != null) p = n;
        }
        _hasMissPos = false;
        if (p == null) return;
        Vector3 fwd = Vector3.ProjectOnPlane(p.forward, Vector3.up).normalized;
        Vector3 basePos = p.position - Vector3.up * playerHeightBelow;
        Vector3 pos = basePos - fwd * playerBackDistance;
        // 足元の道の高さに合わせる（胞子の位置が低い時に道の下へ飛ばされないように）。
        // 後ろに道が無い所（谷のすぐ先のチェックポイントなど）は、道がある所まで近づける
        RaycastHit hit;
        for (float d = playerBackDistance; d > 0.3f; d -= 0.2f)
        {
            Vector3 q = basePos - fwd * d;
            if (Physics.Raycast(q + Vector3.up * 2.5f, Vector3.down, out hit, 5f, 1 << 11)) { pos = hit.point; break; }
        }
        player.TeleportTo(pos, Quaternion.LookRotation(fwd, Vector3.up));
        // 暗転が明けるまで少しだけ動けなくする（真っ暗なまま歩き出して、また落ちたりしないように）
        if (fade != null) { fade.FreezeFor(respawnFreezeTime); return; }
        player.SetVelocity(Vector3.zero);
        player.Immobilize(true);
        _freezeUntil = Time.time + respawnFreezeTime;
        SendCustomEventDelayedSeconds(nameof(ReleaseRespawnFreeze), respawnFreezeTime);
    }

    [Tooltip("リスポーン後に動けない秒数（暗転が明けるまで）")]
    public float respawnFreezeTime = 0.55f;
    private float _freezeUntil;

    public void ReleaseRespawnFreeze()
    {
        // 続けてリスポーンした時は、あとの方の時間まで待つ（「まだ早い」時は少し後にもう一度見る＝戻し忘れ防止）
        if (Time.time < _freezeUntil - 0.01f) { SendCustomEventDelayedSeconds(nameof(ReleaseRespawnFreeze), 0.1f); return; }
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player != null) player.Immobilize(false);
    }


    // コースアウト：道の外（真下に道が無い）で、再開位置より下に落ちたらすぐアウト。
    // 道の上なら床に触った時にアウトになるので、ここでは見ない。
    private void CheckOffCourse()
    {
        if (_waiting || _hidden || _stuck || _carried || _resetAt > 0f || _finished) return;
        if (Time.time < _nextOffCourseCheck) return;
        _nextOffCourseCheck = Time.time + 0.1f;
        Transform rp = respawnPoint != null ? respawnPoint : startPoint;
        if (rp == null) return;
        // 真下に道があれば、その高さを覚えておくだけ（道の上なら床に触った時にアウトになる）
        // 下り坂や崖ダイブで再開位置より大きく下がっても、真下に道があればセーフ
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, groundProbe, 1 << groundLayer, QueryTriggerInteraction.Ignore))
        {
            _lastGroundY = hit.point.y;
            return;
        }
        if (gapZones != null)
        {
            Vector3 bp = transform.position;
            for (int i = 0; i < gapZones.Length; i++)
            {
                if (gapZones[i] != null && gapZones[i].bounds.Contains(bp)) return;
            }
        }
        bool low = false;
        if (lowZones != null)
        {
            Vector3 lp = transform.position;
            for (int i = 0; i < lowZones.Length; i++)
            {
                if (lowZones[i] != null && lowZones[i].bounds.Contains(lp)) { low = true; break; }
            }
        }
        if (!low && rp.position.y - transform.position.y > maxDrop) { Fail(); return; }
        // 道の外：最後に通った道の高さより下に落ちたらアウト（道より上に浮いている間はまだセーフ）
        if (_lastGroundY > -9999f && transform.position.y < _lastGroundY - offCourseDrop) Fail();
    }

    // 別のコース（またはロビー）に切り替える。timed=falseならタイムを計らない
    [Tooltip("全コースのチェックポイント（スタートからやり直す時に光を消す）")]
    public FuwaCheckpoint[] checkpoints;

    [Tooltip("チュートリアルの看板の赤▼。チュートリアルのコースから出た時（別のコース・ロビーへ。ワープでも手動リスポーンでも）に最初から出し直す")]
    public FuwaReadMarks tutorialMarks;

    public void StartCourse(Transform newStart, bool timed)
    {
        if (newStart == null) return;
        _practice = false;
        // チュートリアルから出た時だけ看板の赤▼をリセット（2026-10-20 てじゃ「条件をチュートリアルコースから出たとき、にしよう」。
        // 前は手動リスポーンとチュートリアルへのワープでリセットしていた）
        if (tutorialMarks != null && startPoint != null)
        {
            Transform tut = tutorialMarks.transform.root;
            if (startPoint.root == tut && newStart.root != tut) tutorialMarks.ResetFromRespawn();
        }
        startPoint = newStart;
        timerEnabled = timed;
        _messageUntil = Time.time;
        RestartFromStart();
    }

    // れんしゅうの「ためし撃ちの場所」：ここに浮いて待ち、床に落ちたり道から落ちたら弾けてここへ戻るだけ（タイムなし・プレイヤーは動かさない）。
    // startPoint は変えないので、コースのスタートに戻る時は StartCourse で
    public void SetPracticeSpot(Transform spot)
    {
        if (spot == null) return;
        _practice = true;
        timerEnabled = false;
        respawnPoint = spot;
        _finished = false;
        _running = false; _untimedRun = false;
        ResetToRespawn();
    }

    public Transform GetRespawnPoint() { return respawnPoint; }

    // れんしゅう：次の「ためし撃ちの場所」へ移る時は、今いる所でパッと弾けて、すぐ次の場所に出る
    // （2026-10-09 てじゃ：前は弧を描いて飛んでいったが、走ると追いつかないので一瞬に）。
    // キノコに隠れている時・弾けている途中などは、弾けずにその場所に出すだけ
    public void GlideToPracticeSpot(Transform spot)
    {
        if (spot == null) return;
        bool visible = !_hidden && _resetAt < 0f && !_spiking && gameObject.activeInHierarchy;
        if (visible && popEffect != null && Vector3.Distance(transform.position, spot.position) > 0.05f)
        {
            popEffect.transform.position = transform.position;
            popEffect.Play();
            PlaySfx(sfxPop);
        }
        SetPracticeSpot(spot);
        if (visible && !_hidden) Appear();
    }

    // テスト用（2026-10-22、公開前に消す。FuwaDebugBallSummon から）：今いる所でパッと弾けて spot に出し、そこをチェックポイントにする。
    // タイムは計らない。落ちたら spot に戻る（プレイヤーもふつうのアウトと同じく戻る）。先のチェックポイントを通ればそっちに更新される
    public void DebugSummon(Transform spot, int stage)
    {
        if (spot == null) return;
        SetStage(stage);
        bool visible = !_hidden && _resetAt < 0f && !_spiking && gameObject.activeInHierarchy;
        if (visible && popEffect != null)
        {
            popEffect.transform.position = transform.position;
            popEffect.Play();
            PlaySfx(sfxPop);
        }
        _practice = false;
        timerEnabled = false;
        _finished = false;
        _running = false; _untimedRun = false;
        respawnPoint = spot;
        ResetToRespawn();
        Appear();
    }

    // 出てくる時の一瞬のエフェクト：ポンッとふくらんで（少し行きすぎて戻る）、まわりに綿毛がふわっと散る
    [Header("れんしゅう：次の場所に出てくる時")]
    public float appearTime = 0.22f;
    public int appearPuff = 14;
    private float _appearStart = -1f;

    private void Appear()
    {
        _appearStart = Time.time;
        if (_baseScale.sqrMagnitude > 0f) transform.localScale = _baseScale * 0.05f;
        if (shotPuff != null) shotPuff.Emit(appearPuff);
        if (sfxBoing != null && sfxBoing.clip != null) { sfxBoing.pitch = boingPitchStrong * Random.Range(0.96f, 1.04f); sfxBoing.PlayOneShot(sfxBoing.clip, boingVolumeWeak); }
    }

    private void UpdateAppear()
    {
        if (_appearStart < 0f) return;
        if (_inflateStart >= 0f || _hidden) { _appearStart = -1f; return; }
        float t = Mathf.Clamp01((Time.time - _appearStart) / Mathf.Max(0.01f, appearTime));
        // 0 → 1.15 → 1（バネっぽく）
        float k = t < 0.7f ? Mathf.SmoothStep(0.05f, 1.15f, t / 0.7f) : Mathf.Lerp(1.15f, 1f, Mathf.SmoothStep(0f, 1f, (t - 0.7f) / 0.3f));
        transform.localScale = _baseScale * k;
        if (t >= 1f) { transform.localScale = _baseScale; _appearStart = -1f; }
    }

    public void RestartFromStart()
    {
        respawnPoint = startPoint != null ? startPoint : transform;
        _finished = false;
        _running = false; _untimedRun = false;
        ReviveEnemies();
        // 通ったチェックポイントの光（キノコ）を消して、最初の状態に戻す
        if (checkpoints != null)
            for (int i = 0; i < checkpoints.Length; i++) if (checkpoints[i] != null) checkpoints[i].ResetGlow();
        ResetToRespawn();
    }

    public void ResetToRespawn()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();   // Start より先に呼ばれた時（隠れていたエリアに入ってすぐワープした時など）
        _appearStart = -1f;
        StopInflate();
        _carried = false;
        _resetAt = -1f;
        _spiking = false;
        _goalZone = null;
        _stuck = false;
        _glowUntil = 0f;
        _glowLongUntil = 0f;
        _glowAmount = 0f;
        _flightUntil = 0f;
        _lastGroundY = -10000f;
        if (respawnPoint == startPoint) { _running = false; _untimedRun = false; }
        _rb.isKinematic = false;
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(respawnPoint.position, respawnPoint.rotation);
        _rb.isKinematic = true;
        _waiting = true;
        // スタート（各コース・ロビーの練習用）では、胞子はキノコの中に隠れて待つ（チェックポイントでは見えたまま）
        SetHidden(hideAtStart && respawnPoint == startPoint);
    }

    // スタートのキノコが撃たれた時に呼ばれる。撃たれた向き（水平）＋上へ飛び出す
    public void ReleaseFromMushroom(Vector3 shotDir)
    {
        // ゴールした後にスタートまで戻ってキノコを撃った時は、ここで最初からやり直す（チェックポイントの光も消す）
        if (_finished) RestartFromStart();
        if (!_hidden || _resetAt > 0f) return;
        SetHidden(false);
        // 胞子を出す前にモンスターを倒しておく、はできないように、スタートで全部復活させる
        ReviveEnemies();
        Vector3 flat = new Vector3(shotDir.x, 0f, shotDir.z);
        flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.zero;
        Blow(flat * releasePush + Vector3.up * releaseUp);
    }

    public bool IsHidden() { return _hidden; }
    public bool IsFinished() { return _finished; }
    public bool IsRunning() { return _running; }
    // 挑戦中か（タイムを計らない道も含む）。名場面のフォトポイント用
    public bool IsInRun() { return _running || _untimedRun; }
    public float GetRunStartTime() { return _startTime; }   // 名場面のフォトポイントが「1回の挑戦で1枚」を見分けるため

    public void SetGoalZone(FuwaGoal goal) { _goalZone = goal; }
    public void LeaveGoalZone(FuwaGoal goal) { if (_goalZone == goal) _goalZone = null; }

    // 倒したモンスターを全部復活させる
    public void ReviveEnemies()
    {
        if (enemies == null) return;
        foreach (FuwaEnemy e in enemies) if (e != null) e.Revive();
        if (bosses != null) foreach (FuwaMama m in bosses) if (m != null) m.ResetBoss();
    }

    private void SetHidden(bool hidden)
    {
        _hidden = hidden;
        if (furRenderer != null) furRenderer.enabled = !hidden;
        TrailRenderer tr = GetComponent<TrailRenderer>();
        if (tr != null) { tr.Clear(); tr.emitting = !hidden; }
        if (shotPuff != null && hidden) shotPuff.Clear();
    }

    [Header("撃たれた時に散る綿毛")]
    [Tooltip("撃たれた瞬間に玉から散らすパーティクル（Emitで出す）")]
    public ParticleSystem shotPuff;
    [Tooltip("弱い連射1発で出す数")]
    public int shotPuffMin = 5;
    [Tooltip("フルチャージで出す数")]
    public int shotPuffMax = 16;

    private void EmitShotPuff(Vector3 velocityChange)
    {
        float k = Mathf.Clamp01(velocityChange.magnitude / Mathf.Max(0.01f, squashFullAt));
        if (sfxBoing != null && sfxBoing.clip != null)
        {
            // 重なっても鳴るようにPlayOneShot（音程は最後の1発に合わせる）
            sfxBoing.pitch = Mathf.Lerp(boingPitchWeak, boingPitchStrong, k) * Random.Range(0.96f, 1.04f);
            sfxBoing.PlayOneShot(sfxBoing.clip, Mathf.Lerp(boingVolumeWeak, boingVolumeStrong, k));
        }
        if (shotPuff == null) return;
        shotPuff.Emit(Mathf.RoundToInt(Mathf.Lerp(shotPuffMin, shotPuffMax, k)));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.gameObject == null) return;
        FuwaHazard hazard = collision.gameObject.GetComponent<FuwaHazard>();
        FuwaBounce bouncer = collision.gameObject.GetComponent<FuwaBounce>();
        FuwaTopOut topOut = collision.gameObject.GetComponent<FuwaTopOut>();
        if (topOut != null)
        {
            // 溝の岩壁など：上の面に乗ったらアウト、横は当たるだけ
            // 上がっている途中でふちの角をかすっただけならアウトにしない（水路で道に寄ったまま道の方へ撃つとアウトになった。2026-10-23 てじゃ）
            // 水に浮いたまま道に寄っただけでもアウトにしない（水に浮くと道の上の面とほぼ同じ高さになる。道に乗り上げて水から出たら OnCollisionStay で見る）
            if (collision.contactCount > 0 && collision.GetContact(0).normal.y >= topOut.minUp && _fixedVel.y < topOut.risingSpeed && !InWaterNow()) Fail();
            return;
        }
        if (hazard != null) hazard.HitBall(this);
        else if (bouncer != null)
        {
            // 岩の柱・壁など：当たった面の向きで跳ね返す
            if (bouncer.useContactNormal && collision.contactCount > 0) BounceOffNormal(bouncer, collision.GetContact(0).normal);
            else BounceOff(bouncer);
        }
        else if (collision.gameObject.layer == groundLayer)
        {
            // ゴールのリングの中で着地したらゴール、それ以外の地面はアウト
            if (_goalZone != null) _goalZone.Landed(this);
            else Fail();
        }
    }

    // 柵の上の面に乗っている間（OnCollisionStay は物理の1歩ごと）
    private void OnCollisionStay(Collision collision)
    {
        if (collision == null || collision.gameObject == null || collision.contactCount == 0) return;
        // 水から道に乗り上げて、水から出たまま上の面に乗っていたらアウト（OnCollisionEnter の時は水に浮いていたので見のがした分）
        if (!InWaterNow() && _resetAt < 0f && collision.GetContact(0).normal.y >= 0.6f && _rb.velocity.y < 0.3f)
        {
            FuwaTopOut stayTop = collision.gameObject.GetComponent<FuwaTopOut>();
            if (stayTop != null && collision.GetContact(0).normal.y >= stayTop.minUp) { Fail(); return; }
        }
        if (perchName == "" || !collision.gameObject.name.Contains(perchName)) return;
        if (collision.GetContact(0).normal.y < 0.6f) return;   // 横に当たっているだけの時は数えない
        if (_perchStart < 0f || Time.time - _perchSeen > 0.2f) _perchStart = Time.time;
        _perchSeen = Time.time;
    }

    // 柵のてっぺん（平らな当たり判定）に乗ってほとんど止まったら、まわりで一番高い道（島）の方へそっと転がす
    private void UpdatePerch()
    {
        if (_perchStart < 0f) return;
        if (Time.time - _perchSeen > 0.2f) { _perchStart = -1f; return; }   // もう乗っていない
        if (_rb.velocity.sqrMagnitude > 0.25f) { _perchStart = Time.time; return; }   // まだ動いている
        if (Time.time - _perchStart < perchDelay) return;
        _perchStart = -1f;
        Vector3 p = transform.position;
        Vector3 best = Vector3.zero; float bestY = -1e9f;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            RaycastHit hit;
            if (Physics.Raycast(p + d * perchProbe + Vector3.up * 0.3f, Vector3.down, out hit, groundProbe, 1 << groundLayer, QueryTriggerInteraction.Ignore) && hit.point.y > bestY)
            { bestY = hit.point.y; best = d; }
        }
        if (best == Vector3.zero)
        {
            // まわりに道がない：再開位置の方へ
            Transform rp = respawnPoint != null ? respawnPoint : startPoint;
            if (rp == null) return;
            best = rp.position - p; best.y = 0f;
            if (best.sqrMagnitude < 0.0001f) return;
            best.Normalize();
        }
        _rb.velocity = best * perchPush + Vector3.up * 0.3f;
    }

    private void PlaySfx(AudioSource source)
    {
        if (source != null) source.Play();
    }

    private void ShowMessage(string msg, float seconds)
    {
        if (statusText == null) return;
        statusText.text = msg;
        _messageUntil = Time.time + seconds;
    }

    // 数字の幅をそろえる（数字が変わるたびに文字の幅＝背景の枠がピクピク動かないように）
    private string Mono(string s)
    {
        return "<mspace=0.6em>" + s + "</mspace>";
    }

    // 「分:秒.小数2桁」。99:59.99 で止める（それ以上は表示しない）。
    // 1/100秒単位の整数にしてから分けるので、59.996秒が「0:60.00」になるような丸めのズレが出ない
    private string FormatTime(float t)
    {
        int cs = Mathf.Clamp(Mathf.FloorToInt(t * 100f), 0, 599999);
        int m = cs / 6000;
        int s = (cs / 100) % 60;
        int f = cs % 100;
        return m.ToString() + ":" + s.ToString("00") + "." + f.ToString("00");
    }

    public bool IsWaiting() { return _waiting; }

    // ===== 運ばれる（FuwaBallLift など）=====
    // 運び始め：物理を止めて、位置は運ぶ側が毎フレーム CarryTo で決める。運べない状態なら false
    public bool BeginCarry()
    {
        if (_carried || _waiting || _hidden || _stuck || _spiking || _finished || _resetAt > 0f) return false;
        _carried = true;
        StopInflate();
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        _rb.isKinematic = true;
        return true;
    }
    public bool IsCarried() { return _carried; }
    public void CarryTo(Vector3 pos)
    {
        if (!_carried) return;
        _rb.MovePosition(pos);
        transform.position = pos;
    }
    // 運び終わり：その場の空中で止まって待つ（撃たれたら動き出す。チェックポイントからの再開と同じ待ち方）
    public void EndCarryWaiting()
    {
        if (!_carried) return;
        _carried = false;
        _rb.isKinematic = true;
        _waiting = true;
    }
}
