using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// コース2「いわのみち」のまわりの浮島（2026-10-12 てじゃ「いわのみちだけ周りに浮いてるものなくて寂しい」「浮島に噴気孔とか、湯気でてる温泉とか」）
// ・かぜのおかのたんぽぽの島と同じ島の形（IslandDisc ＋ IslandUnderLow）。地面は砂っぽい色
// ・温泉の島：岩でかこんだ水色のお湯（FuwaCourse/HotSpring）＋ゆらゆら湯気
// ・噴気孔の島：岩の輪の真ん中の穴から、ときどきシュッと湯気（コースの噴気孔の Fx をコピー。当たり判定・音なし、見た目だけ）
// ・岩の島：大きい岩がゴロッと
// 作り直しても島の場所は変えない（前の島の位置・大きさ・向き・種類を覚えておいて使う）
public static class FuwaIwaIslands
{
    const string Course = "FuwaCourse2";
    const string Dir = "Assets/FuwaCourse/Generated/IwaIslands";
    enum Kind { Spring, Vent, Rocks }
    // 並び順（島の数もこれ）
    static readonly Kind[] Kinds = { Kind.Spring, Kind.Vent, Kind.Rocks, Kind.Spring, Kind.Vent, Kind.Vent, Kind.Spring, Kind.Rocks };

    // 頂上の温泉：道の南側のふちを削って（ApplySummitCut）広げた。2026-10-12 てじゃ「道ちょっと削っておおきくしよ」
    static readonly Vector3 SummitSpringPos = new Vector3(107.45f, 14.06f, 0.95f);
    static readonly Vector2 SummitSpringSize = new Vector2(1.75f, 0.75f);   // お湯の半径 x（道に沿って）, z

