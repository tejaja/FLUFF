using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2026-11-15 2–5 上の段：クリスタルの広場（てじゃの案）
//  「円形の広場、中央には浮いたクリスタル」「一定間隔で広場全体に放射状に衝撃波、プレイヤーは吹き飛ばされる、ほぼアウト」
//  「衝撃波発生源から見てふわふわのバリアの周囲+後ろ側はセーフゾーン」「広場に入る時に最初に一発」「衝撃波前は予兆を音と目で」「完全ローカルでいい」
//  ・自分が広場に入ったら、すぐためはじめ（chargeTime）→ ドン。あとは period ごと。広場から出たら止まる（次に入るとまた1発目から）
//  ・ため：クリスタルが光をためて速く回る、外から光の輪がクリスタルへちぢむ、粒が集まる、ためる音
//  ・ドン：クリスタルから光の壁（wave）と床の輪（waveFloor）が waveSpeed で広がる。壁が自分の所を通る瞬間に判定
//    セーフ：バリアのまわり（nearPad）か、クリスタルから見てバリアの後ろ（上から見た2D の影、後ろほど広がる＋shadowPad）
//    アウトじゃない時：外向きに playerPush・上に playerUp で吹き飛ばす（広場にかこいはないので、ほぼ落ちる）
//  ・ふわふわはバリアがあれば動かない（バリアに波紋だけ）。バリアがない時は外へ弾く
//  ・光の壁はバリアの影の所だけ欠ける（シェーダー Fuwa/Shockwave に _Src・_Ball を渡す）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaShockwave : UdonSharpBehaviour
{
    public FuwaBall ball;
    public FuwaShield shield;

    [Header("広場（この物体の位置＝床のまん中）")]
    public float radius = 9f;
    public float enterMargin = 5f, leaveMargin = 5.5f;   // 2026-11-15 手前から察知（0.3/2.5 → 5/5.5）
    public float heightBelow = 2f, heightAbove = 7f;

    [Header("リズム")]
    public float chargeTime = 2f;   // 2026-11-15 てじゃ「チャージ時間伸ばして」1.3 → 2
    [Tooltip("入った時の1発目だけのため（秒）。2026-11-15 てじゃ「初回の一撃もっと早めに」")]
    public float firstChargeTime = 2f;   // 2026-11-15 てじゃ「初回チャージ速すぎ」→ ふつうと同じ（手前から察知するので間に合う）
    [Tooltip("ドンからドンまで（秒）")]
    public float period = 5f;

    [Header("衝撃波")]
    public float waveSpeed = 9f;
    public float waveHeight = 3.2f;
    public float waveExtra = 1.5f;
    public float playerPush = 14f, playerUp = 6f;
    public float ballPush = 9f, ballUp = 3f;
    [Tooltip("バリアのまわりのセーフ（バリアのふちからの距離 m）")]
    public float nearPad = 0.8f;
    [Tooltip("2026-11-15 てじゃ「手前の道は影響あってもいいかも、バリアより奥」→ 広場のほかに、入口の道（広場のまん中から corridorDir の向き、はば corridorHalfW、まん中から corridorLen まで）も当たる")]
    public Vector3 corridorDir = Vector3.zero;
    public float corridorHalfW = 2.7f, corridorLen = 0f;
    [Tooltip("2026-11-16 てじゃ「出口のゲート以降は衝撃波届かなくしたい」→ exitPoint から exitDir の向きの先は当たらない・波も見せない")]
    public bool exitCut = false;
    [Tooltip("2026-11-16 てじゃ「広場の外周（端）にも衝撃は欲しい」：当たる範囲＝広場の半径＋これ（外の一段低いふちまで）")]
    public float hitPad = 0.5f;
    public Vector3 exitPoint, exitDir;
    [Tooltip("影の横のゆとり（m）")]
    public float shadowPad = 0.8f;   // 2026-11-15 nearPad とそろえる（0.45 → 0.8）

    [Header("見た目")]
    public Transform crystal;
    public Renderer[] crystalGlows;
    public Color glowColor = new Color(0.45f, 0.9f, 1f, 1f);
    public float glowIdle = 0.45f, glowCharged = 2.6f;
    public float spinIdle = 25f, spinCharged = 420f;
    public Transform wave, waveFloor, chargeRing;
    [Tooltip("2026-11-15 見た目（7）：光の壁のあとを追う砂ぼこりの輪（Fuwa/DustRing）と、壁の残像（Fuwa/Shockwave）")]
    public Transform dustRing, waveEcho;
    public Renderer dustRenderer, echoRenderer;
    public Renderer[] waveRenderers;
    public Renderer chargeRenderer;
    public ParticleSystem chargeParticles, blastParticles;
    public int blastCount = 60;
    public Renderer[] runeGlows;   // 床のまわりの模様（ためると光る）
    [Tooltip("2026-11-15 てじゃ「衝撃波の範囲、もっと目立たせたいな、特に床」→ 床にかぶせる円盤（Fuwa/ShockFloor）：ためで床いっぱいに流れる光、セーフ（影）は抜けて白いふち、ドンで帯と尾")]
    public Renderer floorZone;
    [Tooltip("2026-11-15 てじゃ「セーフティーゾーンの周りにうっすら白いヴェールほしい」：影のふちに立つ幕（Fuwa/SafeVeil）。床と同じ時に出る")]
    public Renderer safeVeil;
    [Tooltip("2026-11-15 てじゃ「チャージで空もちょっと暗くするとか」→ 空だけ暗くする大きい球（Fuwa/SkyDim）")]
    public Renderer skyDim;
    public float skyDimMax = 0.45f;

    [Header("クリスタルのため（2026-11-15 てじゃ「クリスタル本体にもエネルギーためてる感じ」）")]
    public Transform[] shards;          // まわりのかけら（ためると引きよせられる、ドンではじける）
    public Renderer auraRenderer;       // まわりの光（Fuwa/EnergyGlow）
    public Renderer magicCircle;        // 床の魔法陣（Fuwa/MagicCircle の _Charge・_Flash）
    public float auraIdle = 1.6f, auraCharged = 3.4f;
    public Transform[] orbitRings;      // 斜めの光の輪（ためるとちぢんで速く回る）
    public Renderer[] orbitRingRenderers;
    public float ringIdleR = 2.2f, ringChargedR = 1.15f;
    [Tooltip("2026-11-15 てじゃ「チャージでクリスタルも黒オーラみたいな色つけよ」「撃ったらもどる」→ ためる間だけ黒紫のもや（Fuwa/DarkAura）＋クリスタルの光を紫へ")]
    public Renderer darkAura;
    [Tooltip("2026-11-16 てじゃ「チャージでこれも黒くしよ」：まん中の円柱バリア（_Dark をために合わせて上げる）")]
    public Renderer pillarBarrier;
    public float darkAuraMax = 1.1f;
    public Color chargeTint = new Color(0.62f, 0.2f, 0.95f, 1f);
    private float _dark;

    [Header("音")]
    public AudioSource chargeSound, boomSound, hitSound;

    private bool _active;
    private float _offAt = -10f;
    private float _nextCharge = -1f;
    private bool _charging;
    private bool _first;
    private float _curCharge = 1.3f;
    private float _blastAt = -10f;
    private bool _waveLive;
    private bool _playerDone, _ballDone;
    private Vector3 _src;
    private float _spinAngle, _bob;
    private Vector3 _crystal0;
    private Quaternion _crystalRot0;
    private float _flash;
    private MaterialPropertyBlock _mpb;
    private Vector3[] _shard0;
    private Quaternion[] _ring0;
    private float _ringAng, _pulsePh, _kSmooth;

    private void Start()
    {
        _mpb = new MaterialPropertyBlock();
        if (crystal != null) { _crystal0 = crystal.localPosition; _crystalRot0 = crystal.localRotation; }
        if (shards != null) { _shard0 = new Vector3[shards.Length]; for (int i = 0; i < shards.Length; i++) if (shards[i] != null) _shard0[i] = shards[i].localPosition; }
        if (orbitRings != null) { _ring0 = new Quaternion[orbitRings.Length]; for (int i = 0; i < orbitRings.Length; i++) if (orbitRings[i] != null) _ring0[i] = orbitRings[i].localRotation; }
        HideWave();
        if (chargeRenderer != null) chargeRenderer.enabled = false;
        if (floorZone != null) floorZone.enabled = false;
        if (safeVeil != null) safeVeil.enabled = false;
        if (skyDim != null) skyDim.enabled = false;
    }

    private void HideWave()
    {
        _waveLive = false;
        if (dustRenderer != null) dustRenderer.enabled = false;
        if (echoRenderer != null) echoRenderer.enabled = false;
        if (waveRenderers != null) for (int i = 0; i < waveRenderers.Length; i++) if (waveRenderers[i] != null) waveRenderers[i].enabled = false;
    }

    private void Update()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        Vector3 c = transform.position;
        Vector3 pp = lp.GetPosition();
        // 2026-11-17 軽くする：止まっていて、波も出ていなくて、自分もふわふわも60mより遠い間は何もしない（ロビーから見ている時など。1つで約0.3ms）
        if (!_active && !_waveLive && Time.time - _offAt > 3f && (pp - c).sqrMagnitude > 3600f
            && (ball == null || (ball.transform.position - c).sqrMagnitude > 3600f)) return;
        float dx = pp.x - c.x, dz = pp.z - c.z, d2 = Mathf.Sqrt(dx * dx + dz * dz), dy = pp.y - c.y;
        bool band = dy > -heightBelow && dy < heightAbove;
        float now = Time.time;
        // 2026-11-15 てじゃ「最初の一撃、ふわふわに反応させよう」→ ふわふわが広場に入ったらためはじめ（1発目）。
        //  そのあとは ふわふわ か 自分 のどちらかが広場にいる間つづく。両方出たら止まる
        bool ballIn = false, ballNear = false;
        if (ball != null && !ball.IsWaitingOrHidden())
        {
            Vector3 bp0 = ball.transform.position;
            float bx = bp0.x - c.x, bz = bp0.z - c.z, bd0 = Mathf.Sqrt(bx * bx + bz * bz), by = bp0.y - c.y;
            bool bband = by > -heightBelow && by < heightAbove;
            ballIn = bband && bd0 < radius + enterMargin;
            ballNear = bband && bd0 < radius + leaveMargin;
        }
        bool playerNear = band && d2 < radius + leaveMargin;
        if (!_active)
        {
            if (ballIn)
            {
                _active = true; _nextCharge = now; _charging = false; _first = true;
            }
        }
        else if (!ballNear && !playerNear)
        {
            _active = false; _charging = false; _nextCharge = -1f; _offAt = now;
            if (chargeSound != null) chargeSound.Stop();
            if (chargeParticles != null) { var em = chargeParticles.emission; em.enabled = false; }
            if (chargeRenderer != null) chargeRenderer.enabled = false;
        }

        // ---- ため ----
        float k = 0f;
        if (_active)
        {
            if (!_charging && now >= _nextCharge)
            {
                _charging = true;
                _curCharge = _first ? firstChargeTime : chargeTime;
                _first = false;
                // ためる音は chargeTime の長さなので、短いためは音の途中から（最後のドンの直前に合わせる）
                if (chargeSound != null) { chargeSound.Play(); if (_curCharge < chargeTime) chargeSound.time = Mathf.Max(0f, chargeTime - _curCharge); }
                if (chargeParticles != null) { var em = chargeParticles.emission; em.enabled = true; }
            }
            if (_charging)
            {
                k = Mathf.Clamp01((now - _nextCharge) / Mathf.Max(0.01f, _curCharge));
                if (k >= 1f) { Blast(now); k = 0f; }
            }
        }
        UpdateCrystal(k);
        UpdateChargeRing(k);
        UpdateFloorZone(k, now);
        UpdateSkyDim();

        // ---- 広がる衝撃波 ----
        if (_waveLive)
        {
            float r = (now - _blastAt) * waveSpeed;
            float rEnd = WaveEnd();
            if (r > rEnd) HideWave();
            else
            {
                float fade = 1f - Mathf.Clamp01((r - radius * 0.75f) / (rEnd - radius * 0.75f));
                fade *= Mathf.Clamp01(r / 0.8f);
                if (wave != null) wave.localScale = new Vector3(r, waveHeight, r);
                if (waveFloor != null) waveFloor.localScale = new Vector3(r, 1f, r);
                SetWaveProps(fade);
                UpdateTrail(r, fade);
                // 自分
                if (!_playerDone)
                {
                    float pdx = pp.x - _src.x, pdz = pp.z - _src.z;
                    float pd = Mathf.Sqrt(pdx * pdx + pdz * pdz);
                    if (r >= pd)
                    {
                        _playerDone = true;
                        if (_active && band && InZone(pp) && dy < waveHeight + 0.5f && !Safe(pp))
                        {
                            Vector3 o = new Vector3(pdx, 0f, pdz);
                            o = o.sqrMagnitude > 1e-4f ? o.normalized : Vector3.forward;
                            lp.SetVelocity(o * playerPush + Vector3.up * playerUp);
                            if (hitSound != null) hitSound.Play();
                        }
                    }
                }
                // ふわふわ
                if (!_ballDone && ball != null)
                {
                    Vector3 bp = ball.transform.position;
                    float bdx = bp.x - _src.x, bdz = bp.z - _src.z;
                    float bd = Mathf.Sqrt(bdx * bdx + bdz * bdz);
                    if (r >= bd)
                    {
                        _ballDone = true;
                        float bdy = bp.y - c.y;
                        if (InZone(bp) && bdy > -heightBelow && bdy < waveHeight + 0.5f && !ball.IsWaitingOrHidden())
                        {
                            Vector3 o = new Vector3(bdx, 0f, bdz);
                            o = o.sqrMagnitude > 1e-4f ? o.normalized : Vector3.forward;
                            if (shield != null && shield.IsOn()) shield.Hit(o);
                            else ball.Launch(o * ballPush + Vector3.up * ballUp);
                        }
                    }
                }
            }
        }
    }

    private void Blast(float now)
    {
        _charging = false;
        _nextCharge = now + Mathf.Max(chargeTime + 0.2f, period) - chargeTime;
        _blastAt = now;
        _src = crystal != null ? crystal.position : transform.position;
        _playerDone = false; _ballDone = false;
        _waveLive = true;
        _flash = 1f;
        if (wave != null) { wave.position = new Vector3(_src.x, transform.position.y, _src.z); wave.localScale = new Vector3(0.01f, waveHeight, 0.01f); }
        if (waveFloor != null) { waveFloor.position = new Vector3(_src.x, transform.position.y + 0.02f, _src.z); waveFloor.localScale = new Vector3(0.01f, 1f, 0.01f); }
        if (dustRing != null) { dustRing.position = new Vector3(_src.x, transform.position.y, _src.z); dustRing.localScale = new Vector3(0.01f, 1f, 0.01f); }
        if (waveEcho != null) { waveEcho.position = new Vector3(_src.x, transform.position.y, _src.z); waveEcho.localScale = new Vector3(0.01f, waveHeight, 0.01f); }
        if (dustRenderer != null) dustRenderer.enabled = true;
        if (echoRenderer != null) echoRenderer.enabled = true;
        if (waveRenderers != null) for (int i = 0; i < waveRenderers.Length; i++) if (waveRenderers[i] != null) waveRenderers[i].enabled = true;
        if (chargeSound != null) chargeSound.Stop();
        if (boomSound != null) boomSound.Play();
        if (chargeParticles != null) { var em = chargeParticles.emission; em.enabled = false; }
        if (blastParticles != null) blastParticles.Emit(blastCount);
    }

    // 出口で切る面（xz）：(nx, nz, d, 1)。dot(p.xz, n) > d の所は消す
    private Vector4 ExitVec()
    {
        if (!exitCut) return Vector4.zero;
        Vector2 n = new Vector2(exitDir.x, exitDir.z); float l = n.magnitude; if (l < 1e-4f) return Vector4.zero; n /= l;
        return new Vector4(n.x, n.y, n.x * exitPoint.x + n.y * exitPoint.z, 1f);
    }
    private float WaveEnd() { return Mathf.Max(radius + waveExtra, corridorLen + 0.5f); }

    // 当たる所か：広場（radius+0.5）か、入口の道（corridor）
    private bool InZone(Vector3 p)
    {
        Vector3 c = transform.position;
        float dx = p.x - c.x, dz = p.z - c.z;
        if (exitCut && (p.x - exitPoint.x) * exitDir.x + (p.z - exitPoint.z) * exitDir.z > 0f) return false;   // 出口の門より先
        if (Mathf.Sqrt(dx * dx + dz * dz) <= radius + hitPad) return true;
        if (corridorLen <= 0f) return false;
        Vector2 dir = new Vector2(corridorDir.x, corridorDir.z); float dl = dir.magnitude; if (dl < 1e-4f) return false; dir /= dl;
        Vector2 q = new Vector2(dx, dz);
        float along = Vector2.Dot(q, dir), lat = Mathf.Abs(q.x * dir.y - q.y * dir.x);
        return along > 0f && along <= corridorLen && lat <= corridorHalfW;
    }

    // セーフか（p：自分の足の位置）。バリアのまわり、またはクリスタルから見てバリアの後ろ（上から見た2D）
    private bool Safe(Vector3 p)
    {
        if (shield == null || !shield.IsOn()) return false;
        Vector3 bp = shield.transform.position;
        float rs = shield.Radius();
        if (Mathf.Abs(bp.y - (p.y + 1f)) > 2.6f) return false;   // バリアが高すぎ・低すぎ
        Vector2 b = new Vector2(bp.x - _src.x, bp.z - _src.z);
        Vector2 q = new Vector2(p.x - _src.x, p.z - _src.z);
        // 2026-11-15 形を見た目（FuwaSafeShape.cginc）とそろえる：余白つきの円と、クリスタルからその円に接する2本の線（つなぎ目がなめらか）
        float r0 = rs + nearPad;
        if ((q - b).magnitude < r0) return true;
        float bl = b.magnitude; if (bl <= r0 + 0.01f) return false;
        Vector2 bh = b / bl;
        float along = Vector2.Dot(q, bh);
        float sa = r0 / bl, ca = Mathf.Sqrt(1f - sa * sa);
        if (along < bl - r0 * sa) return false;
        float perp = (q - bh * along).magnitude;
        return perp * ca - along * sa < 0f;
    }

    // 砂ぼこり（壁の少しうしろ、低い）と残像（壁の1.3m うしろ、うすい）
    private void UpdateTrail(float r, float fade)
    {
        Vector4 bv = Vector4.zero;
        if (shield != null && shield.IsOn()) { Vector3 bp = shield.transform.position; bv = new Vector4(bp.x, bp.y, bp.z, shield.Radius()); }
        Vector4 sv = new Vector4(_src.x, _src.y, _src.z, 0f);
        if (dustRing != null && dustRenderer != null)
        {
            float rd = Mathf.Max(0.05f, r - 0.7f);
            dustRing.localScale = new Vector3(rd, 1.4f, rd);
            dustRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat("_Alpha", fade * Mathf.Clamp01(r / 2.5f));
            _mpb.SetVector("_Src", sv); _mpb.SetVector("_Ball", bv); _mpb.SetFloat("_Pad", nearPad);
            dustRenderer.SetPropertyBlock(_mpb);
        }
        if (waveEcho != null && echoRenderer != null)
        {
            float re = Mathf.Max(0.05f, r - 1.3f);
            waveEcho.localScale = new Vector3(re, waveHeight * 0.85f, re);
            echoRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat("_Alpha", fade * 0.35f);
            _mpb.SetVector("_Src", sv); _mpb.SetVector("_Ball", bv); _mpb.SetFloat("_Pad", nearPad);
            echoRenderer.SetPropertyBlock(_mpb);
        }
    }

    private void SetWaveProps(float fade)
    {
        if (waveRenderers == null) return;
        Vector4 bv = Vector4.zero;
        if (shield != null && shield.IsOn()) { Vector3 bp = shield.transform.position; bv = new Vector4(bp.x, bp.y, bp.z, shield.Radius()); }
        for (int i = 0; i < waveRenderers.Length; i++)
        {
            Renderer wr = waveRenderers[i]; if (wr == null) continue;
            wr.GetPropertyBlock(_mpb);
            _mpb.SetFloat("_Alpha", fade);
            _mpb.SetVector("_Src", new Vector4(_src.x, _src.y, _src.z, 0f));
            _mpb.SetVector("_Ball", bv);
            _mpb.SetFloat("_Pad", nearPad);
            _mpb.SetVector("_Exit", ExitVec());
            wr.SetPropertyBlock(_mpb);
        }
    }

    private void UpdateCrystal(float k)
    {
        if (crystal == null) return;
        float dt = Time.deltaTime;
        _flash = Mathf.Max(0f, _flash - dt * 2.5f);
        float kk = k * k;
        _spinAngle += Mathf.Lerp(spinIdle, spinCharged, kk) * dt;
        _bob += dt;
        // ためるほど少し縮んでふるえる → ドンで少しふくらむ
        float shake = kk * 0.03f;
        Vector3 jit = new Vector3(Mathf.Sin(_bob * 61f), Mathf.Sin(_bob * 47f + 1f), Mathf.Sin(_bob * 53f + 2f)) * shake;
        crystal.localPosition = _crystal0 + Vector3.up * (0.12f * Mathf.Sin(_bob * 1.3f)) + jit;
        crystal.localRotation = _crystalRot0 * Quaternion.Euler(0f, _spinAngle, 0f);
        float sc = 1f - 0.08f * kk + 0.18f * _flash * _flash;
        crystal.localScale = Vector3.one * sc;
        if (_mpb == null) return;
        float g = (Mathf.Lerp(glowIdle, glowCharged, kk) * (1f + 0.45f * kk * (0.5f + 0.5f * Mathf.Sin(_pulsePh)))) + 4f * _flash * _flash;   // ためるほど脈うつ
        Color col = Color.Lerp(glowColor, chargeTint, darkAura != null ? kk : 0f) * g; col.a = 1f;
        if (crystalGlows != null)
            for (int i = 0; i < crystalGlows.Length; i++)
            {
                if (crystalGlows[i] == null) continue;
                crystalGlows[i].GetPropertyBlock(_mpb); _mpb.SetColor("_EmissionColor", col); crystalGlows[i].SetPropertyBlock(_mpb);
            }
        UpdateEnergy(k, dt);
        UpdateDarkAura(k, dt);
        if (magicCircle != null)
        {
            magicCircle.GetPropertyBlock(_mpb);
            _mpb.SetFloat("_Charge", k);
            _mpb.SetFloat("_Flash", _flash * _flash);
            magicCircle.SetPropertyBlock(_mpb);
        }
        if (runeGlows != null)
        {
            Color rc = glowColor * (0.4f + 3.5f * kk + 3f * _flash); rc.a = 1f;
            for (int i = 0; i < runeGlows.Length; i++)
            {
                if (runeGlows[i] == null) continue;
                runeGlows[i].GetPropertyBlock(_mpb); _mpb.SetColor("_EmissionColor", rc); runeGlows[i].SetPropertyBlock(_mpb);
            }
        }
    }

    private void UpdateDarkAura(float k, float dt)
    {
        if (darkAura == null) return;
        float target = k * k;   // ためる間だけ。ドンで k=0 → すぐもどる
        _dark = target > _dark ? target : Mathf.MoveTowards(_dark, target, dt * 4f);
        bool on = _dark > 0.01f;
        if (pillarBarrier != null) { pillarBarrier.GetPropertyBlock(_mpb); _mpb.SetFloat("_Dark", _dark); pillarBarrier.SetPropertyBlock(_mpb); }
        if (darkAura.enabled != on) darkAura.enabled = on;
        if (!on) return;
        darkAura.transform.localScale = Vector3.one * Mathf.Lerp(1.3f, 2.4f, _dark);
        darkAura.GetPropertyBlock(_mpb);
        _mpb.SetFloat("_Alpha", darkAuraMax * _dark);
        darkAura.SetPropertyBlock(_mpb);
    }

    // クリスタル本体のため：かけらが寄る・オーラがふくらむ・光の輪がちぢんで速く回る（ドンでぱっとはじける）
    private void UpdateEnergy(float k, float dt)
    {
        _kSmooth = k > _kSmooth ? k : Mathf.MoveTowards(_kSmooth, k, dt * 3f);   // ドンの後はすっと戻す
        float kk = _kSmooth * _kSmooth;
        float fl = _flash * _flash;
        // 脈：ためるほど速く・強く（ドクン ドクン → ドドドド）
        _pulsePh += dt * Mathf.Lerp(3f, 26f, kk);
        float pulse = 0.5f + 0.5f * Mathf.Sin(_pulsePh);
        if (shards != null && _shard0 != null)
            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i] == null) continue;
                Vector3 b = _shard0[i];
                Vector3 flat = new Vector3(b.x, 0f, b.z);
                float pull = Mathf.Lerp(1f, 0.5f, kk) + 0.9f * fl;   // 寄る → はじける
                shards[i].localPosition = flat * pull + Vector3.up * Mathf.Lerp(b.y, 0f, kk) + Vector3.up * (0.06f * Mathf.Sin(_bob * 2.3f + i * 2.1f));
                shards[i].Rotate(0f, (40f + 600f * kk) * dt, 0f, Space.Self);
            }
        if (auraRenderer != null)
        {
            float a = 0.18f + 0.9f * kk * (0.75f + 0.25f * pulse) + 1.4f * fl;
            bool on = a > 0.01f;
            if (auraRenderer.enabled != on) auraRenderer.enabled = on;
            float sc = Mathf.Lerp(auraIdle, auraCharged, kk) * (1f + 0.06f * pulse * kk) + 2.5f * fl;
            auraRenderer.transform.localScale = Vector3.one * sc;
            auraRenderer.GetPropertyBlock(_mpb);
            Color ac = Color.Lerp(glowColor, Color.white, 0.25f * kk); ac.a = 1f;
            _mpb.SetColor("_Color", ac);
            _mpb.SetFloat("_Alpha", a);
            auraRenderer.SetPropertyBlock(_mpb);
        }
        if (orbitRings != null && _ring0 != null)
        {
            _ringAng += dt * Mathf.Lerp(50f, 760f, kk);
            float r = Mathf.Lerp(ringIdleR, ringChargedR, kk) + 3f * fl;
            float ra = 0.12f + 0.75f * kk + 0.5f * fl * (1f - fl);
            for (int i = 0; i < orbitRings.Length; i++)
            {
                Transform t = orbitRings[i]; if (t == null) continue;
                t.localRotation = _ring0[i] * Quaternion.Euler(0f, (i % 2 == 0 ? 1f : -1.3f) * _ringAng, 0f);
                t.localScale = new Vector3(r, 1f, r);
                if (orbitRingRenderers == null || i >= orbitRingRenderers.Length || orbitRingRenderers[i] == null) continue;
                Renderer rr = orbitRingRenderers[i];
                rr.GetPropertyBlock(_mpb);
                _mpb.SetFloat("_Alpha", ra);
                _mpb.SetVector("_Ball", Vector4.zero);
                rr.SetPropertyBlock(_mpb);
            }
        }
    }

    private float _flowPh, _flickPh;
    private void UpdateFloorZone(float k, float now)
    {
        if (floorZone == null) return;
        float wr = -1f, wf = 0f;
        if (_waveLive)
        {
            wr = (now - _blastAt) * waveSpeed;
            float rEnd = WaveEnd();
            wf = (1f - Mathf.Clamp01((wr - rEnd + 2.5f) / 2.5f)) * Mathf.Clamp01(wr / 0.6f);
        }
        bool on = _active && (k > 0f || _waveLive);
        if (floorZone.enabled != on) floorZone.enabled = on;
        if (!on) { if (safeVeil != null && safeVeil.enabled) safeVeil.enabled = false; return; }
        Vector3 s = _waveLive ? _src : (crystal != null ? crystal.position : transform.position);
        Vector4 bv = Vector4.zero;
        if (shield != null && shield.IsOn()) { Vector3 bp = shield.transform.position; bv = new Vector4(bp.x, bp.y, bp.z, shield.Radius()); }
        // ための輪の流れ・ちらつきの進み（速さはためで変わる。シェーダーで t×速さ にするとガクガクするので、ここで足していく）
        float kk2 = k * k;
        _flowPh = (_flowPh + Mathf.Lerp(0.6f, 2.2f, kk2) * Time.deltaTime) % 1000f;
        _flickPh = (_flickPh + Mathf.Lerp(4f, 22f, kk2) * Time.deltaTime) % 6283.185f;
        floorZone.GetPropertyBlock(_mpb);
        _mpb.SetFloat("_FlowPh", _flowPh);
        _mpb.SetFloat("_FlickPh", _flickPh);
        _mpb.SetFloat("_Charge", k);
        _mpb.SetFloat("_WaveR", wr);
        _mpb.SetFloat("_WaveFade", wf);
        _mpb.SetVector("_Src", new Vector4(s.x, s.y, s.z, 0f));
        _mpb.SetVector("_Ball", bv);
        _mpb.SetFloat("_Pad", nearPad);
        _mpb.SetFloat("_Near", nearPad);
        floorZone.SetPropertyBlock(_mpb);
        if (safeVeil != null)
        {
            bool von = bv.w > 0f;
            if (safeVeil.enabled != von) safeVeil.enabled = von;
            if (von)
            {
                safeVeil.GetPropertyBlock(_mpb);
                _mpb.SetFloat("_Charge", k);
                _mpb.SetFloat("_WaveR", wr);
                _mpb.SetFloat("_WaveFade", wf);
                _mpb.SetVector("_Src", new Vector4(s.x, s.y, s.z, 0f));
                _mpb.SetVector("_Ball", bv);
                _mpb.SetFloat("_Pad", nearPad);
                _mpb.SetFloat("_Near", nearPad);
                Vector3 c = transform.position;
                _mpb.SetVector("_Cen", new Vector4(c.x, c.y, c.z, radius + hitPad));
                Vector2 cd = new Vector2(corridorDir.x, corridorDir.z); float cl = cd.magnitude; if (cl > 1e-4f) cd /= cl;
                _mpb.SetVector("_Corr", new Vector4(cd.x, cd.y, corridorHalfW, cl > 1e-4f ? corridorLen : 0f));
                _mpb.SetVector("_Exit", ExitVec());
                safeVeil.SetPropertyBlock(_mpb);
            }
        }
    }

    private float _skyA;
    private void UpdateSkyDim()
    {
        if (skyDim == null) return;
        // ためるほど暗く、ドンのあとは少しかけて戻す（_kSmooth はドンのあとゆっくり下がる）
        float target = _active ? skyDimMax * _kSmooth * _kSmooth : 0f;
        _skyA = Mathf.MoveTowards(_skyA, target, Time.deltaTime * (target > _skyA ? 2f : 0.6f));
        bool on = _skyA > 0.005f;
        if (skyDim.enabled != on) skyDim.enabled = on;
        if (!on) return;
        skyDim.GetPropertyBlock(_mpb);
        _mpb.SetFloat("_Alpha", _skyA);
        skyDim.SetPropertyBlock(_mpb);
    }

    // 外からクリスタルへちぢむ光の輪（ための予告）
    private void UpdateChargeRing(float k)
    {
        if (chargeRing == null || chargeRenderer == null) return;
        bool on = k > 0f;
        if (chargeRenderer.enabled != on) chargeRenderer.enabled = on;
        if (!on) return;
        float e = 1f - (1f - k) * (1f - k);
        float r = Mathf.Lerp(radius, 0.9f, e);
        chargeRing.localScale = new Vector3(r, 1f, r);
        chargeRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat("_Alpha", Mathf.Clamp01(k * 3f) * 0.8f);
        _mpb.SetVector("_Ball", Vector4.zero);
        chargeRenderer.SetPropertyBlock(_mpb);
    }
}
