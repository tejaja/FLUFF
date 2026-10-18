using UnityEditor;
using UnityEngine;

// あそびばの雲カーペットのふちに、ほんのり青い影のグラデをつける（2026-10-14 てじゃ「縁にわずかにちょっと青っぽい影グラデ」）
// ・元のメッシュ（PlaygroundProps.fbx の Chill_Rug）には UV がないので、上から見たXZをUVにしたコピーを作る
// ・ふちからの距離でグラデの模様（_MainTex。白＝そのまま、ふちほど青）を作って、ラグの材質に貼る
// FuwaPlaygroundBuilder.Decorate から呼ぶ。何度実行しても同じ結果
public static class FuwaPlaygroundRugEdge
{
    const string MeshPath = "Assets/FuwaCourse/Generated/Playground_RugEdge.asset";
    const string TexPath = "Assets/FuwaCourse/Generated/Playground_RugEdge.png";
    const int Res = 512;
    const float Width = 0.42f;                                  // グラデの幅（メッシュの座標で。ラグは0.8倍なので実際は約0.34m。0.55→0.42 てじゃ「ちょっとだけ狭めて」）
    static readonly Color Edge = new Color(0.8f, 0.87f, 1f);    // いちばんふちの色（_Color に掛かる）

    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/雲カーペットのふちのグラデを作り直す")]
    public static void Menu()
    {
        var pg = System.Array.Find(Object.FindObjectsOfType<Transform>(true), t => t.name == "FuwaPlayground" && t.parent == null);
        var rug = pg.Find("Room/Deco/Rug");
        Apply(rug.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rug.gameObject.scene);
        Debug.Log("rug edge done");
    }

    public static void Apply(GameObject rug)
    {
        var src = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaCourse/Models/PlaygroundProps.fbx");
        Mesh orig = null;
        foreach (var o in src) if (o is Mesh m && m.name == "Chill_Rug") orig = m;
        var b = orig.bounds;
        float size = Mathf.Max(b.size.x, b.size.z) + Width * 2f;   // まわりに少し余白
        Vector2 o2 = new Vector2(b.center.x - size / 2f, b.center.z - size / 2f);

        // メッシュのコピー＋UV（上から見たXZ）
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        else mesh.Clear();
        mesh.name = "Playground_RugEdge";
        mesh.indexFormat = orig.indexFormat;
        var v = orig.vertices;
        mesh.vertices = v; mesh.normals = orig.normals;
        var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++) uv[i] = new Vector2((v[i].x - o2.x) / size, (v[i].z - o2.y) / size);
        mesh.uv = uv;
        mesh.subMeshCount = orig.subMeshCount;
        for (int s = 0; s < orig.subMeshCount; s++) mesh.SetTriangles(orig.GetTriangles(s), s);
        mesh.RecalculateBounds();
        if (isNew) AssetDatabase.CreateAsset(mesh, MeshPath);
        EditorUtility.SetDirty(mesh);

        // 上から見た形（上向きの面）を塗って、ふちからの距離を出す
        var inside = new bool[Res * Res];
        var tris = orig.triangles; var n = orig.normals;
        for (int t = 0; t < tris.Length; t += 3)
        {
            int a = tris[t], c = tris[t + 1], d = tris[t + 2];
            if (n[a].y < 0.3f && n[c].y < 0.3f && n[d].y < 0.3f) continue;
            Vector2 pa = uv[a] * Res, pb = uv[c] * Res, pc = uv[d] * Res;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, Mathf.Min(pb.x, pc.x)))), x1 = Mathf.Min(Res - 1, Mathf.CeilToInt(Mathf.Max(pa.x, Mathf.Max(pb.x, pc.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, Mathf.Min(pb.y, pc.y)))), y1 = Mathf.Min(Res - 1, Mathf.CeilToInt(Mathf.Max(pa.y, Mathf.Max(pb.y, pc.y))));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = Cross(pb - pa, p - pa), w1 = Cross(pc - pb, p - pb), w2 = Cross(pa - pc, p - pc);
                    if ((w0 >= 0 && w1 >= 0 && w2 >= 0) || (w0 <= 0 && w1 <= 0 && w2 <= 0)) inside[y * Res + x] = true;
                }
        }
        // 外からの距離（2パスのチャンファー距離）
        var dist = new float[Res * Res];
        const float Big = 1e6f, D1 = 1f, D2 = 1.41421f;
        for (int i = 0; i < dist.Length; i++) dist[i] = inside[i] ? Big : 0f;
        for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++)
            {
                int i = y * Res + x; float d = dist[i]; if (d == 0f) continue;
                if (x > 0) d = Mathf.Min(d, dist[i - 1] + D1);
                if (y > 0) d = Mathf.Min(d, dist[i - Res] + D1);
                if (x > 0 && y > 0) d = Mathf.Min(d, dist[i - Res - 1] + D2);
                if (x < Res - 1 && y > 0) d = Mathf.Min(d, dist[i - Res + 1] + D2);
                dist[i] = d;
            }
        for (int y = Res - 1; y >= 0; y--)
            for (int x = Res - 1; x >= 0; x--)
            {
                int i = y * Res + x; float d = dist[i]; if (d == 0f) continue;
                if (x < Res - 1) d = Mathf.Min(d, dist[i + 1] + D1);
                if (y < Res - 1) d = Mathf.Min(d, dist[i + Res] + D1);
                if (x < Res - 1 && y < Res - 1) d = Mathf.Min(d, dist[i + Res + 1] + D2);
                if (x > 0 && y < Res - 1) d = Mathf.Min(d, dist[i + Res - 1] + D2);
                dist[i] = d;
            }
        float px = size / Res;   // 1ピクセルの長さ
        var tex = new Texture2D(Res, Res, TextureFormat.RGB24, false);
        var cols = new Color[Res * Res];
        for (int i = 0; i < cols.Length; i++)
        {
            float k = Mathf.Clamp01(dist[i] * px / Width);
            k = k * k * (3f - 2f * k);   // なめらかに
            cols[i] = Color.Lerp(Edge, Color.white, k);
        }
        tex.SetPixels(cols); tex.Apply();
        System.IO.File.WriteAllBytes(TexPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(TexPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(TexPath);
        ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.sRGBTexture = true; ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.maxTextureSize = Res;
        ti.SaveAndReimport();

        var mr = rug.GetComponent<MeshRenderer>();
        var mat = mr.sharedMaterial;
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath));
        mat.SetTextureScale("_MainTex", Vector2.one); mat.SetTextureOffset("_MainTex", Vector2.zero);
        EditorUtility.SetDirty(mat);
        rug.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mc = rug.GetComponent<MeshCollider>(); if (mc != null) mc.sharedMesh = mesh;
        AssetDatabase.SaveAssets();
    }

    static float Cross(Vector2 a, Vector2 b) { return a.x * b.y - a.y * b.x; }
}
