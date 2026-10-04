using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// メッシュから「箱の形の範囲」をくり抜く（三角形を範囲の面でぴったり切る）。崩落した道・崖のくぼみ作り用。
// 範囲：道に沿った向き axis で s0〜s1、高さ yBottom より上、横方向は |lat| < halfWidth（すべてワールド座標）。
// 範囲の外側の部分だけを残す。頂点の法線・UV・UV2・色は線形補間する。接線は捨てる（シェーダーで使っていない）。
public static class FuwaPitCut
{
    struct V { public Vector3 p, n; public Vector2 uv, uv2; public Color c; }

    static V Lerp(V a, V b, float t)
    {
        return new V { p = Vector3.Lerp(a.p, b.p, t), n = Vector3.Lerp(a.n, b.n, t).normalized, uv = Vector2.Lerp(a.uv, b.uv, t), uv2 = Vector2.Lerp(a.uv2, b.uv2, t), c = Color.Lerp(a.c, b.c, t) };
    }

    // 半空間 f(p) >= 0 で多角形を切る
    static List<V> Clip(List<V> poly, System.Func<Vector3, float> f)
    {
        var o = new List<V>(); int n = poly.Count; if (n == 0) return o;
        for (int i = 0; i < n; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % n];
            float fa = f(a.p), fb = f(b.p);
            if (fa >= 0) o.Add(a);
            if ((fa >= 0) != (fb >= 0)) o.Add(Lerp(a, b, fa / (fa - fb)));
        }
        return o;
    }

    public static Mesh Cut(Mesh src, Transform tr, Vector3 origin, Vector3 axis, float s0, float s1, float yBottom, float halfWidth)
    {
        return CutJagged(src, tr, origin, axis, l => s0, l => s1, yBottom, halfWidth, 0f);
    }

    // 手前・奥の切り口をギザギザにする版。e0(lat)／e1(lat) が切り口の位置（axis 方向）。
    // 切り口付近の三角形は maxEdge 以下まで細かく割ってから切る（ギザギザに沿わせるため）。maxEdge=0 なら割らない
    public static Mesh CutJagged(Mesh src, Transform tr, Vector3 origin, Vector3 axis, System.Func<float, float> e0, System.Func<float, float> e1, float yBottom, float halfWidth, float maxEdge)
    {
        axis.y = 0; axis.Normalize(); var side = new Vector3(-axis.z, 0, axis.x);
        float S(Vector3 p) => Vector3.Dot(p - origin, axis);
        float L(Vector3 p) => Vector3.Dot(p - origin, side);
        float A(Vector3 p) => S(p) - e0(L(p));   // >0 なら手前の切り口より奥
        float B(Vector3 p) => e1(L(p)) - S(p);   // >0 なら奥の切り口より手前
        // ギザギザの幅（割る範囲を決める）
        float a0 = float.MaxValue, a1 = float.MinValue, b0 = float.MaxValue, b1 = float.MinValue;
        for (float l = -halfWidth; l <= halfWidth; l += 0.05f) { a0 = Mathf.Min(a0, e0(l)); a1 = Mathf.Max(a1, e0(l)); b0 = Mathf.Min(b0, e1(l)); b1 = Mathf.Max(b1, e1(l)); }
        float s0 = a0, s1 = b1;
        var l2w = tr.localToWorldMatrix; var w2l = tr.worldToLocalMatrix;
        var pv = src.vertices; var pn = src.normals; var puv = src.uv; var puv2 = src.uv2; var pc = src.colors;
        bool hasN = pn.Length == pv.Length, hasUv = puv.Length == pv.Length, hasUv2 = puv2.Length == pv.Length, hasC = pc.Length == pv.Length;
        V Get(int i) => new V { p = l2w.MultiplyPoint3x4(pv[i]), n = hasN ? l2w.MultiplyVector(pn[i]) : Vector3.up, uv = hasUv ? puv[i] : Vector2.zero, uv2 = hasUv2 ? puv2[i] : Vector2.zero, c = hasC ? pc[i] : Color.white };
        var pieces = new System.Func<Vector3, float>[][]
        {
            new System.Func<Vector3, float>[] { p => -A(p) },
            new System.Func<Vector3, float>[] { p => -B(p) },
            new System.Func<Vector3, float>[] { A, B, p => yBottom - p.y },
            new System.Func<Vector3, float>[] { A, B, p => p.y - yBottom, p => -L(p) - halfWidth },
            new System.Func<Vector3, float>[] { A, B, p => p.y - yBottom, p => L(p) - halfWidth },
        };
        // 切り口の帯にかかる三角形か
        bool NearEdge(V a, V b, V c)
        {
            float smin = Mathf.Min(S(a.p), Mathf.Min(S(b.p), S(c.p))), smax = Mathf.Max(S(a.p), Mathf.Max(S(b.p), S(c.p)));
            float ymax = Mathf.Max(a.p.y, Mathf.Max(b.p.y, c.p.y));
            float lmin = Mathf.Min(L(a.p), Mathf.Min(L(b.p), L(c.p))), lmax = Mathf.Max(L(a.p), Mathf.Max(L(b.p), L(c.p)));
            if (ymax <= yBottom || lmin >= halfWidth || lmax <= -halfWidth) return false;
            bool band0 = smax > a0 && smin < a1 + 0.05f, band1 = smax > b0 - 0.05f && smin < b1;
            return band0 || band1;
        }
        // 辺を割るかどうか（辺の両端だけで決まる）
        bool SplitEdge(V a, V b)
        {
            if ((a.p - b.p).sqrMagnitude <= maxEdge * maxEdge) return false;
            float smin = Mathf.Min(S(a.p), S(b.p)), smax = Mathf.Max(S(a.p), S(b.p));
            if (Mathf.Max(a.p.y, b.p.y) <= yBottom) return false;
            float lmin = Mathf.Min(L(a.p), L(b.p)), lmax = Mathf.Max(L(a.p), L(b.p));
            if (lmin >= halfWidth || lmax <= -halfWidth) return false;
            return (smax > a0 && smin < a1 + 0.05f) || (smax > b0 - 0.05f && smin < b1);
        }
        // 辺の真ん中（向きに関係なく同じ点になるように、順番をそろえて計算）
        V Mid(V a, V b)
        {
            bool swap = a.p.x > b.p.x || (a.p.x == b.p.x && (a.p.y > b.p.y || (a.p.y == b.p.y && a.p.z > b.p.z)));
            return swap ? Lerp(b, a, 0.5f) : Lerp(a, b, 0.5f);
        }
        var OV = new List<V>(); var subs = new List<List<int>>();
        for (int sm = 0; sm < src.subMeshCount; sm++)
        {
            var T = src.GetTriangles(sm); var outT = new List<int>();
            var stack = new Stack<(V, V, V)>();
            for (int k = 0; k < T.Length; k += 3)
            {
                stack.Push((Get(T[k]), Get(T[k + 1]), Get(T[k + 2])));
                while (stack.Count > 0)
                {
                    var (a, b, c) = stack.Pop();
                    float smin = Mathf.Min(S(a.p), Mathf.Min(S(b.p), S(c.p))), smax = Mathf.Max(S(a.p), Mathf.Max(S(b.p), S(c.p)));
                    float ymax = Mathf.Max(a.p.y, Mathf.Max(b.p.y, c.p.y));
                    float lmin = Mathf.Min(L(a.p), Mathf.Min(L(b.p), L(c.p))), lmax = Mathf.Max(L(a.p), Mathf.Max(L(b.p), L(c.p)));
                    if (smax <= s0 || smin >= s1 || ymax <= yBottom || lmin >= halfWidth || lmax <= -halfWidth)
                    {
                        int bi = OV.Count; OV.Add(a); OV.Add(b); OV.Add(c); outT.Add(bi); outT.Add(bi + 1); outT.Add(bi + 2); continue;
                    }
                    // 長い辺を半分に割る（ギザギザに沿わせる）。割るかどうかは「辺ごと」に決めるので、
                    // となりの三角形も同じ辺を同じ点で割る（T字のすき間ができない）
                    if (maxEdge > 0 && NearEdge(a, b, c))
                    {
                        bool sab = SplitEdge(a, b), sbc = SplitEdge(b, c), sca = SplitEdge(c, a);
                        if (sab || sbc || sca)
                        {
                            var poly = new List<V> { a }; int firstMid = -1;
                            if (sab) { if (firstMid < 0) firstMid = poly.Count; poly.Add(Mid(a, b)); }
                            poly.Add(b);
                            if (sbc) { if (firstMid < 0) firstMid = poly.Count; poly.Add(Mid(b, c)); }
                            poly.Add(c);
                            if (sca) { if (firstMid < 0) firstMid = poly.Count; poly.Add(Mid(c, a)); }
                            if (poly.Count == 6)
                            {
                                stack.Push((poly[0], poly[1], poly[5])); stack.Push((poly[1], poly[2], poly[3]));
                                stack.Push((poly[5], poly[3], poly[4])); stack.Push((poly[1], poly[3], poly[5]));
                            }
                            else
                            {
                                int n = poly.Count;
                                for (int q = 1; q + 1 < n; q++) stack.Push((poly[firstMid], poly[(firstMid + q) % n], poly[(firstMid + q + 1) % n]));
                            }
                            continue;
                        }
                    }
                    foreach (var planes in pieces)
                    {
                        var poly = new List<V> { a, b, c };
                        foreach (var f in planes) { poly = Clip(poly, f); if (poly.Count < 3) break; }
                        if (poly.Count < 3) continue;
                        int bi = OV.Count; OV.AddRange(poly);
                        for (int q = 1; q + 1 < poly.Count; q++) { outT.Add(bi); outT.Add(bi + q); outT.Add(bi + q + 1); }
                    }
                }
            }
            subs.Add(outT);
        }
        return Build(src, OV, subs, w2l, hasN, hasUv, hasUv2, hasC);
    }

    static Mesh Build(Mesh src, List<V> OV, List<List<int>> subs, Matrix4x4 w2l, bool hasN, bool hasUv, bool hasUv2, bool hasC)
    {
        // 同じ頂点（位置・法線・UV・色が同じ）をまとめる。切ったり割ったりで頂点がばらばらになっているため
        var map = new Dictionary<(int, int, int, int, int, int, int, int, int, int, int), int>(); var WV = new List<V>(); var remap = new int[OV.Count];
        for (int i = 0; i < OV.Count; i++)
        {
            var v = OV[i];
            var key = (Mathf.RoundToInt(v.p.x * 1000), Mathf.RoundToInt(v.p.y * 1000), Mathf.RoundToInt(v.p.z * 1000),
                       Mathf.RoundToInt(v.n.x * 100), Mathf.RoundToInt(v.n.y * 100), Mathf.RoundToInt(v.n.z * 100),
                       Mathf.RoundToInt(v.uv.x * 2000), Mathf.RoundToInt(v.uv.y * 2000), Mathf.RoundToInt(v.uv2.x * 4000), Mathf.RoundToInt(v.uv2.y * 4000),
                       Mathf.RoundToInt(v.c.r * 255) + Mathf.RoundToInt(v.c.g * 255) * 256 + Mathf.RoundToInt(v.c.b * 255) * 65536);
            if (!map.TryGetValue(key, out int id)) { id = WV.Count; map[key] = id; WV.Add(v); }
            remap[i] = id;
        }
        OV = WV; subs = subs.Select(s => s.Select(i => remap[i]).ToList()).ToList();
        var m = new Mesh { name = src.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(OV.Select(v => w2l.MultiplyPoint3x4(v.p)).ToList());
        if (hasN) m.SetNormals(OV.Select(v => w2l.MultiplyVector(v.n).normalized).ToList());
        if (hasUv) m.SetUVs(0, OV.Select(v => v.uv).ToList());
        if (hasUv2) m.SetUVs(1, OV.Select(v => v.uv2).ToList());
        if (hasC) m.SetColors(OV.Select(v => v.c).ToList());
        m.subMeshCount = subs.Count; for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
        m.RecalculateBounds();
        if (!hasN) m.RecalculateNormals();
        m.Optimize();
        return m;
    }

    static Mesh CutOld(Mesh src, Transform tr, Vector3 origin, Vector3 axis, float s0, float s1, float yBottom, float halfWidth)
    {
        axis.y = 0; axis.Normalize(); var side = new Vector3(-axis.z, 0, axis.x);
        float S(Vector3 p) => Vector3.Dot(p - origin, axis);
        float L(Vector3 p) => Vector3.Dot(p - origin, side);
        var l2w = tr.localToWorldMatrix; var w2l = tr.worldToLocalMatrix;
        var pv = src.vertices; var pn = src.normals; var puv = src.uv; var puv2 = src.uv2; var pc = src.colors;
        bool hasN = pn.Length == pv.Length, hasUv = puv.Length == pv.Length, hasUv2 = puv2.Length == pv.Length, hasC = pc.Length == pv.Length;
        V Get(int i) => new V { p = l2w.MultiplyPoint3x4(pv[i]), n = hasN ? l2w.MultiplyVector(pn[i]) : Vector3.up, uv = hasUv ? puv[i] : Vector2.zero, uv2 = hasUv2 ? puv2[i] : Vector2.zero, c = hasC ? pc[i] : Color.white };
        bool Inside(Vector3 p) => S(p) > s0 && S(p) < s1 && p.y > yBottom && Mathf.Abs(L(p)) < halfWidth;
        // 残す部分：互いに重ならない4つ（手前・奥・範囲の下・横）
        var pieces = new System.Func<Vector3, float>[][]
        {
            new System.Func<Vector3, float>[] { p => s0 - S(p) },
            new System.Func<Vector3, float>[] { p => S(p) - s1 },
            new System.Func<Vector3, float>[] { p => S(p) - s0, p => s1 - S(p), p => yBottom - p.y },
            new System.Func<Vector3, float>[] { p => S(p) - s0, p => s1 - S(p), p => p.y - yBottom, p => -L(p) - halfWidth },
            new System.Func<Vector3, float>[] { p => S(p) - s0, p => s1 - S(p), p => p.y - yBottom, p => L(p) - halfWidth },
        };
        var OV = new List<V>(); var subs = new List<List<int>>();
        for (int sm = 0; sm < src.subMeshCount; sm++)
        {
            var T = src.GetTriangles(sm); var outT = new List<int>();
            for (int k = 0; k < T.Length; k += 3)
            {
                var a = Get(T[k]); var b = Get(T[k + 1]); var c = Get(T[k + 2]);
                // 範囲と関係ない三角形はそのまま（速さのため）
                float smin = Mathf.Min(S(a.p), Mathf.Min(S(b.p), S(c.p))), smax = Mathf.Max(S(a.p), Mathf.Max(S(b.p), S(c.p)));
                float ymax = Mathf.Max(a.p.y, Mathf.Max(b.p.y, c.p.y));
                float lmin = Mathf.Min(L(a.p), Mathf.Min(L(b.p), L(c.p))), lmax = Mathf.Max(L(a.p), Mathf.Max(L(b.p), L(c.p)));
                if (smax <= s0 || smin >= s1 || ymax <= yBottom || lmin >= halfWidth || lmax <= -halfWidth)
                {
                    int bi = OV.Count; OV.Add(a); OV.Add(b); OV.Add(c); outT.Add(bi); outT.Add(bi + 1); outT.Add(bi + 2); continue;
                }
                foreach (var planes in pieces)
                {
                    var poly = new List<V> { a, b, c };
                    foreach (var f in planes) { poly = Clip(poly, f); if (poly.Count < 3) break; }
                    if (poly.Count < 3) continue;
                    int bi = OV.Count; OV.AddRange(poly);
                    for (int q = 1; q + 1 < poly.Count; q++) { outT.Add(bi); outT.Add(bi + q); outT.Add(bi + q + 1); }
                }
            }
            subs.Add(outT);
        }
        var m = new Mesh { name = src.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(OV.Select(v => w2l.MultiplyPoint3x4(v.p)).ToList());
        if (hasN) m.SetNormals(OV.Select(v => w2l.MultiplyVector(v.n).normalized).ToList());
        if (hasUv) m.SetUVs(0, OV.Select(v => v.uv).ToList());
        if (hasUv2) m.SetUVs(1, OV.Select(v => v.uv2).ToList());
        if (hasC) m.SetColors(OV.Select(v => v.c).ToList());
        m.subMeshCount = subs.Count; for (int i = 0; i < subs.Count; i++) m.SetTriangles(subs[i], i);
        m.RecalculateBounds();
        if (!hasN) m.RecalculateNormals();
        // 同じ位置の頂点をまとめて軽く（切ったので頂点がばらばらになっている）
        m.Optimize();
        return m;
    }
}
