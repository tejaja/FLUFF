using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2–4：かみ合う2枚の回転床のお試し（2026-11-07 てじゃ OK。コース案の ⑤ 山場）
//  かごの羽根車（の軸の小歯車 24枚）→ 床下の歯車（16枚）×2 → 回転床 A のふちの下の大きい歯車（72枚）
//  A の歯車と B の歯車（48枚）が直接かみ合う。B は逆向きで 72/48 = 1.5 倍の速さ
// 橋：A はまっすぐ2本（180° はなれ：かごの方と B の方）。B は2本（A の方＝180°、出口＝60°）
//  A は 180° ごとにかごとつながるが、そのたび B は 270° ずれる → 4回に1回だけ全部つながる。最初は A=0°・B は答えから 180° ずれ（どっち向きに回しても 2回ぶん）
// 歯車の半径を床より大きくして、床と床の間を 3.6m あける（ふわふわが浮いて渡れないように）。橋の先どうしのすき間は 40cm
//  橋はどれだけ回ってもぶつからない：A の橋のかどの半径 + B の橋のかどの半径 < 中心距離
// ・回し方は FuwaTurnTable（table = A、B は gears に入れて同じ式で回す）。乗っている自分を運ぶのは A だけ（羽根車はかごの中なので、回す時はかごにいる）
// ・2D の (x, y) は Unity の (x, z)。2D の反時計回りは Unity の Y 回転でマイナス
public static class FuwaTwinTableTest
{
    const float Mod = 0.2f; const int ZA = 72, ZB = 48, ZI = 16, ZP = 24, NI = 2;
    const float DeckA = 4.6f, DeckB = 3.8f, TipA = 6.9f, TipB = 4.7f, BridgeW = 2.5f;
    const float GearY = -1.0f, GearTh = 0.35f, LineDeg = 175.5f;
    const float ExitDeg = 60f;                 // B から見た出口の向き
    const float CageHalfZ = 2.5f, CageLen = 10.4f;
    static readonly Vector3 Origin = new Vector3(30f, -162f, -165f);   // A のまん中、床の上の高さ

    static float CornerR(float tip) => Mathf.Sqrt(tip * tip + BridgeW * BridgeW / 4f);

