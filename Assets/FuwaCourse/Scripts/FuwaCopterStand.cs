using UdonSharp;
using UnityEngine;

// 竹とんぼ立て（2026-11-08 てじゃ「離しても自動で戻らなくする　元の台座をクリックで戻す」）：クリックすると自分の竹とんぼがここに戻る
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaCopterStand : UdonSharpBehaviour
{
    public FuwaBambooCopter copter;

    public override void Interact()
    {
        if (copter != null) copter.ReturnHome();
    }
}
