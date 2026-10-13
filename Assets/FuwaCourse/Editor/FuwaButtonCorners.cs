using System.Linq;
using UnityEditor;
using UnityEngine;

// ボタンの形をそろえる（2026-10-13 てじゃ：あそびばのボードで「音量の板の形（角丸の四角）」にそろえたら「これいいな、チャレンジのほうもぜんぶこれに」）。
// 丸い端のボタン（RoundedPanel を Sliced で、pixelsPerUnitMultiplier 0.125〜0.16）を、音量の板と同じ 0.1667 に。
// ボタンの中の小さい札（PillA/PillB）は高さに合わせた同じ形に。× ボタンの棒はそのまま。ボタンを作り直すメニューを使った後は、もう一度実行する（何度実行してもよい）
public static class FuwaButtonCorners
{
    public const float CornerPPU = 0.1666667f;

    [MenuItem("FuwaCourse/ボタンの角をそろえる（角丸の四角）")]
    public static void Menu()
    {
        Debug.Log(Run(null));
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    // root が null ならシーン全部
    public static string Run(Transform root)
    {
        var imgs = root != null ? root.GetComponentsInChildren<UnityEngine.UI.Image>(true) : Object.FindObjectsOfType<UnityEngine.UI.Image>(true);
        int n = 0;
        foreach (var img in imgs)
        {
            if (img.sprite == null || img.sprite.name != "RoundedPanel" || img.type != UnityEngine.UI.Image.Type.Sliced) continue;
            // ボタンの中の白い札（言語の JP/EN、HUD の ON/OFF、写真館のボタンの中）も同じ形に。高さに合わせて角の丸みの割合をそろえる（2026-10-13 てじゃ「ボタンのなかの白もこれに」）
            if (img.name == "PillA" || img.name == "PillB")
            {
                float h = Mathf.Max(1f, ((RectTransform)img.transform).sizeDelta.y);
                float want = CornerPPU * 400f / h;
                if (Mathf.Abs(img.pixelsPerUnitMultiplier - want) > 0.0005f) { img.pixelsPerUnitMultiplier = want; EditorUtility.SetDirty(img); n++; }
                continue;
            }
            float p = img.pixelsPerUnitMultiplier;
            if (p < 0.12f || p > 0.161f) continue;
            img.pixelsPerUnitMultiplier = CornerPPU; EditorUtility.SetDirty(img); n++;
        }
        return "button corners: " + n;
    }
}
