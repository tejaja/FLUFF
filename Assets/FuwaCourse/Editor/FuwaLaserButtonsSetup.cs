using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// エアガンの台・アバター見本は、台／看板そのもののクリック（Interact）で使う。
// 一度「押すもの全部レーザー式に」でそばにレーザーボタン（_LaserButton）を付けたが、ほかもインタラクト式が多いので戻した（2026-10-12 てじゃ「台をインタラクトのはずだった」「アバターもインタラクト式にもどそうか」）。
// このメニューはそのボタンを消して laserOnly を切る。何度実行してもよい
public static class FuwaLaserButtonsSetup
{
    [MenuItem("FuwaCourse/台・看板のレーザーボタンを外す（クリックで使う）")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        int n = 0;
        foreach (var gs in Object.FindObjectsOfType<FuwaGunStand>(true)) n += Revert(gs);
        foreach (var av in Object.FindObjectsOfType<FuwaAvatarSwitch>(true)) n += Revert(av);
        // 消したボタンの言語の登録（null）を詰める
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        if (lang != null)
        {
            var lso = new SerializedObject(lang);
            var pa = lso.FindProperty("uiTexts"); var pj = lso.FindProperty("uiJa"); var pe = lso.FindProperty("uiEn");
            for (int i = pa.arraySize - 1; i >= 0; i--)
                if (pa.GetArrayElementAtIndex(i).objectReferenceValue == null)
                { pa.DeleteArrayElementAtIndex(i); if (i < pj.arraySize) pj.DeleteArrayElementAtIndex(i); if (i < pe.arraySize) pe.DeleteArrayElementAtIndex(i); }
            lso.ApplyModifiedPropertiesWithoutUndo(); UdonSharpEditorUtility.CopyProxyToUdon(lang);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "removed laser buttons " + n + " / " + FuwaUiProximitySetup.Run();
    }

    // そばのボタンを消してクリックで使えるようにする。消したボタンの数を返す
    static int Revert(UdonSharp.UdonSharpBehaviour b)
    {
        int removed = 0;
        var old = b.transform.parent.Find(b.name + "_LaserButton");
        if (old != null) { Object.DestroyImmediate(old.gameObject); removed = 1; }
        var so = new SerializedObject(b); so.FindProperty("laserOnly").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo();
        UdonSharpEditorUtility.CopyProxyToUdon(b);
        return removed;
    }
}
