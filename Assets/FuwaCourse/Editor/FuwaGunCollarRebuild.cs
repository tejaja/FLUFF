using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// エアガンのラッパの首まわり（ラッパの裏の広がった輪）を作り直す。元は Gun_Accent_Source、書き出し先は Gun_Accent。何度実行しても同じ結果。
// ・前から見た時：輪の外側からラッパの首へ、なめらかな曲面でつなげて埋める（すき間から中が見えない）
// ・後ろから見た時：今まで通り、輪の内側に暗い通気口（Gun_Vent）と支えの棒が見える
// 古い輪の面（外側・後ろのふち・内側の壁）は消して、1枚の回転体に置き換える。ラッパ本体・支えの棒・ほかの黄色い部品はそのまま
public static class FuwaGunCollarRebuild
{
    const string SrcPath = "Assets/FuwaCourse/Meshes/Gun_Accent_Source.asset";
    const string DstPath = "Assets/FuwaCourse/Meshes/Gun_Accent.asset";
    const float CY = 0.055f;   // 銃身の軸の高さ（軸は z 向き）

    // 断面（r, z）：内側の壁（通気口のすぐ前）→ 後ろのふち → 外側のふくらみ → 前へ向かってラッパの外側の面へなめらかにつながる。
    // 最後の点はラッパの外側の面の輪（z=0.17198）とぴったり同じ位置・同じ向きにして、つなぎ目の線が出ないようにする
    static readonly Vector2[] Back =
    {
        new Vector2(0.0200f, 0.1400f), new Vector2(0.0208f, 0.1382f), new Vector2(0.0217f, 0.1366f), new Vector2(0.0228f, 0.1352f),
        new Vector2(0.0240f, 0.1346f), new Vector2(0.0249f, 0.1355f), new Vector2(0.0253f, 0.1375f), new Vector2(0.0252f, 0.1400f),
        new Vector2(0.0247f, 0.1428f), new Vector2(0.0239f, 0.1460f),
    };
    const float SeamZ = 0.17198f, SeamR = 0.02986f;          // ラッパの外側の面の輪（元のメッシュの値）
    static readonly Vector2 SeamN = new Vector2(0.7129f, -0.7012f);   // その輪の法線（r, z）
    const int Segs = 36;                                      // ラッパと同じ分割（0°から10°ごと）

    static Vector2[] BuildProfile(out Vector2[] normals)
    {
        var pts = new List<Vector2>(Back);
        // 最後の点から継ぎ目の輪まで、エルミート曲線でなめらかにつなぐ（向きも合わせる）
        Vector2 A = Back[Back.Length - 1], B = new Vector2(SeamR, SeamZ);
        Vector2 tA = new Vector2(-0.2f, 1f).normalized, tB = new Vector2(-SeamN.y, SeamN.x).normalized;   // 法線を90°回すと接線
        if (tB.y < 0) tB = -tB;
        float L = (B - A).magnitude * 1.1f;
        int K = 12;
        for (int i = 1; i <= K; i++)
        {
            float t = i / (float)K, t2 = t * t, t3 = t2 * t;
            pts.Add((2 * t3 - 3 * t2 + 1) * A + (t3 - 2 * t2 + t) * L * tA + (-2 * t3 + 3 * t2) * B + (t3 - t2) * L * tB);
        }
        var P = pts.ToArray(); int n = P.Length;
        normals = new Vector2[n];
        for (int k = 0; k < n; k++)
        {
            Vector2 a = P[Mathf.Max(0, k - 1)], b = P[Mathf.Min(n - 1, k + 1)];
            var tan = (b - a).normalized; normals[k] = new Vector2(tan.y, -tan.x);
        }
        if (normals[6].x < 0) for (int k = 0; k < n; k++) normals[k] = -normals[k];
        normals[n - 1] = SeamN;
        return P;
    }

