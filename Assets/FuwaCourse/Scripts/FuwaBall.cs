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

    [Header("リスポーン")]
    public Transform startPoint;
    [Tooltip("この高さより下に落ちたらアウト")]
    public float killHeight = -3f;
    [Tooltip("コースアウト判定：真下に道が無く、最後に通った道の高さよりこれだけ下がったらアウト(m)")]
    public float offCourseDrop = 0.8f;
    private float _lastGroundY = -10000f;
    [Tooltip("コースアウト判定：真下に道が無くても、再開位置よりこれだけ下ならどこでもアウト(m)")]
    public float maxDrop = 3f;
    [Tooltip("この範囲の中ではコースアウト判定をしない（崖ダイブの谷の上など。底のアウトは別のトゲ等で）")]
    public Collider[] gapZones;
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
    private bool _teleportOnReset;

    [HideInInspector] public Transform respawnPoint;
    [HideInInspector] public bool english;   // FuwaLanguageが切り替える
    [HideInInspector] public FuwaRace activeRace;   // 対戦レースに参加中ならそのレース
    [HideInInspector] public bool raceLocked;       // GO前：スタートのキノコを撃っても出てこない

    [Header("対戦：アイテムの効果とペナルティ")]
    [Tooltip("おもりキノコを受けた時の重さ（落下の倍率）")]
    public float itemHeavyScale = 2.2f;
    [Tooltip("おいかぜの強さ（前向きの加速度）")]
    public float tailwindPower = 3.2f;
    [Tooltip("おいかぜの持ち上げ（上向きの加速度）")]
    public float tailwindLift = 1.2f;
    [Tooltip("レースでアウトになった後、ふわふわが動かせない秒数")]
    public float racePenaltySeconds = 2f;
    [Tooltip("ペナルティ中にふわふわを包む殻（なくてもOK）")]
    public GameObject penaltyShell;
    public Color penaltyColor = new Color(0.55f, 0.55f, 0.62f, 1f);
    private float _heavyUntil;
    private float _tailwindUntil;
    private Vector3 _tailwindDir;
    private float _penaltyUntil;
    private bool _failedSinceReset;
    private int _penaltyShownCount = -1;
    private bool _softStick;
    [Tooltip("対戦レースでアウトになった時、戻らずに自分の目の前に出てくる（前方の距離m）")]
    public float raceRespawnAhead = 1.0f;
    [Tooltip("同じく、目の高さからどれだけ下に出すか(m)")]
    public float raceRespawnBelowEye = 0.25f;
    private bool _respawnInFront;

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
    public Color windGlowColor = new Color(0.35f, 1f, 0.5f, 1f);
    public Color gravityGlowColor = new Color(0.7f, 0.3f, 1f, 1f);
    public Color launchGlowColor = new Color(1f, 0.6f, 0.2f, 1f);
    [Tooltip("横風がこの強さ(加速度)以上なら一番明るく光る")]
    public float windGlowFull = 3f;
    [Tooltip("発射台で飛ばされてから光っている秒数")]
    public float launchGlowTime = 0.9f;
    private Color _glowColor;
    private bool _launchGlow;   // 今の光が打ち出しの光か
    private Vector3 _glowDir = Vector3.up;
    private float _glowAmount;       // 今の光の強さ（なめらかに変化）
    private float _glowTarget;       // 目標の強さ
    private float _glowUntil;        // この時刻まで目標を保つ

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
    }

    private void FixedUpdate()
    {
        if (_waiting || _stuck) return;

        // アイテムの効果（おもりキノコ・おいかぜ）
        if (Time.time < _heavyUntil) SetGravityScale(itemHeavyScale);
        if (Time.time < _tailwindUntil) AddWind(_tailwindDir * tailwindPower + Vector3.up * tailwindLift);

        // 重くなるゾーンから届いた倍率（届いてなければ1倍）
        float g = _gravityScale > 0f ? _gravityScale : 1f;
        _gravityScale = 0f;

        _rb.AddForce(Vector3.down * fallAcceleration * g, ForceMode.Acceleration);
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
        UpdateInflate();
        UpdateSquash();
        UpdatePenalty();
        // 待機中（空中で止まってる間）もゆっくり回して、ふわふわ感を出す
        if (_waiting) transform.Rotate(new Vector3(0.3f, 1f, 0.15f), idleSpin * Time.deltaTime, Space.World);
        if (_resetAt > 0f && Time.time >= _resetAt)
        {
            StopInflate();
            if (_teleportOnReset) TeleportPlayerToRespawn();
            _teleportOnReset = false;
            if (_respawnInFront) ResetInFrontOfPlayer();
            else ResetToRespawn();
        }
        if (!_waiting && _resetAt < 0f && transform.position.y < killHeight) Fail();
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
        if (Time.time < _penaltyUntil) return;
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
        if (_resetAt > 0f || _hidden || _stuck || _waiting) return;
        Vector3 n = b.GetNormal(transform.position);
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

    // 風ゾーンから毎フレーム呼ばれる（加速度）
    public void AddWind(Vector3 acceleration)
    {
        if (_waiting || _resetAt > 0f || _stuck) return;
        _rb.AddForce(acceleration, ForceMode.Acceleration);
        float mag = acceleration.magnitude;
        if (mag > 0.05f) SetGlow(windGlowColor, acceleration / mag, Mathf.Clamp01(mag / windGlowFull), 0.1f);
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

    // 光らせる（色・向き・強さ・保つ秒数）。発射台の光っている間は、弱い横風などで上書きしない
    private void SetGlow(Color color, Vector3 dir, float amount, float hold)
    {
        // 打ち出しの光（_launchGlow）が残っている間は、ほかの弱い光で上書きしない
        if (_launchGlow && Time.time < _glowUntil) return;
        _launchGlow = false;
        _glowColor = color;
        _glowDir = dir;
        if (Time.time >= _glowUntil || amount >= _glowTarget) _glowTarget = amount;
        _glowUntil = Mathf.Max(_glowUntil, Time.time + hold);
    }

    // 影響がある間はすっと光り、終わったらゆっくり消える
    private void UpdateGlow()
    {
        if (Time.time < _penaltyUntil)
        {
            _furMat.SetColor("_GlowColor", new Color(penaltyColor.r, penaltyColor.g, penaltyColor.b, 0.85f));
            _furMat.SetVector("_GlowDir", new Vector4(0f, 1f, 0f, 0f));
            _glowAmount = 0f;
            return;
        }
        float target = Time.time < _glowUntil && !_hidden ? _glowTarget : 0f;
        float speed = target > _glowAmount ? 8f : 3f;
        _glowAmount = Mathf.MoveTowards(_glowAmount, target, speed * Time.deltaTime);
        _furMat.SetColor("_GlowColor", new Color(_glowColor.r, _glowColor.g, _glowColor.b, _glowAmount));
        _furMat.SetVector("_GlowDir", new Vector4(_glowDir.x, _glowDir.y, _glowDir.z, 0f));
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

    // ===== アイテムの効果 =====
    public void ApplyItemHeavy(float seconds)
    {
        _heavyUntil = Mathf.Max(_heavyUntil, Time.time + seconds);
    }

    public void ApplyTailwind(Vector3 forward, float seconds)
    {
        Vector3 f = new Vector3(forward.x, 0f, forward.z);
        _tailwindDir = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        _tailwindUntil = Mathf.Max(_tailwindUntil, Time.time + seconds);
    }

    public bool IsWaitingOrHidden() { return _waiting || _hidden || _resetAt > 0f; }

    // アイテムなどの一言（普通の大きさ）
    public void ShowInfo(string ja, string en, float seconds)
    {
        ShowMessage(english ? en : ja, seconds);
    }

    // レースのアウト後ペナルティ：灰色＋殻で固定、カウントダウン、解除でポンッ
    private void UpdatePenalty()
    {
        bool on = Time.time < _penaltyUntil;
        if (penaltyShell != null && penaltyShell.activeSelf != on) penaltyShell.SetActive(on);
        if (!on)
        {
            if (_penaltyShownCount > 0)
            {
                _penaltyShownCount = -1;
                ShowMessage(english ? "Go!" : "動ける！", 1f);
                PlaySfx(sfxPop);
            }
            return;
        }
        int count = Mathf.CeilToInt(_penaltyUntil - Time.time);
        if (count != _penaltyShownCount)
        {
            _penaltyShownCount = count;
            ShowMessage((english ? "Frozen " : "かたまり中 ") + count, 1.1f);
        }
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

    public void Fail()
    {
        if (_waiting || _finished || _resetAt > 0f || _spiking) return;
        float inflate = popEffect != null ? Mathf.Max(0f, inflateTime) : 0f;
        _resetAt = Time.time + resetDelay + inflate;
        _failedSinceReset = true;
        ShowMessage(english ? "Out!" : "アウト！", resetDelay + inflate + 0.5f);
        PlaySfx(sfxOut);
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
        // 対戦レース中は戻らない：破裂したら、自分の目の前に出てくる（ペナルティでしばらく動かせない）
        _respawnInFront = activeRace != null;
        if (_respawnInFront) return;
        // コース中なら、破裂を見せてから暗転して、真っ暗の間にプレイヤーも一緒に戻す
        if (movePlayerOnOut && timerEnabled)
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

    public void ReachGoal()
    {
        if (_finished || _resetAt > 0f) return;
        _finished = true;
        float t = _running ? Time.time - _startTime : 0f;
        lastGoalTime = timerEnabled ? t : 0f;
        _running = false;
        if (timerText != null) timerText.text = Mono(FormatTime(t));
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
        if (activeRace != null) activeRace.LocalFinished();
        if (bgm != null && timerEnabled) bgm.PlayFanfare();
        SendCustomEventDelayedSeconds(nameof(RestartFromStart), restartDelay);
    }

    private float _fallLockUntil;

    // プレイヤーがミスした（穴に落ちた・トゲに触った）瞬間に呼ばれる。
    // メッセージと音を出して暗転し、真っ暗になったらチェックポイントへ戻す。
    // すでに処理中ならfalse（複数の判定に続けて触れても一回だけにする）
    public bool BeginPlayerMiss(string messageJa, string messageEn)
    {
        if (Time.time < _fallLockUntil) return false;
        _fallLockUntil = Time.time + 1.5f;
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
    public void TeleportPlayerToRespawn()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null) return;
        Transform p = respawnPoint != null ? respawnPoint : startPoint;
        if (p == null) return;
        Vector3 fwd = Vector3.ProjectOnPlane(p.forward, Vector3.up).normalized;
        Vector3 pos = p.position - Vector3.up * playerHeightBelow - fwd * playerBackDistance;
        // 足元の道の高さに合わせる（胞子の位置が低い時に道の下へ飛ばされないように）
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 2.5f, Vector3.down, out hit, 5f, 1 << 11)) pos = hit.point;
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
        if (_waiting || _hidden || _stuck || _resetAt > 0f || _finished) return;
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
        if (rp.position.y - transform.position.y > maxDrop) { Fail(); return; }
        // 道の外：最後に通った道の高さより下に落ちたらアウト（道より上に浮いている間はまだセーフ）
        if (_lastGroundY > -9999f && transform.position.y < _lastGroundY - offCourseDrop) Fail();
    }

    // 対戦レースのアウト後：チェックポイントに戻さず、自分の目の前の空中にふわふわを出す
    private void ResetInFrontOfPlayer()
    {
        _respawnInFront = false;
        ResetToRespawn();   // 状態のリセットとペナルティ開始（位置はこのあと上書き）
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null || !player.IsValid()) return;
        VRCPlayerApi.TrackingData head = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 fwd = Vector3.ProjectOnPlane(head.rotation * Vector3.forward, Vector3.up);
        fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
        Vector3 pos = head.position + fwd * raceRespawnAhead - Vector3.up * raceRespawnBelowEye;
        _rb.isKinematic = true;
        transform.position = pos;
        SetHidden(false);   // キノコには戻らない
    }

    // 別のコース（またはロビー）に切り替える。timed=falseならタイムを計らない
    [Tooltip("全コースのチェックポイント（スタートからやり直す時に光を消す）")]
    public FuwaCheckpoint[] checkpoints;

    public void StartCourse(Transform newStart, bool timed)
    {
        if (newStart == null) return;
        startPoint = newStart;
        timerEnabled = timed;
        _messageUntil = Time.time;
        RestartFromStart();
    }

    public void RestartFromStart()
    {
        respawnPoint = startPoint != null ? startPoint : transform;
        _finished = false;
        _running = false;
        ReviveEnemies();
        // 通ったチェックポイントの光（キノコ）を消して、最初の状態に戻す
        if (checkpoints != null)
            for (int i = 0; i < checkpoints.Length; i++) if (checkpoints[i] != null) checkpoints[i].ResetGlow();
        ResetToRespawn();
    }

    public void ResetToRespawn()
    {
        StopInflate();
        _resetAt = -1f;
        _spiking = false;
        _goalZone = null;
        _stuck = false;
        _glowUntil = 0f;
        _glowAmount = 0f;
        _heavyUntil = 0f;
        _tailwindUntil = 0f;
        _flightUntil = 0f;
        _penaltyUntil = (_failedSinceReset && activeRace != null && racePenaltySeconds > 0f) ? Time.time + racePenaltySeconds : 0f;
        _failedSinceReset = false;
        _penaltyShownCount = -1;
        _lastGroundY = -10000f;
        // 対戦レース中は、スタートからやり直してもタイムは止めない（GOから計っているので）
        if (respawnPoint == startPoint && activeRace == null) _running = false;
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
        if (!_hidden || _resetAt > 0f) return;
        if (Time.time < _penaltyUntil) return;
        SetHidden(false);
        // 胞子を出す前にモンスターを倒しておく、はできないように、スタートで全部復活させる
        ReviveEnemies();
        Vector3 flat = new Vector3(shotDir.x, 0f, shotDir.z);
        flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.zero;
        Blow(flat * releasePush + Vector3.up * releaseUp);
    }

    public bool IsHidden() { return _hidden; }
    public bool IsRunning() { return _running; }

    // 対戦レースのGO：キノコを撃てるようにして、タイムをGOの瞬間から計り始める
    public void RaceGo()
    {
        raceLocked = false;
        _finished = false;
        _running = true;
        _startTime = Time.time;
    }

    // 対戦レースのカウントダウンなど（大きめに出す）
    public void ShowRaceMessage(string ja, string en, float seconds)
    {
        ShowMessage("<size=160%>" + (english ? en : ja) + "</size>", seconds);
    }

    public void SetGoalZone(FuwaGoal goal) { _goalZone = goal; }
    public void LeaveGoalZone(FuwaGoal goal) { if (_goalZone == goal) _goalZone = null; }

    // 倒したモンスターを全部復活させる
    public void ReviveEnemies()
    {
        if (enemies == null) return;
        foreach (FuwaEnemy e in enemies) if (e != null) e.Revive();
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
        if (hazard != null) hazard.HitBall(this);
        else if (bouncer != null) BounceOff(bouncer);
        else if (collision.gameObject.layer == groundLayer)
        {
            // ゴールのリングの中で着地したらゴール、それ以外の地面はアウト
            if (_goalZone != null) _goalZone.Landed(this);
            else Fail();
        }
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
}
