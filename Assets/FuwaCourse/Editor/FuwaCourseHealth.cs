using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// コースのメッシュの健康診断（見るだけ。何も変えない）。2026-11-24 てじゃ「コース全体的にモデルのトポロジを見直したい」
//  1) 高さのずれ（ヒビ）：同じ xz（1mm）なのに y が 0.5mm〜3cm ずれた点の組。継ぎ足しの直しで片方だけ動いた所（丸い島のヒビ）
//  2) すき間（T字・割れ目）：ふさがっていない辺の中点のすぐそば（3mm）に、別のふさがっていない辺がある＝本当はつながっているはずの所
//  3) 重なった面（メッシュの中）：同じ場所・同じ（または逆の）向きの三角形の組（両面表示だとZファイト）
//  4) 重なった面（メッシュどうし）：上向きの面を 0.25m おきに打って、別の描画物の上向きの面と高さが 3mm 以内で重なる所
//  5) つぶれた三角形、頂点が多いメッシュ
//  場所はコースの根っこから見た座標
public static class FuwaCourseHealth
{
    public class Hit { public string kind, obj; public Vector3 lp; public float val; }

    [MenuItem("FuwaCourse/よく使う/メッシュの健康診断（選んだコース）")]
    static void Menu()
    {
        foreach (var g in Selection.gameObjects) Debug.Log(Run(g.transform, out _));
    }

    // 近くの版と遠くの版（X と X/X_Far、X_Lite）は、どちらか片方しか出ないので重なっていてよい
    static bool SameThing(Transform a, Transform b)
    {
        if (a.IsChildOf(b) || b.IsChildOf(a)) return true;
        string na = a.name.Replace("_Far", "").Replace("_Lite", ""), nb = b.name.Replace("_Far", "").Replace("_Lite", "");
        return na == nb && (a.name != b.name) && (a.parent == b.parent || a.parent == b || b.parent == a);
    }
    static string PathOf(Transform t, Transform root) { var s = t.name; var p = t.parent; while (p && p != root) { s = p.name + "/" + s; p = p.parent; } return s; }