    // 断面（r, z）を軸のまわりに回した面を足す。面の向きは法線に合わせる
    static void Revolve(List<Vector3> V, List<Vector3> N, List<int> T, Vector2[] prof, Vector2[] pn, int S)
    {
        int P = prof.Length, b0 = V.Count;
        for (int s = 0; s <= S; s++)
        {
            float a = s / (float)S * Mathf.PI * 2; float c = Mathf.Cos(a), sn = Mathf.Sin(a);
            for (int k = 0; k < P; k++)
            {
                V.Add(new Vector3(c * prof[k].x, CY + sn * prof[k].x, prof[k].y));
                N.Add(new Vector3(c * pn[k].x, sn * pn[k].x, pn[k].y).normalized);
            }
        }
        for (int s = 0; s < S; s++)
            for (int k = 0; k < P - 1; k++)
            {
                int a0 = b0 + s * P + k, a1 = a0 + 1, c0 = a0 + P, c1 = c0 + 1;
                var fn = Vector3.Cross(V[a1] - V[a0], V[c0] - V[a0]);
                if (Vector3.Dot(fn, N[a0] + N[a1] + N[c0]) > 0) T.AddRange(new[] { a0, a1, c0, a1, c1, c0 });
                else T.AddRange(new[] { a0, c0, a1, a1, c0, c1 });
            }
    }

    [MenuItem("FuwaCourse/エアガンのラッパの首を作り直す")]
    public static void RunMenu() { Debug.Log(Run()); Debug.Log(FuwaGunWeld.Run()); }   // 最後に重複頂点をまとめる

