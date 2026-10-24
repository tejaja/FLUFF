using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 動画プレイヤーの操作パネルの見た目（2026-10-15 てじゃ「ボタンとかオシャレにしたい」→ 3案を順番に見る）
// 0 = A：シンプルなメディアプレイヤー風（角の丸い半透明の黒、背景なしの白いアイコン、細いバー＋丸いつまみ）
// 1 = B：クリーム（FLUFFになじむ風）（白いボタン＋オレンジの枠、紺のアイコン、クリーム色のパネル）
// 2 = C：ぽってり風（白っぽいパネル、ラベンダーの丸っこいボタンにやわらかい影。色はあそびばのテレビに合わせた）
// 作るのは TejaVideoPlayerBuilder。ここは出来上がった物の色・形だけを変える（何度かけても同じ）
public static class TejaVideoStyle
{
    const string Dir = "Assets/TejaVideoPlayer/";
    static readonly string[] Buttons = { "UrlInput", "CopyUrl", "PlayPause", "Loop", "Stop", "Resync" };

    class S
    {
        public Color panel, btn, btnHi, btnPress, btnDisabled, icon, accent, track, handle, time, status, tab, tabIcon;
        public bool btnBg, outline, shadow; public Color outlineCol, shadowCol;
        public float trackH, handleSize;
    }

    static S Get(int style)
    {
        if (style == 1) return new S   // B：クリーム
        {
            panel = new Color(1f, 0.98f, 0.95f, 0.93f), btn = Color.white, btnHi = new Color(1f, 0.93f, 0.88f), btnPress = new Color(0.92f, 0.86f, 0.82f), btnDisabled = new Color(0.85f, 0.85f, 0.85f, 0.6f),
            icon = new Color(0.18f, 0.21f, 0.38f), accent = new Color(0.95f, 0.55f, 0.45f), track = new Color(0.18f, 0.21f, 0.38f, 0.15f), handle = new Color(0.95f, 0.55f, 0.45f),
            time = new Color(0.18f, 0.21f, 0.38f), status = new Color(0.85f, 0.4f, 0.3f), tab = new Color(1f, 0.98f, 0.95f, 0.93f), tabIcon = new Color(0.18f, 0.21f, 0.38f),
            btnBg = true, outline = true, outlineCol = new Color(0.95f, 0.55f, 0.45f), trackH = 14f, handleSize = 40f,
        };
        if (style == 2) return new S   // C：ぽってり。色はあそびばのテレビ（おっかけカメラ）に合わせる：白い雲の枠・ラベンダーのふち・コーラルの見出し（2026-10-15 てじゃ「Cで色あわせ」）
        {
            panel = new Color(0.99f, 0.98f, 1f, 0.95f), btn = new Color(0.62f, 0.55f, 0.86f), btnHi = new Color(0.72f, 0.66f, 0.93f), btnPress = new Color(0.52f, 0.45f, 0.76f), btnDisabled = new Color(0.8f, 0.77f, 0.88f, 0.7f),
            icon = Color.white, accent = new Color(1f, 0.42f, 0.36f), track = new Color(0.62f, 0.55f, 0.86f, 0.22f), handle = Color.white,
            time = new Color(0.25f, 0.22f, 0.3f), status = new Color(1f, 0.42f, 0.36f), tab = new Color(0.99f, 0.98f, 1f, 0.95f), tabIcon = new Color(0.62f, 0.55f, 0.86f),
            btnBg = true, shadow = true, shadowCol = new Color(0.36f, 0.3f, 0.55f, 0.35f), trackH = 18f, handleSize = 46f,
        };
        return new S   // A：シンプル
        {
            panel = new Color(0.05f, 0.06f, 0.08f, 0.72f), btn = new Color(1f, 1f, 1f, 0f), btnHi = new Color(1f, 1f, 1f, 0.14f), btnPress = new Color(1f, 1f, 1f, 0.26f), btnDisabled = new Color(1f, 1f, 1f, 0f),
            icon = Color.white, accent = new Color(1f, 0.47f, 0.42f), track = new Color(1f, 1f, 1f, 0.22f), handle = Color.white,
            time = new Color(0.85f, 0.88f, 0.95f), status = new Color(1f, 0.8f, 0.6f), tab = new Color(0.05f, 0.06f, 0.08f, 0.72f), tabIcon = Color.white,
            btnBg = false, trackH = 10f, handleSize = 34f,
        };
    }

