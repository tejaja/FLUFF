using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2–4：撃って回すと遠くの橋がせり出すお試し（2026-11-07 てじゃ「歯車の意味は 1 速さ・2 向き・3 遠くへ運ぶ の組み合わせ」→ 案A）
//  かごの中の羽根車（縦の軸）→ 足元のかさ歯車（12枚 → 20枚）で横の長い軸へ → 谷の下を渡る
//  → 向こう岸でかさ歯車（14枚 → 20枚）で縦の軸へ → ピニオン（16枚）→ 橋の下のラックで橋が谷へせり出す
//  速さ：羽根車 1 回転でピニオン 0.42 回転（12/20 × 14/20）。羽根車は軽く回るけど橋はゆっくり
// ・橋は向こう岸の床のみぞ（slot）に入っていて、上面は床と同じ高さ。のびても橋のしっぽがみぞの手前をふさぐ。奥は車庫でふた
// ・回し方・当たりは回転床と同じ FuwaTurnTable（table なし、rack に橋）。行き止まりで止まる
// ・2D の (x, y) は Unity の (x, z)。2D の反時計回り θ は Unity の Y 回転 −θ（FuwaTurnTable と同じ）
// ・かさ歯車：頂点（apex）を2枚で共有。縦の歯車は頂点が下、横の歯車は頂点が軸ぞいに相手の軸の方。歯は頂点に向かって細くなる円すい
public static class FuwaRackBridgeTest
{
    static readonly Vector3 Origin = new Vector3(25f, -162f, -135f);   // かごの床の橋側のはしのまん中、床の上の高さ
    const float CageX0 = -7.4f, CageHalfZ = 3.2f;
    const float GapEnd = 9f;                       // 向こう岸のはし（谷は x 0〜9）
    const float FarX1 = 21f, FarHalfZ = 5f;
    const float BridgeW = 2.5f, BridgeL = 10.5f, Slot = 0.01f;
    const float FrontExtended = 0.03f;             // のびきった時の橋の先（かごの床とのすき間 3cm）
    const float ZL = -2.2f, YL = -2.3f;            // 横の軸の z と高さ
    const float XW = -5.8f;                        // 羽根車の軸の x
    const float XP = 9.8f;                         // ピニオンの軸の x
    const float MB = 0.15f; const int Z1 = 12, Z2 = 20, ZH = 14, ZV = 20;   // かさ歯車
    const float MP = 0.2f; const int ZPin = 16, RackTeeth = 16;               // ピニオンとラック
    const float GearY = -1.0f, GearTh = 0.35f;
    const float HubY = 1.05f;                      // 羽根車の高さ
    const float ShaftX0 = -7.0f, ShaftX1 = 12.0f;  // 横の軸のはし（軸受け）

    static float RP => MP * ZPin / 2f;
    static float KV => -((float)ZH / ZV) * ((float)Z1 / Z2);   // ピニオンの角度 / 羽根車の角度
    static float RackPerU => -RP * KV * Mathf.Deg2Rad;         // 羽根車 1° で橋が +x へ何 m
    static float RackC => (XP - GapEnd) + 7f * Mathf.PI * MP;  // 橋の先からラックのまん中まで（引っこんだ時にピニオンの所がすき間）

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

        // ── 床 ──
        Floor(root, "CageFloor", new Vector3(CageX0 / 2f, 0f, 0f), new Vector2(-CageX0, 2f * CageHalfZ), floorM);
        float sz0 = BridgeW / 2f + Slot;
        foreach (int sg in new[] { 1, -1 })
            Floor(root, "FarSide" + (sg > 0 ? "N" : "S"), new Vector3((GapEnd + FarX1) / 2f, 0f, sg * (sz0 + FarHalfZ) / 2f), new Vector2(FarX1 - GapEnd, FarHalfZ - sz0), floorM);
        const float GarX0 = 10.4f, GarX1 = 20.6f;
        Floor(root, "FarBack", new Vector3((GarX1 + FarX1) / 2f, 0f, 0f), new Vector2(FarX1 - GarX1, 2f * sz0), floorM);

