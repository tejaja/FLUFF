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

    [Tooltip("オンなら、看板そのもののクリックはやめて、そばのレーザーボタン（Use を呼ぶ）だけで着替える。今はオフ（看板のサムネをクリック。2026-10-12 てじゃ「ボタンなし、サムネだけで」）")]
    public bool laserOnly = false;

    private void Start() { if (laserOnly) DisableInteractive = true; }

    public override void Interact() { Use(); }

    // レーザーで押すボタンから（SendCustomEvent "Use"）
    public void Use()
    {
        if (pedestal == null) return;
        pedestal.SetAvatarUse(Networking.LocalPlayer);
    }
}
