using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 上昇気流（人用）：トリガーの中にいる自分を上へ持ち上げる。
// ふわふわも持ち上げたい時は、同じオブジェクトに FuwaWindArea（上向き）も付ける。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPlayerUpdraft : UdonSharpBehaviour
{
    [Tooltip("持ち上げる速さ（上向き m/s）。これより遅い時だけ上書き")]
    public float liftSpeed = 3.2f;
    [Tooltip("横の速さをどれだけ残すか（1=そのまま）")]
    public float keepHorizontal = 1f;

    public override void OnPlayerTriggerStay(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        Vector3 v = player.GetVelocity();
        if (v.y >= liftSpeed) return;
        v.x *= keepHorizontal; v.z *= keepHorizontal;
        v.y = liftSpeed;
        player.SetVelocity(v);
    }
}
