using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 2-4 のパイプ（砂糖パイプ SugarPipe*・タンクの管 TankPipe*）を、1本につながった管のメッシュにする（2026-11-25 てじゃ「角パイプの角に隙間あるみたいなおせないかな」）。
//  前：まっすぐの筒（Pipe）＋曲がり目の球（PipeElbow）。筒と球の角の数がちがう（軽量化で 12 角の筒・低ポリの球になった物もある）ので、曲がり目にすき間・スジが出ていた
//  今：筒の両はしをつないだ折れ線を、曲がり目を少し丸めて（BendR）なぞる管（Sides 角、法線はなめらか）。真ちゅうの帯・入口の輪・ふくらみはそのまま。
//  材質は元の筒の材質（色まとめ前の Course2_4_SugarPipe）。このあと「色ちがいの材質をまとめる」をかけ直すと、色まとめ・部品まとめも作り直される
public static class FuwaPipeTubes
{
    const int Sides = 24, BendSteps = 6;
    const string Dir = "Assets/FuwaStage2/Generated/Course2_4";

    // ビルダーから：作ったばかりの管（Pipe と PipeElbow が子にある入れ物）を変換
    public static bool Convert(Transform holder, Material mat)
    {
        var segs = holder.Cast<Transform>().Where(c => c.name == "Pipe").ToList();
        if (segs.Count == 0) return false;
        // 筒の両はし（親から見た座標）と太さ
        var lines = new List<(Vector3 a, Vector3 b)>(); float R = segs[0].localScale.x * 0.5f;
        foreach (var s in segs)
        {
            var up = s.localRotation * Vector3.up; float h = s.localScale.y;
            lines.Add((s.localPosition - up * h, s.localPosition + up * h));
        }
        // 折れ線にならべる（作った順につながっている。向きだけ合わせる）
        var pts = new List<Vector3>();
        for (int i = 0; i < lines.Count; i++)
        {
            var (a, b) = lines[i];
            if (i == 0)
            {
                if (lines.Count > 1) { var n = lines[1]; float da = Mathf.Min((a - n.a).sqrMagnitude, (a - n.b).sqrMagnitude), db = Mathf.Min((b - n.a).sqrMagnitude, (b - n.b).sqrMagnitude); if (da < db) { var t = a; a = b; b = t; } }
                pts.Add(a); pts.Add(b);
            }
            else
            {
                var last = pts[pts.Count - 1];
                if ((b - last).sqrMagnitude < (a - last).sqrMagnitude) { var t = a; a = b; b = t; }
                pts.Add(b);
            }
        }
        // 長さ 0 をのぞく
        var p2 = new List<Vector3> { pts[0] }; for (int i = 1; i < pts.Count; i++) if ((pts[i] - p2[p2.Count - 1]).sqrMagnitude > 1e-6f) p2.Add(pts[i]);
        if (p2.Count < 2) return false;
        // 曲がり目を丸める（2次ベジェ）
        var path = new List<Vector3> { p2[0] };
        for (int i = 1; i < p2.Count - 1; i++)
        {
            Vector3 P = p2[i], d0 = (P - p2[i - 1]).normalized, d1 = (p2[i + 1] - P).normalized;
            float l0 = (P - p2[i - 1]).magnitude, l1 = (p2[i + 1] - P).magnitude;
            float br = Mathf.Min(R * 1.6f, l0 * 0.45f, l1 * 0.45f);
            if (Vector3.Dot(d0, d1) > 0.999f || br < 1e-3f) { path.Add(P); continue; }
            Vector3 A = P - d0 * br, B = P + d1 * br;
            for (int k = 0; k <= BendSteps; k++) { float t = k / (float)BendSteps; path.Add((1 - t) * (1 - t) * A + 2 * (1 - t) * t * P + t * t * B); }
        }
        path.Add(p2[p2.Count - 1]);
        // 管：ねじれないように前の輪の向きを運ぶ（parallel transport）
        var v = new List<Vector3>(); var nrm = new List<Vector3>(); var tri = new List<int>();
        Vector3 tan0 = (path[1] - path[0]).normalized;
        Vector3 nAxis = Vector3.Cross(tan0, Mathf.Abs(tan0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 tan = i == 0 ? (path[1] - path[0]).normalized : i == path.Count - 1 ? (path[i] - path[i - 1]).normalized : ((path[i + 1] - path[i]).normalized + (path[i] - path[i - 1]).normalized).normalized;
            nAxis = Vector3.ProjectOnPlane(nAxis, tan).normalized;
            var bAxis = Vector3.Cross(tan, nAxis);
            for (int s = 0; s <= Sides; s++)
            {
                float th = s * Mathf.PI * 2f / Sides; var dir = nAxis * Mathf.Cos(th) + bAxis * Mathf.Sin(th);
                v.Add(path[i] + dir * R); nrm.Add(dir);
            }
            if (i > 0)
            {
                int r0 = (i - 1) * (Sides + 1), r1 = i * (Sides + 1);
                for (int s = 0; s < Sides; s++) { tri.Add(r0 + s); tri.Add(r1 + s); tri.Add(r1 + s + 1); tri.Add(r0 + s); tri.Add(r1 + s + 1); tri.Add(r0 + s + 1); }
            }
        }
        // 面の向きを外へ（ねじれの決め方で裏返っていたら入れかえ）
        { var a = v[tri[0]]; var b = v[tri[1]]; var c = v[tri[2]]; var fn = Vector3.Cross(b - a, c - a); if (Vector3.Dot(fn, nrm[tri[0]]) < 0f) for (int i = 0; i < tri.Count; i += 3) { int t = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = t; } }
        // 両はしのふた
        foreach (int end in new[] { 0, path.Count - 1 })
        {
            Vector3 tan = end == 0 ? -(path[1] - path[0]).normalized : (path[end] - path[end - 1]).normalized;
            int c0 = v.Count; v.Add(path[end]); nrm.Add(tan);
            int ring = end * (Sides + 1);
            int rs = v.Count; for (int s = 0; s <= Sides; s++) { v.Add(v[ring + s]); nrm.Add(tan); }
            for (int s = 0; s < Sides; s++)
            {
                var a = v[c0]; var b = v[rs + s]; var c = v[rs + s + 1];
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), tan) > 0f) { tri.Add(c0); tri.Add(rs + s); tri.Add(rs + s + 1); } else { tri.Add(c0); tri.Add(rs + s + 1); tri.Add(rs + s); }
            }
        }
        var mesh = new Mesh { name = "PipeTube_" + holder.root.name + "_" + holder.name };
        mesh.SetVertices(v); mesh.SetNormals(nrm); mesh.SetTriangles(tri, 0); mesh.RecalculateBounds();
        string path2 = Dir + "/" + mesh.name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path2);
        if (old) { old.Clear(); old.SetVertices(v); old.SetNormals(nrm); old.SetTriangles(tri, 0); old.RecalculateBounds(); EditorUtility.SetDirty(old); mesh = old; }
        else { if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaStage2/Generated", "Course2_4"); AssetDatabase.CreateAsset(mesh, path2); }
        // 古い筒・球を消して、管を置く（筒の描画の設定をうつす）
        var src = segs[0].GetComponent<MeshRenderer>();
        var flags = GameObjectUtility.GetStaticEditorFlags(segs[0].gameObject);
        var go = new GameObject("PipeTube", typeof(MeshFilter), typeof(MeshRenderer)); Undo.RegisterCreatedObjectUndo(go, "pipe tube");
        go.transform.SetParent(holder, false); go.transform.SetSiblingIndex(0); go.layer = segs[0].gameObject.layer;
        GameObjectUtility.SetStaticEditorFlags(go, flags);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat ? mat : src.sharedMaterial; mr.shadowCastingMode = src.shadowCastingMode; mr.receiveShadows = src.receiveShadows;
        foreach (var t in holder.Cast<Transform>().Where(c => c.name == "Pipe" || c.name == "PipeElbow").ToList()) Undo.DestroyObjectImmediate(t.gameObject);
        return true;
    }

    // 今のシーンの 2-4 を変換（遠くの影の一覧の参照もつなぎかえ）→ そのあと材質まとめをかけ直す
    [MenuItem("FuwaCourse/ステージ2/2–4 パイプを1本の管にする（曲がり目のすき間）")]
    public static void Menu() { Debug.Log(RunScene()); }
    public static string RunScene()
    {
        var root = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "FuwaCourse2_4"); if (!root) return "2–4 がない";
        var pipeM = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_4_SugarPipe.mat");
        // 先に材質まとめ・部品まとめを元にもどす（元の材質・メッシュから作る）
        int rv = FuwaMaterialMergeRevert();
        var holders = root.GetComponentsInChildren<Transform>(true).Where(t => (t.name.StartsWith("SugarPipe") || t.name.StartsWith("TankPipe")) && t.Cast<Transform>().Any(c => c.name == "Pipe")).ToList();
        // 遠くの影の一覧：消す筒・球 → 新しい管
        var olds = new Dictionary<Renderer, Transform>();
        foreach (var h in holders) foreach (var c in h.Cast<Transform>().Where(c => c.name == "Pipe" || c.name == "PipeElbow")) { var r = c.GetComponent<Renderer>(); if (r) olds[r] = h; }
        int n = 0;
        foreach (var h in holders) if (Convert(h, pipeM)) n++;
        int fixedRefs = 0;
        foreach (var fs in Resources.FindObjectsOfTypeAll<FuwaFarShadows>().Where(x => x && x.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(x)))
        {
            if (fs.targets == null) continue;
            var t = fs.targets.ToList(); var c = (fs.centers ?? new Vector3[0]).ToList(); var r = (fs.radii ?? new float[0]).ToList(); var st = (fs.targetStage ?? new int[0]).ToList();
            var addedFor = new HashSet<Transform>(); bool ch = false;
            for (int i = t.Count - 1; i >= 0; i--)
            {
                if (t[i] != null) continue;   // 消した物は null になっている
                t.RemoveAt(i); if (i < c.Count) c.RemoveAt(i); if (i < r.Count) r.RemoveAt(i); if (i < st.Count) st.RemoveAt(i); ch = true;
            }
            if (ch) { fs.targets = t.ToArray(); fs.centers = c.ToArray(); fs.radii = r.ToArray(); fs.targetStage = st.ToArray(); fixedRefs++; }
        }
        // 管を遠くの影の一覧（SmallShadows）に足す（前は筒・球が入っていた物だけ）
        var small = Resources.FindObjectsOfTypeAll<FuwaFarShadows>().FirstOrDefault(x => x && x.gameObject.scene.IsValid() && x.name == "SmallShadows");
        int addN = 0;
        if (small != null)
        {
            var t = small.targets.ToList(); var c = small.centers.ToList(); var r = small.radii.ToList(); var st = small.targetStage.ToList();
            foreach (var h in holders.Distinct()) { var tube = h.Find("PipeTube"); if (!tube) continue; var rr = tube.GetComponent<Renderer>(); if (t.Contains(rr)) continue; t.Add(rr); c.Add(rr.bounds.center); r.Add(rr.bounds.extents.magnitude); st.Add(2); addN++; }
            small.targets = t.ToArray(); small.centers = c.ToArray(); small.radii = r.ToArray(); small.targetStage = st.ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(small); EditorUtility.SetDirty(small);
        }
        AssetDatabase.SaveAssets();
        string mm = FuwaMaterialMergeRun();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        return "[2–4 パイプ] 管にした " + n + " 本（もどした " + rv + "）・影の一覧の掃除 " + fixedRefs + "・SmallShadows に足す " + addN + "\n" + mm;
    }
    static int FuwaMaterialMergeRevert() { var m = typeof(FuwaMaterialMerge).GetMethod("Revert", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static); return m != null ? (int)m.Invoke(null, null) : -1; }
    static string FuwaMaterialMergeRun() { return FuwaMaterialMerge.Run(false); }
}
