using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// コース2「いわのみち」ゴール前の噴気孔の岩場に落とし穴（2026-10-12 てじゃ「いわのみち簡単なんだよね、最後の噴気孔エリアに落とし穴でもつくるか」→ A）
// ・見た目：噴気孔の縦穴と同じしくみ（FuwaVentHoleMask で口に印 → FuwaVentShaft で奥を描く）。光らない、奥ほど真っ暗。ふちに欠けた岩
// ・当たり判定：岩場の判定（Course2_field_Col）から穴にかかる三角形を抜き、穴のまわりは穴の形どおりの輪のメッシュ（PitFloor）でふさぎ直す
//   （判定の三角形は 0.7m くらいと粗いので、抜いただけだと穴がギザギザに大きくなる）
// ・落ちたら：穴の下の PitCatcher（コースの PitCatcher のコピー。ふわふわはアウト、人は戻す）
// 元の判定メッシュは Meshes/Backup/Course2_field_Col_PrePits に残して毎回そこから作るので、何度実行しても同じ
public static class FuwaFieldPits
{
    const string Course = "FuwaCourse2";
    const string Dir = "Assets/FuwaCourse/Generated/FieldPits";
    // 穴の場所（x, z）。噴気孔から 1.7m 以上・ふちから 1.5m 以上の平らな所から選んだ（入ってすぐ／真ん中の噴気孔のそば／ゴール手前の左右）
    static readonly Vector2[] Pits = { new Vector2(136.5f, 14.5f), new Vector2(140.0f, 21.5f), new Vector2(144.5f, 31.0f), new Vector2(146.75f, 28.75f),
        new Vector2(141.5f, 24.5f) };   // 5つめ：噴気孔4つに囲まれた真ん中（2026-10-12 てじゃ「ここにもあけよう」）
    const float Radius = 0.75f;     // 口の半径（ふちのギザギザで ±10%）
    const float Jag = 0.1f;
    const float Depth = 2.6f;       // 縦穴の深さ
    const float FloorOuter = 1.5f;  // ふさぎ直す輪の外側（穴のふちから）

