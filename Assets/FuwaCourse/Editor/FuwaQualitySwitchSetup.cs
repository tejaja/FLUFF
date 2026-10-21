using System.Linq;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

// ロビーの説明ボード（FuwaLobby/HowToPlay）の左の列（JP/EN・HUD・音量）の下に「きれい / ふつう / かるい」ボタンを作る（2026-10-22）。
// HUD ボタン（HudButton）を複製して、中身を3つの押せる札に作り替える。何度実行しても同じ結果
public static class FuwaQualitySwitchSetup
{
    const string Name = "QualityButton";
    static readonly Vector3 Pos = new Vector3(-2.09f, 0.41f, 0f);
    static readonly Color BtnColor = new Color(0.46f, 0.62f, 0.90f, 1f);
    static readonly string[] Ja = { "きれい", "ふつう", "かるい" };
    static readonly string[] En = { "High", "Mid", "Low" };
    static readonly string[] Events = { "SetKirei", "SetFutsuu", "SetKarui" };

    [MenuItem("FuwaCourse/済み（押す前に相談）/ロビーの きれい・ふつう・かるい ボタンを作る")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var howTo = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "HowToPlay" && t.parent != null && t.parent.name == "FuwaLobby");
        var src = howTo.Find("HudButton");
        var old = howTo.Find(Name); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var lod = Object.FindObjectOfType<FuwaCourseLod>(true);
        var lang = Object.FindObjectOfType<FuwaLanguage>(true);

        var go = Object.Instantiate(src.gameObject, howTo); go.name = Name;
        Undo.RegisterCreatedObjectUndo(go, "quality");
        go.transform.localPosition = Pos; go.transform.localRotation = src.localRotation; go.transform.localScale = src.localScale;
        var btn = go.transform.Find("Canvas/Button");
        var srcPill = src.Find("Canvas/Button/Seg/PillA").GetComponent<Image>();
        var srcText = src.Find("Canvas/Button/Seg/TextA").GetComponent<TextMeshProUGUI>();
        var srcSeg = src.Find("Canvas/Button/Seg").GetComponent<FuwaSegToggle>();

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
        var pills = new Image[3]; var texts = new TextMeshProUGUI[3];
        var ub0 = UdonSharpEditorUtility.GetBackingUdonBehaviour(sw);
        const float step = 450f;
        for (int i = 0; i < 3; i++)
        {
            var opt = new GameObject("Opt" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var ort = (RectTransform)opt.transform; ort.SetParent(btn, false);
            ort.anchorMin = ort.anchorMax = new Vector2(0.5f, 0.5f); ort.sizeDelta = new Vector2(step, 400f); ort.anchoredPosition = new Vector2((i - 1) * step, 0f);
            var hit = opt.GetComponent<Image>(); hit.color = new Color(1, 1, 1, 0); hit.raycastTarget = true;
            var b = opt.GetComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = hit;
            UnityEventTools.AddStringPersistentListener(b.onClick, ub0.SendCustomEvent, Events[i]);

            var pill = new GameObject("Pill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var prt = (RectTransform)pill.transform; prt.SetParent(ort, false); prt.sizeDelta = new Vector2(step - 30f, 260f);
            var pi = pill.GetComponent<Image>(); pi.sprite = srcPill.sprite; pi.type = srcPill.type; pi.pixelsPerUnitMultiplier = srcPill.pixelsPerUnitMultiplier;
            pi.color = srcPill.color; pi.material = srcPill.material; pi.raycastTarget = false;
            pills[i] = pi;

            var txt = Object.Instantiate(srcText.gameObject, ort); txt.name = "Text";
            var trt = (RectTransform)txt.transform; trt.anchoredPosition = Vector2.zero; trt.sizeDelta = new Vector2(step, 400f);
            var t = txt.GetComponent<TextMeshProUGUI>(); t.text = Ja[i]; t.fontSize = 150f; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            texts[i] = t;
        }
        sw.lod = lod; sw.pills = pills; sw.texts = texts;
        sw.activeTextColor = BtnColor * new Color(0.85f, 0.85f, 0.85f, 1f); sw.idleTextColor = srcSeg.idleTextColor;
        UdonSharpEditorUtility.CopyProxyToUdon(sw);
        ub0.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub0);

        // 英語の時の文字（FuwaLanguage の UI 文字の一覧に足す。前に足した分は入れ替え）
        if (lang != null)
        {
            Undo.RecordObject(lang, "quality lang");
            var ui = lang.uiTexts.ToList(); var ja = lang.uiJa.ToList(); var en = lang.uiEn.ToList();
            for (int k = ui.Count - 1; k >= 0; k--) if (ui[k] == null) { ui.RemoveAt(k); ja.RemoveAt(k); en.RemoveAt(k); }
            for (int i = 0; i < 3; i++) { ui.Add(texts[i]); ja.Add(Ja[i]); en.Add(En[i]); }
            lang.uiTexts = ui.ToArray(); lang.uiJa = ja.ToArray(); lang.uiEn = en.ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(lang); EditorUtility.SetDirty(lang);
        }
        EditorUtility.SetDirty(sw);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(howTo.gameObject.scene);
        return "[きれい・ふつう・かるい] " + UnityEditor.AnimationUtility.CalculateTransformPath(go.transform, null);
    }
}
