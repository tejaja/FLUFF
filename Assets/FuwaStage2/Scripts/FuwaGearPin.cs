using UdonSharp;
using UnityEngine;

// 2–4 の段つき歯車の上に立てた太いピン（2026-11-17 てじゃ「段つき歯車がただの段差になってる」→ 案C「ピンはありだな、太めにして人も通過できないように」）。
// 歯車（pivot）といっしょに回る柱。人は通れない（ふつうの当たり）。ふわふわが当たると、当たった所の動く向き・速さではじかれる（FuwaBall.HitGearPin。アウトにはならない）。
// 自分では動かない（回すのは FuwaClockwork）。毎フレームの処理なし
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGearPin : UdonSharpBehaviour
{
    [Tooltip("回る歯車（軸＝この transform の up）")]
    public Transform pivot;
    [Tooltip("歯車の回る速さ（度/秒、2D 反時計回り＝Unity では Y マイナス）。ビルダーが ratio×FuwaClockwork.speed を入れる")]
    public float degPerSec = 10f;
    [Tooltip("動く速さの何倍ではじくか")]
    public float boost = 1.6f;
    [Tooltip("当たった面から離れる最低の速さ (m/s)")]
    public float minPush = 1.6f;
    [Tooltip("当たる前の勢いをどれだけ残すか")]
    [Range(0f, 1f)] public float keep = 0.25f;

    // p（ワールド）の所の、ピンの表面の速さ（ワールド）
    public Vector3 SurfaceVelocity(Vector3 p)
    {
        Transform t = pivot != null ? pivot : transform;
        Vector3 axis = t.up;
        Vector3 r = p - t.position;
        r -= axis * Vector3.Dot(r, axis);
        float dt = 0.02f;
        Vector3 r2 = Quaternion.AngleAxis(-degPerSec * dt, axis) * r;
        return (r2 - r) / dt;
    }
}
