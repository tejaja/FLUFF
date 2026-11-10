using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ステージ2 コース2–4「はぐるまのとう」ブロックアウト（2026-11-07 てじゃ「配置してみよう」）
// 流れ（2026-11-07 てじゃ「とりあえずこれで」）：だんだん上へのぼる。しかけは 5つのお試しをそのまま使う
//  ① 乗る歯車（FuwaGearRideTest）→ ② 跳ね橋（FuwaDrawbridgeTest）→ 坂 → ③ せり出す橋（FuwaRackBridgeTest）→ 坂
//  → ④ かみ合う2枚の回転床（FuwaTwinTableTest）→ 坂 → 塔のてっぺんのゴール（1枚の回転床は 2026-11-07 に外した、Turn の表は残す）
// 作り方：お試しのビルダーで作った物を、コースの中の決まった所へ動かす（テスト用のふわふわの台・ワープ・石臼は消す）。間の道・坂・曲がり角・スタート・ゴール・塔はここで作る
//  道は 1本の線をたどって作る：P＝いまの道のはし（コースの座標、上の面の高さ）、th＝進む向き（上から見て x→z の反時計回り、度）
//  お試しの物は「入口の点 e と入る向き」「出口の点 x と出る向き」（お試しの root からのずれ、世界の向き）で表しておいて、入口を P に合わせる
// ・ロビーの Slot3 の方向に放射状（2–3 と同じ置き方）。+Z がロビーから外へ。y=0 がスタートの島の地面
// ・道はグレーのシマシマ（2–4 の色、機械のグレー/黒）
public static class FuwaCourse2_4Builder
{
    const string RootName = "FuwaCourse2_4";
    const float SpokeR = 34.8f, StartBelow = 4f;
    public static float HW = 2f, StripeLen = 1.2f;
    public static Color PathColA = new Color(0.62f, 0.64f, 0.69f), PathColB = new Color(0.45f, 0.47f, 0.52f), UnderCol = new Color(0.33f, 0.35f, 0.4f);
    public static Color TowerCol = new Color(0.52f, 0.5f, 0.6f);
    const string MeshDir = "Assets/FuwaStage2/Meshes/Course2_4";

    static Material pathA, pathB, underMat, towerMat;
    static int pathLayer = 11;
    static FuwaBall ballProxy;
    static List<FuwaCheckpoint> cps = new List<FuwaCheckpoint>();
    static Transform root, pathT, secT;
    static Vector3 P; static float th;
    static int segNo;

    // お試しの物：入口（ずれ・入る向き）、出口（ずれ・出る向き）、のぼる高さ。ずれは root からの世界の向きのずれ（root の回り方によらない）
    struct Mech { public string test, name; public System.Action build; public Vector3 e; public float de; public Vector3 x; public float dx; public float rise; }
    static Mech Ride, Draw, Rack, Turn, Twin;
    // お試しのビルダーの今の大きさから入口・出口を決める
    static void MakeMechs()
    {
        float st = FuwaGearRideTest.Step;
        Ride = new Mech { test = "GearRideTest", name = "S1_GearRide", build = FuwaGearRideTest.Build, e = new Vector3(-4f, 0f, 0f), de = 0f, x = new Vector3(31.1f, 2f * st, 4f), dx = 0f, rise = 2f * st };   // 入口の台にまっすぐ入る（2026-11-07 てじゃ「最初の道も曲がってる必要ない」）
        float dz = -(FuwaDrawbridgeTest.PathNear + FuwaDrawbridgeTest.PathW / 2f);
        Draw = new Mech { test = "DrawbridgeTest", name = "S2_Drawbridge", build = FuwaDrawbridgeTest.Build, e = new Vector3(-FuwaDrawbridgeTest.NearLen, 0f, dz), de = 0f, x = new Vector3(FuwaDrawbridgeTest.BridgeLen + 0.05f + FuwaDrawbridgeTest.FarLen, 0f, dz), dx = 0f };
        // せり出す橋：橋をおりたらすぐ右（−z）の床から出る（2026-11-07「小回りに」。前は向こう岸の奥 (21, 0, 3.13) から）
        Rack = new Mech { test = "RackBridgeTest", name = "S3_RackBridge", build = FuwaRackBridgeTest.Build, e = new Vector3(-7.4f, 0f, 0f), de = 0f, x = new Vector3((FuwaRackBridgeTest.Gap + FuwaRackBridgeTest.FarX1) / 2f, 0f, FuwaRackBridgeTest.FarZ0), dx = -90f };   // 2026-11-07 両側から出る橋に作り直し（谷 6m、向こう岸 5m）
        Turn = new Mech { test = "TurnTableTest", name = "S4_TurnTable", build = FuwaTurnTableTest.Build, e = new Vector3(-17.5f, 0f, 0f), de = 0f, x = new Vector3(8.045f, 0f, 13.934f), dx = 60f };
        // 2枚の回転床：かごの奥 x0 = −(A の橋のかどの半径 + 0.05) − CageLen、出口は B から ExitDeg の向きに 10.43m
        float ex = FuwaTwinTableTest.ExitDeg * Mathf.Deg2Rad;
        Twin = new Mech { test = "TwinTableTest", name = "S5_TwinTable", build = FuwaTwinTableTest.Build, e = new Vector3(FuwaTwinTableTest.EntryX, 0f, 0f), de = 0f, x = new Vector3(FuwaTwinTableTest.BCenterX + Mathf.Cos(ex) * FuwaTwinTableTest.ExitFar, 0f, Mathf.Sin(ex) * FuwaTwinTableTest.ExitFar), dx = FuwaTwinTableTest.ExitDeg };
    }
    // コースでは要らない、お試し用の物（名前の頭）
    static readonly string[] TestOnly = { "BallSpot", "BallStand", "Spawn", "BackSpot", "Warp", "Extras", "Mill", "MA_", "MB_", "MC_", "Axle_MA", "Axle_MB", "Axle_MC", "CatchFloor", "Step", "EngineMount" };   // EngineMount：乗る歯車の動力（クリスタルの箱）→ 2026-11-07 時計塔が回す（TowerDriveRide）   // 受け床と階段：2026-11-07 てじゃ「テストようで床がのこったまま、階段もいらん」

    [MenuItem("FuwaCourse/ステージ2/2–4 はぐるまのこうば（ブロックアウト）を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var lobby = GameObject.Find("FuwaStage2/Lobby"); if (!lobby) return "ステージ2のロビーがない";
        var old = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old) Object.DestroyImmediate(old);
        root = new GameObject(RootName).transform;
        var lc = lobby.transform.Find("LobbySpawn").position; var slot = lobby.transform.Find("Slot3").position;
        var spoke = slot - lc; spoke.y = 0f; spoke.Normalize();
        float floorY = lobby.transform.Find("LobbyFloor").GetComponent<Renderer>().bounds.max.y;
        // 2026-11-07 てじゃ「入口を他のコースみたいにロビーから伸びてる角度に」→ 道はスタートからロビーの外向き（+Z）へまっすぐ。輪は右回り
        root.rotation = Quaternion.LookRotation(spoke);
        root.position = new Vector3(lc.x, floorY - StartBelow, lc.z) + spoke * SpokeR;

        pathA = PathMat("Course2_4_PathA", PathColA); pathB = PathMat("Course2_4_PathB", PathColB);   // ロビーの道もこの色（FuwaStage2LobbyCourses）
        underMat = FuwaStage2WaterTest.ToonMat("Stage2_4_Under", UnderCol);
        towerMat = FuwaStage2WaterTest.ToonMat("Stage2_4_Tower", TowerCol);
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaStage2/Meshes", "Course2_4");
        var pathRef = GameObject.Find("FuwaCourse3/PathMesh"); pathLayer = pathRef ? pathRef.layer : 11;
        ballProxy = GameObject.Find("FuwaSystem/FuwaBall")?.GetComponent<FuwaBall>();
        cps.Clear(); segNo = 0;
        cwG.Clear(); cwR.Clear(); cwP.Clear(); slG.Clear(); slT.Clear(); slRod.Clear(); slAx.Clear(); slPin.Clear(); slPY.Clear(); slLen.Clear(); feeds.Clear(); CwMats();
        var log = new System.Text.StringBuilder();

        float R0 = StartSet(root);
        pathT = new GameObject("Path").transform; pathT.SetParent(root, false);
        secT = new GameObject("Sections").transform; secT.SetParent(root, false);
        th = 90f; P = Vector3.zero;
        IslandJoin(pathT, "StartJoin", Vector3.zero, Dir(th), R0, HW, pathB);
        P = Dir(th) * R0;

        // 2026-11-07 てじゃ「ちょとながすぎる」「橋合わせるやつが過剰かも」「道も詰めよう」→ 1枚の回転床（FuwaTurnTableTest）を外して4つに、坂 14m→8m、まっすぐは最小
        // 2026-11-07 てじゃ「入口はロビーから伸びてる角度」「全体的にもうちょっと小回りに」→ スタートからまっすぐ前、右回り
        //  ① 乗る歯車（前へ）→ 右 ② 跳ね橋 → 右 ③ せり出す橋（おりたらすぐ右）→ ④ 2枚の回転床（①の上を通る）→ まん中の塔
        MakeMechs();
        S(3f);
        Place(Ride, log); CP("Checkpoint1", 1f, 0f, -1.5f); S(1f); HoleGears(); S(1f);   // ①→②：窓のある歯車（2026-11-07「道そのものが歯車」。前は押し出し壁）
        T(-90f); S(2f);
        Place(Draw, log); CP("Checkpoint2", 1f, 0f, -1.5f); S(1f); ChunkyGears(); S(1f); S(8f, 3f);   // ②→③：ごつい歯車（前は回るアーム）
        T(-90f); S(2f);
        Place(Rack, log); CP("Checkpoint3", 1f); S(1f); StepGears(); S(1.5f, 0f, false); T(90f); S(6f, 1.6f, false); T(-90f); S(2f);   // ③→④：段つき歯車で 1.4m 上がる（前はシャッター）。出たすぐの道は下に B の大きい歯車があるので下の石なし
        // チェックポイント4は出口の床（6m 角）に入ってすぐ・床の幅いっぱい（2026-11-08 てじゃ「横から抜けられちゃう」：床の先の曲がり角を斜めに飛ぶと、道の幅の判定を通らなかった）
        Place(Twin, log); CP("Checkpoint4", -5.5f, 3f); S(2f);
        // 塔へ（まん中）：①の上をまたいで
        // 2026-11-08 てじゃ「今のゴールと前の道は完全に別の場所に」→ まん中（バルーン）へ向かう道は位置を決めるためだけに作って消す（GoalPlan 0 で前の通り）
        var P0 = P; float th0 = th; int n0 = pathT.childCount, s0 = segNo;
        T(-120f); S(8f, 3f); S(14f);   // 2026-11-07 2枚の回転床を大きくした（すき間 3.2m）ので塔の位置を合わせ直し
        // 時計塔のトンネルを突っ切って、抜けた先にゴール（2026-11-07 てじゃ「最後は塔を突っ切った先がゴール」）
        // 2026-11-08 てじゃ「時計塔自体なしにして、ここにデカい何かしらの機械を」→ 巨大わたあめ機（UseTower=false）。時計塔・振り子・雲づくり工場のコードは残す
        //  まん中（前の塔の中心）を coreT に。でかい歯車・塔の時計じかけ・めりこみ対策はここにつく
        var coreAt = P + Dir(th) * (TowerW / 2f); float coreTh = th;
        if (GoalPlan != 0) { while (pathT.childCount > n0) Object.DestroyImmediate(pathT.GetChild(pathT.childCount - 1).gameObject); P = P0; th = th0; segNo = s0; }
        if (UseTower) { ClockTower(coreAt, th); coreT = root.Find("ClockTower"); }
        else { coreT = new GameObject("MachineCore").transform; coreT.SetParent(root, false); coreT.localPosition = coreAt; coreT.localRotation = Quaternion.LookRotation(Dir(coreTh)); pendPivot = null; }
        TowerDriveRide(log);
        ClockworkFeeds(coreT);
        if (UseFactory) CloudFactory(coreT);
        if (!UseTower) { CloudBalloon(coreT, log); SugarPipes(coreT, log); TankPipes(coreT, log); }
        ClockworkInstall(coreT); GuardInstall(coreT);
        if (GoalPlan == 0) { S(TowerW); S(1f); }
        else if (GoalPlan == 1) { T(60f); S(3f); }   // A：チェックポイント4のあとの角から、まん中と反対（コースの外の空）へ
        var goalAt = GoalSet(root, P, Dir(th));

        // アウト（下に落ちた）：コースの下（コースの向きの箱、まわり 10m）
        var lb = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
        foreach (var cl in root.GetComponentsInChildren<Collider>(true))
        {
            if (cl.isTrigger) continue;
            Vector3 cc, ex; var t = cl.transform;
            if (cl is BoxCollider bc) { cc = bc.center; ex = bc.size * 0.5f; }
            else if (cl is MeshCollider mc && mc.sharedMesh) { cc = mc.sharedMesh.bounds.center; ex = mc.sharedMesh.bounds.extents; }
            else continue;
            for (int k = 0; k < 8; k++)
            {
                var c = cc + new Vector3((k & 1) == 0 ? -ex.x : ex.x, (k & 2) == 0 ? -ex.y : ex.y, (k & 4) == 0 ? -ex.z : ex.z);
                var lp = root.InverseTransformPoint(t.TransformPoint(c));
                if (first) { lb = new Bounds(lp, Vector3.zero); first = false; } else lb.Encapsulate(lp);
            }
        }
        // 塔の柱は当たりの範囲に入れない（下へのびているので）
        var outSize = new Vector3(lb.size.x + 20f, 2f, lb.size.z + 20f);
        float outY = Mathf.Min(-7f, OutFloorY() - 3f);
        Hazard(root, "BallOut", new Vector3(lb.center.x, outY, lb.center.z), outSize, false);
        Hazard(root, "PlayerOut", new Vector3(lb.center.x, outY - 2f, lb.center.z), outSize, true);

        RegisterCheckpoints();
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
        RegisterBgm(root);
        FuwaStage2LobbyCourses.Run();
        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        log.Append(" ベベル " + FuwaBevelTools.BevelCourseStripes(root, MeshDir + "/Course2_4_PathBevel.asset", PathBevel, 3));   // 2026-11-08 てじゃ「シマの道のフチはちょっとだけベベル」
        return "[2–4] " + log + " ゴール " + goalAt.ToString("F1") + " 範囲 " + lb.min.ToString("F0") + "〜" + lb.max.ToString("F0");
    }

    static float OutFloorY()
    {
        // 乗る歯車の受け床より下
        var cf = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CatchFloor");
        return cf ? root.InverseTransformPoint(cf.position).y - 1f : -4f;
    }

    // BGM（2026-11-09 てじゃ「Clockwork Factory」A案、よそで作った曲をループ化：イントロ37.84s＋79.06sループ）。2–1〜2–3 と同じく作り直すたびに BallStart をつなぎ直す
    const string BgmClip = "Assets/FuwaStage2/Audio/gear_clockwork_introloop.wav";
    const float BgmGain = 1.85f, BgmLoopStart = 37.84f, BgmLoopEnd = 116.9f;
    public static void RegisterBgm(Transform courseRoot)
    {
        FuwaStage2Bgm.Register(courseRoot, "BGM_course2_4", "BGM_course2_3", BgmClip, BgmGain, BgmLoopStart, BgmLoopEnd);
    }

