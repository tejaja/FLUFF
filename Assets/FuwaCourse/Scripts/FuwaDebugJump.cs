using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// デバッグエリアのワープスイッチ（キューブ）。インタラクトで、そのギミックのレーンの手前へワープする。
// ワープ先のレーンに胞子も移す。同期なし（自分だけ）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaDebugJump : UdonSharpBehaviour
{
    [Tooltip("ワープ先（向きも使う）")]
    public Transform target;
    [Tooltip("ワープ先のレーン（胞子をそのレーンのキノコへ移す）")]
    public FuwaDebugLane lane;

    public override void Interact()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || target == null) return;
        p.TeleportTo(target.position, target.rotation);
        if (lane != null) lane.Activate();
    }
}
