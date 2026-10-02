using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// コース3「やみのもり」の道を生成する（FuwaCourse1Builder と同じ作り）。
// 流れ：小さなクモの巣をよける道 → 重い霧×2 → CP1 → 呼吸する巨大クモの巣 → 重力の谷（人は左の細道）
//      → つむじ風で上の段へ（細道は坂で上へ）→ CP2 → クモの巣の回廊（途中に重い霧）→ 穴の位置がずれた巨大な巣×2 → ゴール
public static class FuwaCourse3Builder
{
    public const float Top = 3f, Thick = 0.3f, Bevel = 0.08f;
    const int BevelSteps = 4;
    const float Stripe = 0.75f;
    public const float Upper = 3f;   // 上の段の高さ

    // 中心線：直線0-10 → 右カーブR25(10-30) → 直線30-56 → 左カーブR25(56-76) → 直線76-
    static readonly float[] SegEnd = { 10f, 30f, 56f, 76f, 1e9f };
    static readonly float[] SegCurv = { 0f, 1f / 25f, 0f, -1f / 25f, 0f };

    // かたまり：0 下の道 / 1 谷の向こうの着地点 / 2 上の段 / 3 人用の細道（谷の横→坂で上の段へ）
    public const float ValleyA = 43f, ValleyB = 49f, LandB = 53.6f, UpperA = 54f, UpperEnd = 86.4f;   // 道はゴールの広場（半径3m）のふちまで
    public static readonly Vector2[] Pieces =
    {
        new Vector2(0f, ValleyA), new Vector2(ValleyB, LandB), new Vector2(UpperA, UpperEnd), new Vector2(41f, 56.5f),
    };
    static readonly Vector2[][] HeightKeys =
    {
        new[] { new Vector2(0, 0), new Vector2(100, 0) },
        new[] { new Vector2(0, 0), new Vector2(100, 0) },
        new[] { new Vector2(0, Upper), new Vector2(100, Upper) },
        new[] { new Vector2(49.5f, 0), new Vector2(55.8f, Upper) },
    };

    public static float Width(int piece, float s)
    {
        if (piece == 3) return 1.0f;
        if (piece == 0 && s > 30f && s < 41f) return 3.6f;   // 巨大な巣の前後は少し広め
        return 3f;
    }

    public static float Offset(int piece, float s)
    {
        if (piece == 3) return -(1.5f + 0.5f);   // 本道の左ふちにくっつける
        return 0f;
    }

    public static float Height(int piece, float s)
    {
        var keys = HeightKeys[piece];
        if (piece != 3) return Linear(keys, s);
        float sum = 0; int n = 9;
        for (int i = 0; i < n; i++) sum += Linear(keys, s + Mathf.Lerp(-0.8f, 0.8f, i / (float)(n - 1)));
        return sum / n;
    }

    static float Linear(Vector2[] k, float x)
    {
        if (x <= k[0].x) return k[0].y;
        for (int i = 1; i < k.Length; i++)
            if (x <= k[i].x) return Mathf.Lerp(k[i - 1].y, k[i].y, (x - k[i - 1].x) / (k[i].x - k[i - 1].x));
        return k[k.Length - 1].y;
    }

    public static void Center(float s, out Vector2 p, out Vector2 d)
    {
        p = new Vector2(0, 1);
        float heading = 0f, cur = 0f;
        for (int i = 0; i < SegEnd.Length && cur < s; i++)
        {
            float end = Mathf.Min(SegEnd[i], s), len = end - cur, curv = SegCurv[i];
            if (curv == 0f) p += new Vector2(Mathf.Sin(heading), Mathf.Cos(heading)) * len;
            else
            {
                float h2 = heading + curv * len, r = 1f / curv;
                p += new Vector2(Mathf.Cos(heading) - Mathf.Cos(h2), Mathf.Sin(h2) - Mathf.Sin(heading)) * r;
                heading = h2;
            }
            cur = end;
        }
        d = new Vector2(Mathf.Sin(heading), Mathf.Cos(heading));
    }

