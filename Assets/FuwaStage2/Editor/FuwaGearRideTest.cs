using System.Linq;
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
    public static float Step = 0.7f;     // 1段の高さ。2026-11-07 てじゃ「歩いて登れてしまう」0.4→0.7（歩きの段差こえ＋ジャンプで足が約0.46m上がる、の間）
    const float ClearY = 0.04f;          // 上の段の大きい歯車と、下の段の歯車の上の面とのすき間
    const int ZBig = 20, ZSmall = 12;
    public static float PitchSpeed = 0.45f;   // 2026-11-08 「もっとはやくていいな」0.3 → 0.45   // ピッチ円の速さ（m/秒）。てじゃ「かなりゆっくり」0.15 → 2026-11-07「ちょい遅すぎ」0.2 → 2026-11-08 こしきで回るしかなくなったので「もっとはやくても」0.3
    static readonly Vector3 Origin = new Vector3(50f, -162f, -104f);   // 入口の台の東のはし、床の上の高さ
    const float CatchY = -3f;
    public static float HubH = 1.4f, HubRBig = 2f, HubRSmall = 0.9f, HubSlope = 52f, HubBounce = 1.3f;   // まん中のこしき（2026-11-08）。上は円すい、ふわふわを軽くはじく（2.2 は「弾く力つよすぎ」→ 1.3）

    // こしき：y=0〜hc の筒（半径 r、n 角）＋その上の円すい（てっぺん y=hc+hk）。底はなし（歯車の上の面にのる）
    //  筒の横はなめらか、筒と円すいの境・てっぺんは点を分けて角を立てる
    public static int HubSectors = 8;
    // 筒のまわりのらせんの帯（床屋のポール、2026-11-10）：nb 本、y0〜y1 で turns 回まわる。幅 w、外へ th だけ出っぱる
    public static float HelixTurns = 0.8f, HelixW = 0.16f, HelixTh = 0.02f; public static int HelixBands = 2;
    static Mesh HelixMesh(float r, float y0, float y1, string name)
    {
        float turns = HelixTurns * 0.9f / Mathf.Max(0.3f, r);   // 太い筒ほど少なく巻く（帯のかたむきをそろえる。半径 0.9 で HelixTurns 回）
        var v = new System.Collections.Generic.List<Vector3>(); var tr = new System.Collections.Generic.List<int>();
        int steps = Mathf.Max(16, Mathf.RoundToInt(64 * turns * Mathf.Max(1f, r)));
        void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d); tr.Add(i); tr.Add(i + 1); tr.Add(i + 2); tr.Add(i); tr.Add(i + 2); tr.Add(i + 3); }
        Vector3 P(float a, float y, float rr) => new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
        float hw = HelixW / 2f, ro = r + HelixTh;
        for (int b = 0; b < HelixBands; b++)
            for (int k = 0; k < steps; k++)
            {
                float t0 = k / (float)steps, t1 = (k + 1) / (float)steps;
                float a0 = 2f * Mathf.PI * (b / (float)HelixBands + turns * t0), a1 = 2f * Mathf.PI * (b / (float)HelixBands + turns * t1);
                float yc0 = Mathf.Lerp(y0 + hw, y1 - hw, t0), yc1 = Mathf.Lerp(y0 + hw, y1 - hw, t1);
                // 外の面（外向き）
                Q(P(a0, yc0 - hw, ro), P(a0, yc0 + hw, ro), P(a1, yc1 + hw, ro), P(a1, yc1 - hw, ro));
                // 上と下のふち
                Q(P(a0, yc0 + hw, ro), P(a0, yc0 + hw, r), P(a1, yc1 + hw, r), P(a1, yc1 + hw, ro));
                Q(P(a0, yc0 - hw, r), P(a0, yc0 - hw, ro), P(a1, yc1 - hw, ro), P(a1, yc1 - hw, r));
                if (k == 0) Q(P(a0, yc0 - hw, r), P(a0, yc0 + hw, r), P(a0, yc0 + hw, ro), P(a0, yc0 - hw, ro));
                if (k == steps - 1) Q(P(a1, yc1 - hw, ro), P(a1, yc1 + hw, ro), P(a1, yc1 + hw, r), P(a1, yc1 - hw, r));
            }
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(tr, 0);
        // 向きをそろえる：外の面が外を向くように（巻きが逆なら全部ひっくり返す）
        var tris = m.triangles; var vv = m.vertices;
        var n0 = Vector3.Cross(vv[tris[1]] - vv[tris[0]], vv[tris[2]] - vv[tris[0]]); var c = (vv[tris[0]] + vv[tris[1]] + vv[tris[2]]) / 3f;
        if (Vector3.Dot(n0, new Vector3(c.x, 0f, c.z)) < 0f) { for (int i = 0; i < tris.Length; i += 3) { int t = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = t; } m.triangles = tris; }
        m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    // 筒のまわりのたてのスジ（HubStripeN 本、幅 HelixW、外へ HelixTh）
    public static int HubStripeN = 6;
    static Mesh StripesMesh(float r, float y0, float y1, string name)
    {
        var v = new System.Collections.Generic.List<Vector3>(); var tr = new System.Collections.Generic.List<int>();
        void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want) { int i = v.Count; if (Vector3.Dot(Vector3.Cross(b - a, c - a), want) < 0f) { var tmp = b; b = d; d = tmp; } v.Add(a); v.Add(b); v.Add(c); v.Add(d); tr.Add(i); tr.Add(i + 1); tr.Add(i + 2); tr.Add(i); tr.Add(i + 2); tr.Add(i + 3); }
        Vector3 P(float a, float y, float rr) => new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
        float ro = r + HelixTh, hw = HelixW / 2f / r; const int seg = 3;
        for (int k = 0; k < HubStripeN; k++)
        {
            float ac = 2f * Mathf.PI * k / HubStripeN;
            for (int j = 0; j < seg; j++)
            {
                float a0 = ac - hw + 2f * hw * j / seg, a1 = ac - hw + 2f * hw * (j + 1) / seg; var o = P((a0 + a1) / 2f, 0f, 1f);
                Q(P(a0, y0, ro), P(a0, y1, ro), P(a1, y1, ro), P(a1, y0, ro), o);   // 外
                Q(P(a0, y1, r), P(a0, y1, ro), P(a1, y1, ro), P(a1, y1, r), Vector3.up);   // 上
                Q(P(a0, y0, ro), P(a0, y0, r), P(a1, y0, r), P(a1, y0, ro), Vector3.down);   // 下
            }
            float e0 = ac - hw, e1 = ac + hw;
            Q(P(e0, y0, r), P(e0, y1, r), P(e0, y1, ro), P(e0, y0, ro), P(e0 - Mathf.PI / 2f, 0f, 1f));   // 横
            Q(P(e1, y0, ro), P(e1, y1, ro), P(e1, y1, r), P(e1, y0, r), P(e1 + Mathf.PI / 2f, 0f, 1f));
        }
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(tr, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }
    // サブメッシュつきで保存（FuwaGearTest.SaveMesh は1つだけ）
    public static Mesh SaveMeshSub(Mesh m)
    {
        string path = "Assets/FuwaStage2/Meshes/GearTest/" + m.name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old == null) { AssetDatabase.CreateAsset(m, path); return m; }
        old.Clear(false); old.SetVertices(m.vertices); old.SetNormals(m.normals); old.subMeshCount = m.subMeshCount;
        for (int i = 0; i < m.subMeshCount; i++) old.SetTriangles(m.GetTriangles(i), i);
        old.RecalculateBounds(); EditorUtility.SetDirty(old); return old;
    }
    // こしきの見た目（屋根の塗り分け・らせんの帯）を付け直す。シーンの物にも使う
    public static void HubLooks(Transform t, Transform hub, float hr, Material dark, Material light, Material brass)
    {
        float coneH = hr * Mathf.Tan(HubSlope * Mathf.Deg2Rad);
        var hm = SaveMeshSub(HubMesh(hr, HubH, coneH, 40, "GearRide_Hub_r" + Mathf.RoundToInt(hr * 100)));
        hub.GetComponent<MeshFilter>().sharedMesh = hm; hub.GetComponent<MeshRenderer>().sharedMaterials = new[] { dark, light };
        var mc = hub.GetComponent<MeshCollider>(); if (mc) { mc.sharedMesh = null; mc.sharedMesh = hm; }
        foreach (var x in t.Cast<Transform>().Where(x => x.name == "HubMark" || x.name == "HubHelix" || x.name == "HubStripes").ToArray()) Object.DestroyImmediate(x.gameObject);
        // 2026-11-10 らせんは「びみょう」→ たての金のスジを HubStripeN 本（前は1本の HubMark）
        foreach (var x in t.Cast<Transform>().Where(x => x.name == "HubStripes").ToArray()) Object.DestroyImmediate(x.gameObject);
        var hx = new GameObject("HubStripes"); hx.transform.SetParent(t, false); hx.transform.localPosition = hub.localPosition;
        hx.AddComponent<MeshFilter>().sharedMesh = FuwaGearTest.SaveMesh(StripesMesh(hr, 0.2f, HubH - 0.3f, "GearRide_HubStripes_r" + Mathf.RoundToInt(hr * 100)));
        hx.AddComponent<MeshRenderer>().sharedMaterial = brass;
    }

    static Mesh HubMesh(float r, float hc, float hk, int n, string name)
    {
        var v = new System.Collections.Generic.List<Vector3>(); var nr = new System.Collections.Generic.List<Vector3>(); var tr = new System.Collections.Generic.List<int>();
        Vector3 Ring(int i, float y) { float a = 2f * Mathf.PI * i / n; return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r); }
        Vector3 ConeN(float a) => new Vector3(Mathf.Cos(a) * hk, r, Mathf.Sin(a) * hk).normalized;
        // 筒（i=0..n、点 2 つずつ）
        for (int i = 0; i <= n; i++)
        {
            float a = 2f * Mathf.PI * i / n; var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            v.Add(Ring(i, 0f)); nr.Add(o); v.Add(Ring(i, hc)); nr.Add(o);
        }
        for (int i = 0; i < n; i++) { int a0 = 2 * i, b0 = 2 * i + 2; tr.Add(a0); tr.Add(a0 + 1); tr.Add(b0); tr.Add(b0); tr.Add(a0 + 1); tr.Add(b0 + 1); }
        // 円すい（ふちの点と、面ごとのてっぺん）
        int c0 = v.Count;
        for (int i = 0; i <= n; i++) { v.Add(Ring(i, hc)); nr.Add(ConeN(2f * Mathf.PI * i / n)); }
        int t0 = v.Count;
        for (int i = 0; i < n; i++) { v.Add(new Vector3(0f, hc + hk, 0f)); nr.Add(ConeN(2f * Mathf.PI * (i + 0.5f) / n)); }
        // 2026-11-10 てじゃ「回転してるのをわかりやすく」→ 屋根をサーカステントみたいに HubSectors 個に塗り分け（1つおきにサブメッシュ 1）
        var tr2 = new System.Collections.Generic.List<int>();
        for (int i = 0; i < n; i++) { var dst = ((i * HubSectors / n) % 2 == 0) ? tr : tr2; dst.Add(c0 + i); dst.Add(t0 + i); dst.Add(c0 + i + 1); }
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetNormals(nr); m.subMeshCount = 2; m.SetTriangles(tr, 0); m.SetTriangles(tr2, 1); m.RecalculateBounds();
        return m;
    }

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
            // 2026-11-08 てじゃ「下の歯車はアウトに」：Rigidbody の子の当たりは、ふわふわから見ると歯車（この物）に当たった扱いになる → 歯車も地面のレイヤーに
            t.gameObject.layer = 11;
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
            // まん中の太いこしき（2026-11-08 てじゃ「中央を突破するとただの平地」）：いちばん上の段のまん中に高さ HubH の筒。
            //  まん中を突っ切れないので、ふちの輪っかに乗って運ばれる。当たりあり（歯車と一緒に回る）。回ってるのが分かるように真ちゅうの帯とたての線
            float hr = topZ == ZBig ? HubRBig : HubRSmall;
            // 筒と上のとんがり（2026-11-08 てじゃ「上を円錐状にしないと乗っちゃう」→ HubSlope° の円すい）は1つのメッシュ（別々だと角の数がずれてすき間が見えた）
            float coneH = hr * Mathf.Tan(HubSlope * Mathf.Deg2Rad);
            var hub = new GameObject("Hub"); hub.transform.SetParent(t, false); hub.transform.localPosition = new Vector3(0f, topY, 0f); hub.layer = 11;
            var hm = SaveMeshSub(HubMesh(hr, HubH, coneH, 40, "GearRide_Hub_r" + Mathf.RoundToInt(hr * 100)));
            hub.AddComponent<MeshFilter>().sharedMesh = hm; hub.AddComponent<MeshRenderer>().sharedMaterials = new[] { dark, light }; hub.AddComponent<MeshCollider>().sharedMesh = hm;
            // ふわふわが当たると軽くはじく（2026-11-08 てじゃ「まんなかのやつ、ちょっとフワフワ弾くように」）
            //  こしきにも自分の Rigidbody（kinematic）：歯車の Rigidbody の子の当たりだと、ふわふわの OnCollisionEnter の collision.gameObject が歯車の方になって FuwaBounce が見つからなかった（とんがりに乗って転がった）
            var hrb = hub.AddComponent<Rigidbody>(); hrb.isKinematic = true; hrb.useGravity = false;
            var bo = UdonSharpUndo.AddComponent<FuwaBounce>(hub);
            bo.alwaysUp = false; bo.useContactNormal = true; bo.bounceSpeed = HubBounce; bo.keepTangent = 0.5f;
            UdonSharpEditorUtility.CopyProxyToUdon(bo); FuwaStage2WaterTest.NoSync(bo);
            Prim(PrimitiveType.Cylinder, t, "HubBand", new Vector3(0f, topY + HubH - 0.2f, 0f), new Vector3(2f * hr + 0.08f, 0.12f, 2f * hr + 0.08f), brass);
            Prim(PrimitiveType.Cylinder, t, "HubFoot", new Vector3(0f, topY + 0.1f, 0f), new Vector3(2f * hr + 0.12f, 0.1f, 2f * hr + 0.12f), brass);
            HubLooks(t, hub.transform, hr, dark, light, brass);   // 2026-11-10 てじゃ「今一本だけ黄色のスジ」→ らせんの帯2本＋屋根の塗り分け（前は HubMark 1本）
            Prim(PrimitiveType.Sphere, t, "HubTop", new Vector3(0f, topY + HubH + coneH, 0f), Vector3.one * 0.22f, brass);
            // 軸（受け床から歯車の下の面まで）
            Prim(PrimitiveType.Cylinder, root, "Axle_" + g.name, new Vector3(g.c.x, (CatchY + lowest) / 2f, g.c.y), new Vector3(0.55f, (lowest - CatchY) / 2f, 0.55f), axleM);
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

        // 石臼：G4 から歯車3段で速くして、上の石を回す（オルゴール版 MusicBox は残してあるが使わない）
        var mb = Mill(root, g4, dark, light, brass, axleM, floorM);
        int musicIdx = ts.Length + mb.Length - 1;
        var ts2 = new Transform[ts.Length + mb.Length]; var ratio2 = new float[ts2.Length]; var phase2 = new float[ts2.Length];
        for (int i = 0; i < ts.Length; i++) { ts2[i] = ts[i]; ratio2[i] = ratio[i]; phase2[i] = phase[i]; }
        for (int i = 0; i < mb.Length; i++) { ts2[ts.Length + i] = mb[i].t; ratio2[ts.Length + i] = mb[i].g.ratio; phase2[ts.Length + i] = mb[i].g.phase * Mathf.Rad2Deg; }
        ts = ts2; ratio = ratio2; phase = phase2;

        var ride = UdonSharpUndo.AddComponent<FuwaGearRide>(root.gameObject);
        ride.gears = ts; ride.ratio = ratio; ride.phase = phase;
        ride.speed = PitchSpeed / (Mod * ZBig / 2f) * Mathf.Rad2Deg;   // G1 のピッチ円の速さ → 角速度
        ride.music = null; ride.musicGear = -1;
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

    // ---- 石臼（2026-11-06 オルゴールは「インパクトない、見てる余裕なさそ」→ てじゃ「もっと地味なのでいい、粉引いてるみたいな」）----
    //  G4（大20枚）→ MA 小12枚（G4 と同じ高さの層）＋同じ軸の上に MA2 大30枚 → MB 小10枚＋上に MB2 大30枚 → MC 小10枚（石臼の軸）
    //  G4 の 20/12×30/10×30/10＝15倍の速さで上の石が回る。下の石は台に固定。じょうごから粒、石のすき間から粉
    static MB[] Mill(Transform root, FuwaGearTest.Gear g4, Material dark, Material light, Material brass, Material axleM, Material floorM)
    {
        float S = Step;
        var ma = FuwaGearTest.Mesh2(g4, "MA", ZSmall, 90f, Mod);
        var ma2 = new FuwaGearTest.Gear { name = "MA2", z = 30, c = ma.c, ratio = ma.ratio, phase = ma.phase };
        var mbg = FuwaGearTest.Mesh2(ma2, "MB", 10, 0f, MMod);
        var mb2 = new FuwaGearTest.Gear { name = "MB2", z = 30, c = mbg.c, ratio = mbg.ratio, phase = mbg.phase };
        var mc = FuwaGearTest.Mesh2(mb2, "MC", 10, 0f, MMod);
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
                if (i == 0 && z == ZSmall) { body.layer = 11; body.AddComponent<MeshCollider>().sharedMesh = mesh; }
                lowest = Mathf.Min(lowest, y0);
            }
            Prim(PrimitiveType.Cylinder, root, "Axle_" + gs[i].name, new Vector3(gs[i].c.x, (CatchY + lowest) / 2f, gs[i].c.y), new Vector3(0.12f, (lowest - CatchY) / 2f, 0.12f), axleM);
            res[i] = new MB { t = t, g = gs[i] };
        }
        res[0].t.gameObject.name = "MA_z12"; res[1].t.gameObject.name = "MB_z10"; res[2].t.gameObject.name = "MC_z10_Millstone";

        var stoneM = FuwaGearTest.Mat("GearRideTest_Millstone", new Color(0.66f, 0.64f, 0.6f));
        var woodM = FuwaGearTest.Mat("GearTest_Bridge", new Color(0.66f, 0.52f, 0.38f));
        var flourM = FuwaGearTest.Mat("GearRideTest_Flour", new Color(0.98f, 0.97f, 0.93f));
        var grainM = FuwaGearTest.Mat("GearRideTest_Grain", new Color(0.86f, 0.72f, 0.42f));
        var sackM = FuwaGearTest.Mat("GearRideTest_Sack", new Color(0.82f, 0.74f, 0.6f));
        Vector3 c = new Vector3(mc.c.x, 0f, mc.c.y);
        var mill = new GameObject("Mill").transform; mill.SetParent(root, false); mill.localPosition = c;
        // 台（下の歯車 MB2 に当たらない所に足）。板の上に下の石
        float deck0 = 2f * S + 0.45f, deck1 = deck0 + 0.1f;
        Prim(PrimitiveType.Cube, mill, "Deck", new Vector3(0.3f, (deck0 + deck1) / 2f, 0f), new Vector3(2.4f, deck1 - deck0, 2.5f), woodM);
        foreach (var lp in new[] { new Vector2(1.35f, 1.1f), new Vector2(1.35f, -1.1f), new Vector2(-0.3f, 1.15f), new Vector2(-0.3f, -1.15f) })
            Prim(PrimitiveType.Cube, mill, "Leg", new Vector3(lp.x, (CatchY + deck0) / 2f, lp.y), new Vector3(0.14f, deck0 - CatchY, 0.14f), woodM);
        float bed0 = deck1, bed1 = bed0 + 0.25f, run0 = bed1 + 0.02f, run1 = run0 + 0.28f;
        Prim(PrimitiveType.Cylinder, mill, "BedStone", new Vector3(0f, (bed0 + bed1) / 2f, 0f), new Vector3(1.8f, (bed1 - bed0) / 2f, 1.8f), stoneM);
        // 上の石（MC の軸と一緒に回る）。回ってるのが分かるように、上に溝の線と取っ手
        var runner = res[2].t;
        Prim(PrimitiveType.Cylinder, runner, "Shaft", new Vector3(0f, (2f * S + 0.35f + run1) / 2f, 0f), new Vector3(0.1f, (run1 - 2f * S - 0.35f) / 2f, 0.1f), axleM);
        Prim(PrimitiveType.Cylinder, runner, "RunnerStone", new Vector3(0f, (run0 + run1) / 2f, 0f), new Vector3(1.76f, (run1 - run0) / 2f, 1.76f), stoneM);
        Prim(PrimitiveType.Cylinder, runner, "Eye", new Vector3(0f, run1 + 0.003f, 0f), new Vector3(0.3f, 0.004f, 0.3f), dark);
        for (int k = 0; k < 4; k++)
        {
            var gr = Prim(PrimitiveType.Cube, runner, "Groove" + k, Quaternion.Euler(0f, k * 45f, 0f) * new Vector3(0f, run1 + 0.002f, 0f), new Vector3(1.5f, 0.004f, 0.04f), dark);
            gr.localRotation = Quaternion.Euler(0f, k * 45f, 0f);
        }
        Prim(PrimitiveType.Cube, runner, "Rynd", new Vector3(0f, run1 + 0.05f, 0f), new Vector3(0.5f, 0.06f, 0.1f), axleM);
        // じょうご（四角い木の箱、下がせまい）と支え
        float hop0 = run1 + 0.45f, hop1 = hop0 + 0.5f;
        var hm = Frustum(0.12f, 0.45f, hop1 - hop0, "GearRide_Hopper");
        var hop = new GameObject("Hopper"); hop.transform.SetParent(mill, false); hop.transform.localPosition = new Vector3(0f, hop0, 0f);
        hop.AddComponent<MeshFilter>().sharedMesh = FuwaGearTest.SaveMesh(hm); hop.AddComponent<MeshRenderer>().sharedMaterial = woodM;
        Prim(PrimitiveType.Cube, mill, "HopperGrain", new Vector3(0f, hop1 - 0.04f, 0f), new Vector3(0.82f, 0.02f, 0.82f), grainM);
        foreach (float sz in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Cube, mill, "HopperPost", new Vector3(0.95f, (bed0 + hop1) / 2f, sz * 0.5f), new Vector3(0.1f, hop1 - bed0, 0.1f), woodM);
            Bar(mill, "HopperArm" + sz, new Vector3(0.95f, hop1 - 0.1f, sz * 0.5f), new Vector3(0.45f, hop1 - 0.1f, sz * 0.45f), 0.08f, woodM);
        }
        // 粒（じょうごの下から石の目へ）・粉（石のすき間から外へ、ふわっと）
        Particles(mill, "Grain", new Vector3(0f, hop0 - 0.02f, 0f), Quaternion.Euler(90f, 0f, 0f), grainM, rate: 10f, life: 0.35f, speed: 0.3f, size: 0.035f, grav: 1f, cone: 4f, radius: 0.02f, shape: ParticleSystemShapeType.Cone);
        Particles(mill, "Flour", new Vector3(0f, bed1 + 0.01f, 0f), Quaternion.Euler(-90f, 0f, 0f), flourM, rate: 14f, life: 1.4f, speed: 0.12f, size: 0.05f, grav: 0.05f, cone: 0f, radius: 0.9f, shape: ParticleSystemShapeType.Circle);
        // 粉の箱（石の手前、台の上）と粉袋
        Prim(PrimitiveType.Cube, mill, "FlourTray", new Vector3(0f, deck1 + 0.06f, -1.07f), new Vector3(1.2f, 0.12f, 0.32f), woodM);
        var pile = Prim(PrimitiveType.Sphere, mill, "FlourPile", new Vector3(0f, deck1 + 0.12f, -1.07f), new Vector3(0.9f, 0.12f, 0.24f), flourM);
        foreach (var sp in new[] { new Vector3(1.0f, 0f, 0.85f), new Vector3(1.0f, 0f, -0.75f) })
        {
            var sk = Prim(PrimitiveType.Sphere, mill, "Sack", new Vector3(sp.x, deck1 + 0.28f, sp.z), new Vector3(0.42f, 0.56f, 0.38f), sackM);
            Prim(PrimitiveType.Cylinder, mill, "SackTie", new Vector3(sp.x, deck1 + 0.6f, sp.z), new Vector3(0.12f, 0.06f, 0.12f), sackM);
        }
        return res;
    }

    static void Particles(Transform parent, string name, Vector3 lp, Quaternion lr, Material m, float rate, float life, float speed, float size, float grav, float cone, float radius, ParticleSystemShapeType shape)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localRotation = lr;
        var ps = g.AddComponent<ParticleSystem>();
        var main = ps.main; main.startLifetime = life; main.startSpeed = speed; main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.3f);
        main.gravityModifier = grav; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 80; main.prewarm = true; main.loop = true;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = shape; sh.angle = cone; sh.radius = radius; if (shape == ParticleSystemShapeType.Circle) { sh.radiusThickness = 0f; }
        var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
        var pr = g.GetComponent<ParticleSystemRenderer>(); pr.renderMode = ParticleSystemRenderMode.Mesh;
        pr.mesh = GetCube(); pr.sharedMaterial = m;
    }

    // 四角いじょうご（下の口 a、上の口 b、高さ h）。外と内の両面
    static Mesh Frustum(float a, float b, float h, string name)
    {
        var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
        Vector3[] lo = { new Vector3(-a, 0, -a), new Vector3(a, 0, -a), new Vector3(a, 0, a), new Vector3(-a, 0, a) };
        Vector3[] hi = { new Vector3(-b, h, -b), new Vector3(b, h, -b), new Vector3(b, h, b), new Vector3(-b, h, b) };
        for (int i = 0; i < 4; i++)
        {
            int j = (i + 1) % 4; int s = v.Count;
            v.Add(lo[i]); v.Add(lo[j]); v.Add(hi[j]); v.Add(hi[i]);
            t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2);   // 外
            s = v.Count; v.Add(lo[i]); v.Add(lo[j]); v.Add(hi[j]); v.Add(hi[i]);
            t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3);   // 内
        }
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
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
