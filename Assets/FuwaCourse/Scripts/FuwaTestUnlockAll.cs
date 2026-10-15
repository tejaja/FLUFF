using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// テスト用（2026-10-13 てじゃ「テスト用にコース報酬全部出すボタンほしい」「あとで消す」）：
// あそびばのクリア報酬のおもちゃを、このインスタンスの全員に5つ全部出す。ボタンはインスタンスのマスターにだけ見える（マスターが交代したら新しいマスターに）。
// 消す時は FuwaPlayground/Room/Toys/TestUnlockAll と TestUnlockAllButton を消すだけ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaTestUnlockAll : UdonSharpBehaviour
{
    public FuwaPlaygroundSpawner spawner;
    [Tooltip("マスターにだけ出すボタン")]
    public GameObject button;
    [Tooltip("さらにこの表示名の人だけに限る（空なら名前は見ない）。保険（2026-10-14 てじゃ）")]
    public string[] ownerNames;
    private float _next;

    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + 1f;
        VRCPlayerApi lp = Networking.LocalPlayer;
        bool on = lp != null && CanUse(lp);
        if (button != null && button.activeSelf != on) button.SetActive(on);
    }

    public void UnlockAll()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (spawner == null || lp == null || !CanUse(lp)) return;
        for (int i = 0; i < 5; i++) spawner.Unlock(i);
    }

    // 名前（ownerNames）が入っていればその人だけ（マスターでなくても。入り直すとマスターでなくなって消えたので。2026-10-16 てじゃ）。名前が空ならマスターだけ
    private bool CanUse(VRCPlayerApi p)
    {
        if (ownerNames == null || ownerNames.Length == 0) return p.isMaster;
        string n = p.displayName;
        for (int i = 0; i < ownerNames.Length; i++) if (ownerNames[i] == n) return true;
        return false;
    }
}
