using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// いわのみち：頂上の柱ゾーン手前の崩落の谷を、1組の線（FuwaCourse2Collapse.Near / Far）から作り直す（2026-10-13）。
// てじゃ「頂上後の崖周りが道も岩もところどころ乱れてる」「岩の割れ目そのものもちょっと荒い」「隙間、はみ出し系は徹底的に洗って」。
// 前：山（Course2_mountain）は 10-05 のギザギザで穴が開いたまま、谷の壁と底は別の岩（PitRock）をはめ込み、道は別のギザギザで切ってふたを後付け
// → 3つの切り口がずれて、すき間・はみ出し・トゲ・細い三角形・縦の筋が出ていた。
// 今：山と山の当たり判定を、切る前（Backup/*_PreValley）から同じ線で切り、切り口（手前の壁・奥の壁・谷底）を FuwaCutCap でふさぐ（ゆるいでこぼこの岩肌）。
// 道も同じ線で切ってふさぐ（FuwaCourse2Collapse.CutPath）。PitRock は消す。舌の足場の下の岩（FuwaCourse2Ledge）も作り直す。何度実行しても同じ結果
public static class FuwaCourse2Valley
{
    const string MountainPath = "Assets/FuwaCourse/Meshes/Course2_mountain.asset";
    const string BackupDir = "Assets/FuwaCourse/Meshes/Backup";
    const float WallRough = 0.22f, WallMaxArea = 0.3f;

    [MenuItem("FuwaCourse/Course2/崩落の谷を作り直す（山・道・壁・底）")]
    public static void Menu() { Debug.Log(Run()); UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty(); }

