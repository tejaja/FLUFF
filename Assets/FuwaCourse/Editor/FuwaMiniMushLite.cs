using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ミニキノコ（Models/MiniMush/MiniMush_A〜D、Blender の minimush_build.py 製）を軽くする（2026-11-22 てじゃ「軽量化」）。
// 1 本 1274 三角形（かさ 812＝28 分割×15 段／ひだ 224／軸 238）× ワールドに約 290 本で 36 万あった。高さ 0.3m の飾りには細かすぎる。
//  ・かさ・ひだ：どちらも縦の軸まわりの回転体で、段ごとに同じ高さ・同じ半径（円）。段の (半径, 高さ) を読み取り、
//    まっすぐに近い段を抜いて（ずれ Tol 以内）、Seg 分割で作り直す。円でない・段の高さがそろっていない物はさわらない
//  ・軸（曲がっている）はそのまま
//  ・面の向きは元の法線に合わせる。元は MiniMush/Hi に取っておき、いつもそこから作る（何回押しても同じ）
//  ・まとめ（VC_ の写し）は元のメッシュから作るので、いったん戻して作り直し、まとめ直す
// メッシュのかさの幅は変えない（FuwaMiniMushrooms はメッシュの幅から大きさを逆算するので、幅が変わると大きさがずれる）
public static class FuwaMiniMushLite
{
    const string Dir = "Assets/FuwaCourse/Models/MiniMush";
    public static int Seg = 20;   // 16 だと近くでかさのふちの角が見えた
    public static float Tol = 0.006f;   // メッシュの単位（かさの幅 1.1 くらい）

