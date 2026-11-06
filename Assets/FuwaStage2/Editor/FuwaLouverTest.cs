using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2–4 はぐるまのとう：ルーバー（羽板）の風の道のお試し（2026-11-06）
// 送風機 → ルーバー（羽板6枚）→ 谷の上の風の道。羽板はラック＆小歯車で回し、ラックはクランク（ピストンと同じ動き）で動かす
// ・羽板の間隔 = ラックの歯の間隔 × 5 にして、どの小歯車もラックと同じかみ合い方（同じ角度）になるように
// ・2D の (x, y) は Unity の (x, z)。2D の反時計回りは Unity の Y 回転でマイナス
// ・ワールドの向きのまま（root は回さない）。風は南（-Z）へ
public static class FuwaLouverTest
{
    const float Mod = 0.5f / (5f * Mathf.PI);   // 羽板の間隔 0.5m = 5 × π m
    const int ZP = 13;                          // 羽板の小歯車
    const int NSlat = 6;
    const float SlatH = 3f, SlatY = 0.3f;
    const float CrankMod = 0.04f; const int ZC = 24, ZM = 12;
    const float RodL = 0.6f;
    static readonly Vector3 Origin = new Vector3(-67f, -162f, -128f);   // ルーバーのまん中の足もと（床の南のはし）

