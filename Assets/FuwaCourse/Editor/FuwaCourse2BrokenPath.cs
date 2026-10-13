using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// いわのみち：木の足場（BrokenBridge/Deck）から奥、洞窟（転がる岩の出口）までの黄色い道が割れて抜け落ち、下の岩肌が出ている
//（2026-10-12 てじゃ「うっかり直進する人がいるから、木の足場より上奥の黄色い道は割れて岩肌になってる感じに」）。
// 2026-10-13 てじゃ「道がグレーになった感じ。きっちり段差つけて割って」：前は道の面を岩の色に塗っただけだった。
// 今は割れ目から奥の道の板（見た目も当たり判定も）を消して、すぐ下（約0.34m下）にある山（Mountain）の岩肌を床にする。割れ目には道の断面（黄色の壁）を立てる。
// 割れ目の線は大きくゆるいでこぼこ（てじゃ「ぎざぎざもなめらかに」）。転がる岩はこの段差で跳ねて乗り越える（FixBoulders）。
// 元のメッシュは Backup/*_PreBroken に取っておき、毎回そこから作る（何度実行しても同じ結果）。
// 道を作り直した（FuwaCourse2Builder・FuwaIwaIslands.ApplySummitCut など）後は、PreBroken を作り直してからもう一度実行する
public static class FuwaCourse2BrokenPath
{
    const string MeshPath = "Assets/FuwaCourse/Meshes/Course2Path_v2_Bevel.asset";
    const string BackupPath = "Assets/FuwaCourse/Meshes/Backup/Course2Path_v2_Bevel_PreBroken.asset";
    const string ColPath = "Assets/FuwaCourse/Meshes/Course2Path_v2.asset";
    const string ColBackupPath = "Assets/FuwaCourse/Meshes/Backup/Course2Path_v2_PreBroken.asset";
    // 割れる所（ワールド座標）：x がこれより奥（洞窟側）。右奥（洞窟に向かって右＝z の小さい側）ほど手前まで割れている（2026-10-12 てじゃのイメージ）
    const float CutX = 98.6f, Slope = 0.55f, ZMid = 4f, Amp = 0.45f, MaxY = 12.5f, MinZ = -1.5f, MaxZ = 9.5f;
    // 割れるのは洞窟の奥まで（x < MaxX）。2026-10-13 てじゃ「頂上温泉後の岩柱のとこの道がおかしい」：これが無くて、頂上の先で道が 12.5m より下る所（岩柱〜下りの板の手前、x 119〜129）まで道の板が消えて山の地面だけになっていた
    const float MaxX = 112f;
    const float Seg = 0.2f;          // 割れ目の線はこの幅ごとの直線でつなぐ（なめらかな曲線の近似）
    const float BreakDepth = 0.42f;  // 割れ目の断面の壁の深さ。道の厚み0.3＋下の岩肌まで0.04＋岩肌に少し埋める
    static readonly Bounds BreakBox = new Bounds(new Vector3(100.5f, 10f, 3f), new Vector3(10f, 4f, 8f));

    // 割れ目の x（z ごと）：大きくゆるいでこぼこ（2026-10-13 てじゃ「ぎざぎざもなめらかに」。前は三角波のぎざぎざ）
    static float CutAt(float z)
    {
        float wave = 0.6f * Mathf.Sin(z * 1.7f + 0.6f) + 0.4f * Mathf.Sin(z * 3.3f + 2.2f);
        return CutX + Slope * (z - ZMid) + Amp * wave;
    }

    // 1＝割れて無くなる所、0＝道のまま
    static int Side(Vector3 w)
    {
        if (w.y >= MaxY || w.z <= MinZ || w.z >= MaxZ || w.x >= MaxX) return 0;
        return w.x > CutAt(w.z) ? 1 : 0;
    }

    [MenuItem("FuwaCourse/Course2/木の足場から奥の道を割れた岩肌にする")]
    public static void Menu() { Debug.Log(Run()); }

