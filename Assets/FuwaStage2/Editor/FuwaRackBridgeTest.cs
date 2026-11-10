using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2–4：撃って回すと谷の両側から橋がせり出すお試し（2026-11-07 てじゃ「歯車の意味は 1 速さ・2 向き・3 遠くへ運ぶ の組み合わせ」→ 案A）
// 2026-11-07 作り直し（てじゃ「大掛かりなっちゃう」「手間だけど両端で伸びるようにして、徐々にもどるように」「カバーもいらんかも」）：
//  谷 Gap m。手前（かご）と向こう岸の床の下に半分ずつの橋（HalfL m）がしまってある。撃つと両側から Gap/2 ずつせり出してまん中で合う
//  橋の上の面は床より BridgeDrop 低い（しまう時に床の板の下にもぐる＝車庫もみぞもいらない）。乗る時に一段おりて、渡ったら一段上がる
//  手前：羽根車の軸の下のピニオン（14枚）→ 手前の橋のラック
//  向こう：羽根車の軸の足元のかさ歯車（12 → 20）→ 谷の下の横の軸 → かさ歯車（20 → 12）→ たての軸のピニオン（14枚）→ 向こうの橋のラック
//   かさ歯車の比は 12/20 × 20/12 = 1、回る向きは逆 → 2本の橋はちょうど同じ速さで逆向き（まん中へ寄る）
//  撃たなくなって holdTime 秒たつと、じわじわ引っこむ（FuwaTurnTable の returnSpeed）
// ・回し方・当たりは回転床と同じ FuwaTurnTable（table なし、rack / rack2 に橋）。行き止まりで止まる
// ・2D の (x, y) は Unity の (x, z)。2D の反時計回り θ は Unity の Y 回転 −θ（FuwaTurnTable と同じ）
// ・かさ歯車：頂点（apex）を2枚で共有。縦の歯車は頂点が下、横の歯車は頂点が軸ぞいに相手の軸の方。歯は頂点に向かって細くなる円すい
public static class FuwaRackBridgeTest
{
    static readonly Vector3 Origin = new Vector3(25f, -162f, -135f);   // かごの床の橋側のはしのまん中、床の上の高さ
    const float CageX0 = -7.4f, CageHalfZ = 3.2f;
    public const float Gap = 6f;                   // 谷（x 0〜Gap）
    public const float FarX1 = 10f, FarZ0 = -5f, FarZ1 = 2f;   // 2026-11-10 てじゃ「幅もあわせて」：出口の道と同じ幅 4m（前は 12 / 3.2 で 6×8.2m）   // 向こう岸の床（x Gap〜FarX1、z FarZ0〜FarZ1）。コースでは −z のはしから出る
    const float BridgeW = 2.5f, HalfL = 3.5f, BridgeT = 0.4f, BridgeDrop = 0.45f, MeetGap = 0.03f;
    const float ZL = -2.2f, YL = -3.1f;            // 横の軸の z と高さ
    const float XW = -1.4f;                        // 羽根車の軸の x（2026-11-07 てじゃ「前が見える位置で羽まわしたい」）
    const float XP = 7.5f;                         // 向こうのピニオンの軸の x（向こう岸の床の下）
    const float MB = 0.15f; const int Z1 = 12, Z2 = 20, ZH = 20, ZV = 12;   // かさ歯車
    const float MP = 0.1f; const int ZPin = 14, RackTeeth = 16;              // ピニオンとラック
    const float GearTh = 0.35f;
    const float HubY = 1.05f;                      // 羽根車の高さ
    const float ShaftX0 = XW - 1.5f, ShaftX1 = XP + 2f;  // 横の軸のはし（軸受け）
    public static float ReturnTime = 12f, HoldTime = 2.5f;   // 全部引っこむまで（秒）・撃ってから戻り始めるまで

