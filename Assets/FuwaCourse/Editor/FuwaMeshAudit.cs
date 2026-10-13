using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// メッシュの「すき間・トゲ」を数える確認用（2026-10-13 てじゃ「隙間、はみ出し系は徹底的に洗って」）。
// 開いた辺（三角形1枚にしか使われていない辺＝すき間のふち）、3枚以上が共有する辺、細長い三角形（いちばん小さい角が4°未満で長い辺が30cm以上）を、範囲の中だけ数える
public static class FuwaMeshAudit
{
    public static string Report(Transform tr, Mesh m, Bounds region, string label)
    {
        if (m == null) return label + ": none";
        var v = m.vertices; var l2w = tr.localToWorldMatrix; var W = v.Select(x => l2w.MultiplyPoint3x4(x)).ToArray();
        var weld = FuwaCutCap.Weld(W.ToList(), 0.0003f);
        var edges = new Dictionary<long, int>(); int sliver = 0; var sp = new List<Vector3>();
        for (int sm = 0; sm < m.subMeshCount; sm++)
        {
            var t = m.GetTriangles(sm);
            for (int i = 0; i < t.Length; i += 3)
            {
                int a = weld[t[i]], b = weld[t[i + 1]], c = weld[t[i + 2]];
                if (a == b || b == c || a == c) continue;
                foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) }) { long kk = ((long)Mathf.Min(p, q) << 32) | (uint)Mathf.Max(p, q); edges.TryGetValue(kk, out var cnt); edges[kk] = cnt + 1; }
                var A = W[a]; var B = W[b]; var C = W[c]; if (!region.Contains((A + B + C) / 3f)) continue;
                float aA = Vector3.Angle(B - A, C - A), aB = Vector3.Angle(A - B, C - B), aC = 180f - aA - aB;
                float mn = Mathf.Min(aA, Mathf.Min(aB, aC)); float lg = Mathf.Max((A - B).magnitude, Mathf.Max((B - C).magnitude, (C - A).magnitude));
                if (mn < 4f && lg > 0.3f) { sliver++; sp.Add((A + B + C) / 3f); }
            }
        }
        var open = new List<Vector3>(); int nonman = 0;
        foreach (var kv in edges)
        {
            var mid = (W[(int)(kv.Key >> 32)] + W[(int)(kv.Key & 0xffffffff)]) / 2f;
            if (!region.Contains(mid)) continue;
            if (kv.Value == 1) open.Add(mid); else if (kv.Value > 2) nonman++;
        }
        string Clus(List<Vector3> pts) => string.Join(" ", pts.GroupBy(p => new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z))).OrderByDescending(x => x.Count()).Take(10).Select(x => x.Key + "x" + x.Count()));
        var s = label + " v" + v.Length + " open " + open.Count + " nonmanifold " + nonman + " slivers " + sliver;
        if (open.Count > 0) s += "\n   open: " + Clus(open);
        if (sliver > 0) s += "\n   sliver: " + Clus(sp);
        return s;
    }

    public static string Course2Valley()
    {
        var c2 = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaCourse2" && t.parent == null);
        var region = new Bounds(new Vector3(116f, 11f, 3f), new Vector3(16f, 10f, 16f));
        var log = new List<string>();
        foreach (var path in new[] { "Rocks2/Mountain", "PathMesh", "CollapseLedgeRocks" })
        {
            var tr = c2.Find(path); if (tr == null) { log.Add(path + " missing"); continue; }
            var mf = tr.GetComponent<MeshFilter>(); var mc = tr.GetComponent<MeshCollider>();
            if (mf != null) log.Add(Report(tr, mf.sharedMesh, region, path + " vis"));
            if (mc != null) log.Add(Report(tr, mc.sharedMesh, region, path + " col"));
        }
        return string.Join("\n", log);
    }
}
