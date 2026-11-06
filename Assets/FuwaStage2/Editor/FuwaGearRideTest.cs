using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2–4 序盤：乗って渡る、ゆっくり回り続ける歯車のお試し（2026-11-06。てじゃ「序盤は歯車乗るのがベタでいい」「かなりゆっくりにしよう」）
//  入口の台 → 大きい歯車 G1（20枚）→ 小さい歯車 G2（12枚、速く回る）→ 大きい歯車 G3（20枚）→ 出口の台。ジグザグ（+30°、−30°）
//  かみ合う所は2枚の歯の表面が同じ向き・同じ速さ（ピッチ円の速さ）なので、そこで乗り移る
//  歯車の上の面＝床の高さ。落ちたら下の受け床（階段で入口へ戻る）
// ・2D の (x, y) は Unity の (x, z)。2D の反時計回りは Unity の Y 回転でマイナス
public static class FuwaGearRideTest
{
    const float Mod = 0.4f, Th = 0.6f, Gap = 0.15f;
    const int ZBig = 20, ZSmall = 12;
    public static float PitchSpeed = 0.15f;   // ピッチ円の速さ（m/秒）。てじゃ「かなりゆっくり」
    static readonly Vector3 Origin = new Vector3(50f, -162f, -104f);   // 入口の台の東のはし、床の上の高さ
    const float CatchY = -3f;

