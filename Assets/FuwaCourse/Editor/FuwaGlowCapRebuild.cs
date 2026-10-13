using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// チェックポイントの光るキノコの傘を、きれいな面で作り直す（2026-10-13 てじゃ「チェックポイントのきのこに裏メッシュある、トポロジーちょっときたない」）。
// 前は細かい版（*_Full）を自動で間引いた形で、面の流れがガタガタ・ふちが波打ち、材質が両面表示なので裏の面まで見えていた。
// 細かい版から傘1本ずつ（つながった面のかたまり）の向き・ふちの半径（向きごと）・上の面と裏の面の高さを測り、放射状のきれいな面で作り直す。
// 閉じた形なので材質は片面表示に戻す。メッシュは同じアセットに入れ直す（シーンの参照はそのまま）。ライトマップ用の UV2 も作り直す
public static class FuwaGlowCapRebuild
{
    const string Gen = "Assets/FuwaCourse/Generated/";
    // 左右反対の版（Mirror）は細かい版が粗かったので、同じ形を X で反転して作る（元の Mirror_Full は Fix_Full を X 反転した物と 1.6mm 以内で一致）
    static readonly (string src, string dst, int seg, int prof, bool mirror)[] Jobs =
    {
        ("GlowCaps_Fix_Full", "GlowCaps_Fix", 24, 10, false),
        ("GlowCaps_Fix_Full", "GlowCaps_Mirror_Fix", 24, 10, true),
        ("GlowCaps_Fix_Full", "Far/GlowCaps_Fix_Far", 12, 5, false),
        ("GlowCaps_Fix_Full", "Far/GlowCaps_Mirror_Fix_Far", 12, 5, true),
    };

