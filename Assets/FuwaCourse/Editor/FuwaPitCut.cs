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
