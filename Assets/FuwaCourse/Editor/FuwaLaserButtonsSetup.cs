using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// クリック（Interact）で押していた物に、ほかのUIと同じレーザーで押すボタンを付ける（2026-10-12 てじゃ「押すもの全部レーザー式に」「距離とかも」）。
// ・エアガンの台：ボタンは付けない。台そのもののクリック（Interact）で呼ぶ（2026-10-12 てじゃ「台をインタラクトのはずだった」）。前に付けたボタンは消して laserOnly を切る
// ・入口の裏の島のアバター見本：看板の下に「このアバターになる」→ FuwaAvatarSwitch.Use
// 近くに来た時だけ押せるのは FuwaUiProximity（ビルド前に自動で登録し直す）。何度実行してもよい（前のボタンは消して作り直す）
public static class FuwaLaserButtonsSetup
{
    [MenuItem("FuwaCourse/台・看板のクリックをレーザーボタンにする")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        int n = 0;
        foreach (var gs in Object.FindObjectsOfType<FuwaGunStand>(true))
        {
            var parent = gs.transform.parent;
            string bn = gs.name + "_LaserButton";
            var old = parent.Find(bn); if (old != null) Object.DestroyImmediate(old.gameObject);
            var so = new SerializedObject(gs); so.FindProperty("laserOnly").boolValue = false; so.ApplyModifiedPropertiesWithoutUndo();
            UdonSharpEditorUtility.CopyProxyToUdon(gs);
        }
        foreach (var av in Object.FindObjectsOfType<FuwaAvatarSwitch>(true))
        {
            var parent = av.transform.parent;
            string bn = av.name + "_LaserButton";
            var old = parent.Find(bn); if (old != null) Object.DestroyImmediate(old.gameObject);
            var sr = av.GetComponent<SpriteRenderer>();
            float h = sr != null ? sr.bounds.size.y : 1f;
            var pos = av.transform.position - Vector3.up * (h * 0.5f + 0.2f) - av.transform.forward * 0.02f;   // 看板の下（表は -Z）
            var t = FuwaChillRoomBuilder.LaserButton(parent, bn, pos, av.transform.rotation, 1.25f, "このアバターになる", av, "Use");
            t.transform.parent.parent.parent.localScale *= 0.8f;
            Lang(t, "このアバターになる", "Use this avatar");
            var so = new SerializedObject(av); so.FindProperty("laserOnly").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
            UdonSharpEditorUtility.CopyProxyToUdon(av);
            n++;
        }
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
        return "laser buttons " + n + " / " + FuwaUiProximitySetup.Run();
    }

    static void Lang(TMPro.TextMeshProUGUI t, string ja, string en)
    {
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault(); if (lang == null) return;
        var lso = new SerializedObject(lang);
        var pa = lso.FindProperty("uiTexts"); var pj = lso.FindProperty("uiJa"); var pe = lso.FindProperty("uiEn");
        int k = pa.arraySize; pa.arraySize = k + 1; pj.arraySize = k + 1; pe.arraySize = k + 1;
        pa.GetArrayElementAtIndex(k).objectReferenceValue = t; pj.GetArrayElementAtIndex(k).stringValue = ja; pe.GetArrayElementAtIndex(k).stringValue = en;
        lso.ApplyModifiedPropertiesWithoutUndo(); UdonSharpEditorUtility.CopyProxyToUdon(lang);
    }
}
