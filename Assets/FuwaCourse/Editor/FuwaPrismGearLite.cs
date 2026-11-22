using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 歯車を軽くする その2（2026-11-22）：FuwaGearLite（Extrude の並びの歯車だけ）でさわれなかった、作り方のちがう歯車
// （タンクの輪・大時計の輪・遊星の輪・跳ね橋の歯車・回転台の輪など。内歯車・窓あき・扇形・輪っか）を軽くする。
// どれも「2D の形を上下にのばした板」（頂点が高さ2つだけ）なので、側面の縦の面から輪郭（上のふちの線）を読み取り、
// まっすぐに近い点（前後を結ぶ線からのずれ < Tol）を、となりの点の位置へ寄せて消す（上下の同じ位置の点をいっしょに）。
// 寄せた結果つぶれた三角形は消す。上の面・下の面の三角形が裏返る時はその点は寄せない。
//  ・名前に「_z数字」がある（歯車の）メッシュで、FuwaGearLite で作り直していない物だけ
//  ・元は Generated/GearHi2 に。いつもそこから作る（何回押しても同じ）。平面の UV（歯車の模様用）は付け直す
//  ・まとめ（VC_ の写し）は元から作るので、最後に「色ちがいの材質をまとめる」をかけ直す
public static class FuwaPrismGearLite
{
    const string Bak = "Assets/FuwaCourse/Generated/GearHi2";
    public static float TolPerRadius = 0.0006f;   // 輪郭のずれの許し（歯車の半径に対して）。z45 の輪で約0.7mm

    [MenuItem("FuwaCourse/よく使う/歯車を軽くする その2（作り方のちがう歯車）")]
    static void Menu() { Debug.Log(Run(false)); }
    [MenuItem("FuwaCourse/よく使う/歯車を軽くする その2（数えるだけ）")]
    static void MenuDry() { Debug.Log(Run(true)); }

