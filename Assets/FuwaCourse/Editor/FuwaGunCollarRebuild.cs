using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// エアガンのラッパの首まわり（ラッパの裏の広がった輪）を作り直す。元は Gun_Accent_Source、書き出し先は Gun_Accent。何度実行しても同じ結果。
// ・前から見た時：輪の外側からラッパの首へ、なめらかな曲面でつなげて埋める（すき間から中が見えない）
// ・後ろから見た時：今まで通り、輪の内側に暗い通気口（Gun_Vent）と支えの棒が見える
// 古い輪の面（外側・後ろのふち・内側の壁）は消して、1枚の回転体に置き換える。ラッパ本体・支えの棒・ほかの黄色い部品はそのまま
public static class FuwaGunCollarRebuild
{
    const string SrcPath = "Assets/FuwaCourse/Meshes/Gun_Accent_Source.asset";
    const string DstPath = "Assets/FuwaCourse/Meshes/Gun_Accent.asset";
    const float CY = 0.055f;   // 銃身の軸の高さ（軸は z 向き）

    // 断面（r, z）：内側の壁（通気口のすぐ前）→ 後ろのふち → 外側のふくらみ → 前へ向かってラッパの首に溶け込む
    static readonly Vector2[] Profile =
    {
        new Vector2(0.0200f, 0.1400f), new Vector2(0.0208f, 0.1382f), new Vector2(0.0217f, 0.1366f), new Vector2(0.0228f, 0.1352f),
        new Vector2(0.0240f, 0.1346f), new Vector2(0.0249f, 0.1355f), new Vector2(0.0253f, 0.1375f), new Vector2(0.0252f, 0.1400f),
        new Vector2(0.0247f, 0.1428f), new Vector2(0.0239f, 0.1460f), new Vector2(0.0231f, 0.1500f), new Vector2(0.0226f, 0.1545f),
        new Vector2(0.0225f, 0.1585f), new Vector2(0.0229f, 0.1615f), new Vector2(0.0236f, 0.1635f), new Vector2(0.0234f, 0.1655f),
    };

    [MenuItem("FuwaCourse/エアガンのラッパの首を作り直す")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        var src = AssetDatabase.LoadAssetAtPath<Mesh>(SrcPath);
        var v = src.vertices; var n = src.normals; var t = src.GetTriangles(0);
        float R(Vector3 p) { return new Vector2(p.x, p.y - CY).magnitude; }
        float Ang(Vector3 p) { return Mathf.Atan2(p.y - CY, p.x) * Mathf.Rad2Deg; }
        bool Strut(Vector3 p)
        {
            float a = Mathf.Abs(Mathf.DeltaAngle(Ang(p), 45f)) % 90f; a = Mathf.Min(a, 90f - a);   // ±45°・±135° の近く
            return a < 6f && p.z > 0.137f && p.z < 0.1465f && R(p) > 0.0145f && R(p) < 0.0212f;
        }
        // 古い輪の頂点：z 0.134〜0.1485、半径 0.0198 以上（ラッパの首の外側は 0.0185 以下なので入らない）、支えの棒は除く
        bool Collar(Vector3 p) { return p.z > 0.1338f && p.z < 0.1486f && R(p) > 0.0198f && R(p) < 0.0262f && !Strut(p); }
        var keepT = new List<int>(); int removed = 0;
        for (int i = 0; i < t.Length; i += 3)
        {
            if (Collar(v[t[i]]) && Collar(v[t[i + 1]]) && Collar(v[t[i + 2]])) { removed++; continue; }
            keepT.Add(t[i]); keepT.Add(t[i + 1]); keepT.Add(t[i + 2]);
        }
        // 使っている頂点だけに詰める
        var map = new Dictionary<int, int>(); var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new List<int>();
        foreach (var i in keepT) { if (!map.TryGetValue(i, out int j)) { j = V.Count; map[i] = j; V.Add(v[i]); N.Add(n[i]); } T.Add(j); }

        // 新しい回転体（法線は断面のとなりの辺から、なめらかに）
        int S = 72, P = Profile.Length;
        var pn = new Vector2[P];
        for (int k = 0; k < P; k++)
        {
            Vector2 a = Profile[Mathf.Max(0, k - 1)], b = Profile[Mathf.Min(P - 1, k + 1)];
            var tan = (b - a).normalized;               // (dr, dz)
            pn[k] = new Vector2(tan.y, -tan.x);          // 断面上で外向き（r＋側）になる向き
        }
        // 外側のふくらみの点で外向きか確かめる
        if (pn[6].x < 0) for (int k = 0; k < P; k++) pn[k] = -pn[k];
        int b0 = V.Count;
        for (int s = 0; s <= S; s++)
        {
            float a = s / (float)S * Mathf.PI * 2; float c = Mathf.Cos(a), sn = Mathf.Sin(a);
            for (int k = 0; k < P; k++)
            {
                V.Add(new Vector3(c * Profile[k].x, CY + sn * Profile[k].x, Profile[k].y));
                N.Add(new Vector3(c * pn[k].x, sn * pn[k].x, pn[k].y).normalized);
            }
        }
        for (int s = 0; s < S; s++)
            for (int k = 0; k < P - 1; k++)
            {
                int a0 = b0 + s * P + k, a1 = a0 + 1, c0 = a0 + P, c1 = c0 + 1;
                // 面の向きを法線に合わせる
                var fn = Vector3.Cross(V[a1] - V[a0], V[c0] - V[a0]);
                if (Vector3.Dot(fn, N[a0] + N[a1] + N[c0]) > 0) T.AddRange(new[] { a0, a1, c0, a1, c1, c0 });
                else T.AddRange(new[] { a0, c0, a1, a1, c0, c1 });
            }

        var dst = AssetDatabase.LoadAssetAtPath<Mesh>(DstPath);
        dst.Clear();
        dst.indexFormat = V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        dst.SetVertices(V); dst.SetNormals(N); dst.SetTriangles(T, 0); dst.RecalculateBounds();
        EditorUtility.SetDirty(dst); AssetDatabase.SaveAssets();
        return "removed " + removed + " tris, verts " + V.Count;
    }
}