    public static string Run()
    {
        var src = AssetDatabase.LoadAssetAtPath<Mesh>(SrcPath);
        var v = src.vertices; var n = src.normals; var t = src.GetTriangles(0);
        float R(Vector3 p) { return new Vector2(p.x, p.y - CY).magnitude; }
        float Ang(Vector3 p) { return Mathf.Atan2(p.y - CY, p.x) * Mathf.Rad2Deg; }
        bool Strut(Vector3 p)
        {
            float a = Mathf.Abs(Mathf.DeltaAngle(Ang(p), 45f)) % 90f; a = Mathf.Min(a, 90f - a);   // ±45°・±135° の近く
            return a < 6f && p.z > 0.137f && p.z < 0.1465f && R(p) > 0.0145f && R(p) < 0.0212f;
        }
        // 古い輪の頂点：z 0.134〜0.1485、半径 0.0198 以上（ラッパの首の外側は 0.0185 以下なので入らない）、支えの棒は除く
        bool Collar(Vector3 p) { return p.z > 0.1338f && p.z < 0.1486f && R(p) > 0.0198f && R(p) < 0.0262f && !Strut(p); }
        // ラッパの外側の面（首〜継ぎ目の輪まで）：外向きの法線で、半径 0.017 以上
        bool HornOuter(int i)
        {
            var p = v[i]; if (p.z < 0.1395f || p.z > SeamZ + 0.00005f || R(p) < 0.0170f || Strut(p)) return false;
            var rd = new Vector2(p.x, p.y - CY).normalized; return Vector2.Dot(new Vector2(n[i].x, n[i].y), rd) > 0.05f;
        }
        // ゲージの黄色い台（左右、z -0.006〜0.078）：作り直すので消す（後ろの黄色い輪 z -0.031〜-0.020 は消さない）
        bool Pad(Vector3 p) { return Mathf.Abs(p.x) > 0.017f && p.z > -0.012f && p.z < 0.085f && p.y > 0.028f && p.y < 0.082f; }
        bool Old(int i) { return Collar(v[i]) || HornOuter(i) || Strut(v[i]) || Pad(v[i]); }   // 支えの棒も作り直す（角を丸める）
        var keepT = new List<int>(); int removed = 0;
        for (int i = 0; i < t.Length; i += 3)
        {
            if (Old(t[i]) && Old(t[i + 1]) && Old(t[i + 2])) { removed++; continue; }
            keepT.Add(t[i]); keepT.Add(t[i + 1]); keepT.Add(t[i + 2]);
        }
        // 古いトリガー（つぶした球）も消す。新しいトリガーは FuwaGunTriggerRebuild で足す
        keepT = FuwaGunTriggerRebuild.RemoveIslandsInBox(v, keepT, FuwaGunTriggerRebuild.OldTriggerBox, out int trigRemoved);
        // 使っている頂点だけに詰める
        var map = new Dictionary<int, int>(); var V = new List<Vector3>(); var N = new List<Vector3>(); var T = new List<int>();
        foreach (var i in keepT) { if (!map.TryGetValue(i, out int j)) { j = V.Count; map[i] = j; V.Add(v[i]); N.Add(n[i]); } T.Add(j); }

        // 新しい回転体（ラッパと同じく0°から10°ごと。継ぎ目の輪はラッパの頂点とぴったり同じ位置）
        var Profile = BuildProfile(out var pn);
        Revolve(V, N, T, Profile, pn, Segs);
        // 支えの棒の根元：銃身に巻いた黄色い輪（棒が刺さる所に層がある感じ）。後ろから通気口の奥に見える
        {
            var sp = new[] { new Vector2(0.0140f, 0.1402f), new Vector2(0.0140f, 0.1364f), new Vector2(0.0144f, 0.1356f), new Vector2(0.0152f, 0.1352f),
                             new Vector2(0.0161f, 0.1354f), new Vector2(0.0167f, 0.1361f), new Vector2(0.0168f, 0.1402f) };
            var sn = new Vector2[sp.Length];
            for (int k = 0; k < sp.Length; k++)
            {
                Vector2 a = sp[Mathf.Max(0, k - 1)], b = sp[Mathf.Min(sp.Length - 1, k + 1)];
                var tan = (b - a).normalized; sn[k] = new Vector2(tan.y, -tan.x);
            }
            if (sn[5].x < 0) for (int k = 0; k < sn.Length; k++) sn[k] = -sn[k];   // 外側の面が外向きになるように
            Revolve(V, N, T, sp, sn, Segs);
        }
        // 支えの棒（4本、±45°・±135°）：角を少し丸めた（ベベル）細い板。根元は黄色い輪の中、先は輪の内側の壁の中に埋める
        {
            const float w = 0.0021f, h = 0.0085f, zc = 0.14175f, rad = 0.00025f, r0 = 0.0146f, r1 = 0.0213f; const int cs = 2;   // 角だけ少し落とす（丸い棒にはしない）。法線はなめらか
            // 角を丸めた長方形の断面（接線方向 x、軸方向 y）
            var sec = new List<Vector2>(); var secN = new List<Vector2>();
            var corners = new[] { new Vector2(w / 2 - rad, h / 2 - rad), new Vector2(-(w / 2 - rad), h / 2 - rad), new Vector2(-(w / 2 - rad), -(h / 2 - rad)), new Vector2(w / 2 - rad, -(h / 2 - rad)) };
            for (int q = 0; q < 4; q++)
                for (int k = 0; k <= cs; k++)
                {
                    float a = (q * 90f + k * 90f / cs) * Mathf.Deg2Rad; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    sec.Add(corners[q] + d * rad); secN.Add(d);
                }
            int M = sec.Count;
            foreach (var deg in new[] { 45f, 135f, 225f, 315f })
            {
                float a = deg * Mathf.Deg2Rad; var radial = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0); var tang = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0);
                int b = V.Count;
                foreach (var rr in new[] { r0, r1 })
                    for (int k = 0; k < M; k++)
                    {
                        V.Add(new Vector3(0, CY, zc) + radial * rr + tang * sec[k].x + Vector3.forward * sec[k].y);
                        N.Add((tang * secN[k].x + Vector3.forward * secN[k].y).normalized);
                    }
                for (int k = 0; k < M; k++)
                {
                    int a0 = b + k, a1 = b + (k + 1) % M, c0 = a0 + M, c1 = a1 + M;
                    var fn = Vector3.Cross(V[a1] - V[a0], V[c0] - V[a0]);
                    if (Vector3.Dot(fn, N[a0] + N[a1]) > 0) T.AddRange(new[] { a0, a1, c0, a1, c1, c0 });
                    else T.AddRange(new[] { a0, c0, a1, a1, c0, c1 });
                }
            }
        }

        FuwaGunTriggerRebuild.AddTrigger(V, N, T);

        // ゲージの台（左右）を作り直す。前の面の給気口の暗い底は Gun_Vent に足す
        var VV = new List<Vector3>(); var VN = new List<Vector3>(); var VT = new List<int>();
        foreach (float side in new[] { 1f, -1f }) BuildPad(V, N, T, VV, VN, VT, side);

        var dst = AssetDatabase.LoadAssetAtPath<Mesh>(DstPath);
        dst.Clear();
        dst.indexFormat = V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        dst.SetVertices(V); dst.SetNormals(N); dst.subMeshCount = 1; dst.SetTriangles(T, 0); dst.RecalculateBounds();
        EditorUtility.SetDirty(dst);
        {
            var vs = AssetDatabase.LoadAssetAtPath<Mesh>(VentSrcPath); var vd = AssetDatabase.LoadAssetAtPath<Mesh>(VentDstPath);
            var W = vs.vertices.ToList(); var WN = vs.normals.ToList(); var WT = vs.GetTriangles(0).ToList();
            int wb = W.Count; W.AddRange(VV); WN.AddRange(VN); foreach (var i in VT) WT.Add(wb + i);
            vd.Clear(); vd.SetVertices(W); vd.SetNormals(WN); vd.SetTriangles(WT, 0); vd.RecalculateBounds(); EditorUtility.SetDirty(vd);
        }
        AssetDatabase.SaveAssets();
        // ゲージ（左右の板）を新しい台の面の上へ
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var gun = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "FuwaBlower");
        if (gun != null)
        {
            var acc = gun.transform.Find("Visual/GunAccent");
            if (acc != null)
            {
                var mr = acc.GetComponent<MeshRenderer>(); var mats = mr.sharedMaterials;
                if (mats.Length != 1) { Undo.RecordObject(mr, "accent mat"); mr.sharedMaterials = new[] { mats[0] }; }   // 前の四角いブロック（2つ目の材質）はやめた
            }
        }
        if (gun != null)
            foreach (var gn in new[] { "Visual/GunGaugeR", "Visual/GunGaugeL" })
            {
                var g = gun.transform.Find(gn); if (g == null) continue;
                Undo.RecordObject(g, "gauge"); var lp = g.localPosition; float sd = gn.EndsWith("R") ? 1f : -1f;
                // 台の面（後ろの端を軸に傾けた面）の上、ゲージのまん中の z の所
                float dz = lp.z - PadBack, ang = PadTilt * Mathf.Deg2Rad;
                lp.x = sd * (PadOut + 0.0003f - dz * Mathf.Tan(ang)); g.localPosition = lp;
                var baseRot = sd > 0 ? Quaternion.Euler(0, 270, 0) : Quaternion.Euler(0, 90, 180);
                g.localRotation = Quaternion.AngleAxis(-sd * PadTilt, Vector3.up) * baseRot;
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            }
        return "removed " + removed + " tris (trigger " + trigRemoved + "), verts " + V.Count;
    }

    // ---- ゲージの黄色い台 ----
    // ゲージの面は今まで通り後ろから見やすい向き（銃の横に平行）のまま、台を外へずらして銃身との段差（厚み）がどこでも見えるようにする。
    // 銃身は前に向かって細くなるので、前の方は台が飛び出す。その前の面に縦長の給気口（暗い穴）をあける
    const string VentSrcPath = "Assets/FuwaCourse/Meshes/Gun_Vent_Source.asset";
    const string VentDstPath = "Assets/FuwaCourse/Meshes/Gun_Vent.asset";
    const float PadOut = 0.0333f, PadTilt = 2.63f, PadIn = 0.016f, PadBack = -0.00375f, PadFront = 0.07575f, PadY0 = 0.0445f, PadY1 = 0.0655f;   // ゲージ（z 0〜0.072、高さ13.5mm、両はし半円）のまわりにどこも同じ3.75mmのふち（はしの半円もゲージと同じ中心）
    const float PadBevel = 0.0009f;
    // 前の端の四角いブロック（給気口つき）。X は外側の面からの距離（内側〜外側）、H は高さ、In/Out は台の前の端から後ろへ埋める・前へ出す長さ

    static void AddTri(List<Vector3> V, List<int> T, int a, int b, int c, Vector3 want)
    {
        var fn = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
        if (Vector3.Dot(fn, want) >= 0) T.AddRange(new[] { a, b, c }); else T.AddRange(new[] { a, c, b });
    }

    // 角を丸めた長方形の輪郭（反時計回り）。まっすぐな辺も step(m) ごとに点を入れる（曲面に沿わせる時に辺が面の内側へもぐらないように）
    static List<Vector2> RoundRect(Vector2 c, Vector2 h, float r, int seg, float step = 0.0008f)
    {
        var pts = new List<Vector2>();
        var cs = new[] { new Vector2(h.x - r, h.y - r), new Vector2(-(h.x - r), h.y - r), new Vector2(-(h.x - r), -(h.y - r)), new Vector2(h.x - r, -(h.y - r)) };
        for (int q = 0; q < 4; q++)
        {
            for (int k = 0; k <= seg; k++) { float a = (q * 90f + k * 90f / seg) * Mathf.Deg2Rad; pts.Add(c + cs[q] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r); }
            // 次の角までのまっすぐな辺
            var from = pts[pts.Count - 1]; float a1 = ((q + 1) * 90f) * Mathf.Deg2Rad;
            var to = c + cs[(q + 1) % 4] + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r;
            int n = Mathf.FloorToInt((to - from).magnitude / step);
            for (int i = 1; i < n; i++) pts.Add(Vector2.Lerp(from, to, i / (float)n));
        }
        return pts;
    }

    static void BuildPad(List<Vector3> V, List<Vector3> N, List<int> T, List<Vector3> DV, List<Vector3> DN, List<int> DT, float side)
    {
        int v0 = V.Count;
        float yc = (PadY0 + PadY1) * 0.5f, hr = (PadY1 - PadY0) * 0.5f;
        // 台の輪郭（z, y）：前も後ろも半円の、角の丸い細長い形（前の台と同じ形）。前の半円 → 上の辺 → 後ろの半円 → 下の辺（反時計回り）
        var outl = new List<Vector2>(); var frontSeg = new List<bool>();
        int arcSeg = 28;
        for (int k = 0; k <= arcSeg; k++) { float a = (-90f + 180f * k / arcSeg) * Mathf.Deg2Rad; outl.Add(new Vector2(PadFront - hr, yc) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * hr); frontSeg.Add(false); }
        for (int k = 0; k <= arcSeg; k++) { float a = (90f + 180f * k / arcSeg) * Mathf.Deg2Rad; outl.Add(new Vector2(PadBack + hr, yc) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * hr); frontSeg.Add(false); }
        int M = outl.Count;
        Vector3 P(Vector2 zy, float x) { return new Vector3(x * side, zy.y, zy.x); }
        Vector3 sideN = new Vector3(side, 0, 0);
        var on = new Vector2[M];   // 輪郭の外向き（z-y 面内）
        for (int k = 0; k < M; k++) { var a = outl[(k - 1 + M) % M]; var b = outl[(k + 1) % M]; var tg = (b - a).normalized; on[k] = new Vector2(tg.y, -tg.x); }
        { var cen = Vector2.zero; foreach (var q in outl) cen += q; cen /= M; if (Vector2.Dot(on[0], outl[0] - cen) < 0) for (int k = 0; k < M; k++) on[k] = -on[k]; }

        // 外側の面（ゲージが乗る平らな面）
        {
            int c0 = V.Count; var cen = Vector2.zero; foreach (var q in outl) cen += q; cen /= M;
            V.Add(P(cen, PadOut)); N.Add(sideN);
            for (int k = 0; k < M; k++) { V.Add(P(outl[k] - on[k] * PadBevel, PadOut)); N.Add(sideN); }
            for (int k = 0; k < M; k++) AddTri(V, T, c0, c0 + 1 + k, c0 + 1 + (k + 1) % M, sideN);
        }
        // ふちの丸み（ベベル）＋横の壁（銃身の中まで）。前の面の区間は給気口つきで別に作る
        {
            int bS = 3, rows = bS + 2, b0 = V.Count;
            for (int k = 0; k < M; k++)
                for (int j = 0; j < rows; j++)
                {
                    Vector2 zy; float x; Vector3 nrm;
                    if (j <= bS) { float a = j / (float)bS * Mathf.PI * 0.5f; zy = outl[k] - on[k] * PadBevel * (1 - Mathf.Sin(a)); x = PadOut - PadBevel * (1 - Mathf.Cos(a)); nrm = (new Vector3(0, on[k].y, on[k].x) * Mathf.Sin(a) + sideN * Mathf.Cos(a)).normalized; }
                    else { zy = outl[k]; x = PadIn; nrm = new Vector3(0, on[k].y, on[k].x); }
                    V.Add(P(zy, x)); N.Add(nrm);
                }
            for (int k = 0; k < M; k++)
            {
                int k1 = (k + 1) % M;
                for (int j = 0; j < rows - 1; j++)
                {
                    if (j == rows - 2 && frontSeg[k]) continue;
                    int a0 = b0 + k * rows + j, a1 = a0 + 1, c0 = b0 + k1 * rows + j, c1 = c0 + 1;
                    var want = N[a0] + N[c0];
                    AddTri(V, T, a0, a1, c0, want); AddTri(V, T, a1, c1, c0, want);
                }
            }
        }
        // 台全体を銃身の細くなる傾きに合わせて回す（後ろの端を軸に）。ゲージの面もこの傾きに合わせる
        {
            var q = Quaternion.AngleAxis(-side * PadTilt, Vector3.up);
            var pivot = new Vector3(PadOut * side, 0f, PadBack);
            for (int i = v0; i < V.Count; i++) { V[i] = pivot + q * (V[i] - pivot); N[i] = q * N[i]; }
        }
    }
}
