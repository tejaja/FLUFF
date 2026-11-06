using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2–4：歯車バンパー（2026-11-06 お試し。てじゃ「プレイヤーは歯車を活用してない」→ ふわふわへの当たり方に歯車の性質を出す）
// ・たて軸で回る歯車。ふわふわが当たると、当たった所の歯の動く向き・速さではじかれる（FuwaBall.HitGear）
// ・かみ合った歯車は逆回り。かみ合う所の歯の速さは同じ（本物どおり）。速さを変えるのは同じ軸の大小（2段歯車）
// ・角度 = ratio × u + phase（度、2D 反時計回り）、u = baseDegPerSec × サーバー時刻。Unity の Y 回転はマイナス
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGearBumper : UdonSharpBehaviour
{
    public float ratio = 1f, phase = 0f;
    [Tooltip("ratio=1 の歯車の回る速さ（度/秒、2D 反時計回り）")]
    public float baseDegPerSec = 30f;
    [Tooltip("歯の速さの何倍ではじくか")]
    public float boost = 1.4f;
    [Tooltip("当たった面から離れる最低の速さ (m/s)")]
    public float minPush = 1.2f;
    [Tooltip("当たる前の勢いをどれだけ残すか")]
    [Range(0f, 1f)] public float keep = 0.25f;

    private Quaternion _base;
    private bool _init;

    private void Start() { _base = transform.localRotation; _init = true; }

    private float AngleNow()
    {
        double t = Networking.GetServerTimeInSeconds();
        return (float)((t * baseDegPerSec * ratio + phase) % 360.0);
    }

    private void Update()
    {
        if (!_init) return;
        transform.localRotation = _base * Quaternion.Euler(0f, -AngleNow(), 0f);
    }

    // p（ワールド）の所の、歯車の表面の速さ（ワールド）
    public Vector3 SurfaceVelocity(Vector3 p)
    {
        Vector3 axis = transform.up;
        Vector3 r = p - transform.position;
        r -= axis * Vector3.Dot(r, axis);
        float dt = 0.02f;
        float d = -baseDegPerSec * ratio * dt;   // Unity の回る量（度）
        Vector3 r2 = Quaternion.AngleAxis(d, axis) * r;
        return (r2 - r) / dt;
    }
}
