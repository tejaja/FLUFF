using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ステージ2の遠景（2026-11-02 てじゃ「ステージ2に遠景のオブジェクトがなかった」「島と雲」「ステージ2は変えたい」「雲海いいね」）。
// 空はステージ1と同じ青。雲はステージ1のもこもこの塊と変えて、コースの下に広がる雲海にする。ステージ2の間だけ見える（FuwaAreaVisibility.stage2Objects）。
public static class FuwaStage2Far
{
    const string MeshPath = "Assets/FuwaStage2/Generated/Stage2_CloudSea.asset";
    const string MatPath = "Assets/FuwaStage2/Materials/Stage2_CloudSea.mat";
    public static Vector3 Center = new Vector3(25f, -258f, 50f);   // いちばん高い所でも道の下（約 -185）に届かない高さ。-205 だとスタートが埋まった
    public const float Radius = 1100f;
    const int Grid = 82;

    // 大きいもこもこ＋小さいもこもこ。セルごとに置いた丸い丘を足し合わせる（最大を取ると丘の間が V の字に折れてベコッとへこんで見えた）
    static float Bumps(float x, float z, float cell, int seed)
    {
        int cx = Mathf.FloorToInt(x / cell), cz = Mathf.FloorToInt(z / cell); float sum = 0f;
        for (int i = cx - 2; i <= cx + 2; i++) for (int j = cz - 2; j <= cz + 2; j++)
        {
            var rnd = new System.Random(i * 73856093 ^ j * 19349663 ^ seed);
            float px = (i + (float)rnd.NextDouble()) * cell, pz = (j + (float)rnd.NextDouble()) * cell, r = cell * (0.55f + 0.45f * (float)rnd.NextDouble()), h = 0.5f + 0.5f * (float)rnd.NextDouble();
            float d2 = ((x - px) * (x - px) + (z - pz) * (z - pz)) / (r * r);
            sum += h * Mathf.Exp(-d2 * 1.6f);
        }
        return sum;
    }
    static float Height(float x, float z)
    {
        // ゆるい大きなうねり＋もこもこ
        float swell = (Mathf.PerlinNoise(x * 0.004f + 11f, z * 0.004f + 7f) - 0.5f) * 26f;
        return swell + Bumps(x, z, 74f, 11) * 24f + Bumps(x, z, 30f, 29) * 8f;
    }

