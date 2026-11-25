using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

// 近くだけ影（FuwaFarShadows：SmallShadows・FarShadows）の「どのステージの物か」（targetStage）を、物の一覧（targets）に合わせて付け直す。
// 2026-11-25 てじゃ「2-5ゴールのミニキノコの影がきえてる」「クリアフラグ用ミニキノコも影きえてるのがいくつかある」
//  → SmallShadows で targets 1051 個に対して targetStage が 1071 個。キノコを足す・消すツール（ミニキノコ・チェックポイント・焼きをやめる・各ビルダー…）が
//    targets / centers / radii だけ直して targetStage を直していなかったので、番号が 20 個ずれて、2-5 のゴールのキノコが「ステージ1」になっていた（ステージ2では影がつかない）
//  → アップロードの前に毎回付け直す（IVRCSDKBuildRequestedCallback）。centers / radii も数がずれていたら付け直す。何度押してもよい
public class FuwaFarShadowsStageFix : IVRCSDKBuildRequestedCallback
{
    public int callbackOrder => 5;
    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (requestedBuildType == VRCSDKRequestedBuildType.Scene) Debug.Log(Run());
        return true;
    }

    [MenuItem("FuwaCourse/よく使う/近くだけ影のステージ番号を付け直す")]
    static void Menu() { Debug.Log(Run()); UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty(); }

    // 隠れている物・描画オフの物は Renderer.bounds が使えないので、メッシュから（FuwaFarViewSetup.WorldBounds と同じ）
    static Bounds WB(Renderer r)
    {
        if (r.enabled && r.gameObject.activeInHierarchy) return r.bounds;
        Mesh m = r is SkinnedMeshRenderer sk ? sk.sharedMesh : (r.GetComponent<MeshFilter>() ? r.GetComponent<MeshFilter>().sharedMesh : null);
        if (m == null) return r.bounds;
        var lb = m.bounds; var l2w = r.transform.localToWorldMatrix;
        var b = new Bounds(l2w.MultiplyPoint3x4(lb.center), Vector3.zero);
        for (int k = 0; k < 8; k++) b.Encapsulate(l2w.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1))));
        return b;
    }

    public static int StageOf(Transform t)
    {
        string rn = t.root.name;
        return rn.StartsWith("FuwaCourse2_") || rn == "FuwaStage2" || rn == "Stage2Far" || rn == "CourseLites2" ? 2
             : rn.StartsWith("FuwaCourse") || rn == "FuwaLobby" || rn == "CourseLites" ? 1 : 0;   // FuwaFarViewSetup と同じ決め方
    }

    public static string Run()
    {
        var log = new System.Collections.Generic.List<string>();
        foreach (var fs in Resources.FindObjectsOfTypeAll<FuwaFarShadows>().Where(x => x && x.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(x)))
        {
            if (fs.targets == null) continue;
            int n = fs.targets.Length, changed = 0;
            var st = new int[n];
            for (int i = 0; i < n; i++)
            {
                st[i] = fs.targets[i] ? StageOf(fs.targets[i].transform) : 0;
                if (fs.targetStage == null || i >= fs.targetStage.Length || fs.targetStage[i] != st[i]) changed++;
            }
            bool lenFix = fs.targetStage == null || fs.targetStage.Length != n;
            bool cFix = fs.centers == null || fs.centers.Length != n || fs.radii == null || fs.radii.Length != n;
            // 物を動かしたのに中心が古いまま（reference_fluff_farshadows_stale_centers）も、0.5m 以上ずれていたら付け直す
            int moved = 0;
            if (!cFix) for (int i = 0; i < n; i++) if (fs.targets[i] && (fs.centers[i] - WB(fs.targets[i]).center).magnitude > 0.5f) moved++;
            if (changed == 0 && !lenFix && !cFix && moved == 0) continue;
            fs.targetStage = st;
            if (cFix)
            {
                fs.centers = fs.targets.Select(r => r ? WB(r).center : Vector3.zero).ToArray();
                fs.radii = fs.targets.Select(r => r ? WB(r).extents.magnitude : 0f).ToArray();
            }
            else if (moved > 0)
            {
                var c = fs.centers.ToArray(); var rr = fs.radii.ToArray();
                for (int i = 0; i < n; i++) if (fs.targets[i]) { var wb = WB(fs.targets[i]); if ((c[i] - wb.center).magnitude > 0.5f) { c[i] = wb.center; rr[i] = wb.extents.magnitude; } }
                fs.centers = c; fs.radii = rr;
            }
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fs); EditorUtility.SetDirty(fs);
            log.Add(fs.name + "：ステージ番号 " + changed + " 個" + (lenFix ? "（数がずれていた）" : "") + (cFix ? "・位置も付け直し" : "") + (moved > 0 ? "・位置がずれていた " + moved + " 個" : ""));
        }
        return "[近くだけ影のステージ番号] " + (log.Count == 0 ? "ずれなし" : string.Join(" / ", log));
    }
}
