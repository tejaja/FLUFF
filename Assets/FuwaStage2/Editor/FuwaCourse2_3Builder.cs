using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ステージ2 コース2–3「こおりのほら」ブロックアウト（2026-11-03 てじゃ「いいよ」「雪玉はどんどんデカくしよう」）。
// 決めごと：氷の道では人が滑る（FuwaIceSkate、氷のテスト場の C の方式）。ふわふわは道と同じで、触れたらアウト。
// 流れ（ロビーの Slot2 方向に放射状、+Z が進む向き。y=0 がスタートの島の地面）：
//  ① スタート → つるつるの氷の道（まっすぐ→右へ曲がる→左へ曲がる、柱をよけて滑りに慣れる）
//  ② つららの天井（ほら穴）：下を通ると落ちてくる、落ちたつららがしばらく道をふさぐ（いまは置き場所の目印だけ）
//  ③ 雪玉の坂：上から転がってくる雪玉がどんどん大きくなる。坂は雪（滑らない）（いまは目印だけ）
//  ④ 凍った地底湖：広い氷の上を、結晶の柱をよけて渡る → ゴール
// いまは板と角の踊り場だけの仮の形（2–2 の最初の版と同じ作り）。道は銀のシマシマ、氷は青みの銀、雪は白
public static class FuwaCourse2_3Builder
{
    const string RootName = "FuwaCourse2_3";
    const float SpokeR = 34.8f, StartBelow = 4f;
    public static float StripeLen = 1.2f;
    public static float IntroHW = 2.5f, NarrowHW = 2.0f, CaveHW = 2.0f, CaveH = 4.5f, SlopeHW = 3f, SlopeLen = 26f, SlopeRise = 6f, LakeW = 24f, LakeLen = 30f;
    // 道の色（2026-10-19 決めた 2–3 の色 #B4C2D2 から）。氷＝青みの銀、雪＝白
    public static Color IceColA = new Color(0.80f, 0.86f, 0.92f), IceColB = new Color(0.66f, 0.74f, 0.84f);
    public static Color SnowColA = new Color(0.97f, 0.98f, 1f), SnowColB = new Color(0.88f, 0.91f, 0.95f);
    public static Color RockCol = new Color(0.46f, 0.50f, 0.60f), CrystalCol = new Color(0.62f, 0.84f, 1f), MarkCol = new Color(1f, 0.5f, 0.2f);

    static Material iceA, iceB, snowA, snowB, rockMat, crystalMat, markMat;
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

        iceA = PathMat("Course2_3_PathA", IceColA); iceB = PathMat("Course2_3_PathB", IceColB);   // ロビーの道もこの色（FuwaStage2LobbyCourses）
        snowA = PathMat("Course2_3_SnowA", SnowColA); snowB = PathMat("Course2_3_SnowB", SnowColB);
        rockMat = FuwaStage2WaterTest.ToonMat("Stage2_3_Rock", RockCol);
        crystalMat = FuwaStage2WaterTest.ToonMat("Stage2_3_Crystal", CrystalCol);
        markMat = FuwaStage2WaterTest.ToonMat("Stage2_3_Mark", MarkCol);
        var pathRef = GameObject.Find("FuwaCourse3/PathMesh"); pathLayer = pathRef ? pathRef.layer : 11;
        ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
        cps.Clear(); iceCols.Clear();

        // ① スタート → つるつるの氷の道
        float z0 = StartSet(root);
        var path = new GameObject("Path").transform; path.SetParent(root, false);
        var deco = new GameObject("Marks").transform; deco.SetParent(root, false);
        var p = new Vector3(0, 0, z0 - 0.5f);
        Strip(path, "Intro", p, Vector3.forward, 6f, IntroHW, false); p += Vector3.forward * 6f;
        Strip(path, "Ice1", p, Vector3.forward, 20f, IntroHW, true);
        Crystal(path, "Pillar1a", p + new Vector3(-1.0f, 0, 8f), 0.6f, 2.5f);
        Crystal(path, "Pillar1b", p + new Vector3(1.2f, 0, 14f), 0.6f, 2.5f);
        p += Vector3.forward * 20f;
        // 右へ（踊り場）→ 細い氷の道（柵なし、滑りすぎると落ちる）
        p = Corner(path, "Corner1", p, Vector3.forward, Vector3.right, IntroHW, true);
        Strip(path, "Ice2", p, Vector3.right, 16f, NarrowHW, true); p += Vector3.right * 16f;
        p = Corner(path, "Corner2", p, Vector3.right, Vector3.forward, NarrowHW, true);
        Checkpoint(root, "Checkpoint1", p + Vector3.forward * 2f, Vector3.forward, NarrowHW);

