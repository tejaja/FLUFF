using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 落とし穴の下に置くトリガー。自分が落ちたら「落ちた！」→暗転→最後のチェックポイントへ戻す（FuwaBall側で処理）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPlayerCatcher : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("2026-11-16 てじゃ「竹とんぼもってるときは、各ステージの一番下のアウト判定以外は無視」：竹とんぼを持っている間は無視する")]
    public bool copterSafe = false;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || ball == null) return;
        if (copterSafe && HoldingCopter(player)) return;
        ball.BeginPlayerMiss("落ちた！", "You fell!");
    }

    private bool HoldingCopter(VRCPlayerApi p)
    {
        VRC_Pickup a = p.GetPickupInHand(VRC_Pickup.PickupHand.Left), b = p.GetPickupInHand(VRC_Pickup.PickupHand.Right);
        if (a != null && a.GetComponent<FuwaBambooCopter>() != null) return true;
        if (b != null && b.GetComponent<FuwaBambooCopter>() != null) return true;
        return false;
    }
}
