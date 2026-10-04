using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// まもののす（FuwaCourse4）の飾りを作る。何度実行しても同じ結果（作り直し）。
// A：まわりの空に浮かぶ小さい巣（コースのポッドを縮めて流用）＋中にタマゴ（割れたのも）
// C：巣の床に小枝・羽根・タマゴのカラ・抜け毛
// D：巣の裏から垂れる根っこ・ツタ
// 全部 Den_Twig（InkCap・両面・頂点カラー）で、種類ごとに1つのメッシュにまとめる。当たり判定なし
public static class FuwaMamonoDeco
{
    const string Dir = "Assets/FuwaCourse/Generated/Mamono";
    const string Course = "FuwaCourse4";

    class MB
    {
        public List<Vector3> V = new List<Vector3>(); public List<Vector3> N = new List<Vector3>();
        public List<Color> C = new List<Color>(); public List<int> T = new List<int>();
        public int Add(Vector3 p, Vector3 n, Color c) { V.Add(p); N.Add(n.normalized); C.Add(new Color(c.r, c.g, c.b, 1)); return V.Count - 1; }
        public void Tri(int a, int b, int c) { T.Add(a); T.Add(b); T.Add(c); }
        public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }
    }

    [MenuItem("FuwaCourse/まもののすの飾りを作る")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Mamono");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var root = roots.First(g => g.name == Course).transform;
        var den = root.Find("Den");
        var path = root.Find("PathMesh");
        var twig = AssetDatabase.FindAssets("Den_Twig t:Material").Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).First(m => m.name == "Den_Twig");
        var rnd = new System.Random(4242);
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        var log = new System.Text.StringBuilder();

        // 道（床）の点：距離の判定用
        var pv = path.GetComponent<MeshFilter>().sharedMesh.vertices.Where((v, i) => i % 3 == 0).Select(v => path.TransformPoint(v)).ToList();
        float PathY = pv.Average(p => p.y);
        float DistPath(Vector3 c) { return pv.Min(q => new Vector2(q.x - c.x, q.z - c.z).magnitude); }
        // 他のコース（ここから離して置く）
        // 小さい物は箱で、大きい物（道など。箱だとまわりの空まで含んでしまう）は頂点で調べる
        var otherR = roots.Where(g => g.name.StartsWith("FuwaCourse") && g.name != Course || g.name.Contains("Lobby"))
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

        // 自分のコースの巣（床より外にふちや底がはみ出している）にも近づけない
        var selfPts = new List<Vector3>();
        foreach (var r in den.GetComponentsInChildren<MeshRenderer>())
        {
            var vv = r.GetComponent<MeshFilter>().sharedMesh.vertices; int stp = Mathf.Max(1, vv.Length / 300);
            for (int i = 0; i < vv.Length; i += stp) selfPts.Add(r.transform.TransformPoint(vv[i]));
        }
        float DistSelf(Bounds b) { float d = 999f; foreach (var p in selfPts) { float e = Mathf.Sqrt(b.SqrDistance(p)); if (e < d) d = e; } return d; }

        // ---- A：まわりの小さい巣 ----
        var podSrc = new[] { "Pod1", "Pod2", "Pod3" }.Select(n => den.Find(n)).Where(t => t != null).ToList();
        var pods = new List<(Vector3[] v, Vector3[] n, Color[] c, int[] t, float rad, float depth)>();
        foreach (var p in podSrc)
        {
            var mesh = p.GetComponent<MeshFilter>().sharedMesh; var l2w = p.localToWorldMatrix;
            var v = mesh.vertices.Select(x => l2w.MultiplyPoint3x4(x)).ToArray(); var n = mesh.normals.Select(x => l2w.MultiplyVector(x).normalized).ToArray();
            var b = p.GetComponent<Renderer>().bounds; float rad = Mathf.Max(b.extents.x, b.extents.z);
            // 床の高さ：まん中あたりの一番高い頂点
            float floor = v.Where(x => new Vector2(x.x - b.center.x, x.z - b.center.z).magnitude < rad * 0.3f).Select(x => x.y).DefaultIfEmpty(b.center.y).Max();
            var off = new Vector3(b.center.x, floor, b.center.z);
            for (int i = 0; i < v.Length; i++) v[i] -= off;
            var cols = mesh.colors.Length == v.Length ? mesh.colors : Enumerable.Repeat(new Color(0.5f, 0.38f, 0.27f), v.Length).ToArray();
            // 飾りなので間引いて軽くする（遠景の軽量化と同じ間引き）
            var tmp = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; tmp.SetVertices(v); tmp.SetNormals(n); tmp.SetColors(cols); tmp.SetTriangles(mesh.triangles, 0);
            var simp = (Mesh)typeof(FuwaFarViewSetup).GetMethod("Simplify", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { tmp, Vector3.one * 0.28f });
            pods.Add((simp.vertices, simp.normals, simp.colors, simp.triangles, rad, floor - b.min.y));
        }
        var nests = new MB(); var eggs = new MB(); var roots_ = new MB(); var floorMb = new MB();
        var placed = new List<(Vector3 pos, float rad)>();
        int want = 9, tries = 0;
        while (placed.Count < want && tries++ < 6000)
        {
            var anchor = pv[rnd.Next(pv.Count)];
            float ang = R(0, Mathf.PI * 2), dist = R(7f, 17f);
            var pos = new Vector3(anchor.x + Mathf.Cos(ang) * dist, PathY + R(-4.5f, 4.5f), anchor.z + Mathf.Sin(ang) * dist);
            int pi = rnd.Next(pods.Count); float sc = R(0.45f, 0.8f); var pd = pods[pi];
            float rad = pd.rad * sc;
            float dp = DistPath(pos); if (dp < 5f + rad || dp > 19f) continue;
            if (placed.Any(q => new Vector2(q.pos.x - pos.x, q.pos.z - pos.z).magnitude < q.rad + rad + 4f)) continue;
            var bb = new Bounds(pos + Vector3.down * pd.depth * sc * 0.5f, new Vector3(rad * 2, pd.depth * sc + 1f, rad * 2));
            if (DistOther(bb) < 12f || DistSelf(bb) < 3f) continue;
            placed.Add((pos, rad));
            var xf = Matrix4x4.TRS(pos, Quaternion.Euler(R(-4, 4), R(0, 360), R(-4, 4)), Vector3.one * sc);
            int bi = nests.V.Count;
            for (int i = 0; i < pd.v.Length; i++) { nests.V.Add(xf.MultiplyPoint3x4(pd.v[i])); nests.N.Add(xf.MultiplyVector(pd.n[i]).normalized); nests.C.Add(pd.c[i]); }
            foreach (var t in pd.t) nests.T.Add(bi + t);
            // タマゴ：3個セット。寄り添って少し外へ傾き、まわりをしきわらで囲む（ときどき1個割れてる）
            {
                float er = R(0.17f, 0.22f) * Mathf.Lerp(1f, sc / 0.6f, 0.4f);
                var col = EggColor(rnd); float d = er * 1.18f, a0 = R(0, 360);
                Bedding(eggs, pos, d + er * 0.95f, er, rnd);
                int crackedOne = rnd.NextDouble() < 0.45 ? rnd.Next(3) : -1;
                for (int k = 0; k < 3; k++)
                {
                    var outDir = Quaternion.Euler(0, a0 + k * 120 + R(-10, 10), 0) * Vector3.forward;
                    var ep = pos + outDir * d;
                    var tilt = Quaternion.AngleAxis(R(8, 16), Vector3.Cross(Vector3.up, outDir)) * Quaternion.Euler(0, R(0, 360), 0);
                    bool cracked = k == crackedOne;
                    Shell(eggs, Matrix4x4.TRS(ep + Vector3.up * er * 0.85f, tilt, Vector3.one), er, er * 1.3f, cracked ? R(0.9f, 1.2f) : 0f, Mathf.PI, col, rnd);
                    if (cracked)   // 割れたカラのフタが横に転がってる
                        Shell(eggs, Matrix4x4.TRS(pos + outDir * (d + er * 2.6f) + Vector3.up * er * 0.35f, Quaternion.Euler(R(150, 200), R(0, 360), R(-20, 20)), Vector3.one), er, er * 1.3f, 0f, R(0.8f, 1.0f), col, rnd);
                }
            }
        }
        log.Append("nests " + placed.Count + " ");

        // ---- D：巣の裏から垂れる根っこ・ツタ ----
        var underSrc = new List<(Vector3 p, Vector3 n)>();
        foreach (var r in den.GetComponentsInChildren<MeshRenderer>())
        {
            if (r.name.StartsWith("Bridge")) continue;
            var mesh = r.GetComponent<MeshFilter>().sharedMesh; var l2w = r.transform.localToWorldMatrix;
            var v = mesh.vertices; var n = mesh.normals;
            var cand = new List<(Vector3, Vector3)>();
            for (int i = 0; i < v.Length; i++) { var wn = l2w.MultiplyVector(n[i]).normalized; if (wn.y < -0.35f) cand.Add((l2w.MultiplyPoint3x4(v[i]), wn)); }
            var b = r.bounds; int count = Mathf.Clamp(Mathf.RoundToInt(b.size.x * b.size.z / 2.2f), 8, 28);
            for (int k = 0; k < count && cand.Count > 0; k++) underSrc.Add(cand[rnd.Next(cand.Count)]);
        }
        // 浮かべた巣にも少し
        {
            var cand = new List<(Vector3, Vector3)>();
            for (int i = 0; i < nests.V.Count; i++) if (nests.N[i].y < -0.35f) cand.Add((nests.V[i], nests.N[i]));
            for (int k = 0; k < placed.Count * 6 && cand.Count > 0; k++) underSrc.Add(cand[rnd.Next(cand.Count)]);
        }
        foreach (var (p0, n0) in underSrc)
        {
            bool vine = rnd.NextDouble() < 0.3;
            float len = vine ? R(1.2f, 3.0f) : R(0.8f, 2.4f), r0 = vine ? R(0.028f, 0.042f) : R(0.05f, 0.1f);
            int segs = 7; var pts = new List<Vector3>(); var rr = new List<float>();
            var p = p0 + Vector3.up * 0.06f; var dir = (Vector3.down * 2f + n0).normalized;
            float sway = R(0, Mathf.PI * 2);
            for (int s = 0; s <= segs; s++)
            {
                float u = s / (float)segs;
                pts.Add(p); rr.Add(Mathf.Lerp(r0, r0 * 0.12f, Mathf.Pow(u, 0.8f)));
                dir = (dir + Vector3.down * 0.5f + new Vector3(Mathf.Sin(sway + u * 5f), 0, Mathf.Cos(sway + u * 4f)) * 0.25f).normalized;
                p += dir * len / segs;
            }
            var col = vine ? new Color(0.36f, 0.55f, 0.27f) * R(0.9f, 1.1f) : new Color(0.42f, 0.3f, 0.21f) * R(0.85f, 1.1f);
            Tube(roots_, pts, rr, 5, col);
            // 根っこには小さい枝分かれ
            if (!vine && rnd.NextDouble() < 0.6)
            {
                int at = rnd.Next(2, 5); var q = pts[at]; var bd = (Vector3.down + new Vector3(R(-1, 1), 0, R(-1, 1)) * 0.8f).normalized;
                Tube(roots_, new List<Vector3> { q, q + bd * len * 0.18f, q + (bd + Vector3.down * 0.4f).normalized * len * 0.32f }, new List<float> { rr[at] * 0.6f, rr[at] * 0.35f, rr[at] * 0.08f }, 4, col * 0.95f);
            }
        }
        log.Append("roots " + underSrc.Count + " ");

        // ---- C：巣の床の小物 ----
        var floorCols = den.GetComponentsInChildren<Collider>().Concat(path.GetComponents<Collider>()).ToList();
        var spikes = root.Find("Spikes") != null ? root.Find("Spikes").Cast<Transform>().Select(t => t.position).ToList() : new List<Vector3>();
        var pb = path.GetComponent<Renderer>().bounds;
        Color fur = new Color(0.45f, 0.3f, 0.65f);
        var furMat = AssetDatabase.FindAssets("Fuwa_EnemyFur t:Material").Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        if (furMat != null && furMat.HasProperty("_Color")) fur = furMat.color;
        int nTwig = 0, nFeather = 0, nShell = 0, nTuft = 0; tries = 0;
        int[] quota = { 70, 0, 18, 0 };   // 羽根は鳥じゃないのでなし。抜け毛は下で敵のまわりにだけ散らす
        while (tries++ < 20000 && (nTwig < quota[0] || nFeather < quota[1] || nShell < quota[2] || nTuft < quota[3]))
        {
            var o = new Vector3(R(pb.min.x, pb.max.x), pb.max.y + 6f, R(pb.min.z, pb.max.z));
            if (!Physics.Raycast(o, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!floorCols.Contains(hit.collider) || hit.normal.y < 0.85f) continue;
            if (spikes.Any(s => new Vector2(s.x - hit.point.x, s.z - hit.point.z).magnitude < 1.6f)) continue;
            // ふちの近く（少し先で床が無い／上がっている）を優先
            bool nearEdge = false;
            for (int k = 0; k < 6 && !nearEdge; k++)
            {
                var d = Quaternion.Euler(0, k * 60, 0) * Vector3.forward * 0.9f;
                if (!Physics.Raycast(hit.point + d + Vector3.up * 2f, Vector3.down, out var h2, 3f, ~0, QueryTriggerInteraction.Ignore) || h2.point.y > hit.point.y + 0.15f) nearEdge = true;
            }
            if (!nearEdge && rnd.NextDouble() < 0.75) continue;
            var yaw = Quaternion.Euler(0, R(0, 360), 0); var fp = hit.point + Vector3.up * 0.005f;
            int kind = rnd.Next(4);
            if (kind == 0 && nTwig < quota[0])
            {
                // 小枝は1〜3本の小さな山にする
                int pile = rnd.Next(1, 4);
                for (int k = 0; k < pile; k++) Twig(floorMb, fp + Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward * R(0, 0.15f) + Vector3.up * k * 0.02f, Quaternion.Euler(0, R(0, 360), 0), R(0.4f, 0.9f), R(0.02f, 0.034f), rnd);
                nTwig++;
            }
            else if (kind == 1 && nFeather < quota[1]) { Feather(floorMb, Matrix4x4.TRS(fp, yaw, Vector3.one), R(0.26f, 0.42f), R(0.055f, 0.085f), FeatherColor(rnd)); nFeather++; }
            else if (kind == 2 && nShell < quota[2])
            {
                float er = R(0.11f, 0.17f); var col = EggColor(rnd);
                Shell(floorMb, Matrix4x4.TRS(fp + Vector3.up * er * 0.25f, Quaternion.Euler(R(150, 210), R(0, 360), R(-25, 25)), Vector3.one), er, er * 1.3f, 0f, R(0.9f, 1.3f), col, rnd);
                if (rnd.NextDouble() < 0.5) Shell(floorMb, Matrix4x4.TRS(fp + yaw * Vector3.forward * er * 2.4f + Vector3.up * er * 0.2f, Quaternion.Euler(R(160, 200), R(0, 360), 0), Vector3.one * 0.8f), er, er * 1.3f, 0f, R(0.6f, 0.9f), col, rnd);
                nShell++;
            }
            else if (kind == 3 && nTuft < quota[3]) { Tuft(floorMb, fp, fur, rnd); nTuft++; }
        }
        // 抜け毛：敵（とママ）が潜んでいる所のまわりにだけ少し散らす
        var lurkers = new List<(Vector3 p, int n, float r0, float r1)>();
        var en = root.Find("Enemies"); if (en != null) foreach (Transform e in en) lurkers.Add((e.position, rnd.Next(4, 7), 0.35f, 1.6f));
        var mama = root.Find("Mama"); if (mama != null) lurkers.Add((mama.position, 10, 1.4f, 3.2f));
        foreach (var (lp, n, r0, r1) in lurkers)
            for (int k = 0, t2 = 0; k < n && t2 < 60; t2++)
            {
                var o = lp + Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward * R(r0, r1) + Vector3.up * 3f;
                if (!Physics.Raycast(o, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!floorCols.Contains(hit.collider) || hit.normal.y < 0.85f) continue;
                Tuft(floorMb, hit.point + Vector3.up * 0.005f, fur, rnd); nTuft++; k++;
            }
        log.Append("twig " + nTwig + " feather " + nFeather + " shell " + nShell + " tuft " + nTuft + " ");

        // ---- まとめて保存・配置 ----
        var deco = root.Find("Deco");
        if (deco == null) { deco = new GameObject("Deco").transform; deco.SetParent(root, false); Undo.RegisterCreatedObjectUndo(deco.gameObject, "deco"); }
        deco.localPosition = Vector3.zero; deco.localRotation = Quaternion.identity; deco.localScale = Vector3.one;
        var w2l = deco.worldToLocalMatrix;
        foreach (var (name, mb) in new[] { ("Mamono_Nests", nests), ("Mamono_Eggs", eggs), ("Mamono_Roots", roots_), ("Mamono_Floor", floorMb) })
        {
            var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(mb.V.Select(x => w2l.MultiplyPoint3x4(x)).ToList()); m.SetNormals(mb.N.Select(x => w2l.MultiplyVector(x).normalized).ToList());
            m.SetColors(mb.C); m.SetTriangles(mb.T, 0); m.RecalculateBounds();
            string ap = Dir + "/" + name + ".asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
            if (old != null) { old.Clear(); EditorUtility.CopySerialized(m, old); m = old; } else AssetDatabase.CreateAsset(m, ap);
            var t = deco.Find(name); GameObject go = t != null ? t.gameObject : null;
            if (go == null) { go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(deco, false); Undo.RegisterCreatedObjectUndo(go, "deco"); }
            go.GetComponent<MeshFilter>().sharedMesh = m;
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = twig;
            // 小物と根っこは影を落とさない（軽く）。浮かべた巣とタマゴは落とす
            mr.shadowCastingMode = name == "Mamono_Nests" || name == "Mamono_Eggs" ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            log.Append(name + " tris " + mb.T.Count / 3 + " ");
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return log.ToString();
    }

    static Color EggColor(System.Random rnd)
    {
        Color[] c = { new Color(0.97f, 0.93f, 0.82f), new Color(0.86f, 0.92f, 0.95f), new Color(0.95f, 0.86f, 0.9f), new Color(0.9f, 0.88f, 0.98f) };
        return c[rnd.Next(c.Length)];
    }

    static Color FeatherColor(System.Random rnd)
    {
        Color[] c = { new Color(0.95f, 0.94f, 0.97f), new Color(0.72f, 0.6f, 0.86f), new Color(0.55f, 0.42f, 0.72f), new Color(0.85f, 0.8f, 0.92f) };
        return c[rnd.Next(c.Length)];
    }

    // 曲がった筒（根っこ・小枝・毛）。半径は点ごと
    static void Tube(MB m, IList<Vector3> p, IList<float> r, int sides, Color c)
    {
        int n = p.Count; if (n < 2) return;
        var t0 = (p[1] - p[0]).normalized;
        var nrm = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        int bi = m.V.Count;
        for (int i = 0; i < n; i++)
        {
            var t = (p[Mathf.Min(i + 1, n - 1)] - p[Mathf.Max(i - 1, 0)]).normalized;
            nrm = (nrm - Vector3.Dot(nrm, t) * t).normalized; var b = Vector3.Cross(t, nrm);
            float shade = Mathf.Lerp(1f, 0.85f, i / (float)(n - 1));
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2 / sides; var d = Mathf.Cos(a) * nrm + Mathf.Sin(a) * b;
                m.Add(p[i] + d * r[i], d, c * (d.y < -0.5f ? 0.82f : 1f) * shade);
            }
        }
        for (int i = 0; i < n - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int s1 = (s + 1) % sides;
                m.Quad(bi + i * sides + s, bi + (i + 1) * sides + s, bi + (i + 1) * sides + s1, bi + i * sides + s1);
            }
        // 先をとじる
        int tip = m.Add(p[n - 1] + (p[n - 1] - p[n - 2]).normalized * r[n - 1], (p[n - 1] - p[n - 2]).normalized, c);
        for (int s = 0; s < sides; s++) m.Tri(bi + (n - 1) * sides + s, tip, bi + (n - 1) * sides + (s + 1) % sides);
    }

    // タマゴの殻（th0〜th1 の範囲。上が th=0）。th0>0 なら上のふちを、th1<π なら下のふちをギザギザにする
    static void Shell(MB m, Matrix4x4 xf, float r, float h, float th0, float th1, Color c, System.Random rnd)
    {
        const int seg = 12, rings = 8;
        bool cutTop = th0 > 0.01f, cutBot = th1 < Mathf.PI - 0.01f;
        int bi = m.V.Count;
        Vector3 P(float th, float ph)
        {
            float y = Mathf.Cos(th), s = Mathf.Sin(th);
            float rad = r * s * (y > 0 ? 1f - 0.16f * y : 1f);
            return new Vector3(Mathf.Cos(ph) * rad, y * (y > 0 ? h : h * 0.8f), Mathf.Sin(ph) * rad);
        }
        float t0 = cutTop ? th0 : 0.18f, t1 = cutBot ? th1 : Mathf.PI - 0.18f;
        for (int j = 0; j <= rings; j++)
        {
            for (int i = 0; i < seg; i++)
            {
                float th = Mathf.Lerp(t0, t1, j / (float)rings);
                if (j == 0 && cutTop) th += (i % 2 == 0 ? 0f : 0.28f) + (float)rnd.NextDouble() * 0.06f;
                if (j == rings && cutBot) th -= (i % 2 == 0 ? 0f : 0.28f) + (float)rnd.NextDouble() * 0.06f;
                float ph = i * Mathf.PI * 2 / seg;
                var p = P(th, ph);
                var n = new Vector3(p.x / (r * r), p.y / (h * h), p.z / (r * r)).normalized;
                m.Add(xf.MultiplyPoint3x4(p), xf.MultiplyVector(n), c);
            }
        }
        for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                int i1 = (i + 1) % seg;
                m.Quad(bi + j * seg + i, bi + j * seg + i1, bi + (j + 1) * seg + i1, bi + (j + 1) * seg + i);
            }
        if (!cutTop) { int tp = m.Add(xf.MultiplyPoint3x4(new Vector3(0, h, 0)), xf.MultiplyVector(Vector3.up), c); for (int i = 0; i < seg; i++) m.Tri(tp, bi + (i + 1) % seg, bi + i); }
        if (!cutBot) { int bp = m.Add(xf.MultiplyPoint3x4(new Vector3(0, -h * 0.8f, 0)), xf.MultiplyVector(Vector3.down), c); int last = bi + rings * seg; for (int i = 0; i < seg; i++) m.Tri(bp, last + i, last + (i + 1) % seg); }
    }

    // タマゴのしきわら：小枝を輪に寝かせて何段か重ね（外ほど高く）、底にも敷き、枯れ草を寝かせて混ぜる
    static void Bedding(MB m, Vector3 at, float ring, float er, System.Random rnd)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        Color[] cols = { new Color(0.55f, 0.4f, 0.27f), new Color(0.48f, 0.34f, 0.23f), new Color(0.66f, 0.52f, 0.33f) };
        void Stick(float a0, float span, float r, float y, float tr)
        {
            var pts = new List<Vector3>(); var rad = new List<float>();
            for (int k = 0; k <= 4; k++)
            {
                float a = a0 + span * k / 4f, rr = r * R(0.94f, 1.06f);
                pts.Add(at + new Vector3(Mathf.Cos(a) * rr, y + tr + R(-0.01f, 0.01f), Mathf.Sin(a) * rr));
                rad.Add(tr * Mathf.Lerp(1f, 0.55f, k / 4f));
            }
            Tube(m, pts, rad, 4, cols[rnd.Next(cols.Length)] * R(0.88f, 1.08f));
        }
        // 輪（外側ほど高く積む）
        for (int layer = 0; layer < 4; layer++)
        {
            float rr = ring * (0.92f + layer * 0.07f), y = er * (0.02f + layer * 0.17f);
            int n = Mathf.RoundToInt(rr * 2 * Mathf.PI / (er * 0.9f));
            for (int i = 0; i < n; i++) Stick((i + R(-0.3f, 0.3f)) * Mathf.PI * 2 / n + layer * 0.5f, Mathf.PI * 2 / n * R(1.4f, 2.0f), rr, y, er * R(0.11f, 0.15f));
        }
        // 底に敷いた小枝（タマゴの下からはみ出して見える）
        for (int i = 0; i < 10; i++) Stick(R(0, Mathf.PI * 2), R(0.5f, 1.0f), ring * R(0.3f, 0.8f), 0f, er * R(0.08f, 0.11f));
        // 寝かせた枯れ草
        var straw = new Color(0.82f, 0.7f, 0.42f);
        for (int i = 0; i < 16; i++)
        {
            float a = R(0, Mathf.PI * 2), r = ring * R(0.8f, 1.15f); var b = at + new Vector3(Mathf.Cos(a) * r, er * R(0.1f, 0.6f), Mathf.Sin(a) * r);
            var tang = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * (rnd.NextDouble() < 0.5 ? 1 : -1);
            var d = (tang + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * R(-0.2f, 0.4f) + Vector3.up * R(-0.1f, 0.15f)).normalized;
            float len = er * R(0.7f, 1.2f);
            Tube(m, new List<Vector3> { b, b + d * len * 0.5f, b + d * len + Vector3.down * er * 0.08f }, new List<float> { er * 0.035f, er * 0.03f, er * 0.012f }, 3, straw * R(0.85f, 1.1f));
        }
    }

    // 床に落ちた小枝（少し曲がった棒＋小さい枝分かれ）
    static void Twig(MB m, Vector3 at, Quaternion yaw, float len, float r, System.Random rnd)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        var dir = yaw * Vector3.forward; var side = yaw * Vector3.right;
        var pts = new List<Vector3>(); var rr = new List<float>();
        float bend = R(-0.12f, 0.12f);
        for (int i = 0; i <= 4; i++)
        {
            float u = i / 4f - 0.5f;
            pts.Add(at + dir * u * len + side * bend * len * (0.25f - u * u) * 2f + Vector3.up * r);
            rr.Add(r * Mathf.Lerp(1f, 0.55f, i / 4f));
        }
        Color[] cols = { new Color(0.55f, 0.4f, 0.27f), new Color(0.48f, 0.34f, 0.23f), new Color(0.62f, 0.46f, 0.32f) };
        var col = cols[rnd.Next(cols.Length)] * R(0.9f, 1.08f);
        Tube(m, pts, rr, 4, col);
        if (rnd.NextDouble() < 0.6)
        {
            var q = pts[rnd.Next(1, 3)]; var bd = (dir + side * (rnd.NextDouble() < 0.5 ? 1 : -1) * R(0.6f, 1.2f)).normalized;
            Tube(m, new List<Vector3> { q, q + bd * len * 0.2f, q + bd * len * 0.33f }, new List<float> { r * 0.6f, r * 0.45f, r * 0.3f }, 4, col);
        }
    }

    // 羽根（平たい。中心の軸を少し濃く、先を少し持ち上げる）
    static void Feather(MB m, Matrix4x4 xf, float L, float W, Color c)
    {
        const int k = 9; int bi = m.V.Count; var axis = c * 0.8f;
        for (int i = 0; i <= k; i++)
        {
            float u = i / (float)k;
            float w = W * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 1.05f)), 0.6f);
            float x = 0.12f * L * u * u, y = 0.004f + 0.025f * u * u, z = u * L;
            var nrm = xf.MultiplyVector(Vector3.up);
            m.Add(xf.MultiplyPoint3x4(new Vector3(x - w, y + 0.006f, z - w * 0.15f)), nrm, c);
            m.Add(xf.MultiplyPoint3x4(new Vector3(x, y + 0.008f, z)), nrm, axis);
            m.Add(xf.MultiplyPoint3x4(new Vector3(x + w * 0.8f, y + 0.006f, z - w * 0.15f)), nrm, c);
        }
        for (int i = 0; i < k; i++)
        {
            int a = bi + i * 3, b = bi + (i + 1) * 3;
            m.Quad(a, b, b + 1, a + 1); m.Quad(a + 1, b + 1, b + 2, a + 2);
        }
        // 根元の軸
        Tube(m, new List<Vector3> { xf.MultiplyPoint3x4(new Vector3(0, 0.006f, -L * 0.18f)), xf.MultiplyPoint3x4(new Vector3(0, 0.008f, 0.02f)) }, new List<float> { 0.003f, 0.004f }, 3, axis);
    }

    // 抜け毛のかたまり（細い毛をふんわり上向きに。まん中ほど立てて、外は寝かせる）
    static void Tuft(MB m, Vector3 at, Color fur, System.Random rnd)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        int n = rnd.Next(22, 30); float size = R(0.7f, 1.1f);
        for (int i = 0; i < n; i++)
        {
            float lean = R(0f, 1f);
            var d = Quaternion.Euler(-Mathf.Lerp(80f, 25f, lean), R(0, 360), 0) * Vector3.forward;
            float len = R(0.08f, 0.15f) * size; var c = Color.Lerp(fur, new Color(0.56f, 0.4f, 0.86f), 0.6f) * R(0.9f, 1.15f);
            var b = at + new Vector3(R(-0.03f, 0.03f), 0.01f, R(-0.03f, 0.03f));
            Tube(m, new List<Vector3> { b, b + d * len * 0.5f, b + (d + Vector3.down * 0.35f).normalized * len }, new List<float> { 0.016f * size, 0.011f * size, 0.003f }, 3, c);
        }
    }
}
