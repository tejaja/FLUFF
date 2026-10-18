using System.Linq;
using UnityEditor;
using UnityEngine;

// ボタンの形をそろえる：端が半円の丸いボタン（2026-10-13 てじゃ：一度「角丸の四角」にそろえたが「やっぱ丸いのにもどそかな全部」）。
// RoundedPanel を Sliced で使っている板（ボタン・中の白い札・音量の板）の角の丸みを、高さに合わせて端がちょうど半円になる値（pixelsPerUnitMultiplier = 50 / 高さ）に。
// もともとのボタン（高さ400で 0.125、札の高さ260で 0.192 など）と同じ決まり。音量の板だけ角が小さかったのもこれでそろう。
// × ボタンの棒など、丸みの値が大きい物（0.5より上）は別の形なのでそのまま。ボタンを作り直すメニューを使った後は、もう一度実行する（何度実行してもよい）
public static class FuwaButtonCorners
{
    const float RoundRadius = 50f;   // 板の高さ（キャンバスの単位）× pixelsPerUnitMultiplier。この値で端が半円になる

    [MenuItem("FuwaCourse/済み（押す前に相談）/ボタンの形をそろえる（端が丸いボタン）")]
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
            if (img.pixelsPerUnitMultiplier > 0.5f) continue;
            float h = ((RectTransform)img.transform).rect.height;
            if (h < 1f) continue;
            float want = RoundRadius / h;
            if (Mathf.Abs(img.pixelsPerUnitMultiplier - want) < 0.0005f) continue;
            img.pixelsPerUnitMultiplier = want; EditorUtility.SetDirty(img); n++;
        }
        return "button shapes: " + n;
    }
}
