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

    // 断面（r, z）：内側の壁（通気口のすぐ前）→ 後ろのふち → 外側のふくらみ → 前へ向かってラッパの外側の面へなめらかにつながる。
    // 最後の点はラッパの外側の面の輪（z=0.17198）とぴったり同じ位置・同じ向きにして、つなぎ目の線が出ないようにする
    static readonly Vector2[] Back =
    {
        new Vector2(0.0200f, 0.1400f), new Vector2(0.0208f, 0.1382f), new Vector2(0.0217f, 0.1366f), new Vector2(0.0228f, 0.1352f),
        new Vector2(0.0240f, 0.1346f), new Vector2(0.0249f, 0.1355f), new Vector2(0.0253f, 0.1375f), new Vector2(0.0252f, 0.1400f),
        new Vector2(0.0247f, 0.1428f), new Vector2(0.0239f, 0.1460f),
    };
    const float SeamZ = 0.17198f, SeamR = 0.02986f;          // ラッパの外側の面の輪（元のメッシュの値）
    static readonly Vector2 SeamN = new Vector2(0.7129f, -0.7012f);   // その輪の法線（r, z）
    const int Segs = 36;                                      // ラッパと同じ分割（0°から10°ごと）

    static Vector2[] BuildProfile(out Vector2[] normals)
    {
        var pts = new List<Vector2>(Back);
        // 最後の点から継ぎ目の輪まで、エルミート曲線でなめらかにつなぐ（向きも合わせる）
        Vector2 A = Back[Back.Length - 1], B = new Vector2(SeamR, SeamZ);
        Vector2 tA = new Vector2(-0.2f, 1f).normalized, tB = new Vector2(-SeamN.y, SeamN.x).normalized;   // 法線を90°回すと接線
        if (tB.y < 0) tB = -tB;
        float L = (B - A).magnitude * 1.1f;
        int K = 12;
        for (int i = 1; i <= K; i++)
        {
            float t = i / (float)K, t2 = t * t, t3 = t2 * t;
            pts.Add((2 * t3 - 3 * t2 + 1) * A + (t3 - 2 * t2 + t) * L * tA + (-2 * t3 + 3 * t2) * B + (t3 - t2) * L * tB);
        }
        var P = pts.ToArray(); int n = P.Length;
        normals = new Vector2[n];
        for (int k = 0; k < n; k++)
        {
            Vector2 a = P[Mathf.Max(0, k - 1)], b = P[Mathf.Min(n - 1, k + 1)];
            var tan = (b - a).normalized; normals[k] = new Vector2(tan.y, -tan.x);
        }
        if (normals[6].x < 0) for (int k = 0; k < n; k++) normals[k] = -normals[k];
        normals[n - 1] = SeamN;
        return P;
    }

    // 断面（r, z）を軸のまわりに回した面を足す。面の向きは法線に合わせる
    static void Revolve(List<Vector3> V, List<Vector3> N, List<int> T, Vector2[] prof, Vector2[] pn, int S)
    {
        int P = prof.Length, b0 = V.Count;
        for (int s = 0; s <= S; s++)
        {
            float a = s / (float)S * Mathf.PI * 2; float c = Mathf.Cos(a), sn = Mathf.Sin(a);
            for (int k = 0; k < P; k++)
            {
                V.Add(new Vector3(c * prof[k].x, CY + sn * prof[k].x, prof[k].y));
                N.Add(new Vector3(c * pn[k].x, sn * pn[k].x, pn[k].y).normalized);
            }
        }
        for (int s = 0; s < S; s++)
            for (int k = 0; k < P - 1; k++)
            {
                int a0 = b0 + s * P + k, a1 = a0 + 1, c0 = a0 + P, c1 = c0 + 1;
                var fn = Vector3.Cross(V[a1] - V[a0], V[c0] - V[a0]);
                if (Vector3.Dot(fn, N[a0] + N[a1] + N[c0]) > 0) T.AddRange(new[] { a0, a1, c0, a1, c1, c0 });
                else T.AddRange(new[] { a0, c0, a1, a1, c0, c1 });
            }
    }

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
        // ラッパの外側の面（首〜継ぎ目の輪まで）：外向きの法線で、半径 0.017 以上
        bool HornOuter(int i)
        {
            var p = v[i]; if (p.z < 0.1395f || p.z > SeamZ + 0.00005f || R(p) < 0.0170f || Strut(p)) return false;
            var rd = new Vector2(p.x, p.y - CY).normalized; return Vector2.Dot(new Vector2(n[i].x, n[i].y), rd) > 0.05f;
        }
        bool Old(int i) { return Collar(v[i]) || HornOuter(i) || Strut(v[i]); }   // 支えの棒も作り直す（角を丸める）
        var keepT = new List<int>(); int removed = 0;
        for (int i = 0; i < t.Length; i += 3)
        {
            if (Old(t[i]) && Old(t[i + 1]) && Old(t[i + 2])) { removed++; continue; }
            keepT.Add(t[i]); keepT.Add(t[i + 1]); keepT.Add(t[i + 2]);
        }
        // 使っている頂点だけに詰める
        var map = new Dictionary<int, int>(); var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new List<int>();
        foreach (var i in keepT) { if (!map.TryGetValue(i, out int j)) { j = V.Count; map[i] = j; V.Add(v[i]); N.Add(n[i]); } T.Add(j); }

        // 新しい回転体（ラッパと同じく0°から10°ごと。継ぎ目の輪はラッパの頂点とぴったり同じ位置）
        var Profile = BuildProfile(out var pn);
        Revolve(V, N, T, Profile, pn, Segs);
        // 支えの棒の根元：銃身に巻いた黄色い輪（棒が刺さる所に層がある感じ）。後ろから通気口の奥に見える
        {
            var sp = new[] { new Vector2(0.0140f, 0.1402f), new Vector2(0.0140f, 0.1364f), new Vector2(0.0144f, 0.1356f), new Vector2(0.0152f, 0.1352f),
                             new Vector2(0.0161f, 0.1354f), new Vector2(0.0167f, 0.1361f), new Vector2(0.0168f, 0.1402f) };
            var sn = new Vector2[sp.Length];
            for (int k = 0; k < sp.Length; k++)
            {
                Vector2 a = sp[Mathf.Max(0, k - 1)], b = sp[Mathf.Min(sp.Length - 1, k + 1)];
                var tan = (b - a).normalized; sn[k] = new Vector2(tan.y, -tan.x);
            }
            if (sn[5].x < 0) for (int k = 0; k < sn.Length; k++) sn[k] = -sn[k];   // 外側の面が外向きになるように
            Revolve(V, N, T, sp, sn, Segs);
        }
        // 支えの棒（4本、±45°・±135°）：角を少し丸めた（ベベル）細い板。根元は黄色い輪の中、先は輪の内側の壁の中に埋める
        {
            const float w = 0.0021f, h = 0.0085f, zc = 0.14175f, rad = 0.00025f, r0 = 0.0146f, r1 = 0.0213f; const int cs = 2;   // 角だけ少し落とす（丸い棒にはしない）。法線はなめらか
            // 角を丸めた長方形の断面（接線方向 x、軸方向 y）
            var sec = new List<Vector2>(); var secN = new List<Vector2>();
            var corners = new[] { new Vector2(w / 2 - rad, h / 2 - rad), new Vector2(-(w / 2 - rad), h / 2 - rad), new Vector2(-(w / 2 - rad), -(h / 2 - rad)), new Vector2(w / 2 - rad, -(h / 2 - rad)) };
            for (int q = 0; q < 4; q++)
                for (int k = 0; k <= cs; k++)
                {
                    float a = (q * 90f + k * 90f / cs) * Mathf.Deg2Rad; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    sec.Add(corners[q] + d * rad); secN.Add(d);
                }
            int M = sec.Count;
            foreach (var deg in new[] { 45f, 135f, 225f, 315f })
            {
                float a = deg * Mathf.Deg2Rad; var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0); var tang = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0);
                int b = V.Count;
                foreach (var rr in new[] { r0, r1 })
                    for (int k = 0; k < M; k++)
                    {
                        V.Add(new Vector3(0, CY, zc) + radial * rr + tang * sec[k].x + Vector3.forward * sec[k].y);
                        N.Add((tang * secN[k].x + Vector3.forward * secN[k].y).normalized);
                    }
                for (int k = 0; k < M; k++)
                {
                    int a0 = b + k, a1 = b + (k + 1) % M, c0 = a0 + M, c1 = a1 + M;
                    var fn = Vector3.Cross(V[a1] - V[a0], V[c0] - V[a0]);
                    if (Vector3.Dot(fn, N[a0] + N[a1]) > 0) T.AddRange(new[] { a0, a1, c0, a1, c1, c0 });
                    else T.AddRange(new[] { a0, c0, a1, a1, c0, c1 });
                }
            }
        }

        var dst = AssetDatabase.LoadAssetAtPath<Mesh>(DstPath);
        dst.Clear();
        dst.indexFormat = V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        dst.SetVertices(V); dst.SetNormals(N); dst.SetTriangles(T, 0); dst.RecalculateBounds();
        EditorUtility.SetDirty(dst); AssetDatabase.SaveAssets();
        return "removed " + removed + " tris, verts " + V.Count;
    }
}
