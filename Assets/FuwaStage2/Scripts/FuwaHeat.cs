using UdonSharp;
using UnityEngine;

// ステージ2「ほのおのやま」の熱（2026-10-28 試作）：かがり火（FuwaHeatSource）を通ったふわふわが温まって、しばらく軽くなって上へ浮く（熱気球みたいに）。
// 温まり方は「通ると満タン」、heatTime 秒かけて冷める。浮く力は重力の liftK 倍の上向き（冷めるにつれて弱く）。温まっている間はオレンジに光る
// 1つだけ置いて、かがり火から Heat() を呼ぶ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaHeat : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("満タンから冷めきるまでの秒数")]
    public float heatTime = 4f;
    [Tooltip("満タンの時の上向きの力（ふわふわの落ちる加速度の何倍か。1より大きいと上がる）")]
    public float liftK = 1.35f;
    [Tooltip("冷め方（横軸0＝温まった直後〜1＝冷めきり、縦軸＝浮く力の割合）")]
    public AnimationCurve liftCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.6f, 0.85f), new Keyframe(1f, 0f));
    public Color glowColor = new Color(1f, 0.55f, 0.15f);

    private float _heatAt = -100f;

    public void Heat() { _heatAt = Time.time; }
    public float Heat01() { float t = (Time.time - _heatAt) / Mathf.Max(0.01f, heatTime); return t >= 1f ? 0f : 1f - t; }

    private void FixedUpdate()
    {
        if (ball == null) return;
        float t = (Time.time - _heatAt) / Mathf.Max(0.01f, heatTime);
        if (t >= 1f) return;
        float k = liftCurve.Evaluate(t);
        ball.AddHeat(Vector3.up * (ball.GetFallAcceleration() * liftK * k));
        ball.Glow(glowColor, Vector3.up, 0.35f + 0.65f * k, 0.05f);
    }
}
