using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// FuwaPitCut.CutJagged で切った後の「開いた切り口」を、すき間なく面でふさぐ（2026-10-13 てじゃ「隙間・はみ出し系は徹底的に洗って」）。
// 前は切り口の開いた辺から真下へ壁を後付けしていて（BrokenPath.CapOpenLoops）、壁と道の間にすき間・裏がノコギリ状になっていた。
// 切り口は3つの面の上にある：手前の切り口（s = e0(l)）・奥の切り口（s = e1(l)）・底（y = yBottom）。
// 開いた辺をどの面の上か分けて、面ごとに「切り口の輪」をつないで多角形にし、その面の座標（壁は (l, y)、底は (l, s)）で三角形に割る。
// 輪の点は元のメッシュの開いた辺の点そのものなので、ふたとメッシュはぴったりつながる（すき間ゼロ）。
// 壁が底まで届く所（山の谷など）は、壁の下はしを底の線でつなぐ（底の面と同じ点を使う）。
// rough > 0 なら、大きな面に点を足して、ふちから離れた所だけゆるいでこぼこ（岩肌）にする
public static class FuwaCutCap
{
    public class Frame
    {
        public Vector3 origin, axis, side;
        public System.Func<float, float> e0, e1;
        public float yBottom;
        public float S(Vector3 p) => Vector3.Dot(p - origin, axis);
        public float L(Vector3 p) => Vector3.Dot(p - origin, side);
        public Vector3 At(float s, float l, float y) { var p = origin + axis * s + side * l; p.y = y; return p; }
        // 切り口の線に沿った長さ（舌の横のように線が急な所でも点が重ならないように、壁の座標は l ではなくこれを使う）
        const float A0 = -9f, AStep = 0.002f; const int AN = 9001;
        float[][] arc;
        public System.Func<float, float> E(int face) => face == 0 ? e0 : e1;
        public float Slope(int face, float l) { var e = E(face); return (e(l + 0.002f) - e(l - 0.002f)) / 0.004f; }
        void Build()
        {
            arc = new float[2][];
            for (int face = 0; face < 2; face++)
            {
                var a = new float[AN]; var e = E(face);
                for (int i = 1; i < AN; i++) { float l0 = A0 + (i - 1) * AStep, l1 = l0 + AStep; float ds = e(l1) - e(l0); a[i] = a[i - 1] + Mathf.Sqrt(AStep * AStep + ds * ds); }
                arc[face] = a;
            }
        }
        public float U(int face, float l) { if (arc == null) Build(); float fi = Mathf.Clamp((l - A0) / AStep, 0f, AN - 1.001f); int i = (int)fi; return Mathf.Lerp(arc[face][i], arc[face][i + 1], fi - i); }
        public float Linv(int face, float u)
        {
            if (arc == null) Build(); var a = arc[face];
            int lo = 0, hi = AN - 1; if (u <= a[0]) return A0; if (u >= a[hi]) return A0 + hi * AStep;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (a[mid] <= u) lo = mid; else hi = mid; }
            return A0 + (lo + (u - a[lo]) / Mathf.Max(1e-9f, a[hi] - a[lo])) * AStep;
        }
    }

    // 開いた辺が切り口の面の上か見分ける許し幅。切り口の点は辺の上の直線補間なので、線が急に曲がる所（舌の肩）では面から少しずれる
    const float Tol = 0.015f;

    // mesh（ローカル）の開いた切り口をふさいで、ふたの三角形を capSub 番のサブメッシュに足す。足した三角形の数と、ふさげなかった辺の数を返す
    public static (int tris, int unresolved, string log) Cap(Mesh mesh, Transform tr, Frame f, int capSub, float rough, float maxArea, int seed)
    {
        var l2w = tr.localToWorldMatrix; var w2l = tr.worldToLocalMatrix;
        var V = new List<Vector3>(); mesh.GetVertices(V);
        var N = new List<Vector3>(); mesh.GetNormals(N); bool hasN = N.Count == V.Count;
        var UV = new List<Vector2>(); mesh.GetUVs(0, UV); bool hasUv = UV.Count == V.Count;
        var UV2 = new List<Vector2>(); mesh.GetUVs(1, UV2); bool hasUv2 = UV2.Count == V.Count;
        var subs = new List<int>[mesh.subMeshCount]; for (int s = 0; s < subs.Length; s++) subs[s] = new List<int>(mesh.GetTriangles(s));
        var W = V.Select(v => l2w.MultiplyPoint3x4(v)).ToList();

        // 同じ位置の頂点をまとめる（切った点は計算の向きで小数のごく下の桁がずれるので、丸めではなく 0.3mm 以内を同じ点に）
        var weld = Weld(W, 0.0003f);
        long EK(int a, int b) => ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
        int tFixed = FixTJunctions(W, weld, subs);
        var cnt = new Dictionary<long, int>();
        foreach (var sub in subs)
            for (int i = 0; i < sub.Count; i += 3)
            {
                int a = weld[sub[i]], b = weld[sub[i + 1]], c = weld[sub[i + 2]];
                if (a == b || b == c || a == c) continue;
                foreach (var k in new[] { EK(a, b), EK(b, c), EK(c, a) }) { cnt.TryGetValue(k, out var n); cnt[k] = n + 1; }
            }

        // 面からのずれ（線に直角の距離）
        float DN(Vector3 p) { float l = f.L(p), k = f.Slope(0, l); return Mathf.Abs(f.S(p) - f.e0(l)) / Mathf.Sqrt(1f + k * k); }
        float DF(Vector3 p) { float l = f.L(p), k = f.Slope(1, l); return Mathf.Abs(f.S(p) - f.e1(l)) / Mathf.Sqrt(1f + k * k); }
        float DB(Vector3 p) { float s = f.S(p), l = f.L(p); return (s < f.e0(l) - Tol || s > f.e1(l) + Tol) ? 99f : Mathf.Abs(p.y - f.yBottom); }
        // 面ごとの開いた辺
        var faceEdges = new List<(int a, int b)>[3] { new List<(int, int)>(), new List<(int, int)>(), new List<(int, int)>() };
        int unresolved = 0;
        foreach (var kv in cnt)
        {
            if (kv.Value != 1) continue;
            int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff);
            var pa = W[a]; var pb = W[b]; var pm = (pa + pb) * 0.5f;
            float[] d = { Mathf.Max(DN(pa), DN(pb), DN(pm)), Mathf.Max(DF(pa), DF(pb), DF(pm)), Mathf.Max(DB(pa), DB(pb), DB(pm)) };
            int best = -1; float bd = Tol * 2f;
            for (int k = 0; k < 3; k++) if (d[k] < bd) { bd = d[k]; best = k; }
            if (best < 0) continue;   // 切り口と関係ない開いた辺（もとから開いている所）はさわらない
            faceEdges[best].Add((a, b));
        }

        // 点：既存の点（weld id）＋新しく作る点（底の線）。新しい点は負の番号で持つ
        var newPts = new List<Vector3>();
        Vector3 P(int id) => id >= 0 ? W[id] : newPts[-id - 1];
        int AddNew(Vector3 p) { newPts.Add(p); return -newPts.Count; }

        // 壁の下はし（底の線）：壁ごとに角の点を l の順に並べて2つずつ組にし、その間を底の高さの線で結ぶ
        var bottomLines = new List<List<int>>[2] { new List<List<int>>(), new List<List<int>>() };
        var log = new List<string>();
        for (int wi = 0; wi < 2; wi++)
        {
            var deg = new Dictionary<int, int>();
            foreach (var (a, b) in faceEdges[wi]) { deg[a] = deg.TryGetValue(a, out var x) ? x + 1 : 1; deg[b] = deg.TryGetValue(b, out var y) ? y + 1 : 1; }
            var corners = deg.Where(kv => kv.Value == 1).Select(kv => kv.Key).Where(id => Mathf.Abs(P(id).y - f.yBottom) < Tol * 3f).OrderBy(id => f.L(P(id))).ToList();
            int odd = deg.Count(kv => kv.Value == 1) - corners.Count;
            if (odd != 0) { log.Add((wi == 0 ? "near" : "far") + ": chain ends not on floor " + odd); unresolved += odd; }
            System.Func<float, float> e = wi == 0 ? f.e0 : f.e1;
            for (int i = 0; i + 1 < corners.Count; i += 2)
            {
                int c0 = corners[i], c1 = corners[i + 1];
                float l0 = f.L(P(c0)), l1 = f.L(P(c1));
                int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(l1 - l0) / 0.35f));
                var line = new List<int> { c0 };
                for (int k = 1; k < n; k++) { float l = Mathf.Lerp(l0, l1, (float)k / n); line.Add(AddNew(f.At(e(l), l, f.yBottom))); }
                line.Add(c1);
                bottomLines[wi].Add(line);
            }
        }

        // 面ごとに輪を作って三角形に割る
        var capV = new List<Vector3>(); var capT = new List<int>(); var capF = new List<int>();
        int capTris = 0;
        int brokenLoops = 0;
        for (int face = 0; face < 3; face++)
        {
            var adj = new Dictionary<int, List<int>>();
            void Link(int a, int b) { if (!adj.TryGetValue(a, out var la)) adj[a] = la = new List<int>(); la.Add(b); if (!adj.TryGetValue(b, out var lb)) adj[b] = lb = new List<int>(); lb.Add(a); }
            foreach (var (a, b) in faceEdges[face]) Link(a, b);
            if (face < 2) { foreach (var line in bottomLines[face]) for (int k = 0; k + 1 < line.Count; k++) Link(line[k], line[k + 1]); }
            else { foreach (var wl in bottomLines) foreach (var line in wl) for (int k = 0; k + 1 < line.Count; k++) Link(line[k], line[k + 1]); }
            if (adj.Count == 0) continue;
            int bad = adj.Count(kv => kv.Value.Count != 2);
            if (bad > 0)
            {
                log.Add("face " + face + ": " + bad + " points not on a loop: " + string.Join(" ", adj.Where(kv => kv.Value.Count != 2).Take(8).Select(kv => { var p = P(kv.Key); return "deg" + kv.Value.Count + "@(" + p.x.ToString("F3") + "," + p.y.ToString("F3") + "," + p.z.ToString("F3") + ") s" + f.S(p).ToString("F3") + " l" + f.L(p).ToString("F3"); })));
                unresolved += bad;
            }
            // 輪をたどる
            var used = new HashSet<int>();
            foreach (var start in adj.Keys.ToList())
            {
                if (used.Contains(start) || adj[start].Count != 2) continue;
                var loop = new List<int> { start }; used.Add(start);
                int prev = start, cur = adj[start][0];
                while (cur != start)
                {
                    if (used.Contains(cur) || !adj.TryGetValue(cur, out var nb) || nb.Count != 2) { loop = null; break; }
                    loop.Add(cur); used.Add(cur);
                    int nx = nb[0] == prev ? nb[1] : nb[0]; prev = cur; cur = nx;
                }
                if (loop == null || loop.Count < 3) { brokenLoops++; unresolved++; continue; }
                capTris += TriangulateLoop(loop, face, f, P, capV, capT, capF, rough, maxArea, seed + face * 101);
            }
        }

        if (brokenLoops > 0) log.Add("broken loops " + brokenLoops);
        // ふたを足す（面ごとの平らな法線。頂点は三角形ごとに分ける）
        int baseI = V.Count;
        // UV・UV2（ライトマップ）は、いちばん近い元の点の値を借りる（焼き直すまでの間も、ふただけ真っ黒などにならないように）
        var ugrid = new Dictionary<Vector3Int, List<int>>(); const float ucell = 0.25f;
        if (hasUv || hasUv2) for (int i = 0; i < W.Count; i++) { var c = Vector3Int.FloorToInt(W[i] / ucell); if (!ugrid.TryGetValue(c, out var l)) ugrid[c] = l = new List<int>(); l.Add(i); }
        int Nearest(Vector3 p)
        {
            var c = Vector3Int.FloorToInt(p / ucell);
            for (int r = 1; r <= 8; r++)
            {
                int best = -1; float bd = float.MaxValue;
                for (int dx = -r; dx <= r; dx++) for (int dy = -r; dy <= r; dy++) for (int dz = -r; dz <= r; dz++)
                            if (ugrid.TryGetValue(new Vector3Int(c.x + dx, c.y + dy, c.z + dz), out var l))
                                foreach (var j in l) { float d = (W[j] - p).sqrMagnitude; if (d < bd) { bd = d; best = j; } }
                if (best >= 0) return best;
            }
            return 0;
        }
        for (int i = 0; i < capV.Count; i++)
        {
            V.Add(w2l.MultiplyPoint3x4(capV[i]));
            int nn = (hasUv || hasUv2) ? Nearest(capV[i]) : 0;
            if (hasUv) UV.Add(UV[nn]);
            if (hasUv2) UV2.Add(UV2[nn]);
        }
        for (int i = 0; i < capT.Count; i += 3)
        {
            var n = Vector3.Cross(capV[capT[i + 1]] - capV[capT[i]], capV[capT[i + 2]] - capV[capT[i]]).normalized;
            if (hasN) { }
            subs[capSub].Add(baseI + capT[i]); subs[capSub].Add(baseI + capT[i + 1]); subs[capSub].Add(baseI + capT[i + 2]);
        }
        if (hasN) { var nl = w2l; for (int i = 0; i < capV.Count; i++) N.Add(Vector3.up); }
        mesh.Clear();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(V);
        if (hasUv) mesh.SetUVs(0, UV);
        if (hasUv2) mesh.SetUVs(1, UV2);
        mesh.subMeshCount = subs.Length;
        for (int s = 0; s < subs.Length; s++) mesh.SetTriangles(subs[s], s);
        if (hasN)
        {
            // ふたの法線：三角形ごとの向きだと細い三角形ごとに陰影が変わって斜めの筋に見えた（2026-10-13 てじゃ「斜めのギザギザ感」）。
            // でこぼこなし（道）は切り口の面の向きそのもの（線の傾きから計算）、でこぼこあり（山）は同じ位置の三角形の向きの平均
            var sumN = new Dictionary<Vector3Int, Vector3>();
            Vector3Int Q(Vector3 p) => Vector3Int.RoundToInt(p * 5000f);
            if (rough > 0f)
                for (int i = 0; i < capT.Count; i += 3)
                {
                    var fn = Vector3.Cross(capV[capT[i + 1]] - capV[capT[i]], capV[capT[i + 2]] - capV[capT[i]]);
                    for (int k = 0; k < 3; k++) { var q = Q(capV[capT[i + k]]); sumN[q] = (sumN.TryGetValue(q, out var x) ? x : Vector3.zero) + fn; }
                }
            for (int i = 0; i < capV.Count; i++)
            {
                Vector3 nw;
                int face = capF[i];
                if (rough > 0f) nw = sumN[Q(capV[i])];
                else if (face == 2) nw = Vector3.up;
                else { float l = f.L(capV[i]); nw = (f.axis - f.Slope(face, l) * f.side) * (face == 0 ? 1f : -1f); }
                N[baseI + i] = w2l.MultiplyVector(nw).normalized;
            }
            mesh.SetNormals(N);
        }
        mesh.RecalculateBounds();
        return (capTris, unresolved, "tj " + tFixed + "; " + string.Join("; ", log));
    }

    // 輪を面の座標で多角形にして三角形に割り、capV/capT に足す（頂点は三角形ごとに分ける）
    static int TriangulateLoop(List<int> loop, int face, Frame f, System.Func<int, Vector3> P, List<Vector3> capV, List<int> capT, List<int> capF, float rough, float maxArea, int seed)
    {
        Vector2 To2(Vector3 p) => face < 2 ? new Vector2(f.U(face, f.L(p)), p.y) : new Vector2(f.L(p), f.S(p));
        var pts3 = loop.Select(P).ToList();
        var pts = pts3.Select(To2).ToList();
        // 重なった点・一直線上の点を整理（ほぼ同じ点だけ消す）
        for (int i = pts.Count - 1; i >= 0 && pts.Count > 3; i--)
        {
            int j = (i + 1) % pts.Count;
            if ((pts[i] - pts[j]).sqrMagnitude < 1e-10f) { pts.RemoveAt(i); pts3.RemoveAt(i); }
        }
        if (pts.Count < 3) return 0;
        float area = 0f; for (int i = 0; i < pts.Count; i++) { var a = pts[i]; var b = pts[(i + 1) % pts.Count]; area += a.x * b.y - b.x * a.y; }
        if (area < 0f) { pts.Reverse(); pts3.Reverse(); }
        int nb = pts.Count;
        var tris = EarClip(pts);
        var all2 = new List<Vector2>(pts); var all3 = new List<Vector3>(pts3);
        var isBoundary = new List<bool>(Enumerable.Repeat(true, nb));
        // 境界の辺（ひっくり返さない）
        var fixedE = new HashSet<long>();
        long K(int a, int b) => ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
        for (int i = 0; i < nb; i++) fixedE.Add(K(i, (i + 1) % nb));
        Delaunayize(all2, tris, fixedE);
        if (rough > 0f || maxArea > 0f)
        {
            for (int round = 0; round < 6; round++)
            {
                bool any = false;
                var next = new List<int>();
                for (int t = 0; t < tris.Count; t += 3)
                {
                    var a = all2[tris[t]]; var b = all2[tris[t + 1]]; var c = all2[tris[t + 2]];
                    float ar = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
                    if (maxArea > 0f && ar > maxArea)
                    {
                        int m = all2.Count; all2.Add((a + b + c) / 3f); all3.Add(Vector3.zero); isBoundary.Add(false);
                        next.AddRange(new[] { tris[t], tris[t + 1], m, tris[t + 1], tris[t + 2], m, tris[t + 2], tris[t], m });
                        any = true;
                    }
                    else next.AddRange(new[] { tris[t], tris[t + 1], tris[t + 2] });
                }
                tris = next;
                Delaunayize(all2, tris, fixedE);
                if (!any) break;
            }
        }
        // 3D に戻す。新しい点は面の上（でこぼこはふちから離れた所だけ）
        for (int i = nb; i < all2.Count; i++)
        {
            var q = all2[i];
            float dEdge = float.MaxValue;
            for (int k = 0; k < nb; k++) dEdge = Mathf.Min(dEdge, DistSeg(q, all2[k], all2[(k + 1) % nb]));
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(dEdge / 0.9f));
            float nz = (Mathf.PerlinNoise(q.x * 0.55f + seed * 0.37f, q.y * 0.55f + 11.3f) - 0.5f) * 2f;
            float nz2 = (Mathf.PerlinNoise(q.x * 1.3f + seed * 0.71f, q.y * 1.3f + 3.1f) - 0.5f) * 2f;
            float off = rough * fade * (nz * 0.75f + nz2 * 0.25f);
            if (face < 2) { float l = f.Linv(face, q.x); all3[i] = f.At(f.E(face)(l) + off, l, q.y); }
            else all3[i] = f.At(q.y, q.x, f.yBottom + Mathf.Abs(off) * 0.6f);
        }
        // 向き：手前の壁は谷の方（+axis）、奥の壁は -axis、底は上
        Vector3 want = face == 0 ? f.axis : face == 1 ? -f.axis : Vector3.up;
        int count = 0;
        for (int t = 0; t < tris.Count; t += 3)
        {
            var a = all3[tris[t]]; var b = all3[tris[t + 1]]; var c = all3[tris[t + 2]];
            var n = Vector3.Cross(b - a, c - a);
            if (tris[t] == tris[t + 1] || tris[t + 1] == tris[t + 2] || tris[t] == tris[t + 2]) continue;   // 面積0の三角形も残す（一直線に並んだ点のすき間を作らない）
            int bi = capV.Count;
            if (Vector3.Dot(n, want) >= 0f) { capV.Add(a); capV.Add(b); capV.Add(c); }
            else { capV.Add(a); capV.Add(c); capV.Add(b); }
            capT.Add(bi); capT.Add(bi + 1); capT.Add(bi + 2); capF.Add(face); capF.Add(face); capF.Add(face); count++;
        }
        return count;
    }

    // T字のすき間：開いた辺の途中に、別の開いた辺の端の点が乗っている所（切った時の「かけら」どうしで点の数が合わない所）。
    // その辺を持つ三角形を、乗っている点で割って、辺どうしがぴったり合うようにする（見た目は変わらない）
    static int FixTJunctions(List<Vector3> W, int[] weld, List<int>[] subs)
    {
        long EK(int a, int b) => ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
        int total = 0;
        for (int pass = 0; pass < 4; pass++)
        {
            var cnt = new Dictionary<long, int>();
            foreach (var sub in subs)
                for (int i = 0; i < sub.Count; i += 3)
                {
                    int a = weld[sub[i]], b = weld[sub[i + 1]], c = weld[sub[i + 2]];
                    if (a == b || b == c || a == c) continue;
                    foreach (var k in new[] { EK(a, b), EK(b, c), EK(c, a) }) { cnt.TryGetValue(k, out var n); cnt[k] = n + 1; }
                }
            var openV = new HashSet<int>();
            foreach (var kv in cnt) if (kv.Value == 1) { openV.Add((int)(kv.Key >> 32)); openV.Add((int)(kv.Key & 0xffffffff)); }
            if (openV.Count == 0) break;
            const float cell = 0.05f; var grid = new Dictionary<Vector3Int, List<int>>();
            foreach (var v in openV) { var c = Vector3Int.FloorToInt(W[v] / cell); if (!grid.TryGetValue(c, out var l)) grid[c] = l = new List<int>(); l.Add(v); }
            int fixedNow = 0;
            foreach (var sub in subs)
            {
                var outT = new List<int>(sub.Count);
                for (int i = 0; i < sub.Count; i += 3)
                {
                    int[] tv = { sub[i], sub[i + 1], sub[i + 2] };
                    bool done = false;
                    for (int e = 0; e < 3 && !done; e++)
                    {
                        int ia = tv[e], ib = tv[(e + 1) % 3], ic = tv[(e + 2) % 3];
                        int a = weld[ia], b = weld[ib];
                        if (a == b || !cnt.TryGetValue(EK(a, b), out var n) || n != 1) continue;
                        var pa = W[a]; var pb = W[b]; var ab = pb - pa; float len2 = ab.sqrMagnitude; if (len2 < 1e-10f) continue;
                        var lo = Vector3Int.FloorToInt(Vector3.Min(pa, pb) / cell) - Vector3Int.one; var hi = Vector3Int.FloorToInt(Vector3.Max(pa, pb) / cell) + Vector3Int.one;
                        if ((hi - lo).sqrMagnitude > 40000) continue;
                        var on = new List<(float t, int v)>();
                        for (int x = lo.x; x <= hi.x; x++) for (int y = lo.y; y <= hi.y; y++) for (int z = lo.z; z <= hi.z; z++)
                                    if (grid.TryGetValue(new Vector3Int(x, y, z), out var l))
                                        foreach (var v in l)
                                        {
                                            if (v == a || v == b) continue;
                                            float t = Vector3.Dot(W[v] - pa, ab) / len2; if (t <= 0.0005f || t >= 0.9995f) continue;
                                            if ((pa + ab * t - W[v]).sqrMagnitude > 0.0005f * 0.0005f) continue;
                                            on.Add((t, v));
                                        }
                        if (on.Count == 0) continue;
                        on.Sort((p, q) => p.t.CompareTo(q.t));
                        // ia → (乗っている点) → ib の扇形に割る（向きはそのまま）
                        int prev = ia;
                        foreach (var (t, v) in on) { outT.Add(prev); outT.Add(v); outT.Add(ic); prev = v; }
                        outT.Add(prev); outT.Add(ib); outT.Add(ic);
                        done = true; fixedNow++;
                    }
                    if (!done) outT.AddRange(tv);
                }
                sub.Clear(); sub.AddRange(outT);
            }
            total += fixedNow;
            if (fixedNow == 0) break;
        }
        return total;
    }

    // 距離 tol 以内の点を同じ番号に（格子で近所だけ調べる）
    public static int[] Weld(List<Vector3> W, float tol)
    {
        float cell = tol * 4f; var grid = new Dictionary<Vector3Int, List<int>>(); var weld = new int[W.Count]; float t2 = tol * tol;
        for (int i = 0; i < W.Count; i++)
        {
            var c = Vector3Int.FloorToInt(W[i] / cell); int found = -1;
            for (int dx = -1; dx <= 1 && found < 0; dx++) for (int dy = -1; dy <= 1 && found < 0; dy++) for (int dz = -1; dz <= 1 && found < 0; dz++)
                    if (grid.TryGetValue(new Vector3Int(c.x + dx, c.y + dy, c.z + dz), out var l))
                        foreach (var j in l) if ((W[j] - W[i]).sqrMagnitude <= t2) { found = j; break; }
            if (found < 0) { found = i; if (!grid.TryGetValue(c, out var lc)) grid[c] = lc = new List<int>(); lc.Add(i); }
            weld[i] = found;
        }
        return weld;
    }

    static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-12f, ab.sqrMagnitude));
        return (p - (a + ab * t)).magnitude;
    }

    // 反時計回りの単純な多角形を耳切りで三角形に（反時計回りの三角形の番号）
    static List<int> EarClip(List<Vector2> p)
    {
        var idx = Enumerable.Range(0, p.Count).ToList(); var outT = new List<int>();
        float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        bool Inside(Vector2 q, Vector2 a, Vector2 b, Vector2 c) => Cross(a, b, q) >= 0f && Cross(b, c, q) >= 0f && Cross(c, a, q) >= 0f;
        int guard = 0, start = 0;
        while (idx.Count > 3 && guard++ < 100000)
        {
            bool cut = false;
            // 見つかった耳から順に切る（点が多い輪でも速く）。細い三角形はあとで Delaunayize がならす
            for (int k = 0; k < idx.Count && !cut; k++)
            {
                int i = (start + k) % idx.Count;
                int i0 = idx[(i - 1 + idx.Count) % idx.Count], i1 = idx[i], i2 = idx[(i + 1) % idx.Count];
                var a = p[i0]; var b = p[i1]; var c = p[i2];
                if (Cross(a, b, c) <= 1e-9f) continue;
                bool ok = true;
                for (int j = 0; j < idx.Count && ok; j++)
                {
                    int q = idx[j]; if (q == i0 || q == i1 || q == i2) continue;
                    if (Inside(p[q], a, b, c)) ok = false;
                }
                if (!ok) continue;
                outT.Add(i0); outT.Add(i1); outT.Add(i2); idx.RemoveAt(i); cut = true; start = i;
            }
            if (!cut)
            {
                // 一直線の点が残った時など：一番凸の点を無理に切る
                int i1 = 0; float best = float.MinValue;
                for (int i = 0; i < idx.Count; i++) { float cr = Cross(p[idx[(i - 1 + idx.Count) % idx.Count]], p[idx[i]], p[idx[(i + 1) % idx.Count]]); if (cr > best) { best = cr; i1 = i; } }
                int a0 = idx[(i1 - 1 + idx.Count) % idx.Count], a2 = idx[(i1 + 1) % idx.Count];
                outT.Add(a0); outT.Add(idx[i1]); outT.Add(a2);   // 面積0でも残す（点を捨てるとすき間になる）
                idx.RemoveAt(i1);
            }
        }
        if (idx.Count == 3) { outT.Add(idx[0]); outT.Add(idx[1]); outT.Add(idx[2]); }
        return outT;
    }

    // 境界の辺は固定して、ほかの辺を「外接円に他の点が入らない」ようにひっくり返す（細い三角形を減らす）
    static void Delaunayize(List<Vector2> p, List<int> tris, HashSet<long> fixedE)
    {
        long K(int a, int b) => ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
        for (int pass = 0; pass < 60; pass++)
        {
            var edgeTri = new Dictionary<long, List<int>>();
            for (int t = 0; t < tris.Count; t += 3)
                for (int e = 0; e < 3; e++)
                {
                    long k = K(tris[t + e], tris[t + (e + 1) % 3]);
                    if (!edgeTri.TryGetValue(k, out var l)) edgeTri[k] = l = new List<int>();
                    l.Add(t);
                }
            bool flipped = false; var touched = new HashSet<int>();
            foreach (var kv in edgeTri)
            {
                if (kv.Value.Count != 2 || fixedE.Contains(kv.Key)) continue;
                int t1 = kv.Value[0], t2 = kv.Value[1];
                if (touched.Contains(t1) || touched.Contains(t2)) continue;
                int a = (int)(kv.Key >> 32), b = (int)(kv.Key & 0xffffffff);
                int c = Other(tris, t1, a, b), d = Other(tris, t2, a, b);
                if (c < 0 || d < 0) continue;
                // t1 の向き（反時計回り）で a,b,c の順にそろえる
                int ta = -1, tb = -1;
                for (int e = 0; e < 3; e++) if (tris[t1 + e] == a && tris[t1 + (e + 1) % 3] == b) { ta = a; tb = b; }
                if (ta < 0) { ta = b; tb = a; }
                if (!InCircle(p[ta], p[tb], p[c], p[d])) continue;
                // 新しい辺 c-d が両方の三角形を反時計回りに保てるか（凸の四角形の時だけ）
                if (Cross2(p[c], p[d], p[ta]) == 0f || Cross2(p[d], p[c], p[tb]) == 0f) continue;
                if (Orient(p[ta], p[d], p[c]) <= 0f || Orient(p[tb], p[c], p[d]) <= 0f) continue;
                tris[t1] = ta; tris[t1 + 1] = d; tris[t1 + 2] = c;
                tris[t2] = tb; tris[t2 + 1] = c; tris[t2 + 2] = d;
                touched.Add(t1); touched.Add(t2); flipped = true;
            }
            if (!flipped) break;
        }
    }
    static int Other(List<int> tris, int t, int a, int b) { for (int e = 0; e < 3; e++) { int v = tris[t + e]; if (v != a && v != b) return v; } return -1; }
    static float Orient(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    static float Cross2(Vector2 a, Vector2 b, Vector2 c) => Orient(a, b, c);
    // a,b,c が反時計回りの時、d が外接円の中なら true
    static bool InCircle(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        double ax = a.x - d.x, ay = a.y - d.y, bx = b.x - d.x, by = b.y - d.y, cx = c.x - d.x, cy = c.y - d.y;
        double det = (ax * ax + ay * ay) * (bx * cy - cx * by) - (bx * bx + by * by) * (ax * cy - cx * ay) + (cx * cx + cy * cy) * (ax * by - bx * ay);
        return det > 1e-12;
    }
}
