using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2–4：かみ合う2枚の回転床のお試し（2026-11-07 てじゃ OK。コース案の ⑤ 山場）
// 2026-11-07 てじゃ「合うタイミングすくなすぎる」（A 72・B 48、A の橋2本で羽根車6回転に1回）→ 案2：
//  A の橋を十字の4本（90° ごとにかごとつながる）、B を 36枚（2倍速）。A が 90° 回ると B は 180° → 2回に1回全部つながる（羽根車 1.5 回転に1回）
//  中心距離が 10.8m に縮むので床を小さく（A 4.2・B 3.4、床のすき間 3.2m）。以下のコメントの数字は前の版のもの
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
    // 2026-11-07 てじゃ「小さい方、橋あってなくてもジャンプで届いちゃう」→ すき間を全部 3.2m 以上（走ってジャンプで約 2.5m）に。
    //  B の橋を長くするには中心距離が要るので、歯の大きさ 0.2 → 0.26（中心距離 10.8 → 14.04m）。A の橋もかごまでのすき間 3.2m になる長さに
    const float Mod = 0.26f; const int ZA = 72, ZB = 36, ZI = 16, ZP = 16, NI = 1;   // 羽根車の小歯車 24 → 16（大きくなった歯車でかごの外へはみ出さないように。羽根車1回転で A 80°）
    const float DeckA = 4.2f, DeckB = 3.4f, TipA = 7.24f, TipB = 6.43f, BridgeW = 2.5f;
    const float GearY = -1.85f, GearTh = 0.35f, LineDeg = 169.9f, Layer2 = -1.3f;   // Layer2：小歯車の段（A の輪の上、2026-11-10）
    // 2026-11-07 てじゃ「前が見える位置で羽まわしたい」→ 羽根車の小歯車を A の歯車に直接かませて、かごの口の 3m 手前・まん中から 1.6m 横に   // 2026-11-07 てじゃ「下の歯車が床にみえちゃう」→ 床の 1.5m 下へ、中を抜いた輪＋スポークに
    public const float ExitDeg = -60f;         // B から見た出口の向き（2026-11-07 2–4 の右回りに合わせて 60 → −60。B の橋も左右反転）
    static readonly Vector2 PinTarget = new Vector2(-8.4f, 1.6f);   // 羽根車の小歯車の場所（2026-11-10 かごの口寄りに。口 x=-7.4、羽根の当たり半径 1）
    const float CageHalfZ = 3.0f, CageLen = 7f;   // 2026-11-07「小回りに」10.4 → 7   // 羽根車をまん中から 2m 横に（ふわふわを送る風の線と羽根の当たりを離す）
    static readonly Vector3 Origin = new Vector3(30f, -162f, -165f);   // A のまん中、床の上の高さ

    static float CornerR(float tip) => Mathf.Sqrt(tip * tip + BridgeW * BridgeW / 4f);
    // コース（FuwaCourse2_4Builder）用：かごの奥（入口）の x、B のまん中の x、B から出口のはしまで
    public static float EntryX => -(CornerR(TipA) + 0.05f) - CageLen;
    public static float BCenterX => Mod * (ZA + ZB) / 2f;
    public static float ExitFar => CornerR(TipB) + 0.05f + 6f;

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
        // 2026-11-10 てじゃ「橋とあわせて道みたいな模様にしたらわかりやすいかも」→ 橋と床の上の道を 2–4 の道と同じシマに
        var roadA = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_4_PathA.mat") ?? FuwaGearTest.Mat("TwinTableTest_RoadA", new Color(0.60f, 0.56f, 0.52f));
        var roadB = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_4_PathB.mat") ?? FuwaGearTest.Mat("TwinTableTest_RoadB", new Color(0.40f, 0.36f, 0.33f));
        var deckDarkM = FuwaGearTest.Mat("TwinTableTest_DeckADark", new Color(0.56f, 0.58f, 0.65f));   // 扇形の塗り分け（2026-11-10）
        var deckBDarkM = FuwaGearTest.Mat("TwinTableTest_DeckBDark", new Color(0.64f, 0.58f, 0.49f));

        var root = new GameObject("TwinTableTest").transform; root.position = Origin;

        // ── 歯車のつながり（A を元に）──
        var gA = new FuwaGearTest.Gear { name = "RingA", z = ZA, c = Vector2.zero, ratio = 1f, phase = 0f };
        var gB = FuwaGearTest.Mesh2(gA, "RingB", ZB, 0f, Mod);
        float D = gB.c.x;
        if (CornerR(TipA) + CornerR(TipB) >= D) Debug.LogError("[2枚の回転床] 橋がぶつかる");
        // 2026-11-10 てじゃ「羽ももっと奥寄りになるようにギア追加とかで調整できないかな」（かごを高くしたら、羽根車が坂のてっぺんのすぐ先になった）
        //  → A と羽根車の小歯車の間に歯車を1枚（ZI）。小歯車を PinTarget（かごの口寄り）に置けるよう、間の歯車の向きを2つの円の交点で決める
        var chain = new FuwaGearTest.Gear[NI + 1];
        float RI = Mod * (ZA + ZI) / 2f, dIP = Mod * (ZI + ZP) / 2f, tl = PinTarget.magnitude;
        float tAng = Mathf.Atan2(PinTarget.y, PinTarget.x), phi = Mathf.Acos(Mathf.Clamp((RI * RI + tl * tl - dIP * dIP) / (2f * RI * tl), -1f, 1f));
        Vector2 Pt(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * RI;
        float iAng = Mathf.Abs(Pt(tAng + phi).y) < Mathf.Abs(Pt(tAng - phi).y) ? tAng + phi : tAng - phi;   // かごの中（z が小さい方）
        var gI = FuwaGearTest.Mesh2(gA, "Idler1_z" + ZI, ZI, iAng * Mathf.Rad2Deg, Mod);
        var toP = PinTarget - gI.c;
        var gP = FuwaGearTest.Mesh2(gI, "Pinion_z" + ZP, ZP, Mathf.Atan2(toP.y, toP.x) * Mathf.Rad2Deg, Mod);
        chain[0] = gI; chain[NI] = gP;
        float rp = gP.ratio, php = gP.phase * Mathf.Rad2Deg;   // 小歯車の角度 = rp·v + php（v = A の角度）

        // ── 床（かご・出口）──
        float x1 = -(CornerR(TipA) + 0.05f), x0 = x1 - CageLen;   // かごの床：A の橋のかどが回る円の外で切る
        Floor(root, "CageFloor", new Vector3((x0 + x1) / 2f, 0f, 0f), 0f, new Vector2(x1 - x0, 2f * CageHalfZ), floorM);
        Vector3 cB = new Vector3(D, 0f, 0f);
        float exitNear = CornerR(TipB) + 0.05f;
        Floor(root, "Exit", cB + Polar(ExitDeg, exitNear + 3f), ExitDeg, new Vector2(6f, 4f), floorM);   // 2026-11-10 幅を道と同じ 4m に（前は 6m）

        // ── 回転床 A ──
        var tableA = new GameObject("TableA").transform; tableA.SetParent(root, false);
        Deck(tableA, DeckA, deckM, deckDarkM, brass, 0f);
        var angsA = new[] { 0f, 90f, 180f, 270f }; plazaR = RoadPlazaR(angsA);
        foreach (float ang in angsA) Bridge(tableA, ang, DeckA, TipA, roadA, roadB, RoadArmStripes(DeckA));
        Road(tableA, DeckA, angsA, roadA, roadB);
        var ringAMesh = FuwaGearTest.SaveMesh(RimGear(ZA, Mod, GearTh, 0.5f, "TwinTable_Ring_z" + ZA));
        Ring(tableA, ringAMesh, brass, RimIn(ZA), dark);
        Prim(PrimitiveType.Cylinder, root, "PedestalA", new Vector3(0f, GearY - 0.92f, 0f), new Vector3(1.6f, 0.9f, 1.6f), dark);

        // ── 回転床 B（A とかみ合う。gears に入れて回す）──
        var frB = new GameObject("TableB_Frame").transform; frB.SetParent(root, false); frB.localPosition = cB;
        var tableB = new GameObject("TableB").transform; tableB.SetParent(frB, false);
        // 最初（u = startU）の B の歯車の角度 θB0 = gB.phase。橋は世界で {0°, 240°}（答えの {180°, 60°} から 180° ずれ）に向くよう local を決める
        float thB0 = gB.phase * Mathf.Rad2Deg;
        Deck(tableB, DeckB, deckBM, deckBDarkM, brass, -thB0);
        var angsB = new[] { 0f - thB0, ExitDeg + 180f - thB0 };   // 答えの {180°, ExitDeg} から 180° ずれ
        plazaR = RoadPlazaR(angsB);
        foreach (float ang in angsB) Bridge(tableB, ang, DeckB, TipB, roadA, roadB, RoadArmStripes(DeckB));
        Road(tableB, DeckB, angsB, roadA, roadB);
        var ringBMesh = FuwaGearTest.SaveMesh(RimGear(ZB, Mod, GearTh, 0.5f, "TwinTable_Ring_z" + ZB));
        Ring(tableB, ringBMesh, light, RimIn(ZB), dark);
        Prim(PrimitiveType.Cylinder, root, "PedestalB", cB + new Vector3(0f, GearY - 0.92f, 0f), new Vector3(1.6f, 0.9f, 1.6f), dark);

        // ── 床下の歯車の列（羽根車の小歯車の角度 u を元に）──
        int nc = chain.Length;
        var gearTs = new Transform[nc + 1]; var gearRatio = new float[nc + 1]; var gearPhase = new float[nc + 1];
        var meshI = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZI, 8, Mod), GearTh, "TurnTable_Idler_z" + ZI, Vector2.zero));
        var meshP = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZP, 8, Mod), GearTh, "TwinTable_Pinion_z" + ZP, Vector2.zero));
        float beamY = GearY - 0.35f;
        for (int i = 0; i < nc; i++)
        {
            var g = chain[i];
            // 2026-11-10 てじゃ「かさなってるよ」：小歯車を A の輪の内がわに寄せたので、同じ高さだと輪と重なる → 小歯車は輪の上の段（Layer2）。
            //  間の歯車は同じ軸に2段（下が A の輪と、上が小歯車とかみ合う。同じ形・同じ向きなので計算は1枚と同じ）。小歯車の軸は輪をよけて上からつるす
            bool pinion = i == NI;
            var t = new GameObject(g.name).transform; t.SetParent(root, false); t.localPosition = new Vector3(g.c.x, pinion ? Layer2 : GearY, g.c.y);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = i < NI ? meshI : meshP;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = i % 2 == 1 ? light : dark;
            if (!pinion)
            {
                var up = new GameObject(g.name + "_Upper").transform; up.SetParent(t, false); up.localPosition = new Vector3(0f, Layer2 - GearY, 0f);
                up.gameObject.AddComponent<MeshFilter>().sharedMesh = meshI; up.gameObject.AddComponent<MeshRenderer>().sharedMaterial = i % 2 == 1 ? light : dark;
            }
            gearTs[i] = t;
            gearRatio[i] = g.ratio / rp;
            gearPhase[i] = g.phase * Mathf.Rad2Deg - g.ratio * php / rp;
            float top = pinion ? 1.05f : Layer2 + GearTh + 0.1f, bot = pinion ? Layer2 - 0.1f : beamY;
            Prim(PrimitiveType.Cylinder, root, "Axle_" + g.name, new Vector3(g.c.x, (bot + top) / 2f, g.c.y), new Vector3(0.45f, (top - bot) / 2f, 0.45f), axleM);
        }
        gearTs[nc] = tableB; gearRatio[nc] = gB.ratio / rp; gearPhase[nc] = thB0 - gB.ratio * php / rp;
        // 軸を受ける梁：A の台の柱 → 間の歯車 → 小歯車
        void BeamTo(string n, Vector2 a, Vector2 b)
        {
            var d = b - a; float l = d.magnitude;
            var bm = Prim(PrimitiveType.Cube, root, n, new Vector3((a.x + b.x) / 2f, beamY - 0.1f, (a.y + b.y) / 2f), new Vector3(l + 0.4f, 0.2f, 0.4f), light);
            bm.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f);
        }
        BeamTo("Beam", gI.c.normalized * 1f, gI.c);   // 小歯車の軸は上の床からつるす（下の梁は輪とぶつかるのでなし）

        // ── 羽根車 ──
        var pinT = gearTs[NI];
        var handle = new GameObject("Handle").transform; handle.SetParent(pinT, false); handle.localPosition = new Vector3(0f, 1.05f - Layer2, 0f);
        handle.gameObject.layer = 2;
        FuwaTurnTableTest.Impeller(handle, brass);
        var sc = handle.gameObject.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 0.8f;

        // ── かごの金網（橋の口だけあける）──
        float endZ = CageHalfZ - 0.08f, brZ = BridgeW / 2f + 0.15f;
        FuwaTurnTableTest.WireFence(root, new Vector3((x0 + x1) / 2f, 0f, CageHalfZ), 90f, x1 - x0, 2.2f, light, axleM).name = "Fence_N";
        FuwaTurnTableTest.WireFence(root, new Vector3((x0 + x1) / 2f, 0f, -CageHalfZ), 90f, x1 - x0, 2.2f, light, axleM).name = "Fence_S";
        // 入口（かごの奥、2026-11-07 2–4 のコースに並べる時の入口）：まん中 ±1.5m をあける（回転床のかごと同じ）
        foreach (int sg in new[] { 1, -1 })
            FuwaTurnTableTest.WireFence(root, new Vector3(x0, 0f, sg * (1.5f + endZ) / 2f), 0f, endZ - 1.5f, 2.2f, light, axleM).name = "Fence_In" + (sg > 0 ? "N" : "S");
        foreach (int sg in new[] { 1, -1 })
            FuwaTurnTableTest.WireFence(root, new Vector3(x1, 0f, sg * (brZ + endZ) / 2f), 0f, endZ - brZ, 2.2f, light, axleM).name = "Fence_Br" + (sg > 0 ? "N" : "S");

        // ── 動かすしくみ ──
        var tt = UdonSharpUndo.AddComponent<FuwaTurnTable>(handle.gameObject);
        tt.table = tableA; tt.tableRatio = 1f / rp; tt.tablePhase = -php / rp;
        tt.gears = gearTs; tt.gearRatio = gearRatio; tt.gearPhase = gearPhase;
        tt.handle = handle; tt.hitRadius = 1.0f; tt.hitHalfHeight = 0.55f; tt.hitCone = 4f; tt.startU = php;   // u = php で A が 0°。当たり 2026-11-07 てじゃ「狭くなりすぎてる」0.8・0.3・3° → 1.0・0.55・4°
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

        RaiseCage(root);   // 2026-11-10 かごを高くして、上から橋が見えるように（門と信号ランプは「くどい」でやめた）

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

    // 2026-11-10 てじゃ「回転橋の丸い床、模様ほしい」→ なめらかな丸（DeckSeg 角）に、まん中の金の丸・こい色の細い輪・扇形 DeckSectors 枚の2色・ふち近くの金の細い輪。
    //  模様は同じ面の中で三角形ごとに材質を分ける（重ねないので Zファイトなし）。扇形のまん中が橋の向き（bridgeAng）
    public static int DeckSeg = 64, DeckSectors = 8;
    static void Deck(Transform table, float r, Material m, Material mDark, Material brass, float bridgeAng)
    {
        var mesh = FuwaGearRideTest.SaveMeshSub(DeckMesh(r, 0.4f, bridgeAng, "TwinTable_Deck_r" + Mathf.RoundToInt(r * 100) + "_a" + Mathf.RoundToInt(Mathf.Repeat(bridgeAng, 360f))));
        var deck = new GameObject("Deck"); deck.transform.SetParent(table, false); deck.layer = 11;
        deck.AddComponent<MeshFilter>().sharedMesh = mesh; deck.AddComponent<MeshRenderer>().sharedMaterials = new[] { m, mDark, brass };
        var mc = deck.AddComponent<MeshCollider>(); mc.sharedMesh = mesh;
    }
    // 上の面 y=0、下の面 y=-h。サブメッシュ 0=床の色（扇形の半分・ふち・横・底）1=こい色 2=金
    static Mesh DeckMesh(float r, float h, float bridgeAng, string name)
    {
        int n = DeckSeg, per = n / DeckSectors;
        float[] rad = { 0.15f * r, 0.19f * r, 0.85f * r, 0.875f * r, r };
        var v = new System.Collections.Generic.List<Vector3>(); var nr = new System.Collections.Generic.List<Vector3>();
        var t = new[] { new System.Collections.Generic.List<int>(), new System.Collections.Generic.List<int>(), new System.Collections.Generic.List<int>() };
        float a0 = bridgeAng * Mathf.Deg2Rad - Mathf.PI / DeckSectors;   // 扇形のまん中が橋の向き（Polar と同じ向きの取り方）
        Vector3 Dir(int j) { float a = a0 + 2f * Mathf.PI * j / n; return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }
        // 上の面
        int c0 = v.Count; v.Add(Vector3.zero); nr.Add(Vector3.up);
        int ring0 = v.Count;
        for (int i = 0; i < rad.Length; i++) for (int j = 0; j < n; j++) { v.Add(Dir(j) * rad[i]); nr.Add(Vector3.up); }
        int Idx(int i, int j) => ring0 + i * n + (j % n);
        for (int j = 0; j < n; j++) { t[2].Add(c0); t[2].Add(Idx(0, j + 1)); t[2].Add(Idx(0, j)); }
        for (int i = 0; i < rad.Length - 1; i++)
            for (int j = 0; j < n; j++)
            {
                int sm = i == 2 ? 2 : 0;   // 扇形の塗り分けは道に変えたのでなし（ふち近くの金の輪だけ）
                int a = Idx(i, j), b = Idx(i, j + 1), c = Idx(i + 1, j + 1), d = Idx(i + 1, j);
                t[sm].Add(a); t[sm].Add(b); t[sm].Add(c); t[sm].Add(a); t[sm].Add(c); t[sm].Add(d);
            }
        // 横
        int s0 = v.Count;
        for (int j = 0; j <= n; j++) { var d = Dir(j); v.Add(d * r); nr.Add(d); v.Add(d * r + Vector3.down * h); nr.Add(d); }
        for (int j = 0; j < n; j++) { int a = s0 + 2 * j; t[0].Add(a); t[0].Add(a + 2); t[0].Add(a + 3); t[0].Add(a); t[0].Add(a + 3); t[0].Add(a + 1); }
        // 底
        int b0 = v.Count; v.Add(Vector3.down * h); nr.Add(Vector3.down);
        for (int j = 0; j < n; j++) { v.Add(Dir(j) * r + Vector3.down * h); nr.Add(Vector3.down); }
        for (int j = 0; j < n; j++) { t[0].Add(b0); t[0].Add(b0 + 1 + j); t[0].Add(b0 + 1 + (j + 1) % n); }
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetNormals(nr); m.subMeshCount = 3;
        for (int k = 0; k < 3; k++) m.SetTriangles(t[k], k);
        m.RecalculateBounds();
        // 表の向きのチェック（Cross(b-a,c-a) が法線と同じ向き）
        for (int k = 0; k < 3; k++) { var tr = t[k]; for (int q = 0; q < tr.Count; q += 3) { var cr = Vector3.Cross(v[tr[q + 1]] - v[tr[q]], v[tr[q + 2]] - v[tr[q]]); if (Vector3.Dot(cr, nr[tr[q]]) < 0f) { Debug.LogError("[回転床] 床の三角形が裏返り " + name); k = 3; break; } } }
        return m;
    }

    // 橋：根元は橋のかどが床のふちの円に乗る所から（すき間なし）。重なる所は 1cm 上げて Zファイトを避ける
    // 橋（2026-11-10 木の板 → 道と同じシマ。床の上の道の最後のシマと色が交互につづく）
    static void Bridge(Transform table, float ang, float deckR, float tip, Material roadA, Material roadB, int armStripes)
    {
        float r0 = Mathf.Sqrt(deckR * deckR - BridgeW * BridgeW / 4f), len = tip - r0;
        var br = new GameObject("Bridge" + Mathf.RoundToInt(Mathf.Repeat(ang, 360f))).transform; br.SetParent(table, false);
        br.localPosition = Polar(ang, r0 + len / 2f) + new Vector3(0f, -0.19f, 0f);
        br.localRotation = Quaternion.Euler(0f, -ang, 0f);
        br.gameObject.layer = 11;
        br.gameObject.AddComponent<BoxCollider>().size = new Vector3(len, 0.4f, BridgeW);
        int n = Mathf.Max(1, Mathf.RoundToInt(len / RoadStripe));
        bool lastArmA = (armStripes - 1) % 2 == 0;
        for (int k = 0; k < n; k++)
        {
            bool isA = (k % 2 == 0) != lastArmA;
            Prim(PrimitiveType.Cube, br, "Visual" + k, new Vector3(-len / 2f + len * (k + 0.5f) / n, 0f, 0f), new Vector3(len / n, 0.4f, BridgeW), isA ? roadA : roadB);
        }
    }

    // 床の上の道（橋と橋をまん中の丸い広場でつなぐ）。床の上の面から RoadTh だけ上（＝橋の上の面と同じ高さ）。見た目だけ（当たりは床）
    //  広場の半径は、となりの腕どうしが重ならない大きさ（w / sin(間の角/2) 以上）。腕は広場の円から、橋の根元（弦 r0）まで
    public static float RoadStripe = 1.2f, RoadTh = 0.01f;
    static float plazaR;
    static float RoadPlazaR(float[] angs)
    {
        float w = BridgeW / 2f, minGap = 180f;
        var a = angs.Select(x => Mathf.Repeat(x, 360f)).OrderBy(x => x).ToArray();
        if (a.Length >= 2) { minGap = 360f; for (int i = 0; i < a.Length; i++) { float g = (i + 1 < a.Length ? a[i + 1] : a[0] + 360f) - a[i]; minGap = Mathf.Min(minGap, g); } }
        return Mathf.Max(w * 1.15f, w / Mathf.Sin(Mathf.Min(minGap, 180f) * 0.5f * Mathf.Deg2Rad) + 0.15f);
    }
    static int RoadArmStripes(float deckR)
    {
        float r0 = Mathf.Sqrt(deckR * deckR - BridgeW * BridgeW / 4f);
        return Mathf.Max(1, Mathf.RoundToInt((r0 - plazaR) / RoadStripe));
    }
    static void Road(Transform table, float deckR, float[] angs, Material roadA, Material roadB)
    {
        float w = BridgeW / 2f, r0 = Mathf.Sqrt(deckR * deckR - w * w), rho = plazaR, h = RoadTh;
        var v = new System.Collections.Generic.List<Vector3>(); var nr = new System.Collections.Generic.List<Vector3>();
        var t = new[] { new System.Collections.Generic.List<int>(), new System.Collections.Generic.List<int>() };
        void Tri(int sm, int a, int b, int c, Vector3 nrm)
        {
            if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), nrm) >= 0f) { t[sm].Add(a); t[sm].Add(b); t[sm].Add(c); }
            else { t[sm].Add(a); t[sm].Add(c); t[sm].Add(b); }
        }
        void Quad(int sm, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nrm)
        {
            int i0 = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); for (int q = 0; q < 4; q++) nr.Add(nrm);
            Tri(sm, i0, i0 + 1, i0 + 2, nrm); Tri(sm, i0, i0 + 2, i0 + 3, nrm);
        }
        var up = Vector3.up * h;
        int seg = DeckSeg;
        // 広場（こい色）
        {
            int c0 = v.Count; v.Add(up); nr.Add(Vector3.up);
            for (int j = 0; j < seg; j++) { float a = 2f * Mathf.PI * j / seg; v.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rho + up); nr.Add(Vector3.up); }
            for (int j = 0; j < seg; j++) Tri(1, c0, c0 + 1 + j, c0 + 1 + (j + 1) % seg, Vector3.up);
            for (int j = 0; j < seg; j++)
            {
                float a0 = 2f * Mathf.PI * j / seg, a1 = 2f * Mathf.PI * (j + 1) / seg, am = (a0 + a1) / 2f;
                var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * rho; var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * rho;
                Quad(1, p0, p1, p1 + up, p0 + up, new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am)));
            }
        }
        // 腕（橋の向き）：広場の円 → 弦 r0。進む向きに A/B（広場のとなりは A）
        int n = RoadArmStripes(deckR); const int m = 8;
        foreach (float ang in angs)
        {
            var d = Polar(ang, 1f); var e = new Vector3(-d.z, 0f, d.x);
            float XIn(float y) => Mathf.Sqrt(Mathf.Max(0f, rho * rho - y * y));
            Vector3 P(float y, float f) { float x = Mathf.Lerp(XIn(y), r0, f); return d * x + e * y; }
            for (int k = 0; k < n; k++)
            {
                int sm = k % 2 == 0 ? 0 : 1;
                float fa = (float)k / n, fb = (float)(k + 1) / n;
                for (int q = 0; q < m; q++)
                {
                    float y0 = -w + 2f * w * q / m, y1 = -w + 2f * w * (q + 1) / m;
                    Quad(sm, P(y0, fa) + up, P(y1, fa) + up, P(y1, fb) + up, P(y0, fb) + up, Vector3.up);
                }
                Quad(sm, P(-w, fa), P(-w, fb), P(-w, fb) + up, P(-w, fa) + up, -e);
                Quad(sm, P(w, fa), P(w, fb), P(w, fb) + up, P(w, fa) + up, e);
            }
        }
        var mesh = new Mesh { name = "TwinTable_Road_r" + Mathf.RoundToInt(deckR * 100) + "_" + string.Join("_", angs.Select(x => Mathf.RoundToInt(Mathf.Repeat(x, 360f)).ToString())) };
        mesh.SetVertices(v); mesh.SetNormals(nr); mesh.subMeshCount = 2; mesh.SetTriangles(t[0], 0); mesh.SetTriangles(t[1], 1); mesh.RecalculateBounds();
        mesh = FuwaGearRideTest.SaveMeshSub(mesh);
        var go = new GameObject("Road"); go.transform.SetParent(table, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterials = new[] { roadA, roadB };
    }

    // ===== 橋が見えやすいように（2026-11-10 てじゃ「横から見ると遠く全然みえない、合ってるかもわからん」）=====
    //  門と信号ランプは「くどいかも」→ やめて「羽のある台を高くして、すこし降りて橋にのるかんじ」：
    //  かごの床を CageRise 上げる（入口から CageRamp m の坂で上がる）。かごの口から橋へ CageRise だけ飛びおりる。羽根車もいっしょに上げる
    public static float CageRise = 1.2f, CageRamp = 3f, ApronLen = 1.6f, ApronEndY = 0.4f, ApronTh = 0.2f;
    public static string RaiseCage(Transform root)
    {
        var cf = root.Find("CageFloor"); if (!cf) return "かごの床がない";
        foreach (var x in root.Cast<Transform>().Where(x => x.name == "CageRamp").ToArray()) Object.DestroyImmediate(x.gameObject);
        float x1 = -(CornerR(TipA) + 0.05f), x0 = x1 - CageLen, xr = x0 + CageRamp, H = CageRise;
        var floorM = cf.Find("Visual").GetComponent<Renderer>().sharedMaterial;
        // 平らな所（坂の上〜かごの口）
        cf.localPosition = new Vector3((xr + x1) / 2f, H - 0.2f, 0f);
        cf.Find("Visual").localScale = new Vector3(x1 - xr, 0.4f, 2f * CageHalfZ);
        cf.GetComponent<BoxCollider>().size = new Vector3(x1 - xr, 0.4f, 2f * CageHalfZ);
        // 坂（入口の高さ 0 → H）
        float L = Mathf.Sqrt(CageRamp * CageRamp + H * H), ang = Mathf.Atan2(H, CageRamp) * Mathf.Rad2Deg;
        var rp = new GameObject("CageRamp").transform; rp.SetParent(root, false); rp.gameObject.layer = 11;
        rp.localRotation = Quaternion.Euler(0f, 0f, ang);
        rp.localPosition = new Vector3((x0 + xr) / 2f, H / 2f, 0f) + rp.localRotation * new Vector3(0f, -0.2f, 0f);
        Prim(PrimitiveType.Cube, rp, "Visual", Vector3.zero, new Vector3(L + 0.02f, 0.4f, 2f * CageHalfZ), floorM);
        rp.gameObject.AddComponent<BoxCollider>().size = new Vector3(L + 0.02f, 0.4f, 2f * CageHalfZ);
        // 平らな所と坂のつなぎ目の下の すき間：坂の上のはしは平らな所の下にもぐる（L+0.02）
        // 降りる時の下り坂（2026-11-10 てじゃ「降りる時用のスロープほしい」の絵）：かごの口から外へ、回ってくる橋の上に張り出す。
        //  先は橋の上 ApronEndY で止める（橋はその下をくぐる）
        foreach (var x in root.Cast<Transform>().Where(x => x.name == "CageApron").ToArray()) Object.DestroyImmediate(x.gameObject);
        {
            float ax0 = x1, ax1 = x1 + ApronLen, dy = H - ApronEndY, la = Mathf.Sqrt(ApronLen * ApronLen + dy * dy), aa = -Mathf.Atan2(dy, ApronLen) * Mathf.Rad2Deg;
            var ap = new GameObject("CageApron").transform; ap.SetParent(root, false); ap.gameObject.layer = 11;
            ap.localRotation = Quaternion.Euler(0f, 0f, aa);
            ap.localPosition = new Vector3((ax0 + ax1) / 2f, (H + ApronEndY) / 2f, 0f) + ap.localRotation * new Vector3(0f, -ApronTh / 2f, 0f);
            Prim(PrimitiveType.Cube, ap, "Visual", Vector3.zero, new Vector3(la + 0.1f, ApronTh, BridgeW), floorM);
            ap.gameObject.AddComponent<BoxCollider>().size = new Vector3(la + 0.1f, ApronTh, BridgeW);
        }
        // 金網を背の分だけのばす（下は 0 のまま）
        foreach (var f in root.Cast<Transform>().Where(x => x.name.StartsWith("Fence_")))
            f.localScale = new Vector3(1f, (2.2f + H) / 2.2f, 1f);
        // 羽根車と小歯車の軸を上げる
        var pin = root.Cast<Transform>().FirstOrDefault(x => x.name.StartsWith("Pinion_z"));
        var handle = pin ? pin.Find("Handle") : null;
        if (handle) handle.localPosition = new Vector3(0f, 1.05f + H - pin.localPosition.y, 0f);
        var axle = root.Cast<Transform>().FirstOrDefault(x => x.name.StartsWith("Axle_Pinion"));
        if (axle && pin) { float beamY = pin.localPosition.y > GearY + 0.01f ? pin.localPosition.y - 0.1f : GearY - 0.35f, top = 1.05f + H; axle.localPosition = new Vector3(axle.localPosition.x, (beamY + top) / 2f, axle.localPosition.z); axle.localScale = new Vector3(axle.localScale.x, (top - beamY) / 2f, axle.localScale.z); }
        // お試し用の物（コースにはない）も上げる
        foreach (var n in new[] { "BallSpot", "BallStand", "Spawn" }) { var t = root.Find(n); if (t && t.localPosition.x > xr - 0.5f) t.localPosition += Vector3.up * H; }
        return "かご +" + H + "m 坂 " + CageRamp + "m";
    }

    static float RimIn(int z) => Mod * z / 2f - 1.25f * Mod - 0.5f;   // 輪の内側の半径（歯元から 0.5m）

    // ふちの下の大きい歯車（輪＋スポーク6本＋まん中のこしき）と、床とつなぐ細い柱
    static void Ring(Transform table, Mesh mesh, Material m, float rin, Material colM)
    {
        var ring = new GameObject("Ring").transform; ring.SetParent(table, false); ring.localPosition = new Vector3(0f, GearY, 0f);
        ring.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; ring.gameObject.AddComponent<MeshRenderer>().sharedMaterial = m;
        const float hubR = 0.8f;
        Prim(PrimitiveType.Cylinder, ring, "Hub", new Vector3(0f, GearTh / 2f, 0f), new Vector3(2f * hubR, GearTh / 2f, 2f * hubR), m);
        for (int k = 0; k < 6; k++)
        {
            float l = rin + 0.05f - (hubR - 0.05f);
            var sp = Prim(PrimitiveType.Cube, ring, "Spoke" + k, Quaternion.Euler(0f, k * 60f + 30f, 0f) * new Vector3(hubR - 0.05f + l / 2f, GearTh / 2f, 0f), new Vector3(l, GearTh * 0.8f, 0.35f), m);
            sp.localRotation = Quaternion.Euler(0f, k * 60f + 30f, 0f);
        }
        float top = GearY + GearTh;
        Prim(PrimitiveType.Cylinder, table, "Column", new Vector3(0f, (top - 0.4f) / 2f, 0f), new Vector3(1.1f, (-0.4f - top) / 2f + 0.02f, 1.1f), colM);
    }

    // 歯のついた輪（内側は半径 RimIn の円）。歯の輪郭の点ごとに同じ角度の内側の点と組んで帯にする
    // 輪っかの歯車（2026-11-07 軽くした：内がわの輪は歯1枚あたり4点、歯の側面はなめらかな所で点を共有）
    static Mesh RimGear(int z, float m, float th, float rimW, string name)
    {
        var o = FuwaGearTest.GearOutline(z, 8, m);
        float rin = m * z / 2f - 1.25f * m - rimW;
        int n = o.Length;
        float area = 0f; for (int i = 0; i < n; i++) { var p0 = o[i]; var p1 = o[(i + 1) % n]; area += p0.x * p1.y - p1.x * p0.y; }
        float ori = area > 0f ? 1f : -1f;
        int K = Mathf.Max(1, n / (z * 4)); int M = (n + K - 1) / K;
        var dIn = new Vector2[M]; for (int k = 0; k < M; k++) dIn[k] = o[k * K].normalized;
        var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
        System.Action<int, int, int, Vector3> T = (ia, ib, ic, want) =>
        {
            if (Vector3.Dot(Vector3.Cross(v[ib] - v[ia], v[ic] - v[ia]), want) < 0f) { int sw = ib; ib = ic; ic = sw; }
            t.Add(ia); t.Add(ib); t.Add(ic);
        };
        foreach (float y in new[] { th, 0f })
        {
            var want = y > 0f ? Vector3.up : Vector3.down;
            int bo = v.Count; for (int i = 0; i < n; i++) v.Add(new Vector3(o[i].x, y, o[i].y));
            int bi = v.Count; for (int k = 0; k < M; k++) v.Add(new Vector3(dIn[k].x * rin, y, dIn[k].y * rin));
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n, gi = i / K, gj = j / K;
                T(bi + gi, bo + i, bo + j, want);
                if (gj != gi) T(bi + gi, bo + j, bi + gj, want);
            }
        }
        // 歯の側面（角は分ける）
        var sA = new int[n]; var sB = new int[n];
        for (int i = 0; i < n; i++)
        {
            var e0 = o[i] - o[(i - 1 + n) % n]; var e1 = o[(i + 1) % n] - o[i];
            sA[i] = v.Count; v.Add(new Vector3(o[i].x, 0f, o[i].y)); v.Add(new Vector3(o[i].x, th, o[i].y));
            if (Vector2.Angle(e0, e1) > 35f) { sB[i] = v.Count; v.Add(new Vector3(o[i].x, 0f, o[i].y)); v.Add(new Vector3(o[i].x, th, o[i].y)); } else sB[i] = sA[i];
        }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n; var e = o[j] - o[i]; var nw = new Vector3(e.y, 0f, -e.x) * ori;
            T(sA[i], sB[j], sB[j] + 1, nw); T(sA[i], sB[j] + 1, sA[i] + 1, nw);
        }
        // 内がわの壁（内向き、なめらか）
        int bw = v.Count; for (int k = 0; k < M; k++) { v.Add(new Vector3(dIn[k].x * rin, 0f, dIn[k].y * rin)); v.Add(new Vector3(dIn[k].x * rin, th, dIn[k].y * rin)); }
        for (int k = 0; k < M; k++)
        {
            int k2 = (k + 1) % M; var sd = dIn[k] + dIn[k2]; var nn = -new Vector3(sd.x, 0f, sd.y);
            T(bw + 2 * k, bw + 2 * k2, bw + 2 * k2 + 1, nn); T(bw + 2 * k, bw + 2 * k2 + 1, bw + 2 * k + 1, nn);
        }
        var mesh = new Mesh { name = name };
        mesh.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
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
