using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// デバッグエリアの1レーン。自分がレーンに入ったら、ふわふわ（胞子）をこのレーンのスタートのキノコに移す。
// ふわふわはワールドに1個だけなので、どのギミックでもすぐ試せるように、入ったレーンに付いてくる形にする。
// タイムは計らない（記録には残らない）。同期なし
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaDebugLane : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("このレーンの胞子のスタート位置（スタートのキノコの中）")]
    public Transform laneStart;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        Activate();
    }

    // ワープのスイッチからも呼ばれる（テレポートだとトリガーに入った扱いにならないことがあるので）
    public void Activate()
    {
        if (ball == null || laneStart == null || ball.startPoint == laneStart) return;
        ball.StartCourse(laneStart, false);
    }
}
