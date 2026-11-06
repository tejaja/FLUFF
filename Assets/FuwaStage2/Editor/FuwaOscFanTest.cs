using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2–4 はぐるまのとう：首ふり扇風機のお試し（2026-11-06）
// 本物の首ふり扇風機と同じ：羽根の軸のウォーム（1条ねじ）→ ウォームホイール 30枚（30分の1）→ ホイールのクランク → 棒（台に固定）
//  4節リンク：台(O→A, g)・首(O→W, b)・クランク(W→P, r)・棒(P→A, L)。クランクが首の中で1回転すると、首が左右にふれる
//  寸法は計算で探した（b=0.8 r=0.3 g=0.5 L=0.85 で ±40°）を 1.5 倍
// ・2D の (x, y) は Unity のローカル (x, z)。2D の反時計回りは Unity の Y 回転ではマイナス
// ・ウォームは軸方向の断面がラック（圧力角 20°、軸方向ピッチ = π m）のねじ。ホイールの歯と同じピッチ
public static class FuwaOscFanTest
{
    const float S = 1.5f;
    const float B = 0.8f * S, R = 0.3f * S, G = 0.5f * S, L = 0.85f * S;
    const float ColH = 1.2f;       // 台の柱の高さ（首の軸の高さ）
    const float H = 1.6f;          // 首の軸からホイールの高さ
    const int ZW = 30;             // ホイールの歯数
    const float Mod = 0.05f;       // モジュール
    const float WormR = 0.12f;     // ウォームのピッチ半径
    const float WormLen = 0.6f;   // 0.7 だとはしでホイールの歯先にかかった
    const float BladeR = 1.5f;
    static readonly Vector3 Origin = new Vector3(-67f, -162f, -97f);   // 台の足もと（床の上）

