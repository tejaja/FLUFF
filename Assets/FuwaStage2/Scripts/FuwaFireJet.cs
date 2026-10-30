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
    [Tooltip("ふちの制限なしで足す横の速さ（ワールド、道にそって来た方など。2026-10-30 てじゃ「ふっ飛ばし力もうちょい」）")]
    public Vector3 knockSide = Vector3.zero;
    [Tooltip("人も knockOrigin から playerPushDir へ knockReach m より先へは飛ばさない。残りの距離 × playerPerMeter が横の速さの上限（0 なら制限なし）")]
    public float playerPerMeter = 0f;
    public Vector3 playerSide = Vector3.zero;
    private bool _wasOn, _blasted;
    private float _pushedAt = -10f;

    public bool IsOn() { return T() < onTime; }
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
        float t = T(); bool on = t < onTime; bool w = !on && t > period - warnTime;
        if (flame != null) { var e = flame.emission; e.rateOverTime = on ? flameRate : 0f; }
        if (warn != null) { var e = warn.emission; e.rateOverTime = w ? warnRate : 0f; }
        if (moreFlames != null) for (int i = 0; i < moreFlames.Length; i++) if (moreFlames[i] != null) { var e = moreFlames[i].emission; e.rateOverTime = on ? flameRate : 0f; }
        if (moreWarns != null) for (int i = 0; i < moreWarns.Length; i++) if (moreWarns[i] != null) { var e = moreWarns[i].emission; e.rateOverTime = w ? warnRate : 0f; }
        if (on && !_wasOn) { _blasted = false; if (roar != null && roar.clip != null) roar.PlayOneShot(roar.clip); }
        _wasOn = on;
        if (on) PushPlayer();
    }

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
                sp = Mathf.Min(sp, Mathf.Max(0f, knockReach - Vector3.Dot(off, d)) * knockPerMeter);
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
            sp = Mathf.Min(sp, Mathf.Max(0f, knockReach - Vector3.Dot(off, d)) * playerPerMeter);
        }
        lp.SetVelocity(d * sp + playerSide + Vector3.up * playerPushUp);
    }
}
