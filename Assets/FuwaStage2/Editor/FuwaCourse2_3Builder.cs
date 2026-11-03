using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ステージ2 コース2–3「こおりのみち」ブロックアウト（2026-11-03 てじゃ「ほらちゃうしな」で こおりのほら から名前を変えた）（2026-11-03 てじゃの構成案の2回目で作り直し。洞くつ・地底湖・ぐねぐね道の版はなくした）。
// 決めごと：氷の道では人が滑る（FuwaIceSkate、氷のテスト場の C の方式）。ふわふわは道と同じで、触れたらアウト。
// 流れ（ロビーの Slot2 方向に放射状、+Z が進む向き。y=0 がスタートの島の地面）：
//  ① スタート ② 広い氷のエリア（前半 障害物よけ、後半 割れた氷と吹雪） ③ 大きな池の流氷（前半 止まる、後半 動く）
//  ④ つららの並木（いろんな角度に曲がる道） ⑤ ゴール前の坂（雪だるまの雪玉、左右に弧の氷の道） → ゴール
// 道は銀のシマシマ、氷はつるっとした水色、雪は白。曲がった道・割れた氷・流氷はメッシュ（Meshes/Course2_3）。仕掛け（吹雪・動く流氷・つらら・雪玉）はまだ目印だけ
public static class FuwaCourse2_3Builder
{
    const string RootName = "FuwaCourse2_3";
    const float SpokeR = 34.8f, StartBelow = 4f;
    public static float StripeLen = 1.2f;
    public static float IntroHW = 2.5f;
    // ② 広い氷のエリア（前半 障害物、後半 割れた氷＋吹雪）
    public static float WideHW = 5f, ObsLen = 24f, CrackLen = 26f, CrackT = 1.0f, CrackWaterY = -0.7f;
    // 割れた所（1m のます目。ix は左はし=0〜9、iz は手前=0〜25、w×h ます）。どこを通っても1本は道がつながるように
    public static (int ix, int iz, int w, int h)[] CrackHoles = {
        (1, 2, 2, 2), (6, 3, 3, 1), (4, 6, 2, 3), (8, 8, 2, 2), (0, 10, 3, 1), (5, 11, 1, 3), (2, 14, 2, 2), (7, 15, 2, 2),
        (3, 18, 4, 1), (0, 20, 2, 2), (8, 21, 2, 1), (5, 22, 2, 2) };
    // ③ 大きな池と流氷（x, z＝池の手前からの位置、r＝半径、type 0=滑らない 1=滑る 2=半分ずつ、move＝動く幅（0 は止まっている））
    public static float PondHW = 8f, PondLen = 34.3f, PondWaterY = -0.5f, FloeTop = -0.3f;
    public static (float x, float z, float r, int type, float move)[] Floes = {
        (0f, 2.6f, 1.6f, 0, 0f), (-2.0f, 6.4f, 1.5f, 1, 0f), (0.5f, 9.8f, 1.7f, 2, 0f), (2.8f, 13.3f, 1.4f, 1, 0f), (0.6f, 16.9f, 1.6f, 0, 0f),
        (-1.0f, 20.6f, 1.5f, 1, 1.2f), (1.2f, 24.2f, 1.6f, 2, 1.5f), (-0.8f, 27.9f, 1.5f, 0, 1.2f), (0.6f, 31.6f, 1.7f, 2, 0.8f) };
    // ④ つららの並木（len＝まっすぐの長さ、trees＝そこに立てる枯れ木の数、bend＝そのあと曲がる角度 ＋が右）
    public static float TreeHW = 2.2f, TreePadHW = 1.6f, TreeBendR = 5f;
    public static (float len, int trees, float bend)[] TreeSegs = { (8f, 1, 30f), (9f, 2, -45f), (8f, 1, 35f), (9f, 2, -25f), (6f, 1, 0f) };
    // ⑤ ゴール前（ゆるい上り坂、まん中の雪道と左右の弧の氷の道）
    // 両わきの氷の幅：手前 SideWBottom → 奥 SideWTop（ハの字）、SideBulge＝外のふちのふくらみ（0 でまっすぐ）
    // まん中の雪道の半分の幅も 手前 LaneHWBottom → 奥 LaneHWTop（2026-11-03 てじゃ「道ごと広げて」）
    public static float PlazaLen = 12f;   // 坂の手前の広場（細い道から坂の下の幅まで、じょじょに広がる）
    public static float FinalHW = 4.5f, LaneHWBottom = 7.0f, LaneHWTop = 1.0f, SideWBottom = 6.0f, SideWTop = 2.0f, SideBulge = 0f, FlareK = 2.2f, FinalLen = 34f, FinalRise = 3f, FinalTopLen = 6f;
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

