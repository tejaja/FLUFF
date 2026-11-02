using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ステージ2の遠景（2026-11-02 てじゃ「ステージ2に遠景のオブジェクトがなかった」「島と雲」「ステージ2は変えたい」「雲海いいね」）。
// 空はステージ1と同じ青。雲はステージ1のもこもこの塊と変えて、コースの下に広がる雲海にする。ステージ2の間だけ見える（FuwaAreaVisibility.stage2Objects）。
public static class FuwaStage2Far
{
    const string MeshPath = "Assets/FuwaStage2/Generated/Stage2_CloudSea.asset";
    const string MatPath = "Assets/FuwaStage2/Materials/Stage2_CloudSea.mat";
    public static Vector3 Center = new Vector3(25f, -205f, 50f);
    public const float Radius = 1100f;
    const int Rings = 56, Seg = 94;

    // 大きいもこもこ＋小さいもこもこ。セルごとに置いた丸の高さの最大（なめらかな丘が並ぶ形）
    static float Bumps(float x, float z, float cell, int seed)
    {
        int cx = Mathf.FloorToInt(x / cell), cz = Mathf.FloorToInt(z / cell); float best = 0f;
        for (int i = cx - 1; i <= cx + 1; i++) for (int j = cz - 1; j <= cz + 1; j++)
        {
            var rnd = new System.Random(i * 73856093 ^ j * 19349663 ^ seed);
            float px = (i + (float)rnd.NextDouble()) * cell, pz = (j + (float)rnd.NextDouble()) * cell, r = cell * (0.55f + 0.5f * (float)rnd.NextDouble()), h = 0.55f + 0.45f * (float)rnd.NextDouble();
            float d = Mathf.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz)) / r;
            if (d < 1f) best = Mathf.Max(best, h * Mathf.Sqrt(1f - d * d));
        }
        return best;
    }
    static float Height(float x, float z)
    {
        // ゆるい大きなうねり＋もこもこ
        float swell = (Mathf.PerlinNoise(x * 0.004f + 11f, z * 0.004f + 7f) - 0.5f) * 26f;
        return swell + Bumps(x, z, 70f, 11) * 34f + Bumps(x, z, 26f, 29) * 12f;
    }

    public static Mesh BuildMesh()
    {
        var verts = new List<Vector3>(); var tris = new List<int>();
        verts.Add(new Vector3(0f, Height(Center.x, Center.z), 0f));
        for (int r = 1; r <= Rings; r++)
        {
            float k = r / (float)Rings, rad = Radius * (k * k * 0.82f + k * 0.18f);   // 近くは細かく、遠くは粗く
            float edge = Mathf.Clamp01((Radius - rad) / 220f);
            for (int s = 0; s < Seg; s++)
            {
                float a = s * Mathf.PI * 2f / Seg, x = Mathf.Cos(a) * rad, z = Mathf.Sin(a) * rad;
                verts.Add(new Vector3(x, Height(Center.x + x, Center.z + z) * edge - (1f - edge) * 30f, z));
            }
        }
        for (int s = 0; s < Seg; s++) { int a = 1 + s, b = 1 + (s + 1) % Seg; tris.AddRange(new[] { 0, b, a }); }
        for (int r = 0; r < Rings - 1; r++) for (int s = 0; s < Seg; s++)
        {
            int a = 1 + r * Seg + s, b = 1 + r * Seg + (s + 1) % Seg, c = a + Seg, d = b + Seg;
            tris.AddRange(new[] { a, b, c, b, d, c });
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
        if (!m) { m = new Material(Shader.Find("FuwaCourse/CloudPuff")); AssetDatabase.CreateAsset(m, MatPath); }
        m.SetColor("_Color", Color.white); m.SetColor("_Shade", new Color(0.55f, 0.72f, 0.95f)); m.SetFloat("_Rim", 0.12f); m.SetFloat("_ShadeContrast", 5f);
        m.SetFloat("_Drift", 0f); m.SetFloat("_Bob", 0f);   // 1 枚のメッシュなので動かさない
        m.SetColor("_Haze", new Color(0.6f, 0.83f, 1f)); m.SetFloat("_HazeStart", 180f); m.SetFloat("_HazeEnd", 1000f); m.SetFloat("_HazeMax", 0.9f); m.SetFloat("_FogMax", 0f);
        EditorUtility.SetDirty(m);
        return m;
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
