using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// コース2「いわやま」全部盛り版の道を生成する。
// 中心線はスタート(Start)のローカル座標で定義：z=1 から +z 向きに出発、s=道に沿った距離。
// 道は3つのかたまり：ふもと〜崖下 / 崖の上〜崖ダイブの縁 / 下の台地。崖とすき間は岩(Blender)で埋める。
public static class FuwaCourse2Builder
{
    public const float Top = 3f, Thick = 0.3f, Bevel = 0.08f;
    const int BevelSteps = 4;
    const float Stripe = 0.75f;

    // 中心線：直線0-18 → 右カーブR12(18-26) → 直線26-54 → 左カーブR14(54-66) → 直線66-
    public const float TurnR1 = 12f, TurnS1a = 18f, TurnS1b = 26f;
    public const float TurnR2 = 14f, TurnS2a = 54f, TurnS2b = 66f;
    // 入口のトンネル（s 9〜17）を左へ曲げて、その先のコース全体を左へ振る（ゴールの位置を変えるため。2026-10-03）
    public const float BendA = 9f, BendB = 17f, BendDeg = 15f;
    public static bool BendOn = true;   // false で曲げる前の中心線（古い置き場所からの移し替え用）

    // かたまりごとの s 範囲
    // 3 = 崖ダイブの横の人用の坂（道の左に並べる）
    // 4,5 = 転がる岩が落ちる穴（坂のふもと）の左右の細い道
    public const float PitS0 = 25.0f, PitS1 = 29.0f, PitHalf = 2.0f, PitSide = 2.6f;
    public static readonly Vector2[] Pieces = { new Vector2(0f, 46f), new Vector2(46f, 72f), new Vector2(79f, 87f), new Vector2(68.5f, 80f), new Vector2(PitS0, PitS1), new Vector2(PitS0, PitS1) };
    public const float RampWidth = 2.2f;
    public static float Offset(int piece)
    {
        if (piece == 3) return -(1.5f + RampWidth * 0.5f);   // 実際の横位置は RampShift で s ごとに左へふくらむ
        if (piece == 4) return -(PitHalf + PitSide * 0.5f);
        if (piece == 5) return PitHalf + PitSide * 0.5f;
        return 0f;
    }

    // メッシュを作る範囲（かたまり0は穴の所を抜く）
    public static Vector2[] Spans(int piece)
    {
        if (piece == 0) return new[] { new Vector2(0f, RegionA), new Vector2(RegionB, 46f) };
        if (piece == 4 || piece == 5) return new Vector2[0];   // 穴の所は AddRegion で作る
        if (piece == 1 && AssetDatabase.LoadAssetAtPath<Mesh>(DiveEndMeshPath) != null) return new[] { new Vector2(46f, DiveEndA) };
        return new[] { Pieces[piece] };
    }

    // 高さ（道の上面 - Top）のキー。かたまりごと
    static readonly Vector2[][] HeightKeys =
    {
        new[] { new Vector2(0, 0), new Vector2(8, 0), new Vector2(18, 0.5f), new Vector2(26, 1.0f), new Vector2(42, 5.8f), new Vector2(46, 5.8f) },
        new[] { new Vector2(46, 9.8f), new Vector2(54, 9.8f), new Vector2(66, 6.8f), new Vector2(72, 6.0f) },
        new[] { new Vector2(79, 0f), new Vector2(87, 0f) },
        new[] { new Vector2(68.5f, 6.47f), new Vector2(69.5f, 6.33f), new Vector2(80f, 0f) },
        null, null,
    };

