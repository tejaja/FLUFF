using UdonSharp;
using UnityEngine;

// テスト場用（2026-10-17、公開前に消す）：押すと自分のふわふわを spot に出す。れんしゅうの「ためし撃ちの場所」と同じで、
// 浮いて待ち、撃つと動く。落ちて弾けたらまた spot に戻る（タイムなし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaTestBallSpot : UdonSharpBehaviour
{
    public FuwaBall ball;
    public Transform spot;
    public int stage = 2;

    public override void Interact()
    {
        if (ball == null || spot == null) return;
        ball.SetStage(stage);
        ball.GlideToPracticeSpot(spot);
    }
}
