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
            if (!isCol) clog += " smoothed " + FuwaMeshUnfold.Smooth(cut, mt, new Bounds(new Vector3(111f, 13.6f, 0.3f), new Vector3(6f, 0.9f, 1.3f)), 12);
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

    public static string RunPath()
    {
        var root = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaCourse2" && t.parent == null);
        var log = new List<string>();

        // 3. 道：頂上の削り＋谷の切り抜き（ふた付き）→ 割れた道の元（PreBroken）を更新 → 割れた道
        var asc = typeof(FuwaIwaIslands).GetMethod("ApplySummitCut", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        log.Add((string)asc.Invoke(null, null));
        var pm = root.Find("PathMesh");
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