    [MenuItem("FuwaCourse/チェックポイントの光るキノコの傘を作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new List<string>();
        foreach (var (src, dst, seg, prof, mirror) in Jobs)
        {
            var sm = AssetDatabase.LoadAssetAtPath<Mesh>(Gen + src + ".asset");
            var dm = AssetDatabase.LoadAssetAtPath<Mesh>(Gen + dst + ".asset");
            if (sm == null || dm == null) { log.Add("missing " + src + " / " + dst); continue; }
            var built = Build(sm, seg, prof, out int caps);
            var bv = built.vertices; var bn = built.normals; var bt = built.triangles;
            if (mirror)
            {
                for (int i = 0; i < bv.Length; i++) { bv[i].x = -bv[i].x; bn[i].x = -bn[i].x; }
                for (int i = 0; i < bt.Length; i += 3) { int tmp = bt[i + 1]; bt[i + 1] = bt[i + 2]; bt[i + 2] = tmp; }
            }
            dm.Clear(); dm.SetVertices(bv); dm.SetNormals(bn); dm.SetTriangles(bt, 0);
            Unwrapping.GenerateSecondaryUVSet(dm);
            dm.RecalculateBounds(); EditorUtility.SetDirty(dm);
            Object.DestroyImmediate(built);
            log.Add(dst + ": caps " + caps + " verts " + dm.vertexCount + " tris " + dm.triangles.Length / 3);
        }
        var mat = AssetDatabase.FindAssets("GlowMush_Cap t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault();
        if (mat != null && mat.HasProperty("_Cull")) { mat.SetFloat("_Cull", 2f); EditorUtility.SetDirty(mat); }
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    static Mesh Build(Mesh src, int seg, int prof, out int capCount)
    {
        var v = src.vertices; var t = src.triangles;
        // 同じ位置の頂点をまとめて、つながったかたまり＝傘1本に分ける
        int n = v.Length; var weld = new int[n]; var key = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < n; i++) { var k = Vector3Int.RoundToInt(v[i] * 10000f); if (!key.TryGetValue(k, out var w)) { w = i; key[k] = i; } weld[i] = w; }
        var par = Enumerable.Range(0, n).ToArray();
        int Find(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
        for (int i = 0; i < t.Length; i += 3) { int a = Find(weld[t[i]]), b = Find(weld[t[i + 1]]), c = Find(weld[t[i + 2]]); par[a] = b; par[Find(b)] = Find(c); }
        var groups = Enumerable.Range(0, n).Where(i => weld[i] == i).GroupBy(i => Find(i)).Select(g => g.Select(i => v[i]).ToList()).Where(g => g.Count > 12).ToList();
        capCount = groups.Count;

        var outV = new List<Vector3>(); var outT = new List<int>();
        foreach (var pts in groups) BuildCap(pts, seg, prof, outV, outT);
        var m = new Mesh(); m.SetVertices(outV); m.SetTriangles(outT, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // 傘1本：向き（いちばん薄い方向）、ふちの半径（向きごと）、上の面・裏の面の高さ（ふちまでの割合ごと）を測って回転体で作る
    static void BuildCap(List<Vector3> pts, int seg, int prof, List<Vector3> outV, List<int> outT)
    {
        Vector3 c = Vector3.zero; foreach (var p in pts) c += p; c /= pts.Count;
        // 共分散のいちばん小さい向き＝傘の軸（べき乗法で大きい2つを求めて、その外積）
        var C = new float[3, 3];
        foreach (var p in pts) { var d = p - c; for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) C[i, j] += d[i] * d[j]; }
        Vector3 Mul(Vector3 x) => new Vector3(C[0, 0] * x.x + C[0, 1] * x.y + C[0, 2] * x.z, C[1, 0] * x.x + C[1, 1] * x.y + C[1, 2] * x.z, C[2, 0] * x.x + C[2, 1] * x.y + C[2, 2] * x.z);
        Vector3 e1 = new Vector3(1f, 0.3f, 0.2f).normalized; for (int k = 0; k < 60; k++) e1 = Mul(e1).normalized;
        Vector3 e2 = Vector3.Cross(e1, Vector3.up).sqrMagnitude > 0.01f ? Vector3.Cross(e1, Vector3.up).normalized : Vector3.Cross(e1, Vector3.right).normalized;
        for (int k = 0; k < 60; k++) { e2 = Mul(e2); e2 = (e2 - Vector3.Dot(e2, e1) * e1).normalized; }
        Vector3 ax = Vector3.Cross(e1, e2).normalized;
        // てっぺん側（とがった方）が +ax
        float up = pts.Max(p => Vector3.Dot(p - c, ax)), dn = pts.Max(p => -Vector3.Dot(p - c, ax));
        if (dn > up) ax = -ax;
        // 軸の位置：いちばん上の点の真下に寄せる（中心の取り違えでふちが片寄らないように）。上から1割の点の平均
        var topPts = pts.OrderByDescending(p => Vector3.Dot(p - c, ax)).Take(Mathf.Max(3, pts.Count / 10)).ToList();
        Vector3 tc = Vector3.zero; foreach (var p in topPts) tc += p; tc /= topPts.Count;
        c += (tc - c) - Vector3.Dot(tc - c, ax) * ax;
        Vector3 bu = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized, bv = Vector3.Cross(ax, bu);

        // ふちの半径（向きごと、なめらかに）
        int A = 36; var rim = new float[A];
        var hs = new List<(float th, float rho, float h)>();
        foreach (var p in pts)
        {
            var d = p - c; float h = Vector3.Dot(d, ax); var q = d - h * ax; float rho = q.magnitude;
            float th = Mathf.Atan2(Vector3.Dot(q, bv), Vector3.Dot(q, bu)); hs.Add((th, rho, h));
            int bi = Mathf.FloorToInt((th + Mathf.PI) / (2f * Mathf.PI) * A) % A; rim[bi] = Mathf.Max(rim[bi], rho);
        }
        float rimMean = rim.Where(r => r > 0f).DefaultIfEmpty(0.1f).Average();
        for (int i = 0; i < A; i++) if (rim[i] <= 0f) rim[i] = rimMean;
        var rimS = new float[A];
        for (int i = 0; i < A; i++) { float s = 0f; for (int k = -3; k <= 3; k++) s += rim[(i + k + A) % A]; rimS[i] = s / 7f; }
        float Rim(float th) { float f = (th + Mathf.PI) / (2f * Mathf.PI) * A - 0.5f; int i0 = Mathf.FloorToInt(f); float w = f - i0; return Mathf.Lerp(rimS[(i0 % A + A) % A], rimS[((i0 + 1) % A + A) % A], w); }

        // 上の面・裏の面の高さ（ふちまでの割合 s ごと、全部の向きで平均）
        int S = 20; var top = new float[S + 1]; var bot = new float[S + 1]; var topN = new int[S + 1]; var botN = new int[S + 1];
        var topMax = Enumerable.Repeat(float.MinValue, (S + 1) * A).ToArray(); var botMin = Enumerable.Repeat(float.MaxValue, (S + 1) * A).ToArray();
        foreach (var (th, rho, h) in hs)
        {
            float s = Mathf.Clamp01(rho / Rim(th)); int si = Mathf.RoundToInt(s * S); int bi = Mathf.FloorToInt((th + Mathf.PI) / (2f * Mathf.PI) * A) % A;
            topMax[si * A + bi] = Mathf.Max(topMax[si * A + bi], h); botMin[si * A + bi] = Mathf.Min(botMin[si * A + bi], h);
        }
        for (int si = 0; si <= S; si++) for (int bi = 0; bi < A; bi++)
        {
            if (topMax[si * A + bi] > float.MinValue) { top[si] += topMax[si * A + bi]; topN[si]++; }
            if (botMin[si * A + bi] < float.MaxValue) { bot[si] += botMin[si * A + bi]; botN[si]++; }
        }
        float[] Fill(float[] a, int[] cnt)
        {
            var r = new float[S + 1];
            for (int i = 0; i <= S; i++) r[i] = cnt[i] > 0 ? a[i] / cnt[i] : float.NaN;
            for (int i = 0; i <= S; i++) if (float.IsNaN(r[i]))
            {
                int lo = i - 1; while (lo >= 0 && float.IsNaN(r[lo])) lo--; int hi = i + 1; while (hi <= S && float.IsNaN(r[hi])) hi++;
                r[i] = lo < 0 ? r[hi] : hi > S ? r[lo] : Mathf.Lerp(r[lo], r[hi], (float)(i - lo) / (hi - lo));
            }
            var sm = new float[S + 1];   // 少しならす
            for (int i = 0; i <= S; i++) { float s = 0f, w = 0f; for (int k = -1; k <= 1; k++) { int j = Mathf.Clamp(i + k, 0, S); float ww = k == 0 ? 2f : 1f; s += r[j] * ww; w += ww; } sm[i] = s / w; }
            return sm;
        }
        var topF = Fill(top, topN); var botF = Fill(bot, botN);
        float Lerp(float[] a, float s) { float f = Mathf.Clamp01(s) * S; int i0 = Mathf.Min(S - 1, Mathf.FloorToInt(f)); return Mathf.Lerp(a[i0], a[i0 + 1], f - i0); }
        // てっぺんは軸の上でいちばん高い所（平均だと丸まる）
        float apex = hs.Where(x => x.rho < rimMean * 0.08f).Select(x => x.h).DefaultIfEmpty(topF[0]).Max();

        // 断面の線（てっぺん → 上の面 → ふちの丸み → 裏の面 → 軸の下）。(割合 s, 高さ h)
        var line = new List<Vector2>();
        line.Add(new Vector2(0f, apex));
        for (int i = 1; i <= prof; i++) { float s = 0.94f * Mathf.Pow((float)i / prof, 0.85f); line.Add(new Vector2(s, Lerp(topF, s))); }
        float hT = Lerp(topF, 0.94f), hB = Lerp(botF, 0.94f), mid = (hT + hB) * 0.5f, half = Mathf.Max(0.004f, (hT - hB) * 0.5f);
        for (int k = 1; k <= 3; k++) { float a = Mathf.PI * 0.5f - Mathf.PI * k / 4f; line.Add(new Vector2(0.94f + 0.06f * Mathf.Cos(a), mid + half * Mathf.Sin(a))); }
        int under = Mathf.Max(3, prof / 2);
        for (int i = 1; i <= under; i++) { float s = 0.94f * (1f - (float)i / under); line.Add(new Vector2(s, s <= 0.001f ? botF[0] : Lerp(botF, s))); }

        // 回転体：てっぺんと底の中心は1点にまとめる
        int baseI = outV.Count, L = line.Count;
        outV.Add(c + ax * line[0].y);
        for (int li = 1; li < L - 1; li++)
            for (int a = 0; a < seg; a++)
            {
                float th = -Mathf.PI + 2f * Mathf.PI * a / seg; float r = line[li].x * Rim(th);
                outV.Add(c + ax * line[li].y + (bu * Mathf.Cos(th) + bv * Mathf.Sin(th)) * r);
            }
        outV.Add(c + ax * line[L - 1].y);
        int top0 = baseI, bot0 = outV.Count - 1;
        int Ring(int li, int a) => baseI + 1 + (li - 1) * seg + ((a % seg) + seg) % seg;
        // 表は外向き：上から見て（+ax 側から）反時計回りに th が増える並び → 外向きになるよう向きをそろえる
        for (int a = 0; a < seg; a++) { outT.Add(top0); outT.Add(Ring(1, a + 1)); outT.Add(Ring(1, a)); }
        for (int li = 1; li < L - 2; li++)
            for (int a = 0; a < seg; a++)
            {
                int p0 = Ring(li, a), p1 = Ring(li, a + 1), q0 = Ring(li + 1, a), q1 = Ring(li + 1, a + 1);
                outT.Add(p0); outT.Add(p1); outT.Add(q1); outT.Add(p0); outT.Add(q1); outT.Add(q0);
            }
        for (int a = 0; a < seg; a++) { outT.Add(bot0); outT.Add(Ring(L - 2, a)); outT.Add(Ring(L - 2, a + 1)); }
        // 向きの確認：てっぺんの三角形の法線が +ax を向いていなければ、この傘の三角形を全部裏返す
        var n0 = Vector3.Cross(outV[outT[outT.Count - 3 * seg * (2 * (L - 3) + 2) + 1]] - outV[top0], outV[outT[outT.Count - 3 * seg * (2 * (L - 3) + 2) + 2]] - outV[top0]);
        if (Vector3.Dot(n0, ax) < 0f)
        {
            int start = outT.Count - 3 * seg * (2 * (L - 3) + 2);
            for (int i = start; i < outT.Count; i += 3) { int tmp = outT[i + 1]; outT[i + 1] = outT[i + 2]; outT[i + 2] = tmp; }
        }
    }
}
