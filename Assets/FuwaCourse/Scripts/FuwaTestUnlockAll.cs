using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// テスト用（2026-10-13 てじゃ「テスト用にコース報酬全部出すボタンほしい」「あとで消す」）：
// あそびばのクリア報酬のおもちゃを、このインスタンスの全員に5つ全部出す。ボタンはインスタンスのマスターにだけ見える（マスターが交代したら新しいマスターに）。
// 消す時は ChillRoom/Toys/TestUnlockAll と TestUnlockAllButton を消すだけ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaTestUnlockAll : UdonSharpBehaviour
{
    public FuwaChillSpawner spawner;
    [Tooltip("マスターにだけ出すボタン")]
    public GameObject button;
    private float _next;

    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + 1f;
        VRCPlayerApi lp = Networking.LocalPlayer;
        bool on = lp != null && lp.isMaster;
        if (button != null && button.activeSelf != on) button.SetActive(on);
    }

    public void UnlockAll()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (spawner == null || lp == null || !lp.isMaster) return;
        for (int i = 0; i < 5; i++) spawner.Unlock(i);
    }
}
