using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// ロビーの「あそびかた」看板をたためるようにする（FuwaHowToFold。2026-10-12 てじゃ）。何度実行してもよい
// ・見出しだけの文字（FoldTitle）、開くボタン（帯ぜんぶを覆う透明なレーザーボタン）、×ボタン（右上）を作る
// ・脚はたたんだ帯の下まで届くようにのばす
public static class FuwaHowToFoldSetup
{
    const float ClosedH = 0.62f, CenterY = 1.96f, TitleOpenY = 3.556f;

    [MenuItem("FuwaCourse/ロビーのあそびかたをたためるようにする")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var h = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "HowToPlay" && t.parent != null && t.parent.name == "FuwaLobby");
        var body = h.Find("Text").GetComponent<TMPro.TextMeshPro>();
        var panel = h.Find("Panel").GetComponent<SpriteRenderer>();
        var border = h.Find("Border").GetComponent<SpriteRenderer>();
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        foreach (var n in new[] { "FoldTitle", "FoldOpen", "FoldClose" }) { var o = h.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }

        // 本文の古い案内（台をクリック → 台の前のボタン）
        string ja0 = "台をクリックで台に戻る", ja1 = "台の前のボタンでも呼べる";
        string en0 = "Click a stand to call it there", en1 = "Or use the button by a stand";
        body.text = body.text.Replace(ja0, ja1); EditorUtility.SetDirty(body);
        if (lang != null)
        {
            int i = System.Array.IndexOf(lang.texts, body);
            if (i >= 0)
            {
                var so = new SerializedObject(lang);
                var pj = so.FindProperty("ja").GetArrayElementAtIndex(i); pj.stringValue = pj.stringValue.Replace(ja0, ja1);
                var pe = so.FindProperty("en").GetArrayElementAtIndex(i); pe.stringValue = pe.stringValue.Replace(en0, en1);
                so.ApplyModifiedPropertiesWithoutUndo(); UdonSharpEditorUtility.CopyProxyToUdon(lang);
            }
        }

