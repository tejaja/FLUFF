using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2–4：撃って回す回転床のお試し（2026-11-06）
// 作り直し（てじゃ「乗ってる島が回るとフワフワの維持ムズい」→ B「回すのは動かない地面から」「歯車のつながりも矛盾ないように」）
//  入口の道の北の足場に羽根車。羽根車の軸の小歯車（12枚）→ 床下の歯車（14枚）→（14枚）→ 回転床のふちの下の大きい歯車（44枚）
//  歯車は一直線（中心から 165° の向き）に並べて、Mesh2 で中心距離と最初の角度をそろえる。軸は床下の動かない梁（中心の台から外へ）で受ける
//  歯車は橋の通り道（床の高さ）より下を通るのでぶつからない
// 橋は2本（120° はなれ）。最初は1本が入口（西）、もう1本が行き止まり（300°）。羽根車を回して 2本目を出口（60°）に合わせる
// ・2D の (x, y) は Unity の (x, z)。2D の反時計回りは Unity の Y 回転でマイナス
public static class FuwaTurnTableTest
{
    const float Mod = 0.2f; const int ZR = 44, ZI = 14, ZP = 12;
    const float DeckR = 4.6f, BridgeEnd = 10f, BridgeW = 2.5f;
    const float GearY = -1.0f, GearTh = 0.35f, LineDeg = 165f;
    static readonly Vector3 Origin = new Vector3(31.5f, -162f, -104f);   // 回転床のまん中、床の上の高さ

    [MenuItem("FuwaCourse/ステージ2/回転床のお試しを作る")]
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

        var root = new GameObject("TurnTableTest").transform; root.position = Origin;
        // 入口の道（歯車バンパーの床の東のはし x=14 から、回転床の橋の先 r=10 の手前まで）
        float x0 = 14f - Origin.x, x1 = -(BridgeEnd + 0.05f);
        Floor(root, "EntryPath", new Vector3((x0 + x1) / 2f, 0f, 0f), 0f, new Vector2(x1 - x0, BridgeW + 0.5f), floorM);
        // 出口（60°）と行き止まり（300°）：橋の先 r=10.05 から外へ
        Floor(root, "Exit", Polar(60f, BridgeEnd + 0.05f + 3f), 60f, new Vector2(6f, 6f), floorM);
        Floor(root, "DeadEnd", Polar(300f, BridgeEnd + 0.05f + 1.25f), 300f, new Vector2(2.5f, 3f), floorM);

