using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// トゲ。人が触ると最後のチェックポイントへ戻され、けだまが触るとアウト。
// モンスターも当たると倒れる（FuwaEnemy側で判定）

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSpike : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("人が触った時も「トゲ！」にするか（天井の鍾乳石などは false）")]
    public bool affectPlayers = true;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!affectPlayers) return;
        if (player == null || !player.isLocal || ball == null) return;
        // 「グサッ」→一瞬止まる→暗転→チェックポイントへ（FuwaBall側で処理）
        ball.BeginPlayerSpike("トゲ！", "Spiked!");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        FuwaBall b = other.GetComponent<FuwaBall>();
        if (b != null) b.FailBySpike();
    }
}
