using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 歯車を軽くする（2026-11-22 てじゃ「2-4の歯車とかの軽量化」）。歯車のメッシュは 30 か所くらいのスクリプトで FuwaGearTest.Extrude（輪郭を上下にのばす）
// から作られていて、歯の側面を 8〜10 点で作っているので細かい（2–4 の歯車だけで約 18 万三角形）。
// コースを作り直さずに、シーンで使っている Extrude の形のメッシュから輪郭を読み取り、まっすぐに近い所の点を間引いて（ずれ tol 以内）同じアセットに作り直す。
//  ・Extrude の形：頂点 0 が上の中心、1..n が上の輪郭、n+1 が下の中心、… 三角形 4n。これと完全に同じ並びの物だけさわる（ほかの作り方の歯車はそのまま）
//  ・元の形は Generated/GearHi/*_Hi に取っておき、いつもそこから作る（何回押しても同じ結果）
//  ・まとめ（VC_ の写し）は元のメッシュから作るので、いったん戻してから作り直し、まとめ直す
// ビルダーで作り直したら、これを押し直す
public static class FuwaGearLite
{
    const string Bak = "Assets/FuwaCourse/Generated/GearHi";
    public static float TolPerModule = 0.02f;   // 間引いてよいずれ（歯の大きさ＝モジュールに対して）。0.06 だと歯が台形になる、0.02 で約半分

    [MenuItem("FuwaCourse/よく使う/歯車を軽くする（作り直し後に）")]
    static void Menu() { Debug.Log(Run(false)); }
    [MenuItem("FuwaCourse/よく使う/歯車を軽くする（数えるだけ）")]
    static void MenuDry() { Debug.Log(Run(true)); }

