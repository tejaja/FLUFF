using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ステージ2 コース2–3「こおりのほら」ブロックアウト（2026-11-03 てじゃの構成案で作り直し。洞くつ・地底湖の版はなくした）。
// 決めごと：氷の道では人が滑る（FuwaIceSkate、氷のテスト場の C の方式）。ふわふわは道と同じで、触れたらアウト。
// 流れ（ロビーの Slot2 方向に放射状、+Z が進む向き。y=0 がスタートの島の地面）：
//  ① スタート ② 広い氷の道（氷の岩・結晶の柱をよける） ③ ぐねぐね道（まっすぐはふつう、カーブは氷）
//  ④ 枯れ木の並木（枝の先のつららが落ちる：いまは目印） ⑤ 雪道（氷の池、両側の丘の雪だるまが雪玉を転がす：いまは目印） → ゴール
// 道は銀のシマシマ、氷はつるっとした水色、雪は白。カーブは丸く曲がったメッシュ（Meshes/Course2_3）
public static class FuwaCourse2_3Builder
{
    const string RootName = "FuwaCourse2_3";
    const float SpokeR = 34.8f, StartBelow = 4f;
    public static float StripeLen = 1.2f;
    public static float IntroHW = 2.5f;
    public static float WideHW = 4f, WideLen = 36f;                    // ② 広い氷の道
    public static float WindHW = 1.8f, WindR = 6f;                     // ③ ぐねぐね道（カーブの半径は道のまん中）
    public static float TreeHW = 2.2f, TreeLen = 36f, TreeBankHW = 2.2f; public static int TreeCount = 6;   // ④ 枯れ木の並木
    public static float SnowHW = 3.2f, SnowLen = 42f, HillLen = 6f, HillRise = 1.8f;      // ⑤ 雪道と雪だるまの丘
    public static Color TreeCol = new Color(0.38f, 0.33f, 0.31f), IceRockCol = new Color(0.72f, 0.82f, 0.92f);
    // 道の色（2026-10-19 決めた 2–3 の色 #B4C2D2 から）。ふつうの道＝銀のシマシマ、氷＝つるっとした水色、雪（目印の玉）＝白
    public static Color PathColA = new Color(0.84f, 0.88f, 0.93f), PathColB = new Color(0.71f, 0.76f, 0.82f);
    public static Color IceCol = new Color(0.62f, 0.86f, 0.98f);
    public static Color SnowColA = new Color(0.97f, 0.98f, 1f), SnowColB = new Color(0.88f, 0.91f, 0.95f);
    public static Color RockCol = new Color(0.46f, 0.50f, 0.60f), CrystalCol = new Color(0.62f, 0.84f, 1f), MarkCol = new Color(1f, 0.5f, 0.2f);

    static Material pathA, pathB, iceFlat, snowA, snowB, rockMat, crystalMat, markMat, treeMat, iceRockMat;
    static int meshNo;
    const string MeshDir = "Assets/FuwaStage2/Meshes/Course2_3";
    static int pathLayer = 11;
    static FuwaBall ballProxy;
    static List<FuwaCheckpoint> cps = new List<FuwaCheckpoint>();
    static List<Collider> iceCols = new List<Collider>();