    [MenuItem("FuwaCourse/済み（押す前に相談）/ミニキノコを軽くする")]
    static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder(Dir + "/Hi")) AssetDatabase.CreateFolder(Dir, "Hi");
        FuwaMaterialMerge.Revert();
        var log = new List<string>();
        foreach (var k in new[] { "A", "B", "C", "D" })
        {
            string p = Dir + "/MiniMush_" + k + ".asset", hp = Dir + "/Hi/MiniMush_" + k + "_Hi.asset";
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(p); if (!m) { log.Add("missing " + p); continue; }
            if (!AssetDatabase.LoadAssetAtPath<Mesh>(hp)) AssetDatabase.CopyAsset(p, hp);
            var hi = AssetDatabase.LoadAssetAtPath<Mesh>(hp);
            var v = hi.vertices; var n = hi.normals;
            var outV = new List<Vector3>(); var outN = new List<Vector3>(); var subs = new List<int[]>();
            string info = "";
            for (int s = 0; s < hi.subMeshCount; s++)
            {
                var tri = hi.GetTriangles(s);
                var rebuilt = Revolve(v, n, tri, out string why);
                if (rebuilt == null)
                {
                    // そのまま写す
                    var map = new Dictionary<int, int>(); var nt = new int[tri.Length];
                    for (int i = 0; i < tri.Length; i++) { if (!map.TryGetValue(tri[i], out int j)) { j = outV.Count; map[tri[i]] = j; outV.Add(v[tri[i]]); outN.Add(n[tri[i]]); } nt[i] = j; }
                    subs.Add(nt); info += $" sub{s} keep({why}) {tri.Length / 3}";
                }
                else
                {
                    int b = outV.Count; outV.AddRange(rebuilt.Value.v); outN.AddRange(rebuilt.Value.n);
                    subs.Add(rebuilt.Value.t.Select(i => i + b).ToArray()); info += $" sub{s} {tri.Length / 3}→{rebuilt.Value.t.Length / 3}";
                }
            }
            string name = m.name;
            m.Clear(false); m.SetVertices(outV); m.SetNormals(outN); m.subMeshCount = subs.Count;
            for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s);
            m.name = name; m.RecalculateBounds(); EditorUtility.SetDirty(m);
            log.Add($"MiniMush_{k}: {hi.triangles.Length / 3} → {m.triangles.Length / 3} ({info.Trim()})");
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        log.Add(FuwaMaterialMerge.Run(false));
        return string.Join("\n", log);
    }

    static (List<Vector3> v, List<Vector3> n, int[] t)? Revolve(Vector3[] v, Vector3[] n, int[] tri, out string why)
    {
        why = "";
        var idx = tri.Distinct().ToList();
        // 段：同じ高さの点
        var rings = idx.GroupBy(i => Mathf.RoundToInt(v[i].y * 10000f)).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
        if (rings.Count < 3) { why = "few rings"; return null; }
        // 軸：いちばん点の多い段の重心
        var big = rings.OrderByDescending(r => r.Count).First();
        float cx = big.Average(i => v[i].x), cz = big.Average(i => v[i].z);
        var prof = new List<Vector2>(); bool apexTop = false, apexBot = false;
        for (int ri = 0; ri < rings.Count; ri++)
        {
            var rr = rings[ri].Select(i => new Vector2(v[i].x - cx, v[i].z - cz).magnitude).ToList();
            float r = rr.Average();
            if (rr.Max() - rr.Min() > Mathf.Max(0.004f, r * 0.03f)) { why = "not round"; return null; }
            if (r < 1e-3f) { if (ri == 0) apexBot = true; else if (ri == rings.Count - 1) apexTop = true; else { why = "apex mid"; return null; } }
            prof.Add(new Vector2(r, v[rings[ri][0]].y));
        }
        // まっすぐに近い段を抜く（両端は残す）
        var keep = Enumerable.Range(0, prof.Count).ToList();
        while (true)
        {
            int best = -1; float be = Tol;
            for (int q = 1; q < keep.Count - 1; q++)
            {
                Vector2 a = prof[keep[q - 1]], b = prof[keep[q + 1]], p = prof[keep[q]];
                var ab = b - a; float L = ab.magnitude; float d = L < 1e-6f ? (p - a).magnitude : Mathf.Abs(ab.x * (p.y - a.y) - ab.y * (p.x - a.x)) / L;
                if (d < be) { be = d; best = q; }
            }
            if (best < 0) break; keep.RemoveAt(best);
        }
        var pr = keep.Select(i => prof[i]).ToList();
        int S = Seg, R = pr.Count;
        var ov = new List<Vector3>(); var ot = new List<int>(); var ringStart = new int[R];
        for (int ri = 0; ri < R; ri++)
        {
            ringStart[ri] = ov.Count;
            bool apex = pr[ri].x < 1e-3f;
            if (apex) { ov.Add(new Vector3(cx, pr[ri].y, cz)); continue; }
            for (int a = 0; a < S; a++) { float th = 2f * Mathf.PI * a / S; ov.Add(new Vector3(cx + Mathf.Cos(th) * pr[ri].x, pr[ri].y, cz + Mathf.Sin(th) * pr[ri].x)); }
        }
        for (int ri = 0; ri < R - 1; ri++)
        {
            bool a0 = pr[ri].x < 1e-3f, a1 = pr[ri + 1].x < 1e-3f;
            for (int a = 0; a < S; a++)
            {
                int b = (a + 1) % S;
                if (a0 && a1) continue;
                if (a0) { ot.Add(ringStart[ri]); ot.Add(ringStart[ri + 1] + b); ot.Add(ringStart[ri + 1] + a); }
                else if (a1) { ot.Add(ringStart[ri] + a); ot.Add(ringStart[ri] + b); ot.Add(ringStart[ri + 1]); }
                else { int p0 = ringStart[ri] + a, p1 = ringStart[ri] + b, q0 = ringStart[ri + 1] + a, q1 = ringStart[ri + 1] + b; ot.Add(p0); ot.Add(p1); ot.Add(q1); ot.Add(p0); ot.Add(q1); ot.Add(q0); }
            }
        }
        // 面の向き：元の法線（近い点）と多数決
        var tmp = new Mesh(); tmp.SetVertices(ov); tmp.SetTriangles(ot, 0); tmp.RecalculateNormals(); var tn = tmp.normals; Object.DestroyImmediate(tmp);
        int agree = 0;
        for (int i = 0; i < ov.Count; i++)
        {
            int best = idx[0]; float bd = float.MaxValue; foreach (var j in idx) { float d = (v[j] - ov[i]).sqrMagnitude; if (d < bd) { bd = d; best = j; } }
            agree += Vector3.Dot(tn[i], n[best]) >= 0f ? 1 : -1;
        }
        if (agree < 0) { for (int i = 0; i < ot.Count; i += 3) { int t2 = ot[i + 1]; ot[i + 1] = ot[i + 2]; ot[i + 2] = t2; } for (int i = 0; i < tn.Length; i++) tn[i] = -tn[i]; }
        return (ov, tn.ToList(), ot.ToArray());
    }
}
