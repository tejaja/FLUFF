using UdonSharp;
using UnityEngine;

// 2–4 はぐるまのとう：時計塔の針（2026-11-07 てじゃ「でかい時計塔にして、最後は塔を突っ切った先がゴール」）
// ・見ている人のパソコンの今の時刻を指す（同期なし。だれが見ても自分の時計どおり）
// ・針の軸（pivot）は local +Z が文字盤の外向き、+Y が文字盤の上。外から見て時計回り＝ local Z のプラス回転
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaClockHands : UdonSharpBehaviour
{
    public Transform[] hourHands;
    public Transform[] minuteHands;
    [Tooltip("針を動かす間かく（秒）")]
    public float interval = 0.5f;
    private float _next;

    private void Update()
    {
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
