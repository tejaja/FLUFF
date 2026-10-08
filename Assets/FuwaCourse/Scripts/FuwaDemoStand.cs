using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// れんしゅうの「お手本台」：見た目だけの銃が、見た目だけのふわふわを自動で撃ってみせる（自分の画面だけ・同期なし）。
// 飛ばし方は本物の銃（tuning）と同じ計算、ふわふわの落ち方・空気抵抗も本物（ballTuning）の値を使うので、
// 本物の調整を変えるとお手本も自動で同じになる。
// 1回分の流れ（cycleLength 秒）をくり返す：ふわふわが ballStart に現れる → 決めた時刻に撃つ（複数回OK）→ 最後に消えて最初へ。
// プレイヤーが近くにいる時だけ動く。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaDemoStand : UdonSharpBehaviour
{
    [Header("計算に使う本物（値を読むだけ）")]
    public FuwaBlower tuning;
    public FuwaBall ballTuning;

    [Header("お手本の見た目")]
    [Tooltip("お手本の銃（見た目だけ）。これを狙う向きへ回す")]
    public Transform gun;
    [Tooltip("銃口（ここから撃つ。向きは gun と同じ）")]
    public Transform nozzle;
    [Tooltip("お手本のふわふわ（見た目だけ）")]
    public Transform ball;
    [Tooltip("ふわふわの毛（なびかせる）")]
    public Renderer ballFur;
    [Tooltip("ふわふわが現れる位置")]
    public Transform ballStart;
    [Tooltip("床の高さ。ふわふわがここまで落ちたら本物と同じく弾けて消える（次の回まで出ない）。未設定なら落ち続ける")]
    public Transform floor;
    [Tooltip("弾けた時の粒（本物のふわふわの PopEffect のコピー。ふわふわの子にはしない）")]
    public ParticleSystem popEffect;
    public AudioSource popSound;
    [Tooltip("「床がある」とみなすレイヤー（0=Default、11=Environment）")]
    public int floorMask = (1 << 0) | (1 << 11);

    [Header("銃の演出（本物の銃の Nozzle をコピーしたもの）")]
    public ParticleSystem puffEffect;
    public ParticleSystem airRing;
    public ParticleSystem chargeParticles;
    public ParticleSystem fullBurst;
    public ParticleSystem chargedSparkle;
    public Transform chargeOrb;
    public AudioSource puffSound;
    public AudioSource puffBigSound;
    public AudioSource chargeSound;
    public AudioSource chargedSound;
    public Transform pumpBulb;

    [Header("動き")]
    [Tooltip("1回分の長さ（秒）")]
    public float cycleLength = 5f;
    [Tooltip("開始をずらす（秒）。並べた台どうしでタイミングをそろえる／ずらす")]
    public float timeOffset = 0f;
    [Tooltip("撃つ時刻（1回分の始まりからの秒）")]
    public float[] shotTimes = new float[] { 1.5f };
    [Tooltip("それぞれの溜め（0=ちょん押し、1=満タン）")]
    public float[] shotCharges = new float[] { 1f };
    [Tooltip("それぞれの狙う所＝ふわふわの中心からのずれ（銃から見た 右・上・奥 の m）")]
    public Vector3[] shotAimOffsets = new Vector3[] { Vector3.zero };
    [Tooltip("それぞれ、満タンになってから撃つまで溜めたまま待つ秒数（吸い寄せを見せる時など）")]
    public float[] shotHolds = new float[0];
    [Tooltip("溜めている間、本物と同じようにふわふわを引き寄せる")]
    public bool useChargePull = true;
    [Tooltip("銃がふわふわの方へ向き直る速さ")]
    public float aimTurnSpeed = 6f;
    [Tooltip("1回分の終わりに、ふわふわが縮んで消える時間（秒）")]
    public float vanishTime = 0.35f;
    [Tooltip("この距離(m)より遠くにプレイヤーがいる時は止める")]
    public float activeRange = 14f;
    [Tooltip("ねらった線（銃口からまっすぐ）を、溜め中〜撃った少し後まで見せる。なくてもOK")]
    public LineRenderer aimLine;
    public float aimLineLength = 3.2f;
    public float aimLineKeep = 1.6f;

    [Header("空気ゲージ（ガス欠のお手本だけ。本物と同じ減り方・溜めで戻る）")]
    [Tooltip("ON：撃つと空気が減り、足りない時はスカッと空振りする。ポンプの大きさ・色と銃の横のゲージも本物と同じに動く")]
    public bool useAir = false;
    [Tooltip("銃の横の空気ゲージ（お手本の銃の GunGaugeL/R）")]
    public Renderer[] gunGauges;
    [Tooltip("空振りの音（本物の銃の EmptySfx のコピー）")]
    public AudioSource emptySound;
    [Tooltip("ゲージの色を読むだけ（本物の HUD）")]
    public FuwaHud hudColors;

    [Header("横風（風のお手本だけ。本物の FuwaWindArea と同じく、飛んでいる間ずっと加速度をかける）")]
    [Tooltip("風の加速度（ワールドの向き×強さ m/s²）。0なら風なし")]
    public Vector3 windAccel = Vector3.zero;

    private Vector3 _v;
    private Vector3 _spinAxis;
    private float _spin;
    private bool _flying;
    private int _nextShot;
    private float _cycleStart = -999f;
    private float _chargeFrom = -1f;   // 溜めはじめた時刻（溜めていない時は -1）
    private float _chargeTarget;
    private bool _fullPlayed;
    private float _shotTime = -999f;
    private float _shotCharge;
    private Vector3 _ballBaseScale;
    private Vector3 _bulbBaseScale;
    private Vector3 _bulbBasePos;
    private Material _furMat;
    private Material _orbMat;
    private Vector3 _furDrag;
    private float _puffBaseSpeed = 1f;
    private float _puffBaseVolume = 1f;
    private float _puffBasePitch = 1f;
    private float _ringBaseSize = -1f;
    private bool _active;
    private float _nextRangeCheck;
    private Quaternion _gunBaseRot;
    private float _air = 1f;
    private float _lastRealShot = -999f;
    private Material _bulbMat;
    private Color _bulbBaseColor = Color.white;
    private Material[] _gaugeMats;
    private float _dampStart = -10f, _dampHold, _dampTime, _dampAmount, _dampGlow;
    private Vector3 _dampDir;
    private float _glowAmount;
    private Color _glowColor = Color.white;
    private Vector3 _glowDir = Vector3.up;

    private void Start()
    {
        if (ball != null) _ballBaseScale = ball.localScale;
        if (pumpBulb != null)
        {
            _bulbBaseScale = pumpBulb.localScale; _bulbBasePos = pumpBulb.localPosition;
            Renderer br = pumpBulb.GetComponent<Renderer>();
            if (useAir && br != null) { _bulbMat = br.material; _bulbBaseColor = _bulbMat.color; }
        }
        if (useAir && gunGauges != null)
        {
            _gaugeMats = new Material[gunGauges.Length];
            for (int i = 0; i < gunGauges.Length; i++) if (gunGauges[i] != null) _gaugeMats[i] = gunGauges[i].material;
        }
        if (ballFur != null) _furMat = ballFur.material;
        if (chargeOrb != null)
        {
            Renderer r = chargeOrb.GetComponent<Renderer>();
            if (r != null) _orbMat = r.material;
            chargeOrb.gameObject.SetActive(false);
        }
        if (puffEffect != null) _puffBaseSpeed = puffEffect.main.startSpeedMultiplier;
        if (puffSound != null) { _puffBaseVolume = puffSound.volume; _puffBasePitch = puffSound.pitch; }
        if (airRing != null) _ringBaseSize = airRing.main.startSizeMultiplier;
        if (gun != null) _gunBaseRot = gun.rotation;
        SetActive(false);
    }

    private void SetActive(bool on)
    {
        _active = on;
        if (ball != null) ball.gameObject.SetActive(on);
        if (!on) StopCharge();
        if (!on && aimLine != null) aimLine.enabled = false;
        if (on) BeginCycle();
    }

    private void Update()
    {
        if (Time.time >= _nextRangeCheck)
        {
            _nextRangeCheck = Time.time + 0.5f;
            VRCPlayerApi lp = Networking.LocalPlayer;
            bool near = lp != null && lp.IsValid() && (lp.GetPosition() - transform.position).sqrMagnitude <= activeRange * activeRange;
            if (near != _active) SetActive(near);
        }
        if (!_active || ball == null) return;

        float t = Time.time - _cycleStart;
        if (t >= cycleLength) { BeginCycle(); t = Time.time - _cycleStart; }
        if (_skipCycle)
        {
            ball.localScale = Vector3.one * 0.001f;
            UpdateGunAim();
            return;
        }

        // 次の弾の溜めはじめ・発射
        if (!_popped && tuning != null && shotTimes != null && _nextShot < shotTimes.Length)
        {
            float charge = Charge(_nextShot);
            float hold = (shotHolds != null && _nextShot < shotHolds.Length) ? Mathf.Max(0f, shotHolds[_nextShot]) : 0f;
            float lead = charge > 0f ? tuning.chargeDelay + charge * tuning.chargeTime + hold : 0f;
            float at = shotTimes[_nextShot];
            if (charge > 0f && _chargeFrom < 0f && t >= at - lead + tuning.chargeDelay) StartCharge(charge);
            if (t >= at) { Fire(charge, AimOffset(_nextShot)); _nextShot++; }
        }

        UpdateGunAim();
        UpdateChargeFx();
        UpdateAir();
        UpdateBulb();
        UpdateBallLook(t);
        UpdateGlow();
        UpdateAimLine(t);
    }

    private void UpdateAimLine(float t)
    {
        if (aimLine == null || nozzle == null) return;
        // 撃つ1秒前（溜め中）〜撃った少し後まで
        bool show = (_chargeFrom >= 0f) || (Time.time - _shotTime < aimLineKeep);
        if (!show && shotTimes != null && _nextShot < shotTimes.Length) show = shotTimes[_nextShot] - t < 1.0f;
        if (aimLine.enabled != show) aimLine.enabled = show;
        if (!show) return;
        aimLine.SetPosition(0, nozzle.position);
        aimLine.SetPosition(1, nozzle.position + nozzle.forward * aimLineLength);
    }

    private float Charge(int i)
    {
        return (shotCharges != null && i < shotCharges.Length) ? Mathf.Clamp01(shotCharges[i]) : 1f;
    }

    private Vector3 AimOffset(int i)
    {
        return (shotAimOffsets != null && i < shotAimOffsets.Length) ? shotAimOffsets[i] : Vector3.zero;
    }

    // 1回分の始まりは、時計で cycleLength ごとの区切り（timeOffset ずらし）にそろえる。
    // こうすると、並べた台どうしが同じタイミングで撃つ（近づいた瞬間がばらばらでも）。
    // 区切りの途中で近づいた時は、その回はふわふわを出さずに次の区切りから始める
    private bool _skipCycle;
    private bool _popped;

    private void BeginCycle()
    {
        float len = Mathf.Max(0.1f, cycleLength);
        _cycleStart = Mathf.Floor((Time.time - timeOffset) / len) * len + timeOffset;
        _skipCycle = Time.time - _cycleStart > 0.2f;
        _nextShot = 0;
        _flying = false;
        _popped = false;
        _v = Vector3.zero;
        _spin = 0f;
        _air = 1f;
        _lastRealShot = -999f;
        _dampStart = -10f; _dampGlow = 0f;
        StopCharge();
        if (ballStart != null) { ball.position = ballStart.position; ball.rotation = ballStart.rotation; }
    }

    // 狙う所：ふわふわの中心＋（銃から見た）ずれ
    private Vector3 AimPoint(Vector3 offset)
    {
        Vector3 to = ball.position - nozzle.position;
        Vector3 fwd = to.sqrMagnitude > 0.0001f ? to.normalized : gun.forward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        if (right.sqrMagnitude < 0.0001f) right = gun.right; else right.Normalize();
        Vector3 up = Vector3.Cross(fwd, right);
        return ball.position + right * offset.x + up * offset.y + fwd * offset.z;
    }

    [Tooltip("撃ってから次の狙いに向き直るまでの待ち（秒）。撃った後に飛んでいくふわふわを追いかけないように")]
    public float holdAfterShot = 0.8f;
    [Tooltip("ON：銃は置いた向きのまま動かさない（向きは組み立てる時に決めておく）。OFF：撃つ前にふわふわの方へ向き直る")]
    public bool keepGunPose = true;

    private void UpdateGunAim()
    {
        if (gun == null || nozzle == null || keepGunPose) return;
        // 最後の弾を撃った後は、その向きのまま止める（飛んでいくふわふわを目で追わない）
        if (shotTimes == null || _nextShot >= shotTimes.Length) return;
        if (Time.time - _shotTime < holdAfterShot) return;
        Vector3 off = (shotTimes != null && _nextShot < shotTimes.Length) ? AimOffset(_nextShot) : Vector3.zero;
        Vector3 dir = AimPoint(off) - nozzle.position;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up);
        gun.rotation = Quaternion.Slerp(gun.rotation, want, 1f - Mathf.Exp(-aimTurnSpeed * Time.deltaTime));
    }

    private void FixedUpdate()
    {
        if (!_active || ball == null) return;
        float dt = Time.fixedDeltaTime;
        // 溜めている間の引き寄せ（本物と同じ）
        if (_skipCycle || _popped) return;
        if (useChargePull && _chargeFrom >= 0f && tuning != null && nozzle != null)
        {
            float c = ChargeNow();
            Vector3 pull = tuning.DemoChargePull(nozzle.position, nozzle.forward, c, ball.position);
            if (pull != Vector3.zero) { _v += pull * dt; _flying = true; }
        }
        if (!_flying) return;
        // FuwaBall.FixedUpdate と同じ：重力の代わりのゆっくり落下、空気抵抗、落ちる速さと全体の速さの上限
        float fall = ballTuning != null ? ballTuning.fallAcceleration : 1.5f;
        float drag = ballTuning != null ? ballTuning.airDrag : 1.2f;
        float maxFall = ballTuning != null ? ballTuning.maxFallSpeed : 0.7f;
        float maxSpeed = ballTuning != null ? ballTuning.maxSpeed : 7f;
        if (windAccel != Vector3.zero) _v += DampWind(windAccel) * dt;
        _v += Vector3.down * fall * dt;
        _v *= Mathf.Clamp01(1f - drag * dt);
        if (_v.y < -maxFall) _v.y = -maxFall;
        if (_v.magnitude > maxSpeed) _v = _v.normalized * maxSpeed;
        Vector3 p = ball.position + _v * dt;
        ball.position = p;
        // 床の高さまで落ちて、真下に本当に床がある時だけ弾ける（台の外の空中へ飛んだ時は、そのまま落ちていって回の終わりに消える）
        if (floor != null && p.y < floor.position.y + BallRadius() && p.y > floor.position.y - 0.3f
            && Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, 1.2f, floorMask, QueryTriggerInteraction.Ignore))
        {
            _v = Vector3.zero;
            _flying = false;
            _popped = true;
            if (popEffect != null) { popEffect.transform.position = p; popEffect.Play(); }
            if (popSound != null) { popSound.transform.position = p; popSound.Play(); }
            return;
        }
        if (_spin > 0.001f)
        {
            ball.Rotate(_spinAxis, _spin * Mathf.Rad2Deg * dt, Space.World);
            _spin *= Mathf.Clamp01(1f - (ballTuning != null ? ballTuning.spinDamping : 0.6f) * dt);
        }
    }

    private float BallRadius()
    {
        return ball.lossyScale.y * 0.5f;
    }

    private float ChargeNow()
    {
        if (_chargeFrom < 0f || tuning == null) return 0f;
        float held = Time.time - _chargeFrom;
        // 撃つ時刻から逆算して溜めはじめているので、撃つ瞬間にちょうど狙った溜め（_chargeTarget）になる
        return tuning.chargeTime > 0.001f ? Mathf.Clamp01(held / tuning.chargeTime) : 1f;
    }

    private void StartCharge(float target)
    {
        _chargeFrom = Time.time;
        _chargeTarget = target;
        _fullPlayed = false;
        if (chargeSound != null) chargeSound.Play();
        if (chargeParticles != null) chargeParticles.Play();
    }

    private void StopCharge()
    {
        _chargeFrom = -1f;
        if (chargeSound != null) chargeSound.Stop();
        if (chargeParticles != null) chargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetOrb(0f);
    }

    private void UpdateChargeFx()
    {
        if (_chargeFrom < 0f) return;
        float c = ChargeNow();
        if (c >= 1f && !_fullPlayed)
        {
            _fullPlayed = true;
            if (chargedSound != null) chargedSound.Play();
            if (chargedSparkle != null) chargedSparkle.Play();
        }
        SetOrb(c);
    }

    private void SetOrb(float c)
    {
        if (chargeOrb == null) return;
        bool on = c > 0f;
        if (chargeOrb.gameObject.activeSelf != on) chargeOrb.gameObject.SetActive(on);
        if (!on) return;
        float maxS = tuning != null ? tuning.orbMaxScale : 0.07f;
        float wobble = c >= 1f ? 1f + 0.2f * Mathf.Sin(Time.time * 32f) : 1f + 0.06f * Mathf.Sin(Time.time * (20f + 40f * c));
        chargeOrb.localScale = Vector3.one * maxS * Mathf.Lerp(0.2f, 1f, c) * wobble;
        if (_orbMat != null)
        {
            Color col = c < 1f
                ? Color.Lerp(new Color(0.55f, 0.85f, 1f, 0.4f), new Color(1f, 1f, 1f, 0.9f), c)
                : Color.Lerp(new Color(1f, 0.85f, 0.35f, 0.95f), new Color(1f, 1f, 0.9f, 1f), 0.5f + 0.5f * Mathf.Sin(Time.time * 32f));
            _orbMat.color = col;
        }
    }

    private void Fire(float charge, Vector3 aimOffset)
    {
        StopCharge();
        if (tuning == null || nozzle == null) return;
        // 空気が1発分ないと空振り（本物と同じ：スカッと鳴るだけで何も出ない）
        if (useAir)
        {
            if (_air + 0.0001f < tuning.airPerTap)
            {
                if (emptySound != null) emptySound.Play();
                return;
            }
            _air = Mathf.Max(0f, _air - tuning.airPerTap);
            _lastRealShot = Time.time;
        }
        _shotTime = Time.time;
        _shotCharge = charge;
        // 撃つ瞬間は狙った所へぴったり向ける（向き直り途中のずれでお手本が変わらないように）
        if (!keepGunPose)
        {
            Vector3 dir = AimPoint(aimOffset) - nozzle.position;
            if (dir.sqrMagnitude > 0.0001f) gun.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        Vector3 dv = tuning.DemoShotVelocity(nozzle.position, nozzle.forward, charge, ball.position);
        PlayShotFx(charge);
        if (dv == Vector3.zero) return;
        // FuwaBall.Blow と同じ：落ちている最中に下から吹かれたら、落下を止めてから持ち上げる
        if (_v.y < 0f && dv.y > 0f) _v.y = 0f;
        _v += dv;
        _flying = true;
        // FuwaBall.ShotWindDamp と同じ：撃った直後はしばらく横風の効きが弱まる（溜めるほど強く長く）
        if (ballTuning != null)
        {
            _dampStart = Time.time;
            _dampAmount = Mathf.Lerp(ballTuning.shotWindDampMin, ballTuning.shotWindDampMax, charge);
            _dampHold = ballTuning.shotWindDampHoldMax * charge * charge;
            _dampTime = Mathf.Lerp(ballTuning.shotWindDampTimeMin, ballTuning.shotWindDampTimeMax, charge);
            _dampDir = dv.normalized;
            _dampGlow = ballTuning.resistGlowMax * charge;
        }
        Vector3 flat = new Vector3(dv.x, 0f, dv.z);
        if (flat.sqrMagnitude > 0.0001f && ballTuning != null)
        {
            _spinAxis = Vector3.Cross(Vector3.up, flat.normalized);
            _spin = ballTuning.spinPerPuff;
        }
    }

    // 本物の PlayShotEffect と同じ見た目と音（風の粒は射程まで飛ぶ）
    private void PlayShotFx(float charge)
    {
        if (puffEffect != null && tuning != null)
        {
            float reach = tuning.range * Mathf.Lerp(tuning.minRangeRatio, tuning.maxRangeRatio, charge);
            ParticleSystem.MainModule main = puffEffect.main;
            main.startSpeedMultiplier = _puffBaseSpeed * (tuning.puffBaseDistance > 0.001f ? reach / tuning.puffBaseDistance : 1f);
        }
        if (charge >= 1f)
        {
            if (airRing != null && _ringBaseSize >= 0f) { ParticleSystem.MainModule rm = airRing.main; rm.startSizeMultiplier = _ringBaseSize; }
            if (puffEffect != null) { puffEffect.Play(); puffEffect.Emit(tuning != null ? tuning.strongPuffExtra : 40); }
            if (fullBurst != null) fullBurst.Play();
            if (puffBigSound != null) puffBigSound.Play();
        }
        else
        {
            if (airRing != null && _ringBaseSize >= 0f) { ParticleSystem.MainModule rm = airRing.main; rm.startSizeMultiplier = _ringBaseSize * (1f + (tuning != null ? tuning.midRingGrow : 0.5f) * charge); }
            if (puffEffect != null) { puffEffect.Play(); int extra = Mathf.RoundToInt((tuning != null ? tuning.midPuffExtra : 6) * charge); if (extra > 0) puffEffect.Emit(extra); }
            if (puffSound != null) { puffSound.volume = _puffBaseVolume * 0.6f; puffSound.pitch = _puffBasePitch * 1.15f; puffSound.Play(); }
        }
    }

    // 空気の回復（本物の UpdateAir と同じ）：溜めている間はポンプで早く、撃たずにいると自然に。
    // ゲージの色も本物の HUD と同じ（減ると赤へ、足りない時は点滅、溜めると金色へ）
    private void UpdateAir()
    {
        if (!useAir || tuning == null) return;
        if (_chargeFrom >= 0f)
        {
            if (tuning.airChargeRefillTime > 0.001f) _air += Time.deltaTime / tuning.airChargeRefillTime;
        }
        else if (Time.time - _lastRealShot >= tuning.airRegenDelay)
        {
            if (tuning.airRegenTime > 0.001f) _air += Time.deltaTime / tuning.airRegenTime;
        }
        _air = Mathf.Clamp01(_air);
        bool empty = _air + 0.0001f < tuning.airPerTap;
        float c = ChargeNow();
        if (_gaugeMats != null && hudColors != null)
        {
            Color low = hudColors.airColorLow;
            Color g = empty
                ? Color.Lerp(low, new Color(low.r, low.g, low.b, 0.35f), 0.5f + 0.5f * Mathf.Sin(Time.time * 14f))
                : Color.Lerp(low, hudColors.airColorFull, Mathf.Clamp01(_air * 2.5f));
            if (c >= 1f) g = Color.Lerp(hudColors.airColorCharged, Color.white, (0.5f + 0.5f * Mathf.Sin(Time.time * 18f)) * 0.6f);
            else if (c > 0f) g = Color.Lerp(Color.Lerp(g, hudColors.airColorCharged, c * c), Color.white, 0.45f * Mathf.Sin(c * Mathf.PI));
            for (int i = 0; i < _gaugeMats.Length; i++)
            {
                if (_gaugeMats[i] == null) continue;
                _gaugeMats[i].SetFloat("_Fill", _air);
                _gaugeMats[i].SetColor("_FillColor", g);
            }
        }
        if (_bulbMat != null)
        {
            Color b = Color.Lerp(tuning.bulbEmptyColor, _bulbBaseColor, _air);
            if (empty) b = Color.Lerp(tuning.bulbEmptyColor, tuning.bulbWarnColor, 0.5f + 0.5f * Mathf.Sin(Time.time * 14f));
            else if (c >= 1f) b = Color.Lerp(b, tuning.bulbFullFlashColor, (0.5f + 0.5f * Mathf.Sin(Time.time * 9f)) * tuning.bulbFullFlash);
            _bulbMat.color = b;
        }
    }

    // ポンプ：溜めでふくらみ、撃つとしぼむ（本物の UpdateBulb と同じ形）
    private void UpdateBulb()
    {
        if (pumpBulb == null || tuning == null) return;
        float inflate = 0f;
        float since = Time.time - _shotTime;
        if (_chargeFrom >= 0f)
        {
            float c = ChargeNow();
            float ease = 1f - (1f - c) * (1f - c);
            inflate = tuning.bulbMaxInflate * ease;
            if (c >= 1f) inflate += 0.04f * Mathf.Sin(Time.time * 28f);
        }
        else if (_shotCharge < 0.2f && since < 0.16f)
        {
            inflate = tuning.bulbTapInflate * Mathf.Sin(Mathf.PI * since / 0.16f);
        }
        else if (_shotCharge >= 0.2f && since < 0.3f)
        {
            float t = since / 0.3f;
            inflate = tuning.bulbMaxInflate * _shotCharge * Mathf.Max(0f, 1f - t * 3f) - 0.12f * Mathf.Sin(Mathf.PI * t);
        }
        float airScale = useAir ? Mathf.Lerp(tuning.bulbEmptyScale, 1f, _air) : 1f;   // 空気が減るとしぼむ
        Vector3 s3 = _bulbBaseScale * (airScale * (1f + inflate * 1.05f));
        pumpBulb.localScale = s3;
        pumpBulb.localPosition = _bulbBasePos + new Vector3(0f, 0f, 0.5f * (_bulbBaseScale.z - s3.z));
    }

    // まもの（FuwaDemoMonster）に体当たりされた時：床に落ちた時と同じようにはじけて、この回はもう出ない
    public void PopBall()
    {
        if (_popped || _skipCycle || ball == null) return;
        _v = Vector3.zero;
        _flying = false;
        _popped = true;
        Vector3 p = ball.position;
        if (popEffect != null) { popEffect.transform.position = p; popEffect.Play(); }
        if (popSound != null) { popSound.transform.position = p; popSound.Play(); }
    }

    private float DampLeft()
    {
        float t = Time.time - _dampStart - _dampHold;
        if (_dampTime <= 0f || t >= _dampTime) return 0f;
        return 1f - Mathf.Max(0f, t) / _dampTime;
    }

    // FuwaBall.DampWind と同じ：撃った向きと同じ向きの風はそのまま、横・逆向きの分だけ弱める
    private Vector3 DampWind(Vector3 acc)
    {
        float left = DampLeft();
        if (left <= 0f) return acc;
        float k = 1f - _dampAmount * left;
        float along = Vector3.Dot(acc, _dampDir);
        Vector3 keep = along > 0f ? _dampDir * along : Vector3.zero;
        return keep + (acc - keep) * k;
    }

    // ふわふわの光（FuwaBall.UpdateGlow を簡単にしたもの）：風の中を飛んでいる間は風の色、溜め撃ちの直後は黄色
    private void UpdateGlow()
    {
        if (_furMat == null || ballTuning == null) return;
        if (windAccel == Vector3.zero && _dampGlow <= 0f && _glowAmount <= 0f) return;
        float target = 0f;
        float mag = windAccel.magnitude;
        if (_flying && !_popped && mag > 0.05f)
        {
            target = Mathf.Sqrt(Mathf.Clamp01(mag / Mathf.Max(0.01f, ballTuning.windGlowFull)));
            _glowColor = ballTuning.windGlowColor; _glowDir = windAccel / mag;
        }
        Color gc = _glowColor; Vector3 gd = _glowDir;
        float u = ballTuning.resistGlowTime > 0f ? Mathf.Clamp01((Time.time - _dampStart) / ballTuning.resistGlowTime) : 1f;
        float resist = _popped ? 0f : _dampGlow * (1f - u) * (1f - u);
        if (resist > 0.001f)
        {
            float mix = target > 0.01f ? Mathf.Clamp01(resist / target) : 1f;
            gc = Color.Lerp(_glowColor, ballTuning.resistGlowColor, mix); gd = mix > 0.5f ? _dampDir : _glowDir;
            target = Mathf.Max(target, resist);
        }
        float speed = target > _glowAmount ? 8f : 3f;
        _glowAmount = Mathf.MoveTowards(_glowAmount, target, speed * Time.deltaTime);
        _furMat.SetColor("_GlowColor", new Color(gc.r, gc.g, gc.b, _glowAmount));
        _furMat.SetVector("_GlowDir", new Vector4(gd.x, gd.y, gd.z, 0f));
    }

    // ふわふわ：出てくる時ふくらんで、終わりぎわに縮んで消える。毛は動きと反対へなびく
    private void UpdateBallLook(float t)
    {
        float appear = Mathf.Clamp01(t / 0.25f);
        float vanish = vanishTime > 0.001f ? Mathf.Clamp01((cycleLength - t) / vanishTime) : 1f;
        float s = _popped ? 0f : Mathf.Min(1f - (1f - appear) * (1f - appear), vanish);
        ball.localScale = _ballBaseScale * Mathf.Max(0.001f, s);
        if (!_flying) ball.Rotate(new Vector3(0.3f, 1f, 0.15f), (ballTuning != null ? ballTuning.idleSpin : 25f) * Time.deltaTime, Space.World);
        if (_furMat != null && ballTuning != null)
        {
            Vector3 target = Vector3.ClampMagnitude(-_v * ballTuning.furDragAmount, ballTuning.furDragMax);
            _furDrag = Vector3.Lerp(_furDrag, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            _furMat.SetVector("_Drag", new Vector4(_furDrag.x, _furDrag.y, _furDrag.z, 0f));
        }
    }
}