    [MenuItem("FuwaCourse/ステージ2/2–3 こおりのほら（ブロックアウト）を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var lobby = GameObject.Find("FuwaStage2/Lobby"); if (!lobby) return "ステージ2のロビーがない";
        var old = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;
        var lc = lobby.transform.Find("LobbySpawn").position; var slot = lobby.transform.Find("Slot2").position;
        var spoke = slot - lc; spoke.y = 0f; spoke.Normalize();
        float floorY = lobby.transform.Find("LobbyFloor").GetComponent<Renderer>().bounds.max.y;
        root.rotation = Quaternion.LookRotation(spoke);
        root.position = new Vector3(lc.x, floorY - StartBelow, lc.z) + spoke * SpokeR;

        pathA = PathMat("Course2_3_PathA", PathColA); pathB = PathMat("Course2_3_PathB", PathColB);   // ロビーの道もこの色（FuwaStage2LobbyCourses）
        iceFlat = PathMat("Course2_3_Ice", IceCol);
        snowA = PathMat("Course2_3_SnowA", SnowColA); snowB = PathMat("Course2_3_SnowB", SnowColB);
        rockMat = FuwaStage2WaterTest.ToonMat("Stage2_3_Rock", RockCol);
        crystalMat = FuwaStage2WaterTest.ToonMat("Stage2_3_Crystal", CrystalCol);
        markMat = FuwaStage2WaterTest.ToonMat("Stage2_3_Mark", MarkCol);
        treeMat = FuwaStage2WaterTest.ToonMat("Stage2_3_DeadTree", TreeCol);
        iceRockMat = FuwaStage2WaterTest.ToonMat("Stage2_3_IceRock", IceRockCol);
        if (!AssetDatabase.IsValidFolder("Assets/FuwaStage2/Meshes")) AssetDatabase.CreateFolder("Assets/FuwaStage2", "Meshes");
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaStage2/Meshes", "Course2_3");
        var pathRef = GameObject.Find("FuwaCourse3/PathMesh"); pathLayer = pathRef ? pathRef.layer : 11;
        ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
        cps.Clear(); iceCols.Clear();

        // 2026-11-03 てじゃの構成案：①スタート ②広めの氷の道（置いてある物をよける） ③ふつう〜やや細めのぐねぐね道、カーブが氷
        //  ④道の横に大きな枯れ木、枯れ木からつらら（落ちたら道に残る・当たりあり・落ちてる途中に当たると吹き飛ぶ） ⑤雪道、ところどころ氷の池、まわりの雪だるまが雪玉を転がす（どんどん大きく）
        // 決めたこと：つららで吹き飛ぶのはふわふわ（人は平気）、残ったつららはしばらくで溶ける、近づくと落ちる。雪玉はふわふわも人も吹き飛ばす。氷の池は人だけ滑る（ふわふわは道と同じでアウト）
        float z0 = StartSet(root);
        var path = new GameObject("Path").transform; path.SetParent(root, false);
        var deco = new GameObject("Marks").transform; deco.SetParent(root, false);
        meshNo = 0;
        var p = new Vector3(0, 0, z0 - 0.5f);
        var dir = Vector3.forward;

        // ② 広い氷の道：氷の岩と結晶の柱をよけて進む
        Strip(path, "Intro", p, dir, 5f, IntroHW, false); p += dir * 5f;
        Strip(path, "WideIn", p, dir, 3f, WideHW, false); p += dir * 3f;
        var iceAt = p;
        Strip(path, "WideIce", p, dir, WideLen, WideHW, true); p += dir * WideLen;
        var obstacles = new[] { (x: -1.2f, z: 4f, k: 0), (x: 2.2f, z: 8f, k: 1), (x: -2.6f, z: 12f, k: 0), (x: 0.6f, z: 15.5f, k: 1), (x: 3.0f, z: 19f, k: 0),
                                (x: -1.4f, z: 22f, k: 0), (x: -3.2f, z: 26f, k: 1), (x: 1.6f, z: 28.5f, k: 0), (x: -0.4f, z: 32f, k: 1) };
        var obsT = new GameObject("Obstacles").transform; obsT.SetParent(root, false);
        for (int k = 0; k < obstacles.Length; k++)
        {
            var at = iceAt + new Vector3(obstacles[k].x, 0, obstacles[k].z);
            if (obstacles[k].k == 1) Crystal(obsT, "Crystal" + k, at, 0.55f, 2.6f);
            else IceRock(obsT, "IceRock" + k, at, 1.1f + 0.25f * (k % 3), k * 37f);
        }
        Strip(path, "WideOut", p, dir, 3f, WideHW, false); p += dir * 3f;
        Checkpoint(root, "Checkpoint1", p - dir * 1.5f, dir, WideHW);

        // ③ ぐねぐね道：まっすぐはふつうの道、カーブは氷（丸く曲がったメッシュ）
        Strip(path, "WindIn", p, dir, 3f, WindHW, false); p += dir * 3f;
        var bends = new (float ang, float straight)[] { (-60f, 4f), (110f, 4f), (-110f, 4f), (60f, 3f) };
        for (int k = 0; k < bends.Length; k++)
        {
            Arc(path, "Bend" + k + "_Ice", ref p, ref dir, WindR, bends[k].ang, WindHW, true);
            Strip(path, "Wind" + k, p, dir, bends[k].straight, WindHW, false); p += dir * bends[k].straight;
        }
        Checkpoint(root, "Checkpoint2", p - dir * 1.5f, dir, WindHW);

        // ④ 枯れ木の並木：道の両側に大きな枯れ木、枝の先から道の上へつらら（いまは目印。仕掛けはあとで）
        Strip(path, "TreeIn", p, dir, 3f, TreeHW, false); p += dir * 3f;
        var treeAt = p; var treeDir = dir;
        Strip(path, "TreeRoad", p, dir, TreeLen, TreeHW, false); p += dir * TreeLen;
        var trees = new GameObject("DeadTrees").transform; trees.SetParent(root, false);
        var icicles = new GameObject("Icicles").transform; icicles.SetParent(root, false);
        var tr = Vector3.Cross(Vector3.up, treeDir);
        // 枯れ木の立つ雪の地面（道の両側、道より 0.25m 低い）。ふわふわが落ちたらアウト（道と同じレイヤー）
        foreach (float side in new[] { -1f, 1f })
            Strip(path, side < 0 ? "TreeBankL" : "TreeBankR", treeAt + tr * side * (TreeHW + TreeBankHW) - Vector3.up * 0.25f, treeDir, TreeLen, TreeBankHW, false, true);
        for (int k = 0; k < TreeCount; k++)
        {
            float side = (k % 2 == 0) ? -1f : 1f;
            float along = 3f + k * (TreeLen - 6f) / (TreeCount - 1);
            var basePos = treeAt + treeDir * along + tr * side * (TreeHW + 2.2f);
            var tips = DeadTree(trees, "DeadTree" + k, basePos, -tr * side, treeDir, k);
            for (int j = 0; j < tips.Length; j++) Icicle(icicles, "Icicle" + k + "_" + j, tips[j], 1.6f - 0.3f * j);
        }
        Checkpoint(root, "Checkpoint3", p - dir * 1.5f, dir, TreeHW);

        // ⑤ 雪道：ところどころ氷の池、両側の丘の雪だるまが雪玉を転がしてくる（いまは目印）
        Strip(path, "SnowIn", p, dir, 3f, SnowHW, false); p += dir * 3f;
        var snowAt = p; var snowDir = dir; var sr = Vector3.Cross(Vector3.up, snowDir);
        Strip(path, "SnowRoad", p, dir, SnowLen, SnowHW, false, true); p += dir * SnowLen;
        // 池は道のはしから 0.2m 以上内側（はみ出すと道の外に氷が浮く）
        var ponds = new[] { (x: -1.1f, z: 5f, rx: 1.9f, rz: 2.6f), (x: 1.2f, z: 13f, rx: 1.8f, rz: 3.2f), (x: -0.5f, z: 22f, rx: 2.5f, rz: 2.4f), (x: 0.8f, z: 31f, rx: 2.2f, rz: 3.0f), (x: -1.4f, z: 38f, rx: 1.6f, rz: 2.2f) };
        var pondT = new GameObject("IcePonds").transform; pondT.SetParent(root, false);
        for (int k = 0; k < ponds.Length; k++) Pond(pondT, "Pond" + k, snowAt + snowDir * ponds[k].z + sr * ponds[k].x, ponds[k].rx, ponds[k].rz, snowDir);
        var hills = new GameObject("SnowHills").transform; hills.SetParent(root, false);
        var men = new[] { (z: 8f, side: 1f), (z: 17f, side: -1f), (z: 26f, side: 1f), (z: 35f, side: -1f) };
        for (int k = 0; k < men.Length; k++)
        {
            var edge = snowAt + snowDir * men[k].z + sr * men[k].side * SnowHW;
            var outDir = sr * men[k].side;
            var up = new Vector3(outDir.x * HillLen, HillRise, outDir.z * HillLen);
            Strip(hills, "Hill" + k, edge, up.normalized, up.magnitude, 1.6f, false, true);
            var top = edge + up;
            Strip(hills, "HillTop" + k, top, outDir, 3f, 1.6f, false, true);
            Snowman(deco, "Snowman" + k, top + outDir * 1.8f, -outDir);
            var sb = GameObject.CreatePrimitive(PrimitiveType.Sphere); sb.name = "SnowballSource" + k; sb.transform.SetParent(deco, false);
            sb.transform.localPosition = top + outDir * 0.7f + Vector3.up * 0.45f; sb.transform.localScale = Vector3.one * 0.9f; sb.GetComponent<Renderer>().sharedMaterial = snowA;
            Object.DestroyImmediate(sb.GetComponent<Collider>());
        }
        Strip(path, "SnowOut", p, dir, 3f, IntroHW, false); p += dir * 3f;

        // ゴール（道の向きがまっすぐ前でない時も、ゴールの島は道の向きに置く）
        GoalSet(root, p, dir);

        // 人が滑る
        var sk = new GameObject("IceSkate"); sk.transform.SetParent(root, false);
        var skate = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaIceSkate>(sk);
        skate.ice = iceCols.ToArray();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(skate); FuwaStage2WaterTest.NoSync(skate);

        // アウト（下に落ちた）：コースの下（コースの向きの箱、まわり 10m）
        var lb = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
        foreach (var cl in root.GetComponentsInChildren<Collider>(true))
        {
            if (cl.isTrigger) continue;
            Vector3 cc, e; var t = cl.transform;
            if (cl is BoxCollider bc) { cc = bc.center; e = bc.size * 0.5f; }
            else if (cl is MeshCollider mc && mc.sharedMesh) { cc = mc.sharedMesh.bounds.center; e = mc.sharedMesh.bounds.extents; }
            else continue;
            for (int k = 0; k < 8; k++)
            {
                var c = cc + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z);
                var lp = root.InverseTransformPoint(t.TransformPoint(c));
                if (first) { lb = new Bounds(lp, Vector3.zero); first = false; } else lb.Encapsulate(lp);
            }
        }
        var outSize = new Vector3(lb.size.x + 20f, 2f, lb.size.z + 20f);
        Hazard(root, "BallOut", new Vector3(lb.center.x, -7f, lb.center.z), outSize, false);
        Hazard(root, "PlayerOut", new Vector3(lb.center.x, -9f, lb.center.z), outSize, true);

        RegisterCheckpoints();
        // ステージ2にいる時だけ表示
        var visb = Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(m => m && m.gameObject.scene.IsValid() && m.GetType().Name == "FuwaAreaVisibility");
        if (visb)
        {
            var so = new SerializedObject(visb); var arr = so.FindProperty("stage2Objects");
            bool has = false; for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == root.gameObject) has = true;
            for (int i = arr.arraySize - 1; i >= 0; i--) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(i);
            if (!has) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = root.gameObject; }
            so.ApplyModifiedProperties();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)visb);
        }
        FuwaStage2LobbyCourses.Run();
        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        return "[2–3] ゴールの位置 " + p.ToString("F1") + "、向き " + dir.ToString("F2") + "、範囲 " + lb.min.ToString("F0") + "〜" + lb.max.ToString("F0") + "、氷の当たり " + iceCols.Count + " 個";
    }

    // ---- 部品 ----
    static Material PathMat(string name, Color c)
    {
        string path = "Assets/FuwaStage2/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_1_PathA.mat");
            m = new Material(src); AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_Color", c);
        if (m.HasProperty("_UnderColor")) m.SetColor("_UnderColor", Color.Lerp(c, Color.white, 0.4f));
        EditorUtility.SetDirty(m);
        return m;
    }
    static GameObject Box(Transform p, string name, Vector3 lp, Vector3 size, Material m, int layer)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(p, false);
        g.transform.localPosition = lp; g.transform.localScale = size; g.layer = layer;
        if (m) g.GetComponent<Renderer>().sharedMaterial = m;
        return g;
    }
    // シマシマの道：a（手前のはしのまん中、上の面の高さ）から dir へ len。当たりは1枚の箱、見た目は StripeLen ごとに A/B。ice なら氷（人が滑る）、snow なら白
    static void Strip(Transform parent, string name, Vector3 a, Vector3 dir, float len, float hw, bool ice, bool snow = false)
    {
        if (len <= 0.01f) return;
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        g.localPosition = a; g.localRotation = Quaternion.LookRotation(dir);
        const float T = 0.6f;
        var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = pathLayer;
        var bc = col.AddComponent<BoxCollider>(); bc.center = new Vector3(0, -T * 0.5f, len * 0.5f); bc.size = new Vector3(hw * 2f, T, len);
        if (ice) iceCols.Add(bc);
        // ふつうの道＝銀のシマシマ、氷＝シマシマなしのつるっとした水色（1枚）
        var ma = ice ? iceFlat : pathA; var mb = ice ? iceFlat : pathB;
        if (snow) { ma = snowA; mb = snowB; }
        int n = ice ? 1 : Mathf.Max(1, Mathf.RoundToInt(len / StripeLen));
        for (int k = 0; k < n; k++)
        {
            float l = len / n;
            var s = Box(g, "S" + k, new Vector3(0, -T * 0.5f, l * (k + 0.5f)), new Vector3(hw * 2f, T, l), (k % 2 == 0) ? ma : mb, 0);
            Object.DestroyImmediate(s.GetComponent<Collider>());
        }
        var u = Box(g, "Under", new Vector3(0, -T - 0.75f, len * 0.5f), new Vector3(hw * 2f - 0.2f, 1.5f, len - 0.1f), rockMat, 0);
        Object.DestroyImmediate(u.GetComponent<Collider>());
    }
    // まっすぐな道を (長さ, 氷か) の順に切れ目なく並べる。p は最後のはしまで進む
    static void Seq(Transform parent, string name, ref Vector3 p, Vector3 dir, float hw, params (float len, bool ice)[] parts)
    {
        for (int k = 0; k < parts.Length; k++) { Strip(parent, name + "_" + k + (parts[k].ice ? "_Ice" : ""), p, dir, parts[k].len, hw, parts[k].ice); p += dir * parts[k].len; }
    }
    // 角の踊り場（道の幅の正方形）。入ってきた向き from のはし p から、出ていく向き to のはしを返す
    static Vector3 Corner(Transform parent, string name, Vector3 p, Vector3 from, Vector3 to, float hw, bool ice)
    {
        Strip(parent, name, p, from, hw * 2f, hw, ice);
        var c = p + from * hw;
        return c + to * hw;
    }
    // ---- 曲がった道 ----
    // p（はしのまん中、上の面の高さ）から dir の向きに入って、半径 R（道のまん中）で ang 度曲がる（＋が右）。p と dir は出口へ進む。
    // 見た目は1つのメッシュ（ふつうの道ならシマシマを2つのサブメッシュ、氷なら1つ）、当たりは同じ形の MeshCollider。はしのふたは作らない（となりの道とくっつく）
    static void Arc(Transform parent, string name, ref Vector3 p, ref Vector3 dir, float R, float ang, float hw, bool ice)
    {
        const float T = 0.6f;
        float s = Mathf.Sign(ang), a = Mathf.Abs(ang);
        var right0 = Vector3.Cross(Vector3.up, dir);
        var c = p + right0 * s * R;
        var v0 = p - c;
        float arcLen = a * Mathf.Deg2Rad * R;
        int n = Mathf.Max(4, Mathf.CeilToInt(a / 5f));                          // 5 度ごと
        int stripes = ice ? 1 : Mathf.Max(1, Mathf.RoundToInt(arcLen / StripeLen));
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        // 見た目（上の面＋両側＋下の面）と、下の岩（少し内側）
        var top = NewMesh(name); var under = NewMesh(name + "_Under");
        var tv = new List<Vector3>(); var tA = new List<int>(); var tB = new List<int>();
        var uv = new List<Vector3>(); var uT = new List<int>();
        for (int i = 0; i < n; i++)
        {
            float t0 = a * i / n, t1 = a * (i + 1) / n;
            var q0 = Quaternion.Euler(0, s * t0, 0); var q1 = Quaternion.Euler(0, s * t1, 0);
            var m0 = c + q0 * v0; var m1 = c + q1 * v0;
            var r0 = q0 * right0; var r1 = q1 * right0;
            int stripe = Mathf.Min(stripes - 1, Mathf.FloorToInt((t0 + t1) * 0.5f / a * stripes));
            var tris = (stripe % 2 == 0) ? tA : tB;
            Slab(tv, tris, m0, m1, r0, r1, hw, 0f, -T, Vector3.up);
            Slab(uv, uT, m0, m1, r0, r1, hw - 0.1f, -T, -T - 1.5f, Vector3.up);
        }
        top.SetVertices(tv);
        if (ice || tB.Count == 0) { top.subMeshCount = 1; top.SetTriangles(tA.Concat(tB).ToList(), 0); }
        else { top.subMeshCount = 2; top.SetTriangles(tA, 0); top.SetTriangles(tB, 1); }
        top.RecalculateNormals(); top.RecalculateBounds();
        under.SetVertices(uv); under.SetTriangles(uT, 0); under.RecalculateNormals(); under.RecalculateBounds();
        var vis = new GameObject("S"); vis.transform.SetParent(g, false);
        vis.AddComponent<MeshFilter>().sharedMesh = top;
        vis.AddComponent<MeshRenderer>().sharedMaterials = ice ? new[] { iceFlat } : (top.subMeshCount == 2 ? new[] { pathA, pathB } : new[] { pathA });
        var un = new GameObject("Under"); un.transform.SetParent(g, false);
        un.AddComponent<MeshFilter>().sharedMesh = under; un.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
        var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = pathLayer;
        var mc = col.AddComponent<MeshCollider>(); mc.sharedMesh = top;
        if (ice) iceCols.Add(mc);
        var q = Quaternion.Euler(0, s * a, 0);
        p = c + q * v0; dir = (q * dir).normalized;
    }
    // 曲がった板の1こま：m0→m1（まん中の線）、r0/r1（横の向き）、上の面 y0・下の面 y1（m の高さから）。上・下・外側・内側の4面
    static void Slab(List<Vector3> v, List<int> tris, Vector3 m0, Vector3 m1, Vector3 r0, Vector3 r1, float hw, float y0, float y1, Vector3 up)
    {
        Vector3 L0 = m0 - r0 * hw, R0 = m0 + r0 * hw, L1 = m1 - r1 * hw, R1 = m1 + r1 * hw;
        Vector3 Y0 = up * y0, Y1 = up * y1;
        var fwd = (m1 - m0).normalized;
        Quad(v, tris, L0 + Y0, R0 + Y0, R1 + Y0, L1 + Y0, up);                 // 上
        Quad(v, tris, L0 + Y1, L1 + Y1, R1 + Y1, R0 + Y1, -up);                // 下
        Quad(v, tris, R0 + Y0, R0 + Y1, R1 + Y1, R1 + Y0, (r0 + r1).normalized);   // 右の側面
        Quad(v, tris, L0 + Y0, L1 + Y0, L1 + Y1, L0 + Y1, -(r0 + r1).normalized);  // 左の側面
    }
    // 四角（a b c d の順にまわる）。表は want の向き（Unity は Cross(b-a, c-a) の向きが表）
    static void Quad(List<Vector3> v, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want)
    {
        int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
        bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), want) < 0f;
        if (!flip) tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        else tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
    }
    // メッシュはアセットに（シーンを重くしない）。同じ名前があれば中身を入れ直す（Clear(false)：前の列を残さない）
    static Mesh NewMesh(string name)
    {
        string path = MeshDir + "/" + name + ".asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m) { m.Clear(false); m.name = name; return m; }
        m = new Mesh { name = name }; AssetDatabase.CreateAsset(m, path); return m;
    }

    // ---- 置き物 ----
    // 氷の岩（よける障害物）：角を落とした箱を少し傾けて。当たりあり（ふわふわはアウトにしない）
    static void IceRock(Transform parent, string name, Vector3 at, float size, float yaw)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = at + Vector3.up * (size * 0.38f);
        g.transform.localRotation = Quaternion.Euler(8f, yaw, 12f);
        g.transform.localScale = new Vector3(size * 1.2f, size, size);
        g.GetComponent<Renderer>().sharedMaterial = iceRockMat;
    }
    // 枯れ木：太い幹＋道の上へ張り出す枝2本。枝の先（つららの下がる所）を返す
    static Vector3[] DeadTree(Transform parent, string name, Vector3 basePos, Vector3 toRoad, Vector3 along, int k)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = basePos;
        float h = 7.5f + (k % 3) * 0.6f;
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder); trunk.name = "Trunk"; trunk.transform.SetParent(g, false);
        trunk.transform.localPosition = Vector3.up * (h * 0.5f - 1f); trunk.transform.localScale = new Vector3(0.9f, h * 0.5f + 0.5f, 0.9f);
        trunk.GetComponent<Renderer>().sharedMaterial = treeMat;
        var tips = new List<Vector3>();
        var branches = new[] { (y: h - 2.6f, reach: TreeHW + 2.2f - 0.6f, fwd: -0.8f, rise: 0.9f), (y: h - 1.2f, reach: TreeHW + 2.2f + 0.6f, fwd: 0.9f, rise: 0.7f) };
        foreach (var b in branches)
        {
            var from = Vector3.up * b.y;
            var to = from + toRoad * b.reach + along * b.fwd + Vector3.up * b.rise;
            var br = GameObject.CreatePrimitive(PrimitiveType.Cube); br.name = "Branch"; br.transform.SetParent(g, false);
            br.transform.localPosition = (from + to) * 0.5f; br.transform.localRotation = Quaternion.LookRotation(to - from);
            br.transform.localScale = new Vector3(0.32f, 0.32f, (to - from).magnitude + 0.2f);
            br.GetComponent<Renderer>().sharedMaterial = treeMat;
            tips.Add(basePos + to);
        }
        return tips.ToArray();
    }
    // つらら（いまは目印：枝の先から下がるとがった柱。当たりなし）
    static void Icicle(Transform parent, string name, Vector3 tip, float len)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = tip;
        g.AddComponent<MeshFilter>().sharedMesh = IcicleMesh();
        g.AddComponent<MeshRenderer>().sharedMaterial = crystalMat;
        g.transform.localScale = new Vector3(1f, len, 1f);
    }
    static Mesh IcicleMesh()
    {
        var m = NewMesh("Icicle");
        const int N = 6; const float r = 0.22f;
        var v = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i < N; i++)
        {
            float a0 = Mathf.PI * 2f * i / N, a1 = Mathf.PI * 2f * (i + 1) / N;
            var p0 = new Vector3(Mathf.Cos(a0) * r, 0, Mathf.Sin(a0) * r); var p1 = new Vector3(Mathf.Cos(a1) * r, 0, Mathf.Sin(a1) * r);
            var tip = new Vector3(0, -1f, 0);
            int b = v.Count; v.Add(p0); v.Add(p1); v.Add(tip);
            var nrm = Vector3.Cross(p1 - p0, tip - p0);
            var outw = (p0 + p1) * 0.5f;
            if (Vector3.Dot(nrm, outw) >= 0) t.AddRange(new[] { b, b + 1, b + 2 }); else t.AddRange(new[] { b, b + 2, b + 1 });
            int c = v.Count; v.Add(p0); v.Add(p1); v.Add(Vector3.zero);   // ふた（上）
            if (Vector3.Cross(p1 - p0, Vector3.zero - p0).y >= 0) t.AddRange(new[] { c, c + 1, c + 2 }); else t.AddRange(new[] { c, c + 2, c + 1 });
        }
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    // 氷の池：雪道の上に薄い楕円の氷（道の面から 2cm、下の面は道とぴったりで裏向き）。人だけ滑る、ふわふわは道と同じで触れたらアウト
    static void Pond(Transform parent, string name, Vector3 at, float rx, float rz, Vector3 fwd)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = at + Vector3.up * 0.01f; g.transform.localRotation = Quaternion.LookRotation(fwd);
        g.transform.localScale = new Vector3(rx * 2f, 0.01f, rz * 2f);
        g.GetComponent<Renderer>().sharedMaterial = iceFlat;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.layer = pathLayer;
        var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = g.GetComponent<MeshFilter>().sharedMesh; mc.convex = true;
        iceCols.Add(mc);
    }
    // 雪だるま（いまは目印。当たりなし）：face の向きに顔
    static void Snowman(Transform parent, string name, Vector3 at, Vector3 face)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = at; g.localRotation = Quaternion.LookRotation(face);
        var parts = new[] { (y: 0.65f, d: 1.4f), (y: 1.65f, d: 1.0f), (y: 2.4f, d: 0.7f) };
        foreach (var pt in parts)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); s.name = "Body"; s.transform.SetParent(g, false);
            s.transform.localPosition = Vector3.up * pt.y; s.transform.localScale = Vector3.one * pt.d;
            s.GetComponent<Renderer>().sharedMaterial = snowA; Object.DestroyImmediate(s.GetComponent<Collider>());
        }
        var nose = GameObject.CreatePrimitive(PrimitiveType.Cube); nose.name = "Nose"; nose.transform.SetParent(g, false);
        nose.transform.localPosition = new Vector3(0, 2.4f, 0.4f); nose.transform.localScale = new Vector3(0.1f, 0.1f, 0.3f);
        nose.GetComponent<Renderer>().sharedMaterial = markMat; Object.DestroyImmediate(nose.GetComponent<Collider>());
    }

    // 結晶の柱（よける障害物。当たりあり、ふわふわが当たってもアウトにはしない）
    static void Crystal(Transform parent, string name, Vector3 at, float r, float h)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = at + Vector3.up * (h * 0.5f); g.transform.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);
        g.GetComponent<Renderer>().sharedMaterial = crystalMat;
    }
    static void Hazard(Transform root, string name, Vector3 lp, Vector3 size, bool player)
    {
        var g = new GameObject(name); g.transform.SetParent(root, false); g.transform.localPosition = lp; g.layer = player ? 0 : 4;
        var bc = g.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size;
        if (player) { var pc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(g); pc.ball = ballProxy; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(pc); FuwaStage2WaterTest.NoSync(pc); }
        else { var hz = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHazard>(g); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hz); FuwaStage2WaterTest.NoSync(hz); }
    }

    // スタート：コース1の Start を丸ごと写す（2–1・2–2 と同じ）。キノコの島の前にもう1本の木の橋。橋のはし（コースの道の始まり）の z を返す
    static float StartSet(Transform root)
    {
        var start = new GameObject("Start").transform; start.SetParent(root, false);
        var src = GameObject.Find("FuwaCourse/Start")?.transform;
        if (!src) { Debug.LogWarning("[2–3] FuwaCourse/Start がない"); return 0f; }
        var map = new Dictionary<Object, Object>();
        FuwaCourse2_1Builder.CopyChildren(src, start, null, map);
        foreach (var t in start.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("_Course1")) t.name = t.name.Replace("_Course1", "_Course2_3");
        float islandTop = src.Find("StartMushroom").localPosition.y;
        var area = start.Find("StartArea");
        var ground = area.Find("Ground"); var bridge = area.Find("Bridge"); var bridgeWood = area.Find("BridgeWood");
        var bc = bridge.GetComponent<BoxCollider>();
        float halfLen = (bc ? bc.size.z : 1f) * bridge.localScale.z * 0.5f;
        float frontZ = 2f * ground.localPosition.z - bridge.localPosition.z;
        start.localPosition = new Vector3(-ground.localPosition.x, -islandTop, -ground.localPosition.z);
        foreach (var b in new[] { bridge, bridgeWood })
        {
            var c = Object.Instantiate(b.gameObject, area, false); c.name = b.name + "_Front";
            c.transform.localPosition = new Vector3(b.localPosition.x, b.localPosition.y, 2f * ground.localPosition.z - b.localPosition.z);
            c.transform.localRotation = Quaternion.Euler(0, 180f, 0) * b.localRotation; c.transform.localScale = b.localScale;
        }
        FuwaCourse2_1Builder.FixCopied(start, map);
        FuwaCourse2_1Builder.Stage2Grass(start);
        return frontZ + halfLen - ground.localPosition.z;
    }

    // ゴール：コース1の Goal を丸ごと。道のはし p の先に木の橋 → 島
    static void GoalSet(Transform root, Vector3 p, Vector3 dir)
    {
        var goal = new GameObject("Goal").transform; goal.SetParent(root, false);
        var src = GameObject.Find("FuwaCourse/Goal")?.transform;
        if (!src) { Debug.LogWarning("[2–3] FuwaCourse/Goal がない"); return; }
        var map = new Dictionary<Object, Object>();
        FuwaCourse2_1Builder.CopyChildren(src, goal, null, map);
        var area = GameObject.Find("FuwaCourse/Start/StartArea")?.transform; var ground = area ? area.Find("Ground") : null; var gg = goal.Find("GoalGround");
        float far = 6.1f;
        if (ground && gg)
        {
            var br = area.Find("Bridge"); var bc = br ? br.GetComponent<BoxCollider>() : null;
            if (bc) far = Mathf.Abs(ground.localPosition.z - br.localPosition.z) + bc.size.z * br.localScale.z * 0.5f;
            foreach (var nm in new[] { "Bridge", "BridgeWood" })
            {
                var b = area.Find(nm); if (!b) continue;
                var c = Object.Instantiate(b.gameObject, goal, false); c.name = nm;
                c.transform.localPosition = new Vector3(0f, gg.localPosition.y + b.localPosition.y - ground.localPosition.y, -Mathf.Abs(ground.localPosition.z - b.localPosition.z));
                c.transform.localRotation = b.localRotation; c.transform.localScale = b.localScale;
            }
        }
        goal.localPosition = p + dir * (far - 0.3f) + Vector3.up * 0.02f;
        goal.localRotation = Quaternion.LookRotation(dir);
        FuwaCourse2_1Builder.FixCopied(goal, map);
        FuwaCourse2_1Builder.Stage2Grass(goal);
    }

    // チェックポイント：ステージ1の FuwaCourse3/Checkpoint1 を写す（2–2 と同じ）
    static void Checkpoint(Transform root, string name, Vector3 at, Vector3 dir, float hw)
    {
        var tmpl = GameObject.Find("FuwaCourse3/Checkpoint1"); if (!tmpl) return;
        var g = root.Find("Checkpoints"); if (!g) { g = new GameObject("Checkpoints").transform; g.SetParent(root, false); }
        var cp = Object.Instantiate(tmpl, g, false); cp.name = name;
        cp.transform.localPosition = at; cp.transform.localRotation = Quaternion.LookRotation(dir);
        foreach (var n in new[] { "Post_L", "Post_R", "Bar" }) { var c = cp.transform.Find(n); if (c) Object.DestroyImmediate(c.gameObject); }
        var trig = cp.transform.Find("Trigger");
        if (trig) trig.localScale = new Vector3(hw * 2f + 0.8f, trig.localScale.y, trig.localScale.z);
        var gm = cp.transform.Find("GlowMushrooms");
        if (gm) foreach (Transform cl in gm) cl.localPosition = new Vector3(Mathf.Sign(cl.localPosition.x) * (hw + 0.55f), cl.localPosition.y, 1.5f);
        var fc = cp.GetComponentInChildren<FuwaCheckpoint>(true);
        if (fc) { UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fc); FuwaStage2WaterTest.NoSync(fc); cps.Add(fc); }
    }
    static void RegisterCheckpoints()
    {
        var ball = Object.FindObjectsOfType<FuwaBall>(true).FirstOrDefault(); if (!ball) return;
        var so = new SerializedObject(ball); var arr = so.FindProperty("checkpoints");
        for (int k = arr.arraySize - 1; k >= 0; k--) if (arr.GetArrayElementAtIndex(k).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(k);
        foreach (var fc in cps) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = fc; }
        so.ApplyModifiedProperties();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ball);
    }
}
