using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// エアガンのトリガーとトリガーガードを作り直す（2026-10-11 てじゃ「トリガーがただの伸ばした球なのが気になってきた」）
// ・トリガー（黄色、Gun_Accent に入っている）：横から見て前へ反った板。指がかかる前側がくぼみ、先は少し前へ反り返る。根元は銃身の中
//   → FuwaGunCollarRebuild.Run（Gun_Accent_Source → Gun_Accent）の中で、古い球を消してこれを足す
// ・ガード（茶色、Gun_Grip に入っている）：前は銃身の中から、トリガーの下をくぐって、後ろはグリップの前の面に刺さって終わる（切りっぱなしの端を見せない）
//   → Gun_Grip_Source（Blender から書き出した時のまま）から古いガードを消して、これを足して Gun_Grip に書き出す
// 座標は銃の Visual の中（x 横、y 上、z 前）。何度実行しても同じ結果
public static class FuwaGunTriggerRebuild
{
    const string GripSrcPath = "Assets/FuwaCourse/Meshes/Gun_Grip_Source.asset";
    const string GripDstPath = "Assets/FuwaCourse/Meshes/Gun_Grip.asset";

    [MenuItem("FuwaCourse/エアガンのトリガーとガードを作り直す")]
    public static void RunMenu()
    {
        Debug.Log(FuwaGunCollarRebuild.Run());   // トリガー（黄色の部品）はこの中で作る
        Debug.Log(RunGrip());
    }

