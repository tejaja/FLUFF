using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ヒトヨタケ（InkCap）の当たりから、垂れているしずくを外す（2026-10-14 てじゃ「ここでハマるバグ」）。
// 見た目のメッシュをそのまま当たりにしていたので、しずく1つ1つ（中が空洞の小さな形）にふわふわが入り込むと、撃っても出られなかった。
// しずくは同じ三角形の数の小さなかたまりが何個も並んでいるので、それを除いた当たり用メッシュ（<名前>_Col）を作って MeshCollider に入れる。見た目はそのまま。何度実行してもよい
public static class FuwaInkCapColliders
{
    const string Dir = "Assets/FuwaCourse/Meshes/InkCap/";

    [MenuItem("FuwaCourse/ヒトヨタケの当たりからしずくを外す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new List<string>();
        var made = new Dictionary<Mesh, Mesh>();
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t.name != "InkCap") continue;
            var mf = t.GetComponent<MeshFilter>(); var mc = t.GetComponent<MeshCollider>();
            if (mf == null || mc == null || mf.sharedMesh == null) continue;
            var src = mf.sharedMesh;
            if (!made.TryGetValue(src, out var col)) { col = MakeCollider(src, log); made[src] = col; }
            mc.sharedMesh = null; mc.sharedMesh = col;
            EditorUtility.SetDirty(mc);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return string.Join("\n", log) + "\nset " + made.Count + " meshes";
    }

    static Mesh MakeCollider(Mesh src, List<string> log)
    {
        var v = src.vertices; var tris = src.triangles;
        // 同じ位置の頂点をまとめて、つながっているかたまりに分ける
        var key = new Dictionary<Vector3Int, int>(); var id = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000), Mathf.RoundToInt(v[i].y * 1000), Mathf.RoundToInt(v[i].z * 1000));
            if (!key.TryGetValue(k, out var q)) { q = key.Count; key[k] = q; }
            id[i] = q;
        }
        var par = Enumerable.Range(0, key.Count).ToArray();
        int F(int x) { while (par[x] != x) { par[x] = par[par[x]]; x = par[x]; } return x; }
        for (int i = 0; i < tris.Length; i += 3) { int a = F(id[tris[i]]); par[F(id[tris[i + 1]])] = a; par[F(id[tris[i + 2]])] = a; }
        var compTris = new Dictionary<int, int>();
        for (int i = 0; i < tris.Length; i += 3) { int r = F(id[tris[i]]); compTris[r] = compTris.TryGetValue(r, out var c) ? c + 1 : 1; }
        // しずく：同じ三角形の数のかたまりが4つ以上
        var dripCount = compTris.Values.GroupBy(x => x).Where(g => g.Count() >= 4).Select(g => g.Key).ToHashSet();
        var keep = new List<int>();
        int dropped = 0;
        for (int i = 0; i < tris.Length; i += 3)
        {
            int r = F(id[tris[i]]);
            if (dripCount.Contains(compTris[r])) { dropped++; continue; }
            keep.Add(tris[i]); keep.Add(tris[i + 1]); keep.Add(tris[i + 2]);
        }
        var m = new Mesh { name = src.name + "_Col" };
        m.vertices = v; m.triangles = keep.ToArray(); m.RecalculateNormals(); m.RecalculateBounds();
        string path = Dir + m.name + ".asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex != null) { ex.Clear(); ex.vertices = m.vertices; ex.triangles = m.triangles; ex.RecalculateNormals(); ex.RecalculateBounds(); EditorUtility.SetDirty(ex); m = ex; }
        else AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        log.Add(src.name + ": comps " + compTris.Count + ", drip size " + string.Join(",", dripCount) + ", dropped tris " + dropped + " / " + tris.Length / 3);
        return m;
    }
}
