using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// ステージ2 コース2–5「そらのいせき」ブロックアウト（2026-11-11 てじゃと流れを決めた）
//  ① スタート小島 → ② はじめてのポータル（屋根つきの回廊のつきあたりのポータル → 上の島の部屋）CP1
//  → ③ ちいさくなる門（青い輪 → 鉄格子。人は門の横のクモの巣をくぐる → 白い輪）
//  → ④ でかくなる（オレンジの輪 → 強い横風の細い橋 → 吸いこむ渦のあいだ → 白い輪）CP2
//  → ⑤ 切りかえ（オレンジ → クモの巣の壁（でかいと破れる）→ 鳥（でかいと持てない）→ 白 → 追い風で島から島へ（でかいと落ちる））CP3
//  → ⑥ ポータル＋切りかえ（青 → 小さい窓のポータル（ふわふわ）・クモの巣の扉のポータル（人）→ 上の部屋 → オレンジ → 横風の上り坂）CP4 → ⑦ ゴール
//  考え方（てじゃ）：でかい＝ギミックに強いけど重い、小さい・ふつう＝風に乗れる。地面に着いたらアウトはいつもどおり
//  道はシマシマの若草色（てじゃ「あえて緑」）、遺跡はテラコッタ
//  道の作り方は 2–4 と同じ：P＝いまの道のはし（コースの座標、上の面の高さ）、th＝進む向き（上から見て x→z の反時計回り、度）
//  ロビーの Slot4 の方向に放射状。+Z がロビーから外へ。y=0 がスタートの島の地面
public static class FuwaCourse2_5Builder
{
    const string RootName = "FuwaCourse2_5";
    const float SpokeR = 34.8f, StartBelow = 4f;
    public static float HW = 2f, StripeLen = 1.2f;
    public static Color PathColA = new Color(0.651f, 0.859f, 0.494f), PathColB = new Color(0.549f, 0.796f, 0.40f);   // #A6DB7E / #8CCB66
    const string MeshDir = "Assets/FuwaStage2/Meshes/Course2_5";
    const float WT = 0.5f;   // 部屋の壁の厚み

    static Material pathA, pathB, stone, terra, terraD, moss, glow, maskM, iron, underM;
    static int pathLayer = 11;
    static FuwaBall ballT;
    static float ballR;
    static List<FuwaCheckpoint> cps = new List<FuwaCheckpoint>();
    static Transform root, pathT, secT, viewT;
    static Vector3 P; static float th;
    static int segNo, stencilNo;
    static System.Text.StringBuilder log;

