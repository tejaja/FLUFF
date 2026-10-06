using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// エアガンの台の切り株（GunStand_Stump）を、元のメッシュ（GunStand_Stump_Source）から作り直す。何度実行しても同じ結果。
// ・切り口のふちに、樹皮が少し盛り上がった「ふち」を一周つける（切り口の円盤と樹皮の間のすき間・ギザギザを隠す）
// ・苔は元のもの（板の塊が重なっていてZファイト・飛び出しがあった）を全部消して、表面にぴったり沿うクッション状の苔を生やし直す。
//   苔どうしは重ねない、キノコは避ける、ふちをまたぐ苔は角に沿って曲げる
// サブメッシュの並び：0 樹皮 / 1 切り口 / 2 年輪 / 3 苔 / 4〜6 キノコ（元と同じ。材質の並びもそのまま）
public static class FuwaStumpRebuild
{
    const string SrcPath = "Assets/FuwaCourse/Meshes/GunStand/GunStand_Stump_Source.asset";
    const string DstPath = "Assets/FuwaCourse/Meshes/GunStand_Stump.asset";
    const float CutY = 0.332f, RingY = 0.334f;

    [MenuItem("FuwaCourse/エアガンの台の切り株を作り直す")]
    public static void RunMenu() { Debug.Log(Run()); }

    class MB
    {
        public List<Vector3> V = new List<Vector3>(); public List<Vector3> N = new List<Vector3>(); public List<Vector2> U = new List<Vector2>();
        public List<int>[] S;
        public MB(int subs) { S = new List<int>[subs]; for (int i = 0; i < subs; i++) S[i] = new List<int>(); }
    }

    public static string Run()
    {
        var src = AssetDatabase.LoadAssetAtPath<Mesh>(SrcPath);
        var sv = src.vertices; var sn = src.normals; var su = new List<Vector2>(); src.GetUVs(1, su);
        if (su.Count != sv.Length) su = Enumerable.Repeat(Vector2.zero, sv.Length).ToList();
        int subs = src.subMeshCount;
        var mb = new MB(subs);
        var log = new System.Text.StringBuilder();

        // 苔以外をそのまま写す（使っている頂点だけ）
        for (int s = 0; s < subs; s++)
        {
            if (s == 3) continue;
            var map = new Dictionary<int, int>();
            foreach (var i in src.GetTriangles(s))
            {
                if (!map.TryGetValue(i, out int j)) { j = mb.V.Count; map[i] = j; mb.V.Add(sv[i]); mb.N.Add(sn[i]); mb.U.Add(su[i]); }
                mb.S[s].Add(j);
            }
        }

        // 切り口の半径（角度ごと。なめらかに）
        int B = 180; var raw = new float[B];
        foreach (var i in src.GetTriangles(1).Distinct()) { var p = sv[i]; int b = Bin(p, B); raw[b] = Mathf.Max(raw[b], new Vector2(p.x, p.z).magnitude); }
        var mx = new float[B]; for (int b = 0; b < B; b++) { float m = 0; for (int k = -3; k <= 3; k++) m = Mathf.Max(m, raw[(b + k + B) % B]); mx[b] = m; }
        var rc = new float[B]; for (int b = 0; b < B; b++) { float a = 0; for (int k = -3; k <= 3; k++) a += mx[(b + k + B) % B]; rc[b] = a / 7f; }
        float RC(float ang) { float f = Mathf.Repeat((ang + Mathf.PI) / (Mathf.PI * 2) * B - 0.5f, B); int b0 = Mathf.FloorToInt(f) % B; return Mathf.Lerp(rc[b0], rc[(b0 + 1) % B], f - Mathf.Floor(f)); }

        // 苔を表面に落とすための一時メッシュ（樹皮＋切り口）。見た目のふち（波打つ）は入れずに、切り口と樹皮のすき間を
        // なめらかな帯でふさぎ、盛り上がったふちを少し外側から包む形にする（ふちの波打ちで苔がしわしわにならず、ふちに沿ってかぶさる）
        var colMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        var cv = new List<Vector3>(mb.V); var ct = mb.S[0].Concat(mb.S[1]).ToList();
        {
            int S = 180, cb = cv.Count;
            var band = new[] { new Vector2(-0.012f, CutY + 0.0005f), new Vector2(-0.0085f, CutY + 0.0008f), new Vector2(-0.006f, RingY + 0.0045f), new Vector2(-0.001f, RingY + 0.006f),
                               new Vector2(0.0055f, RingY + 0.003f), new Vector2(0.0068f, 0.322f), new Vector2(0.002f, 0.312f), new Vector2(0.0015f, 0.30f) };   // 見た目のふち（波打ち込み）を少し外側から包む形
            for (int s = 0; s <= S; s++) { float a = s / (float)S * Mathf.PI * 2 - Mathf.PI; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); foreach (var q in band) cv.Add(d * (RC(a) + q.x) + Vector3.up * q.y); }
            for (int s = 0; s < S; s++) for (int k = 0; k < band.Length - 1; k++) { int a0 = cb + s * band.Length + k, b0 = a0 + band.Length; ct.AddRange(new[] { a0, b0, a0 + 1, a0 + 1, b0, b0 + 1, a0, a0 + 1, b0, a0 + 1, b0 + 1, b0 }); }   // 両面
        }
        colMesh.SetVertices(cv); colMesh.SetTriangles(ct, 0); colMesh.RecalculateBounds();
        var colGo = new GameObject("TmpStumpCol"); colGo.hideFlags = HideFlags.HideAndDontSave;
        colGo.transform.position = new Vector3(0, -5000, 0);
        var mc = colGo.AddComponent<MeshCollider>(); mc.sharedMesh = colMesh;
        Physics.SyncTransforms();
        var off = colGo.transform.position;
        var mush = new List<Vector3>(); for (int s = 4; s < subs; s++) foreach (var i in src.GetTriangles(s).Distinct()) mush.Add(sv[i]);