        // ② つららの天井：ほら穴（両側の岩の壁と天井）。つららは天井から（いまは目印）
        Strip(path, "CaveIn", p, Vector3.forward, 4f, CaveHW, false); p += Vector3.forward * 4f;
        float caveLen = 30f;
        Strip(path, "Cave", p, Vector3.forward, caveLen, CaveHW, true);
        var cave = new GameObject("Cave").transform; cave.SetParent(root, false);
        Box(cave, "WallL", p + new Vector3(-CaveHW - 0.5f, CaveH * 0.5f - 0.6f, caveLen * 0.5f), new Vector3(1f, CaveH + 0.6f, caveLen), rockMat, 0);
        Box(cave, "WallR", p + new Vector3(CaveHW + 0.5f, CaveH * 0.5f - 0.6f, caveLen * 0.5f), new Vector3(1f, CaveH + 0.6f, caveLen), rockMat, 0);
        Box(cave, "Ceiling", p + new Vector3(0, CaveH + 0.5f, caveLen * 0.5f), new Vector3(CaveHW * 2f + 2f, 1f, caveLen), rockMat, 0);
        for (int k = 0; k < 8; k++)
        {
            float x = (k % 2 == 0 ? -0.8f : 0.8f), z = 3f + k * 3.5f;
            var ic = Box(deco, "Icicle" + k, p + new Vector3(x, CaveH - 0.7f, z), new Vector3(0.35f, 1.4f, 0.35f), crystalMat, 0);
            Object.DestroyImmediate(ic.GetComponent<Collider>());
        }
        p += Vector3.forward * caveLen;
        Strip(path, "CaveOut", p, Vector3.forward, 4f, CaveHW, false); p += Vector3.forward * 4f;
        Checkpoint(root, "Checkpoint2", p - Vector3.forward * 2f, Vector3.forward, CaveHW);

        // ③ 雪玉の坂（雪、滑らない）。てっぺんから雪玉（いまは目印）
        var slopeDir = new Vector3(0, SlopeRise, SlopeLen).normalized;
        Strip(path, "SnowSlope", p, slopeDir, new Vector3(0, SlopeRise, SlopeLen).magnitude, SlopeHW, false, true);
        p += new Vector3(0, SlopeRise, SlopeLen);
        Strip(path, "SlopeTop", p, Vector3.forward, 6f, SlopeHW, false, true);
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); ball.name = "SnowballSource"; ball.transform.SetParent(deco, false);
        ball.transform.localPosition = p + new Vector3(0, 1f, 4f); ball.transform.localScale = Vector3.one * 1.6f; ball.GetComponent<Renderer>().sharedMaterial = snowA;
        Object.DestroyImmediate(ball.GetComponent<Collider>());
        Checkpoint(root, "Checkpoint3", p + Vector3.forward * 2f, Vector3.forward, SlopeHW);
        p += Vector3.forward * 6f;

        // ④ 凍った地底湖：広い氷、結晶の柱をよける
        Strip(path, "Lake", p, Vector3.forward, LakeLen, LakeW * 0.5f, true);
        var pillars = new[] { (x: -6f, z: 5f), (x: 2f, z: 7f), (x: 7f, z: 12f), (x: -3f, z: 13f), (x: -8f, z: 19f), (x: 4f, z: 20f), (x: -1f, z: 24f), (x: 8f, z: 26f) };
        for (int k = 0; k < pillars.Length; k++) Crystal(path, "LakePillar" + k, p + new Vector3(pillars[k].x, 0, pillars[k].z), 0.9f, 3.5f);
        p += Vector3.forward * LakeLen;
        Strip(path, "LakeOut", p, Vector3.forward, 3f, IntroHW, false); p += Vector3.forward * 3f;

        // ⑤ ゴール
        GoalSet(root, p);

        // 人が滑る
        var sk = new GameObject("IceSkate"); sk.transform.SetParent(root, false);
        var skate = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaIceSkate>(sk);
        skate.ice = iceCols.ToArray();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(skate); FuwaStage2WaterTest.NoSync(skate);

        // アウト（下に落ちた）：コースの下（コースの向きの箱、まわり 10m）
        var lb = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
        foreach (var bc in root.GetComponentsInChildren<BoxCollider>(true))
        {
            if (bc.isTrigger) continue;
            var t = bc.transform; var e = bc.size * 0.5f;
            for (int k = 0; k < 8; k++)
            {
                var c = bc.center + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z);
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
        return "[2–3] ゴールまで " + p.z.ToString("F0") + "m、高さ " + p.y.ToString("F1") + "m、氷の当たり " + iceCols.Count + " 個";
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
        var ma = snow ? snowA : iceA; var mb = snow ? snowB : iceB;
        if (!ice && !snow) { ma = snowB; mb = iceA; }   // 氷でも雪でもない（ふつうの地面）は白と銀の交互
        int n = Mathf.Max(1, Mathf.RoundToInt(len / StripeLen));
        for (int k = 0; k < n; k++)
        {
            float l = len / n;
            var s = Box(g, "S" + k, new Vector3(0, -T * 0.5f, l * (k + 0.5f)), new Vector3(hw * 2f, T, l), (k % 2 == 0) ? ma : mb, 0);
            Object.DestroyImmediate(s.GetComponent<Collider>());
        }
        var u = Box(g, "Under", new Vector3(0, -T - 0.75f, len * 0.5f), new Vector3(hw * 2f - 0.2f, 1.5f, len - 0.1f), rockMat, 0);
        Object.DestroyImmediate(u.GetComponent<Collider>());
    }
    // 角の踊り場（道の幅の正方形）。入ってきた向き from のはし p から、出ていく向き to のはしを返す
    static Vector3 Corner(Transform parent, string name, Vector3 p, Vector3 from, Vector3 to, float hw, bool ice)
    {
        Strip(parent, name, p, from, hw * 2f, hw, ice);
        var c = p + from * hw;
        return c + to * hw;
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
    static void GoalSet(Transform root, Vector3 p)
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
        goal.localPosition = p + Vector3.forward * (far - 0.3f) + Vector3.up * 0.02f;
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