        // ── 車庫（みぞの奥のふた。橋の上 3cm に前の壁） ──
        float gz = sz0 + 0.17f, gh = 1.8f, gt = 0.15f;
        var gar = new GameObject("Garage").transform; gar.SetParent(root, false);
        Box(gar, "WallN", new Vector3((GarX0 + GarX1) / 2f, gh / 2f, gz - gt / 2f), new Vector3(GarX1 - GarX0, gh, gt), dark, true);
        Box(gar, "WallS", new Vector3((GarX0 + GarX1) / 2f, gh / 2f, -(gz - gt / 2f)), new Vector3(GarX1 - GarX0, gh, gt), dark, true);
        Box(gar, "WallFront", new Vector3(GarX0 + gt / 2f, 0.03f + (gh - 0.03f) / 2f, 0f), new Vector3(gt, gh - 0.03f, 2f * gz), dark, true);
        Box(gar, "WallBack", new Vector3(GarX1 - gt / 2f, gh / 2f - 0.2f, 0f), new Vector3(gt, gh + 0.4f, 2f * gz), dark, true);
        Box(gar, "Roof", new Vector3((GarX0 + GarX1) / 2f, gh + gt / 2f, 0f), new Vector3(GarX1 - GarX0 + 0.1f, gt, 2f * gz + 0.1f), light, true);

        // ── 橋（ラックつき。上面は床と同じ高さ） ──
        var bridge = new GameObject("Bridge").transform; bridge.SetParent(root, false); bridge.localPosition = new Vector3(GapEnd, 0f, 0f);
        bridge.gameObject.layer = 11;
        Prim(PrimitiveType.Cube, bridge, "Visual", new Vector3(BridgeL / 2f, -0.2f, 0f), new Vector3(BridgeL, 0.4f, BridgeW), wood);
        var bbc = bridge.gameObject.AddComponent<BoxCollider>(); bbc.center = new Vector3(BridgeL / 2f, -0.2f, 0f); bbc.size = new Vector3(BridgeL, 0.4f, BridgeW);
        for (int k = 1; k < 10; k++) Prim(PrimitiveType.Cube, bridge, "Plank" + k, new Vector3(k * BridgeL / 10f, 0.005f, 0f), new Vector3(0.05f, 0.01f, BridgeW), axleM);
        float pitchZ = ZL + RP;   // ラックのピッチ線（ピニオンの +z 側）
        var rack = new GameObject("Rack").transform; rack.SetParent(bridge, false);
        rack.localPosition = new Vector3(RackC, GearY, pitchZ); rack.localRotation = Quaternion.Euler(0f, 180f, 0f);   // 歯は −z（ピニオン）向き
        rack.gameObject.AddComponent<MeshFilter>().sharedMesh = FuwaGearTest.SaveMesh(RackMesh(RackTeeth, MP, 0.3f, GearTh, "RackBridge_Rack_m02"));
        rack.gameObject.AddComponent<MeshRenderer>().sharedMaterial = brass;
        float rackLen = RackTeeth * Mathf.PI * MP, hf = 1.25f * MP;
        Prim(PrimitiveType.Cube, bridge, "RackPlate", new Vector3(RackC, (GearY + GearTh - 0.4f) / 2f, pitchZ + hf + 0.15f), new Vector3(rackLen, -0.4f - GearY - GearTh + 0.01f, 0.3f), dark);

        // ── 歯車 ──
        float d1 = Mathf.Atan2(Z1, Z2), d2 = Mathf.PI / 2f - d1;      // 手前：縦（12）・横（20）
        float dh = Mathf.Atan2(ZH, ZV), dv = Mathf.PI / 2f - dh;      // 向こう：横（14）・縦（20）
        float A1 = MB * Z1 / (2f * Mathf.Sin(d1)), A2 = MB * ZH / (2f * Mathf.Sin(dh));
        float F1 = 0.3f * A1, F2 = 0.3f * A2;
        Vector3 O1 = new Vector3(XW, YL, ZL), O2 = new Vector3(XP, YL, ZL);

