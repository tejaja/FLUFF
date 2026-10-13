using UdonSharp;
using UnityEngine;

// 持って回せる物についていく物を、いつもまっすぐ（ワールドの向きのまま）にしておく。
// あそびばの重力のたね：たねを回すと、まわりの紫の球のグラデと粒の出る向きまで回って変だった（2026-10-13 てじゃ「グラデの向きも固定にしよう」）
// follow を入れると、持てる物の子にしないで位置だけついていく（持った時の光る輪郭に、まわりの大きな球やウソ影まで入らないように。2026-10-13 てじゃ）
// 持っている手の動きは PostLateUpdate の時に反映されているので、そのあとで向き・位置を合わせる（遅れて1フレームだけずれて見えないように）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaKeepUpright : UdonSharpBehaviour
{
    [Tooltip("まっすぐにしておく物")]
    public Transform[] targets;
    [Tooltip("ついていく相手（なくてもOK）。offsets の差を保って、位置だけついていく")]
    public Transform follow;
    [Tooltip("targets と同じ順：follow からのずれ（ワールドの向き）。作る時に入れておく（Start が走る前に動かされてもずれないように）")]
    public Vector3[] offsets;

    public override void PostLateUpdate()
    {
        if (targets == null) return;
        for (int i = 0; i < targets.Length; i++)
        {
            Transform t = targets[i];
            if (t == null) continue;
            if (follow != null)
            {
                Vector3 p = follow.position + (offsets != null && i < offsets.Length ? offsets[i] : Vector3.zero);
                if (t.position != p) t.position = p;
            }
            if (t.rotation != Quaternion.identity) t.rotation = Quaternion.identity;
        }
    }
}