    [MenuItem("FuwaCourse/ステージ2/乗る歯車のお試しを作る")]
    public static void Build()
    {
        Remove();
        var dark = FuwaGearTest.Mat("GearTest_Gear", new Color(0.36f, 0.38f, 0.43f));
        var light = FuwaGearTest.Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f));
        var brass = FuwaGearTest.Mat("FanTest_Brass", new Color(0.86f, 0.68f, 0.32f));
        var axleM = FuwaGearTest.Mat("GearTest_Axle", new Color(0.2f, 0.21f, 0.24f));
        var floorM = FuwaGearTest.Mat("GearTest_Floor", new Color(0.55f, 0.58f, 0.63f));
        var under = FuwaGearTest.Mat("GearRideTest_Under", new Color(0.78f, 0.8f, 0.85f));

        var root = new GameObject("GearRideTest").transform; root.position = Origin;
        float tipBig = Mod * ZBig / 2f + Mod;
        var g1 = new FuwaGearTest.Gear { name = "G1_z" + ZBig, z = ZBig, c = new Vector2(tipBig + Gap, 0f), ratio = 1f, phase = 0f };
        var g2 = FuwaGearTest.Mesh2(g1, "G2_z" + ZSmall, ZSmall, 30f, Mod);
        var g3 = FuwaGearTest.Mesh2(g2, "G3_z" + ZBig, ZBig, -30f, Mod);
        var gs = new[] { g1, g2, g3 };

        Floor(root, "Entry", new Vector3(-2f, 0f, 0f), new Vector2(4f, 4f), floorM);
        float exitX = g3.c.x + tipBig + Gap;
        Floor(root, "Exit", new Vector3(exitX + 2f, 0f, 0f), new Vector2(4f, 4f), floorM);

        var meshBig = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZBig, 8, Mod), Th, "GearRide_z" + ZBig, Vector2.zero));
        var meshSmall = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZSmall, 8, Mod), Th, "GearRide_z" + ZSmall, Vector2.zero));
        var ts = new Transform[3]; var ratio = new float[3]; var phase = new float[3];
        for (int i = 0; i < 3; i++)
        {
            var g = gs[i];
            var t = new GameObject(g.name).transform; t.SetParent(root, false); t.localPosition = new Vector3(g.c.x, 0f, g.c.y);
            var body = new GameObject("Body"); body.transform.SetParent(t, false); body.transform.localPosition = new Vector3(0f, -Th, 0f);
            body.layer = 11;
            var mesh = g.z == ZBig ? meshBig : meshSmall;
            body.AddComponent<MeshFilter>().sharedMesh = mesh; body.AddComponent<MeshRenderer>().sharedMaterial = i == 1 ? brass : light;
            body.AddComponent<MeshCollider>().sharedMesh = mesh;
            var rb = t.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            // まん中の印（回ってるのが分かるように、軸のキャップと1本の線）
            Prim(PrimitiveType.Cylinder, t, "Cap", new Vector3(0f, 0.005f, 0f), new Vector3(0.6f, 0.005f, 0.6f), dark);
            Prim(PrimitiveType.Cube, t, "Mark", new Vector3(g.z * Mod / 4f, 0.004f, 0f), new Vector3(g.z * Mod / 2f - 0.5f, 0.008f, 0.12f), dark);
            // 軸（受け床まで）
            Prim(PrimitiveType.Cylinder, root, "Axle_" + g.name, new Vector3(g.c.x, (CatchY - Th) / 2f, g.c.y), new Vector3(0.3f, (-Th - CatchY) / 2f, 0.3f), axleM);
            ts[i] = t; ratio[i] = g.ratio; phase[i] = g.phase * Mathf.Rad2Deg;
        }

        // 落ちた時の受け床と、入口へ戻る階段（0.3m × 10段）
        float x0 = -13f, x1 = exitX + 5f;
        var cf = Floor(root, "CatchFloor", new Vector3((x0 + x1) / 2f, CatchY, 1.5f), new Vector2(x1 - x0, 14f), under);
        for (int k = 0; k < 10; k++)
        {
            float top = CatchY + 0.3f * (k + 1);
            float cx = -4f - (9 - k) * 0.8f - 0.4f;
            var st = new GameObject("Step" + k); st.transform.SetParent(root, false); st.layer = 11;
            st.transform.localPosition = new Vector3(cx, (top + CatchY) / 2f, 0f);
            Prim(PrimitiveType.Cube, st.transform, "Visual", Vector3.zero, new Vector3(0.8f, top - CatchY, 2f), floorM);
            st.AddComponent<BoxCollider>().size = new Vector3(0.8f, top - CatchY, 2f);
        }

        var ride = UdonSharpUndo.AddComponent<FuwaGearRide>(root.gameObject);
        ride.gears = ts; ride.ratio = ratio; ride.phase = phase;
        ride.speed = PitchSpeed / (Mod * ZBig / 2f) * Mathf.Rad2Deg;   // G1 のピッチ円の速さ → 角速度
        UdonSharpEditorUtility.CopyProxyToUdon(ride); FuwaStage2WaterTest.NoSync(ride);
        SetA(0f);

        // ふわふわを出す台・ワープ（回転床のお試しの入口の近く ⇄ ここ）
        var spot = new GameObject("BallSpot").transform; spot.SetParent(root, false); spot.localPosition = new Vector3(-2f, 1.6f, 0.8f);
        FuwaStage2WaterTest.BallStand(root, "BallStand", new Vector3(-3.4f, 0f, 1.4f), spot, "ふわふわを出す", new Color(1f, 0.75f, 0.85f));
        var spawn = new GameObject("Spawn").transform; spawn.SetParent(root, false); spawn.localPosition = new Vector3(-2.5f, 0f, -1f); spawn.localRotation = Quaternion.Euler(0f, 90f, 0f);
        var warpMat = FuwaGearTest.Mat("GearRideTest_Warp", new Color(1f, 0.85f, 0.4f));
        var tt = GameObject.Find("TurnTableTest");
        if (tt)
        {
            var back = new GameObject("BackSpot").transform; back.SetParent(root, false); back.position = tt.transform.position + new Vector3(-16f, 0f, -0.5f); back.rotation = Quaternion.Euler(0f, 90f, 0f);
            FuwaIceTestBuilder.Warp(root, "WarpToRide", tt.transform.position + new Vector3(-15f, 0.35f, -2.0f), "GEAR RIDE", spawn, warpMat);
            FuwaIceTestBuilder.Warp(root, "WarpBack", root.TransformPoint(new Vector3(-3.4f, 0.35f, -1.5f)), "BACK", back, warpMat);
        }

        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[乗る歯車] 作った G1 {ride.speed:F2}°/秒（1周 {360f / ride.speed:F0}秒）、G2 {ride.speed * ZBig / ZSmall:F2}°/秒");
    }

    // エディタ用：元の歯車の角度 a にする（FuwaGearRide と同じ計算）
    public static void SetA(float a)
    {
        var o = GameObject.Find("GearRideTest"); if (!o) return;
        var r = o.GetComponent<FuwaGearRide>(); if (!r) return;
        for (int i = 0; i < r.gears.Length; i++) r.gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(r.ratio[i] * a + r.phase[i], 360f), 0f);
    }

    [MenuItem("FuwaCourse/ステージ2/乗る歯車のお試しを消す")]
    public static void Remove() { var o = GameObject.Find("GearRideTest"); if (o) Object.DestroyImmediate(o); }

    static Transform Floor(Transform root, string name, Vector3 c, Vector2 size, Material m)
    {
        var g = new GameObject(name); g.transform.SetParent(root, false); g.layer = 11;
        g.transform.localPosition = c + new Vector3(0f, -0.2f, 0f);
        Prim(PrimitiveType.Cube, g.transform, "Visual", Vector3.zero, new Vector3(size.x, 0.4f, size.y), m);
        g.AddComponent<BoxCollider>().size = new Vector3(size.x, 0.4f, size.y);
        return g.transform;
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
