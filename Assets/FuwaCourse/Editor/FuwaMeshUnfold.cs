using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// メッシュの「折り目」（となりの三角形どうしが 115° 以上折れ返っている細い所）を、まわりの点になじませて平らに戻す（2026-10-13）。
// てじゃ「謎の横線」：いわのみちの頂上の道の南のふちの下で、山の面が細く折り返して影の線に見えていた（10-13 以前の「山の地面を道の下まで持ち上げる」の名残）。
// 折り目にかかる点だけを、となりの点の平均へ少しずつ寄せる（ほかの点は動かさない）。同じ位置の点はいっしょに動かす
public static class FuwaMeshUnfold
{
    // 箱の中の点を、となりの平均へ寄せてならす（細い段差・溝をなくす）。箱のふちの点ほど弱く（まわりとなめらかにつながるように）
    public static int Smooth(Mesh mesh, Transform tr, Bounds box, int iterations, float strength = 0.5f)
    {
        var l2w = tr.localToWorldMatrix; var w2l = tr.worldToLocalMatrix;
        var V = new List<Vector3>(); mesh.GetVertices(V);
        var W = V.Select(v => l2w.MultiplyPoint3x4(v)).ToList();
        var weld = FuwaCutCap.Weld(W, 0.0003f);
        var tris = new List<int>(); for (int s = 0; s < mesh.subMeshCount; s++) tris.AddRange(mesh.GetTriangles(s));
        var nb = new Dictionary<int, HashSet<int>>();
        for (int i = 0; i < tris.Count; i += 3)
        {
            int a = weld[tris[i]], b = weld[tris[i + 1]], c = weld[tris[i + 2]];
            foreach (var (p, q, r) in new[] { (a, b, c), (b, c, a), (c, a, b) }) { if (!nb.TryGetValue(p, out var s)) nb[p] = s = new HashSet<int>(); if (q != p) s.Add(q); if (r != p) s.Add(r); }
        }
        var pos = nb.Keys.ToDictionary(id => id, id => W[id]);
        var inBox = pos.Keys.Where(id => box.Contains(pos[id])).ToList();
        float Wt(Vector3 p)
        {
            var d = Vector3.Min(p - box.min, box.max - p); float m = Mathf.Min(d.x, Mathf.Min(d.y, d.z));
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(m / 0.3f));
        }
        for (int it = 0; it < iterations; it++)
        {
            var np = new Dictionary<int, Vector3>();
            foreach (var v in inBox) { var c = Vector3.zero; foreach (var q in nb[v]) c += pos[q]; c /= nb[v].Count; np[v] = Vector3.Lerp(pos[v], c, strength * Wt(pos[v])); }
            foreach (var kv in np) pos[kv.Key] = kv.Value;
        }
        var moved = new HashSet<int>(inBox);
        for (int i = 0; i < V.Count; i++) if (moved.Contains(weld[i])) V[i] = w2l.MultiplyPoint3x4(pos[weld[i]]);
        mesh.SetVertices(V);
        var N = new List<Vector3>(); mesh.GetNormals(N);
        if (N.Count == V.Count)
        {
            var touch = new HashSet<int>(moved); foreach (var v in moved) foreach (var q in nb[v]) touch.Add(q);
            var sum = new Dictionary<int, Vector3>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = weld[tris[i]], b = weld[tris[i + 1]], c = weld[tris[i + 2]];
                if (!touch.Contains(a) && !touch.Contains(b) && !touch.Contains(c)) continue;
                var n = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
                foreach (var v in new[] { a, b, c }) if (touch.Contains(v)) sum[v] = (sum.TryGetValue(v, out var x) ? x : Vector3.zero) + n;
            }
            for (int i = 0; i < V.Count; i++) if (sum.TryGetValue(weld[i], out var s) && s.sqrMagnitude > 0f) N[i] = w2l.MultiplyVector(s).normalized;
            mesh.SetNormals(N);
        }
        mesh.RecalculateBounds();
        return inBox.Count;
    }

    public static (int before, int after, int moved) Run(Mesh mesh, Transform tr, Bounds region, float foldAngle = 115f, int iterations = 30)
    {
        var l2w = tr.localToWorldMatrix; var w2l = tr.worldToLocalMatrix;
        var V = new List<Vector3>(); mesh.GetVertices(V);
        var W = V.Select(v => l2w.MultiplyPoint3x4(v)).ToList();
        var weld = FuwaCutCap.Weld(W, 0.0003f);
        var tris = new List<int>(); for (int s = 0; s < mesh.subMeshCount; s++) tris.AddRange(mesh.GetTriangles(s));
        // 点のとなり（同じ位置の点はまとめて）
        var nb = new Dictionary<int, HashSet<int>>();
        for (int i = 0; i < tris.Count; i += 3)
        {
            int a = weld[tris[i]], b = weld[tris[i + 1]], c = weld[tris[i + 2]];
            foreach (var (p, q, r) in new[] { (a, b, c), (b, c, a), (c, a, b) }) { if (!nb.TryGetValue(p, out var s)) nb[p] = s = new HashSet<int>(); if (q != p) s.Add(q); if (r != p) s.Add(r); }
        }
        var pos = new Dictionary<int, Vector3>(); foreach (var id in nb.Keys) pos[id] = W[id];
        long EK(int a, int b) => ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
        HashSet<int> FoldVerts(out int count)
        {
            var edgeT = new Dictionary<long, List<int>>(); var tn = new List<Vector3>(); var tA = new List<float>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = weld[tris[i]], b = weld[tris[i + 1]], c = weld[tris[i + 2]];
                var n = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]); tA.Add(n.magnitude); tn.Add(n.normalized);
                if (a == b || b == c || a == c) continue;
                foreach (var k in new[] { EK(a, b), EK(b, c), EK(c, a) }) { if (!edgeT.TryGetValue(k, out var l)) edgeT[k] = l = new List<int>(); l.Add(i / 3); }
            }
            var set = new HashSet<int>(); count = 0;
            foreach (var kv in edgeT)
            {
                if (kv.Value.Count != 2) continue;
                int t0 = kv.Value[0], t1 = kv.Value[1];
                if (tA[t0] < 2e-4f || tA[t1] < 2e-4f) continue;   // 面積がほぼ0の三角形は見えないので数えない
                int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff);
                if (!region.Contains((pos[a] + pos[b]) * 0.5f)) continue;
                if (Vector3.Angle(tn[t0], tn[t1]) < foldAngle) continue;
                count++;
                foreach (var t in new[] { t0, t1 }) for (int k = 0; k < 3; k++) set.Add(weld[tris[t * 3 + k]]);
            }
            return set;
        }
        var fv = FoldVerts(out int before); int after = before; var movedSet = new HashSet<int>();
        for (int it = 0; it < iterations && fv.Count > 0; it++)
        {
            var np = new Dictionary<int, Vector3>();
            foreach (var v in fv)
            {
                var c = Vector3.zero; foreach (var q in nb[v]) c += pos[q]; c /= nb[v].Count;
                np[v] = Vector3.Lerp(pos[v], c, 0.5f);
            }
            foreach (var kv in np) { pos[kv.Key] = kv.Value; movedSet.Add(kv.Key); }
            fv = FoldVerts(out after);
        }
        for (int i = 0; i < V.Count; i++) if (movedSet.Contains(weld[i])) V[i] = w2l.MultiplyPoint3x4(pos[weld[i]]);
        mesh.SetVertices(V);
        // 動かした点とそのとなりの法線を、まわりの三角形の向きの平均に
        var N = new List<Vector3>(); mesh.GetNormals(N);
        if (N.Count == V.Count && movedSet.Count > 0)
        {
            var touch = new HashSet<int>(movedSet); foreach (var v in movedSet) foreach (var q in nb[v]) touch.Add(q);
            var sum = new Dictionary<int, Vector3>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = weld[tris[i]], b = weld[tris[i + 1]], c = weld[tris[i + 2]];
                if (!touch.Contains(a) && !touch.Contains(b) && !touch.Contains(c)) continue;
                var n = Vector3.Cross(pos[b] - pos[a], pos[c] - pos[a]);
                foreach (var v in new[] { a, b, c }) if (touch.Contains(v)) sum[v] = (sum.TryGetValue(v, out var x) ? x : Vector3.zero) + n;
            }
            for (int i = 0; i < V.Count; i++) if (sum.TryGetValue(weld[i], out var s) && s.sqrMagnitude > 0f) N[i] = w2l.MultiplyVector(s).normalized;
            mesh.SetNormals(N);
        }
        mesh.RecalculateBounds();
        return (before, after, movedSet.Count);
    }
}
