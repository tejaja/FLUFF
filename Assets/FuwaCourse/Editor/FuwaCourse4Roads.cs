using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// まもののすの最初と最後の木の橋を、ほかのコースと同じシマシマ道（巣の小枝っぽい茶色）に置きかえる（2026-10-09 てじゃ
// 「道と道で挟めば、道の途中にできた巣っぽい」）。見た目だけ差しかえ：当たり判定は橋のもの（BridgeA_Col / BridgeD_Col）をそのまま使う。
// 道の上面は橋の上面と同じ高さ。巣の側は巣の床が始まる所まで、スタート島の側は島の下にもぐらせる、ゴールの側は島のふちまで。
// 何度実行してもよい（道は作り直し、橋の見た目は非表示にするだけで消さない）
public static class FuwaCourse4Roads
{
    const float Thick = 0.3f, Bevel = 0.08f, Stripe = 0.6f;
    const int BevelSteps = 4;
    const string MeshDir = "Assets/FuwaCourse/Meshes/Course4";

    [MenuItem("FuwaCourse/まもののす/最初と最後の橋をシマシマ道にする")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var c4 = GameObject.Find("FuwaCourse4").transform;
        var den = c4.Find("Den");
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "Course4");
        var matA = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/LobbyCourse4_PathA.mat");
        var matB = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/LobbyCourse4_PathB.mat");
        var log = new System.Text.StringBuilder();
        var startG = c4.Find("Start/StartArea/Ground"); var goalG = c4.Find("Goal/GoalGround");
        // (橋, 始まりの島, 終わりの島, 始まり側のはしの決め方, 終わり側)
        log.Append(Road(den, "BridgeA", startG.position, den.Find("Nest1").GetComponent<Renderer>().bounds.center, null, den.Find("Nest1_Col").GetComponent<Collider>(), matA, matB, "Course4_Road_Start"));
        log.Append(Road(den, "BridgeD", den.Find("MamaNestCenter").position, goalG.position, den.Find("MamaNest_Col").GetComponent<Collider>(), goalG.GetComponent<Collider>(), matA, matB, "Course4_Road_Goal"));
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c4.gameObject.scene);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // a→b の向きに、橋の当たり判定の上面（幅・高さ）を測って道を作る。
    // fromCol があれば「a 側の島（巣）の床が終わる所」から、なければ橋のはしから（島の下にもぐる）。toCol も同じ
    static string Road(Transform den, string bridge, Vector3 a3, Vector3 b3, Collider fromCol, Collider toCol, Material mA, Material mB, string name)
    {
        var br = den.Find(bridge); var bcol = den.Find(bridge + "_Col").GetComponent<Collider>();
        Vector2 a = new Vector2(a3.x, a3.z), b = new Vector2(b3.x, b3.z), dir = (b - a).normalized, right = new Vector2(dir.y, -dir.x);
        float L = (b - a).magnitude;
        bool Hit(Collider c, Vector2 p, out float y) { y = 0f; if (c.Raycast(new Ray(new Vector3(p.x, 50f, p.y), Vector3.down), out var h, 100f)) { y = h.point.y; return true; } return false; }
        // 橋の範囲・幅・上面
        float s0 = 99f, s1 = -99f, xl = 99f, xr = -99f, top = -99f;
        for (float s = 0f; s <= L; s += 0.05f)
            for (float x = -3f; x <= 3f; x += 0.05f)
                if (Hit(bcol, a + dir * s + right * x, out var y)) { s0 = Mathf.Min(s0, s); s1 = Mathf.Max(s1, s); xl = Mathf.Min(xl, x); xr = Mathf.Max(xr, x); top = Mathf.Max(top, y); }
        // はし：島（巣）の床が終わる所。なければ橋のはし
        float e0 = s0, e1 = s1;
        if (fromCol != null) { for (float s = 0f; s <= L; s += 0.02f) if (!Hit(fromCol, a + dir * s, out _)) { e0 = s; break; } }
        if (toCol != null) { for (float s = L; s >= 0f; s -= 0.02f) if (!Hit(toCol, a + dir * s, out _)) { e1 = s + 0.02f; break; } }
        float halfW = (xr - xl) * 0.5f, cx = (xr + xl) * 0.5f;
        var mesh = Strip(e0, e1, halfW, top);
        mesh.name = name;
        string path = MeshDir + "/" + name + ".asset";
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (ex == null) AssetDatabase.CreateAsset(mesh, path); else { ex.Clear(); EditorUtility.CopySerialized(mesh, ex); ex.name = name; mesh = ex; }
        var old = den.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(den, false);
        var o = a + right * cx;
        go.transform.SetPositionAndRotation(new Vector3(o.x, 0f, o.y), Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up));
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { mA, mB };
        go.layer = br.gameObject.layer;
        GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(br.gameObject));
        br.gameObject.SetActive(false);   // 木の橋の見た目は消さずに非表示（当たり判定 _Col はそのまま）
        return bridge + "→" + name + " " + e0.ToString("F2") + "〜" + e1.ToString("F2") + "m 幅" + (halfW * 2f).ToString("F2") + " 上面" + top.ToString("F2") + " / ";
    }

    // ローカル：z が道の向き（z0〜z1）、x が横。上面 top、厚み Thick、上のふちを丸める。しまは Stripe ごと
    static Mesh Strip(float z0, float z1, float halfW, float top)
    {
        float bot = top - Thick;
        var prof = new List<Vector4> { new Vector4(-halfW, bot, -1, 0) };
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(-halfW + Bevel + Bevel * Mathf.Cos(a), top - Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI / 2 - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(halfW - Bevel + Bevel * Mathf.Cos(a), top - Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        prof.Add(new Vector4(halfW, bot, 1, 0));
        int P = prof.Count;
        var cuts = new List<float> { z0 };
        for (float z = Mathf.Ceil(z0 / Stripe) * Stripe; z < z1 - 1e-3f; z += Stripe) if (z > z0 + 1e-3f) cuts.Add(z);
        cuts.Add(z1);
        var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>(); var A = new List<int>(); var B = new List<int>();
        for (int s = 0; s < cuts.Count - 1; s++)
        {
            var T = (Mathf.FloorToInt((cuts[s] + cuts[s + 1]) * 0.5f / Stripe) % 2 == 0) ? A : B;
            int b0 = V.Count;
            foreach (float z in new[] { cuts[s], cuts[s + 1] })
                foreach (var q in prof) { V.Add(new Vector3(q.x, q.y, z)); N.Add(new Vector3(q.z, q.w, 0)); UV.Add(new Vector2(q.x, z)); }
            for (int i = 0; i < P - 1; i++) { int x0 = b0 + i, x1 = b0 + i + 1, y0 = b0 + P + i, y1 = b0 + P + i + 1; T.AddRange(new[] { x0, y0, x1, x1, y0, y1 }); }
            int bb = V.Count;   // 底
            foreach (float z in new[] { cuts[s], cuts[s + 1] }) { V.Add(new Vector3(-halfW, bot, z)); V.Add(new Vector3(halfW, bot, z)); N.Add(Vector3.down); N.Add(Vector3.down); UV.Add(Vector2.zero); UV.Add(Vector2.zero); }
            A.AddRange(new[] { bb, bb + 1, bb + 2, bb + 1, bb + 3, bb + 2 });
        }
        foreach (var (z, sgn) in new[] { (z0, -1f), (z1, 1f) })
        {
            var T = Mathf.FloorToInt((z - sgn * 0.01f) / Stripe) % 2 == 0 ? A : B;
            int cb = V.Count;
            foreach (var q in prof) { V.Add(new Vector3(q.x, q.y, z)); N.Add(new Vector3(0, 0, sgn)); UV.Add(Vector2.zero); }
            for (int i = 1; i < P - 1; i++) T.AddRange(new[] { cb, cb + i, cb + i + 1 });
        }
        foreach (var T in new[] { A, B })
            for (int i = 0; i < T.Count; i += 3)
            {
                Vector3 p0 = V[T[i]], p1 = V[T[i + 1]], p2 = V[T[i + 2]];
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), N[T[i]] + N[T[i + 1]] + N[T[i + 2]]) < 0f) { int t = T[i + 1]; T[i + 1] = T[i + 2]; T[i + 2] = t; }
            }
        var m = new Mesh();
        m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV);
        m.subMeshCount = 2; m.SetTriangles(A, 0); m.SetTriangles(B, 1);
        m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }
}
