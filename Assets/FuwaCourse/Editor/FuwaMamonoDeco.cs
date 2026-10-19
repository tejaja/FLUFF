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

    [MenuItem("FuwaCourse/済み（押す前に相談）/まもののすの飾りを作る")]
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
        // スタートの島（ワープの島・木の橋）も（2026-10-09 最初の橋をのばした時に、浮いてる巣がスタートの島にかぶった）
        var selfRs = den.GetComponentsInChildren<MeshRenderer>().ToList();
        var startT = root.Find("Start"); if (startT != null) selfRs.AddRange(startT.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.GetComponent<MeshFilter>() != null && r.GetComponent<MeshFilter>().sharedMesh != null && r.GetComponent<MeshFilter>().sharedMesh.isReadable));
        foreach (var r in selfRs)
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
            var simp = (Mesh)typeof(FuwaFarViewSetup).GetMethod("Simplify", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { tmp, Vector3.one * 0.22f });
            // 間引くと向きが裏返る三角が出て黒ずんで見えるので、頂点の法線と逆向きの三角は裏返す。つぶれた細い三角は捨てる
            var sv = simp.vertices; var sn = simp.normals; var st = simp.triangles; var ft = new List<int>();
            for (int i = 0; i < st.Length; i += 3)
            {
                var a0 = sv[st[i]]; var a1 = sv[st[i + 1]]; var a2 = sv[st[i + 2]];
                var fn = Vector3.Cross(a1 - a0, a2 - a0); float area = fn.magnitude;
                float e = Mathf.Max((a1 - a0).sqrMagnitude, Mathf.Max((a2 - a1).sqrMagnitude, (a0 - a2).sqrMagnitude));
                if (area < 1e-6f || area * area < e * e * 0.0004f) continue;   // 針みたいに細い三角
                var vn = sn[st[i]] + sn[st[i + 1]] + sn[st[i + 2]];
                if (Vector3.Dot(fn, vn) < 0) { ft.Add(st[i]); ft.Add(st[i + 2]); ft.Add(st[i + 1]); } else { ft.Add(st[i]); ft.Add(st[i + 1]); ft.Add(st[i + 2]); }
            }
            pods.Add((sv, sn, simp.colors, ft.ToArray(), rad, floor - b.min.y));
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
                        Shell(eggs, Matrix4x4.TRS(pos + outDir * (d + er * 2.6f) + Vector3.up * er * 1.3f * 0.92f, Quaternion.Euler(R(150, 200), R(0, 360), R(-20, 20)), Vector3.one), er, er * 1.3f, 0f, R(0.8f, 1.0f), col, rnd);
                }
            }
        }
        log.Append("nests " + placed.Count + " ");

        var eggSpots = new List<Vector3>();
        // ---- 大回りの途中の何もない小島（Islet）：割れていないタマゴのセットを端っこにいくつか（2026-10-07 てじゃ）----
        {
            var islet = den.Find("Islet");
            if (islet != null)
            {
                var ir = new System.Random(77);
                float IR(float a, float b) { return a + (float)ir.NextDouble() * (b - a); }
                var ib = islet.GetComponent<Renderer>().bounds;
                // 床とふち（高い所の頂点）から中心と外周の半径を出す（下に垂れた根っこで bounds は広がっているので使わない）
                var topV = islet.GetComponent<MeshFilter>().sharedMesh.vertices.Select(x => islet.TransformPoint(x)).Where(x => x.y > 5.4f).ToList();
                var ic = topV.Aggregate(Vector3.zero, (a2, b2) => a2 + b2) / topV.Count; ic.y = 0f;
                // 床の半径は当たり判定の床（高さ5.5の円）から（見た目はふちの小枝が外へはみ出している）
                var colT = den.Find("Islet_Col");
                var floorV = colT.GetComponent<MeshCollider>().sharedMesh.vertices.Select(x => colT.TransformPoint(x)).Where(x => Mathf.Abs(x.y - 5.5f) < 0.03f).ToList();
                if (floorV.Count > 0) { ic = floorV.Aggregate(Vector3.zero, (a2, b2) => a2 + b2) / floorV.Count; ic.y = 0f; }
                float outerR = floorV.Count > 0 ? floorV.Max(x => new Vector2(x.x - ic.x, x.z - ic.z).magnitude) : 1.8f;
                // 見た目の床の高さを調べるための一時的な当たり判定
                var tgo = new GameObject("_isletFloor"); tgo.hideFlags = HideFlags.HideAndDontSave; tgo.layer = 31;
                tgo.transform.SetPositionAndRotation(islet.position, islet.rotation);
                tgo.AddComponent<MeshCollider>().sharedMesh = islet.GetComponent<MeshFilter>().sharedMesh;
                Physics.SyncTransforms();
                // 橋の向き（そこには置かない）
                var bdirs = new List<Vector3>();
                foreach (Transform b in den)
                {
                    if (!b.name.StartsWith("Bridge") || b.name.EndsWith("_Col")) continue;
                    var bm = b.GetComponent<MeshFilter>().sharedMesh; var bv = bm.vertices;
                    // 橋の頂点で小島に一番近い所の向き
                    Vector3 best = Vector3.zero; float bd = 1e9f;
                    for (int i = 0; i < bv.Length; i += 5) { var w = b.TransformPoint(bv[i]); float d = new Vector2(w.x - ic.x, w.z - ic.z).magnitude; if (d < bd) { bd = d; best = w; } }
                    if (bd < outerR + 0.6f) bdirs.Add(new Vector3(best.x - ic.x, 0, best.z - ic.z).normalized);
                }
                var angs = new List<float>();
                for (int k = 0; k < 36 && angs.Count < 3; k++)
                {
                    float a = (k * 137.5f + 20f) % 360f;
                    var d = Quaternion.Euler(0, a, 0) * Vector3.forward;
                    if (bdirs.Any(b => Vector3.Angle(b, d) < 45f)) continue;
                    if (angs.Any(o => Mathf.Abs(Mathf.DeltaAngle(o, a)) < 60f)) continue;
                    angs.Add(a);
                }
                float floorR = outerR - 0.35f - 0.1f;   // ふちの内側
                foreach (var a in angs)
                {
                    float er = IR(0.15f, 0.19f);
                    var dirA = Quaternion.Euler(0, a, 0) * Vector3.forward;
                    // ふちの上に乗らない所まで内側へ寄せる
                    Vector3 pos = ic; RaycastHit fh = default; bool ok = false;
                    for (float rr = outerR - er * 1.2f; rr > 0.4f && !ok; rr -= 0.04f)
                    {
                        pos = ic + dirA * rr;
                        ok = true;
                        for (int q = 0; q < 6 && ok; q++)
                        {
                            var probe = pos + Quaternion.Euler(0, q * 60, 0) * Vector3.forward * (er * 2.2f);   // しきわらの外側は少しふちの下にもぐってもいい（壁ぎわに寄せる）
                            if (!Physics.Raycast(new Vector3(probe.x, 9f, probe.z), Vector3.down, out var ph, 6f, 1 << 31) || ph.point.y > 5.6f) ok = false;   // タマゴ（外側まで）が平らな床に乗ること。しきわらは少しもぐってもいい
                        }
                        if (ok) ok = Physics.Raycast(new Vector3(pos.x, 9f, pos.z), Vector3.down, out fh, 6f, 1 << 31);
                    }
                    if (!ok) continue;
                    pos.y = fh.point.y;
                    eggSpots.Add(pos);
                    var col = EggColor(ir); float d = er * 1.18f, a0 = IR(0, 360);
                    Bedding(eggs, pos, d + er * 0.95f, er, ir);
                    for (int k = 0; k < 3; k++)
                    {
                        var outDir = Quaternion.Euler(0, a0 + k * 120 + IR(-10, 10), 0) * Vector3.forward;
                        var tilt = Quaternion.AngleAxis(IR(8, 16), Vector3.Cross(Vector3.up, outDir)) * Quaternion.Euler(0, IR(0, 360), 0);
                        Shell(eggs, Matrix4x4.TRS(pos + outDir * d + Vector3.up * er * 0.85f, tilt, Vector3.one), er, er * 1.3f, 0f, Mathf.PI, col, ir);
                    }
                }
                Object.DestroyImmediate(tgo);
                log.Append("isletEggs " + angs.Count + " ");
            }
        }

        // ---- D：巣の裏から垂れる根っこ・ツタ ----
        var underSrc = new List<(Vector3 p, Vector3 n)>();
        foreach (var r in den.GetComponentsInChildren<MeshRenderer>())
        {
            if (r.name.StartsWith("Bridge") || r.name.StartsWith("Course4_Road")) continue;   // 橋・シマシマ道（とつなぎの小枝）からは垂らさない
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

        // 見た目の床の高さ（当たり判定の床より少し高い所がある）を調べるため、巣の見た目に一時的な当たり判定を付ける（2026-10-07 殻が床に埋まっていた）
        var visTemps = new List<GameObject>();
        foreach (var r in den.GetComponentsInChildren<MeshRenderer>())
        {
            var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            if (r.name.StartsWith("Course4_Road")) continue;   // シマシマ道・つなぎの小枝は床の小物の置き場にしない
            var tgo = new GameObject("_visFloor"); tgo.hideFlags = HideFlags.HideAndDontSave; tgo.layer = 31;
            tgo.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation); tgo.transform.localScale = r.transform.lossyScale;
            tgo.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            visTemps.Add(tgo);
        }
        Physics.SyncTransforms();
        Vector3 VisFloor(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var vh, 3f, 1 << 31, QueryTriggerInteraction.Ignore)) return new Vector3(p.x, vh.point.y, p.z);
            return p;
        }

        // ---- C：巣の床の小物 ----
        // 床の小物はシマシマ道にした所（もとの橋A・D）には置かない（2026-10-09 道にしてから、つなぎの丸太の上に枝が乗っていた）
        var floorCols = den.GetComponentsInChildren<Collider>().Where(c => c.name != "BridgeA_Col" && c.name != "BridgeD_Col" && !c.name.StartsWith("Course4_Road")).Concat(path.GetComponents<Collider>()).ToList();
        var spikes = root.Find("Spikes") != null ? root.Find("Spikes").Cast<Transform>().Select(t => t.position).ToList() : new List<Vector3>();
        var pb = path.GetComponent<Renderer>().bounds;
        Color fur = new Color(0.45f, 0.3f, 0.65f);
        var furMat = AssetDatabase.FindAssets("Fuwa_EnemyFur t:Material").Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        if (furMat != null && furMat.HasProperty("_Color")) fur = furMat.color;
        int nTwig = 0, nFeather = 0, nShell = 0, nTuft = 0; tries = 0;
        var floorSpots = new List<(Vector3 p, float r)>();   // 床の小物の場所（抜け毛と重ならないように）
        foreach (var e in eggSpots) floorSpots.Add((e, 0.75f));   // 小島のタマゴのセット
        foreach (var e in PocketEggSpots(den)) floorSpots.Add((e, 0.75f));   // ママの巣のポケットのタマゴ（PocketEggs）
        // チェックポイントの光るキノコ（丸太ごと床に置いてある）の下・まわりには置かない（2026-10-09 枝が丸太に埋まっていた）
        foreach (Transform cp in root)
        {
            var gm = cp.name.StartsWith("Checkpoint") ? cp.Find("GlowMushrooms") : null;
            if (gm == null) continue;
            foreach (Transform cl in gm)
            {
                if (!cl.gameObject.activeInHierarchy) continue;
                var rs = cl.GetComponentsInChildren<MeshRenderer>(); if (rs.Length == 0) continue;   // 胞子のパーティクルは範囲が原点まで広がるので数えない
                var cb = rs[0].bounds; foreach (var r in rs) cb.Encapsulate(r.bounds);
                floorSpots.Add((new Vector3(cb.center.x, 0f, cb.center.z), Mathf.Max(cb.extents.x, cb.extents.z) + 0.45f));
            }
        }
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
            var vf = VisFloor(hit.point);
            if (vf.y - hit.point.y > 0.12f) continue;
            if (floorSpots.Any(f => new Vector2(f.p.x - vf.x, f.p.z - vf.z).magnitude < f.r + 0.3f)) continue;   // ほかの小物・タマゴと重ねない   // 見た目のふち（当たり判定は低く削ってある）の上には置かない
            var yaw = Quaternion.Euler(0, R(0, 360), 0); var fp = vf + Vector3.up * 0.005f;
            int kind = rnd.Next(4);
            if (kind == 0 && nTwig < quota[0])
            {
                // 小枝は1〜3本の小さな山にする
                int pile = rnd.Next(1, 4);
                for (int k = 0; k < pile; k++)
                {
                    var at = fp + Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward * R(0, 0.15f) + Vector3.up * k * 0.02f;
                    var yawQ = Quaternion.Euler(0, R(0, 360), 0); float len = R(0.4f, 0.9f), rad = R(0.02f, 0.034f);
                    // 枝の両はしの下にも床があること（ふちから空中へはみ出さない）。だめなら短くして、それでもだめなら置かない
                    bool Ends(float L) { var d2 = yawQ * Vector3.forward * (L * 0.5f); foreach (var e in new[] { at + d2, at - d2 }) { if (!Physics.Raycast(e + Vector3.up * 1.5f, Vector3.down, out var eh, 3f, 1 << 31, QueryTriggerInteraction.Ignore) || Mathf.Abs(eh.point.y - fp.y) > 0.06f) return false; } return true; }
                    if (!Ends(len)) { len *= 0.55f; if (!Ends(len)) continue; }
                    Twig(floorMb, at, yawQ, len, rad, rnd);
                }
                nTwig++; floorSpots.Add((fp, 0.5f));
            }
            else if (kind == 1 && nFeather < quota[1]) { Feather(floorMb, Matrix4x4.TRS(fp, yaw, Vector3.one), R(0.26f, 0.42f), R(0.055f, 0.085f), FeatherColor(rnd)); nFeather++; }
            else if (kind == 2 && nShell < quota[2])
            {
                float er = R(0.11f, 0.17f); var col = EggColor(rnd);
                bool edgeOk = true; for (int q = 0; q < 6 && edgeOk; q++) { var e = fp + Quaternion.Euler(0, q * 60, 0) * Vector3.forward * (er * 3.4f); if (!Physics.Raycast(e + Vector3.up * 1.5f, Vector3.down, out var eh, 3f, 1 << 31, QueryTriggerInteraction.Ignore) || Mathf.Abs(eh.point.y - fp.y) > 0.06f) edgeOk = false; }
                if (!edgeOk) continue;   // 殻（と横に転がるかけら）の下にも床があること
                // 床の殻は1つおきだけ出す（2026-10-09 てじゃ「かけらちょっと多い」）。出さない分も乱数は同じだけ使って、ほかの小物の配置は変えない
                var shellMb = nShell % 2 == 0 ? floorMb : new MB();
                Shell(shellMb, Matrix4x4.TRS(fp + Vector3.up * (er * 1.3f + 0.012f),   /* ひっくり返したカラはふくらみが下なので、高さ分だけ上げて床に乗せる（前は大部分が床に埋まっていた） */ Quaternion.Euler(R(150, 210), R(0, 360), R(-25, 25)), Vector3.one), er, er * 1.3f, 0f, R(0.9f, 1.3f), col, rnd);
                if (rnd.NextDouble() < 0.5) Shell(shellMb, Matrix4x4.TRS(fp + yaw * Vector3.forward * er * 2.4f + Vector3.up * (er * 1.3f * 0.8f + 0.012f), Quaternion.Euler(R(160, 200), R(0, 360), 0), Vector3.one * 0.8f), er, er * 1.3f, 0f, R(0.6f, 0.9f), col, rnd);
                nShell++; floorSpots.Add((fp, er * 3.6f));
            }
            else if (kind == 3 && nTuft < quota[3]) { Tuft(floorMb, fp, fur, rnd); nTuft++; }
        }
        // 抜け毛（綿ぼこり）：敵（とママ）が潜んでいる所の近くに数個だけ落としておく（「ここから出てくる」の目印）
        var lurkers = new List<(Vector3 p, int n, float r0, float r1, float size)>();
        var en = root.Find("Enemies"); if (en != null) foreach (Transform e in en) lurkers.Add((e.position, rnd.Next(3, 5), 0.25f, 0.6f, 1.5f));
        var mama = root.Find("Mama"); if (mama != null) lurkers.Add((mama.position, 5, 1.4f, 2.2f, 2f));
        var dustMb = new MB();
        foreach (var (lp, n, r0, r1, size) in lurkers)
            for (int k = 0, t2 = 0; k < n && t2 < 80; t2++)
            {
                float ang = R(0, 360);
                var o = lp + Quaternion.Euler(0, ang, 0) * Vector3.forward * R(r0, r1) + Vector3.up * 3f;
                if (!Physics.Raycast(o, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (!floorCols.Contains(hit.collider) || hit.normal.y < 0.85f) { if (t2 % 8 == 7) k++; continue; }
                var dp = VisFloor(hit.point);
                if (dp.y - hit.point.y > 0.12f || floorSpots.Any(f => new Vector2(f.p.x - dp.x, f.p.z - dp.z).magnitude < f.r + 0.3f)) { if (t2 % 8 == 7) k++; continue; }   // ふちの上・床の小物（殻・小枝）の上は避ける
                DustBunny(dustMb, dp, rnd, size); nTuft++; k++;
            }
        foreach (var tgo in visTemps) Object.DestroyImmediate(tgo);
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
            // 巣とタマゴは影を受けない：焼かない物なので、VRChatではリアルタイムの影の届く所だけ縁が自分の影で暗くなり、
            // 少し離れると急に明るくなって見え方が切り替わっていた（2026-10-11 てじゃ「外周の巣、近距離で切り替わる」）
            if (name == "Mamono_Nests" || name == "Mamono_Eggs") mr.receiveShadows = false;
            log.Append(name + " tris " + mb.T.Count / 3 + " ");
        }
        // 綿ぼこり：敵と同じシェルファーの別メッシュ（毛の長さ・細かさはメートルで）
        {
            var dm = new Mesh { name = "Mamono_Dust" };
            dm.SetVertices(dustMb.V.Select(x => w2l.MultiplyPoint3x4(x)).ToList()); dm.SetNormals(dustMb.N.Select(x => w2l.MultiplyVector(x).normalized).ToList());
            dm.SetTriangles(dustMb.T, 0); dm.RecalculateBounds();
            string ap = Dir + "/Mamono_Dust.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
            if (old != null) { old.Clear(); old.SetVertices(dm.vertices); old.SetNormals(dm.normals); old.SetTriangles(dm.triangles, 0); old.RecalculateBounds(); dm = old; EditorUtility.SetDirty(old); } else AssetDatabase.CreateAsset(dm, ap);
            string mp = "Assets/FuwaCourse/Materials/MamonoDust.mat"; var dmat = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (dmat == null)
            {
                dmat = new Material(Shader.Find("FuwaCourse/Fur")); AssetDatabase.CreateAsset(dmat, mp);
                dmat.SetColor("_Color", Color.Lerp(fur, new Color(0.8f, 0.72f, 0.92f), 0.45f)); dmat.SetColor("_TipColor", new Color(0.9f, 0.85f, 0.98f));
                dmat.SetFloat("_FurLength", 0.035f); dmat.SetFloat("_Density", 200f); dmat.SetFloat("_Thinness", 0.75f);
                dmat.SetFloat("_Shadow", 0.35f); dmat.SetFloat("_Gravity", 0.01f); dmat.SetFloat("_RimStrength", 0.3f);
                EditorUtility.SetDirty(dmat);
            }
            var t = deco.Find("Mamono_Dust"); GameObject go = t != null ? t.gameObject : null;
            if (go == null) { go = new GameObject("Mamono_Dust", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(deco, false); Undo.RegisterCreatedObjectUndo(go, "deco"); }
            go.GetComponent<MeshFilter>().sharedMesh = dm; var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = dmat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            log.Append("Mamono_Dust tris " + dustMb.T.Count / 3 + " ");
        }
        log.Append(PocketEggs());
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return log.ToString();
    }

    // ママの巣のポケット（Tools/den.py の MamaNest の bulges）の壁ぎわにタマゴのセット（2026-10-19 てじゃ「壁際には卵（飾り）」）。
    // 全体の飾り（Run）は作り直すと浮かぶ巣の並びが変わるので、これだけ別のメッシュ（Deco/Mamono_PocketEggs）に
    public static readonly (float ang, float half, float depth)[] MamaBulges = { (5.4f, 13.5f, 1.5f), (77f, 13.5f, 1.5f) };   // den.py と同じ
    const float MamaR = 4.6f; static readonly Vector2 MamaC = new Vector2(73.9f, 21.7f);
    static float MamaNestR(float aDeg)
    {
        float r = MamaR;
        foreach (var b in MamaBulges) { float d = Mathf.DeltaAngle(b.ang, aDeg); if (Mathf.Abs(d) < b.half) { float u = d / b.half; r += b.depth * Mathf.Pow(Mathf.Max(0f, 1f - u * u), 0.75f); } }
        return r;
    }

    // ポケットの奥の左右の角（壁ぎわ）に1セットずつ。ボスが入る真ん中はあける。乱数を使わないので、全体の飾り（Run）からも同じ場所がわかる
    static List<Vector3> PocketEggSpots(Transform den)
    {
        var l = new List<Vector3>();
        foreach (var b in MamaBulges)
            foreach (float side in new[] { -0.62f, 0.62f })
            {
                float a = b.ang + side * b.half;
                float rr = MamaNestR(a) - 0.15f * 2.6f - 0.12f;   // ふちの内側の面から少しはなす
                l.Add(den.TransformPoint(new Vector3(MamaC.x + Mathf.Cos(a * Mathf.Deg2Rad) * rr, 5.5f, MamaC.y + Mathf.Sin(a * Mathf.Deg2Rad) * rr)));
            }
        return l;
    }

    [MenuItem("FuwaCourse/済み（押す前に相談）/ママの巣のポケットのタマゴを作る")]
    public static void PocketEggsMenu() { Debug.Log(PocketEggs()); }

    public static string PocketEggs()
    {
        var root = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == Course).transform;
        var den = root.Find("Den");
        var twig = AssetDatabase.FindAssets("Den_Twig t:Material").Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).First(m => m.name == "Den_Twig");
        var rnd = new System.Random(1019);
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        var eggs = new MB(); int sets = 0;
        var spots = PocketEggSpots(den);
        foreach (var pos in spots)
        {
            {
                float er = R(0.13f, 0.17f);
                var col = EggColor(rnd); float d = er * 1.18f, a0 = R(0, 360);
                Bedding(eggs, pos, d + er * 0.95f, er, rnd);
                int crackedOne = rnd.NextDouble() < 0.4 ? rnd.Next(3) : -1;
                for (int k = 0; k < 3; k++)
                {
                    var outDir = Quaternion.Euler(0, a0 + k * 120 + R(-10, 10), 0) * Vector3.forward;
                    var tilt = Quaternion.AngleAxis(R(8, 16), Vector3.Cross(Vector3.up, outDir)) * Quaternion.Euler(0, R(0, 360), 0);
                    bool cracked = k == crackedOne;
                    Shell(eggs, Matrix4x4.TRS(pos + outDir * d + Vector3.up * er * 0.85f, tilt, Vector3.one), er, er * 1.3f, cracked ? R(0.9f, 1.2f) : 0f, Mathf.PI, col, rnd);
                }
                sets++;
            }
        }
        var deco = root.Find("Deco");
        var w2l = deco.worldToLocalMatrix;
        var m = new Mesh { name = "Mamono_PocketEggs", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        m.SetVertices(eggs.V.Select(x => w2l.MultiplyPoint3x4(x)).ToList()); m.SetNormals(eggs.N.Select(x => w2l.MultiplyVector(x).normalized).ToList());
        m.SetColors(eggs.C); m.SetTriangles(eggs.T, 0); m.RecalculateBounds();
        string ap = Dir + "/Mamono_PocketEggs.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
        if (old != null) { old.Clear(false); EditorUtility.CopySerialized(m, old); m = old; } else AssetDatabase.CreateAsset(m, ap);
        var t = deco.Find("Mamono_PocketEggs"); GameObject go = t != null ? t.gameObject : null;
        if (go == null) { go = new GameObject("Mamono_PocketEggs", typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(deco, false); Undo.RegisterCreatedObjectUndo(go, "deco"); }
        go.GetComponent<MeshFilter>().sharedMesh = m;
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = twig;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; mr.receiveShadows = false;   // Mamono_Eggs と同じ
        var eggsT = deco.Find("Mamono_Eggs");
        if (eggsT != null) { GameObjectUtility.SetStaticEditorFlags(go, GameObjectUtility.GetStaticEditorFlags(eggsT.gameObject)); go.layer = eggsT.gameObject.layer; }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "pocket eggs " + sets + " sets, tris " + eggs.T.Count / 3;
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
        const int seg = 10, rings = 6;
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
            for (int k = 0; k <= 2; k++)
            {
                float a = a0 + span * k / 2f, rr = r * R(0.94f, 1.06f);
                pts.Add(at + new Vector3(Mathf.Cos(a) * rr, y + tr + R(-0.01f, 0.01f), Mathf.Sin(a) * rr));
                rad.Add(tr * Mathf.Lerp(1f, 0.55f, k / 2f));
            }
            Tube(m, pts, rad, 4, cols[rnd.Next(cols.Length)] * R(0.88f, 1.08f));
        }
        // 輪（外側ほど高く積む）
        for (int layer = 0; layer < 3; layer++)
        {
            float rr = ring * (0.92f + layer * 0.07f), y = er * (0.02f + layer * 0.24f);
            int n = Mathf.RoundToInt(rr * 2 * Mathf.PI / (er * 1.25f));
            for (int i = 0; i < n; i++) Stick((i + R(-0.3f, 0.3f)) * Mathf.PI * 2 / n + layer * 0.5f, Mathf.PI * 2 / n * R(1.4f, 2.0f), rr, y, er * R(0.14f, 0.19f));
        }
        // 底に敷いた小枝（タマゴの下からはみ出して見える）
        for (int i = 0; i < 5; i++) Stick(R(0, Mathf.PI * 2), R(0.5f, 1.0f), ring * R(0.3f, 0.8f), 0f, er * R(0.08f, 0.11f));
        // 寝かせた枯れ草
        var straw = new Color(0.82f, 0.7f, 0.42f);
        for (int i = 0; i < 8; i++)
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

    // 綿ぼこり：ふわふわと同じシェルファー（FuwaCourse/Fur）で描く小さな毛玉（芯はつぶれたでこぼこの球）。1〜2個
    static void DustBunny(MB m, Vector3 at, System.Random rnd, float scale = 1f)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        int balls = rnd.NextDouble() < 0.25 ? 2 : 1;
        for (int bI = 0; bI < balls; bI++)
        {
            float r = (bI == 0 ? R(0.035f, 0.05f) : R(0.02f, 0.03f)) * scale;
            var ctr = at + (bI == 0 ? Vector3.zero : Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward * R(0.1f, 0.22f) * scale) + Vector3.up * r * 0.45f;
            const int seg = 8, rings = 5; int bi = m.V.Count;
            float ph0 = R(0, 6.28f), wob = R(0, 10);
            for (int j = 0; j <= rings; j++)
            {
                float th = Mathf.PI * j / rings;
                for (int i = 0; i < seg; i++)
                {
                    float ph = ph0 + Mathf.PI * 2 * i / seg;
                    var dir = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    float rr = r * (1f + 0.2f * Mathf.Sin(ph * 3 + wob + j * 1.3f) * Mathf.Sin(th));
                    m.Add(ctr + new Vector3(dir.x * rr, dir.y * rr * 0.6f, dir.z * rr), dir, Color.white);
                }
            }
            for (int j = 0; j < rings; j++) for (int i = 0; i < seg; i++)
            {
                int a = bi + j * seg + i, b = bi + j * seg + (i + 1) % seg, cc = bi + (j + 1) * seg + (i + 1) % seg, d = bi + (j + 1) * seg + i;
                m.Quad(a, b, cc, d);
            }
        }
    }

    // 抜け毛のかたまり（細い毛をふんわり上向きに。まん中ほど立てて、外は寝かせる）
    static void Tuft(MB m, Vector3 at, Color fur, System.Random rnd, float scale = 1f)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        // 細い毛がパラパラ散っている感じ：まん中に小さく固まった数本＋まわりに寝た毛を散らす
        int n = rnd.Next(26, 34); float size = R(0.7f, 1.1f) * scale;
        for (int i = 0; i < n; i++)
        {
            bool loose = i >= 8;   // 8本目以降はまわりに散らばった抜け毛
            float lean = loose ? R(0.75f, 1f) : R(0f, 0.7f);
            var d = Quaternion.Euler(-Mathf.Lerp(70f, 6f, lean), R(0, 360), 0) * Vector3.forward;
            float len = (loose ? R(0.1f, 0.2f) : R(0.08f, 0.14f)) * size; var c = Color.Lerp(fur, new Color(0.56f, 0.4f, 0.86f), 0.6f) * R(0.9f, 1.15f);
            float spread = loose ? R(0.04f, 0.2f) * size : R(0f, 0.025f);
            var b = at + Quaternion.Euler(0, R(0, 360), 0) * Vector3.forward * spread + Vector3.up * 0.006f;
            // ゆるく曲がった細い毛（3点）
            var side = Vector3.Cross(Vector3.up, d).normalized * R(-0.25f, 0.25f);
            Tube(m, new List<Vector3> { b, b + (d + side) .normalized * len * 0.5f, b + (d - side * 0.5f + Vector3.down * (loose ? 0.05f : 0.3f)).normalized * len }, new List<float> { 0.0065f * size, 0.0045f * size, 0.0015f }, 3, c);
        }
    }
}