    [MenuItem("FuwaCourse/ステージ2/2–5 そらのいせき（ブロックアウト）を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var lobby = GameObject.Find("FuwaStage2/Lobby"); if (!lobby) return "ステージ2のロビーがない";
        var old = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
        if (old) Object.DestroyImmediate(old);
        root = new GameObject(RootName).transform;
        var lc = lobby.transform.Find("LobbySpawn").position; var slot = lobby.transform.Find("Slot4").position;
        var spoke = slot - lc; spoke.y = 0f; spoke.Normalize();
        float floorY = lobby.transform.Find("LobbyFloor").GetComponent<Renderer>().bounds.max.y;
        root.rotation = Quaternion.LookRotation(spoke);
        root.position = new Vector3(lc.x, floorY - StartBelow, lc.z) + spoke * SpokeR;

        pathA = PathMat("Course2_5_PathA", PathColA); pathB = PathMat("Course2_5_PathB", PathColB);   // ロビーの道もこの色（FuwaStage2LobbyCourses）
        stone = FuwaStage2WaterTest.ToonMat("Ruins_Stone", new Color(0.86f, 0.74f, 0.62f));
        terra = FuwaStage2WaterTest.ToonMat("Ruins_Terracotta", new Color(0.82f, 0.48f, 0.33f));
        terraD = FuwaStage2WaterTest.ToonMat("Ruins_TerracottaDark", new Color(0.62f, 0.34f, 0.24f));
        moss = FuwaStage2WaterTest.ToonMat("Ruins_Moss", new Color(0.55f, 0.72f, 0.45f));
        underM = FuwaStage2WaterTest.ToonMat("Ruins_Under", new Color(0.72f, 0.58f, 0.48f));
        glow = FuwaStage2WaterTest.ToonMat("Ruins_PortalRim", new Color(0.55f, 0.9f, 1f));
        glow.SetColor("_EmissionColor", new Color(0.35f, 0.8f, 1f) * 1.4f); EditorUtility.SetDirty(glow);
        iron = FuwaGearTest.Mat("GearTest_Axle", new Color(0.2f, 0.21f, 0.24f));
        maskM = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Ruins_PortalMask.mat");
        if (!maskM) { maskM = new Material(Shader.Find("Fuwa/PortalMask")); AssetDatabase.CreateAsset(maskM, "Assets/FuwaStage2/Materials/Ruins_PortalMask.mat"); }
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaStage2/Meshes", "Course2_5");
        var pathRef = GameObject.Find("FuwaCourse3/PathMesh"); pathLayer = pathRef ? pathRef.layer : 11;
        ballT = Resources.FindObjectsOfTypeAll<FuwaBall>().FirstOrDefault(b => b.gameObject.scene.IsValid() && b.transform.parent && b.transform.parent.name == "FuwaSystem");
        ballR = ballT ? ballT.GetComponent<SphereCollider>().radius * ballT.transform.lossyScale.x : 0.15f;
        cps.Clear(); segNo = 0; stencilNo = 10; FuwaPortalTest.clones.Clear();
        log = new System.Text.StringBuilder();

        float R0 = StartSet(root);
        pathT = new GameObject("Path").transform; pathT.SetParent(root, false);
        secT = new GameObject("Sections").transform; secT.SetParent(root, false);
        viewT = new GameObject("PortalViews").transform; viewT.SetParent(root, false);
        th = 90f; P = Vector3.zero;
        IslandJoin(pathT, "StartJoin", Vector3.zero, Dir(th), R0, HW, pathB);
        P = Dir(th) * R0;

        S(3f);
        Section2_FirstPortal();
        Section3_SmallGate();
        Section4_Big();
        Section5_Switch();
        Section6_PortalSwitch();
        S(2f);
        var goalAt = GoalSet(root, P, Dir(th));

        // アウト（下に落ちた）：コースの下（まわり 12m）
        var lb = LocalCollidersBounds();
        var outSize = new Vector3(lb.size.x + 24f, 2f, lb.size.z + 24f);
        float outY = lb.min.y - 6f;
        Hazard(root, "BallOut", new Vector3(lb.center.x, outY, lb.center.z), outSize, false);
        Hazard(root, "PlayerOut", new Vector3(lb.center.x, outY - 2f, lb.center.z), outSize, true);

        RegisterCheckpoints();
        RegisterVisibility();
        FuwaPortalTest.LinkBlowers();
        FuwaStage2LobbyCourses.Run();
        LinkRecords();
        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        return "[2–5] " + log + " ゴール " + goalAt.ToString("F1") + " 範囲 " + lb.min.ToString("F0") + "〜" + lb.max.ToString("F0");
    }

    // ================= ② はじめてのポータル =================
    //  屋根つきの回廊（部屋1）のつきあたりの壁にポータルA → 上の島の部屋2の奥の壁のポータルB から出る。部屋2の右の壁の出口から道へ
    //  部屋2は回廊の右うしろの上（Aの穴の奥（回廊の先）にはない所）
    public static float CorridorLen = 14f, CorridorW = 4.4f, CorridorH = 4.2f, Room2W = 7f, Room2L = 7f, Room2H = 4.5f;
    // 回廊のまん中から、部屋2の床のまん中へ（回廊の向き基準：右12・上8・うしろ6。Aの穴の奥（回廊の先）には出ない所）
    //  コースはロビーから外（+z）へのびて、横へ出る時は右（+x）へ。左（-x）は 2–4（コースの座標で x < -19）があるので行かない
    public static Vector3 Room2Offset = new Vector3(12f, 8f, -6f);
    static void Section2_FirstPortal()
    {
        float h0 = th;
        var c1 = P + Dir(th) * (CorridorLen / 2f);
        var r1 = Room("Room1_Corridor", c1, th, CorridorW, CorridorLen, CorridorH, new[] { Door('B', 0f, 2f * HW, 3.4f), PortalGap('F', 0f) });
        var r2 = Room("Room2_Up", c1 + Off(h0, Room2Offset), h0, Room2W, Room2L, Room2H, new[] { PortalGap('K', 0f), Door('R', Room2L / 2f - 2.3f, 2f * HW, 3.4f, 0f, true) });
        var A = BigPortal(r1, "PortalA", new Vector3(0f, 0f, CorridorLen / 2f), 180f);
        var B = BigPortal(r2, "PortalB", new Vector3(0f, 0f, -Room2L / 2f), 0f);
        Link(A, B, r2, r1, true, true);
        Under(r2, Room2W + 2f * WT, Room2L + 2f * WT, 3f);
        Under(r1, CorridorW + 2f * WT, CorridorLen + 2f * WT, 2f);
        // 部屋2の右の出口の外へ → すぐ左へ曲がって、また外向き（+z）へ
        th = h0 - 90f;
        var door = r2.TransformPoint(new Vector3(Room2W / 2f + WT, 0f, Room2L / 2f - 2.3f));
        P = root.InverseTransformPoint(door);
        CP("Checkpoint1", 1.2f);
        S(2f); T(90f);
        log.Append("②ポータル ");
    }

    // ================= ③ ちいさくなる門 =================
    //  青い輪 → 鉄格子の門（すき間はちいさいふわふわだけ通る。人も通れない）。人は門の右のクモの巣の通り口をくぐる（人はすり抜け、ふわふわはくっつく）→ 白い輪
    public static float GateH = 3.6f, WebDoorW = 1.6f, WebDoorH = 3.0f;
    static void Section3_SmallGate()
    {
        S(3f);
        Rune("Rune3_Small", -1); S(4f);
        // 門の前後は右へ広げて、人の通り口の床に
        var g = new GameObject("S3_Gate").transform; g.SetParent(secT, false); g.localPosition = P; g.localRotation = Quaternion.LookRotation(Dir(th));
        float sideW = WebDoorW + 1.2f;   // 右の床の幅
        Strip(g, "SideFloor", new Vector3(HW + sideW / 2f, 0f, -2.5f), Vector3.forward * 5f, sideW / 2f, false);
        float x0 = -HW - 0.6f, xm0 = HW, xm1 = HW + 0.5f, xd1 = xm1 + WebDoorW, x1 = HW + sideW + 0.6f, wallH = GateH + 0.6f, t = 0.6f;
        Block(g, "WallL", x0, -HW, -0.6f, wallH, t, stone);
        Block(g, "WallMid", xm0, xm1, -0.6f, wallH, t, stone);
        Block(g, "WallR", xd1, x1, -0.6f, wallH, t, stone);
        Block(g, "OverGrate", -HW, HW, GateH, wallH, t, terra);
        Block(g, "OverDoor", xm1, xd1, WebDoorH, wallH, t, terra);
        FuwaSizeTest.GrateAt(g, "Grate", Vector3.zero, Quaternion.identity, 2f * HW, GateH, stone, iron, 0);
        Web(g, "DoorWeb", new Vector3((xm1 + xd1) / 2f, WebDoorH / 2f, 0f), Quaternion.identity, new Vector2(WebDoorW + 0.1f, WebDoorH + 0.1f), false, 0.6f);
        S(4f);
        Rune("Rune3_Normal", 0); S(3f);
        log.Append("③門 ");
    }

    // ================= ④ でかくなる =================
    //  オレンジの輪 → 強い横風の細い橋（ふつうだと流されて落ちる、でかいと耐える）→ 吸いこむ渦が両わきにある道 → 白い輪
    public static float WindBridgeLen = 12f, WindBridgeHW = 0.9f, CrossWindStrength = 3.4f;
    static void Section4_Big()
    {
        S(2f);
        Rune("Rune4_Big", 1); S(3f);
        // 細い橋（人もここを歩く）と横風
        var bridgeStart = P; float bth = th;
        Strip(pathT, "Road" + (segNo++) + "_WindBridge", P, Dir(th) * WindBridgeLen, WindBridgeHW, true);
        P += Dir(th) * WindBridgeLen;
        WindZone("S4_CrossWind", bridgeStart + Dir(bth) * (WindBridgeLen / 2f) + Vector3.up * 2.5f, bth, new Vector3(9f, 6f, WindBridgeLen - 1f), Vector3.right, CrossWindStrength);
        S(2f);
        // 渦の道：両わきに吸いこむ渦（捕まえるつむじ風）
        var w0 = P;
        S(10f);
        Whirl("S4_WhirlL", w0 + Dir(th) * 3f + Dir(th + 90f) * (HW + 2.4f));
        Whirl("S4_WhirlR", w0 + Dir(th) * 7f + Dir(th - 90f) * (HW + 2.4f));
        Rune("Rune4_Normal", 0); S(2f);
        CP("Checkpoint2", 0.5f);
        S(2f);
        log.Append("④でかい ");
    }

    // ================= ⑤ 切りかえ =================
    //  オレンジ → 道をふさぐクモの巣の壁（でかいと破れる、人はすり抜け）→ 鳥のいる所（でかいと持てない）→ 白 → 追い風の谷（でかいと落ちる）
    public static float GapLen = 12f, TailWind = 2.2f;
    static void Section5_Switch()
    {
        T(-90f); S(2f);
        Rune("Rune5_Big", 1); S(4f);
        // クモの巣の壁（アーチの中）
        var a = new GameObject("S5_WebArch").transform; a.SetParent(secT, false); a.localPosition = P; a.localRotation = Quaternion.LookRotation(Dir(th));
        float ah = 3.6f;
        Block(a, "PostL", -HW - 0.6f, -HW, -0.6f, ah + 0.6f, 0.6f, stone);
        Block(a, "PostR", HW, HW + 0.6f, -0.6f, ah + 0.6f, 0.6f, stone);
        Block(a, "Top", -HW, HW, ah, ah + 0.6f, 0.6f, terra);
        Web(a, "ArchWeb", new Vector3(0f, ah / 2f, 0f), Quaternion.identity, new Vector2(2f * HW + 0.1f, ah + 0.1f), true, 0.6f);
        S(6f);
        // 鳥：道の上で待っていて、ふわふわが来るとつかんで道の外へ運んで落とす（でかいと持てない）
        Bird("S5_Bird", P + Dir(th) * -3f + Vector3.up * 3.2f);
        S(4f);
        Rune("Rune5_Normal", 0); S(3f);
        // 追い風の谷：ふわふわは谷の上を風に乗って渡る（でかいと風が弱く重いので落ちる）。人は谷の右の細い橋
        var gapStart = P; float gth = th;
        var pb = new GameObject("S5_PlayerBridge").transform; pb.SetParent(secT, false);
        Strip(pb, "Bridge", gapStart + Dir(gth - 90f) * (HW + 1.6f), Dir(gth) * GapLen, 0.55f, false);
        // 人の橋へのつなぎ（谷の手前と向こうの道の右へ）
        Strip(pb, "JoinNear", gapStart - Dir(gth) * 1.2f + Dir(gth - 90f) * (HW + 0.8f), Dir(gth) * 1.2f, 0.8f, false);
        Strip(pb, "JoinFar", gapStart + Dir(gth) * GapLen + Dir(gth - 90f) * (HW + 0.8f), Dir(gth) * 1.2f, 0.8f, false);
        P += Dir(th) * GapLen;
        WindZone("S5_TailWind", gapStart + Dir(gth) * (GapLen / 2f) + Vector3.up * 1.5f, gth, new Vector3(2f * HW + 1f, 5f, GapLen + 2f), new Vector3(0f, 0.35f, 1f).normalized, TailWind);
        S(3f);
        CP("Checkpoint3", 0.5f);
        S(2f);
        log.Append("⑤切りかえ ");
    }

    // ================= ⑥ ポータル＋切りかえ =================
    //  青い輪 → 部屋3：奥の壁に小さい窓のポータル（ふわふわだけ、小さくないと入らない）と、クモの巣でふさいだ扉のポータル（人）→ 上の部屋4
    //  → 部屋4の出口 → オレンジの輪 → 横風の上り坂 → ゴール
    public static float Room3W = 8f, Room3L = 7f, Room3H = 4.2f, Room4W = 8f, Room4L = 7f, Room4H = 4.2f;
    public static Vector3 Room4Offset = new Vector3(-10f, 7f, -6f);   // 部屋3から（向き基準：左10・上7・うしろ6）
    public static float WinHalf = 0.12f, WinY = 1.3f;   // 窓：0.24m 角（小さいふわふわ 0.17 は通る、ふつう 0.3 は通らない）
    static void Section6_PortalSwitch()
    {
        T(90f); S(2f);
        Rune("Rune6_Small", -1); S(3f);
        float h0 = th;
        var c3 = P + Dir(th) * (Room3L / 2f);
        float doorX = 2f, winX = -1.6f;
        var r3 = Room("Room3", c3, th, Room3W, Room3L, Room3H, new[] { Door('B', 0f, 2f * HW, 3.4f), PortalGap('F', doorX), WindowGap('F', winX) });
        var r4 = Room("Room4_Up", c3 + Off(h0, Room4Offset), h0, Room4W, Room4L, Room4H, new[] { PortalGap('K', doorX), WindowGap('K', winX), Door('F', 0f, 2f * HW, 3.4f, 0f, true) });
        // 人の扉（ふわふわは運ばない）・ふわふわの窓（人は通れない大きさ）
        var Ad = BigPortal(r3, "PortalDoorA", new Vector3(doorX, 0f, Room3L / 2f), 180f);
        var Bd = BigPortal(r4, "PortalDoorB", new Vector3(doorX, 0f, -Room4L / 2f), 0f);
        Link(Ad, Bd, r4, r3, true, false);
        var Aw = WindowPortal(r3, "PortalWinA", new Vector3(winX, WinY, Room3L / 2f), 180f);
        var Bw = WindowPortal(r4, "PortalWinB", new Vector3(winX, WinY, -Room4L / 2f), 0f);
        Link(Aw, Bw, r4, r3, false, true, WinHalf, 2f * WinHalf);
        // 扉のポータルの前にクモの巣（ふわふわを扉へ行かせない。人はすり抜け）
        Web(r3, "DoorWeb", new Vector3(doorX, FuwaPortalTest.HoleH / 2f, Room3L / 2f - 0.55f), Quaternion.identity, new Vector2(2f * FuwaPortalTest.HalfW + 0.2f, FuwaPortalTest.HoleH + 0.1f), false, 0.6f);
        Under(r3, Room3W + 2f * WT, Room3L + 2f * WT, 2.5f);
        Under(r4, Room4W + 2f * WT, Room4L + 2f * WT, 3f);
        // 部屋4の前の出口から
        th = h0;
        P = root.InverseTransformPoint(r4.TransformPoint(new Vector3(0f, 0f, Room4L / 2f + WT)));
        CP("Checkpoint4", 1.2f);
        S(2f);
        Rune("Rune6_Big", 1); S(3f);
        // 横風の上り坂（でかくないと流される）
        var s0 = P; float sth = th;
        S(12f, 3f);
        WindZone("S6_CrossWind", s0 + Dir(sth) * 6f + Vector3.up * 4f, sth, new Vector3(9f, 7f, 11f), Vector3.left, CrossWindStrength);
        S(2f);
        Rune("Rune6_Normal", 0);
        log.Append("⑥ポータル＋切りかえ ");
    }

    // ================= 部品 =================

    struct DoorSpec { public char side; public float off, w, h, y; public bool floor; }
    // side：B＝うしろ（入口）、F＝前（つきあたり）、K＝うしろ（B と同じ壁、ポータル用の名前）、L＝左、R＝右。off は壁にそった中心のずれ（前の壁・うしろの壁は +x、左右の壁は +z）
    //  floor：壁の厚みの所に床を入れる（外へ出る出入口だけ。道が入ってくる入口・ポータル（面の奥に床があるとコピーとちらつく）は入れない）
    static DoorSpec Door(char side, float off, float w, float h, float y = 0f, bool floor = false) => new DoorSpec { side = side == 'K' ? 'B' : side, off = off, w = w, h = h, y = y, floor = floor };
    // ポータルの穴の所の壁のすき間：枠より少しだけ大きく（壁のすき間の面が枠の中にかくれる。同じ面だとちらつく）
    static DoorSpec PortalGap(char side, float off) => Door(side, off, 2f * FuwaPortalTest.HalfW + 0.1f, FuwaPortalTest.HoleH + 0.05f);
    static DoorSpec WindowGap(char side, float off) => Door(side, off, 2f * WinHalf + 0.02f, 2f * WinHalf + 0.02f, WinY - 0.01f);
    // コースの座標のずれ（部屋の向き heading 基準：x＝右、y＝上、z＝前）
    static Vector3 Off(float heading, Vector3 local) => Dir(heading - 90f) * local.x + Vector3.up * local.y + Dir(heading) * local.z;

    // 部屋：c＝床のまん中（コースの座標、床の上の面）、heading＝前の向き。床はシマの道（w × len）、まわりに厚み WT の壁、上に屋根
    static Transform Room(string name, Vector3 c, float heading, float w, float len, float h, DoorSpec[] doors)
    {
        var r = new GameObject(name).transform; r.SetParent(secT, false); r.localPosition = c; r.localRotation = Quaternion.LookRotation(Dir(heading));
        Strip(r, "Floor", new Vector3(0f, 0f, -len / 2f), Vector3.forward * len, w / 2f, false);
        var walls = new GameObject("Walls").transform; walls.SetParent(r, false);
        float hx = w / 2f, hz = len / 2f;
        // 前・うしろの壁（x にそって、角をふくむ）
        foreach (char side in new[] { 'F', 'B' })
        {
            float z = side == 'F' ? hz + WT / 2f : -hz - WT / 2f;
            WallLine(walls, "Wall" + side, -hx - WT, hx + WT, h, doors.Where(d => d.side == side).ToArray(), (a, b, y0, y1) => WallBox(walls, "Wall" + side, new Vector3((a + b) / 2f, (y0 + y1) / 2f, z), new Vector3(b - a, y1 - y0, WT)));
        }
        // 左・右の壁（z にそって、角はのぞく）
        foreach (char side in new[] { 'L', 'R' })
        {
            float x = side == 'L' ? -hx - WT / 2f : hx + WT / 2f;
            WallLine(walls, "Wall" + side, -hz, hz, h, doors.Where(d => d.side == side).ToArray(), (a, b, y0, y1) => WallBox(walls, "Wall" + side, new Vector3(x, (y0 + y1) / 2f, (a + b) / 2f), new Vector3(WT, y1 - y0, b - a)));
        }
        // 屋根
        var roof = Box(r, "Roof", new Vector3(0f, h + 0.3f, 0f), new Vector3(w + 2f * WT + 0.4f, 0.6f, len + 2f * WT + 0.4f), terra, 0);
        // 出入口の床（壁の厚みの所）
        foreach (var d in doors)
        {
            if (!d.floor) continue;
            if (d.side == 'L' || d.side == 'R')
            {
                float x = d.side == 'L' ? -hx - WT / 2f : hx + WT / 2f;
                Strip(r, "DoorFloor" + d.side, new Vector3(x, 0f, d.off - d.w / 2f), Vector3.forward * d.w, WT / 2f, false);
            }
            else
            {
                float z = d.side == 'F' ? hz : -hz - WT;
                Strip(r, "DoorFloor" + d.side, new Vector3(d.off, 0f, z), Vector3.forward * WT, d.w / 2f, false);
            }
        }
        return r;
    }

    // 壁の1本：a〜b（壁にそった座標）、高さ -0.6〜h。doors の所をあける（出入口の下 y より下と、上 y+h より上は壁）
    static void WallLine(Transform parent, string name, float a, float b, float h, DoorSpec[] doors, System.Action<float, float, float, float> box)
    {
        float y0 = -0.6f;
        var ds = doors.OrderBy(d => d.off).ToList();
        float cur = a;
        foreach (var d in ds)
        {
            float da = d.off - d.w / 2f, db = d.off + d.w / 2f;
            if (da > cur + 1e-3f) box(cur, da, y0, h);
            if (d.y > 0.01f) box(da, db, y0, d.y);          // 窓の下
            if (d.y + d.h < h - 1e-3f) box(da, db, d.y + d.h, h);   // 出入口の上
            cur = db;
        }
        if (b > cur + 1e-3f) box(cur, b, y0, h);
    }

    static void WallBox(Transform parent, string name, Vector3 lp, Vector3 size)
    {
        if (size.x < 1e-3f || size.y < 1e-3f || size.z < 1e-3f) return;
        Box(parent, name, lp, size, stone, 0);
    }

    // 横にのびる壁のかたまり（門・アーチ用）：x0〜x1、y0〜y1、厚み t（z のまん中 0）
    static void Block(Transform parent, string name, float x0, float x1, float y0, float y1, float t, Material m)
    {
        Box(parent, name, new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, 0f), new Vector3(x1 - x0, y1 - y0, t), m, 0);
    }

