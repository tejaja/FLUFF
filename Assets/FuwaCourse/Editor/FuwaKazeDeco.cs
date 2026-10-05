using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// かぜのおか（FuwaCourse）のコースの外の飾り：巨大たんぽぽの浮島。何度実行しても同じ結果（作り直し）。
// ・コースのまわりの空に浮島を浮かべ、巨大たんぽぽ（綿毛・黄色い花）と小さいたんぽぽを生やす
// ・綿毛のパーティクル（Ambient/Dandelions）は、巨大な綿毛の頭から飛んでコースの上を風下（ゴールの方）へ流れる
// たんぽぽは Den_Twig（InkCap・両面・頂点カラー）で1つのメッシュにまとめる。当たり判定なし
public static class FuwaKazeDeco
{
    const string Dir = "Assets/FuwaCourse/Generated/Kaze";
    const string Course = "FuwaCourse";

    class MB
    {
        public List<Vector3> V = new List<Vector3>(); public List<Vector3> N = new List<Vector3>();
        public List<Color> C = new List<Color>(); public List<int> T = new List<int>();
        public List<Vector4> U3 = new List<Vector4>();   // 風のゆれ（x＝重み、y＝位相）
        public int Add(Vector3 p, Vector3 n, Color c) { V.Add(p); N.Add(n.normalized); C.Add(new Color(c.r, c.g, c.b, 1)); U3.Add(Vector4.zero); return V.Count - 1; }
        public void Tri(int a, int b, int c) { T.Add(a); T.Add(b); T.Add(c); }
        public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }
    }

    static Vector3 _islC; static float _islR = 999f;
    static List<float> _headPhase = new List<float>();   // 綿毛の頭ごとのゆれの位相（heads と同じ順）
    const float SwayAmp = 0.38f, SwaySpeed = 1.2f;   // 今の浮島（葉っぱが島からはみ出さないように）
    static readonly Color LeafG = new Color(0.27f, 0.68f, 0.2f), StemG = new Color(0.45f, 0.74f, 0.3f), Yellow = new Color(1f, 0.82f, 0.18f), Fluff = new Color(0.97f, 0.97f, 0.95f);

    [MenuItem("FuwaCourse/かぜのおかの飾りを作る")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Kaze");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var root = roots.First(g => g.name == Course).transform;
        var path = root.Find("PathMesh");
        var twig = AssetDatabase.FindAssets("Den_Twig t:Material").Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).First(m => m.name == "Den_Twig");
        var topMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/IslandDisc/IslandDisc_r64.asset");
        var underMeshes = new[] { 0, 1, 2 }.Select(i => AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/FarIslands/IslandUnderLow_" + i + ".asset")).Where(m => m != null).ToArray();
        var groundMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Goal_Ground.mat");
        var underMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/IslandUnder.mat");
        var rnd = new System.Random(777);
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        var log = new System.Text.StringBuilder();

        var pv = path.GetComponent<MeshFilter>().sharedMesh.vertices.Where((v, i) => i % 3 == 0).Select(v => path.TransformPoint(v)).ToList();
        float DistPath(Vector3 c) { return pv.Min(q => new Vector2(q.x - c.x, q.z - c.z).magnitude); }
        // 他のコース・ロビー：小さい物は箱、大きい物は頂点で
        var otherR = roots.Where(g => g.name.StartsWith("FuwaCourse") && g.name != Course || g.name.Contains("Lobby") || g.name == "FuwaHub")
            .SelectMany(g => g.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer))).ToList();
        var other = otherR.Select(r => r.bounds).Where(b => b.size.magnitude < 12f).ToList();
        var otherPts = new List<Vector3>();
        foreach (var r in otherR.Where(r => r.bounds.size.magnitude >= 12f && r.bounds.size.magnitude < 400f))
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) { other.Add(r.bounds); continue; }
            var vv = mf.sharedMesh.vertices; int stp = Mathf.Max(1, vv.Length / 400);
            for (int i = 0; i < vv.Length; i += stp) otherPts.Add(r.transform.TransformPoint(vv[i]));
        }
        float DistOther(Bounds b)
        {
            float d = other.Count == 0 ? 999f : other.Min(o => Mathf.Sqrt(o.SqrDistance(b.ClosestPoint(o.center))));
            foreach (var p in otherPts) { float e = Mathf.Sqrt(b.SqrDistance(p)); if (e < d) d = e; }
            return d;
        }
        // 自分のコースの物（道より外にある足場・飾り）にも近づけない
        var selfPts = new List<Vector3>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>().Where(r => !r.transform.IsChildOf(root.Find("Deco") ?? r.transform.parent) || true))
        {
            if (root.Find("Deco") != null && r.transform.IsChildOf(root.Find("Deco"))) continue;   // 前回作った飾り（浮島）は数えない
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
            var vv = mf.sharedMesh.vertices; int stp = Mathf.Max(1, vv.Length / 150);
            for (int i = 0; i < vv.Length; i += stp) selfPts.Add(r.transform.TransformPoint(vv[i]));
        }
        float DistSelf(Bounds b) { float d = 999f; foreach (var p in selfPts) { float e = Mathf.Sqrt(b.SqrDistance(p)); if (e < d) d = e; } return d; }

        // 風下の向き（スタート → ゴール）
        var st = root.GetComponentsInChildren<Transform>().First(t => t.name == "BallStart").position; var gl = root.Find("Goal").position;
        var wind = gl - st; wind.y = 0; wind.Normalize();
        float pathY = pv.Average(p => p.y);

        var deco = root.Find("Deco");
        if (deco == null) { deco = new GameObject("Deco").transform; deco.SetParent(root, false); Undo.RegisterCreatedObjectUndo(deco.gameObject, "deco"); }
        deco.localPosition = Vector3.zero; deco.localRotation = Quaternion.identity; deco.localScale = Vector3.one;
        var isl = deco.Find("DandelionIslands");
        // 作り直しても浮島の場所は変えない（道を伸ばすと配置の計算が変わって全部動いてしまうため）。前の浮島の位置・大きさ・向きを覚えておく
        var keep = new List<(Vector3 pos, float rad, Quaternion rot)>();
        if (isl != null)
            foreach (Transform c in isl)
            {
                if (!c.name.StartsWith("Island")) continue;
                var tp = c.Find("IslandTop"); if (tp == null) continue;
                keep.Add((c.position, tp.localScale.x * 0.64f, c.rotation));
            }
        if (isl != null) Undo.DestroyObjectImmediate(isl.gameObject);
        isl = new GameObject("DandelionIslands").transform; isl.SetParent(deco, false); Undo.RegisterCreatedObjectUndo(isl.gameObject, "deco");

        var plants = new MB(); var heads = new List<(Vector3 p, float r)>(); _headPhase.Clear();
        var placed = new List<(Vector3 pos, float rad)>();
        int want = keep.Count > 0 ? keep.Count : 8, tries = 0;
        while (placed.Count < want && tries++ < 8000)
        {
            Vector3 pos; float rad; Quaternion rot;
            if (keep.Count > 0) { (pos, rad, rot) = keep[placed.Count]; }
            else
            {
                var anchor = pv[rnd.Next(pv.Count)];
                float ang = R(0, Mathf.PI * 2), dist = R(11f, 26f);
                pos = new Vector3(anchor.x + Mathf.Cos(ang) * dist, pathY + R(-7f, 1f), anchor.z + Mathf.Sin(ang) * dist);
                rad = R(2.6f, 3.4f);
                float dp = DistPath(pos); if (dp < 8f + rad || dp > 28f) continue;
                if (placed.Any(q => new Vector2(q.pos.x - pos.x, q.pos.z - pos.z).magnitude < q.rad + rad + 7f)) continue;
                var bb = new Bounds(pos + Vector3.up * 1.5f, new Vector3(rad * 2 + 2, 8f, rad * 2 + 2));
                if (DistOther(bb) < 12f || DistSelf(bb) < 4f) continue;
                rot = Quaternion.Euler(0, R(0, 360), 0);
            }
            placed.Add((pos, rad));

            // 浮島
            var g = new GameObject("Island" + placed.Count).transform; g.SetParent(isl, false); g.position = pos; g.rotation = rot;
            var top = new GameObject("IslandTop", typeof(MeshFilter), typeof(MeshRenderer)); top.transform.SetParent(g, false);
            top.GetComponent<MeshFilter>().sharedMesh = topMesh; top.GetComponent<MeshRenderer>().sharedMaterial = groundMat; top.transform.localScale = new Vector3(rad / 0.64f, 1f, rad / 0.64f);
            var under = new GameObject("IslandUnder", typeof(MeshFilter), typeof(MeshRenderer)); under.transform.SetParent(g, false);
            under.GetComponent<MeshFilter>().sharedMesh = underMeshes[rnd.Next(underMeshes.Length)]; under.GetComponent<MeshRenderer>().sharedMaterial = underMat;
            under.transform.localScale = new Vector3(rad / 0.97f, rad / 0.97f * R(0.8f, 1.1f), rad / 0.97f); under.transform.localRotation = Quaternion.Euler(0, R(0, 360), 0);
            float topY = pos.y + 0.03f; _islC = pos; _islR = rad * 0.97f;

            // 大きいたんぽぽ（木よりちょっと低いくらい）：綿毛1本（必ず）＋ときどき黄色い花
            // 綿毛と黄色い花は島の反対側に分けて、頭どうしが重ならないようにする
            var sep = Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward;
            var gp = pos - sep * R(0.9f, 1.2f);
            Giant(plants, new Vector3(gp.x, topY, gp.z), R(3.0f, 3.8f), true, wind, rnd, heads);
            if (rnd.NextDouble() < 0.6)
            {
                var yp = pos + Quaternion.Euler(0, R(-25, 25), 0) * sep * R(1.6f, rad * 0.7f);
                Giant(plants, new Vector3(yp.x, topY, yp.z), R(2.2f, 2.9f), false, wind, rnd, heads);
            }
            // 小さいたんぽぽ（ふつうの大きさ）
            int ns = rnd.Next(5, 10);
            for (int k = 0; k < ns; k++)
            {
                var sp = pos + Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward * R(0.8f, rad * 0.85f);
                Small(new MB(), new Vector3(sp.x, topY, sp.z), rnd.NextDouble() < 0.5, wind, rnd);   // 小さいたんぽぽはなし（乱数だけ進めて配置を変えない）
            }
        }
        log.Append("islands " + placed.Count + " ");

        // まとめたメッシュ
        var w2l = deco.worldToLocalMatrix;
        {
            var m = new Mesh { name = "Kaze_Dandelions", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(plants.V.Select(x => w2l.MultiplyPoint3x4(x)).ToList()); m.SetNormals(plants.N.Select(x => w2l.MultiplyVector(x).normalized).ToList());
            m.SetColors(plants.C); m.SetUVs(3, plants.U3); m.SetTriangles(plants.T, 0); m.RecalculateBounds();
            string ap = Dir + "/Kaze_Dandelions.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
            if (old != null) { old.Clear(); EditorUtility.CopySerialized(m, old); m = old; } else AssetDatabase.CreateAsset(m, ap);
            var go = new GameObject("Dandelions", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(isl, false);
            // 風でゆれる専用の材質（Den_Twig のコピー。ゆれ幅と向きだけ足す）
            string swp = "Assets/FuwaCourse/Materials/Kaze_DandelionSway.mat"; var sway = AssetDatabase.LoadAssetAtPath<Material>(swp);
            if (sway == null) { sway = new Material(twig); AssetDatabase.CreateAsset(sway, swp); }
            sway.CopyPropertiesFromMaterial(twig);
            sway.SetFloat("_SwayAmp", SwayAmp); sway.SetFloat("_SwaySpeed", SwaySpeed); sway.SetVector("_SwayDir", new Vector4(wind.x, 0, wind.z, 0)); EditorUtility.SetDirty(sway);
            go.GetComponent<MeshFilter>().sharedMesh = m; go.GetComponent<MeshRenderer>().sharedMaterial = sway;
            log.Append("tris " + plants.T.Count / 3 + " ");
        }
        // 綿毛の頭：ふわふわと同じシェルファー（FuwaCourse/Fur）の白い毛玉。8個を1つのメッシュにまとめて描画回数を抑える
        {
            var hv = new List<Vector3>(); var hn = new List<Vector3>(); var ht = new List<int>(); var hu = new List<Vector4>(); int hi = 0;
            const int seg = 18, rings = 12;
            foreach (var (hp, hr) in heads)
            {
                int bi = hv.Count; float rr = hr * 0.75f;   // 芯を大きめにして毛は短めに（茎が毛の間から透けて見えないように）
                for (int j = 0; j <= rings; j++)
                    for (int i = 0; i <= seg; i++)
                    {
                        float th = Mathf.PI * j / rings, ph = Mathf.PI * 2 * i / seg;
                        var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                        hv.Add(w2l.MultiplyPoint3x4(hp + d * rr)); hn.Add(w2l.MultiplyVector(d).normalized); hu.Add(new Vector4(1f, _headPhase[hi], 0, 0));
                    }
                for (int j = 0; j < rings; j++)
                    for (int i = 0; i < seg; i++)
                    {
                        int a0 = bi + j * (seg + 1) + i;
                        ht.Add(a0); ht.Add(a0 + 1); ht.Add(a0 + seg + 1); ht.Add(a0 + 1); ht.Add(a0 + seg + 2); ht.Add(a0 + seg + 1);   // 外向きの面
                    }
                hi++;
            }
            var hm = new Mesh { name = "Kaze_PuffFur" }; hm.SetVertices(hv); hm.SetNormals(hn); hm.SetUVs(3, hu); hm.SetTriangles(ht, 0); hm.RecalculateBounds();
            string ap = Dir + "/Kaze_PuffFur.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
            if (old != null) { old.Clear(); EditorUtility.CopySerialized(hm, old); hm = old; } else AssetDatabase.CreateAsset(hm, ap);
            string mp = "Assets/FuwaCourse/Materials/PuffFur.mat"; var hmat = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (hmat == null)
            {
                hmat = new Material(Shader.Find("FuwaCourse/Fur")); AssetDatabase.CreateAsset(hmat, mp);
                // メッシュはまとめてあるので拡大率1。毛の長さ・細かさはメートルで
                hmat.SetColor("_Color", new Color(0.86f, 0.84f, 0.78f)); hmat.SetColor("_TipColor", Color.white);
                hmat.SetFloat("_FurLength", 0.65f); hmat.SetFloat("_Density", 50f); hmat.SetFloat("_Thinness", 0.9f);
                hmat.SetFloat("_Shadow", 0.3f); hmat.SetFloat("_Gravity", 0.02f); hmat.SetFloat("_RimStrength", 0.4f);
            }
            hmat.SetFloat("_SwayAmp", SwayAmp); hmat.SetFloat("_SwaySpeed", SwaySpeed); hmat.SetVector("_SwayDir", new Vector4(wind.x, 0, wind.z, 0)); EditorUtility.SetDirty(hmat);
            var go = new GameObject("PuffFur", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(isl, false);
            go.GetComponent<MeshFilter>().sharedMesh = hm; var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = hmat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        }
        // 綿毛の出どころ：巨大な綿毛の頭の表面（小さな三角を並べたメッシュ）
        {
            var ev = new List<Vector3>(); var et = new List<int>();
            foreach (var (hp, hr) in heads)
                for (int k = 0; k < 24; k++)
                {
                    var d = Random3(rnd); if (Vector3.Dot(d, wind) < -0.2f) d = Vector3.Reflect(d, wind);   // 風下側から多く
                    var c = hp + d * hr; var t1 = Vector3.Cross(d, Vector3.up).normalized * 0.05f; var t2 = Vector3.Cross(d, t1).normalized * 0.05f;
                    int b = ev.Count; ev.Add(w2l.MultiplyPoint3x4(c)); ev.Add(w2l.MultiplyPoint3x4(c + t1)); ev.Add(w2l.MultiplyPoint3x4(c + t2)); et.Add(b); et.Add(b + 1); et.Add(b + 2);
                }
            var em = new Mesh { name = "Kaze_FluffEmit" }; em.SetVertices(ev); em.SetTriangles(et, 0); em.RecalculateNormals(); em.RecalculateBounds();
            string ap = Dir + "/Kaze_FluffEmit.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
            if (old != null) { old.Clear(); EditorUtility.CopySerialized(em, old); em = old; } else AssetDatabase.CreateAsset(em, ap);
            var amb = root.Find("Ambient/Dandelions");
            if (amb != null)
            {
                amb.position = deco.position; amb.rotation = deco.rotation; amb.localScale = Vector3.one;
                var ps = amb.GetComponent<ParticleSystem>();
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Mesh; sh.meshShapeType = ParticleSystemMeshShapeType.Triangle; sh.mesh = em; sh.scale = deco.lossyScale; sh.normalOffset = 0f; sh.randomPositionAmount = 0.3f;
                // 綿毛は大きめ・多めにして目立たせる（形は軽い三角だけの綿毛に作り直す）
                var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 11f); main.maxParticles = 450;   // 飛ぶ距離は短め
                main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.75f);
                var emi = ps.emission; emi.rateOverTime = 28.8f;   // 2026-10-05 1割減
                var pr = amb.GetComponent<ParticleSystemRenderer>(); pr.mesh = SeedMesh();
                var smat = pr.sharedMaterial; if (smat != null) { smat.SetFloat("_FadeStart", 25f); smat.SetFloat("_FadeEnd", 80f); smat.SetFloat("_FarAlpha", 0.65f); smat.SetFloat("_Shade", 0.1f); EditorUtility.SetDirty(smat); }
                var vel = ps.velocityOverLifetime; vel.x = new ParticleSystem.MinMaxCurve(wind.x * 1.2f, wind.x * 2.2f); vel.z = new ParticleSystem.MinMaxCurve(wind.z * 1.2f, wind.z * 2.2f); vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.25f);
                amb.GetComponent<ParticleSystemRenderer>().enableGPUInstancing = false;   // シェーダーがパーティクルのインスタンシング未対応（色がくずれる）
                EditorUtility.SetDirty(ps);
            }
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return log.ToString();
    }

    // 飛んでいく綿毛（パーティクル用）：細い軸＋放射状に開いた白い毛（三角だけ）。1つ約30三角で軽い
    static Mesh SeedMesh()
    {
        string ap = "Assets/FuwaCourse/Meshes/DandelionSeed.asset";
        var V = new List<Vector3>(); var C = new List<Color32>(); var T = new List<int>();
        Color32 white = new Color32(255, 255, 255, 255), tan = new Color32(190, 160, 110, 255), stem = new Color32(235, 230, 215, 255);
        void Tri(Vector3 a, Vector3 b, Vector3 c, Color32 ca, Color32 cb, Color32 cc) { int i = V.Count; V.Add(a); V.Add(b); V.Add(c); C.Add(ca); C.Add(cb); C.Add(cc); T.Add(i); T.Add(i + 1); T.Add(i + 2); }
        // 種（下のふくらみ）と軸：十字の細い板
        for (int k = 0; k < 2; k++)
        {
            var sd = k == 0 ? Vector3.right : Vector3.forward;
            Tri(new Vector3(0, -0.5f, 0), sd * 0.04f + new Vector3(0, -0.38f, 0), -sd * 0.04f + new Vector3(0, -0.38f, 0), tan, tan, tan);
            Tri(sd * 0.04f + new Vector3(0, -0.38f, 0), sd * 0.012f + new Vector3(0, 0.1f, 0), -sd * 0.04f + new Vector3(0, -0.38f, 0), stem, stem, tan);
            Tri(-sd * 0.04f + new Vector3(0, -0.38f, 0), sd * 0.012f + new Vector3(0, 0.1f, 0), -sd * 0.012f + new Vector3(0, 0.1f, 0), tan, stem, stem);
        }
        // 毛：軸の先から放射状（少し上向きに開いた傘）。細長い三角を2枚ずつ（向きの違う板）で、どの角度からも見える
        var top = new Vector3(0, 0.1f, 0); int n = 12;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2 / n; var d = new Vector3(Mathf.Cos(a), 0.55f, Mathf.Sin(a)).normalized; var tip = top + d * 0.42f;
            var side = Vector3.Cross(d, Vector3.up).normalized * 0.035f; var up = Vector3.Cross(side, d).normalized * 0.035f;
            Tri(top, tip + side, tip - side, white, white, white);
            Tri(top, tip + up, tip - up, white, white, white);
        }
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
        if (m == null) { m = new Mesh { name = "DandelionSeed" }; AssetDatabase.CreateAsset(m, ap); }
        m.Clear(); m.SetVertices(V); m.SetColors(C); m.SetUVs(0, new List<Vector2>(new Vector2[V.Count])); m.SetTriangles(T, 0); m.RecalculateNormals(); m.RecalculateBounds();
        EditorUtility.SetDirty(m);
        return m;
    }

    static Vector3 Random3(System.Random rnd)
    {
        while (true)
        {
            var v = new Vector3((float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * 2 - 1);
            float m = v.magnitude; if (m > 0.1f && m <= 1f) return v / m;
        }
    }

    // 巨大たんぽぽ：葉のロゼット＋少し風下にしなる茎＋頭（綿毛 or 黄色い花）
    static void Giant(MB m, Vector3 at, float h, bool puff, Vector3 wind, System.Random rnd, List<(Vector3, float)> heads)
    {
        int v0 = m.V.Count; float phase = (float)rnd.NextDouble() * 6.2832f;
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        float s = h / 4.5f;
        Rosette(m, at, R(2.8f, 3.6f) * s, rnd.Next(8, 11), rnd, GiantLeafSway);
        int leafEnd = m.V.Count;
        var side = Vector3.Cross(Vector3.up, wind);
        var lean = (wind * R(0.6f, 1.2f) + side * R(-0.5f, 0.5f)) * s;
        var pts = new List<Vector3>(); var rr = new List<float>();
        for (int i = 0; i <= 8; i++)
        {
            float u = i / 8f;
            pts.Add(at + Vector3.up * h * u + lean * u * u);
            rr.Add(Mathf.Lerp(0.12f, 0.08f, u) * s);
        }
        Tube(m, pts, rr, 7, StemG, StemG * 0.85f);
        var top = pts[pts.Count - 1];
        if (puff)
        {
            float hr = R(1.1f, 1.5f) * s;
            // 頭そのものは、ふわふわと同じシェルファーの毛玉（PuffFur）で描く
            heads.Add((top + Vector3.up * hr * 0.2f, hr)); _headPhase.Add(phase);
        }
        else Flower(m, top, R(2.1f, 2.6f) * s, rnd);
        // 風のゆれの重み：根元0 → 先1（高さの2乗）。葉は地面近くなのでほぼゆれない
        for (int i2 = leafEnd; i2 < m.V.Count; i2++) { float w =Mathf.Clamp01((m.V[i2].y - at.y) / h); m.U3[i2] = new Vector4(w * w, phase, 0, 0); }
    }

    // ふつうの大きさのたんぽぽ
    static void Small(MB m, Vector3 at, bool puff, Vector3 wind, System.Random rnd)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        float h = R(0.35f, 0.6f);
        Rosette(m, at, R(0.25f, 0.4f), rnd.Next(5, 8), rnd, SmallLeafSway);
        var lean = wind * R(0.03f, 0.1f);
        Tube(m, new List<Vector3> { at, at + Vector3.up * h * 0.5f + lean * 0.3f, at + Vector3.up * h + lean }, new List<float> { 0.018f, 0.015f, 0.013f }, 4, StemG, StemG);
        var top = at + Vector3.up * h + lean;
        if (puff) Puff(m, top + Vector3.up * 0.03f, R(0.11f, 0.15f), rnd, 26, 0.012f);
        else Flower(m, top, R(0.07f, 0.1f), rnd);
    }

    // 葉：地面に放射状に寝た、ギザギザ（ライオンの歯）の細長い葉
    // 葉のゆらゆら：先ほど大きく（根元からの距離の2乗）、葉ごとに位相をずらす。値はゆれ幅（SwayAmp）に掛ける重み
    const float GiantLeafSway = 0.3f, SmallLeafSway = 0.08f;

    static void Rosette(MB m, Vector3 at, float len, int n, System.Random rnd, float leafSway)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        for (int i = 0; i < n; i++)
        {
            float a0 = (i + R(-0.3f, 0.3f)) * Mathf.PI * 2 / n; var d = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var sd = new Vector3(-d.z, 0, d.x);
            float L = len * R(0.7f, 1.1f);
            {   // 島のふちまでで止める
                var o = new Vector2(at.x - _islC.x, at.z - _islC.z); var dd = new Vector2(d.x, d.z);
                float b2 = Vector2.Dot(o, dd), cc = o.sqrMagnitude - _islR * _islR, disc = b2 * b2 - cc;
                if (disc > 0) L = Mathf.Min(L, Mathf.Max(0.15f, -b2 + Mathf.Sqrt(disc)));
            }
            float W = Mathf.Min(L, len) * R(0.15f, 0.2f); int bi = m.V.Count;
            var col = LeafG * R(0.92f, 1.12f); var rib = Color.Lerp(col, new Color(0.75f, 0.9f, 0.5f), 0.35f);
            // パキッと：まっすぐ斜めに立つ葉（曲げない）。ふちは直線のギザギザ（根元向きの三角の歯）、先はとがらせる。中央の葉脈で V 字に折る
            float ang = R(30f, 45f) * Mathf.Deg2Rad;
            var dir = (d * Mathf.Cos(ang) + Vector3.up * Mathf.Sin(ang)).normalized;
            var up = (Vector3.up * Mathf.Cos(ang) - d * Mathf.Sin(ang)).normalized;
            // (u, 幅) の列：細い柄 → 歯と切れ込みを交互に → とがった先
            var prof = new List<Vector2> { new Vector2(0f, 0.12f), new Vector2(0.14f, 0.18f) };
            int teeth = 4;
            for (int t = 0; t < teeth; t++)
            {
                float u0 = 0.18f + t * 0.17f;
                prof.Add(new Vector2(u0, 0.38f + t * 0.06f));                 // 切れ込み
                prof.Add(new Vector2(u0 + 0.05f, 1.0f - t * 0.05f));          // 歯の先（根元向きにとがる）
            }
            prof.Add(new Vector2(0.88f, 0.55f)); prof.Add(new Vector2(1f, 0f));
            int k2 = prof.Count - 1;
            for (int j = 0; j <= k2; j++)
            {
                float u = prof[j].x, w = W * prof[j].y;
                var c = at + Vector3.up * 0.02f + dir * L * u;
                m.Add(c - sd * w + up * w * 0.35f, up, col); m.Add(c, up, rib); m.Add(c + sd * w + up * w * 0.35f, up, col);
            }
            for (int j = 0; j < k2; j++) { int a = bi + j * 3, b = bi + (j + 1) * 3; m.Quad(a, a + 1, b + 1, b); m.Quad(a + 1, a + 2, b + 2, b + 1); }   // 上向きの面
            // ゆらゆら（乱数は使わない：ほかの飾りの並びが変わらないように、場所と葉の番号から位相を決める）
            float lph = Mathf.Repeat(at.x * 1.7f + at.z * 2.3f + i * 2.399f, 6.2832f);
            for (int j = 0; j <= k2; j++) { float u = prof[j].x; for (int q = 0; q < 3; q++) m.U3[bi + j * 3 + q] = new Vector4(leafSway * u * u, lph, 0, 0); }
        }
    }

    // 綿毛の頭：まん中の芯から細い毛が放射状に出て、先に小さなふわふわ。下（茎の側）は少なめ
    static void Puff(MB m, Vector3 c, float r, System.Random rnd, int n, float tipR)
    {
        Sphere(m, c, r * 0.14f, new Color(0.75f, 0.65f, 0.45f), 6, 4);
        float ga = Mathf.PI * (3f - Mathf.Sqrt(5f));
        for (int i = 0; i < n; i++)
        {
            float y = 1f - 2f * (i + 0.5f) / n; float rr = Mathf.Sqrt(1 - y * y); float th = ga * i;
            var d = new Vector3(Mathf.Cos(th) * rr, y, Mathf.Sin(th) * rr);
            if (d.y < -0.75f) continue;   // 茎のところは空ける
            float len = r * (0.92f + (float)rnd.NextDouble() * 0.12f);
            var a = c + d * r * 0.12f; var b = c + d * len;
            Tube(m, new List<Vector3> { a, b }, new List<float> { tipR * 0.18f, tipR * 0.12f }, 3, Fluff * 0.95f, Fluff);
            // 先のふわふわ（平たい星形）
            Star(m, b, d, tipR, 8, rnd);
        }
    }

    static void Star(MB m, Vector3 c, Vector3 n, float r, int k, System.Random rnd)
    {
        var t = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var b = Vector3.Cross(n, t);
        int ci = m.Add(c + n * r * 0.15f, n, Fluff);
        int first = m.V.Count;
        for (int i = 0; i < k * 2; i++)
        {
            float a = i * Mathf.PI / k; float rr = (i % 2 == 0 ? r : r * 0.62f);
            m.Add(c + (Mathf.Cos(a) * t + Mathf.Sin(a) * b) * rr - n * r * 0.25f, n, Fluff);
        }
        for (int i = 0; i < k * 2; i++) m.Tri(ci, first + i, first + (i + 1) % (k * 2));
    }

    // 黄色い花：細い花びらを3重の輪に。下に緑のがく
    static void Flower(MB m, Vector3 c, float r, System.Random rnd)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        for (int layer = 0; layer < 3; layer++)
        {
            int n = 18 - layer * 4; float L = r * (1f - layer * 0.28f), up = 0.12f + layer * 0.35f;
            var col = Color.Lerp(Yellow, new Color(1f, 0.68f, 0.1f), layer * 0.4f);
            for (int i = 0; i < n; i++)
            {
                float a = (i + layer * 0.5f) * Mathf.PI * 2 / n; var d = new Vector3(Mathf.Cos(a), up, Mathf.Sin(a)).normalized; var sd = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                float w = L * 0.11f; var p0 = c + Vector3.up * r * 0.05f * layer; var p1 = p0 + d * L * 0.5f; var p2 = p0 + d * L;
                int b = m.V.Count;
                m.Add(p0 - sd * w * 0.4f, Vector3.up, col); m.Add(p0 + sd * w * 0.4f, Vector3.up, col);
                m.Add(p1 - sd * w, Vector3.up, col); m.Add(p1 + sd * w, Vector3.up, col);
                m.Add(p2 - sd * w * 0.7f, Vector3.up, col * 1.03f); m.Add(p2 + sd * w * 0.7f, Vector3.up, col * 1.03f);
                m.Quad(b, b + 1, b + 3, b + 2); m.Quad(b + 2, b + 3, b + 5, b + 4);   // 上向きの面
            }
        }
        // がく
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * 2 / 10; var d = new Vector3(Mathf.Cos(a), -0.6f, Mathf.Sin(a)).normalized;
            Tube(m, new List<Vector3> { c, c + d * r * 0.45f }, new List<float> { r * 0.05f, r * 0.01f }, 3, LeafG, LeafG);
        }
    }

    static void Sphere(MB m, Vector3 c, float r, Color col, int seg, int rings)
    {
        int bi = m.V.Count;
        for (int j = 0; j <= rings; j++)
            for (int i = 0; i <= seg; i++)
            {
                float th = Mathf.PI * j / rings, ph = Mathf.PI * 2 * i / seg;
                var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                m.Add(c + d * r, d, col);
            }
        for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                int a = bi + j * (seg + 1) + i;
                m.Quad(a, a + 1, a + seg + 2, a + seg + 1);
            }
    }

    static void Tube(MB m, IList<Vector3> p, IList<float> r, int sides, Color c0, Color c1)
    {
        int n = p.Count; if (n < 2) return;
        var t0 = (p[1] - p[0]).normalized;
        var nrm = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        int bi = m.V.Count;
        for (int i = 0; i < n; i++)
        {
            var t = (p[Mathf.Min(i + 1, n - 1)] - p[Mathf.Max(i - 1, 0)]).normalized;
            nrm = (nrm - Vector3.Dot(nrm, t) * t).normalized; var b = Vector3.Cross(t, nrm);
            var c = Color.Lerp(c0, c1, i / (float)(n - 1));
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2 / sides; var d = Mathf.Cos(a) * nrm + Mathf.Sin(a) * b;
                m.Add(p[i] + d * r[i], d, c);
            }
        }
        for (int i = 0; i < n - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int s1 = (s + 1) % sides;
                m.Quad(bi + i * sides + s, bi + (i + 1) * sides + s, bi + (i + 1) * sides + s1, bi + i * sides + s1);
            }
    }
}
