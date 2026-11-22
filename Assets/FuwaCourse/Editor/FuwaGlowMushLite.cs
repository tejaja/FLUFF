using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// チェックポイントの光るキノコの丸太を軽くする（2026-11-22 てじゃ「チェックポイント一回みなおそか」→「見た目の雰囲気はべつにいい、軽量化だけ」）。
// 丸太1つ 9236 三角形（傘6本 4896／柄6本 2016／コケ 1560／丸太と切り口 764）× 全39か所で約63万。見た目はそのままで面の数だけ減らす：
//  ・傘：FuwaGlowCapRebuild の回転体の細かさを下げる（24×10 → 20×3、遠くの版 10×3。12×4 はふちの角が見えた）
//  ・柄：元の筒から芯の線（何か所かの中心）と太さを測って、10角×6節の筒で作り直す（元は 12角×14節）
//  ・コケ：丸太の軸のまわりの網目（角度×長さ）で、丸太の芯から外へ光線を飛ばして元のコケの外側の高さを測り、
//    コケがある所だけ面を張る（ふちはマーチングスクエアで高さ0の線をなぞる）。ふちは元と同じく段になるように、丸太の中まで下ろす「すそ」をつける
//  ・丸太本体・切り口・年輪はそのまま
// 元の細かい形は Generated/CheckpointHi/*_Hi に取っておき、作り直しはいつもそこから作る（何回押しても同じ結果）。
// 左右反対の版（*_Mirror_*）は元も X 反転と完全に一致していたので X 反転で作る。2–5 の鏡の島用の *_Mir（FuwaStage2/Meshes/Course2_5）も同じく作り直す。
// メッシュは同じアセットに入れ直す（シーン・プレハブの参照はそのまま）。このあと「色ちがいの材質をまとめる」を押し直す（VC_ の写しが元から作り直される）
public static class FuwaGlowMushLite
{
    const string Gen = "Assets/FuwaCourse/Generated/";
    const string Bak = Gen + "CheckpointHi/";
    const string MirDir = "Assets/FuwaStage2/Meshes/Course2_5/";
    public static int StemSides = 10, StemSegs = 6;
    public static float MossStepDeg = 14f, MossStepX = 0.055f;

    [MenuItem("FuwaCourse/済み（押す前に相談）/チェックポイントの光るキノコを軽くする")]
    static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new List<string>();
        if (!AssetDatabase.IsValidFolder(Bak.TrimEnd('/'))) AssetDatabase.CreateFolder(Gen.TrimEnd('/'), "CheckpointHi");
        foreach (var n in new[] { "GlowCaps_Fix", "GlowCaps_Mirror_Fix", "Far/GlowCaps_Fix_Far", "Far/GlowCaps_Mirror_Fix_Far", "GlowStems_Fix", "GlowStems_Mirror_Fix", "Base_Lite", "Base_Mirror_Lite" })
        {
            string b = Bak + n.Replace("Far/", "") + "_Hi.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(b) == null) { AssetDatabase.CopyAsset(Gen + n + ".asset", b); log.Add("backup " + b); }
        }

        log.Add(FuwaGlowCapRebuild.Run());

        var stemsHi = AssetDatabase.LoadAssetAtPath<Mesh>(Bak + "GlowStems_Fix_Hi.asset");
        var stems = BuildStems(stemsHi, out int stemCount);
        Put(stems, Gen + "GlowStems_Fix.asset", false); Put(stems, Gen + "GlowStems_Mirror_Fix.asset", true);
        log.Add($"stems: {stemCount} 本 {stemsHi.triangles.Length / 3} → {stems.triangles.Length / 3}");
        Object.DestroyImmediate(stems);

        var baseHi = AssetDatabase.LoadAssetAtPath<Mesh>(Bak + "Base_Lite_Hi.asset");
        var bs = BuildBase(baseHi, out string mossInfo);
        Put(bs, Gen + "Base_Lite.asset", false); Put(bs, Gen + "Base_Mirror_Lite.asset", true);
        log.Add($"base: {baseHi.triangles.Length / 3} → {bs.triangles.Length / 3} ({mossInfo})");
        Object.DestroyImmediate(bs);

