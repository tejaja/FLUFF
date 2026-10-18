using UdonSharp;
using UnityEngine;

// ふわふわが当たると「ぽよん」と跳ね返る物（キノコの傘の足場など）。当たってもアウトにならない。
// ふわふわはゆっくりなので物理の反発（bounceThreshold 2m/s 未満は跳ねない）に任せず、FuwaBall側で速度を直接決める。
// 跳ねる向きは alwaysUp なら必ず真上（救済用、てじゃ希望）。OFFなら、この物体を楕円体とみなした表面の向き。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaBounce : UdonSharpBehaviour
{
    [Tooltip("跳ね返る速さ（表面から離れる向きの最低速度 m/s）")]
    public float bounceSpeed = 3.2f;
    [Tooltip("当たる前の横の勢いをどれだけ残すか")]
    [Range(0f, 1f)] public float keepTangent = 0.8f;
    [Tooltip("ONなら当たった場所に関係なく必ず真上に跳ねる（傘の端で横に飛ばされないように）")]
    public bool alwaysUp = true;
    [Tooltip("ONなら当たった面の向きで跳ね返る（岩の柱・壁など形がでこぼこな物用）。alwaysUp より優先")]
    public bool useContactNormal = false;

    [Tooltip("ここを中心に、落ちた地点から外側へ斜めに跳ねる（空なら使わない）。2026-10-17 きのこのたにのサルノコシカケのらせん階段：" +
             "真上に跳ねると上の段へ順に乗せて楽できたので（てじゃ案）。アウトにはしない（下から当たってアウトは理不尽）。" +
             "サルノコシカケは幹にくっついている根元、赤いキノコは傘の真ん中を基準に")]
    public Transform outwardCenter;
    [Tooltip("外へ倒す角度（度）。0=真上、90=真横")]
    public float outwardTilt = 40f;
    [Tooltip("基準からこの距離(m)で outwardTilt いっぱいに倒す（近いほど真上に近い）。0 なら距離に関係なく outwardTilt")]
    public float outwardRadius = 0f;

    // p の位置での表面の向き（外向き）
    public Vector3 GetNormal(Vector3 p)
    {
        if (outwardCenter != null && p.y >= transform.position.y)
        {
            Vector3 r = p - outwardCenter.position; r.y = 0f;
            if (r.sqrMagnitude > 0.0001f)
            {
                float d = r.magnitude; r /= d;
                float a = outwardTilt * Mathf.Deg2Rad * (outwardRadius > 0f ? Mathf.Clamp01(d / outwardRadius) : 1f);
                return (Vector3.up * Mathf.Cos(a) + r * Mathf.Sin(a)).normalized;
            }
        }
        // 下から傘の裏に当たった時だけは下へ（上に飛ばすと傘にめり込むので）
        if (alwaysUp) return p.y >= transform.position.y ? Vector3.up : Vector3.down;
        Vector3 lp = transform.InverseTransformPoint(p);
        Vector3 s = transform.lossyScale;
        Vector3 n = new Vector3(lp.x / Mathf.Max(0.001f, s.x), lp.y / Mathf.Max(0.001f, s.y), lp.z / Mathf.Max(0.001f, s.z));
        if (n.sqrMagnitude < 0.000001f) return Vector3.up;
        return (transform.rotation * n).normalized;
    }
}