    public static string Run(bool dry)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!dry) FuwaMaterialMerge.Revert();
        var meshes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true)).Select(f => f.sharedMesh)
            .Concat(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshCollider>(true)).Select(c => c.sharedMesh))
            .Where(m => m && AssetDatabase.GetAssetPath(m).StartsWith("Assets/") && !AssetDatabase.GetAssetPath(m).StartsWith(Bak)).Distinct().ToList();
        if (!dry && !AssetDatabase.IsValidFolder(Bak)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "GearHi");
        var log = new List<string>(); long before = 0, after = 0; int count = 0;
        foreach (var m in meshes)
        {
            string bp = Bak + "/" + m.name + "_Hi.asset";
            var hi = AssetDatabase.LoadAssetAtPath<Mesh>(bp);
            var src = hi ? hi : m;
            if (!ParseExtrude(src, out var o, out float thick, out var cen)) continue;
            var so = Simplify(o);
            if (so.Length >= o.Length) continue;
            // UV：歯車の模様（ToonLit の _GearPattern）は上から見た平面の UV（u = a·x + b, v = c·z + d）を使う。元の UV から a〜d を求めて付け直す。
            //  （2026-11-22 最初は UV を付け忘れて、模様が消えて真っ黒・すじになった）。平面の UV でない物はさわらない
            if (!FitPlanarUV(src, out var fu, out var fv)) continue;
            var built = FuwaGearTest.Extrude(so, thick, m.name, cen);
            built.uv = built.vertices.Select(p => new Vector2(fu.x * p.x + fu.y, fv.x * p.z + fv.y)).ToArray();
            count++; before += src.triangles.Length / 3; after += built.triangles.Length / 3;
            log.Add($"{m.name}: {src.triangles.Length / 3} → {built.triangles.Length / 3}");
            if (dry) { Object.DestroyImmediate(built); continue; }
            if (!hi) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(m), bp);
            m.Clear(false);
            m.indexFormat = built.vertexCount > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(built.vertices); m.SetTriangles(built.triangles, 0); m.SetNormals(built.normals); m.SetUVs(0, built.uv); m.RecalculateBounds();
            EditorUtility.SetDirty(m); Object.DestroyImmediate(built);
        }
        string res = $"[歯車を軽く] {count} 個 {before / 1000}k → {after / 1000}k（メッシュ1つずつ。使っている数はかけていない）\n" + string.Join("\n", log.OrderBy(s => s));
        if (!dry)
        {
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            res += "\n" + FuwaMaterialMerge.Run(false);
        }
        return res;
    }

    // u = a·x + b、v = c·z + d を最小二乗で。UV なし（null を返す代わりに恒等）・平面でない（ずれ 1e-3 超）・色や接線などほかの情報がある物は false
    static bool FitPlanarUV(Mesh m, out Vector2 fu, out Vector2 fv)
    {
        fu = fv = Vector2.zero;
        var v = m.vertices; var uv = m.uv;
        if (uv.Length != v.Length || m.colors.Length > 0 || m.tangents.Length > 0 || m.uv2.Length > 0) return false;
        Vector2 Fit(System.Func<int, float> X, System.Func<int, float> Y)
        {
            double sx = 0, sy = 0, sxx = 0, sxy = 0; int n = v.Length;
            for (int i = 0; i < n; i++) { double x = X(i), y = Y(i); sx += x; sy += y; sxx += x * x; sxy += x * y; }
            double den = n * sxx - sx * sx; if (System.Math.Abs(den) < 1e-12) return new Vector2(float.NaN, 0f);
            double a = (n * sxy - sx * sy) / den, b = (sy - a * sx) / n; return new Vector2((float)a, (float)b);
        }
        fu = Fit(i => v[i].x, i => uv[i].x); fv = Fit(i => v[i].z, i => uv[i].y);
        if (float.IsNaN(fu.x) || float.IsNaN(fv.x)) return false;
        for (int i = 0; i < v.Length; i++)
            if (Mathf.Abs(fu.x * v[i].x + fu.y - uv[i].x) > 1e-3f || Mathf.Abs(fv.x * v[i].z + fv.y - uv[i].y) > 1e-3f) return false;
        return true;
    }

    // FuwaGearTest.Extrude と同じ並びか確かめて、輪郭・厚み・中心を取り出す
    static bool ParseExtrude(Mesh m, out Vector2[] o, out float thick, out Vector2 cen)
    {
        o = null; thick = 0f; cen = Vector2.zero;
        if (m.subMeshCount != 1) return false;
        var t = m.triangles; if (t.Length % 12 != 0) return false;
        int n = t.Length / 12; if (n < 12) return false;
        var v = m.vertices; if (v.Length < 2 * n + 2) return false;
        thick = v[0].y; if (thick <= 0f) return false;
        const float e = 1e-5f;
        if (Mathf.Abs(v[n + 1].y) > e || Mathf.Abs(v[n + 1].x - v[0].x) > e || Mathf.Abs(v[n + 1].z - v[0].z) > e) return false;
        o = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            var a = v[1 + i]; var b = v[n + 2 + i];
            if (Mathf.Abs(a.y - thick) > e || Mathf.Abs(b.y) > e || Mathf.Abs(a.x - b.x) > e || Mathf.Abs(a.z - b.z) > e) return false;
            o[i] = new Vector2(a.x, a.z);
        }
        cen = new Vector2(v[0].x, v[0].z);
        // 三角形の並びも確かめる（上の扇・下の扇）
        for (int i = 0; i < n; i++)
        {
            if (t[i * 3] != 0 || t[i * 3 + 1] != 1 + (i + 1) % n || t[i * 3 + 2] != 1 + i) return false;
            int k = (n + i) * 3; if (t[k] != n + 1 || t[k + 1] != n + 2 + i || t[k + 2] != n + 2 + (i + 1) % n) return false;
        }
        return true;
    }

    // ずれの小さい点から順に抜く（抜いた時に、その点から前後の点を結ぶ線までの距離が tol 未満の間）
    static Vector2[] Simplify(Vector2[] o)
    {
        var c = Vector2.zero; // 中心からの距離で歯の大きさを見る（扇形の中心の点は抜かない）
        var rs = o.Select(p => p.magnitude).Where(r => r > 1e-4f).ToList();
        float rmax = rs.Max(), rmin = rs.Where(r => r > rmax * 0.3f).DefaultIfEmpty(rmax * 0.3f).Min();
        float mod = Mathf.Max(1e-3f, (rmax - rmin) / 2.25f);
        float tol = TolPerModule * mod;
        var pts = o.ToList();
        float Err(int i)
        {
            int n = pts.Count; var a = pts[(i - 1 + n) % n]; var b = pts[(i + 1) % n]; var p = pts[i];
            if (p.magnitude < 1e-4f) return float.MaxValue;   // 扇の中心
            var ab = b - a; float L = ab.magnitude; if (L < 1e-6f) return 0f;
            return Mathf.Abs(ab.x * (p.y - a.y) - ab.y * (p.x - a.x)) / L;
        }
        while (pts.Count > 12)
        {
            int best = -1; float be = tol;
            for (int i = 0; i < pts.Count; i++) { float er = Err(i); if (er < be) { be = er; best = i; } }
            if (best < 0) break;
            pts.RemoveAt(best);
        }
        return pts.ToArray();
    }
}
