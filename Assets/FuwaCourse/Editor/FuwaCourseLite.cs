using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 各コースの「床だけの軽い版」を作る（プレイ中に他のコースの代わりに見せる）。
// 大きい物（道・島・足場・岩・大木・巨大キノコ）だけを集めて間引き、色は頂点カラーにして1コース1メッシュにまとめる。
// 飾り・柵・ギミック・半透明・パーティクルは入れない。コースを直したらもう一度実行する（何度実行しても同じ結果）
public static class FuwaCourseLite
{
    static readonly string[] Courses = { "FuwaCourse", "FuwaCourse2", "FuwaCourse3", "FuwaCourse4", "FuwaCourse5", "FuwaCourse0" };
    const string Dir = "Assets/FuwaCourse/Generated/Lite";
    const float MinSize = 2.5f;   // これより小さい物は入れない(m、いちばん長い辺)
    const float Cell = 0.35f;     // 間引きの細かさ(m)

    [MenuItem("FuwaCourse/よく使う/コースの軽い版を作る")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Lite");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var holder = roots.FirstOrDefault(g => g.name == "CourseLites");
        if (holder == null) { holder = new GameObject("CourseLites"); Undo.RegisterCreatedObjectUndo(holder, "lite"); }
        holder.transform.position = Vector3.zero; holder.transform.rotation = Quaternion.identity; holder.transform.localScale = Vector3.one;
        var mat = AssetDatabase.FindAssets("Den_Twig t:Material").Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).First(m => m.name == "Den_Twig");
        var log = new System.Text.StringBuilder();
        var starts = new List<Transform>(); var fulls = new List<GameObject>(); var lites = new List<GameObject>();
        foreach (var cn in Courses)
        {
            var root = roots.FirstOrDefault(g => g.name == cn); if (root == null) continue;
            var V = new List<Vector3>(); var C = new List<Color>(); var T = new List<int>(); int used = 0;
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue;
                if (mr.name.EndsWith("_Far")) continue;
                var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var b = mr.bounds; if (Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) < MinSize) continue;
                if (mr.sharedMaterials.Any(m => m != null && m.renderQueue >= 2450)) continue;
                string path = mr.transform.name; for (var p = mr.transform.parent; p != null && p != root.transform; p = p.parent) path = p.name + "/" + path;
                if (path.Contains("Deco/") || path.Contains("Fence") || path.Contains("Web") || path.Contains("Wind") || path.Contains("Gimmick")) continue;
                var mesh = mf.sharedMesh; if (!mesh.isReadable) continue;
                var mv = mesh.vertices; var mc = mesh.colors; var l2w = mr.transform.localToWorldMatrix;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var m = s < mr.sharedMaterials.Length ? mr.sharedMaterials[s] : mr.sharedMaterials.LastOrDefault();
                    Color mcol = m != null && m.HasProperty("_Color") ? m.color : Color.white; mcol.a = 1;
                    bool useVc = mc.Length == mv.Length && m != null && m.shader.name.Contains("InkCap");
                    var tri = mesh.GetTriangles(s); int bi = V.Count; var map = new Dictionary<int, int>();
                    foreach (var t in tri) { if (!map.TryGetValue(t, out int j)) { j = V.Count; V.Add(l2w.MultiplyPoint3x4(mv[t])); C.Add(useVc ? new Color(mc[t].r, mc[t].g, mc[t].b, 1) : mcol); map[t] = j; } T.Add(j); }
                    // 裏表：拡大率が負の物は向きを反転
                    if (l2w.determinant < 0) for (int k = T.Count - tri.Length; k < T.Count; k += 3) { var x = T[k + 1]; T[k + 1] = T[k + 2]; T[k + 2] = x; }
                }
                used++;
            }
            // 頂点を格子にまとめて間引く（位置が同じ格子の頂点は1つに。色は平均）
            var cellMap = new Dictionary<Vector3Int, int>(); var NV = new List<Vector3>(); var NC = new List<Color>(); var cnt = new List<int>(); var remap = new int[V.Count];
            for (int i = 0; i < V.Count; i++) { var k = Vector3Int.FloorToInt(V[i] / Cell); if (!cellMap.TryGetValue(k, out int j)) { j = NV.Count; cellMap[k] = j; NV.Add(Vector3.zero); NC.Add(Color.clear); cnt.Add(0); } NV[j] += V[i]; NC[j] += C[i]; cnt[j]++; remap[i] = j; }
            for (int j = 0; j < NV.Count; j++) { NV[j] /= cnt[j]; NC[j] /= cnt[j]; }
            var NT = new List<int>();
            for (int k = 0; k < T.Count; k += 3) { int a = remap[T[k]], b2 = remap[T[k + 1]], c = remap[T[k + 2]]; if (a == b2 || b2 == c || a == c) continue; NT.Add(a); NT.Add(b2); NT.Add(c); }
            // 面ごとに頂点を分けて、法線は面の向き（つぶした頂点で法線をなめらかにすると、放射状のヘンな影が出るので）
            var FV = new List<Vector3>(); var FN = new List<Vector3>(); var FC = new List<Color>(); var FT = new List<int>();
            for (int k = 0; k < NT.Count; k += 3)
            {
                var a = NV[NT[k]]; var b2 = NV[NT[k + 1]]; var c = NV[NT[k + 2]]; var fn = Vector3.Cross(b2 - a, c - a); if (fn.sqrMagnitude < 1e-10f) continue; fn.Normalize();
                int bi2 = FV.Count; FV.Add(a); FV.Add(b2); FV.Add(c); FN.Add(fn); FN.Add(fn); FN.Add(fn);
                foreach (var idx in new[] { NT[k], NT[k + 1], NT[k + 2] }) { var col = NC[idx]; col.a = 1; FC.Add(col); }
                FT.Add(bi2); FT.Add(bi2 + 1); FT.Add(bi2 + 2);
            }
            var lite = new Mesh { name = cn + "_Lite", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            lite.SetVertices(FV); lite.SetNormals(FN); lite.SetColors(FC);   // 色はガンマのまま（InkCap シェーダーが中で直す）
            lite.SetTriangles(FT, 0);
            lite.RecalculateBounds();
            string ap = Dir + "/" + cn + "_Lite.asset"; var old = AssetDatabase.LoadAssetAtPath<Mesh>(ap);
            if (old != null) { old.Clear(); EditorUtility.CopySerialized(lite, old); lite = old; } else AssetDatabase.CreateAsset(lite, ap);
            var go = holder.transform.Find(cn + "_Lite")?.gameObject;
            if (go == null) { go = new GameObject(cn + "_Lite"); go.transform.SetParent(holder.transform, false); go.AddComponent<MeshFilter>(); go.AddComponent<MeshRenderer>(); }
            go.GetComponent<MeshFilter>().sharedMesh = lite; var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            go.SetActive(false);
            var st = root.transform.Find("BallStart"); if (st == null) st = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "BallStart");
            starts.Add(st); fulls.Add(root); lites.Add(go);
            log.Append(cn + ": parts " + used + " tris " + (NT.Count / 3) + "\n");
        }
        // 切り替えスクリプトの設定
        var lod = Object.FindObjectsOfType<FuwaCourseLod>(true).FirstOrDefault();
        if (lod != null)
        {
            var so = new SerializedObject(lod);
            void SetArr(string name, Object[] arr) { var p = so.FindProperty(name); p.arraySize = arr.Length; for (int i = 0; i < arr.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = arr[i]; }
            SetArr("starts", starts.ToArray()); SetArr("fullRoots", fulls.ToArray()); SetArr("liteRoots", lites.ToArray());
            so.ApplyModifiedProperties();
            SetBounds(lod);
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("[軽い版] " + log);
    }

    // 近づいたら本体に戻す判定用：各コースの床がある所を cellSize 角のマスにして持たせる（軽い版のメッシュの頂点が入っているマス）。
    // コース全体の箱1個だと隣のコースと重なって、普通に遊んでいても隣が戻りっぱなしになったので
    public static string SetBounds(FuwaCourseLod lod)
    {
        Undo.RecordObject(lod, "lite cells");
        float cs = lod.cellSize > 0f ? lod.cellSize : 10f;
        var centers = new List<Vector3>(); var course = new List<int>(); var log = new System.Text.StringBuilder();
        for (int i = 0; i < lod.liteRoots.Length; i++)
        {
            var mf = lod.liteRoots[i] != null ? lod.liteRoots[i].GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) continue;
            var set = new HashSet<Vector3Int>(); var tr = lod.liteRoots[i].transform;
            foreach (var v in mf.sharedMesh.vertices) set.Add(Vector3Int.FloorToInt(tr.TransformPoint(v) / cs));
            foreach (var k in set) { centers.Add(((Vector3)k + Vector3.one * 0.5f) * cs); course.Add(i); }
            log.Append(lod.liteRoots[i].name + " " + set.Count + "マス\n");
        }
        lod.cellCenters = centers.ToArray(); lod.cellCourse = course.ToArray();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(lod); EditorUtility.SetDirty(lod);
        return log.ToString();
    }
}
