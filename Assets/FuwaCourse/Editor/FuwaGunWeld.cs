using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// エアガンのメッシュの「位置も法線も同じ」重複頂点をまとめる（見た目は1ピクセルも変わらない）。
// 2026-10-16 てじゃ「銃4.2万はヤバイ」：Blender から書き出した部品・作り直した部品は三角形ごとに頂点がバラバラで、
// 1丁 約4.2万頂点のうち 約2/3 が重複だった。頂点をCPU/GPUで処理する回数が減る（三角形の数は同じ）。
// 作り直しのメニュー（FuwaGunCollarRebuild / FuwaGunTriggerRebuild）の最後でも呼ぶ。何度実行しても同じ結果
public static class FuwaGunWeld
{
    public static readonly string[] Meshes = { "Gun_Accent", "Gun_Body", "Gun_Grip", "Gun_Bulb", "Gun_Vent" };

    [MenuItem("FuwaCourse/エアガンの重複頂点をまとめる")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new List<string>();
        foreach (var n in Meshes)
        {
            var m = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/" + n + ".asset");
            if (m == null) continue;
            int before = m.vertexCount;
            Weld(m);
            log.Add(n + " " + before + "→" + m.vertexCount);
        }
        AssetDatabase.SaveAssets();
        return "[銃の頂点] " + string.Join(", ", log);
    }

    // 位置・法線・色・uv（0〜3）が全部同じ頂点を1つにする（丸め：位置0.01mm、法線0.001）
    public static void Weld(Mesh m)
    {
        var v = m.vertices; var nrm = m.normals; var col = m.colors32; var tan = m.tangents;
        var uvs = new List<Vector4>[4]; for (int c = 0; c < 4; c++) { uvs[c] = new List<Vector4>(); m.GetUVs(c, uvs[c]); }
        var key = new Dictionary<string, int>(); var remap = new int[v.Length];
        var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nc = new List<Color32>(); var nt = new List<Vector4>(); var nu = new List<Vector4>[4]; for (int c = 0; c < 4; c++) nu[c] = new List<Vector4>();
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < v.Length; i++)
        {
            sb.Clear();
            sb.Append(Vector3Int.RoundToInt(v[i] * 1e5f)).Append('|');
            if (nrm.Length > 0) sb.Append(Vector3Int.RoundToInt(nrm[i] * 1000f)).Append('|');
            if (tan.Length > 0) sb.Append(Vector4Int(tan[i])).Append('|');
            if (col.Length > 0) sb.Append(col[i].r).Append(',').Append(col[i].g).Append(',').Append(col[i].b).Append(',').Append(col[i].a).Append('|');
            for (int c = 0; c < 4; c++) if (uvs[c].Count > 0) sb.Append(Vector4Int(uvs[c][i])).Append('|');
            var k = sb.ToString();
            if (!key.TryGetValue(k, out int j))
            {
                j = nv.Count; key[k] = j;
                nv.Add(v[i]); if (nrm.Length > 0) nn.Add(nrm[i]); if (col.Length > 0) nc.Add(col[i]); if (tan.Length > 0) nt.Add(tan[i]);
                for (int c = 0; c < 4; c++) if (uvs[c].Count > 0) nu[c].Add(uvs[c][i]);
            }
            remap[i] = j;
        }
        if (nv.Count == v.Length) return;
        var subs = Enumerable.Range(0, m.subMeshCount).Select(s => m.GetTriangles(s).Select(t => remap[t]).ToArray()).ToArray();
        var bounds = m.bounds;
        m.Clear();
        m.indexFormat = nv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.SetVertices(nv); if (nn.Count > 0) m.SetNormals(nn); if (nc.Count > 0) m.SetColors(nc); if (nt.Count > 0) m.SetTangents(nt);
        for (int c = 0; c < 4; c++) if (nu[c].Count > 0) m.SetUVs(c, nu[c]);
        m.subMeshCount = subs.Length; for (int s = 0; s < subs.Length; s++) m.SetTriangles(subs[s], s);
        m.bounds = bounds;
        EditorUtility.SetDirty(m);
    }

    static string Vector4Int(Vector4 a) { return Mathf.RoundToInt(a.x * 1e5f) + "," + Mathf.RoundToInt(a.y * 1e5f) + "," + Mathf.RoundToInt(a.z * 1e5f) + "," + Mathf.RoundToInt(a.w * 1e5f); }
}
