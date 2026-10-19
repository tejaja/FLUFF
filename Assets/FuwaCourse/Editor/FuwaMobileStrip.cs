using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

// スマホ/Quest（Android）版のビルドの時だけ、重い物をシーンから外す（PC版・エディタのシーンはそのまま）。
// ・やみのもりを外から包む暗いもや（YamiMist、FuwaCourse/DarkMist）：画面の大きな範囲に半透明＋ノイズで重い（2026-10-19 てじゃ「ロビーからみたもやもや」「モバイル版は霧切っちゃお」）
public class FuwaMobileStrip : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (Application.isPlaying || EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) return;
        int n = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null || m.shader == null || m.shader.name != "FuwaCourse/DarkMist") continue;
                r.enabled = false;   // 消すと参照している物（軽い版の切り替えなど）が困るので、描かないだけ
                n++;
            }
        if (n > 0) Debug.Log("[FuwaMobileStrip] Android：暗いもや " + n + " 個を外した");
    }
}
