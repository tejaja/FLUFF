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

    // p の位置での表面の向き（外向き）
    public Vector3 GetNormal(Vector3 p)
    {
        // 下から傘の裏に当たった時だけは下へ（上に飛ばすと傘にめり込むので）
        if (alwaysUp) return p.y >= transform.position.y ? Vector3.up : Vector3.down;
        Vector3 lp = transform.InverseTransformPoint(p);
        Vector3 s = transform.lossyScale;
        Vector3 n = new Vector3(lp.x / Mathf.Max(0.001f, s.x), lp.y / Mathf.Max(0.001f, s.y), lp.z / Mathf.Max(0.001f, s.z));
        if (n.sqrMagnitude < 0.000001f) return Vector3.up;
        return (transform.rotation * n).normalized;
    }
}
