using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// コース1「かぜのみち」の道を生成する（FuwaCourse2Builder と同じ考え方）。
// 中心線はスタート(Start)のローカル座標：z=1 から +z 向きに出発、s=道に沿った距離。
// 流れ：追い風のカーブ → 直線 → 左の斜め風 → 右の斜め風 → CP1 → 谷1（切れ目の空中から斜め突風で向こう岸へ、人は左の細道）
//      → 谷2（斜め突風で少し高い上の段へ、人は左の坂）→ CP2 → 小さな螺旋風が邪魔する直線 → ゴール
public static class FuwaCourse1Builder
{
    public const float Top = 3f, Thick = 0.3f, Bevel = 0.08f;
    const int BevelSteps = 4;
    const float Stripe = 0.75f;
    public const float Upper = 2.6f;   // 上の段の高さ

    // 中心線：直線0-3 → 左カーブR30(3-17) → 直線17-49 → 右カーブR30(49-61) → 直線61-
    static readonly float[] SegEnd = { 3f, 17f, 23.5f, 29.5f, 80f, 92f, 1e9f };
    static readonly float[] SegCurv = { 0f, -1f / 30f, 0f, 10f * Mathf.Deg2Rad / 6f, 0f, 1f / 30f, 0f };   // 斜め風の区間の入り口で右へ10°（2026-10-03）   // ゆるく左へ（コース2から離れる）→谷のあとでゆるく右へ戻す（コース0に近づかない）

    // かたまり：0 下の道 / 1 谷の向こうの着地点 / 2 上の段 / 3 ゴールの島 / 4 人用の細道（谷の横→そのまま坂で上の段へ）/ 5 ゴールへの細い橋
    // かたまり：0 下の道 / 1 谷1の向こう岸 / 2 上の段（ゴールまで）/ 3 人用の細道（谷1の横）/ 4 人用の坂（谷2の横）
    public const float GapA1 = 55f, GapB1 = 61f, GapA2 = 69f, GapB2 = 75f, PathEnd = 106.4f;   // 道はゴールの広場（半径3m）のふちまで
    public static readonly Vector2[] Pieces =
    {
        new Vector2(0f, GapA1), new Vector2(GapB1, GapA2), new Vector2(GapB2, PathEnd),
        new Vector2(53f, 63f), new Vector2(67f, 76f),
    };
    static readonly Vector2[][] HeightKeys =
    {
        new[] { new Vector2(0, 0), new Vector2(200, 0) },
        new[] { new Vector2(0, 0), new Vector2(200, 0) },
        new[] { new Vector2(0, Upper), new Vector2(200, Upper) },
        new[] { new Vector2(0, 0), new Vector2(200, 0) },
        new[] { new Vector2(68f, 0), new Vector2(74.8f, Upper) },
    };

