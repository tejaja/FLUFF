using UdonSharp;
using UnityEngine;

// 持って回せる物の子を、親の向きに関係なくいつもまっすぐ（ワールドの向きのまま）にしておく。
// あそびばの重力のたね：たねを回すと、まわりの紫の球のグラデと粒の出る向きまで回って変だった（2026-10-13 てじゃ「グラデの向きも固定にしよう」）
// 持っている手の動きは PostLateUpdate の時に反映されているので、そのあとで向きを戻す（遅れて1フレームだけ回って見えないように）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaKeepUpright : UdonSharpBehaviour
{
    [Tooltip("まっすぐにしておく物")]
    public Transform[] targets;

    public override void PostLateUpdate()
    {
        if (targets == null) return;
        for (int i = 0; i < targets.Length; i++)
        {
            Transform t = targets[i];
            if (t != null && t.rotation != Quaternion.identity) t.rotation = Quaternion.identity;
        }
    }
}