    public static void Apply(Transform controls, int style)
    {
        var s = Get(style);
        var round = Sprite9("UI_Round.png", 40); var outlineSp = Sprite9("UI_RoundOutline.png", 40); var circle = Sprite9("UI_Circle.png", 0); var pill = Sprite9("UI_Pill.png", 16);
        var panel = controls.Find("Panel");

        // パネル・たたむタブ
        Img(panel, round, s.panel);
        var tab = controls.Find("PanelTab");
        if (tab != null)
        {
            Img(tab, round, s.tab);
            var tl = tab.Find("Label"); if (tl != null) tl.GetComponent<Text>().color = s.tabIcon;
            Colors(tab.GetComponent<Selectable>(), Color.white, new Color(1f, 1f, 1f, 0.85f), new Color(0.85f, 0.85f, 0.85f), Color.white);
            tab.GetComponent<Image>().color = s.tab;
        }

        // ボタン（URL・コピー・再生・停止・再同期）
        foreach (var n in Buttons)
        {
            var b = panel.Find(n); if (b == null) continue;
            var img = b.GetComponent<Image>();
            img.sprite = round; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1f;
            // 色の変化は Selectable の ColorBlock で（元の色は白にして、ColorBlock の色がそのまま出るように）
            img.color = Color.white;
            Colors(b.GetComponent<Selectable>(), s.btn, s.btnHi, s.btnPress, s.btnDisabled);
            // 枠（B）
            var ol = b.Find("Outline");
            if (s.outline)
            {
                if (ol == null) { var g = new GameObject("Outline", typeof(RectTransform), typeof(Image)); g.transform.SetParent(b, false); g.transform.SetSiblingIndex(0); ol = g.transform; }
                var ort = (RectTransform)ol; ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one; ort.offsetMin = ort.offsetMax = Vector2.zero;
                var oi = ol.GetComponent<Image>(); oi.sprite = outlineSp; oi.type = Image.Type.Sliced; oi.color = s.outlineCol; oi.raycastTarget = false;
            }
            else if (ol != null) Object.DestroyImmediate(ol.gameObject);
            // 影（C）
            var sh = b.GetComponent<Shadow>();
            if (s.shadow) { if (sh == null) sh = b.gameObject.AddComponent<Shadow>(); sh.effectColor = s.shadowCol; sh.effectDistance = new Vector2(0f, -8f); }
            else if (sh != null) Object.DestroyImmediate(sh);
            // アイコン・文字（▶ ■）の色
            foreach (var g in b.GetComponentsInChildren<Graphic>(true))
            {
                if (g.transform == b || g.name == "Outline") continue;
                if (g.name == "Icon" || g.name == "Label") g.color = s.icon;
            }
        }

        // くり返しのボタン：OFFはコピーの「押せない時」の色、ONは緑（2026-10-15 てじゃ「コピーボタンの無効化カラーと揃えて」「繰り返しオンを緑に」）。
        // ボタンは押せるままにしたいので無効化はせず、色はボタンの絵の色で出す。ColorBlock は明るさの変化だけ（ふだん0.9・乗ると1・押すと0.78）
        // → 絵の色＝ほしい色÷0.9。押した時の色の変化は残る。最初はOFFの見た目
        var loopB = panel.Find("Loop");
        var vp = controls.GetComponentInParent<TejaVideoPlayer>(true);
        if (vp == null) vp = Object.FindObjectsOfType<TejaVideoPlayer>(true).Length > 0 ? Object.FindObjectsOfType<TejaVideoPlayer>(true)[0] : null;
        if (loopB != null && vp != null)
        {
            const float LoopNormal = 0.9f;
            Colors(loopB.GetComponent<Selectable>(), Gray(LoopNormal), Color.white, Gray(0.78f), Gray(LoopNormal));
            vp.loopOffTint = s.btn.a < 0.01f ? new Color(1f, 1f, 1f, 0f) : Div(s.btnDisabled, LoopNormal);
            vp.loopOnColor = Div(new Color(0.45f, 0.8f, 0.58f, 1f), LoopNormal);
            vp.loopOffIconAlpha = 1f;   // コピーも押せない時はアイコンは白のまま
            loopB.GetComponent<Image>().color = vp.loopOffTint;
            var li = loopB.Find("Icon"); if (li != null) li.GetComponent<Image>().color = s.icon;
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(vp);
            EditorUtility.SetDirty(vp);
        }

        // スピーカー
        var mute = panel.Find("MuteButton");
        if (mute != null)
        {
            mute.GetComponent<Image>().color = Color.white;
            var mc = style == 2 ? s.tabIcon : s.icon;   // C はボタンの外なので、白いと薄い背景に溶ける → ラベンダー
            Colors(mute.GetComponent<Selectable>(), mc, Color.Lerp(mc, s.accent, 0.5f), s.accent, mc);
        }

        // シークバー・音量
        foreach (var n in new[] { "Seek", "Volume" }) Slider(panel.Find(n), s, pill, circle);

        // 時間・状態の文字
        var tm = panel.Find("Time"); if (tm != null) tm.GetComponent<Text>().color = s.time;
        var ti = panel.Find("Title"); if (ti != null) ti.GetComponent<Text>().color = s.time;
        var st = panel.Find("Status"); if (st != null) st.GetComponent<Text>().color = s.status;
        EditorUtility.SetDirty(controls.gameObject);
    }