    static Vector3 Dir(float deg) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }

    // お試しの物を作って、入口を P・th に合わせて置く。P・th は出口へ進む
    static void Place(Mech m, System.Text.StringBuilder log)
    {
        m.build();
        var t = GameObject.Find(m.test)?.transform;
        if (!t) { log.Append(m.test + " がない "); return; }
        foreach (Transform c in t.Cast<Transform>().ToArray())
            if (TestOnly.Any(pre => c.name.StartsWith(pre))) Object.DestroyImmediate(c.gameObject);
        t.name = m.name;
        float a = th - m.de;
        var rc = root.rotation * Quaternion.Euler(0f, -a, 0f);   // お試しの世界の向き → コースの中の向き
        var oldRot = t.rotation;
        t.SetParent(secT, true);
        var pos = root.TransformPoint(P) - rc * m.e;
        t.SetPositionAndRotation(pos, rc * oldRot);
        P = root.InverseTransformPoint(pos + rc * m.x);
        th = m.dx + a;
        log.Append(m.name + " @" + root.InverseTransformPoint(pos).ToString("F1") + " ");
    }

    // まっすぐの道（rise で上り坂）
    static void S(float len, float rise = 0f, bool under = true)
    {
        var d = Dir(th) * len + Vector3.up * rise;
        Strip(pathT, "Road" + (segNo++) + (rise != 0f ? "_Slope" : ""), P, d, HW, under);
        P += d;
    }

    // 曲がり角（＋で左）。内側のかどを中心にした扇形の床。道どうしと重ならない
    static void T(float deg)
    {
        float sg = Mathf.Sign(deg);
        var L = Dir(th + 90f);
        var inner = P + L * HW * sg;
        var q = Quaternion.Euler(0f, -deg, 0f);
        var name = "Turn" + (segNo++);
        var g = new GameObject(name); g.transform.SetParent(pathT, false); g.layer = pathLayer;
        const float Tk = 0.6f; int n = Mathf.Max(3, Mathf.CeilToInt(Mathf.Abs(deg) / 7.5f));
        var v = new List<Vector3>(); var tr = new List<int>();
        Vector3 r0 = P - L * HW * sg - inner;   // 内側のかどから外側のかどへ（長さ 2HW）
        var top = new List<Vector3>();
        for (int i = 0; i <= n; i++) top.Add(inner + Quaternion.Euler(0f, -deg * i / n, 0f) * r0);
        // 上の面・下の面（扇）
        for (int pass = 0; pass < 2; pass++)
        {
            var off = pass == 0 ? Vector3.zero : Vector3.down * Tk;
            for (int i = 0; i < n; i++) Tri(v, tr, inner + off, top[i] + off, top[i + 1] + off, pass == 0 ? Vector3.up : Vector3.down);
        }
        // 外の弧の側面
        for (int i = 0; i < n; i++)
        {
            var a = top[i]; var b = top[i + 1]; var dn = Vector3.down * Tk;
            var nrm = ((a + b) * 0.5f - inner); nrm.y = 0f;
            Tri(v, tr, a, b, b + dn, nrm); Tri(v, tr, a, b + dn, a + dn, nrm);
        }
        var m = SaveMesh(name, v, tr);
        g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = pathB;
        g.AddComponent<MeshCollider>().sharedMesh = m;
        P = inner + q * (P - inner);
        th += deg;
    }

    static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 want)
    {
        if (Vector3.Dot(Vector3.Cross(b - a, c - a), want) < 0f) { var s = b; b = c; c = s; }
        int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2);
    }

    static Mesh SaveMesh(string name, List<Vector3> v, List<int> t)
    {
        string path = MeshDir + "/" + name + ".asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!m) { m = new Mesh { name = name }; AssetDatabase.CreateAsset(m, path); }
        m.Clear(false);
        m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;   // でかい歯車は頂点が6万5千をこえる（こえると途中で切れて見えた）
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        EditorUtility.SetDirty(m);
        return m;
    }

    // シマシマの道：a（手前のはしのまん中、上の面）から d だけ（坂なら傾いた箱）。当たりは1枚の箱、見た目は StripeLen ごとに A/B
    static void Strip(Transform parent, string name, Vector3 a, Vector3 d, float hw, bool under = true)
    {
        float len = d.magnitude; if (len <= 0.01f) return;
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        g.localPosition = a; g.localRotation = Quaternion.LookRotation(d / len, Vector3.up);
        const float Tk = 0.6f;
        var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = pathLayer;
        var bc = col.AddComponent<BoxCollider>(); bc.center = new Vector3(0, -Tk * 0.5f, len * 0.5f); bc.size = new Vector3(hw * 2f, Tk, len);
        int n = Mathf.Max(1, Mathf.RoundToInt(len / StripeLen));
        for (int k = 0; k < n; k++)
        {
            float l = len / n;
            var s = Box(g, "S" + k, new Vector3(0, -Tk * 0.5f, l * (k + 0.5f)), new Vector3(hw * 2f, Tk, l), (k % 2 == 0) ? pathA : pathB, 0);
            Object.DestroyImmediate(s.GetComponent<Collider>());
        }
        if (!under || !PathUnder) return;   // 2026-11-08 てじゃ「道の下の■部分もいらない」
        var u = Box(g, "Under", new Vector3(0, -Tk - 0.5f, len * 0.5f), new Vector3(hw * 2f - 0.2f, 1.0f, len - 0.1f), underMat, 0);
        Object.DestroyImmediate(u.GetComponent<Collider>());
    }

    // 時計塔（2026-11-07 てじゃ「まんなかの塔、塔っぽく」「でかい時計塔にして、最後は塔を突っ切った先がゴール」）
    //  c＝トンネルの床のまん中（道の上の面の高さ）、道の向き th。四角い塔（TowerW 角）、道の高さに道の幅のトンネル、上に4面の時計ととんがり屋根
    //  下は雲の下までのびる。トンネルの床は道（S）で作るので、塔の下の段は道の下の石（−1.6m）より下で終わる
    public static float TowerW = 7f, TunnelHW = 2.2f, TunnelH = 4f, TowerTop = 16f, RoofH = 7f, ClockY = 11.5f, ClockR = 2.6f, TowerBottom = -30f;
    // 振り子：吹き抜けの天井 HallTop からつるす。振れ幅はおもりがトンネルの壁（±TunnelHW）に当たらない角度（おもりの端が壁から 0.1m）
    public static float HallTop = 8.6f, PendBobY = 1.6f, PendBobR = 0.65f;
    static float PendPivotY => HallTop - 0.45f;
    static float PendAmp => Mathf.Asin((TunnelHW - 0.1f - PendBobR) / (PendPivotY - PendBobY)) * Mathf.Rad2Deg;
    static Transform pendPivot, escape, weightT, chainT; static Vector3 chainTop;
    public static float WeightDrop = 4.4f, WeightPeriod = 600f;
    static void ClockTower(Vector3 c, float heading)
    {
        var stone = FuwaStage2WaterTest.ToonMat("Stage2_4_TowerStone", TowerCol);
        var trim = FuwaStage2WaterTest.ToonMat("Stage2_4_TowerTrim", new Color(0.36f, 0.35f, 0.43f));
        var roofM = FuwaStage2WaterTest.ToonMat("Stage2_4_TowerRoof", new Color(0.3f, 0.32f, 0.4f));
        var dialM = FuwaStage2WaterTest.ToonMat("Stage2_4_ClockDial", new Color(0.97f, 0.94f, 0.85f));
        var brassM = FuwaStage2WaterTest.ToonMat("Stage2_4_ClockBrass", new Color(0.86f, 0.68f, 0.32f));
        var handM = FuwaStage2WaterTest.ToonMat("Stage2_4_ClockHand", new Color(0.16f, 0.16f, 0.2f));
        var t = new GameObject("ClockTower").transform; t.SetParent(root, false);
        t.localPosition = c; t.localRotation = Quaternion.LookRotation(Dir(heading));   // local z = 道の向き、x = 右
        float h = TowerW / 2f, baseTop = -1.7f;
        // 下の段（トンネルの床の下）・トンネルの左右の壁・トンネルの上
        // 下の段：中を空洞にして、時計の動力（おもり）が見える窓（2026-11-07 てじゃ「動力ある？」→ A「時計のおもり」）
        //  窓はスタートのある側の面（local ±x）。中：大歯車（36枚）→ 小歯車（8）＋2段目（30）→ 小歯車（8）。動力はクリスタル（下）
        //  回り方は FuwaClockwork（ほかの歯車と同じ時計）
        float wsx = Mathf.Sign(t.InverseTransformDirection(root.TransformPoint(Vector3.zero) - t.position).x); if (wsx == 0f) wsx = 1f;
        const float CavB = -12f, CavT = -2.5f, Wall = 0.5f, WinHW = 2.5f;
        TBox(t, "Base", new Vector3(0f, (TowerBottom + CavB) / 2f, 0f), new Vector3(TowerW, CavB - TowerBottom, TowerW), stone, true);
        TBox(t, "BaseTop", new Vector3(0f, (CavT + baseTop) / 2f, 0f), new Vector3(TowerW, baseTop - CavT, TowerW), stone, true);
        float ch = CavT - CavB, cy = (CavB + CavT) / 2f, iw = h - Wall;
        TBox(t, "CavWallBack", new Vector3(-wsx * (h - Wall / 2f), cy, 0f), new Vector3(Wall, ch, TowerW), stone, true);
        foreach (int sz in new[] { -1, 1 }) TBox(t, "CavWallSide" + sz, new Vector3(0f, cy, sz * (h - Wall / 2f)), new Vector3(TowerW - 2f * Wall, ch, Wall), stone, true);
        foreach (int sz in new[] { -1, 1 }) TBox(t, "CavWallWin" + sz, new Vector3(wsx * (h - Wall / 2f), cy, sz * (WinHW + (h - WinHW) / 2f)), new Vector3(Wall, ch, h - WinHW), stone, true);
        TBox(t, "WinSill", new Vector3(wsx * (h + 0.05f), CavB + 0.15f, 0f), new Vector3(0.3f, 0.3f, 2f * WinHW + 0.6f), trim, false);
        TBox(t, "WinLintel", new Vector3(wsx * (h + 0.05f), CavT - 0.15f, 0f), new Vector3(0.3f, 0.3f, 2f * WinHW + 0.6f), trim, false);
        TBox(t, "CavFloor", new Vector3(0f, CavB + 0.02f, 0f), new Vector3(2f * iw, 0.04f, 2f * iw), roofM, false);
        // 歯車の面（frame の local Y＝塔の +x、2D の x＝下、2D の z＝塔の +z）。ドラムは窓から奥へ
        var mv = new GameObject("Movement").transform; mv.SetParent(t, false);
        mv.localPosition = new Vector3(0f, -4.6f, -1.0f); mv.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.right);
        const float MM = 0.1f, Rb = 0.35f;
        float wv = WeightDrop / WeightPeriod;                          // おもりの下がる速さ m/s
        float rB = wv / Rb * Mathf.Rad2Deg / ClockSpeed;               // くさりは −z 側：ドラムが 2D でプラスに回るとくさりがほどけて下がる
        var gw = new FuwaGearTest.Gear { name = "GreatWheel_z36", z = 36, c = Vector2.zero, ratio = rB, phase = 0f };
        var p1 = FuwaGearTest.Mesh2(gw, "Pinion1_z8", 8, 90f, MM);
        var w2 = new FuwaGearTest.Gear { name = "Wheel2_z30", z = 30, c = p1.c, ratio = p1.ratio, phase = 0.2f };
        var p2 = FuwaGearTest.Mesh2(w2, "Pinion2_z8", 8, 0f, MM);
        float gwX = wsx * 2.2f;                                         // 大歯車の面（窓のすぐ奥）
        Transform MvGear(FuwaGearTest.Gear q, float planeX, float th, Material m)
        {
            var g = new GameObject(q.name).transform; g.SetParent(mv, false); g.localPosition = new Vector3(q.c.x, planeX, q.c.y);
            g.gameObject.AddComponent<MeshFilter>().sharedMesh = SpurMesh(q.z, MM, th, "TowerMove_z" + q.z); g.gameObject.AddComponent<MeshRenderer>().sharedMaterial = m;
            AddGear(g, q.ratio, q.phase * Mathf.Rad2Deg);
            return g;
        }
        var gwT = MvGear(gw, gwX, 0.12f, brassM);
        for (int k = 0; k < 6; k++)
        {
            var sp = TBox(gwT, "Spoke" + k, Quaternion.Euler(0f, k * 60f, 0f) * new Vector3(0.9f, wsx > 0f ? 0.14f : -0.02f, 0f), new Vector3(1.4f, 0.04f, 0.14f), brassM, false);
            sp.localRotation = Quaternion.Euler(0f, k * 60f, 0f);
        }
        var p1T = MvGear(p1, gwX, 0.12f, handM);
        var w2T = MvGear(w2, gwX - wsx * 0.3f, 0.1f, brassM);
        var p2T = MvGear(p2, gwX - wsx * 0.3f, 0.1f, handM);
        foreach (var (g, len, ad) in new[] { (gwT, 2.6f, 0.4f), (p1T, 1.4f, 0.3f), (p2T, 1.2f, 0.3f) })
        {
            var ax = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ax.name = "Arbor"; Object.DestroyImmediate(ax.GetComponent<Collider>()); ax.transform.SetParent(g, false);
            ax.transform.localPosition = new Vector3(0f, -wsx * len / 2f + 0.06f, 0f); ax.transform.localScale = new Vector3(ad, len / 2f, ad); ax.GetComponent<Renderer>().sharedMaterial = handM;
        }
        // 動力はクリスタル（2026-11-07 てじゃ「オモリあげないとなのか」「やっぱクリスタル式がいいかな」）：
        //  大歯車の下に大きいクリスタルが浮いて、たての軸でゆっくり回る。軸の上のかさ歯車（16:16）で大歯車の軸を回す
        {
            var O = new Vector3(wsx * 0.1f, -4.6f, -1.0f);                 // 大歯車の軸の線の上
            var axB = new Vector3(wsx, 0f, 0f);                             // 大歯車の方
            // 大歯車の軸の歯車 B の回り方 = 大歯車（mv の local Y＝塔の +x）。B の軸が −x の時は向きが逆
            float rGW = rB, rBev = wsx > 0f ? rGW : -rGW;
            float rA = -rBev;                                               // 16:16 なので rB = −rA
            Transform gA; float rB2, pB2;
            var gBv = BevelPair(t, "CrystalBevel", O, Vector3.down, axB, BevZ, BevZ, rA, 0f, out gA, out rB2, out pB2);
            CwShaft(gBv, 0f, Mathf.Abs(gwX - O.x) - 1.2f, 0.36f);           // 大歯車の軸へ
            CwShaft(gA, 0f, 1.88f, 0.6f);                                   // クリスタルまで（下へ）
            var cryMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaStage2/Meshes/Course2_3/ObsCrystal1_Main.asset");
            var cryMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_3_Crystal.mat");
            if (cryMesh && cryMat)
            {
                var cry = new GameObject("PowerCrystal").transform; cry.SetParent(gA, false);
                cry.localPosition = new Vector3(0f, 2.05f, 0f);   // 軸の先にぶら下がる（gA の local Y は下向き＝クリスタルの先が下）
                cry.localScale = Vector3.one * 1.1f;   // 高さ 約 3.3m（空洞の床より上）
                cry.gameObject.AddComponent<MeshFilter>().sharedMesh = cryMesh; cry.gameObject.AddComponent<MeshRenderer>().sharedMaterial = cryMat;
            }
            var sock = GameObject.CreatePrimitive(PrimitiveType.Cylinder); sock.name = "Socket"; Object.DestroyImmediate(sock.GetComponent<Collider>());
            sock.transform.SetParent(gA, false); sock.transform.localPosition = new Vector3(0f, 1.95f, 0f); sock.transform.localScale = new Vector3(1.6f, 0.1f, 1.6f); sock.GetComponent<Renderer>().sharedMaterial = brassM;   // 軸の先のうけ
            // クリスタルをつかむ爪 6本（クリスタルの半径 約 0.57 の外から）
            for (int k = 0; k < 6; k++)
            {
                var q = Quaternion.Euler(0f, k * 60f + 30f, 0f);
                var claw = TBox(gA, "Claw" + k, q * new Vector3(0.66f, 2.4f, 0f), new Vector3(0.14f, 0.9f, 0.24f), brassM, false);
                claw.localRotation = q;
                var tip = TBox(gA, "ClawTip" + k, q * new Vector3(0.6f, 2.78f, 0f), new Vector3(0.12f, 0.14f, 0.24f), brassM, false);
                tip.localRotation = q;
            }
        }
        weightT = null; chainT = null; chainTop = Vector3.zero;

        // トンネルの中は吹き抜け（HallTop まで）で、そこに振り子（2026-11-08 てじゃ「振り子は時計塔の中に」）。外から見た口の高さは TunnelH のまま（口の上は壁）
        foreach (int sg in new[] { -1, 1 })
            TBox(t, "TunnelWall" + (sg > 0 ? "R" : "L"), new Vector3(sg * (h + TunnelHW) / 2f, (baseTop + HallTop) / 2f, 0f), new Vector3(h - TunnelHW, HallTop - baseTop, TowerW), stone, true);
        foreach (int sz in new[] { -1, 1 })
            TBox(t, "HallLintel" + sz, new Vector3(0f, (TunnelH + HallTop) / 2f, sz * (h - Wall / 2f)), new Vector3(2f * TunnelHW, HallTop - TunnelH, Wall), stone, true);
        TBox(t, "Upper", new Vector3(0f, (HallTop + TowerTop) / 2f, 0f), new Vector3(TowerW, TowerTop - HallTop, TowerW), stone, true);
        // トンネルの口のふち（アーチの代わりの枠）と、帯・てっぺんのふち（少し外へ張り出す）
        foreach (int sz in new[] { -1, 1 })
        {
            float fz = sz * (h + 0.1f);
            TBox(t, "PortalTop" + sz, new Vector3(0f, TunnelH + 0.25f, fz), new Vector3(2f * TunnelHW + 0.8f, 0.5f, 0.2f), trim, false);
            foreach (int sx in new[] { -1, 1 }) TBox(t, "PortalSide" + sz + "_" + sx, new Vector3(sx * (TunnelHW + 0.2f), (baseTop + TunnelH) / 2f, fz), new Vector3(0.4f, TunnelH - baseTop, 0.2f), trim, false);
        }
        foreach (var y in new[] { TunnelH + 1.6f, ClockY - ClockR - 0.7f, TowerTop - 0.3f })
            TBox(t, "Band" + y.ToString("F1"), new Vector3(0f, y, 0f), new Vector3(TowerW + 0.4f, 0.4f, TowerW + 0.4f), trim, false);
        // 4面の時計：外向き n、上 up。ふち（真ちゅう）→ 文字盤 → 目盛り → 針（時・分）→ まん中のふた
        var hours = new List<Transform>(); var mins = new List<Transform>();
        foreach (var n in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
        {
            var face = new GameObject("Clock_" + n.ToString("F0")).transform; face.SetParent(t, false);
            face.localPosition = n * h + Vector3.up * ClockY;   // 壁の面から外へ
            face.localRotation = Quaternion.LookRotation(n, Vector3.up);   // local +Z = 外向き
            Disc(face, "Rim", 0.03f, 0.06f, ClockR + 0.25f, brassM);
            Disc(face, "Dial", 0.08f, 0.04f, ClockR, dialM);
            for (int k = 0; k < 12; k++)
            {
                var q = Quaternion.Euler(0f, 0f, -k * 30f);
                bool big = k % 3 == 0;
                var tick = TBox(face, "Tick" + k, q * new Vector3(0f, ClockR - (big ? 0.38f : 0.3f), 0.13f), new Vector3(big ? 0.18f : 0.1f, big ? 0.5f : 0.34f, 0.02f), handM, false);
                tick.localRotation = q;
            }
            var hp = new GameObject("HourPivot").transform; hp.SetParent(face, false); hp.localPosition = new Vector3(0f, 0f, 0.15f);
            TBox(hp, "Hand", new Vector3(0f, 0.55f, 0f), new Vector3(0.22f, 1.5f, 0.03f), handM, false);
            var mp = new GameObject("MinutePivot").transform; mp.SetParent(face, false); mp.localPosition = new Vector3(0f, 0f, 0.19f);
            TBox(mp, "Hand", new Vector3(0f, 0.85f, 0f), new Vector3(0.14f, 2.2f, 0.03f), handM, false);
            Disc(face, "Cap", 0.22f, 0.04f, 0.2f, brassM);
            hours.Add(hp); mins.Add(mp);
        }
        // 振り子と脱進機（2026-11-07 てじゃ「各道に、なんかしら自動の歯車の仕掛け」→ ④→塔の道：塔の振り子が道の上で左右にゆれる）
        //  トンネルの吹き抜けの天井に振り子の軸（アンクルの軸）。おもりは道の上 PendBobY。ゆれは道を横切る向き（local Z まわり）
        //  軸の下にがんぎ車（30枚）。塔の中から軸がのびて回している。振り子が1回ゆれきるたびに半歯カチッ
        {
            // 2026-11-08 塔の外（入口の前の腕の先）→ トンネルの吹き抜けの天井からつるす。ゆれは道を横切る向き（トンネルの壁に当たらない振れ幅）
            float pz = 0f, py = HallTop - 0.45f, len = py - PendBobY;
            TBox(t, "PendHead", new Vector3(0f, py + 0.3f, pz), new Vector3(1.3f, 0.3f, 0.6f), trim, false);
            // がんぎ車（local Y＝道の向き）とその軸（出口がわの口の上の壁まで）
            var ewF = new GameObject("EscapeWheel_Frame").transform; ewF.SetParent(t, false);
            ewF.localPosition = new Vector3(0f, py - 0.95f, pz + 0.15f); ewF.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
            var ew = new GameObject("EscapeWheel_z30").transform; ew.SetParent(ewF, false);
            ew.gameObject.AddComponent<MeshFilter>().sharedMesh = SaveMesh2(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(30, 6, 0.05f), 0.08f, "ClockTower_EscapeWheel_z30", Vector2.zero));
            ew.gameObject.AddComponent<MeshRenderer>().sharedMaterial = brassM;
            var ax = TBox(t, "EscapeAxle", new Vector3(0f, py - 0.95f, (pz + 0.15f + h - Wall) / 2f), new Vector3(0.1f, 0.1f, h - Wall - pz - 0.15f), handM, false);
            // 振り子：軸（アンクル＋棒＋おもり）。棒とおもりはがんぎ車の手前（−z）
            var pv = new GameObject("PendulumPivot").transform; pv.SetParent(t, false); pv.localPosition = new Vector3(0f, py, pz);
            TBox(pv, "Arbor", new Vector3(0f, 0f, 0.1f), new Vector3(0.14f, 0.14f, 0.5f), handM, false);
            foreach (int sx in new[] { -1, 1 })
            {
                var arm = TBox(pv, "AnchorArm" + sx, Quaternion.Euler(0f, 0f, sx * 38f) * new Vector3(0f, -0.55f, 0f) + new Vector3(0f, 0f, 0.15f), new Vector3(0.1f, 1.1f, 0.06f), handM, false);
                arm.localRotation = Quaternion.Euler(0f, 0f, sx * 38f);
                TBox(pv, "Pallet" + sx, Quaternion.Euler(0f, 0f, sx * 38f) * new Vector3(0f, -1.1f, 0f) + new Vector3(0f, 0f, 0.15f), new Vector3(0.16f, 0.16f, 0.1f), brassM, false);
            }
            TBox(pv, "Rod", new Vector3(0f, -len / 2f, -0.2f), new Vector3(0.12f, len, 0.12f), handM, false);
            var bob = new GameObject("Bob").transform; bob.SetParent(pv, false); bob.localPosition = new Vector3(0f, -len, -0.2f);
            Disc(bob, "Lens", -0.18f, 0.36f, PendBobR, brassM);
            var bc = bob.gameObject.AddComponent<SphereCollider>(); bc.radius = PendBobR;
            var bo = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaBounce>(bob.gameObject);
            bo.outwardCenter = null; bo.useContactNormal = false;   // おもりの中心から外へ（楕円体の向き）
            bo.alwaysUp = false; bo.bounceSpeed = 4.5f; bo.keepTangent = 0.3f;
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(bo); FuwaStage2WaterTest.NoSync(bo);
            pendPivot = pv; escape = ew;
        }
        // とんがり屋根（四角すい）
        var roof = new GameObject("Roof"); roof.transform.SetParent(t, false); roof.transform.localPosition = new Vector3(0f, TowerTop, 0f);
        {
            float rh = h + 0.5f; var v = new List<Vector3>(); var tr = new List<int>();
            var apex = new Vector3(0f, RoofH, 0f);
            var cs = new[] { new Vector3(-rh, 0, -rh), new Vector3(rh, 0, -rh), new Vector3(rh, 0, rh), new Vector3(-rh, 0, rh) };
            for (int i = 0; i < 4; i++) { var a = cs[i]; var b = cs[(i + 1) % 4]; var mid = (a + b) * 0.5f; mid.y = 0f; Tri(v, tr, a, b, apex, mid.normalized + Vector3.up * 0.5f); }
            Tri(v, tr, cs[0], cs[1], cs[2], Vector3.down); Tri(v, tr, cs[0], cs[2], cs[3], Vector3.down);
            var m = SaveMesh("ClockTowerRoof", v, tr);
            roof.AddComponent<MeshFilter>().sharedMesh = m; roof.AddComponent<MeshRenderer>().sharedMaterial = roofM;
        }
        TBox(t, "Spire", new Vector3(0f, TowerTop + RoofH + 0.6f, 0f), new Vector3(0.15f, 1.4f, 0.15f), brassM, false);
        // 針を動かす
        var ck = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaClockHands>(t.gameObject);
        ck.hourHands = hours.ToArray(); ck.minuteHands = mins.ToArray();
        ck.pendulum = pendPivot; ck.pendulumAmp = PendAmp; ck.pendulumPeriod = 2f * Mathf.PI * Mathf.Sqrt((PendPivotY - PendBobY) / 9.81f);   // 本物の振り子と同じ周期
        ck.escapeWheel = escape; ck.escapeTeeth = 30;
        ck.weight = null; ck.chain = null;   // おもりはやめた（クリスタル式）
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ck); FuwaStage2WaterTest.NoSync(ck);
        // エディタでも今の時刻に
        var now = System.DateTime.Now; float mm = now.Minute + now.Second / 60f, hh = (now.Hour % 12) + mm / 60f;
        foreach (var x in hours) x.localRotation = Quaternion.Euler(0f, 0f, hh * 30f);
        foreach (var x in mins) x.localRotation = Quaternion.Euler(0f, 0f, mm * 6f);
    }

    static Mesh SaveMesh2(Mesh m)
    {
        var v = new List<Vector3>(m.vertices); var t = new List<int>(m.triangles);
        return SaveMesh(m.name, v, t);
    }

    static Transform TBox(Transform p, string name, Vector3 lp, Vector3 size, Material m, bool collider)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(p, false);
        g.transform.localPosition = lp; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g.transform;
    }

    // 文字盤などの円盤：face の local +Z の向きに、z0 から厚み th
    static void Disc(Transform face, string name, float z0, float th, float r, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = name; g.transform.SetParent(face, false);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // 円柱の軸（Y）を Z へ
        g.transform.localPosition = new Vector3(0f, 0f, z0 + th / 2f);
        g.transform.localScale = new Vector3(2f * r, th / 2f, 2f * r);
        g.GetComponent<Renderer>().sharedMaterial = m;
    }

    // ① 乗る歯車の動力は時計塔（2026-11-07 てじゃ「Aはほしいね」）：塔の下の段の中から 18枚の歯車が出てきて、いちばん近い G3（大 20枚）とかみ合う
    //  回し方は乗る歯車と同じ FuwaGearRide（gears に足す）。角度 = ratio × a + phase は Mesh2 で G3 から出す
    //  前の動力（クリスタルの箱・G1 のウォームホイール）は消す。お試しで消えた歯車（オルゴール・石臼）の空きも詰める
    static void TowerDriveRide(System.Text.StringBuilder log)
    {
        var ride = root.Find("Sections/S1_GearRide"); var tower = coreT;
        if (!ride || !tower) return;
        foreach (var w in ride.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("WormWheel")).ToArray()) Object.DestroyImmediate(w.gameObject);
        var gr = ride.GetComponentInChildren<FuwaGearRide>(true);
        var gl = new List<Transform>(); var rl = new List<float>(); var pl = new List<float>();
        for (int i = 0; i < gr.gears.Length; i++) if (gr.gears[i]) { gl.Add(gr.gears[i]); rl.Add(gr.ratio[i]); pl.Add(gr.phase[i]); }
        int i3 = gl.FindIndex(x => x.name == "G3"); if (i3 < 0) { log.Append("G3 がない "); return; }
        var g3T = gl[i3]; var body3 = g3T.Find("Body_z20");
        Vector3 c3 = ride.InverseTransformPoint(g3T.position), tc = ride.InverseTransformPoint(tower.position);
        var d = new Vector2(tc.x - c3.x, tc.z - c3.z).normalized;
        // ride の中の 2D（x→z 反時計回り）。G3 の回り方：角度 = ratio·a + phase（度）
        var g3 = new FuwaGearTest.Gear { name = "G3", z = 20, c = new Vector2(c3.x, c3.z), ratio = rl[i3], phase = pl[i3] * Mathf.Deg2Rad };
        // G3 から塔の壁までの距離（塔の四角との交わり）。塔の中から出てくる歯車は、中心が壁の 1m 内側、壁から 1.2m 以上出る
        float dirDeg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        var c3w = ride.TransformPoint(c3); var dw = ride.TransformDirection(new Vector3(d.x, 0f, d.y));
        var lo = tower.InverseTransformPoint(c3w); var ld = tower.InverseTransformDirection(dw);
        float h = TowerW / 2f, tIn = 0f;
        foreach (var (o, dd) in new[] { (lo.x, ld.x), (lo.z, ld.z) }) { if (Mathf.Abs(dd) < 1e-5f) continue; float t1 = (-h - o) / dd, t2 = (h - o) / dd; tIn = Mathf.Max(tIn, Mathf.Min(t1, t2)); }
        // 1枚で届くか、なければ間に1枚（どちらも 12〜24 枚から）
        var chain = new List<FuwaGearTest.Gear>(); float tipOf(int z) => 0.2f * z + 0.4f;
        for (int z = 12; z <= 24 && chain.Count == 0; z++) { float D = 0.2f * (20 + z); if (D >= tIn + 0.8f && D - tipOf(z) <= tIn - 1.2f) chain.Add(FuwaGearTest.Mesh2(g3, "TowerDrive_z" + z, z, dirDeg, 0.4f)); }
        for (int zi = 10; zi <= 20 && chain.Count == 0; zi++) for (int z = 10; z <= 20 && chain.Count == 0; z++)
        {
            float D = 0.2f * (20 + zi) + 0.2f * (zi + z);
            if (D >= tIn + 0.8f && D - tipOf(z) <= tIn - 1.2f) { var gi = FuwaGearTest.Mesh2(g3, "TowerIdler_z" + zi, zi, dirDeg, 0.4f); chain.Add(gi); chain.Add(FuwaGearTest.Mesh2(gi, "TowerDrive_z" + z, z, dirDeg, 0.4f)); }
        }
        if (chain.Count == 0)
        {
            // 塔が遠い：G1 の軸を道の下（FeedY）までのばして、かさ歯車 → 横の軸で塔へ（ほかの歯車の道と同じ）
            //  FuwaGearRide と FuwaClockwork はどちらも Time.time で回るので、ratio を塔の元の角度に直す（× 乗る歯車の速さ / 塔の速さ）
            int i1 = gl.FindIndex(x => x.name == "G1"); var g1T = gl[i1];
            float k = gr.speed / ClockSpeed;
            float yl = g1T.localPosition.y, oy = ride.InverseTransformPoint(root.TransformPoint(new Vector3(0f, FeedY, 0f))).y;
            var oldAx = ride.Find("Axle_G1"); if (oldAx) Object.DestroyImmediate(oldAx.gameObject);   // 動かない軸は消して、回る太い軸だけに（同じ太さで重なってチラつく）
            CwShaft(g1T, oy + 1.2f - yl, -0.5f, 0.55f);
            feeds.Add(new Feed { O = root.InverseTransformPoint(ride.TransformPoint(new Vector3(g1T.localPosition.x, oy, g1T.localPosition.z))), shaftGear = g1T, ratio = rl[i1] * k, phase = pl[i1], below = true });
            gr.gears = gl.ToArray(); gr.ratio = rl.ToArray(); gr.phase = pl.ToArray();
            gr.crank = null; gr.engineMount = null; gr.crankPin = null; gr.piston = null; gr.rod = null; gr.puff = null;
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(gr);
            log.Append("塔→G1 の軸から（塔まで " + tIn.ToString("F1") + "m） ");
            return;
        }
        var b3m = body3.GetComponent<MeshFilter>().sharedMesh; var bb = b3m.bounds;
        foreach (var gd in chain)
        {
            var t = new GameObject(gd.name).transform; t.SetParent(ride, false);
            t.localPosition = new Vector3(gd.c.x, g3T.localPosition.y, gd.c.y);
            var body = new GameObject("Body_z" + gd.z); body.transform.SetParent(t, false);
            body.transform.localPosition = body3.localPosition + new Vector3(0f, bb.min.y * body3.localScale.y, 0f);
            body.layer = body3.gameObject.layer;
            var m = SaveMesh2(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(gd.z, 10, 0.4f), bb.size.y * body3.localScale.y, "TowerDrive_m04_z" + gd.z, Vector2.zero));
            body.AddComponent<MeshFilter>().sharedMesh = m; body.AddComponent<MeshRenderer>().sharedMaterial = body3.GetComponent<Renderer>().sharedMaterial;
            if (body3.GetComponent<MeshCollider>()) body.AddComponent<MeshCollider>().sharedMesh = m;
            if (gd.name.StartsWith("TowerIdler"))
            {
                var ax = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ax.name = "Axle"; Object.DestroyImmediate(ax.GetComponent<Collider>());
                ax.transform.SetParent(t, false); ax.transform.localPosition = new Vector3(0f, -1.5f, 0f); ax.transform.localScale = new Vector3(0.55f, 1.55f, 0.55f);
                ax.GetComponent<Renderer>().sharedMaterial = cwAxle;
            }
            gl.Add(t); rl.Add(gd.ratio); pl.Add(gd.phase * Mathf.Rad2Deg);
        }
        var gdLast = chain[chain.Count - 1];
        gr.gears = gl.ToArray(); gr.ratio = rl.ToArray(); gr.phase = pl.ToArray();
        gr.crank = null; gr.engineMount = null; gr.crankPin = null; gr.piston = null; gr.rod = null; gr.puff = null;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(gr);
        // エディタでも今の角度に
        for (int i = 0; i < gr.gears.Length; i++) gr.gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(gr.phase[i], 360f), 0f);
        log.Append("塔→G3 歯車 " + string.Join("+", chain.Select(x => x.name)) + " 壁まで " + tIn.ToString("F1") + "m ");
    }

    // ===== 時計塔が動かす、道ごとの自動の歯車じかけ（2026-11-07 てじゃ「各道に、なんかしら自動の歯車の仕掛けを追加しよう」→ 案「いいよ」）=====
    //  FuwaClockwork が全部の歯車・クランクを 元の角度 a（ClockSpeed 度/秒）から動かす。歯車 i の角度 = ratio·a + phase（frame の local Y まわり、2D 反時計回り）
    //  塔 ←（空中の軸、高さ ShaftY）→ かさ歯車 → たての軸 → しかけ。塔の中の歯車は見せない（軸は塔の壁にささる）
    public static float ClockSpeed = 10f, ShaftY = 15.4f;
    const float BevM = 0.15f; const int BevZ = 16;
    static List<Transform> cwG = new List<Transform>(); static List<float> cwR = new List<float>(), cwP = new List<float>();
    static List<int> slG = new List<int>(); static List<Transform> slT = new List<Transform>(), slRod = new List<Transform>(); static List<Vector3> slAx = new List<Vector3>(); static List<float> slPin = new List<float>(), slPY = new List<float>(), slLen = new List<float>();
    struct Feed { public Vector3 O; public Transform shaftGear; public float ratio, phase; public bool below; }   // below：O がたての軸の下のはし（道の下の軸）   // たての軸のてっぺん O（コースの座標）と、その軸の歯車（local Y＝上）の回り方
    static List<Feed> feeds = new List<Feed>();
    static Material cwDark, cwLight, cwBrass, cwAxle, cwFloor, cwWall;

    static void CwMats()
    {
        cwDark = FuwaGearTest.Mat("GearTest_Gear", new Color(0.36f, 0.38f, 0.43f));
        cwLight = FuwaGearTest.Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f));
        cwBrass = FuwaGearTest.Mat("FanTest_Brass", new Color(0.86f, 0.68f, 0.32f));
        cwAxle = FuwaGearTest.Mat("GearTest_Axle", new Color(0.2f, 0.21f, 0.24f));
        cwFloor = FuwaGearTest.Mat("GearTest_Floor", new Color(0.55f, 0.58f, 0.63f));
        cwWall = FuwaStage2WaterTest.ToonMat("Stage2_4_Pusher", new Color(0.74f, 0.42f, 0.36f));
    }

    static int AddGear(Transform t, float r, float p) { cwG.Add(t); cwR.Add(r); cwP.Add(p); return cwG.Count - 1; }

    // frame（向きだけ）の下に回る歯車。lp は parent の中。axis＝歯車の軸（frame の local Y）
    static Transform CwGear(Transform parent, string name, Vector3 lp, Vector3 axisLocal, Mesh mesh, Material m, bool collider = false)
    {
        var fr = new GameObject(name + "_Frame").transform; fr.SetParent(parent, false);
        fr.localPosition = lp; fr.localRotation = Quaternion.FromToRotation(Vector3.up, axisLocal);
        var g = new GameObject(name).transform; g.SetParent(fr, false);
        if (mesh)
        {
            g.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; g.gameObject.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collider) { g.gameObject.layer = 11; g.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh; }
        }
        return g;
    }
    static Mesh SpurMesh(int z, float mod, float th, string name) => SaveMesh2(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(z, 8, mod), th, name, Vector2.zero));
    static void CwShaft(Transform g, float y0, float y1, float d)
    {
        // 2026-11-08 てじゃ「ギアの中央のパーツの先端にZファイト多数」：軸・線・カラーの上の面が同じ高さだった → 軸と線はカラーの中で止める
        float lo = Mathf.Min(y0, y1), hi = Mathf.Max(y0, y1), ct = Mathf.Min(0.16f, (hi - lo) / 4f), inset = Mathf.Min(0.02f, ct / 3f);
        float s0 = lo + inset, s1 = hi - inset;
        var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder); s.name = "Shaft"; Object.DestroyImmediate(s.GetComponent<Collider>());
        s.transform.SetParent(g, false); s.transform.localPosition = new Vector3(0f, (s0 + s1) / 2f, 0f); s.transform.localScale = new Vector3(d, (s1 - s0) / 2f, d);
        s.GetComponent<Renderer>().sharedMaterial = cwAxle;
        float l0 = lo + ct - inset, l1 = hi - ct + inset;
        if (l1 > l0)
        {
            var l = GameObject.CreatePrimitive(PrimitiveType.Cube); l.name = "ShaftLine"; Object.DestroyImmediate(l.GetComponent<Collider>());
            l.transform.SetParent(g, false); l.transform.localPosition = new Vector3(d / 2f, (l0 + l1) / 2f, 0f); l.transform.localScale = new Vector3(0.03f, l1 - l0, 0.05f);
            l.GetComponent<Renderer>().sharedMaterial = cwBrass;
        }
        // 両はしに太い輪（カラー）：2026-11-07 てじゃ「軸が細いから違和感」「もっと大げさに」
        foreach (float cy in new[] { lo + ct / 2f, hi - ct / 2f })
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); c.name = "Collar"; Object.DestroyImmediate(c.GetComponent<Collider>());
            c.transform.SetParent(g, false); c.transform.localPosition = new Vector3(0f, cy, 0f); c.transform.localScale = new Vector3(d * 1.6f, ct / 2f, d * 1.6f);
            c.GetComponent<Renderer>().sharedMaterial = cwBrass;
        }
    }
    static Transform CwRod(Transform parent, string name, float len)
    {
        var r = new GameObject(name).transform; r.SetParent(parent, false);
        TBox(r, "Bar", Vector3.zero, new Vector3(0.16f, 0.12f, len), cwAxle, false);
        return r;
    }
    static Transform Platform(Transform parent, string name, Vector3 c, Vector2 size)
    {
        var g = new GameObject(name).transform; g.SetParent(parent, false); g.localPosition = c + new Vector3(0f, -0.3f, 0f); g.gameObject.layer = 11;
        var v = GameObject.CreatePrimitive(PrimitiveType.Cube); v.name = "Visual"; Object.DestroyImmediate(v.GetComponent<Collider>()); v.transform.SetParent(g, false); v.transform.localScale = new Vector3(size.x, 0.6f, size.y); v.GetComponent<Renderer>().sharedMaterial = cwFloor;
        g.gameObject.AddComponent<BoxCollider>().size = new Vector3(size.x, 0.6f, size.y);
        return g;
    }
    static Transform GimmickRoot(string name)
    {
        var holder = root.Find("Gimmicks"); if (!holder) { holder = new GameObject("Gimmicks").transform; holder.SetParent(root, false); }
        var g = new GameObject(name).transform; g.SetParent(holder, false); g.localPosition = P; g.localRotation = Quaternion.LookRotation(Dir(th));
        return g;
    }
    // 2枚のかさ歯車（頂点 O を共有、軸 A・B はどちらも O から歯車の方へ、直角）。A の回り方から B の回り方を出して登録。B の歯車を返す
    //  A の歯が B の方（axisB）を向く時 B はすき間：θB = −(zA/zB)(θA − φA) + φB + 180/zB
    static Transform BevelPair(Transform parent, string name, Vector3 O, Vector3 axA, Vector3 axB, int zA, int zB, float rA, float pA, out Transform gA, out float rB, out float pB)
    {
        float dA = Mathf.Atan2(zA, zB), dB = Mathf.PI / 2f - dA, Ac = BevM * zA / (2f * Mathf.Sin(dA)), F = 0.3f * Ac;
        var mA = SaveMesh2(FuwaRackBridgeTest.Bevel(zA, BevM, dA, F, "Clockwork_Bevel_z" + zA + "_" + zB)); var mB = SaveMesh2(FuwaRackBridgeTest.Bevel(zB, BevM, dB, F, "Clockwork_Bevel_z" + zB + "_" + zA));
        gA = CwGear(parent, name + "_A", O + axA * (Ac * Mathf.Cos(dA)), axA, mA, cwBrass);
        var gB = CwGear(parent, name + "_B", O + axB * (Ac * Mathf.Cos(dB)), axB, mB, cwDark);
        float phA = FuwaRackBridgeTest.Phi2D(gA.parent.localRotation, axB), phB = FuwaRackBridgeTest.Phi2D(gB.parent.localRotation, axA);
        rB = -(float)zA / zB * rA; pB = -(float)zA / zB * (pA - phA) + phB + 180f / zB;
        AddGear(gA, rA, pA); AddGear(gB, rB, pB);
        return gB;
    }

    // ①→② 押し出し壁：道の左（外）の台の上でクランクが回って、壁が道を横切って出たり引っこんだり（クランク1回転 6秒）
    //  台の上の小歯車（たての軸、空中の軸から）→ クランクの歯車（26枚）。壁は引っこむと台の上、出ると道を全部ふさぐ
    public static float PushLen = 6f, PushCycle = 6f;
    static void PushWall()
    {
        var g = GimmickRoot("PushWall"); float zc = PushLen / 2f;
        const float R = 2.25f, L = 4.5f, Cx = -6.85f, Y = 2.4f, GT = 0.25f;   // クランクは壁の上（壁が引っこむとクランクの下にもぐる）
        Platform(g, "Deck", new Vector3(-7.2f, 0f, zc), new Vector2(10.2f, PushLen - 0.2f));
        float rc = 360f / PushCycle / ClockSpeed;
        var crankG = new FuwaGearTest.Gear { z = 26, c = new Vector2(Cx, zc), ratio = rc, phase = Mathf.PI };   // a=0 でピンが −x（壁は引っこんでいる）
        var crank = CwGear(g, "Crank_z26", new Vector3(Cx, Y, zc), Vector3.up, SpurMesh(26, 0.2f, GT, "Clockwork_Spur_z26"), cwLight);
        int ci = AddGear(crank, rc, 180f);
        var pin = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(pin.GetComponent<Collider>()); pin.name = "Pin"; pin.transform.SetParent(crank, false);
        pin.transform.localPosition = new Vector3(R, GT + 0.1f, 0f); pin.transform.localScale = new Vector3(0.2f, 0.1f, 0.2f); pin.GetComponent<Renderer>().sharedMaterial = cwBrass;
        var pg = FuwaGearTest.Mesh2(crankG, "Pinion_z12", 12, 180f, 0.2f);
        var pinion = CwGear(g, "Pinion_z12", new Vector3(pg.c.x, Y, pg.c.y), Vector3.up, SpurMesh(12, 0.2f, GT, "Clockwork_Spur_z12"), cwBrass);
        AddGear(pinion, pg.ratio, pg.phase * Mathf.Rad2Deg);
        // 壁（スライダー、原点＝ピンの高さの壁のまん中）と棒
        var wall = new GameObject("Wall").transform; wall.SetParent(g, false); wall.localPosition = new Vector3(Cx - (L - R), Y + GT + 0.1f, zc);
        var wb = TBox(wall, "Plate", new Vector3(0f, 1.15f - (Y + GT + 0.1f), 0f), new Vector3(4.6f, 2.2f, 0.4f), cwWall, true);
        TBox(wall, "Bracket", new Vector3(0f, -0.25f, 0f), new Vector3(0.3f, 0.5f, 0.3f), cwDark, false);
        foreach (float px in new[] { Cx, pg.c.x }) TBox(g, "Stand" + px.ToString("F1"), new Vector3(px, Y / 2f, zc), new Vector3(0.3f, Y, 0.3f), cwDark, false);
        var bo = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaBounce>(wb.gameObject); bo.useContactNormal = true; bo.alwaysUp = false; bo.bounceSpeed = 3f; bo.keepTangent = 0.5f;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(bo); FuwaStage2WaterTest.NoSync(bo);
        var rod = CwRod(g, "Rod", L);
        slG.Add(ci); slT.Add(wall); slRod.Add(rod); slAx.Add(Vector3.right); slPin.Add(R); slPY.Add(GT + 0.1f); slLen.Add(L);
        // たての軸（小歯車から空中の軸の高さまで）
        float top = ShaftY - P.y;
        CwShaft(pinion, GT, top - 1.25f, 0.18f);
        feeds.Add(new Feed { O = root.InverseTransformPoint(g.TransformPoint(new Vector3(pg.c.x, top, pg.c.y))), shaftGear = pinion, ratio = pg.ratio, phase = pg.phase * Mathf.Rad2Deg });
    }

    // ②→③ 回るアーム：道の左（外）のたての軸に長い腕。9秒で1周、道の上（ふわふわの高さ）をはらう
    public static float ArmLen = 6f, ArmCycle = 9f, ArmReach = 5.5f, ArmY = 1.2f;
    static void SweepArm()
    {
        var g = GimmickRoot("SweepArm"); float zc = ArmLen / 2f; const float Ax = -3.6f;
        Platform(g, "Deck", new Vector3(-3.8f, 0f, zc), new Vector2(3.4f, 3.4f));
        float ra = 360f / ArmCycle / ClockSpeed;
        var hub = CwGear(g, "ArmHub_z14", new Vector3(Ax, 0.05f, zc), Vector3.up, SpurMesh(14, 0.2f, 0.3f, "Clockwork_Spur_z14"), cwBrass);
        AddGear(hub, ra, 90f);   // a=0 で腕は道と平行（道の上にない）
        var arm = new GameObject("Arm").transform; arm.SetParent(hub, false);
        var ab = TBox(arm, "Bar", new Vector3(ArmReach / 2f, ArmY, 0f), new Vector3(ArmReach, 0.35f, 0.35f), cwWall, true);
        var bo = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaBounce>(ab.gameObject); bo.useContactNormal = true; bo.alwaysUp = false; bo.bounceSpeed = 3.5f; bo.keepTangent = 0.4f;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(bo); FuwaStage2WaterTest.NoSync(bo);
        TBox(arm, "Post", new Vector3(0f, ArmY / 2f + 0.15f, 0f), new Vector3(0.4f, ArmY, 0.4f), cwDark, false);
        float top = ShaftY - P.y;
        CwShaft(hub, 0.3f, top - 1.25f, 0.2f);
        feeds.Add(new Feed { O = root.InverseTransformPoint(g.TransformPoint(new Vector3(Ax, top, zc))), shaftGear = hub, ratio = ra, phase = 90f });
    }

    // ③→④ かわりばんこのシャッター：道の上の門に2枚。上の軸（道ぞい）のクランク2つが 180° ずれていて、交互に上下（6秒で1回）
    public static float ShutLen = 6f, ShutCycle = 6f;
    static void Shutters()
    {
        var g = GimmickRoot("Shutters");
        const float R = 1.3f, L = 3.0f, CY = 6.55f, GW = 4.8f, GH = 2.2f;
        float[] zs = { 1.6f, ShutLen - 1.6f };
        float rk = 360f / ShutCycle / ClockSpeed;
        // 門：はしらと上のはり
        foreach (float z in zs) foreach (int sx in new[] { -1, 1 })
            TBox(g, "Post" + z.ToString("F1") + sx, new Vector3(sx * 2.65f, (CY + 0.4f) / 2f, z), new Vector3(0.25f, CY + 0.4f, 0.35f), cwDark, true);
        foreach (float z in new[] { 0.3f, zs[1] + 0.6f })   // 2本目は出口側のかさ歯車（z = ShutLen − 0.3）にかからない所
            TBox(g, "Beam" + z.ToString("F1"), new Vector3(0f, CY, z), new Vector3(5.6f, 0.3f, 0.3f), cwLight, false);
        // クランク（上の軸、local Y＝道の向き）2つ。a=0 で1枚目のピンが真下（閉まっている）、2枚目は真上（開いている）
        Transform first = null;
        for (int k = 0; k < 2; k++)
        {
            var cr = CwGear(g, "Crank" + k, new Vector3(0f, CY, zs[k]), Vector3.forward, SpurMesh(16, 0.2f, 0.2f, "Clockwork_Spur_z16"), cwLight);
            float phD = FuwaRackBridgeTest.Phi2D(cr.parent.localRotation, Vector3.down);
            int ci = AddGear(cr, rk, phD);
            float pr = k == 0 ? R : -R;
            var pin = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(pin.GetComponent<Collider>()); pin.name = "Pin"; pin.transform.SetParent(cr, false);
            pin.transform.localPosition = new Vector3(pr, -0.1f, 0f); pin.transform.localScale = new Vector3(0.16f, 0.15f, 0.16f); pin.GetComponent<Renderer>().sharedMaterial = cwBrass;
            var gate = new GameObject("Gate" + k).transform; gate.SetParent(g, false); gate.localPosition = new Vector3(0f, CY - (L + R), zs[k]);
            var gb = TBox(gate, "Plate", new Vector3(0f, -GH / 2f, 0f), new Vector3(GW, GH, 0.25f), cwWall, true);
            var bo = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaBounce>(gb.gameObject); bo.useContactNormal = true; bo.alwaysUp = false; bo.bounceSpeed = 3f; bo.keepTangent = 0.5f;
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(bo); FuwaStage2WaterTest.NoSync(bo);
            var rod = CwRod(g, "Rod" + k, L);
            slG.Add(ci); slT.Add(gate); slRod.Add(rod); slAx.Add(Quaternion.Inverse(cr.parent.localRotation) * Vector3.down); slPin.Add(pr); slPY.Add(-0.1f); slLen.Add(L);
            if (k == 0) first = cr;
            if (k == 1) CwShaft(cr, -(zs[1] - zs[0]), 0f, 0.16f);   // 2つのクランクをつなぐ軸
        }
        // 上の軸の先（出口側 +z）でかさ歯車 → たての軸 → 空中の軸の高さ
        var Ob = new Vector3(0f, CY, ShutLen + 0.9f);
        var lastCrank = cwG[cwG.Count - 1];
        float rC = cwR[cwR.Count - 1], pC = cwP[cwP.Count - 1];
        // E：上の軸の歯車（Ob から −z＝門の方。上の軸の local Y は +z なので向きが逆：θE = −θC）。F：Ob の上（たての軸）
        Transform gE; float rF, pF;
        var gF = BevelPair(g, "ShutBevel", Ob, Vector3.back, Vector3.up, BevZ, BevZ, -rC, -pC, out gE, out rF, out pF);
        CwShaft(lastCrank, 0f, Ob.z - 1.2f - zs[1], 0.16f);
        float top = ShutTop();
        CwShaft(gF, 0f, top - CY - 2.4f, 0.16f);
        feeds.Add(new Feed { O = root.InverseTransformPoint(g.TransformPoint(new Vector3(0f, top, Ob.z))), shaftGear = gF, ratio = rF, phase = pF });
    }
    static float ShutTop() => ShaftY - P.y;

    // 塔から各しかけへ：塔の土台のまわりを横に回る でかい歯車（BigWheel）が基点（2026-11-07 てじゃ「下に横に回ってるデカイの置いて、そこを基点に」→ OK）
    //  しかけのたての軸の下のはし（FeedY+0.7〜+1.2 の層）に小歯車。でかい歯車のふちと、なか歯車 1〜2 枚でつなぐ（距離のちがいをそこで吸収）
    //  歯の数の都合でしかけの速さを s 倍にする（しかけの歯車の ratio をまとめて s 倍：かみ合いはくずれない）
    const float WM = 0.4f, WT = 0.5f; const int WZ = 88;
    // 2026-11-08 てじゃ「クリスタルはギアに刺さってる感じじゃなくて、空中に浮いた単体の巨大クリスタルのまわりにギア」→ でかい歯車のまん中は穴（BoreR）
    public static float HubR = 4.4f, BoreR = 3.3f;   // 2026-11-08 クリスタルを太くしたので穴も広げた
    public static float WheelRatio = 0f;   // 0 なら自動で決める
    public static float WheelRatioUsed;
    static void ClockworkFeeds(Transform tower)
    {
        var tc = root.InverseTransformPoint(tower.position);
        var gim = root.Find("Gimmicks");
        float wy = FeedY + 1.2f - WT;
        var wf = new GameObject("BigWheel_Frame").transform; wf.SetParent(gim, false); wf.localPosition = new Vector3(tc.x, wy, tc.z);
        float RW = WM * WZ / 2f;
        int nf = feeds.Count;
        var S = new Vector2[nf]; var yawP = new float[nf];
        for (int i = 0; i < nf; i++)
        {
            var lp = wf.InverseTransformPoint(root.TransformPoint(feeds[i].O)); S[i] = new Vector2(lp.x, lp.z);
            var px = wf.InverseTransformDirection(feeds[i].shaftGear.parent.TransformDirection(Vector3.right)); yawP[i] = Mathf.Atan2(px.z, px.x) * Mathf.Rad2Deg;
            if (Vector3.Dot(feeds[i].shaftGear.parent.up, wf.up) < 0.999f) Debug.LogError("[2–4] Feed" + i + " の軸がたてじゃない");
        }
        // なか歯車の置き方（最後の1枚は三角形で解く。2枚なら1枚目はでかい歯車の中心→軸の線の上）
        List<FuwaGearTest.Gear> Chain(Vector2 s, int[] zi, int zp)
        {
            var w = new FuwaGearTest.Gear { name = "BigWheel", z = WZ, c = Vector2.zero, ratio = 1f, phase = 0f };
            var ch = new List<FuwaGearTest.Gear> { w };
            float ang = Mathf.Atan2(s.y, s.x) * Mathf.Rad2Deg;
            FuwaGearTest.Gear prev = w;
            for (int k = 0; k < zi.Length; k++)
            {
                if (k < zi.Length - 1) { prev = FuwaGearTest.Mesh2(prev, "Idler" + k, zi[k], ang, WM); ch.Add(prev); continue; }
                float a = WM * (prev.z + zi[k]) / 2f, bb = WM * (zi[k] + zp) / 2f; Vector2 v = s - prev.c; float dd = v.magnitude;
                if (dd > a + bb - 0.3f || dd < Mathf.Abs(a - bb) + 0.3f) return null;
                float x = (a * a - bb * bb + dd * dd) / (2f * dd), y = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
                Vector2 e = v / dd, nn = new Vector2(-e.y, e.x); Vector2 c = prev.c + e * x + nn * y;
                prev = FuwaGearTest.Mesh2(prev, "Idler" + k, zi[k], Mathf.Atan2((c - prev.c).y, (c - prev.c).x) * Mathf.Rad2Deg, WM); ch.Add(prev);
            }
            var pg = FuwaGearTest.Mesh2(prev, "Pinion", zp, Mathf.Atan2((s - prev.c).y, (s - prev.c).x) * Mathf.Rad2Deg, WM);
            if ((pg.c - s).magnitude > 0.01f) return null;
            // となりのとなり（でかい歯車と小歯車など）がぶつからない
            for (int p = 0; p < ch.Count; p++) for (int q = p + 2; q <= ch.Count; q++)
            {
                var A = ch[p]; var B = q == ch.Count ? pg : ch[q];
                if ((A.c - B.c).magnitude < WM * (A.z + B.z) / 2f + 3f * WM) return null;
            }
            ch.Add(pg); return ch;
        }
        var cands = new List<List<FuwaGearTest.Gear>>[nf];
        for (int i = 0; i < nf; i++)
        {
            cands[i] = new List<List<FuwaGearTest.Gear>>();
            for (int zp = 8; zp <= 32; zp++)
            {
                for (int z1 = 12; z1 <= 40; z1++) { var c = Chain(S[i], new[] { z1 }, zp); if (c != null) cands[i].Add(c); }
                for (int z1 = 12; z1 <= 60; z1 += 2) for (int z2 = 12; z2 <= 30; z2 += 2) { var c = Chain(S[i], new[] { z1, z2 }, zp); if (c != null) cands[i].Add(c); }
            }
            if (cands[i].Count == 0) { Debug.LogError("[2–4] Feed" + i + " がでかい歯車につながらない 距離 " + S[i].magnitude.ToString("F1")); return; }
        }
        // でかい歯車の ratio rW：各しかけの s = rW × (小歯車の ratio、rW=1 の時) / いまの軸の ratio が 1 に近く、正（向きが同じ）
        float bestCost = 1e9f, bestRW = 0f; int[] pick = new int[nf];
        var tries = new List<float>();
        for (int i = 0; i < nf; i++) foreach (var c in cands[i]) tries.Add(feeds[i].ratio / c[c.Count - 1].ratio);
        if (WheelRatio != 0f) { tries.Clear(); tries.Add(WheelRatio); }
        foreach (float rW in tries)
        {
            float worst = 0f; var pk = new int[nf];
            for (int i = 0; i < nf; i++)
            {
                float bc = 1e9f;
                for (int j = 0; j < cands[i].Count; j++)
                {
                    var c = cands[i][j]; float sc = rW * c[c.Count - 1].ratio / feeds[i].ratio; if (sc <= 0f) continue;
                    float cost = Mathf.Abs(Mathf.Log(sc)) + 0.03f * (c.Count - 3);
                    if (cost < bc) { bc = cost; pk[i] = j; }
                }
                worst = Mathf.Max(worst, bc);
            }
            if (worst < bestCost) { bestCost = worst; bestRW = rW; pick = pk; }
        }
        WheelRatioUsed = bestRW;
        // でかい歯車（まん中は塔の土台の中にかくれる。こしきの輪は塔の角の外）
        float rHub = UseTower ? 5.9f : HubR, rWin = RW - 1.25f * WM - 0.9f;
        var wins = new (float, float)[8]; for (int k = 0; k < 8; k++) wins[k] = (k * 45f + 22.5f, 18.5f);
        var wMesh = WindowGear(WZ, WM, WT, rHub, rWin, wins, "Clockwork_BigWheel_z" + WZ, 8, false, UseTower ? 0f : BoreR);
        var wg = CwGear(wf, "BigWheel_z" + WZ, Vector3.zero, Vector3.up, wMesh, cwDark);
        AddGear(wg, bestRW, 0f);
        var log = new System.Text.StringBuilder("[2–4] でかい歯車 z" + WZ + " 直径 " + (2f * RW).ToString("F1") + "m ratio " + bestRW.ToString("F4") + " ");
        for (int i = 0; i < nf; i++)
        {
            var c = cands[i][pick[i]]; var f = feeds[i];
            var pg = c[c.Count - 1]; float pr = bestRW * pg.ratio, sc = pr / f.ratio;
            // なか歯車（chain は でかい歯車 ratio 1 で出した。全部 rW 倍しても φ はそのままでかみ合う）
            for (int k = 1; k < c.Count - 1; k++)
            {
                var q = c[k]; float rq = WM * q.z / 2f;
                Mesh m = q.z >= 30 ? WindowGear(q.z, WM, WT, 0.8f, rq - 1.25f * WM - 0.5f, new[] { (45f, 30f), (135f, 30f), (225f, 30f), (315f, 30f) }, "Clockwork_Idler_m40_z" + q.z)
                                   : SpurMesh(q.z, WM, WT, "Clockwork_Spur_m40_th50_z" + q.z);
                var ig = CwGear(wf, "Feed" + i + "_Idler" + k + "_z" + q.z, new Vector3(q.c.x, 0f, q.c.y), Vector3.up, m, k % 2 == 1 ? cwBrass : cwLight);
                AddGear(ig, bestRW * q.ratio, q.phase * Mathf.Rad2Deg);
                CwShaft(ig, -0.3f, WT + 0.3f, q.z >= 30 ? 0.7f : 0.5f);
            }
            // 小歯車：たての軸の子。軸の角度（wf の 2D）= s·f.ratio·a + f.phase + yawP、欲しい = pr·a + φp → 子のずれ δ = φp − f.phase − yawP
            float delta = pg.phase * Mathf.Rad2Deg - f.phase - yawP[i];
            var pin = new GameObject("Feed" + i + "_Pinion_z" + pg.z).transform; pin.SetParent(f.shaftGear, false);
            pin.position = wf.TransformPoint(new Vector3(pg.c.x, 0f, pg.c.y)); pin.localPosition = new Vector3(0f, pin.localPosition.y, 0f);
            pin.localRotation = Quaternion.Euler(0f, -delta, 0f);
            var pm = SpurMesh(pg.z, WM, WT, "Clockwork_Spur_m40_th50_z" + pg.z);
            pin.gameObject.AddComponent<MeshFilter>().sharedMesh = pm; pin.gameObject.AddComponent<MeshRenderer>().sharedMaterial = cwBrass;
            CwShaft(pin, -0.3f, WT, 0.5f);
            // しかけの速さを sc 倍
            Transform groot = f.shaftGear; while (groot.parent != null && groot.parent != gim && groot.parent != root) groot = groot.parent;
            var ride = f.shaftGear.GetComponentInParent<FuwaGearRide>();
            if (ride != null) { ride.speed *= sc; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ride); }
            else for (int k = 0; k < cwG.Count; k++) if (cwG[k] != null && cwG[k].IsChildOf(groot)) cwR[k] *= sc;
            log.Append(" | " + groot.name + " 距離 " + S[i].magnitude.ToString("F1") + " なか " + string.Join("+", c.Skip(1).Take(c.Count - 2).Select(x => "z" + x.z)) + " 小 z" + pg.z + " 速さ×" + sc.ToString("F2"));
        }
        Debug.Log(log.ToString());
    }

    static void ClockworkInstall(Transform tower)
    {
        var cw = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaClockwork>(tower.gameObject);
        cw.speed = ClockSpeed;
        cw.gears = cwG.ToArray(); cw.ratio = cwR.ToArray(); cw.phase = cwP.ToArray();
        cw.sliderGear = slG.ToArray(); cw.sliders = slT.ToArray(); cw.rods = slRod.ToArray(); cw.sliderAxis = slAx.ToArray(); cw.pinR = slPin.ToArray(); cw.pinY = slPY.ToArray(); cw.rodLen = slLen.ToArray();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(cw); FuwaStage2WaterTest.NoSync(cw);
        ClockworkSet(cw, 0f);
    }
    // ===== 雲づくり工場（2026-11-08 てじゃ「歯車のしごとやろか」→ A「雲づくり工場」）=====
    //  塔はクリスタルの力で雲を作る機械。各しかけのたての軸（塔のでかい歯車から回る）の途中に大きい歯車（64枚）→ 小さい歯車（8枚、8倍速）→
    //   乗る歯車：大きいふいご＋煙突 ／ 穴あき歯車：雲の糸巻き ／ ごつい歯車：らせんカムで持ち上げて落とす臼 ／ 段つき歯車：小さいふいご2つ（交互）
    //  道の下（軸のてっぺんから FacDrop 下）、軸から外向き（ほかの物とぶつからない向きを探す）。動きは FuwaCloudFactory（軸の角度を読むだけ）
    // 機械の大きさ（2026-11-08 てじゃ「ちっちゃすぎる」→ まるごと2倍。大きい歯車も同じ倍率なのでかみ合いはそのまま）と高さ：しかけの軸のてっぺん（道の歯車の下の面）から FacDrop 下
    public static float FacScale = 2f;
    static float FacDrop => 1.35f * FacScale + 0.1f;
    const float FacMod = 0.05f; const int FacZBig = 64, FacZS = 8;
    static Material facLeather, facCloud, facWood;
    static void CloudFactory(Transform tower)
    {
        facLeather = FuwaStage2WaterTest.ToonMat("Stage2_4_Leather", new Color(0.47f, 0.32f, 0.24f));
        facWood = FuwaStage2WaterTest.ToonMat("Stage2_4_Wood", new Color(0.62f, 0.45f, 0.3f));
        facCloud = FuwaGearTest.Mat("GearRideTest_Steam", new Color(0.97f, 0.98f, 1f));
        var holder = new GameObject("CloudFactory").transform; holder.SetParent(root.Find("Gimmicks"), false);
        var tc = root.InverseTransformPoint(tower.position);
        var log = new System.Text.StringBuilder("[2–4] 雲づくり工場");
        foreach (var f in feeds)
        {
            var sh = f.shaftGear; string nm = sh.name;
            int kind = nm == "G1" ? 0 : nm.StartsWith("HoleGears") ? 2 : nm.StartsWith("Chunky") ? 1 : nm.StartsWith("StepGears") ? 3 : -1;
            if (kind < 0) continue;
            // 軸の回る速さ（2D 反時計回り、度/秒）
            float rate;
            var ride = sh.GetComponentInParent<FuwaGearRide>();
            if (ride != null) { int gi = System.Array.IndexOf(ride.gears, sh); rate = ride.ratio[gi] * ride.speed; }
            else { int ci = cwG.IndexOf(sh); rate = cwR[ci] * ClockSpeed; }
            log.Append(" | " + Machine(holder, sh, kind, tc, rate));
        }
        Debug.Log(log.ToString());
    }

    // 機械の場所の中の箱（frame の中 lo〜hi）が、ほかの物の見た目とぶつかる数
    static int FacHits(Transform F, Vector3 lo, Vector3 hi, Transform skip)
    {
        var b = new Bounds(F.TransformPoint(lo), Vector3.zero);
        for (int k = 0; k < 8; k++) b.Encapsulate(F.TransformPoint(new Vector3((k & 1) == 0 ? lo.x : hi.x, (k & 2) == 0 ? lo.y : hi.y, (k & 4) == 0 ? lo.z : hi.z)));
        int n = 0;
        foreach (var rr in root.GetComponentsInChildren<Renderer>())
        {
            if (rr.transform.IsChildOf(skip) || rr.transform.IsChildOf(F) || rr is ParticleSystemRenderer) continue;
            if (rr.transform.name.StartsWith("Factory")) continue;
            // 見た目の箱は回った物だと大きすぎるので、当たりがあればそちらも見る
            if (!rr.bounds.Intersects(b)) continue;
            // 中心どうしの横の距離で、細い物の箱の大きすぎをすこし割り引く（ざっくり）
            n++;
        }
        return n;
    }

    static string Machine(Transform holder, Transform shaft, int kind, Vector3 tc, float rate)
    {
        string[] names = { "Bellows", "Stamp", "Spool", "TwinBellows" };
        var F = new GameObject("Factory_" + names[kind] + "_" + shaft.name).transform; F.SetParent(holder, false);
        var S = root.InverseTransformPoint(shaft.position); float shaftTop = S.y; S.y = shaftTop - FacDrop - (kind == 0 ? 0.7f : 0f);   // 乗る歯車は軸のてっぺん＝歯車の上の面なので、歯車の厚みぶんさらに下
        F.localPosition = S; F.localScale = Vector3.one * FacScale;
        float Rb = FacMod * FacZBig / 2f, Rs = FacMod * FacZS / 2f, px = Rb + Rs;
        float topY = (shaftTop - 0.3f - S.y) / FacScale;   // 煙突のてっぺん（frame の y、道よりすこし下）
        // 機械の大きさ（frame の中）
        Vector3 lo, hi;
        switch (kind)
        {
            case 0: lo = new Vector3(Rb - 0.3f, -0.8f, -0.9f); hi = new Vector3(px + 3.6f, topY + 0.4f, 0.9f); break;
            case 3: lo = new Vector3(Rb - 0.3f, -0.8f, -2.7f); hi = new Vector3(px + 0.9f, topY + 0.4f, 2.7f); break;
            case 2: lo = new Vector3(Rb - 0.3f, -0.8f, -0.9f); hi = new Vector3(px + 4.0f, 1.8f, 0.9f); break;
            default: lo = new Vector3(Rb - 0.3f, -3.0f, -0.9f); hi = new Vector3(px + 1.6f, 1.4f, 0.9f); break;
        }
        // 向き：ぶつからない向きのうち、塔から外向きに近いもの
        Vector3 outw = S - tc; outw.y = 0f; outw.Normalize();
        float bestScore = 1e9f; Vector3 bestU = outw; int bestHits = -1;
        for (int k = 0; k < 24; k++)
        {
            float a = k * 15f * Mathf.Deg2Rad; var u = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            F.localRotation = Quaternion.LookRotation(Vector3.Cross(u, Vector3.up), Vector3.up);
            int h = FacHits(F, lo, hi, shaft);
            float score = h * 10f - Vector3.Dot(u, outw);
            if (score < bestScore) { bestScore = score; bestU = u; bestHits = h; }
        }
        F.localRotation = Quaternion.LookRotation(Vector3.Cross(bestU, Vector3.up), Vector3.up);

        // 大きい歯車（軸の子、いっしょに回る）と小さい歯車（ここで回す）
        var big = new GameObject("FactoryBig_z" + FacZBig).transform; big.SetParent(shaft, false);
        big.position = F.position; big.localPosition = new Vector3(0f, big.localPosition.y, 0f); big.localRotation = Quaternion.identity; big.localScale = Vector3.one * FacScale;
        big.gameObject.AddComponent<MeshFilter>().sharedMesh = SpurMesh(FacZBig, FacMod, 0.12f, "Factory_Big_z" + FacZBig);
        big.gameObject.AddComponent<MeshRenderer>().sharedMaterial = cwBrass;
        var bg = new FuwaGearTest.Gear { name = "Big", z = FacZBig, c = Vector2.zero, ratio = 1f, phase = 0f };
        var pg = FuwaGearTest.Mesh2(bg, "Pinion", FacZS, 0f, FacMod);
        var pin = new GameObject("FactoryPinion_z" + FacZS).transform; pin.SetParent(F, false); pin.localPosition = new Vector3(pg.c.x, 0f, pg.c.y);
        pin.gameObject.AddComponent<MeshFilter>().sharedMesh = SpurMesh(FacZS, FacMod, 0.3f, "Factory_Pinion_z" + FacZS);
        pin.gameObject.AddComponent<MeshRenderer>().sharedMaterial = cwDark;
        CwShaft(pin, kind == 0 ? -0.1f : -0.7f, 0.3f, 0.12f);   // 乗る歯車のは下に塔からの歯車があるので短く
        // 台（小さい歯車の下から機械の先まで）と、軸へのうで
        float bedEnd = kind == 1 ? px + 0.45f : hi.x - 0.2f;   // 臼は棒が台を通らないように、小さい歯車の所まで
        TBox(F, "FactoryBed", new Vector3((Rb - 0.2f + bedEnd) / 2f, -0.76f, (lo.z + hi.z) / 2f), new Vector3(bedEnd - (Rb - 0.2f), 0.12f, hi.z - lo.z - 0.2f), facWood, false);
        TBox(F, "FactoryArm", new Vector3((Rb - 0.2f) / 2f, -0.76f, 0f), new Vector3(Rb - 0.2f, 0.1f, 0.18f), cwAxle, false);
        var bear = GameObject.CreatePrimitive(PrimitiveType.Cylinder); bear.name = "FactoryBearing"; Object.DestroyImmediate(bear.GetComponent<Collider>());
        bear.transform.SetParent(F, false); bear.transform.localPosition = new Vector3(0f, -0.76f, 0f); bear.transform.localScale = new Vector3(0.75f, 0.08f, 0.75f); bear.GetComponent<Renderer>().sharedMaterial = cwAxle;

        var fc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaCloudFactory>(F.gameObject);
        fc.frame = F; fc.bigGear = big; fc.pinion = pin; fc.ratio = pg.ratio; fc.phase = pg.phase * Mathf.Rad2Deg;
        float pinRate = pg.ratio * rate;   // 小さい歯車の回る速さ（度/秒）
        fc.mode = kind == 1 ? 1 : kind == 2 ? 2 : 0;
        if (kind == 0) FacBellows(F, fc, pin, new[] { Vector3.right }, 1.25f, 0.36f, 1.15f, 2.8f, topY, 0.55f);
        else if (kind == 3) FacBellows(F, fc, pin, new[] { Vector3.forward, Vector3.back }, 0.75f, 0.24f, 0.8f, 1.85f, topY, 0.35f);
        else if (kind == 2) FacSpool(F, pin);
        else FacStamp(F, fc, pin, pinRate);
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fc); FuwaStage2WaterTest.NoSync(fc);
        // 今の角度に（エディタでも）
        float th; { var x = Quaternion.Inverse(F.rotation) * (big.rotation * Vector3.right); th = Mathf.Atan2(x.z, x.x) * Mathf.Rad2Deg; }
        pin.localRotation = Quaternion.Euler(0f, -Mathf.Repeat(fc.ratio * th + fc.phase, 360f), 0f);
        return names[kind] + "@" + shaft.name + " 向き " + (Mathf.Atan2(bestU.z, bestU.x) * Mathf.Rad2Deg).ToString("F0") + "° ぶつかり " + bestHits + " 1周 " + (360f / Mathf.Max(0.01f, Mathf.Abs(pinRate))).ToString("F1") + "秒";
    }

    // ふいご（1つか2つ）：小さい歯車の上にクランクの円盤とピン、棒、動く板、じゃばら、動かない板、ノズル→たての管→煙突、雲
    //  dirs：板が動く向き（frame の中）。B：ふいごの大きさ、R：ピンの半径、L：棒の長さ、fd：動かない板（小さい歯車の軸から）
    static void FacBellows(Transform F, FuwaCloudFactory fc, Transform pin, Vector3[] dirs, float B, float R, float L, float fd, float topY, float puffSize)
    {
        const float PY = 0.5f;   // ピン・ふいごの高さ（小さい歯車の上の面 0.3 から）
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); disc.name = "CrankDisc"; Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.transform.SetParent(pin, false); disc.transform.localPosition = new Vector3(0f, 0.34f, 0f); disc.transform.localScale = new Vector3(2f * R + 0.24f, 0.04f, 2f * R + 0.24f); disc.GetComponent<Renderer>().sharedMaterial = cwBrass;
        var cp = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cp.name = "CrankPin"; Object.DestroyImmediate(cp.GetComponent<Collider>());
        cp.transform.SetParent(pin, false); cp.transform.localPosition = new Vector3(R, (0.38f + PY + 0.08f) / 2f, 0f); cp.transform.localScale = new Vector3(0.09f, (PY + 0.08f - 0.38f) / 2f, 0.09f); cp.GetComponent<Renderer>().sharedMaterial = cwAxle;
        fc.pinR = R; fc.pinY = PY; fc.rodLen = L;
        int n = dirs.Length; var plates = new Transform[n]; var rods = new Transform[n]; var skins = new Transform[n]; var fds = new float[n]; var puffs = new ParticleSystem[n];
        var rings = new List<Transform>(); var owner = new List<int>(); var rt = new List<float>();
        var pc = pin.localPosition;
        for (int k = 0; k < n; k++)
        {
            var d = dirs[k]; var q = Quaternion.LookRotation(Vector3.Cross(d, Vector3.up), Vector3.up);   // local x = d
            // 動く板（原点＝板の、棒がつく面）
            var pl = new GameObject("BellowsPlate" + k).transform; pl.SetParent(F, false); pl.localRotation = q; pl.localPosition = pc + Vector3.up * PY + d * L;
            TBox(pl, "Board", new Vector3(0.05f, 0f, 0f), new Vector3(0.1f, B, B), facWood, false);
            TBox(pl, "Lug", new Vector3(-0.06f, 0f, 0f), new Vector3(0.12f, 0.16f, 0.16f), cwAxle, false);
            plates[k] = pl;
            var rod = new GameObject("BellowsRod" + k).transform; rod.SetParent(F, false);
            TBox(rod, "Bar", Vector3.zero, new Vector3(0.07f, 0.07f, L), cwAxle, false);
            rods[k] = rod;
            // 動かない板とその先：ノズル → たての管 → 煙突
            fds[k] = fd;
            var fx = new GameObject("BellowsFixed" + k).transform; fx.SetParent(F, false); fx.localRotation = q; fx.localPosition = pc + Vector3.up * PY + d * fd;
            TBox(fx, "Board", new Vector3(0.05f, 0f, 0f), new Vector3(0.1f, B, B), facWood, false);
            float nz = 0.55f, pr = Mathf.Max(0.12f, B * 0.16f);
            var noz = GameObject.CreatePrimitive(PrimitiveType.Cylinder); noz.name = "Nozzle"; Object.DestroyImmediate(noz.GetComponent<Collider>());
            noz.transform.SetParent(fx, false); noz.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); noz.transform.localPosition = new Vector3(0.1f + nz / 2f, 0f, 0f); noz.transform.localScale = new Vector3(pr * 2f, nz / 2f, pr * 2f); noz.GetComponent<Renderer>().sharedMaterial = cwDark;
            float up0 = 0f, up1 = topY - PY;
            var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pipe.name = "Pipe"; Object.DestroyImmediate(pipe.GetComponent<Collider>());
            pipe.transform.SetParent(fx, false); pipe.transform.localPosition = new Vector3(0.1f + nz + pr, (up0 - pr + up1) / 2f, 0f); pipe.transform.localScale = new Vector3(pr * 2f, (up1 - up0 + pr) / 2f, pr * 2f); pipe.GetComponent<Renderer>().sharedMaterial = cwDark;
            var elbow = GameObject.CreatePrimitive(PrimitiveType.Sphere); elbow.name = "Elbow"; Object.DestroyImmediate(elbow.GetComponent<Collider>());
            elbow.transform.SetParent(fx, false); elbow.transform.localPosition = new Vector3(0.1f + nz + pr, 0f, 0f); elbow.transform.localScale = Vector3.one * pr * 2.3f; elbow.GetComponent<Renderer>().sharedMaterial = cwDark;
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cap.name = "ChimneyCap"; Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.transform.SetParent(fx, false); cap.transform.localPosition = new Vector3(0.1f + nz + pr, up1 + 0.12f, 0f); cap.transform.localScale = new Vector3(pr * 3.4f, 0.12f, pr * 3.4f); cap.GetComponent<Renderer>().sharedMaterial = cwBrass;
            puffs[k] = FacPuff(fx, new Vector3(0.1f + nz + pr, up1 + 0.26f, 0f), Vector3.up, puffSize, 0f, pr * 1.2f);
            // じゃばら：輪 5つ（大小を交互）と、中の革
            for (int i = 1; i <= 5; i++)
            {
                var ring = TBox(F, "BellowsRing" + k + "_" + i, Vector3.zero, new Vector3(0.05f, (i % 2 == 1) ? B * 0.97f : B * 0.8f, (i % 2 == 1) ? B * 0.97f : B * 0.8f), facLeather, false);
                ring.localRotation = q; rings.Add(ring); owner.Add(k); rt.Add(i / 6f);
            }
            var skin = TBox(F, "BellowsSkin" + k, Vector3.zero, new Vector3(1f, B * 0.84f, B * 0.84f), facLeather, false);
            skin.localRotation = q; skins[k] = skin;
        }
        fc.axisDir = dirs; fc.plates = plates; fc.rods = rods; fc.skins = skins; fc.fixedDist = fds; fc.puffs = puffs;
        fc.rings = rings.ToArray(); fc.ringOwner = owner.ToArray(); fc.ringT = rt.ToArray();
        fc.puffCount = n == 1 ? 8 : 5;
        // エディタでも形を（今の角度で）：ピンが +x の時
        for (int k = 0; k < n; k++)
        {
            float dist = L + Vector3.Dot(new Vector3(R, 0f, 0f), dirs[k]);
            plates[k].localPosition = pc + Vector3.up * PY + dirs[k] * dist;
            for (int i = 0; i < rings.Count; i++) if (owner[i] == k) rings[i].localPosition = pc + Vector3.up * PY + dirs[k] * Mathf.Lerp(dist, fds[k], rt[i]);
            skins[k].localPosition = pc + Vector3.up * PY + dirs[k] * ((dist + fds[k]) / 2f); var ls = skins[k].localScale; ls.x = fds[k] - dist; skins[k].localScale = ls;
            var pinP = pc + Vector3.up * PY + new Vector3(R, 0f, 0f); var sp = plates[k].localPosition;
            rods[k].localPosition = (pinP + sp) / 2f; rods[k].localRotation = Quaternion.LookRotation((sp - pinP).normalized, Vector3.up);
        }
    }

    // 雲の粒（ぽふっ）：rate > 0 なら出しっぱなし
    static ParticleSystem FacPuff(Transform parent, Vector3 lp, Vector3 dir, float size, float rate, float radius)
    {
        var go = new GameObject("CloudPuff"); go.transform.SetParent(parent, false); go.transform.localPosition = lp;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, dir);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.playOnAwake = rate > 0f; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.4f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size); main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 80; main.gravityModifier = -0.02f;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = radius;
        var vel = ps.limitVelocityOverLifetime; vel.enabled = true; vel.limit = 0.35f; vel.dampen = 0.08f;
        var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1.4f), new Keyframe(1f, 0f)));
        var pr = go.GetComponent<ParticleSystemRenderer>(); pr.renderMode = ParticleSystemRenderMode.Mesh;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere); pr.mesh = tmp.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(tmp);
        pr.sharedMaterial = facCloud;
        if (rate <= 0f) ps.Stop();
        return ps;
    }

    // 雲の糸巻き：小さい歯車の上に糸巻き（いっしょに回る）。雲のかたまりから糸がのびて巻き取られている
    static void FacSpool(Transform F, Transform pin)
    {
        var drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder); drum.name = "SpoolDrum"; Object.DestroyImmediate(drum.GetComponent<Collider>());
        drum.transform.SetParent(pin, false); drum.transform.localPosition = new Vector3(0f, 0.85f, 0f); drum.transform.localScale = new Vector3(0.5f, 0.55f, 0.5f); drum.GetComponent<Renderer>().sharedMaterial = facWood;
        foreach (float y in new[] { 0.36f, 1.34f })
        {
            var fl = GameObject.CreatePrimitive(PrimitiveType.Cylinder); fl.name = "SpoolFlange"; Object.DestroyImmediate(fl.GetComponent<Collider>());
            fl.transform.SetParent(pin, false); fl.transform.localPosition = new Vector3(0f, y, 0f); fl.transform.localScale = new Vector3(1.5f, 0.04f, 1.5f); fl.GetComponent<Renderer>().sharedMaterial = cwBrass;
        }
        var yarn = GameObject.CreatePrimitive(PrimitiveType.Cylinder); yarn.name = "SpoolYarn"; Object.DestroyImmediate(yarn.GetComponent<Collider>());
        yarn.transform.SetParent(pin, false); yarn.transform.localPosition = new Vector3(0f, 0.85f, 0f); yarn.transform.localScale = new Vector3(1.15f, 0.44f, 1.15f); yarn.GetComponent<Renderer>().sharedMaterial = facCloud;
        // 巻いた雲のでこぼこ（回っているのが分かるように）
        for (int k = 0; k < 7; k++)
        {
            float a = k * 360f / 7f * Mathf.Deg2Rad;
            var lump = GameObject.CreatePrimitive(PrimitiveType.Sphere); lump.name = "YarnLump"; Object.DestroyImmediate(lump.GetComponent<Collider>());
            lump.transform.SetParent(pin, false); lump.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.55f, 0.6f + 0.5f * ((k * 3) % 5) / 4f, Mathf.Sin(a) * 0.55f); lump.transform.localScale = Vector3.one * 0.3f;
            lump.GetComponent<Renderer>().sharedMaterial = facCloud;
        }
        // 雲のかたまり（動かない）と、そこから糸巻きへの糸
        var pc = pin.localPosition; var src = pc + new Vector3(3.3f, 0.95f, 0f);
        var tuft = new GameObject("CloudTuft").transform; tuft.SetParent(F, false); tuft.localPosition = src;
        foreach (var (o, s) in new[] { (new Vector3(0f, 0f, 0f), 0.9f), (new Vector3(0.35f, 0.2f, 0.3f), 0.65f), (new Vector3(0.25f, -0.15f, -0.35f), 0.6f), (new Vector3(-0.2f, 0.3f, 0.1f), 0.5f) })
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); b.name = "Tuft"; Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(tuft, false); b.transform.localPosition = o; b.transform.localScale = Vector3.one * s; b.GetComponent<Renderer>().sharedMaterial = facCloud;
        }
        var a0 = pc + new Vector3(0.55f, 0.85f, 0f); var a1 = src + new Vector3(-0.4f, 0f, 0f);
        var strand = TBox(F, "CloudStrand", (a0 + a1) / 2f, new Vector3(0.07f, 0.07f, (a1 - a0).magnitude), facCloud, false);
        strand.localRotation = Quaternion.LookRotation(a1 - a0);
        // 糸にそって小さい雲がすこしずつ糸巻きへ
        var ps = FacPuff(F, src + new Vector3(-0.45f, 0f, 0f), Vector3.left, 0.16f, 2.5f, 0.08f);
        var m = ps.main; m.startSpeed = 0.9f; m.startLifetime = 3.0f; m.gravityModifier = 0f;
        var lv = ps.limitVelocityOverLifetime; lv.enabled = false;
        var sh = ps.shape; sh.angle = 2f;
        // 台の上の、雲のかたまりを支える柱
        TBox(F, "TuftPost", src + new Vector3(0f, (-0.7f - 0.95f) / 2f - 0.05f, 0f), new Vector3(0.12f, 1.55f, 0.12f), cwAxle, false);
    }

    // 臼：小さい歯車の上のらせんカム（1周で steps 回、lift 上がってストンと落ちる）に、臼の腕のころが乗っている。落ちると下のおけの雲の生地をたたいて、ぽふっ
    static void FacStamp(Transform F, FuwaCloudFactory fc, Transform pin, float pinRate)
    {
        const float r1 = 0.14f, r2 = 0.55f, y0 = 0.42f, lift = 0.6f; const int steps = 2, N = 24;
        float sg = pinRate >= 0f ? 1f : -1f;
        // らせんカム（小さい歯車の local、y0 から）：t で 0→lift、角度 φ = sg × (−(j+t)×360/steps)。台の円盤も
        var v = new List<Vector3>(); var tr = new List<int>();
        Vector3 P(float r, float phi, float y) { float a = phi * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
        for (int j = 0; j < steps; j++)
        {
            for (int i = 0; i < N; i++)
            {
                float t0 = (float)i / N, t1 = (float)(i + 1) / N;
                float f0 = sg * (-(j + t0) * 360f / steps), f1 = sg * (-(j + t1) * 360f / steps);
                float h0 = y0 + lift * t0, h1 = y0 + lift * t1;
                Tri(v, tr, P(r1, f0, h0), P(r2, f0, h0), P(r2, f1, h1), Vector3.up); Tri(v, tr, P(r1, f0, h0), P(r2, f1, h1), P(r1, f1, h1), Vector3.up);
                var om = (P(r2, f0, 0f) + P(r2, f1, 0f)) * 0.5f; om.y = 0f;
                Tri(v, tr, P(r2, f0, y0), P(r2, f1, y0), P(r2, f1, h1), om); Tri(v, tr, P(r2, f0, y0), P(r2, f1, h1), P(r2, f0, h0), om);
                Tri(v, tr, P(r1, f0, y0), P(r1, f1, y0), P(r1, f1, h1), -om); Tri(v, tr, P(r1, f0, y0), P(r1, f1, h1), P(r1, f0, h0), -om);
            }
            // 落ちる面（t=1 の所）
            float fe = sg * (-(j + 1) * 360f / steps); var tg = P(1f, fe - sg * 5f, 0f) - P(1f, fe, 0f);
            Tri(v, tr, P(r1, fe, y0), P(r2, fe, y0), P(r2, fe, y0 + lift), tg); Tri(v, tr, P(r1, fe, y0), P(r2, fe, y0 + lift), P(r1, fe, y0 + lift), tg);
        }
        var cam = new GameObject("StampCam"); cam.transform.SetParent(pin, false);
        cam.AddComponent<MeshFilter>().sharedMesh = SaveMesh("Factory_StampCam_" + (sg > 0 ? "p" : "n"), v, tr); cam.AddComponent<MeshRenderer>().sharedMaterial = cwBrass;
        var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cylinder); basePlate.name = "CamBase"; Object.DestroyImmediate(basePlate.GetComponent<Collider>());
        basePlate.transform.SetParent(pin, false); basePlate.transform.localPosition = new Vector3(0f, (0.3f + y0) / 2f, 0f); basePlate.transform.localScale = new Vector3(2f * r2 + 0.06f, (y0 - 0.3f) / 2f, 2f * r2 + 0.06f); basePlate.GetComponent<Renderer>().sharedMaterial = cwDark;
        // 臼（上下に動く）：原点はころの下（カムの面の上）。ころ → 腕 → たての棒 → 下の重い頭
        var pc = pin.localPosition; float rf = (r1 + r2) / 2f, stemX = pc.x + r2 + 0.35f, rollR = 0.07f;
        var hm = new GameObject("Stamp").transform; hm.SetParent(F, false); hm.localPosition = new Vector3(stemX, y0 + rollR, pc.z);
        var roll = GameObject.CreatePrimitive(PrimitiveType.Cylinder); roll.name = "Roller"; Object.DestroyImmediate(roll.GetComponent<Collider>());
        roll.transform.SetParent(hm, false); roll.transform.localPosition = new Vector3(pc.x + rf - stemX, 0f, 0f); roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); roll.transform.localScale = new Vector3(2f * rollR, 0.1f, 2f * rollR); roll.GetComponent<Renderer>().sharedMaterial = cwAxle;
        TBox(hm, "Arm", new Vector3((pc.x + rf - stemX) / 2f, 0.06f, 0f), new Vector3(stemX - pc.x - rf + 0.1f, 0.08f, 0.14f), cwDark, false);
        const float stemLen = 1.9f;
        var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder); stem.name = "Stem"; Object.DestroyImmediate(stem.GetComponent<Collider>());
        stem.transform.SetParent(hm, false); stem.transform.localPosition = new Vector3(0f, 0.15f - stemLen / 2f, 0f); stem.transform.localScale = new Vector3(0.16f, stemLen / 2f, 0.16f); stem.GetComponent<Renderer>().sharedMaterial = cwAxle;
        float headY = 0.15f - stemLen - 0.25f;
        TBox(hm, "Head", new Vector3(0f, headY, 0f), new Vector3(0.75f, 0.5f, 0.75f), facWood, false);
        TBox(hm, "HeadBand", new Vector3(0f, headY + 0.12f, 0f), new Vector3(0.79f, 0.1f, 0.79f), cwBrass, false);
        // おけ（動かない）：頭がいちばん下（u=0）の時、頭の下の面＝雲の生地の上の面
        float doughTop = y0 + rollR + headY - 0.25f;
        var tubC = new Vector3(stemX, doughTop - 0.3f, pc.z);
        TBox(F, "TubFloor", tubC + new Vector3(0f, -0.28f, 0f), new Vector3(1.3f, 0.08f, 1.3f), facWood, false);
        foreach (var (o, s) in new[] { (new Vector3(0.62f, 0f, 0f), new Vector3(0.08f, 0.6f, 1.3f)), (new Vector3(-0.62f, 0f, 0f), new Vector3(0.08f, 0.6f, 1.3f)), (new Vector3(0f, 0f, 0.62f), new Vector3(1.16f, 0.6f, 0.08f)), (new Vector3(0f, 0f, -0.62f), new Vector3(1.16f, 0.6f, 0.08f)) })
            TBox(F, "TubWall", tubC + o, s, facWood, false);
        var dough = GameObject.CreatePrimitive(PrimitiveType.Sphere); dough.name = "CloudDough"; Object.DestroyImmediate(dough.GetComponent<Collider>());
        dough.transform.SetParent(F, false); dough.transform.localPosition = new Vector3(stemX, doughTop - 0.22f, pc.z); dough.transform.localScale = new Vector3(1.12f, 0.44f, 1.12f); dough.GetComponent<Renderer>().sharedMaterial = facCloud;
        // 棒のガイド（動かない柱と、棒を通す輪 2つ）
        float postX = stemX + 0.42f;
        TBox(F, "GuidePost", new Vector3(postX, (tubC.y + 0.3f + y0 + 0.9f) / 2f, pc.z), new Vector3(0.14f, y0 + 0.9f - tubC.y - 0.3f, 0.14f), cwDark, false);
        foreach (float gy in new[] { y0 - 0.5f, doughTop + 1.3f })
        {
            TBox(F, "GuideArm", new Vector3((stemX + postX) / 2f + 0.06f, gy, pc.z), new Vector3(postX - stemX - 0.05f, 0.08f, 0.1f), cwDark, false);
            foreach (int sz in new[] { -1, 1 }) TBox(F, "GuideJaw", new Vector3(stemX, gy, pc.z + sz * 0.13f), new Vector3(0.26f, 0.1f, 0.05f), cwDark, false);
        }
        fc.hammer = hm; fc.hammerBaseY = y0 + rollR; fc.lift = lift; fc.steps = steps; fc.camSign = sg;
        fc.hammerPuff = FacPuff(F, new Vector3(stemX, doughTop + 0.05f, pc.z), Vector3.up, 0.4f, 0f, 0.45f);
        fc.puffCount = 7;
    }

    // ===== まん中の巨大わたあめ機（2026-11-08 てじゃ「時計塔自体なしにして、デカい機械を」→ A わたあめ機「機械はその歯車の上にういててもいい」）=====
    //  動力：でかい歯車（88枚）のまん中に立つでかいクリスタル（歯車といっしょに回る。てじゃ「最初の動力源は下のデカい歯車の中心のデカいクリスタル」）
    //  → 浮いている歯車箱（60:12 を4組、同じ軸にもどる形で 625 倍）→ わたあめ機の頭（鍋のまん中でくるくる、雲の糸が出る）
    //  鍋は浮いていて回らない。ふちに雲の糸がたまっている。大きさは回転床 A・まわりの道とぶつからないように
    public static int GoalPlan = 1;
    public static bool PathUnder = false; public static float PathBevel = 0.08f;
    public static bool UseTower = false, UseFactory = false;
    static Transform coreT;
    public static float CrystalH = 6.6f, BoxY = 3.4f, CrystalGap = 0.8f, CrystalDown = 5f, CrystalW = 4.6f, CrystalHalf = 6.5f, CrystalSpin = -0.3f, FanSpin = 70f;
    const float CcMod = 0.1f; const int CcBig = 60, CcSmall = 12;
    static void CloudBalloon(Transform core, System.Text.StringBuilder log)
    {
        var wf = root.Find("Gimmicks/BigWheel_Frame"); var wheel = wf ? wf.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "BigWheel_z" + WZ) : null;
        if (!wheel) { log.Append("でかい歯車がない "); return; }
        var holder = new GameObject("CloudBalloon").transform; holder.SetParent(root.Find("Gimmicks"), false);
        var cc = root.InverseTransformPoint(core.position); holder.localPosition = new Vector3(cc.x, 0f, cc.z);
        var crystalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_3_Crystal.mat");
        var cryMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaStage2/Meshes/Course2_3/ObsCrystal1_Main.asset");
        float wheelTop = WT;   // でかい歯車の上の面（歯車の local）
        // クリスタル（2026-11-08 てじゃ「空中に浮いた単体の巨大クリスタルのまわりにギア」「下にもとびだしてる」）：でかい歯車のまん中の穴に浮く、上下にとがった1本。
        //  歯車にはつかない（ゆっくり自分で回る）。上のとがりの少し上にバルーンの首の輪と羽根（さわっていない）
        float wy = root.InverseTransformPoint(wf.position).y, cMid = wy + WT / 2f, cTop = cMid + CrystalHalf, cBot = cMid - CrystalHalf;   // 2026-11-08 てじゃ「上下おなじながさにして」「もうちょい短く、かつ太く」
        if (cryMesh && crystalMat)
        {
            // 2026-11-08 てじゃ「ちゃんと切れ目消して」→ 上下を1つのメッシュに（六角の柱＋上下のとがり、面ごとに平ら）
            var cr = new GameObject("PowerCrystal").transform; cr.SetParent(holder, false); cr.localPosition = new Vector3(0f, cMid, 0f);
            cr.gameObject.AddComponent<MeshFilter>().sharedMesh = CrystalMesh(CrystalW / 2f, cTop - cMid, cMid - cBot, "Power_Crystal");
            cr.gameObject.AddComponent<MeshRenderer>().sharedMaterial = crystalMat;
            AddGear(cr, CrystalSpin, 0f);   // ゆっくり回る（FuwaClockwork）
        }
        // 2026-11-08 てじゃ「こうなるとバルーンの下のギアはいらんな」→ 歯車箱なし。「下のプロペラいらんかも、穴うめて、ドームも球状に」→ 羽根もなし、首の輪の底はふさぐ
        // 首の輪（動かない）：パイプはこの横につながる
        var collar = new GameObject("BalloonCollar"); collar.transform.SetParent(holder, false);
        collar.AddComponent<MeshFilter>().sharedMesh = RingMesh(CollarR0, CollarR1, CollarY0, CollarY1, 48, "Balloon_Collar"); collar.AddComponent<MeshRenderer>().sharedMaterial = cwBrass;
        { var bt = GameObject.CreatePrimitive(PrimitiveType.Cylinder); bt.name = "CollarBottom"; Object.DestroyImmediate(bt.GetComponent<Collider>());
          bt.transform.SetParent(holder, false); bt.transform.localPosition = new Vector3(0f, CollarY0 + 0.06f, 0f); bt.transform.localScale = new Vector3(2f * CollarR0 + 0.02f, 0.05f, 2f * CollarR0 + 0.02f); bt.GetComponent<Renderer>().sharedMaterial = cwBrass; }   // 底のふた（輪の内がわにはまる。下の面は輪の下の面より 1cm 上）
        // バルーン（ゆっくり息をする：FuwaCloudBalloon）
        var body = new GameObject("BalloonBody").transform; body.SetParent(holder, false); body.localPosition = new Vector3(0f, BalloonY, 0f);
        var balM = FuwaStage2WaterTest.ToonMat("Stage2_4_Balloon", new Color(0.97f, 0.97f, 0.99f));
        var prof = BalloonProf;
        var skin = new GameObject("BalloonSkin"); skin.transform.SetParent(body, false); skin.layer = 11;
        var sm = LatheMesh(prof, 0f, 64, "Balloon_Skin");
        skin.AddComponent<MeshFilter>().sharedMesh = sm; skin.AddComponent<MeshRenderer>().sharedMaterial = balM;
        { var col = new GameObject("BalloonCol"); col.layer = 11; col.transform.SetParent(holder, false); col.transform.localPosition = body.localPosition; col.AddComponent<MeshCollider>().sharedMesh = sm; }   // 当たりは息をしない（毎フレーム大きさを変えると当たりの作り直しになる）
        // 真ちゅうの帯（いちばん太い所と、下のほう）と、たての網（8本）
        foreach (float lat in new[] { 0f, -38f })
        {
            float y = BalloonR + BalloonR * Mathf.Sin(lat * Mathf.Deg2Rad), yn = Mathf.Abs(lat) < 1f ? BalloonR : y + 0.11f;   // 帯の中でいちばん太い所
            float r = Mathf.Sqrt(Mathf.Max(0f, BalloonR * BalloonR - (yn - BalloonR) * (yn - BalloonR)));
            var hp = new GameObject("BalloonHoop"); hp.transform.SetParent(body, false);
            hp.AddComponent<MeshFilter>().sharedMesh = RingMesh(r - 0.05f, r + 0.1f, y - 0.11f, y + 0.11f, 64, "Balloon_Hoop" + (int)lat); hp.AddComponent<MeshRenderer>().sharedMaterial = cwBrass;
        }
        var net = new GameObject("BalloonNet"); net.transform.SetParent(body, false);
        net.AddComponent<MeshFilter>().sharedMesh = LatheRibbons(prof, 0.035f, 0.07f, 8, "Balloon_Net"); net.AddComponent<MeshRenderer>().sharedMaterial = cwAxle;
        // てっぺんの煙突（真ちゅうの輪＋中のふた）
        float topY = prof[prof.Length - 1].y;
        var chim = new GameObject("BalloonChimney"); chim.transform.SetParent(body, false);
        chim.AddComponent<MeshFilter>().sharedMesh = RingMesh(0.72f, prof[prof.Length - 1].x + 0.12f, topY - 0.3f, topY + 0.55f, 32, "Balloon_Chimney"); chim.AddComponent<MeshRenderer>().sharedMaterial = cwBrass;
        var lid = GameObject.CreatePrimitive(PrimitiveType.Cylinder); lid.name = "ChimneyInside"; Object.DestroyImmediate(lid.GetComponent<Collider>());
        lid.transform.SetParent(body, false); lid.transform.localPosition = new Vector3(0f, topY + 0.1f, 0f); lid.transform.localScale = new Vector3(1.46f, 0.02f, 1.46f); lid.GetComponent<Renderer>().sharedMaterial = cwAxle;
        // 雲：いつもぽわぽわ＋パイプからざらめが着いたらぽわっ
        facCloud = FuwaGearTest.Mat("GearRideTest_Steam", new Color(0.97f, 0.98f, 1f));
        var idle = FacPuff(holder, new Vector3(0f, BalloonY + topY + 0.5f, 0f), Vector3.up, 1.4f, 1.6f, 0.5f); idle.name = "BalloonPuffIdle";
        { var mm = idle.main; mm.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 1.6f); mm.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f); var lv = idle.limitVelocityOverLifetime; lv.limit = 0.5f; }
        var burst = FacPuff(holder, new Vector3(0f, BalloonY + topY + 0.5f, 0f), Vector3.up, 1.9f, 0f, 0.6f); burst.name = "BalloonPuffBurst";
        { var mm = burst.main; mm.startSpeed = new ParticleSystem.MinMaxCurve(2f, 3.2f); mm.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f); var lv = burst.limitVelocityOverLifetime; lv.limit = 0.6f; var sh = burst.shape; sh.angle = 30f; }
        var cb = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaCloudBalloon>(holder.gameObject);
        cb.body = body; cb.burst = burst; cb.burstCount = 7;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(cb); FuwaStage2WaterTest.NoSync(cb);
        balloon = cb;
        log.Append(" バルーン：球 半径 " + BalloonR + " ");
    }
    // 上下にとがった六角のクリスタル（中心 0、上 up・下 dn まで、半径 r）。とがりの長さは半径の 1.6 倍
    static Mesh CrystalMesh(float r, float up, float dn, string name)
    {
        var v = new List<Vector3>(); var t = new List<int>(); const int n = 6; float tip = r * 1.6f;
        float yT = up - tip, yB = -dn + tip;
        Vector3 Rim(int k, float y) { float a = (k * 60f + 30f) * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
        float mid = (yT + yB) / 2f;
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            var cen = (a + b + c) / 3f; var o = cen - new Vector3(0f, Mathf.Clamp(cen.y, yB, yT) == cen.y ? cen.y : mid, 0f);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), o) < 0f) { var s2 = b; b = c; c = s2; }
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }
        for (int k = 0; k < n; k++)
        {
            int k2 = (k + 1) % n;
            Tri(Rim(k, yB), Rim(k2, yB), Rim(k2, yT)); Tri(Rim(k, yB), Rim(k2, yT), Rim(k, yT));
            Tri(Rim(k, yT), Rim(k2, yT), new Vector3(0f, up, 0f));
            Tri(Rim(k, yB), Rim(k2, yB), new Vector3(0f, -dn, 0f));
        }
        return SaveMesh(name, v, t);
    }
    static FuwaCloudBalloon balloon;
    public static float CollarR0 = 1.7f, CollarR1 = 2.3f, CollarY0 = 5.0f, CollarY1 = 6.4f, PipeInY = 5.7f, BalloonR = 5.2f;
    // 球のバルーン：首の輪のてっぺんで球の半径が輪の外より少し内（2.2m）になる高さに球の底
    static float BalloonY => CollarY1 - (BalloonR - Mathf.Sqrt(BalloonR * BalloonR - 2.2f * 2.2f));
    // バルーンの形（r, y）：下の首から、ふくらんで、てっぺんの煙突まで
    // 球（底から、てっぺんの煙突の半径 1.0 まで）
    static Vector2[] BalloonProf
    {
        get
        {
            float top = Mathf.Acos(1.0f / BalloonR) * Mathf.Rad2Deg; const int n = 28; var pr = new Vector2[n + 1];
            for (int i = 0; i <= n; i++) { float a = Mathf.Lerp(-90f, top, i / (float)n) * Mathf.Deg2Rad; pr[i] = new Vector2(Mathf.Max(0.001f, BalloonR * Mathf.Cos(a)), BalloonR + BalloonR * Mathf.Sin(a)); }
            return pr;
        }
    }

    // 回転体（外向き、1枚）。prof を外へ off ずらす
    static Mesh LatheMesh(Vector2[] prof, float off, int n, string name)
    {
        var v = new List<Vector3>(); var t = new List<int>(); int m = prof.Length;
        var line = OffsetProf(prof, off);
        for (int j = 0; j <= n; j++) { float a = 2f * Mathf.PI * j / n; for (int i = 0; i < m; i++) v.Add(new Vector3(Mathf.Cos(a) * line[i].x, line[i].y, Mathf.Sin(a) * line[i].x)); }
        for (int j = 0; j < n; j++) for (int i = 0; i < m - 1; i++) { int a0 = j * m + i, a1 = a0 + 1, b0 = a0 + m, b1 = b0 + 1; t.Add(a0); t.Add(a1); t.Add(b0); t.Add(a1); t.Add(b1); t.Add(b0); }
        return SaveMesh(name, v, t);
    }
    static Vector2[] OffsetProf(Vector2[] prof, float off)
    {
        int m = prof.Length; var o = new Vector2[m];
        for (int i = 0; i < m; i++) { Vector2 tg = (i < m - 1 ? prof[i + 1] : prof[i]) - (i > 0 ? prof[i - 1] : prof[i]); tg.Normalize(); o[i] = prof[i] + new Vector2(tg.y, -tg.x) * off; }
        return o;
    }
    // 回転体にそったたての帯（cnt 本、幅 w、外へ off）
    static Mesh LatheRibbons(Vector2[] prof, float off, float w, int cnt, string name)
    {
        var v = new List<Vector3>(); var t = new List<int>(); var line = OffsetProf(prof, off); int m = line.Length;
        for (int k = 0; k < cnt; k++)
        {
            float a = 2f * Mathf.PI * k / cnt; var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); var s = new Vector3(-d.z, 0f, d.x);
            int b = v.Count;
            for (int i = 0; i < m; i++) { var c = d * line[i].x + Vector3.up * line[i].y; float hw = w * 0.5f; v.Add(c - s * hw); v.Add(c + s * hw); }
            for (int i = 0; i < m - 1; i++) { int a0 = b + 2 * i; t.Add(a0); t.Add(a0 + 2); t.Add(a0 + 1); t.Add(a0 + 1); t.Add(a0 + 2); t.Add(a0 + 3); }
        }
        return SaveMesh(name, v, t);
    }

    // ===== 色ざらめのパイプ（2026-11-08 てじゃ「周りの自動で回ってる歯車の末端も、何かしらの形でつなげて」→ A。「羽でまわす歯車はべつにいい」）=====
    //  自動で回るしかけ（乗る歯車・穴あき・ごつい・段つき）の最後の歯車の下に、64:8 の増速歯車 → クランク → ピストンのポンプ（FuwaCloudFactory）。
    //  ひと押しごとに色つきの太いパイプ（FuwaSugarPipe）の中をざらめのふくらみが流れて、まん中の鍋の底へ。着いたらその色の雲の糸
    //  2026-11-08 てじゃ「情報量がふえすぎた」「白パイプだけにして、上にあげてでっかいバルーン状の機械に接続」「ふくらみはあってもいい」→ 色なしの白いパイプ、まん中のバルーンの首へ
    static readonly Color PipeCol = new Color(0.95f, 0.95f, 0.97f), BulgeCol = new Color(1f, 0.99f, 0.96f);
    public static float PumpScale = 1.3f, PipeR = 0.3f, PipeRiseR = 5.2f, TankR = 0.55f, TankH = 0.9f;
    static void SugarPipes(Transform core, System.Text.StringBuilder log)
    {
        facWood = FuwaStage2WaterTest.ToonMat("Stage2_4_Wood", new Color(0.62f, 0.45f, 0.3f));
        facCloud = FuwaGearTest.Mat("GearRideTest_Steam", new Color(0.97f, 0.98f, 1f));
        var holder = new GameObject("SugarPipes").transform; holder.SetParent(root.Find("Gimmicks"), false);
        var cc = root.InverseTransformPoint(core.position); cc.y = 0f;
        string[] names = { "G4", "HG2_z24", "CG1_z8", "SGB" };
        for (int k = 0; k < names.Length; k++)
        {
            var g = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == names[k]);
            if (!g) { log.Append(names[k] + " がない "); continue; }
            float rate;
            var ride = g.GetComponentInParent<FuwaGearRide>();
            if (ride != null) { int gi = System.Array.IndexOf(ride.gears, g); rate = ride.ratio[gi] * ride.speed; }
            else { int ci = cwG.IndexOf(g); rate = ci >= 0 ? cwR[ci] * ClockSpeed : 0f; }
            // 歯車の下の面（見た目のいちばん下）
            float bottom = 1e9f;
            foreach (var rr in g.GetComponentsInChildren<Renderer>(true)) { if (rr.name.StartsWith("Factory")) continue; bottom = Mathf.Min(bottom, root.InverseTransformPoint(rr.bounds.min).y); }
            log.Append(" | " + Pump(holder, g, k, bottom, rate, cc));
        }
    }

    static string Pump(Transform holder, Transform g, int k, float bottom, float rate, Vector3 cc)
    {
        float sc = PumpScale;
        var gp = root.InverseTransformPoint(g.position);
        var F = new GameObject("SugarPump" + k + "_" + g.name).transform; F.SetParent(holder, false);
        F.localPosition = new Vector3(gp.x, bottom - 0.35f - 0.9f * sc, gp.z); F.localScale = Vector3.one * sc;
        Vector3 u = cc - new Vector3(gp.x, 0f, gp.z); u.y = 0f; u.Normalize();
        F.localRotation = Quaternion.LookRotation(Vector3.Cross(u, Vector3.up), Vector3.up);
        var pipeM = FuwaStage2WaterTest.ToonMat("Course2_4_SugarPipe", PipeCol);
        // 大きい歯車（最後の歯車の子：いっしょに回る）と、そこまでの軸
        var big = new GameObject("FactoryBig_z" + FacZBig).transform; big.SetParent(g, false);
        big.position = F.position; big.localPosition = new Vector3(0f, big.localPosition.y, 0f); big.localRotation = Quaternion.identity; big.localScale = Vector3.one * sc;
        big.gameObject.AddComponent<MeshFilter>().sharedMesh = SpurMesh(FacZBig, FacMod, 0.12f, "Factory_Big_z" + FacZBig);
        big.gameObject.AddComponent<MeshRenderer>().sharedMaterial = cwBrass;
        float bigY = big.localPosition.y, gearBottomLocal = g.InverseTransformPoint(root.TransformPoint(new Vector3(gp.x, bottom, gp.z))).y;
        CwShaft(g, bigY - 0.15f, gearBottomLocal, 0.3f);
        var bg = new FuwaGearTest.Gear { name = "Big", z = FacZBig, c = Vector2.zero, ratio = 1f, phase = 0f };
        var pg = FuwaGearTest.Mesh2(bg, "Pinion", FacZS, 0f, FacMod);
        // タンクが道や歯車にぶつからない向きにポンプごと回す（大きい歯車の軸のまわり）
        int tankHits;
        {
            var baseRot = F.localRotation; float bestYaw = 0f; int best = int.MaxValue;
            var tcl = new Vector3(pg.c.x, 0f, pg.c.y) + new Vector3(1.9f + 0.1f + 0.2f + TankR, -0.5f + TankR + TankH / 2f, 0f);
            foreach (float yaw in new[] { 0f, 180f, 90f, -90f, 45f, -45f, 135f, -135f })
            {
                F.localRotation = Quaternion.Euler(0f, yaw, 0f) * baseRot;
                int h = TankHits(F, g, tcl);
                if (h < best) { best = h; bestYaw = yaw; }
                if (h == 0) break;
            }
            F.localRotation = Quaternion.Euler(0f, bestYaw, 0f) * baseRot; tankHits = best;
        }
        
        var pin = new GameObject("FactoryPinion_z" + FacZS).transform; pin.SetParent(F, false); pin.localPosition = new Vector3(pg.c.x, 0f, pg.c.y);
        pin.gameObject.AddComponent<MeshFilter>().sharedMesh = SpurMesh(FacZS, FacMod, 0.3f, "Factory_Pinion_z" + FacZS);
        pin.gameObject.AddComponent<MeshRenderer>().sharedMaterial = cwDark;
        CwShaft(pin, -0.5f, 0.3f, 0.12f);
        // 台と、大きい歯車の軸へのうで
        float px = pg.c.x, fd = 1.9f, R = 0.3f, L = 1.0f, PY = 0.5f;
        TBox(F, "PumpBed", new Vector3((px - 0.4f + px + fd - 0.1f) / 2f, -0.56f, 0f), new Vector3(fd + 0.3f, 0.1f, 1.1f), facWood, false);   // パイプが下りる所にかからないように、出口の手前まで
        TBox(F, "PumpArm", new Vector3((px - 0.4f) / 2f, -0.56f, 0f), new Vector3(px - 0.4f, 0.09f, 0.16f), cwAxle, false);
        var bear = GameObject.CreatePrimitive(PrimitiveType.Cylinder); bear.name = "PumpBearing"; Object.DestroyImmediate(bear.GetComponent<Collider>());
        bear.transform.SetParent(F, false); bear.transform.localPosition = new Vector3(0f, -0.56f, 0f); bear.transform.localScale = new Vector3(0.6f, 0.07f, 0.6f); bear.GetComponent<Renderer>().sharedMaterial = cwAxle;
        // クランク
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); disc.name = "CrankDisc"; Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.transform.SetParent(pin, false); disc.transform.localPosition = new Vector3(0f, 0.34f, 0f); disc.transform.localScale = new Vector3(2f * R + 0.24f, 0.04f, 2f * R + 0.24f); disc.GetComponent<Renderer>().sharedMaterial = cwBrass;
        var cp = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cp.name = "CrankPin"; Object.DestroyImmediate(cp.GetComponent<Collider>());
        cp.transform.SetParent(pin, false); cp.transform.localPosition = new Vector3(R, (0.38f + PY + 0.08f) / 2f, 0f); cp.transform.localScale = new Vector3(0.09f, (PY + 0.08f - 0.38f) / 2f, 0.09f); cp.GetComponent<Renderer>().sharedMaterial = cwAxle;
        var pc = pin.localPosition; var axisY = Vector3.up * PY;
        // ピストン（動く）と、ピストンの通るかご（動かない）
        var piston = new GameObject("Piston").transform; piston.SetParent(F, false);
        var head = GameObject.CreatePrimitive(PrimitiveType.Cylinder); head.name = "Head"; Object.DestroyImmediate(head.GetComponent<Collider>());
        head.transform.SetParent(piston, false); head.transform.localRotation = Quaternion.Euler(0f, 0f, 90f); head.transform.localPosition = new Vector3(0.12f, 0f, 0f); head.transform.localScale = new Vector3(0.62f, 0.12f, 0.62f); head.GetComponent<Renderer>().sharedMaterial = pipeM;
        var rod = new GameObject("PumpRod").transform; rod.SetParent(F, false);
        TBox(rod, "Bar", Vector3.zero, new Vector3(0.07f, 0.07f, L), cwAxle, false);
        // 2026-11-08 てじゃ「ポンプがただ押してるだけ」「ポンプのすぐ横に丸みのあるタンク、クリスタルつけて処理してそうな雰囲気」
        //  ピストンの先に丸いタンク（たての筒＋丸い上下）。上にクリスタル。ふくらみが中に入るとクリスタルがきらっと回る（FuwaSugarPipe.pop）
        var tc = pc + new Vector3(fd + 0.1f + 0.2f + TankR, -0.5f + TankR + TankH / 2f, 0f);   // 台の上に立つ
        var tank = new GameObject("SugarTank").transform; tank.SetParent(F, false); tank.localPosition = tc;
        var tankM = FuwaStage2WaterTest.ToonMat("Course2_4_SugarTank", new Color(0.93f, 0.94f, 0.97f));
        Transform needleT = null;
        {
            needleT = PumpLooks(F, pc + axisY, tank, pipeM, tankM);   // 2026-11-10 缶とピストンの筒（Blender）
            // のぞき窓（外がわ）
            var win = GameObject.CreatePrimitive(PrimitiveType.Cylinder); win.name = "TankWindow"; Object.DestroyImmediate(win.GetComponent<Collider>());
            win.transform.SetParent(tank, false); win.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); win.transform.localPosition = new Vector3(0f, 0.05f, -TankR + 0.03f); win.transform.localScale = new Vector3(0.36f, 0.04f, 0.36f); win.GetComponent<Renderer>().sharedMaterial = cwBrass;
            var glass = GameObject.CreatePrimitive(PrimitiveType.Cylinder); glass.name = "TankGlass"; Object.DestroyImmediate(glass.GetComponent<Collider>());
            glass.transform.SetParent(tank, false); glass.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); glass.transform.localPosition = new Vector3(0f, 0.05f, -TankR + 0.005f); glass.transform.localScale = new Vector3(0.26f, 0.04f, 0.26f);
            glass.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_3_Crystal.mat") ?? cwAxle;
            TBox(F, "TankBed", new Vector3(tc.x, -0.56f, 0f), new Vector3(2f * TankR + 0.2f, 0.1f, 2f * TankR + 0.2f), facWood, false);
            TBox(F, "TankBedJoin", new Vector3((px + fd - 0.1f + tc.x - TankR - 0.1f) / 2f + 0.01f, -0.56f, 0f), new Vector3(tc.x - TankR - 0.1f - (px + fd - 0.1f) - 0.02f, 0.06f, 0.5f), facWood, false);
        }
        Transform popT = CrystalTop(tank);
        // 動き
        var fc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaCloudFactory>(F.gameObject);
        fc.mode = 0; fc.frame = F; fc.bigGear = big; fc.pinion = pin; fc.ratio = pg.ratio; fc.phase = pg.phase * Mathf.Rad2Deg;
        fc.pinR = R; fc.pinY = PY; fc.rodLen = L;
        fc.axisDir = new[] { Vector3.right }; fc.plates = new[] { piston }; fc.rods = new[] { rod }; fc.fixedDist = new[] { fd };
        fc.rings = new Transform[0]; fc.ringOwner = new int[0]; fc.ringT = new float[0]; fc.skins = new Transform[0]; fc.puffs = new ParticleSystem[1];
        piston.localPosition = pc + axisY + new Vector3(L + R, 0f, 0f);
        { var pinP = pc + axisY + new Vector3(R, 0f, 0f); var sp = piston.localPosition; rod.localPosition = (pinP + sp) / 2f; rod.localRotation = Quaternion.LookRotation((sp - pinP).normalized, Vector3.up); }
        // パイプ：ポンプの出口 → 下へ（道の下の高さ）→ まん中へ → 鍋の底へ上がる
        Vector3 outW = F.TransformPoint(pc + axisY + new Vector3(fd + 0.1f, 0f, 0f));
        var pOut = root.InverseTransformPoint(outW);
        var pTank = root.InverseTransformPoint(F.TransformPoint(tc));
        var p0 = root.InverseTransformPoint(F.TransformPoint(tc + new Vector3(TankR + 0.05f, -TankH / 2f, 0f)));   // タンクの向こうがわの下から出る
        var dirC = new Vector3(cc.x - p0.x, 0f, cc.z - p0.z).normalized;   // まん中へ向かう向き
        // 歯車箱の大きい歯車より外で上がって、バルーンの首の輪の横へ。ぶつからない高さ・半径・向きを探す
        float topY = PipeInY, spoutR = CollarR1 + 0.02f;
        Vector3[] Route(float yR, float rr, float yaw)
        {
            var d = Quaternion.Euler(0f, yaw, 0f) * (-dirC);
            var q = cc + d * rr; var sp = cc + d * spoutR;
            var e0 = p0 + (p0 - new Vector3(pTank.x, p0.y, pTank.z)).normalized * 0.35f;
            return new[] { p0, e0, new Vector3(e0.x, yR, e0.z), new Vector3(q.x, yR, q.z), new Vector3(q.x, topY, q.z), new Vector3(sp.x, topY, sp.z) };
        }
        Vector3[] pts = null; int bestHits = int.MaxValue; float bestY = 0f;
        // 2026-11-08 てじゃ「パイプ、一回下に降りてるのなんで？」→ 歯車箱がなくなったので、まずはタンクの出口の高さのまま（下りない）まん中の近くまで行く道を探す。だめな時だけ道の下をくぐる
        foreach (float yR in new[] { p0.y, p0.y - 0.5f, p0.y + 0.5f, p0.y - 1f, -2.2f, -1.7f, -2.7f, -1.2f, -3.2f })
            foreach (float rr in new[] { 3.6f, 4.6f, 5.6f, 7.0f, 7.6f, 8.3f })
                foreach (float yaw in new[] { 0f, 12f, -12f, 24f, -24f })
                {
                    var cand = Route(yR, rr, yaw);
                    int h = PipeHits(cand, F);
                    if (h < bestHits) { bestHits = h; pts = cand; bestY = yR; }
                    if (h == 0) goto found;
                }
        found:
        { var dd = new List<Vector3> { pts[0] }; for (int i = 1; i < pts.Length; i++) if ((pts[i] - dd[dd.Count - 1]).sqrMagnitude > 1e-4f) dd.Add(pts[i]); pts = dd.ToArray(); }   // 長さ 0 の所（下りない時）を消す
        var pipeT = new GameObject("SugarPipe" + k).transform; pipeT.SetParent(holder, false);
        for (int i = 1; i < pts.Length; i++)
        {
            Vector3 a = pts[i - 1], b = pts[i];
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder); seg.name = "Pipe"; Object.DestroyImmediate(seg.GetComponent<Collider>());
            seg.transform.SetParent(pipeT, false); seg.transform.localPosition = (a + b) / 2f; seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            seg.transform.localScale = new Vector3(2f * PipeR, (b - a).magnitude / 2f, 2f * PipeR); seg.GetComponent<Renderer>().sharedMaterial = pipeM;
            if (i < pts.Length - 1)
            {
                var j = GameObject.CreatePrimitive(PrimitiveType.Sphere); j.name = "PipeElbow"; Object.DestroyImmediate(j.GetComponent<Collider>());
                j.transform.SetParent(pipeT, false); j.transform.localPosition = b; j.transform.localScale = Vector3.one * 2f * PipeR; j.GetComponent<Renderer>().sharedMaterial = pipeM;
            }
            var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder); band.name = "PipeBand"; Object.DestroyImmediate(band.GetComponent<Collider>());
            band.transform.SetParent(pipeT, false); band.transform.localPosition = Vector3.Lerp(a, b, 0.5f); band.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            band.transform.localScale = new Vector3(2f * PipeR + 0.1f, 0.06f, 2f * PipeR + 0.1f); band.GetComponent<Renderer>().sharedMaterial = cwBrass;
        }
        var inlet = GameObject.CreatePrimitive(PrimitiveType.Cylinder); inlet.name = "PipeInlet"; Object.DestroyImmediate(inlet.GetComponent<Collider>());
        inlet.transform.SetParent(pipeT, false); inlet.transform.localPosition = pts[pts.Length - 1]; inlet.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (pts[pts.Length - 1] - pts[pts.Length - 2]).normalized); inlet.transform.localScale = new Vector3(2f * PipeR + 0.2f, 0.12f, 2f * PipeR + 0.2f); inlet.GetComponent<Renderer>().sharedMaterial = cwBrass;
        // ざらめのふくらみ（使い回し 5 つ、はじめは隠す）
        var bulgeM = FuwaStage2WaterTest.ToonMat("Course2_4_SugarBulge", BulgeCol);
        var bulges = new Transform[5];
        for (int i = 0; i < bulges.Length; i++)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); b.name = "SugarBulge"; Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(pipeT, false); b.transform.localScale = new Vector3(2f * PipeR + 0.3f, 2f * PipeR + 0.3f, 2f * PipeR + 0.6f); b.GetComponent<Renderer>().sharedMaterial = bulgeM;
            b.gameObject.SetActive(false); bulges[i] = b.transform;
        }
        var sp2 = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaSugarPipe>(pipeT.gameObject);
        var full = new[] { pOut, pTank }.Concat(pts).ToArray();   // ポンプの出口 → タンクの中 → タンクの下から出て、まん中へ
        sp2.pts = full.Select(x => root.TransformPoint(x)).ToArray();
        sp2.pop = popT; sp2.popAt = Vector3.Distance(pOut, pTank); sp2.needle = needleT;
        sp2.glows = (popT ? popT.GetComponentsInChildren<Renderer>().Where(x => x.name != "GemHolder").ToArray() : new Renderer[0]).Concat(tank.GetComponentsInChildren<Renderer>().Where(x => x.name == "TankGlass")).ToArray(); sp2.bulges = bulges; sp2.speed = 4.5f; sp2.endCount = 12;
        sp2.endPuff = null; sp2.balloon = balloon;   // 着いたらバルーンがふくらんで、てっぺんから雲
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(sp2); FuwaStage2WaterTest.NoSync(sp2);
        fc.pipes = new[] { sp2 };
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fc); FuwaStage2WaterTest.NoSync(fc);
        float th; { var x = Quaternion.Inverse(F.rotation) * (big.rotation * Vector3.right); th = Mathf.Atan2(x.z, x.x) * Mathf.Rad2Deg; }
        pin.localRotation = Quaternion.Euler(0f, -Mathf.Repeat(fc.ratio * th + fc.phase, 360f), 0f);
        float pinRate = pg.ratio * rate;
        return g.name + " タンク " + tankHits + " 1回 " + (360f / Mathf.Max(0.01f, Mathf.Abs(pinRate))).ToString("F1") + "秒 パイプ高さ " + bestY.ToString("F1") + " ぶつかり " + bestHits;
    }

    // ===== 砂糖ポンプの見た目（2026-11-10 てじゃ「タンク周りのモデルのクオリティアップ」「スキマはあるしシリンダーは丸裸」「タンク自体もただのカプセル」）=====
    //  Blender（Tools/blender/stage2_4_sugar_tank.py）で作った缶（脚・帯とボルト・首のフランジ・液面計・圧力計・入口出口のフランジ）と、ピストンの筒（クランク側は開いている、のぞき窓、サドル）
    //  缶は SugarTank の local（まん中が原点）、筒はピストンの軸の上・ピンの所（pc+axisY）が原点。針は圧力計の文字盤の上（FuwaSugarPipe.needle がふくらみで振る）
    const string SugarModel = "Assets/FuwaStage2/Models/SugarTankV2.fbx";
    public static float SugarNeckTop = 0.95f;   // 缶の軸受けの上（クリスタルの台の下の面）
    public static Vector3 GaugeC = new Vector3(-0.2348f, 0.7646f, 0.4066f), GaugeN = new Vector3(-0.4719f, 0.3304f, 0.8174f);
    static Transform PumpLooks(Transform F, Vector3 axisP, Transform tank, Material pipeM, Material tankM)
    {
        var meshes = AssetDatabase.LoadAllAssetsAtPath(SugarModel).OfType<Mesh>().ToArray();
        Mesh M(string n) => meshes.FirstOrDefault(x => x.name == n);
        var glassM = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_4_TankGlass.mat");
        var levelM = FuwaStage2WaterTest.ToonMat("Course2_4_SugarLevel", new Color(0.62f, 0.82f, 1f));
        var faceM = FuwaStage2WaterTest.ToonMat("Course2_4_GaugeFace", new Color(0.98f, 0.97f, 0.93f));
        var barrel = new GameObject("PumpBarrel"); barrel.transform.SetParent(F, false); barrel.transform.localPosition = axisP;
        barrel.AddComponent<MeshFilter>().sharedMesh = M("PumpBarrel"); barrel.AddComponent<MeshRenderer>().sharedMaterials = SugarMats("PumpBarrel", tankM, glassM, levelM, faceM);
        var body = new GameObject("TankBody"); body.transform.SetParent(tank, false);
        body.AddComponent<MeshFilter>().sharedMesh = M("SugarTankV2"); body.AddComponent<MeshRenderer>().sharedMaterials = SugarMats("SugarTankV2", tankM, glassM, levelM, faceM);
        var nb = new GameObject("GaugeNeedle").transform; nb.SetParent(tank, false);
        nb.localPosition = GaugeC + GaugeN.normalized * 0.004f; nb.localRotation = Quaternion.LookRotation(-GaugeN.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, 45f);
        TBox(nb, "Needle", new Vector3(0f, 0.03f, 0f), new Vector3(0.012f, 0.07f, 0.004f), cwAxle, false);
        TBox(nb, "NeedleHub", Vector3.zero, new Vector3(0.024f, 0.024f, 0.006f), cwBrass, false);
        return nb;
    }

    // fbx の中の材質の名前（SugarTank_Paint など、使われている物だけ・サブメッシュの順）から、こちらの材質へ
    static Material[] SugarMats(string obj, Material paint, Material glass, Material level, Material face)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(SugarModel).GetComponentsInChildren<MeshRenderer>(true).First(x => x.name == obj).sharedMaterials;
        return src.Select(m =>
        {
            string n = m ? m.name : "";
            if (n.EndsWith("Brass")) return cwBrass; if (n.EndsWith("Glass")) return glass; if (n.EndsWith("Dark")) return cwAxle;
            if (n.EndsWith("Liquid")) return level; if (n.EndsWith("Face")) return face; return paint;
        }).ToArray();
    }

    // 缶のてっぺんのクリスタル（2026-11-10 てじゃ「上のクリスタルの置き方もちょっと雑だからきれいにしよ」）
    //  回る台（GemHolder、Blender）にまん中の大きい1本を3本のつめで、小さい3本を 60/180/300° に外へ 20° かたむけて輪で受ける
    //  TankCrystal（FuwaSugarPipe.pop が回してふくらませる）の原点は台の下の面＝缶の軸受けの上（SugarNeckTop）
    public static float GemSmallR = 0.225f, GemSmallTilt = 20f, GemBaseY = 0.035f;
    static Transform CrystalTop(Transform tank)
    {
        var crystalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_3_Crystal.mat");
        var cryMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaStage2/Meshes/Course2_3/ObsCrystal1_Main.asset");
        if (!cryMesh || !crystalMat) return null;
        float s0 = 0.75f / Mathf.Max(0.01f, cryMesh.bounds.size.y);
        var popT = new GameObject("TankCrystal").transform; popT.SetParent(tank, false); popT.localPosition = new Vector3(0f, SugarNeckTop, 0f);
        var hold = new GameObject("GemHolder"); hold.transform.SetParent(popT, false);
        hold.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAllAssetsAtPath(SugarModel).OfType<Mesh>().First(x => x.name == "GemHolder"); hold.AddComponent<MeshRenderer>().sharedMaterial = cwBrass;
        void Cry(string n, float sc, Vector3 basePos, Quaternion rot)
        {
            var c = new GameObject(n).transform; c.SetParent(popT, false);
            c.localRotation = rot; c.localScale = Vector3.one * sc;
            c.localPosition = basePos + rot * new Vector3(0f, -cryMesh.bounds.min.y * sc, 0f);
            c.gameObject.AddComponent<MeshFilter>().sharedMesh = cryMesh; c.gameObject.AddComponent<MeshRenderer>().sharedMaterial = crystalMat;
        }
        Cry("Crystal", s0, new Vector3(0f, GemBaseY, 0f), Quaternion.identity);
        int k = 0;
        foreach (float deg in new[] { 60f, 180f, 300f })
        {
            var d = new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0f, Mathf.Sin(deg * Mathf.Deg2Rad));
            var ax = (d * Mathf.Sin(GemSmallTilt * Mathf.Deg2Rad) + Vector3.up * Mathf.Cos(GemSmallTilt * Mathf.Deg2Rad)).normalized;
            Cry("CrystalSmall" + k++, s0 * 0.46f, d * GemSmallR + Vector3.up * GemBaseY, Quaternion.FromToRotation(Vector3.up, ax));
        }
        return popT;
    }

    // シーンの砂糖ポンプ4つの見た目だけ作り直す
    public static string RebuildSugarPumpLooks()
    {
        root = GameObject.Find(RootName).transform; CwMats();
        var pipeM = FuwaStage2WaterTest.ToonMat("Course2_4_SugarPipe", PipeCol);
        var tankM = FuwaStage2WaterTest.ToonMat("Course2_4_SugarTank", new Color(0.93f, 0.94f, 0.97f));
        int n = 0;
        foreach (var F in root.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("SugarPump") && x.GetComponent<FuwaCloudFactory>()).ToArray())
        {
            var tank = F.Find("SugarTank"); var pin = F.Cast<Transform>().FirstOrDefault(x => x.name.StartsWith("FactoryPinion"));
            if (!tank || !pin) continue;
            foreach (var x in F.Cast<Transform>().Concat(tank.Cast<Transform>()).Where(x => x.name == "CageRing" || x.name == "CageBar" || x.name == "CageEnd" || x.name == "TankStub" || x.name == "TankBody" || x.name == "TankBand" || x.name == "PumpBarrel" || x.name == "GaugeNeedle").ToArray())
                Object.DestroyImmediate(x.gameObject);
            var needle = PumpLooks(F, pin.localPosition + Vector3.up * 0.5f, tank, pipeM, tankM);
            foreach (var x in tank.Cast<Transform>().Where(x => x.name == "TankCrystal" || x.name == "CrystalMount").ToArray()) Object.DestroyImmediate(x.gameObject);
            var popT = CrystalTop(tank);
            var fc = F.GetComponent<FuwaCloudFactory>();
            if (fc.pipes != null) foreach (var sp in fc.pipes) if (sp)
            {
                sp.needle = needle; sp.pop = popT;
                sp.glows = (popT ? popT.GetComponentsInChildren<Renderer>().Where(x => x.name != "GemHolder").ToArray() : new Renderer[0]).Concat(tank.GetComponentsInChildren<Renderer>().Where(x => x.name == "TankGlass")).ToArray();
                UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(sp); EditorUtility.SetDirty(sp);
            }
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "ポンプ " + n;
    }

    // タンク（F の中の tcl が中心）とクリスタルのあたりが、ほかの物とぶつかる数（ポンプ自身・最後の歯車は数えない）
    static int TankHits(Transform F, Transform g, Vector3 tcl)
    {
        float sc = F.lossyScale.x, r = (TankR + 0.1f) * sc;
        Vector3 a = F.TransformPoint(tcl + Vector3.down * (TankH / 2f + TankR - 0.1f)) + Vector3.up * r, b = F.TransformPoint(tcl + Vector3.up * (TankH / 2f + TankR + 0.8f));
        int n = 0;
        foreach (var c in Physics.OverlapCapsule(a, b, r, ~0, QueryTriggerInteraction.Ignore)) if (!c.transform.IsChildOf(g) && !c.transform.IsChildOf(F)) n++;
        var bb = new Bounds(a, Vector3.zero); bb.Encapsulate(b); bb.Expand(2f * r);
        foreach (var rr in root.GetComponentsInChildren<Renderer>(true))
        {
            if (rr is ParticleSystemRenderer || rr.transform.IsChildOf(g) || rr.transform.IsChildOf(F) || rr.bounds.size.x > 9f || rr.bounds.size.z > 9f) continue;
            if (rr.bounds.Intersects(bb)) n++;
        }
        return n;
    }

    // パイプの道すじ（コースの座標）が、ほかの物（当たり・見た目の小さい物）とぶつかる数
    // ===== 回るタンクから砂糖パイプのタンクへの管（2026-11-10 てじゃ「せっかくだからどっかに流したかった」→ 上を通して気球へ →「ごちゃつく」
    //   →「緑とピンク、それぞれ別の白い管に合流」「なるべく下を通る」「白い管の根本のタンクにつなげばいい」）=====
    //  歯車の下の軸にロータリージョイント（回る軸に止まった輪の継手）。そこから道より下を通って、いちばん近い砂糖ポンプのタンク（白い管の根本）の横へ（2台は別のタンク）
    //  タンク1回転ごとに液の色のふくらみが流れて、砂糖タンク → 白い管 → 気球（着くと気球がふくらむ）
    public static float TankPipeR = 0.16f;
    static void TankPipes(Transform core, System.Text.StringBuilder log)
    {
        var holder = root.Find("Gimmicks");
        var pipeM = FuwaStage2WaterTest.ToonMat("Course2_4_SugarPipe", PipeCol);
        float R = TankPipeR;
        var sugar = root.GetComponentsInChildren<FuwaSugarPipe>(true).Where(x => x.name.StartsWith("SugarPipe")).ToList();
        var stanks = sugar.Select(sp => sp.pts.Length > 1 ? root.InverseTransformPoint(sp.pts[1]) : Vector3.zero).ToList();   // pts[1] がタンクのまん中
        var tanks = root.GetComponentsInChildren<Transform>(true).Where(x => x.name == "SpinTank").ToArray();
        // どのタンクをどの砂糖タンクへ：距離の合計がいちばん短い組み合わせ（1つの砂糖タンクに1本）
        var assign = new int[tanks.Length]; for (int i = 0; i < assign.Length; i++) assign[i] = -1;
        {
            float best = 1e9f; var cur = new int[tanks.Length]; var usedS = new bool[stanks.Count];
            float D(int a, int b) { var c0 = root.InverseTransformPoint(tanks[a].parent.position); return Vector2.Distance(new Vector2(c0.x, c0.z), new Vector2(stanks[b].x, stanks[b].z)); }
            void Rec(int i, float sum)
            {
                if (sum >= best) return;
                if (i == tanks.Length) { best = sum; System.Array.Copy(cur, assign, cur.Length); return; }
                for (int b = 0; b < stanks.Count; b++) if (!usedS[b]) { usedS[b] = true; cur[i] = b; Rec(i + 1, sum + D(i, b)); usedS[b] = false; }
                if (tanks.Length > stanks.Count) { cur[i] = -1; Rec(i + 1, sum + 1000f); }
            }
            Rec(0, 0f);
        }
        int tIdx = -1;
        int k = 0; var balloonC = root.GetComponentInChildren<FuwaCloudBalloon>(true);
        var bigs = root.GetComponentsInChildren<MeshFilter>(true).Where(x => x.name.StartsWith("FactoryBig") && x.sharedMesh).Select(x => (t: x.transform, y0: x.sharedMesh.bounds.min.y, y1: x.sharedMesh.bounds.max.y, r: x.sharedMesh.bounds.extents.x)).ToList();
        var others = new List<(Vector3 a, Vector3 b)>();
        foreach (var sp in sugar) for (int i = 1; i < sp.pts.Length; i++) others.Add((root.InverseTransformPoint(sp.pts[i - 1]), root.InverseTransformPoint(sp.pts[i])));
        float SegDist(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            float best = 1e9f;
            for (int i = 0; i <= 16; i++) { var p = Vector3.Lerp(p1, q1, i / 16f); var d2 = q2 - p2; float t = Mathf.Clamp01(Vector3.Dot(p - p2, d2) / Mathf.Max(1e-5f, d2.sqrMagnitude)); best = Mathf.Min(best, Vector3.Distance(p, p2 + d2 * t)); }
            return best;
        }
        foreach (var fix in tanks)
        {
            var frame = fix.parent; var gear = frame.GetChild(0);
            var liqR = fix.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == "TankLiquid");
            Color col = liqR && liqR.sharedMaterial ? liqR.sharedMaterial.GetColor("_Color") : Color.white;
            var sun = fix.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name.StartsWith("TankSun_z") && !x.name.EndsWith("_Frame"));
            int si = sun ? cwG.IndexOf(sun) : -1; float sunRate = si >= 0 ? Mathf.Abs(cwR[si]) * ClockSpeed : 60f;
            var c = root.InverseTransformPoint(frame.position);   // 歯車の下の面のまん中
            tIdx++; int ti = assign[tIdx];
            if (ti < 0) { log.Append(" | タンクの管: 砂糖タンクがない"); continue; }
            var st = stanks[ti];
            float sR = (TankR * PumpScale) + 0.02f;
            var toUs = new Vector3(c.x - st.x, 0f, c.z - st.z).normalized;
            Vector3[] Route(float y, int shape, float side)
            {
                var sideDir = Quaternion.Euler(0f, side, 0f) * toUs;
                var E = st + sideDir * sR; var Ein = st + sideDir * (sR + 0.5f);
                var J = new Vector3(c.x, y, c.z); var Eo = new Vector3(Ein.x, y, Ein.z);
                var l = new List<Vector3> { J };
                if (shape == 1) l.Add(new Vector3(Eo.x, y, J.z)); else if (shape == 2) l.Add(new Vector3(J.x, y, Eo.z));
                l.Add(Eo); if (Mathf.Abs(Ein.y - y) > 0.01f) l.Add(Ein); l.Add(E);
                return l.ToArray();
            }
            var stT = sugar[ti].transform.parent.GetComponentsInChildren<Transform>(true).Where(x => x.name == "SugarTank").OrderBy(x => Vector3.Distance(root.InverseTransformPoint(x.position), st)).FirstOrDefault();
            int Hits(Vector3[] cand)
            {
                var body = cand.Take(cand.Length - 1).ToArray();   // 最後（タンクにささる所）は数えない
                int n = PipeHits(body, frame, stT);
                for (int i = 1; i < body.Length; i++) foreach (var o in others) if (SegDist(body[i - 1], body[i], o.a, o.b) < PipeR + R + 0.15f) n++;
                // 砂糖ポンプの大きい歯車（FactoryBig、回る円板）は PipeHits が数えないので、円板の厚み±管の太さの中で半径の内を通ったら数える
                foreach (var fb in bigs)
                    for (int i = 1; i < body.Length; i++)
                        for (int q = 0; q <= 24; q++)
                        {
                            var lp = fb.t.InverseTransformPoint(root.TransformPoint(Vector3.Lerp(body[i - 1], body[i], q / 24f))); float pad = (R + 0.1f) / fb.t.lossyScale.y;
                            if (lp.y > fb.y0 - pad && lp.y < fb.y1 + pad && new Vector2(lp.x, lp.z).magnitude < fb.r + pad) { n++; break; }
                        }
                return n;
            }
            Vector3[] pts = null; int bestHits = int.MaxValue; float bestLen = 1e9f, bestY = 0f;
            foreach (float y in new[] { st.y, st.y - 0.5f, st.y - 1.0f, st.y + 0.3f, st.y - 1.6f, c.y - 0.6f, c.y - 1.2f, st.y - 2.2f, st.y - 2.8f }.Where(yy => yy <= c.y - 0.5f))   // ジョイントは歯車の下の面より 0.5 以上下
                foreach (int shape in new[] { 0, 1, 2 })
                    foreach (float side in new[] { 0f, 30f, -30f, 60f, -60f })
                    {
                        var cand = Route(y, shape, side);
                        int h = Hits(cand); float len = 0f; for (int i = 1; i < cand.Length; i++) len += Vector3.Distance(cand[i - 1], cand[i]);
                        if (h < bestHits || (h == bestHits && len < bestLen)) { bestHits = h; bestLen = len; pts = cand; bestY = y; }
                    }
            var pipeT = new GameObject("TankPipe" + k).transform; pipeT.SetParent(holder, false);
            for (int i = 1; i < pts.Length; i++)
            {
                Vector3 a = pts[i - 1], b = pts[i];
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cylinder); seg.name = "Pipe"; Object.DestroyImmediate(seg.GetComponent<Collider>());
                seg.transform.SetParent(pipeT, false); seg.transform.localPosition = (a + b) / 2f; seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
                seg.transform.localScale = new Vector3(2f * R, (b - a).magnitude / 2f, 2f * R); seg.GetComponent<Renderer>().sharedMaterial = pipeM;
                if (i < pts.Length - 1)
                {
                    var j = GameObject.CreatePrimitive(PrimitiveType.Sphere); j.name = "PipeElbow"; Object.DestroyImmediate(j.GetComponent<Collider>());
                    j.transform.SetParent(pipeT, false); j.transform.localPosition = b; j.transform.localScale = Vector3.one * 2f * R; j.GetComponent<Renderer>().sharedMaterial = pipeM;
                }
                if ((b - a).magnitude > 1.5f)
                {
                    var band = GameObject.CreatePrimitive(PrimitiveType.Cylinder); band.name = "PipeBand"; Object.DestroyImmediate(band.GetComponent<Collider>());
                    band.transform.SetParent(pipeT, false); band.transform.localPosition = Vector3.Lerp(a, b, 0.5f); band.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
                    band.transform.localScale = new Vector3(2f * R + 0.08f, 0.05f, 2f * R + 0.08f); band.GetComponent<Renderer>().sharedMaterial = cwBrass;
                }
            }
            // ロータリージョイント：歯車の下の軸（なければ短い軸を足す）に止まった輪
            {
                var shaft = gear.Find("Shaft");
                float jy = pts[0].y;
                if (!shaft)
                {
                    float top = 0f, bot = gear.InverseTransformPoint(root.TransformPoint(pts[0])).y - 0.25f;
                    var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder); s.name = "TankShaft"; Object.DestroyImmediate(s.GetComponent<Collider>());
                    s.transform.SetParent(gear, false); s.transform.localPosition = new Vector3(0f, (top + bot) / 2f - 0.01f, 0f); s.transform.localScale = new Vector3(0.3f, (top - bot) / 2f, 0.3f);
                    s.GetComponent<Renderer>().sharedMaterial = cwAxle;
                    var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cap.name = "TankShaftCap"; Object.DestroyImmediate(cap.GetComponent<Collider>());
                    cap.transform.SetParent(gear, false); cap.transform.localPosition = new Vector3(0f, bot + 0.02f, 0f); cap.transform.localScale = new Vector3(0.38f, 0.04f, 0.38f);
                    cap.GetComponent<Renderer>().sharedMaterial = cwBrass;
                }
                float sd = shaft ? shaft.localScale.x : 0.3f;
                var jt = GameObject.CreatePrimitive(PrimitiveType.Cylinder); jt.name = "RotaryJoint"; Object.DestroyImmediate(jt.GetComponent<Collider>());
                jt.transform.SetParent(pipeT, false); jt.transform.localPosition = pts[0]; jt.transform.localScale = new Vector3(sd + 0.26f, 0.2f, sd + 0.26f); jt.GetComponent<Renderer>().sharedMaterial = cwBrass;
                foreach (float dy in new[] { -0.22f, 0.22f })
                {
                    var r2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder); r2.name = "RotaryJointRing"; Object.DestroyImmediate(r2.GetComponent<Collider>());
                    r2.transform.SetParent(pipeT, false); r2.transform.localPosition = pts[0] + Vector3.up * dy; r2.transform.localScale = new Vector3(sd + 0.12f, 0.03f, sd + 0.12f); r2.GetComponent<Renderer>().sharedMaterial = cwAxle;
                }
            }
            var inlet = GameObject.CreatePrimitive(PrimitiveType.Cylinder); inlet.name = "PipeInlet"; Object.DestroyImmediate(inlet.GetComponent<Collider>());
            inlet.transform.SetParent(pipeT, false); inlet.transform.localPosition = Vector3.Lerp(pts[pts.Length - 2], pts[pts.Length - 1], 0.75f); inlet.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (pts[pts.Length - 1] - pts[pts.Length - 2]).normalized); inlet.transform.localScale = new Vector3(2f * R + 0.14f, 0.06f, 2f * R + 0.14f); inlet.GetComponent<Renderer>().sharedMaterial = cwBrass;
            // 液の色のふくらみ：この管 → 砂糖タンクのまん中 → 白い管の残り（気球まで）
            var bulgeM = FuwaStage2WaterTest.ToonMat("Course2_4_TankBulge" + k, Color.Lerp(col, Color.white, 0.15f));
            var bulges = new Transform[4];
            for (int i = 0; i < bulges.Length; i++)
            {
                var b = GameObject.CreatePrimitive(PrimitiveType.Sphere); b.name = "LiquidBulge"; Object.DestroyImmediate(b.GetComponent<Collider>());
                b.transform.SetParent(pipeT, false); b.transform.localScale = new Vector3(2f * R + 0.16f, 2f * R + 0.16f, 2f * R + 0.4f); b.GetComponent<Renderer>().sharedMaterial = bulgeM;
                b.gameObject.SetActive(false); bulges[i] = b.transform;
            }
            var full = pts.Select(x => root.TransformPoint(x)).Concat(sugar[ti].pts.Skip(1)).ToArray();
            var sp2 = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaSugarPipe>(pipeT.gameObject);
            sp2.pts = full; sp2.bulges = bulges; sp2.speed = 3.5f; sp2.endCount = 8; sp2.endPuff = null; sp2.balloon = balloonC; sp2.glows = new Renderer[0];
            sp2.autoInterval = 360f / Mathf.Max(1f, sunRate);   // タンク1回転ごと
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(sp2); FuwaStage2WaterTest.NoSync(sp2);
            for (int i = 1; i < pts.Length; i++) others.Add((pts[i - 1], pts[i]));
            log.Append(" | タンクの管" + k + " → " + sugar[ti].name + " 高さ " + bestY.ToString("F1") + " 長さ " + bestLen.ToString("F1") + " ぶつかり " + bestHits);
            k++;
        }
    }

    // シーンでタンクの管だけ作り直す
    public static string RebuildTankPipesOnly()
    {
        root = GameObject.Find(RootName).transform; CwMats();
        var cw = Object.FindObjectsOfType<FuwaClockwork>(true).FirstOrDefault();
        cwG = cw.gears.ToList(); cwR = cw.ratio.ToList(); cwP = cw.phase.ToList();
        foreach (var t in root.Find("Gimmicks").Cast<Transform>().Where(x => x.name.StartsWith("TankPipe")).ToArray()) Object.DestroyImmediate(t.gameObject);
        foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(x => x.name == "TankShaft" || x.name == "TankShaftCap").ToArray()) Object.DestroyImmediate(t.gameObject);
        var core = root.Find("MachineCore"); if (!core) return "MachineCore なし";
        var log = new System.Text.StringBuilder();
        TankPipes(core, log);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return log.ToString();
    }

    static int PipeHits(Vector3[] pts, Transform skip, Transform skip2 = null)
    {
        int n = 0;
        var rs = root.GetComponentsInChildren<Renderer>(true).Where(x => !(x is ParticleSystemRenderer) && !x.transform.IsChildOf(skip) && !(skip2 && x.transform.IsChildOf(skip2)) && !x.name.StartsWith("Factory") && x.name != "FlossPile" && x.bounds.size.x < 9f && x.bounds.size.z < 9f && !(x.transform.parent && x.transform.parent.name.StartsWith("SugarPipe"))).ToArray();
        for (int i = 1; i < pts.Length; i++)
        {
            Vector3 a = root.TransformPoint(pts[i - 1]), b = root.TransformPoint(pts[i]);
            foreach (var c in Physics.OverlapCapsule(a, b, PipeR + 0.1f, ~0, QueryTriggerInteraction.Ignore)) if (!(c.transform.parent && c.transform.parent.name == "CloudBalloon")) n++;
            var bb = new Bounds(a, Vector3.zero); bb.Encapsulate(b); bb.Expand(2f * PipeR);
            foreach (var r in rs)
            {
                if (!r.bounds.Intersects(bb)) continue;
                var c = r.bounds.ClosestPoint(Vector3.Lerp(a, b, 0.5f));
                float t = Mathf.Clamp01(Vector3.Dot(c - a, b - a) / Mathf.Max(1e-4f, (b - a).sqrMagnitude));
                if (Vector3.Distance(c, Vector3.Lerp(a, b, t)) < PipeR + 0.05f) n++;
            }
        }
        return n;
    }

    // 輪（半径 r0〜r1、高さ y0〜y1）
    static Mesh RingMesh(float r0, float r1, float y0, float y1, int n, string name)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        Vector3 P(float r, float y, int j) { float a = 2f * Mathf.PI * j / n; return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
        for (int j = 0; j < n; j++)
        {
            Vector3 o = P(1f, 0f, j) + P(1f, 0f, j + 1);
            Tri(v, t, P(r0, y1, j), P(r1, y1, j), P(r1, y1, j + 1), Vector3.up); Tri(v, t, P(r0, y1, j), P(r1, y1, j + 1), P(r0, y1, j + 1), Vector3.up);
            Tri(v, t, P(r0, y0, j), P(r1, y0, j), P(r1, y0, j + 1), Vector3.down); Tri(v, t, P(r0, y0, j), P(r1, y0, j + 1), P(r0, y0, j + 1), Vector3.down);
            Tri(v, t, P(r1, y0, j), P(r1, y1, j), P(r1, y1, j + 1), o); Tri(v, t, P(r1, y0, j), P(r1, y1, j + 1), P(r1, y0, j + 1), o);
            Tri(v, t, P(r0, y0, j), P(r0, y1, j), P(r0, y1, j + 1), -o); Tri(v, t, P(r0, y0, j), P(r0, y1, j + 1), P(r0, y0, j + 1), -o);
        }
        return SaveMesh(name, v, t);
    }

    // 回転体の鍋：prof（半径, 高さ）を内がわの面として、外がわは th だけ外下へ。ふちとまん中の穴のふちもふさぐ
    static Mesh BowlMesh(Vector2[] prof, float th, int n, string name)
    {
        var v = new List<Vector3>(); var t = new List<int>();
        int m = prof.Length;
        // 外がわの線：内がわの線を法線（外下）向きに th ずらす
        var outer = new Vector2[m];
        for (int i = 0; i < m; i++)
        {
            Vector2 tg = (i < m - 1 ? prof[i + 1] : prof[i]) - (i > 0 ? prof[i - 1] : prof[i]); tg.Normalize();
            Vector2 nr = new Vector2(tg.y, -tg.x);   // 内がわの面の外下
            outer[i] = prof[i] + nr * th;
        }
        void Surface(Vector2[] line, bool inner)
        {
            int b = v.Count;
            for (int j = 0; j <= n; j++)
            {
                float a = 2f * Mathf.PI * j / n;
                for (int i = 0; i < m; i++) v.Add(new Vector3(Mathf.Cos(a) * line[i].x, line[i].y, Mathf.Sin(a) * line[i].x));
            }
            for (int j = 0; j < n; j++) for (int i = 0; i < m - 1; i++)
            {
                int a0 = b + j * m + i, a1 = a0 + 1, b0 = a0 + m, b1 = b0 + 1;
                if (inner) { t.Add(a0); t.Add(b0); t.Add(a1); t.Add(a1); t.Add(b0); t.Add(b1); }
                else { t.Add(a0); t.Add(a1); t.Add(b0); t.Add(a1); t.Add(b1); t.Add(b0); }
            }
        }
        Surface(prof, true); Surface(outer, false);
        // ふち（いちばん外）とまん中の穴のふち：内と外をつなぐ帯
        foreach (int i in new[] { 0, m - 1 })
        {
            int b = v.Count;
            for (int j = 0; j <= n; j++)
            {
                float a = 2f * Mathf.PI * j / n;
                v.Add(new Vector3(Mathf.Cos(a) * prof[i].x, prof[i].y, Mathf.Sin(a) * prof[i].x));
                v.Add(new Vector3(Mathf.Cos(a) * outer[i].x, outer[i].y, Mathf.Sin(a) * outer[i].x));
            }
            for (int j = 0; j < n; j++)
            {
                int a0 = b + 2 * j, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
                if (i == 0) { t.Add(a0); t.Add(a1); t.Add(b0); t.Add(a1); t.Add(b1); t.Add(b0); }
                else { t.Add(a0); t.Add(b0); t.Add(a1); t.Add(a1); t.Add(b0); t.Add(b1); }
            }
        }
        return SaveMesh(name, v, t);
    }

    // 歯車に巻き込まれた時のめりこみ対策（2026-11-08 てじゃ「歯車に巻き込まれて潰される感じになると、めり込んでちょっと変」）
    //  FuwaGearGuard を塔に1つ。動く物（塔が回す歯車・乗る歯車・回転床とその歯車・せり出す橋・振り子のおもり）の当たりを movers に。
    //  人を運ぶ FuwaClockwork・FuwaGearRide は guard を通して運ぶ（行き先がふさがっていたら運ばない）
    static void GuardInstall(Transform tower)
    {
        var gd = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaGearGuard>(tower.gameObject);
        var movers = new List<Collider>(); var groups = new List<Transform>();   // groups：いっしょに動くまとまり（足もとと同じなら数えない）
        void AddUnder(Transform t) { if (!t) return; foreach (var c in t.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger && !movers.Contains(c)) { movers.Add(c); groups.Add(t); } }
        var cw = tower.GetComponent<FuwaClockwork>();
        foreach (var g in cw.gears) AddUnder(g);
        foreach (var gr in root.GetComponentsInChildren<FuwaGearRide>(true)) { foreach (var g in gr.gears) AddUnder(g); gr.guard = gd; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(gr); }
        foreach (var tt in root.GetComponentsInChildren<FuwaTurnTable>(true)) { AddUnder(tt.table); AddUnder(tt.rack); AddUnder(tt.rack2); if (tt.gears != null) foreach (var g in tt.gears) AddUnder(g); }
        AddUnder(pendPivot);
        gd.movers = movers.ToArray(); gd.moverGroup = groups.ToArray();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(gd); FuwaStage2WaterTest.NoSync(gd);
        cw.guard = gd; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(cw);
        Debug.Log("[2–4] めりこみ対策 動く物の当たり " + movers.Count);
    }

    // エディタ用：元の角度 a にする（FuwaClockwork と同じ計算）
    public static void ClockworkSet(FuwaClockwork cw, float a)
    {
        for (int i = 0; i < cw.gears.Length; i++) if (cw.gears[i]) cw.gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(cw.ratio[i] * a + cw.phase[i], 360f), 0f);
        for (int k = 0; k < cw.sliders.Length; k++)
        {
            var gg = cw.gears[cw.sliderGear[k]]; float py = cw.pinY[k]; var c = gg.TransformPoint(new Vector3(0f, py, 0f)); var pin = gg.TransformPoint(new Vector3(cw.pinR[k], py, 0f));
            var ax = gg.parent.TransformDirection(cw.sliderAxis[k]).normalized; var dd = pin - c; float al = Vector3.Dot(dd, ax); var side = dd - ax * al;
            var sp = c + ax * (al + Mathf.Sqrt(Mathf.Max(0f, cw.rodLen[k] * cw.rodLen[k] - side.sqrMagnitude)));
            cw.sliders[k].position = sp;
            if (cw.rods[k]) { cw.rods[k].position = (pin + sp) * 0.5f; cw.rods[k].rotation = Quaternion.LookRotation((sp - pin).normalized, gg.up); }
        }
    }

    // ===== 道そのものが歯車（2026-11-07 てじゃ：押し出し壁・アーム・シャッターは「だいぶ無理やり感」→「道そのものを歯車にする」「いいよ」）=====
    //  道の一部が、時計塔から動かされる歯車の足場。外側の小歯車 → たての軸が道の下へ → 道の下の横の軸（FeedY）→ 塔の下の段
    //  乗っている自分は FuwaClockwork が一緒に運ぶ。ふわふわは運ばない（歯車の上を押していく）
    const float PM = 0.2f;
    public static float FeedY = -5f;   // 道の下の横の軸の高さ（コースの y）
    static float RatioFor(float pitchSpeed, float r) => pitchSpeed / r * Mathf.Rad2Deg / ClockSpeed;   // ピッチ円が pitchSpeed m/s で回る ratio

    // 外側の小歯車（道の左＝外、2D 180°）とたての軸。たての軸の下（FeedY）でかさ歯車 → 塔へ
    //  walk：上に乗れる（当たり・道のレイヤー。FuwaClockwork が乗った人を運ぶ、めりこみ対策の movers にも入る）。2026-11-10 てじゃ「この歯車乗れないのが気になる」
    static Transform Drive(Transform g, FuwaGearTest.Gear from, int z, float mod, float layerY, float th, Material m, float dirDeg = 180f, bool walk = false)
    {
        var dg = FuwaGearTest.Mesh2(from, "Drive_z" + z, z, dirDeg, mod);
        var t = CwGear(g, g.name + "_Drive_z" + z, new Vector3(dg.c.x, layerY, dg.c.y), Vector3.up, SpurMesh(z, mod, th, "Clockwork_Spur_m" + Mathf.RoundToInt(mod * 100) + "_z" + z), m);
        if (walk) { t.gameObject.layer = 11; var mc = t.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = t.GetComponent<MeshFilter>().sharedMesh; }
        AddGear(t, dg.ratio, dg.phase * Mathf.Rad2Deg);
        float oy = FeedY - P.y;                    // gimmick の中の O の高さ
        CwShaft(t, oy + 1.2f - layerY, 0f, 0.5f); // A（O の上 1.2m）の上の面から小歯車の下の面まで
        feeds.Add(new Feed { O = root.InverseTransformPoint(g.TransformPoint(new Vector3(dg.c.x, oy, dg.c.y))), shaftGear = t, ratio = dg.ratio, phase = dg.phase * Mathf.Rad2Deg, below = true });
        return t;
    }

    // 窓のある歯車：外のふち（歯）と、まん中のこしき（rHub）の間に、wins の角度（中心, 半分の幅）だけ rHub〜rWin の窓（穴）
    //  cutSub：窓の断面（内がわの壁）を2つ目のサブメッシュに分ける（2026-11-08 てじゃ「断面をもっと暗く」）
    static Mesh WindowGear(int z, float mod, float h, float rHub, float rWin, (float c, float hw)[] wins, string name, int flank = 8, bool cutSub = false, float rBore = 0f)
    {
        var cutT = new List<int>();
        // 2026-11-07 軽くした：点を使い回す（前は三角形ごとに3点ずつで、88枚歯は11万点あった）
        //  ふちの内がわ（rWin）とこしき（rHub）の輪は、歯1枚あたり4点のあらい輪。歯の側面は、なめらかな所は点を共有して角だけ分ける
        var o = FuwaGearTest.GearOutline(z, flank, mod); int n = o.Length;
        float area = 0f; for (int i = 0; i < n; i++) { var p0 = o[i]; var p1 = o[(i + 1) % n]; area += p0.x * p1.y - p1.x * p0.y; }
        float ori = area > 0f ? 1f : -1f;   // 1：反時計回り（x→z）
        int K = Mathf.Max(1, n / (z * 4)); int M = (n + K - 1) / K;
        var dIn = new Vector2[M]; for (int k = 0; k < M; k++) dIn[k] = o[k * K].normalized;
        bool InWin(float a) { foreach (var w in wins) if (Mathf.Abs(Mathf.DeltaAngle(a, w.c)) < w.hw) return true; return false; }
        var segWin = new bool[M];
        for (int k = 0; k < M; k++) { var s = dIn[k] + dIn[(k + 1) % M]; segWin[k] = InWin(Mathf.Atan2(s.y, s.x) * Mathf.Rad2Deg); }
        var v = new List<Vector3>(); var t = new List<int>(); var cur = t;
        void T(int a, int b, int c, Vector3 want)
        {
            if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), want) < 0f) { int s = b; b = c; c = s; }
            cur.Add(a); cur.Add(b); cur.Add(c);
        }
        Vector3 P3(Vector2 d, float r, float y) => new Vector3(d.x * r, y, d.y * r);
        // 表と裏
        foreach (var y in new[] { h, 0f })
        {
            var want = y > 0f ? Vector3.up : Vector3.down;
            int bo = v.Count; for (int i = 0; i < n; i++) v.Add(new Vector3(o[i].x, y, o[i].y));
            int bw = v.Count; for (int k = 0; k < M; k++) v.Add(P3(dIn[k], rWin, y));
            int bh = v.Count; for (int k = 0; k < M; k++) v.Add(P3(dIn[k], rHub, y));
            int bc = v.Count; v.Add(new Vector3(0f, y, 0f));
            int bb = v.Count; if (rBore > 0f) for (int k = 0; k < M; k++) v.Add(P3(dIn[k], rBore, y));   // まん中の穴（2026-11-08 でかい歯車のまん中にクリスタルが浮く）
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n, gi = i / K, gj = j / K;
                T(bw + gi, bo + i, bo + j, want);
                if (gj != gi) T(bw + gi, bo + j, bw + gj, want);
            }
            for (int k = 0; k < M; k++)
            {
                int k2 = (k + 1) % M;
                if (rBore > 0f) { T(bb + k, bh + k, bh + k2, want); T(bb + k, bh + k2, bb + k2, want); }
                else T(bc, bh + k, bh + k2, want);
                if (!segWin[k]) { T(bh + k, bw + k, bw + k2, want); T(bh + k, bw + k2, bh + k2, want); }
            }
        }
        // 歯の側面（角は分ける）
        var sA = new int[n]; var sB = new int[n];
        for (int i = 0; i < n; i++)
        {
            var e0 = o[i] - o[(i - 1 + n) % n]; var e1 = o[(i + 1) % n] - o[i];
            bool sharp = Vector2.Angle(e0, e1) > 35f;
            sA[i] = v.Count; v.Add(new Vector3(o[i].x, 0f, o[i].y)); v.Add(new Vector3(o[i].x, h, o[i].y));
            if (sharp) { sB[i] = v.Count; v.Add(new Vector3(o[i].x, 0f, o[i].y)); v.Add(new Vector3(o[i].x, h, o[i].y)); } else sB[i] = sA[i];
        }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n; var e = o[j] - o[i]; var nw = new Vector3(e.y, 0f, -e.x) * ori;
            int a = sA[i], b = sB[j];
            T(a, b, b + 1, nw); T(a, b + 1, a + 1, nw);
        }
        if (rBore > 0f)   // まん中の穴の壁（内向き）
        {
            int b0 = v.Count; for (int k = 0; k <= M; k++) { v.Add(P3(dIn[k % M], rBore, 0f)); v.Add(P3(dIn[k % M], rBore, h)); }
            for (int k = 0; k < M; k++) { var s2 = dIn[k] + dIn[(k + 1) % M]; var nn = -new Vector3(s2.x, 0f, s2.y); int a = b0 + 2 * k; T(a, a + 2, a + 3, nn); T(a, a + 3, a + 1, nn); }
        }
        // 窓の内がわの壁（こしき側は外向き、ふち側は内向き）。窓の中では点を共有（丸くなめらか）
        if (cutSub) cur = cutT;
        foreach (var (r, sg) in new[] { (rHub, 1f), (rWin, -1f) })
        {
            var idx = new int[M];
            for (int k = 0; k < M; k++)
            {
                idx[k] = -1;
                if (!segWin[k] && !segWin[(k - 1 + M) % M]) continue;
                idx[k] = v.Count; v.Add(P3(dIn[k], r, 0f)); v.Add(P3(dIn[k], r, h));
            }
            for (int k = 0; k < M; k++)
            {
                if (!segWin[k]) continue;
                int k2 = (k + 1) % M; var s = dIn[k] + dIn[k2]; var nn = new Vector3(s.x, 0f, s.y) * sg;
                T(idx[k], idx[k2], idx[k2] + 1, nn); T(idx[k], idx[k2] + 1, idx[k] + 1, nn);
            }
        }
        // 窓のはしの壁（窓と窓でない所の境）。向きは窓の方
        for (int k = 0; k < M; k++)
        {
            bool after = segWin[k], before = segWin[(k - 1 + M) % M];
            if (after == before) continue;
            var tg = dIn[(k + 1) % M] - dIn[(k - 1 + M) % M]; var nn = new Vector3(tg.x, 0f, tg.y) * (after ? 1f : -1f);
            int b0 = v.Count; v.Add(P3(dIn[k], rHub, 0f)); v.Add(P3(dIn[k], rWin, 0f)); v.Add(P3(dIn[k], rWin, h)); v.Add(P3(dIn[k], rHub, h));
            T(b0, b0 + 1, b0 + 2, nn); T(b0, b0 + 2, b0 + 3, nn);
        }
        var mesh = SaveMesh(name, v, t);
        if (cutSub) { mesh.subMeshCount = 2; mesh.SetTriangles(t, 0); mesh.SetTriangles(cutT, 1); mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh); }
        else if (mesh.subMeshCount != 1) { mesh.subMeshCount = 1; mesh.SetTriangles(t, 0); EditorUtility.SetDirty(mesh); }
        return mesh;
    }

    // 窓（穴）のふちのしましま（2026-11-08 てじゃ「穴が見えづらい」）：WindowGear と同じ窓の形のふちの、穴でない側に黄色と黒の帯
    //  上の面から少し浮かせた薄い板（ふちの弧は +0.02、たての辺は +0.025 で、かどの重なりも同じ高さにならない）。当たりなし、歯車の子
    static void WindowLip(Transform gear, int z, float mod, float h, float rHub, float rWin, (float c, float hw)[] wins, string name, int flank = 8)
    {
        const float W = 0.2f, Seg = 0.3f, YArc = 0.02f, YRad = 0.025f;
        var o = FuwaGearTest.GearOutline(z, flank, mod); int n = o.Length;
        int K = Mathf.Max(1, n / (z * 4)); int M = (n + K - 1) / K;
        var dIn = new Vector2[M]; for (int k = 0; k < M; k++) dIn[k] = o[k * K].normalized;
        bool InWin(float a) { foreach (var w in wins) if (Mathf.Abs(Mathf.DeltaAngle(a, w.c)) < w.hw) return true; return false; }
        var segWin = new bool[M];
        for (int k = 0; k < M; k++) { var s = dIn[k] + dIn[(k + 1) % M]; segWin[k] = InWin(Mathf.Atan2(s.y, s.x) * Mathf.Rad2Deg); }
        var vv = new[] { new List<Vector3>(), new List<Vector3>() }; var tt = new[] { new List<int>(), new List<int>() };
        void Tri(int c, Vector3 a, Vector3 b, Vector3 d, Vector3 want)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, d - a), want) < 0f) { var s = b; b = d; d = s; }
            int i = vv[c].Count; vv[c].Add(a); vv[c].Add(b); vv[c].Add(d); tt[c].Add(i); tt[c].Add(i + 1); tt[c].Add(i + 2);
        }
        // 平面の四角 a b c d（どちら回りでも）を、h から h+y の薄い板に
        void Slab(int c, Vector2 a, Vector2 b, Vector2 cc, Vector2 d, float y)
        {
            var p = new[] { a, b, cc, d }; var m = (a + b + cc + d) / 4f;
            Vector3 T(Vector2 q, float yy) => new Vector3(q.x, yy, q.y);
            Tri(c, T(a, h + y), T(b, h + y), T(cc, h + y), Vector3.up); Tri(c, T(a, h + y), T(cc, h + y), T(d, h + y), Vector3.up);
            for (int e = 0; e < 4; e++)
            {
                var q0 = p[e]; var q1 = p[(e + 1) % 4]; var mid = (q0 + q1) / 2f - m; var nw = new Vector3(mid.x, 0f, mid.y);
                Tri(c, T(q0, h), T(q1, h), T(q1, h + y), nw); Tri(c, T(q0, h), T(q1, h + y), T(q0, h + y), nw);
            }
        }
        // 弧の帯（k0 から k1 の区間、半径 r0〜r1）。しま1本の長さ Seg ぶんの区間ごとに色をかえる
        void Arc(int k0, int k1, float r0, float r1)
        {
            float rm = (r0 + r1) / 2f, len = 0f;
            for (int k = k0; k <= k1; k++)
            {
                var a = dIn[k % M]; var b = dIn[(k + 1) % M];
                int c = Mathf.FloorToInt((len + (a - b).magnitude * rm * 0.5f) / Seg) % 2;
                len += (a - b).magnitude * rm;
                Slab(c, a * r0, b * r0, b * r1, a * r1, YArc);
            }
        }
        // たての辺の帯：向き d の線の、穴でない側（side）に幅 W。半径 rHub−W〜rWin+W（かどを覆う）
        void Radial(Vector2 d, Vector2 side)
        {
            float r0 = rHub - W, r1 = rWin + W; int ns = Mathf.Max(1, Mathf.RoundToInt((r1 - r0) / Seg));
            for (int s = 0; s < ns; s++)
            {
                float a0 = Mathf.Lerp(r0, r1, (float)s / ns), a1 = Mathf.Lerp(r0, r1, (float)(s + 1) / ns);
                Slab(s % 2, d * a0, d * a1, d * a1 + side * W, d * a0 + side * W, YRad);
            }
        }
        for (int s = 0; s < M; s++)
        {
            if (!segWin[s] || segWin[(s - 1 + M) % M]) continue;   // 窓のはじまり
            int e = s; while (segWin[(e + 1) % M] && e - s < M) e++;
            Arc(s, e, rWin, rWin + W);                // 外のふち（穴の外がわ）
            Arc(s, e, rHub - W, rHub);                // こしきのふち（穴の内がわ）
            Vector2 ds = dIn[s], de = dIn[(e + 1) % M];
            Vector2 ps = new Vector2(-ds.y, ds.x); if (Vector2.Dot(ps, dIn[(s - 1 + M) % M]) < 0f) ps = -ps;
            Vector2 pe = new Vector2(-de.y, de.x); if (Vector2.Dot(pe, dIn[(e + 2) % M]) < 0f) pe = -pe;
            Radial(ds, ps); Radial(de, pe);
        }
        var mats = new[] { FuwaGearTest.Mat("Course2_4_HazardYellow", new Color(1f, 0.8f, 0.12f)), FuwaGearTest.Mat("Course2_4_HazardBlack", new Color(0.13f, 0.13f, 0.15f)) };
        for (int c = 0; c < 2; c++)
        {
            var go = new GameObject(c == 0 ? "LipYellow" : "LipBlack"); go.transform.SetParent(gear, false);
            go.AddComponent<MeshFilter>().sharedMesh = SaveMesh(name + (c == 0 ? "_Y" : "_K"), vv[c], tt[c]);
            go.AddComponent<MeshRenderer>().sharedMaterial = mats[c];
        }
    }

    // ①→② 窓のある歯車3枚（24・18・24枚）。かみ合う所で次へ乗り移る。窓が道の上に来たら落ちる
    public static float HoleSpeed = 0.35f;
    static void HoleGears()
    {
        var g = GimmickRoot("HoleGears"); const float H = 0.5f;
        int[] zs = { 24, 18, 24 };
        var gs = new List<FuwaGearTest.Gear>();
        gs.Add(new FuwaGearTest.Gear { name = "HG0_z24", z = zs[0], c = new Vector2(0f, PM * zs[0] / 2f + PM + 0.05f), ratio = RatioFor(HoleSpeed, PM * zs[0] / 2f), phase = 0f });
        for (int i = 1; i < zs.Length; i++) gs.Add(FuwaGearTest.Mesh2(gs[i - 1], "HG" + i + "_z" + zs[i], zs[i], 90f, PM));
        // 2026-11-08 てじゃ「穴が見えづらい」：明るいグレーだと下の白い雲と溶ける → 歯車は濃い色、穴の断面（内がわの壁）はほぼ黒
        //  （穴のふちの黄色と黒のしましまは「ちょとくどい」で外した。WindowLip は残す）
        var darkM = FuwaGearTest.Mat("Course2_4_HoleGear", new Color(0.3f, 0.32f, 0.37f));
        var bronzeM = FuwaGearTest.Mat("Course2_4_HoleGearBronze", new Color(0.48f, 0.36f, 0.24f));
        var cutM = FuwaGearTest.Mat("Course2_4_HoleCut", new Color(0.07f, 0.07f, 0.09f));
        for (int i = 0; i < gs.Count; i++)
        {
            var q = gs[i]; float r = PM * q.z / 2f, rf = r - 1.25f * PM;
            var wins = i == 1 ? new[] { (0f, 50f), (180f, 50f) } : new[] { (30f, 45f), (210f, 45f) };
            var mesh = WindowGear(q.z, PM, H, 0.45f, rf - 0.45f, wins, "PathGear_Hole_z" + q.z + "_" + i, 8, true);
            var t = CwGear(g, q.name, new Vector3(q.c.x, -H, q.c.y), Vector3.up, mesh, i == 1 ? bronzeM : darkM, true);
            t.GetComponent<MeshRenderer>().sharedMaterials = new[] { i == 1 ? bronzeM : darkM, cutM };
            AddGear(t, q.ratio, q.phase * Mathf.Rad2Deg);
        }
        var drive = Drive(g, gs[2], 18, PM, -H, H, cwDark, 180f, true);   // 大きめ（でかい歯車からの速さ合わせ）。最後の歯車の外（最初の歯車の外はチェックポイントのきのこの丸太とぶつかる）
        SpinTank(drive, H, TankLiquidB);   // 2026-11-10 てじゃ「りょうほうに」：駆動歯車の上にもタンク（色ちがい）
        TankGear(g, gs[0], H);   // 2026-11-10 最初の歯車（チェックポイント側）の外に歯車を足して、上に遊星歯車で速く回るタンク
        var last = gs[gs.Count - 1];
        P += Dir(th) * (last.c.y + PM * last.z / 2f + PM + 0.05f);
    }

    // ===== 駆動歯車の上の、速く回るタンク（2026-11-10 てじゃ「この歯車の上あたりに、なんか速く回ってるタンクを作りたい」→ 遊星歯車の案「最初の案でとりあえず」）=====
    //  遊星歯車：外のリング（内歯）は動かない。腕（キャリア）＝駆動歯車そのもの（惑星3つの軸が駆動歯車に立つ）。まん中の太陽が速く回って、上のガラスのタンクを回す
    //  リングを止めて腕から回すと 太陽 = 腕 ×（1 + Zr/Zs）＝ 6倍。惑星の自転（腕から見て）= −(Zr/Zp)× 腕
    //  リングは上の冠（3本の柱）に固定、冠はまん中の動かない軸（タンクの中を通る）で下から支える
    //  かみ合わせ（角度0に歯の真ん中、2D 反時計回り）：惑星 k を腕の中の角度 120k に置き、惑星の歯を外（リング）へ向ける。Zp が偶数なので太陽にも歯が向く
    //   → 太陽はすき間を向ける：太陽 = ψk − 180/Zs、リングもすき間：リング = ψk − 180/Zr（Zs・Zr が 3 で割れるので3つともそろう）
    public static int STankZs = 9, STankZp = 18, STankZr = 45;
    public static float STankM = 0.05f, STankGearT = 0.15f, STankGearY = 0.08f, STankBacklash = 0.006f;
    public static float STankR = 0.72f, STankY0 = 0.6f, STankY1 = 1.8f, STankRingOut = 1.3f, STankCrownY = 2.1f;
    public static float LiqWall = 0.95f, LiqDip = 0.4f;   // 液：ふちの高さ（タンクの底から）、まん中のくぼみ
    // 最初の歯車（HG0、チェックポイント側）の外（道の左、2D 180°）にかみ合う18枚の歯車。乗れる。上に SpinTank
    //  2026-11-10 てじゃ「チェックポイント側の歯車も意味なさそうだし、横に歯車ふやしてこれのせようよ」（前は最後の駆動歯車の上に乗せていた）
    //  （前は HG0 の外はチェックポイントのきのこの丸太とぶつかったが、丸太は道の手前 −1.5 に移したので空いた）
    static Transform TankGear(Transform g, FuwaGearTest.Gear hg0, float H)
    {
        var q = FuwaGearTest.Mesh2(hg0, "HoleGears_TankGear_z18", 18, 180f, PM);
        var t = CwGear(g, q.name, new Vector3(q.c.x, -H, q.c.y), Vector3.up, SpurMesh(18, PM, H, "Clockwork_Spur_m" + Mathf.RoundToInt(PM * 100) + "_z18"), cwDark, true);
        AddGear(t, q.ratio, q.phase * Mathf.Rad2Deg);
        SpinTank(t, H, TankLiquidA);
        return t;
    }

    // シーンでタンクの歯車だけ作り直す（丸ごと作り直さずに試す用）。FuwaClockwork の配列とめりこみ対策の movers に足す
    // 段の歯車の回るタンク（ラベンダー）（2026-11-10）：駆動歯車の上だと上の段の歯車（SGB）にめりこむ（てじゃ「ラベンダーのほうめりこんでる」）
    //  → 上の段の薄いグレーの歯車（SGB、24枚）とかみ合う18枚の歯車を同じ段に足して、その上に（てじゃ「上の薄いグレーの歯車とからませたい」）
    //  向き StepTankDir：道の右の少し前（ほかの物と重ならない向きを調べた）
    public static float StepTankDir = 20f;
    static Transform StepTankGear(Transform g, FuwaGearTest.Gear sgb, float y0, float th)
    {
        var q = FuwaGearTest.Mesh2(sgb, "StepGears_TankGear_z18", 18, StepTankDir, PM);
        var t = CwGear(g, q.name, new Vector3(q.c.x, y0, q.c.y), Vector3.up, SpurMesh(18, PM, th, "Clockwork_Spur_m" + Mathf.RoundToInt(PM * 100) + "_z18_t" + Mathf.RoundToInt(th * 100)), cwDark);
        AddGear(t, q.ratio, q.phase * Mathf.Rad2Deg);
        SpinTank(t, th, TankLiquidD);
        return t;
    }

    // シーンで回るタンク（4台）と管を全部作り直す（丸ごと作り直さずに試す用）。FuwaClockwork の配列とめりこみ対策の movers も直す
    public static string RebuildSpinTankOnly()
    {
        root = GameObject.Find(RootName).transform;
        var g = GameObject.Find("HoleGears"); if (!g) return "HoleGears なし";
        var cw = Object.FindObjectsOfType<FuwaClockwork>(true).FirstOrDefault(); if (!cw) return "FuwaClockwork なし";
        CwMats();
        var keep = new List<int>();
        for (int i = 0; i < cw.gears.Length; i++) if (cw.gears[i] && !cw.gears[i].name.StartsWith("TankPlanet") && !cw.gears[i].name.StartsWith("TankSun") && !cw.gears[i].name.StartsWith("HoleGears_TankGear") && !cw.gears[i].name.StartsWith("StepGears_TankGear")) keep.Add(i);
        var hg0 = cw.gears.First(x => x && x.name == "HG0_z24");
        int i0 = System.Array.IndexOf(cw.gears, hg0);
        var gear0 = new FuwaGearTest.Gear { name = "HG0_z24", z = 24, c = new Vector2(hg0.parent.localPosition.x, hg0.parent.localPosition.z), ratio = cw.ratio[i0], phase = cw.phase[i0] * Mathf.Deg2Rad };
        cwG = keep.Select(i => cw.gears[i]).ToList(); cwR = keep.Select(i => cw.ratio[i]).ToList(); cwP = keep.Select(i => cw.phase[i]).ToList();
        foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(x => x && (x.name == "SpinTank" || x.name == "SpinTank_Carrier" || x.name == "HoleGears_TankGear_z18_Frame" || x.name == "StepGears_TankGear_z18_Frame" || x.name == "TankShaft" || x.name == "TankShaftCap" || (x.name.StartsWith("TankPipe") && x.parent && x.parent.name == "Gimmicks"))).ToArray())
            if (t) Object.DestroyImmediate(t.gameObject);
        var tg = TankGear(g.transform, gear0, 0.5f);
        SpinTank(cw.gears.First(x => x && x.name == "HoleGears_Drive_z18"), 0.5f, TankLiquidB);
        SpinTank(cw.gears.First(x => x && x.name == "ChunkyGears_Drive_z8"), 0.6f, TankLiquidC);
        {
            var sg = GameObject.Find("StepGears").transform; var sgbT = cw.gears.First(x => x && x.name == "SGB"); int bi = System.Array.IndexOf(cw.gears, sgbT);
            var sgb = new FuwaGearTest.Gear { name = "SGB_z24", z = 24, c = new Vector2(sgbT.parent.localPosition.x, sgbT.parent.localPosition.z), ratio = cw.ratio[bi], phase = cw.phase[bi] * Mathf.Deg2Rad };
            StepTankGear(sg, sgb, sgbT.parent.localPosition.y, 0.7f - 0.04f);
        }
        Undo.RecordObject(cw, "SpinTank");
        cw.gears = cwG.ToArray(); cw.ratio = cwR.ToArray(); cw.phase = cwP.ToArray();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(cw); EditorUtility.SetDirty(cw);
        var gd = cw.guard; string gs = "guard なし";
        if (gd)
        {
            var mv = gd.movers.ToList(); var grp = gd.moverGroup.ToList();
            for (int i = mv.Count - 1; i >= 0; i--) if (!mv[i]) { mv.RemoveAt(i); grp.RemoveAt(i); }
            foreach (var c in tg.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger && !mv.Contains(c)) { mv.Add(c); grp.Add(tg); }
            gd.movers = mv.ToArray(); gd.moverGroup = grp.ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(gd); EditorUtility.SetDirty(gd); gs = "movers " + mv.Count;
        }
        var log = new System.Text.StringBuilder();
        TankPipes(root.Find("MachineCore"), log);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(g.scene);
        FuwaCourse2_4Builder.ClockworkSet(cw, 0f);
        return "歯車 " + cw.gears.Length + " " + gs + log;
    }

    public static (Color, Color, string) TankLiquidA = (new Color(1f, 0.6f, 0.8f), new Color(0.85f, 0.32f, 0.6f), "Pink"), TankLiquidB = (new Color(0.55f, 0.95f, 0.85f), new Color(0.2f, 0.62f, 0.66f), "Mint"),
        TankLiquidC = (new Color(1f, 0.9f, 0.42f), new Color(0.88f, 0.62f, 0.16f), "Lemon"), TankLiquidD = (new Color(0.8f, 0.68f, 1f), new Color(0.5f, 0.38f, 0.85f), "Lavender");
    static void SpinTank(Transform drive, float th, (Color top, Color deep, string name) liq)
    {
        int gi = cwG.IndexOf(drive); float rc = cwR[gi], pc = cwP[gi];
        var fr = drive.parent;
        int Zs = STankZs, Zp = STankZp, Zr = STankZr; float m = STankM, GT = STankGearT, gy = STankGearY;
        float a = m * (Zs + Zp) / 2f, Rr = m * Zr / 2f, gTop = gy + GT;
        Mesh Spur(int z, string n) => SaveMesh2(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(z, 8, m, 0f, 0f, STankBacklash), GT, n, Vector2.zero));
        var glassM = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_4_TankGlass.mat");
        if (!glassM) { glassM = new Material(Shader.Find("FuwaCourse/TankGlass")); AssetDatabase.CreateAsset(glassM, "Assets/FuwaStage2/Materials/Course2_4_TankGlass.mat"); }
        string lp = "Assets/FuwaStage2/Materials/Course2_4_TankLiquid" + liq.name + ".mat";
        var liqM = AssetDatabase.LoadAssetAtPath<Material>(lp);
        if (!liqM) { liqM = new Material(Shader.Find("FuwaCourse/TankLiquid")); AssetDatabase.CreateAsset(liqM, lp); }
        liqM.SetColor("_Color", liq.top); liqM.SetColor("_Deep", liq.deep); EditorUtility.SetDirty(liqM);
        Transform Cyl(Transform p, string n, float r, float y0, float y1, Material mm, Vector3 at = default(Vector3))
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); c.name = n; Object.DestroyImmediate(c.GetComponent<Collider>());
            c.transform.SetParent(p, false); c.transform.localPosition = at + new Vector3(0f, (y0 + y1) / 2f, 0f); c.transform.localScale = new Vector3(2f * r, (y1 - y0) / 2f, 2f * r);
            c.GetComponent<Renderer>().sharedMaterial = mm; return c.transform;
        }
        Vector3 Dir2(float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0f, Mathf.Sin(deg * Mathf.Deg2Rad));

        // 動かない物（駆動歯車の frame の下、y=0 が駆動歯車の上の面）
        var fix = new GameObject("SpinTank").transform; fix.SetParent(fr, false); fix.localPosition = new Vector3(0f, th, 0f);
        // 腕（駆動歯車と一緒に回る）
        var car = new GameObject("SpinTank_Carrier").transform; car.SetParent(drive, false); car.localPosition = new Vector3(0f, th, 0f);

        // 惑星3つ
        var pMesh = Spur(Zp, "Tank_Planet_z" + Zp);
        for (int k = 0; k < 3; k++)
        {
            float phi = 120f * k; var pp = Dir2(phi) * a;
            Cyl(car, "PlanetPin" + k, 0.045f, 0f, gTop + 0.1f, cwAxle, pp);
            Cyl(car, "PlanetSpacer" + k, 0.1f, -0.01f, gy, cwBrass, pp);
            var pg = CwGear(car, "TankPlanet" + k, pp + Vector3.up * gy, Vector3.up, pMesh, cwBrass);
            AddGear(pg, -(float)Zr / Zp * rc, phi);
            Cyl(pg, "PlanetCap", 0.07f, GT - 0.01f, GT + 0.05f, cwLight);
        }
        // 太陽（6倍で回る）とタンク
        var sun = CwGear(fix, "TankSun_z" + Zs, Vector3.up * gy, Vector3.up, Spur(Zs, "Tank_Sun_z" + Zs), cwLight);
        AddGear(sun, rc * (1f + (float)Zr / Zs), pc - 180f / Zs);
        {
            float o = -gy;   // 太陽の中の y = fix の y − gy
            Cyl(sun, "SunShaft", 0.13f, GT - 0.01f, 0.5f + o, cwLight);
            Cyl(sun, "TankBase", STankR + 0.06f, 0.5f + o, STankY0 + o, cwLight);
            var gl = new GameObject("TankGlass"); gl.transform.SetParent(sun, false); gl.transform.localPosition = new Vector3(0f, o, 0f);
            gl.AddComponent<MeshFilter>().sharedMesh = LatheMesh(new[] { new Vector2(STankR, STankY0 - 0.01f), new Vector2(STankR, STankY1 + 0.01f) }, 0f, 40, "Tank_Glass");
            gl.AddComponent<MeshRenderer>().sharedMaterial = glassM;
            foreach (var (b0, b1) in new[] { (STankY0, STankY0 + 0.08f), (STankY1 - 0.08f, STankY1) })
            {
                var bd = new GameObject("TankBand"); bd.transform.SetParent(sun, false); bd.transform.localPosition = new Vector3(0f, o, 0f);
                bd.AddComponent<MeshFilter>().sharedMesh = RingMesh(STankR - 0.02f, STankR + 0.04f, b0, b1, 40, "Tank_Band" + (b0 < 1f ? "0" : "1")); bd.AddComponent<MeshRenderer>().sharedMaterial = cwBrass;
            }
            for (int k = 0; k < 3; k++)   // たての細い帯（回っているのが見える）
            {
                var rb = TBox(sun, "TankRib" + k, Dir2(120f * k + 60f) * (STankR + 0.015f) + new Vector3(0f, (STankY0 + STankY1) / 2f + o, 0f), new Vector3(0.04f, STankY1 - STankY0 - 0.12f, 0.06f), cwBrass, false);
                rb.localRotation = Quaternion.Euler(0f, -(120f * k + 60f), 0f);
            }
            Cyl(sun, "TankTop", STankR + 0.06f, STankY1 + o, STankY1 + 0.1f + o, glassM);   // ふたもガラス（2026-11-10 上から液のうずが見えるように）
            Cyl(sun, "TankTopHub", 0.1f, STankY1 + 0.1f + o, STankCrownY - 0.02f + o, cwBrass);
            // 中：回る液。回っている液は上の面がすり鉢（放物面）になる。ふちの高さ LiqWall、まん中は LiqDip だけ下がる
            {
                var prof = new List<Vector2>(); float R = STankR - 0.015f, y0 = STankY0 + 0.005f, yw = STankY0 + LiqWall;
                prof.Add(new Vector2(0.001f, y0)); prof.Add(new Vector2(R, y0)); prof.Add(new Vector2(R, yw));
                for (int i = 1; i <= 12; i++) { float u = 1f - i / 12f; prof.Add(new Vector2(Mathf.Max(0.001f, R * u), yw - LiqDip * (1f - u * u))); }
                var lq = new GameObject("TankLiquid"); lq.transform.SetParent(sun, false); lq.transform.localPosition = new Vector3(0f, o, 0f);
                lq.AddComponent<MeshFilter>().sharedMesh = LatheMesh(prof.ToArray(), 0f, 40, "Tank_Liquid"); lq.AddComponent<MeshRenderer>().sharedMaterial = liqM;
            }
        }
        // 動かない軸（太陽・タンクの中を通って冠を支える）
        Cyl(fix, "FixedAxle", 0.035f, gy + 0.02f, STankCrownY + 0.12f, cwAxle);
        // リング（内歯、動かない）
        {
            var o2 = FuwaGearTest.GearOutline(Zr, 8, m, 0f, 0f, STankBacklash);
            var v = new List<Vector3>(); var t = new List<int>(); int n = o2.Length;
            var inr = new Vector3[n]; var outr = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float rr = o2[i].magnitude, an = Mathf.Atan2(o2[i].y, o2[i].x); float r2 = 2f * Rr - rr;   // 外歯の歯を内がわへ折り返す（歯先 Rr+m → Rr−m）
                inr[i] = new Vector3(Mathf.Cos(an) * r2, 0f, Mathf.Sin(an) * r2); outr[i] = new Vector3(Mathf.Cos(an) * STankRingOut, 0f, Mathf.Sin(an) * STankRingOut);
            }
            Vector3 U = Vector3.up * GT;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n; Vector3 mid = (inr[i] + inr[j]) * 0.5f; mid.y = 0f; Vector3 rad = mid.normalized;
                Tri(v, t, inr[i] + U, outr[j] + U, outr[i] + U, Vector3.up); Tri(v, t, inr[i] + U, inr[j] + U, outr[j] + U, Vector3.up);
                Tri(v, t, inr[i], outr[j], outr[i], Vector3.down); Tri(v, t, inr[i], inr[j], outr[j], Vector3.down);
                Vector3 e = inr[j] - inr[i]; Vector3 nIn = Vector3.Cross(Vector3.up, e).normalized; if (Vector3.Dot(nIn, rad) > 0f) nIn = -nIn;   // 歯の面の向き（中へ）
                Tri(v, t, inr[i], inr[i] + U, inr[j] + U, nIn); Tri(v, t, inr[i], inr[j] + U, inr[j], nIn);
                Tri(v, t, outr[i], outr[i] + U, outr[j] + U, rad); Tri(v, t, outr[i], outr[j] + U, outr[j], rad);
            }
            var ring = new GameObject("TankRing_z" + Zr); ring.transform.SetParent(fix, false); ring.transform.localPosition = Vector3.up * gy;
            ring.transform.localRotation = Quaternion.Euler(0f, -(pc - 180f / Zr), 0f);
            ring.AddComponent<MeshFilter>().sharedMesh = SaveMesh("Tank_Ring_z" + Zr, v, t); ring.AddComponent<MeshRenderer>().sharedMaterial = cwLight;
        }
        // 冠と3本の柱（リングを上からつる）
        {
            float pr = (Rr + m * 1.25f + STankRingOut) / 2f;
            var cr = new GameObject("TankCrown"); cr.transform.SetParent(fix, false);
            cr.AddComponent<MeshFilter>().sharedMesh = RingMesh(STankRingOut - 0.16f, STankRingOut, STankCrownY, STankCrownY + 0.1f, 40, "Tank_Crown"); cr.AddComponent<MeshRenderer>().sharedMaterial = cwDark;
            Cyl(fix, "CrownHub", 0.15f, STankCrownY - 0.04f, STankCrownY + 0.16f, cwBrass);
            for (int k = 0; k < 3; k++)
            {
                float ang = 120f * k + 60f; var d = Dir2(ang);
                var sp = TBox(fix, "CrownSpoke" + k, d * ((0.14f + STankRingOut - 0.08f) / 2f) + Vector3.up * (STankCrownY + 0.05f), new Vector3(STankRingOut - 0.08f - 0.14f, 0.07f, 0.1f), cwDark, false);
                sp.localRotation = Quaternion.Euler(0f, -ang, 0f);
                Cyl(fix, "RingPost" + k, 0.04f, gTop - 0.02f, STankCrownY + 0.02f, cwDark, d * pr);
                Cyl(fix, "RingPostFoot" + k, 0.075f, gTop - 0.01f, gTop + 0.05f, cwBrass, d * pr);
            }
        }
        // 当たり：まとめて1つの筒（動かない）。駆動歯車に運ばれてぶつかったら FuwaGearGuard が止める
        {
            var v = new List<Vector3>(); var t = new List<int>(); const int N = 16; float R = STankRingOut + 0.02f, Y = STankCrownY + 0.16f;
            for (int i = 0; i < N; i++) { var d = Dir2(360f * i / N) * R; v.Add(d + Vector3.up * 0.02f); v.Add(d + Vector3.up * Y); }
            for (int i = 0; i < N; i++) { int j = (i + 1) % N; t.Add(2 * i); t.Add(2 * i + 1); t.Add(2 * j + 1); t.Add(2 * i); t.Add(2 * j + 1); t.Add(2 * j); }
            for (int i = 1; i < N - 1; i++) { t.Add(0); t.Add(2 * i); t.Add(2 * i + 2); t.Add(1); t.Add(2 * i + 3); t.Add(2 * i + 1); }
            var bl = new GameObject("SpinTank_Block"); bl.transform.SetParent(fix, false);
            var mc = bl.AddComponent<MeshCollider>(); mc.sharedMesh = SaveMesh("Tank_Block", v, t); mc.convex = true;
        }
    }

    // ②→③ ごつい歯車2枚（8枚歯、歯の大きさ 0.8）：入る所・乗り移る所・出る所で、歯が来ている時だけ足場がつながる
    public static float ChunkSpeed = 0.3f;
    static void ChunkyGears()
    {
        var g = GimmickRoot("ChunkyGears"); const float H = 0.6f, CM = 0.8f; const int Z = 8;
        float r = CM * Z / 2f, ra = r + CM;
        var a = new FuwaGearTest.Gear { name = "CG0_z8", z = Z, c = new Vector2(0f, ra + 0.05f), ratio = RatioFor(ChunkSpeed, r), phase = 0f };
        var b = FuwaGearTest.Mesh2(a, "CG1_z8", Z, 90f, CM);
        var mesh = SaveMesh2(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(Z, 10, CM), H, "PathGear_Chunky_z8", Vector2.zero));
        foreach (var q in new[] { a, b })
        {
            var t = CwGear(g, q.name, new Vector3(q.c.x, -H, q.c.y), Vector3.up, mesh, q == a ? cwLight : cwBrass, true);
            AddGear(t, q.ratio, q.phase * Mathf.Rad2Deg);
        }
        var drv = Drive(g, a, 8, CM, -H, H, cwDark);
        SpinTank(drv, H, TankLiquidC);   // 2026-11-10 てじゃ「残り2個にもこんなかんじに」：駆動歯車の上に回るタンク（レモン）
        P += Dir(th) * (b.c.y + ra + 0.05f);
    }

    // ③→④ 段つき歯車2つで 1.4m 上がる：A（大24・上に小14 が +0.7）→ B（大24 が +0.7 の層で A の小とかみ合う、上に小14 が +1.4）
    public static float StepSpeed = 0.22f;
    static void StepGears()
    {
        var g = GimmickRoot("StepGears"); const float S = 0.7f, Cl = 0.04f;
        var aB = new FuwaGearTest.Gear { name = "SGA_z24", z = 24, c = new Vector2(0f, PM * 12 + PM + 0.05f), ratio = RatioFor(StepSpeed, PM * 12), phase = 0f };
        var aS = new FuwaGearTest.Gear { name = "SGA_z14", z = 14, c = aB.c, ratio = aB.ratio, phase = 0.3f };
        var bB = FuwaGearTest.Mesh2(aS, "SGB_z24", 24, 90f, PM);
        var bS = new FuwaGearTest.Gear { name = "SGB_z14", z = 14, c = bB.c, ratio = bB.ratio, phase = bB.phase + 0.2f };
        var m24 = SaveMesh2(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(24, 8, PM), S - Cl, "PathGear_Step_z24", Vector2.zero));
        var m24b = SaveMesh2(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(24, 8, PM), S, "PathGear_Step_z24b", Vector2.zero));
        var m14 = SaveMesh2(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(14, 8, PM), S - Cl, "PathGear_Step_z14", Vector2.zero));
        // A：大は −0.7〜0、小は 0.04〜0.7。B：大は 0.04〜0.7、小は 0.74〜1.4
        var tA = CwGear(g, "SGA", new Vector3(aB.c.x, -S, aB.c.y), Vector3.up, m24b, cwLight, true);
        AddGear(tA, aB.ratio, aB.phase * Mathf.Rad2Deg);
        Child(tA, "Small_z14", new Vector3(0f, S + Cl, 0f), m14, cwBrass, aS.phase - aB.phase);
        var tB = CwGear(g, "SGB", new Vector3(bB.c.x, Cl, bB.c.y), Vector3.up, m24, cwLight, true);
        AddGear(tB, bB.ratio, bB.phase * Mathf.Rad2Deg);
        Child(tB, "Small_z14", new Vector3(0f, S, 0f), m14, cwBrass, bS.phase - bB.phase);
        var drv = Drive(g, aB, 12, PM, -S, S, cwDark, 140f);   // 少し前より（真横はチェックポイントのきのこの丸太とぶつかる）
        StepTankGear(g, bB, Cl, S - Cl);
        P += Dir(th) * (bB.c.y + PM * 7 + PM + 0.05f) + Vector3.up * 2f * S;
    }
    // 同じ軸で一緒に回る段（歯の向きのずれ dphase ラジアン）
    static void Child(Transform gear, string name, Vector3 lp, Mesh m, Material mat, float dphase)
    {
        var c = new GameObject(name); c.transform.SetParent(gear, false); c.transform.localPosition = lp; c.transform.localRotation = Quaternion.Euler(0f, -dphase * Mathf.Rad2Deg, 0f);
        c.layer = 11; c.AddComponent<MeshFilter>().sharedMesh = m; c.AddComponent<MeshRenderer>().sharedMaterial = mat; c.AddComponent<MeshCollider>().sharedMesh = m;
    }

    // ---- 2–3 と同じ部品 ----
    //  hw：判定の半分の幅（床が道より広い所で使う。0 なら道の幅）
    //  mushZ：きのこの前後（判定の線から先へ+）。すぐ先に回る歯車がある所は手前へ（2026-11-10 てじゃ「跳ね橋の次のチェックポイント、歯車にうまってる」「その一個前のも」）
    static void CP(string name, float along, float hw = 0f, float mushZ = 1.5f)
    {
        if (hw <= 0f) hw = HW;
        var tmpl = GameObject.Find("FuwaCourse3/Checkpoint1"); if (!tmpl) return;
        var g = root.Find("Checkpoints"); if (!g) { g = new GameObject("Checkpoints").transform; g.SetParent(root, false); }
        var cp = Object.Instantiate(tmpl, g, false); cp.name = name;
        cp.transform.localPosition = P + Dir(th) * along; cp.transform.localRotation = Quaternion.LookRotation(Dir(th));
        foreach (var n in new[] { "Post_L", "Post_R", "Bar" }) { var c = cp.transform.Find(n); if (c) Object.DestroyImmediate(c.gameObject); }
        var trig = cp.transform.Find("Trigger");
        if (trig) trig.localScale = new Vector3(hw * 2f + 0.8f, trig.localScale.y, trig.localScale.z);
        var gm = cp.transform.Find("GlowMushrooms");
        if (gm) foreach (Transform cl in gm) cl.localPosition = new Vector3(Mathf.Sign(cl.localPosition.x) * (hw + 0.55f), cl.localPosition.y, mushZ);
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

    static float StartSet(Transform r)
    {
        var start = new GameObject("Start").transform; start.SetParent(r, false);
        var src = GameObject.Find("FuwaCourse/Start")?.transform;
        if (!src) { Debug.LogWarning("[2–4] FuwaCourse/Start がない"); return 3f; }
        var map = new Dictionary<Object, Object>();
        FuwaCourse2_1Builder.CopyChildren(src, start, null, map);
        foreach (var t in start.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("_Course1")) t.name = t.name.Replace("_Course1", "_Course2_4");
        float islandTop = src.Find("StartMushroom").localPosition.y;
        var area = start.Find("StartArea"); var ground = area.Find("Ground");
        start.localPosition = new Vector3(-ground.localPosition.x, -islandTop, -ground.localPosition.z);
        // コース1のスタートは +Z（ロビーから外）向き。2–4 は道が +Z へ出る
        FuwaCourse2_1Builder.FixCopied(start, map);
        FuwaCourse2_1Builder.Stage2Grass(start);
        return ground.localScale.x * 0.5f;
    }

    static Vector3 GoalSet(Transform r, Vector3 p, Vector3 dir)
    {
        var goal = new GameObject("Goal").transform; goal.SetParent(r, false);
        var src = GameObject.Find("FuwaCourse/Goal")?.transform;
        if (!src) { Debug.LogWarning("[2–4] FuwaCourse/Goal がない"); return p; }
        var map = new Dictionary<Object, Object>();
        FuwaCourse2_1Builder.CopyChildren(src, goal, null, map);
        var gg = goal.Find("GoalGround");
        float far = gg ? gg.localScale.x * 0.5f : 6.1f;
        goal.localPosition = p + dir * far + Vector3.up * 0.02f;
        goal.localRotation = Quaternion.LookRotation(dir);
        IslandJoin(pathT, "GoalJoin", p + dir * far, -dir, far, HW, pathB);
        FuwaCourse2_1Builder.FixCopied(goal, map);
        FuwaCourse2_1Builder.Stage2Grass(goal);
        return p + dir * far;
    }

    // 丸い島と道のつなぎ（2–3 と同じ）：島のまん中 c から dir の向きに、島のふち（半径 R）から c から R の直線までのすき間をうめる
    static void IslandJoin(Transform parent, string name, Vector3 c, Vector3 dir, float R, float hw, Material mat)
    {
        const float Tk = 0.6f; int nx = 16;
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = c; g.transform.localRotation = Quaternion.LookRotation(dir); g.layer = pathLayer;
        var v = new List<Vector3>(); var t = new List<int>();
        float Zb(float x) => Mathf.Sqrt(Mathf.Max(0f, R * R - x * x));
        for (int i = 0; i < nx; i++)
        {
            float x0 = Mathf.Lerp(-hw, hw, i / (float)nx), x1 = Mathf.Lerp(-hw, hw, (i + 1) / (float)nx);
            Vector3 a = new Vector3(x0, 0, Zb(x0)), b = new Vector3(x1, 0, Zb(x1)), cc = new Vector3(x1, 0, R), d = new Vector3(x0, 0, R), dn = Vector3.down * Tk;
            Tri(v, t, a, b, cc, Vector3.up); Tri(v, t, a, cc, d, Vector3.up);
            Tri(v, t, a + dn, cc + dn, b + dn, Vector3.down); Tri(v, t, a + dn, d + dn, cc + dn, Vector3.down);
        }
        foreach (float sx in new[] { -1f, 1f })
        {
            Vector3 a = new Vector3(sx * hw, 0, Zb(hw)), b = new Vector3(sx * hw, 0, R), dn = Vector3.down * Tk;
            Tri(v, t, a, b, b + dn, new Vector3(sx, 0, 0)); Tri(v, t, a, b + dn, a + dn, new Vector3(sx, 0, 0));
        }
        var m = SaveMesh(name, v, t);
        g.AddComponent<MeshFilter>().sharedMesh = m; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        g.AddComponent<MeshCollider>().sharedMesh = m;
    }

    static void Hazard(Transform r, string name, Vector3 lp, Vector3 size, bool player)
    {
        var g = new GameObject(name); g.transform.SetParent(r, false); g.transform.localPosition = lp; g.layer = player ? 0 : 4;
        var bc = g.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size;
        if (player) { var pc = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(g); pc.ball = ballProxy; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(pc); FuwaStage2WaterTest.NoSync(pc); }
        else { var hz = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaHazard>(g); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(hz); FuwaStage2WaterTest.NoSync(hz); }
    }

    static Material PathMat(string name, Color c)
    {
        string path = "Assets/FuwaStage2/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_1_PathA.mat"); m = new Material(src); AssetDatabase.CreateAsset(m, path); }
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
}