    public static Mesh BuildMesh()
    {
        // 正方形の格子を、まん中が細かくなるようにゆがめる。円形の格子だと中心に放射状の線と尖りが出た（2026-11-02 てじゃ「ベコッとへこんで気になる」）。隅は丸く切る
        var verts = new List<Vector3>(); var tris = new List<int>(); int N = Grid; var id = new int[N + 1, N + 1];
        for (int gx = 0; gx <= N; gx++) for (int gz = 0; gz <= N; gz++)
        {
            float u = gx / (float)N * 2f - 1f, v = gz / (float)N * 2f - 1f;
            float x = Radius * (0.28f * u + 0.72f * u * u * u), z = Radius * (0.28f * v + 0.72f * v * v * v), rad = Mathf.Sqrt(x * x + z * z);
            id[gx, gz] = -1; if (rad > Radius * 1.02f) continue;
            float edge = Mathf.Clamp01((Radius - rad) / 220f);
            id[gx, gz] = verts.Count; verts.Add(new Vector3(x, Height(Center.x + x, Center.z + z) * edge - (1f - edge) * 30f, z));
        }
        for (int gx = 0; gx < N; gx++) for (int gz = 0; gz < N; gz++)
        {
            int a = id[gx, gz], b = id[gx + 1, gz], c = id[gx, gz + 1], d = id[gx + 1, gz + 1]; if (a < 0 || b < 0 || c < 0 || d < 0) continue;
            tris.AddRange(new[] { a, c, b, b, c, d });
        }
        var m = new Mesh { name = "Stage2_CloudSea", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds();
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (old == null) { AssetDatabase.CreateAsset(m, MeshPath); return m; }
        old.Clear(false); old.indexFormat = m.indexFormat; old.SetVertices(verts); old.SetTriangles(tris, 0); old.RecalculateNormals(); old.RecalculateBounds(); EditorUtility.SetDirty(old);
        return old;
    }

    static Material Mat()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (!m) { m = new Material(Shader.Find("FuwaCourse/CloudSea")); AssetDatabase.CreateAsset(m, MatPath); }
        m.shader = Shader.Find("FuwaCourse/CloudSea");   // うねる雲（2026-11-02 てじゃ「ちょっとうねうね」）
        m.SetColor("_Color", Color.white); m.SetColor("_Shade", new Color(0.55f, 0.72f, 0.95f)); m.SetFloat("_Rim", 0.12f); m.SetFloat("_ShadeContrast", 5f);
        m.SetColor("_Haze", Stage2Horizon); m.SetFloat("_HazeStart", 180f); m.SetFloat("_HazeEnd", 1000f); m.SetFloat("_HazeMax", 0.9f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // クリスタルの刺さった島（Assets/FuwaStage2/Models/Stage2_FarIslands.fbx、Blender の FLUFF_models.blend シーン Stage2FarIslands）。コースのまわり、雲海の上に並べる
    const string IslandFbx = "Assets/FuwaStage2/Models/Stage2_FarIslands.fbx", IslandMat = "Assets/FuwaStage2/Materials/Stage2_FarIsland.mat";
    static readonly (float ang, float dist, float y, float scale, int kind)[] Islands = {
        (12f, 300f, -150f, 1.2f, 0), (48f, 460f, -130f, 1.6f, 2), (86f, 240f, -135f, 0.9f, 1), (118f, 520f, -165f, 1.9f, 0), (152f, 330f, -125f, 1.1f, 2),
        (188f, 430f, -155f, 1.5f, 1), (222f, 260f, -148f, 1.0f, 0), (256f, 560f, -120f, 2.0f, 2), (292f, 350f, -170f, 1.3f, 1), (328f, 500f, -135f, 1.7f, 0) };
    [MenuItem("FuwaCourse/ステージ2/遠景の島を置き直す")]
    public static void BuildIslands()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        GameObject root = null; foreach (var g in scene.GetRootGameObjects()) if (g.name == "Stage2Far") root = g;
        if (!root) { root = new GameObject("Stage2Far"); }
        var old = root.transform.Find("Islands"); if (old) Object.DestroyImmediate(old.gameObject);
        var parent = new GameObject("Islands").transform; parent.SetParent(root.transform, false);
        var meshes = new Dictionary<int, Mesh>();
        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(IslandFbx)) if (a is Mesh me) for (int k = 0; k < 3; k++) if (me.name.EndsWith("Island" + k)) meshes[k] = me;
        var mat = AssetDatabase.LoadAssetAtPath<Material>(IslandMat);
        if (!mat) { mat = new Material(Shader.Find("FuwaCourse/FarIsland")); AssetDatabase.CreateAsset(mat, IslandMat); }
        mat.SetColor("_Haze", Stage2Horizon); mat.SetFloat("_HazeStart", 150f); mat.SetFloat("_HazeEnd", 900f); mat.SetFloat("_HazeMax", 0.7f); mat.SetFloat("_Bob", 1.6f); mat.SetFloat("_FogMax", 0f); EditorUtility.SetDirty(mat);
        var rnd = new System.Random(41); int n = 0;
        foreach (var it in Islands)
        {
            if (!meshes.TryGetValue(it.kind, out var m)) continue;
            float a = it.ang * Mathf.Deg2Rad;
            var o = new GameObject("Island" + n++, typeof(MeshFilter), typeof(MeshRenderer)); o.transform.SetParent(parent, false);
            o.transform.position = new Vector3(Center.x + Mathf.Sin(a) * it.dist, it.y - 38f, Center.z + Mathf.Cos(a) * it.dist);
            o.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f); o.transform.localScale = Vector3.one * it.scale;
            o.GetComponent<MeshFilter>().sharedMesh = m; var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }
        foreach (var a in Object.FindObjectsOfType<FuwaAreaVisibility>(true))
        {
            var list = new List<GameObject>(a.stage2Objects ?? new GameObject[0]);
            if (!list.Contains(root)) { list.Add(root); a.stage2Objects = list.ToArray(); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(a); EditorUtility.SetDirty(a); }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[FuwaStage2Far] 島 " + n + " 個");
    }