    static float BridgeTop => -BridgeDrop;
    static float GearY => BridgeTop - BridgeT - 0.25f - GearTh;   // ピニオン・ラックの下の面（橋の下にラックの板 0.25m）
    static float RP => MP * ZPin / 2f;
    static float KV => -((float)ZH / ZV) * ((float)Z1 / Z2);   // 向こうのピニオンの角度 / 羽根車の角度（= −1）
    static float PerU => RP * Mathf.Deg2Rad;                   // 羽根車 1° で橋が何 m
    static float Travel => Gap / 2f - MeetGap / 2f;            // 片側の橋がせり出す長さ

    [MenuItem("FuwaCourse/ステージ2/橋がせり出すお試しを作る")]
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

        var root = new GameObject("RackBridgeTest").transform; root.position = Origin;

        // ── 床（かご・向こう岸）。橋はこの板の下にしまう ──
        Floor(root, "CageFloor", new Vector3(CageX0 / 2f, 0f, 0f), new Vector2(-CageX0, 2f * CageHalfZ), floorM);
        Floor(root, "FarBank", new Vector3((Gap + FarX1) / 2f, 0f, (FarZ0 + FarZ1) / 2f), new Vector2(FarX1 - Gap, FarZ1 - FarZ0), floorM);

        // ── 半分ずつの橋（ラックつき）。原点＝橋の先（谷のまん中の方のはし）──
        float pitchZ = ZL + RP, hf = 1.25f * MP, p = Mathf.PI * MP;
        var rackMesh = FuwaGearTest.SaveMesh(RackMesh(RackTeeth, MP, 0.3f, GearTh, "RackBridge_Rack_m01"));
        // 引っこんだ時、ピニオンの所がラックの歯のすき間になるように（歯は偶数本で、まん中から p ごとにすき間）
        var near = HalfBridge(root, "BridgeNear", 0f, -1f, XW - 6f * p, pitchZ, hf, rackMesh, wood, axleM, brass, dark);
        var far = HalfBridge(root, "BridgeFar", Gap, 1f, XP - Gap + 6f * p, pitchZ, hf, rackMesh, wood, axleM, brass, dark);

        // ── 歯車 ──
        float d1 = Mathf.Atan2(Z1, Z2), d2 = Mathf.PI / 2f - d1;      // 手前：縦（12）・横（20）
        float dh = Mathf.Atan2(ZH, ZV), dv = Mathf.PI / 2f - dh;      // 向こう：横（20）・縦（12）
        float A1 = MB * Z1 / (2f * Mathf.Sin(d1)), A2 = MB * ZH / (2f * Mathf.Sin(dh));
        float F1 = 0.3f * A1, F2 = 0.3f * A2;
        Vector3 O1 = new Vector3(XW, YL, ZL), O2 = new Vector3(XP, YL, ZL);