    // 部屋の下の岩（ブロックアウト：うすい石の台＋先細りの岩）
    static void Under(Transform room, float w, float len, float depth)
    {
        var u = Box(room, "Under", new Vector3(0f, -0.6f - depth / 2f, 0f), new Vector3(w * 0.92f, depth, len * 0.92f), underM, 0);
        Object.DestroyImmediate(u.GetComponent<Collider>());
        var u2 = Box(room, "Under2", new Vector3(0f, -0.6f - depth - depth * 0.4f, 0f), new Vector3(w * 0.55f, depth * 0.8f, len * 0.55f), underM, 0);
        Object.DestroyImmediate(u2.GetComponent<Collider>());
    }

    // 人もふわふわも通れるポータル（試作と同じ枠・穴）
    static Transform BigPortal(Transform room, string name, Vector3 lp, float yaw)
    {
        var p = FuwaPortalTest.Portal(room, name, lp, yaw, stone, terraD, glow, maskM, ++stencilNo);
        return p;
    }

    // 小さい窓のポータル：枠（4本の石）＋穴。lp は窓の下のまん中。PortalHole の大きさだけ変える
    static Transform WindowPortal(Transform room, string name, Vector3 lp, float yaw)
    {
        int st = ++stencilNo;
        var p = new GameObject(name).transform; p.SetParent(room, false); p.localPosition = lp; p.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var fr = new GameObject("PortalFrame").transform; fr.SetParent(p, false);
        float d = WT + 0.2f, f = 0.12f, s = 2f * WinHalf;
        FuwaPortalTest.Prim(PrimitiveType.Cube, fr, "Rim", new Vector3(-WinHalf - f / 2f, WinHalf, 0f), new Vector3(f, s + 2f * f, d), glow);
        FuwaPortalTest.Prim(PrimitiveType.Cube, fr, "Rim", new Vector3(WinHalf + f / 2f, WinHalf, 0f), new Vector3(f, s + 2f * f, d), glow);
        FuwaPortalTest.Prim(PrimitiveType.Cube, fr, "Rim", new Vector3(0f, -f / 2f, 0f), new Vector3(s, f, d), glow);
        FuwaPortalTest.Prim(PrimitiveType.Cube, fr, "Rim", new Vector3(0f, s + f / 2f, 0f), new Vector3(s, f, d), glow);
        var m = GameObject.CreatePrimitive(PrimitiveType.Quad); m.name = "PortalHole"; Object.DestroyImmediate(m.GetComponent<Collider>());
        m.transform.SetParent(p, false); m.transform.localPosition = new Vector3(0f, WinHalf, 0f); m.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        m.transform.localScale = new Vector3(s, s, 1f);
        var mr = m.GetComponent<MeshRenderer>(); var mm = new Material(maskM) { name = "Ruins_PortalMask_" + st }; mm.SetFloat("_StencilRef", st);
        string mp = "Assets/FuwaStage2/Materials/Ruins_PortalMask_" + st + ".mat";
        var old = AssetDatabase.LoadAssetAtPath<Material>(mp); if (old) { old.CopyPropertiesFromMaterial(mm); old.shader = mm.shader; mm = old; } else AssetDatabase.CreateAsset(mm, mp);
        mr.sharedMaterial = mm; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        return p;
    }

