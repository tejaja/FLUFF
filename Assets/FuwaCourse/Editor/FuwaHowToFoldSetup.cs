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
    const float ClosedH = 0.62f, CenterY = 1.96f, TitleOpenY = 2.630f;   // 2026-11-17 文をシンプルにして板を1.04m短くした（元 3.556）→ 開いた時に0.405下げる（openLower＝開いた板のまん中があそびばボタンの高さ）ので 2.630

    [MenuItem("FuwaCourse/済み（押す前に相談）/ロビーのあそびかたをたためるようにする")]
    public static void Menu() { Debug.Log(Run()); }

    // 2026-11-27 てじゃ「展開前のボードとボタンの仕組みが違うのが問題」「ボードの白い部分をベージュ、ハイライトを白に」：
    //  たたんだ帯を、隣のボタンと同じUIのボタン（Fuwa/UIGradient）で作る。オレンジのふち（Button の画像）＋ベージュの中身（Inner、色の変化はこっち）。
    //  たたんだ時は板（Panel・Border）を消す（FuwaHowToFold.hidePanelWhenClosed）。大きさ・丸みは たたんだ板とふちにそろえる
    public static readonly Color Beige = new Color(1f, 0.976f, 0.91f, 0.92f);   // クリーム色（2026-11-27 ベージュ (1,.953,.878)「見た目わるい」→ 白 → てじゃ「もちょいクリーム色っぽく」）
    public static readonly Color BorderCol = new Color(1f, 0.549f, 0.42f, 1f);

    [MenuItem("FuwaCourse/済み（押す前に相談）/ロビーのあそびかた：たたんだ帯をUIボタンにする")]
    public static void MenuClosedButton()
    {
        var h = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "HowToPlay" && t.parent != null && t.parent.name == "FuwaLobby");
        MakeClosedButton(h, h.Find("FoldOpen").gameObject);
        var fold = h.GetComponent<FuwaHowToFold>(); fold.hidePanelWhenClosed = true; UdonSharpEditorUtility.CopyProxyToUdon(fold); EditorUtility.SetDirty(fold);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(h.gameObject.scene);
        Debug.Log("あそびかた：たたんだ帯をUIボタンにした");
    }

    public static void MakeClosedButton(Transform h, GameObject open)
    {
        var panel = h.Find("Panel").GetComponent<SpriteRenderer>();
        var border = h.Find("Border").GetComponent<SpriteRenderer>();
        panel.color = Beige; EditorUtility.SetDirty(panel);   // 開いた板もベージュに
        var spr = panel.sprite;
        float bLen = spr.border.y / spr.pixelsPerUnit;                       // 板の丸み（m、看板のローカル）
        float thick = (border.size.x * border.transform.localScale.x - panel.size.x) * 0.5f;   // ふちの太さ
        float rIn = bLen, rOut = bLen * border.transform.localScale.y;
        var canvas = (RectTransform)open.transform.Find("Canvas");
        float k = canvas.lossyScale.x / h.lossyScale.x;   // キャンバス1単位＝看板のローカル何m
        var btn = open.GetComponentInChildren<UnityEngine.UI.Button>(true);
        var frame = btn.GetComponent<UnityEngine.UI.Image>();
        var src = h.Find("TogetherButton").GetComponentInChildren<UnityEngine.UI.Button>(true).GetComponent<UnityEngine.UI.Image>();
        // 位置：板と同じ所。奥行きは見出し（FoldTitle、z −0.002）より奥・板（+0.03）より手前
        open.transform.localPosition = new Vector3(panel.transform.localPosition.x, panel.transform.localPosition.y, 0.01f);
        float W = panel.size.x + thick * 2f, H = ClosedH + thick * 2f;
        canvas.sizeDelta = new Vector2(W / k, H / k);
        var bc = canvas.GetComponent<BoxCollider>(); bc.size = new Vector3(canvas.sizeDelta.x, canvas.sizeDelta.y, bc.size.z); bc.center = Vector3.zero;
        float ppuRef = spr.pixelsPerUnit / 100f;   // キャンバスの referencePixelsPerUnit = 100
        // ふち（押す所も兼ねる）
        frame.sprite = src.sprite; frame.type = UnityEngine.UI.Image.Type.Sliced; frame.material = src.material; frame.raycastTarget = true;
        frame.color = BorderCol;
        frame.pixelsPerUnitMultiplier = spr.border.y / ppuRef / (rOut / k);
        // 中身（ベージュ、ハイライトはここ）
        var innerT = btn.transform.Find("Inner");
        if (innerT == null) { var g = new GameObject("Inner", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image)); innerT = g.transform; innerT.SetParent(btn.transform, false); innerT.SetAsFirstSibling(); }
        var irt = (RectTransform)innerT; irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.anchoredPosition3D = new Vector3(0f, 0f, -3f) /* ふちと同じ奥行きだとふちに負けるので少し手前 */; irt.sizeDelta = new Vector2(panel.size.x / k, ClosedH / k); irt.localRotation = Quaternion.identity; irt.localScale = Vector3.one;
        var inner = innerT.GetComponent<UnityEngine.UI.Image>();
        inner.sprite = src.sprite; inner.type = UnityEngine.UI.Image.Type.Sliced; inner.material = src.material; inner.raycastTarget = false; inner.color = Color.white;   // 色は Button の色の変化で（ふつう＝クリーム、当てると白）
        inner.pixelsPerUnitMultiplier = spr.border.y / ppuRef / (rIn / k);
        // ハイライト：中身がクリーム→白（てじゃ「ふちが明るくなるだけだと目立たん」「クリーム色っぽく」）
        btn.targetGraphic = inner; btn.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
        var cream = new Color(Beige.r, Beige.g, Beige.b, 1f);   // 下にふちがあるので不透明に
        var cb = btn.colors;
        cb.normalColor = cream; cb.highlightedColor = Color.white; cb.pressedColor = cream * new Color(0.9f, 0.9f, 0.9f, 1f);
        cb.selectedColor = cream; cb.disabledColor = cream; cb.colorMultiplier = 1f; cb.fadeDuration = 0.1f; btn.colors = cb;
        foreach (var o in new Object[] { open.transform, canvas, bc, frame, inner, btn, irt }) EditorUtility.SetDirty(o);
    }

    public static string Run()
    {
        var h = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "HowToPlay" && t.parent != null && t.parent.name == "FuwaLobby");
        var body = h.Find("Text").GetComponent<TMPro.TextMeshPro>();
        var panel = h.Find("Panel").GetComponent<SpriteRenderer>();
        var border = h.Find("Border").GetComponent<SpriteRenderer>();
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        foreach (var n in new[] { "FoldTitle", "FoldOpen", "FoldClose" }) { var o = h.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }

        // 本文の台の案内は「台をクリック」（台のボタンはやめた。2026-10-12 てじゃ）
        string ja0 = "台をクリックで台に戻る", ja1 = "台の前のボタンでも呼べる";
        string en0 = "Click a stand to call it there", en1 = "Or use the button by a stand";
        body.text = body.text.Replace(ja1, ja0); EditorUtility.SetDirty(body);
        if (lang != null)
        {
            int i = System.Array.IndexOf(lang.texts, body);
            if (i >= 0)
            {
                var so = new SerializedObject(lang);
                var pj = so.FindProperty("ja").GetArrayElementAtIndex(i); pj.stringValue = pj.stringValue.Replace(ja1, ja0);
                var pe = so.FindProperty("en").GetArrayElementAtIndex(i); pe.stringValue = pe.stringValue.Replace(en1, en0);
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
            if (invisible)
            {
                // 2026-11-27 てじゃ「隣のボタンはレーザーでハイライトされるのに、あそびかたはされない」：
                //  ふだんは透明、レーザーが当たるとふちのオレンジがうっすら（色の変化で透明度を変える）。角の丸みは板と同じ 0.25m、奥行きは書かない材質
                var img = btn.GetComponent<UnityEngine.UI.Image>();
                img.color = new Color(1f, 0.549f, 0.42f, 1f);
                img.pixelsPerUnitMultiplier = 0.1f;
                img.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/UIIconCullBack.mat");
                var cb = btn.colors; cb.normalColor = new Color(1f, 1f, 1f, 0f); cb.highlightedColor = new Color(1f, 1f, 1f, 0.16f); cb.pressedColor = new Color(1f, 1f, 1f, 0.28f);
                cb.selectedColor = cb.normalColor; cb.disabledColor = cb.normalColor; cb.colorMultiplier = 1f; btn.colors = cb;
                btn.transition = UnityEngine.UI.Selectable.Transition.ColorTint;
            }
            return g;
        }
        var open = Btn("FoldOpen", new Vector3(0f, CenterY, -0.03f), new Vector2(panelW, ClosedH), "", "Open", true);
        float r = 0.34f;
        var close = Btn("FoldClose", new Vector3(panelW * 0.5f - 0.12f, panel.transform.localPosition.y + panel.size.y * 0.5f - 0.12f, -0.03f), new Vector2(r, r), "<b>x</b>", "Close", false);   // × はフォントに無かった（ø になった）
        // ×ボタンは 赤い丸に白い大きい×（文字でなく棒2本で描く。2026-10-12 てじゃ）
        {
            var cbtn = close.GetComponentInChildren<UnityEngine.UI.Button>(true);
            var img = cbtn.GetComponent<UnityEngine.UI.Image>();   // 丸のまま（てじゃ「×を大きくするだけ」「丸をもうちょい赤寄りに」）
            var cols = cbtn.colors; cols.normalColor = new Color(0.93f, 0.3f, 0.27f); cols.highlightedColor = new Color(0.98f, 0.42f, 0.38f);
            cols.pressedColor = new Color(0.75f, 0.25f, 0.18f); cols.selectedColor = cols.normalColor; cbtn.colors = cols;
            close.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text = "";
            // 丸は奥行きを書かない普通のUIの材質で。Fuwa/UIDepth だと丸のふちの半透明な所が奥行きを書いて、
            // あとから描く看板の板・ふちがそこだけ欠けて、空が透けた隙間に見えた（2026-10-12 てじゃ）
            // 2026-11-05: 裏から見えないように、奥行きを書かず裏を描かない UIIconCullBack に
            img.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/UIIconCullBack.mat");
            var size = ((RectTransform)close.transform.Find("Canvas")).sizeDelta.x;
            foreach (float ang in new[] { 45f, -45f })
            {
                var bar = new GameObject("XBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                var brt = (RectTransform)bar.transform; brt.SetParent(cbtn.transform, false);
                brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.sizeDelta = new Vector2(size * 0.62f, size * 0.15f);
                brt.localRotation = Quaternion.Euler(0f, 0f, ang);
                var bi = bar.GetComponent<UnityEngine.UI.Image>(); bi.color = Color.white; bi.raycastTarget = false; bi.material = img.material;
                bi.sprite = img.sprite; bi.type = UnityEngine.UI.Image.Type.Sliced; bi.pixelsPerUnitMultiplier = 2f;
            }
            // 押せる範囲は見た目の丸より大きく（2026-10-13 てじゃ「VRだとレーザー狙いづらいかも、判定でっかくしちゃお」）。
            // ボタンの画像は透明な大きい四角（レーザーが当たる所）にして、赤い丸と×はその子（Dot）に移す。色の変化は Dot にかける
            {
                // てじゃ「右上の余白に広げよう」「でかすぎるのもあれだし、ボタン１個分くらい余白に広げるイメージ」：
                // 丸の右・上はそのまま、左と下へ丸1個分（0.34）ずつ広げた四角
                const float hitW = 0.68f, hitH = 0.68f;
                float right = close.transform.localPosition.x + r * 0.5f, top = close.transform.localPosition.y + r * 0.5f;
                var hitCenter = new Vector2(right - hitW * 0.5f, top - hitH * 0.5f);
                var canvas = (RectTransform)close.transform.Find("Canvas");
                float k = canvas.lossyScale.x / h.lossyScale.x;
                var off = hitCenter - (Vector2)close.transform.localPosition;   // 押せる範囲の真ん中（×の丸からのずれ）
                canvas.localPosition = new Vector3(off.x, off.y, canvas.localPosition.z);
                canvas.sizeDelta = new Vector2(hitW / k, hitH / k);
                var bc = canvas.GetComponent<BoxCollider>(); bc.size = new Vector3(canvas.sizeDelta.x, canvas.sizeDelta.y, bc.size.z); bc.center = Vector3.zero;
                var brt0 = (RectTransform)cbtn.transform; brt0.anchorMin = Vector2.zero; brt0.anchorMax = Vector2.one; brt0.offsetMin = brt0.offsetMax = Vector2.zero;
                var dot = new GameObject("Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                var drt = (RectTransform)dot.transform; drt.SetParent(cbtn.transform, false);
                drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f); drt.sizeDelta = new Vector2(size, size);
                drt.anchoredPosition = new Vector2(-off.x / k, -off.y / k);   // 丸は元の位置のまま
                var di = dot.GetComponent<UnityEngine.UI.Image>(); di.sprite = img.sprite; di.type = img.type; di.pixelsPerUnitMultiplier = img.pixelsPerUnitMultiplier; di.color = Color.white; di.raycastTarget = false; di.material = img.material;
                foreach (var bar in cbtn.transform.Cast<Transform>().Where(t => t.name == "XBar").ToList()) bar.SetParent(dot.transform, false);
                img.sprite = null; img.color = new Color(1f, 1f, 1f, 0f); img.raycastTarget = true;
                cbtn.targetGraphic = di;
            }
        }

        MakeClosedButton(h, open);
        fold.hidePanelWhenClosed = true;
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