    [MenuItem("FuwaCourse/ステージ2/2–3 こおりのみち（ブロックアウト）を作り直す")]
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

        // 2026-11-03 てじゃの構成案（2回目）：
        //  ①スタート ②広めの氷のエリア：前半は障害物をよける、後半は吹雪（左右交互にたまに、ややつよい・短い）＋ところどころ割れていて下は水
        //  ③大きな池：流氷をジャンプで渡る、前半は止まっている・後半は動く ④つららの並木：いろんな角度に曲がる道
        //  ⑤ゴール前：雪だるま1体が雪玉をまっすぐ転がす（どんどん巨大に）。雪玉の通るまん中は雪道（滑らない）、左右に弧を描く細い氷の道で迂回できる。雪だるまの横を通り越して後ろにゴール
        // 決めたこと：吹雪はふわふわだけ押す（人は押さない）。水は人が落ちたらアウト、ふわふわが触れたら凍ってアウト。
        //  流氷は滑る・滑らない・半分ずつをまぜる、動く流氷は乗っている人も運ぶ、ふわふわは流氷に触れたらアウト（足場＝道あつかい）。
        //  つららで吹き飛ぶのはふわふわ（人は平気）、残ったつららはしばらくで溶ける、近づくと落ちる。雪玉はふわふわも人も吹き飛ばす
        float z0 = StartSet(root);
        var path = new GameObject("Path").transform; path.SetParent(root, false);
        var deco = new GameObject("Marks").transform; deco.SetParent(root, false);
        var waterT = new GameObject("Water").transform; waterT.SetParent(root, false);
        meshNo = 0;
        var p = new Vector3(0, 0, z0 - 0.5f);
        var dir = Vector3.forward;

        // ② 広い氷のエリア 前半：氷の岩と結晶の柱をよける
        Strip(path, "Intro", p, dir, 5f, IntroHW, false); p += dir * 5f;
        Strip(path, "WideIn", p, dir, 3f, WideHW, false); p += dir * 3f;
        var iceAt = p;
        Strip(path, "WideIce", p, dir, ObsLen, WideHW, true); p += dir * ObsLen;
        var obstacles = new[] { (x: -1.6f, z: 4f, k: 0), (x: 2.6f, z: 7.5f, k: 1), (x: -3.0f, z: 11f, k: 0), (x: 0.8f, z: 13.5f, k: 1),
                                (x: 3.4f, z: 16.5f, k: 0), (x: -1.8f, z: 19f, k: 0), (x: 1.4f, z: 22f, k: 1) };
        var obsT = new GameObject("Obstacles").transform; obsT.SetParent(root, false);
        for (int k = 0; k < obstacles.Length; k++)
        {
            var at = iceAt + new Vector3(obstacles[k].x, 0, obstacles[k].z);
            if (obstacles[k].k == 1) Crystal(obsT, "Crystal" + k, at, 0.55f, 2.6f);
            else IceRock(obsT, "IceRock" + k, at, 1.1f + 0.25f * (k % 3), k * 37f);
        }
        // ② 後半：割れた氷（1m のます目で穴、下は水）＋吹雪
        var crackAt = p;
        CrackedFloor(path, "CrackedIce", crackAt, CrackLen, WideHW, CrackHoles);
        Water(waterT, "CrackWater", crackAt + new Vector3(0, CrackWaterY, CrackLen * 0.5f), WideHW * 2f, CrackLen);
        var bz = new GameObject("BlizzardZone"); bz.transform.SetParent(root, false);   // 吹雪のかかる所（仕掛けはあとで。ふわふわだけ押す）
        bz.transform.localPosition = crackAt + new Vector3(0, 3f, CrackLen * 0.5f);
        var bzc = bz.AddComponent<BoxCollider>(); bzc.isTrigger = true; bzc.size = new Vector3(WideHW * 2f + 6f, 8f, CrackLen);
        p += dir * CrackLen;
        Strip(path, "WideOut", p, dir, 3f, WideHW, false); p += dir * 3f;
        Checkpoint(root, "Checkpoint1", p - dir * 1.5f, dir, WideHW);