    public static string Run(bool dry)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!dry) FuwaMaterialMerge.Revert();   // 中で部品まとめも戻る… は MaterialMerge.Run がやるので、ここでは VC_ の写しだけ戻す
        var meshes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true)).Select(f => f.sharedMesh)
            .Where(m => m && System.Text.RegularExpressions.Regex.IsMatch(m.name, @"_z\d+") && !m.name.StartsWith("VC_") && AssetDatabase.GetAssetPath(m).StartsWith("Assets/") && !AssetDatabase.GetAssetPath(m).Contains("/GearHi")).Distinct().ToList();
        var done1 = new HashSet<string>(AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/FuwaCourse/Generated/GearHi" }).Select(g => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)).Replace("_Hi", "")));
        if (!dry && !AssetDatabase.IsValidFolder(Bak)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "GearHi2");
        var log = new List<string>(); long before = 0, after = 0; int count = 0;
        foreach (var m in meshes)
        {
            if (done1.Contains(m.name)) continue;
            string bp = Bak + "/" + m.name + "_Hi.asset";
            var hi = AssetDatabase.LoadAssetAtPath<Mesh>(bp); var src = hi ? hi : m;
            var res = Simplify(src, out string why);
            if (res == null) { if (why != "notprism") log.Add($"{m.name}: skip ({why})"); continue; }
            count++; before += src.triangles.Length / 3; after += res.triangles.Length / 3;
            log.Add($"{m.name}: {src.triangles.Length / 3} → {res.triangles.Length / 3}");
            if (dry) { Object.DestroyImmediate(res); continue; }
            if (!hi) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(m), bp);
            string nm = m.name;
            m.Clear(false); m.indexFormat = res.indexFormat; m.SetVertices(res.vertices); if (res.normals.Length > 0) m.SetNormals(res.normals);
            if (res.uv.Length > 0) m.SetUVs(0, res.uv); if (res.colors32.Length > 0) m.SetColors(res.colors32); if (res.tangents.Length > 0) m.SetTangents(res.tangents);
            m.subMeshCount = res.subMeshCount; for (int s = 0; s < res.subMeshCount; s++) m.SetTriangles(res.GetTriangles(s), s);
            m.name = nm; m.RecalculateBounds(); EditorUtility.SetDirty(m); Object.DestroyImmediate(res);
        }
        string head = $"[歯車を軽く その2] {count} 個 {before / 1000}k → {after / 1000}k（メッシュ1つずつ）\n" + string.Join("\n", log.OrderBy(s => s));
        if (!dry) { AssetDatabase.SaveAssets(); head += "\n" + FuwaMaterialMerge.Run(false); }
        return head;
    }

    static Mesh Simplify(Mesh src, out string why)
    {
        why = "";
        var v = src.vertices; var n = src.normals; var uv = src.uv;
        if (v.Length == 0) { why = "empty"; return null; }
        // 高さが2つだけか（板）
        var ys = v.Select(p => Mathf.Round(p.y * 10000f)).Distinct().ToList();
        if (ys.Count != 2) { why = "notprism"; return null; }
        float y0 = ys.Min() / 10000f, y1 = ys.Max() / 10000f;
        // 平面の UV か（u=a·x+b, v=c·z+d）
        bool planarUV = false; Vector4 fit = Vector4.zero;
        if (uv.Length == v.Length && v.Length > 3)
        {
            Vector2 Fit(System.Func<int, float> X, System.Func<int, float> Y) { double sx = 0, sy = 0, sxx = 0, sxy = 0; int N = v.Length; for (int i = 0; i < N; i++) { double x = X(i), y = Y(i); sx += x; sy += y; sxx += x * x; sxy += x * y; } double den = N * sxx - sx * sx; if (System.Math.Abs(den) < 1e-12) return new Vector2(float.NaN, 0); double a = (N * sxy - sx * sy) / den; return new Vector2((float)a, (float)((sy - a * sx) / N)); }
            var fu = Fit(i => v[i].x, i => uv[i].x); var fv = Fit(i => v[i].z, i => uv[i].y);
            if (!float.IsNaN(fu.x) && !float.IsNaN(fv.x)) { planarUV = true; for (int i = 0; i < v.Length; i++) if (Mathf.Abs(fu.x * v[i].x + fu.y - uv[i].x) > 1e-3f || Mathf.Abs(fv.x * v[i].z + fv.y - uv[i].y) > 1e-3f) { planarUV = false; break; } fit = new Vector4(fu.x, fu.y, fv.x, fv.y); }
        }
        Vector2Int K(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x * 100000f), Mathf.RoundToInt(p.z * 100000f));
        // 側面（上と下の高さの点をまたぐ三角形）から輪郭の辺を取る：同じ三角形の中で、同じ高さの2点を結ぶ辺
        var subs = Enumerable.Range(0, src.subMeshCount).Select(s => src.GetTriangles(s)).ToList();
        var adj = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
        var pos2 = new Dictionary<Vector2Int, Vector2>();
        foreach (var t in subs)
            for (int i = 0; i < t.Length; i += 3)
            {
                int a = t[i], b = t[i + 1], c = t[i + 2];
                bool ta = v[a].y > (y0 + y1) * 0.5f, tb = v[b].y > (y0 + y1) * 0.5f, tc = v[c].y > (y0 + y1) * 0.5f;
                if (ta == tb && tb == tc) continue;   // 上か下の面
                foreach (var (p, q, sp, sq) in new[] { (a, b, ta, tb), (b, c, tb, tc), (c, a, tc, ta) })
                {
                    if (sp != sq) continue;
                    var kp = K(v[p]); var kq = K(v[q]); if (kp == kq) continue;
                    if (!adj.TryGetValue(kp, out var s1)) adj[kp] = s1 = new HashSet<Vector2Int>(); s1.Add(kq);
                    if (!adj.TryGetValue(kq, out var s2)) adj[kq] = s2 = new HashSet<Vector2Int>(); s2.Add(kp);
                    pos2[kp] = new Vector2(v[p].x, v[p].z); pos2[kq] = new Vector2(v[q].x, v[q].z);
                }
            }
        if (adj.Count < 12) { why = "nooutline"; return null; }
        float R = pos2.Values.Max(p => p.magnitude); float tol = TolPerRadius * Mathf.Max(R, 0.05f);
        // 点の位置（2D キー）→ その位置の頂点たち
        var at = new Dictionary<Vector2Int, List<int>>();
        for (int i = 0; i < v.Length; i++) { var k = K(v[i]); if (!at.TryGetValue(k, out var l)) at[k] = l = new List<int>(); l.Add(i); }
        var nv = (Vector3[])v.Clone();
        var faceTris = subs.SelectMany(t => Enumerable.Range(0, t.Length / 3).Select(i => (t[i * 3], t[i * 3 + 1], t[i * 3 + 2]))).Where(tr => { bool a = v[tr.Item1].y > (y0 + y1) * 0.5f, b = v[tr.Item2].y > (y0 + y1) * 0.5f, c = v[tr.Item3].y > (y0 + y1) * 0.5f; return a == b && b == c; }).ToList();
        var trisOf = new Dictionary<Vector2Int, List<int>>();   // 2D 位置 → 上下の面の三角形の番号
        for (int ti = 0; ti < faceTris.Count; ti++) foreach (var vi in new[] { faceTris[ti].Item1, faceTris[ti].Item2, faceTris[ti].Item3 }) { var k = K(v[vi]); if (!trisOf.TryGetValue(k, out var l)) trisOf[k] = l = new List<int>(); if (!l.Contains(ti)) l.Add(ti); }
        float Area(int ti) { var (a, b, c) = faceTris[ti]; var p = nv[a]; var q = nv[b]; var r = nv[c]; return (q.x - p.x) * (r.z - p.z) - (q.z - p.z) * (r.x - p.x); }
        var removed = new HashSet<Vector2Int>(); int snaps = 0;
        bool progress = true;
        while (progress)
        {
            progress = false;
            var cand = adj.Where(kv => !removed.Contains(kv.Key) && kv.Value.Count == 2).Select(kv =>
            {
                var nb = kv.Value.ToArray(); var a = pos2[nb[0]]; var b = pos2[nb[1]]; var p = pos2[kv.Key];
                var ab = b - a; float L = ab.magnitude; float d = L < 1e-7f ? 0f : Mathf.Abs(ab.x * (p.y - a.y) - ab.y * (p.x - a.x)) / L;
                return (k: kv.Key, d, u: nb[0], w: nb[1]);
            }).Where(c => c.d < tol).OrderBy(c => c.d).ToList();
            foreach (var c in cand)
            {
                if (removed.Contains(c.k) || removed.Contains(c.u) || removed.Contains(c.w)) continue;
                if (!adj[c.k].Contains(c.u) || !adj[c.k].Contains(c.w)) continue;
                // 近い方のとなりへ寄せる
                var target = (pos2[c.u] - pos2[c.k]).sqrMagnitude <= (pos2[c.w] - pos2[c.k]).sqrMagnitude ? c.u : c.w; var other = target.Equals(c.u) ? c.w : c.u;
                var idx = at[c.k]; var old = idx.Select(i => nv[i]).ToArray();
                var tp = pos2[target];
                var before = (trisOf.TryGetValue(c.k, out var tl) ? tl : new List<int>()).Select(Area).ToArray();
                foreach (var i in idx) nv[i] = new Vector3(tp.x, nv[i].y, tp.y);
                bool flip = false; int j = 0;
                foreach (var ti in tl ?? new List<int>()) { float a1 = Area(ti); float a0 = before[j++]; if (Mathf.Abs(a1) > 1e-10f && Mathf.Sign(a1) != Mathf.Sign(a0)) { flip = true; break; } }
                if (flip) { for (int q = 0; q < idx.Count; q++) nv[idx[q]] = old[q]; continue; }
                // つなぎかえ
                adj[c.u].Remove(c.k); adj[c.w].Remove(c.k); adj[c.u].Add(c.w); adj[c.w].Add(c.u); adj[c.k].Clear();
                removed.Add(c.k);
                // 寄せた点を含む三角形は、寄せ先の位置の三角形の一覧にも入れる
                if (tl != null) { if (!trisOf.TryGetValue(target, out var tt)) trisOf[target] = tt = new List<int>(); foreach (var ti in tl) if (!tt.Contains(ti)) tt.Add(ti); }
                if (!at.TryGetValue(target, out var ta2)) at[target] = ta2 = new List<int>(); ta2.AddRange(idx); at.Remove(c.k);
                snaps++; progress = true;
            }
        }
        if (snaps == 0) { why = "nothing"; return null; }
        // つぶれた三角形を消して、使っていない頂点を詰める
        var newSubs = new List<int[]>(); var used = new bool[nv.Length];
        foreach (var t in subs)
        {
            var o = new List<int>();
            for (int i = 0; i < t.Length; i += 3)
            {
                var a = nv[t[i]]; var b = nv[t[i + 1]]; var c = nv[t[i + 2]];
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-14f) continue;
                o.Add(t[i]); o.Add(t[i + 1]); o.Add(t[i + 2]); used[t[i]] = used[t[i + 1]] = used[t[i + 2]] = true;
            }
            newSubs.Add(o.ToArray());
        }
        var remap = new int[nv.Length]; int cnt = 0; for (int i = 0; i < nv.Length; i++) remap[i] = used[i] ? cnt++ : -1;
        var m = new Mesh { indexFormat = cnt > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
        var keep = Enumerable.Range(0, nv.Length).Where(i => used[i]).ToArray();
        m.SetVertices(keep.Select(i => nv[i]).ToArray());
        if (n.Length == v.Length) m.SetNormals(keep.Select(i => n[i]).ToArray());
        if (uv.Length == v.Length) m.SetUVs(0, planarUV ? keep.Select(i => new Vector2(fit.x * nv[i].x + fit.y, fit.z * nv[i].z + fit.w)).ToArray() : keep.Select(i => uv[i]).ToArray());
        var col = src.colors32; if (col.Length == v.Length) m.SetColors(keep.Select(i => col[i]).ToArray());
        var tan = src.tangents; if (tan.Length == v.Length) m.SetTangents(keep.Select(i => tan[i]).ToArray());
        m.subMeshCount = newSubs.Count; for (int s = 0; s < newSubs.Count; s++) m.SetTriangles(newSubs[s].Select(i => remap[i]).ToArray(), s);
        m.RecalculateBounds();
        return m;
    }
}
