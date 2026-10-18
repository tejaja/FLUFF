using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 噴気孔の穴（黒い円盤）を「奥の見える縦穴」にする（2026-10-05）。
// ・穴の円盤は FuwaVentHoleMask（色は描かず、見えている所だけ印をつける）に差し替える
// ・円盤のふちの形に沿って、下へ伸びる岩の縦穴（FuwaVentShaft、内側だけ描く）を作る
// 地面に本当に穴をあけなくても、穴の口から奥が見える。何度実行しても同じ結果
public static class FuwaVentShafts
{
    const string MatDir = "Assets/FuwaCourse/Materials/";
    const float Depth = 1.3f;

    [MenuItem("FuwaCourse/済み（押す前に相談）/噴気孔の縦穴を作る")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var mask = GetMat("VentHoleMask", "FuwaCourse/VentHoleMask");
        var shaft = GetMat("VentShaft", "FuwaCourse/VentShaft");
        string log = "";
        var root = GameObject.Find("FuwaCourse2").transform;
        // 溝の噴気孔（1つずつ）
        foreach (Transform v in root.Find("Gimmick_GrooveVents"))
        {
            var hole = v.Find("Hole"); if (hole == null) continue;
            log += v.name + ":" + MakeFor(hole, mask, shaft, "Course2_GrooveVentShaft", false) + " ";
        }
        // 山の噴気孔（まとめた1つのメッシュに複数の穴）
        var vh = root.Find("Rocks2/VentHoles");
        // 何度実行しても同じになるよう、口の輪は毎回もとの円盤（Blenderから取り込んだ物）から取る
        if (vh != null) { var g = AssetDatabase.FindAssets("Course2_ventholes_wide_raised t:Mesh"); if (g.Length > 0) { var o = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(g[0])); if (o != null) vh.GetComponent<MeshFilter>().sharedMesh = o; } }
        if (vh != null) log += "VentHoles:" + MakeFor(vh, mask, shaft, "Course2_VentShafts", true);   // 山の地面の下に埋もれているので、口を地面の高さまで上げる
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return log;
    }

    static Material GetMat(string name, string shader)
    {
        string p = MatDir + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, p); }
        m.shader = Shader.Find(shader);
        if (name == "VentShaft") { var rock = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Rock.mat"); if (rock != null) { var c = rock.GetColor("_Color"); m.SetColor("_Top", new Color(c.r * 0.62f, c.g * 0.6f, c.b * 0.66f, 1f)); } }
        EditorUtility.SetDirty(m); return m;
    }

    // 穴の円盤メッシュ（いくつかの円盤でもよい）から、それぞれのふちに沿った縦穴を作って子に置く
    static string MakeFor(Transform holeT, Material mask, Material shaftMat, string meshName, bool liftToSurface)
    {
        var mf = holeT.GetComponent<MeshFilter>(); var mr = holeT.GetComponent<MeshRenderer>(); var src = mf.sharedMesh;
        Undo.RecordObject(mr, "vent"); mr.sharedMaterial = mask; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        // ライトマップには入れない（色を描かないので）
        var fl = GameObjectUtility.GetStaticEditorFlags(holeT.gameObject); GameObjectUtility.SetStaticEditorFlags(holeT.gameObject, fl & ~StaticEditorFlags.ContributeGI);
        // 元の円盤はそのまま使わず（真ん中に小さな穴があいていて、そこから地面が見えてしまう）、ふちの輪から作り直した円盤を使う
        var srcKey = "Assets/FuwaCourse/Meshes/" + meshName + (holeT.parent != null && holeT.parent.name.StartsWith("Vent") ? "_" + holeT.parent.name : "") + "_Mouth.asset";

        var loops = BoundaryLoops(src);
        // 小さすぎる輪（円盤の真ん中の穴など）は捨てる
        loops.RemoveAll(l => { float per = 0; for (int i = 0; i < l.Count; i++) per += (src.vertices[l[i]] - src.vertices[l[(i + 1) % l.Count]]).magnitude; return per < 0.5f; });
        // 輪をワールドの点に。埋もれている穴は、真上から見た地面（いちばん上に当たる面）の高さまで持ち上げ、煙突の内側に収まるよう少しすぼめる
        var wloops = new List<List<Vector3>>(); string liftLog = "";
        Physics.SyncTransforms();
        foreach (var loop in loops)
        {
            var w = new List<Vector3>(); foreach (var i in loop) w.Add(holeT.TransformPoint(src.vertices[i]));
            if (liftToSurface)
            {
                var c = Vector3.zero; foreach (var p in w) c += p; c /= w.Count;
                // 穴の中の地面は斜めのことがあるので、口の輪の上で一番高い所に合わせる（低い所に合わせると、半分が地面に隠れる）
                float top = float.NegativeInfinity;
                foreach (var p0 in w) { var q = c + (p0 - c) * 0.97f; q.y = c.y; if (Physics.Raycast(q + Vector3.up * 4f, Vector3.down, out var hp, 8f, ~0, QueryTriggerInteraction.Ignore)) top = Mathf.Max(top, hp.point.y); }
                if (Physics.Raycast(c + Vector3.up * 4f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) top = Mathf.Max(top, hit.point.y);
                if (!float.IsNegativeInfinity(top))
                {
                    float lift = top + 0.015f - c.y;
                    if (lift > 0.02f) { for (int i = 0; i < w.Count; i++) { var o = w[i] - c; o.y = 0; w[i] = c + o * 0.97f + Vector3.up * (w[i].y - c.y + lift); } liftLog += "+" + lift.ToString("F2"); }
                }
            }
            wloops.Add(w);
        }
        // 縦穴はワールドで作ってから、子のローカルに直す（親が拡大縮小していても深さを正しく）
        var V = new List<Vector3>(); var N = new List<Vector3>(); var UV = new List<Vector2>(); var T = new List<int>();
        int seed = 1;
        foreach (var w0 in wloops)
        {
            var w = new List<Vector3>(w0);
            // 輪の向きを上から見て反時計回りにそろえる
            float area = 0; for (int i = 0; i < w.Count; i++) { var a = w[i]; var b = w[(i + 1) % w.Count]; area += a.x * b.z - b.x * a.z; }
            if (area < 0) w.Reverse();
            var c = Vector3.zero; foreach (var p in w) c += p; c /= w.Count;
            // ふちの点を間引く（16前後）
            int stepN = Mathf.Max(1, w.Count / 18); var ring0 = new List<Vector3>(); for (int i = 0; i < w.Count; i += stepN) ring0.Add(w[i]);
            BuildShaft(ring0, c, seed++, V, N, UV, T);
        }
        // 穴の口：ふちの輪＋真ん中の点の扇（元の円盤の座標のまま）
        {
            var MV = new List<Vector3>(); var MT = new List<int>();
            foreach (var w in wloops)
            {
                var lp = new List<Vector3>(); foreach (var p in w) lp.Add(holeT.InverseTransformPoint(p));
                var c0 = Vector3.zero; foreach (var p in lp) c0 += p; c0 /= lp.Count;
                int b0 = MV.Count; MV.Add(c0); MV.AddRange(lp);
                for (int i = 0; i < lp.Count; i++) { int a1 = b0 + 1 + i, a2 = b0 + 1 + (i + 1) % lp.Count; MT.Add(b0); MT.Add(a1); MT.Add(a2); }
            }
            // 上を向くように
            for (int q = 0; q < MT.Count; q += 3) { var n = Vector3.Cross(MV[MT[q + 1]] - MV[MT[q]], MV[MT[q + 2]] - MV[MT[q]]); if (holeT.TransformDirection(n).y < 0) { int tmp = MT[q + 1]; MT[q + 1] = MT[q + 2]; MT[q + 2] = tmp; } }
            var mouth = AssetDatabase.LoadAssetAtPath<Mesh>(srcKey);
            if (mouth == null) { mouth = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(srcKey) }; AssetDatabase.CreateAsset(mouth, srcKey); }
            { mouth.Clear(); mouth.SetVertices(MV); mouth.SetTriangles(MT, 0); mouth.RecalculateNormals(); mouth.RecalculateBounds(); EditorUtility.SetDirty(mouth); Undo.RecordObject(mf, "vent"); mf.sharedMesh = mouth; }
        }
        var mesh = new Mesh { name = meshName, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        // 子のローカルへ
        var t = holeT.Find("Shaft");
        if (t == null) { t = new GameObject("Shaft", typeof(MeshFilter), typeof(MeshRenderer)).transform; t.SetParent(holeT, false); Undo.RegisterCreatedObjectUndo(t.gameObject, "vent"); }
        t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one; t.gameObject.layer = holeT.gameObject.layer;
        for (int i = 0; i < V.Count; i++) { V[i] = t.InverseTransformPoint(V[i]); N[i] = t.InverseTransformDirection(N[i]).normalized; }
        mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, UV); mesh.SetTriangles(T, 0); mesh.RecalculateBounds();
        string path = "Assets/FuwaCourse/Meshes/" + meshName + (holeT.parent != null && holeT.parent.name.StartsWith("Vent") ? "_" + holeT.parent.name : "") + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old != null) { old.Clear(); old.indexFormat = mesh.indexFormat; old.SetVertices(V); old.SetNormals(N); old.SetUVs(0, UV); old.SetTriangles(T, 0); old.RecalculateBounds(); EditorUtility.SetDirty(old); mesh = old; }
        else AssetDatabase.CreateAsset(mesh, path);
        t.GetComponent<MeshFilter>().sharedMesh = mesh;
        var smr = t.GetComponent<MeshRenderer>(); smr.sharedMaterial = shaftMat; smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; smr.receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(t.gameObject, GameObjectUtility.GetStaticEditorFlags(holeT.gameObject) & ~StaticEditorFlags.ContributeGI);
        return loops.Count + "穴" + liftLog;
    }

    static float H(int i) { float x = Mathf.Sin(i * 127.1f + 311.7f) * 43758.5453f; return x - Mathf.Floor(x); }

    // 口の輪から下へ：少しずつすぼまって、岩肌のでこぼこ（段）をつけ、底は丸くふさぐ。面ごとに頂点を分けて角ばった見た目に
    static void BuildShaft(List<Vector3> top, Vector3 c, int seed, List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<int> T)
    {
        int n = top.Count; const int K = 7;
        var rings = new Vector3[K + 1, n];
        for (int k = 0; k <= K; k++)
        {
            float t = k / (float)K; float dy = -Depth * t + 0.06f;   // 口は円盤より少し上から（すき間が出ないように）
            float shrink = 1.05f - 0.25f * t * t;
            for (int i = 0; i < n; i++)
            {
                var off = top[i] - c; off.y = 0f;
                float j = (k == 0) ? 0f : 0.14f * (H(seed * 97 + k * 31 + i * 7) - 0.5f);
                var p = c + off * (shrink + j) + Vector3.up * (top[i].y - c.y + dy + ((k == 0) ? 0f : 0.06f * (H(seed * 13 + k * 5 + i) - 0.5f)));
                rings[k, i] = p;
            }
        }
        for (int k = 0; k < K; k++)
            for (int i = 0; i < n; i++)
            {
                int i1 = (i + 1) % n;
                Quad(V, N, UV, T, rings[k, i], rings[k, i1], rings[k + 1, i1], rings[k + 1, i], k / (float)K, (k + 1) / (float)K, c);
            }
        // 底：真ん中を少しへこませた扇
        var bc = Vector3.zero; for (int i = 0; i < n; i++) bc += rings[K, i]; bc /= n; bc += Vector3.down * 0.12f;
        for (int i = 0; i < n; i++) Tri(V, N, UV, T, rings[K, i], rings[K, (i + 1) % n], bc, 1f, 1f, 1f, c, true);
    }

    // 内側を向く四角（縦穴の壁）
    static void Quad(List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<int> T, Vector3 a, Vector3 b, Vector3 c2, Vector3 d, float va, float vb, Vector3 center)
    {
        Tri(V, N, UV, T, a, b, c2, va, va, vb, center, false);
        Tri(V, N, UV, T, a, c2, d, va, vb, vb, center, false);
    }

    static void Tri(List<Vector3> V, List<Vector3> N, List<Vector2> UV, List<int> T, Vector3 a, Vector3 b, Vector3 c, float ua, float ub, float uc, Vector3 center, bool bottom)
    {
        var n = Vector3.Cross(b - a, c - a).normalized;
        var mid = (a + b + c) / 3f;
        // 内側（穴の中心の方）か上を向くように
        var want = bottom ? Vector3.up : (new Vector3(center.x - mid.x, 0, center.z - mid.z)).normalized;
        int i0 = V.Count;
        if (Vector3.Dot(n, want) < 0) { var tmp = b; b = c; c = tmp; var tu = ub; ub = uc; uc = tu; n = -n; }
        V.Add(a); V.Add(b); V.Add(c); N.Add(n); N.Add(n); N.Add(n);
        UV.Add(new Vector2(0, ua)); UV.Add(new Vector2(0, ub)); UV.Add(new Vector2(0, uc));
        T.Add(i0); T.Add(i0 + 1); T.Add(i0 + 2);
    }

    // メッシュのふち（1つの三角形にしか使われていない辺）をつないで輪にする
    static List<List<int>> BoundaryLoops(Mesh m)
    {
        var vs = m.vertices; var tris = m.triangles;
        // 同じ位置の頂点はまとめる
        var canon = new int[vs.Length]; var map = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < vs.Length; i++) { var key = new Vector3Int(Mathf.RoundToInt(vs[i].x * 1000), Mathf.RoundToInt(vs[i].y * 1000), Mathf.RoundToInt(vs[i].z * 1000)); if (!map.TryGetValue(key, out var c)) { c = i; map[key] = i; } canon[i] = c; }
        var edgeCount = new Dictionary<long, int>(); var dirEdge = new Dictionary<int, int>();
        System.Func<int, int, long> K = (a, b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        for (int t = 0; t < tris.Length; t += 3) for (int e = 0; e < 3; e++) { int a = canon[tris[t + e]], b = canon[tris[t + (e + 1) % 3]]; var k = K(a, b); edgeCount[k] = edgeCount.TryGetValue(k, out var c) ? c + 1 : 1; }
        for (int t = 0; t < tris.Length; t += 3) for (int e = 0; e < 3; e++) { int a = canon[tris[t + e]], b = canon[tris[t + (e + 1) % 3]]; if (edgeCount[K(a, b)] == 1) dirEdge[a] = b; }
        var loops = new List<List<int>>(); var used = new HashSet<int>();
        foreach (var start in dirEdge.Keys)
        {
            if (used.Contains(start)) continue;
            var loop = new List<int>(); int cur = start; int guard = 0;
            while (!used.Contains(cur) && dirEdge.ContainsKey(cur) && guard++ < 10000) { used.Add(cur); loop.Add(cur); cur = dirEdge[cur]; }
            if (loop.Count >= 5) loops.Add(loop);
        }
        return loops;
    }
}
