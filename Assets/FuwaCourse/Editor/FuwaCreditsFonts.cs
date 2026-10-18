using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

// 入口の島の裏のクレジット（FuwaHub/BackIsland/Credits）のフォント一覧で、それぞれの名前をそのフォントで書く（2026-10-12 てじゃ「該当のフォントにしたらオサレ」）。
// 2026-10-12 Kosugi Maru（M PLUS の予備）は使っていなかったので削除。
// 元の文字（M PLUS）の名前の部分は透明にして、同じ位置に名前だけの文字（そのフォント）を重ねる。何度実行しても同じ結果
public static class FuwaCreditsFonts
{
    static readonly (string name, string font)[] Fonts =
    {
        ("Sniglet", "Sniglet SDF"),
        ("Cherry Bomb One", "CherryBombOne SDF"),
        ("Darumadrop One", "DarumadropOne SDF Dyn"),
        ("M PLUS Rounded 1c", "MPLUSRounded1c SDF"),
        ("Yusei Magic", "YuseiMagic SDF Dyn"),
    };
    // 見出しの下・ライセンスの前に少しすき間（てじゃ）。フォント名どうしの行間は少しあけ、ライセンスは小さく（2026-10-15 てじゃ「行間ちょっぴりあける、ライセンスは小さく」）
    const string Plain = "<b>Fonts</b>\n<size=45%> </size>\n<size=75%><line-height=125%>Sniglet\nCherry Bomb One\nDarumadrop One\nM PLUS Rounded 1c\nYusei Magic</line-height></size>\n<size=45%> </size>\n<size=34%>SIL Open Font License 1.1</size>";

    [MenuItem("FuwaCourse/済み（押す前に相談）/クレジットのフォント名をそのフォントで書く")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var c = Object.FindObjectsOfType<TextMeshPro>(true).First(t => t.name == "Credits" && t.transform.parent != null && t.transform.parent.name == "BackIsland");
        // 元の文字：名前の部分を透明に
        string text = Plain;
        foreach (var f in Fonts) text = text.Replace(f.name, "<alpha=#00>" + f.name + "<alpha=#FF>");
        c.text = text; EditorUtility.SetDirty(c);
        c.ForceMeshUpdate();
        var old = c.transform.Find("FontNames"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject("FontNames").transform; holder.SetParent(c.transform, false);
        var info = c.textInfo; var log = new List<string>();
        foreach (var f in Fonts)
        {
            // 透明にした名前の文字の位置（行の基準線・左右のはし）
            int start = c.GetParsedText().IndexOf(f.name);
            if (start < 0) { log.Add(f.name + " not found"); continue; }
            var c0 = info.characterInfo[start]; var c1 = info.characterInfo[start + f.name.Length - 1];
            float cx = (c0.bottomLeft.x + c1.topRight.x) * 0.5f, by = c0.baseLine;
            var fa = AssetDatabase.FindAssets(f.font + " t:TMP_FontAsset").Select(AssetDatabase.GUIDToAssetPath).Select(p => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p)).FirstOrDefault(a => a != null && a.name == f.font);
            if (fa == null) { log.Add(f.font + " missing"); continue; }
            var go = new GameObject(f.name, typeof(RectTransform), typeof(MeshRenderer), typeof(TextMeshPro));
            go.transform.SetParent(holder, false);
            var t = go.GetComponent<TextMeshPro>();
            t.font = fa; t.fontSharedMaterial = fa.material;
            t.fontSize = c.fontSize * 0.75f; t.color = c.color; t.enableWordWrapping = false; t.enableAutoSizing = false;
            t.alignment = TextAlignmentOptions.Baseline;   // 横は真ん中、縦は基準線
            t.rectTransform.sizeDelta = new Vector2(c.rectTransform.sizeDelta.x, 0.1f);
            t.rectTransform.localPosition = new Vector3(cx, by, -0.001f);
            t.text = f.name;
            t.ForceMeshUpdate();
            // 幅が元より広すぎる時は少し小さく（行の中におさめる）
            float w = t.textBounds.size.x, w0 = c1.topRight.x - c0.bottomLeft.x;
            if (w > w0 * 1.25f) t.fontSize *= w0 * 1.25f / w;
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            log.Add(f.name + " ok");
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
        return string.Join(", ", log);
    }
}
