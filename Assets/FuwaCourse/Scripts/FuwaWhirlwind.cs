using UdonSharp;
using UnityEngine;

// つむじ風：筒の中のふわふわを、ぐるぐる回しながら上へ巻き上げる。てっぺんまで来たら前へ送り出す。
// このオブジェクトの位置＝筒の底の中心、+Y＝上、+Z＝てっぺんで送り出す向き。判定は子の Collider（トリガー）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWhirlwind : UdonSharpBehaviour
{
    [Tooltip("筒の半径(m)")]
    public float radius = 1.6f;
    [Tooltip("巻き上げる高さ(m)。ここより上は送り出し")]
    public float height = 6f;
    [Tooltip("上へ持ち上げる強さ（加速度）。上へ行くほど少し弱まる")]
    public float lift = 4.2f;
    [Range(0f, 1f)] public float liftFade = 0.35f;
    [Tooltip("ぐるぐる回す強さ（加速度）")]
    public float swirl = 2.2f;
    [Tooltip("真ん中へ吸い寄せる強さ（加速度）。外ほど強い")]
    public float inward = 2.5f;
    [Tooltip("てっぺんで +Z へ送り出す強さ（加速度）")]
    public float exitPush = 3.5f;
    [Tooltip("送り出しが始まる高さ（height に対する割合）")]
    [Range(0f, 1f)] public float exitFrom = 0.85f;
    [Tooltip("回る向き（1=上から見て反時計回り、-1=時計回り）")]
    public float spinDir = 1f;

    public void OnTriggerStay(Collider other)
    {
        if (other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;
        Vector3 local = transform.InverseTransformPoint(other.transform.position);
        float h = Mathf.Clamp01(local.y / Mathf.Max(0.01f, height));
        Vector3 flat = new Vector3(local.x, 0f, local.z);
        float r = flat.magnitude;
        Vector3 acc = Vector3.zero;
        // 上へ（てっぺんに近いほど弱め、送り出し区間では浮かせる程度）
        float up = lift * (1f - liftFade * h);
        if (h > exitFrom) up = Mathf.Lerp(up, 1.6f, (h - exitFrom) / (1f - exitFrom));
        acc += Vector3.up * up;
        if (r > 0.001f)
        {
            Vector3 radial = flat / r;
            Vector3 tangent = Vector3.Cross(Vector3.up, radial) * spinDir;
            float outer = Mathf.Clamp01(r / Mathf.Max(0.01f, radius));
            // 送り出し区間では回転を止めて、まっすぐ前へ出す（横に飛ばされないように）
            float exitness = h > exitFrom ? Mathf.Clamp01((h - exitFrom) / (1f - exitFrom) * 2f) : 0f;
            acc += tangent * swirl * (0.4f + 0.6f * outer) * (1f - exitness);
            acc -= radial * inward * outer;
        }
        // てっぺんで前へ送り出す
        if (h > exitFrom) acc += Vector3.forward * exitPush * Mathf.Clamp01((h - exitFrom) / (1f - exitFrom) * 2f);
        ball.AddWind(transform.TransformDirection(acc));
    }
}
