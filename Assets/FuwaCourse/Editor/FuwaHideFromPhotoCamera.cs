using System.Linq;
using UnityEditor;
using UnityEngine;

// 自分だけに出る表示を VRChat のカメラ（写真）に写さない（2026-11-03 てじゃ「UIをVRChatカメラに映らないようにしたい」）。
// 実機で試した結果（2026-11-03、レイヤーを変えた板）：目では見えて写真には写らないのは 2 Ignore Raycast・5 UI・12 UiMenu。
// 18 MirrorReflection は目で見えず写真にだけ写る。→ 5 UI を使う（おっかけカメラも 5 を写さない）。
// 対象：タイマー・ゴールの文字・画面の空気ゲージ（FuwaHud）、銃を呼ぶゲージ、ワープのたまるゲージ（*_Trigger_Gauge）。
// そのまま写す：銃の側面の空気ゲージ（GunGauge）、ワープのヴェール・床の輪（てじゃ「ヴェールもうつっていいかも、ゲージはいらん」）
public static class FuwaHideFromPhotoCamera
{
    public const int HiddenLayer = 5;   // UI

    [MenuItem("FuwaCourse/よく使う/自分だけの表示をVRChatカメラに写さない（レイヤーをそろえる）")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        int hud = 0, gauge = 0, summon = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "FuwaHud")
                    foreach (var r in t.GetComponentsInChildren<Renderer>(true)) { Set(r.gameObject); hud++; }
                else if (t.name == "SummonGauge") { Set(t.gameObject); summon++; }
                else if (t.name.EndsWith("_Trigger_Gauge") && t.GetComponent<Renderer>()) { Set(t.gameObject); gauge++; }
            }
        // 名場面アルバムの自動撮影カメラも、自分だけの表示を写さない（前は NoMonitor のタイマーが写る設定だった）
        int cams = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var c in root.GetComponentsInChildren<Camera>(true))
                if (c.name == "ShotCam") { Undo.RecordObject(c, "mask"); c.cullingMask &= ~((1 << HiddenLayer) | (1 << 23)); EditorUtility.SetDirty(c); cams++; }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return "[写真に写さない] HUD " + hud + "・ワープのゲージ " + gauge + "・銃を呼ぶゲージ " + summon + "・アルバムのカメラ " + cams;
    }

    static void Set(GameObject g)
    {
        if (g.layer == HiddenLayer) return;
        Undo.RecordObject(g, "layer"); g.layer = HiddenLayer; EditorUtility.SetDirty(g);
    }
}
