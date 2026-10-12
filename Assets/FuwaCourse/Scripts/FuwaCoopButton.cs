using UdonSharp;
using UnityEngine;

// 「みんなで」協力の試作場の「コースを変える」ボタン（押した人が新しい種を決める。FuwaCoopCourse.Reroll）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaCoopButton : UdonSharpBehaviour
{
    public FuwaCoopCourse course;
    public override void Interact() { if (course != null) course.Reroll(); }
}
