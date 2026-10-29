using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ステージ2 コース2–2「ほのおのやま」ブロックアウト（2026-10-29 てじゃ「試しに作ってみよう」）。
// 熱のテスト場（FuwaStage2FireTest）で決めた仕組み：火（かがり火・火の輪・火柱）を通るとふわふわが火の決まった所で止まって浮く／浮きすぎると燃える→撃って消す。
// 流れ（ロビーの Slot1 方向に放射状、+Z が進む向き。y=0 がスタートの島の地面）：
//  ① スタート（コース1のスタートの島を写す）→ まっすぐな道
//  ② 火を知る所（2026-10-29 てじゃ「段を登ること自体微妙」「最初に火の特性を知るギミックを置きたい」→ 段々をやめた）：
//     かがり火→すぐ先の低い柵を浮いた所から撃って越える／もう1つのかがり火→長めの平らな道（浮かせて待つと燃える→撃って消す、を試せる）
//  ③ 火柱の谷：手前のふちすぐの火柱が吹いている時に入れると止まる → 真後ろ（手前の道）から撃って高い向こう岸へ。人は左の坂の橋
//  ④ ジグザグ：角の上の火の輪で止めて、向きを変えて撃つ。Uターンあり
// 2026-10-29 てじゃの試し：人が横の橋にいるとふわふわの後ろに立てず、浮いたふわふわを前に撃てない（詰む）→ 浮く所は必ず人が真後ろに立てる所に
//  ⑤ （まだ）動く足場を待つ所。待ちすぎると燃える
//  ⑥ 溶岩の川：空中の火の輪を飛び石みたいに渡る（溶岩に落ちたらアウト）。人は左の細い橋＋各輪の真後ろへの桟橋
//  ⑦ ゴール（コース1のゴールの島を写す）
// 道はシマシマ（赤系、Course2_2_PathA/B）。人だけが通る坂・橋は上にふわふわが乗ったらアウト（FuwaTopOut）
public static class FuwaCourse2_2Builder
{
    const string RootName = "FuwaCourse2_2";
    const float SpokeR = 34.8f, StartBelow = 4f;
    public static float PathHW = 1.5f, StripeLen = 1.2f;
    public static float FenceH = 1.0f;   // 火を知る所の柵の高さ（かがり火の止まる所は火の 2.2m 上＝柵より 1.2m 上）
    public static float WalkW = 1.3f, WalkGap = 0.3f;   // 人の道（左）の幅と、本道とのすき間
    public static Color PathColA = new Color(1f, 0.56f, 0.50f), PathColB = new Color(0.94f, 0.44f, 0.38f);
    public static Color LavaCol = new Color(1f, 0.42f, 0.1f), RockCol = new Color(0.42f, 0.30f, 0.30f);

    static Material pathA, pathB, rockMat, lavaMat;
    static int pathLayer = 11;
    static FuwaHeat heat;
    static FuwaBall ballProxy;
    static List<FuwaCheckpoint> cps = new List<FuwaCheckpoint>();

    [MenuItem("FuwaCourse/ステージ2/2–2 ほのおのやま（ブロックアウト）を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var lobby = GameObject.Find("FuwaStage2/Lobby"); if (!lobby) return "ステージ2のロビーがない";
        var old = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old) Object.DestroyImmediate(old);
        var root = new GameObject(RootName).transform;
        var lc = lobby.transform.Find("LobbySpawn").position; var slot = lobby.transform.Find("Slot1").position;
        var spoke = slot - lc; spoke.y = 0f; spoke.Normalize();
        float floorY = lobby.transform.Find("LobbyFloor").GetComponent<Renderer>().bounds.max.y;
        root.rotation = Quaternion.LookRotation(spoke);
        root.position = new Vector3(lc.x, floorY - StartBelow, lc.z) + spoke * SpokeR;

        pathA = PathMat("Course2_2_PathA", PathColA); pathB = PathMat("Course2_2_PathB", PathColB);
        rockMat = FuwaStage2WaterTest.ToonMat("Stage2_2_Rock", RockCol);
        lavaMat = FuwaStage2WaterTest.ToonMat("Stage2_2_Lava", LavaCol);
        var pathRef = GameObject.Find("FuwaCourse3/PathMesh"); pathLayer = pathRef ? pathRef.layer : 11;
        ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
        cps.Clear();

        // 熱（このコースに1つ）
        var hg = new GameObject("Heat"); hg.transform.SetParent(root, false);
        heat = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHeat>(hg); heat.ball = ballProxy;
        heat.burnFx = FuwaStage2FireTest.FlamePS(hg.transform, "BurnFx", 0.25f, 1.5f, 0.6f, new Color(1f, 0.6f, 0.15f), new Color(0.9f, 0.15f, 0.05f));
        { var m = heat.burnFx.main; m.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.55f); }
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(heat); FuwaStage2WaterTest.NoSync(heat);

