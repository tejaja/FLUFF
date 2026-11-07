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
    static readonly string[] TestOnly = { "BallSpot", "BallStand", "Spawn", "BackSpot", "Warp", "Extras", "Mill", "MA_", "MB_", "MC_", "Axle_MA", "Axle_MB", "Axle_MC", "CatchFloor", "Step" };   // 受け床と階段：2026-11-07 てじゃ「テストようで床がのこったまま、階段もいらん」

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
        Place(Ride, log); CP("Checkpoint1", 1f); S(2f);
        T(-90f); S(2f);
        Place(Draw, log); CP("Checkpoint2", 1f); S(2f); S(8f, 3f);
        T(-90f); S(2f);
        Place(Rack, log); CP("Checkpoint3", 1f); S(2f);
        T(90f); S(6f, 3f); T(-90f); S(2f);
        Place(Twin, log); CP("Checkpoint4", 1f); S(2f);
        // 塔へ（まん中）：①の上をまたいで
        T(-120f); S(8f, 3f); S(10f);   // 2026-11-07 2枚の回転床を大きくした（すき間 3.2m）ので塔の位置を合わせ直し
        // 塔のてっぺんのゴール
        var goalAt = GoalSet(root, P, Dir(th));
        Tower(goalAt);

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
    static void S(float len, float rise = 0f)
    {
        var d = Dir(th) * len + Vector3.up * rise;
        Strip(pathT, "Road" + (segNo++) + (rise != 0f ? "_Slope" : ""), P, d, HW);
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
    static void Strip(Transform parent, string name, Vector3 a, Vector3 d, float hw)
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
        var u = Box(g, "Under", new Vector3(0, -Tk - 0.5f, len * 0.5f), new Vector3(hw * 2f - 0.2f, 1.0f, len - 0.1f), underMat, 0);
        Object.DestroyImmediate(u.GetComponent<Collider>());
    }

    // 塔：ゴールの島の下にのびる柱（見た目だけ。島より 0.3m 下で終わる＝島の面と重ならない）
    static void Tower(Vector3 goalAt)
    {
        var gg = root.Find("Goal/GoalGround");
        float r = gg ? gg.lossyScale.x * 0.5f * 0.6f : 4f;   // 島より細く（島へつなぐ道が塔の上にかからない、乗る歯車とも 1m 以上はなす）
        float topY = goalAt.y - 0.3f, botY = -24f;
        var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder); t.name = "Tower"; t.transform.SetParent(root, false);
        Object.DestroyImmediate(t.GetComponent<Collider>());
        t.transform.localPosition = new Vector3(goalAt.x, (topY + botY) / 2f, goalAt.z);
        t.transform.localScale = new Vector3(2f * r, (topY - botY) / 2f, 2f * r);
        t.GetComponent<Renderer>().sharedMaterial = towerMat;
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
