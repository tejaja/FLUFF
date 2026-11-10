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
    [Tooltip("最初（[0] から出る）の1回の、高い方の点より上がる高さ。火山の火口から飛び出す（2026-10-30 岩柱→コースの外の浮いてる火山）")]
    public float firstUp = 1.5f;
    [Tooltip("最初（火口から飛んでくる）の1回だけ hopTime の何倍かける（遠いので）")]
    public float firstHopScale = 1f;
    [Tooltip("火口から飛び出す時の火の粉（なくてもOK）")]
    public ParticleSystem launchFx;
    public AudioSource launchSound;
    public AudioSource bounceSound;
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
    [Tooltip("次に着く所の予告の丸。[k-1] が points[k] に着く時（なくてもOK。2026-11-10 てじゃ「予告をもっとわかりやすく」）")]
    public Renderer[] landWarns;
    [Tooltip("着く何秒前から予告の丸を出すか")]
    public float landWarnLead = 0.9f;
    [Tooltip("当たったら飛ぶまもの（なくてもOK。2026-10-30 てじゃ「まものも当たったら一応飛ぶ」）")]
    public FuwaEnemy[] enemies;
    public float enemyRadius = 0.7f, enemyKnock = 3.4f;
    [Header("通り道の点線（2026-11-04 てじゃ「岩が出たら奥から手前まで順番に点灯するのを繰り返す」）")]
    [Tooltip("点線のメッシュ（FuwaCourse/RockPathDots）。なくてもOK")]
    public Renderer pathDots;
    [Tooltip("点線の長さ(m)・光が流れる速さ(m/s)・光の長さ(m)")]
    public float pathLength = 30f, dotSpeed = 10f, dotTail = 4f;
    [Tooltip("光と光の間隔(m)。0 で 1 つだけ流す")]
    public float dotSpacing = 0f;
    [Tooltip("ON なら光を岩の少し前（dotLead m 先まで）にくっつけて、岩といっしょに動かす（2026-11-04 てじゃ「岩のちょっと前を岩と同じくらいの速度でひからせよう」）")]
    public bool dotFollow = false;
    public float dotLead = 6f;
    [Tooltip("はね k の中の、u = 0, 1/8, …, 1 の時の点線ぞいの距離(m)を 9 個ずつ（弧の長さは u に比例しないので表で）。[0]（火口から）は点線の手前なのでマイナス")]
    public float[] dotHopTable;
    private Material _dotMat;

    private bool _flying, _hitBall, _hitPlayer;
    private Material[] _warnMats; private bool[] _warnOn;
    private bool[] _hitEnemy;
    private int _hop = -1;
    private float _ballRadius = 0.4f;

    private void Start()
    {
        if (ball != null)
        {
            SphereCollider sc = ball.GetComponent<SphereCollider>();
            if (sc != null) _ballRadius = sc.radius * ball.transform.lossyScale.x;
        }
        _hitEnemy = new bool[enemies == null ? 0 : enemies.Length];
        if (pathDots != null) { _dotMat = pathDots.material; _dotMat.SetFloat("_Tail", dotTail); _dotMat.SetFloat("_Spacing", dotSpacing); _dotMat.SetFloat("_Head", -100f); }
        Show(false);
        int nw = landWarns == null ? 0 : landWarns.Length;
        _warnMats = new Material[nw]; _warnOn = new bool[nw];
        for (int i = 0; i < nw; i++) if (landWarns[i] != null) { _warnMats[i] = landWarns[i].material; landWarns[i].enabled = false; }
        // はねる音は岩から外して、着いた点に置いて鳴らす（2026-11-10 てじゃ「下の段から聞くと中段の2回が聞こえない、横からだと聞こえる」）。
        //  岩についたままだと、鳴っている間に音の出る所が岩といっしょに上へ飛んでいく
        if (bounceSound != null) bounceSound.transform.SetParent(transform.parent, true);
    }

    // k 回目のはねの長さ（最初・最後だけ長い。1回だけなら両方）
    private float Dur(int k, int hops)
    {
        float d = hopTime;
        if (k == 0) d *= firstHopScale;
        if (k == hops - 1) d *= lastHopScale;
        return d;
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

    // 予告の丸：points[k] に着く landWarnLead 秒前から出して、まん中から埋まっていき、ふちまで埋まった時に着く。着いたら消す
    private void LandWarns(bool flying, float t, int hops)
    {
        if (_warnMats == null) return;
        float land = 0f;
        for (int k = 1; k < hops; k++)
        {
            land += Dur(k - 1, hops);
            int i = k - 1; if (i >= _warnMats.Length) break;
            Material m = _warnMats[i]; if (m == null) continue;
            float dt = land - t;
            bool on = flying && dt > 0f && dt < landWarnLead;
            if (on != _warnOn[i]) { _warnOn[i] = on; landWarns[i].enabled = on; }
            if (!on) continue;
            float f = 1f - dt / landWarnLead;
            m.SetFloat("_Fill", f);
            m.SetFloat("_Alpha", Mathf.Clamp01(f * 4f));
        }
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
        float fly = 0f;
        for (int i = 0; i < hops; i++) fly += Dur(i, hops);
        float t = T(fly + rest);
        bool flying = t < fly;
        int k = -1; float t0 = 0f;
        if (flying)
        {
            k = 0;
            while (k < hops - 1 && t >= t0 + Dur(k, hops)) { t0 += Dur(k, hops); k++; }
        }
        if (flying != _flying)
        {
            if (flying) { _hitBall = false; _hitPlayer = false; for (int i = 0; i < _hitEnemy.Length; i++) _hitEnemy[i] = false; }
            _flying = flying; Show(flying);
        }
        if (k != _hop)
        {
            // 板に着いた所で火の粉（最初の空と最後の外は除く）
            if (k > 0 && k < hops && bounce != null) { bounce.transform.position = points[k].position; bounce.Play(); }
            if (k > 0 && k < hops && bounceSound != null) { bounceSound.transform.position = points[k].position; bounceSound.Play(); }
            if (k == 0 && launchFx != null) launchFx.Play();
            if (k == 0 && launchSound != null) launchSound.Play();
            _hop = k;
        }
        // 点線：飛んでいる間、火口を出た時から奥→手前へ光を流し続ける（1 周は点線の長さ＋光の長さ）
        //  間隔ありの時は先頭を止めずに進めて、うしろのくり返しで流れ続ける（間隔の倍数で巻き戻すので値は大きくならない）
        if (_dotMat != null && dotFollow && dotHopTable != null && flying && k * 9 + 8 < dotHopTable.Length)
        {
            float uu = Mathf.Clamp01((t - t0) / Dur(k, hops)) * 8f; int ui = Mathf.Min(7, Mathf.FloorToInt(uu));
            _dotMat.SetFloat("_Head", Mathf.Lerp(dotHopTable[k * 9 + ui], dotHopTable[k * 9 + ui + 1], uu - ui) + dotLead);
        }
        else if (_dotMat != null) _dotMat.SetFloat("_Head", !flying ? -100f : dotSpacing > 0.01f ? (t * dotSpeed > pathLength + dotTail ? pathLength + dotTail + Mathf.Repeat(t * dotSpeed - pathLength - dotTail, dotSpacing) : t * dotSpeed) : (t * dotSpeed) % (pathLength + dotTail));
        if (pathDots != null && pathDots.enabled != flying) pathDots.enabled = flying;   // 休みの間は点線ごと消す
        LandWarns(flying, t, hops);
        if (!flying) return;
        float u = Mathf.Clamp01((t - t0) / Dur(k, hops));
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
        for (int i = 0; i < _hitEnemy.Length; i++)
        {
            FuwaEnemy e = enemies[i];
            if (_hitEnemy[i] || e == null || !e.IsActive()) continue;
            if ((e.transform.position - p).magnitude < hitRadius + enemyRadius) { _hitEnemy[i] = true; e.Blown(hdir * enemyKnock, true); }
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
