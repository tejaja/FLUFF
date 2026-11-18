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
    [Tooltip("copterSafe の時、デバッグ飛行中（FuwaDebugFly）も無視する（公開前に消す物）")]
    public FuwaDebugFly debugFly;
    [Tooltip("2026-11-17 2–4 の回転床の下（下の段のコースの上）：足（GetPosition）がこの高さ（ワールド y）より下なら無視。下の段を歩いている人の頭が入っても落ちた扱いにしない")]
    public float minFeetY = -100000f;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || ball == null) return;
        if (player.GetPosition().y < minFeetY) return;
        bool held = HoldingCopter(player);
        // 2026-11-18 竹とんぼで落ちるとすぐ戻される原因さがし（あとで消す）
        Debug.Log("[FuwaCatch] " + gameObject.name + " (" + transform.root.name + ") copterSafe=" + copterSafe + " held=" + held + " copters=" + (copters != null ? copters.Length : -1) + " y=" + player.GetPosition().y.ToString("F1"));
        if (copterSafe && (held || (debugFly != null && debugFly.IsFlying()))) return;
        ball.BeginPlayerMiss("落ちた！", "You fell!");
    }

    [Tooltip("竹とんぼ（自分が持っているかは竹とんぼに聞く。手の中の物から探すのは実機で効かなかった。2026-11-18）")]
    public FuwaBambooCopter[] copters;

    private bool HoldingCopter(VRCPlayerApi p)
    {
        if (copters != null) for (int i = 0; i < copters.Length; i++) if (copters[i] != null && copters[i].IsMine()) return true;
        VRC_Pickup a = p.GetPickupInHand(VRC_Pickup.PickupHand.Left), b = p.GetPickupInHand(VRC_Pickup.PickupHand.Right);
        if (a != null && a.GetComponent<FuwaBambooCopter>() != null) return true;
        if (b != null && b.GetComponent<FuwaBambooCopter>() != null) return true;
        return false;
    }
}
