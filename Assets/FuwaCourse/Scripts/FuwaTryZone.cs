using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// れんしゅうの説明エリア：この範囲に入ると、自分のふわふわが「ためし撃ちの場所」（spot）に移ってくる。
// spot が空の時は、コースのスタート（courseStart、キノコの中）へ戻す（説明エリアからコースへ進んだ所に置く）。
// 自分の画面だけ・同期なし。当たり判定はこの物体のトリガー（Default レイヤーにしないとプレイヤーのイベントが来ない）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaTryZone : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("ふわふわが浮いて待つ所。空ならコースのスタートへ")]
    public Transform spot;
    [Tooltip("spot が空の時に戻すコースのスタート（BallStart）")]
    public Transform courseStart;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || ball == null) return;
        if (spot != null)
        {
            if (ball.GetRespawnPoint() == spot && !ball.IsHidden()) return;   // もうここにいる
            ball.GlideToPracticeSpot(spot);
        }
        else if (courseStart != null)
        {
            if (ball.GetRespawnPoint() == courseStart) return;
            ball.StartCourse(courseStart, false);
        }
    }
}
