using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// いわのみち：木の足場（BrokenBridge/Deck）から奥、洞窟（転がる岩の出口）までの黄色い道を、割れて岩肌が出ている見た目にする
//（2026-10-12 てじゃ「うっかり直進する人がいるから、木の足場より上奥の黄色い道は割れて岩肌になってる感じに」）。
// 見た目のメッシュ（Course2Path_v2_Bevel）だけ変える。当たり判定（Course2Path_v2）はそのままなので、転がる岩の通り道は変わらない。
// 元のメッシュは Backup/Course2Path_v2_Bevel_PreBroken に取っておき、毎回そこから作る（何度実行しても同じ結果）。
// 道を作り直した（FuwaCourse2Builder など）後はもう一度実行する
public static class FuwaCourse2BrokenPath
{
    const string MeshPath = "Assets/FuwaCourse/Meshes/Course2Path_v2_Bevel.asset";
    const string BackupPath = "Assets/FuwaCourse/Meshes/Backup/Course2Path_v2_Bevel_PreBroken.asset";
    // 割れる所（ワールド座標）：x がこれより奥（洞窟側）。ふちは z に沿ってぎざぎざ
    // 2026-10-12 てじゃのイメージ（赤線）：ぎざぎざの割れ目、右奥（洞窟に向かって右＝z の小さい側）ほど多めに削れる
    const float CutX = 98.6f, Slope = 0.55f, ZMid = 4f, Tooth = 1.4f, Amp = 0.6f, EndX = 104.9f, MaxY = 12.5f, MinZ = -1.5f, MaxZ = 9.5f;
    const int Depth = 6;

    // 割れ目の x（z ごと）：右（z 小）ほど手前。ぎざぎざは三角波（歯ごとに少し大きさを変える）
    static float CutAt(float z)
    {
        float u = z / Tooth; float f = u - Mathf.Floor(u);
        float tri = 1f - 4f * Mathf.Abs(f - 0.5f);                 // -1..1 のとがった波
        float k = 0.75f + 0.5f * Mathf.PerlinNoise(Mathf.Floor(u) * 0.73f, 0.31f);
        return CutX + Slope * (z - ZMid) + Amp * k * tri;
    }

    // 1＝岩にする所、0＝黄色のまま
    static int Side(Vector3 w)
    {
        if (w.x >= EndX || w.y >= MaxY || w.z <= MinZ || w.z >= MaxZ) return 0;
        return w.x > CutAt(w.z) ? 1 : 0;
    }