    static Color Gray(float v) { return new Color(v, v, v, 1f); }
    static Color Div(Color c, float k) { return new Color(Mathf.Clamp01(c.r / k), Mathf.Clamp01(c.g / k), Mathf.Clamp01(c.b / k), c.a); }

    static void Slider(Transform t, S s, Sprite pill, Sprite circle)
    {
        if (t == null) return;
        var bg = t.Find("Background"); var fillArea = t.Find("Fill Area"); var fill = t.Find("Fill Area/Fill"); var handleArea = t.Find("Handle Slide Area"); var handle = t.Find("Handle Slide Area/Handle");
        // バーは細く、縦の真ん中に
        foreach (var r in new[] { bg, fillArea })
        {
            if (r == null) continue;
            var rt = (RectTransform)r; rt.anchorMin = new Vector2(0f, 0.5f); rt.anchorMax = new Vector2(1f, 0.5f); rt.sizeDelta = new Vector2(r == fillArea ? -20f : 0f, s.trackH); rt.anchoredPosition = new Vector2(r == fillArea ? -5f : 0f, 0f);
        }
        if (bg != null) Img(bg, pill, s.track);
        if (fill != null) { Img(fill, pill, s.accent); var frt = (RectTransform)fill; frt.sizeDelta = new Vector2(10f, 0f); }
        if (handle != null)
        {
            Img(handle, circle, s.handle); handle.GetComponent<Image>().type = Image.Type.Simple;
            var hrt = (RectTransform)handle; hrt.sizeDelta = new Vector2(s.handleSize, 0f);
            // つまみは丸く（縦は Handle Slide Area の高さいっぱいなので、高さ＝つまみの大きさになるよう Handle Slide Area を細くする）
            var hart = (RectTransform)handleArea; hart.anchorMin = new Vector2(0f, 0.5f); hart.anchorMax = new Vector2(1f, 0.5f); hart.sizeDelta = new Vector2(-20f, s.handleSize); hart.anchoredPosition = Vector2.zero;
            var sh = handle.GetComponent<Shadow>();
            if (s.shadow) { if (sh == null) sh = handle.gameObject.AddComponent<Shadow>(); sh.effectColor = s.shadowCol; sh.effectDistance = new Vector2(0f, -4f); }
            else if (sh != null) Object.DestroyImmediate(sh);
        }
        var sel = t.GetComponent<Selectable>(); if (sel != null) Colors(sel, Color.white, new Color(1f, 1f, 1f, 1f), new Color(0.9f, 0.9f, 0.9f), new Color(1f, 1f, 1f, 0.5f));
    }

    static void Img(Transform t, Sprite sp, Color c)
    {
        if (t == null) return;
        var i = t.GetComponent<Image>(); if (i == null) return;
        i.sprite = sp; i.type = Image.Type.Sliced; i.color = c;
    }

    static void Colors(Selectable sel, Color normal, Color hi, Color press, Color disabled)
    {
        if (sel == null) return;
        var cb = sel.colors; cb.normalColor = normal; cb.highlightedColor = hi; cb.pressedColor = press; cb.selectedColor = normal; cb.disabledColor = disabled; cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
        sel.colors = cb; sel.transition = Selectable.Transition.ColorTint;
    }

    // 9スライスのスプライト（角の丸み border px はそのまま、真ん中だけ伸びる）
    static Sprite Sprite9(string file, int border)
    {
        string path = Dir + file;
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp != null)
        {
            var b = new Vector4(border, border, border, border);
            if (imp.textureType != TextureImporterType.Sprite || imp.spriteBorder != b || !imp.alphaIsTransparency)
            {
                imp.textureType = TextureImporterType.Sprite; imp.alphaIsTransparency = true; imp.mipmapEnabled = false; imp.spriteBorder = b;
                imp.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    [MenuItem("Tools/Teja Player/見た目/A シンプル")] static void MA() { ApplyScene(0); }
    [MenuItem("Tools/Teja Player/見た目/B クリーム")] static void MB() { ApplyScene(1); }
    [MenuItem("Tools/Teja Player/見た目/C ぽってり")] static void MC() { ApplyScene(2); }

    // シーンの Teja Player の操作パネル全部（選んでいる物があれば、その中のプレイヤーだけ）に見た目をかける
    public static void ApplyScene(int style)
    {
        var all = Object.FindObjectsOfType<TejaVideoPlayer>(true);
        var sel = Selection.transforms;
        int n = 0;
        foreach (var vp in all)
        {
            if (vp.panelRoot == null || vp.panelRoot.transform.parent == null) continue;
            var c = vp.panelRoot.transform.parent;
            if (sel.Length > 0 && !sel.Any(t => c.IsChildOf(t) || vp.transform.IsChildOf(t))) continue;
            Apply(c, style);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
            n++;
        }
        if (n == 0) Debug.LogWarning("Teja Player の操作パネルが見つからない");
    }
}
