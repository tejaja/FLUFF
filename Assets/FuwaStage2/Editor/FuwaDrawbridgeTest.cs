using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2–4 はぐるまのとう：風車で動く跳ね橋のお試し（2026-11-06）
// 風車（A 16枚）→ B 40枚 =同じ軸= C 16枚 → D 60枚の扇形（橋のつけ根）。本物の跳ね橋と同じで、橋の根元の扇形の歯車を小さい歯車で回す
// ・しかけは縦の面（ローカル Y が歯車の軸 = ワールドの -Z）。2D の (u, v) = (ワールド X, ワールド上)
// ・層：奥の段 D・C、手前の段 A・B（B と D は大きくて重なるので段を分ける）。風車の羽根は A の手前
// ・橋はふだん上がっていて（D = 90°）、風車を回すと下りる（D = 0°）。D を元にして C → A の順に角度をそろえる
public static class FuwaDrawbridgeTest
{
    const float Mod = 0.3f;          // モジュール
    const float Th = 0.5f;           // 歯車の厚み
    const float Front = Th + 0.1f;   // 手前の段の位置（ローカル Y）
    const int ZA = 16, ZB = 40, ZC = 16, ZD = 60;
    public const float BridgeLen = 8f, PathW = 4f, PathNear = 1.9f;   // 2026-11-10 てじゃ「幅もあわせて」：道（2–4 の 2×HW）と同じ 4m（前は 4.5m）
    // 橋の手前・向こうの道の長さ。2026-11-07 てじゃ「全体的にもうちょっと小回りに」16・14 → 8・4
    public static float NearLen = 8f, FarLen = 4f;   // 道は手前（ローカル Y = 1.9〜6.4）
    static readonly Vector3 Origin = new Vector3(-40f, -162f, -100f);
    const bool WithBird = false;