        // 見出しだけの文字
        var tg = Object.Instantiate(body.gameObject, h, false); tg.name = "FoldTitle";
        var title = tg.GetComponent<TMPro.TextMeshPro>();
        title.text = "<size=150%><color=#FF6B5C>あそびかた</color></size>";
        title.alignment = TMPro.TextAlignmentOptions.Center;
        title.rectTransform.sizeDelta = new Vector2(body.rectTransform.sizeDelta.x, 0.6f);
        tg.transform.localPosition = new Vector3(0f, CenterY, body.transform.localPosition.z - 0.002f);
        if (lang != null)
        {
            // FoldTitle は言語の配列に入れない（FuwaHowToFold が書く）。コピーで入っていたら消す
            var so = new SerializedObject(lang); var pa = so.FindProperty("texts"); var pj = so.FindProperty("ja"); var pe = so.FindProperty("en");
            for (int k = pa.arraySize - 1; k >= 0; k--)
                if (pa.GetArrayElementAtIndex(k).objectReferenceValue == title) { pa.GetArrayElementAtIndex(k).objectReferenceValue = null; pa.DeleteArrayElementAtIndex(k); pj.DeleteArrayElementAtIndex(k); pe.DeleteArrayElementAtIndex(k); }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 脚：たたんだ帯の下まで
        float stripBottom = CenterY - ClosedH * 0.5f + 0.1f;
        foreach (Transform c in h)
        {
            if (c.name != "Leg") continue;
            float bottom = c.localPosition.y - c.localScale.y;   // 円柱は高さ2
            float half = (stripBottom - bottom) * 0.5f;
            c.localPosition = new Vector3(c.localPosition.x, bottom + half, c.localPosition.z);
            c.localScale = new Vector3(c.localScale.x, half, c.localScale.z);
        }

        // 動かす係
        var fold = h.GetComponent<FuwaHowToFold>();
        if (fold == null)
        {
            string path = "Assets/FuwaCourse/Scripts/FuwaHowToFold.asset";
            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path) == null)
            {
                var prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/FuwaHowToFold.cs");
                AssetDatabase.CreateAsset(prog, path); AssetDatabase.SaveAssets();
            }
            fold = UdonSharpUndo.AddComponent<FuwaHowToFold>(h.gameObject);
        }

        // ボタン（ロビーの「みんなで」ボタンをコピー）
        var src = h.Find("TogetherButton");
        float panelW = panel.size.x;
        GameObject Btn(string name, Vector3 lp, Vector2 sizeLocal, string text, string evt, bool invisible)
        {
            var g = Object.Instantiate(src.gameObject, h, false); g.name = name;
            g.transform.localPosition = lp; g.transform.localRotation = Quaternion.identity;
            var canvas = (RectTransform)g.transform.Find("Canvas");
            float k = canvas.lossyScale.x / h.lossyScale.x;   // キャンバス1単位＝看板のローカル何m
            canvas.sizeDelta = new Vector2(sizeLocal.x / k, sizeLocal.y / k);
            var bc = canvas.GetComponent<BoxCollider>(); bc.size = new Vector3(canvas.sizeDelta.x, canvas.sizeDelta.y, bc.size.z);
            var btn = g.GetComponentInChildren<UnityEngine.UI.Button>(true);
            var so = new SerializedObject(btn); var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls"); calls.arraySize = 1;
            var c0 = calls.GetArrayElementAtIndex(0);
            c0.FindPropertyRelative("m_Target").objectReferenceValue = UdonSharpEditorUtility.GetBackingUdonBehaviour(fold);
            c0.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = "VRC.Udon.UdonBehaviour, VRC.Udon";
            c0.FindPropertyRelative("m_MethodName").stringValue = "SendCustomEvent";
            c0.FindPropertyRelative("m_Mode").intValue = 5;
            c0.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = evt;
            c0.FindPropertyRelative("m_CallState").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
            var tm = g.GetComponentInChildren<TMPro.TextMeshProUGUI>(true); tm.text = text;
            if (lang != null) { int li = System.Array.IndexOf(lang.uiTexts, tm); if (li >= 0) { var ls = new SerializedObject(lang); var pa = ls.FindProperty("uiTexts"); pa.GetArrayElementAtIndex(li).objectReferenceValue = null; ls.ApplyModifiedPropertiesWithoutUndo(); } }
            if (invisible) { var img = btn.GetComponent<UnityEngine.UI.Image>(); var col = img.color; col.a = 0f; img.color = col; }
            return g;
        }
        var open = Btn("FoldOpen", new Vector3(0f, CenterY, -0.03f), new Vector2(panelW, ClosedH), "", "Open", true);
        float r = 0.34f;
        var close = Btn("FoldClose", new Vector3(panelW * 0.5f - 0.12f, panel.transform.localPosition.y + panel.size.y * 0.5f - 0.12f, -0.03f), new Vector2(r, r), "<b>x</b>", "Close", false);   // × はフォントに無かった（ø になった）
        close.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).enableAutoSizing = false;

        fold.panel = panel; fold.border = border; fold.body = body; fold.title = title; fold.language = lang;
        fold.titleT = title.transform; fold.titleGo = title.gameObject; fold.bodyGo = body.gameObject;
        fold.whenClosed = open; fold.whenOpen = close;
        fold.closedHeight = ClosedH; fold.borderScale = border.transform.localScale.y; fold.titleClosedY = CenterY; fold.titleOpenY = TitleOpenY;
        UdonSharpEditorUtility.CopyProxyToUdon(fold);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(fold); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
        // 消した文字の言語登録（null）を詰める
        if (lang != null)
        {
            var ls = new SerializedObject(lang);
            var pa = ls.FindProperty("uiTexts"); var pj = ls.FindProperty("uiJa"); var pe = ls.FindProperty("uiEn");
            for (int i = pa.arraySize - 1; i >= 0; i--)
                if (pa.GetArrayElementAtIndex(i).objectReferenceValue == null) { pa.DeleteArrayElementAtIndex(i); if (i < pj.arraySize) pj.DeleteArrayElementAtIndex(i); if (i < pe.arraySize) pe.DeleteArrayElementAtIndex(i); }
            ls.ApplyModifiedPropertiesWithoutUndo(); UdonSharpEditorUtility.CopyProxyToUdon(lang);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(h.gameObject.scene);
        return "how-to fold ok / " + FuwaUiProximitySetup.Run();
    }
}
