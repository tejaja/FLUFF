using UdonSharp;
using UnityEngine;

// 2–4 はぐるまのとう：時計塔の針と振り子（2026-11-07 てじゃ「でかい時計塔にして、最後は塔を突っ切った先がゴール」「なにかしら歯車と絡めたい」）
// ・針：見ている人のパソコンの今の時刻を指す（同期なし。だれが見ても自分の時計どおり）
//   針の軸（pivot）は local +Z が文字盤の外向き、+Y が文字盤の上。外から見て時計回り＝ local Z のプラス回転
// ・振り子：塔の入口の前で道の上を左右にゆれる（local Z まわり）。ふわふわに当たるとはじく（おもりに FuwaBounce）
//   脱進機：振り子が1回ゆれきるたびに、がんぎ車（escapeWheel、local Y まわり）が歯の半分だけカチッと進む
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaClockHands : UdonSharpBehaviour
{
    public Transform[] hourHands;
    public Transform[] minuteHands;
    [Tooltip("針を動かす間かく（秒）")]
    public float interval = 0.5f;
    private float _next;

    [Header("振り子と脱進機")]
    public Transform pendulum;
    public float pendulumAmp = 28f;
    [Tooltip("1往復の時間（秒）")]
    public float pendulumPeriod = 5.3f;
    public Transform escapeWheel;
    public int escapeTeeth = 30;
    [Tooltip("カチッと進むのにかかる時間（往復の何割か）")]
    public float tickTime = 0.06f;

    private void Update()
    {
        if (pendulum != null && pendulumPeriod > 0f)
        {
            float ph = Time.time / pendulumPeriod;
            pendulum.localRotation = Quaternion.Euler(0f, 0f, pendulumAmp * Mathf.Sin(ph * 2f * Mathf.PI));
            if (escapeWheel != null && escapeTeeth > 0)
            {
                // ゆれきる所（ph = 0.25, 0.75 …）で半歯ずつ
                float b = ph * 2f - 0.5f;
                float n = Mathf.Floor(b), f = Mathf.Clamp01((b - n) / Mathf.Max(0.001f, tickTime * 2f));
                f = f * f * (3f - 2f * f);
                escapeWheel.localRotation = Quaternion.Euler(0f, -(n + f) * 180f / escapeTeeth, 0f);
            }
        }
        if (Time.time < _next) return;
        _next = Time.time + interval;
        System.DateTime t = System.DateTime.Now;
        float m = t.Minute + t.Second / 60f;
        float h = (t.Hour % 12) + m / 60f;
        Quaternion qm = Quaternion.Euler(0f, 0f, m * 6f), qh = Quaternion.Euler(0f, 0f, h * 30f);
        if (hourHands != null) for (int i = 0; i < hourHands.Length; i++) if (hourHands[i] != null) hourHands[i].localRotation = qh;
        if (minuteHands != null) for (int i = 0; i < minuteHands.Length; i++) if (minuteHands[i] != null) minuteHands[i].localRotation = qm;
    }
}