    [MenuItem("FuwaCourse/ステージ2/首ふり扇風機のお試しを作る")]
    public static void Build()
    {
        Remove();
        // ===== 4節リンクの表：ホイールの角度 θ（1°ごと）→ 首の角度 ψ =====
        var table = new float[360];
        float psi = FindStart();
        for (int i = 0; i < 360; i++)
        {
            float th = i * Mathf.Deg2Rad;
            psi = Newton(psi, th);
            table[i] = psi * Mathf.Rad2Deg;
        }
        float mn = 1e9f, mx = -1e9f; foreach (var v in table) { mn = Mathf.Min(mn, v); mx = Mathf.Max(mx, v); }
        float mid = (mn + mx) / 2f;

        var dark = FuwaGearTest.Mat("GearTest_Gear", new Color(0.36f, 0.38f, 0.43f));
        var light = FuwaGearTest.Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f));
        var axleM = FuwaGearTest.Mat("GearTest_Axle", new Color(0.2f, 0.21f, 0.24f));
        var bladeM = FuwaGearTest.Mat("FanTest_Blade", new Color(0.55f, 0.82f, 0.95f));
        var brass = FuwaGearTest.Mat("FanTest_Brass", new Color(0.86f, 0.68f, 0.32f));
        var floorM = FuwaGearTest.Mat("GearTest_Floor", new Color(0.55f, 0.58f, 0.63f));

        var root = new GameObject("OscFanTest").transform;
        root.position = Origin;
        root.rotation = Quaternion.Euler(0f, 90f, 0f);   // ふれのまん中で、2D の +x（ローカル +X）→ ワールド -Z を向く

        // 床（扇風機の前の広場）。ワールドの向きで置く
        var floor = new GameObject("Floor"); floor.transform.SetParent(root, false); floor.layer = 11;
        floor.transform.position = Origin + new Vector3(0f, -0.5f, -7f); floor.transform.rotation = Quaternion.identity;
        Prim(PrimitiveType.Cube, floor.transform, "Visual", Vector3.zero, new Vector3(22f, 1f, 16f), floorM);
        floor.AddComponent<BoxCollider>().size = new Vector3(22f, 1f, 16f);

        // 台：柱（O）と、棒の台側のピンを持つ柱（A）
        Prim(PrimitiveType.Cylinder, root, "BaseColumn", new Vector3(0f, ColH / 2f, 0f), new Vector3(0.6f, ColH / 2f, 0.6f), dark);
        Prim(PrimitiveType.Cylinder, root, "BaseFoot", new Vector3(0f, 0.08f, 0f), new Vector3(2.4f, 0.08f, 2.4f), dark);
        float linkY = ColH + H - 0.06f - 0.16f;   // クランクのピンと同じ高さ（棒が水平）
        Prim(PrimitiveType.Cylinder, root, "AnchorPost", new Vector3(0f, linkY / 2f, -G), new Vector3(0.14f, linkY / 2f, 0.14f), light);
        var anchor = new GameObject("Anchor").transform; anchor.SetParent(root, false); anchor.localPosition = new Vector3(0f, linkY, -G);
        Prim(PrimitiveType.Sphere, anchor, "Pin", Vector3.zero, Vector3.one * 0.16f, brass);

        // 首（O で回る）
        var head = new GameObject("Head").transform; head.SetParent(root, false); head.localPosition = new Vector3(0f, ColH, 0f);
        Prim(PrimitiveType.Cylinder, head, "Neck", new Vector3(0f, (H + 0.3f) / 2f, 0f), new Vector3(0.32f, (H + 0.3f) / 2f, 0.32f), light);
        // ふれのまん中で前を向く向き（首の中の 2D では -mid）
        float fa = -mid * Mathf.Deg2Rad;
        Vector3 F = new Vector3(Mathf.Cos(fa), 0f, Mathf.Sin(fa));
        Vector3 Sd = new Vector3(Mathf.Cos(fa - Mathf.PI / 2f), 0f, Mathf.Sin(fa - Mathf.PI / 2f));   // F を時計回りに 90°
        Vector3 W = new Vector3(0f, H, -B);
        float rp = Mod * ZW / 2f;
        float a = rp + WormR;                        // 中心距離
        Vector3 Wc = W + Sd * a;                     // ウォームの中心（ホイールの横、同じ高さ）
        // 首のうで：首のてっぺん → ホイールの軸 → ウォームの軸受け
        Bar(head, "ArmW", new Vector3(0f, H + 0.3f, 0f), W + Vector3.up * 0.3f, 0.12f, light);
        Bar(head, "ArmWorm", W + Vector3.up * 0.3f, Wc + Vector3.up * 0.3f, 0.1f, light);
        Prim(PrimitiveType.Cylinder, head, "AxleW", W + Vector3.up * 0.12f, new Vector3(0.12f, 0.2f, 0.12f), axleM);
        foreach (float e in new[] { -0.5f, 0.5f })
        {
            Vector3 bp = Wc + F * (e * (WormLen + 0.1f));
            Bar(head, "Bearing" + e, bp + Vector3.up * 0.3f, bp, 0.08f, light);
        }

        // ホイール（たて軸）。歯の 1 本がウォームとかみ合う所（2D で Sd の向き）に来るように回しておく（wheelPhase）
        float zetaC = Mathf.Atan2(Sd.z, Sd.x);
        float pitchA = 2f * Mathf.PI / ZW;
        float wheelPhaseDeg = Mathf.Repeat(zetaC, pitchA) * Mathf.Rad2Deg;
        var wheel = new GameObject("Wheel_z30").transform; wheel.SetParent(head, false); wheel.localPosition = W - Vector3.up * 0.06f;
        var wmesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(ZW, 8, Mod), 0.12f, "Fan_Wheel_z30", Vector2.zero));
        wheel.gameObject.AddComponent<MeshFilter>().sharedMesh = wmesh;
        wheel.gameObject.AddComponent<MeshRenderer>().sharedMaterial = brass;
        // クランクのピン（ホイールの下）。ホイールが wheelPhase 回っているぶん、ピンは -wheelPhase の位置に置く→ 実際の角度＝θ
        float pa = -wheelPhaseDeg * Mathf.Deg2Rad;
        var pin = new GameObject("CrankPin").transform; pin.SetParent(wheel, false);
        pin.localPosition = new Vector3(R * Mathf.Cos(pa), -0.16f, R * Mathf.Sin(pa));
        Prim(PrimitiveType.Cylinder, pin, "Pin", new Vector3(0f, 0.08f, 0f), new Vector3(0.09f, 0.1f, 0.09f), axleM);
        Prim(PrimitiveType.Sphere, pin, "Ball", Vector3.zero, Vector3.one * 0.15f, axleM);
        // 棒（スクリプトが毎フレーム、ピンとピンの間にのばす）
        var link = Prim(PrimitiveType.Cube, root, "Link", Vector3.zero, new Vector3(0.09f, 0.09f, L), light);

        // ウォーム（羽根と同じ軸）
        var wormMount = new GameObject("WormMount").transform; wormMount.SetParent(head, false);
        wormMount.localPosition = Wc; wormMount.localRotation = Quaternion.LookRotation(F, Vector3.up);
        var worm = new GameObject("Worm").transform; worm.SetParent(wormMount, false);
        worm.gameObject.AddComponent<MeshFilter>().sharedMesh = FuwaGearTest.SaveMesh(WormMesh());
        worm.gameObject.AddComponent<MeshRenderer>().sharedMaterial = dark;
        Prim(PrimitiveType.Cylinder, worm, "Shaft", Vector3.zero, new Vector3(0.07f, 1.1f, 0.07f), axleM).localRotation = Quaternion.Euler(90f, 0f, 0f);
        foreach (float e in new[] { -1f, 1f })   // ねじの両はしのふた（はしは山を根元まで細くしてあるので、根元の太さで閉じる）
            Prim(PrimitiveType.Cylinder, worm, "Cap" + e, new Vector3(0f, 0f, e * WormLen / 2f), new Vector3((WormR - 1.25f * Mod) * 2f + 0.01f, 0.012f, (WormR - 1.25f * Mod) * 2f + 0.01f), dark).localRotation = Quaternion.Euler(90f, 0f, 0f);
        // かみ合う所（ウォームから見たホイールの向き）で、ねじの山の谷がホイールの歯に来るように回しておく（wormPhase）
        Vector3 toW = Quaternion.Inverse(Quaternion.LookRotation(F, Vector3.up)) * (-Sd);
        float phiC = Mathf.Atan2(toW.y, toW.x) * Mathf.Rad2Deg;
        float wormPhaseDeg = phiC + 180f;

        // モーター＋羽根
        Vector3 motorC = Wc + F * (WormLen / 2f + 0.45f);
        var motor = Prim(PrimitiveType.Cylinder, head, "Motor", motorC, new Vector3(0.62f, 0.4f, 0.62f), light);
        motor.localRotation = Quaternion.LookRotation(F, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
        var bladeMount = new GameObject("BladeMount").transform; bladeMount.SetParent(head, false);
        bladeMount.localPosition = Wc + F * (WormLen / 2f + 1.05f); bladeMount.localRotation = Quaternion.LookRotation(F, Vector3.up);
        var blades = new GameObject("Blades").transform; blades.SetParent(bladeMount, false);
        Prim(PrimitiveType.Sphere, blades, "Hub", Vector3.zero, new Vector3(0.36f, 0.36f, 0.3f), brass);
        for (int k = 0; k < 3; k++)
        {
            var bl = new GameObject("Blade" + k).transform; bl.SetParent(blades, false); bl.localRotation = Quaternion.Euler(0f, 0f, k * 120f);
            var bv = Prim(PrimitiveType.Sphere, bl, "Visual", new Vector3(BladeR * 0.55f, 0f, 0f), new Vector3(BladeR * 0.95f, 0.55f, 0.05f), bladeM);
            bv.localRotation = Quaternion.Euler(28f, 0f, 0f);   // ひねり
        }
        // 羽根のまわりの輪（ガード）
        for (int k = 0; k < 24; k++)
        {
            float a0 = k * 15f * Mathf.Deg2Rad, a1 = (k + 1) * 15f * Mathf.Deg2Rad;
            Bar(bladeMount, "Guard" + k, new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0.05f) * (BladeR + 0.12f), new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0.05f) * (BladeR + 0.12f), 0.05f, light);
        }

        // 風：コース5 の突風（GustZone と粒）を写して、羽根の前に
        var src = GameObject.Find("FuwaCourse5/Gimmick_DeckGust/GustZone");
        if (src)
        {
            var gz = Object.Instantiate(src, bladeMount); gz.name = "Wind";
            var srcT = src.transform;
            gz.transform.localRotation = Quaternion.identity;
            gz.transform.localScale = srcT.lossyScale;
            var bc = gz.GetComponent<BoxCollider>();
            float len = bc.size.z * srcT.lossyScale.z;
            gz.transform.localPosition = new Vector3(0f, -bc.center.y * srcT.lossyScale.y, 0.3f + len / 2f - bc.center.z * srcT.lossyScale.z);
            var srcW = src.GetComponent<FuwaWindArea>(); var dstW = gz.GetComponent<FuwaWindArea>();
            dstW.offSeconds = 0f; dstW.strength = 8f;
            // 粒（となりの GustStreaks など）も、風との位置関係を保って写す
            foreach (Transform sib in srcT.parent)
            {
                if (!sib.name.StartsWith("GustStreaks")) continue;
                var c = Object.Instantiate(sib.gameObject, bladeMount); c.name = sib.name;
                Matrix4x4 rel = srcT.worldToLocalMatrix * sib.localToWorldMatrix;   // 風から見た粒の置き方（大きさ込み）
                Matrix4x4 m = gz.transform.localToWorldMatrix * rel;
                c.transform.position = m.MultiplyPoint3x4(Vector3.zero);
                c.transform.rotation = m.rotation;
                if (dstW.visual == sib.gameObject || srcW.visual == sib.gameObject) dstW.visual = c;
                if (dstW.extraVisuals != null) for (int i = 0; i < dstW.extraVisuals.Length; i++) if (srcW.extraVisuals[i] != null && srcW.extraVisuals[i].gameObject == sib.gameObject) dstW.extraVisuals[i] = c.GetComponent<ParticleSystem>();
            }
            UdonSharpEditorUtility.CopyProxyToUdon(dstW);
        }

        // 回す人
        var fan = UdonSharpUndo.AddComponent<FuwaOscFan>(root.gameObject);
        fan.head = head; fan.wheel = wheel; fan.worm = worm; fan.blades = blades;
        fan.crankPin = pin; fan.anchor = anchor; fan.link = link;
        fan.period = 10f; fan.wheelTeeth = ZW; fan.wormPerWheel = -ZW;
        fan.yawTable = table; fan.wheelPhase = wheelPhaseDeg; fan.wormPhase = wormPhaseDeg;
        UdonSharpEditorUtility.CopyProxyToUdon(fan);
        FuwaStage2WaterTest.NoSync(fan);

        // ふわふわを出す台（テスト用、風の当たる所の横）
        var ex = new GameObject("Extras").transform; ex.SetParent(root, false); ex.rotation = Quaternion.identity; ex.position = Origin;
        var spot = new GameObject("BallSpot").transform; spot.SetParent(ex, false); spot.position = Origin + new Vector3(4.5f, 1.6f, -6f);
        FuwaStage2WaterTest.BallStand(ex, "BallStand", new Vector3(6f, 0f, -7.5f), spot, "ふわふわを出す", new Color(1f, 0.75f, 0.85f));   // ex は回っていないので、ワールドの向きのまま

        SetTime(0f);
        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[首ふり扇風機のお試し] 作った ふれ {mn:F1}〜{mx:F1}°（±{(mx - mn) / 2f:F1}）");
    }

    // 4節リンク：|R(ψ)(W + r e(θ)) - A| = L を満たす ψ
    static float F(float psi, float th)
    {
        float px = R * Mathf.Cos(th), py = -B + R * Mathf.Sin(th);
        float X = px * Mathf.Cos(psi) - py * Mathf.Sin(psi), Y = px * Mathf.Sin(psi) + py * Mathf.Cos(psi);
        return X * X + (Y + G) * (Y + G) - L * L;
    }
    static float Newton(float psi, float th)
    {
        for (int k = 0; k < 40; k++)
        {
            float d = (F(psi + 1e-4f, th) - F(psi - 1e-4f, th)) / 2e-4f;
            if (Mathf.Abs(d) < 1e-9f) break;
            float step = F(psi, th) / d;
            psi -= Mathf.Clamp(step, -0.2f, 0.2f);
            if (Mathf.Abs(step) < 1e-7f) break;
        }
        return psi;
    }
    static float FindStart()
    {
        // θ=0 で、ふれのまん中あたり（+80°前後）の解
        float best = 0f, bv = 1e9f;
        for (int i = 0; i <= 360; i++) { float p = (i - 180) * Mathf.Deg2Rad; float v = Mathf.Abs(F(p, 0f)); if (v < bv && p > 0f) { bv = v; best = p; } }
        return Newton(best, 0f);
    }

    // ウォーム：軸（ローカル Z）まわりの 1 条ねじ。軸方向の断面はラックの歯（山の真ん中が u=0）
    static Mesh WormMesh()
    {
        float p = Mathf.PI * Mod, ta = Mathf.Tan(20f * Mathf.Deg2Rad);
        float rTip = WormR + Mod, rRoot = WormR - 1.25f * Mod;
        int na = 64; int nz = Mathf.CeilToInt(WormLen / p * 28f);
        var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
        for (int iz = 0; iz <= nz; iz++)
        {
            float z = -WormLen / 2f + WormLen * iz / nz;
            for (int ia = 0; ia <= na; ia++)
            {
                float phi = 2f * Mathf.PI * ia / na;
                float u = Mathf.Repeat(z - p * phi / (2f * Mathf.PI), p);
                float d = Mathf.Min(u, p - u);                                   // 山の真ん中からの距離
                float half = p / 4f - FuwaGearTest.Backlash * 0.1f;               // ピッチ線での山の半分の幅
                float rr = Mathf.Clamp(WormR + (half - d) / ta, rRoot, rTip);
                float k = Mathf.Clamp01((WormLen / 2f - Mathf.Abs(z)) / 0.07f);   // はしは山を根元まで細く（面取り）
                rr = Mathf.Lerp(rRoot, rr, Mathf.SmoothStep(0f, 1f, k));
                v.Add(new Vector3(rr * Mathf.Cos(phi), rr * Mathf.Sin(phi), z));
            }
        }
        int row = na + 1;
        for (int iz = 0; iz < nz; iz++)
            for (int ia = 0; ia < na; ia++)
            {
                int a0 = iz * row + ia, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                t.Add(a0); t.Add(a1); t.Add(b0); t.Add(a1); t.Add(b1); t.Add(b0);   // 外向き（逆だと内側が見えた）
            }
        var m = new Mesh { name = "Fan_Worm" };
        m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // エディタで動画を撮る用（スクリプトと同じ計算）
    public static void SetTime(float sec)
    {
        var root = GameObject.Find("OscFanTest"); if (!root) return;
        var f = root.GetComponent<FuwaOscFan>(); if (!f) return;
        float th = 360f * Mathf.Repeat(sec, f.period) / f.period;
        int n = f.yawTable.Length; float x = th / 360f * n; int i0 = Mathf.FloorToInt(x) % n, i1 = (i0 + 1) % n;
        float yaw = Mathf.LerpAngle(f.yawTable[i0], f.yawTable[i1], x - Mathf.Floor(x));
        f.head.localRotation = Quaternion.Euler(0f, -yaw, 0f);
        f.wheel.localRotation = Quaternion.Euler(0f, -(th + f.wheelPhase), 0f);
        float wa = Mathf.Repeat(th * f.wormPerWheel + f.wormPhase, 360f);
        f.worm.localRotation = Quaternion.Euler(0f, 0f, wa); f.blades.localRotation = Quaternion.Euler(0f, 0f, wa);
        Vector3 a = f.crankPin.position, b = f.anchor.position, d = b - a;
        f.link.position = (a + b) * 0.5f; f.link.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        var s = f.link.localScale; s.z = d.magnitude; f.link.localScale = s;
    }

    [MenuItem("FuwaCourse/ステージ2/首ふり扇風機のお試しを消す")]
    public static void Remove() { var o = GameObject.Find("OscFanTest"); if (o) Object.DestroyImmediate(o); }

    static Transform Bar(Transform parent, string name, Vector3 a, Vector3 b, float thick, Material m)
    {
        var g = Prim(PrimitiveType.Cube, parent, name, (a + b) / 2f, new Vector3(thick, thick, (b - a).magnitude + thick), m);
        if ((b - a).sqrMagnitude > 1e-6f) g.localRotation = Quaternion.LookRotation((b - a).normalized, Mathf.Abs(Vector3.Dot((b - a).normalized, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up);
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
