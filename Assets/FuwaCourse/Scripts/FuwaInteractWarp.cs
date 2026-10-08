using UdonSharp;
using UnityEngine;

// クリック（Interact）か、UIボタン（Press）でワープする。乗ると飛ぶ床（FuwaCourseWarp）と同じ処理を、クリックで呼ぶだけ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaInteractWarp : UdonSharpBehaviour
{
    public FuwaCourseWarp warp;
    [Tooltip("暗転せずにすぐワープする（デバッグ用のキューブなど）")]
    public bool noFade = false;

    public override void Interact()
    {
        Press();
    }

    // レーザーで押すUIボタン（Button の onClick → SendCustomEvent("Press")）からも呼ぶ
    public void Press()
    {
        if (warp == null) return;
        if (noFade) warp.DoWarp();
        else warp.WarpLocalPlayer();
    }
}
