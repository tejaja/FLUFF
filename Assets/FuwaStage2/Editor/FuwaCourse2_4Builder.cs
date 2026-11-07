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

    [MenuItem("FuwaCourse/ステージ2/2–4 はぐるまのとう（ブロックアウト）を作り直す")]
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
        Place(Ride, log); CP("Checkpoint1", 1f); S(1f); HoleGears(); S(1f);   // ①→②：窓のある歯車（2026-11-07「道そのものが歯車」。前は押し出し壁）
        T(-90f); S(2f);
        Place(Draw, log); CP("Checkpoint2", 1f); S(1f); ChunkyGears(); S(1f); S(8f, 3f);   // ②→③：ごつい歯車（前は回るアーム）
        T(-90f); S(2f);
        Place(Rack, log); CP("Checkpoint3", 1f); S(1f); StepGears(); S(1.5f, 0f, false); T(90f); S(6f, 1.6f, false); T(-90f); S(2f);   // ③→④：段つき歯車で 1.4m 上がる（前はシャッター）。出たすぐの道は下に B の大きい歯車があるので下の石なし
        Place(Twin, log); CP("Checkpoint4", 1f); S(2f);
        // 塔へ（まん中）：①の上をまたいで
        T(-120f); S(8f, 3f); S(14f);   // 2026-11-07 2枚の回転床を大きくした（すき間 3.2m）ので塔の位置を合わせ直し
        // 時計塔のトンネルを突っ切って、抜けた先にゴール（2026-11-07 てじゃ「最後は塔を突っ切った先がゴール」）
        ClockTower(P + Dir(th) * (TowerW / 2f), th);
        TowerDriveRide(log);
        ClockworkFeeds(root.Find("ClockTower")); ClockworkInstall(root.Find("ClockTower"));
        S(TowerW); S(1f);
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
        FuwaStage2LobbyCourses.Run();
        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        return "[2–4] " + log + " ゴール " + goalAt.ToString("F1") + " 範囲 " + lb.min.ToString("F0") + "〜" + lb.max.ToString("F0");
    }

    static float OutFloorY()
    {
        // 乗る歯車の受け床より下
        var cf = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CatchFloor");
        return cf ? root.InverseTransformPoint(cf.position).y - 1f : -4f;
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
        if (!under) return;
        var u = Box(g, "Under", new Vector3(0, -Tk - 0.5f, len * 0.5f), new Vector3(hw * 2f - 0.2f, 1.0f, len - 0.1f), underMat, 0);
        Object.DestroyImmediate(u.GetComponent<Collider>());
    }

    // 時計塔（2026-11-07 てじゃ「まんなかの塔、塔っぽく」「でかい時計塔にして、最後は塔を突っ切った先がゴール」）
    //  c＝トンネルの床のまん中（道の上の面の高さ）、道の向き th。四角い塔（TowerW 角）、道の高さに道の幅のトンネル、上に4面の時計ととんがり屋根
    //  下は雲の下までのびる。トンネルの床は道（S）で作るので、塔の下の段は道の下の石（−1.6m）より下で終わる
    public static float TowerW = 7f, TunnelHW = 2.2f, TunnelH = 4f, TowerTop = 16f, RoofH = 7f, ClockY = 11.5f, ClockR = 2.6f, TowerBottom = -30f;
    public static float PendOut = 3f, PendPivotY = 8.6f, PendBobY = 1.6f, PendBobR = 0.8f, PendAmp = 28f;
    static Transform pendPivot, escape;
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
        TBox(t, "Base", new Vector3(0f, (TowerBottom + baseTop) / 2f, 0f), new Vector3(TowerW, baseTop - TowerBottom, TowerW), stone, true);
        foreach (int sg in new[] { -1, 1 })
            TBox(t, "TunnelWall" + (sg > 0 ? "R" : "L"), new Vector3(sg * (h + TunnelHW) / 2f, (baseTop + TunnelH) / 2f, 0f), new Vector3(h - TunnelHW, TunnelH - baseTop, TowerW), stone, true);
        TBox(t, "Upper", new Vector3(0f, (TunnelH + TowerTop) / 2f, 0f), new Vector3(TowerW, TowerTop - TunnelH, TowerW), stone, true);
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
        //  入口（local −z の面）から腕を PendOut m 出して、その先に振り子の軸（アンクルの軸）。おもりは道の上 PendBobY。ゆれは道を横切る向き（local Z まわり）
        //  軸の下にがんぎ車（30枚）。塔の中から軸がのびて回している。振り子が1回ゆれきるたびに半歯カチッ
        {
            float pz = -(h + PendOut), py = PendPivotY, len = py - PendBobY;
            foreach (int sx in new[] { -1, 1 })
            {
                TBox(t, "PendArm" + sx, new Vector3(sx * 0.45f, py + 0.35f, -(h + PendOut / 2f)), new Vector3(0.22f, 0.3f, PendOut + 0.4f), trim, false);
                var br = TBox(t, "PendBrace" + sx, new Vector3(sx * 0.45f, py - 0.6f, -(h + PendOut * 0.3f)), new Vector3(0.16f, 0.16f, PendOut * 0.75f), trim, false);
                br.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            }
            TBox(t, "PendHead", new Vector3(0f, py + 0.35f, pz), new Vector3(1.3f, 0.3f, 0.5f), trim, false);
            // がんぎ車（local Y＝塔の面に直角）とその軸（塔の面まで）
            var ewF = new GameObject("EscapeWheel_Frame").transform; ewF.SetParent(t, false);
            ewF.localPosition = new Vector3(0f, py - 0.95f, pz + 0.15f); ewF.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
            var ew = new GameObject("EscapeWheel_z30").transform; ew.SetParent(ewF, false);
            ew.gameObject.AddComponent<MeshFilter>().sharedMesh = SaveMesh2(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(30, 6, 0.05f), 0.08f, "ClockTower_EscapeWheel_z30", Vector2.zero));
            ew.gameObject.AddComponent<MeshRenderer>().sharedMaterial = brassM;
            var ax = TBox(t, "EscapeAxle", new Vector3(0f, py - 0.95f, (pz + 0.15f - h) / 2f), new Vector3(0.1f, 0.1f, PendOut - 0.15f), handM, false);
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
        var ride = root.Find("Sections/S1_GearRide"); var tower = root.Find("ClockTower");
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
            CwShaft(g1T, oy + 1.2f - yl, -0.5f, 0.2f);
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
                ax.transform.SetParent(t, false); ax.transform.localPosition = new Vector3(0f, -1.5f, 0f); ax.transform.localScale = new Vector3(0.3f, 1.55f, 0.3f);
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
        var s = GameObject.CreatePrimitive(PrimitiveType.Cylinder); s.name = "Shaft"; Object.DestroyImmediate(s.GetComponent<Collider>());
        s.transform.SetParent(g, false); s.transform.localPosition = new Vector3(0f, (y0 + y1) / 2f, 0f); s.transform.localScale = new Vector3(d, (y1 - y0) / 2f, d);
        s.GetComponent<Renderer>().sharedMaterial = cwAxle;
        var l = GameObject.CreatePrimitive(PrimitiveType.Cube); l.name = "ShaftLine"; Object.DestroyImmediate(l.GetComponent<Collider>());
        l.transform.SetParent(g, false); l.transform.localPosition = new Vector3(d / 2f, (y0 + y1) / 2f, 0f); l.transform.localScale = new Vector3(0.03f, y1 - y0, 0.05f);
        l.GetComponent<Renderer>().sharedMaterial = cwBrass;
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

    // 塔から各しかけへ：てっぺん O でかさ歯車（A：たての軸、O の下／B：空中の軸、塔の方）。軸は塔の壁の中まで
    static void ClockworkFeeds(Transform tower)
    {
        var tc = root.InverseTransformPoint(tower.position);
        int n = 0;
        foreach (var f in feeds)
        {
            var d = new Vector3(tc.x - f.O.x, 0f, tc.z - f.O.z); float dist = d.magnitude; d /= dist;
            // たての軸の歯車（local Y＝上）の θ と、O の下の A（local Y＝下）：θA = −θ
            Transform gA; float rB, pB;
            var gB = f.below ? BevelPair(root.Find("Gimmicks"), "Feed" + n, f.O, Vector3.up, d, BevZ, BevZ, f.ratio, f.phase, out gA, out rB, out pB)
                             : BevelPair(root.Find("Gimmicks"), "Feed" + n, f.O, Vector3.down, d, BevZ, BevZ, -f.ratio, -f.phase, out gA, out rB, out pB);
            CwShaft(gB, 0f, dist - 1.2f - 1.0f, 0.2f);   // 塔のまん中から 1m 手前まで（塔の中）
            n++;
        }
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
    static void Drive(Transform g, FuwaGearTest.Gear from, int z, float mod, float layerY, float th, Material m)
    {
        var dg = FuwaGearTest.Mesh2(from, "Drive_z" + z, z, 180f, mod);
        var t = CwGear(g, g.name + "_Drive_z" + z, new Vector3(dg.c.x, layerY, dg.c.y), Vector3.up, SpurMesh(z, mod, th, "Clockwork_Spur_m" + Mathf.RoundToInt(mod * 100) + "_z" + z), m);
        AddGear(t, dg.ratio, dg.phase * Mathf.Rad2Deg);
        float oy = FeedY - P.y;                    // gimmick の中の O の高さ
        CwShaft(t, oy + 1.2f - layerY, 0f, 0.18f); // A（O の上 1.2m）の上の面から小歯車の下の面まで
        feeds.Add(new Feed { O = root.InverseTransformPoint(g.TransformPoint(new Vector3(dg.c.x, oy, dg.c.y))), shaftGear = t, ratio = dg.ratio, phase = dg.phase * Mathf.Rad2Deg, below = true });
    }

    // 窓のある歯車：外のふち（歯）と、まん中のこしき（rHub）の間に、wins の角度（中心, 半分の幅）だけ rHub〜rWin の窓（穴）
    static Mesh WindowGear(int z, float mod, float h, float rHub, float rWin, (float c, float hw)[] wins, string name)
    {
        var o = FuwaGearTest.GearOutline(z, 8, mod); int n = o.Length;
        var v = new List<Vector3>(); var t = new List<int>();
        bool InWin(float a) { foreach (var w in wins) if (Mathf.Abs(Mathf.DeltaAngle(a, w.c)) < w.hw) return true; return false; }
        Vector3 P3(Vector2 d, float r, float y) => new Vector3(d.x * r, y, d.y * r);
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector2 di = o[i].normalized, dj = o[j].normalized;
            float am = Mathf.Atan2((di + dj).y, (di + dj).x) * Mathf.Rad2Deg;
            bool win = InWin(am);
            foreach (var y in new[] { h, 0f })
            {
                var want = y > 0f ? Vector3.up : Vector3.down;
                // ふち（rWin〜歯）
                Tri(v, t, P3(di, rWin, y), new Vector3(o[i].x, y, o[i].y), new Vector3(o[j].x, y, o[j].y), want);
                Tri(v, t, P3(di, rWin, y), new Vector3(o[j].x, y, o[j].y), P3(dj, rWin, y), want);
                // こしき（0〜rHub）
                Tri(v, t, new Vector3(0f, y, 0f), P3(di, rHub, y), P3(dj, rHub, y), want);
                if (!win) { Tri(v, t, P3(di, rHub, y), P3(di, rWin, y), P3(dj, rWin, y), want); Tri(v, t, P3(di, rHub, y), P3(dj, rWin, y), P3(dj, rHub, y), want); }
            }
            // 歯の側面
            var a0 = new Vector3(o[i].x, 0f, o[i].y); var b0 = new Vector3(o[j].x, 0f, o[j].y); var up = Vector3.up * h;
            var nrm = new Vector3((o[i] + o[j]).x, 0f, (o[i] + o[j]).y);
            Tri(v, t, a0, b0, b0 + up, nrm); Tri(v, t, a0, b0 + up, a0 + up, nrm);
            if (win)
            {
                // 窓の内側の壁（こしき側は外向き、ふち側は内向き）
                foreach (var (r, s) in new[] { (rHub, 1f), (rWin, -1f) })
                {
                    var p0 = P3(di, r, 0f); var p1 = P3(dj, r, 0f); var nn = new Vector3((di + dj).x, 0f, (di + dj).y) * s;
                    Tri(v, t, p0, p1, p1 + up, nn); Tri(v, t, p0, p1 + up, p0 + up, nn);
                }
            }
            // 窓のはしの壁（窓と窓でない所の境）
            int k = (i + 1) % n; Vector2 dk = o[(i + 2) % n].normalized;
            float an = Mathf.Atan2((dj + dk).y, (dj + dk).x) * Mathf.Rad2Deg;
            if (InWin(an) != win)
            {
                var q0 = P3(dj, rHub, 0f); var q1 = P3(dj, rWin, 0f); var tang = new Vector3(-dj.y, 0f, dj.x) * (win ? -1f : 1f);
                Tri(v, t, q0, q1, q1 + up, tang); Tri(v, t, q0, q1 + up, q0 + up, tang);
            }
        }
        return SaveMesh(name, v, t);
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
        var wood = cwLight;
        for (int i = 0; i < gs.Count; i++)
        {
            var q = gs[i]; float r = PM * q.z / 2f, rf = r - 1.25f * PM;
            var wins = i == 1 ? new[] { (0f, 50f), (180f, 50f) } : new[] { (30f, 45f), (210f, 45f) };
            var mesh = WindowGear(q.z, PM, H, 0.45f, rf - 0.45f, wins, "PathGear_Hole_z" + q.z + "_" + i);
            var t = CwGear(g, q.name, new Vector3(q.c.x, -H, q.c.y), Vector3.up, mesh, i == 1 ? cwBrass : wood, true);
            AddGear(t, q.ratio, q.phase * Mathf.Rad2Deg);
        }
        Drive(g, gs[0], 12, PM, -H, H, cwDark);
        var last = gs[gs.Count - 1];
        P += Dir(th) * (last.c.y + PM * last.z / 2f + PM + 0.05f);
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
        Drive(g, a, 8, CM, -H, H, cwDark);
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
        Drive(g, aB, 12, PM, -S, S, cwDark);
        P += Dir(th) * (bB.c.y + PM * 7 + PM + 0.05f) + Vector3.up * 2f * S;
    }
    // 同じ軸で一緒に回る段（歯の向きのずれ dphase ラジアン）
    static void Child(Transform gear, string name, Vector3 lp, Mesh m, Material mat, float dphase)
    {
        var c = new GameObject(name); c.transform.SetParent(gear, false); c.transform.localPosition = lp; c.transform.localRotation = Quaternion.Euler(0f, -dphase * Mathf.Rad2Deg, 0f);
        c.layer = 11; c.AddComponent<MeshFilter>().sharedMesh = m; c.AddComponent<MeshRenderer>().sharedMaterial = mat; c.AddComponent<MeshCollider>().sharedMesh = m;
    }

    // ---- 2–3 と同じ部品 ----
    static void CP(string name, float along)
    {
        var tmpl = GameObject.Find("FuwaCourse3/Checkpoint1"); if (!tmpl) return;
        var g = root.Find("Checkpoints"); if (!g) { g = new GameObject("Checkpoints").transform; g.SetParent(root, false); }
        var cp = Object.Instantiate(tmpl, g, false); cp.name = name;
        cp.transform.localPosition = P + Dir(th) * along; cp.transform.localRotation = Quaternion.LookRotation(Dir(th));
        foreach (var n in new[] { "Post_L", "Post_R", "Bar" }) { var c = cp.transform.Find(n); if (c) Object.DestroyImmediate(c.gameObject); }
        var trig = cp.transform.Find("Trigger");
        if (trig) trig.localScale = new Vector3(HW * 2f + 0.8f, trig.localScale.y, trig.localScale.z);
        var gm = cp.transform.Find("GlowMushrooms");
        if (gm) foreach (Transform cl in gm) cl.localPosition = new Vector3(Mathf.Sign(cl.localPosition.x) * (HW + 0.55f), cl.localPosition.y, 1.5f);
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
