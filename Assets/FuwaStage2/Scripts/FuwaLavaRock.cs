using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 火山から降ってきて、ジグザグの板の上をはねながら下へ落ちていく火の岩（2026-10-30 てじゃ「道に沿って落ちるとこの狭さじゃ回避できないし、
// この方向に３個くらい列ずらしで落とす、ちょうどいい位置でバウンドして落ちてくる感じ」＝坂の下る向きに、板を横切ってはねる）。
// points[0]（空）→ [1]（いちばん上の板）→ … → [n-1]（下の外、ここで消える）の順に、1回 hopTime 秒ではねる。そのあと休んで、また空から。
// 全員同じ時に落ちるようにサーバー時刻で（FuwaFireball と同じ整数ミリ秒）。
// ふわふわに当たる：燃えだす＋転がる向きに押される。人に当たる：ちょっと吹っ飛ぶ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaLavaRock : UdonSharpBehaviour
{
    public FuwaHeat heat;
    public FuwaBall ball;
    [Tooltip("はねる点。[0] は空（降ってくる所）、最後は下の外（消える所）")]
    public Transform[] points;
    public float hopTime = 0.6f, hopUp = 1.2f, rest = 3f, phase = 0f;
    [Tooltip("見た目（飛んでいない時は隠す）")]
    public GameObject visual;
    [Tooltip("尾を引く炎（なくてもOK）")]
    public ParticleSystem trail;
    public float trailRate = 30f;
    [Tooltip("はねた時の火の粉（なくてもOK）")]
    public ParticleSystem bounce;
    public float hitRadius = 0.5f;
    public float ballKnock = 2.5f, ballKnockUp = 1.5f;
    public float playerKnock = 5f, playerKnockUp = 4f;

    private bool _flying, _hitBall, _hitPlayer;
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

    private float T(float period)
    {
        int per = Mathf.Max(1, Mathf.RoundToInt(period * 1000f));
        int ms = (Networking.GetServerTimeInMilliseconds() + Mathf.RoundToInt(phase * 1000f)) % per;
        if (ms < 0) ms += per;
        return ms / 1000f;
    }

    // 1回のはね。最初（空から）は上がらずにそのまま落ちる
    private Vector3 At(int k, float u)
    {
        Vector3 a = points[k].position, b = points[k + 1].position;
        float top = Mathf.Max(a.y, b.y) + (k == 0 ? 0f : hopUp);
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
        if (points == null || points.Length < 2) return;
        int hops = points.Length - 1;
        float fly = hops * hopTime;
        float t = T(fly + rest);
        bool flying = t < fly;
        int k = flying ? Mathf.Min(hops - 1, Mathf.FloorToInt(t / hopTime)) : -1;
        if (flying != _flying)
        {
            if (flying) { _hitBall = false; _hitPlayer = false; }
            _flying = flying; Show(flying);
        }
        if (k != _hop)
        {
            // 板に着いた所で火の粉（最初の空と最後の外は除く）
            if (k > 0 && k < hops && bounce != null) { bounce.transform.position = points[k].position; bounce.Play(); }
            _hop = k;
        }
        if (!flying) return;
        float u = (t - k * hopTime) / hopTime;
        Vector3 p = At(k, u);
        Vector3 vel = At(k, Mathf.Min(1f, u + 0.02f)) - At(k, Mathf.Max(0f, u - 0.02f));
        transform.position = p;
        if (trail != null) trail.transform.position = p;
        Vector3 hdir = new Vector3(vel.x, 0f, vel.z);
        hdir = hdir.sqrMagnitude > 0.0001f ? hdir.normalized : Vector3.back;
        if (visual != null) visual.transform.Rotate(Vector3.Cross(Vector3.up, hdir), 540f * Time.deltaTime, Space.World);

        if (!_hitBall && ball != null && !ball.IsWaitingOrHidden() && (ball.transform.position - p).magnitude < hitRadius + _ballRadius)
        {
            _hitBall = true;
            if (heat != null) heat.Ignite();
            ball.Launch(hdir * ballKnock + Vector3.up * ballKnockUp);
        }
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!_hitPlayer && lp != null)
        {
            Vector3 f = lp.GetPosition();
            Vector3 head = lp.GetBonePosition(HumanBodyBones.Head);
            float h = head == Vector3.zero ? 1.6f : Mathf.Clamp(head.y - f.y + 0.2f, 1f, 2.2f);
            float y = Mathf.Clamp(p.y, f.y, f.y + h);
            if ((new Vector3(f.x, y, f.z) - p).magnitude < hitRadius + 0.35f)
            {
                _hitPlayer = true;
                Vector3 v = lp.GetVelocity(); v.y = Mathf.Max(v.y, 0f);
                lp.SetVelocity(v * 0.3f + hdir * playerKnock + Vector3.up * playerKnockUp);
            }
        }
    }
}