    static Mesh Backup(string path, string backup, string name)
    {
        var src = AssetDatabase.LoadAssetAtPath<Mesh>(backup);
        if (src != null) return src;
        if (!AssetDatabase.IsValidFolder("Assets/FuwaCourse/Meshes/Backup")) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "Backup");
        src = Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>(path)); src.name = name;
        AssetDatabase.CreateAsset(src, backup);
        return src;
    }

    public static string Run()
    {
        var pm = GameObject.Find("FuwaCourse2").transform.Find("PathMesh");
        var mr = pm.GetComponent<MeshRenderer>();
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        var src = Backup(MeshPath, BackupPath, "Course2Path_v2_Bevel_PreBroken");
        var colMesh = AssetDatabase.LoadAssetAtPath<Mesh>(ColPath);
        var colSrc = Backup(ColPath, ColBackupPath, "Course2Path_v2_PreBroken");
        var l2w = pm.localToWorldMatrix;

        // 割れ目の壁の下はしは「元の道の上面 - BreakDepth」。元の道の上面の高さは元の見た目のメッシュから
        var topGo = new GameObject("tmpTop"); topGo.transform.SetPositionAndRotation(pm.position, pm.rotation); topGo.transform.localScale = pm.lossyScale;
        var topCol = topGo.AddComponent<MeshCollider>(); topCol.sharedMesh = src; Physics.SyncTransforms();
        System.Func<Vector3, float> wallBottom = w =>
            topCol.Raycast(new Ray(new Vector3(w.x, w.y + 2f, w.z), Vector3.down), out var h, 4f) ? Mathf.Min(w.y, h.point.y - BreakDepth) : w.y - BreakDepth;

        // 見た目
        var (newV, newN, uv, hasUv, subs, removed) = CutMesh(src, l2w, 2);
        int breakWalls = CapOpenLoops(newV, newN, uv, hasUv, subs[0], subs, l2w, BreakBox, wallBottom);
        // 頂上の柱ゾーン手前の崩落（FuwaCourse2Collapse で道を切った所）の切り口にふたをする（2026-10-12 てじゃ「頂上付近の道の端が割れてる」）。
        // 切っただけだと切り口の断面が開いていて、横から見ると中が抜けて見えた。開いている辺の輪をふさぐ（見た目だけ。ふたは道と同じ黄色＝てじゃ「黄色でふさいでほしかった」）
        int capped = CapOpenLoops(newV, newN, uv, hasUv, subs[0], subs, l2w, new Bounds(new Vector3(121f, 12.5f, 5f), new Vector3(20f, 7f, 16f)), null);
        // 切り口の断面（縦の面）は、元の道の面取りの法線を引きずっていて、縦の筋のような明暗のむらが出ていた（2026-10-13 てじゃ「筋もあるしちょっときたなく」）。
        // 崩落の範囲の縦向きの三角形は、頂点を分けて、面の向き（水平）の法線にそろえる
        var (cutO, cutAx) = FuwaCourse2Collapse.Frame(pm.parent);
        FlattenCutNormals(newV, newN, uv, hasUv, subs, l2w, cutO, cutAx);
        mesh.Clear();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(newV); if (hasUv) mesh.SetUVs(0, uv);
        mesh.subMeshCount = subs.Length;
        for (int s = 0; s < subs.Length; s++) mesh.SetTriangles(subs[s], s);
        mesh.SetNormals(newN); mesh.RecalculateBounds();   // 法線は元のまま（ベベルのなめらかさを崩さない）
        EditorUtility.SetDirty(mesh);
        var mats = mr.sharedMaterials;
        mr.sharedMaterials = new[] { mats[0], mats.Length > 1 ? mats[1] : mats[0] };   // 前は3つ目に岩の色（道を塗っていた）
        EditorUtility.SetDirty(mr);

        // 当たり判定も同じ線で切って、割れ目に壁（転がる岩・ふわふわ・プレイヤーが段差に当たる）
        var (cV, cN, cUv, cHasUv, cSubs, cRemoved) = CutMesh(colSrc, l2w, colSrc.subMeshCount);
        int colWalls = CapOpenLoops(cV, cN, cUv, cHasUv, cSubs[0], cSubs, l2w, BreakBox, wallBottom);
        colMesh.Clear();
        colMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        colMesh.SetVertices(cV); colMesh.subMeshCount = cSubs.Length;
        for (int s = 0; s < cSubs.Length; s++) colMesh.SetTriangles(cSubs[s], s);
        colMesh.SetNormals(cN); colMesh.RecalculateBounds();
        EditorUtility.SetDirty(colMesh);
        Object.DestroyImmediate(topGo);
        var mc = pm.GetComponent<MeshCollider>(); if (mc != null && mc.sharedMesh == colMesh) { mc.sharedMesh = null; mc.sharedMesh = colMesh; }
        AssetDatabase.SaveAssets();
        Physics.SyncTransforms();
        string boulders = FixBoulders();
        string settled = SettleOnFloor(pm.parent);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pm.gameObject.scene);
        return "broken path removed tris " + removed + " (col " + cRemoved + "), break walls " + breakWalls + " (col " + colWalls + "), capped " + capped + ", " + boulders + settled;
    }

    // 道の板が無くなって床が下がった分、元の道の上に乗っていた物を下の岩肌に下ろす：
    // 洞窟の奥の暗がり（CaveDark）の底（元の道の高さのままだと入口に暗い縁が見えた）・洞窟の入口の柵（CaveBarrier）・小石（Deco_StonesSmall）
    static float Floor(Vector3 p)
    {
        float best = float.NaN;
        foreach (var h in Physics.RaycastAll(new Ray(new Vector3(p.x, p.y + 1.5f, p.z), Vector3.down), 4f))
            if ((h.collider.name == "PathMesh" || h.collider.name == "Mountain") && !h.collider.isTrigger && !(h.point.y <= best)) best = h.point.y;
        return best;
    }
    static string SnapDarkToRim(Transform dark, Transform mouth)
    {
        // アーチの内側のふち＝入口の岩のメッシュの開いている辺のうち、入口の面（x が 104.2〜104.5）にあって床より上の物
        var mm = mouth.GetComponent<MeshFilter>().sharedMesh; var ml2w = mouth.localToWorldMatrix;
        var MV = mm.vertices.Select(v => ml2w.MultiplyPoint3x4(v)).ToArray(); var mt = mm.triangles;
        var key = new Dictionary<Vector3Int, int>(); var id = new int[MV.Length];
        for (int i = 0; i < MV.Length; i++) { var k = new Vector3Int(Mathf.RoundToInt(MV[i].x * 1000f), Mathf.RoundToInt(MV[i].y * 1000f), Mathf.RoundToInt(MV[i].z * 1000f)); if (!key.TryGetValue(k, out int q)) { q = key.Count; key[k] = q; } id[i] = q; }
        var cnt = new Dictionary<long, int>(); var ed = new Dictionary<long, (int, int)>();
        for (int i = 0; i < mt.Length; i += 3) for (int e = 0; e < 3; e++)
            {
                int a = mt[i + e], b = mt[i + (e + 1) % 3]; int A = id[a], B = id[b]; if (A == B) continue;
                long kk = A < B ? ((long)A << 32) | (uint)B : ((long)B << 32) | (uint)A; cnt[kk] = cnt.TryGetValue(kk, out int c) ? c + 1 : 1; ed[kk] = (a, b);
            }
        var rim = new List<(Vector3 a, Vector3 b)>();
        foreach (var kv in cnt)
        {
            if (kv.Value != 1) continue; var (a, b) = ed[kv.Key]; Vector3 pa = MV[a], pb = MV[b];
            if (pa.x < 104.2f || pa.x > 104.5f || pb.x < 104.2f || pb.x > 104.5f) continue;
            if (Mathf.Abs(pa.y - pb.y) < 0.03f && Mathf.Max(pa.y, pb.y) < 10.2f) continue;   // 足の下はし（床ぎわ）は除く
            rim.Add((pa, pb));
        }
        if (rim.Count == 0) return " rim not found";
        float rimBottom = rim.Min(r => Mathf.Min(r.a.y, r.b.y));
        float cz = rim.Average(r => (r.a.z + r.b.z) * 0.5f);
        var m = dark.GetComponent<MeshFilter>().sharedMesh; var l2w = dark.localToWorldMatrix; var w2l = l2w.inverse;
        var vs = m.vertices; int n = 0;
        float frontX = vs.Min(v => l2w.MultiplyPoint3x4(v).x);
        for (int i = 0; i < vs.Length; i++)
        {
            var w = l2w.MultiplyPoint3x4(vs[i]); if (w.x > frontX + 0.15f) continue;   // 入口側のふちだけ
            var q = new Vector2(Mathf.Max(w.y, rimBottom), w.z);   // (y, z) の面で一番近いふちの点
            Vector3 best = Vector3.zero; float bd = float.MaxValue;
            foreach (var (ra, rb) in rim)
            {
                Vector2 a2 = new Vector2(ra.y, ra.z), b2 = new Vector2(rb.y, rb.z), ab = b2 - a2;
                float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(q - a2, ab) / ab.sqrMagnitude) : 0f;
                float d = (a2 + ab * t - q).sqrMagnitude; if (d < bd) { bd = d; best = Vector3.Lerp(ra, rb, t); }
            }
            // ふちの少しだけ外（岩の中）へ。下の方は横向き、天井は上向き
            Vector2 outDir = new Vector2(best.y - Mathf.Min(best.y, 11f), best.z - cz); outDir = outDir.sqrMagnitude > 1e-6f ? outDir.normalized : Vector2.right;
            var nw = new Vector3(best.x, w.y < rimBottom ? w.y : best.y + outDir.x * 0.005f, best.z + outDir.y * 0.005f);
            if ((nw - w).sqrMagnitude < 1e-8f) continue;
            vs[i] = w2l.MultiplyPoint3x4(nw); n++;
        }
        if (n > 0) { m.vertices = vs; m.RecalculateBounds(); EditorUtility.SetDirty(m); }
        return " dark→rim " + n;
    }
    const float CaveBarrierTop = 12.15f;   // 洞窟の入口のバリアの上はし（ワールドの y、元の高さ）
    static string SettleOnFloor(Transform root)
    {
        var sb = new System.Text.StringBuilder();
        var dark = root.Find("Rocks2/CaveDark");
        if (dark != null)
        {
            var m = dark.GetComponent<MeshFilter>().sharedMesh; var l2w = dark.localToWorldMatrix; var w2l = l2w.inverse;
            var vs = m.vertices; float minY = vs.Min(v => l2w.MultiplyPoint3x4(v).y);
            var mouth = l2w.MultiplyPoint3x4(vs.OrderBy(v => l2w.MultiplyPoint3x4(v).x).First());
            float floor = Floor(new Vector3(mouth.x - 0.3f, minY, dark.GetComponent<Renderer>().bounds.center.z));
            if (!float.IsNaN(floor))
            {
                int n = 0;
                for (int i = 0; i < vs.Length; i++) { var w = l2w.MultiplyPoint3x4(vs[i]); if (w.y < minY + 0.05f) { w.y = floor - 0.06f; vs[i] = w2l.MultiplyPoint3x4(w); n++; } }
                m.vertices = vs; m.RecalculateBounds(); EditorUtility.SetDirty(m);
                sb.Append(" cavedark " + n + " verts → " + (floor - 0.06f).ToString("F2"));
            }
        }
        // 洞窟の入口の岩（CaveMouth）のアーチの足の内側の下はしが、元の道の上面で止まっていて床から浮いていた（2026-10-13 てじゃ「洞窟の下が切れちゃった」）。
        // 床から浮いた（0.5mまで）低い頂点を床の少し下まで下ろす（2026-10-13 てじゃ「トンネルの前のぶぶんにすきま」：0.15m以上だけ下ろしたら、足の前の面の下が細くとがって床との間にすき間が見えた）
        var mouthT = root.Find("Rocks2/CaveMouth");
        if (mouthT != null)
        {
            var m = mouthT.GetComponent<MeshFilter>().sharedMesh; var l2w = mouthT.localToWorldMatrix; var w2l = l2w.inverse;
            var vs = m.vertices; int n = 0;
            for (int i = 0; i < vs.Length; i++)
            {
                var w = l2w.MultiplyPoint3x4(vs[i]); if (w.y > 10.45f) continue;
                float g = Floor(new Vector3(w.x, 10.6f, w.z)); if (float.IsNaN(g)) continue;
                float gap = w.y - g; if (gap < 0.005f || gap > 0.5f) continue;
                w.y = g - 0.04f; vs[i] = w2l.MultiplyPoint3x4(w); n++;
            }
            if (n > 0) { m.vertices = vs; m.RecalculateBounds(); EditorUtility.SetDirty(m); sb.Append(" cavemouth " + n + " verts"); }
        }
        // 洞窟の暗がり（CaveDark）の入口側のふちを、入口の岩（CaveMouth）のアーチの内側のふち（開いている辺）にぴったり合わせる。
        // 6cmほど外にずれていて、近くから斜めに見るとアーチのふちと暗がりの壁の間から外が透けた（2026-10-13 てじゃ「トンネルの前のぶぶんにすきま」）
        if (dark != null && mouthT != null) sb.Append(SnapDarkToRim(dark, mouthT));
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            if (r.name != "CaveBarrier" && r.name != "Deco_StonesSmall") continue;
            var b = r.bounds; if (b.center.x < 96f || b.center.x > 106f || b.center.z < 0.5f || b.center.z > 5f) continue;
            float g = Floor(new Vector3(b.center.x, b.max.y, b.center.z)); if (float.IsNaN(g)) continue;
            float want = g - (r.name == "CaveBarrier" ? 0.04f : 0.02f);
            if (r.name == "CaveBarrier")
            {
                // 近づくと出るバリア（縦の板）は、上はしは元の高さ（洞窟の天井）のまま、下はしだけ床まで伸ばす（2026-10-13 てじゃ「バリアも上がきれてる」：前は丸ごと下げていた）
                float h = CaveBarrierTop - want;
                if (Mathf.Abs(b.min.y - want) < 0.005f && Mathf.Abs(b.max.y - CaveBarrierTop) < 0.005f) continue;
                Undo.RecordObject(r.transform, "settle");
                var s = r.transform.localScale; s.y = h / r.transform.parent.lossyScale.y; r.transform.localScale = s;
                var p = r.transform.position; p.y = (want + CaveBarrierTop) * 0.5f; r.transform.position = p;
                sb.Append(" CaveBarrier " + want.ToString("F2") + "〜" + CaveBarrierTop.ToString("F2"));
                continue;
            }
            if (Mathf.Abs(b.min.y - want) < 0.005f) continue;
            Undo.RecordObject(r.transform, "settle"); r.transform.position += Vector3.up * (want - b.min.y);
            sb.Append(" " + r.name + " " + (want - b.min.y).ToString("F2"));
        }
        return sb.ToString();
    }

    // src を割れ目の線でぴったり切って、奥（Side==1）を捨てる。subCount 個までのサブメッシュを返す
    static (List<Vector3>, List<Vector3>, List<Vector2>, bool, List<int>[], int) CutMesh(Mesh src, Matrix4x4 l2w, int subCount)
    {
        var verts = src.vertices; var norms = src.normals;
        if (norms.Length != verts.Length) norms = new Vector3[verts.Length];
        var newV = new List<Vector3>(verts); var newN = new List<Vector3>(norms);
        var uv = new List<Vector2>(); src.GetUVs(0, uv); bool hasUv = uv.Count == verts.Length;
        var subs = new List<int>[subCount]; for (int s = 0; s < subCount; s++) subs[s] = new List<int>();
        int removed = 0;
        // ふちにかかる三角形は、割れ目の線でぴったり切る（割れ目は z の Seg 幅ごとに直線）。
        // 切った多角形は扇形に三角形にする。となりの三角形も同じ線で切るので、すき間はできない
        int AddV(Vector3 lp, Vector3 n, Vector2 t)
        {
            newV.Add(lp); newN.Add(n); if (hasUv) uv.Add(t); return newV.Count - 1;
        }
        void Emit(List<int> poly, int sub)
        {
            if (poly.Count < 3) return;
            Vector3 c = Vector3.zero; foreach (int v in poly) c += l2w.MultiplyPoint3x4(newV[v]); c /= poly.Count;
            if (Side(c) == 1) { removed += poly.Count - 2; return; }
            for (int i = 1; i + 1 < poly.Count; i++) { subs[sub].Add(poly[0]); subs[sub].Add(poly[i]); subs[sub].Add(poly[i + 1]); }
        }
        // poly を f(w) >= 0 側と反対側に分ける（どちらも返す）
        void Split(List<int> poly, System.Func<Vector3, float> f, List<int> pos, List<int> neg)
        {
            int n = poly.Count;
            for (int i = 0; i < n; i++)
            {
                int a = poly[i], b2 = poly[(i + 1) % n];
                Vector3 wa = l2w.MultiplyPoint3x4(newV[a]), wb = l2w.MultiplyPoint3x4(newV[b2]);
                float fa = f(wa), fb = f(wb);
                if (fa >= 0f) pos.Add(a); else neg.Add(a);
                if ((fa >= 0f) != (fb >= 0f))
                {
                    float t = fa / (fa - fb);
                    var nv = AddV(Vector3.Lerp(newV[a], newV[b2], t), Vector3.Lerp(newN[a], newN[b2], t).normalized, hasUv ? Vector2.Lerp(uv[a], uv[b2], t) : Vector2.zero);
                    pos.Add(nv); neg.Add(nv);
                }
            }
        }
        for (int s = 0; s < src.subMeshCount && s < subCount; s++)
        {
            var tris = src.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 w0 = l2w.MultiplyPoint3x4(verts[tris[i]]), w1 = l2w.MultiplyPoint3x4(verts[tris[i + 1]]), w2 = l2w.MultiplyPoint3x4(verts[tris[i + 2]]);
                Vector3 cen = (w0 + w1 + w2) / 3f;
                float zmin = Mathf.Min(w0.z, Mathf.Min(w1.z, w2.z)), zmax = Mathf.Max(w0.z, Mathf.Max(w1.z, w2.z));
                float xmin = Mathf.Min(w0.x, Mathf.Min(w1.x, w2.x)), xmax = Mathf.Max(w0.x, Mathf.Max(w1.x, w2.x));
                bool inBox = cen.y < MaxY && cen.z > MinZ && cen.z < MaxZ;
                float lo = CutX + Slope * (zmin - ZMid) - Amp - 0.1f, hi = CutX + Slope * (zmax - ZMid) + Amp + 0.1f;
                var tri = new List<int> { tris[i], tris[i + 1], tris[i + 2] };
                if (!inBox || xmax < lo || xmin > hi) { Emit(tri, s); continue; }
                // z の区間で切る
                var pieces = new List<List<int>> { tri };
                for (float zk = Mathf.Floor(zmin / Seg) * Seg + Seg; zk < zmax; zk += Seg)
                {
                    float zz = zk; var next = new List<List<int>>();
                    foreach (var pc in pieces) { var a = new List<int>(); var bb = new List<int>(); Split(pc, w => w.z - zz, a, bb); if (a.Count >= 3) next.Add(a); if (bb.Count >= 3) next.Add(bb); }
                    pieces = next;
                }
                // 区間ごとに割れ目の直線で切る
                foreach (var pc in pieces)
                {
                    Vector3 pc0 = Vector3.zero; foreach (int v in pc) pc0 += l2w.MultiplyPoint3x4(newV[v]); pc0 /= pc.Count;
                    float z0 = Mathf.Floor(pc0.z / Seg) * Seg, z1 = z0 + Seg;
                    float x0 = CutAt(z0), x1 = CutAt(z1);
                    System.Func<Vector3, float> f = w => w.x - (x0 + (x1 - x0) * (w.z - z0) / Seg);
                    var a = new List<int>(); var bb = new List<int>(); Split(pc, f, a, bb);
                    Emit(a, s); Emit(bb, s);
                }
            }
        }
        return (newV, newN, uv, hasUv, subs, removed);
    }

    // 転がる岩（FuwaBoulder）の通り道を、新しい床（割れた所は下の岩肌）の高さに合わせ、割れ目の段差で跳ねて乗り越えるようにする。
    // 通り道の点の高さは床に当てて決め直し、段差の前後の点は毎回作り直す（何度実行してもよい）
    const float HopBefore = 0.9f, HopAfter = 1.5f, HopHeight = 0.35f;
    static string FixBoulders()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var b in Object.FindObjectsOfType<FuwaBoulder>(true))
        {
            if (b.path == null || b.path.Length < 2) continue;
            float r = b.radius;
            float Ground(Vector3 p)
            {
                float best = float.NaN;
                foreach (var h in Physics.RaycastAll(new Ray(new Vector3(p.x, p.y + 1.5f, p.z), Vector3.down), 4f))
                    if ((h.collider.name == "PathMesh" || h.collider.name == "Mountain") && !h.collider.isTrigger && !(h.point.y <= best)) best = h.point.y;
                return best;
            }
            var pts = new List<Vector3>(b.path);
            // 床の高さに合わせる（床が見つからない洞窟の中は、出口の床の高さ）
            var ys = new float[pts.Count];
            for (int i = 0; i < pts.Count; i++) ys[i] = Ground(pts[i]);
            for (int i = 0; i < pts.Count; i++) if (float.IsNaN(ys[i])) for (int k = i + 1; k < pts.Count; k++) if (!float.IsNaN(ys[k])) { ys[i] = ys[k]; break; }
            for (int i = 1; i < pts.Count; i++) if (float.IsNaN(ys[i])) ys[i] = ys[i - 1];
            for (int i = 0; i < pts.Count; i++) if (!float.IsNaN(ys[i])) pts[i] = new Vector3(pts[i].x, ys[i] + r, pts[i].z);
            // 段差の位置（割れ目の線を、転がる向きに横切る所）
            int cross = -1;
            for (int i = 1; i < pts.Count; i++) if (pts[i - 1].x > CutAt(pts[i - 1].z) && pts[i].x <= CutAt(pts[i].z)) { cross = i; break; }
            if (cross < 0) { b.path = pts.ToArray(); EditorUtility.SetDirty(b); sb.Append(b.name + " no step; "); continue; }
            Vector3 pa = pts[cross - 1], pb = pts[cross];
            float fa = pa.x - CutAt(pa.z), fb = pb.x - CutAt(pb.z); float t = fa / (fa - fb);
            Vector3 edge = Vector3.Lerp(pa, pb, t);
            Vector3 dir = pb - pa; dir.y = 0f; dir.Normalize();
            float DistAlong(Vector3 p) { var d = p - edge; d.y = 0f; return Vector3.Dot(d, dir); }
            // 段差の前後（HopBefore 手前〜HopAfter 先）の点を消して、跳ねる弧を入れ直す
            var keep = new List<Vector3>(); int insertAt = -1;
            for (int i = 0; i < pts.Count; i++)
            {
                float da = DistAlong(pts[i]);
                if (da >= -HopBefore - 0.05f && da <= HopAfter + 0.05f) { if (insertAt < 0) insertAt = keep.Count; continue; }
                keep.Add(pts[i]);
            }
            if (insertAt < 0) insertAt = keep.Count;
            Vector3 p0 = edge - dir * HopBefore, p1 = edge + dir * HopAfter;
            float g0 = Ground(p0), g1 = Ground(p1);
            p0.y = (float.IsNaN(g0) ? edge.y - r - 0.34f : g0) + r; p1.y = (float.IsNaN(g1) ? edge.y - r : g1) + r;
            // 低い床から段差の上へ：段差のふちに当たって持ち上がり、少し浮いて着地（二次ベジェ）
            float ctrl = Mathf.Max(p0.y, p1.y) + HopHeight * 2f;
            var arc = new List<Vector3>();
            const int N = 10;
            for (int k = 0; k <= N; k++)
            {
                float u = k / (float)N;
                Vector3 q = Vector3.Lerp(p0, p1, u);
                q.y = (1f - u) * (1f - u) * p0.y + 2f * u * (1f - u) * ctrl + u * u * p1.y;
                arc.Add(q);
            }
            keep.InsertRange(insertAt, arc);
            b.path = keep.ToArray(); EditorUtility.SetDirty(b);
            sb.Append(b.name + " step at " + edge.ToString("F2") + " low " + p0.y.ToString("F2") + " high " + p1.y.ToString("F2") + "; ");
        }
        return sb.ToString();
    }

    // 崩落の谷の切り口の面（谷を横切る向き ±axis を向いた縦の面）だけ。道の横の面（面取り）は触らない（触ると角が剥がれたように見えた。2026-10-13 てじゃ）
    static void FlattenCutNormals(List<Vector3> V, List<Vector3> N, List<Vector2> UV, bool hasUv, List<int>[] subs, Matrix4x4 l2w, Vector3 cutO, Vector3 cutAx)
    {
        var w2l = l2w.inverse;
        foreach (var list in subs)
            for (int i = 0; i < list.Count; i += 3)
            {
                Vector3 a = l2w.MultiplyPoint3x4(V[list[i]]), b = l2w.MultiplyPoint3x4(V[list[i + 1]]), c = l2w.MultiplyPoint3x4(V[list[i + 2]]);
                Vector3 ctr = (a + b + c) / 3f; Vector3 d = ctr - cutO;
                float sAlong = Vector3.Dot(d, cutAx), lSide = Vector3.Dot(d, new Vector3(-cutAx.z, 0f, cutAx.x));
                if (sAlong < -1.5f || sAlong > 4.5f || Mathf.Abs(lSide) > 3.5f || ctr.y < 9.5f) continue;
                Vector3 fn = Vector3.Cross(b - a, c - a); if (fn.sqrMagnitude < 1e-10f) continue; fn.Normalize();
                if (Mathf.Abs(fn.y) > 0.35f) continue;   // 上面・下面はそのまま
                fn.y = 0f; fn.Normalize();
                if (Mathf.Abs(Vector3.Dot(fn, cutAx)) < 0.6f) continue;   // 道の横の面はそのまま
                Vector3 ln = w2l.MultiplyVector(fn).normalized;
                for (int k = 0; k < 3; k++)
                {
                    int vi = list[i + k]; int ni = V.Count;
                    V.Add(V[vi]); N.Add(ln); if (hasUv) UV.Add(UV[vi]);
                    list[i + k] = ni;
                }
            }
    }

    const float WallDepth = 0.3f;   // 道の厚み（FuwaCourse2Builder.Thick）
    // 開いている辺（三角形1枚にしか使われていない辺）のうち box の中のものに、断面の壁（ふた）を足す。足した壁の数を返す。
    // 切り口は輪になっていない（道の上面のふち・下面のふち・角の丸み、とばらばらに開いている）ので、
    // 上面と角の開いた辺を、真下の下面の高さ（近くの下面の開いた辺の頂点）までのばした縦の壁にする
    // bottom を渡すと、壁の下はしをその高さ（ワールドの y、上はしの点ごと）に。null なら上はしから道の厚みぶん下
    static int CapOpenLoops(List<Vector3> V, List<Vector3> N, List<Vector2> UV, bool hasUv, List<int> dst, List<int>[] all, Matrix4x4 l2w, Bounds box, System.Func<Vector3, float> bottom)
    {
        var id = new int[V.Count]; var key = new Dictionary<Vector3Int, int>();
        for (int i = 0; i < V.Count; i++)
        {
            var k = new Vector3Int(Mathf.RoundToInt(V[i].x * 1000f), Mathf.RoundToInt(V[i].y * 1000f), Mathf.RoundToInt(V[i].z * 1000f));
            if (!key.TryGetValue(k, out int q)) { q = key.Count; key[k] = q; }
            id[i] = q;
        }
        var count = new Dictionary<long, int>(); var dir = new Dictionary<long, (int a, int b, int c)>();
        long K(int a, int b) { return a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a; }
        foreach (var list in all)
            for (int i = 0; i < list.Count; i += 3)
                for (int e = 0; e < 3; e++)
                {
                    int va = list[i + e], vb = list[i + (e + 1) % 3]; int a = id[va], b = id[vb]; if (a == b) continue;
                    long kk = K(a, b); count[kk] = count.TryGetValue(kk, out int c) ? c + 1 : 1; dir[kk] = (va, vb, list[i + (e + 2) % 3]);
                }
        var upper = new List<(Vector3 a, Vector3 b)>(); var lower = new List<Vector3>();
        foreach (var kv in count)
        {
            if (kv.Value != 1) continue;
            var (va, vb, vc) = dir[kv.Key];
            Vector3 wa = l2w.MultiplyPoint3x4(V[va]), wb = l2w.MultiplyPoint3x4(V[vb]), wc = l2w.MultiplyPoint3x4(V[vc]);
            if (!box.Contains((wa + wb) * 0.5f)) continue;
            Vector3 fn = Vector3.Cross(wb - wa, wc - wa).normalized;
            if (fn.y < -0.7f) { lower.Add(wa); lower.Add(wb); }
            else upper.Add((V[va], V[vb]));   // 三角形の向き a→b。壁は b→a の向き
        }
        if (lower.Count == 0 && bottom == null) return 0;
        var w2l = l2w.inverse;
        Vector3 Down(Vector3 local)
        {
            Vector3 w = l2w.MultiplyPoint3x4(local); float best = float.MaxValue; float y = w.y - 0.5f;
            foreach (var p in lower) { float d = (p.x - w.x) * (p.x - w.x) + (p.z - w.z) * (p.z - w.z); if (d < best) { best = d; y = p.y; } }
            return w2l.MultiplyPoint3x4(new Vector3(w.x, Mathf.Min(y, w.y), w.z));
        }
        int walls = 0;
        foreach (var (ua, ub) in upper)
        {
            Vector3 a = ua, b = ub;
            Vector3 a2 = Down(a), b2 = Down(b);
            if (bottom == null && (a - a2).sqrMagnitude < 1e-6f && (b - b2).sqrMagnitude < 1e-6f) continue;
            // 壁の下はしは、上はしから道の厚みぶん下にそろえる（近くの下面の頂点まで伸ばすと、ところどころ長く垂れて「黄色がたれて」見えた。2026-10-13 てじゃ）。
            // となりの壁とは横に少し重ねて髪の毛くらいのすき間を消す（2026-10-13 てじゃ「つなぎ目に隙間」）
            Vector3 wa = l2w.MultiplyPoint3x4(a), wb = l2w.MultiplyPoint3x4(b);
            a2 = w2l.MultiplyPoint3x4(new Vector3(wa.x, bottom != null ? bottom(wa) : wa.y - WallDepth, wa.z));
            b2 = w2l.MultiplyPoint3x4(new Vector3(wb.x, bottom != null ? bottom(wb) : wb.y - WallDepth, wb.z));
            if (bottom != null && (a - a2).sqrMagnitude < 1e-6f && (b - b2).sqrMagnitude < 1e-6f) continue;
            Vector3 along = (a - b).normalized * 0.012f;
            a += along; b -= along; a2 += along; b2 -= along;
            // 法線は水平に（縦の筋のような明暗のむらが出ないように）
            Vector3 n = Vector3.Cross(a - b, b2 - b); n.y = 0f; n = n.sqrMagnitude > 1e-8f ? n.normalized : Vector3.Cross(a - b, b2 - b).normalized;
            int i0 = V.Count;
            V.Add(b); V.Add(a); V.Add(a2); V.Add(b2);
            for (int k = 0; k < 4; k++) { N.Add(n); if (hasUv) UV.Add(Vector2.zero); }
            dst.Add(i0); dst.Add(i0 + 1); dst.Add(i0 + 2); dst.Add(i0); dst.Add(i0 + 2); dst.Add(i0 + 3);
            walls++;
        }
        return walls;
    }
}