    [MenuItem("FuwaCourse/ステージ2/かみ合う2枚の回転床のお試しを作る")]
    public static void Build()
    {
        Remove();
        var dark = FuwaGearTest.Mat("GearTest_Gear", new Color(0.36f, 0.38f, 0.43f));
        var light = FuwaGearTest.Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f));
        var axleM = FuwaGearTest.Mat("GearTest_Axle", new Color(0.2f, 0.21f, 0.24f));
        var brass = FuwaGearTest.Mat("FanTest_Brass", new Color(0.86f, 0.68f, 0.32f));
        var floorM = FuwaGearTest.Mat("GearTest_Floor", new Color(0.55f, 0.58f, 0.63f));
        var wood = FuwaGearTest.Mat("GearTest_Bridge", new Color(0.66f, 0.52f, 0.38f));
        var bladeM = FuwaGearTest.Mat("FanTest_Blade", new Color(0.55f, 0.82f, 0.95f));
        var deckM = FuwaGearTest.Mat("TurnTableTest_Deck", new Color(0.7f, 0.72f, 0.77f));
        var deckBM = FuwaGearTest.Mat("TwinTableTest_DeckB", new Color(0.78f, 0.74f, 0.66f));

        var root = new GameObject("TwinTableTest").transform; root.position = Origin;

        // ── 歯車のつながり（A を元に）──
        var gA = new FuwaGearTest.Gear { name = "RingA", z = ZA, c = Vector2.zero, ratio = 1f, phase = 0f };
        var gB = FuwaGearTest.Mesh2(gA, "RingB", ZB, 0f, Mod);
        float D = gB.c.x;
        if (CornerR(TipA) + CornerR(TipB) >= D) Debug.LogError("[2枚の回転床] 橋がぶつかる");
        var chain = new FuwaGearTest.Gear[NI + 1];
        var prev = gA;
        for (int i = 0; i < NI; i++) { chain[i] = FuwaGearTest.Mesh2(prev, "Idler" + (i + 1) + "_z" + ZI, ZI, LineDeg, Mod); prev = chain[i]; }
        var gP = FuwaGearTest.Mesh2(prev, "Pinion_z" + ZP, ZP, LineDeg, Mod);
        chain[NI] = gP;
        float rp = gP.ratio, php = gP.phase * Mathf.Rad2Deg;   // 小歯車の角度 = rp·v + php（v = A の角度）

        // ── 床（かご・出口）──
        float x1 = -(CornerR(TipA) + 0.05f), x0 = x1 - CageLen;   // かごの床：A の橋のかどが回る円の外で切る
        Floor(root, "CageFloor", new Vector3((x0 + x1) / 2f, 0f, 0f), 0f, new Vector2(x1 - x0, 2f * CageHalfZ), floorM);
        Vector3 cB = new Vector3(D, 0f, 0f);
        float exitNear = CornerR(TipB) + 0.05f;
        Floor(root, "Exit", cB + Polar(ExitDeg, exitNear + 3f), ExitDeg, new Vector2(6f, 6f), floorM);

        // ── 回転床 A ──
        var tableA = new GameObject("TableA").transform; tableA.SetParent(root, false);
        Deck(tableA, DeckA, deckM);
        foreach (float ang in new[] { 0f, 180f }) Bridge(tableA, ang, DeckA, TipA, wood, axleM);
        var ringAMesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZA, 8, Mod), GearTh, "TwinTable_Ring_z" + ZA, Vector2.zero));
        Ring(tableA, ringAMesh, brass, DeckA, dark);
        Prim(PrimitiveType.Cylinder, root, "PedestalA", new Vector3(0f, -1.9f, 0f), new Vector3(2.4f, 0.9f, 2.4f), dark);

        // ── 回転床 B（A とかみ合う。gears に入れて回す）──
        var frB = new GameObject("TableB_Frame").transform; frB.SetParent(root, false); frB.localPosition = cB;
        var tableB = new GameObject("TableB").transform; tableB.SetParent(frB, false);
        Deck(tableB, DeckB, deckBM);
        // 最初（u = startU）の B の歯車の角度 θB0 = gB.phase。橋は世界で {0°, 240°}（答えの {180°, 60°} から 180° ずれ）に向くよう local を決める
        float thB0 = gB.phase * Mathf.Rad2Deg;
        foreach (float wAng in new[] { 0f, 240f }) Bridge(tableB, wAng - thB0, DeckB, TipB, wood, axleM);
        var ringBMesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZB, 8, Mod), GearTh, "TwinTable_Ring_z" + ZB, Vector2.zero));
        Ring(tableB, ringBMesh, light, DeckB, dark);
        Prim(PrimitiveType.Cylinder, root, "PedestalB", cB + new Vector3(0f, -1.9f, 0f), new Vector3(2.0f, 0.9f, 2.0f), dark);

        // ── 床下の歯車の列（羽根車の小歯車の角度 u を元に）──
        int nc = chain.Length;
        var gearTs = new Transform[nc + 1]; var gearRatio = new float[nc + 1]; var gearPhase = new float[nc + 1];
        var meshI = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZI, 8, Mod), GearTh, "TurnTable_Idler_z" + ZI, Vector2.zero));
        var meshP = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZP, 8, Mod), GearTh, "TwinTable_Pinion_z" + ZP, Vector2.zero));
        float beamY = GearY - 0.35f;
        for (int i = 0; i < nc; i++)
        {
            var g = chain[i];
            var t = new GameObject(g.name).transform; t.SetParent(root, false); t.localPosition = new Vector3(g.c.x, GearY, g.c.y);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = i < NI ? meshI : meshP;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = i % 2 == 1 ? light : dark;
            gearTs[i] = t;
            gearRatio[i] = g.ratio / rp;
            gearPhase[i] = g.phase * Mathf.Rad2Deg - g.ratio * php / rp;
            float top = i == NI ? 1.05f : GearY + GearTh + 0.1f;
            Prim(PrimitiveType.Cylinder, root, "Axle_" + g.name, new Vector3(g.c.x, (beamY + top) / 2f, g.c.y), new Vector3(0.16f, (top - beamY) / 2f, 0.16f), axleM);
        }
        gearTs[nc] = tableB; gearRatio[nc] = gB.ratio / rp; gearPhase[nc] = thB0 - gB.ratio * php / rp;
        float rEnd = gP.c.magnitude + 0.6f;
        var beam = Prim(PrimitiveType.Cube, root, "Beam", Polar(LineDeg, (1f + rEnd) / 2f) + new Vector3(0f, beamY - 0.1f, 0f), new Vector3(rEnd - 1f, 0.2f, 0.4f), light);
        beam.localRotation = Quaternion.Euler(0f, -LineDeg, 0f);

        // ── 羽根車 ──
        var pinT = gearTs[NI];
        var handle = new GameObject("Handle").transform; handle.SetParent(pinT, false); handle.localPosition = new Vector3(0f, 1.05f - GearY, 0f);
        handle.gameObject.layer = 2;
        Prim(PrimitiveType.Cylinder, handle, "Hub", Vector3.zero, new Vector3(0.22f, 0.15f, 0.22f), brass);
        for (int k = 0; k < 6; k++)
        {
            var pd = Prim(PrimitiveType.Cube, handle, "Paddle" + k, Quaternion.Euler(0f, k * 60f, 0f) * new Vector3(0.45f, 0f, 0f), new Vector3(0.7f, 0.3f, 0.04f), bladeM);
            pd.localRotation = Quaternion.Euler(0f, k * 60f, 0f);
        }
        var sc = handle.gameObject.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 0.8f;

        // ── かごの金網（橋の口だけあける）──
        float endZ = CageHalfZ - 0.08f, brZ = BridgeW / 2f + 0.15f;
        FuwaTurnTableTest.WireFence(root, new Vector3((x0 + x1) / 2f, 0f, CageHalfZ), 90f, x1 - x0, 2.2f, light, axleM).name = "Fence_N";
        FuwaTurnTableTest.WireFence(root, new Vector3((x0 + x1) / 2f, 0f, -CageHalfZ), 90f, x1 - x0, 2.2f, light, axleM).name = "Fence_S";
        FuwaTurnTableTest.WireFence(root, new Vector3(x0, 0f, 0f), 0f, 2f * endZ, 2.2f, light, axleM).name = "Fence_Back";
        foreach (int sg in new[] { 1, -1 })
            FuwaTurnTableTest.WireFence(root, new Vector3(x1, 0f, sg * (brZ + endZ) / 2f), 0f, endZ - brZ, 2.2f, light, axleM).name = "Fence_Br" + (sg > 0 ? "N" : "S");

        // ── 動かすしくみ ──
        var tt = UdonSharpUndo.AddComponent<FuwaTurnTable>(handle.gameObject);
        tt.table = tableA; tt.tableRatio = 1f / rp; tt.tablePhase = -php / rp;
        tt.gears = gearTs; tt.gearRatio = gearRatio; tt.gearPhase = gearPhase;
        tt.handle = handle; tt.hitRadius = 0.8f; tt.hitHalfHeight = 0.3f; tt.hitCone = 3f; tt.startU = php;   // u = php で A が 0°
        UdonSharpEditorUtility.CopyProxyToUdon(tt); FuwaStage2WaterTest.NoSync(tt);
        SetU(php);

        // ── ふわふわの台・ワープ（回転床のお試しのかご ⇄ ここ）──
        var spot = new GameObject("BallSpot").transform; spot.SetParent(root, false); spot.localPosition = new Vector3(x0 + 2.2f, 1.6f, -0.8f);
        FuwaStage2WaterTest.BallStand(root, "BallStand", new Vector3(x0 + 1.2f, 0f, -1.6f), spot, "ふわふわを出す", new Color(1f, 0.75f, 0.85f));
        var spawn = new GameObject("Spawn").transform; spawn.SetParent(root, false); spawn.localPosition = new Vector3(x0 + 1.0f, 0f, 0f); spawn.localRotation = Quaternion.Euler(0f, 90f, 0f);
        var warpMat = FuwaGearTest.Mat("TwinTableTest_Warp", new Color(0.95f, 0.6f, 0.55f));
        var ttRoot = GameObject.Find("TurnTableTest");
        if (ttRoot)
        {
            var back = new GameObject("BackSpot").transform; back.SetParent(root, false); back.position = ttRoot.transform.position + new Vector3(-16f, 0f, 0.5f); back.rotation = Quaternion.Euler(0f, 90f, 0f);
            FuwaIceTestBuilder.Warp(root, "WarpToTwin", ttRoot.transform.position + new Vector3(-11.5f, 0.35f, -2.0f), "TWIN TABLE", spawn, warpMat);
            FuwaIceTestBuilder.Warp(root, "WarpBack", root.TransformPoint(new Vector3(x0 + 0.6f, 0.35f, 1.8f)), "BACK", back, warpMat);
        }

        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[2枚の回転床] 作った 中心距離 {D:F2}  羽根車1回転で A {360f / rp:F1}°  B の比 {gB.ratio:F2}  橋のかど {CornerR(TipA):F2}+{CornerR(TipB):F2}");
    }

    // エディタ用：羽根車（小歯車）の角度 u にする（FuwaTurnTable と同じ計算）
    public static void SetU(float u)
    {
        var o = GameObject.Find("TwinTableTest"); if (!o) return;
        var tt = o.GetComponentInChildren<FuwaTurnTable>(true); if (!tt) return;
        tt.table.localRotation = Quaternion.Euler(0f, -Mathf.Repeat(tt.tableRatio * u + tt.tablePhase, 360f), 0f);
        for (int i = 0; i < tt.gears.Length; i++) tt.gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(tt.gearRatio[i] * u + tt.gearPhase[i], 360f), 0f);
    }

    [MenuItem("FuwaCourse/ステージ2/かみ合う2枚の回転床のお試しを消す")]
    public static void Remove() { var o = GameObject.Find("TwinTableTest"); if (o) Object.DestroyImmediate(o); }

    static void Deck(Transform table, float r, Material m)
    {
        var deck = Prim(PrimitiveType.Cylinder, table, "Deck", new Vector3(0f, -0.2f, 0f), new Vector3(2f * r, 0.2f, 2f * r), m);
        deck.gameObject.layer = 11;
        var mc = deck.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = deck.GetComponent<MeshFilter>().sharedMesh;
    }

    // 橋：根元は橋のかどが床のふちの円に乗る所から（すき間なし）。重なる所は 1cm 上げて Zファイトを避ける
    static void Bridge(Transform table, float ang, float deckR, float tip, Material wood, Material lineM)
    {
        float r0 = Mathf.Sqrt(deckR * deckR - BridgeW * BridgeW / 4f), len = tip - r0;
        var br = new GameObject("Bridge" + Mathf.RoundToInt(Mathf.Repeat(ang, 360f))).transform; br.SetParent(table, false);
        br.localPosition = Polar(ang, r0 + len / 2f) + new Vector3(0f, -0.19f, 0f);
        br.localRotation = Quaternion.Euler(0f, -ang, 0f);
        br.gameObject.layer = 11;
        Prim(PrimitiveType.Cube, br, "Visual", Vector3.zero, new Vector3(len, 0.4f, BridgeW), wood);
        br.gameObject.AddComponent<BoxCollider>().size = new Vector3(len, 0.4f, BridgeW);
        int nk = Mathf.Max(2, Mathf.RoundToInt(len / 0.8f));
        for (int k = 1; k < nk; k++) Prim(PrimitiveType.Cube, br, "Plank" + k, new Vector3(-len / 2f + k * len / nk, 0.205f, 0f), new Vector3(0.05f, 0.01f, BridgeW), lineM);
    }

    // ふちの下の大きい歯車と、床とつなぐ胴
    static void Ring(Transform table, Mesh mesh, Material m, float deckR, Material skirtM)
    {
        var ring = new GameObject("Ring").transform; ring.SetParent(table, false); ring.localPosition = new Vector3(0f, GearY, 0f);
        ring.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; ring.gameObject.AddComponent<MeshRenderer>().sharedMaterial = m;
        float sd = 2f * deckR - 1f;
        Prim(PrimitiveType.Cylinder, table, "Skirt", new Vector3(0f, (GearY + GearTh - 0.4f) / 2f, 0f), new Vector3(sd, (-0.4f - GearY - GearTh) / 2f + 0.02f, sd), skirtM);
    }

    static Vector3 Polar(float deg, float r) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); }

    static void Floor(Transform root, string name, Vector3 c, float deg, Vector2 size, Material m)
    {
        var g = new GameObject(name); g.transform.SetParent(root, false); g.layer = 11;
        g.transform.localPosition = c + new Vector3(0f, -0.2f, 0f); g.transform.localRotation = Quaternion.Euler(0f, -deg, 0f);
        Prim(PrimitiveType.Cube, g.transform, "Visual", Vector3.zero, new Vector3(size.x, 0.4f, size.y), m);
        g.AddComponent<BoxCollider>().size = new Vector3(size.x, 0.4f, size.y);
    }

    static Transform Prim(PrimitiveType t, Transform parent, string name, Vector3 lp, Vector3 ls, Material m)
    {
        var g = GameObject.CreatePrimitive(t);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = name; g.transform.SetParent(parent, false);
        g.transform.localPosition = lp; g.transform.localScale = ls;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        return g.transform;
    }
}
