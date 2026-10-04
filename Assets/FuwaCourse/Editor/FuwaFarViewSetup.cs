using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 遠くから見た時の軽量化（ロビー＋ひとりでのコース）。
// ・飾り（草・花・ツタ・小石・小さいキノコの輪・看板・モンスターの目）は、画面上で小さくなったら描かない（LODGroup）
//   風の道・風車・足場・モンスターの体などギミックは消さない
// ・影を落とす物は全部 FuwaFarShadows に登録して、自分から遠い物は影を切る
// コースを作り直したらもう一度実行する（何度実行しても同じ結果になる）
public static class FuwaFarViewSetup
{
    static readonly string[] Areas = { "FuwaLobby", "FuwaCourse", "FuwaCourse0", "FuwaCourse2", "FuwaCourse3", "FuwaCourse4", "FuwaCourse5" };

    // 遠くで消す飾り（親をたどった名前のどこかに当てはまれば対象）
    static readonly Regex DecoPath = new Regex(@"(^|/)(Deco|FairyRing|StartArea/.*/(Cap|Stem)$|Label|Panel|EyeWhite|Pupil|FloorShadow)(/|$|\d)");
    // 画面の高さに対する割合がこれより小さくなったら消す
    const float CullHeight = 0.01f;

    [MenuItem("FuwaCourse/遠景の軽量化を設定")]
    public static void Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = new Dictionary<string, GameObject>();
        foreach (var g in scene.GetRootGameObjects()) roots[g.name] = g;

        int lod = 0;
        var shadowTargets = new List<Renderer>();
        foreach (var a in Areas)
        {
            if (!roots.TryGetValue(a, out var root)) continue;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (IsDeco(r.transform, root.transform) && SetCull(r)) lod++;
                if (r.shadowCastingMode == ShadowCastingMode.On) shadowTargets.Add(r);
            }
        }

        var holder = roots.TryGetValue("FuwaSystem", out var sys) ? sys.transform.Find("FarShadows") : null;
        if (holder == null)
        {
            holder = new GameObject("FarShadows").transform;
            holder.SetParent(sys != null ? sys.transform : null, false);
            Undo.RegisterCreatedObjectUndo(holder.gameObject, "FarShadows");
        }
        var fs = holder.GetComponent<FuwaFarShadows>();
        if (fs == null) fs = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaFarShadows>(holder.gameObject);
        Undo.RecordObject(fs, "FarShadows");
        fs.targets = shadowTargets.ToArray();
        fs.centers = new Vector3[fs.targets.Length];
        fs.radii = new float[fs.targets.Length];
        for (int i = 0; i < fs.targets.Length; i++)
        {
            Bounds b = fs.targets[i].bounds;
            fs.centers[i] = b.center;
            fs.radii[i] = b.extents.magnitude;
        }
        EditorUtility.SetDirty(fs);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[遠景] 飾りのLOD {lod}個、影を切る対象 {fs.targets.Length}個");
    }

    static bool IsDeco(Transform t, Transform root)
    {
        var parts = new List<string>();
        for (var c = t; c != null && c != root; c = c.parent) parts.Insert(0, c.name);
        return DecoPath.IsMatch(string.Join("/", parts));
    }

    static bool SetCull(Renderer r)
    {
        var g = r.GetComponent<LODGroup>();
        if (g == null) g = Undo.AddComponent<LODGroup>(r.gameObject);
        else Undo.RecordObject(g, "LOD");
        g.fadeMode = LODFadeMode.None;
        g.SetLODs(new[] { new LOD(CullHeight, new[] { r }) });
        g.RecalculateBounds();
        return true;
    }
}