    // ステージ2の空の濃さ（ためしに 2026-11-02 の 1 番）。normal は今の空のマテリアルの色
    public static Color Stage2Top = new Color(0.04f, 0.26f, 0.80f), Stage2Horizon = new Color(0.36f, 0.68f, 1f), Stage2Bottom = new Color(0.20f, 0.48f, 0.90f);
    [MenuItem("FuwaCourse/ステージ2/ステージ2の空を設定")]
    public static string BuildSky()
    {
        const string ScriptPath = "Assets/FuwaStage2/Scripts/FuwaStageSky.cs", ProgramPath = "Assets/FuwaStage2/Scripts/FuwaStageSky.asset";
        if (AssetDatabase.LoadAssetAtPath<UdonSharp.UdonSharpProgramAsset>(ProgramPath) == null) return "プログラムアセットが先に要る";
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var sys = GameObject.Find("FuwaSystem"); var old = sys.transform.Find("StageSky"); if (old) Object.DestroyImmediate(old.gameObject);
        var o = new GameObject("StageSky"); o.transform.SetParent(sys.transform, false);
        var s = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaStageSky>(o);
        var sky = RenderSettings.skybox; s.sky = sky; s.area = Object.FindObjectOfType<FuwaAreaVisibility>(true);
        s.normalTop = sky.GetColor("_TopColor"); s.normalHorizon = sky.GetColor("_HorizonColor"); s.normalBottom = sky.GetColor("_BottomColor"); s.normalFog = RenderSettings.fogColor;
        s.stage2Top = Stage2Top; s.stage2Horizon = Stage2Horizon; s.stage2Bottom = Stage2Bottom; s.stage2Fog = Stage2Horizon;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(s); FuwaStage2WaterTest.NoSync(s);
        // 遠くの雲と島のかすみも濃い空に合わせる（どちらもステージ2でしか見えない）
        foreach (var path in new[] { MatPath, IslandMat }) { var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (m) { m.SetColor("_Haze", Stage2Horizon); EditorUtility.SetDirty(m); } }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return "空 OK area=" + (s.area != null);
    }

    [MenuItem("FuwaCourse/ステージ2/遠景の雲海を作り直す")]
    public static void BuildCloudSea()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        GameObject root = null; foreach (var g in scene.GetRootGameObjects()) if (g.name == "Stage2Far") root = g;
        if (!root) { root = new GameObject("Stage2Far"); }
        var old = root.transform.Find("CloudSea"); if (old) Object.DestroyImmediate(old.gameObject);
        var o = new GameObject("CloudSea", typeof(MeshFilter), typeof(MeshRenderer)); o.transform.SetParent(root.transform, false); o.transform.position = Center;
        o.GetComponent<MeshFilter>().sharedMesh = BuildMesh();
        var r = o.GetComponent<MeshRenderer>(); r.sharedMaterial = Mat(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;

        // ステージ2にいる間だけ見える
        foreach (var a in Object.FindObjectsOfType<FuwaAreaVisibility>(true))
        {
            var list = new List<GameObject>(a.stage2Objects ?? new GameObject[0]);
            if (!list.Contains(root)) { list.Add(root); a.stage2Objects = list.ToArray(); UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(a); EditorUtility.SetDirty(a); }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[FuwaStage2Far] 雲海 三角形 " + (o.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3));
    }
}
