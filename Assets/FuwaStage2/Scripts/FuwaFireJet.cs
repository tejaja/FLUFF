using UdonSharp;
using UnityEngine;

// 定期的に噴き出す火柱（2026-10-28 2–2 試作、てじゃ「火柱が定期的にでるとかできそう」）。
// 吹いている間だけ、柱の中に入ったふわふわを温める（FuwaHeat で止まる）。止まっている時に入っても何もない＝落ちる。
// 吹く前に warnTime 秒、小さいゆらぎ（前ぶれ）を出す。全員同じ時に吹くようにサーバー時刻で
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaFireJet : UdonSharpBehaviour
{
    public FuwaHeat heat;
    [Tooltip("ふわふわが止まる所（なければ入った所の少し上）")]
    public Transform hoverPoint;
    public float period = 3f, onTime = 1.2f, phase = 0f, warnTime = 0.6f;
    [Tooltip("吹いている間の炎")]
    public ParticleSystem flame;
    [Tooltip("前ぶれ（吹く前のちらちら）")]
    public ParticleSystem warn;
    public float flameRate = 80f, warnRate = 25f;
    [Tooltip("いっしょに吹くほかの炎・前ぶれ（縦に並べた炎の壁など）")]
    public ParticleSystem[] moreFlames, moreWarns;
    public AudioSource roar;
    [Tooltip("噴くと伸びる炎の本体（上向きが炎の向き）。前ぶれで少し出て、止まると引っ込む（2026-10-31）")]
    public Transform flameBody;
    public float bodyWarn = 0.16f, bodySpeed = 7f;
    [Tooltip("いっしょに噴く列の炎の本体（下から順）。flameBody の次の列から。rowDelay 秒ずつ遅れて出る（2026-11-02 てじゃ「3本の出るタイミングほんのちょっとずらそう」）")]
    public Transform[] moreBodies;
    public float rowDelay = 0f;
    private float[] _bodies, _burns; private Material[] _bodyMats;
    [Tooltip("止まった後、炎が根元から燃えつきて消えるまでの秒数（棒が引っ込むように見えた。2026-11-01 てじゃ）")]
    public float bodyAfter = 0.8f;
    private float _burn = 1f; private Material _bodyMat;
    [Header("予兆（2026-11-02 てじゃ「当たるエリアを地面に予兆として見せたい、赤いグラデ」）")]
    [Tooltip("地面の赤いエリア。吹く areaLead 秒前から出て、止まると消える")]
    public Renderer warnArea;
    public float areaLead = 1.2f, areaFade = 0.25f;
    private float _area; private Material _areaMat;
    [Header("火炎放射（2026-10-29 てじゃの 2–2「横の岩の穴から吹き出す、当たるとアウト、人はちょっと吹っ飛ぶ」）")]
    [Tooltip("当たったふわふわは黒焦げ→アウト（温めて浮かせるのではなく）")]
    public bool deadly = false;
    [Tooltip("吹いている間に入った人を、炎の向き（このトリガーの上向き）へ吹っ飛ばす（0なら何もしない）")]
    public float playerPush = 0f, playerPushUp = 1.5f;
    [Tooltip("人を飛ばす向き（ワールド、水平）。ゼロなら炎の向き")]
    public Vector3 playerPushDir = Vector3.zero;
    [Header("吹っ飛ばし（2026-10-30 てじゃ「あたると即死じゃなくて大きく吹き飛び炎上、飛ばされたら急いで拾わないと燃えちゃう」）")]
    [Tooltip("ballKnock > 0 なら、当たったふわふわを燃やして knockDir へ吹っ飛ばす（1回吹くごとに1回）。deadly より先")]
    public FuwaBall ball;
    public Vector3 knockDir = Vector3.back;
    public float ballKnock = 0f, ballKnockUp = 5f, knockFlight = 0.8f, knockDrag = 0.4f;
    [Tooltip("あれば、ここから knockDir へ knockReach m の所より先へは飛ばさない（道から落ちないくらい外へ。2026-10-30 てじゃ「落ちないくらい外に吹っ飛ばす」）。残りの距離 × knockPerMeter が横の速さの上限")]
    public Transform knockOrigin;
    public float knockReach = 0f, knockPerMeter = 0.8f;
    [Tooltip("ふちまでの残りが少なくても、これだけの横の速さでは飛ばす（2026-11-02 てじゃ「先端に当たった時のふっ飛ばしがヨワすぎる」。先端では残り距離の計算で 0.2m/s しかなかった）")]
    public float knockMin = 0f;
    [Tooltip("ふちの制限なしで足す横の速さ（ワールド、道にそって来た方など。2026-10-30 てじゃ「ふっ飛ばし力もうちょい」）")]
    public Vector3 knockSide = Vector3.zero;
    [Tooltip("人も knockOrigin から playerPushDir へ knockReach m より先へは飛ばさない。残りの距離 × playerPerMeter が横の速さの上限（0 なら制限なし）")]
    public float playerPerMeter = 0f;
    [Tooltip("人だけ別の到達距離（0 なら knockReach）。ふわふわはふちの外まで飛ばしても、人は落とさない")]
    public float playerReach = 0f;
    [Tooltip("人の横の速さの下限（先端で当たっても、これだけは飛ぶ）")]
    public float playerMin = 0f;
    public Vector3 playerSide = Vector3.zero;
    private bool _wasOn, _blasted;
    private float _pushedAt = -10f, _body;

    // ずっと燃えているだけの物（溶岩・炎の壁）は毎フレームの処理をしない（2026-11-01 負荷対策）
    private bool _idleChecked, _idle;
    public bool IsOn() { return onTime >= period || T() < onTime; }
    // 周期の中の今の時刻（秒）。サーバー時刻の整数ミリ秒で割り算する（2026-10-29 VRChat でだけ火炎放射が止まらない・火の玉が変な所を飛んだ：
    // 秒の double にずらしを足して割る方式だと実機で狂った。FuwaWhirlwind と同じミリ秒の方式に）
    private float T()
    {
        int per = Mathf.Max(1, Mathf.RoundToInt(period * 1000f));
        int ms = (VRC.SDKBase.Networking.GetServerTimeInMilliseconds() + Mathf.RoundToInt(phase * 1000f)) % per;
        if (ms < 0) ms += per;
        return ms / 1000f;
    }

    private void Update()
    {
        if (!_idleChecked) { _idleChecked = true; _idle = onTime >= period && flame == null && warn == null && flameBody == null && roar == null && playerPush <= 0f && (moreFlames == null || moreFlames.Length == 0) && (moreWarns == null || moreWarns.Length == 0); }
        if (_idle) return;
        float t = T(); bool on = t < onTime; bool w = !on && t > period - warnTime;
        if (flame != null) { var e = flame.emission; e.rateOverTime = on ? flameRate : 0f; }
        if (warn != null) { var e = warn.emission; e.rateOverTime = w ? warnRate : 0f; }
        if (moreFlames != null) for (int i = 0; i < moreFlames.Length; i++) if (moreFlames[i] != null) { var e = moreFlames[i].emission; e.rateOverTime = RowOn(t, i + 1) ? flameRate : 0f; }
        if (moreWarns != null) for (int i = 0; i < moreWarns.Length; i++) if (moreWarns[i] != null) { var e = moreWarns[i].emission; e.rateOverTime = w ? warnRate : 0f; }
        if (flameBody != null)
        {
            if (on) { _body = Mathf.MoveTowards(_body, 1f, bodySpeed * Time.deltaTime); _burn = 0f; }
            else if (_burn < 1f && _body > bodyWarn + 0.01f) { _burn = Mathf.Min(1f, _burn + Time.deltaTime / Mathf.Max(0.05f, bodyAfter)); if (_burn >= 1f) _body = 0f; }   // 長さはそのまま、根元から燃えつきる
            else { _body = w ? Mathf.MoveTowards(_body, bodyWarn, bodySpeed * Time.deltaTime) : 0f; _burn = w ? 0f : 1f; }
            bool show = _body > 0.001f;
            if (flameBody.gameObject.activeSelf != show) flameBody.gameObject.SetActive(show);
            if (show)
            {
                flameBody.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, _body), _body, Mathf.Lerp(0.6f, 1f, _body));
                if (_bodyMat == null) _bodyMat = flameBody.GetComponent<Renderer>().material;
                _bodyMat.SetFloat("_Burn", _burn);
            }
            if (moreBodies != null && moreBodies.Length > 0)
            {
                if (_bodies == null) { _bodies = new float[moreBodies.Length]; _burns = new float[moreBodies.Length]; _bodyMats = new Material[moreBodies.Length]; for (int i = 0; i < _burns.Length; i++) _burns[i] = 1f; }
                for (int i = 0; i < moreBodies.Length; i++)
                {
                    Transform b = moreBodies[i]; if (b == null) continue;
                    float tr = RowT(t, i + 1); bool ron = tr < onTime, rw = !ron && tr > period - warnTime;
                    if (ron) { _bodies[i] = Mathf.MoveTowards(_bodies[i], 1f, bodySpeed * Time.deltaTime); _burns[i] = 0f; }
                    else if (_burns[i] < 1f && _bodies[i] > bodyWarn + 0.01f) { _burns[i] = Mathf.Min(1f, _burns[i] + Time.deltaTime / Mathf.Max(0.05f, bodyAfter)); if (_burns[i] >= 1f) _bodies[i] = 0f; }
                    else { _bodies[i] = rw ? Mathf.MoveTowards(_bodies[i], bodyWarn, bodySpeed * Time.deltaTime) : 0f; _burns[i] = rw ? 0f : 1f; }
                    bool rshow = _bodies[i] > 0.001f;
                    if (b.gameObject.activeSelf != rshow) b.gameObject.SetActive(rshow);
                    if (rshow)
                    {
                        float s = Mathf.Lerp(0.6f, 1f, _bodies[i]);
                        b.localScale = new Vector3(s, _bodies[i], s);
                        if (_bodyMats[i] == null) _bodyMats[i] = b.GetComponent<Renderer>().material;
                        _bodyMats[i].SetFloat("_Burn", _burns[i]);
                    }
                }
            }
        }
        if (warnArea != null)
        {
            float want = on || t > period - areaLead ? 1f : 0f;
            if (_area != want)
            {
                _area = Mathf.MoveTowards(_area, want, Time.deltaTime / Mathf.Max(0.05f, areaFade));
                bool showArea = _area > 0.001f;
                if (warnArea.enabled != showArea) warnArea.enabled = showArea;
                if (_areaMat == null) _areaMat = warnArea.material;
                _areaMat.SetFloat("_Alpha", _area);
            }
        }
        if (on && !_wasOn) { _blasted = false; if (roar != null && roar.clip != null) roar.PlayOneShot(roar.clip); }
        _wasOn = on;
        if (on) PushPlayer();
    }

    // row 列目（0 が先頭）の周期の中の時刻。rowDelay 秒ずつ遅れる
    private float RowT(float t, int row)
    {
        float r = t - row * rowDelay;
        return r < 0f ? r + period : r;
    }
    private bool RowOn(float t, int row) { return RowT(t, row) < onTime; }

    private void OnTriggerStay(Collider other)
    {
        if (heat == null || other == null || !IsOn()) return;
        if (other.GetComponent<FuwaBall>() == null) return;
        if (ballKnock > 0f && ball != null)
        {
            if (_blasted) return;
            _blasted = true;
            heat.Ignite();
            Vector3 d = knockDir; d.y = 0f; d = d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.zero;
            float sp = ballKnock;
            if (knockOrigin != null && knockReach > 0f)
            {
                Vector3 off = ball.transform.position - knockOrigin.position; off.y = 0f;
                sp = Mathf.Max(knockMin, Mathf.Min(sp, Mathf.Max(0f, knockReach - Vector3.Dot(off, d)) * knockPerMeter));
            }
            ball.LaunchFlight(d * sp + knockSide + Vector3.up * ballKnockUp, knockFlight, knockDrag);
            return;
        }
        if (deadly) { heat.BurnOut(); return; }
        if (hoverPoint != null) heat.HeatAt(hoverPoint.position); else heat.Heat();
    }

    // 人：トリガーの通知でなく毎フレーム位置で見る（レイヤーによっては人の通知が来ないので）
    private BoxCollider _box;
    private void PushPlayer()
    {
        if (playerPush <= 0f || Time.time - _pushedAt < 0.6f) return;
        VRC.SDKBase.VRCPlayerApi lp = VRC.SDKBase.Networking.LocalPlayer;
        if (lp == null) return;
        if (_box == null) _box = GetComponent<BoxCollider>();
        if (_box == null) return;
        Vector3 f = lp.GetPosition();
        bool inside = false;
        for (int k = 0; k < 3 && !inside; k++)
        {
            Vector3 q = transform.InverseTransformPoint(f + Vector3.up * (0.2f + 0.6f * k)) - _box.center;
            Vector3 e = _box.size * 0.5f;
            inside = Mathf.Abs(q.x) < e.x && Mathf.Abs(q.y) < e.y && Mathf.Abs(q.z) < e.z;
        }
        if (!inside) return;
        _pushedAt = Time.time;
        Vector3 d = playerPushDir.sqrMagnitude > 0.0001f ? playerPushDir : transform.up; d.y = 0f; d = d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.zero;
        float sp = playerPush;
        if (playerPerMeter > 0f && knockOrigin != null && knockReach > 0f)
        {
            Vector3 off = f - knockOrigin.position; off.y = 0f;
            sp = Mathf.Max(playerMin, Mathf.Min(sp, Mathf.Max(0f, (playerReach > 0f ? playerReach : knockReach) - Vector3.Dot(off, d)) * playerPerMeter));
        }
        lp.SetVelocity(d * sp + playerSide + Vector3.up * playerPushUp);
    }
}
