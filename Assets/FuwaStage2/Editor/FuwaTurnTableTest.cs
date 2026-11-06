using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2–4：撃って回す回転床のお試し（2026-11-06）
// まん中に動かない太陽歯車（24枚）、回転床に乗った遊星歯車（12枚）と撃つ用の羽根車。橋は2本（120° はなれ）
// 入口（西）に1本目を合わせて乗り、羽根車を撃って回して、2本目を出口（2D で 60°）に合わせる。300° は行き止まり
// ・2D の (x, y) は Unity の (x, z)。2D の反時計回りは Unity の Y 回転でマイナス
public static class FuwaTurnTableTest
{
    const float Mod = 0.12f; const int ZS = 24, ZPl = 12;
    const float DeckR = 4.6f, BridgeEnd = 10f, BridgeW = 2.5f;
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

        // 太陽歯車（動かない）と、まん中の柱
        float gy = 0.05f, gTh = 0.25f;
        var sun = new GameObject("Sun_z24").transform; sun.SetParent(root, false); sun.localPosition = new Vector3(0f, gy, 0f);
        sun.gameObject.AddComponent<MeshFilter>().sharedMesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZS, 8, Mod), gTh, "TurnTable_Sun_z24", Vector2.zero));
        sun.gameObject.AddComponent<MeshRenderer>().sharedMaterial = dark;
        Prim(PrimitiveType.Cylinder, root, "SunPost", new Vector3(0f, -0.6f, 0f), new Vector3(0.5f, 1.0f, 0.5f), axleM);

        // 遊星歯車（回転床の東、太陽歯車とかみ合う）と羽根車
        var sg = new FuwaGearTest.Gear { name = "s", z = ZS, c = Vector2.zero, ratio = 1f, phase = 0f };
        var pg = FuwaGearTest.Mesh2(sg, "p", ZPl, 0f, Mod);
        var planet = new GameObject("Planet_z12").transform; planet.SetParent(table, false); planet.localPosition = new Vector3(pg.c.x, gy, pg.c.y);
        planet.gameObject.AddComponent<MeshFilter>().sharedMesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZPl, 8, Mod), gTh, "TurnTable_Planet_z12", Vector2.zero));
        planet.gameObject.AddComponent<MeshRenderer>().sharedMaterial = brass;
        Prim(PrimitiveType.Cylinder, planet, "Axle", new Vector3(0f, 0.55f, 0f), new Vector3(0.09f, 0.55f, 0.09f), axleM);
        var handle = new GameObject("Handle").transform; handle.SetParent(planet, false); handle.localPosition = new Vector3(0f, 1.05f, 0f);
        handle.gameObject.layer = 2;
        Prim(PrimitiveType.Cylinder, handle, "Hub", Vector3.zero, new Vector3(0.22f, 0.15f, 0.22f), brass);
        for (int k = 0; k < 6; k++)
        {
            var pd = Prim(PrimitiveType.Cube, handle, "Paddle" + k, Quaternion.Euler(0f, k * 60f, 0f) * new Vector3(0.45f, 0f, 0f), new Vector3(0.7f, 0.3f, 0.04f), bladeM);
            pd.localRotation = Quaternion.Euler(0f, k * 60f, 0f);
        }
        var sc = handle.gameObject.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 0.9f;
        // 回転床の下の支え（動かない）
        Prim(PrimitiveType.Cylinder, root, "Pedestal", new Vector3(0f, -1.6f, 0f), new Vector3(2.4f, 1.2f, 2.4f), dark);

        var tt = UdonSharpUndo.AddComponent<FuwaTurnTable>(handle.gameObject);
        tt.table = table; tt.planet = planet; tt.handle = handle;
        tt.sunTeeth = ZS; tt.planetTeeth = ZPl; tt.planetPhase = pg.phase * Mathf.Rad2Deg;
        tt.hitRadius = 0.9f; tt.startDeg = 0f;
        UdonSharpEditorUtility.CopyProxyToUdon(tt); FuwaStage2WaterTest.NoSync(tt);

        // 金網の壁（羽根車の向こう＝東。てじゃ「フワフワをぶつけながら維持できる壁」「金網なら向こうも見える」）。回転床と一緒に回る
        WireFence(table, new Vector3(pg.c.x + 1.7f, 0f, pg.c.y), 0f, 3f, 2.2f, light, axleM);
        SetTable(0f);

        // ふわふわを出す台
        var spot = new GameObject("BallSpot").transform; spot.SetParent(root, false); spot.localPosition = new Vector3(x0 + 2f, 1.6f, 0f);
        FuwaStage2WaterTest.BallStand(root, "BallStand", new Vector3(x0 + 1.2f, 0f, -1.1f), spot, "ふわふわを出す", new Color(1f, 0.75f, 0.85f));

        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[回転床のお試し] 作った");
    }

    // エディタ用：回転床を th 度（2D）にする（FuwaTurnTable と同じ計算）
    public static void SetTable(float th)
    {
        var o = GameObject.Find("TurnTableTest"); if (!o) return;
        var tt = o.GetComponentInChildren<FuwaTurnTable>(true); if (!tt) return;
        tt.table.localRotation = Quaternion.Euler(0f, -th, 0f);
        float phi = (float)tt.sunTeeth / tt.planetTeeth * th + tt.planetPhase;
        tt.planet.localRotation = Quaternion.Euler(0f, -Mathf.Repeat(phi, 360f), 0f);
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
        g.transform.localPosition = c + new Vector3(0f, -0.5f, 0f); g.transform.localRotation = Quaternion.Euler(0f, -deg, 0f);
        Prim(PrimitiveType.Cube, g.transform, "Visual", Vector3.zero, new Vector3(size.x, 1f, size.y), m);
        g.AddComponent<BoxCollider>().size = new Vector3(size.x, 1f, size.y);
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
