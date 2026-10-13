using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// まものの寝ている時の目「︶」（2026-10-13 てじゃ「寝てる時の目玉、うっすら黒目みえてこわい」「😴こんな目に切り替えるとか」）。
// 目（Eyes）の白目ごとに、細い弧の線（SleepEye）を足す。寝ている間だけ白目・黒目の代わりに出す（FuwaEnemy / FuwaMama / FuwaChillPlush が切り替える）。
// まものを作り直したら（コース4・チュートリアル・あそびば）、このメニューをもう一度。何度実行してもよい
public static class FuwaSleepEyesSetup
{
    const string MeshPath = "Assets/FuwaCourse/Meshes/SleepEyeArc.asset";
    // 目（Eyes）の中の大きさ。白目は直径0.15、前の面は z 0.34
    const float Width = 0.15f, Sag = 0.04f, Thick = 0.024f, FrontZ = 0.335f, DownY = -0.012f;

    [MenuItem("FuwaCourse/まものの目を整える（寝てる目︶・なめらかな目玉）")]
    public static void Menu()
    {
        Debug.Log(Run());
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    public static string Run()
    {
        var mesh = GetMesh();
        var eyesList = new List<Transform>();
        foreach (var e in Object.FindObjectsOfType<FuwaEnemy>(true)) if (e.eyes != null) eyesList.Add(e.eyes);
        foreach (var m in Object.FindObjectsOfType<FuwaMama>(true)) if (m.eyes != null) eyesList.Add(m.eyes);
        foreach (var p in Object.FindObjectsOfType<FuwaChillPlush>(true)) if (p.eyes != null) eyesList.Add(p.eyes);
        int n = 0;
        foreach (var eyes in eyesList.Distinct()) { AddTo(eyes, mesh); n++; }
        // 白目・黒目をなめらかな球に（低ポリの球だと、上から見た時に白目のてっぺんがカクカクに割れて、くぼんでいるように見えた。2026-10-13 てじゃ）
        var demoEyes = Object.FindObjectsOfType<FuwaDemoMonster>(true).Where(d => d.eyes != null).Select(d => d.eyes);
        int sm = 0;
        foreach (var eyes in eyesList.Concat(demoEyes).Distinct()) sm += SmoothEyes(eyes);
        return "sleep eyes: " + n + ", smooth eye parts: " + sm;
    }

    public static void AddTo(Transform eyes, Mesh mesh = null)
    {
        if (mesh == null) mesh = GetMesh();
        foreach (var old in eyes.Cast<Transform>().Where(t => t.name == "SleepEye").ToList()) Object.DestroyImmediate(old.gameObject);
        var pupil = eyes.Cast<Transform>().FirstOrDefault(t => t.name == "Pupil");
        var mat = pupil != null ? pupil.GetComponent<Renderer>().sharedMaterial : null;   // 黒目と同じ色（濃い紫）
        foreach (var white in eyes.Cast<Transform>().Where(t => t.name == "EyeWhite").ToList())
        {
            var g = new GameObject("SleepEye"); g.layer = white.gameObject.layer;
            g.transform.SetParent(eyes, false);
            g.transform.localPosition = new Vector3(white.localPosition.x, white.localPosition.y + DownY, FrontZ);
            g.transform.localRotation = Quaternion.identity; g.transform.localScale = Vector3.one;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(g, 0);
            g.SetActive(false);
        }
    }

    public static int SmoothEyes(Transform eyes)
    {
        var sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        int n = 0;
        foreach (Transform t in eyes)
        {
            if (t.name != "EyeWhite" && t.name != "Pupil") continue;
            var mf = t.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == sphere) continue;
            mf.sharedMesh = sphere; EditorUtility.SetDirty(mf); n++;
        }
        return n;
    }

    // 「︶」の弧：両はしが上、まん中が下の放物線に沿った細い筒。前（+Z）から見る
    static Mesh GetMesh()
    {
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        bool isNew = m == null;
        if (isNew) m = new Mesh { name = "SleepEyeArc" };
        const int seg = 14, ring = 8;
        var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
        Vector3 P(float t) { float u = t * 2f - 1f; return new Vector3(u * Width * 0.5f, -Sag * (1f - u * u), 0f); }
        for (int i = 0; i <= seg; i++)
        {
            float t = i / (float)seg;
            Vector3 c = P(t);
            Vector3 tan = (P(Mathf.Min(1f, t + 0.01f)) - P(Mathf.Max(0f, t - 0.01f))).normalized;
            Vector3 a = Vector3.Cross(tan, Vector3.forward).normalized, b = Vector3.forward;
            // 両はしは少し細く
            float rr = Thick * 0.5f * Mathf.Lerp(0.55f, 1f, Mathf.Sin(t * Mathf.PI));
            for (int j = 0; j < ring; j++)
            {
                float ang = j / (float)ring * Mathf.PI * 2f;
                Vector3 n = a * Mathf.Cos(ang) + b * Mathf.Sin(ang);
                verts.Add(c + n * rr); norms.Add(n);
            }
        }
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < ring; j++)
            {
                int a0 = i * ring + j, a1 = i * ring + (j + 1) % ring, b0 = a0 + ring, b1 = a1 + ring;
                tris.Add(a0); tris.Add(b0); tris.Add(a1);
                tris.Add(a1); tris.Add(b0); tris.Add(b1);
            }
        // はしのふた
        foreach (int i in new[] { 0, seg })
        {
            Vector3 c = P(i / (float)seg); Vector3 tan = (P(1f) - P(0f)).normalized * (i == 0 ? -1f : 1f);
            int ci = verts.Count; verts.Add(c); norms.Add(tan);
            for (int j = 0; j < ring; j++)
            {
                int v0 = i * ring + j, v1 = i * ring + (j + 1) % ring;
                if (i == 0) { tris.Add(ci); tris.Add(v0); tris.Add(v1); } else { tris.Add(ci); tris.Add(v1); tris.Add(v0); }
            }
        }
        m.Clear(); m.SetVertices(verts); m.SetNormals(norms); m.SetTriangles(tris, 0); m.RecalculateBounds();
        if (isNew) { System.IO.Directory.CreateDirectory("Assets/FuwaCourse/Meshes"); AssetDatabase.CreateAsset(m, MeshPath); }
        else EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }
}
