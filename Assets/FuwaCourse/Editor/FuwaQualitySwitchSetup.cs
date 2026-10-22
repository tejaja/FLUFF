using System.Collections.Generic;
using System.Linq;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

// ロビーの説明ボード（FuwaLobby/HowToPlay）の左の列（JP/EN・HUD・音量）の下と、タイトルの設定の台に「きれい / ふつう / かるい」ボタンを作る（2026-10-22）。
// HUD ボタン（HudButton）を複製して、中身を「がしつ ◀ ふつう ▶」（おっかけカメラのボタンと同じ形）に作り替える。何度実行しても同じ結果。
// 2026-10-22 てじゃ「なんとか画質設定であるってことを付け加えたい、でも長さが足りない」→「がしつ：◁ふつう▷ みたいに」「おっかけカメラにつかってるやつみたいな」
public static class FuwaQualitySwitchSetup
{
    const string Name = "QualityButton";
    static readonly Vector3 Pos = new Vector3(-2.09f, 0.41f, 0f);
    static readonly Vector3 TitlePos = new Vector3(0f, 1.26f, 0f);   // タイトルの台：言語（-1.05, 1.86）と音量（1.05, 1.86）の真ん中の下
    static readonly Color BtnColor = new Color(0.46f, 0.62f, 0.90f, 1f);
    static readonly string[] Ja = { "きれい", "ふつう", "かるい" };
    static readonly string[] En = { "High", "Mid", "Low" };
    const string HeadJa = "がしつ", HeadEn = "Quality";

