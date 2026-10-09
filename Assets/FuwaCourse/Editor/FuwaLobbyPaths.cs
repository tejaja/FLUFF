using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// ロビーの床を芝にして、まん中（出てくる所）から各コースのワープへ、そのコースの道と同じ色のシマシマ道をのばす（2026-10-09 てじゃ）。
// 道は床から少しだけ（Rise。ワープのうず y+0.04 より下にして重ならないように）上がった薄い板。上のふちは小さく丸める。先はワープのうずの下にもぐらせる。
// 何度実行してもよい
public static class FuwaLobbyPaths
{
    const float HalfW = 0.55f, Rise = 0.025f, Sink = 0.03f, Bevel = 0.02f, Stripe = 0.5f;
    const float StartR = 1.3f;          // まん中から何mで道を始めるか（道どうしが重ならない所）
    const float RingIn = 0.85f;         // ワープのうず（半径）の何割の所まで道を入れるか（うずが道の先に重なって、つながって見える）
    const int BevelSteps = 3;
    const string MeshDir = "Assets/FuwaCourse/Meshes/Lobby";
    // コース4（まもののす）はシマシマ道を使っていない（巣と木の橋）ので、巣の小枝っぽい茶色を専用に作る（2026-10-09）
    static readonly string[] CourseMat = { "Course0", "Course1", "Course2", "Course3", "LobbyCourse4", "Course5" };

    [MenuItem("FuwaCourse/ロビーの道を作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var lobby = GameObject.Find("FuwaLobby").transform;
        var floor = lobby.Find("LobbyFloor");
        var center = lobby.Find("LobbySpawn").position;
        float floorY = floor.GetComponent<Renderer>().bounds.max.y;
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "Lobby");

        // コース4用の茶色（なければ Course4 の道の材質を写して作る）
        foreach (var (n, col) in new[] { ("LobbyCourse4_PathA", new Color(0.76f, 0.58f, 0.42f)), ("LobbyCourse4_PathB", new Color(0.66f, 0.49f, 0.35f)) })
        {
            string mp = "Assets/FuwaCourse/Materials/" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (m == null) { m = new Material(FindMat(n.Replace("LobbyCourse4", "Course4"))); AssetDatabase.CreateAsset(m, mp); }
            m.color = col; EditorUtility.SetDirty(m);
        }
        // 床を芝に
        floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Goal_Ground.mat");

        var holder = lobby.Find("CoursePaths");
        if (holder != null) Object.DestroyImmediate(holder.gameObject);
        holder = new GameObject("CoursePaths").transform; holder.SetParent(lobby, false);
        var log = new System.Text.StringBuilder();
        for (int i = 0; i < 6; i++)
        {
            var slot = lobby.Find("Slot" + i); if (slot == null) continue;
            var warp = slot.Find("WarpToCourse" + i);
            float warpR = warp != null && warp.GetComponent<Renderer>() != null ? warp.GetComponent<Renderer>().bounds.extents.x : 1.2f;
            var to = slot.position - center; to.y = 0f;
            float len = to.magnitude - warpR * RingIn;
            var dir = to.normalized;
            var mesh = Strip(StartR, len, floorY - center.y);
            mesh.name = "LobbyPath_" + i;
            string path = MeshDir + "/LobbyPath_" + i + ".asset";
            var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (ex == null) AssetDatabase.CreateAsset(mesh, path); else { ex.Clear(); EditorUtility.CopySerialized(mesh, ex); ex.name = mesh.name; mesh = ex; }
            var go = new GameObject("Path_Course" + i, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(holder, false);
            go.transform.SetPositionAndRotation(center, Quaternion.LookRotation(dir, Vector3.up));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var a = FindMat(CourseMat[i] + "_PathA"); var b = FindMat(CourseMat[i] + "_PathB");
            go.GetComponent<MeshRenderer>().sharedMaterials = new[] { a, b };
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            go.layer = floor.gameObject.layer;
            log.Append("Course" + i + " " + (len - StartR).ToString("F1") + "m ");
        }

        // 銃の台・キノコの位置は、見た目を決めてから考える（てじゃ）。ここでは動かさない
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    static Material FindMat(string n)
    {
        foreach (var g in AssetDatabase.FindAssets(n + " t:Material"))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
            if (m != null && m.name == n) return m;
        }
        return null;
    }

    // ローカル：z が道の向き（z0〜z1）、x が横、y=floorY が床の上面。上面は floorY+Rise、下は床に埋める
    static Mesh Strip(float z0, float z1, float floorY)
    {
        float top = floorY + Rise, bot = floorY - Sink;
        // 断面（横, 高さ, 法線x, 法線y）：左の側面 → 丸め → 上面 → 丸め → 右の側面
        var prof = new List<Vector4> { new Vector4(-HalfW, bot, -1, 0) };
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(-HalfW + Bevel + Bevel * Mathf.Cos(a), top - Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI / 2 - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(HalfW - Bevel + Bevel * Mathf.Cos(a), top - Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        prof.Add(new Vector4(HalfW, bot, 1, 0));
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
        }
        // 両はしのふた（はしの色はそこのしまの色）
        foreach (var (z, sgn, T) in new[] { (z0, -1f, Mathf.FloorToInt((z0 + 0.01f) / Stripe) % 2 == 0 ? A : B), (z1, 1f, Mathf.FloorToInt((z1 - 0.01f) / Stripe) % 2 == 0 ? A : B) })
        {
            int cb = V.Count;
            foreach (var q in prof) { V.Add(new Vector3(q.x, q.y, z)); N.Add(new Vector3(0, 0, sgn)); UV.Add(Vector2.zero); }
            for (int i = 1; i < P - 1; i++) T.AddRange(new[] { cb, cb + i, cb + i + 1 });
        }
        // 三角形の向きを法線にそろえる
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
