using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// レースのエントリー床（トリガー）。乗っている人を全員のPCで数えてレースに伝える。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaRaceEntry : UdonSharpBehaviour
{
    public FuwaRace race;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player != null && race != null) race.EntryEnter(player.playerId);
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player != null && race != null) race.EntryExit(player.playerId);
    }
}
