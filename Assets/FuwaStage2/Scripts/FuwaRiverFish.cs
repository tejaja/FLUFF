using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 川の2回目の渡り場の魚（コース2–1「みずのこみち」、2026-10-25）。サケの川のぼりのように、いつも川上へ向かって水面から跳ねる。
// 空中で運んでいるふわふわに当たると、川上へ少し上向きにポーンと飛ばされて川に落ち、また流れて渡り場へ戻ってくる（すくい直せばいい）。
// てじゃ「川下にふっとばすと理不尽感あるから、川上にジャンプしてる感じで当たると川上にふっとばされるのがいい」。
// プレイヤーに当たっても川上へ吹き飛ばす（2026-10-25 てじゃ「プレイヤーもふきとばそう」）。
// 跳ぶ前に水面にぶくぶく＋波紋（貝がパカッと開くのと同じ「来るよ」の合図）。
// 跳ぶタイミングはサーバーの時刻でそろえる（みんな同じリズムに見える）。当たるのは自分のふわふわだけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaRiverFish : UdonSharpBehaviour
{
    public FuwaBall ball;
    public FuwaWaterFx fx;

    [Header("魚（fish[i] が跳ぶ。jumpFrom[i] → jumpTo[i]、どちらも水面の少し下）")]
    public Transform[] fish;
    public Transform[] jumpFrom;
    public Transform[] jumpTo;
    [Tooltip("魚ごとのずらし（秒）")]
    public float[] phases;
    [Tooltip("1周の長さ（秒）。この中で1回ずつ跳ぶ。periods に魚ごとの値があればそっちを使う")]
    public float period = 3.2f;
    [Tooltip("魚ごとの1周の長さ（2026-10-25 てじゃ「魚が出るタイミングズラそう、高いほど周期を長く」）")]
    public float[] periods;
    [Tooltip("跳んでいる時間（秒）")]
    public float jumpTime = 0.9f;
    [Tooltip("いちばん高い所（水面から、m）。jumpHeights に魚ごとの値があればそっちを使う")]
    public float jumpHeight = 1.6f;
    [Tooltip("魚ごとのいちばん高い所（2026-10-25 てじゃ「高さも３匹バラバラにしよ」）")]
    public float[] jumpHeights;
    [Tooltip("跳ぶ前の合図（ぶくぶく・波紋）の長さ（秒）")]
    public float warnLead = 0.7f;
    public float warnInterval = 0.18f;

    [Header("当たった時")]
    [Tooltip("当たる距離（魚の中心とふわふわの中心）")]
    public float hitRadius = 0.55f;
    [Tooltip("渡り場（+Z が川上、+X が川の右岸の向き、原点が川のまん中）。ふわふわを飛ばす向きの基準")]
    public Transform crossing;
    [Tooltip("川上へ飛ばす速さ(m/s)・上へ(m/s)・川のまん中へ寄せる強さ（まん中からのずれ1mあたり m/s）")]
    public float knockBack = 3.2f;
    public float knockUp = 3.4f;
    public float knockCenter = 0.8f;
    public Color knockGlow = new Color(1f, 0.55f, 0.35f, 1f);
    [Tooltip("当たったあと、また当たるまで（秒）")]
    public float hitCooldown = 0.6f;

    [Header("プレイヤーに当たった時（2026-10-25 てじゃ「プレイヤーもふきとばそう」）")]
    [Tooltip("体（足から playerTop m までの縦の線）とこの距離まで近づいたら当たり")]
    public float playerHitRadius = 0.75f;
    public float playerTop = 1.5f;
    [Tooltip("川上へ飛ばす速さ(m/s)・上へ(m/s)")]
    public float playerKnockBack = 4.5f;
    public float playerKnockUp = 4f;
    [Tooltip("ここに立っている間は当たらない（飛び石の上の面の中心。2026-10-25 てじゃ「飛び石の上は安全な感じにしたい」）")]
    public Transform[] safeSpots;
    public float safeRadius = 0.55f;

    [Header("音")]
    public AudioSource jumpSound;
    public AudioSource hitSound;

    private int _n;
    private int[] _lastWarn;
    private bool[] _up;
    private float _noHitUntil, _noPlayerHitUntil;

    private void Start()
    {
        _n = fish != null ? fish.Length : 0;
        _lastWarn = new int[_n];
        _up = new bool[_n];
        for (int i = 0; i < _n; i++) { _lastWarn[i] = -1; if (fish[i] != null) fish[i].gameObject.SetActive(false); }
    }

    private void OnDisable()
    {
        if (fish == null) return;
        for (int i = 0; i < fish.Length; i++) if (fish[i] != null) fish[i].gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_n == 0) return;
        double server = Networking.GetServerTimeInSeconds();
        bool ballOk = ball != null && !ball.IsWaitingOrHidden() && ball.gameObject.activeInHierarchy;
        Vector3 bp = ballOk ? ball.transform.position : Vector3.zero;
        VRCPlayerApi lp = Networking.LocalPlayer;
        bool playerOk = Utilities.IsValid(lp);
        Vector3 pp = playerOk ? lp.GetPosition() : Vector3.zero;
        if (playerOk && OnSafeSpot(pp)) playerOk = false;
        for (int i = 0; i < _n; i++)
        {
            if (fish[i] == null || jumpFrom[i] == null || jumpTo[i] == null) continue;
            float ph = phases != null && i < phases.Length ? phases[i] : 0f;
            double tt = server + ph;
            float per = periods != null && i < periods.Length && periods[i] > warnLead + jumpTime ? periods[i] : period;
            int cyc = (int)System.Math.Floor(tt / per);
            float inCyc = (float)(tt - (double)cyc * per);   // 0〜warnLead：合図、warnLead〜+jumpTime：跳ぶ
            Vector3 a = jumpFrom[i].position, b = jumpTo[i].position;
            if (inCyc < warnLead)
            {
                if (_up[i]) Hide(i, b);
                int k = (int)(inCyc / warnInterval) + cyc * 64;
                if (k != _lastWarn[i]) { _lastWarn[i] = k; if (fx != null) fx.Visual(a + Vector3.up * 0.05f, 0.12f + 0.25f * (inCyc / warnLead)); }
                continue;
            }
            float t = (inCyc - warnLead) / jumpTime;
            if (t >= 1f) { if (_up[i]) Hide(i, b); continue; }
            if (!_up[i])
            {
                _up[i] = true;
                fish[i].gameObject.SetActive(true);
                if (fx != null) fx.Visual(a + Vector3.up * 0.05f, 0.6f);
                if (jumpSound != null) { jumpSound.transform.position = a; jumpSound.pitch = Random.Range(0.9f, 1.1f); jumpSound.Play(); }
            }
            // 放物線：a → b、まん中で jumpHeight（魚ごと）
            float jh = jumpHeights != null && i < jumpHeights.Length ? jumpHeights[i] : jumpHeight;
            Vector3 p = Vector3.Lerp(a, b, t) + Vector3.up * (4f * jh * t * (1f - t));
            Vector3 tan = (b - a) + Vector3.up * (4f * jh * (1f - 2f * t));
            fish[i].position = p;
            if (tan.sqrMagnitude > 1e-4f) fish[i].rotation = Quaternion.LookRotation(tan.normalized, Vector3.up);
            // 水の上に出ている間だけ当たる
            if (t <= 0.06f || t >= 0.94f) continue;
            if (ballOk && Time.time >= _noHitUntil && (p - bp).magnitude < hitRadius) Knock(p);
            if (playerOk && Time.time >= _noPlayerHitUntil)
            {
                // 体の縦の線（足〜playerTop）でいちばん近い点
                float y = Mathf.Clamp(p.y, pp.y, pp.y + playerTop);
                if ((p - new Vector3(pp.x, y, pp.z)).magnitude < playerHitRadius) KnockPlayer(lp, p);
            }
        }
    }

    private void Hide(int i, Vector3 at)
    {
        _up[i] = false;
        fish[i].gameObject.SetActive(false);
        if (fx != null) fx.Visual(at + Vector3.up * 0.05f, 0.45f);
    }

    private void Knock(Vector3 at)
    {
        _noHitUntil = Time.time + hitCooldown;
        Vector3 v = Vector3.up * knockUp;
        if (crossing != null)
        {
            Vector3 f = crossing.forward; f.y = 0f; f.Normalize();
            Vector3 r = crossing.right; r.y = 0f; r.Normalize();
            float lat = Vector3.Dot(ball.transform.position - crossing.position, r);
            v += f * knockBack - r * Mathf.Clamp(lat * knockCenter, -1.5f, 1.5f);
        }
        ball.LaunchTinted(v, knockGlow);
        if (hitSound != null) { hitSound.transform.position = at; hitSound.pitch = Random.Range(0.95f, 1.1f); hitSound.Play(); }
    }

    // 飛び石の上に立っている（足が石の上の面より上で、横の距離が safeRadius 以内）
    private bool OnSafeSpot(Vector3 feet)
    {
        if (safeSpots == null) return false;
        for (int i = 0; i < safeSpots.Length; i++)
        {
            Transform s = safeSpots[i];
            if (s == null) continue;
            Vector3 d = feet - s.position;
            if (d.y > -0.1f && d.y < 2f && d.x * d.x + d.z * d.z < safeRadius * safeRadius) return true;
        }
        return false;
    }

    // プレイヤーも川上へ（少し上向きに）。自分のプレイヤーだけ
    private void KnockPlayer(VRCPlayerApi lp, Vector3 at)
    {
        _noPlayerHitUntil = Time.time + hitCooldown;
        Vector3 f = crossing != null ? crossing.forward : Vector3.forward; f.y = 0f; f.Normalize();
        lp.SetVelocity(f * playerKnockBack + Vector3.up * playerKnockUp);
        if (hitSound != null) { hitSound.transform.position = at; hitSound.pitch = Random.Range(0.8f, 0.9f); hitSound.Play(); }
    }
}
