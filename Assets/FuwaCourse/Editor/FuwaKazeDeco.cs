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

    // 何回乱数を引いたか数える乱数。同じ所まで引き直した複製を作れる（試し置きしても本番の並びを変えないため）
    class CRandom : System.Random
    {
        readonly int seed; public int Count;
        public CRandom(int s) : base(s) { seed = s; }
        public override int Next() { Count++; return base.Next(); }
        public override int Next(int max) { Count++; return base.Next(max); }
        public override int Next(int a, int b) { Count++; return base.Next(a, b); }
        public override double NextDouble() { Count++; return base.NextDouble(); }
        public CRandom Clone() { var c = new CRandom(seed); for (int i = 0; i < Count; i++) c.Next(); return c; }
    }

    static Vector3 _islC; static float _islR = 999f;
    static List<float> _headPhase = new List<float>();   // 綿毛の頭ごとのゆれの位相（heads と同じ順）
    const float SwayAmp = 0.38f, SwaySpeed = 1.2f;   // 今の浮島（葉っぱが島からはみ出さないように）
    // 綿毛の頭：外側の半径（前の毛玉の芯 hr*0.75＋毛 0.65 と同じ）、芯の大きさ（外側に対する比）、冠毛の数（外側の半径1.58mの頭で）
    static float HeadOuter(float hr) { return hr * 0.75f + 0.65f; }
    const float HeadCore = 0.26f;
    const int TuftsPerHead = 420;
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
        var rnd = new CRandom(777);
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
        var views = PathViews(root, st);

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
        // 浮島がもうあるときは作り直さずにそのまま使う（島の地面は影を焼いてあるので、作り直すと焼き直しが要る）。たんぽぽのメッシュだけ作り直す
        bool reuse = isl != null && keep.Count > 0;
        if (!reuse)
        {
            if (isl != null) Undo.DestroyObjectImmediate(isl.gameObject);
            isl = new GameObject("DandelionIslands").transform; isl.SetParent(deco, false); Undo.RegisterCreatedObjectUndo(isl.gameObject, "deco");
        }

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

            // 浮島（乱数は作り直さないときも同じだけ引いて、後の並びを変えない）
            int um = rnd.Next(underMeshes.Length); float uy = R(0.8f, 1.1f), ur = R(0, 360);
            if (!reuse)
            {
                var g = new GameObject("Island" + placed.Count).transform; g.SetParent(isl, false); g.position = pos; g.rotation = rot;
                var top = new GameObject("IslandTop", typeof(MeshFilter), typeof(MeshRenderer)); top.transform.SetParent(g, false);
                top.GetComponent<MeshFilter>().sharedMesh = topMesh; top.GetComponent<MeshRenderer>().sharedMaterial = groundMat; top.transform.localScale = new Vector3(rad / 0.64f, 1f, rad / 0.64f);
                var under = new GameObject("IslandUnder", typeof(MeshFilter), typeof(MeshRenderer)); under.transform.SetParent(g, false);
                under.GetComponent<MeshFilter>().sharedMesh = underMeshes[um]; under.GetComponent<MeshRenderer>().sharedMaterial = underMat;
                under.transform.localScale = new Vector3(rad / 0.97f, rad / 0.97f * uy, rad / 0.97f); under.transform.localRotation = Quaternion.Euler(0, ur, 0);
            }
            float topY = pos.y + 0.03f; _islC = pos; _islR = rad * 0.97f;

            // 大きいたんぽぽ（木よりちょっと低いくらい）：綿毛1本（必ず）＋ときどき黄色い花
            // 綿毛と黄色い花は島の反対側に分ける。分ける向きは、コースを進みながら前〜斜め前を見たときに頭どうしが重なって見えにくい向きの中から選ぶ
            // （一番いい向きに固定すると全部同じ見え方になるので、合格した向きの中からランダム）
            float? useAng = null; var snap = rnd.Clone();
            {
                var tp = new List<(Vector3, float)>(); var tph = new List<float>();
                if (Pair(new MB(), snap.Clone(), pos, rad, topY, null, wind, tp, tph, out _) == 2)
                {
                    var cost = new List<(float ang, float c)>();
                    for (int ai = 0; ai < 72; ai++)
                    {
                        tp.Clear(); tph.Clear();
                        Pair(new MB(), snap.Clone(), pos, rad, topY, ai * 5f, wind, tp, tph, out var hd);
                        cost.Add((ai * 5f, ViewCost(hd[0], hd[1], views)));
                    }
                    float best = cost.Min(q => q.c), lim = Mathf.Max(best + 0.01f, 0.03f);
                    var ok = cost.Where(q => q.c <= lim).ToList();
                    tp.Clear(); tph.Clear(); Pair(new MB(), snap.Clone(), pos, rad, topY, null, wind, tp, tph, out var hd0);
                    float c0 = ViewCost(hd0[0], hd0[1], views);
                    if (c0 > lim)   // 元の向きで合格ならそのまま
                    {
                        var pick = ok[new System.Random(9100 + placed.Count).Next(ok.Count)];
                        useAng = pick.ang;
                        log.Append("\n島" + placed.Count + " 重なり " + c0.ToString("F3") + " → " + pick.c.ToString("F3") + "（合格 " + ok.Count + "/72）");
                    }
                }
            }
            Pair(plants, rnd, pos, rad, topY, useAng, wind, heads, _headPhase, out _);
            // 小さいたんぽぽ（ふつうの大きさ）
            int ns = rnd.Next(5, 10);
            for (int k = 0; k < ns; k++)
            {
                var sp = pos + Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward * R(0.8f, rad * 0.85f);
                Small(new MB(), new Vector3(sp.x, topY, sp.z), rnd.NextDouble() < 0.5, wind, rnd);   // 小さいたんぽぽはなし（乱数だけ進めて配置を変えない）
            }
        }
        log.Append("islands " + placed.Count + " ");

        // 綿毛の頭（2026-10-09 てじゃの写真に寄せて作り直し）：茶色の芯＋放射状の細い軸（たんぽぽのメッシュにまとめる）
        // ＋軸の先の冠毛（いつもカメラを向く放射状の毛の半透明の板。下の PuffTufts）。外側の大きさは前の毛玉（芯 hr*0.75＋毛0.65）と同じ
        var tufts = new List<(Vector3 p, Vector3 d, float size, float rot, int head)>();
        {
            var hrnd = new System.Random(4242);   // 配置の乱数（rnd）は進めない
            float ga = Mathf.PI * (3f - Mathf.Sqrt(5f));
            Color stalkC = new Color(0.92f, 0.91f, 0.86f), coreA = new Color(0.55f, 0.45f, 0.32f), coreB = new Color(0.68f, 0.58f, 0.42f);
            for (int hi = 0; hi < heads.Count; hi++)
            {
                var (hp, hr) = heads[hi]; float ph = _headPhase[hi];
                float R0 = HeadOuter(hr), coreR = R0 * HeadCore;
                int n = Mathf.RoundToInt(TuftsPerHead * (R0 / 1.58f) * (R0 / 1.58f));
                int vb = plants.V.Count;
                for (int k = 0; k < n; k++)
                {
                    float y = 1f - 2f * (k + 0.5f) / n, r = Mathf.Sqrt(1f - y * y), th = ga * k;
                    var d = new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r);
                    d = (d + new Vector3((float)hrnd.NextDouble() - 0.5f, (float)hrnd.NextDouble() - 0.5f, (float)hrnd.NextDouble() - 0.5f) * 0.1f).normalized;
                    float rad = R0 * Mathf.Lerp(0.78f, 0.9f, (float)hrnd.NextDouble()), sz = R0 * Mathf.Lerp(0.15f, 0.21f, (float)hrnd.NextDouble()), rot = (float)hrnd.NextDouble() * 6.28f;
                    if (d.y < -0.8f) continue;   // 茎のまわりは空ける
                    var p1 = hp + d * rad; var p0 = hp + d * coreR * 0.9f;
                    var t1 = Vector3.Cross(d, Mathf.Abs(d.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var t2 = Vector3.Cross(d, t1);
                    const float sw = 0.006f;   // 軸：細い三角柱（先ほど細く）。両面
                    for (int s = 0; s < 3; s++)
                    {
                        float a0 = s * 2.094f, a1 = (s + 1) * 2.094f;
                        var o0 = t1 * Mathf.Cos(a0) + t2 * Mathf.Sin(a0); var o1 = t1 * Mathf.Cos(a1) + t2 * Mathf.Sin(a1); var nn = o0 + o1;
                        int i0 = plants.Add(p0 + o0 * sw, nn, stalkC), i1 = plants.Add(p0 + o1 * sw, nn, stalkC), i2 = plants.Add(p1 + o1 * sw * 0.5f, nn, stalkC), i3 = plants.Add(p1 + o0 * sw * 0.5f, nn, stalkC);
                        plants.Quad(i0, i1, i2, i3); plants.Quad(i0, i3, i2, i1);
                    }
                    tufts.Add((p1, d, sz, rot, hi));
                }
                // 芯（花床）：小さな茶色の球。茎の先はこの中に入る
                const int seg = 16, rings = 12; int cb = plants.V.Count;
                for (int j = 0; j <= rings; j++)
                    for (int i = 0; i <= seg; i++)
                    {
                        float th = Mathf.PI * j / rings, phi = Mathf.PI * 2 * i / seg;
                        var dd = new Vector3(Mathf.Sin(th) * Mathf.Cos(phi), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(phi));
                        plants.Add(hp + dd * coreR, dd, Color.Lerp(coreA, coreB, ((i * 7 + j * 13) % 5) / 5f));
                    }
                for (int j = 0; j < rings; j++)
                    for (int i = 0; i < seg; i++) { int a0 = cb + j * (seg + 1) + i; plants.Tri(a0, a0 + 1, a0 + seg + 1); plants.Tri(a0 + 1, a0 + seg + 2, a0 + seg + 1); }
                // 頭は茎の先と同じだけゆれる
                for (int i = vb; i < plants.V.Count; i++) plants.U3[i] = new Vector4(1f, ph, 0, 0);
            }
            log.Append("tufts " + tufts.Count + " ");
        }

        // まとめたメッシュ
        var w2l = deco.worldToLocalMatrix;
        {
            var m = new Mesh { name = "Kaze_Dandelions", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(plants.V.Select(x => w2l.MultiplyPoint3x4(x)).ToList()); m.SetNormals(plants.N.Select(x => w2l.MultiplyVector(x).normalized).ToList());
            m.SetColors(plants.C); m.SetUVs(3, plants.U3); m.SetTriangles(plants.T, 0); m.RecalculateBounds();
            string ap = Dir + "/Kaze_Dandelions.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
            if (old != null) { old.Clear(); EditorUtility.CopySerialized(m, old); m = old; } else AssetDatabase.CreateAsset(m, ap);
            var go = Child(isl, "Dandelions");
            // 風でゆれる専用の材質（Den_Twig のコピー。ゆれ幅と向きだけ足す）
            string swp = "Assets/FuwaCourse/Materials/Kaze_DandelionSway.mat"; var sway = AssetDatabase.LoadAssetAtPath<Material>(swp);
            if (sway == null) { sway = new Material(twig); AssetDatabase.CreateAsset(sway, swp); }
            sway.CopyPropertiesFromMaterial(twig);
            sway.SetFloat("_SwayAmp", SwayAmp); sway.SetFloat("_SwaySpeed", SwaySpeed); sway.SetVector("_SwayDir", new Vector4(wind.x, 0, wind.z, 0)); EditorUtility.SetDirty(sway);
            go.GetComponent<MeshFilter>().sharedMesh = m; go.GetComponent<MeshRenderer>().sharedMaterial = sway;
            go.GetComponent<MeshRenderer>().receiveShadows = false;   // 花びらや葉どうしの影が濃く出て、ゆれとずれて見えるので自分には影を落とさない（島の地面には落とす）
            log.Append("tris " + plants.T.Count / 3 + " ");
        }
        // 綿毛の冠毛：いつもカメラを向く放射状の毛の半透明の板（FuwaCourse/DandelionTuft）。8個の頭を1つのメッシュにまとめる
        // 1枚＝4頂点を冠毛の中心に置き、uv＝角、uv2＝(大きさm, 回転)、uv3＝風のゆれ（茎の先と同じ）。法線＝頭の外向き（日の当たり具合）
        {
            var tv = new List<Vector3>(); var tn = new List<Vector3>(); var tu = new List<Vector2>(); var tu2 = new List<Vector2>(); var tu3 = new List<Vector4>(); var tt = new List<int>();
            var corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
            foreach (var (p, d, size, rot, head) in tufts)
            {
                int b = tv.Count;
                foreach (var cu in corners) { tv.Add(w2l.MultiplyPoint3x4(p)); tn.Add(w2l.MultiplyVector(d).normalized); tu.Add(cu); tu2.Add(new Vector2(size, rot)); tu3.Add(new Vector4(1f, _headPhase[head], 0, 0)); }
                tt.Add(b); tt.Add(b + 1); tt.Add(b + 2); tt.Add(b); tt.Add(b + 2); tt.Add(b + 3);
            }
            var tm = new Mesh { name = "Kaze_PuffTufts" }; tm.SetVertices(tv); tm.SetNormals(tn); tm.SetUVs(0, tu); tm.SetUVs(1, tu2); tm.SetUVs(3, tu3); tm.SetTriangles(tt, 0);
            tm.RecalculateBounds(); var bb = tm.bounds; bb.Expand(1.0f); tm.bounds = bb;   // 板は描く時に広がるぶん、ゆれのぶんも足す
            string ap = Dir + "/Kaze_PuffTufts.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
            if (old != null) { old.Clear(); EditorUtility.CopySerialized(tm, old); old.bounds = bb; tm = old; } else AssetDatabase.CreateAsset(tm, ap);
            string mp = "Assets/FuwaCourse/Materials/DandelionTuft.mat"; var tmat = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (tmat == null)
            {
                tmat = new Material(Shader.Find("FuwaCourse/DandelionTuft")); AssetDatabase.CreateAsset(tmat, mp);
                tmat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaCourse/Textures/DandelionPappus.png"); tmat.SetFloat("_Opacity", 1.1f);
            }
            tmat.SetFloat("_SwayAmp", SwayAmp); tmat.SetFloat("_SwaySpeed", SwaySpeed); tmat.SetVector("_SwayDir", new Vector4(wind.x, 0, wind.z, 0)); EditorUtility.SetDirty(tmat);
            var go = Child(isl, "PuffTufts");
            go.GetComponent<MeshFilter>().sharedMesh = tm; var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = tmat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            // 前の毛玉（シェルファー）はもう使わない
            var oldFur = isl.Find("PuffFur"); if (oldFur != null) Undo.DestroyObjectImmediate(oldFur.gameObject);
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

    // まとめメッシュの入れ物：あればそのまま使う（遠くで消す LODGroup などを残すため）
    static GameObject Child(Transform parent, string name)
    {
        var t = parent.Find(name); if (t != null) return t.gameObject;
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false); Undo.RegisterCreatedObjectUndo(go, "deco");
        return go;
    }

    // 綿毛（必ず）＋黄色い花（ときどき）の大きいたんぽぽ1組。ang が null なら分ける向きは乱数のまま。
    // 乱数を引く回数は ang に関係なく同じ。戻り値は頭の数、hd は頭の (中心, 見た目の半径)
    static int Pair(MB plants, System.Random rnd, Vector3 pos, float rad, float topY, float? ang, Vector3 wind, List<(Vector3, float)> heads, List<float> phases, out List<(Vector3 c, float r)> hd)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        hd = new List<(Vector3, float)>();
        float a0 = R(0, 360);
        var sep = Quaternion.Euler(0, ang ?? a0, 0) * Vector3.forward;
        var gp = pos - sep * R(0.9f, 1.2f);
        hd.Add(Giant(plants, new Vector3(gp.x, topY, gp.z), R(3.0f, 3.8f), true, wind, rnd, heads, phases));
        if (rnd.NextDouble() < 0.6)
        {
            var yp = pos + Quaternion.Euler(0, R(-25, 25), 0) * sep * R(1.6f, rad * 0.7f);
            hd.Add(Giant(plants, new Vector3(yp.x, topY, yp.z), R(2.2f, 2.9f), false, wind, rnd, heads, phases));
        }
        return hd.Count;
    }

    // コースの上の視点：道の上（目の高さ）と、進む向き（スタートから道に沿った距離が増える向き）
    static List<(Vector3 eye, Vector3 fwd)> PathViews(Transform root, Vector3 start)
    {
        var cells = new Dictionary<(int, int), float>();
        foreach (var nm in new[] { "PathMesh", "BridgePlanks" })
        {
            var t = root.Find(nm); var mf = t != null ? t.GetComponent<MeshFilter>() : null; if (mf == null || mf.sharedMesh == null) continue;
            foreach (var v in mf.sharedMesh.vertices) { var p = t.TransformPoint(v); var k = (Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z)); if (!cells.TryGetValue(k, out var y) || p.y > y) cells[k] = p.y; }
        }
        var keys = cells.Keys.ToList();
        var s0 = keys.OrderBy(k => new Vector2(k.Item1 + 0.5f - start.x, k.Item2 + 0.5f - start.z).sqrMagnitude).First();
        var dist = new Dictionary<(int, int), float> { [s0] = 0 };
        var open = new SortedSet<(float, int, int)> { (0f, s0.Item1, s0.Item2) };
        while (open.Count > 0)
        {
            var c = open.Min; open.Remove(c); var k = (c.Item2, c.Item3); if (c.Item1 > dist[k]) continue;
            for (int dx = -3; dx <= 3; dx++)
                for (int dz = -3; dz <= 3; dz++)
                {
                    var n = (k.Item1 + dx, k.Item2 + dz); if (n == k || !cells.TryGetValue(n, out var ny) || Mathf.Abs(ny - cells[k]) > 1.5f) continue;
                    float nd = c.Item1 + Mathf.Sqrt(dx * dx + dz * dz);
                    if (!dist.TryGetValue(n, out var od) || nd < od) { dist[n] = nd; open.Add((nd, n.Item1, n.Item2)); }
                }
        }
        var res = new List<(Vector3, Vector3)>();
        foreach (var k in dist.Keys)
        {
            var f = Vector3.zero;
            for (int dx = -3; dx <= 3; dx++)
                for (int dz = -3; dz <= 3; dz++)
                    if (dist.TryGetValue((k.Item1 + dx, k.Item2 + dz), out var nd) && (dx != 0 || dz != 0)) f += new Vector3(dx, 0, dz).normalized * (nd - dist[k]);
            if (f.sqrMagnitude < 1e-4f) continue;
            res.Add((new Vector3(k.Item1 + 0.5f, cells[k] + 1.5f, k.Item2 + 0.5f), f.normalized));
        }
        return res;
    }

    // 2つの頭の重なって見える度合い：前〜斜め前（左右80°以内）に見える視点で、小さい方の頭が3割より多く隠れる視点の割合。
    // 同じ割合なら重なりの平均が小さい方がいい（少しだけ足す）
    static float ViewCost((Vector3 c, float r) a, (Vector3 c, float r) b, List<(Vector3 eye, Vector3 fwd)> views)
    {
        int n = 0, bad = 0; float sum = 0;
        foreach (var (eye, fwd) in views)
        {
            var da = a.c - eye; var db = b.c - eye; float la = da.magnitude, lb = db.magnitude;
            if (la > 70f) continue;
            var mid = (da / la + db / lb); mid.y = 0; if (Vector3.Angle(mid, fwd) > 80f) continue;
            float ra = Mathf.Asin(Mathf.Min(1f, a.r / la)), rb = Mathf.Asin(Mathf.Min(1f, b.r / lb));
            float sepA = Vector3.Angle(da, db) * Mathf.Deg2Rad;
            float o = Mathf.Clamp01((ra + rb - sepA) / (2f * Mathf.Min(ra, rb)));
            n++; sum += o; if (o > 0.3f) bad++;
        }
        return n == 0 ? 0f : (float)bad / n + 0.1f * sum / n;
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
    // 戻り値は頭の (中心, 見た目の半径)
    static (Vector3, float) Giant(MB m, Vector3 at, float h, bool puff, Vector3 wind, System.Random rnd, List<(Vector3, float)> heads, List<float> phases)
    {
        (Vector3, float) look;
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
            // 頭そのもの（芯・軸・冠毛）は Run の最後に heads から作る。中心は茎の先の向きの延長に置く（芯が茎の先からずれて見えないように）
            var tan = (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized;
            heads.Add((top + tan * hr * 0.2f, hr)); phases.Add(phase);
            look = (top + Vector3.up * hr * 0.2f, hr * 0.75f + 0.5f);   // 芯＋毛の長さ
        }
        else
        {
            float fr = R(2.1f, 2.6f) * s; int fb = m.V.Count; Flower(m, top, fr, rnd);
            // 花の頭（花びら＋がく）は、風でしなった茎の先の向きに合わせて風下へうなずかせる（頭の重みで茎の先より少し深め）
            var tan = (Vector3.up * h + lean * 2f).normalized;
            var q = Quaternion.AngleAxis(Vector3.Angle(Vector3.up, tan) * 1.4f, Vector3.Cross(Vector3.up, tan).normalized);
            for (int i = fb; i < m.V.Count; i++) { m.V[i] = top + q * (m.V[i] - top); m.N[i] = q * m.N[i]; }
            look = (top + Vector3.up * fr * 0.15f, fr * 0.9f);
        }
        // 風のゆれの重み：根元0 → 先1（高さの2乗）。葉は地面近くなのでほぼゆれない
        for (int i2 = leafEnd; i2 < m.V.Count; i2++) { float w =Mathf.Clamp01((m.V[i2].y - at.y) / h); m.U3[i2] = new Vector4(w * w, phase, 0, 0); }
        return look;
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
    const float GiantLeafSway = 0.55f, SmallLeafSway = 0.14f;

    // 2026-10-16 てじゃ「葉っぱもクオリティあげよ　実物と比べるとちいさすぎる　とはいえ実物ほどでかいと花が目立たない」
    // → ひと回り大きく（LeafScale）、根元から立ち上がって先ほど弓なりに垂れる形、深い切れ込みの根元向きの歯（左右で少しずらす）、
    //   根元は明るい黄緑→先ほど濃い緑のグラデ、明るい葉脈。長い葉のあいだに短い葉を足してロゼットらしく。
    // rnd は前と同じ回数だけ引く（1枚につき5回。ほかの飾りの並びを変えないため）。形のゆらぎは場所から作った別の乱数で
    const float LeafScale = 1.35f;
    static void Rosette(MB m, Vector3 at, float len, int n, System.Random rnd, float leafSway)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        var fr = new System.Random(Mathf.RoundToInt(at.x * 613f) * 17 + Mathf.RoundToInt(at.z * 727f) + n);
        float F(float a, float b) { return a + (float)fr.NextDouble() * (b - a); }
        for (int i = 0; i < n; i++)
        {
            float a0 = (i + R(-0.3f, 0.3f)) * Mathf.PI * 2 / n;
            float L = len * R(0.7f, 1.1f) * LeafScale;
            float W = Mathf.Min(L, len * LeafScale) * R(0.15f, 0.2f) * 0.95f;
            float tone = R(0.92f, 1.12f);
            float ang = R(30f, 45f) * Mathf.Deg2Rad;
            float lph = Mathf.Repeat(at.x * 1.7f + at.z * 2.3f + i * 2.399f, 6.2832f);
            Leaf(m, at, a0, L, W, tone, ang, lph, leafSway, F);
            // あいだの短い葉（6割くらい）
            if (fr.NextDouble() < 0.6)
            {
                float a1 = a0 + Mathf.PI / n * F(0.7f, 1.3f);
                Leaf(m, at, a1, L * F(0.5f, 0.7f), W * F(0.75f, 0.9f), tone * F(0.95f, 1.05f), ang + F(0.05f, 0.2f), lph + 1.3f, leafSway, F);
            }
        }
    }

    delegate float RandF(float a, float b);

    static void Leaf(MB m, Vector3 at, float a0, float L, float W, float tone, float ang, float lph, float leafSway, RandF F)
    {
        var d = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)); var sd = new Vector3(-d.z, 0, d.x);
        {   // 島のふちまで。先は垂れるので少しはみ出してよい
            var o = new Vector2(at.x - _islC.x, at.z - _islC.z); var dd = new Vector2(d.x, d.z);
            float b2 = Vector2.Dot(o, dd), cc = o.sqrMagnitude - _islR * _islR, disc = b2 * b2 - cc;
            if (disc > 0) L = Mathf.Min(L, Mathf.Max(0.15f, -b2 + Mathf.Sqrt(disc) + 0.5f));
        }
        // 幅の折れ線（u, 幅）：細い柄 → 深い切れ込みと根元向きの歯 → 大きい三角の先。左右で歯を少しずらす
        int teeth = 4;
        List<Vector2> Side(float shift)
        {
            var pr = new List<Vector2> { new Vector2(0f, 0.07f), new Vector2(0.1f, 0.12f) };
            for (int t = 0; t < teeth; t++)
            {
                float u0 = 0.15f + t * 0.16f + shift;
                float deep = F(0.13f, 0.2f) + t * 0.03f, tip = Mathf.Lerp(0.72f, 1.0f, t / (teeth - 1f)) * F(0.88f, 1.06f);
                pr.Add(new Vector2(u0, deep));                 // 切れ込み（深く）
                pr.Add(new Vector2(u0 + 0.06f, tip));          // 裂片の先（少し根元向き）
            }
            pr.Add(new Vector2(0.8f + shift * 0.5f, 0.3f));    // 先の大きい三角の付け根
            pr.Add(new Vector2(0.84f + shift * 0.5f, 1.0f));
            pr.Add(new Vector2(1f, 0f));
            return pr;
        }
        var left = Side(0f); var right = Side(0.05f);
        float Wd(List<Vector2> pr, float u) { for (int k = 1; k < pr.Count; k++) if (u <= pr[k].x) return Mathf.Lerp(pr[k - 1].y, pr[k].y, (u - pr[k - 1].x) / Mathf.Max(1e-4f, pr[k].x - pr[k - 1].x)); return 0f; }
        var us = left.Select(p => p.x).Concat(right.Select(p => p.x)).Concat(Enumerable.Range(0, 6).Select(k => k / 5f)).Where(u => u >= 0f && u <= 1f).Distinct().OrderBy(u => u).ToList();
        // 中心線：根元は ang で立ち上がり、先ほど下へ（先は水平より少し下まで）。横にも少しよじる
        float droopEnd = F(12f, 26f) * Mathf.Deg2Rad, curl = F(-0.18f, 0.18f);
        var baseCol = new Color(0.55f, 0.78f, 0.3f) * tone; var tipCol = new Color(0.2f, 0.56f, 0.17f) * tone;
        int bi = m.V.Count; Vector3 pos = at + Vector3.up * 0.02f; float prevU = 0f;
        for (int j = 0; j < us.Count; j++)
        {
            float u = us[j];
            float th = ang - (ang + droopEnd) * Mathf.Pow(u, 1.4f), yw = curl * u;
            var hd = Quaternion.AngleAxis(yw * Mathf.Rad2Deg, Vector3.up) * d;
            var tan = (hd * Mathf.Cos(th) + Vector3.up * Mathf.Sin(th)).normalized;
            if (j > 0) pos += tan * L * (u - prevU);
            prevU = u;
            var sdj = Vector3.Cross(Vector3.up, hd).normalized * -1f;   // 横（左右は sd と同じ向き）
            if (Vector3.Dot(sdj, sd) < 0) sdj = -sdj;
            var nrm = Vector3.Cross(sdj, tan).normalized; if (nrm.y < 0) nrm = -nrm;
            float wl = W * Wd(left, u), wr = W * Wd(right, u);
            var col = Color.Lerp(baseCol, tipCol, Mathf.SmoothStep(0f, 1f, u));
            var rib = Color.Lerp(col, new Color(0.82f, 0.93f, 0.55f), Mathf.Lerp(0.55f, 0.3f, u));
            m.Add(pos - sdj * wl + nrm * wl * 0.3f, nrm, col * 0.97f); m.Add(pos + nrm * W * 0.03f, nrm, rib); m.Add(pos + sdj * wr + nrm * wr * 0.3f, nrm, col);
            for (int q = 0; q < 3; q++) m.U3[m.V.Count - 1 - q] = new Vector4(leafSway * u * u, lph, 0, 0);
        }
        for (int j = 0; j < us.Count - 1; j++) { int a = bi + j * 3, b = bi + (j + 1) * 3; m.Quad(a, a + 1, b + 1, b); m.Quad(a + 1, a + 2, b + 2, b + 1); }   // 上向きの面
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

    // 黄色い花（2026-10-16 てじゃ「白たんぽぽめっちゃクオリティあがってるし、こっちもクオリティアップできないかな、負荷と相談」）。
    // 本物のたんぽぽの頭のように、細い舌状花を5重に（外ほど長く平らで先が少し垂れ、内ほど短く立ち上がって真ん中がこんもり）。
    // 花びらは根元オレンジ→先が明るい黄色、先は5つにギザギザ。がくは花びらに添う内側と、下に反り返る外側の2種類。
    // 形のゆらぎは花の位置から作った別の乱数で（rnd を引く回数を変えると、ほかの飾りの並びが全部変わるので rnd は使わない）。
    // 大きい黄色い花は6本だけなので、1本2千ポリゴンくらいまでは気にしない
    static void Flower(MB m, Vector3 c, float r, System.Random rnd)
    {
        var fr = new System.Random(Mathf.RoundToInt(c.x * 997f) * 31 + Mathf.RoundToInt(c.z * 991f));
        float F(float a0, float b0) { return a0 + (float)fr.NextDouble() * (b0 - a0); }
        int pv0 = m.V.Count, pt0 = m.T.Count;
        const int Rings = 5, Seg = 3;
        int[] counts = { 30, 26, 22, 18, 13 };
        for (int k = 0; k < Rings; k++)
        {
            float t = k / (Rings - 1f);
            int n = counts[k];
            float L = r * Mathf.Lerp(1f, 0.3f, t);              // 花びらの長さ
            float rise = Mathf.Lerp(0.12f, 1.15f, t * t);        // 立ち上がり（内ほど立つ）
            float droop = Mathf.Lerp(0.16f, 0f, t);              // 先の垂れ（外ほど）
            float baseY = r * 0.035f * k, baseR = r * Mathf.Lerp(0.1f, 0.03f, t);
            var colBase = Color.Lerp(new Color(1f, 0.6f, 0.07f), new Color(0.98f, 0.5f, 0.04f), t);
            var colTip = Color.Lerp(new Color(1f, 0.9f, 0.26f), new Color(1f, 0.74f, 0.14f), t);
            for (int i = 0; i < n; i++)
            {
                float a = (i + k * 0.37f + F(-0.18f, 0.18f)) * Mathf.PI * 2 / n;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var sd = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                float len = L * F(0.86f, 1.08f), w = len * Mathf.Lerp(0.085f, 0.14f, t), dr = droop * F(0.6f, 1.3f), rs = rise * F(0.85f, 1.15f);
                int b = m.V.Count;
                Vector3 endDir = dir;
                for (int sgi = 0; sgi <= Seg; sgi++)
                {
                    float u = sgi / (float)Seg;
                    var pc = c + dir * (baseR + len * u) + Vector3.up * (baseY + len * (rs * u - dr * u * u));
                    float wu = w * (0.35f + 0.65f * Mathf.Sin(Mathf.Min(1f, u * 1.6f) * Mathf.PI * 0.5f)) * (u > 0.75f ? Mathf.Lerp(1f, 0.85f, (u - 0.75f) * 4f) : 1f);
                    var col = Color.Lerp(colBase, colTip, Mathf.SmoothStep(0f, 1f, u * 1.15f));
                    m.Add(pc - sd * wu, Vector3.up, col); m.Add(pc + sd * wu, Vector3.up, col);
                    if (sgi > 0) { int q = b + (sgi - 1) * 2; m.Quad(q, q + 1, q + 3, q + 2); }
                    if (sgi == Seg) endDir = (pc - (c + dir * (baseR + len * (u - 1f / Seg)) + Vector3.up * (baseY + len * (rs * (u - 1f / Seg) - dr * (u - 1f / Seg) * (u - 1f / Seg))))).normalized;
                }
                // 先のギザギザ（とがり3つ＋へこみ2つ）
                int eL = b + Seg * 2, eR = eL + 1; var pL = m.V[eL]; var pR = m.V[eR]; var tc = Color.Lerp(colTip, Color.white, 0.08f);
                var d1 = Vector3.Lerp(pL, pR, 0.34f); var d2 = Vector3.Lerp(pL, pR, 0.66f); var ext = endDir * len * 0.05f;
                int i1 = m.Add(d1, Vector3.up, tc), i2 = m.Add(d2, Vector3.up, tc);
                int tL = m.Add(Vector3.Lerp(pL, d1, 0.5f) + ext * 0.8f, Vector3.up, tc), tM = m.Add(Vector3.Lerp(d1, d2, 0.5f) + ext, Vector3.up, tc), tR = m.Add(Vector3.Lerp(d2, pR, 0.5f) + ext * 0.8f, Vector3.up, tc);
                m.Tri(eL, i1, tL); m.Tri(i1, i2, tM); m.Tri(i2, eR, tR);
            }
        }
        // 花びらの裏：両面描画でも裏から見ると法線が下向きになって暗く沈む（うなずいた花を後ろから見た時に茶色っぽくなる）。
        // 少し下にずらした裏向きの面を足して、法線は表と同じ上向きのままにして明るく見せる（裏も黄色、少しだけ濃いめ）
        {
            int pv1 = m.V.Count, pt1 = m.T.Count; float off = r * 0.01f;
            for (int i = pv0; i < pv1; i++) m.Add(m.V[i] - m.N[i] * off, m.N[i], Color.Lerp(m.C[i], new Color(0.95f, 0.62f, 0.12f), 0.25f));
            for (int t = pt0; t < pt1; t += 3) m.Tri(m.T[t] - pv0 + pv1, m.T[t + 2] - pv0 + pv1, m.T[t + 1] - pv0 + pv1);
        }
        // 花のまんなか：つぼみのような小さい山（いちばん内の花びらの根元をふさぐ）
        {
            var cc = new Color(0.97f, 0.55f, 0.06f); int n = 12, ci = m.Add(c + Vector3.up * r * 0.2f, Vector3.up, cc);
            for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2 / n; m.Add(c + new Vector3(Mathf.Cos(a) * r * 0.08f, r * 0.13f, Mathf.Sin(a) * r * 0.08f), Vector3.up, cc); }
            for (int i = 0; i < n; i++) m.Tri(ci, ci + 1 + (i + 1) % n, ci + 1 + i);
            int ri = m.V.Count;
            for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2 / n; m.Add(c + new Vector3(Mathf.Cos(a) * r * 0.13f, 0f, Mathf.Sin(a) * r * 0.13f), Vector3.up, cc); }
            for (int i = 0; i < n; i++) m.Quad(ci + 1 + i, ci + 1 + (i + 1) % n, ri + (i + 1) % n, ri + i);
        }
        // がく（2026-10-16）：花の中心から出すと花びらの上に突き抜けて緑がもれた。細い筒はトゲに、平たい葉を放射状に並べると
        // 下から見て緑の花がもう1つ咲いたように見えた（てじゃ「重なりがきになる」「しっくりこない」）。
        // 2026-10-16 てじゃ「しっくりこない」→ カップ型（1案）：内側のがくは立てて花の裏をすき間なく包む緑のおわん
        //（回転体。ふちは丸いがくの先が16個並ぶ波形。まん中が明るい縦すじ）。外側のがくは細く短く、茎ぞいに垂れる
        {
            const int CSeg = 48, Lobes = 16;
            var prof = new[] { new Vector2(0.034f, -0.15f), new Vector2(0.06f, -0.125f), new Vector2(0.095f, -0.09f), new Vector2(0.13f, -0.05f), new Vector2(0.155f, -0.015f) };
            var dark = StemG * 0.8f; var light = StemG * 0.92f;   // 茎と同じ色み（明るいと浮いて見えた。2026-10-16 てじゃ「茎より明るいから違和感」）
            int rows = prof.Length + 1, cb = m.V.Count;
            for (int j = 0; j < rows; j++)
                for (int i = 0; i <= CSeg; i++)
                {
                    float a = i * Mathf.PI * 2 / CSeg; var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float lobe = Mathf.Abs(Mathf.Cos(i * Mathf.PI * Lobes / CSeg));   // 1＝がくの真ん中、0＝あいだ
                    Vector2 pr; float k;
                    if (j < prof.Length) { pr = prof[j]; k = j / (float)prof.Length; }
                    else { pr = prof[prof.Length - 1] + new Vector2(0.012f, 0.045f) * Mathf.Sqrt(lobe); k = 1f; }   // ふち：がくの先が丸く出る
                    var pos = c + o * pr.x * r + Vector3.up * pr.y * r;
                    var col = Color.Lerp(dark, light, k * 0.7f + lobe * 0.3f);
                    m.Add(pos, (o + Vector3.up * 0.15f).normalized, col);   // 法線は茎と同じく横向き（上向きだと茎より明るく照らされた）
                }
            for (int j = 0; j < rows - 1; j++)
                for (int i = 0; i < CSeg; i++) { int q = cb + j * (CSeg + 1) + i; m.Quad(q, q + CSeg + 1, q + CSeg + 2, q + 1); }
            // 底のふた（茎とのすき間）
            int bc = m.Add(c + Vector3.up * prof[0].y * r - Vector3.up * r * 0.01f, Vector3.down, dark);
            for (int i = 0; i < CSeg; i++) m.Tri(bc, cb + i + 1, cb + i);
        }
        void Beard(float a)
        {
            // 外側のがく：細いひげ。おわんの下のほうから外へ少し出て、茎ぞいに下へ垂れる。両面同じ明るさ
            var o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); var side = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            var p0 = c + o * r * 0.075f + Vector3.up * -0.115f * r; var p1 = p0 + (o * 0.45f - Vector3.up * 0.9f).normalized * r * 0.055f; var p2 = p1 + (o * 0.05f - Vector3.up).normalized * r * 0.06f;   // 外へ張り出すとクモの足に見えたので、茎ぞいに
            var ps = new[] { p0, p1, p2 }; var ws = new[] { r * 0.022f, r * 0.018f, r * 0.009f };
            int bi = m.V.Count, bt = m.T.Count;
            for (int j = 0; j < 3; j++) { m.Add(ps[j] - side * ws[j], o, StemG * 0.82f); m.Add(ps[j] + side * ws[j], o, StemG * 0.82f); }
            m.Quad(bi, bi + 1, bi + 3, bi + 2); m.Quad(bi + 2, bi + 3, bi + 5, bi + 4);
            int tip = m.Add(p2 + (p2 - p1).normalized * ws[2], o, StemG * 0.82f); m.Tri(bi + 4, bi + 5, tip);
            var pn = Vector3.Cross(side, (p1 - p0).normalized).normalized * r * 0.003f;
            int vb2 = m.V.Count, ve = vb2, te = m.T.Count;
            for (int i = bi; i < ve; i++) m.Add(m.V[i] - pn, m.N[i], m.C[i]);
            for (int t = bt; t < te; t += 3) m.Tri(m.T[t] - bi + vb2, m.T[t + 2] - bi + vb2, m.T[t + 1] - bi + vb2);
        }
        for (int i = 0; i < 9; i++) Beard((i + 0.25f) * Mathf.PI * 2 / 9);
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
