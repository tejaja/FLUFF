using UdonSharp;
using UnityEngine;

// 扇風機の台のボタン：手持ち扇風機（FuwaHandFan）を全部、元の場所へ戻して止める
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaHandFanReset : UdonSharpBehaviour
{
    public FuwaHandFan[] fans;
    public override void Interact()
    {
        if (fans == null) return;
        foreach (FuwaHandFan f in fans) if (f != null) f.ResetFan();
    }
}