    public static float PitFactor(float s)
    {
        return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(21.8f, 24.8f, s)) * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(29.2f, 32.2f, s)));
    }

    // かたまり0の中心線を穴の所だけ右へずらす量
    public static float PitShift(int piece, float s)
    {
        if (piece == 3) return -1.1f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((s - 68.5f) / (80f - 68.5f)));   // 人用の坂：道から左へ離れて、台地の左へ降りる
        return piece == 0 ? PitSide * 0.5f * PitFactor(s) : 0f;
    }

    public static float Width(int piece, float s)
    {
        if (piece == 3) return RampWidth;
        if (piece == 4 || piece == 5) return PitSide;
        return Width(s);
    }

    public static float Width(float s)
    {
        // 転がる岩の坂は広め（左右によけられる）、下の台地は人用の坂が降りてくるので広め
        if (s >= 79f) return 7.6f;
        if (s >= 46f) return 3f;
        // 坂と坂の上の踊り場（洞窟の口の前）は幅4
        float w = 3f;
        w += 1f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(24f, 27f, s));
        // 穴の前後は右の細い道につながるよう、右側だけ広げる（中心は PitShift で右へずらす）
        w = Mathf.Lerp(w, 2f * PitHalf + PitSide, PitFactor(s));
        return w;
    }

    public static float Height(int piece, float s)
    {
        // 折れ線を ±1m の箱フィルタでならす（坂の始まり・終わりを丸める）
        var keys = HeightKeys[piece] ?? HeightKeys[0];
        float sum = 0; int n = 9;
        for (int i = 0; i < n; i++)
        {
            float x = s + Mathf.Lerp(-1f, 1f, i / (float)(n - 1));
            sum += Linear(keys, x);
        }
        return sum / n;
    }

    static float Linear(Vector2[] k, float x)
    {
        if (x <= k[0].x) return k[0].y;
        for (int i = 1; i < k.Length; i++)
            if (x <= k[i].x) return Mathf.Lerp(k[i - 1].y, k[i].y, (x - k[i - 1].x) / (k[i].x - k[i - 1].x));
        return k[k.Length - 1].y;
    }

    // 中心線（Startローカルの xz）と進行方向
    public static void Center(float s, out Vector2 p, out Vector2 d)
    {
        p = new Vector2(0, 1); d = new Vector2(0, 1);
        float heading = 0f; // 右回りが+（ラジアン）
        // 区間を順に進める
        float[] seg = { BendA, BendB, TurnS1a, TurnS1b, TurnS2a, TurnS2b, 1e9f };
        float cur = 0f;
        for (int i = 0; i < seg.Length && cur < s; i++)
        {
            float end = Mathf.Min(seg[i], s);
            float len = end - cur;
            float curv = (i == 1) ? (BendOn ? -BendDeg * Mathf.Deg2Rad / (BendB - BendA) : 0f) : (i == 3) ? 1f / TurnR1 : (i == 5) ? -1f / TurnR2 : 0f;
            if (curv == 0f)
            {
                p += new Vector2(Mathf.Sin(heading), Mathf.Cos(heading)) * len;
            }
            else
            {
                float h2 = heading + curv * len;
                float r = 1f / curv;
                p += new Vector2(Mathf.Cos(heading) - Mathf.Cos(h2), Mathf.Sin(h2) - Mathf.Sin(heading)) * r;
                heading = h2;
            }
            cur = end;
        }
        d = new Vector2(Mathf.Sin(heading), Mathf.Cos(heading));
    }

    // Startローカルの点（横x, 高さy=上面からの差, s）
    public static Vector3 Point(int piece, float s, float lateral, float dy)
    {
        Center(s, out var p, out var d);
        var r = new Vector2(d.y, -d.x);
        var q = p + r * (lateral + Offset(piece) + PitShift(piece, s));
        return new Vector3(q.x, Top + Height(piece, s) + dy, q.y);
    }

    [MenuItem("FuwaCourse/Course2/Rebuild Path")]
    public static void RebuildMenu() { Debug.Log(Rebuild()); }

    public static string Rebuild()
    {
        var root = GameObject.Find("FuwaCourse2").transform;
        var start = root.Find("Start");
        Matrix4x4 toCourse = Matrix4x4.TRS(new Vector3(start.localPosition.x, 0, start.localPosition.z), Quaternion.Euler(0, start.localEulerAngles.y, 0), Vector3.one);

        var vis = new Mesh { name = "Course2Path_Bevel" };
        var col = new Mesh { name = "Course2Path" };
        BuildVisual(vis, toCourse);
        BuildCollider(col, toCourse);
        Save(vis, "Assets/FuwaCourse/Meshes/Course2Path_v2_Bevel.asset");
        Save(col, "Assets/FuwaCourse/Meshes/Course2Path_v2.asset");

        var pm = root.Find("PathMesh");
        pm.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Course2Path_v2_Bevel.asset");
        var mc = pm.GetComponent<MeshCollider>();
        mc.sharedMesh = null;
        mc.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Course2Path_v2.asset");
        EditorUtility.SetDirty(pm.gameObject);
        string planks = BuildRampPlanks(root, toCourse);
        return "Course2 path rebuilt: verts " + vis.vertexCount + " / " + planks;
    }

    // Startローカル → コースのローカル
    public static Matrix4x4 ToCourse(Transform root)
    {
        var start = root.Find("Start");
        return Matrix4x4.TRS(new Vector3(start.localPosition.x, 0, start.localPosition.z), Quaternion.Euler(0, start.localEulerAngles.y, 0), Vector3.one);
    }

    public static float Yaw(Transform root, float s)
    {
        Center(s, out _, out var d);
        return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg + root.Find("Start").localEulerAngles.y;
    }

    public const float CP1 = 18.5f, CP2 = 50f, GoalS = 89.5f;

    [MenuItem("FuwaCourse/Course2/Place Objects")]
    public static void PlaceMenu() { Debug.Log(PlaceObjects()); }

    public static string PlaceObjects()
    {
        var root = GameObject.Find("FuwaCourse2").transform;
        var m = ToCourse(root);
        var log = "";
        void PlaceCp(string name, int piece, float s)
        {
            var cp = root.Find(name);
            Undo.RecordObject(cp, "place");
            cp.localPosition = m.MultiplyPoint3x4(Point(piece, s, 0, 0));
            cp.localRotation = Quaternion.Euler(0, Yaw(root, s), 0);
            // 光るキノコ：判定の3m先、道のふちの外（ふちから8cm、丸太のてっぺん=道+12cm）
            float sa = s + 3f, half = Width(piece, sa) * 0.5f + 0.08f + 0.45f;
            foreach (var (cn, side, yaw) in new[] { ("ClusterL", -1f, 69.9f), ("ClusterR", 1f, -69.9f) })
            {
                var c = cp.Find("GlowMushrooms/" + cn);
                if (c == null) continue;
                Undo.RecordObject(c, "place");
                var wp = root.TransformPoint(m.MultiplyPoint3x4(Point(piece, sa, side * half, 0)));
                c.position = wp + Vector3.up * -0.66f;
                c.rotation = root.rotation * Quaternion.Euler(0, Yaw(root, sa) + yaw, 0);
            }
            log += name + " " + cp.localPosition.ToString("F2") + "  ";
        }
        PlaceCp("Checkpoint1", 0, CP1);
        PlaceCp("Checkpoint2", 1, CP2);

        var goal = root.Find("Goal");
        Undo.RecordObject(goal, "place");
        var gp = m.MultiplyPoint3x4(Point(2, GoalS, 0, 0));
        goal.localPosition = gp + Vector3.down * 0.02f;
        goal.localRotation = Quaternion.Euler(0, Yaw(root, 87f), 0);

        // 落下キャッチ：道全体の下
        var fc = root.Find("FallCatcher");
        var b = root.Find("PathMesh").GetComponent<MeshFilter>().sharedMesh.bounds;
        Undo.RecordObject(fc, "place");
        fc.localRotation = Quaternion.identity;
        fc.localPosition = new Vector3(b.center.x, -6f, b.center.z);
        fc.localScale = new Vector3(b.size.x + 20f, 2f, b.size.z + 20f);
        return log + "goal " + goal.localPosition.ToString("F2");
    }

    // Blender（FLUFF_models.blend の C2Rock）で作った岩を JSON から取り込む。
    // key=JSONのキー, name=Rocks2の下のオブジェクト名, layer=11なら「地面」（ふわふわが触るとアウト）
    public static string ImportRocks(string jsonPath, string[] keys, string[] names, int[] layers)
    {
        var json = System.IO.File.ReadAllText(jsonPath);
        var root = GameObject.Find("FuwaCourse2").transform;
        var holder = root.Find("Rocks2");
        if (holder == null) { holder = new GameObject("Rocks2").transform; holder.SetParent(root, false); Undo.RegisterCreatedObjectUndo(holder.gameObject, "rocks"); }
        var rockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Rock.mat");
        string log = "";
        for (int n = 0; n < keys.Length; n++)
        {
            string key = keys[n];
            int k = json.IndexOf("\"" + key + "\"");
            if (k < 0) { log += key + " missing  "; continue; }
            int vi = json.IndexOf("\"v\"", k), ti = json.IndexOf("\"t\"", vi), end = json.IndexOf("}", ti);
            var vs = Nums(json.Substring(vi + 3, ti - vi - 3));
            var ts = Nums(json.Substring(ti + 3, end - ti - 3));
            // 見た目：三角形ごとに頂点を分けてフラットシェーディング
            var V = new List<Vector3>(); var T = new List<int>();
            for (int i = 0; i < ts.Count; i++) { int idx = (int)ts[i]; V.Add(new Vector3(vs[idx * 3], vs[idx * 3 + 1], vs[idx * 3 + 2])); T.Add(i); }
            var vis = new Mesh { name = "Course2_" + key, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            vis.SetVertices(V); vis.SetTriangles(T, 0); vis.RecalculateNormals(); vis.RecalculateBounds();
            var col = new Mesh { name = "Course2_" + key + "_Col", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var cv = new List<Vector3>(); for (int i = 0; i + 2 < vs.Count; i += 3) cv.Add(new Vector3(vs[i], vs[i + 1], vs[i + 2]));
            var ct = new List<int>(); foreach (var f in ts) ct.Add((int)f);
            col.SetVertices(cv); col.SetTriangles(ct, 0); col.RecalculateNormals(); col.RecalculateBounds();
            Save(vis, "Assets/FuwaCourse/Meshes/Course2_" + key + ".asset");
            Save(col, "Assets/FuwaCourse/Meshes/Course2_" + key + "_Col.asset");
            var t = holder.Find(names[n]);
            GameObject go = t != null ? t.gameObject : null;
            if (go == null) { go = new GameObject(names[n], typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)); go.transform.SetParent(holder, false); Undo.RegisterCreatedObjectUndo(go, "rocks"); }
            go.layer = layers[n];
            go.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Course2_" + key + ".asset");
            go.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
            var mc = go.GetComponent<MeshCollider>(); mc.sharedMesh = null;
            mc.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Course2_" + key + "_Col.asset");
            go.isStatic = true;
            log += names[n] + " tris " + (T.Count / 3) + "  ";
        }
        return log;
    }


    // ---- 穴の所の板を Blender でくり抜く方式 ----
    public const string PitMeshPath = "Assets/FuwaCourse/Meshes/Course2Path_Pit.asset";

    static bool AppendPitMesh(List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<int> A, List<int> Bt)
    {
        var pm = AssetDatabase.LoadAssetAtPath<Mesh>(PitMeshPath);
        if (pm == null) return false;
        int off = V.Count;
        V.AddRange(pm.vertices); N.AddRange(pm.normals);
        var uv = pm.uv; if (uv.Length == pm.vertexCount) UV.AddRange(uv); else for (int i = 0; i < pm.vertexCount; i++) UV.Add(Vector2.zero);
        foreach (var t in pm.GetTriangles(0)) A.Add(t + off);
        if (pm.subMeshCount > 1) foreach (var t in pm.GetTriangles(1)) Bt.Add(t + off);
        return true;
    }

    // 穴の所（RegionA〜RegionB）の、穴のない閉じた板と、穴の形・中心線を JSON に書き出す（Blender用、コースのローカル座標）
    public static string ExportPitSlab(string path)
    {
        var root = GameObject.Find("FuwaCourse2").transform;
        var m = ToCourse(root);
        var V = new List<Vector3>(); var T = new List<int>();
        const float step = 0.125f;
        int n = Mathf.RoundToInt((RegionB - RegionA) / step);
        int P = 0;
        for (int k = 0; k <= n; k++)
        {
            float s = RegionA + k * step;
            var prof = Profile(Width(0, s)); P = prof.Count;
            foreach (var q in prof) V.Add(m.MultiplyPoint3x4(Point(0, s, q.x, q.y)));
        }
        for (int k = 0; k < n; k++)
        {
            int a = k * P, b = (k + 1) * P;
            for (int i = 0; i < P; i++) { int i1 = (i + 1) % P; T.AddRange(new[] { a + i, b + i, a + i1, a + i1, b + i, b + i1 }); }
        }
        // 両端のふた
        int last = n * P;
        for (int i = 1; i < P - 1; i++) { T.AddRange(new[] { 0, i + 1, i }); T.AddRange(new[] { last, last + i, last + i + 1 }); }
        var sb = new System.Text.StringBuilder();
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        sb.Append("{\"v\":[");
        for (int i = 0; i < V.Count; i++) { if (i > 0) sb.Append(","); sb.Append(string.Format(ci, "[{0:F4},{1:F4},{2:F4}]", V[i].x, V[i].y, V[i].z)); }
        sb.Append("],\"t\":[");
        for (int i = 0; i < T.Count; i++) { if (i > 0) sb.Append(","); sb.Append(T[i]); }
        // 穴の輪郭（コースのローカル xz）
        sb.Append("],\"hole\":[");
        for (int i = 0; i < 160; i++)
        {
            float th = 2f * Mathf.PI * i / 160;
            var h = HolePoint(th);
            var q = m.MultiplyPoint3x4(Point(0, h.x, h.y - PitShift(0, h.x), 0));
            if (i > 0) sb.Append(",");
            sb.Append(string.Format(ci, "[{0:F4},{1:F4},{2:F4}]", q.x, q.y, q.z));
        }
        // 中心線（s, 点, 進行方向）
        sb.Append("],\"center\":[");
        bool first = true;
        for (float s = RegionA - 0.5f; s <= RegionB + 0.5f; s += 0.05f)
        {
            Center(s, out _, out var d);
            var c = m.MultiplyPoint3x4(Point(0, s, 0, 0));
            var dir = m.MultiplyVector(new Vector3(d.x, 0, d.y));
            if (!first) sb.Append(","); first = false;
            sb.Append(string.Format(ci, "[{0:F3},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4}]", s, c.x, c.y, c.z, dir.x, dir.z));
        }
        sb.Append("],\"stripe\":").Append(Stripe.ToString(ci)).Append(",\"sa\":").Append(RegionA.ToString(ci)).Append(",\"sb\":").Append(RegionB.ToString(ci)).Append("}");
        System.IO.File.WriteAllText(path, sb.ToString());
        return "slab verts " + V.Count + " tris " + T.Count / 3;
    }

    // Blender で作った穴の板（三角形ごとの頂点・法線・材質）を取り込む
    public static string ImportPitMesh(string path)
    {
        var json = System.IO.File.ReadAllText(path);
        int ip = json.IndexOf("\"p\""), inn = json.IndexOf("\"n\""), im = json.IndexOf("\"s\"");
        var Pn = Nums(json.Substring(ip + 3, inn - ip - 3)); var Nn = Nums(json.Substring(inn + 3, im - inn - 3)); var Sn = Nums(json.Substring(im + 3));
        var V = new List<Vector3>(); var N = new List<Vector3>(); var A = new List<int>(); var Bt = new List<int>();
        for (int t = 0; t < Sn.Count; t++)
        {
            int b = V.Count;
            foreach (var k in new[] { 0, 2, 1 }) { int i = (t * 3 + k) * 3; V.Add(new Vector3(Pn[i], Pn[i + 1], Pn[i + 2])); N.Add(new Vector3(Nn[i], Nn[i + 1], Nn[i + 2])); }
            ((int)Sn[t] == 0 ? A : Bt).AddRange(new[] { b, b + 1, b + 2 });
        }
        var mesh = new Mesh { name = "Course2Path_Pit", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V); mesh.SetNormals(N); mesh.subMeshCount = 2; mesh.SetTriangles(A, 0); mesh.SetTriangles(Bt, 1); mesh.RecalculateBounds();
        Save(mesh, PitMeshPath);
        return "pit tris " + Sn.Count;
    }


    // ---- 崖ダイブの縁を崩れた形に（Blender でギザギザにくり抜く） ----
    public const float DiveEndA = 69.0f, DiveEndB = 72.0f;
    public const string DiveEndMeshPath = "Assets/FuwaCourse/Meshes/Course2Path_DiveEnd.asset";

    static bool AppendMesh(string path, List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<int> A, List<int> Bt)
    {
        var pm = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (pm == null) return false;
        int off = V.Count;
        V.AddRange(pm.vertices); N.AddRange(pm.normals);
        for (int i = 0; i < pm.vertexCount; i++) UV.Add(Vector2.zero);
        foreach (var t in pm.GetTriangles(0)) A.Add(t + off);
        if (pm.subMeshCount > 1) foreach (var t in pm.GetTriangles(1)) Bt.Add(t + off);
        return true;
    }

    // かたまり1の DiveEndA〜DiveEndB の閉じた板と、先端を欠けさせる「くり抜き形」を書き出す
    public static string ExportDiveEndSlab(string path)
    {
        var root = GameObject.Find("FuwaCourse2").transform;
        var m = ToCourse(root);
        var V = new List<Vector3>(); var T = new List<int>();
        const float step = 0.125f;
        int n = Mathf.RoundToInt((DiveEndB - DiveEndA) / step);
        int P = 0;
        for (int k = 0; k <= n; k++)
        {
            float s = DiveEndA + k * step;
            var prof = Profile(Width(1, s)); P = prof.Count;
            foreach (var q in prof) V.Add(m.MultiplyPoint3x4(Point(1, s, q.x, q.y)));
        }
        for (int k = 0; k < n; k++)
        {
            int a = k * P, b = (k + 1) * P;
            for (int i = 0; i < P; i++) { int i1 = (i + 1) % P; T.AddRange(new[] { a + i, b + i, a + i1, a + i1, b + i, b + i1 }); }
        }
        int last = n * P;
        for (int i = 1; i < P - 1; i++) { T.AddRange(new[] { 0, i + 1, i }); T.AddRange(new[] { last, last + i, last + i + 1 }); }
        // くり抜き形：先端のギザギザ（横方向に沿って s が前後にガタつく線）から先を全部
        var rnd = new System.Random(21);
        var cut = new List<Vector2>();
        float half = Width(1, DiveEndB) * 0.5f + 0.6f;
        int cn = 14;
        for (int i = 0; i <= cn; i++)
        {
            float lat = Mathf.Lerp(-half, half, i / (float)cn);
            float sj = 70.9f + (float)rnd.NextDouble() * 1.0f - (Mathf.Abs(lat) > 1.2f ? 0.5f * (float)rnd.NextDouble() : 0f);
            cut.Add(new Vector2(sj, lat));
        }
        cut.Add(new Vector2(DiveEndB + 2f, half)); cut.Add(new Vector2(DiveEndB + 2f, -half));
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"v\":[");
        for (int i = 0; i < V.Count; i++) { if (i > 0) sb.Append(","); sb.Append(string.Format(ci, "[{0:F4},{1:F4},{2:F4}]", V[i].x, V[i].y, V[i].z)); }
        sb.Append("],\"t\":[");
        for (int i = 0; i < T.Count; i++) { if (i > 0) sb.Append(","); sb.Append(T[i]); }
        sb.Append("],\"hole\":[");
        for (int i = 0; i < cut.Count; i++)
        {
            var q = m.MultiplyPoint3x4(Point(1, cut[i].x, cut[i].y, 0));
            if (i > 0) sb.Append(",");
            sb.Append(string.Format(ci, "[{0:F4},{1:F4},{2:F4}]", q.x, q.y, q.z));
        }
        sb.Append("],\"center\":[");
        bool first = true;
        for (float s = DiveEndA - 0.5f; s <= DiveEndB + 2.5f; s += 0.05f)
        {
            Center(s, out _, out var d);
            var c = m.MultiplyPoint3x4(Point(1, s, 0, 0));
            var dir = m.MultiplyVector(new Vector3(d.x, 0, d.y));
            if (!first) sb.Append(","); first = false;
            sb.Append(string.Format(ci, "[{0:F3},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4}]", s, c.x, c.y, c.z, dir.x, dir.z));
        }
        sb.Append("],\"stripe\":").Append(Stripe.ToString(ci)).Append(",\"sa\":").Append(DiveEndA.ToString(ci)).Append(",\"sb\":").Append((DiveEndB + 5f).ToString(ci)).Append("}");
        System.IO.File.WriteAllText(path, sb.ToString());
        return "diveend slab verts " + V.Count;
    }

    public static string ImportMeshTo(string jsonPath, string assetPath)
    {
        var json = System.IO.File.ReadAllText(jsonPath);
        int ip = json.IndexOf("\"p\""), inn = json.IndexOf("\"n\""), im = json.IndexOf("\"s\"");
        var Pn = Nums(json.Substring(ip + 3, inn - ip - 3)); var Nn = Nums(json.Substring(inn + 3, im - inn - 3)); var Sn = Nums(json.Substring(im + 3));
        var V = new List<Vector3>(); var N = new List<Vector3>(); var A = new List<int>(); var Bt = new List<int>();
        for (int t = 0; t < Sn.Count; t++)
        {
            int b = V.Count;
            foreach (var k in new[] { 0, 2, 1 }) { int i = (t * 3 + k) * 3; V.Add(new Vector3(Pn[i], Pn[i + 1], Pn[i + 2])); N.Add(new Vector3(Nn[i], Nn[i + 1], Nn[i + 2])); }
            ((int)Sn[t] == 0 ? A : Bt).AddRange(new[] { b, b + 1, b + 2 });
        }
        var mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(assetPath), indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V); mesh.SetNormals(N); mesh.subMeshCount = 2; mesh.SetTriangles(A, 0); mesh.SetTriangles(Bt, 1); mesh.RecalculateBounds();
        Save(mesh, assetPath);
        return "tris " + Sn.Count;
    }


    // ---- 人用の坂：道が崩れたので後から渡した板張り ----
    // 横向きの板（すき間あり・長さと色を少しバラつかせる）＋両わきの角材＋下に地面がある所は柱。見た目だけ（当たり判定は道のなめらかな坂）
    const float PlankLen = 0.26f, PlankGap = 0.045f, PlankT = 0.07f, BeamW = 0.12f, BeamH = 0.2f, PostW = 0.13f;

    static Material WoodMat(string name, Color c)
    {
        string path = "Assets/FuwaCourse/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
        mat.color = c; mat.SetFloat("_Glossiness", 0.12f); EditorUtility.SetDirty(mat);
        return mat;
    }

    // 8つの角（下面 a0..a3、上面 b0..b3 の順でぐるっと）から、面ごとに頂点を分けた箱を足す
    static void AddBox(List<Vector3> V, List<int> T, Vector3[] c)
    {
        int[][] faces = { new[] { 4, 5, 6, 7 }, new[] { 3, 2, 1, 0 }, new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 } };
        var center = Vector3.zero; foreach (var q in c) center += q; center /= 8f;
        foreach (var f in faces)
        {
            int b = V.Count;
            foreach (var i in f) V.Add(c[i]);
            var fc = (c[f[0]] + c[f[1]] + c[f[2]] + c[f[3]]) * 0.25f;
            AddTriDir(V, T, b, b + 1, b + 2, fc - center); AddTriDir(V, T, b, b + 2, b + 3, fc - center);
        }
    }

    static string BuildRampPlanks(Transform root, Matrix4x4 m)
    {
        var rnd = new System.Random(7);
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        Vector3 P(float s, float lat, float dy) { return m.MultiplyPoint3x4(Point(3, s, lat, dy)); }
        var V = new List<Vector3>(); var TA = new List<int>(); var TB = new List<int>(); var TC = new List<int>();
        float s0 = Pieces[3].x, s1 = Pieces[3].y, hw = RampWidth * 0.5f;
        // 台地に着く所：板の下が台地にもぐらない所までで止める（同じ高さの面を作らない）
        float sEnd = s1;
        while (sEnd > s0 && Height(3, sEnd) < PlankT + 0.012f) sEnd -= 0.01f;
        int nPl = 0;
        for (float s = s0 + 0.02f; s + PlankLen <= sEnd; s += PlankLen + PlankGap)
        {
            float a = s + R(-0.012f, 0.012f), b = s + PlankLen + R(-0.012f, 0.012f);
            float l0 = -hw + 0.04f + R(-0.05f, 0.03f), l1 = hw - 0.04f + R(-0.03f, 0.05f);
            if (s < 70f) l1 = Mathf.Min(l1, hw - 0.05f);   // 道のとなりは道のふちにさわらない
            float lift = R(0f, 0.012f), tilt = R(-0.01f, 0.01f);
            AddBox(V, (R(0, 1) < 0.5f) ? TA : TB, new[] {
                P(a, l0, -PlankT + lift - tilt), P(b, l0, -PlankT + lift - tilt), P(b, l1, -PlankT + lift + tilt), P(a, l1, -PlankT + lift + tilt),
                P(a, l0, lift - tilt), P(b, l0, lift - tilt), P(b, l1, lift + tilt), P(a, l1, lift + tilt) });
            nPl++;
        }
        // 両わきの角材（板の下）。坂に沿って短い区間をつなぐ
        float bs0 = s0 + 0.05f, bs1 = sEnd - 0.03f;
        foreach (float side in new[] { -1f, 1f })
        {
            float lc = side * (hw - 0.22f);
            int n = Mathf.CeilToInt((bs1 - bs0) / 0.5f);
            for (int k = 0; k < n; k++)
            {
                float a = Mathf.Lerp(bs0, bs1, k / (float)n), b = Mathf.Lerp(bs0, bs1, (k + 1) / (float)n);
                float t0 = -PlankT - 0.002f, t1 = t0 - BeamH;
                // つなぎ目で隙間が出ないよう、少しだけ重ねる（同じ面にはならない：断面が同じ箱の続き）
                AddBox(V, TC, new[] {
                    P(a, lc - BeamW / 2, t1), P(b, lc - BeamW / 2, t1), P(b, lc + BeamW / 2, t1), P(a, lc + BeamW / 2, t1),
                    P(a, lc - BeamW / 2, t0), P(b, lc - BeamW / 2, t0), P(b, lc + BeamW / 2, t0), P(a, lc + BeamW / 2, t0) });
            }
        }
        // 柱：角材の下に地面（岩・台地）があれば立てる
        int nPost = 0;
        Physics.SyncTransforms();
        var toWorld = root.localToWorldMatrix;
        for (float s = 71f; s < sEnd - 0.8f; s += 2.1f)
        foreach (float side in new[] { -1f, 1f })
        {
            float lc = side * (hw - 0.22f);
            var topL = P(s, lc, -PlankT - BeamH);
            var topW = toWorld.MultiplyPoint3x4(topL);
            if (!Physics.Raycast(topW + Vector3.down * 0.05f, Vector3.down, out var hit, 9f, ~(1 << 2), QueryTriggerInteraction.Ignore)) continue;
            if (hit.distance < 0.25f) continue;
            float bottomY = root.InverseTransformPoint(hit.point).y - 0.3f;   // 地面に少し埋める
            float h = PostW / 2;
            var c = new Vector3(topL.x, 0, topL.z);
            var cs = new Vector3[8];
            int i = 0;
            foreach (var y in new[] { bottomY, topL.y })
            {
                cs[i++] = c + new Vector3(-h, y, -h); cs[i++] = c + new Vector3(h, y, -h); cs[i++] = c + new Vector3(h, y, h); cs[i++] = c + new Vector3(-h, y, h);
            }
            AddBox(V, TC, cs); nPost++;
        }
        var mesh = new Mesh { name = "Course2_RampPlanks", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V); mesh.subMeshCount = 3; mesh.SetTriangles(TA, 0); mesh.SetTriangles(TB, 1); mesh.SetTriangles(TC, 2);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        Save(mesh, "Assets/FuwaCourse/Meshes/Course2_RampPlanks.asset");
        var t = root.Find("RampPlanks");
        GameObject go = t != null ? t.gameObject : null;
        if (go == null) { go = new GameObject("RampPlanks", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root, false); Undo.RegisterCreatedObjectUndo(go, "planks"); }
        go.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Course2_RampPlanks.asset");
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] {
            WoodMat("Course2_PlankA", new Color(0.74f, 0.57f, 0.39f)), WoodMat("Course2_PlankB", new Color(0.64f, 0.48f, 0.32f)), WoodMat("Course2_Beam", new Color(0.47f, 0.35f, 0.25f)) };
        EditorUtility.SetDirty(go);
        return "planks " + nPl + " posts " + nPost + " / " + BuildRampPatch(root, m, go.GetComponent<MeshRenderer>().sharedMaterials);
    }

    // 坂の上り口と道のすき間：短い板を雑に（角度・幅・長さバラバラで）道のふちから坂の板の上へ渡す。歩けるように当たり判定つき
    static string BuildRampPatch(Transform root, Matrix4x4 m, Material[] mats)
    {
        var rnd = new System.Random(19);
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        var V = new List<Vector3>(); var TA = new List<int>(); var TB = new List<int>();
        float hw = RampWidth * 0.5f;
        int n = 0, k = 0;
        Physics.SyncTransforms();
        for (float sa = 68.7f; sa < 70.5f; sa += R(0.26f, 0.4f), k++)
        {
            float sb = sa + R(-0.22f, 0.22f);
            float w = R(0.2f, 0.3f) * 0.5f, t = R(0.04f, 0.055f);
            float stack = (k % 2) * 0.022f;                        // となりと重なっても面がそろわないよう交互に少し高さを変える
            // 道側：ふちから少し内側に乗せる / 坂側：坂の板の上に乗せる
            var A = m.MultiplyPoint3x4(Point(1, sa, -1.5f + R(0.14f, 0.3f), 0.004f + stack));
            var B = m.MultiplyPoint3x4(Point(3, sb, hw - R(0.15f, 0.35f), 0.02f + stack));
            var d = (B - A).normalized;
            A -= d * R(0.0f, 0.06f); B += d * R(0.02f, 0.12f);
            var side = Vector3.Cross(Vector3.up, d).normalized * w;
            // 下の道・坂の板にめりこむ所があれば、その端を持ち上げる（道の当たり判定＝上面で調べる。坂の板は少し浮いてるのでその分も足す）
            float NeedAt(float fa, float fc)
            {
                var q = Vector3.Lerp(Vector3.Lerp(A - side, B - side, fa), Vector3.Lerp(A + side, B + side, fa), fc);
                var wq = root.TransformPoint(q);
                float best = -1e9f;
                foreach (var h in Physics.RaycastAll(wq + Vector3.up * 2f, Vector3.down, 4f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    if (h.collider.gameObject.name == "RampPatch") continue;
                    best = Mathf.Max(best, root.InverseTransformPoint(h.point).y);
                }
                return best < -1e8f ? 0f : best + 0.024f + 0.008f - q.y;
            }
            float liftB = 0f, liftA = 0f;
            for (int ia = 6; ia <= 10; ia++) for (int ic = 0; ic <= 4; ic++) { float fa = ia / 10f; liftB = Mathf.Max(liftB, NeedAt(fa, ic / 4f) / fa); }
            for (int ia = 0; ia <= 5; ia++) for (int ic = 0; ic <= 4; ic++) { float fa = ia / 10f; liftA = Mathf.Max(liftA, (NeedAt(fa, ic / 4f) - liftB * fa) / (1f - fa)); }
            A += Vector3.up * liftA; B += Vector3.up * liftB;
            d = (B - A).normalized;
            side = Vector3.Cross(Vector3.up, d).normalized * w;
            var up = Vector3.Cross(d, side).normalized; if (up.y < 0) up = -up;
            AddBox(V, (k % 3 == 1) ? TB : TA, new[] { A - side, B - side, B + side, A + side, A - side + up * t, B - side + up * t, B + side + up * t, A + side + up * t });
            n++;
        }
        var mesh = new Mesh { name = "Course2_RampPatch", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V); mesh.subMeshCount = 2; mesh.SetTriangles(TA, 0); mesh.SetTriangles(TB, 1);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        Save(mesh, "Assets/FuwaCourse/Meshes/Course2_RampPatch.asset");
        var tr = root.Find("RampPatch");
        GameObject go = tr != null ? tr.gameObject : null;
        if (go == null) { go = new GameObject("RampPatch", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider)); go.transform.SetParent(root, false); Undo.RegisterCreatedObjectUndo(go, "patch"); }
        go.layer = root.Find("PathMesh").gameObject.layer;
        var saved = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Course2_RampPatch.asset");
        go.GetComponent<MeshFilter>().sharedMesh = saved;
        var mc = go.GetComponent<MeshCollider>(); mc.sharedMesh = null; mc.sharedMesh = saved;
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] { mats[0], mats[1] };
        EditorUtility.SetDirty(go);
        return "patch " + n;
    }

    static List<float> Nums(string s)
    {
        var l = new List<float>(); var cur = new System.Text.StringBuilder();
        foreach (var ch in s)
        {
            if (char.IsDigit(ch) || ch == '-' || ch == '.' || ch == 'e' || ch == 'E' || ch == '+') cur.Append(ch);
            else if (cur.Length > 0) { l.Add(float.Parse(cur.ToString(), System.Globalization.CultureInfo.InvariantCulture)); cur.Clear(); }
        }
        if (cur.Length > 0) l.Add(float.Parse(cur.ToString(), System.Globalization.CultureInfo.InvariantCulture));
        return l;
    }

    static void Save(Mesh m, string path)
    {
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { FuwaBevelTools.CopyInto(old, m); old.UploadMeshData(false); EditorUtility.SetDirty(old); }
        else AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
    }

    static List<Vector4> Profile(float w)
    {
        float W = w * 0.5f, B = Top - Thick;
        var prof = new List<Vector4> { new Vector4(-W, -Thick, -1, 0) };
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(-W + Bevel + Bevel * Mathf.Cos(a), -Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI / 2 - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(W - Bevel + Bevel * Mathf.Cos(a), -Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        prof.Add(new Vector4(W, -Thick, 1, 0));
        return prof;
    }

    // 断面の法線を坂の傾きに合わせて立体に
    static Vector3 Normal(int piece, float s, float nx, float ny)
    {
        Center(s, out _, out var d);
        float slope = (Height(piece, s + 0.05f) - Height(piece, s - 0.05f)) / 0.1f;
        var t = new Vector3(d.x, slope, d.y).normalized;
        var right = new Vector3(d.y, 0, -d.x);
        var up = Vector3.Cross(t, right).normalized; // t × right = 上向き
        if (up.y < 0) up = -up;
        return (right * nx + up * ny).normalized;
    }

    // かたまり0の穴の所（RegionA〜RegionB）は、でこぼこの楕円の穴をあけた板を別に作る
    public const float RegionA = 24f, RegionB = 30f;
    public const float HoleS = 27f, HoleL = -0.7f, HoleRS = 2.0f, HoleRL = 2.9f;

    // 穴のふち（パラメータ空間：s, 絶対横位置）。θ方向の半径（楕円＋でこぼこ）
    static float HoleR(float th) { return 1f + 0.12f * Mathf.Sin(3f * th + 1f) + 0.07f * Mathf.Sin(5f * th + 2f) + 0.04f * Mathf.Sin(8f * th); }
    // 左側（道のふちを切る所）は、ふちに直角なまっすぐの切り口にする
    const float HoleCutL = -1.0f;
    static Vector2 HoleRaw(float th) { float r = HoleR(th); return new Vector2(HoleS + HoleRS * r * Mathf.Cos(th), HoleL + HoleRL * r * Mathf.Sin(th)); }
    // でこぼこの楕円が横位置 HoleCutL を横切る所の s（切り口がずれて段にならないように）
    static float CutS(float side)
    {
        float a = side > 0 ? 0f : -Mathf.PI, b = -Mathf.PI / 2;
        for (int i = 0; i < 40; i++) { float c = (a + b) * 0.5f; if (HoleRaw(c).y > HoleCutL) a = c; else b = c; }
        return HoleRaw((a + b) * 0.5f).x;
    }
    static Vector2 HolePoint(float th)
    {
        var p = HoleRaw(th);
        if (p.y < HoleCutL) p.x = CutS(Mathf.Sign(Mathf.Cos(th)));
        return p;
    }
    // 点(s, 絶対横)が穴の中か
    public static bool InHole(float s, float la)
    {
        if (la < HoleCutL) return s > CutS(-1f) && s < CutS(1f);
        float ds = (s - HoleS) / HoleRS, dl = (la - HoleL) / HoleRL; float th = Mathf.Atan2(dl, ds); return Mathf.Sqrt(ds * ds + dl * dl) < HoleR(th);
    }

    static Vector3 P0(float s, float lr, float dy) { return Point(0, s, lr, dy); }

    static void AddRegion(List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<int> A, List<int> Bt, Matrix4x4 m)
    {
        float Wh(float s) { return Width(0, s) * 0.5f; }
        float Sh(float s) { return PitShift(0, s); }
        Vector2 HoleRel(float th) { var h = HolePoint(th); return new Vector2(h.x, h.y - Sh(h.x)); }
        float Margin(Vector2 q) { return q.y - (-Wh(q.x) + Bevel); }   // >0 なら道の中（左ふちのベベルより内側）
        var cRel = new Vector2(HoleS, HoleL - Sh(HoleS));

        // ---- 穴のふちを、道の中にある部分だけの「鎖」にする（左ふちを切る所は正確な交点を入れる）
        const int HN = 160;
        var ths = new List<float>(); for (int i = 0; i < HN; i++) ths.Add(2f * Mathf.PI * i / HN);
        int start = -1;
        for (int i = 0; i < HN; i++) { if (Margin(HoleRel(ths[i])) < 0 && Margin(HoleRel(ths[(i + 1) % HN])) >= 0) { start = (i + 1) % HN; break; } }
        var chain = new List<Vector2>(); bool closed = start < 0;
        if (closed) { foreach (var t in ths) chain.Add(HoleRel(t)); }
        else
        {
            // 入口の交点
            float Bis(float a, float b) { for (int it = 0; it < 30; it++) { float c = (a + b) * 0.5f; if (Margin(HoleRel(c)) < 0) a = c; else b = c; } return b; }
            float ta = ths[(start + HN - 1) % HN], tb = ths[start]; if (tb < ta) tb += 2f * Mathf.PI;
            chain.Add(HoleRel(Bis(ta, tb)));
            int i = start;
            while (Margin(HoleRel(ths[i])) >= 0) { chain.Add(HoleRel(ths[i])); i = (i + 1) % HN; if (i == start) break; }
            // 出口の交点（中→外）
            float tc = ths[(i + HN - 1) % HN], td = ths[i]; if (td < tc) td += 2f * Mathf.PI;
            float a2 = tc, b2 = td; for (int it = 0; it < 30; it++) { float c = (a2 + b2) * 0.5f; if (Margin(HoleRel(c)) >= 0) a2 = c; else b2 = c; }
            chain.Add(HoleRel(a2));
        }
        int cn = chain.Count;
        var nrm2 = new Vector2[cn];
        for (int i = 0; i < cn; i++)
        {
            var a = chain[closed ? (i + cn - 1) % cn : Mathf.Max(0, i - 1)]; var c = chain[closed ? (i + 1) % cn : Mathf.Min(cn - 1, i + 1)];
            var t = (c - a).normalized; var nn = new Vector2(t.y, -t.x);
            if (Vector2.Dot(nn, chain[i] - cRel) < 0) nn = -nn;
            nrm2[i] = nn;
        }
        // 上面の穴の境目（ふちの丸めの分だけ外）。道の外に出た部分は道のふちに沿わせる
        var holeOut = new List<Vector2>(); var holePoly = new List<Vector2>();
        for (int i = 0; i < HN; i++) { var h = HoleRel(ths[i]); holePoly.Add(h); }
        for (int i = 0; i < cn; i++) holeOut.Add(chain[i] + nrm2[i] * Bevel);
        // 上面用の多角形：鎖を外へずらしたもの＋（切れている時は）道の外をぐるっと回って閉じる
        var topPoly = new List<Vector2>(holeOut);
        if (!closed) { topPoly.Add(new Vector2(holeOut[cn - 1].x, -50f)); topPoly.Add(new Vector2(holeOut[0].x, -50f)); }

        bool Cross(List<Vector2> poly, float s, out float lo, out float hi)
        {
            lo = 1e9f; hi = -1e9f; bool any = false;
            for (int i = 0; i < poly.Count; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                if ((a.x - s) * (b.x - s) > 0f || Mathf.Approximately(a.x, b.x)) continue;
                float t = (s - a.x) / (b.x - a.x); float l = Mathf.Lerp(a.y, b.y, t);
                lo = Mathf.Min(lo, l); hi = Mathf.Max(hi, l); any = true;
            }
            return any;
        }
        void Tips(List<Vector2> poly, out float mn, out float mx, out Vector2 tf, out Vector2 tb)
        {
            mn = 1e9f; mx = -1e9f; tf = tb = Vector2.zero;
            foreach (var p in poly) { if (p.x < mn) { mn = p.x; tf = p; } if (p.x > mx) { mx = p.x; tb = p; } }
        }

        // ---- 上面・底面（s方向に細かく区切った帯。縞は道と同じ向き）
        const float st = 0.125f;
        int ns = Mathf.RoundToInt((RegionB - RegionA) / st);
        var botPoly = new List<Vector2>(chain); if (!closed) { botPoly.Add(new Vector2(chain[cn - 1].x, -50f)); botPoly.Add(new Vector2(chain[0].x, -50f)); }
        for (int pass = 0; pass < 2; pass++)
        {
            bool top = pass == 0;
            var poly = top ? topPoly : botPoly;
            Tips(poly, out float mn, out float mx, out Vector2 tf, out Vector2 tb);
            float dy = top ? 0f : -Thick;
            Vector3 want = m.MultiplyVector(top ? Vector3.up : Vector3.down);
            float[] Lx = new float[ns + 1], Rx = new float[ns + 1], H0 = new float[ns + 1], H1 = new float[ns + 1];
            for (int k = 0; k <= ns; k++)
            {
                float s = RegionA + k * st;
                Lx[k] = -Wh(s) + (top ? Bevel : 0f); Rx[k] = Wh(s) - (top ? Bevel : 0f);
                if (!Cross(poly, s, out H0[k], out H1[k])) { float c = s < mn ? tf.y : tb.y; H0[k] = H1[k] = Mathf.Max(c, Lx[k]); }
                H0[k] = Mathf.Clamp(H0[k], Lx[k], Rx[k]); H1[k] = Mathf.Clamp(H1[k], Lx[k], Rx[k]);
            }
            for (int k = 0; k < ns; k++)
            {
                float s0 = RegionA + k * st, s1 = s0 + st;
                var list = (!top) ? A : ((Mathf.FloorToInt((s0 + 0.001f) / Stripe) % 2 == 0) ? A : Bt);
                foreach (var part in new[] { 0, 1 })
                {
                    float a0 = part == 0 ? Lx[k] : H1[k], a1 = part == 0 ? H0[k] : Rx[k];
                    float b0 = part == 0 ? Lx[k + 1] : H1[k + 1], b1 = part == 0 ? H0[k + 1] : Rx[k + 1];
                    if (a1 - a0 < 1e-4f && b1 - b0 < 1e-4f) continue;
                    int b = V.Count;
                    foreach (var q in new[] { new Vector2(s0, a0), new Vector2(s0, a1), new Vector2(s1, b1), new Vector2(s1, b0) })
                    { V.Add(m.MultiplyPoint3x4(P0(q.x, q.y, dy))); N.Add(top ? m.MultiplyVector(Normal(0, q.x, 0, 1)) : want); UV.Add(q); }
                    AddTriDir(V, list, b, b + 1, b + 2, want); AddTriDir(V, list, b, b + 2, b + 3, want);
                }
            }
        }

        // ---- 穴のふちのベベル＋壁（鎖に沿って）
        const int BS = 4;
        int[] Column(Vector2 q, Vector2 u, bool asRim)
        {
            var col = new int[BS + 2];
            var u3 = RightFwd(q.x, u);
            for (int k = 0; k <= BS + 1; k++)
            {
                Vector2 p; float dyy; Vector3 nn;
                if (k <= BS) { float ph = (Mathf.PI / 2) * k / BS; p = q + u * (Bevel - Bevel * Mathf.Sin(ph)); dyy = -Bevel + Bevel * Mathf.Cos(ph); nn = Vector3.up * Mathf.Cos(ph) - u3 * Mathf.Sin(ph); }
                else { p = q; dyy = -Thick; nn = -u3; }
                col[k] = V.Count; V.Add(m.MultiplyPoint3x4(P0(p.x, p.y, dyy))); N.Add(m.MultiplyVector(nn.normalized)); UV.Add(p);
            }
            return col;
        }
        int segs = closed ? cn : cn - 1;
        for (int i = 0; i < segs; i++)
        {
            int j = (i + 1) % cn;
            var c0 = Column(chain[i], nrm2[i], true); var c1 = Column(chain[j], nrm2[j], true);
            var listB = (Mathf.FloorToInt((chain[i].x + 0.001f) / Stripe) % 2 == 0) ? A : Bt;
            for (int k = 0; k <= BS; k++)
            {
                var lst = k < BS ? listB : A;
                AddTriOut(V, N, lst, c0[k], c1[k], c1[k + 1]); AddTriOut(V, N, lst, c0[k], c1[k + 1], c0[k + 1]);
            }
        }

        // ---- 左右の側面（ベベルつき）。左は穴が切っている所（鎖の両端の間）は作らない
        float cut0 = closed ? 1e9f : Mathf.Min(chain[0].x, chain[cn - 1].x), cut1 = closed ? -1e9f : Mathf.Max(chain[0].x, chain[cn - 1].x);
        void SideStrip(float side, float sa, float sb)
        {
            if (sb - sa < 1e-4f) return;
            int n = Mathf.Max(1, Mathf.CeilToInt((sb - sa) / st));
            for (int k = 0; k < n; k++)
            {
                float s = Mathf.Lerp(sa, sb, k / (float)n), s1 = Mathf.Lerp(sa, sb, (k + 1) / (float)n);
                var p0 = Profile(Width(0, s)); var p1 = Profile(Width(0, s1));
                int half = p0.Count / 2;
                int from = side < 0 ? 0 : half, to = side < 0 ? half - 1 : p0.Count - 1;
                var lst = (Mathf.FloorToInt((s + 0.001f) / Stripe) % 2 == 0) ? A : Bt;
                for (int i = from; i < to; i++)
                {
                    int b = V.Count;
                    foreach (var (pr, sv) in new[] { (p0, s), (p1, s1) })
                        for (int e = i; e <= i + 1; e++) { var q = pr[e]; V.Add(m.MultiplyPoint3x4(P0(sv, q.x, q.y))); N.Add(m.MultiplyVector(Normal(0, sv, q.z, q.w))); UV.Add(new Vector2(q.x, sv)); }
                    AddTriOut(V, N, lst, b, b + 2, b + 1); AddTriOut(V, N, lst, b + 1, b + 2, b + 3);
                }
            }
        }
        SideStrip(1f, RegionA, RegionB);
        if (closed) SideStrip(-1f, RegionA, RegionB);
        else { SideStrip(-1f, RegionA, cut0); SideStrip(-1f, cut1, RegionB); }

        // ---- つなぎ目：穴のふちの断面と、左ふちの断面を角でつなぐ
        if (!closed)
        {
            foreach (var ci in new[] { 0, cn - 1 })
            {
                var q = chain[ci]; float s = q.x;
                var rim = Column(q, nrm2[ci], true);                      // 上→下
                var prof = Profile(Width(0, s)); int half = prof.Count / 2;
                var side = new int[half];                                   // 下→上（左ふち）
                for (int e = 0; e < half; e++) { var pq = prof[e]; side[e] = V.Count; V.Add(m.MultiplyPoint3x4(P0(s, pq.x, pq.y))); N.Add(m.MultiplyVector(Normal(0, s, pq.z, pq.w))); UV.Add(new Vector2(pq.x, s)); }
                // ふさぐ面の向き：鎖の外側（穴がない方＝道が続く方の反対）
                Center(s, out _, out var d);
                var fwd = m.MultiplyVector(new Vector3(d.x, 0, d.y)) * (ci == 0 ? (chain[0].x < chain[cn - 1].x ? -1f : 1f) : (chain[0].x < chain[cn - 1].x ? 1f : -1f));
                int cnt = Mathf.Min(rim.Length, side.Length);
                for (int k = 0; k < cnt - 1; k++)
                {
                    int r0 = rim[k], r1 = rim[k + 1], s0i = side[half - 1 - k], s1i = side[half - 2 - k];
                    // 向きがどちらでも見えるよう両面
                    A.AddRange(new[] { r0, s0i, s1i, r0, s1i, s0i, r0, s1i, r1, r0, r1, s1i });
                }
            }
        }
    }

    // 上面の外周の点を、側面のふちまで広げる（底面用）
    static Vector2 Full(Vector2 q, float w)
    {
        if (Mathf.Abs(Mathf.Abs(q.y) - (w - Bevel)) < 0.002f) return new Vector2(q.x, Mathf.Sign(q.y) * w);
        return q;
    }

    // (s, 横)の向きを、Startローカルの3D方向に
    static Vector3 RightFwd(float s, Vector2 u)
    {
        Center(s, out _, out var d);
        var fwd = new Vector3(d.x, 0, d.y); var right = new Vector3(d.y, 0, -d.x);
        return (fwd * u.x + right * u.y).normalized;
    }
    // 法線と同じ向きを表にして三角形を足す
    static void AddTriOut(List<Vector3> V, List<Vector3> N, List<int> T, int a, int b, int c)
    {
        var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
        var want = N[a] + N[b] + N[c];
        if (Vector3.Dot(n, want) >= 0) T.AddRange(new[] { a, b, c }); else T.AddRange(new[] { a, c, b });
    }
    static void AddTriDir(List<Vector3> V, List<int> T, int a, int b, int c, Vector3 want)
    {
        var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
        if (Vector3.Dot(n, want) >= 0) T.AddRange(new[] { a, b, c }); else T.AddRange(new[] { a, c, b });
    }

    // 道の端のうち、ほかの道につながっていない所はふちを丸める
    static bool FreeEnd(int pc, float s)
    {
        if (pc == 0 && (Mathf.Abs(s - RegionA) < 0.01f || Mathf.Abs(s - RegionB) < 0.01f)) return false;
        if (pc == 1 && Mathf.Abs(s - DiveEndA) < 0.01f && AssetDatabase.LoadAssetAtPath<Mesh>(DiveEndMeshPath) != null) return false;
        return true;
    }

    static void BuildVisual(Mesh mesh, Matrix4x4 m)
    {
        var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>();
        var A = new List<int>(); var Bt = new List<int>();
        const float step = 0.25f;
        for (int pc = 0; pc < Pieces.Length; pc++)
        foreach (var span in Spans(pc))
        {
            if (pc == 3) continue;   // 人用の坂は板張り（BuildRampPlanks）
            float s0p = span.x, s1p = span.y;
            bool b0 = FreeEnd(pc, s0p), b1 = FreeEnd(pc, s1p);
            // 断面を置く位置（x=s, y=内側へのずらし量, z=端の丸めの角度, w=向き）
            var st = new List<Vector4>();
            if (b0) for (int k = BevelSteps; k >= 1; k--) { float th = (Mathf.PI / 2) * k / BevelSteps; st.Add(new Vector4(s0p + Bevel * (1 - Mathf.Sin(th)), Bevel * (1 - Mathf.Cos(th)), th, -1)); }
            float a0 = b0 ? s0p + Bevel : s0p, a1 = b1 ? s1p - Bevel : s1p;
            int n = Mathf.Max(1, Mathf.CeilToInt((a1 - a0) / step));
            for (int k = 0; k <= n; k++) st.Add(new Vector4(Mathf.Lerp(a0, a1, k / (float)n), 0, 0, 0));
            if (b1) for (int k = 1; k <= BevelSteps; k++) { float th = (Mathf.PI / 2) * k / BevelSteps; st.Add(new Vector4(s1p - Bevel * (1 - Mathf.Sin(th)), Bevel * (1 - Mathf.Cos(th)), th, 1)); }
            var rings = new List<int>(); int P = 0;
            foreach (var sv in st)
            {
                float s = sv.x, inset = sv.y, th = sv.z, dir = sv.w;
                var prof = Profile(Width(pc, s)); P = prof.Count;
                Center(s, out _, out var d);
                float slope = (Height(pc, s + 0.05f) - Height(pc, s - 0.05f)) / 0.1f;
                var t3 = new Vector3(d.x, slope, d.y).normalized;
                rings.Add(V.Count);
                foreach (var q in prof)
                {
                    V.Add(m.MultiplyPoint3x4(Point(pc, s, q.x - q.z * inset, q.y - q.w * inset)));
                    var nn = Normal(pc, s, q.z, q.w) * Mathf.Cos(th) + t3 * dir * Mathf.Sin(th);
                    N.Add(m.MultiplyVector(nn.normalized)); UV.Add(new Vector2(q.x, s));
                }
            }
            for (int r = 0; r < st.Count - 1; r++)
            {
                var list = (Mathf.FloorToInt((st[r].x + 0.001f) / Stripe) % 2 == 0) ? A : Bt;
                int ra = rings[r], rb = rings[r + 1];
                for (int i = 0; i < P - 1; i++) { int x0 = ra + i, x1 = ra + i + 1, y0 = rb + i, y1 = rb + i + 1; list.AddRange(new[] { x0, y0, x1, x1, y0, y1 }); }
                // 底面
                int bb = V.Count;
                foreach (var idx in new[] { ra, rb })
                {
                    V.Add(V[idx]); N.Add(m.MultiplyVector(Vector3.down)); UV.Add(Vector2.zero);
                    V.Add(V[idx + P - 1]); N.Add(m.MultiplyVector(Vector3.down)); UV.Add(Vector2.zero);
                }
                AddTriDir(V, list, bb, bb + 1, bb + 2, m.MultiplyVector(Vector3.down)); AddTriDir(V, list, bb + 1, bb + 3, bb + 2, m.MultiplyVector(Vector3.down));
            }
            // 両端のふた（丸めた後の一番内側の断面で閉じる）
            foreach (var (ri, sign) in new[] { (0, -1f), (st.Count - 1, 1f) })
            {
                int baseR = rings[ri]; float s = st[ri].x;
                Center(s, out _, out var d);
                int cb = V.Count; var nrm = m.MultiplyVector(new Vector3(d.x, 0, d.y) * sign);
                for (int i = 0; i < P; i++) { V.Add(V[baseR + i]); N.Add(nrm); UV.Add(Vector2.zero); }
                for (int i = 1; i < P - 1; i++) AddTriDir(V, A, cb, cb + i, cb + i + 1, nrm);
            }
        }
        if (!AppendPitMesh(V, N, UV, A, Bt)) AddRegion(V, N, UV, A, Bt, m);
        AppendMesh(DiveEndMeshPath, V, N, UV, A, Bt);
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, UV);
        mesh.subMeshCount = 2; mesh.SetTriangles(A, 0); mesh.SetTriangles(Bt, 1);
        mesh.RecalculateBounds(); mesh.RecalculateTangents();
    }

    static void BuildCollider(Mesh mesh, Matrix4x4 m)
    {
        var V = new List<Vector3>(); var T = new List<int>();
        const float step = 0.5f;
        for (int pc = 0; pc < Pieces.Length; pc++)
        foreach (var span in Spans(pc))
        {
            float s0p = span.x, s1p = span.y;
            int n = Mathf.CeilToInt((s1p - s0p) / step);
            int first = V.Count;
            for (int k = 0; k <= n; k++)
            {
                float s = Mathf.Min(s1p, s0p + k * step), W = Width(pc, s) * 0.5f;
                V.Add(m.MultiplyPoint3x4(Point(pc, s, -W, 0))); V.Add(m.MultiplyPoint3x4(Point(pc, s, W, 0)));
                V.Add(m.MultiplyPoint3x4(Point(pc, s, W, -Thick))); V.Add(m.MultiplyPoint3x4(Point(pc, s, -W, -Thick)));
            }
            for (int k = 0; k < n; k++)
            {
                int a = first + k * 4, b = a + 4;
                for (int f = 0; f < 4; f++) { int f1 = (f + 1) % 4; T.AddRange(new[] { a + f, b + f, a + f1, a + f1, b + f, b + f1 }); }
            }
            int e = first + n * 4;
            T.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            T.AddRange(new[] { e, e + 2, e + 1, e, e + 3, e + 2 });
        }
        // 穴の所の板（見た目と同じ形）
        var rv = new List<Vector3>(); var rn = new List<Vector3>(); var ruv = new List<Vector2>(); var ra = new List<int>(); var rb = new List<int>();
        if (!AppendPitMesh(rv, rn, ruv, ra, rb)) AddRegion(rv, rn, ruv, ra, rb, m);
        AppendMesh(DiveEndMeshPath, rv, rn, ruv, ra, rb);
        int off = V.Count; V.AddRange(rv); foreach (var t in ra) T.Add(t + off); foreach (var t in rb) T.Add(t + off);
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(V); mesh.SetTriangles(T, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
    }
}
