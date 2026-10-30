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
    [Tooltip("最後（板から下の外へ落ちる）の1回だけ hopTime の何倍かける。同じ時間だと距離が長くて速く見えた（2026-10-30 てじゃ「最後のバウンドがはやく感じる」）")]
    public float lastHopScale = 2f;
    [Tooltip("最初（[0] から出る）の1回の、高い方の点より上がる高さ。柱のてっぺんの溶岩から飛び出す（2026-10-30 岩柱）")]
    public float firstUp = 1.5f;
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
    [Tooltip("予告線（2026-10-30 てじゃ「軌道を点線で」→「岩より先に点線が伸びながら落ちていく→岩がおいつくと消えてく」）：" +
             "飛び出す warnTime 秒前に柱から点線がのび始め、一定の速さで道すじを先に進む（岩が飛んでいる時間の warnHeadSpeed 倍の時点で下に着く速さ）。岩が通った所から消える。" +
             "（岩の時間に合わせてのばしたら「速度不定」→ 長さで一定に）")]
    public LineRenderer warnLine;
    public float warnTime = 1.2f, warnHeadSpeed = 0.55f;
    public Color warnColor = new Color(1f, 0.5f, 0.15f, 1f);
    public int warnSamples = 14;
    [Tooltip("材質の横の繰り返し（1m あたりの点の数）。点は世界に対して止まって見えるように")]
    public float dotPerM = 2.2f;

    private bool _flying, _hitBall, _hitPlayer;
    private int _hop = -1;
    private float _ballRadius = 0.4f;
    private Vector3[] _path;
    private float[] _cum;
    private Material _lineMat;
    private bool _lineOn;
    private float _headV;

    private void Start()
    {
        if (ball != null)
        {
            SphereCollider sc = ball.GetComponent<SphereCollider>();
            if (sc != null) _ballRadius = sc.radius * ball.transform.lossyScale.x;
        }
        Show(false);
        if (warnLine != null && points != null && points.Length >= 2)
        {
            int hops = points.Length - 1;
            _path = new Vector3[hops * warnSamples + 1];
            for (int k = 0; k < hops; k++)
                for (int j = 0; j < warnSamples; j++) _path[k * warnSamples + j] = At(k, j / (float)warnSamples);
            _path[hops * warnSamples] = points[hops].position;
            _cum = new float[_path.Length];
            for (int i = 1; i < _path.Length; i++) _cum[i] = _cum[i - 1] + (_path[i] - _path[i - 1]).magnitude;
            _lineMat = warnLine.material;
            float flyT = (hops - 1) * hopTime + hopTime * lastHopScale;
            _headV = _cum[_cum.Length - 1] / Mathf.Max(0.1f, warnTime + flyT * warnHeadSpeed);
            warnLine.useWorldSpace = true;
            warnLine.enabled = false;
        }
    }

    private float T(float period)
    {
        int per = Mathf.Max(1, Mathf.RoundToInt(period * 1000f));
        int ms = (Networking.GetServerTimeInMilliseconds() + Mathf.RoundToInt(phase * 1000f)) % per;
        if (ms < 0) ms += per;
        return ms / 1000f;
    }

    // 1回のはね。最初は柱のてっぺんから firstUp 飛び出す
    private Vector3 At(int k, float u)
    {
        Vector3 a = points[k].position, b = points[k + 1].position;
        float top = Mathf.Max(a.y, b.y) + (k == 0 ? firstUp : hopUp);
        Vector3 m = (a + b) * 0.5f; m.y = 2f * top - (a.y + b.y) * 0.5f;
        float v = 1f - u;
        return v * v * a + 2f * v * u * m + u * u * b;
    }

    // 飛び出してからの時間 tau（秒）→ 道すじの点の番号（小数）。最後の1回は長い
    private float PathIndex(float tau, int hops)
    {
        if (tau <= 0f) return 0f;
        float lastT = hopTime * lastHopScale;
        int k; float u;
        if (tau >= (hops - 1) * hopTime) { k = hops - 1; u = Mathf.Clamp01((tau - k * hopTime) / lastT); }
        else { k = Mathf.FloorToInt(tau / hopTime); u = tau / hopTime - k; }
        return (k + u) * warnSamples;
    }

    // 道すじにそった長さ d → 点の番号（小数）
    private float DistIndex(float d)
    {
        int last = _cum.Length - 1;
        if (d <= 0f) return 0f;
        if (d >= _cum[last]) return last;
        int i = 1; while (i < last && _cum[i] < d) i++;
        float seg = _cum[i] - _cum[i - 1];
        return (i - 1) + (seg > 0.0001f ? (d - _cum[i - 1]) / seg : 0f);
    }

    // 予告線：うしろ＝岩の位置（飛ぶ前は柱）、先＝岩より速く進む頭。頭は飛び出す warnTime 秒前に柱を出る
    private void UpdateWarnLine(float tau, int hops)
    {
        if (warnLine == null || _path == null) return;
        int last = _path.Length - 1;
        float tail = Mathf.Min(PathIndex(tau, hops), last);
        float head = tau > -warnTime ? DistIndex((tau + warnTime) * _headV) : 0f;
        bool on = head - tail > 0.05f;
        if (on != _lineOn) { _lineOn = on; warnLine.enabled = on; }
        if (!on) return;
        int i0 = Mathf.FloorToInt(tail), i1 = Mathf.Min(last, Mathf.CeilToInt(head));
        int n = Mathf.Max(2, i1 - i0 + 1);
        warnLine.positionCount = n;
        for (int i = 0; i < n; i++) warnLine.SetPosition(i, _path[Mathf.Min(last, i0 + i)]);
        // 先のはしは頭の位置まで（点と点の間）
        int hb = Mathf.Min(last, Mathf.FloorToInt(head));
        if (hb < last) warnLine.SetPosition(n - 1, Vector3.Lerp(_path[hb], _path[hb + 1], head - hb));
        warnLine.startColor = warnColor; warnLine.endColor = warnColor;
        // 点を世界に対して止めておく（うしろを削った長さぶん横のずれを足す）
        if (_lineMat != null) _lineMat.mainTextureOffset = new Vector2(Mathf.Repeat(_cum[i0] * dotPerM, 1f), 0f);
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
        float lastT = hopTime * lastHopScale;
        float fly = (hops - 1) * hopTime + lastT;
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
        UpdateWarnLine(flying ? t : t - (fly + rest), hops);
        if (!flying) return;
        float u = Mathf.Clamp01((t - k * hopTime) / (k == hops - 1 ? lastT : hopTime));
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