    public static float Width(int piece, float s)
    {
        if (piece == 3 || piece == 4) return 1.0f;
        if (piece == 2) return Mathf.Lerp(3f, PathNarrow, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(80f, 82.5f, s)));   // 螺旋風の区間は細い道
        // 横風の区間は広め（左右に流されても落ちにくく）
        if (piece == 0) return Mathf.Lerp(3f, 4.6f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(23.5f, 25.5f, s)) * (1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(43.5f, 45.5f, s))));
        return 3f;
    }

    public static float Offset(int piece, float s)
    {
        if (piece == 3 || piece == 4) return -(1.5f + 0.5f);   // 本道の左ふちにくっつける
        if (piece == 2) return WhirlShift(s);
        return 0f;
    }


    // 螺旋風の所は、風の下を穴（道なし）にして、道は反対側へふくらんでよける（うねうねの道）
    public const float PathNarrow = 2.3f;
    public const float WhirlSide = 1.75f - PathNarrow * 0.5f + 0.9f - 0.8f;   // 螺旋風の円が道のふちに80cmかかる     // 螺旋風の中心の横位置（もとの中心線から）
    static float WhirlBump(float d) { return Mathf.Exp(-(d * d) / (2f * 1.9f * 1.9f)); }   // 曲がりの半径が道の半分より大きくなる幅（内側がつぶれないように）
    public static float WhirlShift(float s)
    {
        float sh = 0f;
        for (int i = 0; i < WhirlS.Length; i++) sh += -(i % 2 == 0 ? 1f : -1f) * 1.75f * WhirlBump(s - WhirlS[i]);
        return sh;
    }
    static float WhirlWiden(float s)
    {
        float w = 0f;
        for (int i = 0; i < WhirlS.Length; i++) w = Mathf.Max(w, WhirlBump(s - WhirlS[i]));
        return w;
    }

    public static float Height(int piece, float s)
    {
        var keys = HeightKeys[piece];
        if (piece != 4) return Linear(keys, s);
        // 坂は折れ線を ±0.8m でならす
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

    // Startローカルの点（横=右が+, 高さ=上面からの差）
    public static Vector3 Point(int piece, float s, float lateral, float dy)
    {
        // 横ずれ（Offset）を足した実際の道の中心線を求めて、断面はその中心線に直角に置く（うねる所で道がゆがまないように）
        Vector2 C(float ss) { Center(ss, out var pp, out var dd); return pp + new Vector2(dd.y, -dd.x) * Offset(piece, ss); }
        var c = C(s);
        var t = C(s + 0.05f) - C(s - 0.05f);
        t = t.sqrMagnitude > 1e-8f ? t.normalized : Vector2.up;
        var r = new Vector2(t.y, -t.x);
        var q = c + r * lateral;
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
            if (r.name == "FuwaCourse") return r.transform;
        return null;
    }

    [MenuItem("FuwaCourse/Course1/Rebuild Path")]
    public static void RebuildMenu() { Debug.Log(Rebuild()); }

    public static string Rebuild()
    {
        var root = Root();
        var m = ToCourse(root);
        var vis = new Mesh { name = "Course1Path_Kaze_Bevel" };
        var col = new Mesh { name = "Course1Path_Kaze" };
        BuildVisual(vis, m);
        BuildCollider(col, m);
        Save(vis, "Assets/FuwaCourse/Meshes/Course1Path_Kaze_Bevel.asset");
        Save(col, "Assets/FuwaCourse/Meshes/Course1Path_Kaze.asset");
        var pm = root.Find("PathMesh");
        Undo.RecordObject(pm.GetComponent<MeshFilter>(), "path");
        pm.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Course1Path_Kaze_Bevel.asset");
        var mc = pm.GetComponent<MeshCollider>();
        mc.sharedMesh = null;
        mc.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Course1Path_Kaze.asset");
        EditorUtility.SetDirty(pm.gameObject);
        BuildBridges(root, m);
        return "Course1 path rebuilt: verts " + vis.vertexCount;
    }

    public const float CP1 = 46f, CP2 = 79f, GoalS = 109f;

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


    // ---- ギミック（Gimmicks_Kaze の下に毎回作り直す。見た目の元はコース2・旧コース1から複製） ----
    public const float TailA = 2.5f, TailB = 14f;
    public const float CrossL = 28.5f, CrossR = 41f, CrossLen = 5f;
    public static readonly float[] WhirlS = { 85f, 91f, 97f, 103f };

    public static Transform FindIn(string rootName, string path)
    {
        foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == rootName) return r.transform.Find(path);
        return null;
    }

    public static GameObject Dup(Transform src, Transform parent, string name)
    {
        var go = Object.Instantiate(src.gameObject, parent);
        go.name = name;
        go.SetActive(true);
        return go;
    }

    static void Tint(ParticleSystem ps, Color c)
    {
        var m = ps.main; m.startColor = c;
    }


    // 風のエリアの地面ハイライトを、今の道の形に合わせて作り直す。
    // zone（BoxCollider の箱）の真下で道の上面に乗っている所だけ、uv.x＝風の向きに沿った距離(m)。
    // 頂点アルファで、箱のふち・道のふちをぼかす。メッシュは hl のローカル座標で作る（親のスケール込み）。
    public static Mesh PathHighlight(Transform hl, Transform zone, Vector3 windLocal, string assetName, float fade = 0.6f, bool fadeStart = true, bool fadeEnd = true, float uvOffset = 0f)
    {
        var col = Root().Find("PathMesh").GetComponent<MeshCollider>();
        Physics.SyncTransforms();
        var box = zone.GetComponent<BoxCollider>();
        Vector3 bc = box != null ? box.center : Vector3.zero, bs = box != null ? box.size : Vector3.one;
        // 箱の4隅（ワールド）から範囲を決める
        var ax = zone.TransformVector(new Vector3(bs.x, 0, 0)); var az = zone.TransformVector(new Vector3(0, 0, bs.z));
        var c0 = zone.TransformPoint(bc);
        float lx = ax.magnitude, lz = az.magnitude; var ux = ax.normalized; var uz = az.normalized;
        var wind = zone.TransformDirection(windLocal); wind.y = 0; wind.Normalize();
        const float step = 0.12f;
        int nx = Mathf.CeilToInt(lx / step), nz = Mathf.CeilToInt(lz / step);
        var idx = new Dictionary<Vector2Int, int>();
        var V = new List<Vector3>(); var UV = new List<Vector2>(); var C = new List<Color>(); var T = new List<int>();
        bool OnPath(Vector3 w, out float y)
        {
            y = 0;
            if (col.Raycast(new Ray(new Vector3(w.x, c0.y + 30f, w.z), Vector3.down), out var h, 80f) && h.normal.y > 0.7f) { y = h.point.y; return true; }
            return false;
        }
        for (int i = 0; i <= nx; i++) for (int k = 0; k <= nz; k++)
        {
            float fx = i / (float)nx - 0.5f, fz = k / (float)nz - 0.5f;
            var w = c0 + ux * (fx * lx) + uz * (fz * lz);
            if (!OnPath(w, out float y)) continue;
            float edge = 1f;
            for (int a = 0; a < 8; a++)
            {
                var d = Quaternion.Euler(0, a * 45f, 0) * Vector3.forward;
                for (int st = 1; st <= 3; st++) { if (!OnPath(w + d * (0.12f * st), out _)) { edge = Mathf.Min(edge, (st - 1) / 3f); break; } }
            }
            float bx = Mathf.Min(fx + 0.5f, 0.5f - fx) * lx, bz = Mathf.Min(fadeStart ? (fz + 0.5f) * lz : 999f, fadeEnd ? (0.5f - fz) * lz : 999f);
            float boxFade = Mathf.Clamp01(Mathf.Min(bx, bz) / fade);
            idx[new Vector2Int(i, k)] = V.Count;
            V.Add(hl.InverseTransformPoint(new Vector3(w.x, y + 0.015f, w.z)));
            UV.Add(new Vector2(Vector3.Dot(w - c0, wind) + uvOffset, 0));
            C.Add(new Color(1, 1, 1, Mathf.Min(edge, boxFade)));
        }
        foreach (var kv in idx)
        {
            var g = kv.Key;
            if (idx.TryGetValue(g + new Vector2Int(1, 0), out int b) && idx.TryGetValue(g + new Vector2Int(0, 1), out int d) && idx.TryGetValue(g + new Vector2Int(1, 1), out int e))
                T.AddRange(new[] { kv.Value, d, b, b, d, e });
        }
        string path = "Assets/FuwaCourse/Meshes/" + assetName + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear(); mesh.name = assetName;
        mesh.SetVertices(V); mesh.SetUVs(0, UV); mesh.SetColors(C); mesh.SetTriangles(T, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        hl.GetComponent<MeshFilter>().sharedMesh = mesh;
        return mesh;
    }


    // つむじ風（コース3でも使う）。parent の下に作る。localPos/rot は道の上面の位置
    public static GameObject MakeWhirlwind(Transform parent, Vector3 localPos, Quaternion localRot, float upper)
    {
        var tplUpdraft = FindIn("FuwaCourse2", "Gimmick_Updrafts/Updraft1");
        var g = new GameObject("G5_Whirlwind", typeof(BoxCollider)); g.transform.SetParent(parent, false);
        g.transform.localPosition = localPos; g.transform.localRotation = localRot;
        var bc = g.GetComponent<BoxCollider>(); bc.isTrigger = true; bc.center = new Vector3(0, (upper + 1.6f) * 0.5f + 0.3f, 0.9f); bc.size = new Vector3(3.2f, upper + 2.2f, 4.8f);
        var ww = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaWhirlwind>(g);
        ww.radius = 1.5f; ww.height = upper + 1.6f; ww.exitPush = 5.5f; ww.exitFrom = 0.75f;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ww);
        g.layer = 2;
        var swirlDefs = new[] { (tplUpdraft.Find("UpdraftSwirl"), "Swirl", 70f, 2.2f, 2.6f), (tplUpdraft.Find("UpdraftStreaks"), "Streaks", 45f, 1.6f, 3.6f) };
        foreach (var (tpl, nm, rate, life, up) in swirlDefs)
        {
            var ps = Dup(tpl, g.transform, nm).GetComponent<ParticleSystem>();
            ps.transform.localPosition = new Vector3(0, 0.05f, 0); ps.transform.localRotation = Quaternion.identity;
            var main = ps.main; main.startSpeed = 0f; main.startLifetime = life; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.25f; sh.radiusThickness = 0.3f; sh.rotation = new Vector3(90, 0, 0); sh.position = Vector3.zero;
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local;
            v.x = 0f; v.y = up; v.z = 0f; v.orbitalX = 0f; v.orbitalY = 2.4f; v.orbitalZ = 0f; v.radial = -0.15f;
            var em = ps.emission; em.rateOverTimeMultiplier = rate;
        }
        return g;
    }

    public static string BuildGimmicks()
    {
        var root = Root();
        var m = ToCourse(root);
        var old = root.Find("Gimmicks_Kaze");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject("Gimmicks_Kaze").transform;
        holder.SetParent(root, false);
        Undo.RegisterCreatedObjectUndo(holder.gameObject, "kaze");
        Vector3 P(int pc, float s, float lat, float dy) => m.MultiplyPoint3x4(Point(pc, s, lat, dy));
        Quaternion R(float s) => Quaternion.Euler(0, Yaw(root, s), 0);
        string log = "";

        var tplTailZone = FindIn("FuwaCourse2", "Gimmick_Tailwind/TailwindZone");
        var tplTailPs = FindIn("FuwaCourse2", "Gimmick_Tailwind/TailwindParticles");
        var tplCross = root.Find("BuildTemplates/Gimmick1_CrossWind");
        var tplUpdraft = FindIn("FuwaCourse2", "Gimmick_Updrafts/Updraft1");
        var tplPad = root.Find("BuildTemplates/Gimmick2_LaunchWall");

        // 1. 追い風のカーブ：短い箱を道に沿って並べる（それぞれ道の向きに吹く）
        {
            var g = new GameObject("G1_Tailwind").transform; g.SetParent(holder, false);
            int n = 5; float seg = (TailB - TailA) / n;
            for (int i = 0; i < n; i++)
            {
                float sc = TailA + seg * (i + 0.5f);
                var z = Dup(tplTailZone, g, "Zone" + i).transform;
                z.localPosition = P(0, sc, 0, 2.2f); z.localRotation = R(sc); z.localScale = new Vector3(3.0f, 4.4f, seg + 0.05f);
                var w = z.GetComponent<FuwaWindArea>(); w.localDirection = Vector3.forward; w.strength = 1.4f; w.offSeconds = 0f;
                var ps = Dup(tplTailPs, g, "Particles" + i).GetComponent<ParticleSystem>();
                ps.transform.localPosition = P(0, sc, 0, 1.9f); ps.transform.localRotation = R(sc) * Quaternion.Euler(0, 270, 0);
                var sh = ps.shape; sh.scale = new Vector3(seg, 3.2f, 2.6f);
                var em = ps.emission; em.rateOverTimeMultiplier = 90f * seg / 11.5f * 1.2f;
                { var vel = ps.velocityOverLifetime; vel.space = ParticleSystemSimulationSpace.Local; vel.x = 4.5f; var pmm = ps.main; pmm.startLifetime = 0.55f; }   // 区間ごとの道の向きに流す（カーブの外へはみ出さない長さ）
                { var pm = ps.main; pm.startColor = new Color(1f, 1f, 1f, 0.75f); }
                w.visual = ps.gameObject;
                // 床のグラデ（横風と同じもの）を、この区間の道に合わせて
                var hlSrc = tplCross.Find("CrossWind/GroundHighlight");
                if (hlSrc != null)
                {
                    var hl = Dup(hlSrc, z, "GroundHighlight").transform;
                    hl.localPosition = Vector3.zero; hl.localRotation = Quaternion.identity; hl.localScale = Vector3.one;
                    PathHighlight(hl, z, Vector3.forward, "Kaze_TailHighlight_" + i, 0.6f, i == 0, i == n - 1, seg * i - seg * 0.5f);
                    w.groundHighlight = hl.GetComponent<MeshRenderer>();
                }
                UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(w);
            }
            log += "tailwind ";
        }

        // 2. 横風：左に吹くゾーン → 右に吹くゾーン（交互ではなく場所で分ける。粒の色も分ける）
        var crossDefs = new[] { ("G2_CrossLeft", CrossL, -1f, 0f), ("G3_CrossRight", CrossR, 1f, 1.5f) };
        foreach (var (name, sc, dir, phase) in crossDefs)
        {
            var g = Dup(tplCross, holder, name).transform;
            g.localPosition = P(0, sc, 0, 0) + Vector3.down * Top;
            g.localRotation = R(sc);
            var zone = g.Find("CrossWind");
            zone.localScale = new Vector3(11f, 5f, CrossLen);
            var w = zone.GetComponent<FuwaWindArea>();
            w.alternate = false; w.localDirection = new Vector3(dir * 0.8f, 0, 0.6f).normalized; w.phaseOffset = phase;   // 斜め前への追い風（横に流しつつ前へ運ぶ）
            var psF = g.Find("WindParticles"); var psR = g.Find("WindParticlesReverse");
            var use = dir > 0 ? psF : psR; var unused = dir > 0 ? psR : psF;
            var ups = use.GetComponent<ParticleSystem>(); var sh = ups.shape; sh.scale = new Vector3(11f, 5f, CrossLen);
            {
                // 粒も風と同じ斜めの向きに流す（ゾーンのローカルで向きを決める）
                var vel = ups.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
                var wd = w.localDirection.normalized * 7f;
                var lpZone = use.InverseTransformDirection(zone.TransformDirection(wd).normalized) * 7f;
                vel.x = lpZone.x; vel.y = 0f; vel.z = lpZone.z;
            }
            w.visual = use.gameObject; w.visualReverse = null;
            var hlx = zone.Find("GroundHighlight"); if (hlx) PathHighlight(hlx, zone, w.localDirection, "Kaze_WindHighlight_" + name);
            Object.DestroyImmediate(unused.gameObject);
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(w);
        }
        log += "cross ";

        // 斜め突風：道の切れ目（谷の手前のふち）の下から、斜め前へ吹き上がる太い風の流れ。
        // 流れの中に入ったふわふわは、見た目どおり流れに沿って吹き飛ばされる（打ち出しではなく、流れの中で加速）
        void Gust(string name, float s, Vector3 dir, float length, float strength)
        {
            var g = new GameObject(name).transform; g.SetParent(holder, false);
            g.localPosition = P(0, s, 0, 0); g.localRotation = R(s);
            var d = dir.normalized;
            var start = new Vector3(0, -1.6f, 0.2f);   // 切れ目のすぐ先、道の上面より下
            // 風の流れ（箱）：ローカル z が流れの向き
            var zone = Dup(tplTailZone, g, "GustZone").transform;
            zone.localRotation = Quaternion.LookRotation(d, Vector3.up);
            zone.localPosition = start + d * (length * 0.5f);
            zone.localScale = new Vector3(2.8f, 2.4f, length);
            var w = zone.GetComponent<FuwaWindArea>();
            w.localDirection = Vector3.forward; w.strength = strength; w.fadeAlongDirection = 0.55f; w.offSeconds = 0f; w.visual = null;
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(w);
            // 見た目：上昇気流と同じ粒を、太く・流れの向きに
            var emit = new GameObject("GustEmitter").transform; emit.SetParent(g, false);
            emit.localPosition = start; emit.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            emit.localScale = new Vector3(2.6f, 1f, 2.6f);
            foreach (Transform c in tplUpdraft)
            {
                if (c.GetComponent<ParticleSystem>() == null) continue;
                var e = Dup(c, emit, c.name); e.transform.localPosition = Vector3.zero; e.transform.localRotation = Quaternion.identity;
                var ps = e.GetComponent<ParticleSystem>();
                var mm = ps.main; mm.scalingMode = ParticleSystemScalingMode.Hierarchy;
                var em = ps.emission; em.rateOverTimeMultiplier *= 2f;
                // 人用の上昇気流（いわやま）と見分けやすく：全体をうっすら緑に、まっすぐの筋は薄く・短く・細かく
                var col = mm.startColor.color;   // 風の色は白で統一（緑はのせない）
                if (c.name == "UpdraftStreaks")
                {
                    col.a *= 0.4f;
                    mm.startLifetime = mm.startLifetime.constant * 0.5f;
                    mm.startSize = mm.startSize.constant * 0.6f;
                    em.rateOverTimeMultiplier *= 1.5f;
                }
                mm.startColor = col;
            }
        }

        // 谷の上ではコースアウト判定をしない（落ちたら下の判定で）
        var gapList = new List<Collider>();
        void GapZone(string name, float a, float b, float h0)
        {
            var gz = new GameObject(name, typeof(BoxCollider)); gz.transform.SetParent(holder, false); gz.layer = 2;
            float sc = (a + b) * 0.5f;
            var bc = gz.GetComponent<BoxCollider>(); bc.isTrigger = true;
            gz.transform.localPosition = P(0, sc, 0, 1f + h0 * 0.5f); gz.transform.localRotation = R(sc);
            bc.size = new Vector3(5f, 12f + h0, b - a + 2f);
            gapList.Add(bc);
        }

        // 3. 1つ目の谷：同じ高さの向こう岸へ（人は左の細道で迂回）
        Gust("G3_Gust1", GapA1, new Vector3(0, 1f, 1.15f), 9f, 9f);
        GapZone("GapZone1", GapA1, GapB1, 0f);
        // 4. 2つ目の谷：少し高い上の段へ（人は左の坂で迂回）
        Gust("G4_Gust2", GapA2, new Vector3(0, 1.25f, 1f), 9.5f, 8.5f);
        GapZone("GapZone2", GapA2, GapB2, Upper);
        {
            var ball = Object.FindObjectOfType<FuwaBall>(true);
            var list = new List<Collider>();
            if (ball.gapZones != null) foreach (var c in ball.gapZones) if (c != null && !c.transform.IsChildOf(root)) list.Add(c);
            list.AddRange(gapList);
            Undo.RecordObject(ball, "gap"); ball.gapZones = list.ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ball);
        }
        log += "gusts ";

        // 5. 上の段の直線に、小さな螺旋風をいくつか置いて邪魔する
        {
            var g = new GameObject("G5_SmallWhirls").transform; g.SetParent(holder, false);
            for (int i = 0; i < WhirlS.Length; i++)
            {
                float s = WhirlS[i], lat = (i % 2 == 0 ? WhirlSide : -WhirlSide) - WhirlShift(s);   // 道は反対側へよけているので、中心線基準の位置に戻す
                var w = MakeWhirlwind(g, P(2, s, lat, 0), R(s), 0.6f);
                w.name = "Whirl" + i;
                var ww = w.GetComponent<FuwaWhirlwind>();
                ww.radius = 0.9f; ww.height = 1.9f; ww.lift = 2.2f; ww.swirl = 4.2f; ww.inward = 3.2f; ww.exitPush = 0f;   // 巻き込まれると抜けにくい
                ww.spinDir = (i % 2 == 0) ? 1f : -1f;
                UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ww);
                var bc = w.GetComponent<BoxCollider>(); bc.center = new Vector3(0, 1.05f, 0); bc.size = new Vector3(2.0f, 2.1f, 2.0f);
                foreach (var ps in w.GetComponentsInChildren<ParticleSystem>())
                {
                    var sh = ps.shape; sh.radius = 0.77f;
                    var main = ps.main; main.startLifetime = main.startLifetime.constant * 0.48f;
                }
            }
            log += "whirls ";
        }

        EditorUtility.SetDirty(root.gameObject);
        return log;
    }


    static Mesh DiscMesh(float r)
    {
        string path = "Assets/FuwaCourse/Meshes/VentHole_Disc.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear(); mesh.name = "VentHole_Disc";
        int n = 40; var V = new List<Vector3> { Vector3.zero }; var T = new List<int>();
        for (int i = 0; i <= n; i++) { float a = i * Mathf.PI * 2 / n; V.Add(new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)); }
        for (int i = 1; i <= n; i++) T.AddRange(new[] { 0, i + 1, i });
        mesh.SetVertices(V); mesh.SetTriangles(T, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh); return mesh;
    }

    static Mesh TorusMesh2(string name, float R, float r)
    {
        string path = "Assets/FuwaCourse/Meshes/" + name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear(); mesh.name = name;
        int U = 48, Vn = 10;
        var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new List<int>();
        for (int i = 0; i <= U; i++)
        {
            float a = i * Mathf.PI * 2 / U; var c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
            for (int j = 0; j <= Vn; j++) { float b = j * Mathf.PI * 2 / Vn; var nn = c * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b); V.Add(c * R + nn * r); N.Add(nn); }
        }
        for (int i = 0; i < U; i++) for (int j = 0; j < Vn; j++) { int a0 = i * (Vn + 1) + j, a1 = a0 + Vn + 1; T.AddRange(new[] { a0, a1, a0 + 1, a0 + 1, a1, a1 + 1 }); }
        var cr = Vector3.Cross(V[T[1]] - V[T[0]], V[T[2]] - V[T[0]]);
        if (Vector3.Dot(cr, N[T[0]]) < 0) for (int k = 0; k < T.Count; k += 3) { var t = T[k + 1]; T[k + 1] = T[k + 2]; T[k + 2] = t; }
        mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetTriangles(T, 0); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh); return mesh;
    }

    static Material WindRingMat()
    {
        string path = "Assets/FuwaCourse/Materials/WindRing.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("FuwaCourse/WindRing")); AssetDatabase.CreateAsset(mat, path); }
        return mat;
    }

    static Mesh TorusMesh(float R, float r)
    {
        string path = "Assets/FuwaCourse/Meshes/WindRing_Torus.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear(); mesh.name = "WindRing_Torus";
        int U = 64, Vn = 12;
        var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new List<int>(); var UV = new List<Vector2>();
        for (int i = 0; i <= U; i++)
        {
            float a = i * Mathf.PI * 2 / U; var c = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
            for (int j = 0; j <= Vn; j++)
            {
                float b = j * Mathf.PI * 2 / Vn; var n = c * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                V.Add(c * R + n * r); N.Add(n); UV.Add(new Vector2(i / (float)U, j / (float)Vn));
            }
        }
        for (int i = 0; i < U; i++) for (int j = 0; j < Vn; j++)
        {
            int a0 = i * (Vn + 1) + j, a1 = a0 + Vn + 1;
            T.AddRange(new[] { a0, a1, a0 + 1, a0 + 1, a1, a1 + 1 });
        }
        // 面の向きを外向き（法線の向き）にそろえる
        var cr = Vector3.Cross(V[T[1]] - V[T[0]], V[T[2]] - V[T[0]]);
        if (Vector3.Dot(cr, N[T[0]]) < 0) for (int k = 0; k < T.Count; k += 3) { var t = T[k + 1]; T[k + 1] = T[k + 2]; T[k + 2] = t; }
        mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, UV); mesh.SetTriangles(T, 0); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
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
        if (pc == 3 || pc == 4) return false;   // 細道・坂：両端とも本道の横にくっつく
        return true;
    }

    static void BuildVisual(Mesh mesh, Matrix4x4 m)
    {
        var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>();
        var A = new List<int>(); var Bt = new List<int>();
        const float step = 0.25f;
        for (int pc = 0; pc < Pieces.Length; pc++)
        {
            if (IsBridge(pc)) continue;   // 人用の細道・坂は木の橋（BuildBridges）
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

    static void BuildCollider(Mesh mesh, Matrix4x4 m, bool bridgesOnly = false)
    {
        var V = new List<Vector3>(); var T = new List<int>();
        const float step = 0.5f;
        for (int pc = 0; pc < Pieces.Length; pc++)
        {
            if (IsBridge(pc) != bridgesOnly) continue;   // 橋は別の当たり判定（飾りが乗らないように）
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


    // ---- 人用の細道・坂（かたまり3,4）は木の板の橋（いわやまの板張りと同じ作り）。当たり判定は別の BridgeCollider ----
    static bool IsBridge(int pc) { return pc == 3 || pc == 4; }

    static void BuildBridges(Transform root, Matrix4x4 m)
    {
        // 当たり判定（なめらかな箱）
        var col = new Mesh { name = "Course1Bridge_Col" };
        BuildCollider(col, m, true);
        Save(col, "Assets/FuwaCourse/Meshes/Course1Bridge_Col.asset");
        var bt = root.Find("BridgeCollider");
        GameObject bgo = bt != null ? bt.gameObject : null;
        if (bgo == null) { bgo = new GameObject("BridgeCollider", typeof(MeshCollider)); bgo.transform.SetParent(root, false); Undo.RegisterCreatedObjectUndo(bgo, "bridge"); }
        bgo.layer = root.Find("PathMesh").gameObject.layer;
        var mc = bgo.GetComponent<MeshCollider>(); mc.sharedMesh = null; mc.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Course1Bridge_Col.asset");

        // 見た目：横板（すき間あり・長さと色バラつき）＋両わきの角材
        const float PlankLen = 0.26f, PlankGap = 0.045f, PlankT = 0.07f, BeamW = 0.1f, BeamH = 0.18f;
        var rnd = new System.Random(11);
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        var V = new List<Vector3>(); var TA = new List<int>(); var TB = new List<int>(); var TC = new List<int>();
        int nPl = 0;
        for (int pc = 0; pc < Pieces.Length; pc++)
        {
            if (!IsBridge(pc)) continue;
            Vector3 P(float s, float lat, float dy) { return m.MultiplyPoint3x4(Point(pc, s, lat, dy)); }
            float s0 = Pieces[pc].x, s1 = Pieces[pc].y, hw = Width(pc, s0) * 0.5f;
            for (float s = s0 + 0.02f; s + PlankLen <= s1 - 0.02f; s += PlankLen + PlankGap)
            {
                float a = s + R(-0.012f, 0.012f), b = s + PlankLen + R(-0.012f, 0.012f);
                // 本道側（右）は道のふちにさわらないよう少し離す、外側（左）は少しはみ出してバラつかせる
                float l0 = -hw - 0.04f + R(-0.05f, 0.04f), l1 = hw - 0.05f;
                float lift = R(0f, 0.012f), tilt = R(-0.01f, 0.01f);
                AddBox(V, (R(0, 1) < 0.5f) ? TA : TB, new[] {
                    P(a, l0, -PlankT + lift - tilt), P(b, l0, -PlankT + lift - tilt), P(b, l1, -PlankT + lift + tilt), P(a, l1, -PlankT + lift + tilt),
                    P(a, l0, lift - tilt), P(b, l0, lift - tilt), P(b, l1, lift + tilt), P(a, l1, lift + tilt) });
                nPl++;
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float lc = side * (hw - 0.16f);
                int n = Mathf.CeilToInt((s1 - s0 - 0.1f) / 0.5f);
                for (int k = 0; k < n; k++)
                {
                    float a = Mathf.Lerp(s0 + 0.05f, s1 - 0.05f, k / (float)n), b = Mathf.Lerp(s0 + 0.05f, s1 - 0.05f, (k + 1) / (float)n);
                    float t0 = -PlankT - 0.002f, t1 = t0 - BeamH;
                    AddBox(V, TC, new[] {
                        P(a, lc - BeamW / 2, t1), P(b, lc - BeamW / 2, t1), P(b, lc + BeamW / 2, t1), P(a, lc + BeamW / 2, t1),
                        P(a, lc - BeamW / 2, t0), P(b, lc - BeamW / 2, t0), P(b, lc + BeamW / 2, t0), P(a, lc + BeamW / 2, t0) });
                }
            }
        }
        var mesh = new Mesh { name = "Course1_BridgePlanks", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(V); mesh.subMeshCount = 3; mesh.SetTriangles(TA, 0); mesh.SetTriangles(TB, 1); mesh.SetTriangles(TC, 2);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        Save(mesh, "Assets/FuwaCourse/Meshes/Course1_BridgePlanks.asset");
        var pt = root.Find("BridgePlanks");
        GameObject go = pt != null ? pt.gameObject : null;
        if (go == null) { go = new GameObject("BridgePlanks", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(root, false); Undo.RegisterCreatedObjectUndo(go, "planks"); }
        go.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/Course1_BridgePlanks.asset");
        go.GetComponent<MeshRenderer>().sharedMaterials = new[] {
            AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course2_PlankA.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course2_PlankB.mat"),
            AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course2_Beam.mat") };
        EditorUtility.SetDirty(go);
    }

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

    static void Save(Mesh m, string path)
    {
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { FuwaBevelTools.CopyInto(old, m); old.UploadMeshData(false); EditorUtility.SetDirty(old); }
        else AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
    }
}