        // ③ 大きな池：流氷をジャンプで渡る（前半は止まっている、後半は動く）
        Strip(path, "PondIn", p, dir, 3f, IntroHW, false); p += dir * 3f;
        var pondAt = p;
        Water(waterT, "PondWater", pondAt + new Vector3(0, PondWaterY, PondLen * 0.5f), PondHW * 2f, PondLen);
        var basin = Box(waterT, "PondBasin", pondAt + new Vector3(0, PondWaterY - 0.05f - 0.75f, PondLen * 0.5f), new Vector3(PondHW * 2f + 0.6f, 1.5f, PondLen), rockMat, 0);
        Object.DestroyImmediate(basin.GetComponent<Collider>());   // 人は水に沈んで下のアウトへ
        var floeT = new GameObject("Floes").transform; floeT.SetParent(root, false);
        for (int k = 0; k < Floes.Length; k++)
        {
            var f = Floes[k];
            Floe(floeT, "Floe" + k + (f.move > 0 ? "_Move" : ""), pondAt + new Vector3(f.x, FloeTop, f.z), f.r, f.type, 2562 + k * 17);
        }
        p += dir * PondLen;
        Strip(path, "PondOut", p, dir, 3f, IntroHW, false); p += dir * 3f;
        Checkpoint(root, "Checkpoint2", p - dir * 1.5f, dir, IntroHW);

        // ④ つららの並木：いろんな角度に曲がる道。まっすぐの所の横に枯れ木（1本ずつ雪の台にのせる）、枝の先から道の上へつらら（いまは目印）
        var trees = new GameObject("DeadTrees").transform; trees.SetParent(root, false);
        var icicles = new GameObject("Icicles").transform; icicles.SetParent(root, false);
        int treeNo = 0;
        for (int k = 0; k < TreeSegs.Length; k++)
        {
            var seg = TreeSegs[k];
            var segAt = p; var segDir = dir; var segR = Vector3.Cross(Vector3.up, segDir);
            Strip(path, "TreeRoad" + k, p, dir, seg.len, TreeHW, false); p += dir * seg.len;
            // 曲がる外側（＝見通しのいい側）と内側に交互。曲がる直前・直後はさける
            for (int j = 0; j < seg.trees; j++)
            {
                float side = (treeNo % 2 == 0) ? -1f : 1f;
                float along = seg.len * (j + 1f) / (seg.trees + 1f);
                float off = TreeHW + TreePadHW;
                Strip(path, "TreePad" + treeNo, segAt + segDir * (along - TreePadHW) + segR * side * off - Vector3.up * 0.25f, segDir, TreePadHW * 2f, TreePadHW, false, true);
                var tips = DeadTree(trees, "DeadTree" + treeNo, segAt + segDir * along + segR * side * off, -segR * side, segDir, treeNo, off);
                for (int t = 0; t < tips.Length; t++) Icicle(icicles, "Icicle" + treeNo + "_" + t, tips[t], 1.6f - 0.3f * t);
                treeNo++;
            }
            if (Mathf.Abs(seg.bend) > 0.5f) Arc(path, "TreeBend" + k, ref p, ref dir, TreeBendR, seg.bend, TreeHW, false);
        }
        Checkpoint(root, "Checkpoint3", p - dir * 1.5f, dir, TreeHW);

