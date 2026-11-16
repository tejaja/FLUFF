using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 道のつなぎ目のスキマ調べと直し（2026-11-16 てじゃ「2-4にみちのスキマおおいかも」「道にnon manifoldがないようにチェックしよう　ここおわったら他のコースもチェックしよ」）
//  Analyze：コースの道の見た目（シマの材質 _PathA/_PathB のメッシュ）を世界座標で1つにまとめ、2mm で頂点をくっつけて
//   ・ふちの辺（三角1つだけの辺）・3つ以上の三角が使う辺（non-manifold）・T字のつなぎ（辺の途中に別の頂点がのっている）
//   ・すき間：上の面の高さで平行に並んだふちの辺どうしが 4cm 以内（＝となりの板とちょっと離れていて、両方のふちが丸まって溝になっている）
//  FixGaps：すき間の溝を平らにふさぐ（両がわのふちの丸めを、上の面の高さで真ん中の線にそろえる）。PathBevel のメッシュを直接直す
public static class FuwaPathSeamCheck
{
    const float Weld = 0.002f, GapMax = 0.04f;

    static List<(MeshFilter mf, MeshRenderer mr)> PathMeshes(Transform course)
    {
        return course.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.enabled && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh && r.sharedMaterials.Any(m => m && (m.name.EndsWith("_PathA") || m.name.EndsWith("_PathB"))))
            .Select(r => (r.GetComponent<MeshFilter>(), r)).ToList();
    }

    public struct Gap { public Vector3 a0, a1, b0, b1; public float gap; }

    public static string Analyze(Transform course, out List<Gap> gaps)
    {
        gaps = new List<Gap>();
        var ms = PathMeshes(course);
        if (ms.Count == 0) { return course.name + ": 道のメッシュなし"; }
        var key = new Dictionary<Vector3Int, int>(); var pos = new List<Vector3>();
        int W(Vector3 p) { var k = new Vector3Int(Mathf.RoundToInt(p.x / Weld), Mathf.RoundToInt(p.y / Weld), Mathf.RoundToInt(p.z / Weld)); if (!key.TryGetValue(k, out int id)) { id = pos.Count; pos.Add(p); key[k] = id; } return id; }
        var edges = new Dictionary<long, int>();
        long EK(int x, int y) => x < y ? ((long)x << 32) | (uint)y : ((long)y << 32) | (uint)x;
        int tris = 0;
        foreach (var (mf, _) in ms)
        {
            var m = mf.sharedMesh; var tr = mf.transform; var ids = m.vertices.Select(v => W(tr.TransformPoint(v))).ToArray();
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var t = m.GetTriangles(s);
                for (int i = 0; i < t.Length; i += 3)
                {
                    int a = ids[t[i]], b = ids[t[i + 1]], c = ids[t[i + 2]]; if (a == b || b == c || a == c) continue; tris++;
                    foreach (var (x, y) in new[] { (a, b), (b, c), (c, a) }) { long k = EK(x, y); edges[k] = edges.TryGetValue(k, out int n) ? n + 1 : 1; }
                }
            }
        }
        var bnd = edges.Where(e => e.Value == 1).Select(e => (pos[(int)(e.Key >> 32)], pos[(int)(e.Key & 0xffffffff)])).ToList();
        int nonMan = edges.Count(e => e.Value > 2);
        // T字：ふちの辺の途中に、別の頂点がのっている
        int tj = 0;
        var grid = new Dictionary<Vector3Int, List<int>>();
        for (int i = 0; i < pos.Count; i++) { var g = Vector3Int.FloorToInt(pos[i] / 0.5f); if (!grid.TryGetValue(g, out var l)) grid[g] = l = new List<int>(); l.Add(i); }
        foreach (var (a, b) in bnd)
        {
            float len = (b - a).magnitude; if (len < 0.01f) continue; var d = (b - a) / len;
            var mn = Vector3Int.FloorToInt(Vector3.Min(a, b) / 0.5f); var mx = Vector3Int.FloorToInt(Vector3.Max(a, b) / 0.5f);
            bool hit = false;
            for (int x = mn.x; x <= mx.x && !hit; x++) for (int y = mn.y; y <= mx.y && !hit; y++) for (int z = mn.z; z <= mx.z && !hit; z++)
                if (grid.TryGetValue(new Vector3Int(x, y, z), out var l))
                    foreach (int i in l) { var r = pos[i] - a; float t = Vector3.Dot(r, d); if (t < 0.005f || t > len - 0.005f) continue; if ((r - d * t).magnitude < Weld * 1.5f) { hit = true; break; } }
            if (hit) tj++;
        }
        // すき間：上の面の高さ（水平）で平行なふちの辺が 4cm 以内
        var top = bnd.Where(e => Mathf.Abs(e.Item1.y - e.Item2.y) < 0.002f && (e.Item1 - e.Item2).magnitude > 0.3f).ToList();
        for (int i = 0; i < top.Count; i++) for (int j = i + 1; j < top.Count; j++)
        {
            var (a0, a1) = top[i]; var (b0, b1) = top[j];
            if (Mathf.Abs(a0.y - b0.y) > 0.005f) continue;
            var da = (a1 - a0).normalized; var db = (b1 - b0).normalized; if (Mathf.Abs(Vector3.Dot(da, db)) < 0.995f) continue;
            var rel = (b0 + b1) * 0.5f - a0; var perp = rel - Vector3.Dot(rel, da) * da; float gp = perp.magnitude;
            if (gp > GapMax || gp < 0.0005f) continue;
            float t = Vector3.Dot(rel, da); if (t < -0.5f || t > (a1 - a0).magnitude + 0.5f) continue;
            gaps.Add(new Gap { a0 = a0, a1 = a1, b0 = b0, b1 = b1, gap = gp });
        }
        return course.name + ": 三角 " + tris + "、ふちの辺 " + bnd.Count + "、non-manifold " + nonMan + "、T字 " + tj + "、すき間 " + gaps.Count
            + (gaps.Count > 0 ? "（" + string.Join(" / ", gaps.Take(8).Select(g => (g.gap * 100f).ToString("F1") + "cm@" + ((g.a0 + g.a1) * 0.5f).ToString("F1"))) + "）" : "");
    }

    // すき間の溝をふさぐ（PathBevel を直接）。上の面の高さのふちの辺の組だけ
    public static string FixGaps(Transform course)
    {
        Analyze(course, out var gaps);
        if (gaps.Count == 0) return course.name + ": すき間なし";
        var pb = course.Find("PathBevel"); var mf = pb ? pb.GetComponent<MeshFilter>() : null;
        if (mf == null) return course.name + ": PathBevel がない（すき間 " + gaps.Count + "）";
        var m = mf.sharedMesh; var v = m.vertices; var tr = mf.transform; int moved = 0;
        foreach (var g in gaps)
        {
            float top = g.a0.y;
            var d = g.a1 - g.a0; d.y = 0f; float len = d.magnitude; d /= len;
            var n = ((g.b0 + g.b1) * 0.5f - g.a0); n.y = 0f; n -= Vector3.Dot(n, d) * d; float gp = n.magnitude; n /= gp;
            // 2つの上の辺の重なる範囲
            float tb0 = Vector3.Dot(g.b0 - g.a0, d), tb1 = Vector3.Dot(g.b1 - g.a0, d);
            float tmin = Mathf.Max(0f, Mathf.Min(tb0, tb1)), tmax = Mathf.Min(len, Mathf.Max(tb0, tb1));
            for (int i = 0; i < v.Length; i++)
            {
                var w = tr.TransformPoint(v[i]); var r = w - g.a0;
                float t = Vector3.Dot(r, d), s = Vector3.Dot(r, n), h = top - w.y;
                if (t < tmin - 0.003f || t > tmax + 0.003f) continue;
                if (s < -0.12f || s > gp + 0.12f) continue;
                if (h < -0.002f || h > 0.12f) continue;
                // 上の面の内がわの頂点（ふちから離れた所）は動かさない：ふち（s≈0 か s≈gp）か、その間・外の丸めの所だけ
                if (s < -0.005f && h < 0.002f) continue;
                if (s > gp + 0.005f && h < 0.002f) continue;
                var nw = g.a0 + d * Mathf.Clamp(t, tmin, tmax) + n * (gp * 0.5f); nw.y = top;
                v[i] = tr.InverseTransformPoint(nw); moved++;
            }
        }
        Undo.RecordObject(m, "fix path gaps");
        m.vertices = v; m.RecalculateBounds(); EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
        return course.name + ": すき間 " + gaps.Count + " をふさいだ（頂点 " + moved + "）";
    }

    // 上から見たすき間（溝・抜け）：ふちの辺にそって、両がわ（±6cm）は同じ高さの床なのに、辺の所（±2cm）がへこむ／抜ける所
    public static List<(Vector3 p, float depth)> ScanCracks(Transform course)
    {
        var res = new List<(Vector3, float)>();
        var ms = PathMeshes(course); if (ms.Count == 0) return res;
        var P = new List<Vector3>(); var T = new List<int>();
        foreach (var (mf, _) in ms)
        {
            var m = mf.sharedMesh; var tr = mf.transform; int b0 = P.Count;
            foreach (var v in m.vertices) P.Add(tr.TransformPoint(v));
            for (int s = 0; s < m.subMeshCount; s++) foreach (var t in m.GetTriangles(s)) T.Add(b0 + t);
        }
        var grid = new Dictionary<Vector2Int, List<int>>();
        for (int i = 0; i < T.Count; i += 3)
        {
            Vector3 a = P[T[i]], b = P[T[i + 1]], c = P[T[i + 2]];
            var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < 1e-12f) continue; n.Normalize(); if (Mathf.Abs(n.y) < 0.3f) continue;
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
            int z0 = Mathf.FloorToInt(Mathf.Min(a.z, Mathf.Min(b.z, c.z))), z1 = Mathf.FloorToInt(Mathf.Max(a.z, Mathf.Max(b.z, c.z)));
            for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++) { var k = new Vector2Int(x, z); if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>(); l.Add(i); }
        }
        // (x,z) の真上から、yRef 付近（±0.5m）でいちばん高い上向きの面
        float Top(Vector3 q, float yRef)
        {
            var k = new Vector2Int(Mathf.FloorToInt(q.x), Mathf.FloorToInt(q.z)); if (!grid.TryGetValue(k, out var l)) return float.NegativeInfinity;
            float best = float.NegativeInfinity;
            foreach (int i in l)
            {
                Vector3 a = P[T[i]], b = P[T[i + 1]], c = P[T[i + 2]];
                // 2D の中か（重心座標）
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z); if (Mathf.Abs(d) < 1e-10f) continue;
                float w0 = ((b.z - c.z) * (q.x - c.x) + (c.x - b.x) * (q.z - c.z)) / d, w1 = ((c.z - a.z) * (q.x - c.x) + (a.x - c.x) * (q.z - c.z)) / d, w2 = 1f - w0 - w1;
                if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                float y = w0 * a.y + w1 * b.y + w2 * c.y;
                if (Mathf.Abs(y - yRef) < 0.5f && y > best) best = y;
            }
            return best;
        }
        // ふちの辺（溶接して三角1つだけ）
        var key = new Dictionary<Vector3Int, int>(); var pos = new List<Vector3>();
        int W(Vector3 p) { var kk = new Vector3Int(Mathf.RoundToInt(p.x / Weld), Mathf.RoundToInt(p.y / Weld), Mathf.RoundToInt(p.z / Weld)); if (!key.TryGetValue(kk, out int id)) { id = pos.Count; pos.Add(p); key[kk] = id; } return id; }
        var ids = P.Select(W).ToArray();
        var edges = new Dictionary<long, int>();
        for (int i = 0; i < T.Count; i += 3)
        {
            int a = ids[T[i]], b = ids[T[i + 1]], c = ids[T[i + 2]]; if (a == b || b == c || a == c) continue;
            foreach (var (x, y) in new[] { (a, b), (b, c), (c, a) }) { long kk = x < y ? ((long)x << 32) | (uint)y : ((long)y << 32) | (uint)x; edges[kk] = edges.TryGetValue(kk, out int n) ? n + 1 : 1; }
        }
        foreach (var e in edges.Where(e => e.Value == 1))
        {
            Vector3 a = pos[(int)(e.Key >> 32)], b = pos[(int)(e.Key & 0xffffffff)];
            var dir = b - a; dir.y = 0f; float len = dir.magnitude; if (len < 0.05f) continue; dir /= len;
            var n = new Vector3(dir.z, 0f, -dir.x);
            for (float t = 0.03f; t < len - 0.03f; t += 0.2f)
            {
                var p = Vector3.Lerp(a, b, t / len);
                // 両がわ ±w（7cm と 18cm の2通り：狭いすき間と、少し広いすき間）が同じ高さの床で、その間がへこむ／抜ける
                foreach (float w in new[] { 0.07f, 0.18f })
                {
                    float hL = Top(p - n * w, p.y), hR = Top(p + n * w, p.y);
                    if (float.IsNegativeInfinity(hL) || float.IsNegativeInfinity(hR) || Mathf.Abs(hL - hR) > 0.015f) continue;   // 片がわが床でない（ふち・段差）
                    float ref0 = Mathf.Min(hL, hR), hc = float.PositiveInfinity;
                    for (float u = -w + 0.02f; u <= w - 0.02f; u += 0.02f) hc = Mathf.Min(hc, Top(p + n * u, p.y));
                    float depth = float.IsNegativeInfinity(hc) ? 1f : ref0 - hc;
                    if (depth > 0.01f) { res.Add((p, depth)); break; }
                }
            }
        }
        return res;
    }

    // 道の端のすき間（2026-11-16 てじゃ「まだある」）：ふちを丸めなかった端（となりに床がある扱い）は、下の壁の上を r 下げてあるので、
    //  となりの床が低い・離れていると、上の面のふちと壁の上の間に細いすき間（空が見える）があく → その間を板でふさぐ（PathBevel を直接）
    public static string CloseEndSlits(Transform course, bool apply)
    {
        int total = 0; var sb = new List<string>(); var done = new HashSet<Mesh>();
        foreach (var (mf, _) in PathMeshes(course))
        {
            var mesh = mf.sharedMesh; if (done.Contains(mesh)) continue; done.Add(mesh);
            if (!AssetDatabase.GetAssetPath(mesh).StartsWith("Assets/")) continue;   // 組み込みの箱などは触らない
            int n = CloseEndSlitsMesh(mf, apply, out string where);
            if (n > 0) { total += n; sb.Add(mf.name + "×" + n + where); }
        }
        return course.name + ": 端のすき間 " + total + (apply ? " をふさいだ" : "") + (sb.Count > 0 ? "（" + string.Join(" / ", sb.Take(12)) + "）" : "");
    }

    static int CloseEndSlitsMesh(MeshFilter mf, bool apply, out string where)
    {
        where = "";
        var m = mf.sharedMesh; var v = m.vertices;
        var key = new Dictionary<Vector3Int, int>(); var pos = new List<Vector3>();
        int W(Vector3 p) { var k = new Vector3Int(Mathf.RoundToInt(p.x / Weld), Mathf.RoundToInt(p.y / Weld), Mathf.RoundToInt(p.z / Weld)); if (!key.TryGetValue(k, out int id)) { id = pos.Count; pos.Add(p); key[k] = id; } return id; }
        var ids = v.Select(W).ToArray();
        var edges = new Dictionary<long, (int cnt, int sub, Vector3 tc, int a, int b)>();
        for (int s = 0; s < m.subMeshCount; s++)
        {
            var t = m.GetTriangles(s);
            for (int i = 0; i < t.Length; i += 3)
            {
                int a = ids[t[i]], b = ids[t[i + 1]], c = ids[t[i + 2]]; if (a == b || b == c || a == c) continue;
                var tc = (pos[a] + pos[b] + pos[c]) / 3f;
                foreach (var (x, y) in new[] { (a, b), (b, c), (c, a) })
                {
                    long k = x < y ? ((long)x << 32) | (uint)y : ((long)y << 32) | (uint)x;
                    if (edges.TryGetValue(k, out var e)) edges[k] = (e.cnt + 1, e.sub, e.tc, e.a, e.b); else edges[k] = (1, s, tc, x, y);
                }
            }
        }
        var bnd = edges.Values.Where(e => e.cnt == 1).ToList();
        var used = new HashSet<int>(); var add = new List<(int sub, Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Vector3 outN)>();
        for (int i = 0; i < bnd.Count; i++)
        {
            var E = bnd[i]; Vector3 a0 = pos[E.a], a1 = pos[E.b];
            if (Mathf.Abs(a0.y - a1.y) > 0.05f || (a1 - a0).magnitude < 0.15f) continue;
            // 上の面のふちだけ（となりの三角が上向きの面）
            var dA = a1 - a0; dA.y = 0f; dA.Normalize();
            var outN = (a0 + a1) * 0.5f - E.tc; outN.y = 0f; outN -= Vector3.Dot(outN, dA) * dA; if (outN.sqrMagnitude < 1e-6f) continue; outN.Normalize();
            if (E.tc.y < Mathf.Min(a0.y, a1.y) - 0.01f) continue;   // 三角がふちより下（壁など）→ 上の面ではない
            for (int j = 0; j < bnd.Count; j++)
            {
                if (j == i || used.Contains(j)) continue;
                var F = bnd[j]; Vector3 b0 = pos[F.a], b1 = pos[F.b];
                var mA = (a0 + a1) * 0.5f; var mB = (b0 + b1) * 0.5f;
                float dy = mA.y - mB.y; if (dy < 0.02f || dy > 0.16f) continue;
                var dB = b1 - b0; dB.y = 0f; if (dB.magnitude < 0.1f) continue; dB.Normalize();
                if (Mathf.Abs(Vector3.Dot(dA, dB)) < 0.995f) continue;
                var off = mB - mA; off.y = 0f; float side = Mathf.Abs(Vector3.Dot(off, outN)); float along = Mathf.Abs(Vector3.Dot(off, dA));
                if (side > 0.03f || along > 0.25f) continue;
                if (Vector3.Dot(dA, dB) < 0f) { var tmp = b0; b0 = b1; b1 = tmp; }
                used.Add(j); add.Add((E.sub, a0, a1, b0, b1, outN)); break;
            }
        }
        if (add.Count > 0) where = "@" + mf.transform.TransformPoint((add[0].a0 + add[0].a1) * 0.5f).ToString("F0");
        if (!apply || add.Count == 0) return add.Count;
        var V = new List<Vector3>(v); var N = new List<Vector3>(m.normals); var U = new List<Vector2>(); m.GetUVs(0, U);
        var subs = Enumerable.Range(0, m.subMeshCount).Select(s => m.GetTriangles(s).ToList()).ToList();
        foreach (var q in add)
        {
            int b = V.Count;
            foreach (var p in new[] { q.a0, q.a1, q.b1, q.b0 }) { V.Add(p); N.Add(q.outN); if (U.Count > 0) U.Add(new Vector2(p.x + p.z, p.y)); }
            // 外向き
            var nrm = Vector3.Cross(V[b + 1] - V[b], V[b + 3] - V[b]);
            if (Vector3.Dot(nrm, q.outN) >= 0f) subs[q.sub].AddRange(new[] { b, b + 1, b + 3, b + 1, b + 2, b + 3 });
            else subs[q.sub].AddRange(new[] { b, b + 3, b + 1, b + 1, b + 3, b + 2 });
        }
        Undo.RecordObject(m, "close end slits");
        m.SetVertices(V); m.SetNormals(N); if (U.Count == V.Count) m.SetUVs(0, U);
        for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s);
        m.RecalculateBounds(); EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
        return add.Count;
    }

    [MenuItem("FuwaCourse/よく使う/道のスキマを調べる（全コース）")]
    public static void MenuAnalyzeAll()
    {
        var names = new[] { "FuwaCourse0", "FuwaCourse", "FuwaCourse2", "FuwaCourse3", "FuwaCourse4", "FuwaCourse5", "FuwaCourse2_1", "FuwaCourse2_2", "FuwaCourse2_3", "FuwaCourse2_4", "FuwaCourse2_5" };
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var n in names) { var r = roots.FirstOrDefault(g => g.name == n); if (r) Debug.Log("[道のスキマ] " + Analyze(r.transform, out _)); }
    }
}
