using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// テスト用（2026-10-22 てじゃ「テストのたびにふわふわはこぶのめんどい」。公開前に消す）：
// PC で key を押すと、目の前にふわふわをポンと出して、そこをチェックポイントにする（タイムなし。落ちたらそこに戻る）。
// ownerNames の人だけ使える。消す時は FuwaSystem/DebugBallSummon と FuwaBall.DebugSummon を消すだけ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaDebugBallSummon : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("ふわふわを置く場所（押すたびに目の前へ動かす）")]
    public Transform spot;
    public KeyCode key = KeyCode.G;
    [Tooltip("この表示名の人だけ使える（空なら誰も使えない）")]
    public string[] ownerNames;
    public float distance = 1.6f;   // 頭から前へ(m)
    public float drop = 0.4f;       // 頭から下へ(m)
    [Tooltip("これより低い所で出したらステージ2（アウトの高さが変わる）")]
    public float stage2BelowY = -60f;

    private void Update()
    {
        if (Input.GetKeyDown(key)) Summon();
    }

    public void Summon()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || ball == null || spot == null || !CanUse(p)) return;
        VRCPlayerApi.TrackingData head = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 fwd = head.rotation * Vector3.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        fwd.Normalize();
        spot.SetPositionAndRotation(head.position + fwd * distance + Vector3.down * drop, Quaternion.LookRotation(fwd, Vector3.up));
        ball.DebugSummon(spot, spot.position.y < stage2BelowY ? 2 : 1);
    }

    private bool CanUse(VRCPlayerApi p)
    {
        if (ownerNames == null) return false;
        string n = p.displayName;
        for (int i = 0; i < ownerNames.Length; i++) if (ownerNames[i] == n) return true;
        return false;
    }
}