        // ⑤ ゴール前：ゆるい上り坂。まん中は雪道（雪玉の通り道・滑らない）、左右に弧を描く細い氷の道。てっぺんに雪だるま、その後ろにゴール
        // 2026-11-03 てじゃ「氷と道はつなげてほしい、ハの字みたいに手前ほど広く」→ 雪道の両わきに氷をくっつけて、外のふちは手前ほど外へ（雪玉が大きくなる手前ほどよけやすい）
        //  さらに「道ごと広げて」→ まん中の雪道も手前ほど広いハの字に（雪道は左右の半分ずつ、まん中で継ぎ目なしにくっつける）
        //  てじゃ「手前のつながり部分、いきなりドンと広がるんじゃなくてワンクッション広場を置こう」→ 細い道 → 広場 → 坂の下の台
        //  「じょじょに広くなってくように」→ 広場は道の幅から坂の下の幅まで、なめらかに（smoothstep）広がる。左右の半分をまん中でくっつける
        {
            var pr = Vector3.Cross(Vector3.up, dir);
            int pn = Mathf.Max(4, Mathf.RoundToInt(PlazaLen / StripeLen));
            float w0 = TreeHW, w1 = LaneHWBottom + SideWBottom;
            foreach (float side in new[] { -1f, 1f })
            {
                var mid = new List<Vector3>(); var edge = new List<Vector3>();
                for (int i = 0; i <= pn; i++)
                {
                    float t = (float)i / pn, s = t * t * (3f - 2f * t);
                    var c = p + dir * (PlazaLen * t);
                    mid.Add(c); edge.Add(c + pr * side * Mathf.Lerp(w0, w1, s));
                }
                Band(path, side < 0 ? "FinalPlazaL" : "FinalPlazaR", mid, edge, false, false, 0f);
            }
            p += dir * PlazaLen;
        }
        Strip(path, "FinalBase", p, dir, 3f, LaneHWBottom + SideWBottom, false); p += dir * 3f;
        var fAt = p; var fDir = dir; var fR = Vector3.Cross(Vector3.up, fDir);
        var rise = new Vector3(0, FinalRise, 0) + fDir * FinalLen;
        int fn = Mathf.Max(4, Mathf.RoundToInt(FinalLen / StripeLen));
        foreach (float side in new[] { -1f, 1f })
        {
            var mid = new List<Vector3>(); var inner = new List<Vector3>(); var outer = new List<Vector3>();
            for (int i = 0; i <= fn; i++)
            {
                float t = (float)i / fn;
                var c = fAt + rise * t;
                // てじゃ「それぞれの直線を曲げたい、手前にくるほど角度が急に」→ 幅は (1-t)^FlareK で開く（ラッパの口のよう）
                float f = Mathf.Pow(1f - t, FlareK);
                float lw = LaneHWTop + (LaneHWBottom - LaneHWTop) * f;
                float w = SideWTop + (SideWBottom - SideWTop) * f + SideBulge * Mathf.Sin(Mathf.PI * t);
                mid.Add(c); inner.Add(c + fR * side * lw); outer.Add(c + fR * side * (lw + w));
            }
            Band(path, side < 0 ? "SnowLaneL" : "SnowLaneR", mid, inner, false, true, 0f);
            Band(path, side < 0 ? "SideIceL" : "SideIceR", inner, outer, true);
        }
        p += rise;
        Strip(path, "FinalTop", p, dir, FinalTopLen, FinalHW, false, true);
        Snowman(deco, "Snowman", p + dir * 2.2f, -dir);
        var sbm = GameObject.CreatePrimitive(PrimitiveType.Sphere); sbm.name = "SnowballSource"; sbm.transform.SetParent(deco, false);
        sbm.transform.localPosition = p + dir * 0.9f + Vector3.up * 0.45f; sbm.transform.localScale = Vector3.one * 0.9f; sbm.GetComponent<Renderer>().sharedMaterial = snowA;
        Object.DestroyImmediate(sbm.GetComponent<Collider>());
        p += dir * FinalTopLen;

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