    // ---- 部品の島（つながった三角形のまとまり）ごとに、全部の頂点が箱の中にある島の三角形を消す ----
    public static List<int> RemoveIslandsInBox(Vector3[] v, List<int> tris, Bounds box, out int removed)
    {
        // 位置が同じ頂点（法線の切れ目で分かれた頂点）も同じ島にする
        var parent = Enumerable.Range(0, v.Length).ToArray();
        int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }
        var byPos = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < v.Length; i++)
        {
            var k = Vector3Int.RoundToInt(v[i] * 1e6f);
            if (byPos.TryGetValue(k, out int j)) Union(i, j); else byPos[k] = i;
        }
        for (int i = 0; i < tris.Count; i += 3) { Union(tris[i], tris[i + 1]); Union(tris[i + 1], tris[i + 2]); }
        var outside = new HashSet<int>();
        foreach (var i in tris) if (!box.Contains(v[i])) outside.Add(Find(i));
        var keep = new List<int>(); removed = 0;
        for (int i = 0; i < tris.Count; i += 3)
        {
            if (!outside.Contains(Find(tris[i]))) { removed++; continue; }
            keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
        }
        return keep;
    }

    // 古いトリガー（つぶした球）がいる箱
    public static readonly Bounds OldTriggerBox = new Bounds(new Vector3(0f, 0.026f, 0.031f), new Vector3(0.010f, 0.022f, 0.016f));
    // 古いガード（トーラスの半分）がいる箱
    static readonly Bounds OldGuardBox = new Bounds(new Vector3(0f, 0.0205f, 0.030f), new Vector3(0.008f, 0.023f, 0.052f));

    // ---- 中心線に沿って断面を動かした筒（角の丸い四角っぽい断面）。両はしは閉じる ----
    // center[k]：中心線、side：断面の横（x）の向き（全部 x 軸）、hw/hd：断面の横・奥行きの半分、tipDome：最後の端を丸く閉じる長さ
    static void Sweep(List<Vector3> V, List<Vector3> N, List<int> T, Vector3[] c, float[] hw, float[] hd, float pw, float tipDome, int segs, int domeRings, float[] flare = null)
    {
        int n = c.Length; var tm = new List<Vector3>(); var tt = new List<int>();
        var tan = new Vector3[n]; var nor = new Vector3[n];
        for (int k = 0; k < n; k++)
        {
            tan[k] = (c[Mathf.Min(n - 1, k + 1)] - c[Mathf.Max(0, k - 1)]).normalized;
            nor[k] = Vector3.Cross(Vector3.right, tan[k]).normalized;   // 断面の縦（中心線に直角、x に直角）
        }
        // 角の丸い四角（スーパー楕円）
        Vector2 Sec(int s, float w, float d)
        {
            float a = s / (float)segs * Mathf.PI * 2; float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
            return new Vector2(Mathf.Sign(ca) * Mathf.Pow(Mathf.Abs(ca), 2f / pw) * w, Mathf.Sign(sa) * Mathf.Pow(Mathf.Abs(sa), 2f / pw) * d);
        }
        var rings = new List<(Vector3 o, Vector3 nr, float w, float d)>();
        // flare：外側のふちはそのまま、内側（穴の側）へだけ太らせる。付け根の角を丸く埋めるのに使う
        for (int k = 0; k < n; k++) { float f = flare != null ? flare[k] : 0f; rings.Add((c[k] + nor[k] * f, nor[k], hw[k], hd[k] + f)); }
        // 先を丸く閉じる（中心線の先へ伸ばしながら断面を小さく）
        var last = rings[n - 1];
        for (int r = 1; r <= domeRings; r++)
        {
            float a = r / (float)(domeRings + 1) * Mathf.PI * 0.5f;
            rings.Add((last.o + tan[n - 1] * tipDome * Mathf.Sin(a), last.nr, last.w * Mathf.Cos(a), last.d * Mathf.Cos(a)));
        }
        foreach (var rg in rings)
            for (int s = 0; s < segs; s++) { var q = Sec(s, rg.w, rg.d); tm.Add(rg.o + Vector3.right * q.x + rg.nr * q.y); }
        int R = rings.Count;
        for (int k = 0; k < R - 1; k++)
            for (int s = 0; s < segs; s++)
            {
                int a0 = k * segs + s, a1 = k * segs + (s + 1) % segs, b0 = a0 + segs, b1 = a1 + segs;
                tt.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
            }
        int tip = tm.Count; tm.Add(last.o + tan[n - 1] * tipDome);
        int lr = (R - 1) * segs;
        for (int s = 0; s < segs; s++) tt.AddRange(new[] { lr + s, tip, lr + (s + 1) % segs });
        int root = tm.Count; tm.Add(c[0]);
        for (int s = 0; s < segs; s++) tt.AddRange(new[] { root, s, (s + 1) % segs });
        // 面の向き：外向きにそろえる（最初の輪の外側の点で確かめる）
        var tmp = new Mesh(); tmp.SetVertices(tm); tmp.SetTriangles(tt, 0); tmp.RecalculateNormals();
        var tn = tmp.normals;
        int mid = (n / 2) * segs;   // 真ん中の輪の s=0（+x 側）の法線は +x 向きのはず
        if (tn[mid].x < 0)
        {
            for (int i = 0; i < tt.Count; i += 3) { int x = tt[i + 1]; tt[i + 1] = tt[i + 2]; tt[i + 2] = x; }
            tmp.SetTriangles(tt, 0); tmp.RecalculateNormals(); tn = tmp.normals;
        }
        Object.DestroyImmediate(tmp);
        int b = V.Count; V.AddRange(tm); N.AddRange(tn); foreach (var i in tt) T.Add(b + i);
    }


    // トリガー：横から見るとグリップの前の面とガードの下の帯までつながった一枚の板（「ガードの下までつながっててほしい」）（2026-10-11 てじゃ「横からみるとグリップまで繋がった板」ナーフの写真）。
    // 前のふちは指がかかるようにくぼみ、前の下の角は丸い。後ろはグリップの中、上は銃身の中。
    // 板の厚みはふち（前・下）に向かって丸く落ちる（ふちの丸み半径 ≒ 厚みの半分）。表と裏はふちの頂点を共有して、ふちに線が出ないように
    public static void AddTrigger(List<Vector3> V, List<Vector3> N, List<int> T)
    {
        const float yb = 0.0080f, yt = 0.0310f, h0 = 0.0019f, rb = 0.0019f, rc = 0.0025f;
        // 前のふち（y → z）
        float[] fy = { 0.0310f, 0.0260f, 0.0205f, 0.0155f, 0.0110f, 0.0080f };
        float[] fz = { 0.0318f, 0.0302f, 0.0288f, 0.0293f, 0.0312f, 0.0322f };   // 下はガードの帯の中へ（前へ少し反ってつながる）
        float Zf(float y)
        {
            for (int i = 0; i < fy.Length - 1; i++)
                if (y >= fy[i + 1] - 1e-6f)
                {
                    float t = Mathf.InverseLerp(fy[i], fy[i + 1], y);
                    int i0 = Mathf.Max(0, i - 1), i3 = Mathf.Min(fy.Length - 1, i + 2);
                    float p0 = fz[i0], p1 = fz[i], p2 = fz[i + 1], p3 = fz[i3], t2 = t * t, t3 = t2 * t;
                    return 0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
                }
            return fz[fz.Length - 1];
        }
        float Zr(float y) { return 0.0139f + (y - 0.006f) * 0.21f - 0.0030f; }   // グリップの前の面より 3mm 奥
        // ふちまでの距離（前の下の角は半径 rc で丸める）
        float Dist(float y, float z)
        {
            float a = Zf(y) - z, b = y - yb;
            if (a < rc && b < rc) return rc - new Vector2(rc - a, rc - b).magnitude;
            return Mathf.Min(a, b);
        }
        // その高さでの前のふちの z（角の丸みを含む）
        float Edge(float y)
        {
            float b = y - yb; float zf = Zf(y);
            if (b >= rc) return zf;
            float u = rc - b; return zf - rc + Mathf.Sqrt(Mathf.Max(0f, rc * rc - u * u));
        }
        const int rows = 56, cols = 34;
        var pos = new Vector3[rows, cols]; var hh = new float[rows, cols];
        for (int r = 0; r < rows; r++)
        {
            float s = r / (float)(rows - 1);
            float y = yb + (yt - yb) * (1 - Mathf.Cos(s * Mathf.PI * 0.5f));   // 下の方を細かく
            float z0 = Zr(y), z1 = Edge(y);
            for (int c = 0; c < cols; c++)
            {
                float u = c / (float)(cols - 1); u = 1 - (1 - u) * (1 - u);    // 前のふちの近くを細かく
                float z = Mathf.Lerp(z0, z1, u);
                pos[r, c] = new Vector3(0, y, z);
                float d = r == 0 || c == cols - 1 ? 0f : Mathf.Max(0f, Dist(y, z));
                float q = Mathf.Clamp01(d / rb);
                hh[r, c] = h0 * Mathf.Sqrt(1 - (1 - q) * (1 - q));
            }
        }
        var tm = new List<Vector3>(); var tt = new List<int>();
        var idF = new int[rows, cols]; var idB = new int[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                idF[r, c] = tm.Count; tm.Add(pos[r, c] + Vector3.right * hh[r, c]);
                if (hh[r, c] <= 0f) idB[r, c] = idF[r, c];
                else { idB[r, c] = tm.Count; tm.Add(pos[r, c] - Vector3.right * hh[r, c]); }
            }
        for (int r = 0; r < rows - 1; r++)
            for (int c = 0; c < cols - 1; c++)
            {
                // 表（+x を向く）：y が上、z が前 → (r,c),(r+1,c),(r,c+1) は +x から見て…向きは後でそろえる
                tt.AddRange(new[] { idF[r, c], idF[r + 1, c], idF[r, c + 1], idF[r, c + 1], idF[r + 1, c], idF[r + 1, c + 1] });
                tt.AddRange(new[] { idB[r, c], idB[r, c + 1], idB[r + 1, c], idB[r, c + 1], idB[r + 1, c + 1], idB[r + 1, c] });
            }
        // 表の面が +x を向くように
        {
            var a = tm[idF[rows / 2, cols / 2]]; var b = tm[idF[rows / 2 + 1, cols / 2]]; var c = tm[idF[rows / 2, cols / 2 + 1]];
            if (Vector3.Cross(b - a, c - a).x < 0)
                for (int i = 0; i < tt.Count; i += 3) { int x = tt[i + 1]; tt[i + 1] = tt[i + 2]; tt[i + 2] = x; }
        }
        var tmp = new Mesh(); tmp.SetVertices(tm); tmp.SetTriangles(tt, 0); tmp.RecalculateNormals();
        var tn = tmp.normals; Object.DestroyImmediate(tmp);
        int b0 = V.Count; V.AddRange(tm); N.AddRange(tn); foreach (var i in tt) T.Add(b0 + i);
    }

    // ガード：平たい板（横から見ると太い帯、正面から見ると薄い）。前は銃身の中 → 下へ → トリガーの下をくぐって → 後ろはグリップの前の面の中へ
    // （2026-10-11 てじゃ「板状でグリップまで繋がってる形がいい」ナーフの写真）。銃身・グリップとの付け根の内側の角は丸く盛る
    static void AddGuard(List<Vector3> V, List<Vector3> N, List<int> T)
    {
        var pts = new[]
        {
            new Vector3(0, 0.0310f, 0.0505f), new Vector3(0, 0.0205f, 0.0500f), new Vector3(0, 0.0115f, 0.0462f), new Vector3(0, 0.0070f, 0.0390f),
            new Vector3(0, 0.0060f, 0.0310f), new Vector3(0, 0.0060f, 0.0230f), new Vector3(0, 0.0062f, 0.0160f), new Vector3(0, 0.0066f, 0.0090f),
        };
        // Catmull-Rom でなめらかに
        var c = new List<Vector3>();
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector3 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Length - 1, i + 2)];
            for (int s = 0; s < 12; s++)
            {
                float t = s / 12f, t2 = t * t, t3 = t2 * t;
                c.Add(0.5f * (2 * p1 + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3));
            }
        }
        c.Add(pts[pts.Length - 1]);
        var hw = Enumerable.Repeat(0.0024f, c.Count).ToArray(); var hd = Enumerable.Repeat(0.0042f, c.Count).ToArray();
        var flare = new float[c.Count];
        for (int k = 0; k < c.Count; k++)
        {
            var p = c[k];
            // 付け根の内側の角を、四分円のくぼみ（フィレット）で埋める。d は付け根の面までの距離（面より中は d<0 で最大のまま）
            flare[k] = Fillet(0.0255f - p.y, 0.0045f);   // 銃身の下の面（グリップ側の角はトリガーの板で埋まるのでいらない）
        }
        Sweep(V, N, T, c.ToArray(), hw, hd, 4f, 0.0015f, 36, 3, flare);
    }

    static float Fillet(float d, float r)
    {
        if (d >= r) return 0f;
        if (d <= 0f) return r;
        float u = r - d; return r - Mathf.Sqrt(r * r - u * u);
    }

    public static string RunGrip()
    {
        var src = AssetDatabase.LoadAssetAtPath<Mesh>(GripSrcPath);
        var dst = AssetDatabase.LoadAssetAtPath<Mesh>(GripDstPath);
        if (src == null)
        {
            // 初回：今の Gun_Grip（Blender から書き出したまま）を元として残す
            src = Object.Instantiate(dst); src.name = "Gun_Grip_Source";
            AssetDatabase.CreateAsset(src, GripSrcPath);
        }
        var v = src.vertices; var nn = src.normals;
        var keep = RemoveIslandsInBox(v, src.GetTriangles(0).ToList(), OldGuardBox, out int removed);
        var map = new Dictionary<int, int>(); var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new List<int>();
        foreach (var i in keep) { if (!map.TryGetValue(i, out int j)) { j = V.Count; map[i] = j; V.Add(v[i]); N.Add(nn[i]); } T.Add(j); }
        AddGuard(V, N, T);
        dst.Clear();
        dst.indexFormat = V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        dst.SetVertices(V); dst.SetNormals(N); dst.subMeshCount = 1; dst.SetTriangles(T, 0); dst.RecalculateBounds();
        EditorUtility.SetDirty(dst); AssetDatabase.SaveAssets();
        return "grip: removed " + removed + " guard tris, verts " + V.Count;
    }
}
