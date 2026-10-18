using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

// シーンで使っている自作メッシュ（Assets/FuwaCourse の Generated・Meshes）の「位置・法線・色・uv が全部同じ」重複頂点をまとめる。
// 見た目は変わらない（頂点の数だけ減る）。2026-10-16 てじゃ「全体的に見た目をそこなわない軽量化」。
// ビルダーで作り直すと重複が戻るので、アップロードの前にも自動で実行する（何度実行しても同じ結果）
public class FuwaMeshWeldAll : IVRCSDKBuildRequestedCallback
{
    public int callbackOrder => 10;

    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (requestedBuildType == VRCSDKRequestedBuildType.Scene) Debug.Log(Run());
        return true;
    }

    [MenuItem("FuwaCourse/よく使う/自作メッシュの重複頂点をまとめる（見た目は同じ）")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var meshes = new HashSet<Mesh>();
        foreach (var mf in Object.FindObjectsOfType<MeshFilter>(true))
        {
            var m = mf.sharedMesh; if (m == null || mf.GetComponent<MeshRenderer>() == null) continue;
            var p = AssetDatabase.GetAssetPath(m);
            if (!p.StartsWith("Assets/FuwaCourse/Generated/") && !p.StartsWith("Assets/FuwaCourse/Meshes/")) continue;
            if (!p.EndsWith(".asset") || !m.isReadable || m.blendShapeCount > 0 || m.bindposes.Length > 0) continue;
            meshes.Add(m);
        }
        long before = 0, after = 0; int changed = 0;
        foreach (var m in meshes)
        {
            int b = m.vertexCount; before += b;
            FuwaGunWeld.Weld(m);
            after += m.vertexCount; if (m.vertexCount != b) changed++;
        }
        AssetDatabase.SaveAssets();
        return "[重複頂点] " + changed + "/" + meshes.Count + "個のメッシュ、" + before + "→" + after + "頂点";
    }
}