    [MenuItem("FuwaCourse/Course2/ゴール前の岩場の落とし穴を作る")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "FieldPits");
        var root = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == Course).transform;
        var field = root.Find("Rocks2/RockField");
        var mc = field.GetComponent<MeshCollider>();
        string livePath = AssetDatabase.GetAssetPath(mc.sharedMesh);
        string bakPath = "Assets/FuwaCourse/Meshes/Backup/" + System.IO.Path.GetFileNameWithoutExtension(livePath) + "_PrePits.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(bakPath) == null) AssetDatabase.CopyAsset(livePath, bakPath);
        var bak = AssetDatabase.LoadAssetAtPath<Mesh>(bakPath);
        var live = AssetDatabase.LoadAssetAtPath<Mesh>(livePath);

        // 高さを測るための元の判定（一時的な物）
        var probe = new GameObject("PitProbe"); probe.transform.SetPositionAndRotation(field.position, field.rotation); probe.transform.localScale = field.lossyScale;
        var pcol = probe.AddComponent<MeshCollider>(); pcol.sharedMesh = bak;
        Physics.SyncTransforms();
        float Ground(float x, float z, float fallback) { return pcol.Raycast(new Ray(new Vector3(x, 40f, z), Vector3.down), out var h, 80f) ? h.point.y : fallback; }

        var shaftMat = ShaftMat(); var maskMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/VentHoleMask.mat");
        var rockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Rock.mat");
        var rocks = new[] { "CrumbleRock1", "CrumbleRock2", "CrumbleRock4" }.Select(n => AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/" + n + ".asset")).ToArray();
        var catcherSrc = root.Find("PitCatcher");

        var old = root.Find("Gimmick_FieldPits"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject("Gimmick_FieldPits").transform; holder.SetParent(root, false);
        var log = new List<string>();
        var rnd = new System.Random(505);
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }

        // ---- 判定：穴にかかる三角形を抜く ----
        var bv = bak.vertices; var bt = bak.triangles; var ft = field;
        var wv = bv.Select(v => ft.TransformPoint(v)).ToArray();
        float inner = Radius * (1f - Jag);   // 判定の穴は口のいちばん内側に合わせる（見た目は地面なのに落ちる、がないように）
        var keep = new List<int>(bt.Length); int removed = 0;
        for (int i = 0; i < bt.Length; i += 3)
        {
            Vector2 a = XZ(wv[bt[i]]), b = XZ(wv[bt[i + 1]]), c = XZ(wv[bt[i + 2]]);
            bool hit = TopTri(wv[bt[i]], wv[bt[i + 1]], wv[bt[i + 2]]) && Pits.Any(p => DistTri(p, a, b, c) < inner + 0.02f);
            if (hit) { removed++; continue; }
            keep.Add(bt[i]); keep.Add(bt[i + 1]); keep.Add(bt[i + 2]);
        }
        live.Clear(); live.indexFormat = bak.indexFormat; live.SetVertices(bv); live.SetTriangles(keep, 0); live.RecalculateNormals(); live.RecalculateBounds(); EditorUtility.SetDirty(live);
        mc.sharedMesh = null; mc.sharedMesh = live;
        log.Add("field col: removed " + removed + " tris");
        // 抜いた三角形がふさぎ直す輪の外まで出ていないか（出ていたら穴があく）
        float maxReach = 0f;
        for (int i = 0; i < bt.Length; i += 3)
        {
            Vector2 a = XZ(wv[bt[i]]), b = XZ(wv[bt[i + 1]]), c = XZ(wv[bt[i + 2]]);
            if (TopTri(wv[bt[i]], wv[bt[i + 1]], wv[bt[i + 2]])) foreach (var p in Pits) if (DistTri(p, a, b, c) < inner + 0.02f) maxReach = Mathf.Max(maxReach, (a - p).magnitude, (b - p).magnitude, (c - p).magnitude);
        }
        log.Add("removed reach " + maxReach.ToString("F2") + " / floor outer " + (inner + FloorOuter).ToString("F2"));

        for (int k = 0; k < Pits.Length; k++)
        {
            var c = Pits[k]; float cy = Ground(c.x, c.y, 4.5f);
            var pit = new GameObject("Pit" + (k + 1)).transform; pit.SetParent(holder, false); pit.position = new Vector3(c.x, cy, c.y);
            // 口の形（角度ごとの半径）
            const int N = 36; var rad = new float[N]; float p1 = R(0, 6.28f), p2 = R(0, 6.28f);
            for (int i = 0; i < N; i++) { float a = i / (float)N * Mathf.PI * 2f; rad[i] = Radius * (1f + Jag * (0.55f * Mathf.Sin(a * 3 + p1) + 0.45f * Mathf.Sin(a * 5 + p2)) * (i % 2 == 0 ? 1f : 0.8f)); }
            Vector3 Edge(int i, float scale, float dy) { float a = i / (float)N * Mathf.PI * 2f; float r = rad[i % N] * scale; float x = c.x + Mathf.Cos(a) * r, z = c.y + Mathf.Sin(a) * r; return new Vector3(x, Ground(x, z, cy) + dy, z); }

            // 判定の輪（口のいちばん内側の円 〜 外側）。地面の高さに沿わせる
            {
                const int M = 40, RINGS = 5; var V = new List<Vector3>(); var T = new List<int>();
                for (int j = 0; j <= RINGS; j++)
                {
                    float rr = inner + FloorOuter * (j / (float)RINGS);
                    for (int i = 0; i < M; i++) { float a = i / (float)M * Mathf.PI * 2f; float x = c.x + Mathf.Cos(a) * rr, z = c.y + Mathf.Sin(a) * rr; V.Add(pit.InverseTransformPoint(new Vector3(x, Ground(x, z, cy), z))); }
                }
                for (int j = 0; j < RINGS; j++) for (int i = 0; i < M; i++)
                {
                    int a0 = j * M + i, a1 = j * M + (i + 1) % M, b0 = a0 + M, b1 = a1 + M;
                    T.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
                var m = UpFacing(MakeMesh(V, T, null, "FieldPit" + (k + 1) + "_Floor"));
                var fl = new GameObject("PitFloor"); fl.transform.SetParent(pit, false); fl.layer = field.gameObject.layer;
                var fc = fl.AddComponent<MeshCollider>(); fc.sharedMesh = Save(m, Dir + "/FieldPit" + (k + 1) + "_Floor.asset"); fc.sharedMaterial = mc.sharedMaterial;
            }
            // 見た目：口（印をつける円盤、地面から少し上）と縦穴
            {
                var V = new List<Vector3> { pit.InverseTransformPoint(new Vector3(c.x, cy + 0.015f, c.y)) }; var T = new List<int>();
                for (int i = 0; i < N; i++) V.Add(pit.InverseTransformPoint(Edge(i, 1f, 0.015f)));
                for (int i = 0; i < N; i++) T.AddRange(new[] { 0, 1 + (i + 1) % N, 1 + i });
                var m = UpFacing(MakeMesh(V, T, null, "FieldPit" + (k + 1) + "_Mouth"));
                var mo = new GameObject("Hole", typeof(MeshFilter), typeof(MeshRenderer)); mo.transform.SetParent(pit, false);
                mo.GetComponent<MeshFilter>().sharedMesh = Save(m, Dir + "/FieldPit" + (k + 1) + "_Mouth.asset");
                var mr = mo.GetComponent<MeshRenderer>(); mr.sharedMaterial = maskMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;

                // 縦穴：口から下へ、少しずつすぼまりながら（でこぼこ）。内側だけ。uv.y＝深さ（0〜1）
                const int K = 8; var SV = new List<Vector3>(); var SN = new List<Vector3>(); var SU = new List<Vector2>(); var ST = new List<int>();
                var ring = new Vector3[K + 1, N];
                for (int j = 0; j <= K; j++)
                {
                    float t = j / (float)K, shrink = 1.04f - 0.3f * t * t;
                    for (int i = 0; i < N; i++)
                    {
                        var e = Edge(i, 1f, 0f); var off = new Vector3(e.x - c.x, 0, e.z - c.y);
                        float jit = j == 0 ? 0f : R(-0.07f, 0.07f);
                        ring[j, i] = new Vector3(c.x, e.y + 0.05f - Depth * t, c.y) + off * (shrink + jit);
                    }
                }
                for (int j = 0; j < K; j++) for (int i = 0; i < N; i++)
                {
                    int i1 = (i + 1) % N;
                    Tri(SV, SN, SU, ST, ring[j, i], ring[j, i1], ring[j + 1, i1], j / (float)K, j / (float)K, (j + 1) / (float)K, pit.position);
                    Tri(SV, SN, SU, ST, ring[j, i], ring[j + 1, i1], ring[j + 1, i], j / (float)K, (j + 1) / (float)K, (j + 1) / (float)K, pit.position);
                }
                var bc = Vector3.zero; for (int i = 0; i < N; i++) bc += ring[K, i]; bc /= N;
                for (int i = 0; i < N; i++) Tri(SV, SN, SU, ST, ring[K, i], ring[K, (i + 1) % N], bc, 1f, 1f, 1f, bc + Vector3.up * 5f);
                for (int i = 0; i < SV.Count; i++) { SV[i] = pit.InverseTransformPoint(SV[i]); }
                var sm = MakeMesh(SV, ST, SU, "FieldPit" + (k + 1) + "_Shaft"); sm.SetNormals(SN); sm.RecalculateBounds();
                var sh = new GameObject("Shaft", typeof(MeshFilter), typeof(MeshRenderer)); sh.transform.SetParent(mo.transform, false);
                sh.GetComponent<MeshFilter>().sharedMesh = Save(sm, Dir + "/FieldPit" + (k + 1) + "_Shaft.asset", true);
                var sr = sh.GetComponent<MeshRenderer>(); sr.sharedMaterial = shaftMat; sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; sr.receiveShadows = false;
            }
            // ふちの欠けた岩（噴気孔と見分けがつくように。口にかからない所に、半分うめて）
            int nr = 0; rnd.Next(4, 7);   // 2026-10-12：口の手前の地面より低い所にあると黒い穴の上に浮いて見えたので、今は置かない（乱数は進めておく）
            for (int q = 0; q < nr; q++)
            {
                int i = (int)((q + R(-0.3f, 0.3f)) / nr * N + N) % N; float s = R(0.22f, 0.36f);
                var e = Edge(i, 1f, 0f); var dir = new Vector3(e.x - c.x, 0, e.z - c.y).normalized;
                var p = e + dir * (s * 0.75f + R(0.04f, 0.14f)) + Vector3.down * s * 0.15f;   // 穴の上にはみ出すと黒い穴の上に浮いて見えるので外へ
                var rk = new GameObject("Chip", typeof(MeshFilter), typeof(MeshRenderer)); rk.transform.SetParent(pit, false);
                rk.transform.position = p; rk.transform.rotation = Quaternion.Euler(R(-15, 15), R(0, 360), R(-15, 15));
                rk.transform.localScale = new Vector3(s * R(0.9f, 1.2f), s * R(0.5f, 0.8f), s * R(0.9f, 1.2f));
                rk.GetComponent<MeshFilter>().sharedMesh = rocks[rnd.Next(rocks.Length)];
                var rr = rk.GetComponent<MeshRenderer>(); rr.sharedMaterial = rockMat; rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // 落ちたら：コースの PitCatcher のコピー（ふわふわはアウト、人は戻す）。穴の下 1.0〜1.6m
            // （ふわふわはゆっくり落ちる（0.7m/秒くらい）ので、深くすると暗い穴の中で待たされる。ClientSim で落ちてアウトになるのを確認）
            if (catcherSrc != null)
            {
                var cat = Object.Instantiate(catcherSrc.gameObject, pit).transform; cat.name = "PitCatcher";
                cat.position = new Vector3(c.x, cy - 1.3f, c.y); cat.rotation = Quaternion.identity;
                var bx = cat.GetComponent<BoxCollider>(); bx.center = Vector3.zero; bx.size = new Vector3(1f, 1f, 1f);
                cat.localScale = new Vector3(Radius * 2.4f, 0.6f, Radius * 2.4f);
            }
            foreach (var t in pit.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent<MeshRenderer>() != null) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
            log.Add(pit.name + " " + pit.position.ToString("F2"));
        }
        // 穴の口にかかっている飾りの小石（Deco の Cluster）は、穴の外へずらす（黒い穴の上に浮いて見えるので）
        var deco = root.Find("Deco");
        if (deco != null)
            foreach (var r in deco.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.transform.IsChildOf(deco.Find("IwaIslands") ?? holder)) continue;
                var t = r.transform; if (r.bounds.size.magnitude > 3f) continue;
                foreach (var p in Pits)
                {
                    var d = new Vector2(t.position.x - p.x, t.position.z - p.y); if (d.magnitude > Radius * (1f + Jag) + 0.3f) continue;
                    var dir = d.magnitude > 0.01f ? d.normalized : Vector2.right; var np = p + dir * (Radius * (1f + Jag) + 0.45f);
                    t.position = new Vector3(np.x, Ground(np.x, np.y, t.position.y) + (t.position.y - Ground(t.position.x, t.position.z, t.position.y)), np.y);
                    log.Add("moved deco " + t.parent.name + "/" + t.name);
                }
            }
        Object.DestroyImmediate(probe);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return string.Join("\n", log);
    }

    // 岩場の上の面（上を向いていて、床の高さ 4.5m 付近）だけ。裏側（下を向いた大きな三角形）は抜かない
    static bool TopTri(Vector3 a, Vector3 b, Vector3 c) { return Vector3.Cross(b - a, c - a).normalized.y > 0.3f && (a.y + b.y + c.y) / 3f > 3.8f; }

    static Vector2 XZ(Vector3 v) { return new Vector2(v.x, v.z); }

    // 点 p から三角形 abc（真上から見て）までの距離（中なら0）
    static float DistTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        if (!(neg && pos)) return 0f;
        return Mathf.Min(Seg(p, a, b), Mathf.Min(Seg(p, b, c), Seg(p, c, a)));
    }
    static float Cross(Vector2 u, Vector2 v) { return u.x * v.y - u.y * v.x; }
    static float Seg(Vector2 p, Vector2 a, Vector2 b) { var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude)); return (p - (a + ab * t)).magnitude; }

    static Mesh MakeMesh(List<Vector3> V, List<int> T, List<Vector2> UV, string name)
    {
        var m = new Mesh { name = name }; m.SetVertices(V); if (UV != null) m.SetUVs(0, UV); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
    }

    static Mesh UpFacing(Mesh m)
    {
        var n = m.normals; float sum = 0; foreach (var x in n) sum += x.y;
        if (sum < 0) { var t = m.triangles; for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; } m.triangles = t; m.RecalculateNormals(); }
        return m;
    }

    // 内側（center の方）を向く三角形。面ごとに頂点を分けて角ばった岩肌に
    static void Tri(List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<int> T, Vector3 a, Vector3 b, Vector3 c, float ua, float ub, float uc, Vector3 center)
    {
        var n = Vector3.Cross(b - a, c - a).normalized; var mid = (a + b + c) / 3f;
        var want = center - mid;   // 壁は穴の中心（口の高さ）の方、底は真上に置いた点の方
        int i0 = V.Count;
        if (Vector3.Dot(n, want) < 0) { var tmp = b; b = c; c = tmp; var tu = ub; ub = uc; uc = tu; n = -n; }
        V.Add(a); V.Add(b); V.Add(c); N.Add(n); N.Add(n); N.Add(n);
        UV.Add(new Vector2(0, ua)); UV.Add(new Vector2(0, ub)); UV.Add(new Vector2(0, uc));
        T.Add(i0); T.Add(i0 + 1); T.Add(i0 + 2);
    }

    static Mesh Save(Mesh m, string path, bool keepNormals = false)
    {
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null)
        {
            var nrm = m.normals;
            old.Clear(); old.SetVertices(m.vertices); if (m.uv.Length == m.vertexCount) old.SetUVs(0, m.uv); old.SetTriangles(m.triangles, 0);
            if (keepNormals) old.SetNormals(nrm); else old.RecalculateNormals();
            old.RecalculateBounds(); EditorUtility.SetDirty(old); Object.DestroyImmediate(m); return old;
        }
        AssetDatabase.CreateAsset(m, path); return m;
    }

    // 落とし穴の縦穴：噴気孔の縦穴（VentShaft）と同じで、奥がもっと早く真っ暗に（底が見えない感じ）
    static Material ShaftMat()
    {
        string p = Dir + "/PitShaft.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/VentShaft.mat")) { name = "PitShaft" }; AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_Bottom", new Color(0.01f, 0.005f, 0.02f, 1f)); m.SetFloat("_Falloff", 2.6f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
