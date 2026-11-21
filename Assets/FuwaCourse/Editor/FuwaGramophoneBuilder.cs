using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// あそびばの蓄音機（2026-11-20 てじゃ「あそびばのBGM変更モード」「みためは蓄音機」「球の右側に」）
// モデルは Blender（BlenderSource/FLUFF_models.blend の Gramophone シーン）→ Models/PlaygroundGramophone.fbx
// FuwaPlayground の直下に置く（Room の作り直しでは消えない）。何度でも置き直せる
public static class FuwaGramophoneBuilder
{
    const string Fbx = "Assets/FuwaCourse/Models/PlaygroundGramophone.fbx";
    const string MatDir = "Assets/FuwaCourse/Materials/Playground";
    public const float Angle = 4f;       // カメラの球（-8°）の右。もどすボタンがいた場所
    public const float Radius = 12.3f;   // 島の真ん中から（柵は 14m）
    public const float Scale = 1.25f;    // 島の真ん中からも見えるよう少し大きく
    static readonly Vector3 Center = new Vector3(-72f, 88f, 80f);
    // ボタンの色（蓄音機の木に合わせた茶色）。Theme＝帯・文字、Dark＝押した時・矢印に乗った時、Idle＝OFF の文字
    static readonly Color Theme = new Color(0.66f, 0.45f, 0.32f), ThemeDark = new Color(0.45f, 0.28f, 0.18f), ThemeIdle = new Color(0.64f, 0.58f, 0.54f);
    const float TrackHeadCut = 250f;    // 曲名の段：左の見出し（♪）を詰める幅（CAM の単位）
    // 下の段（2026-11-21 てじゃ「下のBGMバー撤去、代わりに再生 停止ボタン、蓄音機専用ボリュームバー」）：▶ ■ ＋ 音量バー。世界での大きさ(m)、全体の幅は曲名の段（0.72m）とそろえる
    const float Row2H = 0.13f, BtnW = 0.15f, Gap = 0.02f, RowW = 0.72f;

    // えらべる曲（FuwaBgm.tracks の番号）。おまけ（LockedIndex）は全クリ後だけ出る
    // 2026-11-21 てじゃ「おまかせってなに？」→おまかせは削除。同じ日「BGMなしは削除」「ロビー曲はリストの最後に変更」
    static readonly int[] Tracks = { 6, 1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 7, 14, 0 };
    const int LockedIndex = 12;
    static readonly string[] Ja = { "れんしゅう", "1 かぜのおか", "2 いわのみち", "3 やみのもり", "4 まもののす", "5 きのこのたに",
        "2–1 みずのこみち", "2–2 ほのおのやま", "2–3 こおりのみち", "2–4 はぐるまのしま", "2–5 そらのいせき", "ゴールのあと", "おまけ", "ロビー" };
    static readonly string[] En = { "Practice", "1 Windy Hill", "2 Rock Road", "3 Dark Forest", "4 Monster Den", "5 Mushroom Valley",
        "2-1 Water Path", "2-2 Flame Mountain", "2-3 Ice Road", "2-4 Gear Isles", "2-5 Sky Ruins", "After the goal", "Bonus", "Lobby" };

