using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 柵の見えない面を消す（2026-11-22 てじゃ「C も一応見よう」）。
// 柵のメッシュは角を丸めた棒（凸の閉じた塊）の寄せ集めで、横木は何本も重ねて作ってあるので、ほかの棒の中にすっぽり入っている面がたくさんある
// （スタートの柵は 11988 三角形のうち 6448 が中）。3つの頂点がぜんぶ「ほかの凸の塊の中（ふちから Margin 以上内側）」にある三角形を消す。
// 外から見える所は1ピクセルも変わらない（前後で撮り比べて差 0）。
//  ・対象：シーンで使っている名前に「Fence」がある自作メッシュ（VC_ の写し・遠くの軽い版 _Far は除く）
//  ・塊＝つながった三角形のまとまり。凸でない塊は「中」の判定に使わない
//  ・元は Generated/FenceHi に。いつもそこから作る。最後にマテリアルまとめをかけ直す
public static class FuwaFenceCull
{
    const string Bak = "Assets/FuwaCourse/Generated/FenceHi";
    public static float Margin = 0.004f, ConvexEps = 0.0025f;

    [MenuItem("FuwaCourse/よく使う/柵の見えない面を消す")]
    static void Menu() { Debug.Log(Run(false)); }
    [MenuItem("FuwaCourse/よく使う/柵の見えない面を消す（数えるだけ）")]
    static void MenuDry() { Debug.Log(Run(true)); }

    public static string Run(bool dry)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!dry) FuwaMaterialMerge.Revert();
        var meshes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true)).Select(f => f.sharedMesh)
            .Where(m => m && m.name.Contains("Fence") && !m.name.StartsWith("VC_") && !m.name.EndsWith("_Far") && !m.name.Contains("Collider") && AssetDatabase.GetAssetPath(m).StartsWith("Assets/") && !AssetDatabase.GetAssetPath(m).Contains("/FenceHi/")).Distinct().ToList();
        if (!dry && !AssetDatabase.IsValidFolder(Bak)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "FenceHi");
        var log = new List<string>(); long before = 0, after = 0;
        foreach (var m in meshes)
        {
            string bp = Bak + "/" + m.name + "_Hi.asset";
            var hi = AssetDatabase.LoadAssetAtPath<Mesh>(bp); var src = hi ? hi : m;
            var subs = Cull(src, out int removed);
            if (removed == 0) continue;
            before += src.triangles.Length / 3; after += src.triangles.Length / 3 - removed;
            log.Add($"{m.name}: {src.triangles.Length / 3} → {src.triangles.Length / 3 - removed}");
            if (dry) continue;
            if (!hi) { AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(m), bp); hi = AssetDatabase.LoadAssetAtPath<Mesh>(bp); }
            // 元の中身（hi）をそのまま写して、三角形だけ入れかえる
            string nm = m.name;
            if (src != m) FuwaBevelTools.CopyInto(m, src);
            m.subMeshCount = subs.Count; for (int s = 0; s < subs.Count; s++) m.SetTriangles(subs[s], s);
            m.name = nm; m.RecalculateBounds(); EditorUtility.SetDirty(m);
        }
        string head = $"[柵の見えない面] {log.Count} 個 {before / 1000}k → {after / 1000}k\n" + string.Join("\n", log);
        if (!dry) { AssetDatabase.SaveAssets(); head += "\n" + FuwaMaterialMerge.Run(false); }
        return head;
    }

    static List<int[]> Cull(Mesh m, out int removed)
    {
        removed = 0;
        var v = m.vertices; var subs = Enumerable.Range(0, m.subMeshCount).Select(s => m.GetTriangles(s)).ToList();
        // 頂点を位置でまとめて、つながった塊に分ける
        int n = v.Length; var weld = new int[n]; var key = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < n; i++) { var k = Vector3Int.RoundToInt(v[i] * 100000f); if (!key.TryGetValue(k, out var w)) { w = i; key[k] = i; } weld[i] = w; }
        var par = Enumerable.Range(0, n).ToArray();
        int Find(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
        var allTris = new List<(int s, int a, int b, int c)>();
        for (int s = 0; s < subs.Count; s++) for (int i = 0; i < subs[s].Length; i += 3) { allTris.Add((s, subs[s][i], subs[s][i + 1], subs[s][i + 2])); int a = Find(weld[subs[s][i]]), b = Find(weld[subs[s][i + 1]]), c = Find(weld[subs[s][i + 2]]); par[a] = b; par[Find(b)] = Find(c); }
        var comp = allTris.Select(t => Find(weld[t.a])).ToArray();
        // 塊ごとの面（外向きの平面）と AABB。凸か確かめる
        var groups = Enumerable.Range(0, allTris.Count).GroupBy(i => comp[i]).ToList();
        var solids = new List<(int id, Bounds b, List<(Vector3 n, float d)> planes)>();
        foreach (var g in groups)
        {
            var planes = new List<(Vector3 n, float d)>(); var pts = new HashSet<int>(); Bounds b = new Bounds(v[allTris[g.First()].a], Vector3.zero);
            foreach (var ti in g)
            {
                var t = allTris[ti]; var cr = Vector3.Cross(v[t.b] - v[t.a], v[t.c] - v[t.a]); if (cr.sqrMagnitude < 1e-14f) continue;
                var nn = cr.normalized; planes.Add((nn, Vector3.Dot(nn, v[t.a]))); pts.Add(t.a); pts.Add(t.b); pts.Add(t.c); b.Encapsulate(v[t.a]); b.Encapsulate(v[t.b]); b.Encapsulate(v[t.c]);
            }
            if (planes.Count < 4) continue;
            // 凸（ぜんぶの点がぜんぶの面の裏側）か。面の向きが内向きなら（体積が負）ひっくり返す
            bool convexOut = pts.All(p => planes.All(pl => Vector3.Dot(pl.n, v[p]) - pl.d <= ConvexEps));   // 角の丸めの四角が少しねじれていて 1.8mm くらい外に出る
            bool convexIn = !convexOut && pts.All(p => planes.All(pl => Vector3.Dot(pl.n, v[p]) - pl.d >= -ConvexEps));
            if (convexIn) planes = planes.Select(pl => (-pl.n, -pl.d)).ToList();
            if (!convexOut && !convexIn) continue;
            solids.Add((g.Key, b, planes));
        }
        bool Inside(Vector3 p, (int id, Bounds b, List<(Vector3 n, float d)> planes) s)
        {
            if (!s.b.Contains(p)) return false;
            foreach (var pl in s.planes) if (Vector3.Dot(pl.n, p) - pl.d > -Margin) return false;
            return true;
        }
        var outSubs = subs.Select(_ => new List<int>()).ToList();
        for (int ti = 0; ti < allTris.Count; ti++)
        {
            var t = allTris[ti]; bool hid = false;
            foreach (var s in solids)
            {
                if (s.id == comp[ti]) continue;
                if (Inside(v[t.a], s) && Inside(v[t.b], s) && Inside(v[t.c], s)) { hid = true; break; }
            }
            if (hid) { removed++; continue; }
            outSubs[t.s].Add(t.a); outSubs[t.s].Add(t.b); outSubs[t.s].Add(t.c);
        }
        return outSubs.Select(l => l.ToArray()).ToList();
    }
}
