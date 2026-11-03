using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// つるつるの床の実機テスト場（2026-11-03）。ステージ2のロビーの「ICE TEST」をクリックして行く。公開前に「消す」メニューで消す
public static class FuwaIceTestBuilder
{
    const string MatDir = "Assets/FuwaStage2/Materials/";
    const float Top = -162f, StartZ = -42f, Len = 30f, Wide = 8f, SlopeDeg = 10f;

    [MenuItem("FuwaCourse/ステージ2/氷のテスト場を作る")]
    public static void Build()
    {
        Remove();
        var stage = GameObject.Find("FuwaStage2").transform;
        var root = new GameObject("IceTest").transform; root.SetParent(stage, true);
        var ground = Mat("IceTest_Ground", new Color(0.72f, 0.78f, 0.84f));
        var wall = Mat("IceTest_Wall", new Color(0.45f, 0.5f, 0.6f));
        var pillar = Mat("IceTest_Pillar", new Color(0.55f, 0.7f, 0.9f));

        Box(root, "Pad", new Vector3(13f, Top - 0.25f, -38f), Quaternion.identity, new Vector3(40f, 0.5f, 8f), ground, true);
        var spawn = new GameObject("Spawn").transform; spawn.SetParent(root, true);
        spawn.position = new Vector3(13f, Top + 0.05f, -38f); spawn.rotation = Quaternion.LookRotation(Vector3.back);

        // A・D＝速さを上書き＋自前のジャンプ（平ら・坂）、C・B＝勢いだけワープ（平ら・坂）
        Strip(root, "A", 0, 3, 0f, new Color(0.62f, 0.86f, 1f), wall, pillar);
        Strip(root, "B", 1, 2, SlopeDeg, new Color(0.72f, 0.92f, 1f), wall, pillar);
        Strip(root, "C", 2, 2, 0f, new Color(0.82f, 0.96f, 1f), wall, pillar);
        Strip(root, "D", 3, 3, SlopeDeg, new Color(0.7f, 0.82f, 1f), wall, pillar);

        var lobby = stage.Find("Lobby/LobbySpawn");
        Warp(root, "WarpToIce", new Vector3(13f, -161.3f, -19.2f), "ICE TEST", spawn, ground);
        Warp(root, "WarpBack", new Vector3(13f, -161.3f, -35.2f), "BACK", lobby, ground);

        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[氷のテスト場] 作った");
    }

    [MenuItem("FuwaCourse/ステージ2/氷のテスト場を消す")]
    public static void Remove()
    {
        var stage = GameObject.Find("FuwaStage2"); if (!stage) return;
        var old = stage.transform.Find("IceTest"); if (!old) return;
        Object.DestroyImmediate(old.gameObject);
        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stage.scene);
    }

    static void Strip(Transform root, string label, int index, int mode, float slope, Color col, Material wall, Material pillar)
    {
        float x = 13f + (index - 1.5f) * (Wide + 1f);
        var dir = Quaternion.AngleAxis(slope, Vector3.right) * Vector3.back;   // 奥（-z）へ、slope 度だけ上る
        var start = new Vector3(x, Top, StartZ);
        var s = new GameObject("Ice" + label).transform; s.SetParent(root, true);
        s.position = start + dir * (Len * 0.5f); s.rotation = Quaternion.LookRotation(dir, Vector3.up);

        var col0 = s.gameObject.AddComponent<BoxCollider>(); col0.center = new Vector3(0f, -0.25f, 0f); col0.size = new Vector3(Wide, 0.5f, Len);
        Visual(s, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(Wide, 0.5f, Len), Mat("IceTest_Ice" + label, col));

        var ice = UdonSharpUndo.AddComponent<FuwaIceFloor>(s.gameObject);
        ice.label = label; ice.mode = mode; ice.area = col0;
        UdonSharpEditorUtility.CopyProxyToUdon(ice);
        FuwaStage2WaterTest.NoSync(ice);

        Box(s, "WallL", s.TransformPoint(new Vector3(-Wide * 0.5f - 0.15f, 0.6f, 0f)), s.rotation, new Vector3(0.3f, 1.2f, Len), wall, true);
        Box(s, "WallR", s.TransformPoint(new Vector3(Wide * 0.5f + 0.15f, 0.6f, 0f)), s.rotation, new Vector3(0.3f, 1.2f, Len), wall, true);
        Box(s, "WallEnd", s.TransformPoint(new Vector3(0f, 0.6f, Len * 0.5f + 0.15f)), s.rotation, new Vector3(Wide + 0.6f, 1.2f, 0.3f), wall, true);
        foreach (var p in new[] { new Vector3(-2f, 1.5f, -7f), new Vector3(2f, 1.5f, 0f), new Vector3(-1f, 1.5f, 7f) })
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); c.name = "Pillar"; c.transform.SetParent(s, false);
            c.transform.localPosition = p; c.transform.localRotation = Quaternion.identity; c.transform.localScale = new Vector3(1f, 1.5f, 1f);
            c.GetComponent<Renderer>().sharedMaterial = pillar;
        }
        Text(root, "Label" + label, start + Vector3.up * 3.2f, label, 10f);
    }

    static void Warp(Transform root, string name, Vector3 pos, string label, Transform target, Material mat)
    {
        var g = Box(root, name, pos, Quaternion.identity, new Vector3(0.7f, 0.7f, 0.7f), mat, true);
        var w = UdonSharpUndo.AddComponent<FuwaIceTestWarp>(g.gameObject); w.target = target; w.InteractionText = label;
        UdonSharpEditorUtility.CopyProxyToUdon(w);
        FuwaStage2WaterTest.NoSync(w);
        Text(root, name + "_Label", pos + Vector3.up * 0.9f, label, 2f);
    }

    static void Text(Transform root, string name, Vector3 pos, string text, float size)
    {
        var g = new GameObject(name); g.transform.SetParent(root, true);
        g.transform.position = pos; g.transform.rotation = Quaternion.LookRotation(Vector3.back);
        var t = g.AddComponent<TMPro.TextMeshPro>();
        t.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/FuwaCourse/Fonts/DarumadropOne SDF Dyn.asset");
        t.text = text; t.fontSize = size; t.alignment = TMPro.TextAlignmentOptions.Center; t.color = new Color(0.1f, 0.15f, 0.3f);
        t.rectTransform.sizeDelta = new Vector2(8f, 3f);
    }

    static Transform Box(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 size, Material mat, bool collider)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name;
        g.transform.SetParent(parent, true); g.transform.position = pos; g.transform.rotation = rot; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g.transform;
    }

    static void Visual(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = localPos; g.transform.localRotation = Quaternion.identity; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material Mat(string name, Color c)
    {
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Stage2_LobbyGround.mat"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_Color", c); EditorUtility.SetDirty(m);
        return m;
    }
}