        // 回転床
        var table = new GameObject("Table").transform; table.SetParent(root, false);
        var deck = Prim(PrimitiveType.Cylinder, table, "Deck", new Vector3(0f, -0.2f, 0f), new Vector3(2f * DeckR, 0.2f, 2f * DeckR), deckM);
        deck.gameObject.layer = 11;
        var mc = deck.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = deck.GetComponent<MeshFilter>().sharedMesh;
        foreach (float ang in new[] { 180f, 300f })
        {
            // 橋の根元は、橋のかどが床のふちの円に乗る所から（すき間なし）。重なる所は 1cm 上げて Zファイトを避ける
            float r0 = Mathf.Sqrt(DeckR * DeckR - BridgeW * BridgeW / 4f);
            float len = BridgeEnd - r0;
            var br = new GameObject("Bridge" + ang).transform; br.SetParent(table, false);
            br.localPosition = Polar(ang, r0 + len / 2f) + new Vector3(0f, -0.19f, 0f);
            br.localRotation = Quaternion.Euler(0f, -ang, 0f);
            br.gameObject.layer = 11;
            Prim(PrimitiveType.Cube, br, "Visual", Vector3.zero, new Vector3(len, 0.4f, BridgeW), wood);
            br.gameObject.AddComponent<BoxCollider>().size = new Vector3(len, 0.4f, BridgeW);
            for (int k = 1; k < 7; k++) Prim(PrimitiveType.Cube, br, "Plank" + k, new Vector3(-len / 2f + k * len / 7f, 0.205f, 0f), new Vector3(0.05f, 0.01f, BridgeW), axleM);
        }
        // 回転床のふちの下の大きい歯車（回転床と一緒に回る）と、床とつなぐ胴
        var ringMesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZR, 8, Mod), GearTh, "TurnTable_Ring_z44", Vector2.zero));
        var ring = new GameObject("Ring_z44").transform; ring.SetParent(table, false); ring.localPosition = new Vector3(0f, GearY, 0f);
        ring.gameObject.AddComponent<MeshFilter>().sharedMesh = ringMesh; ring.gameObject.AddComponent<MeshRenderer>().sharedMaterial = brass;
        Prim(PrimitiveType.Cylinder, table, "Skirt", new Vector3(0f, (GearY + GearTh - 0.4f) / 2f, 0f), new Vector3(7.6f, (-0.4f - GearY - GearTh) / 2f + 0.02f, 7.6f), dark);
        // 中心の台（動かない、回転床の軸受け）
        Prim(PrimitiveType.Cylinder, root, "Pedestal", new Vector3(0f, -1.9f, 0f), new Vector3(2.4f, 0.9f, 2.4f), dark);

        // 歯車の列：回転床（ring）を元に Mesh2 で並べる → 羽根車の軸の小歯車の角度 u を元に直す
        var gR = new FuwaGearTest.Gear { name = "ring", z = ZR, c = Vector2.zero, ratio = 1f, phase = 0f };
        var gI1 = FuwaGearTest.Mesh2(gR, "Idler1_z14", ZI, LineDeg, Mod);
        var gI2 = FuwaGearTest.Mesh2(gI1, "Idler2_z14", ZI, LineDeg, Mod);
        var gP = FuwaGearTest.Mesh2(gI2, "Pinion_z12", ZP, LineDeg, Mod);
        var chain = new[] { gI1, gI2, gP };
        float rp = gP.ratio, php = gP.phase * Mathf.Rad2Deg;   // 小歯車の角度 = rp·v + php（v = 回転床の角度）
        var gearTs = new Transform[3]; var gearRatio = new float[3]; var gearPhase = new float[3];
        var meshI = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZI, 8, Mod), GearTh, "TurnTable_Idler_z14", Vector2.zero));
        var meshP = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZP, 8, Mod), GearTh, "TurnTable_Pinion_z12", Vector2.zero));
        float beamY = GearY - 0.35f;
        for (int i = 0; i < 3; i++)
        {
            var g = chain[i];
            var t = new GameObject(g.name).transform; t.SetParent(root, false); t.localPosition = new Vector3(g.c.x, GearY, g.c.y);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = i < 2 ? meshI : meshP;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = i == 1 ? light : dark;
            gearTs[i] = t;
            // 角度 = (r_i / rp)·u + (ph_i − r_i·php/rp)
            gearRatio[i] = g.ratio / rp;
            gearPhase[i] = g.phase * Mathf.Rad2Deg - g.ratio * php / rp;
            // 軸（梁から歯車の上まで。小歯車は羽根車まで）
            float top = i == 2 ? 1.05f : GearY + GearTh + 0.1f;
            Prim(PrimitiveType.Cylinder, root, "Axle_" + g.name, new Vector3(g.c.x, (beamY + top) / 2f, g.c.y), new Vector3(0.16f, (top - beamY) / 2f, 0.16f), axleM);
        }
        // 床下の梁（中心の台から歯車の列にそって、小歯車の先まで。動かない）
        float rEnd = gP.c.magnitude + 0.6f;
        var beam = Prim(PrimitiveType.Cube, root, "Beam", Polar(LineDeg, (1f + rEnd) / 2f) + new Vector3(0f, beamY - 0.1f, 0f), new Vector3(rEnd - 1f, 0.2f, 0.4f), light);
        beam.localRotation = Quaternion.Euler(0f, -LineDeg, 0f);

        // 羽根車（小歯車の軸の上）と、まわりの足場・金網
        var pinT = gearTs[2];
        var handle = new GameObject("Handle").transform; handle.SetParent(pinT, false); handle.localPosition = new Vector3(0f, 1.05f - GearY, 0f);
        handle.gameObject.layer = 2;
        Prim(PrimitiveType.Cylinder, handle, "Hub", Vector3.zero, new Vector3(0.22f, 0.15f, 0.22f), brass);
        for (int k = 0; k < 6; k++)
        {
            var pd = Prim(PrimitiveType.Cube, handle, "Paddle" + k, Quaternion.Euler(0f, k * 60f, 0f) * new Vector3(0.45f, 0f, 0f), new Vector3(0.7f, 0.3f, 0.04f), bladeM);
            pd.localRotation = Quaternion.Euler(0f, k * 60f, 0f);
        }
        var sc = handle.gameObject.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 0.9f;
        // 足場：入口の道の北どなり（橋の通り道 r<10 にかからない所）
        var hp = new Vector2(gP.c.x, gP.c.y);
        float padX0 = Mathf.Min(hp.x - 1.4f, -10.3f), padX1 = -10.3f, padZ0 = (BridgeW + 0.5f) / 2f, padZ1 = hp.y + 2.1f;
        Floor(root, "HandlePad", new Vector3((padX0 + padX1) / 2f, 0f, (padZ0 + padZ1) / 2f), 0f, new Vector2(padX1 - padX0, padZ1 - padZ0), floorM);
        // 金網は羽根車と反対側（入口の道の南どなり）。てじゃ「金網を風車とかぶらせると、ふわふわを動かす時にまた羽が回る」
        //  → 道に立って南へ撃つ＝ふわふわ、北へ撃つ＝羽根車。ふわふわを橋へ送る風の線も羽根車から 2m 以上はなれる
        float fX0 = -16f, fX1 = -11.2f, fZ1 = -(BridgeW + 0.5f) / 2f, fZ0 = -4.6f;
        Floor(root, "FencePad", new Vector3((fX0 + fX1) / 2f, 0f, (fZ0 + fZ1) / 2f), 0f, new Vector2(fX1 - fX0, fZ1 - fZ0), floorM);
        WireFence(root, new Vector3((fX0 + fX1) / 2f, 0f, fZ0 + 0.3f), 90f, fX1 - fX0 - 0.2f, 2.2f, light, axleM);

        var tt = UdonSharpUndo.AddComponent<FuwaTurnTable>(handle.gameObject);
        tt.table = table; tt.tableRatio = 1f / rp; tt.tablePhase = -php / rp;
        tt.gears = gearTs; tt.gearRatio = gearRatio; tt.gearPhase = gearPhase;
        tt.handle = handle; tt.hitRadius = 0.9f; tt.startU = php;   // u = php で回転床が 0°
        UdonSharpEditorUtility.CopyProxyToUdon(tt); FuwaStage2WaterTest.NoSync(tt);
        SetU(php);

        // ふわふわを出す台
        var spot = new GameObject("BallSpot").transform; spot.SetParent(root, false); spot.localPosition = new Vector3(x0 + 2f, 1.6f, 0f);
        FuwaStage2WaterTest.BallStand(root, "BallStand", new Vector3(x0 + 1.2f, 0f, -1.1f), spot, "ふわふわを出す", new Color(1f, 0.75f, 0.85f));

        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[回転床のお試し] 作った 小歯車1回転で回転床 {360f / rp:F1}°  小歯車の位置 {gP.c}");
    }

    // エディタ用：羽根車（小歯車）の角度 u にする（FuwaTurnTable と同じ計算）
    public static void SetU(float u)
    {
        var o = GameObject.Find("TurnTableTest"); if (!o) return;
        var tt = o.GetComponentInChildren<FuwaTurnTable>(true); if (!tt) return;
        tt.table.localRotation = Quaternion.Euler(0f, -Mathf.Repeat(tt.tableRatio * u + tt.tablePhase, 360f), 0f);
        for (int i = 0; i < tt.gears.Length; i++) tt.gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(tt.gearRatio[i] * u + tt.gearPhase[i], 360f), 0f);
    }

    [MenuItem("FuwaCourse/ステージ2/回転床のお試しを消す")]
    public static void Remove() { var o = GameObject.Find("TurnTableTest"); if (o) Object.DestroyImmediate(o); }

    // 金網：ひし形（±45° の細い針金）＋まわりの枠。面は local の YZ（法線 +X）。当たりは薄い箱（地面レイヤーではない＝当たってもアウトにならない）
    // ふわふわはゆっくり跳ね返す（FuwaBounce、面の向き）。くっついて止まらないように
    public static Transform WireFence(Transform parent, Vector3 basePos, float yawDeg, float w, float h, Material frameM, Material wireM)
    {
        var f = new GameObject("WireFence").transform; f.SetParent(parent, false);
        f.localPosition = basePos; f.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
        float y0 = 0.15f, y1 = y0 + h, s = 0.2f, wr = 0.012f;
        // 針金（1つのメッシュにまとめる）
        var cis = new System.Collections.Generic.List<CombineInstance>();
        var cube = GetCube();
        foreach (int dir in new[] { 1, -1 })
        {
            // 直線 z = dir*(y - y0) + c を、z∈[-w/2, w/2]、y∈[y0, y1] で切る
            for (float c = -w / 2f - h; c <= w / 2f + h; c += s * Mathf.Sqrt(2f))
            {
                Vector2 a = Vector2.zero, b = Vector2.zero; int n = 0;
                Vector2[] cand = new Vector2[4];
                // 4 辺との交点（y, z）
                float zA = c, zB = dir * h + c;
                if (zA >= -w / 2f && zA <= w / 2f) cand[n++] = new Vector2(y0, zA);
                if (zB >= -w / 2f && zB <= w / 2f) cand[n++] = new Vector2(y1, zB);
                float yL = (-w / 2f - c) / dir + y0, yR = (w / 2f - c) / dir + y0;
                if (yL > y0 && yL < y1) cand[n++] = new Vector2(yL, -w / 2f);
                if (yR > y0 && yR < y1) cand[n++] = new Vector2(yR, w / 2f);
                if (n < 2) continue;
                a = cand[0]; b = cand[1];
                Vector3 pa = new Vector3(0f, a.x, a.y), pb = new Vector3(0f, b.x, b.y);
                float len = (pb - pa).magnitude; if (len < 0.05f) continue;
                var m = Matrix4x4.TRS((pa + pb) / 2f, Quaternion.LookRotation((pb - pa).normalized, Vector3.right), new Vector3(wr, wr, len));
                cis.Add(new CombineInstance { mesh = cube, transform = m });
            }
        }
        var wm = new Mesh { name = "WireFence_Wires" }; wm.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        wm.CombineMeshes(cis.ToArray(), true, true); wm.RecalculateBounds();
        var wires = new GameObject("Wires"); wires.transform.SetParent(f, false);
        wires.AddComponent<MeshFilter>().sharedMesh = FuwaGearTest.SaveMesh(wm);
        wires.AddComponent<MeshRenderer>().sharedMaterial = wireM;
        // 枠と柱
        Prim(PrimitiveType.Cube, f, "FrameTop", new Vector3(0f, y1, 0f), new Vector3(0.06f, 0.06f, w + 0.06f), frameM);
        Prim(PrimitiveType.Cube, f, "FrameBottom", new Vector3(0f, y0, 0f), new Vector3(0.06f, 0.06f, w + 0.06f), frameM);
        Prim(PrimitiveType.Cube, f, "PostL", new Vector3(0f, (y1) / 2f, -w / 2f), new Vector3(0.07f, y1 + 0.03f, 0.07f), frameM);
        Prim(PrimitiveType.Cube, f, "PostR", new Vector3(0f, (y1) / 2f, w / 2f), new Vector3(0.07f, y1 + 0.03f, 0.07f), frameM);
        // 当たり
        var col = new GameObject("Collider"); col.transform.SetParent(f, false);
        var bc = col.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, (y0 + y1) / 2f, 0f); bc.size = new Vector3(0.06f, h + 0.1f, w + 0.06f);
        var bo = UdonSharpUndo.AddComponent<FuwaBounce>(col);
        bo.useContactNormal = true; bo.alwaysUp = false; bo.bounceSpeed = 0.6f; bo.keepTangent = 0.85f;
        UdonSharpEditorUtility.CopyProxyToUdon(bo); FuwaStage2WaterTest.NoSync(bo);
        return f;
    }

    static Mesh _cube;
    static Mesh GetCube()
    {
        if (_cube) return _cube;
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); _cube = g.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(g);
        return _cube;
    }

    static Vector3 Polar(float deg, float r) { float a = deg * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); }

    static void Floor(Transform root, string name, Vector3 c, float deg, Vector2 size, Material m)
    {
        var g = new GameObject(name); g.transform.SetParent(root, false); g.layer = 11;
        g.transform.localPosition = c + new Vector3(0f, -0.2f, 0f); g.transform.localRotation = Quaternion.Euler(0f, -deg, 0f);   // 厚み 0.4（床下の歯車にかからない）
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
