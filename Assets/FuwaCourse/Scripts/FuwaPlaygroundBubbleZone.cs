using UdonSharp;
using UnityEngine;

// 泡が出る貝（FuwaPlaygroundBubbleClam）の泡の柱。このオブジェクトの位置＝柱の底（貝の口）、+Y＝上、+Z＝てっぺんで送り出す向き。判定はこの物体のトリガー
// 貝がオンの間、中の同期ふわふわ（FuwaSharedBall）をゆっくり上へ運ぶ（全員の画面で同じ風＝AddWind）。てっぺんで +Z へ送り出す
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPlaygroundBubbleZone : UdonSharpBehaviour
{
    public FuwaPlaygroundBubbleClam clam;
    [Tooltip("上がる速さ(m/s)")]
    public float riseSpeed = 1.5f;
    [Tooltip("上がる速さに寄せる強さ（1/s）")]
    public float riseResponse = 3f;
    [Tooltip("柱の真ん中へ寄せる強さ（1/s²、1m あたり）と横の動きの止まりやすさ")]
    public float centerPull = 5f, sideDamping = 2.5f;
    [Tooltip("柱の高さ(m)。この 85% より上で送り出す")]
    public float height = 4.2f;
    [Tooltip("てっぺんで +Z へ送り出す強さ（加速度）")]
    public float exitPush = 4f;

    private void OnTriggerStay(Collider other)
    {
        if (other == null || clam == null || !clam.IsOn()) return;
        FuwaSharedBall b = other.GetComponent<FuwaSharedBall>();
        if (b == null || !b.IsFlying()) return;
        Rigidbody rb = other.attachedRigidbody; if (rb == null) return;
        Vector3 v = rb.velocity;
        Vector3 rel = rb.position - transform.position;
        float y = Vector3.Dot(rel, transform.up);
        Vector3 side = rel - transform.up * y;
        Vector3 acc = transform.up * (b.fallAcceleration + (riseSpeed - Vector3.Dot(v, transform.up)) * riseResponse);
        Vector3 vs = v - transform.up * Vector3.Dot(v, transform.up);
        acc += -side * centerPull - vs * sideDamping;
        if (y > height * 0.85f) acc += transform.forward * exitPush;
        b.AddWind(acc);
    }
}
