using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 溶岩溜まりから道を飛び越えて飛ぶ火の玉（2026-10-29 てじゃの 2–2「両脇に溶岩溜まり、火の玉が道を飛び越えて飛んでいる」）。
// from → to へ決まった弧（てっぺんは from/to の高い方より arcHeight 上）を flightTime 秒で飛ぶ。period 秒ごと、phase でずらす。
// 全員同じ時に飛ぶようにサーバー時刻で（タイミングは決まっていて覚えられる。てじゃ「タイミングは決まっててほしい」）。
// ふわふわに当たる：燃えだす（FuwaHeat.Ignite、撃てば消える）＋飛ぶ向きに吹っ飛ぶ。人に当たる：ちょっと吹っ飛ぶ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaFireball : UdonSharpBehaviour
{
    public FuwaHeat heat;
    public FuwaBall ball;
    public Transform from, to;
    public float arcHeight = 2.5f, flightTime = 1.6f, period = 4f, phase = 0f;
    [Tooltip("見た目（飛んでいない時は隠す）")]
    public GameObject visual;
    [Tooltip("尾を引く炎（なくてもOK）")]
    public ParticleSystem trail;
    public float trailRate = 40f;
    [Tooltip("当たり（球）の半径")]
    public float hitRadius = 0.45f;
    [Tooltip("ふわふわを吹っ飛ばす：飛ぶ向き（水平）× ballKnock ＋ 上 × ballKnockUp")]
    public float ballKnock = 4f, ballKnockUp = 1.5f;
    [Tooltip("人を吹っ飛ばす")]
    public float playerKnock = 3.5f, playerKnockUp = 2f;
    [Tooltip("溶岩から出る時・入る時のしぶき（なくてもOK）")]
    public ParticleSystem splash;
    public AudioSource whoosh;

    private bool _flying, _hitBall, _hitPlayer;
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

    private float T()
    {
        double t = Networking.GetServerTimeInSeconds() + phase;
        return (float)(t % period);
    }

    private Vector3 At(float u)
    {
        Vector3 a = from.position, b = to.position;
        float top = Mathf.Max(a.y, b.y) + arcHeight;
        // 2次のベジェ（てっぺんが top になるように真ん中の点を 2*top - (a+b)/2）
        Vector3 m = (a + b) * 0.5f; m.y = 2f * top - (a.y + b.y) * 0.5f;
        float v = 1f - u;
        return v * v * a + 2f * v * u * m + u * u * b;
    }

    private void Show(bool on)
    {
        if (visual != null) visual.SetActive(on);
        if (trail != null) { var e = trail.emission; e.rateOverTime = on ? trailRate : 0f; }
    }

    private void Update()
    {
        if (from == null || to == null) return;
        float t = T();
        bool fly = t < flightTime;
        if (fly != _flying)
        {
            _flying = fly; Show(fly);
            if (splash != null) { splash.transform.position = fly ? from.position : to.position; splash.Play(); }
            if (fly) { _hitBall = false; _hitPlayer = false; if (whoosh != null && whoosh.clip != null) whoosh.PlayOneShot(whoosh.clip); }
        }
        if (!fly) return;
        float u = t / flightTime;
        Vector3 p = At(u);
        Vector3 vel = (At(Mathf.Min(1f, u + 0.02f)) - At(Mathf.Max(0f, u - 0.02f)));
        transform.position = p;
        if (trail != null) trail.transform.position = p;
        Vector3 hdir = new Vector3(vel.x, 0f, vel.z); hdir = hdir.sqrMagnitude > 0.0001f ? hdir.normalized : transform.forward;

        // ふわふわ（当たり判定はトリガーの通知でなく距離で：動く物のトリガーは取りこぼしやすいので）
        if (!_hitBall && ball != null && !ball.IsWaitingOrHidden() && (ball.transform.position - p).magnitude < hitRadius + _ballRadius)
        {
            _hitBall = true;
            if (heat != null) heat.Ignite();
            ball.Launch(hdir * ballKnock + Vector3.up * ballKnockUp);
        }
        // 人（自分だけ）
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!_hitPlayer && lp != null)
        {
            Vector3 f = lp.GetPosition();
            // 体を縦の線（足〜頭）として、線との距離
            float y = Mathf.Clamp(p.y, f.y, f.y + 1.6f);
            Vector3 q = new Vector3(f.x, y, f.z);
            if ((q - p).magnitude < hitRadius + 0.3f)
            {
                _hitPlayer = true;
                lp.SetVelocity(hdir * playerKnock + Vector3.up * playerKnockUp);
            }
        }
    }
}
