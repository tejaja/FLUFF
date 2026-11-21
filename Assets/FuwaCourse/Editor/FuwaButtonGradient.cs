using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

// ボタンの板にうすいグラデーション（2026-11-21 てじゃ「各種ボタンの見た目をちょっといじりたい」「ちょっとグラデあるかんじ」→見本を見て「おおいいね」）。
// 押せるボタン（Selectable のある Image）で、角丸の板 RoundedPanel を使っている物を RoundedPanelGrad に差しかえる。
// RoundedPanelGrad は同じ形・同じ 9 分割で、上がそのままの色→下に向かって1割弱濃く（2割だと「すこし暗く感じる」で弱めた）、ふちがほんのり濃い白黒の絵（色はボタンの Color でつく）。
// ボタンの中の白い札・看板の枠（ボタンでない物）と、透明な当たり用の板はそのまま。音量の板・人数の札にはかける（IsExtraPlate）。
// ボタンを作り直すメニューを使ってもアップロードの前に自動でかかる（IVRCSDKBuildRequestedCallback）。何度実行してもよい
public class FuwaButtonGradient : IVRCSDKBuildRequestedCallback
{
    const string GradPath = "Assets/FuwaCourse/Textures/RoundedPanelGrad.png";
    public int callbackOrder => 0;

    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (requestedBuildType == VRCSDKRequestedBuildType.Scene) Debug.Log(Run());
        return true;
    }

    [MenuItem("FuwaCourse/よく使う/ボタンにグラデーションをかける")]
    public static void Menu()
    {
        Debug.Log(Run());
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    // ボタンでなくてもかける板（2026-11-21 てじゃ「ボリュームとかいまいるとこのヤツもこれつけたい」）：
    //  音量バーの板（BgmVolume / 蓄音機の VolumeBar の Back）と、いまいる人数の札（HereCount / SoloCount の Plate）
    static bool IsExtraPlate(UnityEngine.UI.Image img)
    {
        var t = img.transform; var owner = t.parent != null ? t.parent.parent : null;   // Back/Plate → Canvas → 持ち主
        if (owner == null) return false;
        if (img.name == "Back" && (owner.name == "BgmVolume" || owner.name == "VolumeBar")) return true;
        if (img.name == "Plate" && owner.name.EndsWith("Count")) return true;
        return false;
    }

    public static string Run()
    {
        var grad = AssetDatabase.LoadAssetAtPath<Sprite>(GradPath);
        if (grad == null) return "RoundedPanelGrad がない";
        int n = 0;
        foreach (var img in Object.FindObjectsOfType<UnityEngine.UI.Image>(true))
        {
            if (img.sprite == null || img.sprite.name != "RoundedPanel") continue;
            if (img.GetComponent<UnityEngine.UI.Selectable>() == null && !IsExtraPlate(img)) continue;   // 押せるボタン＋音量の板・人数の札
            if (img.color.a < 0.5f) continue;                                       // 透明な当たり用の板
            Undo.RecordObject(img, "button gradient");
            img.sprite = grad; EditorUtility.SetDirty(img); n++;
        }
        return "button gradient: " + n;
    }
}