        // ---- 切り口のふち（樹皮の材質）----
        var barkSet = new HashSet<int>(mb.S[0]);
        var barkTopIdx = Enumerable.Range(0, mb.V.Count).Where(i => mb.V[i].y > 0.25f && barkSet.Contains(i)).ToList();
        {
            var prof = new[] { new Vector2(-0.008f, CutY - 0.001f), new Vector2(-0.0055f, RingY + 0.0018f), new Vector2(-0.001f, RingY + 0.0032f),
                               new Vector2(0.0035f, RingY + 0.0008f), new Vector2(0.0045f, 0.322f), new Vector2(-0.004f, 0.315f) };
            int S = 180, P = prof.Length;
            var lv = new List<Vector3>(); var lt = new List<int>();
            for (int s = 0; s <= S; s++)
            {
                float a = s / (float)S * Mathf.PI * 2 - Mathf.PI; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); float r = RC(a);
                // ふちは少しだけ波打たせる（機械的な輪にならないように）
                float wob = 0.0012f * Mathf.Sin(a * 7f + 1.3f) + 0.0008f * Mathf.Sin(a * 13f + 0.4f);
                foreach (var q in prof) lv.Add(d * (r + q.x + wob) + Vector3.up * (q.y + (q.y > CutY ? wob * 0.4f : 0f)));
            }
            for (int s = 0; s < S; s++)
                for (int k = 0; k < P - 1; k++)
                {
                    int a0 = s * P + k, a1 = a0 + 1, b0 = a0 + P, b1 = b0 + 1;
                    lt.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
            AppendSmooth(mb, 0, lv, lt, barkTopIdx, outwardCheck: true);
        }

