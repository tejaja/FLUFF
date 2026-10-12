using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// チュートリアルの練習場の区切りの透明な壁（2026-10-12 てじゃ「うっすらみえてて、あたったらふわふわは割れる、自分はリスポンなし」）
// ・自分のふわふわが当たったら、ふわふわは弾けて練習場のためし撃ちの場所に戻る（プレイヤーは動かさない）
// ・壁は消さない（試したら「ふわふわで壁が消える」のがいまいちだった。てじゃ）。当たった所が光るのはシェーダー側
// ・当たった瞬間（Enter）だけ見ていたら、なかなか弾けない時があった。ふわふわが割れられない状態の時に当たると、
//   そのまま壁にこすれ続けても二度と試さなかったので、触れている間はずっと試す（Stay）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPopWall : UdonSharpBehaviour
{
    public FuwaBall ball;

    private void OnCollisionEnter(Collision collision) { TryPop(collision); }
    private void OnCollisionStay(Collision collision) { TryPop(collision); }

    private void TryPop(Collision collision)
    {
        if (ball == null || collision == null || collision.gameObject != ball.gameObject) return;
        ball.FailStay();   // 割れられない状態の時は FuwaBall.Fail 側で何もしない
    }
}
