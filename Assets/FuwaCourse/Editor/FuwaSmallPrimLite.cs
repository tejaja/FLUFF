using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 小さい「Unity 標準の球・円柱」を軽い形に差しかえる（2026-11-22 てじゃ「2-4の歯車とかの軽量化」）。
// 標準の球は 768 三角形・円柱は 80 三角形で、2–4 のびょう（直径 7cm くらい）96 個だけで 7.4 万あった。
//  ・球：直径 0.12m 未満 → 8×5（64）、0.6m 未満 → 12×8（168）。それ以上はそのまま
//  ・円柱：直径 0.12m 未満 → 8 角（28）、0.6m 未満 → 12 角（44）。それ以上はそのまま（標準は 20 角）
// 形の大きさ・向きは標準と同じ（球は半径 0.5、円柱は半径 0.5・高さ 2）。円柱の側面と底はかどを立てる（標準と同じ）。
// マテリアルまとめ（VC_ の写し）は元のメッシュから作るので、いったん戻してから差しかえて、まとめ直す。
// コースをビルダーで作り直したら、これを押し直す（そのあと「色ちがいの材質をまとめる」もこの中でやる）
public static class FuwaSmallPrimLite
{
    const string Dir = "Assets/FuwaCourse/Generated/LowPrim";

    [MenuItem("FuwaCourse/よく使う/小さい球・円柱を軽い形にする（作り直し後に）")]
    static void Menu() { Debug.Log(Run(false)); }
    [MenuItem("FuwaCourse/よく使う/小さい球・円柱を軽い形にする（数えるだけ）")]
    static void MenuDry() { Debug.Log(Run(true)); }

    public static string Run(bool dry)
    {
        if (!dry) FuwaMaterialMerge.Revert();
        var meshes = new Dictionary<string, Mesh>();
        Mesh Get(string name, System.Func<Mesh> make)
        {
            if (meshes.TryGetValue(name, out var m)) return m;
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "LowPrim");
            string p = Dir + "/" + name + ".asset";
            m = AssetDatabase.LoadAssetAtPath<Mesh>(p);
            var built = make();
            if (m == null) { m = built; m.name = name; AssetDatabase.CreateAsset(m, p); }
            else { m.Clear(); m.SetVertices(built.vertices); m.SetNormals(built.normals); m.SetUVs(0, built.uv); m.SetTriangles(built.triangles, 0); m.RecalculateBounds(); m.RecalculateTangents(); EditorUtility.SetDirty(m); Object.DestroyImmediate(built); }
            meshes[name] = m; return m;
        }
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        int nS = 0, nC = 0; long saved = 0;
        foreach (var r in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
        {
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
            var src = mf.sharedMesh;
            if (AssetDatabase.GetAssetPath(src) != "Library/unity default resources") continue;
            var mc = r.GetComponent<MeshCollider>(); if (mc && mc.sharedMesh == src) continue;
            // 親の伸び（ゆがみ）込みの実際の大きさ：各軸の向きを世界に移した長さ（lossyScale はゆがみで小さく出る。2–4 のびょうは 0.05 と出て実際は 0.3m）
            var M = r.transform.localToWorldMatrix;
            var s = new Vector3(M.MultiplyVector(Vector3.right).magnitude, M.MultiplyVector(Vector3.up).magnitude, M.MultiplyVector(Vector3.forward).magnitude);
            Mesh to = null;
            if (src.name == "Sphere")
            {
                float d = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                if (d < 0.12f) to = Get("LowSphere_8x5", () => Sphere(8, 5));
                else if (d < 0.6f) to = Get("LowSphere_12x8", () => Sphere(12, 8));
                if (to) nS++;
            }
            else if (src.name == "Cylinder")
            {
                float d = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
                if (d < 0.12f) to = Get("LowCylinder_8", () => Cylinder(8));
                else if (d < 0.6f) to = Get("LowCylinder_12", () => Cylinder(12));
                if (to) nC++;
            }
            if (!to) continue;
            saved += src.triangles.Length / 3 - to.triangles.Length / 3;
            if (dry) continue;
            Undo.RecordObject(mf, "小さい球・円柱を軽く");
            mf.sharedMesh = to;
        }
        string res = $"[小さい球・円柱] 球 {nS}・円柱 {nC} を軽い形に（-{saved / 1000}k 三角形）";
        if (!dry)
        {
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            res += "\n" + FuwaMaterialMerge.Run(false);
        }
        return res;
    }

    static Mesh Sphere(int seg, int rings)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int i = 0; i <= rings; i++)
        {
            float phi = Mathf.PI * i / rings;
            for (int a = 0; a <= seg; a++)
            {
                float th = 2f * Mathf.PI * a / seg;
                var d = new Vector3(Mathf.Sin(phi) * Mathf.Cos(th), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(th));
                v.Add(d * 0.5f); n.Add(d); uv.Add(new Vector2((float)a / seg, 1f - (float)i / rings));
            }
        }
        int W = seg + 1;
        for (int i = 0; i < rings; i++)
            for (int a = 0; a < seg; a++)
            {
                int p0 = i * W + a, p1 = p0 + 1, q0 = p0 + W, q1 = q0 + 1;
                if (i > 0) { t.Add(p0); t.Add(p1); t.Add(q0); }
                if (i < rings - 1) { t.Add(p1); t.Add(q1); t.Add(q0); }
            }
        return Finish(v, n, uv, t);
    }

    static Mesh Cylinder(int seg)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int a = 0; a <= seg; a++)
        {
            float th = 2f * Mathf.PI * a / seg; var d = new Vector3(Mathf.Cos(th), 0f, Mathf.Sin(th));
            v.Add(d * 0.5f + Vector3.up); n.Add(d); uv.Add(new Vector2((float)a / seg, 1f));
            v.Add(d * 0.5f - Vector3.up); n.Add(d); uv.Add(new Vector2((float)a / seg, 0f));
        }
        for (int a = 0; a < seg; a++) { int p = a * 2; t.Add(p); t.Add(p + 2); t.Add(p + 1); t.Add(p + 2); t.Add(p + 3); t.Add(p + 1); }
        foreach (float y in new[] { 1f, -1f })
        {
            int b = v.Count;
            for (int a = 0; a < seg; a++) { float th = 2f * Mathf.PI * a / seg; v.Add(new Vector3(Mathf.Cos(th) * 0.5f, y, Mathf.Sin(th) * 0.5f)); n.Add(Vector3.up * y); uv.Add(new Vector2(Mathf.Cos(th) * 0.5f + 0.5f, Mathf.Sin(th) * 0.5f + 0.5f)); }
            for (int a = 1; a < seg - 1; a++) { if (y > 0f) { t.Add(b); t.Add(b + a + 1); t.Add(b + a); } else { t.Add(b); t.Add(b + a); t.Add(b + a + 1); } }
        }
        return Finish(v, n, uv, t);
    }

    // 面の向きを外向きにそろえる（Unity は Cross(b-a, c-a) が表）
    static Mesh Finish(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
    {
        for (int i = 0; i < t.Count; i += 3)
        {
            var c = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
            if (Vector3.Dot(c, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) < 0f) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
        }
        var m = new Mesh(); m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }
}