    // ポータルの組：A の奥に B の部屋のコピー、B の奥に A の部屋のコピー。人を運ぶ（players）・ふわふわを運ぶ（balls）
    static void Link(Transform A, Transform B, Transform roomOfB, Transform roomOfA, bool players, bool balls, float halfW = -1f, float height = -1f)
    {
        int sa = (int)A.Find("PortalHole").GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_StencilRef");
        int sb = (int)B.Find("PortalHole").GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_StencilRef");
        FuwaPortalTest.Copy(roomOfB, A, B, viewT, "ViewThrough_" + A.name, sa);
        FuwaPortalTest.Copy(roomOfA, B, A, viewT, "ViewThrough_" + B.name, sb);
        foreach (var (en, ex, sref) in new[] { (A, B, sa), (B, A, sb) })
        {
            var pt = UdonSharpUndo.AddComponent<FuwaPortal>(en.gameObject);
            pt.entry = en; pt.exit = ex;
            pt.halfW = halfW > 0f ? halfW : FuwaPortalTest.HalfW; pt.height = height > 0f ? height : FuwaPortalTest.HoleH;
            pt.ball = balls ? ballT : null; pt.ballRadius = ballR;
            if (balls)
            {
                pt.ghost = FuwaPortalTest.BallGhost(viewT, ballT, en, sref, false);
                pt.nearGhost = FuwaPortalTest.BallGhost(viewT, ballT, en, sref, true);
            }
            UdonSharpEditorUtility.CopyProxyToUdon(pt); FuwaStage2WaterTest.NoSync(pt);
            // 人を運ばない窓：FuwaPortal は人も見るので、穴の高さを人の頭が届かない大きさにしてある（窓は人が通れないので頭が入ることはない）
            if (!players) { }
        }
    }