    public static string Run(Transform root, out List<Hit> hits, int maxList = 12)
    {
        hits = new List<Hit>();
        var rends = root.GetComponentsInChildren<MeshRenderer>(false).Where(r => r.enabled && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh).ToList();
        var sum = new StringBuilder(); int totalV = 0, degenerate = 0, unreadable = 0;
        var grid = new Dictionary<Vector2Int, List<(int ri, float y)>>();
        const float G = 0.25f;
        for (int ri = 0; ri < rends.Count; ri++)
        {
            var r = rends[ri]; var t = r.transform; var m = r.GetComponent<MeshFilter>().sharedMesh;
            if (!m.isReadable) { unreadable++; continue; }
            var vs = m.vertices; var tr = m.triangles; totalV += vs.Length;
            string nm = PathOf(t, root);
            bool twoSided = r.sharedMaterials.Any(mt => mt && mt.HasProperty("_Cull") && mt.GetFloat("_Cull") < 0.5f);
            var lv = new Vector3[vs.Length]; for (int i = 0; i < vs.Length; i++) lv[i] = root.InverseTransformPoint(t.TransformPoint(vs[i]));
            if (vs.Length > 30000) hits.Add(new Hit { kind = "頂点が多い", obj = nm, lp = root.InverseTransformPoint(r.bounds.center), val = vs.Length });
            // 1) 高さのずれ
            //   上向きの点だけ・同じ材質（サブメッシュ）どうしだけ（雪の層と氷の面のような別の層は数えない）
            var nrm = m.normals; var subOf = new int[vs.Length];
            for (int sm = 0; sm < m.subMeshCount; sm++) foreach (var ix in m.GetTriangles(sm)) subOf[ix] = sm;
            var byXZ = new Dictionary<(Vector2Int, int), List<float>>();
            for (int i = 0; i < lv.Length; i++)
            {
                if (nrm.Length == vs.Length && t.TransformDirection(nrm[i]).normalized.y < 0.7f) continue;
                var k = (new Vector2Int(Mathf.RoundToInt(lv[i].x * 1000), Mathf.RoundToInt(lv[i].z * 1000)), subOf[i]); if (!byXZ.TryGetValue(k, out var l)) byXZ[k] = l = new List<float>(); l.Add(lv[i].y);
            }
            foreach (var kv in byXZ)
            {
                if (kv.Value.Count < 2) continue; kv.Value.Sort();
                for (int j = 1; j < kv.Value.Count; j++) { float d = kv.Value[j] - kv.Value[j - 1]; if (d > 0.0005f && d < 0.03f) { hits.Add(new Hit { kind = "高さのずれ", obj = nm, lp = new Vector3(kv.Key.Item1.x / 1000f, kv.Value[j], kv.Key.Item1.y / 1000f), val = d }); break; } }
            }
            // 位置でまとめた番号
            var pid = new int[lv.Length]; var key = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < lv.Length; i++) { var k = new Vector3Int(Mathf.RoundToInt(lv[i].x * 2000), Mathf.RoundToInt(lv[i].y * 2000), Mathf.RoundToInt(lv[i].z * 2000)); if (!key.TryGetValue(k, out var j)) { j = key.Count; key[k] = j; } pid[i] = j; }
            var ecount = new Dictionary<long, int>(); var eany = new Dictionary<long, (int, int)>();
            var triKey = new HashSet<(Vector3Int, int)>();
            for (int s = 0; s < tr.Length; s += 3)
            {
                Vector3 a = lv[tr[s]], b = lv[tr[s + 1]], c = lv[tr[s + 2]];
                var n = Vector3.Cross(b - a, c - a); float area = n.magnitude * 0.5f;
                if (area < 1e-7f) { degenerate++; continue; }   // 見えないので数だけ
                n /= area * 2f;
                for (int e = 0; e < 3; e++)
                {
                    int ia = pid[tr[s + e]], ib = pid[tr[s + (e + 1) % 3]]; if (ia == ib) continue;
                    long k = ia < ib ? ((long)ia << 32) | (uint)ib : ((long)ib << 32) | (uint)ia;
                    ecount[k] = ecount.TryGetValue(k, out var q) ? q + 1 : 1; eany[k] = (tr[s + e], tr[s + (e + 1) % 3]);
                }
                // 3) 重なった面：重心（1mm）＋向き（表裏を区別しない、0.1刻み）
                var cen = (a + b + c) / 3f; var ck = new Vector3Int(Mathf.RoundToInt(cen.x * 1000), Mathf.RoundToInt(cen.y * 1000), Mathf.RoundToInt(cen.z * 1000));
                var an = n.y < 0 || (n.y == 0 && n.x < 0) ? -n : n; int nk = Mathf.RoundToInt(an.x * 10) * 441 + Mathf.RoundToInt(an.y * 10) * 21 + Mathf.RoundToInt(an.z * 10);
                if (!triKey.Add((ck, nk)) && twoSided) hits.Add(new Hit { kind = "重なった面（中）", obj = nm, lp = cen, val = area });   // 片面表示なら表と裏の2枚は正常
                // 4) 上向きの面を打つ
                if (n.y > 0.85f && area > 1e-4f)
                {
                    float minx = Mathf.Min(a.x, Mathf.Min(b.x, c.x)), maxx = Mathf.Max(a.x, Mathf.Max(b.x, c.x)), minz = Mathf.Min(a.z, Mathf.Min(b.z, c.z)), maxz = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
                    Vector2 A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z), C = new Vector2(c.x, c.z);
                    float d = (B.y - C.y) * (A.x - C.x) + (C.x - B.x) * (A.y - C.y); if (Mathf.Abs(d) < 1e-9f) continue;
                    for (int gx = Mathf.CeilToInt(minx / G); gx * G <= maxx; gx++)
                        for (int gz = Mathf.CeilToInt(minz / G); gz * G <= maxz; gz++)
                        {
                            var p = new Vector2(gx * G, gz * G);
                            float w1 = ((B.y - C.y) * (p.x - C.x) + (C.x - B.x) * (p.y - C.y)) / d, w2 = ((C.y - A.y) * (p.x - C.x) + (A.x - C.x) * (p.y - C.y)) / d, w3 = 1 - w1 - w2;
                            const float in_ = 0.02f; if (w1 < in_ || w2 < in_ || w3 < in_) continue;   // ふちぎりぎりは数えない
                            float y = w1 * a.y + w2 * b.y + w3 * c.y;
                            var gk = new Vector2Int(gx, gz); if (!grid.TryGetValue(gk, out var gl)) grid[gk] = gl = new List<(int, float)>();
                            gl.Add((ri, y));
                        }
                }
            }
            // 2) すき間：ふさがっていない辺の中点を 5mm のます目に入れて、別の辺がそばにあるか
            var open = ecount.Where(kv => kv.Value == 1).Select(kv => eany[kv.Key]).ToList();
            var og = new Dictionary<Vector3Int, List<int>>(); const float H = 0.005f;
            for (int i = 0; i < open.Count; i++) { var mp = (lv[open[i].Item1] + lv[open[i].Item2]) * 0.5f; var k = new Vector3Int(Mathf.FloorToInt(mp.x / H), Mathf.FloorToInt(mp.y / H), Mathf.FloorToInt(mp.z / H)); if (!og.TryGetValue(k, out var l)) og[k] = l = new List<int>(); l.Add(i); }
            int cracks = 0;
            for (int i = 0; i < open.Count && cracks < 400; i++)
            {
                var a = lv[open[i].Item1]; var b = lv[open[i].Item2]; var mp = (a + b) * 0.5f; float L = (b - a).magnitude; if (L < 1e-4f) continue;
                var k0 = new Vector3Int(Mathf.FloorToInt(mp.x / H), Mathf.FloorToInt(mp.y / H), Mathf.FloorToInt(mp.z / H)); bool f = false;
                for (int dx = -1; dx <= 1 && !f; dx++) for (int dy = -1; dy <= 1 && !f; dy++) for (int dz = -1; dz <= 1 && !f; dz++)
                {
                    if (!og.TryGetValue(k0 + new Vector3Int(dx, dy, dz), out var l)) continue;
                    foreach (var j in l)
                    {
                        if (j == i) continue;
                        int ja = pid[open[j].Item1], jb = pid[open[j].Item2], xa = pid[open[i].Item1], xb = pid[open[i].Item2];
                        if (ja == xa || ja == xb || jb == xa || jb == xb) continue;
                        var c = lv[open[j].Item1]; var cd = lv[open[j].Item2] - c;
                        float tt = Mathf.Clamp01(Vector3.Dot(mp - c, cd) / Mathf.Max(cd.sqrMagnitude, 1e-9f)); if ((c + cd * tt - mp).magnitude < 0.003f) { f = true; break; }
                    }
                }
                if (f) { cracks++; hits.Add(new Hit { kind = "すき間", obj = nm, lp = mp, val = L }); }
            }
        }
        // 4) 判定
        var zf = new HashSet<(int, int)>(); var zfCount = new Dictionary<(int, int), int>();
        foreach (var kv in grid)
        {
            var l = kv.Value; if (l.Count < 2) continue;
            for (int i = 0; i < l.Count; i++) for (int j = i + 1; j < l.Count; j++)
            {
                if (l[i].ri == l[j].ri) continue; if (Mathf.Abs(l[i].y - l[j].y) > 0.003f) continue;
                if (SameThing(rends[l[i].ri].transform, rends[l[j].ri].transform)) continue;   // 近い版と遠い版（_Far）・親子は数えない
                int x = Mathf.Min(l[i].ri, l[j].ri), y = Mathf.Max(l[i].ri, l[j].ri);
                zfCount[(x, y)] = zfCount.TryGetValue((x, y), out var q) ? q + 1 : 1;
                if (zf.Add((x, y))) hits.Add(new Hit { kind = "重なった面（どうし）", obj = PathOf(rends[x].transform, root) + " × " + PathOf(rends[y].transform, root), lp = new Vector3(kv.Key.x * G, l[i].y, kv.Key.y * G), val = 0 });
            }
        }
        foreach (var h in hits.Where(h => h.kind == "重なった面（どうし）")) { var pr = zfCount.First(kv => PathOf(rends[kv.Key.Item1].transform, root) + " × " + PathOf(rends[kv.Key.Item2].transform, root) == h.obj); h.val = pr.Value * G * G; }
        // まとめ
        var sb = new StringBuilder("【" + root.name + "】描画物 " + rends.Count + "・頂点 " + totalV + sum + "\n");
        foreach (var grp in hits.GroupBy(h => h.kind))
        {
            sb.Append("■" + grp.Key + " " + grp.Count() + "\n");
            foreach (var g2 in grp.GroupBy(h => h.obj).OrderByDescending(g => g.Count() + g.Sum(h => h.val)).Take(maxList))
            {
                sb.Append("  " + g2.Key + " ×" + g2.Count() + "  例 " + g2.First().lp.ToString("F2"));
                if (grp.Key == "高さのずれ") sb.Append(" 最大 " + (g2.Max(h => h.val) * 100).ToString("F1") + "cm");
                if (grp.Key == "重なった面（どうし）") sb.Append(" 約 " + g2.First().val.ToString("F2") + "m²");
                sb.Append("\n");
            }
        }
        return sb.ToString();
    }
}
