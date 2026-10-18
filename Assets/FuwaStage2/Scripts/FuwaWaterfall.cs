using UdonSharp;
using UnityEngine;

// 滝（ステージ2「みずのたに」、2026-10-17）。トリガーの範囲が落ちてくる水のカーテン。
// 中に入ったふわふわを下へ叩きつける（重くして速く落とす＋下向きに押す＋横の勢いを弱める）。
// 下に滝つぼ（FuwaWater）があればそこで浮いてセーフ。滝つぼのない崖の滝はそのまま落ちてアウト（てじゃ「水はセーフ、落ちたらアウト」）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWaterfall : UdonSharpBehaviour
{
    [Tooltip("中にいる間、落ちる加速度と落ちる速さの上限を何倍にするか")]
    public float gravityScale = 6f;
    [Tooltip("下向きにさらに押す加速度(m/s²)")]
    public float push = 6f;
    [Tooltip("横の勢いをどれだけ弱めるか（1/s）。大きいほど水に押されて真下に落ちる")]
    public float sideDamping = 2.5f;
    [Tooltip("水の進む向き（この物のローカル +Z）へ少し押す(m/s²)。滝の真下に居座らず、滝つぼの手前へ落ちるように")]
    public float forwardPush = 1.5f;
    [Tooltip("中にいる間の光の色（重くなるゾーンの紫と見分ける水色）")]
    public Color glow = new Color(0.55f, 0.85f, 1f, 1f);

    private void OnTriggerStay(Collider other)
    {
        if (other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;
        ball.SetGravityScaleTint(gravityScale, glow);
        Vector3 v = ball.GetVelocity();
        Vector3 f = transform.forward; f.y = 0f; f = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.zero;
        ball.AddWater(new Vector3(-v.x * sideDamping, -push, -v.z * sideDamping) + f * forwardPush);
    }
}