        // ---- 苔を生やし直す ----
        var rnd = new System.Random(4242);
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        var pads = new List<(Vector3 c, float r)>();
        int made = 0;
        // (種類, 数)：0 ふちをまたぐ / 1 横（上の方）/ 2 切り口の上の小さいの / 3 根元
        var plan = new[] { (0, 4), (1, 3), (2, 1), (3, 2) };   // 2026-10-06 19→10個に減らした
        foreach (var (kind, count) in plan)
        {
            int ok = 0, tries = 0;
            while (ok < count && tries++ < 400)
            {
                float ang = R(-Mathf.PI, Mathf.PI); var d = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)); float r0 = RC(ang);
                Vector3 n, guess; float rad, thick;
                if (kind == 0) { n = (d + Vector3.up).normalized; guess = d * r0 + Vector3.up * CutY; rad = R(0.035f, 0.055f); thick = R(0.009f, 0.013f); }
                else if (kind == 1) { n = d; guess = d * 0.17f + Vector3.up * R(0.08f, 0.27f); rad = R(0.026f, 0.045f); thick = R(0.007f, 0.011f); }
                else if (kind == 2) { n = Vector3.up; rad = R(0.016f, 0.024f); guess = d * (r0 - rad - R(0.02f, 0.035f)) + Vector3.up * CutY; thick = R(0.005f, 0.008f); }   // ふちにかからない所
                else { n = (d * 2f + Vector3.up).normalized; guess = d * 0.25f + Vector3.up * R(-0.3f, -0.18f); rad = R(0.035f, 0.055f); thick = R(0.009f, 0.013f); }
                System.Func<Vector2, (Vector3, Vector3)> surf;
                if (kind == 0)
                {
                    // ふちをまたぐ苔：ふちの丸い断面（上の平らな所 → 角の丸み → 横）に沿った座標で格子を作る。
                    // x＝ふちに沿った長さ、y＝断面に沿った長さ（＋で上の平らな所へ、−で横へ）。各点はその場所の面の向きに垂直に落とす
                    float a0 = ang, rr0 = r0, cr = 0.01f, arc = cr * Mathf.PI / 4f;
                    surf = q =>
                    {
                        float a = a0 + q.x / rr0; var dd = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); float rA = RC(a);
                        float cR = rA - cr, cY = CutY - cr; float rr, yy; Vector3 nn;
                        if (q.y > arc) { rr = cR - (q.y - arc); yy = cY + cr; nn = Vector3.up; }
                        else if (q.y < -arc) { rr = cR + cr; yy = cY - (-q.y - arc); nn = dd; }
                        else { float th = Mathf.PI / 4f + q.y / cr; rr = cR + Mathf.Cos(th) * cr; yy = cY + Mathf.Sin(th) * cr; nn = (dd * Mathf.Cos(th) + Vector3.up * Mathf.Sin(th)).normalized; }
                        return (dd * rr + Vector3.up * yy, nn);
                    };
                }
                else
                {
                    // 中心を表面に落として、その接平面で格子を作る
                    if (!Physics.Raycast(guess + off + n * 0.3f, -n, out var ch, 0.6f) || ch.collider != mc) continue;
                    var c0 = ch.point - off; var n0 = n;
                    var t1 = Vector3.Cross(n0, Mathf.Abs(n0.y) < 0.95f ? Vector3.up : Vector3.right).normalized; var t2 = Vector3.Cross(n0, t1);
                    surf = q => (c0 + t1 * q.x + t2 * q.y, n0);
                }
                var c = surf(Vector2.zero).Item1;
                if (pads.Any(p => (p.c - c).magnitude < (p.r + rad) * 1.08f)) continue;
                if (mush.Any(p => (p - c).magnitude < rad + 0.02f)) continue;
                if (BuildPad(mb, surf, rad, thick, mc, off, rnd, RC)) { pads.Add((c, rad)); ok++; made++; }
            }
        }
        Object.DestroyImmediate(colGo); Object.DestroyImmediate(colMesh);
        log.Append("moss pads " + made + " ");

        // 苔の中で、面の向きとなめらかな法線が逆になっている三角（強く折れた所の小さい三角）は捨てる（裏返って見えないように）
        {
            var keep = new List<int>(); var t = mb.S[3];
            for (int i = 0; i < t.Count; i += 3)
            {
                var fn = Vector3.Cross(mb.V[t[i + 1]] - mb.V[t[i]], mb.V[t[i + 2]] - mb.V[t[i]]);
                if (fn.sqrMagnitude < 1e-14f || Vector3.Dot(fn, mb.N[t[i]] + mb.N[t[i + 1]] + mb.N[t[i + 2]]) < 0f) continue;
                keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]);
            }
            log.Append("dropped " + (t.Count - keep.Count) / 3 + " ");
            mb.S[3] = keep;
        }

        // ---- 書き出し（既存のアセットへ Clear→Set で入れ直す）----
        var dst = AssetDatabase.LoadAssetAtPath<Mesh>(DstPath);
        dst.Clear();
        dst.indexFormat = mb.V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        dst.SetVertices(mb.V); dst.SetNormals(mb.N); dst.SetUVs(1, mb.U);
        dst.subMeshCount = subs; for (int s = 0; s < subs; s++) dst.SetTriangles(mb.S[s], s);
        dst.RecalculateBounds();
        EditorUtility.SetDirty(dst); AssetDatabase.SaveAssets();
        log.Append("verts " + mb.V.Count);
        return log.ToString();
    }

    static int Bin(Vector3 p, int B) { return ((int)Mathf.Floor((Mathf.Atan2(p.z, p.x) + Mathf.PI) / (Mathf.PI * 2) * B)) % B; }

    // 頂点を共有した部品を足す（法線はなめらかに計算）。lightmap用UVは近くの樹皮の頂点から借りる
    static void AppendSmooth(MB mb, int sub, List<Vector3> v, List<int> t, List<int> uvFrom, bool outwardCheck)
    {
        var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals();
        var n = m.normals;
        if (outwardCheck)
        {
            // 全体として外向き（中心から離れる・上向き）になっているか確かめて、逆なら裏返す
            float dot = 0; for (int i = 0; i < v.Count; i++) dot += Vector3.Dot(n[i], new Vector3(v[i].x, 0, v[i].z).normalized + Vector3.up * 0.3f);
            if (dot < 0) { for (int i = 0; i < t.Count; i += 3) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; } for (int i = 0; i < n.Length; i++) n[i] = -n[i]; }
        }
        int b = mb.V.Count;
        for (int i = 0; i < v.Count; i++)
        {
            mb.V.Add(v[i]); mb.N.Add(n[i]);
            Vector2 uv = Vector2.zero; float best = float.MaxValue;
            if (uvFrom != null) foreach (var j in uvFrom) { float d = (mb.V[j] - v[i]).sqrMagnitude; if (d < best) { best = d; uv = mb.U[j]; } }
            mb.U.Add(uv);
        }
        foreach (var i in t) mb.S[sub].Add(b + i);
        Object.DestroyImmediate(m);
    }

    // クッション状の苔1つ：中心 c・向き n・半径 rad。円盤の格子を表面へ落として、厚み分だけ浮かせる。ふちは表面に少しもぐらせる
    static bool BuildPad(MB mb, System.Func<Vector2, (Vector3, Vector3)> surf, float rad, float thick, MeshCollider mc, Vector3 off, System.Random rnd, System.Func<float, float> rcAt)
    {
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        int rings = 7, segs = 22;
        var bns = new List<Vector3>();   // 各頂点の、落とした面の向き（裏返りの確認用）
        float ph1 = R(0, 6.28f), ph2 = R(0, 6.28f), ph3 = R(0, 6.28f);
        float Edge(float a) { return 1f + 0.16f * Mathf.Sin(a * 2 + ph1) + 0.1f * Mathf.Sin(a * 3 + ph2) + 0.06f * Mathf.Sin(a * 5 + ph3); }
        var v = new List<Vector3>(); var t = new List<int>();
        bool Proj(Vector2 local, float h, out Vector3 p)
        {
            p = Vector3.zero;
            var (bp, bn) = surf(local);
            if (!Physics.Raycast(bp + off + bn * 0.06f, -bn, out var hit, 0.2f) || hit.collider != mc) return false;
            bns.Add(bn);
            var hp = hit.point - off; var hn = hit.normal; if (Vector3.Dot(hn, bn) < 0f) hn = -hn;   // 両面にした帯の裏側に当たった時
            if (hn.y > 0.9f && hp.y > CutY - 0.003f) hp.y = Mathf.Max(hp.y, RingY + 0.0004f);   // 切り口の上は年輪より上から
            p = hp + hn * h;
            return true;
        }
        // 中心
        if (!Proj(Vector2.zero, thick, out var pc)) return false;
        v.Add(pc);
        for (int i = 1; i <= rings + 1; i++)
        {
            float u = Mathf.Min(1f, i / (float)rings);
            for (int s = 0; s < segs; s++)
            {
                float a = s / (float)segs * Mathf.PI * 2;
                float er = rad * Edge(a) * u;
                var local = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * er;
                // ふっくらしたドーム＋小さなでこぼこ。外側の輪（i=rings+1）は表面の少し下へもぐらせて、ふちが浮かないように
                float h = i > rings ? -0.003f : thick * Mathf.Pow(Mathf.Max(0f, 1f - u * u), 0.55f) * (1f + 0.18f * u * Mathf.Sin(a * 4 + ph2 + u * 3f)) + 0.0012f;
                if (!Proj(local, h, out var p)) return false;
                v.Add(p);
            }
        }
        for (int s = 0; s < segs; s++) t.AddRange(new[] { 0, 1 + (s + 1) % segs, 1 + s });
        for (int i = 0; i < rings; i++)
            for (int s = 0; s < segs; s++)
            {
                int a0 = 1 + i * segs + s, a1 = 1 + i * segs + (s + 1) % segs, b0 = a0 + segs, b1 = a1 + segs;
                t.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
            }
        // 向きを確かめる（表＝外向き）
        var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals();
        if (Vector3.Dot(m.normals[0], bns[0]) < 0) { for (int i = 0; i < t.Count; i += 3) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; } }
        Object.DestroyImmediate(m);
        // 表面の角で格子が折り返して裏返った三角（ふちのもぐらせた所に出る）は捨てる
        var keep = new List<int>();
        for (int i = 0; i < t.Count; i += 3)
        {
            var fn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
            if (fn.sqrMagnitude < 1e-14f || Vector3.Dot(fn, bns[t[i]] + bns[t[i + 1]] + bns[t[i + 2]]) <= 0f) continue;
            keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]);
        }
        t = keep;
        AppendSmooth(mb, 3, v, t, null, false);
        return true;
    }
}
