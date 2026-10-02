using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 落とし穴の下に置くトリガー。自分が落ちたら「落ちた！」→暗転→最後のチェックポイントへ戻す（FuwaBall側で処理）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPlayerCatcher : UdonSharpBehaviour
{
    public FuwaBall ball;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || ball == null) return;
        ball.BeginPlayerMiss("落ちた！", "You fell!");
    }
}