    [MenuItem("FuwaCourse/いわのみちのまわりの浮島を作り直す")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run(bool forceNewPlaces = false)
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "IwaIslands");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var root = roots.First(g => g.name == Course).transform;
        var path = root.Find("PathMesh");
        var topMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/IslandDisc/IslandDisc_r64.asset");
        var underMeshes = new[] { 0, 1, 2 }.Select(i => AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/FarIslands/IslandUnderLow_" + i + ".asset")).Where(m => m != null).ToArray();
        var underMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/IslandUnder.mat");
        var rockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Rock.mat");
        var rocks = new[] { "CrumbleRock1", "CrumbleRock2", "CrumbleRock4" }.Select(n => AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/" + n + ".asset")).ToArray();
        GameObject Prefab(string n) { return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/FuwaCourse/Prefabs/Deco/" + n + ".prefab"); }
        var stoneL = Prefab("Deco_StoneLarge"); var stoneM = Prefab("Deco_StonesMid"); var stoneS = Prefab("Deco_StonesSmall"); var grass = Prefab("Deco_GrassSmall"); var pebbles = Prefab("Deco_Pebbles");
        var ventFx = root.Find("Gimmick_FieldVents/Vent1/Fx");
        var groundMat = GroundMat(); var waterMat = WaterMat(); var holeMat = HoleMat(rockMat);

        var rnd = new System.Random(2112);
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        var log = new List<string>();

        var pv = path.GetComponentsInChildren<MeshFilter>().SelectMany(mf => mf.sharedMesh.vertices.Where((v, i) => i % 3 == 0).Select(v => mf.transform.TransformPoint(v))).ToList();
        float DistPath(Vector3 c) { return pv.Min(q => new Vector2(q.x - c.x, q.z - c.z).magnitude); }
        // 他のコース・ロビー（小さい物は箱、大きい物は頂点）と、自分のコースの物に近づけない（FuwaKazeDeco と同じ考え方）
        var otherR = roots.Where(g => g.name.StartsWith("FuwaCourse") && g.name != Course || g.name.Contains("Lobby") || g.name == "FuwaHub")
            .SelectMany(g => g.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer))).ToList();
        var other = otherR.Select(r => r.bounds).Where(b => b.size.magnitude < 12f).ToList();
        var pts = new List<Vector3>();
        foreach (var r in otherR.Where(r => r.bounds.size.magnitude >= 12f && r.bounds.size.magnitude < 400f))
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) { other.Add(r.bounds); continue; }
            var vv = mf.sharedMesh.vertices; int stp = Mathf.Max(1, vv.Length / 400);
            for (int i = 0; i < vv.Length; i += stp) pts.Add(r.transform.TransformPoint(vv[i]));
        }
        var deco = root.Find("Deco");
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
        {
            if (deco != null && r.transform.IsChildOf(deco)) continue;
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            var vv = mf.sharedMesh.vertices; int stp = Mathf.Max(1, vv.Length / 150);
            for (int i = 0; i < vv.Length; i += stp) pts.Add(r.transform.TransformPoint(vv[i]));
        }
        float DistAny(Bounds b)
        {
            float d = other.Count == 0 ? 999f : other.Min(o => Mathf.Sqrt(o.SqrDistance(b.ClosestPoint(o.center))));
            foreach (var p in pts) { float e = Mathf.Sqrt(b.SqrDistance(p)); if (e < d) d = e; }
            return d;
        }

        if (deco == null) { deco = new GameObject("Deco").transform; deco.SetParent(root, false); }
        var holder = deco.Find("IwaIslands");
        var keep = new List<(Vector3 pos, float rad, Quaternion rot)>();
        if (holder != null && !forceNewPlaces)
            foreach (Transform c in holder) { var tp = c.Find("IslandTop"); if (tp != null) keep.Add((c.position, tp.localScale.x * 0.64f, c.rotation)); }
        if (holder != null) Object.DestroyImmediate(holder.gameObject);
        holder = new GameObject("IwaIslands").transform; holder.SetParent(deco, false);

        var placed = new List<(Vector3 pos, float rad)>();
        int tries = 0;
        while (placed.Count < Kinds.Length && tries++ < 20000)
        {
            int idx = placed.Count; Kind kind = Kinds[idx];
            Vector3 pos; float rad; Quaternion rot;
            if (idx < keep.Count) (pos, rad, rot) = keep[idx];
            else
            {
                var anchor = pv[rnd.Next(pv.Count)];
                float ang = R(0, Mathf.PI * 2), dist = R(10f, 24f);
                rad = kind == Kind.Spring ? R(3.0f, 3.6f) : R(2.4f, 3.2f);
                pos = new Vector3(anchor.x + Mathf.Cos(ang) * dist, anchor.y + R(-6f, 0.5f), anchor.z + Mathf.Sin(ang) * dist);
                float dp = DistPath(pos); if (dp < 8f + rad || dp > 26f) continue;
                if (placed.Any(q => new Vector2(q.pos.x - pos.x, q.pos.z - pos.z).magnitude < q.rad + rad + 6f)) continue;
                var bb = new Bounds(pos + Vector3.up * 1f, new Vector3(rad * 2 + 2, 7f, rad * 2 + 2));
                if (DistAny(bb) < 5f) continue;
                rot = Quaternion.Euler(0, R(0, 360), 0);
            }
            placed.Add((pos, rad));

            var g = new GameObject("Island" + (idx + 1) + "_" + kind).transform; g.SetParent(holder, false); g.position = pos; g.rotation = rot;
            var top = Part(g, "IslandTop", topMesh, groundMat, Vector3.zero, Quaternion.identity, new Vector3(rad / 0.64f, 1f, rad / 0.64f), true);
            var uy = R(0.85f, 1.15f);
            Part(g, "IslandUnder", underMeshes[rnd.Next(underMeshes.Length)], underMat, Vector3.zero, Quaternion.Euler(0, R(0, 360), 0), new Vector3(rad / 0.97f, rad / 0.97f * uy, rad / 0.97f), true);
            const float topY = 0.03f;   // 島の上の面（ローカル）

            // 置いた物の場所（重ならないように）
            var used = new List<(Vector2 p, float r)>();
            bool Free(Vector2 p, float r) { return p.magnitude + r < rad * 0.92f && used.All(u => (u.p - p).magnitude > u.r + r); }
            Vector2 RandIn(float maxR) { float a = R(0, Mathf.PI * 2), d = Mathf.Sqrt(R(0, 1)) * maxR; return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d; }

            if (kind == Kind.Spring)
            {
                float pr = rad * R(0.4f, 0.48f);
                var c = RandIn(rad * 0.15f);
                var pool = new GameObject("Spring").transform; pool.SetParent(g, false); pool.localPosition = new Vector3(c.x, topY + 0.06f, c.y);
                var pm = PoolMesh(pr, rnd, Dir + "/IwaSpring_" + (idx + 1) + ".asset");
                Part(pool, "Water", pm, waterMat, Vector3.zero, Quaternion.identity, Vector3.one, true);
                // ふちの石：大きい平たい石と、ごろっとした岩を交互に、ぐるっと
                int n = Mathf.RoundToInt(2f * Mathf.PI * pr / 0.42f);
                for (int k = 0; k < n; k++)
                {
                    float a = (k + R(-0.2f, 0.2f)) / n * Mathf.PI * 2f; float er = EdgeR(pm, a) + R(-0.02f, 0.08f);
                    var p = new Vector3(Mathf.Cos(a) * er, -0.04f, Mathf.Sin(a) * er);
                    if (k % 3 == 1) RockMesh(pool, rocks[rnd.Next(rocks.Length)], rockMat, p + Vector3.down * 0.05f, R(0.32f, 0.46f), rnd);
                    else Inst(pool, stoneL, p, Quaternion.Euler(R(-8, 8), R(0, 360), R(-8, 8)), Vector3.one * R(1.3f, 1.9f));
                }
                used.Add((c, pr + 0.35f));
                Steam(pool, pr, ventFx);
                // そばに大きめの岩をひとつ（温泉らしく）
                for (int t = 0; t < 40; t++) { var p = RandIn(rad * 0.85f); if (!Free(p, 0.6f)) continue; RockMesh(g, rocks[rnd.Next(rocks.Length)], rockMat, new Vector3(p.x, topY - 0.15f, p.y), R(0.8f, 1.15f), rnd); used.Add((p, 0.6f)); break; }
            }
            else if (kind == Kind.Vent)
            {
                var c = RandIn(rad * 0.2f);
                var v = new GameObject("Vent").transform; v.SetParent(g, false); v.localPosition = new Vector3(c.x, topY, c.y);
                // コースの噴気孔のような、まん中に穴のあいた岩の小山（＋ふもとに岩を少し）
                Part(v, "Mound", MoundMesh(Dir + "/IwaVentMound_" + (idx + 1) + ".asset", rnd), rockMat, Vector3.zero, Quaternion.Euler(0, R(0, 360), 0), Vector3.one, true);
                for (int k = 0; k < 3; k++)
                {
                    float a = (k + R(-0.25f, 0.25f)) / 3f * Mathf.PI * 2f, rr = R(0.95f, 1.15f);
                    RockMesh(v, rocks[rnd.Next(rocks.Length)], rockMat, new Vector3(Mathf.Cos(a) * rr, -0.1f, Mathf.Sin(a) * rr), R(0.35f, 0.5f), rnd);
                }
                Part(v, "Hole", HoleMesh(Dir + "/IwaVentHole.asset"), holeMat, new Vector3(0, MoundHoleY - 0.02f, 0), Quaternion.identity, Vector3.one * MoundHoleR, false);
                if (ventFx != null) VentFx(v, ventFx, R(4.5f, 8f), R(0f, 4f), rnd);
                used.Add((c, 0.95f));
                // まわりに転がる岩
                for (int t = 0, m = 0; t < 60 && m < 2; t++) { var p = RandIn(rad * 0.85f); if (!Free(p, 0.5f)) continue; RockMesh(g, rocks[rnd.Next(rocks.Length)], rockMat, new Vector3(p.x, topY - 0.12f, p.y), R(0.55f, 0.85f), rnd); used.Add((p, 0.5f)); m++; }
            }
            else
            {
                // 岩の島：大きい岩を2〜3段に積んだ岩の塔（＋ふもとに岩を1〜2個）。岩のメッシュは中心が原点で高さ約0.86
                var c = RandIn(rad * 0.25f);
                float s0 = R(1.9f, 2.3f), y = topY - 0.3f * s0;
                var lean = new Vector2(R(-0.15f, 0.15f), R(-0.15f, 0.15f));
                int levels = rnd.Next(2, 4); float sz = s0;
                for (int k = 0; k < levels; k++)
                {
                    var p = c + lean * k;
                    var rk = RockMesh(g, rocks[rnd.Next(rocks.Length)], rockMat, new Vector3(p.x, y, p.y), sz, rnd);
                    rk.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    float h = 0.43f * rk.localScale.y;
                    float next = sz * R(0.55f, 0.68f);
                    y += h + 0.43f * next * 0.55f; sz = next;
                }
                used.Add((c, s0 * 0.5f));
                for (int t = 0, k = 0; t < 60 && k < 2; t++) { var p = RandIn(rad * 0.85f); if (!Free(p, 0.5f)) continue; RockMesh(g, rocks[rnd.Next(rocks.Length)], rockMat, new Vector3(p.x, topY + 0.05f, p.y), R(0.7f, 1.0f), rnd); used.Add((p, 0.5f)); k++; }
            }
            // 小石・草（島のふち寄り）
            for (int t = 0, k = 0; t < 120 && k < 9; t++)
            {
                var p = RandIn(rad * 0.88f); if (p.magnitude < rad * 0.35f || !Free(p, 0.2f)) continue;
                var pick = k % 3 == 0 ? stoneM : k % 3 == 1 ? grass : (rnd.NextDouble() < 0.5 ? stoneS : pebbles);
                if (pick == null) continue;
                Inst(g, pick, new Vector3(p.x, topY, p.y), Quaternion.Euler(0, R(0, 360), 0), Vector3.one * (pick == grass ? R(1.4f, 2.0f) : R(1.0f, 1.6f)));
                used.Add((p, 0.2f)); k++;
            }
            log.Add(g.name + " " + pos.ToString("F0") + " r" + rad.ToString("F1") + " path " + DistPath(pos).ToString("F0"));
        }
        log.Add(ApplySummitCut());
        // 頂上の崖っぷちの温泉（2026-10-12 てじゃ「頂上のこの崖っぷちも温泉にしよ、邪魔にならんし」）
        // 頂上の木の足場の向かい、道の南側（-z）の平らな岩（y≒14.0、x 105.5〜110、z 0〜1.2）。道に沿って細長く
        {
            var sp = new GameObject("SummitSpring").transform; sp.SetParent(holder, false);
            sp.position = SummitSpringPos; sp.rotation = Quaternion.identity;
            var pm = PoolMesh(1f, new System.Random(31), Dir + "/IwaSpring_Summit.asset");
            Part(sp, "Water", pm, waterMat, Vector3.zero, Quaternion.identity, new Vector3(SummitSpringSize.x, 1f, SummitSpringSize.y), true);
            var srnd = new System.Random(77);
            float SR(float a, float b) { return a + (float)srnd.NextDouble() * (b - a); }
            int n = 18;
            for (int k = 0; k < n; k++)
            {
                float a = (k + SR(-0.2f, 0.2f)) / n * Mathf.PI * 2f, er = EdgeR(pm, a);
                var p = new Vector3(Mathf.Cos(a) * er * SummitSpringSize.x, -0.04f, Mathf.Sin(a) * er * SummitSpringSize.y) * 1f;
                p += new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * SR(0.0f, 0.08f);
                bool roadSide = Mathf.Sin(a) > 0.3f;   // 道の側（+z）は小さい平たい石だけ（道にはみ出さない）
                if (!roadSide && k % 3 == 1) RockMesh(sp, rocks[srnd.Next(rocks.Length)], rockMat, p + Vector3.down * 0.05f, SR(0.3f, 0.42f), srnd);
                else Inst(sp, stoneL, p, Quaternion.Euler(SR(-8, 8), SR(0, 360), SR(-8, 8)), Vector3.one * (roadSide ? SR(0.9f, 1.2f) : SR(1.3f, 1.8f)));
            }
            Steam(sp, Mathf.Min(SummitSpringSize.x, SummitSpringSize.y) * 1.3f, ventFx);
            var st = sp.Find("Steam");
            if (st != null)
            {
                var sps = st.GetComponent<ParticleSystem>();
                var shp = sps.shape; shp.scale = new Vector3(SummitSpringSize.x / SummitSpringSize.y, 1f, 1f);
                // 道のすぐ横なので湯気はひかえめに（コースの見通しのじゃまにならないように）
                var sem = sps.emission; sem.rateOverTime = 4f;
                var sm = sps.main; sm.startColor = new Color(1f, 1f, 1f, 0.35f); sm.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            }
            log.Add("SummitSpring " + sp.position.ToString("F1"));
        }

        // 島の物は動かないので静的に（影は小物は落とさない）
        foreach (var t in holder.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<ParticleSystem>() != null || t.GetComponentInParent<ParticleSystem>() != null) continue;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return "IwaIslands " + placed.Count + "/" + Kinds.Length + " (kept " + keep.Count + ")\n" + string.Join("\n", log);
    }

    // ---- 頂上の道のふちを削る ----
    // 頂上の平らな道（幅3m、y 14.0〜14.3、x 105.5〜113、南のふち z≒1.13）の南側を、スタート側のはし（x 105.5）〜温泉の先（x 109）だけ
    // CutDepth m 北へ寄せる（その先 1.3m でゆるやかに元の幅へ）。南半分を縮めるように動かすので、ふちの丸み（ベベル）の形は残る。
    // 元のメッシュは Meshes/Backup に *_PreSpringCut として残し、いつもそこから作るので何度やっても同じ。
    // ※FuwaCourse2Builder で道を作り直したらこの削りは消える（作り直した後にこのメニューをもう一度）
    const float CutDepth = 0.7f, CutX0 = 104.5f, CutFull = 109.0f, CutEnd = 110.3f, CutSouth = 1.13f, CutMid = 2.6f;
    static string ApplySummitCut()
    {
        var root = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == Course).transform;
        var pm = root.Find("PathMesh"); var tr = pm.transform;
        var mf = pm.GetComponent<MeshFilter>(); var mc = pm.GetComponent<MeshCollider>();
        int moved = 0;
        foreach (var live in new[] { mf.sharedMesh, mc != null ? mc.sharedMesh : null })
        {
            if (live == null) continue;
            string lp = AssetDatabase.GetAssetPath(live);
            string bp = "Assets/FuwaCourse/Meshes/Backup/" + System.IO.Path.GetFileNameWithoutExtension(lp) + "_PreSpringCut.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(bp) == null) AssetDatabase.CopyAsset(lp, bp);
            var src = AssetDatabase.LoadAssetAtPath<Mesh>(bp);
            var vs = src.vertices;
            for (int i = 0; i < vs.Length; i++)
            {
                var w = tr.TransformPoint(vs[i]);
                if (w.x < CutX0 || w.x > CutEnd || w.y < 13.8f || w.z > CutMid) continue;
                float d = CutDepth * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CutFull, CutEnd, w.x)));
                float k = Mathf.Clamp01((CutMid - w.z) / (CutMid - CutSouth));
                w.z += d * k; vs[i] = tr.InverseTransformPoint(w); moved++;
            }
            live.vertices = vs; live.RecalculateBounds(); EditorUtility.SetDirty(live);
        }
        if (mc != null) { var m = mc.sharedMesh; mc.sharedMesh = null; mc.sharedMesh = m; }
        return "summit cut: moved " + moved;
    }

    // ---- 部品 ----
    static Transform Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 lp, Quaternion lr, Vector3 ls, bool shadow)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false);
        go.transform.localPosition = lp; go.transform.localRotation = lr; go.transform.localScale = ls;
        go.GetComponent<MeshFilter>().sharedMesh = mesh; var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
        if (!shadow) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    static Transform RockMesh(Transform parent, Mesh mesh, Material mat, Vector3 lp, float size, System.Random rnd)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        return Part(parent, "Rock", mesh, mat, lp, Quaternion.Euler(R(-10, 10), R(0, 360), R(-10, 10)), new Vector3(size * R(0.9f, 1.15f), size * R(0.75f, 1.0f), size * R(0.9f, 1.15f)), false);
    }

    static void Inst(Transform parent, GameObject prefab, Vector3 lp, Quaternion lr, Vector3 ls)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.localPosition = lp; go.transform.localRotation = lr; go.transform.localScale = Vector3.Scale(prefab.transform.localScale, ls);
        foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // 温泉のお湯：ふちがゆるく波打つ円。uv.x＝まん中0〜ふち1、uv.y＝一周の位置
    static Mesh PoolMesh(float r, System.Random rnd, string path)
    {
        const int N = 48;
        float p1 = (float)rnd.NextDouble() * 6.28f, p2 = (float)rnd.NextDouble() * 6.28f;
        var V = new List<Vector3> { Vector3.zero }; var UV = new List<Vector2> { new Vector2(0, 0) }; var T = new List<int>();
        for (int i = 0; i <= N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f;
            float er = r * (1f + 0.1f * Mathf.Sin(a * 2 + p1) + 0.06f * Mathf.Sin(a * 3 + p2));
            // まん中〜ふちの間に輪を2本（波紋の輪がカクカクしないように）
            V.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * er * 0.5f); UV.Add(new Vector2(0.5f, i / (float)N));
            V.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * er); UV.Add(new Vector2(1f, i / (float)N));
        }
        for (int i = 0; i < N; i++)
        {
            int a0 = 1 + i * 2, a1 = a0 + 1, b0 = a0 + 2, b1 = a0 + 3;
            T.AddRange(new[] { 0, b0, a0 }); T.AddRange(new[] { a0, b0, b1, a0, b1, a1 });
        }
        var m = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path) };
        m.SetVertices(V); m.SetUVs(0, UV); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        // 上向きにそろえる
        if (m.normals[1].y < 0) { var t = m.triangles; for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; } m.triangles = t; m.RecalculateNormals(); }
        return Save(m, path);
    }

    static float EdgeR(Mesh pool, float a)
    {
        var vs = pool.vertices; int N = (vs.Length - 1) / 2 - 1;
        int i = Mathf.RoundToInt(Mathf.Repeat(a / (Mathf.PI * 2f), 1f) * N);
        return new Vector2(vs[1 + i * 2 + 1].x, vs[1 + i * 2 + 1].z).magnitude;
    }

    // 噴気孔の小山：外のすそ → ふち（少し高い）→ 内側へ下がって穴。角度ごとに少しでこぼこ
    const float MoundHoleY = 0.12f, MoundHoleR = 0.45f;
    static Mesh MoundMesh(string path, System.Random rnd)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        var prof = new[] { new Vector2(1.15f, -0.08f), new Vector2(1.0f, 0.1f), new Vector2(0.78f, 0.3f), new Vector2(0.58f, 0.4f), new Vector2(0.47f, 0.33f), new Vector2(0.42f, MoundHoleY - 0.02f) };
        const int N = 28; var V = new List<Vector3>(); var T = new List<int>();
        var bump = new float[N]; for (int i = 0; i < N; i++) bump[i] = R(-0.07f, 0.07f);
        float p1 = R(0, 6.28f);
        for (int i = 0; i < N; i++)
        {
            float a = i / (float)N * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            float k = 1f + bump[i] + 0.06f * Mathf.Sin(a * 3 + p1);
            for (int j = 0; j < prof.Length; j++) V.Add(d * prof[j].x * (j == prof.Length - 1 ? 1f : k) + Vector3.up * prof[j].y * (j == 0 || j == prof.Length - 1 ? 1f : k));
        }
        int P = prof.Length;
        for (int i = 0; i < N; i++) for (int j = 0; j < P - 1; j++)
        {
            int a0 = i * P + j, a1 = ((i + 1) % N) * P + j;
            T.AddRange(new[] { a0, a0 + 1, a1, a1, a0 + 1, a1 + 1 });
        }
        var m = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path) };
        m.SetVertices(V); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        // 外側（すそ）の法線が外を向くように
        if (Vector3.Dot(m.normals[1], new Vector3(1, 0, 0)) < 0) { var t = m.triangles; for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; } m.triangles = t; m.RecalculateNormals(); }
        return Save(m, path);
    }

    static Mesh HoleMesh(string path)
    {
        var ex = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (ex != null) return ex;
        const int N = 20; var V = new List<Vector3> { Vector3.zero }; var T = new List<int>();
        for (int i = 0; i < N; i++) { float a = i / (float)N * Mathf.PI * 2f; V.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (1f + 0.08f * Mathf.Sin(a * 3))); }
        for (int i = 0; i < N; i++) T.AddRange(new[] { 0, 1 + (i + 1) % N, 1 + i });
        var m = new Mesh { name = "IwaVentHole" }; m.SetVertices(V); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        if (m.normals[0].y < 0) { var t = m.triangles; for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; } m.triangles = t; m.RecalculateNormals(); }
        AssetDatabase.CreateAsset(m, path); return m;
    }

    static Mesh Save(Mesh m, string path)
    {
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { old.Clear(); old.SetVertices(m.vertices); old.SetUVs(0, m.uv); old.SetNormals(m.normals); old.SetTriangles(m.triangles, 0); old.RecalculateBounds(); EditorUtility.SetDirty(old); Object.DestroyImmediate(m); return old; }
        AssetDatabase.CreateAsset(m, path); return m;
    }

    // 温泉の湯気：お湯の上からゆっくり立ちのぼる、うすい大きな湯気（コースの噴気孔の Idle と同じ材質）
    static void Steam(Transform pool, float pr, Transform ventFx)
    {
        // SteamPuff はコマ割りの絵なので、コマの設定ごとコースの噴気孔の Idle をコピーして使う
        var idle = ventFx != null ? ventFx.Find("Idle")?.GetComponent<ParticleSystem>() : null;
        var go = idle != null ? Object.Instantiate(idle.gameObject, pool) : new GameObject("Steam", typeof(ParticleSystem));
        go.name = "Steam"; go.transform.SetParent(pool, false); go.transform.localPosition = new Vector3(0, 0.05f, 0);
        go.transform.localRotation = Quaternion.Euler(-90f, 0, 0);   // コーンの向き（+z）を上へ
        go.transform.localScale = Vector3.one;
        var ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.duration = 5f; main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(1f, 1f, 1f, 0.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60; main.gravityModifier = 0f;
        var em = ps.emission; em.rateOverTime = 9f; em.burstCount = 0;
        var vel = ps.velocityOverLifetime; vel.enabled = false;
        var force = ps.forceOverLifetime; force.enabled = false;
        var inh = ps.inheritVelocity; inh.enabled = false;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6f; sh.radius = pr * 0.75f; sh.radiusThickness = 1f; sh.position = Vector3.zero; sh.rotation = Vector3.zero; sh.scale = Vector3.one;   // 円の底から上へ（Circle だと横に広がる）
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(1, 1.5f)));
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        var nz = ps.noise; nz.enabled = true; nz.strength = 0.25f; nz.frequency = 0.35f; nz.scrollSpeed = 0.2f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = idle != null ? idle.GetComponent<ParticleSystemRenderer>().sharedMaterial : AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/SteamPuff.mat");
        r.renderMode = ParticleSystemRenderMode.Billboard; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        ps.Play();
    }

    // 噴気孔の湯気：コースの噴気孔の Fx をコピー。音は外す。シュッ（Erupt・Jet）は period 秒ごとに最初の0.7秒だけ出す
    static void VentFx(Transform v, Transform src, float period, float delay, System.Random rnd)
    {
        var fx = Object.Instantiate(src.gameObject, v).transform; fx.name = "Fx";
        fx.localPosition = new Vector3(0, MoundHoleY + 0.03f, 0); fx.localRotation = Quaternion.identity; fx.localScale = Vector3.one;
        foreach (var a in fx.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a.gameObject);
        foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main; main.playOnAwake = true; main.loop = true;
            if (ps.name == "Erupt" || ps.name == "Jet")
            {
                float on = 0.7f / period;
                var em = ps.emission; float rate = em.rateOverTime.constantMax;
                main.duration = period; main.startDelay = delay;
                em.rateOverTime = new ParticleSystem.MinMaxCurve(rate, new AnimationCurve(new Keyframe(0, 1, 0, 0), new Keyframe(on, 1, 0, 0), new Keyframe(on + 0.001f, 0, 0, 0), new Keyframe(1, 0, 0, 0)));
            }
        }
        var glow = fx.Find("Glow"); if (glow != null) { glow.localScale = Vector3.one * 0.7f; glow.localPosition = new Vector3(0, -0.015f, 0); }   // 穴の底（黒い円）のすぐ上
    }

    // ---- 材質 ----
    static Material GroundMat()
    {
        string p = Dir + "/IwaIsland_Ground.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Goal_Ground.mat")) { name = "IwaIsland_Ground" }; AssetDatabase.CreateAsset(m, p); }
        // 砂っぽいベージュ（いわのみちの道の色に寄せる）。模様の色ムラも砂色に
        m.SetColor("_Color", new Color(0.86f, 0.76f, 0.6f, 1f));
        if (m.HasProperty("_GroundTintA")) m.SetColor("_GroundTintA", new Color(0.93f, 0.86f, 0.72f, 1f));
        if (m.HasProperty("_GroundTintB")) m.SetColor("_GroundTintB", new Color(0.78f, 0.68f, 0.56f, 1f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material WaterMat()
    {
        string p = Dir + "/IwaSpring_Water.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("FuwaCourse/HotSpring")) { name = "IwaSpring_Water" }; AssetDatabase.CreateAsset(m, p); }
        m.enableInstancing = true;
        return m;
    }

    static Material HoleMat(Material rock)
    {
        string p = Dir + "/IwaVent_Hole.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(rock) { name = "IwaVent_Hole" }; AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_Color", new Color(0.22f, 0.17f, 0.2f, 1f));
        if (m.HasProperty("_RimStrength")) m.SetFloat("_RimStrength", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
