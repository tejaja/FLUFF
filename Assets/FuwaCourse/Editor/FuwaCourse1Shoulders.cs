using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// かぜのおかの横風の風下に、道の横へくっつく芝生の張り出し（浮島と同じ「芝生の上面＋岩の裏側」）を作る。何度実行してもOK（作り直し）。
// 横風で押し出されても「落ちる」ではなく「流される」になり、人も芝生に出て追いかけて押し戻せる（2026-10-09 てじゃ案）。
// ・芝生の上面は道の上面より少し低く、内側は道の下にもぐらせる（つなぎ目は道の丸いふちがかぶさる）
// ・当たり判定は道と同じ layer 11（ふわふわは触るとアウト、道の外に出ても真下に芝生があればセーフ）
public static class FuwaCourse1Shoulders
{
    // 横風ごとの張り出し：side（-1=左, 1=右）、道に沿った範囲 s、いちばん広い所の幅
    public static readonly (string name, float side, float s0, float s1, float width)[] Shoulders =
    {
        ("ShoulderL", -1f, 26f, 38f, 3f),   // 1つ目の横風（左へ押す）
        ("ShoulderR",  1f, 39f, 47.8f, 3f),   // 2つ目の横風（右へ押す）。CP1の光るキノコの丸太（s47.8〜）の手前まで
    };
    const float Ramp = 3f;            // 両端で幅が 0 → 最大になるまでの長さ
    const float Sink = 0.03f;         // 芝生の上面は道の上面よりこれだけ低い
    const float Tuck = 0.35f;         // 内側を道の下へもぐらせる幅
    const float EdgeR = 0.06f;        // 芝生の外のふちの丸み
    const float Step = 0.25f;         // 道に沿った刻み
    const int Across = 6;             // 横の刻み

    [MenuItem("FuwaCourse/済み（押す前に相談）/Course1/横風の張り出しを作る")]
    public static void RunMenu() { Debug.Log(Run()); }