    [MenuItem("FuwaCourse/済み（押す前に相談）/ロビーとタイトルの がしつ（きれい・ふつう・かるい）ボタンを作る")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var howTo = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "HowToPlay" && t.parent != null && t.parent.name == "FuwaLobby");
        // タイトルの設定の台（言語・音量の2個の真ん中の下）にも同じボタン（2026-10-22 てじゃ「タイトルの今ある２個のボタンの中央下につけよう」）
        var stand = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "SettingsStand" && t.parent != null && t.parent.name == "TitleBoard");
        var lod = Object.FindObjectOfType<FuwaCourseLod>(true);
        var lang = Object.FindObjectOfType<FuwaLanguage>(true);
        var log = new List<string>();
        var texts = new List<(TextMeshProUGUI t, string ja, string en)>();
        var switches = new List<FuwaQualitySwitch>();
        foreach (var (parent, pos) in new[] { (howTo, Pos), (stand, TitlePos) })
        {
            var sw = Make(parent, pos, lod, texts);
            switches.Add(sw);
            log.Add("[きれい・ふつう・かるい] " + UnityEditor.AnimationUtility.CalculateTransformPath(sw.transform, null));
        }

        // どれかのボタンで切り替えたら、ほかのボタンの表示も合わせる
        Undo.RecordObject(lod, "quality switches");
        lod.switches = switches.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(lod); EditorUtility.SetDirty(lod);

        // 英語の時の文字（FuwaLanguage の UI 文字の一覧に足す。前に足した分は入れ替え）
        if (lang != null)
        {
            Undo.RecordObject(lang, "quality lang");
            var ui = lang.uiTexts.ToList(); var ja = lang.uiJa.ToList(); var en = lang.uiEn.ToList();
            for (int k = ui.Count - 1; k >= 0; k--) if (ui[k] == null) { ui.RemoveAt(k); ja.RemoveAt(k); en.RemoveAt(k); }
            foreach (var (t, j, e) in texts) { ui.Add(t); ja.Add(j); en.Add(e); }
            lang.uiTexts = ui.ToArray(); lang.uiJa = ja.ToArray(); lang.uiEn = en.ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(lang); EditorUtility.SetDirty(lang);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(howTo.gameObject.scene);
        return string.Join("\n", log);
    }

    // HUD ボタン（HudButton）を複製して作る。タイトルの台の HudButton は隠してあるので、複製は出す。
    // 並びはおっかけカメラのボタン（FuwaLobby/PhotoStudio/CamButton）と同じ：左に見出し、白い札（◀ 名前 ▶）、札の左半分（と見出し）で前、右半分で次
    static FuwaQualitySwitch Make(Transform parent, Vector3 pos, FuwaCourseLod lod, List<(TextMeshProUGUI, string, string)> allTexts)
    {
        var src = parent.Find("HudButton");
        var old = parent.Find(Name); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var go = Object.Instantiate(src.gameObject, parent); go.name = Name; go.SetActive(true);
        Undo.RegisterCreatedObjectUndo(go, "quality");
        go.transform.localPosition = pos; go.transform.localRotation = src.localRotation; go.transform.localScale = src.localScale;
        var btn = go.transform.Find("Canvas/Button");
        var srcPill = src.Find("Canvas/Button/Seg/PillA").GetComponent<Image>();
        var srcText = src.Find("Canvas/Button/Seg/TextA").GetComponent<TextMeshProUGUI>();
        var srcHead = src.Find("Canvas/Button/Seg/Head").GetComponent<TextMeshProUGUI>();
        var srcArrow = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "ArrowL" && t.parent != null && t.parent.name == "Seg"
            && t.parent.parent != null && t.parent.parent.parent != null && t.parent.parent.parent.parent != null && t.parent.parent.parent.parent.name == "CamButton").GetComponent<TextMeshProUGUI>();

        // 複製した HUD の中身（文字・2択の札・押した時の処理）は外して、下地の色だけ残す
        Object.DestroyImmediate(btn.GetComponent<Button>());
        btn.GetComponent<Image>().color = BtnColor;
        foreach (Transform c in btn.Cast<Transform>().ToList())
        {
            foreach (var u in c.GetComponentsInChildren<UdonSharpBehaviour>(true))
            {
                var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(u);
                Object.DestroyImmediate(u); if (ub) Object.DestroyImmediate(ub);
            }
            Object.DestroyImmediate(c.gameObject);
        }

        var sw = UdonSharpUndo.AddComponent<FuwaQualitySwitch>(go);
        var ub0 = UdonSharpEditorUtility.GetBackingUdonBehaviour(sw);
        RectTransform Rect(GameObject o, Vector2 p, Vector2 size)
        {
            var r = (RectTransform)o.transform; r.SetParent(btn, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.anchoredPosition = p; r.sizeDelta = size; return r;
        }
        TextMeshProUGUI Text(TextMeshProUGUI from, string name, Vector2 p, Vector2 size)
        {
            var o = Object.Instantiate(from.gameObject); o.name = name; o.SetActive(true); Rect(o, p, size);
            var t = o.GetComponent<TextMeshProUGUI>(); t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false; return t;
        }
        // 見出し「がしつ」（英語は Quality。長いので自動で縮める）
        var head = Text(srcHead, "Head", new Vector2(-415f, 0f), new Vector2(510f, 400f));   // 札の左はし（-140）まで広げる（2026-10-22 てじゃ「もうちょい大きくてもはいる」）
        head.text = HeadJa; head.enableAutoSizing = true; head.fontSizeMax = 177f; head.fontSizeMin = 60f;
        allTexts.Add((head, HeadJa, HeadEn));
        // 白い札
        var pill = new GameObject("Pill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); Rect(pill, new Vector2(190f, 0f), new Vector2(660f, 260f));
        var pi = pill.GetComponent<Image>(); pi.sprite = srcPill.sprite; pi.type = srcPill.type; pi.pixelsPerUnitMultiplier = srcPill.pixelsPerUnitMultiplier;
        pi.color = srcPill.color; pi.material = srcPill.material; pi.raycastTarget = false;
        // ◀ ▶（おっかけカメラの矢印を写して、色だけボタンの色に）
        foreach (var (nm, x, ch) in new[] { ("ArrowL", -95f, "◀"), ("ArrowR", 475f, "▶") })
        {
            var a = Text(srcArrow, nm, new Vector2(x, 4f), new Vector2(120f, 400f)); a.text = ch; a.color = BtnColor;
        }
        // 3つの名前（同じ場所に重ねて、選んでいる1つだけ出す）
        var texts = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            var t = Text(srcText, "Text" + i, new Vector2(190f, 0f), new Vector2(440f, 400f));
            t.text = Ja[i]; t.enableAutoSizing = false; t.fontSize = 165f; t.color = BtnColor * new Color(0.85f, 0.85f, 0.85f, 1f); t.enabled = i == 1;
            texts[i] = t; allTexts.Add((t, Ja[i], En[i]));
        }
        // 押す所：左（見出し＋札の左半分）で前、札の右半分で次
        foreach (var (nm, x, w, ev) in new[] { ("PrevButton", -255f, 890f, "Prev"), ("NextButton", 445f, 510f, "Next") })
        {
            var o = new GameObject(nm, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button)); Rect(o, new Vector2(x, 0f), new Vector2(w, 400f));
            var hit = o.GetComponent<Image>(); hit.color = new Color(1, 1, 1, 0); hit.raycastTarget = true;
            var b = o.GetComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = hit;
            UnityEventTools.AddStringPersistentListener(b.onClick, ub0.SendCustomEvent, ev);
        }
        sw.lod = lod; sw.texts = texts;
        UdonSharpEditorUtility.CopyProxyToUdon(sw);
        FuwaArrowHover.Apply(go.transform.Find("Canvas"));   // 当たっている側の矢印だけ濃く
        ub0.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub0);
        EditorUtility.SetDirty(sw);
        return sw;
    }
}