    [MenuItem("FuwaCourse/ステージ2/ルーバーの風の道のお試しを作る")]
    public static void Build()
    {
        Remove();
        float rp = Mod * ZP / 2f, p = Mathf.PI * Mod;
        float crankR = rp * Mathf.PI / 4f;          // ラックが 2c 動く = 羽板 90°
        var dark = FuwaGearTest.Mat("GearTest_Gear", new Color(0.36f, 0.38f, 0.43f));
        var light = FuwaGearTest.Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f));
        var axleM = FuwaGearTest.Mat("GearTest_Axle", new Color(0.2f, 0.21f, 0.24f));
        var brass = FuwaGearTest.Mat("FanTest_Brass", new Color(0.86f, 0.68f, 0.32f));
        var floorM = FuwaGearTest.Mat("GearTest_Floor", new Color(0.55f, 0.58f, 0.63f));
        var slatM = FuwaGearTest.Mat("LouverTest_Slat", new Color(0.78f, 0.8f, 0.84f));
        var bladeM = FuwaGearTest.Mat("FanTest_Blade", new Color(0.55f, 0.82f, 0.95f));

        var root = new GameObject("LouverTest").transform; root.position = Origin;
        // 床：扇風機の広場から南へ通路 → ルーバーの手前の床 → 谷（横に細い道）→ 向こうの床
        Floor(root, "Walkway", new Vector3(0f, 0f, 14f), new Vector2(4f, 4f), floorM);          // z -116〜-112
        Floor(root, "StartFloor", new Vector3(0f, 0f, 6f), new Vector2(12f, 12f), floorM);       // z -128〜-116
        Floor(root, "Catwalk", new Vector3(-3.25f, 0f, -6f), new Vector2(1.5f, 12f), floorM);    // 谷の横の細い道
        Floor(root, "EndFloor", new Vector3(0f, 0f, -16f), new Vector2(12f, 8f), floorM);        // z -148〜-140

        // ルーバーの枠
        float x0 = -(NSlat - 1) * 0.5f / 2f;
        float topY = SlatY + SlatH;
        Prim(PrimitiveType.Cube, root, "FrameL", new Vector3(x0 - 0.4f, (topY + 0.3f) / 2f, 0f), new Vector3(0.15f, topY + 0.3f, 0.3f), light);
        Prim(PrimitiveType.Cube, root, "FrameR", new Vector3(-x0 + 0.4f, (topY + 0.3f) / 2f, 0f), new Vector3(0.15f, topY + 0.3f, 0.3f), light);
        Prim(PrimitiveType.Cube, root, "FrameBottom", new Vector3(0f, SlatY - 0.08f, 0f), new Vector3(-2f * x0 + 0.95f, 0.12f, 0.3f), light);
        // 上の枠はなし（ラックと小歯車が見えるように）

        // 羽板（それぞれの軸の上に小歯車）。0°で閉じる（板が X にそって風をふさぐ）
        var slats = new Transform[NSlat];
        var pinMesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZP, 8, Mod), 0.08f, "Louver_Pinion_z13", Vector2.zero));
        float gearY = topY + 0.12f;
        for (int i = 0; i < NSlat; i++)
        {
            var s = new GameObject("Slat" + i).transform; s.SetParent(root, false);
            s.localPosition = new Vector3(x0 + i * 0.5f, 0f, 0f);
            var v = Prim(PrimitiveType.Cube, s, "Board", new Vector3(0f, SlatY + SlatH / 2f, 0f), new Vector3(0.49f, SlatH, 0.05f), slatM);
            v.gameObject.AddComponent<BoxCollider>();
            Prim(PrimitiveType.Cylinder, s, "Axle", new Vector3(0f, (SlatY + gearY + 0.1f) / 2f, 0f), new Vector3(0.05f, (gearY + 0.1f - SlatY + 0.2f) / 2f, 0.05f), axleM);
            var pg = new GameObject("Pinion").transform; pg.SetParent(s, false);
            pg.localPosition = new Vector3(0f, gearY, 0f);
            pg.localRotation = Quaternion.Euler(0f, 90f, 0f);   // 歯の1本がラック（-z）を向く
            pg.gameObject.AddComponent<MeshFilter>().sharedMesh = pinMesh;
            pg.gameObject.AddComponent<MeshRenderer>().sharedMaterial = brass;
            slats[i] = s;
        }

        // ラック（羽板の南がわ、歯は北向き）。ラックのローカル原点 = 羽板0 の x、閉じた時
        float travel = 2f * crankR;
        int kMin = -3, kMax = Mathf.CeilToInt(((NSlat - 1) * 0.5f + travel) / p) + 3;
        var rack = new GameObject("Rack").transform; rack.SetParent(root, false);
        Vector3 rackClosed = new Vector3(x0, gearY, -rp);
        rack.localPosition = rackClosed;
        var rmesh = FuwaGearTest.SaveMesh(RackMesh(kMin, kMax, 0.08f, "Louver_Rack"));
        rack.gameObject.AddComponent<MeshFilter>().sharedMesh = rmesh;
        rack.gameObject.AddComponent<MeshRenderer>().sharedMaterial = dark;
        float hf = 1.25f * Mod, baseW = 0.06f;
        float rackBodyZ = -hf - baseW / 2f;                       // ラックの体のまん中（ラックのローカル z）
        float rackEndX = kMax * p;
        var rackEnd = new GameObject("RackEnd").transform; rackEnd.SetParent(rack, false); rackEnd.localPosition = new Vector3(rackEndX, 0.04f, rackBodyZ);
        Prim(PrimitiveType.Sphere, rackEnd, "Pin", Vector3.zero, Vector3.one * 0.06f, axleM);
        // ラックの受け（動かない）
        foreach (float bx in new[] { -0.2f, (NSlat - 1) * 0.5f + 0.2f })
            Prim(PrimitiveType.Cube, root, "RackGuide", new Vector3(x0 + bx, gearY + 0.12f, -rp + rackBodyZ), new Vector3(0.08f, 0.16f, 0.1f), light);

        // クランク：ラックの線の東。ピン（φ=0）は西を向く → ラックの東のはしから l 離れる
        Vector3 C = rackClosed + new Vector3(rackEndX + crankR + RodL, 0f, rackBodyZ);
        float crankY = gearY - 0.22f;                              // 歯車はラックより下（上から見て重なるので段をずらす）
        var crank = new GameObject("CrankWheel_z24").transform; crank.SetParent(root, false); crank.localPosition = new Vector3(C.x, crankY, C.z);
        var cmesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZC, 8, CrankMod), 0.08f, "Louver_Crank_z24", Vector2.zero));
        crank.gameObject.AddComponent<MeshFilter>().sharedMesh = cmesh; crank.gameObject.AddComponent<MeshRenderer>().sharedMaterial = brass;
        var cpin = new GameObject("CrankPin").transform; cpin.SetParent(crank, false); cpin.localPosition = new Vector3(-crankR, gearY + 0.04f - crankY, 0f);   // 2D で 180°
        Prim(PrimitiveType.Cylinder, cpin, "Post", new Vector3(0f, -(gearY + 0.04f - crankY) / 2f + 0.04f, 0f), new Vector3(0.04f, (gearY + 0.04f - crankY) / 2f, 0.04f), axleM);
        Prim(PrimitiveType.Sphere, cpin, "Ball", Vector3.zero, Vector3.one * 0.07f, axleM);
        Prim(PrimitiveType.Cylinder, root, "CrankAxle", new Vector3(C.x, crankY - 0.3f, C.z), new Vector3(0.06f, 0.35f, 0.06f), axleM);
        var rod = Prim(PrimitiveType.Cube, root, "Rod", Vector3.zero, new Vector3(0.05f, 0.05f, RodL), light);
        // モーターの小歯車（クランクの東）
        var cg = new FuwaGearTest.Gear { name = "c", z = ZC, c = Vector2.zero, ratio = -1f, phase = 0f };
        var mg = FuwaGearTest.Mesh2(cg, "m", ZM, 0f, CrankMod);
        var motor = new GameObject("MotorPinion_z12").transform; motor.SetParent(root, false); motor.localPosition = new Vector3(C.x + mg.c.x, crankY, C.z + mg.c.y);
        var mmesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZM, 8, CrankMod), 0.08f, "Louver_Motor_z12", Vector2.zero));
        motor.gameObject.AddComponent<MeshFilter>().sharedMesh = mmesh; motor.gameObject.AddComponent<MeshRenderer>().sharedMaterial = dark;
        Prim(PrimitiveType.Cylinder, root, "MotorBody", new Vector3(C.x + mg.c.x, crankY - 0.3f, C.z + mg.c.y), new Vector3(0.3f, 0.25f, 0.3f), light);
        // 台（クランクとモーターをのせる）
        Prim(PrimitiveType.Cube, root, "DrivePost", new Vector3(C.x + 0.3f, (crankY - 0.55f) / 2f, C.z), new Vector3(0.2f, crankY - 0.55f, 0.2f), light);
        Prim(PrimitiveType.Cube, root, "DriveTable", new Vector3(C.x + 0.3f, crankY - 0.6f, C.z), new Vector3(1.6f, 0.08f, 0.9f), light);

        // 送風機（北のはし、南を向く）
        var fan = new GameObject("Blower").transform; fan.SetParent(root, false); fan.localPosition = new Vector3(0f, SlatY + SlatH / 2f, 11.4f);
        fan.localRotation = Quaternion.LookRotation(Vector3.back);
        for (int k = 0; k < 24; k++)
        {
            float a0 = k * 15f * Mathf.Deg2Rad, a1 = (k + 1) * 15f * Mathf.Deg2Rad;
            Bar(fan, "Ring" + k, new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0f) * 1.55f, new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0f) * 1.55f, 0.12f, light);
        }
        Prim(PrimitiveType.Cylinder, fan, "Post", new Vector3(0f, -(SlatY + SlatH / 2f) / 2f, 0.2f), new Vector3(0.25f, (SlatY + SlatH / 2f) / 2f, 0.25f), dark);
        var rotor = new GameObject("Rotor").transform; rotor.SetParent(fan, false);
        Prim(PrimitiveType.Sphere, rotor, "Hub", Vector3.zero, new Vector3(0.4f, 0.4f, 0.35f), brass);
        for (int k = 0; k < 5; k++)
        {
            var bl = new GameObject("Blade" + k).transform; bl.SetParent(rotor, false); bl.localRotation = Quaternion.Euler(0f, 0f, k * 72f);
            Prim(PrimitiveType.Sphere, bl, "V", new Vector3(0.78f, 0f, 0f), new Vector3(1.35f, 0.45f, 0.05f), bladeM).localRotation = Quaternion.Euler(30f, 0f, 0f);
        }
        var sp = UdonSharpUndo.AddComponent<FuwaSpinner>(rotor.gameObject);
        sp.axis = Vector3.forward; sp.degPerSec = 540f;
        UdonSharpEditorUtility.CopyProxyToUdon(sp); FuwaStage2WaterTest.NoSync(sp);

        // 風の道：送風機の前から向こうの床の手前まで（コース5の突風の風と粒を写す）
        FuwaWindArea windArea = null;
        var src = GameObject.Find("FuwaCourse5/Gimmick_DeckGust/GustZone");
        if (src)
        {
            var gz = Object.Instantiate(src, root); gz.name = "Wind";
            var bc = gz.GetComponent<BoxCollider>();
            float zN = 11f, zS = -12f, len = zN - zS;
            gz.transform.localRotation = Quaternion.LookRotation(Vector3.back);
            gz.transform.localScale = new Vector3(3f, 3f / bc.size.y, len / bc.size.z);
            gz.transform.localPosition = new Vector3(0f, SlatY + SlatH / 2f - bc.center.y * gz.transform.localScale.y, (zN + zS) / 2f);
            windArea = gz.GetComponent<FuwaWindArea>();
            var srcW = src.GetComponent<FuwaWindArea>();
            windArea.offSeconds = 0f; windArea.fadeAlongDirection = 0f;
            foreach (Transform sib in src.transform.parent)
            {
                if (!sib.name.StartsWith("GustStreaks")) continue;
                // 粒はルーバーの出口（谷の上）に、風の向きにそろえて
                var c = Object.Instantiate(sib.gameObject, root); c.name = sib.name;
                c.transform.position = root.TransformPoint(new Vector3(0f, SlatY + SlatH / 2f, -1f));
                c.transform.rotation = root.rotation * Quaternion.LookRotation(Vector3.back) * Quaternion.Inverse(src.transform.rotation) * sib.rotation;
                if (srcW.visual == sib.gameObject) windArea.visual = c;
                if (windArea.extraVisuals != null) for (int i = 0; i < windArea.extraVisuals.Length; i++) if (srcW.extraVisuals[i] != null && srcW.extraVisuals[i].gameObject == sib.gameObject) windArea.extraVisuals[i] = c.GetComponent<ParticleSystem>();
            }
            UdonSharpEditorUtility.CopyProxyToUdon(windArea);
        }

        // 回す人
        var lv = UdonSharpUndo.AddComponent<FuwaLouver>(root.gameObject);
        lv.slats = slats; lv.rack = rack; lv.crankWheel = crank; lv.motorPinion = motor; lv.crankPin = cpin; lv.rackEnd = rackEnd; lv.rod = rod;
        lv.wind = windArea; lv.maxStrength = 6f; lv.period = 9f;
        lv.crankR = crankR; lv.rodL = RodL; lv.pinionR = rp; lv.rackClosedLocal = rackClosed;
        lv.crankRatio = -1f; lv.crankPhase = 0f; lv.motorRatio = mg.ratio; lv.motorPhase = mg.phase * Mathf.Rad2Deg;
        UdonSharpEditorUtility.CopyProxyToUdon(lv); FuwaStage2WaterTest.NoSync(lv);

        // ふわふわを出す台
        var spot = new GameObject("BallSpot").transform; spot.SetParent(root, false); spot.localPosition = new Vector3(0f, 1.6f, 8f);
        FuwaStage2WaterTest.BallStand(root, "BallStand", new Vector3(4.5f, 0f, 8f), spot, "ふわふわを出す", new Color(1f, 0.75f, 0.85f));

        SetPhase(0f);
        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ルーバーのお試し] 作った m={Mod:F4} rp={rp:F3} crankR={crankR:F3}");
    }

    public static void SetPhase(float phi)
    {
        var o = GameObject.Find("LouverTest"); if (!o) return;
        var lv = o.GetComponent<FuwaLouver>(); if (lv) lv.Apply(phi);
    }

    [MenuItem("FuwaCourse/ステージ2/ルーバーの風の道のお試しを消す")]
    public static void Remove() { var o = GameObject.Find("LouverTest"); if (o) Object.DestroyImmediate(o); }

    // ラック（x にのびる、ピッチ線 z=0、歯は +z）。歯の真ん中は (k+0.5)p → x = 0 がすき間の真ん中
    static Mesh RackMesh(int kMin, int kMax, float thick, string name)
    {
        float p = Mathf.PI * Mod, ha = Mod, hf = 1.25f * Mod, ta = Mathf.Tan(FuwaGearTest.Alpha), baseW = 0.06f;
        var list = new System.Collections.Generic.List<Vector2[]>();
        list.Add(new[] { new Vector2(kMin * p, -hf - baseW), new Vector2(kMax * p, -hf - baseW), new Vector2(kMax * p, -hf), new Vector2(kMin * p, -hf) });
        for (int k = kMin; k < kMax; k++)
        {
            float c = (k + 0.5f) * p, q4 = p / 4f - FuwaGearTest.Backlash * 0.1f;
            list.Add(new[] { new Vector2(c - q4 - hf * ta, -hf), new Vector2(c + q4 + hf * ta, -hf), new Vector2(c + q4 - ha * ta, ha), new Vector2(c - q4 + ha * ta, ha) });
        }
        var cis = new CombineInstance[list.Count];
        for (int i = 0; i < list.Count; i++) { var q = list[i]; cis[i] = new CombineInstance { mesh = FuwaGearTest.Extrude(q, thick, "q", (q[0] + q[1] + q[2] + q[3]) / 4f), transform = Matrix4x4.identity }; }
        var m = new Mesh { name = name }; m.CombineMeshes(cis, true); m.RecalculateBounds();
        return m;
    }

    static void Floor(Transform root, string name, Vector3 c, Vector2 size, Material m)
    {
        var g = new GameObject(name); g.transform.SetParent(root, false); g.layer = 11;
        g.transform.localPosition = c + new Vector3(0f, -0.5f, 0f);
        Prim(PrimitiveType.Cube, g.transform, "Visual", Vector3.zero, new Vector3(size.x, 1f, size.y), m);
        g.AddComponent<BoxCollider>().size = new Vector3(size.x, 1f, size.y);
    }

    static Transform Bar(Transform parent, string name, Vector3 a, Vector3 b, float thick, Material m)
    {
        var g = Prim(PrimitiveType.Cube, parent, name, (a + b) / 2f, new Vector3(thick, thick, (b - a).magnitude + thick), m);
        if ((b - a).sqrMagnitude > 1e-6f) g.localRotation = Quaternion.LookRotation((b - a).normalized, Vector3.up);
        return g;
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
