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
    public static float Step = 0.4f;     // 1段の高さ（ジャンプ力3で足が約0.46m上がる）
    const float ClearY = 0.04f;          // 上の段の大きい歯車と、下の段の歯車の上の面とのすき間
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
        // 2026-11-06 てじゃ「ジャンプで上がる程度の段差ほしい、歯車の組み合わせで」→ 段つき歯車（同じ軸に大20枚＋上に小12枚）で1段 Step m ずつ上がる
        //  G1（大、高さ0）→ G2（下：大＝G1とかみ合う、上：小＝1段上）→ G3（下：大、1段上＝G2の小とかみ合う、上：小＝2段上）→ G4（大、2段上）→ 出口（2段上）
        //  かみ合う2枚は同じ高さの層。上の段の大きい歯車は下の段の歯車の上を ClearY だけ浮いて通る
        //  回る速さ：G1＝G2、G3＝G2×12/20、G4＝G3×12/20（上の段ほどゆっくり）
        float tipBig = Mod * ZBig / 2f + Mod;
        var g1 = new FuwaGearTest.Gear { name = "G1", z = ZBig, c = new Vector2(tipBig + Gap, 0f), ratio = 1f, phase = 0f };
        var g2 = FuwaGearTest.Mesh2(g1, "G2", ZBig, 30f, Mod);
        var g2s = new FuwaGearTest.Gear { name = "G2s", z = ZSmall, c = g2.c, ratio = g2.ratio, phase = g2.phase };
        var g3 = FuwaGearTest.Mesh2(g2s, "G3", ZBig, -30f, Mod);
        var g3s = new FuwaGearTest.Gear { name = "G3s", z = ZSmall, c = g3.c, ratio = g3.ratio, phase = g3.phase };
        var g4 = FuwaGearTest.Mesh2(g3s, "G4", ZBig, 30f, Mod);
        var gs = new[] { g1, g2, g3, g4 };
        // 各歯車の段（歯数, 下の面, 上の面）。床の高さ＝上の面
        float S = Step;
        var parts = new[] {
            new[] { new Vector3(ZBig, -Th, 0f) },
            new[] { new Vector3(ZBig, -Th, 0f), new Vector3(ZSmall, 0f, S) },
            new[] { new Vector3(ZBig, ClearY, S), new Vector3(ZSmall, S, 2f * S) },
            new[] { new Vector3(ZBig, S + ClearY, 2f * S) },
        };

        Floor(root, "Entry", new Vector3(-2f, 0f, 0f), new Vector2(4f, 4f), floorM);
        float exitX = g4.c.x + tipBig + Gap;
        Floor(root, "Exit", new Vector3(exitX + 2f, 2f * S, g4.c.y), new Vector2(4f, 4f), floorM);

        var ts = new Transform[4]; var ratio = new float[4]; var phase = new float[4];
        for (int i = 0; i < 4; i++)
        {
            var g = gs[i];
            var t = new GameObject(g.name).transform; t.SetParent(root, false); t.localPosition = new Vector3(g.c.x, 0f, g.c.y);
            var rb = t.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            float lowest = 99f, topY = 0f; int topZ = ZBig;
            for (int k = 0; k < parts[i].Length; k++)
            {
                int z = (int)parts[i][k].x; float y0 = parts[i][k].y, y1 = parts[i][k].z;
                var mesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(z, 8, Mod), y1 - y0, "GearRide_z" + z + "_t" + Mathf.RoundToInt((y1 - y0) * 100f), Vector2.zero));
                var body = new GameObject("Body_z" + z); body.transform.SetParent(t, false); body.transform.localPosition = new Vector3(0f, y0, 0f);
                body.layer = 11;
                body.AddComponent<MeshFilter>().sharedMesh = mesh; body.AddComponent<MeshRenderer>().sharedMaterial = z == ZSmall ? brass : light;
                body.AddComponent<MeshCollider>().sharedMesh = mesh;
                lowest = Mathf.Min(lowest, y0); if (y1 >= topY) { topY = y1; topZ = z; }
            }
            // まん中の印（回ってるのが分かるように、軸のキャップと1本の線）。いちばん上の段に
            Prim(PrimitiveType.Cylinder, t, "Cap", new Vector3(0f, topY + 0.005f, 0f), new Vector3(0.6f, 0.005f, 0.6f), dark);
            Prim(PrimitiveType.Cube, t, "Mark", new Vector3(topZ * Mod / 4f, topY + 0.004f, 0f), new Vector3(topZ * Mod / 2f - 0.5f, 0.008f, 0.12f), dark);
            // 軸（受け床から歯車の下の面まで）
            Prim(PrimitiveType.Cylinder, root, "Axle_" + g.name, new Vector3(g.c.x, (CatchY + lowest) / 2f, g.c.y), new Vector3(0.3f, (lowest - CatchY) / 2f, 0.3f), axleM);
            ts[i] = t; ratio[i] = g.ratio; phase[i] = g.phase * Mathf.Rad2Deg;
        }

        // 落ちた時の受け床と、入口へ戻る階段（0.3m × 10段）
        float x0 = -13f, x1 = exitX + 5f;
        var cf = Floor(root, "CatchFloor", new Vector3((x0 + x1) / 2f, CatchY, 3.25f), new Vector2(x1 - x0, 18.5f), under);
        for (int k = 0; k < 10; k++)
        {
            float top = CatchY + 0.3f * (k + 1);
            float cx = -4f - (9 - k) * 0.8f - 0.4f;
            var st = new GameObject("Step" + k); st.transform.SetParent(root, false); st.layer = 11;
            st.transform.localPosition = new Vector3(cx, (top + CatchY) / 2f, 0f);
            Prim(PrimitiveType.Cube, st.transform, "Visual", Vector3.zero, new Vector3(0.8f, top - CatchY, 2f), floorM);
            st.AddComponent<BoxCollider>().size = new Vector3(0.8f, top - CatchY, 2f);
        }

        // オルゴール：G4 から歯車3段で速くして、シリンダーを回す（1周＝曲1ループ）
        var mb = MusicBox(root, g4, dark, light, brass, axleM, floorM);
        int musicIdx = ts.Length + mb.Length - 1;
        var ts2 = new Transform[ts.Length + mb.Length]; var ratio2 = new float[ts2.Length]; var phase2 = new float[ts2.Length];
        for (int i = 0; i < ts.Length; i++) { ts2[i] = ts[i]; ratio2[i] = ratio[i]; phase2[i] = phase[i]; }
        for (int i = 0; i < mb.Length; i++) { ts2[ts.Length + i] = mb[i].t; ratio2[ts.Length + i] = mb[i].g.ratio; phase2[ts.Length + i] = mb[i].g.phase * Mathf.Rad2Deg; }
        ts = ts2; ratio = ratio2; phase = phase2;

        var ride = UdonSharpUndo.AddComponent<FuwaGearRide>(root.gameObject);
        ride.gears = ts; ride.ratio = ratio; ride.phase = phase;
        ride.speed = PitchSpeed / (Mod * ZBig / 2f) * Mathf.Rad2Deg;   // G1 のピッチ円の速さ → 角速度
        float wC = ride.speed * Mathf.Abs(ratio[musicIdx]);
        MusicLen = 360f / wC;
        ride.musicGear = musicIdx; ride.musicLen = MusicLen;
        Pins(ts[musicIdx], Mathf.Sign(ratio[musicIdx]), brass);
        ride.music = MusicSource(ts[musicIdx].parent, ts[musicIdx].localPosition);
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

        // 動力：光るクリスタルの熱でボイラーをわかす蒸気機関（2026-11-06 てじゃ「光ってるクリスタルっぽいのからパワー得てる感じ」「仕組みのベースは蒸気機関」）
        //  たてのシリンダー → ピストン → 連接棒 → クランク軸（＋はずみ車）。クランク軸はコースの下までのびて、先がウォーム
        //  ウォーム（1条）→ G1 の軸のウォームホイール（WheelZ 枚）。ウォーム1回転でホイール歯1枚 → クランク軸は G1 の WheelZ 倍で回る
        //  2D：ホイールの中心から +x（Sd）にウォーム、ウォームの軸は +z（F）。首ふり扇風機（FuwaOscFanTest）と同じ置き方・同じ式
        Engine(root, ride, ts[0], g1, dark, light, brass, axleM, floorM);

        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[乗る歯車] 作った G1 {ride.speed:F2}°/秒（1周 {360f / ride.speed:F0}秒）、比 {string.Join(", ", ratio)}");
    }

    const int WheelZ = 16; const float WMod = 0.2f, WormR = 0.4f, WormLen = 1.2f, WheelTh = 0.3f, CrankY = -1.85f;
    const float EngineZ = -6.8f, BoxW = 1.6f;
    static void Engine(Transform root, FuwaGearRide ride, Transform g1T, FuwaGearTest.Gear g1, Material dark, Material light, Material brass, Material axleM, Material floorM)
    {
        // ウォームホイール（G1 の軸、G1 と一緒に回る）。歯の1本が +x（ウォームの方）を向く（2D で角度0＝歯の真ん中なので回さない）
        var wheel = new GameObject("WormWheel_z" + WheelZ).transform; wheel.SetParent(g1T, false); wheel.localPosition = new Vector3(0f, CrankY - WheelTh / 2f, 0f);
        // 歯はウォームのねじれ（進み角 λ、tanλ = ピッチ / ウォームの円周）にそろえて斜めに（はすば）。まっすぐだとねじの山と少しめりこんだ
        float tanLead = Mathf.PI * WMod / (2f * Mathf.PI * WormR);
        float twist = WheelTwistSign * WheelTh * tanLead / (WMod * WheelZ / 2f);
        var wmesh = FuwaGearTest.SaveMesh(ExtrudeTwist(FuwaGearTest.GearOutline(WheelZ, 8, WMod), WheelTh, twist, 8, "GearRide_WormWheel_z" + WheelZ));
        wheel.gameObject.AddComponent<MeshFilter>().sharedMesh = wmesh; wheel.gameObject.AddComponent<MeshRenderer>().sharedMaterial = brass;
        float rpW = WMod * WheelZ / 2f;
        // クランク軸の台（向き＝ローカル Z が +z）。ウォームの中心
        var mount = new GameObject("EngineMount").transform; mount.SetParent(root, false);
        mount.localPosition = new Vector3(g1.c.x + rpW + WormR, CrankY, 0f); mount.localRotation = Quaternion.identity;
        var crank = new GameObject("Crankshaft").transform; crank.SetParent(mount, false);
        var worm = new GameObject("Worm").transform; worm.SetParent(crank, false);
        worm.gameObject.AddComponent<MeshFilter>().sharedMesh = FuwaGearTest.SaveMesh(WormMesh());
        worm.gameObject.AddComponent<MeshRenderer>().sharedMaterial = dark;
        float rRoot = WormR - 1.25f * WMod;
        foreach (float e in new[] { -1f, 1f })
            Prim(PrimitiveType.Cylinder, worm, "Cap" + e, new Vector3(0f, 0f, e * WormLen / 2f), new Vector3(rRoot * 2f + 0.01f, 0.012f, rRoot * 2f + 0.01f), dark).localRotation = Quaternion.Euler(90f, 0f, 0f);
        // 軸（ウォームの先 → エンジン）
        // 2026-11-06 てじゃ「おもしろいけど仰々しい」→ B「小さい箱からちょこっと」：蒸気機関は箱の中（見せない）。箱の上にクリスタル、小さい煙突からたまに煙
        float z0 = WormLen / 2f + 0.3f, z1 = EngineZ + BoxW / 2f - 0.05f;
        Prim(PrimitiveType.Cylinder, crank, "Shaft", new Vector3(0f, 0f, (z0 + z1) / 2f), new Vector3(0.16f, (z0 - z1) / 2f, 0.16f), axleM).localRotation = Quaternion.Euler(90f, 0f, 0f);
        // 軸受け（受け床から）
        foreach (float bz in new[] { z0 - 0.15f, -WormLen / 2f - 0.3f, -3.5f })
        {
            float h = CrankY - CatchY;
            Prim(PrimitiveType.Cube, mount, "Bearing" + bz, new Vector3(0f, -h / 2f, bz), new Vector3(0.3f, h, 0.3f), light);
            Prim(PrimitiveType.Cylinder, mount, "BearingCap" + bz, new Vector3(0f, 0f, bz), new Vector3(0.34f, 0.12f, 0.34f), light).localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        // 箱（下は受け床の高さ、上はコースの床の高さ）。軸は箱の横の穴から出る
        float yb = CatchY - CrankY, yt = -CrankY;
        Prim(PrimitiveType.Cube, mount, "Box", new Vector3(0f, (yb + yt) / 2f, EngineZ), new Vector3(BoxW, yt - yb, BoxW), dark);
        Prim(PrimitiveType.Cube, mount, "BoxLid", new Vector3(0f, yt + 0.04f, EngineZ), new Vector3(BoxW + 0.12f, 0.08f, BoxW + 0.12f), light);
        Prim(PrimitiveType.Cylinder, mount, "ShaftCollar", new Vector3(0f, 0f, EngineZ + BoxW / 2f + 0.03f), new Vector3(0.36f, 0.03f, 0.36f), brass).localRotation = Quaternion.Euler(90f, 0f, 0f);
        // クリスタル（ふたの台座にささって、ゆっくり回る）
        Prim(PrimitiveType.Cylinder, mount, "CrystalSocket", new Vector3(0f, yt + 0.16f, EngineZ), new Vector3(0.9f, 0.08f, 0.9f), brass);
        var cryMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaStage2/Meshes/Course2_3/ObsCrystal1_Main.asset");
        var cryMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Stage2_3_Crystal.mat");
        if (cryMesh && cryMat)
        {
            var cry = new GameObject("Crystal").transform; cry.SetParent(mount, false); cry.localPosition = new Vector3(0f, yt + 0.12f, EngineZ);
            cry.localScale = Vector3.one * 0.6f;
            cry.gameObject.AddComponent<MeshFilter>().sharedMesh = cryMesh; cry.gameObject.AddComponent<MeshRenderer>().sharedMaterial = cryMat;
            var sp = UdonSharpUndo.AddComponent<FuwaSpinner>(cry.gameObject); sp.axis = Vector3.up; sp.degPerSec = 15f;
            UdonSharpEditorUtility.CopyProxyToUdon(sp); FuwaStage2WaterTest.NoSync(sp);
        }
        // 小さい煙突と煙（クランク1回転＝約10秒に1回）
        Vector3 chim = new Vector3(BoxW / 2f - 0.25f, yt, EngineZ - BoxW / 2f + 0.25f);
        Prim(PrimitiveType.Cylinder, mount, "Chimney", chim + new Vector3(0f, 0.35f, 0f), new Vector3(0.22f, 0.35f, 0.22f), light);
        var puffGo = new GameObject("Puff"); puffGo.transform.SetParent(mount, false); puffGo.transform.localPosition = chim + new Vector3(0f, 0.72f, 0f);
        puffGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = puffGo.AddComponent<ParticleSystem>();
        var main = ps.main; main.playOnAwake = false; main.startLifetime = 1.6f; main.startSpeed = 0.9f; main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 60; main.gravityModifier = -0.05f;
        var em = ps.emission; em.rateOverTime = 0f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 20f; sh.radius = 0.05f;
        var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.2f));
        var pr = puffGo.GetComponent<ParticleSystemRenderer>(); pr.renderMode = ParticleSystemRenderMode.Mesh;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere); pr.mesh = tmp.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(tmp);
        pr.sharedMaterial = FuwaGearTest.Mat("GearRideTest_Steam", new Color(0.97f, 0.98f, 1f));

        // ウォームの山と谷：ホイールの歯（+x）に向く所が谷になる角度（首ふり扇風機と同じ：phiC＋180°、phiC＝180° なので 0°）
        ride.crank = crank; ride.crankRatio = -WheelZ; ride.crankPhase = 0f;
        ride.engineMount = null; ride.crankPin = null; ride.piston = null; ride.rod = null; ride.puff = ps;
        UdonSharpEditorUtility.CopyProxyToUdon(ride);
    }

    public static float WheelTwistSign = -1f;   // 向きは計算で確かめた（-1 でめりこみ 0、+1 だと逆にねじれて増えた）
    // FuwaGearTest.Extrude と同じ面の向きで、高さにそって輪郭を回す（まん中の高さで回さない＝かみ合わせの位置はそのまま）
    static Mesh ExtrudeTwist(Vector2[] o, float thick, float twist, int layers, string name)
    {
        var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
        int n = o.Length;
        System.Func<Vector2, float, Vector3> P = (q, y) => { float ang = twist * (y / thick - 0.5f); float c = Mathf.Cos(ang), sn = Mathf.Sin(ang); return new Vector3(q.x * c - q.y * sn, y, q.x * sn + q.y * c); };
        int top = v.Count; v.Add(new Vector3(0f, thick, 0f)); foreach (var q in o) v.Add(P(q, thick));
        for (int i = 0; i < n; i++) { t.Add(top); t.Add(top + 1 + (i + 1) % n); t.Add(top + 1 + i); }
        int bot = v.Count; v.Add(Vector3.zero); foreach (var q in o) v.Add(P(q, 0f));
        for (int i = 0; i < n; i++) { t.Add(bot); t.Add(bot + 1 + i); t.Add(bot + 1 + (i + 1) % n); }
        for (int k = 0; k < layers; k++)
        {
            float y0 = thick * k / layers, y1 = thick * (k + 1) / layers;
            for (int i = 0; i < n; i++)
            {
                var a = o[i]; var b = o[(i + 1) % n]; int s0 = v.Count;
                v.Add(P(a, y0)); v.Add(P(b, y0)); v.Add(P(b, y1)); v.Add(P(a, y1));
                t.Add(s0); t.Add(s0 + 3); t.Add(s0 + 2); t.Add(s0); t.Add(s0 + 2); t.Add(s0 + 1);
            }
        }
        var m = new Mesh { name = name }; m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // ウォーム：軸（ローカル Z）まわりの 1 条ねじ（FuwaOscFanTest.WormMesh と同じ作り、寸法だけ）
    static Mesh WormMesh()
    {
        float p = Mathf.PI * WMod, ta = Mathf.Tan(20f * Mathf.Deg2Rad);
        float rTip = WormR + WMod, rRoot = WormR - 1.25f * WMod;
        int na = 64; int nz = Mathf.CeilToInt(WormLen / p * 28f);
        var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
        for (int iz = 0; iz <= nz; iz++)
        {
            float z = -WormLen / 2f + WormLen * iz / nz;
            for (int ia = 0; ia <= na; ia++)
            {
                float phi = 2f * Mathf.PI * ia / na;
                float u = Mathf.Repeat(z - p * phi / (2f * Mathf.PI), p);
                float d = Mathf.Min(u, p - u);
                float half = p / 4f - FuwaGearTest.Backlash * 0.1f;
                float rr = Mathf.Clamp(WormR + (half - d) / ta, rRoot, rTip);
                float k = Mathf.Clamp01((WormLen / 2f - Mathf.Abs(z)) / 0.15f);
                rr = Mathf.Lerp(rRoot, rr, Mathf.SmoothStep(0f, 1f, k));
                v.Add(new Vector3(rr * Mathf.Cos(phi), rr * Mathf.Sin(phi), z));
            }
        }
        int row = na + 1;
        for (int iz = 0; iz < nz; iz++)
            for (int ia = 0; ia < na; ia++)
            {
                int a0 = iz * row + ia, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                t.Add(a0); t.Add(a1); t.Add(b0); t.Add(a1); t.Add(b1); t.Add(b0);
            }
        var m = new Mesh { name = "GearRide_Worm" };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static Transform Bar(Transform parent, string name, Vector3 a, Vector3 b, float thick, Material m)
    {
        var g = Prim(PrimitiveType.Cube, parent, name, (a + b) / 2f, new Vector3(thick, thick, (b - a).magnitude + thick), m);
        if ((b - a).sqrMagnitude > 1e-6f) g.localRotation = Quaternion.LookRotation((b - a).normalized, Mathf.Abs(Vector3.Dot((b - a).normalized, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up);
        return g;
    }

    // ---- オルゴール（2026-11-06 てじゃ「Aいいなぁ 何箇所かで音出してて、あわさってBGMなってたらかわいい」「お試しでね」）----
    //  G4（大20枚, モジュール Mod）→ MA 小12枚（同じ高さの層で G4 とかみ合う）、同じ軸の上に MA2 大30枚（モジュール MMod）
    //  → MB 小10枚、同じ軸の上に MB2 大30枚 → MC 小10枚＝シリンダーの軸。G4 の 20/12×30/10×30/10＝15倍の速さ
    //  シリンダー1周＝曲1ループ（MusicLen 秒）。ピンは音符の時刻と高さどおりに並べて、くし（+x の向き）の所に来た時に鳴る
    public class MB { public Transform t; public FuwaGearTest.Gear g; }
    const float MMod = 0.1f, CylR = 0.35f, CylH = 0.8f, MTh = 0.1f;
    public static float MusicLen;
    static MB[] MusicBox(Transform root, FuwaGearTest.Gear g4, Material dark, Material light, Material brass, Material axleM, Material floorM)
    {
        float S = Step;
        var ma = FuwaGearTest.Mesh2(g4, "MA", ZSmall, 90f, Mod);
        var ma2 = new FuwaGearTest.Gear { name = "MA2", z = 30, c = ma.c, ratio = ma.ratio, phase = ma.phase };
        var mbg = FuwaGearTest.Mesh2(ma2, "MB", 10, 0f, MMod);
        var mb2 = new FuwaGearTest.Gear { name = "MB2", z = 30, c = mbg.c, ratio = mbg.ratio, phase = mbg.phase };
        var mc = FuwaGearTest.Mesh2(mb2, "MC", 10, 0f, MMod);
        // 段（歯数, モジュール, 下, 上）
        var defs = new[] {
            new[] { new Vector4(ZSmall, Mod, S + ClearY, 2f * S), new Vector4(30, MMod, 2f * S + 0.1f, 2f * S + 0.1f + MTh) },
            new[] { new Vector4(10, MMod, 2f * S + 0.1f, 2f * S + 0.1f + MTh), new Vector4(30, MMod, 2f * S + 0.25f, 2f * S + 0.25f + MTh) },
            new[] { new Vector4(10, MMod, 2f * S + 0.25f, 2f * S + 0.25f + MTh) },
        };
        var gs = new[] { ma, mbg, mc };
        var res = new MB[3];
        for (int i = 0; i < 3; i++)
        {
            var t = new GameObject(gs[i].name).transform; t.SetParent(root, false); t.localPosition = new Vector3(gs[i].c.x, 0f, gs[i].c.y);
            float lowest = 99f;
            foreach (var d in defs[i])
            {
                int z = (int)d.x; float m = d.y, y0 = d.z, y1 = d.w;
                var mesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(z, 8, m), y1 - y0, "GearRide_M_z" + z + "_m" + Mathf.RoundToInt(m * 100f) + "_t" + Mathf.RoundToInt((y1 - y0) * 100f), Vector2.zero));
                var body = new GameObject("Body_z" + z); body.transform.SetParent(t, false); body.transform.localPosition = new Vector3(0f, y0, 0f);
                body.AddComponent<MeshFilter>().sharedMesh = mesh; body.AddComponent<MeshRenderer>().sharedMaterial = z == 30 ? light : brass;
                if (i == 0 && z == ZSmall) { body.layer = 11; body.AddComponent<MeshCollider>().sharedMesh = mesh; }   // 歩ける所の横にある段だけ当たりあり
                lowest = Mathf.Min(lowest, y0);
            }
            Prim(PrimitiveType.Cylinder, root, "Axle_" + gs[i].name, new Vector3(gs[i].c.x, (CatchY + lowest) / 2f, gs[i].c.y), new Vector3(0.12f, (lowest - CatchY) / 2f, 0.12f), axleM);
            res[i] = new MB { t = t, g = gs[i] };
        }
        // シリンダー（MC の軸の上）
        var cyl = res[2].t;
        float cy0 = 2f * S + 0.4f;
        Prim(PrimitiveType.Cylinder, cyl, "Cylinder", new Vector3(0f, cy0 + CylH / 2f, 0f), new Vector3(CylR * 2f, CylH / 2f, CylR * 2f), brass);
        Prim(PrimitiveType.Cylinder, cyl, "CylAxle", new Vector3(0f, cy0 - 0.05f, 0f), new Vector3(0.08f, 0.1f, 0.08f), axleM);
        // くし（+x の向き、動かない）と台
        Vector3 cc = new Vector3(mc.c.x, 0f, mc.c.y);
        var comb = new GameObject("Comb").transform; comb.SetParent(root, false); comb.localPosition = cc;
        Prim(PrimitiveType.Cube, comb, "CombBase", new Vector3(CylR + 0.48f, cy0 + CylH / 2f, 0f), new Vector3(0.14f, CylH + 0.05f, 0.3f), dark);
        Prim(PrimitiveType.Cube, comb, "Stand", new Vector3(CylR + 0.48f, (CatchY + cy0) / 2f, 0f), new Vector3(0.14f, cy0 - CatchY, 0.14f), dark);
        res[0].t.gameObject.name = "MA_z12"; res[1].t.gameObject.name = "MB_z10"; res[2].t.gameObject.name = "MC_z10_Cylinder";
        _combRoot = comb; _cy0 = cy0; _combDark = dark;
        return res;
    }
    static Transform _combRoot; static float _cy0; static Material _combDark;

    [System.Serializable] public class NoteFile { public float length; public float bpm; public Note[] notes; }
    [System.Serializable] public class Note { public float t; public int midi; }

    // ピン：音符ごとに、時刻→シリンダーの角度、高さ→音の高さ（くしの歯）。くしの歯も音の数だけ
    static void Pins(Transform cyl, float sign, Material brass)
    {
        string path = System.IO.Path.Combine(Application.dataPath, "../Tools/audio/musicbox_test_melody.json");
        if (!System.IO.File.Exists(path)) { Debug.LogWarning("[乗る歯車] 音符の表がない " + path); return; }
        var nf = JsonUtility.FromJson<NoteFile>(System.IO.File.ReadAllText(path));
        if (Mathf.Abs(nf.length - MusicLen) > 0.01f) Debug.LogWarning($"[乗る歯車] 曲の長さ {nf.length:F3} と歯車の1周 {MusicLen:F3} がちがう → make_musicbox_test.py {MusicLen:F4} で作り直す");
        var pitches = new System.Collections.Generic.List<int>();
        foreach (var n in nf.notes) if (!pitches.Contains(n.midi)) pitches.Add(n.midi);
        pitches.Sort();
        int np = pitches.Count;
        System.Func<int, float> rowY = k => _cy0 + 0.07f + (np <= 1 ? 0f : k * (CylH - 0.14f) / (np - 1));
        var cube = GetCube(); var cis = new System.Collections.Generic.List<CombineInstance>();
        foreach (var n in nf.notes)
        {
            float al = (0f - sign * 360f * n.t / nf.length) * Mathf.Deg2Rad;   // くしは 2D の角度 0（+x）
            var dir = new Vector3(Mathf.Cos(al), 0f, Mathf.Sin(al));
            var pos = dir * (CylR + 0.02f) + Vector3.up * rowY(pitches.IndexOf(n.midi));
            cis.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(pos, Quaternion.LookRotation(dir, Vector3.up), new Vector3(0.03f, 0.03f, 0.05f)) });
        }
        var pm = new Mesh { name = "GearRide_MusicPins" }; pm.CombineMeshes(cis.ToArray(), true, true); pm.RecalculateBounds();
        var pins = new GameObject("Pins"); pins.transform.SetParent(cyl, false);
        pins.AddComponent<MeshFilter>().sharedMesh = FuwaGearTest.SaveMesh(pm); pins.AddComponent<MeshRenderer>().sharedMaterial = _combDark;
        // くしの歯（低い音ほど長い。先はピンがはじく所）
        for (int k = 0; k < np; k++)
        {
            float len = 0.36f;
            Prim(PrimitiveType.Cube, _combRoot, "Tooth" + k, new Vector3(CylR + 0.04f + len / 2f, rowY(k), 0f), new Vector3(len, 0.025f, 0.05f - 0.02f * k / Mathf.Max(1, np - 1)), brass);
        }
        Debug.Log($"[乗る歯車] オルゴール：ピン {nf.notes.Length}本、くしの歯 {np}本、1周 {MusicLen:F3}秒");
    }

    static AudioSource MusicSource(Transform parent, Vector3 lp)
    {
        var clipPath = "Assets/FuwaStage2/Audio/musicbox_test_melody.wav";
        var imp = AssetImporter.GetAtPath(clipPath) as AudioImporter;
        if (imp != null)
        {
            var st = imp.defaultSampleSettings; st.loadType = AudioClipLoadType.CompressedInMemory; st.compressionFormat = AudioCompressionFormat.Vorbis; st.quality = 0.6f;
            imp.defaultSampleSettings = st; imp.forceToMono = true; imp.SaveAndReimport();
        }
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        var g = new GameObject("MusicBoxSound"); g.transform.SetParent(parent, false); g.transform.localPosition = lp + new Vector3(0f, 2f * Step + 0.8f, 0f);
        var a = g.AddComponent<AudioSource>();
        a.clip = clip; a.loop = true; a.playOnAwake = false; a.volume = 0.6f; a.spatialBlend = 1f; a.dopplerLevel = 0f;
        a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 3f; a.maxDistance = 28f;
        return a;
    }

    static Mesh _cube;
    static Mesh GetCube()
    {
        if (_cube) return _cube;
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); _cube = g.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(g);
        return _cube;
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
