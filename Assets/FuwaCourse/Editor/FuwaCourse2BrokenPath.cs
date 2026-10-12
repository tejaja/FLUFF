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
    const float CutX = 98.2f, Jag = 0.9f, EndX = 104.9f, MaxY = 12.5f, MinZ = -1.5f, MaxZ = 9.5f;

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
        var verts = src.vertices;
        var newV = new List<Vector3>(verts); var newN = new List<Vector3>(src.normals);
        var uv = new List<Vector2>(); src.GetUVs(0, uv); bool hasUv = uv.Count == verts.Length;
        var subs = new List<int>[3]; for (int s = 0; s < 3; s++) subs[s] = new List<int>();
        int moved = 0;
        for (int s = 0; s < src.subMeshCount && s < 2; s++)
        {
            var tris = src.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 c = l2w.MultiplyPoint3x4((verts[tris[i]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3f);
                float cut = CutX + Jag * (0.55f * Mathf.Sin(c.z * 2.3f + 0.7f) + 0.45f * Mathf.Sin(c.z * 5.1f + 2.1f));
                bool rock = c.x > cut && c.x < EndX && c.y < MaxY && c.z > MinZ && c.z < MaxZ;
                var dst = rock ? subs[2] : subs[s];
                dst.Add(tris[i]); dst.Add(tris[i + 1]); dst.Add(tris[i + 2]);
                if (rock) moved++;
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
                Vector3 w = l2w.MultiplyPoint3x4(verts[vi]);
                float n = Mathf.PerlinNoise(w.x * 1.7f, w.z * 1.7f) - 0.5f;
                w += Vector3.up * n * 0.08f;
                newV.Add(w2l.MultiplyPoint3x4(w)); newN.Add(src.normals[vi]); if (hasUv) uv.Add(uv[vi]);
            }
            subs[2][i] = ni;
        }
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
        return "broken path tris " + moved;
    }
}
