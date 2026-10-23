using UdonSharp;
using UnityEngine;

// 上の面に乗ったら（着地したら）アウト、横の面に当たった時は何もしない（ふつうの物理で当たるだけ）物。
// 溝の岩壁など。判定は FuwaBall が当たった面の向きで行う
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaTopOut : UdonSharpBehaviour
{
    [Tooltip("当たった面の上向き成分がこれ以上なら「上の面」＝アウト（0.6 ≒ 53度より平ら）")]
    public float minUp = 0.6f;
    [Tooltip("当たる直前にこれ(m/s)より速く上がっていたら、上の面でもアウトにしない（ふちの角をかすって上がっていく時）")]
    public float risingSpeed = 0.5f;
}
