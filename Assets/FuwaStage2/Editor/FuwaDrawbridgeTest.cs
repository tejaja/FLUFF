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
    const float BridgeLen = 8f, PathW = 4.5f, PathNear = 1.9f;   // 道は手前（ローカル Y = 1.9〜6.4）
    static readonly Vector3 Origin = new Vector3(-40f, -162f, -100f);

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
        int pathLayer = 11;

        var ts = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            var g = gears[i];
            var go = new GameObject(g.name);
            go.transform.SetParent(root, false);
            bool front = g == a || g == b;
            go.transform.localPosition = new Vector3(g.c.x, front ? Front : 0f, g.c.y);
            Vector2[] outline;
            if (g == d)
            {
                // 扇形：橋が 0〜90° 動く間にかみ合う所（C の向き 200° から、ローカルで 110〜200°）＋ゆとり
                outline = FuwaGearTest.GearOutline(ZD, 10, Mod, 96f * Mathf.Deg2Rad, 214f * Mathf.Deg2Rad);
            }
            else outline = FuwaGearTest.GearOutline(g.z, 10, Mod);
            var mesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(outline, Th, "Bridge_" + g.name, Vector2.zero));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = front ? light : dark;
            ts[i] = go.transform;
            // 回ってるのがわかる印
            var mk = Prim(PrimitiveType.Cylinder, go.transform, "Mark", new Vector3(Mod * g.z / 2f - 1.8f * Mod, Th + 0.01f, 0f), new Vector3(0.3f, 0.01f, 0.3f), mark);
        }
        // 軸
        Prim(PrimitiveType.Cylinder, root, "Axle_BC", new Vector3(b.c.x, Front + Th / 2f, b.c.y) + Vector3.zero, new Vector3(0.7f, (Front + Th) / 2f + 0.2f, 0.7f), axleM).localPosition = new Vector3(b.c.x, (Front + Th) / 2f, b.c.y);
        Prim(PrimitiveType.Cylinder, root, "Axle_A", new Vector3(a.c.x, (Front + 1.8f) / 2f, a.c.y), new Vector3(0.5f, (1.8f - Front) / 2f + 0.1f, 0.5f), axleM).localPosition = new Vector3(a.c.x, (Front + 1.8f) / 2f, a.c.y);
        // 橋のつけ根の軸（D から橋まで、道の幅いっぱい）
        Prim(PrimitiveType.Cylinder, ts[3], "Axle_D", new Vector3(0f, (PathNear + PathW) / 2f, 0f), new Vector3(0.6f, (PathNear + PathW) / 2f + 0.2f, 0.6f), axleM);

        // 橋（D の子、D = 0° でローカル +X へのびる。上の面が軸の高さ）
        var leaf = new GameObject("BridgeLeaf");
        leaf.transform.SetParent(ts[3], false);
        leaf.transform.localPosition = new Vector3(BridgeLen / 2f, PathNear + PathW / 2f, -0.2f);
        leaf.layer = pathLayer;
        var lv = Prim(PrimitiveType.Cube, leaf.transform, "Visual", Vector3.zero, new Vector3(BridgeLen, PathW, 0.4f), wood);
        var lc = leaf.AddComponent<BoxCollider>(); lc.size = new Vector3(BridgeLen, PathW, 0.4f);
        // 橋の板の筋（回ってるのが見えるように）
        for (int k = 1; k < 8; k++) Prim(PrimitiveType.Cube, leaf.transform, "Plank" + k, new Vector3(-BridgeLen / 2f + k * BridgeLen / 8f, 0f, 0.2f), new Vector3(0.06f, PathW, 0.02f), axleM);

        // 道（手前の床）：橋の手前と向こう
        Floor(root, "PathNear", -16f, 0f, floorM, pathLayer);
        Floor(root, "PathFar", BridgeLen + 0.05f, BridgeLen + 14f, floorM, pathLayer);

        // 風車（A の軸の手前）
        var wmGo = new GameObject("Windmill");
        wmGo.transform.SetParent(root, false);
        wmGo.transform.localPosition = new Vector3(a.c.x, 1.8f, a.c.y);
        wmGo.layer = 2;
        var sc = wmGo.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 1.2f;
        var rotor = new GameObject("Rotor").transform; rotor.SetParent(wmGo.transform, false);
        Prim(PrimitiveType.Cylinder, rotor, "Hub", Vector3.zero, new Vector3(0.45f, 0.12f, 0.45f), axleM);
        for (int k = 0; k < 4; k++)
        {
            var bl = Prim(PrimitiveType.Cube, rotor, "Blade" + k, Vector3.zero, new Vector3(1.05f, 0.05f, 0.42f), blade);
            float ang = k * 90f;
            bl.localRotation = Quaternion.Euler(0f, -ang, 0f) * Quaternion.Euler(18f, 0f, 0f);   // ひねり（風を受ける角度）
            bl.localPosition = Quaternion.Euler(0f, -ang, 0f) * new Vector3(0.7f, 0f, 0f);
        }

        // 回す人（自分の画面だけ）
        var wm = UdonSharpUndo.AddComponent<FuwaWindmill>(wmGo);
        wm.rotor = rotor;
        wm.axisRef = wmGo.transform;
        wm.gears = ts;
        wm.gearRatio = new float[4]; wm.gearPhase = new float[4];
        for (int i = 0; i < 4; i++) { wm.gearRatio[i] = gears[i].ratio; wm.gearPhase[i] = gears[i].phase * Mathf.Rad2Deg; }
        wm.maxTurn = maxTurn;
        wm.hitRadius = 1.2f;
        UdonSharpEditorUtility.CopyProxyToUdon(wm);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(wm);
        ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None;

        // 最初の角度（turn = 0：橋が上）
        for (int i = 0; i < 4; i++) ts[i].localRotation = Quaternion.Euler(0f, -gears[i].phase * Mathf.Rad2Deg, 0f);

        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[跳ね橋のお試し] 作った maxTurn={maxTurn:F1} rA={rA:F3}");
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
