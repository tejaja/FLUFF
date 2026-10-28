using UdonSharp;
using UnityEngine;

// ステージ2「ほのおのやま」の熱（2026-10-28 試作）：かがり火（FuwaHeatSource）を通ったふわふわが温まって、
// 少し上がった所でふわっと止まって浮いたまま待つ（熱気球みたいに）。次に撃たれるまで浮いたまま（冷めない）。
// 2026-10-28 てじゃ「推進力がないから活用がむずかしい」「とどまるのを利用する手もある、タイミング図るのムズイポイント」「冷めるのはいらんかも」
// → 浮いて止まっている間に、動く物のすき間をねらって撃つ遊び。温まっている間はオレンジに光る
// 1つだけ置いて、かがり火から Heat() を呼ぶ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaHeat : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("温まった所から、どれだけ上で止まるか(m)")]
    public float hoverRise = 0.8f;
    [Tooltip("止まる所へ引っぱるばねの強さと、ゆれをおさえる強さ")]
    public float spring = 9f, damping = 4.5f;
    [Tooltip("横の動きをおさえる強さ（1/s）")]
    public float sideDamping = 3f;
    [Tooltip("ゆらゆら：上下の幅(m)と周期(秒)")]
    public float bobAmp = 0.08f, bobPeriod = 2.2f;
    [Tooltip("撃ってからこの秒数は温まり直さない（火の中から撃ってもすぐ止まらないように）")]
    public float reheatDelay = 0.6f;
    [Tooltip("止まる所からこれ以上はなれたら（スタートに戻ったなど）浮くのをやめる(m)")]
    public float maxLeash = 6f;
    public Color glowColor = new Color(1f, 0.55f, 0.15f);

    private bool _hover;
    private float _heatAt = -100f;
    private Vector3 _anchor;

    public void Heat()
    {
        if (ball == null || _hover) return;
        if (ball.LastShotAge() < reheatDelay) return;
        _hover = true; _heatAt = Time.time;
        _anchor = ball.transform.position + Vector3.up * hoverRise;
    }
    public bool IsHovering() { return _hover; }

    private void FixedUpdate()
    {
        if (ball == null || !_hover) return;
        // 撃たれたら（温まったあとに撃った）おしまい。はなれすぎてもおしまい
        if (ball.LastShotAge() < Time.time - _heatAt - 0.05f) { _hover = false; return; }
        Vector3 p = ball.transform.position;
        if ((p - _anchor).magnitude > maxLeash) { _hover = false; return; }
        Vector3 v = ball.GetVelocity();
        float ty = _anchor.y + Mathf.Sin((Time.time - _heatAt) * Mathf.PI * 2f / Mathf.Max(0.1f, bobPeriod)) * bobAmp;
        float ay = ball.GetFallAcceleration() + (ty - p.y) * spring - v.y * damping;
        Vector3 a = new Vector3(-v.x * sideDamping, ay, -v.z * sideDamping);
        ball.AddHeat(a);
        ball.Glow(glowColor, Vector3.up, 0.8f, 0.05f);
    }
}