    // 大きさのルーン（くぐる向き＝進む向き）。いまの P に置く
    static void Rune(string name, int mode)
    {
        FuwaSizeTest.RuneAt(secT, name, P + Vector3.up * FuwaSizeTest.RuneHeight, Quaternion.LookRotation(Dir(th)), mode, FuwaSizeTest.RuneColor(mode));
    }

    // 風のゾーン（ふわふわだけ）：c＝まん中（コースの座標）、heading の向きの箱、dirLocal はその箱の中での向き（+z が進む向き、+x が右）
    static void WindZone(string name, Vector3 c, float heading, Vector3 size, Vector3 dirLocal, float strength)
    {
        var w = new GameObject(name); w.transform.SetParent(secT, false); w.layer = 2;
        w.transform.localPosition = c; w.transform.localRotation = Quaternion.LookRotation(Dir(heading));
        var bc = w.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = size;
        var wa = UdonSharpUndo.AddComponent<FuwaWindArea>(w);
        wa.localDirection = dirLocal.normalized; wa.strength = strength; wa.offSeconds = 0f; wa.baseLevel = 1f;
        UdonSharpEditorUtility.CopyProxyToUdon(wa); FuwaStage2WaterTest.NoSync(wa);
        // 目印：風の筋（うすい板、ブロックアウト用）
        var m = FuwaGearTest.Mat("SizeTest_WindMark", new Color(0.8f, 0.95f, 1f));
        var rot = Quaternion.LookRotation(dirLocal.normalized);
        for (int k = 0; k < 8; k++)
        {
            var p = new Vector3(Mathf.Lerp(-size.x * 0.4f, size.x * 0.4f, (k * 0.37f) % 1f), Mathf.Lerp(-size.y * 0.3f, size.y * 0.3f, (k * 0.61f) % 1f), Mathf.Lerp(-size.z * 0.4f, size.z * 0.4f, (k * 0.23f + 0.1f) % 1f));
            var s = FuwaPortalTest.Prim(PrimitiveType.Cube, w.transform, "Streak", p, new Vector3(0.03f, 0.03f, 1.2f), m); s.localRotation = rot;
        }
    }