        var meshNV = FuwaGearTest.SaveMesh(Bevel(Z1, MB, d1, F1, "RackBridge_Bevel_z12"));
        var meshNH = FuwaGearTest.SaveMesh(Bevel(Z2, MB, d2, F1, "RackBridge_Bevel_z20a"));
        var meshFH = FuwaGearTest.SaveMesh(Bevel(ZH, MB, dh, F2, "RackBridge_Bevel_z20c"));
        var meshFV = FuwaGearTest.SaveMesh(Bevel(ZV, MB, dv, F2, "RackBridge_Bevel_z12b"));
        var meshP = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZPin, 8, MP), GearTh, "RackBridge_Pinion_m01_z14", Vector2.zero));

        // 横の歯車の枠：local Y を軸に。手前は +x（頂点は −x 側）、向こうは −x（頂点は +x 側）
        Quaternion qN = Quaternion.FromToRotation(Vector3.up, Vector3.right), qF = Quaternion.FromToRotation(Vector3.up, Vector3.left);
        float phiN = Phi2D(qN, Vector3.up), phiF = Phi2D(qF, Vector3.up);   // 相手（縦の歯車）とかみ合う所の向き

        var gNV = Gear(root, "BevelNear_z12", O1 + Vector3.up * (A1 * Mathf.Cos(d1)), Quaternion.identity, meshNV, brass);
        var gNH = Gear(root, "BevelNear_z20", O1 + Vector3.right * (A1 * Mathf.Cos(d2)), qN, meshNH, dark);
        var gFH = Gear(root, "BevelFar_z20", O2 + Vector3.left * (A2 * Mathf.Cos(dh)), qF, meshFH, light);
        var gFV = Gear(root, "BevelFar_z12", O2 + Vector3.up * (A2 * Mathf.Cos(dv)), Quaternion.identity, meshFV, dark);
        var gPN = Gear(root, "PinionNear_z14", new Vector3(XW, GearY, ZL), Quaternion.identity, meshP, brass);
        var gPF = Gear(root, "PinionFar_z14", new Vector3(XP, GearY, ZL), Quaternion.identity, meshP, brass);

        var gearTs = new[] { gNV, gNH, gFH, gFV, gPN, gPF };
        float r12 = (float)Z1 / Z2;
        // 角度（2D、度）= ratio·u + phase。u=0 で：縦の歯車は相手の方に歯、横の歯車はすき間、ピニオンは +z（ラック）に歯
        var gearRatio = new[] { 1f, -r12, r12, KV, 1f, KV };
        var gearPhase = new[] { 0f, phiN + 180f / Z2, phiF + 180f / ZH, 180f, 90f, 90f };

        // 軸（歯車と一緒に回る。回ってるのが分かるように線を1本）
        float topNV = gNV.position.y - Origin.y;
        Shaft(gNV, 0f, HubY - topNV, 0.4f, axleM, brass);                                   // 羽根車の軸（床をつきぬける。途中に手前のピニオン）
        float cxN = gNH.position.x - Origin.x;
        Shaft(gNH, ShaftX0 - cxN, ShaftX1 - cxN, 0.4f, axleM, brass);                       // 横の長い軸
        float topFV = gFV.position.y - Origin.y;
        Shaft(gFV, 0f, -0.4f - topFV, 0.4f, axleM, brass);                                  // 向こうのピニオンの軸（向こう岸の床の下まで）

        // 軸受け（動かない）：床の下からつるす
        foreach (float bx in new[] { ShaftX0 + 0.15f, ShaftX1 - 0.15f })
        {
            Prim(PrimitiveType.Cube, root, "Hanger" + bx.ToString("F1"), new Vector3(bx, (-0.4f + YL) / 2f, ZL), new Vector3(0.2f, -0.4f - YL, 0.2f), light);
            // 軸のはしのカラー（太さ0.64）の外の面が軸受けの外の面とぴったり同じでZファイト（2026-11-10 てじゃ）→ 軸受けを外へ 2cm ずつ厚くして、カラーを中にかくす
            float outSide = bx < (ShaftX0 + ShaftX1) / 2f ? -1f : 1f;
            Prim(PrimitiveType.Cube, root, "Bearing" + bx.ToString("F1"), new Vector3(bx + outSide * 0.02f, YL, ZL), new Vector3(0.34f, 0.66f, 0.66f), dark);
        }
        Prim(PrimitiveType.Cylinder, root, "BearingPinion", new Vector3(XP, -0.48f, ZL), new Vector3(0.66f, 0.08f, 0.66f), dark);
        Prim(PrimitiveType.Cylinder, root, "CollarHandle", new Vector3(XW, 0.02f, ZL), new Vector3(0.7f, 0.02f, 0.7f), dark);

        // ── 羽根車 ──
        var handle = new GameObject("Handle").transform; handle.SetParent(gNV, false); handle.localPosition = new Vector3(0f, HubY - topNV, 0f);
        handle.gameObject.layer = 2;
        FuwaTurnTableTest.Impeller(handle, brass);
        var sc = handle.gameObject.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 0.8f;

        // ── かごの金網（橋の口だけあける） ──
        float endZ = CageHalfZ - 0.08f, brZ = BridgeW / 2f + 0.15f;
        FuwaTurnTableTest.WireFence(root, new Vector3(CageX0 / 2f, 0f, CageHalfZ), 90f, -CageX0, 2.2f, light, axleM).name = "Fence_N";
        FuwaTurnTableTest.WireFence(root, new Vector3(CageX0 / 2f, 0f, -CageHalfZ), 90f, -CageX0, 2.2f, light, axleM).name = "Fence_S";
        // 入口（かごの奥、2026-11-07 2–4 のコースに並べる時の入口）：まん中 ±1.5m をあける（回転床のかごと同じ）
        foreach (int sg in new[] { 1, -1 })
            FuwaTurnTableTest.WireFence(root, new Vector3(CageX0, 0f, sg * (1.5f + endZ) / 2f), 0f, endZ - 1.5f, 2.2f, light, axleM).name = "Fence_In" + (sg > 0 ? "N" : "S");
        foreach (int sg in new[] { 1, -1 })
            FuwaTurnTableTest.WireFence(root, new Vector3(0f, 0f, sg * (brZ + endZ) / 2f), 0f, endZ - brZ, 2.2f, light, axleM).name = "Fence_Br" + (sg > 0 ? "N" : "S");

        // ── 動かすしくみ ──
        var tt = UdonSharpUndo.AddComponent<FuwaTurnTable>(handle.gameObject);
        tt.table = null; tt.tableRatio = 0f; tt.detentDeg = 0f;
        tt.gears = gearTs; tt.gearRatio = gearRatio; tt.gearPhase = gearPhase;
        tt.handle = handle; tt.hitRadius = 1.0f; tt.hitHalfHeight = 0.55f; tt.hitCone = 4f;   // 2026-11-07 てじゃ「当たり判定が狭くなりすぎてる」0.8・0.3・3° → 1.0・0.55・4°
        tt.spinPerShot = 170f; tt.maxSpin = 300f;   // 1発で羽根車 約 100°（橋 片側 約 1.2m）→ 3発くらいでつながる
        tt.startU = 0f;
        // 手前の橋：ピニオンの +z 側のラックは、ピニオンが反時計回り（u が +）だと −x。u を − にすると +x（谷へ）
        tt.rack = near; tt.rackBase = Vector3.zero; tt.rackAxis = Vector3.right; tt.rackPerU = -PerU;
        // 向こうの橋：ピニオンは −u で回る → u を − にすると −x（谷へ）
        tt.rack2 = far; tt.rack2Base = new Vector3(Gap, 0f, 0f); tt.rack2Axis = Vector3.right; tt.rack2PerU = -PerU * KV;
        tt.uMin = -Travel / PerU; tt.uMax = 0f;
        tt.holdTime = HoldTime; tt.returnSpeed = (-tt.uMin) / ReturnTime;
        UdonSharpEditorUtility.CopyProxyToUdon(tt); FuwaStage2WaterTest.NoSync(tt);
        SetU(0f);

        // ── ふわふわの台（テスト用） ──
        var spot = new GameObject("BallSpot").transform; spot.SetParent(root, false); spot.localPosition = new Vector3(-4.2f, 1.6f, 1.2f);
        FuwaStage2WaterTest.BallStand(root, "BallStand", new Vector3(-4.6f, 0f, 2.5f), spot, "ふわふわを出す", new Color(1f, 0.75f, 0.85f));

        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[橋がせり出すお試し] 作った 羽根車1回転で橋（片側） {PerU * 360f:F2}m  つながるまで羽根車 {-tt.uMin / 360f:F2} 回転");
    }

    // 半分の橋：原点＝橋の先。dir＝橋の本体がのびる向き（手前の橋は −1、向こうは +1）。上の面は BridgeTop。下にラック（歯は −z）とラックの板
    static Transform HalfBridge(Transform root, string name, float x0, float dir, float rackC, float pitchZ, float hf, Mesh rackMesh, Material wood, Material lineM, Material brass, Material dark)
    {
        var b = new GameObject(name).transform; b.SetParent(root, false); b.localPosition = new Vector3(x0, 0f, 0f);
        b.gameObject.layer = 11;
        var c = new Vector3(dir * HalfL / 2f, BridgeTop - BridgeT / 2f, 0f);
        Prim(PrimitiveType.Cube, b, "Visual", c, new Vector3(HalfL, BridgeT, BridgeW), wood);
        var bc = b.gameObject.AddComponent<BoxCollider>(); bc.center = c; bc.size = new Vector3(HalfL, BridgeT, BridgeW);
        for (int k = 1; k < 5; k++) Prim(PrimitiveType.Cube, b, "Plank" + k, new Vector3(dir * k * HalfL / 5f, BridgeTop + 0.005f, 0f), new Vector3(0.05f, 0.01f, BridgeW), lineM);
        var rack = new GameObject("Rack").transform; rack.SetParent(b, false);
        rack.localPosition = new Vector3(rackC, GearY, pitchZ); rack.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 歯は −z（ピニオン）向き
        rack.gameObject.AddComponent<MeshFilter>().sharedMesh = rackMesh;
        rack.gameObject.AddComponent<MeshRenderer>().sharedMaterial = brass;
        float rackLen = RackTeeth * Mathf.PI * MP, top = GearY + GearTh, bot = BridgeTop - BridgeT;
        Prim(PrimitiveType.Cube, b, "RackPlate", new Vector3(rackC, (top + bot) / 2f, pitchZ + hf + 0.15f), new Vector3(rackLen, bot - top + 0.01f, 0.3f), dark);
        // 先っぽの目印の柱（2026-11-08 てじゃ「橋が目線から見えづらい」）：床より低い橋でも、どこまで出ているか遠くから分かる。当たりなし
        //  橋の先から少し谷の側（引っこんでも床にささらない）、橋の幅のすぐ外。手前の橋は +z・向こうは −z（まん中で合った時にすれちがう）
        var postM = FuwaGearTest.Mat("RackBridge_TipPost", new Color(0.88f, 0.34f, 0.27f));
        float pz = (dir < 0f ? 1f : -1f) * (BridgeW / 2f + 0.08f), px = -dir * 0.12f, py0 = BridgeTop - BridgeT, py1 = 1.7f;
        Prim(PrimitiveType.Cylinder, b, "TipPost", new Vector3(px, (py0 + py1) / 2f, pz), new Vector3(0.14f, (py1 - py0) / 2f, 0.14f), postM);
        Prim(PrimitiveType.Sphere, b, "TipPostBall", new Vector3(px, py1 + 0.12f, pz), Vector3.one * 0.32f, brass);
        // 橋のかどから柱へのうけ（橋の幅の外だけ：まん中で合った時、相手の橋にささらない）
        Prim(PrimitiveType.Cube, b, "TipPostBracket", new Vector3(px / 2f - dir * 0.02f, BridgeTop - BridgeT / 2f, Mathf.Sign(pz) * (BridgeW / 2f + 0.075f)), new Vector3(Mathf.Abs(px) + 0.18f, BridgeT * 0.6f, 0.15f), dark);
        return b;
    }

    // エディタ用：羽根車の角度 u にする（FuwaTurnTable と同じ計算）
    public static void SetU(float u)
    {
        var o = GameObject.Find("RackBridgeTest"); if (!o) return;
        var tt = o.GetComponentInChildren<FuwaTurnTable>(true); if (!tt) return;
        if (tt.rack) tt.rack.localPosition = tt.rackBase + tt.rackAxis * (tt.rackPerU * (u - tt.startU));
        if (tt.rack2) tt.rack2.localPosition = tt.rack2Base + tt.rack2Axis * (tt.rack2PerU * (u - tt.startU));
        for (int i = 0; i < tt.gears.Length; i++) tt.gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(tt.gearRatio[i] * u + tt.gearPhase[i], 360f), 0f);
    }

    [MenuItem("FuwaCourse/ステージ2/橋がせり出すお試しを消す")]
    public static void Remove() { var o = GameObject.Find("RackBridgeTest"); if (o) Object.DestroyImmediate(o); }

    // 枠 q の中で、世界の向き dir が 2D の何度か（local の x→z 反時計回り）
    public static float Phi2D(Quaternion q, Vector3 dir) { var l = Quaternion.Inverse(q) * dir; return Mathf.Atan2(l.z, l.x) * Mathf.Rad2Deg; }

    // 歯車：枠（動かない、向きだけ）の下に回る本体
    static Transform Gear(Transform root, string name, Vector3 localPos, Quaternion frameRot, Mesh mesh, Material m)
    {
        var fr = new GameObject(name + "_Frame").transform; fr.SetParent(root, false);
        fr.localPosition = localPos; fr.localRotation = frameRot;
        var g = new GameObject(name).transform; g.SetParent(fr, false);
        g.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; g.gameObject.AddComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }

    // 回る軸（local Y ぞい、y0〜y1）に、回ってるのが分かる線
    static void Shaft(Transform g, float y0, float y1, float d, Material m, Material lineM)
    {
        // 2026-11-08 てじゃ「軸の先端にZファイト」：軸・線・カラーの上の面が同じ高さ → 軸と線はカラーの中で止める
        float lo = Mathf.Min(y0, y1), hi = Mathf.Max(y0, y1), ct = Mathf.Min(0.14f, (hi - lo) / 4f), inset = Mathf.Min(0.02f, ct / 3f);
        float s0 = lo + inset, s1 = hi - inset, l0 = lo + ct - inset, l1 = hi - ct + inset;
        Prim(PrimitiveType.Cylinder, g, "Shaft", new Vector3(0f, (s0 + s1) / 2f, 0f), new Vector3(d, (s1 - s0) / 2f, d), m);
        if (l1 > l0) Prim(PrimitiveType.Cube, g, "ShaftLine", new Vector3(d / 2f, (l0 + l1) / 2f, 0f), new Vector3(0.03f, l1 - l0, 0.05f), lineM);
        // 両はしに太い輪（カラー）（2026-11-07 てじゃ「もっと大げさに」）
        foreach (float cy in new[] { lo + ct / 2f, hi - ct / 2f }) Prim(PrimitiveType.Cylinder, g, "Collar", new Vector3(0f, cy, 0f), new Vector3(d * 1.6f, ct / 2f, d * 1.6f), lineM);
    }

    // かさ歯車：外の面（y=0）の歯の輪郭を、頂点（y = −A cosδ）に向かって縮めた円すい台。歯の幅 F
    public static Mesh Bevel(int z, float m, float delta, float faceW, string name)
    {
        // 外の面の歯は「背円すい」（ピッチ円すいに直角な円すい）の上に、トレッドゴールドの近似で置く（2026-11-07）：
        //  背円すいを開いた「仮想の平歯車」（半径 r/cosδ、歯数 z/cosδ）の歯形を描いて、ピッチ円からの高さ dρ は
        //  半径方向 +dρ·cosδ ＋ 軸方向 −dρ·sinδ（ピッチ円すいに直角＝歯先は頂点の側へ下がる）、角度は ψ/cosδ に戻す。平らな面に平歯車の形だと歯先が相手の歯元にめりこんだ
        float A = m * z / (2f * Mathf.Sin(delta)), r = m * z / 2f, cd = Mathf.Cos(delta), sd = Mathf.Sin(delta);
        float k = (A - faceW) / A, H = A * cd, Rv = r / cd;
        var tooth = VirtualTooth(z / cd, m);
        int n = tooth.Length * z;
        var outer = new Vector3[n]; var inner = new Vector3[n];
        Vector3 apex = new Vector3(0f, -H, 0f);
        for (int kk = 0, i = 0; kk < z; kk++)
            foreach (var q in tooth)
            {
                float dr = q.x - Rv, phi = kk * 2f * Mathf.PI / z + q.y / cd, rr = r + dr * cd;
                outer[i] = new Vector3(Mathf.Cos(phi) * rr, -dr * sd, Mathf.Sin(phi) * rr);
                inner[i] = apex + (outer[i] - apex) * k;
                i++;
            }
        var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
        int top = v.Count; v.Add(Vector3.zero);
        v.AddRange(outer);
        for (int i = 0; i < n; i++) { t.Add(top); t.Add(top + 1 + (i + 1) % n); t.Add(top + 1 + i); }
        int bot = v.Count; v.Add(apex + (Vector3.zero - apex) * k);
        v.AddRange(inner);
        for (int i = 0; i < n; i++) { t.Add(bot); t.Add(bot + 1 + i); t.Add(bot + 1 + (i + 1) % n); }
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            int s = v.Count;
            v.Add(inner[i]); v.Add(inner[j]); v.Add(outer[j]); v.Add(outer[i]);
            t.Add(s); t.Add(s + 3); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 1);
        }
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    // 仮想の平歯車（歯数 zv は小数でよい）の歯1本ぶん：(半径, 角度) を、前のすき間のまん中から次のすき間のまん中の手前まで。
    //  FuwaGearTest.GearOutline と同じ形（インボリュート、あそびつき）
    public static Vector2[] VirtualTooth(float zv, float m)
    {
        float al = FuwaGearTest.Alpha;
        float r = m * zv / 2f, rb = r * Mathf.Cos(al), ra = r + m, rf = Mathf.Max(r - 1.25f * m, 0.1f);
        float half = (Mathf.PI * m / 2f - FuwaGearTest.Backlash) / (2f * r) + FuwaGearTest.Inv(al);
        float pitch = 2f * Mathf.PI / zv, rootHalf = pitch / 2f - half;
        var flank = new System.Collections.Generic.List<Vector2>();
        float r0 = Mathf.Max(rf, rb);
        if (rf < rb) flank.Add(new Vector2(rf, half));
        for (int i = 0; i <= 6; i++)
        {
            float rr = Mathf.Lerp(r0, ra, i / 6f);
            float ph = Mathf.Acos(Mathf.Clamp(rb / rr, -1f, 1f));
            flank.Add(new Vector2(rr, half - FuwaGearTest.Inv(ph)));
        }
        float tipHalf = flank[flank.Count - 1].y;
        var pts = new System.Collections.Generic.List<Vector2>();
        for (int s = 0; s < 3; s++) pts.Add(new Vector2(rf, -half - rootHalf + rootHalf * s / 3f));
        for (int i = 0; i < flank.Count; i++) pts.Add(new Vector2(flank[i].x, -flank[i].y));
        for (int s = 1; s < 3; s++) pts.Add(new Vector2(ra, -tipHalf + 2f * tipHalf * s / 3f));
        for (int i = flank.Count - 1; i >= 0; i--) pts.Add(new Vector2(flank[i].x, flank[i].y));
        for (int s = 1; s < 3; s++) pts.Add(new Vector2(rf, half + rootHalf * s / 3f));
        return pts.ToArray();
    }

    // テスト用：かさ歯車の local の点が中に入っているか。入っていたら深さ（m、だいたい）、外なら −1
    public static float InsideBevel(Vector3 l, int z, float m, float delta, float faceW)
    {
        float A = m * z / (2f * Mathf.Sin(delta)), r = m * z / 2f, cd = Mathf.Cos(delta), sd = Mathf.Sin(delta);
        float k = (A - faceW) / A, H = A * cd, Rv = r / cd;
        float rhoP = new Vector2(l.x, l.z).magnitude, yP = l.y + H;
        if (yP <= 1e-5f) return -1f;
        float c = rhoP / yP, dr = (c * H - r) / (cd + c * sd);
        if (dr < -1.3f * m || dr > 1.1f * m) return -1f;
        float rhoQ = r + dr * cd, f = rhoP / rhoQ;
        if (f < k || f > 1f) return -1f;
        float pitch = 2f * Mathf.PI / z;
        float phi = Mathf.Repeat(Mathf.Atan2(l.z, l.x) + pitch / 2f, pitch) - pitch / 2f;
        float psi = phi * cd, rv = Rv + dr;
        Vector2 p = new Vector2(rv * Mathf.Cos(psi), rv * Mathf.Sin(psi));
        var tp = VirtualTooth(z / cd, m);
        var poly = new Vector2[tp.Length + 1];
        for (int i = 0; i < tp.Length; i++) poly[i] = new Vector2(tp[i].x * Mathf.Cos(tp[i].y), tp[i].x * Mathf.Sin(tp[i].y));
        poly[tp.Length] = Vector2.zero;
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)) inside = !inside;
        if (!inside) return -1f;
        float best = 1e9f;
        for (int i = 0; i < tp.Length - 1; i++) { Vector2 a = poly[i], b = poly[i + 1]; float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (b - a).sqrMagnitude); best = Mathf.Min(best, (a + (b - a) * t - p).magnitude); }
        return best * f;
    }

    // ラック（FuwaGearTest.RackMesh と同じ形、モジュールと土台の厚みを指定）。歯は local +z、ピッチ線は z=0、x=0 はすき間のまん中（偶数本）
    static Mesh RackMesh(int teeth, float mm, float baseD, float thick, string name)
    {
        float p = Mathf.PI * mm, ha = mm, hf = 1.25f * mm, ta = Mathf.Tan(FuwaGearTest.Alpha);
        float x0 = -teeth * p / 2f, bl = FuwaGearTest.Backlash;   // GearOutline と同じあそび
        var quads = new System.Collections.Generic.List<Vector2[]>();
        quads.Add(new[] { new Vector2(x0, -hf - baseD), new Vector2(x0 + teeth * p, -hf - baseD), new Vector2(x0 + teeth * p, -hf), new Vector2(x0, -hf) });
        for (int k = 0; k < teeth; k++)
        {
            float c = x0 + (k + 0.5f) * p, q4 = p / 4f - bl / 2f;
            quads.Add(new[] { new Vector2(c - q4 - hf * ta, -hf), new Vector2(c + q4 + hf * ta, -hf), new Vector2(c + q4 - ha * ta, ha), new Vector2(c - q4 + ha * ta, ha) });
        }
        var cis = new CombineInstance[quads.Count];
        for (int i = 0; i < quads.Count; i++)
        {
            var q = quads[i]; var cen = (q[0] + q[1] + q[2] + q[3]) / 4f;
            cis[i] = new CombineInstance { mesh = FuwaGearTest.Extrude(q, thick, "q", cen), transform = Matrix4x4.identity };
        }
        var mesh = new Mesh { name = name }; mesh.CombineMeshes(cis, true); mesh.RecalculateBounds();
        return mesh;
    }

    static void Floor(Transform root, string name, Vector3 c, Vector2 size, Material m)
    {
        var g = new GameObject(name); g.transform.SetParent(root, false); g.layer = 11;
        g.transform.localPosition = c + new Vector3(0f, -0.2f, 0f);
        Prim(PrimitiveType.Cube, g.transform, "Visual", Vector3.zero, new Vector3(size.x, 0.4f, size.y), m);
        g.AddComponent<BoxCollider>().size = new Vector3(size.x, 0.4f, size.y);
    }

    static void Box(Transform parent, string name, Vector3 lp, Vector3 size, Material m, bool collider)
    {
        var t = Prim(PrimitiveType.Cube, parent, name, lp, size, m);
        if (collider) t.gameObject.AddComponent<BoxCollider>();
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