        // 2–5 の鏡の島の _Mir（自分の座標の X 反転）
        foreach (var n in new[] { "GlowCaps_Fix", "GlowCaps_Mirror_Fix", "Far/GlowCaps_Fix_Far", "Far/GlowCaps_Mirror_Fix_Far", "GlowStems_Fix", "GlowStems_Mirror_Fix", "Base_Lite", "Base_Mirror_Lite" })
        {
            string mp = MirDir + n.Replace("Far/", "") + "_Mir.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(mp) == null) continue;
            Put(AssetDatabase.LoadAssetAtPath<Mesh>(Gen + n + ".asset"), mp, true);
            log.Add("mir " + mp);
        }
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    // src の中身を dst のアセットに入れ直す（flip なら X 反転して面の向きも戻す）
    static void Put(Mesh src, string dstPath, bool flip)
    {
        var dst = AssetDatabase.LoadAssetAtPath<Mesh>(dstPath);
        if (dst == null) { Debug.LogWarning("missing " + dstPath); return; }
        var v = src.vertices; var n = src.normals; var t = src.tangents;
        if (flip)
        {
            for (int i = 0; i < v.Length; i++) v[i].x = -v[i].x;
            for (int i = 0; i < n.Length; i++) n[i].x = -n[i].x;
            for (int i = 0; i < t.Length; i++) { t[i].x = -t[i].x; t[i].w = -t[i].w; }
        }
        var uv = src.uv; var uv2 = src.uv2; var col = src.colors;
        string name = dst.name;
        dst.Clear(false);
        dst.indexFormat = src.indexFormat;
        dst.SetVertices(v);
        if (n.Length > 0) dst.SetNormals(n);
        if (t.Length > 0) dst.SetTangents(t);
        if (uv.Length > 0) dst.SetUVs(0, uv);
        if (uv2.Length > 0) dst.SetUVs(1, uv2);
        if (col.Length > 0) dst.SetColors(col);
        dst.subMeshCount = src.subMeshCount;
        for (int s = 0; s < src.subMeshCount; s++)
        {
            var tri = src.GetTriangles(s);
            if (flip) for (int k = 0; k < tri.Length; k += 3) { int tmp = tri[k + 1]; tri[k + 1] = tri[k + 2]; tri[k + 2] = tmp; }
            dst.SetTriangles(tri, s);
        }
        dst.name = name;
        dst.RecalculateBounds();
        EditorUtility.SetDirty(dst);
    }

    // ===== 柄 =====
    static List<List<Vector3>> Components(Mesh m, int sub)
    {
        var v = m.vertices; var t = sub < 0 ? m.triangles : m.GetTriangles(sub); int n = v.Length;
        var weld = new int[n]; var key = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < n; i++) { var k = Vector3Int.RoundToInt(v[i] * 10000f); if (!key.TryGetValue(k, out var w)) { w = i; key[k] = i; } weld[i] = w; }
        var par = Enumerable.Range(0, n).ToArray();
        int Find(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
        for (int i = 0; i < t.Length; i += 3) { int a = Find(weld[t[i]]), b = Find(weld[t[i + 1]]), c = Find(weld[t[i + 2]]); par[a] = b; par[Find(b)] = Find(c); }
        var used = new HashSet<int>(t.Select(i => weld[i]));
        return used.GroupBy(i => Find(i)).Select(g => g.Select(i => v[i]).ToList()).ToList();
    }

