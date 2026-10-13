using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// いわのみち：頂上の柱ゾーン手前の崩落（谷）の、手前の切り口から突き出た足場（FuwaCourse2Collapse.Tongue で道の切り口そのものを伸ばした所）の下を、岩でささえる見た目
//（2026-10-13 てじゃ「黄色い道をこの赤枠の分伸ばして」。別の板を乗せたら「とってつけた感がすごい」→ 切り口の形を変えて、下は谷底から足場の裏まで岩がつながっている形に）。
// 足場の形（Tongue）をそのまま下へ押し出した岩の柱（少し内側・下ほど少し太く・でこぼこ）。足場の裏の切り口の壁やすき間が見えないように、裏まで岩でふさぐ
//（2026-10-13 てじゃ「つなぎ目に隙間」：前は岩を4つ置いただけで、足場の裏の切り口の壁がばらばらに見えていた）。
// 見た目だけ（当たり判定なし）。何度実行してもよい
public static class FuwaCourse2Ledge
{
    static readonly Vector3 OldRootPos = new Vector3(44.312717f, 1.5f, 16.566227f);
    static readonly Quaternion OldRootRot = new Quaternion(0f, 0.6234898f, 0f, 0.78183156f);
    static readonly Vector3 OldOrigin = new Vector3(100f, 0f, 0.95f);
    static readonly Vector3 OldAxis = new Vector3(1f, 0f, 0.08f);
    const string MeshPath = "Assets/FuwaCourse/Meshes/Course2_LedgeRock.asset";

    [MenuItem("FuwaCourse/いわのみち：崩落の谷の足場の下の岩を作り直す")]
    public static void Menu()
    {
        Debug.Log(Run());
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    public static string Run()
    {
        var root = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaCourse2" && t.parent == null);
        var map = root.localToWorldMatrix * Matrix4x4.TRS(OldRootPos, OldRootRot, Vector3.one).inverse;
        Vector3 origin = map.MultiplyPoint3x4(OldOrigin);
        Vector3 axis = map.MultiplyVector(OldAxis.normalized); axis.y = 0f; axis.Normalize();
        Vector3 side = new Vector3(-axis.z, 0f, axis.x);   // CutJagged と同じ向き
        foreach (var n in new[] { "CollapseLedge", "CollapseLedgeRocks" }) { var o = root.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }

        // 足場の上面の高さ（道の見た目のメッシュから）
        var path = root.Find("PathMesh");
        float topY = 14.27f;
        {
            var pm = path.GetComponent<MeshFilter>().sharedMesh; var l2w = path.localToWorldMatrix; float best = float.MinValue;
            foreach (var v in pm.vertices)
            {
                var wv = l2w.MultiplyPoint3x4(v); var d = wv - origin; float ss = Vector3.Dot(d, axis), ll = Vector3.Dot(d, side);
                if (ss < 0.2f || ss > 0.8f || ll < 0f || ll > 1f || wv.y < 13.5f || wv.y > 15f) continue;
                best = Mathf.Max(best, wv.y);
            }
            if (best > float.MinValue) topY = best;
        }
        float rockTop = topY - 0.3f;            // 足場の裏（道の厚み0.3）
        const float rockBottom = 9.8f;          // 谷底より少し下
        const float inset = 0.1f;               // 足場のふちより少し内側（足場が少し張り出す）
        const float flare = 0.35f;              // 下ほど外へ広がる
        // 輪郭：足場の先（Tongue）に沿った点を、横の左→右に。うしろは切り口の奥（崖の中）
        float l0 = FuwaCourse2Collapse.TongueL0 - FuwaCourse2Collapse.TongueShoulder + 0.15f, l1 = FuwaCourse2Collapse.TongueL1 + FuwaCourse2Collapse.TongueShoulder - 0.15f;
        const float back = -0.6f;
        var outline = new List<Vector2>();     // (s, l)
        outline.Add(new Vector2(back, l0));
        for (float l = l0; l <= l1 + 0.001f; l += 0.08f) outline.Add(new Vector2(Mathf.Max(back + 0.2f, FuwaCourse2Collapse.NearEdge(l) - inset), l));
        outline.Add(new Vector2(back, l1));
        int nO = outline.Count;
        // 中心（外へ広げる向きを決める）
        Vector2 cen = Vector2.zero; foreach (var p in outline) cen += p; cen /= nO;
        // 縦の段
        const int rows = 8;
        var verts = new List<Vector3>(); var tris = new List<int>();
        var rnd = new System.Random(7731);
        float N(int i, int r) { return (float)new System.Random(i * 131 + r * 7919 + 17).NextDouble() - 0.5f; }
        for (int r = 0; r <= rows; r++)
        {
            float t = r / (float)rows;                 // 0=下 1=上
            float y = Mathf.Lerp(rockBottom, rockTop, t);
            float grow = flare * (1f - t) * (1f - t);
            for (int i = 0; i < nO; i++)
            {
                var p = outline[i];
                var dir = (p - cen); if (dir.sqrMagnitude > 1e-6f) dir.Normalize();
                bool backPt = p.x <= back + 0.001f;
                // 上の方は足場のふちより外へ出ない（出ると道の上に岩が見える）。下ほどでこぼこ・広がる
                float bump = backPt ? 0f : (t > 0.7f ? -Mathf.Abs(N(i, r)) * 0.15f : N(i, r) * 0.18f);
                var q = p + dir * (backPt ? 0f : grow + bump);
                Vector3 w = origin + axis * q.x + side * q.y; w.y = y + (r > 0 && r < rows ? N(i + 999, r) * 0.15f : 0f);
                verts.Add(root.InverseTransformPoint(w));
            }
        }
        for (int r = 0; r < rows; r++)
            for (int i = 0; i < nO - 1; i++)
            {
                int a = r * nO + i, b = a + 1, c = a + nO, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
        // 上のふた（足場の裏にくっつく）
        {
            int topRow = rows * nO; int ci = verts.Count;
            Vector3 cw = origin + axis * cen.x + side * cen.y; cw.y = rockTop; verts.Add(root.InverseTransformPoint(cw));
            for (int i = 0; i < nO - 1; i++) tris.AddRange(new[] { ci, topRow + i + 1, topRow + i });
        }
        // 外向きの面になっているか（輪郭の向きで裏返る）を確かめて、内向きならひっくり返す
        var m = new Mesh { name = "Course2_LedgeRock" };
        m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateNormals();
        {
            var nm = m.normals; Vector3 cw = root.InverseTransformPoint(origin + axis * cen.x + side * cen.y);
            int sample = nO * (rows / 2) + nO / 2;
            var vv = verts[sample]; var outDir = vv - cw; outDir.y = 0f;
            if (Vector3.Dot(nm[sample], outDir) < 0f) { for (int i = 0; i < tris.Count; i += 3) { int tmp = tris[i + 1]; tris[i + 1] = tris[i + 2]; tris[i + 2] = tmp; } m.SetTriangles(tris, 0); m.RecalculateNormals(); }
        }
        m.RecalculateBounds();
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if (old != null) { old.Clear(); old.SetVertices(m.vertices); old.SetTriangles(m.triangles, 0); old.SetNormals(m.normals); old.RecalculateBounds(); EditorUtility.SetDirty(old); m = old; }
        else AssetDatabase.CreateAsset(m, MeshPath);
        AssetDatabase.SaveAssets();

        var go = new GameObject("CollapseLedgeRocks", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(root, false); go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
        go.GetComponent<MeshFilter>().sharedMesh = m;
        go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Rock.mat");
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        return "ledge rock top " + rockTop.ToString("F2") + " outline " + nO;
    }
}
