using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// コース4「まもののす」の小枝の巣・橋を Tools/den.json（Blender の Tools/den.py が書き出す）から取り込む（2026-10-07）。
// den.py の座標は FuwaCourse4/Den のローカル（コースを動かす前のワールド座標のまま）なので、Den の子にそのまま入れる。
// 見た目 Generated/Den/<名前>.asset（頂点カラー、Den_Twig）、当たり判定 Generated/Den/<名前>_Col.asset。
// 巣（Nest/Pod/Islet）の当たり判定は、ふちの壁を低く削ってふちの上に足場を足した Meshes/<名前>_Col_Low.asset を使う
// （ジャンプで出入りできる高さ。前に手作業で作った処理をここにまとめた）。橋はそのまま。
public static class FuwaDenImport
{
    const string JsonPath = "Tools/den.json";
    const string GenDir = "Assets/FuwaCourse/Generated/Den/";
    const float RimTopY = 5.82f, RimBotY = 5.3f;   // ふちの上の足場の高さ（床 5.5 + 32cm）

    [MenuItem("FuwaCourse/まもののすの巣と橋を取り込む")]
    public static void Menu() { Debug.Log(Run()); }

    class M { public List<Vector3> v = new List<Vector3>(); public List<int> t = new List<int>(); public List<Color> c; }

    static int Close(string s, int open)
    {
        int depth = 0;
        for (int i = open; i < s.Length; i++)
        {
            if (s[i] == '[') depth++;
            else if (s[i] == ']' && --depth == 0) return i;
        }
        return -1;
    }

    static List<float> Nums(string s)
    {
        var r = new List<float>();
        foreach (var tok in s.Split(new[] { '[', ']', ',', ' ', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries))
            r.Add(float.Parse(tok, CultureInfo.InvariantCulture));
        return r;
    }

    static Dictionary<string, M> Parse(string json)
    {
        var d = new Dictionary<string, M>();
        int i = 0;
        while (true)
        {
            int q = json.IndexOf("\": {", i);
            if (q < 0) break;
            int qs = json.LastIndexOf('"', q - 1);
            string name = json.Substring(qs + 1, q - qs - 1);
            var m = new M();
            int end = json.IndexOf('}', q);
            foreach (var key in new[] { "v", "t", "c" })
            {
                int k = json.IndexOf("\"" + key + "\": [", q);
                if (k < 0 || k > end) continue;
                int a = json.IndexOf('[', k), b = Close(json, a);
                var nums = Nums(json.Substring(a, b - a + 1));
                if (key == "v") for (int n = 0; n + 2 < nums.Count; n += 3) m.v.Add(new Vector3(nums[n], nums[n + 1], nums[n + 2]));
                else if (key == "t") foreach (var x in nums) m.t.Add((int)x);
                else { m.c = new List<Color>(); for (int n = 0; n + 2 < nums.Count; n += 3) m.c.Add(new Color(nums[n], nums[n + 1], nums[n + 2], 1f)); }
            }
            d[name] = m;
            i = end + 1;
        }
        return d;
    }

    static Mesh SaveMesh(string path, string name, M m)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool fresh = mesh == null;
        if (fresh) mesh = new Mesh();
        mesh.Clear();
        mesh.name = name;
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(m.v);
        if (m.c != null) mesh.SetColors(m.c);
        mesh.SetTriangles(m.t, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if (fresh) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    public static string Run()
    {
        var root = GameObject.Find("FuwaCourse4").transform;
        var den = root.Find("Den");
        var data = Parse(System.IO.File.ReadAllText(JsonPath));
        var mat = AssetDatabase.LoadAssetAtPath<Material>(GenDir + "Den_Twig.mat");
        string log = "";
        foreach (var kv in data)
        {
            string name = kv.Key;
            bool isCol = name.EndsWith("_Col");
            var mesh = SaveMesh(GenDir + name + ".asset", name, kv.Value);
            var t = den.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(den, false);
                Undo.RegisterCreatedObjectUndo(go, "den");
                t = go.transform;
                log += "+" + name + " ";
            }
            t.gameObject.layer = 11;
            if (isCol)
            {
                var mc = t.GetComponent<MeshCollider>(); if (mc == null) mc = t.gameObject.AddComponent<MeshCollider>();
                string baseName = name.Substring(0, name.Length - 4);
                var use = baseName.StartsWith("Bridge") ? mesh : BuildLowCollider(baseName, mesh);
                mc.sharedMesh = null; mc.sharedMesh = use;
            }
            else
            {
                var mf = t.GetComponent<MeshFilter>(); if (mf == null) mf = t.gameObject.AddComponent<MeshFilter>();
                var mr = t.GetComponent<MeshRenderer>(); if (mr == null) mr = t.gameObject.AddComponent<MeshRenderer>();
                mf.sharedMesh = mesh;
                if (mr.sharedMaterial == null) mr.sharedMaterial = mat;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)~0);
            }
        }
        // den.json に無くなった巣・橋は消す（MamaNestCenter などは残す）
        foreach (Transform t in den.Cast<Transform>().ToList())
        {
            bool generated = t.name.StartsWith("Nest") || t.name.StartsWith("Pod") || t.name.StartsWith("Bridge") || t.name.StartsWith("MamaNest") && t.name != "MamaNestCenter" || t.name.StartsWith("SideNest") || t.name.StartsWith("Islet");
            if (generated && !data.ContainsKey(t.name)) { log += "-" + t.name + " "; Undo.DestroyObjectImmediate(t.gameObject); }
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "den import: " + data.Count + " meshes " + log;
    }

