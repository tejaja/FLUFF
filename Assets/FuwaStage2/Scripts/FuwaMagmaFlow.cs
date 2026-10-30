using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 火口から一定間隔で噴き出して、山の斜面をつたって まわりの輪っかの道の一部に流れ出るマグマ（2026-10-30 てじゃの 2–2 の最後：
// 「火口の周りに道、左右どっちから進んでもok。火口からは一定間隔でマグマが吹き出て道の一部にながれてくる、タイミングを見て進む」「マグマは即死、ふわふわも人も」）。
// 1周期（period 秒）：0 で噴火 → 斜面を流れ落ちる（streamTime）→ 道に広がる（spread）→ 居座る（stay）→ 冷えて沈む（sink）→ 次の噴火まで何もない。
// 全員同じ時に流れるようにサーバー時刻で（FuwaFireball と同じ整数ミリ秒）。
// 道の上の広がりは center のまわりの扇形（角度 a0〜a1、半径 rIn〜rOut）。角度は center から見て、0 が center の後ろ（-z）、+ が右（+x）回り。
// 見た目の sheet は center を中心に横に縮めた所から 1 へ広げる（内側は山の中に隠れる）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaMagmaFlow : UdonSharpBehaviour
{
    public FuwaHeat heat;
    public FuwaBall ball;
    [Tooltip("火口の中心（道の面の高さ）")]
    public Transform center;
    public float period = 6f, phase = 0f;
    public float streamTime = 0.9f, spread = 0.7f, stay = 2.4f, sink = 0.6f;
    public float a0 = -10f, a1 = 10f, rIn = 6f, rOut = 9f;
    [Tooltip("道の上に広がるマグマ（center と同じ所・向き。横の大きさを変えて広げる）")]
    public Transform sheet;
    [Tooltip("斜面をつたうマグマ（上のはしが原点、+z が下向き。z の大きさを変えて伸ばす）")]
    public Transform stream;
    [Tooltip("噴火の火柱（なくてもOK。1つの火口に1つだけつける）")]
    public ParticleSystem burst;
    [Tooltip("噴火の前ぶれ（なくてもOK）")]
    public ParticleSystem warn;
    public float warnTime = 1.2f, warnRate = 30f;
    public AudioSource boom;
    [Tooltip("道の面からこの高さまでのふわふわ・人に当たる")]
    public float hitHeight = 1.0f;

    private float _lastT = -1f;
    private float _ballRadius = 0.4f;
    private bool _hitPlayer;
    private Vector3 _sheetPos;

    private void Start()
    {
        if (ball != null)
        {
            SphereCollider sc = ball.GetComponent<SphereCollider>();
            if (sc != null) _ballRadius = sc.radius * ball.transform.lossyScale.x;
        }
        if (sheet != null) _sheetPos = sheet.localPosition;
    }

    private float T()
    {
        int per = Mathf.Max(1, Mathf.RoundToInt(period * 1000f));
        int ms = (Networking.GetServerTimeInMilliseconds() + Mathf.RoundToInt(phase * 1000f)) % per;
        if (ms < 0) ms += per;
        return ms / 1000f;
    }

    private void Update()
    {
        float t = T();
        if (t < _lastT)
        {
            if (burst != null) burst.Play();
            if (boom != null && boom.clip != null) boom.PlayOneShot(boom.clip);
            _hitPlayer = false;
        }
        _lastT = t;
        if (warn != null) { var e = warn.emission; e.rateOverTime = t > period - warnTime ? warnRate : 0f; }

        // 斜面：0〜streamTime で上から伸びる、道で冷えて沈みはじめたら消える
        float t1 = streamTime + spread, t2 = t1 + stay, t3 = t2 + sink;
        if (stream != null)
        {
            bool on = t < t2;
            if (stream.gameObject.activeSelf != on) stream.gameObject.SetActive(on);
            if (on) stream.localScale = new Vector3(1f, 1f, Mathf.Clamp(t / streamTime, 0.02f, 1f));
        }
        // 道：streamTime〜t1 で広がる、t2〜t3 で沈む
        float s0 = rIn / rOut * 0.97f, s = 0f, down = 0f;
        bool active = t > streamTime && t < t3;
        if (active)
        {
            s = Mathf.Lerp(s0, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - streamTime) / spread)));
            down = Mathf.Clamp01((t - t2) / sink);
        }
        if (sheet != null)
        {
            if (sheet.gameObject.activeSelf != active) sheet.gameObject.SetActive(active);
            if (active) { sheet.localScale = new Vector3(s, 1f, s); sheet.localPosition = _sheetPos + Vector3.down * (down * 0.2f); }
        }
        // 沈みはじめたらもう当たらない
        if (!active || down > 0f || center == null) return;
        float reach = s * rOut;

        if (ball != null && !ball.IsWaitingOrHidden() && Inside(ball.transform.position, reach + _ballRadius, _ballRadius))
        {
            if (heat != null) heat.BurnOut();
        }
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!_hitPlayer && lp != null && ball != null && Inside(lp.GetPosition(), reach, 0f))
        {
            _hitPlayer = true;
            ball.BeginPlayerMiss("あちっ！", "Too hot!");
        }
    }

    // 扇形（角度 a0〜a1、半径 rIn〜reach）の中で、道の面から hitHeight＋pad までの高さ
    private bool Inside(Vector3 p, float reach, float pad)
    {
        Vector3 q = center.InverseTransformPoint(p);
        if (q.y < -0.5f || q.y > hitHeight + pad) return false;
        float r = Mathf.Sqrt(q.x * q.x + q.z * q.z);
        if (r > reach || r < rIn - 0.5f) return false;
        float a = Mathf.Atan2(q.x, -q.z) * Mathf.Rad2Deg;
        float pa = pad / Mathf.Max(0.5f, r) * Mathf.Rad2Deg;
        return a > a0 - pa && a < a1 + pa;
    }
}
