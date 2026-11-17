using UdonSharp;
using UnityEngine;

// 2026-11-17 持てる物の子にしないで、位置と向きをまるごとついていかせる（持った時のハイライトに大きな光の板が入らないように）
// あそびばのポータル：光るふち・足もとの光・粒（FloatFrame）を持ち手の外に出して、ポータル本体についていく
// 持っている手の動きは PostLateUpdate で反映済みなので、そのあとで合わせる（FuwaKeepUpright と同じ）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaFollowPose : UdonSharpBehaviour
{
    public Transform follow;
    public override void PostLateUpdate()
    {
        if (follow == null) return;
        Transform t = transform;
        Vector3 p = follow.position; Quaternion r = follow.rotation;
        if (t.position != p || t.rotation != r) t.SetPositionAndRotation(p, r);
    }
}