    [MenuItem("FuwaCourse/ステージ2/風車の跳ね橋のお試しを作る")]
    public static void Build()
    {
        Remove();
        // 歯車の並び（D を元に）
        var d = new FuwaGearTest.Gear { name = "D_z60", z = ZD, c = Vector2.zero, ratio = 1f, phase = Mathf.PI / 2f };
        var c = FuwaGearTest.Mesh2(d, "C_z16", ZC, 200f, Mod);
        var b = new FuwaGearTest.Gear { name = "B_z40", z = ZB, c = c.c, ratio = c.ratio, phase = 0f };
        var a = FuwaGearTest.Mesh2(b, "A_z16", ZA, 40f, Mod);
        var gears = new[] { a, b, c, d };
        float rA = a.ratio;   // u あたりの A の回り方
        float maxTurn = 90f * Mathf.Abs(rA);   // 橋 90° ぶん

        var root = new GameObject("DrawbridgeTest").transform;
        root.position = Origin;
        root.rotation = Quaternion.Euler(-90f, 0f, 0f);   // ローカル Z → 上、ローカル Y → ワールドの -Z（手前）

        var dark = FuwaGearTest.Mat("GearTest_Gear", new Color(0.36f, 0.38f, 0.43f));
        var light = FuwaGearTest.Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f));
        var axleM = FuwaGearTest.Mat("GearTest_Axle", new Color(0.2f, 0.21f, 0.24f));
        var wood = FuwaGearTest.Mat("GearTest_Bridge", new Color(0.66f, 0.52f, 0.38f));
        var floorM = FuwaGearTest.Mat("GearTest_Floor", new Color(0.55f, 0.58f, 0.63f));
        var blade = FuwaGearTest.Mat("GearTest_Blade", new Color(0.95f, 0.93f, 0.88f));
        var mark = FuwaGearTest.Mat("GearTest_Mark", new Color(0.95f, 0.72f, 0.25f));
        var brass = FuwaGearTest.Mat("FanTest_Brass", new Color(0.86f, 0.68f, 0.32f));
        var roadA = FuwaTwinTableTest.BridgeMat(true); var roadB = FuwaTwinTableTest.BridgeMat(false);   // 動く橋の色（2026-11-10）
        int pathLayer = 11;

        var ts = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            var g = gears[i];
            var go = new GameObject(g.name);
            go.transform.SetParent(root, false);
            bool front = g == a || g == b;
            go.transform.localPosition = new Vector3(g.c.x, front ? Front : 0f, g.c.y);
            Mesh mesh;
            if (g == d)
            {
                // 扇形：橋が 0〜90° 動く間にかみ合う所（C の向き 200° から、ローカルで 110〜200°）＋ゆとり
                // 2026-11-10 てじゃ「跳ね橋あたりもクオリティ上げたい」→ のっぺりした板をやめて、歯のふち（帯）＋スポーク＋こしき
                mesh = FuwaGearTest.SaveMesh(SectorRim(FuwaGearTest.GearOutline(ZD, 10, Mod, SecA0 * Mathf.Deg2Rad, SecA1 * Mathf.Deg2Rad), Th, RimW, "Bridge_D_z60_Rim"));
            }
            else mesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(g.z, 10, Mod), Th, "Bridge_" + g.name, Vector2.zero));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = front ? light : dark;
            ts[i] = go.transform;
            // 回ってるのがわかる印
            if (g != d) Prim(PrimitiveType.Cylinder, go.transform, "Mark", new Vector3(Mod * g.z / 2f - 1.8f * Mod, Th + 0.01f, 0f), new Vector3(0.3f, 0.01f, 0.3f), mark);
            else SectorInside(go.transform, dark, brass);
        }
        // 軸
        // 軸（2026-11-10 支えの枠までのばす：BC はうしろの梁〜前のうで、A は歯車〜羽根車）
        Prim(PrimitiveType.Cylinder, root, "Axle_BC", new Vector3(b.c.x, (BackY0 + ArmY1 + 0.04f) / 2f, b.c.y), new Vector3(0.7f, (ArmY1 + 0.04f - BackY0) / 2f, 0.7f), axleM);
        Prim(PrimitiveType.Cylinder, root, "Axle_A", new Vector3(a.c.x, (Front - 0.05f + WmY) / 2f, a.c.y), new Vector3(0.5f, (WmY - Front + 0.05f) / 2f, 0.5f), axleM);
        // 橋のつけ根の軸：うしろの梁 → 橋の手前のはしの桁、橋の向こうのはしの桁 → 前の軸受け（2026-11-10 道の幅いっぱいの軸は道の上に半分出て段に見えた → 道の幅の所はなし、橋は両はしの桁で軸にのる）
        float gIn = PathNear - 0.14f, gOut = PathNear + PathW + 0.14f;
        Prim(PrimitiveType.Cylinder, ts[3], "Axle_D", new Vector3(0f, (BackY0 + gIn + 0.1f) / 2f, 0f), new Vector3(0.6f, (gIn + 0.1f - BackY0) / 2f, 0.6f), axleM);
        Prim(PrimitiveType.Cylinder, ts[3], "Axle_DFront", new Vector3(0f, (gOut - 0.1f + FrontBearY1 + 0.04f) / 2f, 0f), new Vector3(0.6f, (FrontBearY1 + 0.04f - gOut + 0.1f) / 2f, 0.6f), axleM);

        // 橋（D の子、D = 0° でローカル +X へのびる。上の面が軸の高さ）
        var leaf = new GameObject("BridgeLeaf");
        leaf.transform.SetParent(ts[3], false);
        leaf.transform.localPosition = new Vector3(BridgeLen / 2f, PathNear + PathW / 2f, -0.2f);
        leaf.layer = pathLayer;
        var lc = leaf.AddComponent<BoxCollider>(); lc.size = new Vector3(BridgeLen, PathW, 0.4f);
        LeafLooks(leaf.transform, roadA, roadB, dark, brass);

        // 道（手前の床）：橋の手前と向こう
        Floor(root, "PathNear", -NearLen, 0f, floorM, pathLayer);
        Floor(root, "PathFar", BridgeLen + 0.05f, BridgeLen + 0.05f + FarLen, floorM, pathLayer);

        // 風車（A の軸の手前）
        var wmGo = new GameObject("Windmill");
        wmGo.transform.SetParent(root, false);
        wmGo.transform.localPosition = new Vector3(a.c.x, 1.8f, a.c.y);
        wmGo.layer = 2;
        var sc = wmGo.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 1.2f;
        var rotor = new GameObject("Rotor").transform; rotor.SetParent(wmGo.transform, false);
        // 2026-11-10 白い板4枚 → ラック・ふたごの台と同じ羽根車（目印の柱は道の方へ出るのでなし）。横向きの軸の水車になる
        {
            var imp = new GameObject("Impeller").transform; imp.SetParent(rotor, false); imp.localPosition = new Vector3(0f, WmY - 1.8f, 0f); imp.localScale = Vector3.one * WmScale;
            FuwaTurnTableTest.Impeller(imp, brass);
            foreach (var x in imp.Cast<Transform>().Where(x => x.name.StartsWith("Mast")).ToArray()) Object.DestroyImmediate(x.gameObject);
        }

        Supports(root, a, c, dark, light, brass);

        // 回す人（自分の画面だけ）
        var wm = UdonSharpUndo.AddComponent<FuwaWindmill>(wmGo);
        wm.rotor = rotor;
        wm.axisRef = wmGo.transform;
        wm.gears = ts;
        wm.gearRatio = new float[4]; wm.gearPhase = new float[4];
        for (int i = 0; i < 4; i++) { wm.gearRatio[i] = gears[i].ratio; wm.gearPhase[i] = gears[i].phase * Mathf.Rad2Deg; }
        wm.maxTurn = maxTurn;
        wm.hitRadius = 1.2f;
        wm.leafCollider = lc;   // 乗っている間に向きがとぶのを戻す（2026-11-10）
        UdonSharpEditorUtility.CopyProxyToUdon(wm);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(wm);
        ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None;

        // 最初の角度（turn = 0：橋が上）
        for (int i = 0; i < 4; i++) ts[i].localRotation = Quaternion.Euler(0f, -gears[i].phase * Mathf.Rad2Deg, 0f);

        // ===== ここからはワールドの向きのまま（root は回っているので、まっすぐな入れ物を別に） =====
        var ex = new GameObject("Extras").transform; ex.SetParent(root, false); ex.rotation = Quaternion.identity; ex.position = Origin;
        float pathZ = Origin.z - (PathNear + PathW / 2f);   // 道のまん中
        float top = Origin.y;
        var ball = Object.FindObjectsOfType<FuwaBall>(true);
        FuwaBall myBall = null;
        foreach (var bb in ball) if (bb.transform.parent && bb.transform.parent.name == "FuwaSystem") myBall = bb;
        if (myBall == null && ball.Length > 0) myBall = ball[0];

        // 鳥（仮：まるの組み合わせ）と止まり木
        // 2026-11-07 てじゃ「倒れる橋も鳥なくてもいけるかも」「浮いてる間に撃って間に合いそう」→ 鳥なし（コードは残す、WithBird で戻せる）
        if (WithBird) BuildBird(ex, myBall, new Vector3(Origin.x - 5f, top + 3.0f, pathZ), pathZ, top);

        // ふわふわを出す台・ワープ（テスト用）
        var spot = new GameObject("BallSpot").transform; spot.SetParent(ex, false);
        spot.position = new Vector3(Origin.x - 12f, top + 1.6f, pathZ);
        FuwaStage2WaterTest.BallStand(ex, "BallStand", new Vector3(-13.5f, 0f, pathZ - Origin.z + 1.4f), spot, "ふわふわを出す", new Color(1f, 0.75f, 0.85f));
        var spawn = new GameObject("Spawn").transform; spawn.SetParent(ex, false);
        spawn.position = new Vector3(Origin.x - 14.5f, top, pathZ); spawn.rotation = Quaternion.LookRotation(Vector3.right);
        var warpMat = FuwaGearTest.Mat("GearTest_Warp", new Color(0.55f, 0.8f, 1f));
        var lobby = GameObject.Find("FuwaStage2/Lobby/LobbySpawn");
        FuwaIceTestBuilder.Warp(ex, "WarpToGear", new Vector3(13f, -161.3f, -19.2f), "GEAR TEST", spawn, warpMat);
        if (lobby) FuwaIceTestBuilder.Warp(ex, "WarpBack", new Vector3(Origin.x - 15.5f, top + 0.7f, pathZ - 1.6f), "BACK", lobby.transform, warpMat);

        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[跳ね橋のお試し] 作った maxTurn={maxTurn:F1} rA={rA:F3}");
    }

    // 仮の鳥：まるい体＋おなか＋目＋くちばし＋羽2枚＋しっぽ＋足。止まり木は道の奥のはしから枝をのばす
    static void BuildBird(Transform ex, FuwaBall ball, Vector3 perchPos, float pathZ, float top)
    {
        var bodyM = FuwaGearTest.Mat("BirdTest_Body", new Color(0.97f, 0.74f, 0.45f));
        var wingM = FuwaGearTest.Mat("BirdTest_Wing", new Color(0.86f, 0.55f, 0.32f));
        var bellyM = FuwaGearTest.Mat("BirdTest_Belly", new Color(1f, 0.94f, 0.84f));
        var darkM = FuwaGearTest.Mat("BirdTest_Eye", new Color(0.15f, 0.12f, 0.12f));
        var beakM = FuwaGearTest.Mat("BirdTest_Beak", new Color(1f, 0.82f, 0.3f));
        var woodM = FuwaGearTest.Mat("BirdTest_Branch", new Color(0.5f, 0.36f, 0.25f));

        // 止まり木：道の奥のはし（ワールド -Z 側）に柱、そこから鳥の足の下まで枝
        float footY = perchPos.y - 0.85f;
        float postZ = pathZ - PathW / 2f + 0.15f;
        var post = Prim(PrimitiveType.Cylinder, ex, "PerchPost", Vector3.zero, Vector3.one, woodM);
        post.position = new Vector3(perchPos.x, (top + footY) / 2f, postZ); post.localScale = new Vector3(0.22f, (footY - top) / 2f, 0.22f);
        var branch = Prim(PrimitiveType.Cylinder, ex, "PerchBranch", Vector3.zero, Vector3.one, woodM);
        float bl = perchPos.z - postZ;
        branch.position = new Vector3(perchPos.x, footY - 0.06f, postZ + bl / 2f); branch.rotation = Quaternion.Euler(90f, 0f, 0f); branch.localScale = new Vector3(0.12f, bl / 2f + 0.15f, 0.12f);

        var bird = new GameObject("Bird").transform; bird.SetParent(ex, false);
        bird.position = perchPos; bird.rotation = Quaternion.LookRotation(Vector3.right);
        Prim(PrimitiveType.Sphere, bird, "Body", Vector3.zero, new Vector3(1.1f, 1.0f, 1.25f), bodyM);
        Prim(PrimitiveType.Sphere, bird, "Belly", new Vector3(0f, -0.12f, 0.18f), new Vector3(0.8f, 0.7f, 0.85f), bellyM);
        Prim(PrimitiveType.Sphere, bird, "EyeL", new Vector3(-0.27f, 0.17f, 0.48f), Vector3.one * 0.12f, darkM);
        Prim(PrimitiveType.Sphere, bird, "EyeR", new Vector3(0.27f, 0.17f, 0.48f), Vector3.one * 0.12f, darkM);
        var beak = Prim(PrimitiveType.Cube, bird, "Beak", new Vector3(0f, 0.02f, 0.66f), new Vector3(0.16f, 0.16f, 0.16f), beakM);
        beak.localRotation = Quaternion.Euler(45f, 0f, 45f);
        var tail = Prim(PrimitiveType.Sphere, bird, "Tail", new Vector3(0f, 0.12f, -0.66f), new Vector3(0.45f, 0.12f, 0.45f), wingM);
        tail.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        // 羽：つけ根（体の横）で回る。見た目は外へのびる平たいまる
        var wl = new GameObject("WingL").transform; wl.SetParent(bird, false); wl.localPosition = new Vector3(-0.48f, 0.08f, 0f);
        Prim(PrimitiveType.Sphere, wl, "Wing", new Vector3(-0.35f, 0f, -0.05f), new Vector3(0.8f, 0.1f, 0.55f), wingM);
        var wr = new GameObject("WingR").transform; wr.SetParent(bird, false); wr.localPosition = new Vector3(0.48f, 0.08f, 0f);
        Prim(PrimitiveType.Sphere, wr, "Wing", new Vector3(0.35f, 0f, -0.05f), new Vector3(0.8f, 0.1f, 0.55f), wingM);
        // 足
        Prim(PrimitiveType.Cylinder, bird, "LegL", new Vector3(-0.16f, -0.65f, 0.05f), new Vector3(0.06f, 0.2f, 0.06f), beakM);
        Prim(PrimitiveType.Cylinder, bird, "LegR", new Vector3(0.16f, -0.65f, 0.05f), new Vector3(0.06f, 0.2f, 0.06f), beakM);

        // 飛ぶ道すじ：止まり木 → 少し上がって谷の上 → 向こう岸の上で放す（なめらかに点を並べる）
        // 上がっている橋（高さ8m）を飛びこえる：ふわふわの下のはしが橋の上を通るように
        var keys = new[]
        {
            perchPos,
            perchPos + new Vector3(2.5f, 3.0f, 0f),
            perchPos + new Vector3(5f, 7.2f, 0f),
            perchPos + new Vector3(9f, 7.6f, 0f),
            perchPos + new Vector3(13.5f, 5.0f, 0f),
            perchPos + new Vector3(19f, 1.5f, 0f),
        };
        var pts = new System.Collections.Generic.List<Vector3>();
        for (int i = 0; i < keys.Length - 1; i++)
        {
            Vector3 p0 = keys[Mathf.Max(i - 1, 0)], p1 = keys[i], p2 = keys[i + 1], p3 = keys[Mathf.Min(i + 2, keys.Length - 1)];
            for (int s = 0; s < 10; s++)
            {
                float t = s / 10f, t2 = t * t, t3 = t2 * t;
                pts.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        pts.Add(keys[keys.Length - 1]);

        var b = UdonSharpUndo.AddComponent<FuwaBird>(bird.gameObject);
        b.ball = ball; b.body = bird; b.wingL = wl; b.wingR = wr;
        b.path = pts.ToArray();
        b.carrySpeed = 2.4f;   // 道すじ約24m → 約10秒
        UdonSharpEditorUtility.CopyProxyToUdon(b);
        FuwaStage2WaterTest.NoSync(b);
    }

    // エディタで動画を撮る用：turn（風車の回った度）にする
    public static void SetTurn(float turn)
    {
        var root = GameObject.Find("DrawbridgeTest"); if (!root) return;
        var wm = root.GetComponentInChildren<FuwaWindmill>(true); if (!wm) return;
        float u = -turn / wm.gearRatio[0];
        for (int i = 0; i < wm.gears.Length; i++) wm.gears[i].localRotation = Quaternion.Euler(0f, -(wm.gearRatio[i] * u + wm.gearPhase[i]), 0f);
        wm.rotor.localRotation = Quaternion.Euler(0f, turn, 0f);
    }

    [MenuItem("FuwaCourse/ステージ2/風車の跳ね橋のお試しを消す")]
    public static void Remove()
    {
        var old = GameObject.Find("DrawbridgeTest");
        if (old) Object.DestroyImmediate(old);
    }

    // ===== 2026-11-10 てじゃ「跳ね橋あたりもクオリティ上げたい」→ 全部（橋の板・扇形の歯車・軸受け・羽根車・支えの枠）=====
    //  層（ローカル Y）：うしろの梁 BackY0〜BackY1 ／ 奥の段 D・C 0〜Th ／ 手前の段 A・B Front〜Front+Th ／ 前のうで ArmY0〜ArmY1 ／ 羽根車 WmY のまわり ／ 道 PathNear〜
    //  A の軸はうしろへのばさない（D の扇形が回る範囲を通る）。A は前のうでで B・C の軸に、B・C の軸はうしろの梁で D の軸に、D の軸は道の床の横の軸受けに
    const float SecA0 = 96f, SecA1 = 214f, RimW = 0.55f;
    const float BackY0 = -0.5f, BackY1 = -0.16f, ArmY0 = 1.17f, ArmY1 = 1.37f, WmY = 1.78f, WmScale = 1.1f;
    const float BackBearY0 = 1.36f, BackBearY1 = 1.72f, FrontBearY0 = 6.08f, FrontBearY1 = 6.44f;

    // 扇形の歯のふち：歯の輪郭（中心の点はのぞく）と、その内がわ RimW の弧のあいだの帯。表・裏・歯の側面・内がわの弧・両はし
    static Mesh SectorRim(Vector2[] o0, float h, float rimW, string name)
    {
        int n = o0.Length - 1; var o = new Vector2[n]; System.Array.Copy(o0, o, n);
        float rf = float.MaxValue; foreach (var p in o) rf = Mathf.Min(rf, p.magnitude);
        float rIn = rf - rimW;
        var inn = new Vector2[n]; for (int i = 0; i < n; i++) inn[i] = o[i].normalized * rIn;
        var v = new System.Collections.Generic.List<Vector3>(); var t = new System.Collections.Generic.List<int>();
        void Tri(int a, int b, int c, Vector3 want) { if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), want) < 0f) { int q = b; b = c; c = q; } t.Add(a); t.Add(b); t.Add(c); }
        Vector3 P3(Vector2 q, float y) => new Vector3(q.x, y, q.y);
        foreach (float y in new[] { h, 0f })
        {
            var want = y > 0f ? Vector3.up : Vector3.down;
            int bo = v.Count; for (int i = 0; i < n; i++) v.Add(P3(o[i], y));
            int bi = v.Count; for (int i = 0; i < n; i++) v.Add(P3(inn[i], y));
            for (int i = 0; i < n - 1; i++) { Tri(bi + i, bo + i, bo + i + 1, want); Tri(bi + i, bo + i + 1, bi + i + 1, want); }
        }
        // 歯の側面（角は分ける）
        for (int i = 0; i < n - 1; i++)
        {
            var e = o[i + 1] - o[i]; var mid = (o[i] + o[i + 1]) / 2f; var nn = new Vector3(e.y, 0f, -e.x); if (Vector2.Dot(new Vector2(nn.x, nn.z), mid) < 0f) nn = -nn;
            int b0 = v.Count; v.Add(P3(o[i], 0f)); v.Add(P3(o[i + 1], 0f)); v.Add(P3(o[i + 1], h)); v.Add(P3(o[i], h));
            Tri(b0, b0 + 1, b0 + 2, nn); Tri(b0, b0 + 2, b0 + 3, nn);
        }
        // 内がわの弧（なめらか）
        {
            int b0 = v.Count; for (int i = 0; i < n; i++) { v.Add(P3(inn[i], 0f)); v.Add(P3(inn[i], h)); }
            for (int i = 0; i < n - 1; i++) { var mid = (inn[i] + inn[i + 1]) / 2f; var nn = -new Vector3(mid.x, 0f, mid.y); int a = b0 + 2 * i; Tri(a, a + 2, a + 3, nn); Tri(a, a + 3, a + 1, nn); }
        }
        // 両はし
        foreach (int i in new[] { 0, n - 1 })
        {
            var dir = o[i].normalized; var tg = new Vector3(-dir.y, 0f, dir.x) * (i == 0 ? -1f : 1f);
            int b0 = v.Count; v.Add(P3(inn[i], 0f)); v.Add(P3(o[i], 0f)); v.Add(P3(o[i], h)); v.Add(P3(inn[i], h));
            Tri(b0, b0 + 1, b0 + 2, tg); Tri(b0, b0 + 2, b0 + 3, tg);
        }
        var m = new Mesh { name = name }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    // 扇形の内がわ：こしき（真ちゅうのボルトつき）と、こしき → ふちのスポーク（両はしと、まん中2本）。厚みは歯車より少しうすく（表の面が重ならない）
    static void SectorInside(Transform g, Material dark, Material brass)
    {
        float r = Mod * ZD / 2f, rIn = r - 1.25f * Mod - RimW, hubR = 1.15f;
        var hub = Prim(PrimitiveType.Cylinder, g, "SectorHub", new Vector3(0f, Th / 2f, 0f), new Vector3(2f * hubR, Th / 2f + 0.08f, 2f * hubR), dark);
        Prim(PrimitiveType.Cylinder, g, "SectorHubCap", new Vector3(0f, Th + 0.1f, 0f), new Vector3(1.3f, 0.02f, 1.3f), brass);
        for (int k = 0; k < 8; k++)
        {
            float a = k * 45f * Mathf.Deg2Rad;
            Prim(PrimitiveType.Cylinder, g, "HubBolt" + k, new Vector3(Mathf.Cos(a) * 0.88f, Th + 0.1f, Mathf.Sin(a) * 0.88f), new Vector3(0.16f, 0.03f, 0.16f), brass);
        }
        float pitchDeg = 360f / ZD;
        var angs = new[] { SecA0 + pitchDeg * 0.6f, Mathf.Lerp(SecA0, SecA1, 0.36f), Mathf.Lerp(SecA0, SecA1, 0.64f), SecA1 - pitchDeg * 0.6f };
        float r0 = hubR - 0.1f, r1 = rIn + 0.12f, sw = 0.42f;
        foreach (float ad in angs)
        {
            var dir = new Vector3(Mathf.Cos(ad * Mathf.Deg2Rad), 0f, Mathf.Sin(ad * Mathf.Deg2Rad));
            var sp = Prim(PrimitiveType.Cube, g, "Spoke" + Mathf.RoundToInt(ad), dir * (r0 + r1) / 2f + new Vector3(0f, Th / 2f, 0f), new Vector3(r1 - r0, Th - 0.06f, sw), dark);
            sp.localRotation = Quaternion.FromToRotation(Vector3.right, dir);
        }
    }

    // 橋の板：道と同じシマ（道と同じ 0.6m の厚み、上の面はそのまま）＋両はしの鉄の桁（真ちゅうのびょう）＋裏の横の梁
    static void LeafLooks(Transform leaf, Material roadA, Material roadB, Material dark, Material brass)
    {
        // leaf の local：X＝橋の長さ、Y＝幅、Z＝厚み（上の面が +0.2）
        float top = 0.2f, tk = 0.6f; int n = Mathf.Max(1, Mathf.RoundToInt(BridgeLen / 1.2f));
        for (int k = 0; k < n; k++)
            FuwaTwinTableTest.PlateBox(leaf, "Visual" + k, new Vector3(-BridgeLen / 2f + BridgeLen * (k + 0.5f) / n, 0f, top - tk / 2f), new Vector3(BridgeLen / n, PathW, tk), k % 2 == 0 ? roadB : roadA, 2);   // 縞鋼板（上の面＝leaf の +z）
        float gw = 0.14f, gd = 0.86f, gx0 = -BridgeLen / 2f + 0.06f, gx1 = BridgeLen / 2f;
        foreach (float sg in new[] { -1f, 1f })
        {
            float gy = sg * (PathW / 2f + gw / 2f);
            Prim(PrimitiveType.Cube, leaf, "Girder" + (sg < 0 ? "L" : "R"), new Vector3((gx0 + gx1) / 2f, gy, top - gd / 2f), new Vector3(gx1 - gx0, gw, gd), dark);
            int nr = Mathf.RoundToInt((gx1 - gx0) / 0.7f);
            for (int k = 0; k < nr; k++)
                foreach (float rz in new[] { top - 0.16f, top - gd + 0.16f })
                {
                    var rv = Prim(PrimitiveType.Cylinder, leaf, "Rivet", new Vector3(gx0 + (gx1 - gx0) * (k + 0.5f) / nr, gy + sg * (gw / 2f + 0.012f), rz), new Vector3(0.1f, 0.014f, 0.1f), brass);
                    rv.localRotation = Quaternion.identity;   // 円柱の軸は Y（桁の外の面に垂直）
                }
        }
        // つけ根の輪（桁の手前のはし、軸のはしをかくす。いっしょに回る）
        foreach (float sg in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Cylinder, leaf, "HingeLug", new Vector3(-BridgeLen / 2f, sg * (PathW / 2f + gw / 2f), top), new Vector3(0.84f, gw / 2f, 0.84f), dark);
            Prim(PrimitiveType.Cylinder, leaf, "HingeLugCap", new Vector3(-BridgeLen / 2f, sg * (PathW / 2f + gw + 0.006f), top), new Vector3(0.5f, 0.006f, 0.5f), brass);
        }
        for (int k = 0; k < 4; k++)
            Prim(PrimitiveType.Cube, leaf, "CrossBeam" + k, new Vector3(-BridgeLen / 2f + BridgeLen * (k + 0.5f) / 4f, 0f, top - tk - 0.1f), new Vector3(0.22f, PathW, 0.2f), dark);
    }

    // 支え：橋のつけ根の軸受け2つ（道の床の横に腕で止める）、うしろの梁（D の軸 ↔ C の軸）、前のうで（C の軸 ↔ A の軸）
    static void Supports(Transform root, FuwaGearTest.Gear a, FuwaGearTest.Gear c, Material dark, Material light, Material brass)
    {
        // 橋のつけ根の軸受け（x = 0、z = 0 のまわり）。腕は道の床（x < 0、z −0.6〜0）の横の面にあたる
        foreach (var (y0, y1, floorY) in new[] { (BackBearY0, BackBearY1, PathNear), (FrontBearY0, FrontBearY1, PathNear + PathW) })
        {
            float yc = (y0 + y1) / 2f;
            Prim(PrimitiveType.Cube, root, "HingeBearing", new Vector3(0f, yc, -0.015f), new Vector3(0.84f, y1 - y0, 0.87f), dark);
            Prim(PrimitiveType.Cylinder, root, "HingeBearingCap", new Vector3(0f, yc, 0f), new Vector3(0.74f, (y1 - y0) / 2f + 0.01f, 0.74f), brass);   // 橋のつけ根の輪の金のふたとくっつかないよう、出っぱりは 1cm
            float by0 = Mathf.Min(y0, floorY), by1 = Mathf.Max(y1, floorY);
            Prim(PrimitiveType.Cube, root, "HingeBracket", new Vector3(-0.975f, (by0 + by1) / 2f, -0.55f), new Vector3(1.35f, by1 - by0, 0.2f), light);   // 橋の桁のつけ根の輪（半径 0.42）が回る所をよける
        }
        // うしろの梁：D の軸（0,0）と C の軸。両はしに丸いこしき
        Vector2 pD = Vector2.zero, pC = c.c;
        Beam(root, "BackBeam", pD, pC, (BackY0 + BackY1) / 2f, BackY1 - BackY0, 0.62f, light);
        foreach (var q in new[] { pD, pC })
            Prim(PrimitiveType.Cylinder, root, "BackBoss", new Vector3(q.x, (BackY0 + BackY1) / 2f, q.y), new Vector3(1.05f, (BackY1 - BackY0) / 2f + 0.03f, 1.05f), dark);
        // 前のうで：C の軸 → A の軸（B・A の歯車の前、羽根車のうしろ）
        Beam(root, "FrontArm", pC, a.c, (ArmY0 + ArmY1) / 2f, ArmY1 - ArmY0, 0.46f, light);
        foreach (var q in new[] { pC, a.c })
            Prim(PrimitiveType.Cylinder, root, "ArmBoss", new Vector3(q.x, (ArmY0 + ArmY1) / 2f, q.y), new Vector3(0.9f, (ArmY1 - ArmY0) / 2f + 0.03f, 0.9f), dark);
    }
    static void Beam(Transform root, string name, Vector2 p, Vector2 q, float y, float th, float w, Material m)
    {
        var d = q - p; var mid = (p + q) / 2f;
        var b = Prim(PrimitiveType.Cube, root, name, new Vector3(mid.x, y, mid.y), new Vector3(d.magnitude, th, w), m);
        b.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 0f);
    }

    static void Floor(Transform root, string name, float u0, float u1, Material m, int layer)
    {
        var g = new GameObject(name); g.transform.SetParent(root, false); g.layer = layer;
        g.transform.localPosition = new Vector3((u0 + u1) / 2f, PathNear + PathW / 2f, -0.5f);
        Prim(PrimitiveType.Cube, g.transform, "Visual", Vector3.zero, new Vector3(u1 - u0, PathW, 1f), m);
        var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(u1 - u0, PathW, 1f);
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