    public static Vector3 Point(int piece, float s, float lateral, float dy)
    {
        Center(s, out var p, out var d);
        var r = new Vector2(d.y, -d.x);
        var q = p + r * (lateral + Offset(piece, s));
        return new Vector3(q.x, Top + Height(piece, s) + dy, q.y);
    }

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

    // コースのローカル座標の点（ギミック置き場に使う）
    public static Vector3 Local(Transform root, int piece, float s, float lateral, float dy)
    {
        return ToCourse(root).MultiplyPoint3x4(Point(piece, s, lateral, dy));
    }

    public static Transform Root()
    {
        foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == "FuwaCourse3") return r.transform;
        return null;
    }

    [MenuItem("FuwaCourse/Course3/Rebuild Path")]
    public static void RebuildMenu() { Debug.Log(Rebuild()); }

    public static string Rebuild()
    {
        var root = Root();
        var m = ToCourse(root);
        var vis = new Mesh { name = "Course3Path_Yami_Bevel" };
        var col = new Mesh { name = "Course3Path_Yami" };
        BuildVisual(vis, m);
        BuildCollider(col, m);
        Save(vis, "Assets/FuwaCourse/Meshes/Course3Path_Yami_Bevel.asset");
        Save(col, "Assets/FuwaCourse/Meshes/Course3Path_Yami.asset");
        var pm = root.Find("PathMesh");
        Undo.RecordObject(pm.GetComponent<MeshFilter>(), "path");
        pm.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Course3Path_Yami_Bevel.asset");
        var mc = pm.GetComponent<MeshCollider>();
        mc.sharedMesh = null;
        mc.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Course3Path_Yami.asset");
        EditorUtility.SetDirty(pm.gameObject);
        return "Course3 path rebuilt: verts " + vis.vertexCount;
    }

    public const float CP1 = 29f, CP2 = 57.5f, GoalS = 89f;

    public static string PlaceObjects()
    {
        var root = Root();
        var m = ToCourse(root);
        var log = "";
        void PlaceCp(string name, int piece, float s)
        {
            var cp = root.Find(name);
            Undo.RecordObject(cp, "place");
            cp.localPosition = m.MultiplyPoint3x4(Point(piece, s, 0, 0));
            cp.localRotation = Quaternion.Euler(0, Yaw(root, s), 0);
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
        PlaceCp("Checkpoint2", 2, CP2);

        var goal = root.Find("Goal");
        Undo.RecordObject(goal, "place");
        goal.localPosition = m.MultiplyPoint3x4(Point(2, GoalS, 0, 0)) + Vector3.down * 0.02f;
        goal.localRotation = Quaternion.Euler(0, Yaw(root, GoalS), 0);

        var fc = root.Find("FallCatcher");
        var b = root.Find("PathMesh").GetComponent<MeshFilter>().sharedMesh.bounds;
        Undo.RecordObject(fc, "place");
        fc.localRotation = Quaternion.identity;
        fc.localPosition = new Vector3(b.center.x, -6f, b.center.z);
        fc.localScale = new Vector3(b.size.x + 20f, 2f, b.size.z + 20f);
        return log + "goal " + goal.localPosition.ToString("F2");
    }



    // ---- ギミック（Gimmicks_Yami の下に毎回作り直す。元は BuildTemplates（旧ミックスから残したお手本）の巣・重い霧、コース1のつむじ風） ----
    public static readonly float[] DodgeS = { 5f, 8.5f, 12f, 15.5f };
    public const float FogA = 18f, FogB = 24.5f, BigWebS = 37f, WhirlS = 51.4f;
    public static readonly float[] CorridorS = { 62f, 65f, 68f, 71f };
    public const float CorridorFog = 66.5f, WebX1 = 76.5f, WebX2 = 81.5f;

    static Transform FindIn(string rootName, string path)
    {
        foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == rootName) return r.transform.Find(path);
        return null;
    }

    static GameObject Dup(Transform src, Transform parent, string name)
    {
        var go = Object.Instantiate(src.gameObject, parent);
        go.name = name; go.SetActive(true);
        return go;
    }


    // 重い霧の地面の波紋：group の原点の真上を中心に、半径 rr の円で、道の上面に乗っている所だけ。
    // uv.x＝中心からの距離(m)（波紋が中心へ流れる）、頂点アルファで円のふちと道のふちをぼかす
    static Mesh HighlightMesh(Transform group, float rr, string name)
    {
        var col = Root().Find("PathMesh").GetComponent<MeshCollider>();
        Physics.SyncTransforms();
        const float step = 0.12f;
        int n = Mathf.CeilToInt(rr / step);
        var idx = new Dictionary<Vector2Int, int>();
        var V = new List<Vector3>(); var UV = new List<Vector2>(); var C = new List<Color>(); var T = new List<int>();
        Vector3 c0 = group.position;
        float topLocal = 0f; bool topSet = false;
        bool OnPath(Vector3 w, out float y)
        {
            y = 0;
            if (col.Raycast(new Ray(new Vector3(w.x, c0.y + 30f, w.z), Vector3.down), out var h, 60f) && h.normal.y > 0.7f) { y = h.point.y; return true; }
            return false;
        }
        for (int i = -n; i <= n; i++) for (int k = -n; k <= n; k++)
        {
            var lp = new Vector3(i * step, 0, k * step);
            float r = new Vector2(lp.x, lp.z).magnitude;
            if (r > rr + step) continue;
            var w = group.TransformPoint(lp);
            if (!OnPath(w, out float y)) continue;
            // 道のふちまでの近さ（まわり8方向・0.35m先が道でなければぼかす）
            float edge = 1f;
            for (int a = 0; a < 8; a++)
            {
                var d = Quaternion.Euler(0, a * 45f, 0) * Vector3.forward;
                for (int st = 1; st <= 3; st++) { if (!OnPath(w + d * (0.12f * st), out _)) { edge = Mathf.Min(edge, (st - 1) / 3f); break; } }
            }
            float circ = 1f - Mathf.Clamp01((r - (rr - 0.5f)) / 0.5f);
            idx[new Vector2Int(i, k)] = V.Count;
            var local = group.InverseTransformPoint(new Vector3(w.x, y + 0.015f, w.z));
            V.Add(local); UV.Add(new Vector2(r, 0)); C.Add(new Color(1, 1, 1, Mathf.Min(circ, edge)));
        }
        foreach (var kv in idx)
        {
            var g = kv.Key;
            if (idx.TryGetValue(g + new Vector2Int(1, 0), out int b) && idx.TryGetValue(g + new Vector2Int(0, 1), out int d) && idx.TryGetValue(g + new Vector2Int(1, 1), out int e))
            {
                int a = kv.Value;
                T.AddRange(new[] { a, d, b, b, d, e });
            }
        }
        string path = "Assets/FuwaCourse/Meshes/Yami_FogHighlight_" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear(); mesh.name = "Yami_FogHighlight_" + name;
        mesh.SetVertices(V); mesh.SetUVs(0, UV); mesh.SetColors(C); mesh.SetTriangles(T, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        // 上向きでなければ裏返す（両面描画なので見た目は同じだが念のため）
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    public static string BuildGimmicks()
    {
        var root = Root();
        var m = ToCourse(root);
        var old = root.Find("Gimmicks_Yami");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject("Gimmicks_Yami").transform;
        holder.SetParent(root, false);
        Undo.RegisterCreatedObjectUndo(holder.gameObject, "yami");
        Vector3 P(int pc, float s, float lat, float dy) => m.MultiplyPoint3x4(Point(pc, s, lat, dy));
        Quaternion R(float s) => Quaternion.Euler(0, Yaw(root, s), 0);
        var tplWeb = root.Find("BuildTemplates/SpikeBush");
        var tplHeavy = root.Find("BuildTemplates/Gimmick4_Heavy");
        var tplWindow = root.Find("BuildTemplates/Gimmick6_Window");
        var webs = new List<SpiderWebBuilder>();
        string log = "";

        // 小さな巣：道の片側をふさぐ。外側に木（自動で浮島）、内側は支えなし
        SpiderWebBuilder SmallWeb(Transform parent, string name, int pc, float s, float lat, float w, float h, float lift, bool treeOnRight)
        {
            var go = Dup(tplWeb, parent, name);
            go.transform.localPosition = P(pc, s, lat, lift + h * 0.5f);
            go.transform.localRotation = R(s);
            go.transform.localScale = new Vector3(w, h, 0.8f);
            var b = go.GetComponent<SpiderWebBuilder>();
            b.size = new Vector2(w, h); b.seed = s * 3.7f + lat; b.holeRadius = 0f;
            b.left.anchor = treeOnRight ? SpiderWebBuilder.AnchorType.None : SpiderWebBuilder.AnchorType.Tree;
            b.right.anchor = treeOnRight ? SpiderWebBuilder.AnchorType.Tree : SpiderWebBuilder.AnchorType.None;
            webs.Add(b);
            return b;
        }

        // 1. 小さな巣をよける道（左右交互に半分ずつふさぐ）
        {
            var g = new GameObject("G1_DodgeWebs").transform; g.SetParent(holder, false);
            for (int i = 0; i < DodgeS.Length; i++)
            {
                bool right = i % 2 == 0;
                SmallWeb(g, "Web" + i, 0, DodgeS[i], right ? 0.7f : -0.7f, 1.6f, 2.6f, 0.15f, right);
            }
            log += "dodge ";
        }

        // 重い霧（球）。group の原点は道の上面から3m下（元の配置と同じ決まり）
        GameObject Fog(Transform parent, string name, int pc, float s, float size)
        {
            var go = Dup(tplHeavy, parent, name);
            go.transform.localPosition = P(pc, s, 0, -3f); go.transform.localRotation = R(s);
            float k = size / 7f;
            // 球の中心は道の上1.2m（ふわふわが通る高さを包む）
            const float centerH = 1.2f;
            var z = go.transform.Find("HeavyZone"); z.localScale = Vector3.one * size;
            z.localPosition = new Vector3(0, 3f + centerH, 0);
            var ps = go.transform.Find("HeavyParticles"); if (ps) { ps.localScale = Vector3.one * k; ps.localPosition = z.localPosition; }
            // 地面の波紋：球が道の上面を切る円の中で、今の道の上にある所だけにメッシュを作り直す
            var hl = go.transform.Find("GroundHighlight");
            if (hl)
            {
                float rr = Mathf.Sqrt(Mathf.Max(0.01f, size * size * 0.25f - centerH * centerH));
                hl.localPosition = Vector3.zero; hl.localRotation = Quaternion.identity; hl.localScale = Vector3.one;
                hl.GetComponent<MeshFilter>().sharedMesh = HighlightMesh(go.transform, rr, name);
            }
            return go;
        }

        // 2. 重い霧×2
        {
            var g = new GameObject("G2_Fogs").transform; g.SetParent(holder, false);
            Fog(g, "FogA", 0, FogA, 5.5f); Fog(g, "FogB", 0, FogB, 5.5f);
            log += "fog ";
        }

        // 3. 呼吸する巨大な巣
        GameObject BigWeb(string name, float s, float holeX, bool breathe)
        {
            var go = Dup(tplWindow, holder, name);
            go.transform.localRotation = R(s);
            var bw = go.transform.Find("BigWeb");
            var b = bw.GetComponent<SpiderWebBuilder>();
            float pathTop = P(s > UpperA ? 2 : 0, s, 0, 0).y;
            // 巣の下ふちを道の上面より少し上に（穴の中心が道+0.7m）
            go.transform.localPosition = P(s > UpperA ? 2 : 0, s, 0, 0) - Vector3.up * (bw.localPosition.y - (b.size.y * 0.5f - 0.0f)) + Vector3.up * 0.2f;
            var whp = bw.GetComponent<FuwaWebHole>();
            float rMax = breathe && whp != null ? whp.maxRadius : 0.62f;
            // 穴のふち（光る輪）が道にめりこまない高さ：一番大きい時の半径＋輪の太さ分の余裕
            b.holeCenter = new Vector2(holeX, -b.size.y * 0.5f + rMax + 0.3f);
            b.seed = s * 1.3f;
            var wh = bw.GetComponent<FuwaWebHole>();
            if (wh != null) { wh.breathe = breathe; if (!breathe) wh.holeRadius = 0.62f; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(wh); }
            webs.Add(b);
            return go;
        }
        BigWeb("G3_BreathingWeb", BigWebS, 0f, true);
        log += "bigweb ";

        // 4. 重力の谷：谷の上に重い霧。谷の上ではコースアウト判定をしない
        {
            var g = new GameObject("G4_GravityValley").transform; g.SetParent(holder, false);
            float sc = (ValleyA + ValleyB) * 0.5f;
            var vf = Fog(g, "ValleyFog", 0, sc, 6.5f);
            var vhl = vf.transform.Find("GroundHighlight"); if (vhl) vhl.gameObject.SetActive(false);   // 谷なので地面がない
            var gz = new GameObject("GapZone", typeof(BoxCollider)); gz.transform.SetParent(g, false); gz.layer = 2;
            var bc = gz.GetComponent<BoxCollider>(); bc.isTrigger = true;
            gz.transform.localPosition = P(0, sc, 0, 1f); gz.transform.localRotation = R(sc);
            bc.size = new Vector3(4f, 12f, ValleyB - ValleyA + 1f);
            var ball = Object.FindObjectOfType<FuwaBall>(true);
            var list = new List<Collider>();
            if (ball.gapZones != null) foreach (var c in ball.gapZones) if (c != null && !c.transform.IsChildOf(root)) list.Add(c);
            list.Add(bc);
            Undo.RecordObject(ball, "gap"); ball.gapZones = list.ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ball);
            log += "valley ";
        }

        // 5. つむじ風で上の段へ
        {
            FuwaCourse1Builder.MakeWhirlwind(holder, P(1, WhirlS, 0, 0), R(WhirlS), Upper);
            log += "whirl ";
        }

        // 6. クモの巣の回廊：左右の巣で真ん中1mだけ空ける。途中に重い霧
        {
            var g = new GameObject("G6_WebCorridor").transform; g.SetParent(holder, false);
            for (int i = 0; i < CorridorS.Length; i++)
            {
                SmallWeb(g, "WebL" + i, 2, CorridorS[i], -1.0f, 1.0f, 2.6f, 0.15f, false);
                SmallWeb(g, "WebR" + i, 2, CorridorS[i], 1.0f, 1.0f, 2.6f, 0.15f, true);
            }
            Fog(g, "CorridorFog", 2, CorridorFog, 5f);
            log += "corridor ";
        }

        // 7. 穴の位置がずれた巨大な巣×2
        BigWeb("G7_WebLeftHole", WebX1, -0.9f, false);
        BigWeb("G7_WebRightHole", WebX2, 0.9f, false);
        log += "webs2 ";

        // 巣の中身（板・支え・当たり判定）を作り直す
        foreach (var b in webs) SpiderWebBuild.Build(b);
        EditorUtility.SetDirty(root.gameObject);
        return log + " webs " + webs.Count;
    }

    // ---- メッシュ（コース2と同じ作り：断面を並べて、端は丸め、縞は0.75mごと） ----
    static List<Vector4> Profile(float w)
    {
        float W = w * 0.5f;
        var prof = new List<Vector4> { new Vector4(-W, -Thick, -1, 0) };
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(-W + Bevel + Bevel * Mathf.Cos(a), -Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        for (int i = 0; i <= BevelSteps; i++) { float a = Mathf.PI / 2 - (Mathf.PI / 2) * i / BevelSteps; prof.Add(new Vector4(W - Bevel + Bevel * Mathf.Cos(a), -Bevel + Bevel * Mathf.Sin(a), Mathf.Cos(a), Mathf.Sin(a))); }
        prof.Add(new Vector4(W, -Thick, 1, 0));
        return prof;
    }

    static Vector3 Normal(int piece, float s, float nx, float ny)
    {
        Center(s, out _, out var d);
        float slope = (Height(piece, s + 0.05f) - Height(piece, s - 0.05f)) / 0.1f;
        var t = new Vector3(d.x, slope, d.y).normalized;
        var right = new Vector3(d.y, 0, -d.x);
        var up = Vector3.Cross(t, right).normalized;
        if (up.y < 0) up = -up;
        return (right * nx + up * ny).normalized;
    }

    static void AddTriDir(List<Vector3> V, List<int> T, int a, int b, int c, Vector3 want)
    {
        var n = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
        if (Vector3.Dot(n, want) >= 0) T.AddRange(new[] { a, b, c }); else T.AddRange(new[] { a, c, b });
    }

    // つながっている端（細道・坂・橋が本道にくっつく所）は丸めない
    static bool FreeEnd(int pc, float s)
    {
        if (pc == 3) return false;   // 細道：始まりは下の道の横、終わりは上の段の横
        return true;
    }

    static void BuildVisual(Mesh mesh, Matrix4x4 m)
    {
        var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>();
        var A = new List<int>(); var Bt = new List<int>();
        const float step = 0.25f;
        for (int pc = 0; pc < Pieces.Length; pc++)
        {
            float s0p = Pieces[pc].x, s1p = Pieces[pc].y;
            bool b0 = FreeEnd(pc, s0p), b1 = FreeEnd(pc, s1p);
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
                int bb = V.Count;
                foreach (var idx in new[] { ra, rb })
                {
                    V.Add(V[idx]); N.Add(m.MultiplyVector(Vector3.down)); UV.Add(Vector2.zero);
                    V.Add(V[idx + P - 1]); N.Add(m.MultiplyVector(Vector3.down)); UV.Add(Vector2.zero);
                }
                AddTriDir(V, list, bb, bb + 1, bb + 2, m.MultiplyVector(Vector3.down)); AddTriDir(V, list, bb + 1, bb + 3, bb + 2, m.MultiplyVector(Vector3.down));
            }
            foreach (var (ri, sign) in new[] { (0, -1f), (st.Count - 1, 1f) })
            {
                int baseR = rings[ri]; float s = st[ri].x;
                Center(s, out _, out var d);
                int cb = V.Count; var nrm = m.MultiplyVector(new Vector3(d.x, 0, d.y) * sign);
                for (int i = 0; i < P; i++) { V.Add(V[baseR + i]); N.Add(nrm); UV.Add(Vector2.zero); }
                for (int i = 1; i < P - 1; i++) AddTriDir(V, A, cb, cb + i, cb + i + 1, nrm);
            }
        }
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
        {
            float s0p = Pieces[pc].x, s1p = Pieces[pc].y;
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
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(V); mesh.SetTriangles(T, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
    }

    static void Save(Mesh m, string path)
    {
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { FuwaBevelTools.CopyInto(old, m); old.UploadMeshData(false); EditorUtility.SetDirty(old); }
        else AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
    }
}