        // ① スタート
        float z0 = StartSet(root);
        var path = new GameObject("Path").transform; path.SetParent(root, false);
        var walk = new GameObject("Walk").transform; walk.SetParent(root, false);
        var fire = new GameObject("Fire").transform; fire.SetParent(root, false);
        float z = z0 - 0.5f, y = 0f;
        Strip(path, "S1", new Vector3(0, y, z), Vector3.forward, 14.5f, PathHW); z += 14.5f;

        // ② 火を知る所：かがり火（道のまん中）→ 2.5m 先に柵（道の幅いっぱい、人は左の小道で回る）→ 10m 先にもう1つのかがり火 → 12m の平らな道
        float xw = -(PathHW + WalkGap + WalkW * 0.5f);
        FuwaStage2FireTest.Brazier(fire, "Brazier0", new Vector3(0, y, z + 3f), heat, false);
        Strip(path, "Learn1", new Vector3(0, y, z), Vector3.forward, 10f, PathHW);
        Box(path, "Fence", new Vector3(0, y + FenceH * 0.5f, z + 5.5f), new Vector3(PathHW * 2f, FenceH, 0.4f), rockMat, 0);
        Strip(walk, "FenceWalk", new Vector3(xw, y, z + 4f), Vector3.forward, 3f, WalkW * 0.5f, true);
        z += 10f;
        FuwaStage2FireTest.Brazier(fire, "Brazier1", new Vector3(0, y, z + 2f), heat, false);
        Strip(path, "Learn2", new Vector3(0, y, z), Vector3.forward, 16f, PathHW);
        z += 16f;
        Checkpoint(root, "Checkpoint1", new Vector3(0, y, z - 4f), Vector3.forward, PathHW);

        // ③ 火柱の谷：7m の谷、手前のふちから 1.2m の所に火柱1本。止まる所は道の 2.4m 上（人が手前の道から真後ろで撃てる）。
        //    向こう岸は 1.8m 高い（火なしで撃って越えられないように。てじゃ「橋通ればOK」）。噴き出し口の当たりはなし（落ちたら素直にアウト、口の上で詰まない）
        float gap = 7f, farUp = 1.8f, jetHover = 2.4f;
        FuwaStage2FireTest.Jet(fire, "Jet1", new Vector3(0, y + jetHover - 4.2f, z + 1.2f), heat, 0f);
        Object.DestroyImmediate(fire.Find("Jet1/Mouth").GetComponent<Collider>());
        Bridge(walk, "JetBridge", new Vector3(xw, y, z), new Vector3(xw, y + farUp, z + gap));
        z += gap; y += farUp;
        Strip(path, "JetFar", new Vector3(0, y, z), Vector3.forward, 8f, PathHW); z += 8f;

