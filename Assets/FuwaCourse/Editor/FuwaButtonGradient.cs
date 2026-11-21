using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

// ボタンの板にうすいグラデーション（2026-11-21 てじゃ「各種ボタンの見た目をちょっといじりたい」「ちょっとグラデあるかんじ」→見本を見て「おおいいね」）。
// 押せるボタン（Selectable のある Image）で、角丸の板 RoundedPanel を使っている物に、材質 UIButtonGradient（シェーダー Fuwa/UIGradient）をつける。
// 下に向かって色味のある影：色つきは自分の色で濃く・あざやかに、白っぽいのはうすい青むらさき（看板と同じ考え方。
//  2026-11-21 はじめは白黒のグラデの絵 RoundedPanelGrad を掛けていたが、てじゃ「黒だとくすんでみえる」→ 看板を色味の影にして「さりげなくていいね」→「ボタンもやってみよ」）
// ボタンの中の白い札・看板の枠（ボタンでない物）と、透明な当たり用の板はそのまま。音量の板・人数の札にはかける（IsExtraPlate）。
// ボタンを作り直すメニューを使ってもアップロードの前に自動でかかる（IVRCSDKBuildRequestedCallback）。何度実行してもよい
public class FuwaButtonGradient : IVRCSDKBuildRequestedCallback
{
    const string PanelPath = "Assets/FuwaCourse/Textures/RoundedPanel.png";
    const string MatPath = "Assets/FuwaCourse/Materials/UIButtonGradient.mat";
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
        var panel = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
        var sh = Shader.Find("Fuwa/UIGradient");
        if (panel == null || sh == null) return "RoundedPanel か Fuwa/UIGradient がない";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(sh) { name = "UIButtonGradient" }; AssetDatabase.CreateAsset(mat, MatPath); }
        mat.shader = sh;
        int n = 0;
        foreach (var img in Object.FindObjectsOfType<UnityEngine.UI.Image>(true))
        {
            if (img.sprite == null || (img.sprite.name != "RoundedPanel" && img.sprite.name != "RoundedPanelGrad")) continue;
            if (img.GetComponent<UnityEngine.UI.Selectable>() == null && !IsExtraPlate(img)) continue;   // 押せるボタン＋音量の板・人数の札
            if (img.color.a < 0.5f) continue;                                       // 透明な当たり用の板
            if (img.sprite == panel && img.material == mat) continue;
            Undo.RecordObject(img, "button gradient");
            img.sprite = panel; img.material = mat; EditorUtility.SetDirty(img); n++;
        }
        return "button gradient: " + n;
    }
}
