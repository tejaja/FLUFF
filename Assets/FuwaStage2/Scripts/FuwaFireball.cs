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
    [Tooltip("自分のアバターが eyeBase より背が高いぶん、弧を高くする（その差 × arcPerEye、最大 maxArcExtra m）。背が高いと銃口が高くてふわふわが上を越えられたので（2026-10-30 てじゃ）。低い背では下げない")]
    public float eyeBase = 1.6f, arcPerEye = 1f, maxArcExtra = 1.5f;

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
        float x = Mathf.Clamp((eye - eyeBase) * arcPerEye, 0f, maxArcExtra);
        if (Mathf.Abs(x - _arcExtra) > 0.01f) Debug.Log("[FuwaFireball] eye=" + eye.ToString("F2") + " arcExtra=" + x.ToString("F2") + " " + transform.parent.name);
        return x;
    }

    private void Show(bool on)
    {
        if (visual != null) visual.SetActive(on);
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

        // ふわふわ（当たり判定はトリガーの通知でなく距離で：動く物のトリガーは取りこぼしやすいので）
        if (!_hitBall && ball != null && !ball.IsWaitingOrHidden() && (ball.transform.position - p).magnitude < hitRadius + _ballRadius)
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
