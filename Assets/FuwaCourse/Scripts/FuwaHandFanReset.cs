using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

// おもちゃ広場の「おもちゃを戻す」ボタン：手持ち扇風機・うちわ（FuwaHandFan）を止めて元の場所へ。
// ほかの持てるおもちゃ（リング・竜巻のたね・重力のたね・ぬいぐるみ）も元の場所へ（出ている物だけ）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaHandFanReset : UdonSharpBehaviour
{
    public FuwaHandFan[] fans;
    [Tooltip("ほかの持てるおもちゃ（VRCObjectSync つき）")]
    public GameObject[] others;

    public override void Interact()
    {
        if (fans != null) foreach (FuwaHandFan f in fans) if (f != null) f.ResetFan();
        if (others == null) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        foreach (GameObject g in others)
        {
            if (g == null || !g.activeInHierarchy) continue;
            if (!Networking.IsOwner(g)) Networking.SetOwner(lp, g);
            VRCPickup p = (VRCPickup)g.GetComponent(typeof(VRCPickup));
            if (p != null) p.Drop();
            VRCObjectSync s = (VRCObjectSync)g.GetComponent(typeof(VRCObjectSync));
            if (s != null) s.Respawn();
        }
    }
}