    [MenuItem("FuwaCourse/Course2/木の足場から奥の道を割れた岩肌にする")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var pm = GameObject.Find("FuwaCourse2").transform.Find("PathMesh");
        var mf = pm.GetComponent<MeshFilter>();
        var mr = pm.GetComponent<MeshRenderer>();
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        var src = AssetDatabase.LoadAssetAtPath<Mesh>(BackupPath);
        if (src == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/FuwaCourse/Meshes/Backup")) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "Backup");
            src = Object.Instantiate(mesh); src.name = "Course2Path_v2_Bevel_PreBroken";
            AssetDatabase.CreateAsset(src, BackupPath);
        }
        var l2w = pm.localToWorldMatrix;
        var verts = src.vertices; var norms = src.normals;
        var newV = new List<Vector3>(verts); var newN = new List<Vector3>(norms);
        var uv = new List<Vector2>(); src.GetUVs(0, uv); bool hasUv = uv.Count == verts.Length;
        var subs = new List<int>[3]; for (int s = 0; s < 3; s++) subs[s] = new List<int>();
        int moved = 0;
        // ふちにかかる三角形は、割れ目の線でぴったり切る（割れ目は z の区間ごとに直線＝三角波の山・谷で区切る）。
        // 切った多角形は扇形に三角形にする。となりの三角形も同じ線で切るので、すき間はできない
        var w2lc = l2w.inverse;
        int AddV(Vector3 lp, Vector3 n, Vector2 t)
        {
            newV.Add(lp); newN.Add(n); if (hasUv) uv.Add(t); return newV.Count - 1;
        }
        void Emit(List<int> poly, int sub)
        {
            if (poly.Count < 3) return;
            Vector3 c = Vector3.zero; foreach (int v in poly) c += l2w.MultiplyPoint3x4(newV[v]); c /= poly.Count;
            bool rock = Side(c) == 1;
            var dst = rock ? subs[2] : subs[sub];
            for (int i = 1; i + 1 < poly.Count; i++) { dst.Add(poly[0]); dst.Add(poly[i]); dst.Add(poly[i + 1]); if (rock) moved++; }
        }
        // poly を「dot(w, n) - d >= 0」側と反対側に分ける（どちらも返す）
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
        float half = Tooth * 0.5f;
        for (int s = 0; s < src.subMeshCount && s < 2; s++)
        {
            var tris = src.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 w0 = l2w.MultiplyPoint3x4(verts[tris[i]]), w1 = l2w.MultiplyPoint3x4(verts[tris[i + 1]]), w2 = l2w.MultiplyPoint3x4(verts[tris[i + 2]]);
                Vector3 cen = (w0 + w1 + w2) / 3f;
                float zmin = Mathf.Min(w0.z, Mathf.Min(w1.z, w2.z)), zmax = Mathf.Max(w0.z, Mathf.Max(w1.z, w2.z));
                float xmin = Mathf.Min(w0.x, Mathf.Min(w1.x, w2.x)), xmax = Mathf.Max(w0.x, Mathf.Max(w1.x, w2.x));
                bool inBox = cen.x < EndX && cen.y < MaxY && cen.z > MinZ && cen.z < MaxZ;
                float lo = CutX + Slope * (zmin - ZMid) - Amp * 1.3f - 0.1f, hi = CutX + Slope * (zmax - ZMid) + Amp * 1.3f + 0.1f;
                var tri = new List<int> { tris[i], tris[i + 1], tris[i + 2] };
                if (!inBox || xmax < lo || xmin > hi) { Emit(tri, s); continue; }
                // z の区間（三角波の折れ目）で切る
                var pieces = new List<List<int>> { tri };
                for (float zk = Mathf.Floor(zmin / half) * half + half; zk < zmax; zk += half)
                {
                    float zz = zk; var next = new List<List<int>>();
                    foreach (var pc in pieces) { var a = new List<int>(); var bb = new List<int>(); Split(pc, w => w.z - zz, a, bb); if (a.Count >= 3) next.Add(a); if (bb.Count >= 3) next.Add(bb); }
                    pieces = next;
                }
                // 区間ごとに割れ目の直線で切る
                foreach (var pc in pieces)
                {
                    Vector3 pc0 = Vector3.zero; foreach (int v in pc) pc0 += l2w.MultiplyPoint3x4(newV[v]); pc0 /= pc.Count;
                    float z0 = Mathf.Floor(pc0.z / half) * half, z1 = z0 + half;
                    float x0 = CutAt(z0 + 1e-4f), x1 = CutAt(z1 - 1e-4f);
                    System.Func<Vector3, float> f = w => w.x - (x0 + (x1 - x0) * (w.z - z0) / half);
                    var a = new List<int>(); var bb = new List<int>(); Split(pc, f, a, bb);
                    Emit(a, s); Emit(bb, s);
                }
            }
        }
        // 岩の部分は頂点を分けて、少しでこぼこ（割れた感じ）に。当たり判定は別メッシュなので見た目だけ（±4cm、岩が浮いて見えない程度）
        var w2l = l2w.inverse; var remap = new Dictionary<int, int>();
        for (int i = 0; i < subs[2].Count; i++)
        {
            int vi = subs[2][i];
            if (!remap.TryGetValue(vi, out int ni))
            {
                ni = newV.Count; remap[vi] = ni;
                Vector3 w = l2w.MultiplyPoint3x4(newV[vi]);
                float n = Mathf.PerlinNoise(w.x * 1.7f, w.z * 1.7f) - 0.5f;
                w += Vector3.up * n * 0.08f;
                newV.Add(w2l.MultiplyPoint3x4(w)); newN.Add(newN[vi]); if (hasUv) uv.Add(uv[vi]);
            }
            subs[2][i] = ni;
        }
        int capped = 0;
        // 頂上の柱ゾーン手前の崩落（FuwaCourse2Collapse で道を切った所）の切り口にふたをする（2026-10-12 てじゃ「頂上付近の道の端が割れてる」）。
        // 切っただけだと切り口の断面が開いていて、横から見ると中が抜けて見えた。開いている辺の輪を岩のふたでふさぐ（見た目だけ）
        capped = CapOpenLoops(newV, newN, uv, hasUv, subs[0], subs,   /* ふたは道と同じ黄色（てじゃ「黄色でふさいでほしかった」） */ l2w, new Bounds(new Vector3(121f, 12.5f, 5f), new Vector3(20f, 7f, 16f)));
        mesh.Clear();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(newV); if (hasUv) mesh.SetUVs(0, uv);
        mesh.subMeshCount = 3;
        for (int s = 0; s < 3; s++) mesh.SetTriangles(subs[s], s);
        mesh.SetNormals(newN); mesh.RecalculateBounds();   // 法線は元のまま（ベベルのなめらかさを崩さない）
        EditorUtility.SetDirty(mesh);
        var rockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Rock.mat");
        var mats = mr.sharedMaterials;
        mr.sharedMaterials = new[] { mats[0], mats.Length > 1 ? mats[1] : mats[0], rockMat };
        EditorUtility.SetDirty(mr);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pm.gameObject.scene);
        return "broken path tris " + moved + ", capped loops " + capped;
    }
    // 開いている辺（三角形1枚にしか使われていない辺）のうち box の中のものに、断面の壁（ふた）を足す。足した壁の数を返す。
    // 切り口は輪になっていない（道の上面のふち・下面のふち・角の丸み、とばらばらに開いている）ので、
    // 上面と角の開いた辺を、真下の下面の高さ（近くの下面の開いた辺の頂点）までのばした縦の壁にする
    static int CapOpenLoops(List<Vector3> V, List<Vector3> N, List<Vector2> UV, bool hasUv, List<int> dst, List<int>[] all, Matrix4x4 l2w, Bounds box)
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
        if (lower.Count == 0) return 0;
        var w2l = l2w.inverse;
        Vector3 Down(Vector3 local)
        {
            Vector3 w = l2w.MultiplyPoint3x4(local); float best = float.MaxValue; float y = w.y - 0.5f;
            foreach (var p in lower) { float d = (p.x - w.x) * (p.x - w.x) + (p.z - w.z) * (p.z - w.z); if (d < best) { best = d; y = p.y; } }
            return w2l.MultiplyPoint3x4(new Vector3(w.x, Mathf.Min(y, w.y), w.z));
        }
        int walls = 0;
        foreach (var (a, b) in upper)
        {
            Vector3 a2 = Down(a), b2 = Down(b);
            if ((a - a2).sqrMagnitude < 1e-6f && (b - b2).sqrMagnitude < 1e-6f) continue;
            Vector3 n = Vector3.Cross(a - b, b2 - b).normalized;
            int i0 = V.Count;
            V.Add(b); V.Add(a); V.Add(a2); V.Add(b2);
            for (int k = 0; k < 4; k++) { N.Add(n); if (hasUv) UV.Add(Vector2.zero); }
            dst.Add(i0); dst.Add(i0 + 1); dst.Add(i0 + 2); dst.Add(i0); dst.Add(i0 + 2); dst.Add(i0 + 3);
            walls++;
        }
        return walls;
    }
}