    // 吸いこむ渦（捕まえるつむじ風）：c＝底のまん中（コースの座標、道の高さ）
    static void Whirl(string name, Vector3 c)
    {
        var src = GameObject.Find("FuwaCourse/Gimmicks_Kaze/G5_SmallWhirls/Whirl0");
        if (!src) { log.Append("（つむじ風の見本がない）"); return; }
        var g = Object.Instantiate(src, secT, false); g.name = name;
        g.transform.localPosition = c + Vector3.down * 0.5f; g.transform.localRotation = Quaternion.LookRotation(Dir(th));
        var ww = g.GetComponent<FuwaWhirlwind>();
        if (ww)
        {
            ww.trap = true; ww.trapPull = 3.2f; ww.radius = 2.6f;
            UdonSharpEditorUtility.CopyProxyToUdon(ww); FuwaStage2WaterTest.NoSync(ww);
        }
        // 判定の筒を道の上まで広げる
        var bc = g.GetComponentInChildren<BoxCollider>(true);
        if (bc) { bc.size = new Vector3(5.6f, 5f, 5.6f); bc.center = new Vector3(0f, 2.5f, 0f); }
        // 足場（渦の下の小さい浮島、見た目だけ）
        var isl = FuwaPortalTest.Prim(PrimitiveType.Cylinder, secT, name + "_Isle", c + Vector3.down * 0.9f, new Vector3(2.2f, 0.4f, 2.2f), underM);
    }

    // 鳥：道の上で待つ（止まり木＝c）→ ふわふわをつかむと道の外（左）へ運んで落とす。でかいと持てない
    static void Bird(string name, Vector3 c)
    {
        var t = new GameObject(name).transform; t.SetParent(secT, false);
        var body = new GameObject("Body").transform; body.SetParent(t, false);
        var bm = FuwaStage2WaterTest.ToonMat("Ruins_Bird", new Color(0.36f, 0.32f, 0.42f));
        var bk = FuwaStage2WaterTest.ToonMat("Ruins_BirdBeak", new Color(1f, 0.75f, 0.3f));
        FuwaPortalTest.Prim(PrimitiveType.Sphere, body, "Torso", Vector3.zero, new Vector3(0.7f, 0.55f, 0.9f), bm);
        FuwaPortalTest.Prim(PrimitiveType.Sphere, body, "Head", new Vector3(0f, 0.3f, 0.45f), new Vector3(0.42f, 0.42f, 0.42f), bm);
        var beak = FuwaPortalTest.Prim(PrimitiveType.Cube, body, "Beak", new Vector3(0f, 0.27f, 0.72f), new Vector3(0.12f, 0.1f, 0.25f), bk);
        Transform Wing(string n, float sx)
        {
            var pivot = new GameObject(n).transform; pivot.SetParent(body, false); pivot.localPosition = new Vector3(sx * 0.3f, 0.1f, 0f);
            FuwaPortalTest.Prim(PrimitiveType.Cube, pivot, "Feather", new Vector3(sx * 0.6f, 0f, 0f), new Vector3(1.2f, 0.06f, 0.6f), bm);
            return pivot;
        }
        var wl = Wing("WingL", -1f); var wr = Wing("WingR", 1f);
        var side = Dir(th + 90f);   // 左（コースの外）へ運ぶ
        var w = new[] { c, c + side * 4f + Vector3.up * 1.5f, c + side * 10f + Vector3.up * 2f, c + side * 16f + Vector3.up * 1f };
        var fb = UdonSharpUndo.AddComponent<FuwaBird>(t.gameObject);
        fb.ball = ballT; fb.body = body; fb.wingL = wl; fb.wingR = wr;
        fb.path = w.Select(p => root.TransformPoint(p)).ToArray();
        fb.carrySpeed = 3.5f; fb.returnSpeed = 6f; fb.grabRadius = 1.8f; fb.cooldown = 1.5f; fb.cantLiftBig = true;
        body.position = fb.path[0];
        UdonSharpEditorUtility.CopyProxyToUdon(fb); FuwaStage2WaterTest.NoSync(fb);
    }