        // ④ ジグザグ：L字の道、角の上に火の輪（輪の下が床から 0.3m）。右 → 前 → 左 → 前
        var p = new Vector3(0, y, z);
        // 右 → 前（2m だけ）→ 左（Uターン）→ 前 → 右 → 前へ出る。てじゃ「もっと急なカーブあってもいい」
        var dirs = new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.forward, Vector3.right };
        var lens = new[] { 7f, 2f, 10f, 6f, 3f };
        var cur = Vector3.forward;
        for (int k = 0; k < dirs.Length; k++)
        {
            // 角の床（3m 四方）と、その上の火の輪
            var corner = p + cur * PathHW;
            Strip(path, "Corner" + k, corner - cur * PathHW, cur, PathHW * 2f, PathHW);
            var ring = corner + Vector3.up * 1.4f;
            FuwaStage2FireTest.Brazier(fire, "CornerRing" + k, ring, heat, true);
            fire.Find("CornerRing" + k).localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, cur).normalized);   // 輪の面を進む向きに向ける（輪は x 軸が穴の向き）
            var nd = dirs[k];
            p = corner + nd * PathHW;
            Strip(path, "Zig" + k, p, nd, lens[k], PathHW);
            p += nd * lens[k]; cur = nd;
        }
        // 最後の角のあと、まっすぐ前へ
        {
            var corner = p + cur * PathHW;
            string cn = "Corner" + dirs.Length;
            Strip(path, cn, corner - cur * PathHW, cur, PathHW * 2f, PathHW);
            FuwaStage2FireTest.Brazier(fire, "CornerRing" + dirs.Length, corner + Vector3.up * 1.4f, heat, true);
            fire.Find("CornerRing" + dirs.Length).localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, cur).normalized);
            p = corner + Vector3.forward * PathHW; cur = Vector3.forward;
        }
        Strip(path, "ZigOut", p, Vector3.forward, 8f, PathHW);
        Checkpoint(root, "Checkpoint2", p + Vector3.forward * 2.5f, Vector3.forward, PathHW);
        p += Vector3.forward * 8f;

        // ⑥ 溶岩の川：幅 18m、空中の火の輪 3つ（5m おき、道より 1.6m 上）。向こう岸へ
        float river = 18f;
        var lava = Box(root, "Lava", p + new Vector3(0, -2.2f, river * 0.5f), new Vector3(14f, 0.4f, river), lavaMat, 0);
        Object.DestroyImmediate(lava.GetComponent<Collider>());
        Hazard(root, "LavaBallOut", p + new Vector3(0, -2.2f, river * 0.5f), new Vector3(14f, 1.2f, river), false);
        Hazard(root, "LavaPlayerOut", p + new Vector3(0, -2.6f, river * 0.5f), new Vector3(14f, 1.2f, river), true);
        for (int k = 0; k < 3; k++)
        {
            var rp = p + new Vector3(0, 1.6f, 4f + k * 5f);
            FuwaStage2FireTest.Brazier(fire, "LavaRing" + k, rp, heat, true);
            fire.Find("LavaRing" + k).localRotation = Quaternion.LookRotation(Vector3.right);
        }
        Bridge(walk, "LavaBridge", p + new Vector3(xw, 0, 0), p + new Vector3(xw, 0, river));
        // 桟橋：橋から、2つ目・3つ目の輪の 2.5m 手前（真後ろ）へ。1つ目の輪へは岸から撃つ。輪の間を飛ぶふわふわは桟橋の 1.6m 上を通る
        for (int k = 1; k < 3; k++)
        {
            float pz = 4f + k * 5f - 2.5f;
            Bridge(walk, "LavaPier" + k, p + new Vector3(xw + WalkW * 0.5f, 0, pz), p + new Vector3(0.8f, 0, pz));
        }
        p += Vector3.forward * river;
        Strip(path, "LavaFar", p, Vector3.forward, 6f, PathHW);
        p += Vector3.forward * 6f;

        // ⑦ ゴール
        GoalSet(root, p);

        // アウト（下に落ちた）：コースの下（コースの向きの箱、まわり 10m）。ほかのコース（2–1）にかからないように大きくしすぎない
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
        return "[2–2] ゴールまで " + p.z.ToString("F0") + "m、高さ " + p.y.ToString("F1") + "m";
    }

    // ---- 部品 ----
    static Material PathMat(string name, Color c)
    {
        string path = "Assets/FuwaStage2/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/" + name.Replace("2_2", "2_1") + ".mat");
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
    // シマシマの道：a（手前のはしのまん中、上の面の高さ）から dir へ len。当たりは1枚の箱、見た目は StripeLen ごとに A/B
    static void Strip(Transform parent, string name, Vector3 a, Vector3 dir, float len, float hw, bool walkOnly = false)
    {
        if (len <= 0.01f) return;
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        g.localPosition = a; g.localRotation = Quaternion.LookRotation(dir);
        const float T = 0.6f;
        var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = walkOnly ? 0 : pathLayer;
        var bc = col.AddComponent<BoxCollider>(); bc.center = new Vector3(0, -T * 0.5f, len * 0.5f); bc.size = new Vector3(hw * 2f, T, len);
        if (walkOnly) TopOut(col);
        int n = Mathf.Max(1, Mathf.RoundToInt(len / StripeLen));
        for (int k = 0; k < n; k++)
        {
            float l = len / n;
            var s = Box(g, "S" + k, new Vector3(0, -T * 0.5f, l * (k + 0.5f)), new Vector3(hw * 2f, T, l), (k % 2 == 0) ? pathA : pathB, 0);
            Object.DestroyImmediate(s.GetComponent<Collider>());
        }
        // 下の岩（道の下 1.5m）
        var u = Box(g, "Under", new Vector3(0, -T - 0.75f, len * 0.5f), new Vector3(hw * 2f - 0.2f, 1.5f, len - 0.1f), rockMat, 0);
        Object.DestroyImmediate(u.GetComponent<Collider>());
    }
    // 段のはしの岩の壁（下の段の高さ y から h 上まで、上の段の道の下に入る）
    static void Wall(Transform parent, string name, Vector3 at, float h, float hw)
    {
        var w = Box(parent, name, at + new Vector3(0, h * 0.5f - 0.3f, 0.25f), new Vector3(hw * 2f, h - 0.6f + 0.3f, 0.5f), rockMat, 0);
    }
    // 人だけの坂（a→b、上の面の高さ）。上にふわふわが乗ったらアウト
    static void Ramp(Transform parent, string name, Vector3 a, Vector3 b, float w)
    {
        var d = b - a; float len = d.magnitude;
        var g = Box(parent, name, (a + b) * 0.5f - Vector3.up * 0.15f, new Vector3(w, 0.3f, len), pathB, 0);
        g.transform.localRotation = Quaternion.LookRotation(d);
        TopOut(g);
    }
    // 人だけの細い橋（木の板の色、上にふわふわが乗ったらアウト）
    static void Bridge(Transform parent, string name, Vector3 a, Vector3 b)
    {
        var d = b - a; float len = d.magnitude;
        var wood = FuwaStage2WaterTest.ToonMat("Stage2_2_Wood", new Color(0.62f, 0.45f, 0.32f));
        var g = Box(parent, name, (a + b) * 0.5f - Vector3.up * 0.1f, new Vector3(WalkW, 0.2f, len), wood, 0);
        g.transform.localRotation = Quaternion.LookRotation(d);
        TopOut(g);
    }
    static void TopOut(GameObject g)
    {
        var to = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaTopOut>(g);
        if (to) { UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(to); FuwaStage2WaterTest.NoSync(to); }
    }
    static void Hazard(Transform root, string name, Vector3 lp, Vector3 size, bool player)
    {
        var g = new GameObject(name); g.transform.SetParent(root, false); g.transform.localPosition = lp; g.layer = player ? 0 : 4;
        var bc = g.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size;
        if (player) { var pc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(g); pc.ball = ballProxy; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(pc); FuwaStage2WaterTest.NoSync(pc); }
        else { var hz = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHazard>(g); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hz); FuwaStage2WaterTest.NoSync(hz); }
    }

    // スタート：コース1の Start を丸ごと写す（2–1 と同じ）。キノコの島の前にもう1本の木の橋。橋のはし（コースの道の始まり）の z を返す
    static float StartSet(Transform root)
    {
        var start = new GameObject("Start").transform; start.SetParent(root, false);
        var src = GameObject.Find("FuwaCourse/Start")?.transform;
        if (!src) { Debug.LogWarning("[2–2] FuwaCourse/Start がない"); return 0f; }
        var map = new Dictionary<Object, Object>();
        FuwaCourse2_1Builder.CopyChildren(src, start, null, map);
        foreach (var t in start.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("_Course1")) t.name = t.name.Replace("_Course1", "_Course2_2");
        float islandTop = src.Find("StartMushroom").localPosition.y;
        var area = start.Find("StartArea");
        var ground = area.Find("Ground"); var bridge = area.Find("Bridge"); var bridgeWood = area.Find("BridgeWood");
        var bc = bridge.GetComponent<BoxCollider>();
        float halfLen = (bc ? bc.size.z : 1f) * bridge.localScale.z * 0.5f;
        float frontZ = 2f * ground.localPosition.z - bridge.localPosition.z;
        // 島の地面の中心をコースの原点に
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
        if (!src) { Debug.LogWarning("[2–2] FuwaCourse/Goal がない"); return; }
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

    // チェックポイント：ステージ1の FuwaCourse3/Checkpoint1（光るキノコ＋丸太、通り抜けの判定）を写す。門の柱・横木は消す
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
