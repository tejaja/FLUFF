using UdonSharp;
using UnityEngine;

// みんなでひろばの「重力のたね」（コース3 やみのもり を誰かがクリアすると出てくる）。
// 持って動かせる紫の玉（手を離すとその場に浮く、VRCObjectSync）。まわり radius の中のふわふわを玉の方へ引き寄せる（小さな星のまわりを回るように）。
// それぞれの画面で同じように AddWind（全員で同じ風）。中では重さを少し打ち消して、ふわふわが玉のまわりに浮いていられるように
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGravitySeed : UdonSharpBehaviour
{
    public FuwaChillSpawner spawner;
    public float radius = 2.6f;
    [Tooltip("引き寄せる強さ（加速度）")]
    public float pull = 4f;
    [Tooltip("中で重さを打ち消す割合（1で浮く）")]
    [Range(0f, 1f)] public float antiFall = 0.85f;
    [Tooltip("玉に近い所で回す強さ（くっつかずに回るように）")]
    public float swirl = 1.2f;
    [Tooltip("ふわふわが回る距離(m)")]
    public float orbit = 0.9f;
    [Tooltip("玉に向かう・離れる動きのブレーキ")]
    public float radialDamp = 1.2f;
    public Transform spinner;

    private void Update()
    {
        if (spinner != null) spinner.Rotate(20f * Time.deltaTime, 90f * Time.deltaTime, 0f, Space.Self);
    }

    private void FixedUpdate()
    {
        if (spawner == null || spawner.balls == null) return;
        Vector3 c = transform.position;
        FuwaSharedBall[] balls = spawner.balls;
        for (int i = 0; i < balls.Length; i++)
        {
            FuwaSharedBall b = balls[i];
            if (b == null || !b.IsFlying()) continue;
            Vector3 rel = c - b.transform.position;
            float d = rel.magnitude;
            if (d > radius || d < 0.001f) continue;
            Vector3 dir = rel / d;   // 玉の方へ
            // orbit の距離へばねで寄せる（中まで吸い込まない）＋その方向の動きにブレーキ＋横に回す
            float k = 1f - d / radius;
            Vector3 acc = dir * pull * Mathf.Clamp((d - orbit) / Mathf.Max(0.1f, radius - orbit), -1.5f, 1f);
            Rigidbody rb = b.GetComponent<Rigidbody>();
            if (rb != null) acc -= dir * Vector3.Dot(rb.velocity, dir) * radialDamp;
            acc += Vector3.up * b.fallAcceleration * antiFall;
            Vector3 tangent = Vector3.Cross(Vector3.up, dir);
            acc += tangent * swirl * (0.3f + 0.7f * k);
            b.AddWind(acc * Mathf.Sqrt(Mathf.Max(0.05f, b.shotScale)));
        }
    }
}