    // クモの巣（人はすり抜け＝トリガー、ふわふわはくっつく）。tear＝でかいと破れる。lp は巣のまん中、面の向きは lr の forward
    static void Web(Transform parent, string name, Vector3 lp, Quaternion lr, Vector2 size, bool tear, float playerSlow)
    {
        var tmpl = Resources.FindObjectsOfTypeAll<SpiderWebBuilder>().FirstOrDefault(b => b && b.gameObject.scene.IsValid() && b.name == "BigWeb" && b.transform.parent && b.transform.parent.name == "Gimmick6_Window");
        if (!tmpl) { log.Append("（クモの巣の見本がない）"); return; }
        var g = Object.Instantiate(tmpl.gameObject, parent, false); g.name = name; g.SetActive(true);
        g.transform.localPosition = lp; g.transform.localRotation = lr; g.transform.localScale = Vector3.one;
        var wh = g.GetComponent<FuwaWebHole>(); if (wh) Object.DestroyImmediate(wh);
        var b = g.GetComponent<SpiderWebBuilder>();
        b.size = size; b.holeCenter = Vector2.zero; b.holeRadius = 0.0f; b.spokes = 20; b.floorShadow = false; b.autoIsland = false;
        b.left.anchor = SpiderWebBuilder.AnchorType.None; b.right.anchor = SpiderWebBuilder.AnchorType.None; b.topThread = false;
        b.outerMin = 0.96f; b.outerMax = 1f; b.cornerRound = 0.15f;
        b.assetId = "Web_2_5_" + parent.name + "_" + name;
        SpiderWebBuild.Build(b);
        var hz = g.GetComponent<FuwaHazard>();
        if (hz)
        {
            hz.holeCenter = null; hz.sticky = true; hz.playerSlow = playerSlow; hz.playerSlowLinger = 0f;
            hz.tearWhenBig = tear;
            if (tear) hz.tearHide = g.transform.Cast<Transform>().Where(x => x.name == "Front" || x.name == "Back" || x.name == "WebSupports").Select(x => x.gameObject).ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(hz); FuwaStage2WaterTest.NoSync(hz);
        }
    }