    // 巣の当たり判定：ふちの壁を RimTopY で平らに削り、壁の上に幅0.4mの足場を足す（ジャンプで出入りでき、ふちの上も歩ける）
    static Mesh BuildLowCollider(string n, Mesh src)
    {
        int N = 64;
        var wv = src.vertices.ToList();
        var c = wv.Aggregate(Vector3.zero, (a, b) => a + b) / wv.Count;
        var sum = new float[N]; var cnt = new int[N];
        foreach (var p in wv)
        {
            if (p.y <= 5.62f) continue;
            var d = new Vector2(p.x - c.x, p.z - c.z);
            int s = (int)((Mathf.Atan2(d.y, d.x) + Mathf.PI) / (Mathf.PI * 2) * N) % N;
            sum[s] += d.magnitude; cnt[s]++;
        }
        var rad = new float[N]; var closed = new bool[N];
        for (int i = 0; i < N; i++) { closed[i] = cnt[i] > 0; rad[i] = closed[i] ? sum[i] / cnt[i] : -1; }
        // 3区間以下の短い抜けは頂点の間隔のせいなので埋める（本当の切れ目は長く続く）
        var orig = (bool[])closed.Clone();
        for (int i = 0; i < N; i++)
        {
            if (orig[i]) continue;
            int a = i; while (!orig[((a - 1) % N + N) % N] && (i - a) < N) a--;
            int b = i; while (!orig[(b + 1) % N] && (b - i) < N) b++;
            int len = b - a + 1;
            if (len <= 3)
            {
                int pa = ((a - 1) % N + N) % N, pb = (b + 1) % N;
                float tt = (i - a + 1f) / (len + 1f);
                closed[i] = true; rad[i] = Mathf.Lerp(rad[pa], rad[pb], tt);
            }
        }
        var V = src.vertices.ToList();
        for (int i = 0; i < V.Count; i++) if (V[i].y > RimTopY) V[i] = new Vector3(V[i].x, RimTopY, V[i].z);
        var T = src.triangles.ToList(); int baseTri = T.Count; float w = 0.4f;
        for (int i = 0; i < N; i++)
        {
            int j = (i + 1) % N; if (!closed[i] || !closed[j]) continue;
            float a0 = (i + 0.5f) * Mathf.PI * 2 / N - Mathf.PI, a1 = (j + 0.5f) * Mathf.PI * 2 / N - Mathf.PI; if (j == 0) a1 += Mathf.PI * 2;
            var d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
            Vector3 P(Vector3 d, float r, float y) => new Vector3(c.x, y, c.z) + d * r;
            int b = V.Count;
            V.Add(P(d0, rad[i] - 0.05f, RimTopY)); V.Add(P(d1, rad[j] - 0.05f, RimTopY)); V.Add(P(d1, rad[j] + w, RimTopY)); V.Add(P(d0, rad[i] + w, RimTopY));
            V.Add(P(d0, rad[i] + w, RimBotY)); V.Add(P(d1, rad[j] + w, RimBotY));
            T.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 }); T.AddRange(new[] { b + 3, b + 5, b + 2, b + 3, b + 4, b + 5 });
        }
        var tri = T.ToArray();
        for (int k = baseTri; k < tri.Length; k += 3)
        {
            var nW = Vector3.Cross(V[tri[k + 1]] - V[tri[k]], V[tri[k + 2]] - V[tri[k]]);
            var cen = (V[tri[k]] + V[tri[k + 1]] + V[tri[k + 2]]) / 3f;
            bool topFace = Mathf.Abs(nW.normalized.y) > 0.5f;
            var want = topFace ? Vector3.up : new Vector3(cen.x - c.x, 0, cen.z - c.z);
            if (Vector3.Dot(nW, want) < 0) { var t = tri[k + 1]; tri[k + 1] = tri[k + 2]; tri[k + 2] = t; }
        }
        string path = "Assets/FuwaCourse/Meshes/" + n + "_Col_Low.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool fresh = mesh == null;
        if (fresh) mesh = new Mesh();
        mesh.Clear(); mesh.name = n + "_Col_Low";
        mesh.SetVertices(V); mesh.triangles = tri; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if (fresh) AssetDatabase.CreateAsset(mesh, path); else EditorUtility.SetDirty(mesh);
        return mesh;
    }
}
