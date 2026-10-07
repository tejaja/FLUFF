using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;

// クリック（Interact）でサンプルアバターに着替える看板。
// 着替えそのものは VRCAvatarPedestal の SetAvatarUse を使う。ペデスタル標準の板（真四角でサムネが縦横つぶれる）は
// Placement を見えない所・極小にして隠し、見た目はこの看板の画像で出す。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaAvatarSwitch : UdonSharpBehaviour
{
    public VRCAvatarPedestal pedestal;

    public override void Interact()
    {
        if (pedestal == null) return;
        pedestal.SetAvatarUse(Networking.LocalPlayer);
    }
}
