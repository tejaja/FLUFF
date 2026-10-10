using UdonSharp;
using UnityEngine;

// チーム分けの部屋のボタン（クリックで FuwaMatch の Ui〇〇 を呼ぶだけ）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaMatchButton : UdonSharpBehaviour
{
    public FuwaMatch match;
    [Tooltip("FuwaMatch の呼ぶ関数（UiJoin0 / UiStart など）")]
    public string eventName;

    public override void Interact()
    {
        if (match != null && !string.IsNullOrEmpty(eventName)) match.SendCustomEvent(eventName);
    }
}