    static Mesh Backup(Mesh live, string suffix)
    {
        string lp = AssetDatabase.GetAssetPath(live);
        string bp = BackupDir + "/" + System.IO.Path.GetFileNameWithoutExtension(lp) + suffix + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(bp) == null) AssetDatabase.CopyAsset(lp, bp);
        return AssetDatabase.LoadAssetAtPath<Mesh>(bp);
    }

    static void Write(Mesh live, Mesh src)
    {
        live.Clear(); live.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        live.SetVertices(src.vertices);
        if (src.normals.Length > 0) live.SetNormals(src.normals);
        var u = new List<Vector2>(); src.GetUVs(0, u); if (u.Count > 0) live.SetUVs(0, u);
        u = new List<Vector2>(); src.GetUVs(1, u); if (u.Count > 0) live.SetUVs(1, u);
        if (src.colors.Length > 0) live.SetColors(src.colors);
        live.subMeshCount = src.subMeshCount; for (int i = 0; i < src.subMeshCount; i++) live.SetTriangles(src.GetTriangles(i), i);
        live.RecalculateBounds(); EditorUtility.SetDirty(live);
    }

    public static string Run() { return RunMountain() + "\n" + RunPath(); }

    static FuwaCutCap.Frame MountainFrame(Transform root)
    {
        var (origin, axis) = FuwaCourse2Collapse.Frame(root);
        return new FuwaCutCap.Frame { origin = origin, axis = axis, side = new Vector3(-axis.z, 0f, axis.x), e0 = FuwaCourse2Collapse.Near, e1 = FuwaCourse2Collapse.Far, yBottom = FuwaCourse2Collapse.ValleyBottom };
    }

    public static string RunMountain()
    {
        var root = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaCourse2" && t.parent == null);
        var log = new List<string>();
        var (origin, axis) = FuwaCourse2Collapse.Frame(root);
        var f = new FuwaCutCap.Frame { origin = origin, axis = axis, side = new Vector3(-axis.z, 0f, axis.x), e0 = FuwaCourse2Collapse.Near, e1 = FuwaCourse2Collapse.Far, yBottom = FuwaCourse2Collapse.ValleyBottom };

        // 1. 山（見た目＋当たり判定）
        var mt = root.Find("Rocks2/Mountain");
        var mf = mt.GetComponent<MeshFilter>(); var mc = mt.GetComponent<MeshCollider>();
        foreach (var (live, isCol) in new[] { (mf.sharedMesh, false), (mc != null ? mc.sharedMesh : null, true) })
        {
            if (live == null) continue;
            var src = Backup(live, "_PreValley");
            var cut = FuwaPitCut.CutJagged(src, mt, origin, axis, f.e0, f.e1, f.yBottom, FuwaCourse2Collapse.ValleyHalfWidth, isCol ? 0.3f : 0.2f);
            var (tris, bad, clog) = FuwaCutCap.Cap(cut, mt, f, 0, WallRough, WallMaxArea, 7);
            // 山の面の折り目（細く折り返して影の線に見える所）をならす（2026-10-13 てじゃ「謎の横線」）
            var uf = FuwaMeshUnfold.Run(cut, mt, new Bounds(new Vector3(115f, 8f, 4f), new Vector3(60f, 30f, 60f)));
            clog += " folds " + uf.before + "->" + uf.after + " moved " + uf.moved;
            // 温泉の先、道の南のふちの下の細い段差（影の横線に見えた）をならす
            // （温泉の先の細い段差をならす Smooth は、ゴツゴツの崖まで平らにしたのでやめた。2026-10-14 てじゃ「頂上の崖がまっすぐになっちゃってる」）
            // ライトマップの UV2 は作り直さない（作り直すと焼いた影がずれて山じゅうにシミが出た。切った所は元の値を引き継ぎ、ふたは近くの点の値を借りる）
            Write(live, cut); Object.DestroyImmediate(cut);
            log.Add((isCol ? "mountain col" : "mountain") + ": cap tris " + tris + (bad > 0 ? " UNRESOLVED " + bad : "") + " " + clog);
        }
        if (mc != null) { var m = mc.sharedMesh; mc.sharedMesh = null; mc.sharedMesh = m; }

        // 2. 谷の壁と底だった岩（PitRock）は消す（山の切り口のふたが壁と底になる）
        var pit = root.Find("Rocks2/PitRock");
        if (pit != null) { Object.DestroyImmediate(pit.gameObject); log.Add("PitRock removed"); }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return string.Join("\n", log);
    }

    // 上の道の始まり（洞窟の上、x≒105.5）のはしを、欠けた形に（2026-10-14 てじゃ「頂上の崖、前はいい感じだったのにまっすぐになっちゃってる」。
    // 10-04 の PathBrokenEdge は道の作り直しで消えていた）。はしを 0.4〜0.9m ゆるくでこぼこに切り落として、切り口を FuwaCutCap でふさぐ（見た目も当たり判定も）
    const float EdgeX = 105.35f, EdgeZ = 3f, EdgeYBottom = 13.4f, EdgeHalf = 3.2f;
    static float EdgeCut(float l)
    {
        float u = l / 0.55f; int i0 = Mathf.FloorToInt(u); float f = Mathf.SmoothStep(0f, 1f, u - i0);
        float h0 = Mathf.PerlinNoise(i0 * 0.83f, 4.2f), h1 = Mathf.PerlinNoise((i0 + 1) * 0.83f, 4.2f);
        return 0.45f + Mathf.Lerp(h0, h1, f) * 0.5f;
    }
    static string UpperStartEdge(Transform pm)
    {
        var axis = Vector3.right; var origin = new Vector3(EdgeX, 0f, EdgeZ);
        var f = new FuwaCutCap.Frame { origin = origin, axis = axis, side = new Vector3(-axis.z, 0f, axis.x), e0 = l => -3f, e1 = EdgeCut, yBottom = EdgeYBottom };
        var log = new List<string>();
        // 当たり判定：三角形を消すだけ（CutJagged に通すと、この当たり判定のメッシュでは三角形がつぶれて道が大きく欠けた。2026-10-14）。はしの 0.5m くらいの差は遊びに影響しない
        {
            var colM = pm.GetComponent<MeshCollider>().sharedMesh;
            if (colM != null)
            {
                var l2w = pm.localToWorldMatrix; var V = colM.vertices; var T = colM.triangles; var keep = new List<int>(); int removed = 0;
                for (int i = 0; i < T.Length; i += 3)
                {
                    var c = (l2w.MultiplyPoint3x4(V[T[i]]) + l2w.MultiplyPoint3x4(V[T[i + 1]]) + l2w.MultiplyPoint3x4(V[T[i + 2]])) / 3f;
                    float s = Vector3.Dot(c - origin, axis), l = c.z - origin.z;
                    if (c.y > EdgeYBottom && Mathf.Abs(l) < EdgeHalf && s > -3f && s < EdgeCut(l)) { removed++; continue; }
                    keep.Add(T[i]); keep.Add(T[i + 1]); keep.Add(T[i + 2]);
                }
                colM.SetTriangles(keep, 0); colM.RecalculateBounds(); EditorUtility.SetDirty(colM);
                log.Add("upper edge col removed " + removed);
            }
        }
        foreach (var (live, edge) in new[] { (pm.GetComponent<MeshFilter>().sharedMesh, 0.08f) })
        {
            if (live == null) continue;
            var cut = FuwaPitCut.CutJagged(live, pm, origin, axis, f.e0, f.e1, f.yBottom, EdgeHalf, edge);
            var (tris, bad, clog) = FuwaCutCap.Cap(cut, pm, f, 0, 0f, 0f, 3);
            Write(live, cut); Object.DestroyImmediate(cut);
            log.Add("upper edge cap " + tris + (bad > 0 ? " UNRESOLVED " + bad + " " + clog : ""));
        }
        var mc = pm.GetComponent<MeshCollider>(); if (mc != null) { var m = mc.sharedMesh; mc.sharedMesh = null; mc.sharedMesh = m; }
        return string.Join("; ", log);
    }

    public static string RunPath()
    {
        var root = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaCourse2" && t.parent == null);
        var log = new List<string>();

        // 3. 道：頂上の削り＋谷の切り抜き（ふた付き）→ 割れた道の元（PreBroken）を更新 → 割れた道
        var asc = typeof(FuwaIwaIslands).GetMethod("ApplySummitCut", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        log.Add((string)asc.Invoke(null, null));
        var pm = root.Find("PathMesh");
        log.Add(UpperStartEdge(pm));
        foreach (var (live, bp) in new[] { (pm.GetComponent<MeshFilter>().sharedMesh, "Assets/FuwaCourse/Meshes/Backup/Course2Path_v2_Bevel_PreBroken.asset"), (pm.GetComponent<MeshCollider>().sharedMesh, "Assets/FuwaCourse/Meshes/Backup/Course2Path_v2_PreBroken.asset") })
        {
            var b = AssetDatabase.LoadAssetAtPath<Mesh>(bp);
            if (b == null) continue;
            Write(b, live);
        }
        AssetDatabase.SaveAssets();
        log.Add(FuwaCourse2BrokenPath.Run());

        // 4. 舌の足場の下の岩は置かない（2026-10-13 てじゃ「飛び出した道の下に岩追加はいらなかったかも、浮いてる感じでいい」）
        var ledge = root.Find("CollapseLedgeRocks"); if (ledge != null) { Object.DestroyImmediate(ledge.gameObject); log.Add("ledge rocks removed"); }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return string.Join("\n", log);
    }
}
