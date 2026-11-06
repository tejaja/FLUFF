using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2–4：歯車バンパーのお試し（2026-11-06）
// ふわふわの高さに、たて軸の歯車が3枚かみ合って回る（30枚 → 20枚 → 14枚）。かみ合った歯車は逆回り・歯の速さは同じ
// 14枚の軸の上の段に 30枚（2段歯車）：同じ回る速さで半径が大きい → 歯の速さが約2倍（強くはじく）
public static class FuwaGearBumperTest
{
    const float Mod = 0.3f, Th = 0.7f, LowY = 1.25f, HighY = 2.6f;
    const float BaseDeg = 30f;
    static readonly Vector3 Origin = new Vector3(4f, -162f, -104f);   // 床のまん中

    [MenuItem("FuwaCourse/ステージ2/歯車バンパーのお試しを作る")]
    public static void Build()
    {
        Remove();
        var dark = FuwaGearTest.Mat("GearTest_Gear", new Color(0.36f, 0.38f, 0.43f));
        var light = FuwaGearTest.Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f));
        var axleM = FuwaGearTest.Mat("GearTest_Axle", new Color(0.2f, 0.21f, 0.24f));
        var brass = FuwaGearTest.Mat("FanTest_Brass", new Color(0.86f, 0.68f, 0.32f));
        var floorM = FuwaGearTest.Mat("GearTest_Floor", new Color(0.55f, 0.58f, 0.63f));
        var mark = FuwaGearTest.Mat("GearTest_Mark", new Color(0.95f, 0.72f, 0.25f));

        var root = new GameObject("GearBumperTest").transform; root.position = Origin;
        var floor = new GameObject("Floor"); floor.transform.SetParent(root, false); floor.layer = 11;
        floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        Prim(PrimitiveType.Cube, floor.transform, "Visual", Vector3.zero, new Vector3(20f, 1f, 16f), floorM);
        floor.AddComponent<BoxCollider>().size = new Vector3(20f, 1f, 16f);

        // 歯車の並び（ローカルの 2D）
        var g1 = new FuwaGearTest.Gear { name = "Bumper_z30", z = 30, c = new Vector2(-2f, 3f), ratio = 1f, phase = 0f };
        var g2 = FuwaGearTest.Mesh2(g1, "Bumper_z20", 20, -60f, Mod);
        var g3 = FuwaGearTest.Mesh2(g2, "Bumper_z14", 14, 0f, Mod);
        var g3b = new FuwaGearTest.Gear { name = "Bumper_z30_upper", z = 30, c = g3.c, ratio = g3.ratio, phase = 0f };   // 2段（同じ軸）
        var list = new[] { g1, g2, g3, g3b };
        foreach (var g in list)
        {
            bool upper = g == g3b;
            var go = new GameObject(g.name); go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(g.c.x, upper ? HighY : LowY, g.c.y);
            var mesh = FuwaGearTest.SaveMesh(FuwaGearTest.Extrude(FuwaGearTest.GearOutline(g.z, 10, Mod), Th, "Bumper_m03_z" + g.z, Vector2.zero));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = upper ? brass : (g == g2 ? light : dark);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            var mk = Prim(PrimitiveType.Cylinder, go.transform, "Mark", new Vector3(Mod * g.z / 2f - 1.8f * Mod, Th + 0.01f, 0f), new Vector3(0.4f, 0.01f, 0.4f), mark);
            var b = UdonSharpUndo.AddComponent<FuwaGearBumper>(go);
            b.ratio = g.ratio; b.phase = g.phase * Mathf.Rad2Deg; b.baseDegPerSec = BaseDeg;
            UdonSharpEditorUtility.CopyProxyToUdon(b); FuwaStage2WaterTest.NoSync(b);
            go.transform.localRotation = Quaternion.Euler(0f, -b.phase, 0f);
            // 軸
            if (!upper) Prim(PrimitiveType.Cylinder, root, "Axle_" + g.name, new Vector3(g.c.x, (g == g3 ? HighY + Th : LowY + Th) / 2f, g.c.y), new Vector3(0.5f, (g == g3 ? HighY + Th : LowY + Th) / 2f + 0.1f, 0.5f), axleM);
        }

        // ふわふわを出す台
        var spot = new GameObject("BallSpot").transform; spot.SetParent(root, false); spot.localPosition = new Vector3(-7.5f, 1.6f, -3f);
        FuwaStage2WaterTest.BallStand(root, "BallStand", new Vector3(-8.5f, 0f, -6f), spot, "ふわふわを出す", new Color(1f, 0.75f, 0.85f));

        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[歯車バンパーのお試し] 作った z20 {g2.c} z14 {g3.c}");
    }

    public static void SetTime(float sec)
    {
        var o = GameObject.Find("GearBumperTest"); if (!o) return;
        foreach (var b in o.GetComponentsInChildren<FuwaGearBumper>(true))
            b.transform.localRotation = Quaternion.Euler(0f, -Mathf.Repeat(sec * b.baseDegPerSec * b.ratio + b.phase, 360f), 0f);
    }

    [MenuItem("FuwaCourse/ステージ2/歯車バンパーのお試しを消す")]
    public static void Remove() { var o = GameObject.Find("GearBumperTest"); if (o) Object.DestroyImmediate(o); }

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