    // 曲がった細い道：pts（まん中の線、上の面の高さ）にそった板。横の向きは前後の点から（つなぎ目で角がそろう）。ice なら氷、snow なら雪、ほかはシマシマ
    static void Ribbon(Transform parent, string name, List<Vector3> pts, float hw, bool ice, bool snow = false)
    {
        const float T = 0.6f;
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        var rights = new Vector3[pts.Count];
        for (int i = 0; i < pts.Count; i++)
        {
            var tg = pts[Mathf.Min(pts.Count - 1, i + 1)] - pts[Mathf.Max(0, i - 1)]; tg.y = 0f;
            rights[i] = Vector3.Cross(Vector3.up, tg.normalized).normalized;
        }
        var top = NewMesh(name); var under = NewMesh(name + "_Under");
        var tv = new List<Vector3>(); var tA = new List<int>(); var tB = new List<int>();
        var uv = new List<Vector3>(); var uT = new List<int>();
        float run = 0f;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            float segLen = (pts[i + 1] - pts[i]).magnitude;
            int stripe = Mathf.FloorToInt((run + segLen * 0.5f) / StripeLen); run += segLen;
            var tris = (!ice && !snow && stripe % 2 == 1) || (snow && stripe % 2 == 1) ? tB : tA;
            Slab(tv, tris, pts[i], pts[i + 1], rights[i], rights[i + 1], hw, 0f, -T, Vector3.up);
            Slab(uv, uT, pts[i], pts[i + 1], rights[i], rights[i + 1], hw - 0.1f, -T, -T - 1.5f, Vector3.up);
        }
        top.SetVertices(tv);
        if (ice || tB.Count == 0) { top.subMeshCount = 1; top.SetTriangles(tA.Concat(tB).ToList(), 0); }
        else { top.subMeshCount = 2; top.SetTriangles(tA, 0); top.SetTriangles(tB, 1); }
        top.RecalculateNormals(); top.RecalculateBounds();
        under.SetVertices(uv); under.SetTriangles(uT, 0); under.RecalculateNormals(); under.RecalculateBounds();
        var vis = new GameObject("S"); vis.transform.SetParent(g, false);
        vis.AddComponent<MeshFilter>().sharedMesh = top;
        var ma = snow ? snowA : pathA; var mb = snow ? snowB : pathB;
        vis.AddComponent<MeshRenderer>().sharedMaterials = ice ? new[] { iceFlat } : (top.subMeshCount == 2 ? new[] { ma, mb } : new[] { ma });
        var un = new GameObject("Under"); un.transform.SetParent(g, false);
        un.AddComponent<MeshFilter>().sharedMesh = under; un.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
        var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = pathLayer;
        var mc = col.AddComponent<MeshCollider>(); mc.sharedMesh = top;
        if (ice) iceCols.Add(mc);
    }

    // 内と外のふちの線で決める板（雪道のわきにくっつける氷）。内側の側面・はしのふたは作らない（となりとくっつく）。下に岩
    // snow なら雪のシマシマ（1こまごとに A/B）。innerInset＝下の岩の内側のふちを内へずらす量（雪道の半分どうしは 0、氷は雪道の岩にそろえて 0.1）
    static void Band(Transform parent, string name, List<Vector3> inner, List<Vector3> outer, bool ice, bool snow = false, float innerInset = 0.1f)
    {
        const float T = 0.6f;
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        var top = NewMesh(name); var under = NewMesh(name + "_Under");
        var v = new List<Vector3>(); var t = new List<int>(); var t2 = new List<int>(); var uv = new List<Vector3>(); var ut = new List<int>();
        var dn = Vector3.down * T; var dn2 = Vector3.down * (T + 1.5f);
        for (int i = 0; i + 1 < inner.Count; i++)
        {
            Vector3 a = inner[i], b = outer[i], c = outer[i + 1], d = inner[i + 1];
            var outw = ((b - a) + (c - d)); outw.y = 0f; outw.Normalize();
            var tt = (!ice && i % 2 == 1) ? t2 : t;
            Quad(v, tt, a, b, c, d, Vector3.up);
            Quad(v, tt, a + dn, d + dn, c + dn, b + dn, Vector3.down);
            Quad(v, tt, b, b + dn, c + dn, c, outw);
            Vector3 ai = a - outw * innerInset, di = d - outw * innerInset, bo = b - outw * 0.1f, co = c - outw * 0.1f;
            Quad(uv, ut, ai + dn, bo + dn, co + dn, di + dn, Vector3.up);
            Quad(uv, ut, ai + dn2, di + dn2, co + dn2, bo + dn2, Vector3.down);
            Quad(uv, ut, bo + dn, bo + dn2, co + dn2, co + dn, outw);
        }
        top.SetVertices(v);
        if (t2.Count == 0) { top.subMeshCount = 1; top.SetTriangles(t, 0); } else { top.subMeshCount = 2; top.SetTriangles(t, 0); top.SetTriangles(t2, 1); }
        top.RecalculateNormals(); top.RecalculateBounds();
        under.SetVertices(uv); under.SetTriangles(ut, 0); under.RecalculateNormals(); under.RecalculateBounds();
        var vis = new GameObject("S"); vis.transform.SetParent(g, false);
        vis.AddComponent<MeshFilter>().sharedMesh = top;
        var ma = snow ? snowA : pathA; var mb = snow ? snowB : pathB;
        vis.AddComponent<MeshRenderer>().sharedMaterials = ice ? new[] { iceFlat } : (t2.Count == 0 ? new[] { ma } : new[] { ma, mb });
        var un = new GameObject("Under"); un.transform.SetParent(g, false);
        un.AddComponent<MeshFilter>().sharedMesh = under; un.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
        var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = pathLayer;
        var mc = col.AddComponent<MeshCollider>(); mc.sharedMesh = top;
        if (ice) iceCols.Add(mc);
    }

    // 割れた氷の床：a（手前のはしのまん中）から +Z へ len、幅 hw×2。1m のます目で holes の所に穴（下は水）。
    // 1つのメッシュ：のこったます目の上・下の面と、穴・外のふちにだけ側面（となり合うます目の間には面を作らない＝重なりなし）
    static void CrackedFloor(Transform parent, string name, Vector3 a, float len, float hw, (int ix, int iz, int w, int h)[] holes)
    {
        int nx = Mathf.RoundToInt(hw * 2f), nz = Mathf.RoundToInt(len);
        var solid = new bool[nx, nz];
        for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++) solid[x, z] = true;
        foreach (var h in holes) for (int x = h.ix; x < h.ix + h.w; x++) for (int z = h.iz; z < h.iz + h.h; z++) if (x >= 0 && x < nx && z >= 0 && z < nz) solid[x, z] = false;
        System.Func<int, int, bool> S = (x, z) => x >= 0 && x < nx && z >= 0 && z < nz && solid[x, z];
        float cw = hw * 2f / nx, cl = len / nz, T = CrackT;
        var v = new List<Vector3>(); var t = new List<int>();
        for (int x = 0; x < nx; x++) for (int z = 0; z < nz; z++)
        {
            if (!solid[x, z]) continue;
            float x0 = -hw + x * cw, x1 = x0 + cw, z0 = z * cl, z1 = z0 + cl;
            Quad(v, t, new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), new Vector3(x0, 0, z1), Vector3.up);
            Quad(v, t, new Vector3(x0, -T, z0), new Vector3(x0, -T, z1), new Vector3(x1, -T, z1), new Vector3(x1, -T, z0), Vector3.down);
            if (!S(x - 1, z)) Quad(v, t, new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x0, -T, z1), new Vector3(x0, -T, z0), Vector3.left);
            if (!S(x + 1, z)) Quad(v, t, new Vector3(x1, 0, z0), new Vector3(x1, -T, z0), new Vector3(x1, -T, z1), new Vector3(x1, 0, z1), Vector3.right);
            if (!S(x, z - 1)) Quad(v, t, new Vector3(x0, 0, z0), new Vector3(x0, -T, z0), new Vector3(x1, -T, z0), new Vector3(x1, 0, z0), Vector3.back);
            if (!S(x, z + 1)) Quad(v, t, new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, -T, z1), new Vector3(x0, -T, z1), Vector3.forward);
        }
        var m = NewMesh(name);
        m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = a;
        g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = iceFlat;
        g.layer = pathLayer;
        var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = m;
        iceCols.Add(mc);
    }

    // 水：見た目の面（上向き）＋ふわふわのアウト（水面）＋人のアウト（水の下）
    static void Water(Transform parent, string name, Vector3 center, float w, float l)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = name; q.transform.SetParent(parent, false);
        q.transform.localPosition = center; q.transform.localRotation = Quaternion.Euler(90f, 0, 0); q.transform.localScale = new Vector3(w, l, 1f);
        Object.DestroyImmediate(q.GetComponent<Collider>());
        var wm = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_Water_Still.mat");
        q.GetComponent<Renderer>().sharedMaterial = wm ? wm : crystalMat;
        Hazard(parent, name + "_BallOut", center, new Vector3(w, 0.3f, l), false);
        Hazard(parent, name + "_PlayerOut", center + Vector3.down * 0.9f, new Vector3(w, 0.6f, l), true);
    }

    // 流氷：少しでこぼこした多角形の板（上の面 c.y、厚み 0.8）。type 0＝上に雪（滑らない）、1＝氷（滑る）、2＝線で半分に分けて片方が氷・片方が雪
    static void Floe(Transform parent, string name, Vector3 c, float r, int type, int seed)
    {
        var rnd = new System.Random(seed);
        System.Func<float, float, float> R = (lo, hi) => lo + (float)rnd.NextDouble() * (hi - lo);
        const int N = 8; const float T = 0.8f;
        var poly = new List<Vector2>();
        float a0 = R(0f, 360f);
        for (int i = 0; i < N; i++) { float ang = (a0 + 360f * i / N + R(-10f, 10f)) * Mathf.Deg2Rad; float rr = r * R(0.86f, 1.06f); poly.Add(new Vector2(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr)); }
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = c;
        if (type == 2)
        {
            float cut = R(0f, 180f) * Mathf.Deg2Rad; var n = new Vector2(Mathf.Cos(cut), Mathf.Sin(cut));
            FloePiece(g, name + "_Ice", Clip(poly, n), T, true);
            FloePiece(g, name + "_Snow", Clip(poly, -n), T, false);
        }
        else FloePiece(g, name + (type == 1 ? "_Ice" : "_Snow"), poly, T, type == 1);
    }
    // 凸の多角形を n・p ≥ 0 の側だけ残して切る
    static List<Vector2> Clip(List<Vector2> poly, Vector2 n)
    {
        var o = new List<Vector2>();
        for (int i = 0; i < poly.Count; i++)
        {
            var A = poly[i]; var B = poly[(i + 1) % poly.Count];
            float da = Vector2.Dot(A, n), db = Vector2.Dot(B, n);
            if (da >= 0f) o.Add(A);
            if ((da >= 0f) != (db >= 0f)) o.Add(A + (B - A) * (da / (da - db)));
        }
        return o;
    }
    static void FloePiece(Transform g, string name, List<Vector2> poly, float T, bool ice)
    {
        var v = new List<Vector3>(); var top = new List<int>(); var side = new List<int>();
        Vector2 cen = Vector2.zero; foreach (var q in poly) cen += q; cen /= poly.Count;
        for (int i = 0; i < poly.Count; i++)
        {
            var A = poly[i]; var B = poly[(i + 1) % poly.Count];
            Vector3 a = new Vector3(A.x, 0, A.y), b = new Vector3(B.x, 0, B.y), cc = new Vector3(cen.x, 0, cen.y);
            int k = v.Count; v.Add(cc); v.Add(a); v.Add(b);   // 上（扇）
            if (Vector3.Cross(a - cc, b - cc).y >= 0) top.AddRange(new[] { k, k + 1, k + 2 }); else top.AddRange(new[] { k, k + 2, k + 1 });
            var dn = Vector3.down * T;
            k = v.Count; v.Add(cc + dn); v.Add(a + dn); v.Add(b + dn);   // 下
            if (Vector3.Cross(a - cc, b - cc).y < 0) side.AddRange(new[] { k, k + 1, k + 2 }); else side.AddRange(new[] { k, k + 2, k + 1 });
            var outw = ((a + b) * 0.5f - cc).normalized;
            Quad(v, side, a, b, b + dn, a + dn, outw);   // 側面
        }
        var m = NewMesh(name);
        m.SetVertices(v); m.subMeshCount = 2; m.SetTriangles(top, 0); m.SetTriangles(side, 1); m.RecalculateNormals(); m.RecalculateBounds();
        var p = new GameObject(name); p.transform.SetParent(g, false); p.layer = pathLayer;
        p.AddComponent<MeshFilter>().sharedMesh = m;
        p.AddComponent<MeshRenderer>().sharedMaterials = new[] { ice ? iceFlat : snowA, iceFlat };
        var mc = p.AddComponent<MeshCollider>(); mc.sharedMesh = m; mc.convex = true;
        if (ice) iceCols.Add(mc);
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
    static Vector3[] DeadTree(Transform parent, string name, Vector3 basePos, Vector3 toRoad, Vector3 along, int k, float off)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = basePos;
        float h = 7.5f + (k % 3) * 0.6f;
        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder); trunk.name = "Trunk"; trunk.transform.SetParent(g, false);
        trunk.transform.localPosition = Vector3.up * (h * 0.5f - 1f); trunk.transform.localScale = new Vector3(0.9f, h * 0.5f + 0.5f, 0.9f);
        trunk.GetComponent<Renderer>().sharedMaterial = treeMat;
        var tips = new List<Vector3>();
        var branches = new[] { (y: h - 2.6f, reach: off - 0.6f, fwd: -0.8f, rise: 0.9f), (y: h - 1.2f, reach: off + 0.6f, fwd: 0.9f, rise: 0.7f) };
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