    // ================= 道（2–4 と同じ） =================
    static Vector3 Dir(float deg) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }

    static void S(float len, float rise = 0f, bool under = true)
    {
        var d = Dir(th) * len + Vector3.up * rise;
        Strip(pathT, "Road" + (segNo++) + (rise != 0f ? "_Slope" : ""), P, d, HW, under);
        P += d;
    }

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
        Vector3 r0 = P - L * HW * sg - inner;
        var top = new List<Vector3>();
        for (int i = 0; i <= n; i++) top.Add(inner + Quaternion.Euler(0f, -deg * i / n, 0f) * r0);
        for (int pass = 0; pass < 2; pass++)
        {
            var off = pass == 0 ? Vector3.zero : Vector3.down * Tk;
            for (int i = 0; i < n; i++) Tri(v, tr, inner + off, top[i] + off, top[i + 1] + off, pass == 0 ? Vector3.up : Vector3.down);
        }
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
        m.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        EditorUtility.SetDirty(m);
        return m;
    }

    // シマシマの道：a（手前のはしのまん中、上の面）から d だけ。当たりは1枚の箱、見た目は StripeLen ごとに A/B
    static void Strip(Transform parent, string name, Vector3 a, Vector3 d, float hw, bool under = true)
    {
        float len = d.magnitude; if (len <= 0.01f) return;
        var g = new GameObject(name).transform; g.SetParent(parent, false);
        g.localPosition = a; g.localRotation = Quaternion.LookRotation(d / len, Vector3.up);
        const float Tk = 0.6f;
        var col = new GameObject("Col"); col.transform.SetParent(g, false); col.layer = pathLayer;
        var bc = col.AddComponent<BoxCollider>(); bc.center = new Vector3(0, -Tk * 0.5f, len * 0.5f); bc.size = new Vector3(hw * 2f, Tk, len);
        int n = Mathf.Max(1, Mathf.RoundToInt(len / StripeLen));
        bool slope = Mathf.Abs(d.y) > 0.001f;
        var vUp = g.InverseTransformDirection(parent.TransformDirection(Vector3.up)).normalized * Tk;
        for (int k = 0; k < n; k++)
        {
            float l = len / n;
            if (!slope)
            {
                var s = Box(g, "S" + k, new Vector3(0, -Tk * 0.5f, l * (k + 0.5f)), new Vector3(hw * 2f, Tk, l), (k % 2 == 0) ? pathA : pathB, 0);
                Object.DestroyImmediate(s.GetComponent<Collider>());
                continue;
            }
            float z0 = l * k, z1 = l * (k + 1);
            Vector3 T(float x, float z) => new Vector3(x, 0f, z);
            var p = new[] { T(-hw, z0), T(hw, z0), T(hw, z1), T(-hw, z1) };
            var q = p.Select(x => x - vUp).ToArray();
            var vv = new List<Vector3>(); var tt = new List<int>();
            void F(Vector3 a2, Vector3 b2, Vector3 c2, Vector3 e2, Vector3 want)
            {
                if (Vector3.Dot(Vector3.Cross(b2 - a2, c2 - a2), want) < 0f) { var tmp = b2; b2 = e2; e2 = tmp; }
                int i0 = vv.Count; vv.Add(a2); vv.Add(b2); vv.Add(c2); vv.Add(e2); tt.Add(i0); tt.Add(i0 + 1); tt.Add(i0 + 2); tt.Add(i0); tt.Add(i0 + 2); tt.Add(i0 + 3);
            }
            var up = Vector3.up; var fw = Vector3.forward;
            F(p[0], p[1], p[2], p[3], up); F(q[0], q[3], q[2], q[1], -vUp);
            F(p[0], p[3], q[3], q[0], Vector3.left); F(p[1], q[1], q[2], p[2], Vector3.right);
            F(p[0], q[0], q[1], p[1], -fw); F(p[3], p[2], q[2], q[3], fw);
            var me = new Mesh { name = "SlopeStripe" }; me.SetVertices(vv); me.SetTriangles(tt, 0); me.RecalculateNormals(); me.RecalculateBounds();
            var so = new GameObject("S" + k); so.transform.SetParent(g, false);
            so.AddComponent<MeshFilter>().sharedMesh = me; so.AddComponent<MeshRenderer>().sharedMaterial = (k % 2 == 0) ? pathA : pathB;
        }
        if (!under) return;
        // 道の下の岩（空に浮いた遺跡の道。ブロックアウトなので箱）
        var u = Box(g, "Under", new Vector3(0, -Tk - 0.6f, len * 0.5f), new Vector3(hw * 2f - 0.3f, 1.2f, Mathf.Max(0.1f, len - 0.2f)), underM, 0);
        Object.DestroyImmediate(u.GetComponent<Collider>());
    }

    // ================= スタート・ゴール・チェックポイント（2–4 と同じ） =================
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
        if (fc) { UdonSharpEditorUtility.CopyProxyToUdon(fc); FuwaStage2WaterTest.NoSync(fc); cps.Add(fc); }
    }

    static void RegisterCheckpoints()
    {
        var ball = Object.FindObjectsOfType<FuwaBall>(true).FirstOrDefault(); if (!ball) return;
        var so = new SerializedObject(ball); var arr = so.FindProperty("checkpoints");
        for (int k = arr.arraySize - 1; k >= 0; k--) if (arr.GetArrayElementAtIndex(k).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(k);
        foreach (var fc in cps) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = fc; }
        so.ApplyModifiedProperties();
        UdonSharpEditorUtility.CopyProxyToUdon(ball);
    }

    static void RegisterVisibility()
    {
        var visb = Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(m => m && m.gameObject.scene.IsValid() && m.GetType().Name == "FuwaAreaVisibility");
        if (!visb) return;
        var so = new SerializedObject(visb); var arr = so.FindProperty("stage2Objects");
        bool has = false; for (int i = 0; i < arr.arraySize; i++) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == root.gameObject) has = true;
        for (int i = arr.arraySize - 1; i >= 0; i--) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(i);
        if (!has) { arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = root.gameObject; }
        so.ApplyModifiedProperties();
        UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)visb);
    }

    // さいそくタイムの記録係（Records2）の 2–5 のスタートをつなぐ（作り直すと同期物のIDが変わるので、名前と同じく中身だけ書き換え）
    static void LinkRecords()
    {
        var rec = Object.FindObjectsOfType<FuwaRecords>(true).FirstOrDefault(r => r.name == "Records2"); if (!rec || rec.courseStarts == null || rec.courseStarts.Length < 5) return;
        rec.courseStarts[4] = root.Find("Start/BallStart");
        EditorUtility.SetDirty(rec); UdonSharpEditorUtility.CopyProxyToUdon(rec);
    }

    static float StartSet(Transform r)
    {
        var start = new GameObject("Start").transform; start.SetParent(r, false);
        var src = GameObject.Find("FuwaCourse/Start")?.transform;
        if (!src) { Debug.LogWarning("[2–5] FuwaCourse/Start がない"); return 3f; }
        var map = new Dictionary<Object, Object>();
        FuwaCourse2_1Builder.CopyChildren(src, start, null, map);
        foreach (var t in start.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("_Course1")) t.name = t.name.Replace("_Course1", "_Course2_5");
        float islandTop = src.Find("StartMushroom").localPosition.y;
        var area = start.Find("StartArea"); var ground = area.Find("Ground");
        start.localPosition = new Vector3(-ground.localPosition.x, -islandTop, -ground.localPosition.z);
        FuwaCourse2_1Builder.FixCopied(start, map);
        FuwaCourse2_1Builder.Stage2Grass(start);
        return ground.localScale.x * 0.5f;
    }

    static Vector3 GoalSet(Transform r, Vector3 p, Vector3 dir)
    {
        var goal = new GameObject("Goal").transform; goal.SetParent(r, false);
        var src = GameObject.Find("FuwaCourse/Goal")?.transform;
        if (!src) { Debug.LogWarning("[2–5] FuwaCourse/Goal がない"); return p; }
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
        if (player) { var pc = UdonSharpUndo.AddComponent<FuwaPlayerCatcher>(g); pc.ball = ballT; UdonSharpEditorUtility.CopyProxyToUdon(pc); FuwaStage2WaterTest.NoSync(pc); }
        else { var hz = UdonSharpUndo.AddComponent<FuwaHazard>(g); UdonSharpEditorUtility.CopyProxyToUdon(hz); FuwaStage2WaterTest.NoSync(hz); }
    }

    // コースの当たり（トリガー以外）の範囲（コースの座標）
    static Bounds LocalCollidersBounds()
    {
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
        return lb;
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
