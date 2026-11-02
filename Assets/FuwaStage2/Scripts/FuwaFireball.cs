using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 道にあいた溶岩の穴から穴へ、行ったり来たりする火の玉（2026-10-29 てじゃの 2–2 の案：「青◯を溶岩溜まりの穴、紫ラインを火球が往復、最後は3つの穴を行き来する」）。
// holes[0] → [1] → … → [n-1] → … → [0] と往復。1回の飛び（flightTime 秒、てっぺんは高い方の穴より arcHeight 上）のあと、着いた穴の中で rest 秒休む。
// 全員同じ時に飛ぶようにサーバー時刻で（タイミングは決まっていて覚えられる。てじゃ「タイミングは決まっててほしい」）。
// ふわふわに当たる：燃えだす（FuwaHeat.Ignite、撃てば消える）＋飛ぶ向きにちょっと押される。人に当たる：ちょっと吹っ飛ぶ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaFireball : UdonSharpBehaviour
{
    public FuwaHeat heat;
    public FuwaBall ball;
    [Tooltip("溶岩の穴（の溶岩の面）。この順に往復する")]
    public Transform[] holes;
    public float arcHeight = 2.5f, flightTime = 1.2f, rest = 1.2f, phase = 0f;
    [Tooltip("見た目（飛んでいない時は隠す）")]
    public GameObject visual;
    [Tooltip("尾を引く炎（なくてもOK）")]
    public ParticleSystem trail;
    public float trailRate = 40f;
    [Tooltip("当たり（球）の半径")]
    public float hitRadius = 0.7f;
    [Tooltip("ふわふわを押す：飛ぶ向き（水平）× ballKnock ＋ 上 × ballKnockUp")]
    public float ballKnock = 3.2f, ballKnockUp = 1.5f;
    [Tooltip("人を吹っ飛ばす")]
    public float playerKnock = 6f, playerKnockUp = 4.5f;
    [Tooltip("溶岩から出る時・入る時のしぶき（なくてもOK）")]
    public ParticleSystem splash;
    public AudioSource whoosh;
    [Tooltip("自分のアバターの目の高さと eyeBase の差 × arcPerEye だけ弧を上げ下げする（-maxArcLower 〜 +maxArcExtra m）。背が高いとふわふわが上を越え、低いと下をくぐれたので（2026-10-30 てじゃ）")]
    public float eyeBase = 1.6f, arcPerEye = 1f, maxArcExtra = 1.5f, maxArcLower = 1.3f;

    [Header("火のオーラ（2026-11-02 てじゃ「ふわふわを上に無理やり通すのも妨害したい」「火のオーラでも足すか」）")]
    [Tooltip("火球の上に立つ炎。飛びはじめと終わりは低く、最高点で auraHeight まで伸びる")]
    public GameObject aura;
    [Tooltip("オーラのてっぺんまでの高さ（火球の中心から）。当たりも同じ高さまで")]
    public float auraHeight = 0f;
    [Tooltip("オーラのてっぺんの当たりの半径。根元は hitRadius")]
    public float auraTopRadius = 0.25f;
    [Tooltip("オーラを進む向きと逆へ倒す強さ（0 で真上。2026-11-02 てじゃ「垂直に燃えて飛んでくの違和感」）。当たりも同じ向き")]
    public float auraLean = 0.75f;
    [Tooltip("空中に置いていかれて立ちのぼる炎のかたまり（ワールド座標の粒。2026-11-02 てじゃ「粒子みたいなのが立ち上ってるはずなのに全部くっついてる」）")]
    public ParticleSystem auraFx;
    public float auraFxRate = 28f;
    private float _auraNow;

    [Header("地面の予兆（2026-11-03 てじゃ「火球の下にも、飛んでる時だけ火炎放射みたいなグラデーション」）")]
    [Tooltip("holes[i] と holes[i+1] の間の地面のグラデ。真上に飛ぶ（穴が 1 つの）火球にはなし")]
    public Renderer[] warnAreas;
    [Tooltip("warnAreas それぞれの長さ(m)。帯の間隔をそろえる")]
    public float[] warnLengths;
    [Tooltip("飛びはじめと終わりのフェードの秒数")]
    public float warnFade = 0.12f;
    private Material[] _warnMats;
    private int _warnSeg = -1;

    private bool _flying, _hitBall, _hitPlayer;
    private float _arcExtra;
    private int _hop = -1;
    private float _ballRadius = 0.4f;

    private void Start()
    {
        if (ball != null)
        {
            SphereCollider sc = ball.GetComponent<SphereCollider>();
            if (sc != null) _ballRadius = sc.radius * ball.transform.lossyScale.x;
        }
        Show(false);
        if (warnAreas != null)
        {
            _warnMats = new Material[warnAreas.Length];
            for (int i = 0; i < warnAreas.Length; i++)
            {
                if (warnAreas[i] == null) continue;
                _warnMats[i] = warnAreas[i].material;
                if (warnLengths != null && i < warnLengths.Length) _warnMats[i].SetFloat("_Length", warnLengths[i]);
                warnAreas[i].enabled = false;
            }
        }
    }

    // 今の飛びの地面のグラデだけ出す。穴の番号が増える向きの飛びなら +、戻る飛びなら - に流す
    private void UpdateWarn(bool fly, int k, float tau)
    {
        if (_warnMats == null || _warnMats.Length == 0) return;
        int seg = -1; float flow = 1f;
        if (fly)
        {
            int ia = HoleAt(k), ib = HoleAt(k + 1); seg = Mathf.Min(ia, ib); flow = ia < ib ? 1f : -1f;
            if (seg >= _warnMats.Length) seg = -1;
        }
        if (seg != _warnSeg)
        {
            if (_warnSeg >= 0 && warnAreas[_warnSeg] != null) warnAreas[_warnSeg].enabled = false;
            _warnSeg = seg;
            if (seg >= 0 && warnAreas[seg] != null) { warnAreas[seg].enabled = true; _warnMats[seg].SetFloat("_Flow", flow); }
        }
        else if (seg >= 0 && _warnMats[seg] != null && k != _hop) _warnMats[seg].SetFloat("_Flow", flow);
        if (seg >= 0 && _warnMats[seg] != null) _warnMats[seg].SetFloat("_Alpha", Mathf.Clamp01(Mathf.Min(tau, flightTime - tau) / Mathf.Max(0.01f, warnFade)));
    }

    // 穴が1つなら真上に飛び上がって同じ穴に戻る（2026-10-29 てじゃ「垂直に飛び上がる穴を追加」）
    private int Hops() { return holes == null || holes.Length == 0 ? 0 : holes.Length == 1 ? 1 : 2 * (holes.Length - 1); }
    // 往復の k 番目の飛びの出発・到着の穴
    private int HoleAt(int k)
    {
        int n = holes.Length;
        if (n == 1) return 0;
        return k < n - 1 ? k : 2 * (n - 1) - k;
    }

    // 周期の中の今の時刻（秒）。サーバー時刻の整数ミリ秒で割り算する（2026-10-29 秒の double で割る方式は VRChat でだけ狂った。FuwaWhirlwind と同じ）
    private float T(float period)
    {
        int per = Mathf.Max(1, Mathf.RoundToInt(period * 1000f));
        int ms = (Networking.GetServerTimeInMilliseconds() + Mathf.RoundToInt(phase * 1000f)) % per;
        if (ms < 0) ms += per;
        return ms / 1000f;
    }

    private Vector3 At(Vector3 a, Vector3 b, float u)
    {
        float top = Mathf.Max(a.y, b.y) + arcHeight + _arcExtra;
        // 2次のベジェ（てっぺんが top になるように真ん中の点を 2*top - (a+b)/2）
        Vector3 m = (a + b) * 0.5f; m.y = 2f * top - (a.y + b.y) * 0.5f;
        float v = 1f - u;
        return v * v * a + 2f * v * u * m + u * u * b;
    }

    private Vector3 Away(Vector3 hdir, bool vert, Vector3 target, Vector3 p)
    {
        if (!vert) return hdir;
        Vector3 d = target - p; d.y = 0f;
        return d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward;
    }

    // 各自のローカルで自分の背に合わせる（人によって見える弧が違う。てじゃ「ローカルで各自でいい」）。飛んでいる途中で弧が変わらないよう、飛び出す時に決める
    private float ArcExtra()
    {
        VRCPlayerApi o = Networking.LocalPlayer;
        if (o == null || !o.IsValid()) return 0f;
        float eye = o.GetAvatarEyeHeightAsMeters();
        float x = Mathf.Clamp((eye - eyeBase) * arcPerEye, -maxArcLower, maxArcExtra);
        if (Mathf.Abs(x - _arcExtra) > 0.01f) Debug.Log("[FuwaFireball] eye=" + eye.ToString("F2") + " arcExtra=" + x.ToString("F2") + " " + transform.parent.name);
        return x;
    }

    private void Show(bool on)
    {
        if (visual != null) visual.SetActive(on);
        if (aura != null) aura.SetActive(on);
        if (auraFx != null) { var fx = auraFx.emission; fx.rateOverTime = on ? auraFxRate : 0f; }
        if (trail != null) { var e = trail.emission; e.rateOverTime = on ? trailRate : 0f; }
    }

    private void Update()
    {
        int hops = Hops(); if (hops == 0) return;
        float step = flightTime + rest;
        float t = T(step * hops);
        int k = Mathf.Min(hops - 1, Mathf.FloorToInt(t / step));
        float tau = t - k * step;
        bool fly = tau < flightTime;
        Vector3 a = holes[HoleAt(k)].position, b = holes[HoleAt(k + 1)].position;
        UpdateWarn(fly, k, tau);
        if (fly != _flying || (fly && k != _hop))
        {
            if (splash != null) { splash.transform.position = fly ? a : b; splash.Play(); }
            if (fly) { _arcExtra = ArcExtra(); _hitBall = false; _hitPlayer = false; if (whoosh != null && whoosh.clip != null) whoosh.PlayOneShot(whoosh.clip); }
            _flying = fly; _hop = k; Show(fly);
        }
        if (!fly) return;
        float u = tau / flightTime;
        Vector3 p = At(a, b, u);
        Vector3 vel = At(a, b, Mathf.Min(1f, u + 0.02f)) - At(a, b, Mathf.Max(0f, u - 0.02f));
        transform.position = p;
        if (trail != null) trail.transform.position = p;
        Vector3 hdir = new Vector3(vel.x, 0f, vel.z); bool vert = hdir.sqrMagnitude <= 0.0001f; if (!vert) hdir = hdir.normalized;   // 真上に飛ぶ時は、当たった物を火の玉から離れる向きに

        // オーラの高さ：穴の近くは低く、最高点でいちばん高い
        _auraNow = auraHeight * Mathf.Lerp(0.4f, 1f, Mathf.Sin(u * Mathf.PI));
        // 進む向きと逆へ倒す（真上に飛ぶ時はまっすぐ）
        Vector3 auraDir = vert ? Vector3.up : (Vector3.up - hdir * auraLean).normalized;
        // 見た目は当たりと別：粒はワールド座標に置いていかれるので、飛んだ後ろに自然に残る（2026-11-02 てじゃ「板が角度を変えてついてきてる」「粒が出る形のほうがきれい」でメッシュの炎はやめた）

        // ふわふわ（当たり判定はトリガーの通知でなく距離で：動く物のトリガーは取りこぼしやすいので）
        // 火球の中心からオーラの向きへ _auraNow の線との距離で見る。先へ行くほど細い
        bool ballHit = false;
        if (!_hitBall && ball != null && !ball.IsWaitingOrHidden())
        {
            Vector3 bp = ball.transform.position;
            float upY = _auraNow > 0.01f ? Mathf.Clamp(Vector3.Dot(bp - p, auraDir), 0f, _auraNow) : 0f;
            float r = _auraNow > 0.01f ? Mathf.Lerp(hitRadius, auraTopRadius, upY / _auraNow) : hitRadius;
            ballHit = (bp - (p + auraDir * upY)).magnitude < r + _ballRadius;
        }
        if (ballHit)
        {
            _hitBall = true;
            if (heat != null) heat.Ignite();
            ball.Launch(Away(hdir, vert, ball.transform.position, p) * ballKnock + Vector3.up * ballKnockUp);
        }
        // 人（自分だけ）
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!_hitPlayer && lp != null)
        {
            Vector3 f = lp.GetPosition();
            // 体を縦の線（足〜頭）として、線との距離
            Vector3 head = lp.GetBonePosition(HumanBodyBones.Head);
            float h = head == Vector3.zero ? 1.6f : Mathf.Clamp(head.y - f.y + 0.2f, 1f, 2.2f);
            float y = Mathf.Clamp(p.y, f.y, f.y + h);
            Vector3 q = new Vector3(f.x, y, f.z);
            if ((q - p).magnitude < hitRadius + 0.35f)
            {
                _hitPlayer = true;
                Vector3 v = lp.GetVelocity(); v.y = Mathf.Max(v.y, 0f);
                lp.SetVelocity(v * 0.3f + Away(hdir, vert, f, p) * playerKnock + Vector3.up * playerKnockUp);
                Debug.Log("[FuwaFireball] hit player " + gameObject.transform.parent.name);
            }
        }
    }
}