    // 張り出しの外側までの幅（道のふちから）。両端はなめらかに 0 へ、ふちは少しうねらせる
    public static float Extra(int idx, float s)
    {
        var sh = Shoulders[idx];
        if (s <= sh.s0 || s >= sh.s1) return 0f;
        float a = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(sh.s0, sh.s0 + Ramp, s)) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(sh.s1, sh.s1 - Ramp, s));
        float wob = 1f + 0.07f * Mathf.Sin(s * 1.3f + idx * 2f) + 0.05f * Mathf.Sin(s * 2.9f + 1f + idx);
        return sh.width * Mathf.Sqrt(a) * wob;   // sqrt で端が丸くふくらむ（とがらない）
    }

    public static string Run()
    {
        var root = FuwaCourse1Builder.Root();
        var m = FuwaCourse1Builder.ToCourse(root);
        var grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Goal_Ground.mat");
        var rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/IslandUnder.mat");
        var pathGo = root.Find("PathMesh").gameObject;
        var holder = root.Find("CrossShoulders");
        if (holder == null) { holder = new GameObject("CrossShoulders").transform; holder.SetParent(root, false); Undo.RegisterCreatedObjectUndo(holder.gameObject, "shoulders"); }
        holder.localPosition = Vector3.zero; holder.localRotation = Quaternion.identity; holder.localScale = Vector3.one;
        var log = new System.Text.StringBuilder();

        for (int idx = 0; idx < Shoulders.Length; idx++)
        {
            var sh = Shoulders[idx];
            // 道に沿った断面の列（両端は幅 0 になる所まで）
            var ss = new List<float>();
            for (float s = sh.s0; s < sh.s1; s += Step) ss.Add(s);
            ss.Add(sh.s1);
            float Edge(float s) { return FuwaCourse1Builder.Width(0, s) * 0.5f; }
            // lateral（道の中心から右が+）とdy（道の上面から）→ コースのローカル
            Vector3 P(float s, float lat, float dy) { return m.MultiplyPoint3x4(FuwaCourse1Builder.Point(0, s, lat, dy)); }
            float Lat(float s, float fromEdge) { return sh.side * (Edge(s) + fromEdge); }

            // ---- 芝生の上面（＋外のふちの丸み） ----
            var gv = new List<Vector3>(); var gn = new List<Vector3>(); var gt = new List<int>();
            int cols = Across + 1 + 3;   // 上面 Across+1 列 ＋ 丸み3列
            foreach (var s in ss)
            {
                float E = Extra(idx, s);
                for (int k = 0; k <= Across; k++)
                {
                    float f = Mathf.Lerp(-Tuck, Mathf.Max(0f, E - EdgeR), k / (float)Across);
                    gv.Add(P(s, Lat(s, f), -Sink)); gn.Add(Vector3.up);
                }
                for (int k = 1; k <= 3; k++)
                {
                    float th = (Mathf.PI / 2) * k / 3f;
                    float f = Mathf.Max(0f, E - EdgeR) + EdgeR * Mathf.Sin(th) * Mathf.Min(1f, E / EdgeR);
                    gv.Add(P(s, Lat(s, f), -Sink - EdgeR * (1 - Mathf.Cos(th))));
                    var outDir = (P(s, Lat(s, 1f), 0) - P(s, Lat(s, 0f), 0)).normalized;
                    gn.Add((Vector3.up * Mathf.Cos(th) + outDir * Mathf.Sin(th)).normalized);
                }
            }
            for (int i = 0; i < ss.Count - 1; i++)
                for (int k = 0; k < cols - 1; k++)
                {
                    int a = i * cols + k, b = a + 1, c = a + cols, d = c + 1;
                    if (sh.side < 0) { gt.Add(a); gt.Add(b); gt.Add(d); gt.Add(a); gt.Add(d); gt.Add(c); }
                    else { gt.Add(a); gt.Add(d); gt.Add(b); gt.Add(a); gt.Add(c); gt.Add(d); }
                }

            // ---- 岩の裏側（浮島 IslandUnder と同じ色：上に土の帯、下へ行くほど暗い紫） ----
            var rv = new List<Vector3>(); var rn = new List<Vector3>(); var rc = new List<Color>(); var rt = new List<int>();
            // 断面：(道のふちからの横位置の割合, 深さの割合)。外のふち（丸みの下）から下へすぼまり、最後は道の下にもぐる
            var prof = new (float fx, float fy, float addY)[]
            {
                (1.00f, 0.00f, EdgeR), (0.99f, 0.10f, EdgeR + 0.04f), (0.94f, 0.28f, 0f), (0.80f, 0.52f, 0f), (0.58f, 0.76f, 0f), (0.32f, 0.93f, 0f), (0.08f, 1.00f, 0f),
            };
            Color soilA = new Color(0.27f, 0.15f, 0.08f), soilB = new Color(0.20f, 0.11f, 0.07f), rockTop = new Color(0.39f, 0.30f, 0.45f), rockBot = new Color(0.18f, 0.13f, 0.25f);
            int rcols = prof.Length + 1;
            foreach (var s in ss)
            {
                float E = Extra(idx, s), depth = Mathf.Lerp(0.25f, 1.6f, Mathf.Clamp01(E / sh.width)) * Mathf.Clamp01(E / 0.4f);
                for (int k = 0; k < prof.Length; k++)
                {
                    var (fx, fy, addY) = prof[k];
                    float n = k == 0 ? 0f : (Mathf.PerlinNoise(s * 0.9f + idx * 13f, k * 1.7f) - 0.5f) * 0.25f * Mathf.Clamp01(E);
                    float y = -Sink - (k <= 1 ? addY : 0f) - (k <= 1 ? fy * 0.35f : fy * depth);
                    rv.Add(P(s, Lat(s, fx * E + n), y));
                    float dd = Mathf.InverseLerp(0.1f, 1f, fy);
                    rc.Add(k <= 1 ? (k == 0 ? soilA : soilB) : Color.Lerp(rockTop, rockBot, dd));
                }
                // 最後は道の下（道の裏面の高さ）へ
                rv.Add(P(s, Lat(s, -Tuck), -FuwaCourse1Builder.Thick + 0.02f)); rc.Add(rockTop);
            }
            for (int i = 0; i < ss.Count - 1; i++)
                for (int k = 0; k < rcols - 1; k++)
                {
                    int a = i * rcols + k, b = a + 1, c = a + rcols, d = c + 1;
                    if (sh.side > 0) { rt.Add(a); rt.Add(d); rt.Add(b); rt.Add(a); rt.Add(c); rt.Add(d); }
                    else { rt.Add(a); rt.Add(b); rt.Add(d); rt.Add(a); rt.Add(d); rt.Add(c); }
                }
            // 両端のふた（幅がほぼ 0 なので小さい）は、断面を扇形にふさぐ
            foreach (int i in new[] { 0, ss.Count - 1 })
            {
                int b0 = i * rcols; bool front = i == 0;
                for (int k = 1; k < rcols - 1; k++)
                {
                    if ((sh.side > 0) == front) { rt.Add(b0); rt.Add(b0 + k + 1); rt.Add(b0 + k); }
                    else { rt.Add(b0); rt.Add(b0 + k); rt.Add(b0 + k + 1); }
                }
            }

            var gm = MakeMesh("Course1_" + sh.name + "_Grass", gv, gn, null, gt, false);
            Unwrapping.GenerateSecondaryUVSet(gm);
            var rm = MakeMesh("Course1_" + sh.name + "_Rock", rv, null, rc, rt, true);

            // ---- 当たり判定：上面の外形を厚み0.3で閉じた板（道と同じ layer） ----
            var cv = new List<Vector3>(); var ct = new List<int>();
            foreach (var s in ss)
            {
                float E = Extra(idx, s);
                cv.Add(P(s, Lat(s, -Tuck), -0.01f)); cv.Add(P(s, Lat(s, E), -0.01f));
                cv.Add(P(s, Lat(s, E), -0.3f)); cv.Add(P(s, Lat(s, -Tuck), -0.3f));
            }
            for (int i = 0; i < ss.Count - 1; i++)
                for (int k = 0; k < 4; k++)
                {
                    int a = i * 4 + k, b = i * 4 + (k + 1) % 4, c = a + 4, d = b + 4;
                    if (sh.side < 0) { ct.Add(a); ct.Add(b); ct.Add(d); ct.Add(a); ct.Add(d); ct.Add(c); }
                    else { ct.Add(a); ct.Add(d); ct.Add(b); ct.Add(a); ct.Add(c); ct.Add(d); }
                }
            var cm = MakeMesh("Course1_" + sh.name + "_Col", cv, null, null, ct, true);

            gm = Save(gm); rm = Save(rm); cm = Save(cm);

            var gt0 = holder.Find(sh.name);
            var go = gt0 != null ? gt0.gameObject : null;
            if (go == null) { go = new GameObject(sh.name); go.transform.SetParent(holder, false); Undo.RegisterCreatedObjectUndo(go, "shoulders"); }
            go.layer = pathGo.layer;
            var grassGo = Child(go.transform, "Grass", gm, grass);
            GameObjectUtility.SetStaticEditorFlags(grassGo, GameObjectUtility.GetStaticEditorFlags(pathGo));
            var rockGo = Child(go.transform, "Rock", rm, rock);
            rockGo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var mc = go.GetComponent<MeshCollider>();
            if (mc == null) mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = null; mc.sharedMesh = cm;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            log.Append(sh.name + " verts " + (gm.vertexCount + rm.vertexCount) + " ");
        }
        // 張り出しの上にある吹き流し・かざぐるまは、張り出しの外のふちへ移す（道に沿った位置 s と高さはそのまま）
        var deco = root.Find("WindDeco");
        if (deco != null)
            foreach (Transform t in deco)
            {
                float s = NearestS(m, root.InverseTransformPoint(t.position), out float lat, out float dy);
                for (int idx = 0; idx < Shoulders.Length; idx++)
                {
                    var sh = Shoulders[idx]; float E = Extra(idx, s);
                    if (E < 0.8f || Mathf.Sign(lat) != sh.side) continue;
                    Undo.RecordObject(t, "shoulders");
                    t.localPosition = m.MultiplyPoint3x4(FuwaCourse1Builder.Point(0, s, sh.side * (FuwaCourse1Builder.Width(0, s) * 0.5f + E - 0.45f), dy));
                    log.Append(t.name + "→ふち ");
                }
            }
        log.Append(PlantDeco(root, m, holder));
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // 芝生の外のふちに沿って草・花・小石のかたまり（道のふちの飾りと同じプレハブ）。内側にも花をぽつぽつ
    static string PlantDeco(Transform root, Matrix4x4 m, Transform holder)
    {
        var rnd = new System.Random(919);
        float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
        GameObject Pf(string n) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FuwaCourse/Prefabs/Deco/Deco_" + n + ".prefab");
        // 吹き流し・かざぐるまの足元はあける
        var avoid = new List<Vector3>();
        var wd = root.Find("WindDeco"); if (wd != null) foreach (Transform t in wd) avoid.Add(root.InverseTransformPoint(t.position));
        int items = 0;
        for (int idx = 0; idx < Shoulders.Length; idx++)
        {
            var sh = Shoulders[idx];
            var dh = holder.Find(sh.name + "/Deco"); if (dh != null) Undo.DestroyObjectImmediate(dh.gameObject);
            dh = new GameObject("Deco").transform; dh.SetParent(holder.Find(sh.name), false); Undo.RegisterCreatedObjectUndo(dh.gameObject, "shoulders");
            var occupied = new List<(Vector3 p, float r)>();
            void Put(string name, float s, float fromEdge, float sc)
            {
                float E = Extra(idx, s); if (fromEdge > E - 0.12f || fromEdge < 0.25f) return;   // 道のすぐ横（道の飾りと重なる所）と芝生の外には置かない
                var lp = m.MultiplyPoint3x4(FuwaCourse1Builder.Point(0, s, sh.side * (FuwaCourse1Builder.Width(0, s) * 0.5f + fromEdge), -Sink - 0.01f));
                float clear = (name == "GrassSmall" ? 0.11f : name == "GrassLarge" ? 0.26f : name == "Pebbles" ? 0.25f : 0.16f) * sc;
                foreach (var a in avoid) if (new Vector2(a.x - lp.x, a.z - lp.z).magnitude < 0.45f + clear) return;
                foreach (var o in occupied) if (new Vector2(o.p.x - lp.x, o.p.z - lp.z).magnitude < o.r + clear + 0.02f) return;
                occupied.Add((lp, clear));
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(Pf(name), dh);
                inst.transform.position = root.TransformPoint(lp);
                inst.transform.rotation = root.rotation * Quaternion.Euler(0, R(0, 360), 0);
                inst.transform.localScale = Vector3.one * sc;
                foreach (var t in inst.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                items++;
            }
            // ふちのかたまり
            for (float s = sh.s0 + 1.2f; s < sh.s1 - 1f; s += R(1.6f, 2.8f))
            {
                int n = rnd.Next(2, 5);
                for (int k = 0; k < n; k++)
                {
                    float pick = (float)rnd.NextDouble();
                    string name = pick < 0.35f ? "GrassSmall" : pick < 0.6f ? "GrassLarge" : pick < 0.88f ? new[] { "FlowersWhite", "FlowersYellow", "FlowersPink" }[rnd.Next(3)] : "Pebbles";
                    float ss = s + R(-0.6f, 0.6f);
                    Put(name, ss, Extra(idx, ss) - R(0.2f, 0.6f), R(0.8f, 1.25f) * 1.6f);
                }
            }
            // 内側の花（まばら）
            for (int k = 0; k < 7; k++)
            {
                float ss = R(sh.s0 + 2f, sh.s1 - 2f);
                Put(new[] { "FlowersWhite", "FlowersYellow", "FlowersPink", "GrassSmall" }[rnd.Next(4)], ss, R(0.6f, Extra(idx, ss) - 0.8f), R(0.8f, 1.2f) * 1.5f);
            }
        }
        return "deco " + items + " ";
    }

    // コースのローカルの点に一番近い道（かたまり0）の s と、横位置・高さ
    static float NearestS(Matrix4x4 m, Vector3 lp, out float lat, out float dy)
    {
        float best = 1e9f, bs = 0f; lat = 0f;
        for (float s = 0f; s < 60f; s += 0.05f)
        {
            var c = m.MultiplyPoint3x4(FuwaCourse1Builder.Point(0, s, 0, 0));
            var r = m.MultiplyPoint3x4(FuwaCourse1Builder.Point(0, s, 1, 0)) - c;
            var d = lp - c; float l = Vector3.Dot(d, r); var off = d - r * l; off.y = 0;
            if (off.sqrMagnitude < best) { best = off.sqrMagnitude; bs = s; lat = l; }
        }
        dy = lp.y - m.MultiplyPoint3x4(FuwaCourse1Builder.Point(0, bs, 0, 0)).y;
        return bs;
    }

    static Mesh MakeMesh(string name, List<Vector3> v, List<Vector3> n, List<Color> c, List<int> t, bool recalcNormals)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v);
        if (c != null) mesh.SetColors(c);
        mesh.SetTriangles(t, 0);
        if (n != null) mesh.SetNormals(n); else if (recalcNormals) mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // 既存アセットは Clear → Set 系で入れ直す（CopySerialized だと描画が古いまま・サブメッシュが消える罠）
    static Mesh Save(Mesh m)
    {
        string dir = "Assets/FuwaCourse/Meshes/Shoulders";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "Shoulders");
        string p = dir + "/" + m.name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (old == null) { AssetDatabase.CreateAsset(m, p); return m; }
        old.Clear();
        old.SetVertices(m.vertices);
        if (m.colors.Length > 0) old.SetColors(m.colors);
        old.SetNormals(m.normals);
        if (m.uv2.Length > 0) old.uv2 = m.uv2;
        old.SetTriangles(m.triangles, 0);
        old.RecalculateBounds(); old.UploadMeshData(false);
        EditorUtility.SetDirty(old);
        return old;
    }

    static GameObject Child(Transform parent, string name, Mesh mesh, Material mat)
    {
        var t = parent.Find(name);
        GameObject go;
        if (t == null) { go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false); Undo.RegisterCreatedObjectUndo(go, "shoulders"); }
        else go = t.gameObject;
        go.layer = parent.gameObject.layer;
        go.GetComponent<MeshFilter>().sharedMesh = null;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }
}