    static Vector3 MainAxis(List<Vector3> pts, out Vector3 c)
    {
        c = Vector3.zero; foreach (var p in pts) c += p; c /= pts.Count;
        var C = new float[3, 3];
        foreach (var p in pts) { var d = p - c; for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) C[i, j] += d[i] * d[j]; }
        Vector3 e = new Vector3(0.2f, 1f, 0.1f).normalized;
        for (int k = 0; k < 100; k++) e = new Vector3(C[0, 0] * e.x + C[0, 1] * e.y + C[0, 2] * e.z, C[1, 0] * e.x + C[1, 1] * e.y + C[1, 2] * e.z, C[2, 0] * e.x + C[2, 1] * e.y + C[2, 2] * e.z).normalized;
        return e;
    }

    static Mesh BuildStems(Mesh src, out int count)
    {
        var comps = Components(src, -1).Where(g => g.Count > 8).ToList();
        count = comps.Count;
        var outV = new List<Vector3>(); var outT = new List<int>();
        int S = StemSides, N = StemSegs;
        foreach (var pts in comps)
        {
            var e = MainAxis(pts, out var c); if (e.y < 0f) e = -e;
            float tmin = pts.Min(p => Vector3.Dot(p - c, e)), tmax = pts.Max(p => Vector3.Dot(p - c, e));
            float h = (tmax - tmin) / N;
            // 節ごとの中心（軸に直角なずれだけ近くの点で平均。端で片側だけになっても長さはずれない）
            var cen = new Vector3[N + 1];
            for (int k = 0; k <= N; k++)
            {
                float tk = tmin + h * k; Vector3 q = Vector3.zero; float ws = 0f;
                foreach (var p in pts)
                {
                    float t = Vector3.Dot(p - c, e); float w = Mathf.Max(0f, 1f - Mathf.Abs(t - tk) / h); if (w <= 0f) continue;
                    var d = p - c - e * t; q += d * w; ws += w;
                }
                cen[k] = c + e * tk + (ws > 0f ? q / ws : Vector3.zero);
            }
            var tan = new Vector3[N + 1];
            for (int k = 0; k <= N; k++) tan[k] = (cen[Mathf.Min(N, k + 1)] - cen[Mathf.Max(0, k - 1)]).normalized;
            // 太さ：その節の近くの点の、芯の線までの距離の平均
            var rad = new float[N + 1];
            for (int k = 0; k <= N; k++)
            {
                float tk = tmin + h * k, rs = 0f, ws = 0f;
                foreach (var p in pts)
                {
                    float t = Vector3.Dot(p - c, e); float w = Mathf.Max(0f, 1f - Mathf.Abs(t - tk) / (h * 0.6f)); if (w <= 0f) continue;
                    var d = p - cen[k]; d -= Vector3.Dot(d, tan[k]) * tan[k]; rs += d.magnitude * w; ws += w;
                }
                rad[k] = ws > 0f ? rs / ws : 0.01f;
            }
            // 筒（ねじれないように前の節の向きを引き継ぐ）
            int b0 = outV.Count;
            Vector3 nrm = Vector3.Cross(tan[0], Mathf.Abs(tan[0].y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            for (int k = 0; k <= N; k++)
            {
                nrm = (nrm - Vector3.Dot(nrm, tan[k]) * tan[k]).normalized; var bin = Vector3.Cross(tan[k], nrm);
                for (int a = 0; a < S; a++) { float th = 2f * Mathf.PI * a / S; outV.Add(cen[k] + (nrm * Mathf.Cos(th) + bin * Mathf.Sin(th)) * rad[k]); }
            }
            int t0 = outT.Count;
            for (int k = 0; k < N; k++)
                for (int a = 0; a < S; a++)
                {
                    int p0 = b0 + k * S + a, p1 = b0 + k * S + (a + 1) % S, q0 = p0 + S, q1 = p1 + S;
                    outT.Add(p0); outT.Add(q0); outT.Add(q1); outT.Add(p0); outT.Add(q1); outT.Add(p1);
                }
            // 外向きにそろえる
            var n0 = Vector3.Cross(outV[outT[t0 + 1]] - outV[outT[t0]], outV[outT[t0 + 2]] - outV[outT[t0]]);
            if (Vector3.Dot(n0, outV[outT[t0]] - cen[0]) < 0f)
                for (int i = t0; i < outT.Count; i += 3) { int tmp = outT[i + 1]; outT[i + 1] = outT[i + 2]; outT[i + 2] = tmp; }
        }
        var m = new Mesh(); m.SetVertices(outV); m.SetTriangles(outT, 0); m.RecalculateNormals();
        Unwrapping.GenerateSecondaryUVSet(m); m.RecalculateBounds();
        return m;
    }

    // ===== コケ =====
    static bool Ray(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c, out float t)
    {
        t = 0f; var e1 = b - a; var e2 = c - a; var p = Vector3.Cross(d, e2); float det = Vector3.Dot(e1, p);
        if (Mathf.Abs(det) < 1e-9f) return false;
        float inv = 1f / det; var s = o - a; float u = Vector3.Dot(s, p) * inv; if (u < 0f || u > 1f) return false;
        var q = Vector3.Cross(s, e1); float w = Vector3.Dot(d, q) * inv; if (w < 0f || u + w > 1f) return false;
        t = Vector3.Dot(e2, q) * inv; return t > 0f;
    }
    static float MaxHit(Vector3 o, Vector3 d, Vector3[] v, int[] tri)
    {
        float best = -1f;
        for (int i = 0; i < tri.Length; i += 3) if (Ray(o, d, v[tri[i]], v[tri[i + 1]], v[tri[i + 2]], out float t) && t > best) best = t;
        return best;
    }

    // 丸太：サブメッシュ 0 樹皮 / 1 切り口 / 2 コケ / 3 年輪。コケだけ作り直す
    static Mesh BuildBase(Mesh src, out string info)
    {
        var v = src.vertices; var barkT = src.GetTriangles(0); var mossT = src.GetTriangles(2);
        var barkPts = barkT.Distinct().Select(i => v[i]).ToList();
        var ax = MainAxis(barkPts, out var c);
        var up = (Vector3.up - ax * ax.y).normalized; var side = Vector3.Cross(ax, up);
        Vector3 Dir(float th) => up * Mathf.Cos(th) + side * Mathf.Sin(th);

        var mossPts = mossT.Distinct().Select(i => v[i]).ToList();
        float thMin = float.MaxValue, thMax = float.MinValue, xMin = float.MaxValue, xMax = float.MinValue;
        foreach (var p in mossPts)
        {
            var d = p - c; float x = Vector3.Dot(d, ax); var q = d - ax * x; float th = Mathf.Atan2(Vector3.Dot(q, side), Vector3.Dot(q, up));
            thMin = Mathf.Min(thMin, th); thMax = Mathf.Max(thMax, th); xMin = Mathf.Min(xMin, x); xMax = Mathf.Max(xMax, x);
        }
        float dTh = MossStepDeg * Mathf.Deg2Rad, dX = MossStepX;
        // コケの範囲を 1 マス広げて、外側が必ず「コケなし」になるように。マスの数は範囲に合わせて割り切る
        int nT = Mathf.CeilToInt((thMax - thMin) / dTh) + 2, nX = Mathf.CeilToInt((xMax - xMin) / dX) + 2;
        float th0 = (thMin + thMax) * 0.5f - dTh * nT * 0.5f, x0 = (xMin + xMax) * 0.5f - dX * nX * 0.5f;
        float barkMean = barkPts.Average(p => { var d = p - c; return (d - ax * Vector3.Dot(d, ax)).magnitude; });

        var H = new float[nT + 1, nX + 1]; var Rout = new float[nT + 1, nX + 1]; var Rbark = new float[nT + 1, nX + 1];
        for (int i = 0; i <= nT; i++)
            for (int j = 0; j <= nX; j++)
            {
                float th = th0 + dTh * i, x = x0 + dX * j; var o = c + ax * x; var d = Dir(th);
                float rb = MaxHit(o, d, v, barkT); if (rb < 0f) rb = barkMean;
                float rm = MaxHit(o, d, v, mossT);
                Rbark[i, j] = rb; Rout[i, j] = rm;
                H[i, j] = rm > 0f ? rm - rb : -0.02f;
            }

        // 網目の点：コケの上の点（格子）、ふちの点（辺の上、上と下＝すそ）
        var mv = new List<Vector3>(); var mt = new List<int>(); var muv = new List<Vector2>();
        var gridIdx = new Dictionary<(int, int), int>(); var edgeTop = new Dictionary<(int, int, int, int), int>(); var edgeLow = new Dictionary<(int, int, int, int), int>();
        Vector3 P(float th, float x, float r) => c + ax * x + Dir(th) * r;
        int Grid(int i, int j)
        {
            if (gridIdx.TryGetValue((i, j), out var k)) return k;
            k = mv.Count; float th = th0 + dTh * i, x = x0 + dX * j; mv.Add(P(th, x, Rout[i, j])); muv.Add(new Vector2(th, x)); gridIdx[(i, j)] = k; return k;
        }
        // ふち：中のかど a と外のかど b の間で高さ 0 になる所。上の点はコケの外側の高さ（中のかどの高さ）、下の点は丸太の中へ
        (int top, int low) Edge(int ia, int ja, int ib, int jb)
        {
            var key = (ia, ja, ib, jb);
            if (edgeTop.TryGetValue(key, out var tp)) return (tp, edgeLow[key]);
            float ha = H[ia, ja], hb = H[ib, jb]; float s = Mathf.Clamp(ha / (ha - hb), 0.05f, 0.95f);
            float th = th0 + dTh * Mathf.Lerp(ia, ib, s), x = x0 + dX * Mathf.Lerp(ja, jb, s);
            float rb = Mathf.Lerp(Rbark[ia, ja], Rbark[ib, jb], s);
            float rTop = Mathf.Max(rb + 0.002f, Rout[ia, ja] - 0.35f * ha);   // ふちは少し丸める（中のかどより少し低く）
            tp = mv.Count; mv.Add(P(th, x, rTop)); muv.Add(new Vector2(th, x));
            int lw = mv.Count; mv.Add(P(th, x, rb - 0.006f)); muv.Add(new Vector2(th, x));
            edgeTop[key] = tp; edgeLow[key] = lw; return (tp, lw);
        }
        void Tri(int a, int b, int d)
        {
            // 外向き（丸太の芯から外）にそろえる
            var n = Vector3.Cross(mv[b] - mv[a], mv[d] - mv[a]); var mid = (mv[a] + mv[b] + mv[d]) / 3f; var r = mid - c; r -= ax * Vector3.Dot(r, ax);
            if (Vector3.Dot(n, r) >= 0f) { mt.Add(a); mt.Add(b); mt.Add(d); } else { mt.Add(a); mt.Add(d); mt.Add(b); }
        }
        void SkirtTri(int a, int b, int d, Vector3 outward)
        {
            var n = Vector3.Cross(mv[b] - mv[a], mv[d] - mv[a]);
            if (Vector3.Dot(n, outward) >= 0f) { mt.Add(a); mt.Add(b); mt.Add(d); } else { mt.Add(a); mt.Add(d); mt.Add(b); }
        }
        int cells = 0;
        for (int i = 0; i < nT; i++)
            for (int j = 0; j < nX; j++)
            {
                var cs = new[] { (i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1) };
                bool[] ins = cs.Select(q => H[q.Item1, q.Item2] > 0f).ToArray();
                if (!ins.Any(b => b)) continue;
                cells++;
                // かどを順に回って、中のかど・ふちの点を並べる（ふちからふちへ進む所がコケの外周）
                var poly = new List<int>(); var rimSeg = new List<(int, int, int, int)>();
                int lastEdgeTop = -1, lastEdgeLow = -1;
                for (int k = 0; k < 4; k++)
                {
                    var a = cs[k]; var b = cs[(k + 1) % 4];
                    if (ins[k]) poly.Add(Grid(a.Item1, a.Item2));
                    if (ins[k] != ins[(k + 1) % 4])
                    {
                        var e = ins[k] ? Edge(a.Item1, a.Item2, b.Item1, b.Item2) : Edge(b.Item1, b.Item2, a.Item1, a.Item2);
                        poly.Add(e.top);
                        if (ins[k]) { lastEdgeTop = e.top; lastEdgeLow = e.low; }          // 外へ出た
                        else if (lastEdgeTop >= 0) { rimSeg.Add((lastEdgeTop, lastEdgeLow, e.top, e.low)); lastEdgeTop = -1; }   // 中へ戻った
                        else { rimSeg.Add((-1, -1, e.top, e.low)); }                         // 最初のかどが外：あとで最後の「外へ出た」とつなぐ
                    }
                }
                for (int k = 0; k < rimSeg.Count; k++) if (rimSeg[k].Item1 < 0) rimSeg[k] = (lastEdgeTop, lastEdgeLow, rimSeg[k].Item3, rimSeg[k].Item4);
                for (int k = 1; k + 1 < poly.Count; k++) Tri(poly[0], poly[k], poly[k + 1]);
                // すそ：外周の2点から丸太の中へ。向きはマスの中心→外
                Vector3 cellMid = P(th0 + dTh * (i + 0.5f), x0 + dX * (j + 0.5f), Rout[i, j] > 0f ? Rout[i, j] : barkMean);
                foreach (var (at, al, bt, bl) in rimSeg)
                {
                    if (at < 0) continue;
                    var mid = (mv[at] + mv[bt]) * 0.5f; var outward = mid - cellMid;
                    // 中のかど（コケ側）の重心から離れる向き
                    Vector3 inC = Vector3.zero; int nIn = 0; for (int k = 0; k < 4; k++) if (ins[k]) { inC += mv[Grid(cs[k].Item1, cs[k].Item2)]; nIn++; }
                    if (nIn > 0) outward = mid - inC / nIn;
                    SkirtTri(at, bt, bl, outward); SkirtTri(at, bl, al, outward);
                }
            }

        // 丸太の他のサブメッシュはそのまま（コケの元の点は使わないので詰める）
        var keepSubs = new[] { 0, 1, 3 };
        var remap = new Dictionary<int, int>(); var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nuv = new List<Vector2>();
        var srcN = src.normals; var srcUV = src.uv;
        var subs = new List<int>[4];
        foreach (int s in keepSubs)
        {
            subs[s] = new List<int>();
            foreach (int i in src.GetTriangles(s))
            {
                if (!remap.TryGetValue(i, out int k)) { k = nv.Count; remap[i] = k; nv.Add(v[i]); nn.Add(srcN.Length > 0 ? srcN[i] : Vector3.up); nuv.Add(srcUV.Length > 0 ? srcUV[i] : Vector2.zero); }
                subs[s].Add(k);
            }
        }
        // コケの法線はコケだけで計算
        var tmp = new Mesh(); tmp.SetVertices(mv); tmp.SetTriangles(mt, 0); tmp.RecalculateNormals(); var mn = tmp.normals; Object.DestroyImmediate(tmp);
        int baseM = nv.Count; nv.AddRange(mv); nn.AddRange(mn);
        // コケの UV：元のコケの UV の範囲に合わせて（角度・長さ）を並べる
        nuv.AddRange(muv.Select(q => new Vector2((q.x - thMin) / Mathf.Max(1e-4f, thMax - thMin), (q.y - xMin) / Mathf.Max(1e-4f, xMax - xMin))));
        subs[2] = mt.Select(i => i + baseM).ToList();

        var m = new Mesh(); m.SetVertices(nv); m.SetNormals(nn); m.SetUVs(0, nuv);
        m.subMeshCount = 4; for (int s = 0; s < 4; s++) m.SetTriangles(subs[s], s);
        m.RecalculateTangents(); Unwrapping.GenerateSecondaryUVSet(m); m.RecalculateBounds();
        info = $"moss {mossT.Length / 3} → {mt.Count / 3}, grid {nT}x{nX}, cells {cells}";
        return m;
    }
}
