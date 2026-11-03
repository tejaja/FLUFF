using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 氷のテスト場とロビーの行き来（テスト用、公開前に IceTest ごと消す）。クリックでワープ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaIceTestWarp : UdonSharpBehaviour
{
    public Transform target;

    public override void Interact()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p != null && target != null) p.TeleportTo(target.position, target.rotation);
    }
}