        var meshNV = FuwaGearTest.SaveMesh(Bevel(Z1, MB, d1, F1, "RackBridge_Bevel_z12"));
        var meshNH = FuwaGearTest.SaveMesh(Bevel(Z2, MB, d2, F1, "RackBridge_Bevel_z20a"));
        var meshFH = FuwaGearTest.SaveMesh(Bevel(ZH, MB, dh, F2, "RackBridge_Bevel_z14"));
        var meshFV = FuwaGearTest.SaveMesh(Bevel(ZV, MB, dv, F2, "RackBridge_Bevel_z20b"));
        var meshP = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZPin, 8, MP), GearTh, "RackBridge_Pinion_z16", Vector2.zero));

        // 横の歯車の枠：local Y を軸に。手前は +x（頂点は −x 側）、向こうは −x（頂点は +x 側）
        Quaternion qN = Quaternion.FromToRotation(Vector3.up, Vector3.right), qF = Quaternion.FromToRotation(Vector3.up, Vector3.left);
        float phiN = Phi2D(qN, Vector3.up), phiF = Phi2D(qF, Vector3.up);   // 相手（縦の歯車）とかみ合う所の向き

        var gNV = Gear(root, "BevelNear_z12", O1 + Vector3.up * (A1 * Mathf.Cos(d1)), Quaternion.identity, meshNV, brass);
        var gNH = Gear(root, "BevelNear_z20", O1 + Vector3.right * (A1 * Mathf.Cos(d2)), qN, meshNH, dark);
        var gFH = Gear(root, "BevelFar_z14", O2 + Vector3.left * (A2 * Mathf.Cos(dh)), qF, meshFH, light);
        var gFV = Gear(root, "BevelFar_z20", O2 + Vector3.up * (A2 * Mathf.Cos(dv)), Quaternion.identity, meshFV, dark);
        var gP = Gear(root, "Pinion_z16", new Vector3(XP, GearY, ZL), Quaternion.identity, meshP, brass);

        var gearTs = new[] { gNV, gNH, gFH, gFV, gP };
        float r12 = (float)Z1 / Z2;
        // 角度（2D、度）= ratio·u + phase。u=0 で：縦の歯車は相手の方に歯、横の歯車はすき間、ピニオンは +z（ラック）に歯
        var gearRatio = new[] { 1f, -r12, r12, KV, KV };
        var gearPhase = new[] { 0f, phiN + 180f / Z2, phiF + 180f / ZH, 180f, 90f };

        // 軸（歯車と一緒に回る。回ってるのが分かるように線を1本）
        float topNV = gNV.position.y - Origin.y;
        Shaft(gNV, 0f, HubY - topNV, 0.16f, axleM, brass);                                   // 羽根車の軸（床をつきぬける）
        float cxN = gNH.position.x - Origin.x;
        Shaft(gNH, ShaftX0 - cxN, ShaftX1 - cxN, 0.16f, axleM, brass);                       // 横の長い軸
        float topFV = gFV.position.y - Origin.y;
        Shaft(gFV, 0f, -0.4f - topFV, 0.16f, axleM, brass);                                  // ピニオンの軸（向こう岸の床の下まで）

        // 軸受け（動かない）：床の下からつるす
        foreach (float bx in new[] { ShaftX0 + 0.15f, -0.6f, ShaftX1 - 0.15f })
        {
            Prim(PrimitiveType.Cube, root, "Hanger" + bx.ToString("F1"), new Vector3(bx, (-0.4f + YL) / 2f, ZL), new Vector3(0.12f, -0.4f - YL, 0.12f), light);
            Prim(PrimitiveType.Cube, root, "Bearing" + bx.ToString("F1"), new Vector3(bx, YL, ZL), new Vector3(0.22f, 0.34f, 0.34f), dark);
        }
        Prim(PrimitiveType.Cylinder, root, "BearingPinion", new Vector3(XP, -0.48f, ZL), new Vector3(0.34f, 0.08f, 0.34f), dark);
        Prim(PrimitiveType.Cylinder, root, "CollarHandle", new Vector3(XW, 0.02f, ZL), new Vector3(0.36f, 0.02f, 0.36f), dark);

        // ── 羽根車 ──
        var handle = new GameObject("Handle").transform; handle.SetParent(gNV, false); handle.localPosition = new Vector3(0f, HubY - topNV, 0f);
        handle.gameObject.layer = 2;
        Prim(PrimitiveType.Cylinder, handle, "Hub", Vector3.zero, new Vector3(0.22f, 0.15f, 0.22f), brass);
        for (int k = 0; k < 6; k++)
        {
            var pd = Prim(PrimitiveType.Cube, handle, "Paddle" + k, Quaternion.Euler(0f, k * 60f, 0f) * new Vector3(0.45f, 0f, 0f), new Vector3(0.7f, 0.3f, 0.04f), bladeM);
            pd.localRotation = Quaternion.Euler(0f, k * 60f, 0f);
        }
        var sc = handle.gameObject.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 0.8f;

        // ── かごの金網（橋の口だけあける） ──
        float endZ = CageHalfZ - 0.08f, brZ = BridgeW / 2f + 0.15f;
        FuwaTurnTableTest.WireFence(root, new Vector3(CageX0 / 2f, 0f, CageHalfZ), 90f, -CageX0, 2.2f, light, axleM).name = "Fence_N";
        FuwaTurnTableTest.WireFence(root, new Vector3(CageX0 / 2f, 0f, -CageHalfZ), 90f, -CageX0, 2.2f, light, axleM).name = "Fence_S";
        FuwaTurnTableTest.WireFence(root, new Vector3(CageX0, 0f, 0f), 0f, 2f * endZ, 2.2f, light, axleM).name = "Fence_Back";
        foreach (int sg in new[] { 1, -1 })
            FuwaTurnTableTest.WireFence(root, new Vector3(0f, 0f, sg * (brZ + endZ) / 2f), 0f, endZ - brZ, 2.2f, light, axleM).name = "Fence_Br" + (sg > 0 ? "N" : "S");

        // ── 動かすしくみ ──
        var tt = UdonSharpUndo.AddComponent<FuwaTurnTable>(handle.gameObject);
        tt.table = null; tt.tableRatio = 0f; tt.detentDeg = 0f;
        tt.gears = gearTs; tt.gearRatio = gearRatio; tt.gearPhase = gearPhase;
        tt.handle = handle; tt.hitRadius = 0.8f; tt.hitHalfHeight = 0.3f; tt.hitCone = 3f;
        tt.maxSpin = 300f;
        tt.startU = 0f;
        tt.rack = bridge; tt.rackBase = new Vector3(GapEnd, 0f, 0f); tt.rackAxis = Vector3.right; tt.rackPerU = RackPerU;
        tt.uMin = (FrontExtended - GapEnd) / RackPerU; tt.uMax = 0f;
        UdonSharpEditorUtility.CopyProxyToUdon(tt); FuwaStage2WaterTest.NoSync(tt);
        SetU(0f);

        // ── ふわふわの台・ワープ（回転床のお試しのかご ⇄ ここ） ──
        var spot = new GameObject("BallSpot").transform; spot.SetParent(root, false); spot.localPosition = new Vector3(-4.2f, 1.6f, 1.2f);
        FuwaStage2WaterTest.BallStand(root, "BallStand", new Vector3(-4.6f, 0f, 2.5f), spot, "ふわふわを出す", new Color(1f, 0.75f, 0.85f));
        var spawn = new GameObject("Spawn").transform; spawn.SetParent(root, false); spawn.localPosition = new Vector3(-6.5f, 0f, 0.6f); spawn.localRotation = Quaternion.Euler(0f, 90f, 0f);
        var warpMat = FuwaGearTest.Mat("RackBridgeTest_Warp", new Color(0.6f, 0.9f, 0.6f));
        var ttRoot = GameObject.Find("TurnTableTest");
        if (ttRoot)
        {
            var back = new GameObject("BackSpot").transform; back.SetParent(root, false); back.position = ttRoot.transform.position + new Vector3(-16f, 0f, 0.5f); back.rotation = Quaternion.Euler(0f, 90f, 0f);
            FuwaIceTestBuilder.Warp(root, "WarpToBridge", ttRoot.transform.position + new Vector3(-13f, 0.35f, 2.0f), "RACK BRIDGE", spawn, warpMat);
            FuwaIceTestBuilder.Warp(root, "WarpBack", root.TransformPoint(new Vector3(-6.8f, 0.35f, 2.4f)), "BACK", back, warpMat);
        }

        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[橋がせり出すお試し] 作った 羽根車1回転で橋 {RackPerU * 360f:F2}m  のびきるまで羽根車 {-tt.uMin / 360f:F2} 回転");
    }

    // エディタ用：羽根車の角度 u にする（FuwaTurnTable と同じ計算）
    public static void SetU(float u)
    {
        var o = GameObject.Find("RackBridgeTest"); if (!o) return;
        var tt = o.GetComponentInChildren<FuwaTurnTable>(true); if (!tt) return;
        if (tt.rack) tt.rack.localPosition = tt.rackBase + tt.rackAxis * (tt.rackPerU * (u - tt.startU));
        for (int i = 0; i < tt.gears.Length; i++) tt.gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(tt.gearRatio[i] * u + tt.gearPhase[i], 360f), 0f);
    }

    [MenuItem("FuwaCourse/ステージ2/橋がせり出すお試しを消す")]
    public static void Remove() { var o = GameObject.Find("RackBridgeTest"); if (o) Object.DestroyImmediate(o); }

    // 枠 q の中で、世界の向き dir が 2D の何度か（local の x→z 反時計回り）
    static float Phi2D(Quaternion q, Vector3 dir) { var l = Quaternion.Inverse(q) * dir; return Mathf.Atan2(l.z, l.x) * Mathf.Rad2Deg; }

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
        float len = y1 - y0, mid = (y0 + y1) / 2f;
        Prim(PrimitiveType.Cylinder, g, "Shaft", new Vector3(0f, mid, 0f), new Vector3(d, len / 2f, d), m);
        Prim(PrimitiveType.Cube, g, "ShaftLine", new Vector3(d / 2f, mid, 0f), new Vector3(0.03f, len, 0.05f), lineM);
    }

    // かさ歯車：外の面（y=0）の歯の輪郭を、頂点（y = −A cosδ）に向かって縮めた円すい台。歯の幅 F
    static Mesh Bevel(int z, float m, float delta, float faceW, string name)
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
