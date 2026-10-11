using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

// レーザーで押すUI（Canvas＋VRCUiShape）を、FuwaSystem/UiProximity（FuwaUiProximity）に全部登録する。
// 2026-10-11 てじゃ「デスクトップだと遠くからボタン押せちゃう、誤爆する」→ あとから足した HUD・CAM・みんなで等が登録漏れしてた。
// 漏れないように、ビルドの直前にも自動で拾い直す。何度実行してもよい
public class FuwaUiProximitySetup : IVRCSDKBuildRequestedCallback
{
    public int callbackOrder => 0;

    [MenuItem("FuwaCourse/UIボタンを近くだけ押せるように（登録し直す）")]
    public static void Menu() { Debug.Log(Run()); }

    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (requestedBuildType == VRCSDKRequestedBuildType.Scene) Debug.Log(Run());
        return true;
    }

    public static string Run()
    {
        var prox = Object.FindObjectsOfType<FuwaUiProximity>(true).FirstOrDefault();
        if (prox == null) return "FuwaUiProximity が見つからない";
        var cols = Object.FindObjectsOfType<Canvas>(true)
            .Where(c => c.renderMode == RenderMode.WorldSpace && c.GetComponent("VRCUiShape") != null
                        && c.GetComponentsInChildren<UnityEngine.UI.Selectable>(true).Length > 0)
            .Select(c => (Collider)c.GetComponent<BoxCollider>())
            .Where(c => c != null)
            .OrderBy(c => c.transform.GetHierarchyPath())
            .ToArray();
        if (prox.uiColliders != null && prox.uiColliders.SequenceEqual(cols)) return "UiProximity: 変更なし " + cols.Length + "個";
        int before = prox.uiColliders == null ? 0 : prox.uiColliders.Length;
        prox.uiColliders = cols;
        UdonSharpEditorUtility.CopyProxyToUdon(prox);
        EditorUtility.SetDirty(prox);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(prox.gameObject.scene);
        return "UiProximity: " + before + "->" + cols.Length + "個";
    }
}

static class FuwaUiProximitySetupExt
{
    public static string GetHierarchyPath(this Transform t)
    {
        var s = t.name;
        while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
        return s;
    }
}
