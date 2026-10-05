using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// いわのみちの噴気孔の溝（2026-10-05）：溝に接する黄色い道の端を「崩れた」形にする。
// ・入口側：スタートからの道（StartExt）の最後 JagLen を、ギザギザに欠けた端で作る
// ・出口側：元の道（PathMesh）の s<ExitCut を切り取って、ギザギザに欠けた始まりで作り直す
// 欠けた所の下には溝の岩の床（道の上面より 0.07 低い）がのぞく。崩れたかけらも少し転がす。
// 当たり判定は変えない（道の判定はまっすぐのまま）
public static class FuwaGrooveEdges
{
    public const float ExitCut = 1.5f;      // 元の道をここから先だけ残す（縞の境目＝輪のある所）
    public const float ExitS0 = 0.08f;      // 出口の崩れた道の始まり（溝の床の坂が上がりきる所）
    const string EntPath = "Assets/FuwaCourse/Meshes/Course2Path_JagEnter.asset";
    const string ExitPath = "Assets/FuwaCourse/Meshes/Course2Path_JagExit.asset";
    const string PathBackup = "Assets/FuwaCourse/Meshes/Backup/Course2Path_broken_beforeGrooveCut.asset";

    [MenuItem("FuwaCourse/Course2/溝の道の端を崩す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var root = GameObject.Find("FuwaCourse2").transform; var pm = root.Find("PathMesh");
        var m = FuwaCourse2Builder.ToCourse(root);
        var toPm = pm.worldToLocalMatrix * root.localToWorldMatrix * m;   // Startローカル → PathMesh ローカル
        string log = "";
        // 入口：道の終わりが欠ける
        var ent = BuildPiece(FuwaCourse2Builder.GrooveS0 - FuwaCourse2Builder.JagLen, FuwaCourse2Builder.GrooveS0, false, true, 11, toPm, true);
        FuwaCourse2Builder.Save(ent, EntPath);
        // 出口：道の始まりが欠ける
        var ex = BuildPiece(ExitS0, ExitCut, true, false, 29, toPm, true);
        FuwaCourse2Builder.Save(ex, ExitPath);
        Place(pm, "JagEnter", EntPath); Place(pm, "JagExit", ExitPath);
        log += "pieces ok; " + TrimMainPath(pm, toPm);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return log;
    }

    static void Place(Transform pm, string name, string path)
    {
        var t = pm.Find(name);
        if (t == null) { t = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)).transform; t.SetParent(pm, false); Undo.RegisterCreatedObjectUndo(t.gameObject, "jag"); }
        t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        t.gameObject.layer = pm.gameObject.layer; t.gameObject.isStatic = true;
        t.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        t.GetComponent<MeshRenderer>().sharedMaterials = pm.GetComponent<MeshRenderer>().sharedMaterials;
    }

    static float H(int i) { float x = Mathf.Sin(i * 127.1f + 311.7f) * 43758.5453f; return x - Mathf.Floor(x); }

    // 断面：Profile（両肩が丸い）の上面を細かく割る
    static List<Vector4> FineProfile(float w, int extra)
    {
        var p = FuwaCourse2Builder.Profile(w); var o = new List<Vector4>();
        for (int i = 0; i < p.Count; i++)
        {
            o.Add(p[i]);
            if (i + 1 < p.Count && Mathf.Abs(p[i].y) < 1e-4f && Mathf.Abs(p[i + 1].y) < 1e-4f && p[i + 1].x - p[i].x > 0.5f)
                for (int k = 1; k <= extra; k++) { float t = k / (float)(extra + 1); o.Add(new Vector4(Mathf.Lerp(p[i].x, p[i + 1].x, t), 0, 0, 1)); }
        }
        return o;
    }

    // s0〜s1 の道のかたまり。jagStart/jagEnd の端は、断面の場所ごとに欠ける長さを変えたギザギザの面で閉じる
    static Mesh BuildPiece(float s0, float s1, bool jagStart, bool jagEnd, int seed, Matrix4x4 toPm, bool chips)
    {
        const int pc = 0; float w = FuwaCourse2Builder.Width(pc, s0);
        var prof = FineProfile(w, 9); int P = prof.Count;
        // 断面の各点の欠け：上面は点ごとにばらばら（尖ったのと深くえぐれたのを混ぜる）、肩・横は一番近い上面の点に合わせる
        var jag = new float[P];
        int firstTop = -1, lastTop = -1; for (int i = 0; i < P; i++) if (prof[i].w > 0.99f && Mathf.Abs(prof[i].y) < 1e-4f) { if (firstTop < 0) firstTop = i; lastTop = i; }
        for (int i = 0; i < P; i++)
        {
            int j = Mathf.Clamp(i, firstTop, lastTop);
            float r = H(seed * 31 + j * 7), r2 = H(seed * 17 + j * 13);
            jag[i] = 0.05f + 0.5f * r * r + (r2 > 0.8f ? 0.12f : 0f);
            jag[i] = Mathf.Min(jag[i], s1 - s0 - 0.08f);
        }
        float Lim0(int i) => jagStart ? s0 + jag[i] : s0;
        float Lim1(int i) => jagEnd ? s1 - jag[i] : s1;
        var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>(); var A = new List<int>(); var B = new List<int>();
        // 輪：0.05 刻み。列ごとに範囲の外は端に寄せる（つぶれた三角形は後で捨てる）
        int R = Mathf.CeilToInt((s1 - s0) / 0.05f);
        var ringS = new float[R + 1]; for (int r = 0; r <= R; r++) ringS[r] = Mathf.Lerp(s0, s1, r / (float)R);
        var idx = new int[R + 1, P];
        for (int r = 0; r <= R; r++)
            for (int i = 0; i < P; i++)
            {
                float s = Mathf.Clamp(ringS[r], Lim0(i), Lim1(i)); var q = prof[i];
                idx[r, i] = V.Count;
                V.Add(toPm.MultiplyPoint3x4(FuwaCourse2Builder.Point(pc, s, q.x, q.y)));
                N.Add(toPm.MultiplyVector(FuwaCourse2Builder.Normal(pc, s, q.z, q.w)).normalized); UV.Add(new Vector2(q.x, s));
            }
        System.Func<float, List<int>> listOf = s => (Mathf.FloorToInt((s + 60f + 0.001f) / FuwaCourse2Builder.Stripe) % 2 == 0) ? A : B;
        for (int r = 0; r < R; r++)
        {
            var list = listOf((ringS[r] + ringS[r + 1]) * 0.5f);
            for (int i = 0; i < P - 1; i++) { int x0 = idx[r, i], x1 = idx[r, i + 1], y0 = idx[r + 1, i], y1 = idx[r + 1, i + 1]; Tri(V, list, x0, y0, x1); Tri(V, list, x1, y0, y1); }
            // 底
            int bb = V.Count; var dn = toPm.MultiplyVector(Vector3.down).normalized;
            foreach (var k in new[] { idx[r, 0], idx[r, P - 1], idx[r + 1, 0], idx[r + 1, P - 1] }) { V.Add(V[k]); N.Add(dn); UV.Add(Vector2.zero); }
            TriDir(V, list, bb, bb + 1, bb + 2, dn); TriDir(V, list, bb + 1, bb + 3, bb + 2, dn);
        }
        // 欠けた端のふた：外周（断面の点）を真ん中の点から扇形に。ふたの面は欠けた岩肌っぽく、少しへこませる
        if (jagStart) Cap(V, N, UV, A, B, idx, 0, P, toPm, -1f, s0, prof, listOf);
        if (jagEnd) Cap(V, N, UV, A, B, idx, R, P, toPm, 1f, s1, prof, listOf);
        if (!jagStart) CapFlat(V, N, UV, A, idx, 0, P, toPm, -1f, s0);
        if (!jagEnd) CapFlat(V, N, UV, A, idx, R, P, toPm, 1f, s1);
        // 崩れたかけら：欠けた端の先の岩の床に、小さな道のかけらを3つ
        if (chips)
        {
            bool atEnd = jagEnd; float baseS = atEnd ? s1 : s0;
            for (int c = 0; c < 3; c++)
            {
                float u = H(seed * 5 + c * 11), v = H(seed * 9 + c * 3);
                float s = baseS + (atEnd ? 1f : -1f) * (0.15f + 0.45f * u);
                float lat = Mathf.Lerp(-0.7f, 0.7f, v);
                float y = FloorZ(s) + 0.012f;   // 少し床にめり込ませて、置いた感じを消す
                var ctr = FuwaCourse2Builder.Point(pc, s, lat, y - FuwaCourse2Builder.Top - FuwaCourse2Builder.Height(pc, s) + FuwaCourse2Builder.Top);
                ctr.y = FuwaCourse2Builder.Top + y;
                var size = new Vector3(0.07f + 0.09f * u, 0.05f, 0.06f + 0.07f * v);
                var rot = Quaternion.Euler(30f * (u - 0.5f), 360f * H(seed + c * 19), 36f * (v - 0.5f));
                Box(V, N, UV, (c % 2 == 0) ? A : B, toPm, ctr, rot, size);
            }
        }
        // つぶれた三角形を捨てる
        foreach (var L in new[] { A, B }) for (int t = L.Count - 3; t >= 0; t -= 3) if (Vector3.Cross(V[L[t + 1]] - V[L[t]], V[L[t + 2]] - V[L[t]]).sqrMagnitude < 1e-12f) L.RemoveRange(t, 3);
        var mesh = new Mesh { name = jagEnd ? "Course2Path_JagEnter" : "Course2Path_JagExit", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, UV); mesh.subMeshCount = 2; mesh.SetTriangles(A, 0); mesh.SetTriangles(B, 1);
        mesh.RecalculateBounds(); Unwrapping.GenerateSecondaryUVSet(mesh);
        return mesh;
    }

    // 溝の岩の床の上面の高さ（道の上面からの差）。FLUFF_models.blend の C2_groove_floor と同じ式
    public static float FloorZ(float s)
    {
        const float D = 1f, Lip = 0.07f; float ra0 = -9.3f, ra1 = -7.7f, rb0 = -1.5f, rb1 = ExitS0;
        System.Func<float, float> sm = t => { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); };
        if (s < ra0 || s > rb1) return -Lip;
        if (s < ra1) return Mathf.Lerp(-Lip, -D, sm((s - ra0) / (ra1 - ra0)));
        if (s > rb0) return Mathf.Lerp(-D, -Lip, sm((s - rb0) / (rb1 - rb0)));
        return -D;
    }

    static void Cap(List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<int> A, List<int> B, int[,] idx, int r, int P, Matrix4x4 toPm, float dir, float sEnd, List<Vector4> prof, System.Func<float, List<int>> listOf)
    {
        FuwaCourse2Builder.Center(sEnd, out _, out var d);
        var nrm = toPm.MultiplyVector(new Vector3(d.x, 0, d.y) * dir).normalized;
        // 真ん中の点（少し奥へ引っ込めて、欠けた面にでこぼこを出す）
        Vector3 c = Vector3.zero; for (int i = 0; i < P; i++) c += V[idx[r, i]]; c /= P; c -= nrm * 0.08f;
        int cb = V.Count; V.Add(c); N.Add(nrm); UV.Add(Vector2.zero);
        var list = listOf(sEnd - dir * 0.02f);
        for (int i = 0; i < P; i++) { V.Add(V[idx[r, i]]); N.Add(nrm); UV.Add(Vector2.zero); }
        for (int i = 0; i < P - 1; i++) TriDir(V, list, cb, cb + 1 + i, cb + 2 + i, nrm);
        TriDir(V, list, cb, cb + P, cb + 1, nrm);   // 底の辺
    }

    static void CapFlat(List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<int> A, int[,] idx, int r, int P, Matrix4x4 toPm, float dir, float sEnd)
    {
        // つながる側は元の道／StartExt と背中合わせ（中に隠れる）。ふたは付けずに開けておく
    }

    static void Box(List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<int> L, Matrix4x4 toPm, Vector3 c, Quaternion rot, Vector3 size)
    {
        var dirs = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (var f in dirs)
        {
            Vector3 u = Vector3.Cross(f, Mathf.Abs(f.y) > 0.5f ? Vector3.forward : Vector3.up).normalized, v = Vector3.Cross(f, u);
            int b = V.Count; var n = toPm.MultiplyVector(rot * f).normalized;
            foreach (var (a1, a2) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                var lp = Vector3.Scale(f + u * a1 + v * a2, size * 0.5f);
                V.Add(toPm.MultiplyPoint3x4(c + rot * lp)); N.Add(n); UV.Add(Vector2.zero);
            }
            TriDir(V, L, b, b + 1, b + 2, n); TriDir(V, L, b, b + 2, b + 3, n);
        }
    }

    static void Tri(List<Vector3> V, List<int> L, int a, int b, int c) { L.Add(a); L.Add(b); L.Add(c); }
    static void TriDir(List<Vector3> V, List<int> L, int a, int b, int c, Vector3 want)
    {
        var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
        if (Vector3.Dot(n, want) >= 0) { L.Add(a); L.Add(b); L.Add(c); } else { L.Add(a); L.Add(c); L.Add(b); }
    }

    // 元の道の見た目メッシュから s<ExitCut の三角形を取り除く（最初の1回はバックアップを残す）
    static string TrimMainPath(Transform pm, Matrix4x4 toPm)
    {
        var mf = pm.GetComponent<MeshFilter>(); var mesh = mf.sharedMesh; string path = AssetDatabase.GetAssetPath(mesh);
        if (!System.IO.Directory.Exists("Assets/FuwaCourse/Meshes/Backup")) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "Backup");
        if (AssetDatabase.LoadAssetAtPath<Mesh>(PathBackup) == null) AssetDatabase.CopyAsset(path, PathBackup);
        var inv = toPm.inverse; var vs = mesh.vertices; var sv = new float[vs.Length];
        for (int i = 0; i < vs.Length; i++) { var l = inv.MultiplyPoint3x4(vs[i]); sv[i] = (l.z >= 0f && l.z <= 10f && Mathf.Abs(l.x) < 4f && Mathf.Abs(l.y - FuwaCourse2Builder.Top) < 0.6f) ? l.z - 1f : 99f; }   // s=0〜9 はまっすぐ（Startローカル z = s+1）
        int removed = 0;
        for (int sm = 0; sm < mesh.subMeshCount; sm++)
        {
            var t = new List<int>(mesh.GetTriangles(sm)); var keep = new List<int>();
            for (int k = 0; k < t.Count; k += 3)
            {
                bool cut = sv[t[k]] < ExitCut - 0.01f || sv[t[k + 1]] < ExitCut - 0.01f || sv[t[k + 2]] < ExitCut - 0.01f;
                bool allBefore = sv[t[k]] < ExitCut + 0.01f && sv[t[k + 1]] < ExitCut + 0.01f && sv[t[k + 2]] < ExitCut + 0.01f;
                if (cut && allBefore) { removed++; continue; }
                keep.AddRange(new[] { t[k], t[k + 1], t[k + 2] });
            }
            mesh.SetTriangles(keep, sm);
        }
        EditorUtility.SetDirty(mesh);
        return "main path tris removed " + removed + " (" + path + ")";
    }
}
