using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 浮島の緑の円盤（IslandTop）：上のふちを丸めた円盤メッシュ。半径ごとに Meshes/IslandDisc/ に保存して使い回す。
// 厚みは元の Cylinder×(…, 0.03, …) と同じ 6cm（中心から上下 3cm）。スケールは 1 のまま使う。
public static class FuwaIslandDisc
{
    public const float HalfThick = 0.03f, TopBevel = 0.025f, BottomBevel = 0.008f;
    const int BevelSteps = 4;
    const string Dir = "Assets/FuwaCourse/Meshes/IslandDisc";

    public static Mesh Get(float radius)
    {
        int cm = Mathf.Max(1, Mathf.RoundToInt(radius * 100f));
        string path = Dir + "/IslandDisc_r" + cm + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "IslandDisc");
        mesh = Build(cm / 100f);
        mesh.name = "IslandDisc_r" + cm;
        AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        return mesh;
    }

    static Mesh Build(float R)
    {
        int seg = Mathf.Clamp(Mathf.RoundToInt(R * 40f), 40, 160);
        // 断面（中心からの距離, 高さ, 法線の横, 法線の上）：上面の中心 → 上のふちの丸め → 側面 → 下のふちの丸め → 底面の中心
        var prof = new List<Vector4>();
        for (int k = 0; k <= BevelSteps; k++)
        {
            float a = (Mathf.PI / 2) * k / BevelSteps;
            prof.Add(new Vector4(R - TopBevel + TopBevel * Mathf.Sin(a), HalfThick - TopBevel + TopBevel * Mathf.Cos(a), Mathf.Sin(a), Mathf.Cos(a)));
        }
        for (int k = 0; k <= 2; k++)
        {
            float a = (Mathf.PI / 2) * k / 2;
            prof.Add(new Vector4(R - BottomBevel + BottomBevel * Mathf.Cos(a), -HalfThick + BottomBevel - BottomBevel * Mathf.Sin(a), Mathf.Cos(a), -Mathf.Sin(a)));
        }
        var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>(); var T = new List<int>();
        int P = prof.Count;
        // ふちの輪（つなぎ目は角度0と2πで頂点を分ける）
        for (int i = 0; i <= seg; i++)
        {
            float th = 2f * Mathf.PI * i / seg; var d = new Vector3(Mathf.Cos(th), 0, Mathf.Sin(th));
            foreach (var q in prof)
            {
                V.Add(d * q.x + Vector3.up * q.y); N.Add((d * q.z + Vector3.up * q.w).normalized);
                UV.Add(new Vector2(0.5f + 0.5f * d.x * q.x / R, 0.5f + 0.5f * d.z * q.x / R));
            }
        }
        for (int i = 0; i < seg; i++)
            for (int k = 0; k < P - 1; k++)
            {
                int a = i * P + k, b = (i + 1) * P + k;
                T.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        // 上面・底面（平らな扇）
        foreach (var (y, ny, ring) in new[] { (HalfThick, 1f, 0), (-HalfThick, -1f, P - 1) })
        {
            int c = V.Count; V.Add(new Vector3(0, y, 0)); N.Add(Vector3.up * ny); UV.Add(new Vector2(0.5f, 0.5f));
            int first = V.Count;
            for (int i = 0; i <= seg; i++) { var p = V[i * P + ring]; V.Add(p); N.Add(Vector3.up * ny); UV.Add(UV[i * P + ring]); }
            for (int i = 0; i < seg; i++)
            {
                int a = first + i, b = first + i + 1;
                if (ny > 0) T.AddRange(new[] { c, b, a }); else T.AddRange(new[] { c, a, b });
            }
        }
        // 向きを法線にそろえる（念のため）
        for (int t = 0; t < T.Count; t += 3)
        {
            var n = Vector3.Cross(V[T[t + 1]] - V[T[t]], V[T[t + 2]] - V[T[t]]);
            if (Vector3.Dot(n, N[T[t]] + N[T[t + 1]] + N[T[t + 2]]) < 0) { int s = T[t + 1]; T[t + 1] = T[t + 2]; T[t + 2] = s; }
        }
        var m = new Mesh();
        m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetTriangles(T, 0);
        m.RecalculateBounds(); m.RecalculateTangents();
        return m;
    }

    // シーンの IslandTop（Unity の Cylinder を平たくしたもの）を、丸めた円盤に置き換える
    [MenuItem("FuwaCourse/Bevel Island Discs")]
    public static void ReplaceAllMenu() { Debug.Log(ReplaceAll()); }

    public static string ReplaceAll()
    {
        int n = 0;
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (mf.name != "IslandTop" || mf.sharedMesh == null || mf.sharedMesh.name != "Cylinder") continue;
            var t = mf.transform;
            float r = t.localScale.x * 0.5f;
            var disc = Get(r);
            Undo.RecordObject(t, "disc"); Undo.RecordObject(mf, "disc");
            t.localScale = Vector3.one;
            mf.sharedMesh = disc;
            var mc = mf.GetComponent<MeshCollider>();
            if (mc != null) { Undo.RecordObject(mc, "disc"); mc.sharedMesh = null; mc.sharedMesh = disc; }
            EditorUtility.SetDirty(mf.gameObject);
            n++;
        }
        return "island discs replaced: " + n;
    }
}
