using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase.Editor.BuildPipeline;

// 2026-11-23 てじゃ「いわのみちの噴気孔の音がステージ１中にきこえてるかも」
//  VRChat は VRCSpatialAudioSource が付いていない音に、実行時に既定の設定（Far 40m・Gain 10dB）を付ける。
//  そのため AudioSource の maxDistance（湯気 12m など）が効かず、40m 先まで聞こえていた。
//  → 3D の音（spatialBlend 0.5 以上）で VRCSpatialAudioSource が無い物に付けて、Far を AudioSource の maxDistance に合わせる。
//    Gain は既定と同じ 10（今までの近くの大きさのまま）、Near は minDistance。
//  ビルドの前に毎回自動で（作り直しで付け忘れても大丈夫）。メニューからも
public class FuwaSpatialAudioFix : IVRCSDKBuildRequestedCallback
{
    public int callbackOrder => 0;
    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (requestedBuildType == VRCSDKRequestedBuildType.Scene) Debug.Log(Run());
        return true;
    }

    [MenuItem("FuwaCourse/音/3Dの音の聞こえる範囲を maxDistance に合わせる")]
    static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        int n = 0;
        foreach (var a in Resources.FindObjectsOfTypeAll<AudioSource>())
        {
            if (!a || !a.gameObject.scene.IsValid() || a.spatialBlend < 0.5f) continue;
            if (a.GetComponent<VRCSpatialAudioSource>()) continue;
            var sp = Undo.AddComponent<VRCSpatialAudioSource>(a.gameObject);
            sp.Gain = 10f; sp.Near = Mathf.Max(0f, a.minDistance); sp.Far = Mathf.Max(sp.Near + 0.1f, a.maxDistance);
            sp.EnableSpatialization = true; sp.UseAudioSourceVolumeCurve = false;
            EditorUtility.SetDirty(sp); n++;
        }
        if (n > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        return "[音] VRCSpatialAudioSource を付けた（Far＝maxDistance）：" + n;
    }
}