    static Material Mat(string name, Color c, bool metal = false)
    {
        string p = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("FuwaCourse/ToonLit")) { name = name }; AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_Color", c);
        m.SetColor("_ShadowTint", new Color(0.72f, 0.78f, 0.92f));
        m.SetFloat("_ShadowStrength", 0.4f);
        m.SetFloat("_RimStrength", 0.25f);
        if (metal) { m.SetFloat("_MetalSpec", 0.6f); m.SetFloat("_MetalEnv", 0.25f); m.SetFloat("_MetalSize", 0.92f); }   // 1 だとラッパが白く飛んだ
        EditorUtility.SetDirty(m);
        return m;
    }

    [MenuItem("FuwaCourse/あそびば/蓄音機を置く")]
    public static string Build()
    {
        var pg = GameObject.Find("FuwaPlayground"); if (pg == null) return "FuwaPlayground がない";
        var old = pg.transform.Find("Gramophone"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx); if (src == null) return "FBX がない";
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src); PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = "Gramophone"; go.transform.SetParent(pg.transform, false);

        // 置き場所：デッキの上（床は下に光線を飛ばして決める）、島の真ん中を向く
        float a = Angle * Mathf.Deg2Rad;
        var pos = Center + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * Radius;
        float floorY = Center.y;
        if (Physics.Raycast(pos + Vector3.up * 5f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore)) floorY = hit.point.y;
        pos.y = floorY;
        var toC = Center - pos; toC.y = 0f;
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(toC.normalized));
        go.transform.localScale = Vector3.one * Scale;

        // 材質
        var wood = Mat("Gramo_Wood", new Color(0.86f, 0.62f, 0.42f));
        var woodDark = Mat("Gramo_WoodDark", new Color(0.62f, 0.42f, 0.28f));
        var brass = Mat("Gramo_Brass", new Color(0.86f, 0.62f, 0.3f), true);
        // レコード：上の面に みぞ＋光の帯（回っているのが分かる）＋ラベルに空の太陽 の絵（BlenderSource/gramophone_record_texture.py で作る。UV は真上から）
        //（2026-11-21 てじゃ「レコードが回転してるのわかりづらいから模様つけよう」「真ん中のピンなくして、中心にこのワールドの真上にある太陽のイラスト」）
        var record = Mat("Gramo_Record", Color.white);
        record.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaCourse/Textures/Gramo_RecordTop.png"));
        var label = Mat("Gramo_Label", new Color(1f, 0.62f, 0.68f));
        var plate = Mat("Gramo_Plate", new Color(1f, 0.96f, 0.88f));
        var button = Mat("Gramo_Button", new Color(1f, 0.66f, 0.7f));
        var sw = Mat("Gramo_Switch", new Color(0.6f, 0.8f, 1f));
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material m = wood;
            switch (r.name)
            {
                case "Gramo_Plinth": case "Gramo_Top": case "Gramo_Platter": m = woodDark; break;
                case "Gramo_Horn": case "Gramo_Arm": case "Gramo_Neck": case "Gramo_Soundbox": case "Gramo_Crank": case "Gramo_Spindle": m = brass; break;
                case "Gramo_Record": m = record; break;
                case "Gramo_RecordLabel": m = label; break;
                case "Gramo_Plate": case "Gramo_SwitchBase": m = plate; break;
                case "Gramo_BtnPrev": case "Gramo_BtnNext": m = button; break;
                case "Gramo_SwitchLever": m = sw; break;
            }
            r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = r.name == "Gramo_Horn" || r.name == "Gramo_Box" || r.name == "Gramo_Top" ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, 0);
        }
        var T = go.transform;
        Transform F(string n) => T.Find(n);

        // レコードの子（ラベル・軸）：FBX で X に90°回った向きで入ってきて、レコードの下の箱の中に埋まっていた。
        // 回すとレコードの周りをぐるぐる回っていた（2026-11-21 てじゃ「レコードの動きおかしい」）。頂点はもう上向きなので回転をなくす
        foreach (var n in new[] { "Gramo_RecordLabel", "Gramo_Spindle" })
        {
            var c = T.Find("Gramo_Record/" + n); if (c == null) continue;
            c.localPosition = Vector3.zero; c.localRotation = Quaternion.identity; c.localScale = Vector3.one;
        }

        // 当たり：台（ぶつかる）。前は台座ごとの大きい箱1つで、前のボタンが箱の中に埋まってレーザーが当たらなかった
        //（2026-11-21 てじゃ「ボタンがメッシュ判定が邪魔でおせない」）→ 下の台座（ひと回り大きい）と箱の2つに分けて、ボタンを箱の前の面より手前に出す
        var bcP = go.AddComponent<BoxCollider>(); bcP.center = new Vector3(0f, 0.037f, 0f); bcP.size = new Vector3(0.86f, 0.074f, 0.86f);
        var bcB = go.AddComponent<BoxCollider>(); bcB.center = new Vector3(0f, 0.27f, 0f); bcB.size = new Vector3(0.78f, 0.42f, 0.78f);

        // 本体のスクリプト
        var bgm = Object.FindObjectsOfType<FuwaBgm>(true).FirstOrDefault();
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        var clear = Object.FindObjectsOfType<FuwaAllClearReveal>(true).FirstOrDefault();
        var gm = UdonSharpUndo.AddComponent<FuwaGramophone>(go);
        gm.bgm = bgm; gm.language = lang; gm.allClear = clear;
        gm.trackIds = Tracks; gm.namesJa = Ja; gm.namesEn = En; gm.lockedIndex = LockedIndex;
        gm.record = F("Gramo_Record");

        // ボタン：テレビの「CAM」と同じ形（左に見出し・右の白い札に ◀ 今の値 ▶、つながった1本で真ん中で区切る）をコピーする
        //（2026-11-21 てじゃ「ほかの切り替えボタンみたいにボタンとタイトルがつながってて真ん中で区切ってるタイプにしよ」）
        //  上の段：◀ 曲名 ▶（右半分で次、左半分で前）／下の段：▶再生 ■停止 ＋ 蓄音機の音量バー
        // 前の3Dのボタン・レバー・札は隠す
        foreach (var n in new[] { "Gramo_BtnPrev", "Gramo_BtnNext", "Gramo_SwitchBase", "Gramo_SwitchLever", "Gramo_Plate" }) { var t = F(n); if (t != null) t.gameObject.SetActive(false); }
        UdonSharpEditorUtility.CopyProxyToUdon(gm);
        var srcCam = GameObject.Find("FuwaPlayground/Room/Monitor").GetComponentsInChildren<Transform>(true).First(t => t.name == "CamButton" && t.Find("Canvas") != null);
        var follow = Object.FindObjectsOfType<FuwaFollowCam>(true).FirstOrDefault();
        var ugm = UdonSharpEditorUtility.GetBackingUdonBehaviour(gm);
        void SetEvent(UnityEngine.UI.Button b, string evt)
        {
            var so = new SerializedObject(b);
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            calls.arraySize = 1;
            var c0 = calls.GetArrayElementAtIndex(0);
            c0.FindPropertyRelative("m_Target").objectReferenceValue = ugm;
            c0.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = "VRC.Udon.UdonBehaviour, VRC.Udon";
            c0.FindPropertyRelative("m_MethodName").stringValue = "SendCustomEvent";
            c0.FindPropertyRelative("m_Mode").intValue = 5;
            c0.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = evt;
            c0.FindPropertyRelative("m_CallState").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        // height：世界での高さ(m)。extraPill：右の白い札を伸ばす幅、extraHead：左の見出しを伸ばす幅（どちらも CAM の 1400×400 の単位。マイナスで縮める）
        // wpos/wrot：世界での位置と向き（文字が読める向き＝見る人の方を -Z が向く）
        Transform Seg(string name, Vector3 wpos, Quaternion wrot, float height, float extraPill, float extraHead, string nextEvt, string backEvt)
        {
            var g = Object.Instantiate(srcCam.gameObject, T, false); g.name = name;
            var r = g.transform;
            r.SetPositionAndRotation(wpos, wrot);
            var cv = (RectTransform)r.Find("Canvas");
            float k = height / (cv.sizeDelta.y * cv.localScale.y);   // 根元の大きさ（世界）
            r.localScale = Vector3.one * (k / T.lossyScale.x);
            float E = extraPill + extraHead;
            cv.sizeDelta += new Vector2(E, 0f);
            var box = cv.GetComponent<BoxCollider>(); box.size = new Vector3(cv.sizeDelta.x, box.size.y, box.size.z);
            void Shift(string path, float dx, float dw)
            {
                var t = (RectTransform)r.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == path); if (t == null) return;
                t.anchoredPosition += new Vector2(dx, 0f); t.sizeDelta += new Vector2(dw, 0f);
            }
            // 左の見出しは左はしに、右の札は右はしにそろえたまま、見出しを extraHead・札を extraPill だけ広げる
            Shift("Head", -E / 2 + extraHead / 2, extraHead);
            Shift("Icon", -E / 2 + extraHead / 2, 0f);
            Shift("ArrowL", -E / 2 + extraHead, 0f);
            Shift("PillA", -E / 2 + extraHead + extraPill / 2, extraPill);
            Shift("TextA", -E / 2 + extraHead + extraPill / 2, extraPill);
            Shift("ArrowR", E / 2, 0f);
            Shift("BackButton", -E / 2 + (extraHead + extraPill / 2) / 2, extraHead + extraPill / 2);   // 左半分（見出し＋札の左）
            SetEvent(r.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "Button"), nextEvt);
            SetEvent(r.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "BackButton"), backEvt);
            // 色：テレビの紫ではなく蓄音機の木の茶色（2026-11-21 てじゃ「そもそも紫がヤダな」）
            foreach (var b in r.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                var cb = b.colors; cb.normalColor = Theme; cb.highlightedColor = Color.Lerp(Theme, Color.white, 0.18f); cb.selectedColor = Theme;
                cb.pressedColor = ThemeDark; b.colors = cb; EditorUtility.SetDirty(b);
            }
            foreach (var n in new[] { "TextA", "ArrowL", "ArrowR" }) { var x = r.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).FirstOrDefault(q => q.name == n); if (x != null) x.color = Theme; }
            var fx = r.GetComponentInChildren<FuwaArrowHoverFx>(true);
            if (fx != null) { fx.normalColor = Theme; fx.hoverColor = ThemeDark; UdonSharpEditorUtility.CopyProxyToUdon(fx); EditorUtility.SetDirty(fx); }
            return r;
        }
        TMPro.TextMeshProUGUI Find(Transform r, string n) => r.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).First(x => x.name == n);
        var mplus = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/FuwaCourse/Fonts/MPLUSRounded1c SDF.asset");

        var front = T.rotation * Quaternion.Euler(0f, 180f, 0f);   // 蓄音機の前（+Z）から読める
        // 上の段：曲（2026-11-21 てじゃ「曲名の左の♫いらんかも」→ 見出しなし、白い札だけ）
        var trackRow = Seg("TrackButton", T.TransformPoint(new Vector3(0f, 0.335f, 0.398f)), front, 0.15f, 520f + TrackHeadCut, -TrackHeadCut, "Next", "Prev");
        {
            trackRow.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(x => x.name == "Icon").gameObject.SetActive(false);
            var ta = Find(trackRow, "TextA");
            // 曲名は「2–4 はぐるまのしま」など長いので、M PLUS（字が全部ある）を太字で、入りきらなければ小さく
            ta.font = mplus; ta.fontSharedMaterial = mplus.material; ta.fontStyle = TMPro.FontStyles.Bold;
            { var m = ta.margin; ta.margin = new Vector4(m.x, m.y, m.z, Mathf.Max(0f, m.w - 40f)); }
            ta.enableAutoSizing = true; ta.fontSizeMin = 50f; ta.fontSizeMax = 130f; ta.enableWordWrapping = false;
            ta.text = Ja[0];
            gm.trackText = ta;
        }
        // 下の段：▶再生 ■停止 ＋ 蓄音機専用の音量バー（ワールドのBGM音量とは別）
        var row2 = T.TransformPoint(new Vector3(0f, 0.165f, 0.398f));
        var right = front * Vector3.right;   // 見る人から見て右
        var panelSprite = srcCam.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(x => x.name == "Button").sprite;
        (UnityEngine.UI.Image back, TMPro.TextMeshProUGUI icon) Btn(string name, float x, string glyph, string evt)
        {
            var go = new GameObject(name); go.transform.SetParent(T, false);
            go.transform.SetPositionAndRotation(row2 + right * x, front);
            var cvGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            cvGo.transform.SetParent(go.transform, false);
            cvGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var cv = (RectTransform)cvGo.transform; cv.sizeDelta = new Vector2(400f * BtnW / Row2H, 400f); cv.localScale = Vector3.one * 0.001f;
            go.transform.localScale = Vector3.one * (Row2H / 0.4f / T.lossyScale.x);
            cvGo.AddComponent<VRC.SDK3.Components.VRCUiShape>();
            var bc = cvGo.AddComponent<BoxCollider>(); bc.size = new Vector3(cv.sizeDelta.x, cv.sizeDelta.y, 10f); bc.isTrigger = true;
            var bGo = new GameObject("Button", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button)); bGo.transform.SetParent(cvGo.transform, false);
            var brt = (RectTransform)bGo.transform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
            var img = bGo.GetComponent<UnityEngine.UI.Image>(); img.sprite = panelSprite; img.type = UnityEngine.UI.Image.Type.Sliced; img.pixelsPerUnitMultiplier = 0.22f; img.color = Theme;   // 角を丸く（曲名の段の帯と同じ絵）
            var b = bGo.GetComponent<UnityEngine.UI.Button>();
            var cb = b.colors; cb.normalColor = Color.white; cb.selectedColor = Color.white; cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f); cb.pressedColor = new Color(0.75f, 0.75f, 0.75f); cb.colorMultiplier = 1f; b.colors = cb;
            SetEvent(b, evt);
            var tGo = new GameObject("Icon", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)); tGo.transform.SetParent(bGo.transform, false);
            var trt = (RectTransform)tGo.transform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var t = tGo.GetComponent<TMPro.TextMeshProUGUI>(); t.font = mplus; t.fontSharedMaterial = mplus.material; t.text = glyph; t.fontSize = 200f;
            t.alignment = TMPro.TextAlignmentOptions.Center; t.color = Color.white; t.raycastTarget = false;
            return (img, t);
        }
        float x0 = -RowW / 2f;
        var play = Btn("PlayButton", x0 + BtnW / 2f, "▶", nameof(FuwaGramophone.PlayGramo));
        var stop = Btn("StopButton", x0 + BtnW * 1.5f + Gap, "■", nameof(FuwaGramophone.StopGramo));
        gm.playBack = play.back; gm.playIcon = play.icon; gm.stopBack = stop.back; gm.stopIcon = stop.icon;
        gm.onBack = Theme; gm.offBack = new Color(0.95f, 0.9f, 0.86f); gm.onIcon = Color.white; gm.offIcon = Theme;
        // 停止中の見た目（■を塗る）
        play.back.color = gm.offBack; play.icon.color = gm.offIcon; stop.back.color = gm.onBack; stop.icon.color = gm.onIcon;

        // 音量バー：あそびばの設定ボードの音量バーの写し（見た目そのまま）。動かすと蓄音機の音量だけ変わる（FuwaBgm.volumeSliders には入れない）
        var srcVol = pg.transform.Find("HowToPlay/BgmVolume");
        if (srcVol != null)
        {
            float vw = RowW - BtnW * 2f - Gap * 2f;
            var v = Object.Instantiate(srcVol.gameObject, T, false).transform; v.name = "VolumeBar";
            v.SetPositionAndRotation(row2 + right * (RowW / 2f - vw / 2f), front);
            var cv = (RectTransform)v.Find("Canvas");
            cv.sizeDelta = new Vector2(cv.sizeDelta.y * vw / Row2H, cv.sizeDelta.y);
            var box = cv.GetComponent<BoxCollider>(); if (box != null) box.size = new Vector3(cv.sizeDelta.x, cv.sizeDelta.y, box.size.z);
            float k = Row2H / (cv.sizeDelta.y * cv.localScale.y);
            v.localScale = Vector3.one * (k / T.lossyScale.x);
            // ♪ はただの絵に（元はワールドのBGMのミュートボタン）
            foreach (var bt in v.GetComponentsInChildren<UnityEngine.UI.Button>(true)) Object.DestroyImmediate(bt);
            // ワールドの音量バーとまぎらわしいので、絵はスピーカー・色は蓄音機の茶色に（2026-11-21 てじゃ「ボリュームっぽいアイコンにかえて」「色もかえよ」）
            foreach (var im in v.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (im.name == "MusicIcon") { im.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TejaVideoPlayer/Icon_Speaker.png"); im.preserveAspect = true; im.color = Theme; }
                else if (im.name == "Fill") im.color = Theme;
                else if (im.name == "Back") im.color = new Color(0.98f, 0.94f, 0.9f, 0.95f);
                else if (im.name == "Background") im.color = new Color(0.66f, 0.45f, 0.32f, 0.3f);
                else if (im.name == "Handle") im.color = Color.white;
            }
            var sl = v.GetComponentInChildren<UnityEngine.UI.Slider>(true);
            sl.wholeNumbers = false; sl.minValue = 0f; sl.maxValue = 1f; sl.SetValueWithoutNotify(gm.defaultVolume);   // 元は 0〜100 の整数
            {
                var so = new SerializedObject(sl);
                var calls = so.FindProperty("m_OnValueChanged.m_PersistentCalls.m_Calls");
                calls.arraySize = 1;
                var c0 = calls.GetArrayElementAtIndex(0);
                c0.FindPropertyRelative("m_Target").objectReferenceValue = ugm;
                c0.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = "VRC.Udon.UdonBehaviour, VRC.Udon";
                c0.FindPropertyRelative("m_MethodName").stringValue = "SendCustomEvent";
                c0.FindPropertyRelative("m_Mode").intValue = 5;
                c0.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = nameof(FuwaGramophone.OnVolume);
                c0.FindPropertyRelative("m_CallState").intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            gm.volumeSlider = sl;
        }
        // ワールドのBGMの音量バーの一覧から、前の版の蓄音機のバー（消えて null）を外す
        if (bgm != null && bgm.volumeSliders != null) bgm.volumeSliders = bgm.volumeSliders.Where(x => x != null).ToArray();

        gm.activeTextColor = Theme; gm.idleTextColor = ThemeIdle;
        gm.trackText.color = gm.activeTextColor;
        UdonSharpEditorUtility.CopyProxyToUdon(gm);
        var gub = UdonSharpEditorUtility.GetBackingUdonBehaviour(gm); if (gub != null) { gub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(gub); }

        // BGM：蓄音機の曲が効く場所＝あそびばの BallPark
        if (bgm != null)
        {
            bgm.overrideArea = pg.transform.Find("BallPark");
            UdonSharpEditorUtility.CopyProxyToUdon(bgm); EditorUtility.SetDirty(bgm);
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pg.scene);
        return $"蓄音機を置いた pos={pos} bgm={(bgm != null)} lang={(lang != null)} allClear={(clear != null)} area={(bgm != null ? bgm.overrideArea : null)}";
    }
}
