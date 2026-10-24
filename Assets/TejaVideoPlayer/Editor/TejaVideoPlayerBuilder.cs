using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components.AVPro;

// Teja Player：シンプルな同期動画プレイヤー（てじゃ用の使い回し。2026-10-15 FLUFF のあそびばのテレビ用に作ったものを汎用にした）。
// ・メニュー「Tools/Teja Player/プレイヤーを置く」で一式を置ける
// ・ほかの物に組み込む時は、ここの public な部品（EnsureCore・MakeScreen・MakeStandby・MakeSpeakers・BuildControls など）を使う
//   （FLUFF のテレビへの組み込みは Assets/FuwaCourse/Editor/FuwaTvVideoBuilder.cs）
// ・このフォルダ（Assets/TejaVideoPlayer）だけで完結する（フォントも同梱：M PLUS Rounded 1c、OFL）
public static class TejaVideoPlayerBuilder
{
    public const string Dir = "Assets/TejaVideoPlayer/";
    const string ScriptPath = Dir + "Scripts/TejaVideoPlayer.cs";
    const string ProgramPath = Dir + "Scripts/TejaVideoPlayer.asset";
    const string MatPath = Dir + "TejaVideoScreen.mat";
    const string FontPath = Dir + "Fonts/MPLUSRounded1c-Medium.ttf";

    public const float ScreenW = 1.92f, ScreenH = 1.08f;   // 16:9
    public const float ScreenCenterY = 1.55f;              // 床からの画面の真ん中の高さ
    public const float TabH = 72f;   // パネルの下のたたむタブの段の高さ（Canvas の単位）
    public const float TitleH = 64f;  // パネルの一番上の、動画のタイトルの段（2026-10-24 てじゃ「タイトルは操作パネルの一番上」）
    public const float PanelH = 212f + TitleH;   // パネルの高さ（下から ボタン・シークバー＋時間・タイトル）
    public static int Style = 2;   // 操作パネルの見た目（TejaVideoStyle：0=A シンプル、1=B FLUFF風、2=C ぽってり）

    // ===== 汎用：シーンビューの真ん中あたりに、プレイヤー一式（画面・スピーカー・操作パネル・待機画面）を置く =====
    // 置いたあとは TejaPlayer ごと自由に動かしてOK。何個置いても別々のプレイヤーになる
    [MenuItem("Tools/Teja Player/プレイヤーを置く")]
    public static void PlaceNew()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var root = new GameObject("TejaPlayer").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Teja Player");
        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            Vector3 fwd = sv.camera.transform.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward; fwd.Normalize();
            Vector3 pos = sv.pivot; RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out hit, 50f)) pos.y = hit.point.y;
            root.position = pos;
            root.rotation = Quaternion.LookRotation(fwd, Vector3.up);   // 見ている人の方を向く（Quad は -Z 側が表）
        }
        var font = DefaultFont();
        var core = EnsureCore(root, "Player");
        var av = core.Item1; var vp = core.Item2;
        var ts = MakeScreen(root, "Screen", new Vector3(0f, ScreenCenterY, 0f), Quaternion.identity, new Vector3(ScreenW, ScreenH, 1f), av, true);
        vp.standbyScreens = new[] { MakeStandby(ts.transform) };
        vp.loadingSpinners = new[] { vp.standbyScreens[0].transform.Find("Spinner") };
        vp.speakers = MakeSpeakers(root, new[] { new Vector3(-ScreenW * 0.5f, ScreenCenterY, 0f), new Vector3(ScreenW * 0.5f, ScreenCenterY, 0f) }, av, 5f, 30f);
        // 操作パネルは画面のすぐ下、右上の角（時間の右）に ▼▲ の隠すボタン（FLUFF のテレビと同じ並び）
        const float cs = 0.001f, gap = 0.018f;   // FLUFF のテレビ（画面2.79m・すき間2.5cm）と同じ比率
        float screenBottom = ScreenCenterY - ScreenH * 0.5f;
        float panelCenter = screenBottom - gap - PanelH * 0.5f * cs;
        var controls = BuildControls(root, new Vector3(0f, panelCenter, -0.01f), cs, false, vp, font);
        var hide = MakeHideButton(root, new Vector3(0f, panelCenter, -0.012f) + HideButtonOffset(cs), cs, font);
        var hub = UdonSharpEditorUtility.GetBackingUdonBehaviour(vp);
        UnityEventTools.AddStringPersistentListener(hide.GetComponentInChildren<UnityEngine.UI.Button>().onClick, hub.SendCustomEvent, nameof(TejaVideoPlayer.TogglePanel));
        vp.panelTabLabel = hide.GetComponentInChildren<Text>(); vp.panelTab = null;
        vp.panelHideTarget = controls.gameObject;
        Finish(vp, scene);
        Selection.activeGameObject = root.gameObject;
    }

    // 隠すボタンの位置：操作パネル（1920×212、真ん中基準）の右上の角＝時間の右（パネルの真ん中からのずれ、scale は Canvas 1単位の大きさ）
    public static Vector3 HideButtonOffset(float scale)
    {
        return new Vector3((1820f - 960f) * scale, (PanelH - 38f - PanelH * 0.5f) * scale, 0f);
    }

    // 操作パネルを隠す／出すボタン（▼▲。小さい丸っこいラベンダーのボタン）。localPos は Canvas の真ん中。押した時の配線は呼ぶ側で。
    // パネルとは別の Canvas（パネルを隠しても、これだけ残る）
    public static GameObject MakeHideButton(Transform parent, Vector3 localPos, float scale, Font font)
    {
        var hdCv = new GameObject("PanelHide", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        hdCv.transform.SetParent(parent, false);
        hdCv.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        hdCv.GetComponent<Canvas>().sortingOrder = 1;   // 操作パネル（半透明の板、Order 0）より必ず手前に描く。重なる所にあるので、距離順だと見る角度でパネルの下に沈んだ
        var hrt = (RectTransform)hdCv.transform; hrt.sizeDelta = new Vector2(140f, 60f); hrt.localScale = Vector3.one * scale;
        hrt.localPosition = localPos; hrt.localRotation = Quaternion.identity;
        hdCv.AddComponent<VRC.SDK3.Components.VRCUiShape>();
        var hbc = hdCv.AddComponent<BoxCollider>(); hbc.size = new Vector3(140f, 60f, 10f); hbc.isTrigger = true;
        var hGo = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button)); hGo.transform.SetParent(hdCv.transform, false);
        var hbrt = (RectTransform)hGo.transform; hbrt.anchorMin = Vector2.zero; hbrt.anchorMax = Vector2.one; hbrt.offsetMin = Vector2.zero; hbrt.offsetMax = Vector2.zero;
        var hImg = hGo.GetComponent<Image>(); hImg.sprite = IconSprite9(Dir + "UI_Round.png"); hImg.type = Image.Type.Sliced; hImg.color = Color.white;
        var hSel = hGo.GetComponent<UnityEngine.UI.Button>();
        var hcb = hSel.colors; hcb.normalColor = new Color(0.62f, 0.55f, 0.86f); hcb.highlightedColor = new Color(0.72f, 0.66f, 0.93f); hcb.pressedColor = new Color(0.52f, 0.45f, 0.76f); hcb.selectedColor = hcb.normalColor; hSel.colors = hcb;
        var hNav = hSel.navigation; hNav.mode = Navigation.Mode.None; hSel.navigation = hNav;   // キー操作で選ばれないように
        var hLab = Label(hGo.transform, "Label", "▲", font, 36, Color.white, TextAnchor.MiddleCenter); Fill(hLab.gameObject, 2f);
        return hdCv;
    }

    // 本体（AVPro の再生部品＋TejaVideoPlayer）を parent の下に用意する（あれば使い回す。消して作り直すとネットワークIDがずれるので）。
    // 同期するので、エリアの表示切り替えなどで隠れない所に置くこと（隠れている間の同期を取りこぼす。スピーカーも隠れると音が戻らなくなる）
    public static System.Tuple<VRCAVProVideoPlayer, TejaVideoPlayer> EnsureCore(Transform parent, string name = "VideoPlayer")
    {
        EnsureProgram();
        var coreT = parent != null ? parent.Find(name) : null;
        var core = coreT != null ? coreT.gameObject : new GameObject(name);
        if (parent != null) core.transform.SetParent(parent, false);
        var av = core.GetComponent<VRCAVProVideoPlayer>(); if (av == null) av = core.AddComponent<VRCAVProVideoPlayer>();
        var avSo = new SerializedObject(av);
        avSo.FindProperty("autoPlay").boolValue = false;
        avSo.FindProperty("loop").boolValue = false;
        avSo.FindProperty("maximumResolution").intValue = 1080;
        avSo.ApplyModifiedPropertiesWithoutUndo();
        var vp = core.GetComponent<TejaVideoPlayer>(); if (vp == null) vp = UdonSharpUndo.AddComponent<TejaVideoPlayer>(core);
        vp.player = av;
        return System.Tuple.Create(av, vp);
    }

    public static GameObject MakeScreen(Transform parent, string name, Vector3 lp, Quaternion lr, Vector3 ls, VRCAVProVideoPlayer av, bool withBack)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(Shader.Find("TejaVideo/Screen")); AssetDatabase.CreateAsset(mat, MatPath); }
        mat.SetFloat("_ScreenAspect", ls.x / ls.y);
        var screen = GameObject.CreatePrimitive(PrimitiveType.Quad); screen.name = name;
        Object.DestroyImmediate(screen.GetComponent<Collider>());
        screen.transform.SetParent(parent, false);
        screen.transform.localPosition = lp; screen.transform.localRotation = lr; screen.transform.localScale = ls;
        var mr = screen.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        var vs = screen.AddComponent<VRCAVProVideoScreen>();
        var vsSo = new SerializedObject(vs);
        vsSo.FindProperty("videoPlayer").objectReferenceValue = av;
        vsSo.FindProperty("materialIndex").intValue = 0;
        vsSo.FindProperty("textureProperty").stringValue = "_MainTex";
        vsSo.FindProperty("useSharedMaterial").boolValue = false;
        vsSo.ApplyModifiedPropertiesWithoutUndo();
        if (withBack)
        {
            // 裏面（黒い板。裏から見て透けないように）
            var back = GameObject.CreatePrimitive(PrimitiveType.Quad); back.name = "Back";
            Object.DestroyImmediate(back.GetComponent<Collider>());
            back.transform.SetParent(screen.transform, false);
            back.transform.localPosition = new Vector3(0f, 0f, 0.005f);
            back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var blackMat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "TejaVideoBack.mat");
            if (blackMat == null) { blackMat = new Material(Shader.Find("Unlit/Color")); blackMat.color = new Color(0.05f, 0.05f, 0.07f); AssetDatabase.CreateAsset(blackMat, Dir + "TejaVideoBack.mat"); }
            back.GetComponent<MeshRenderer>().sharedMaterial = blackMat;
        }
        return screen;
    }

    // 待機画面：画面のすぐ手前に黒い板＋まん中にうすいロゴ（▶テレビのマーク＋「Teja Player」）（画面の子。画面と一緒に出る・隠れる）
    //（2026-10-15 てじゃ「うすいのいいね」「ここ以外でも使うし私の名前に、Teja Player」）
    public static GameObject MakeStandby(Transform screen)
    {
        var sb = GameObject.CreatePrimitive(PrimitiveType.Quad); sb.name = "Standby";
        Object.DestroyImmediate(sb.GetComponent<Collider>());
        sb.transform.SetParent(screen, false);
        sb.transform.localPosition = new Vector3(0f, 0f, -0.002f / Mathf.Max(screen.lossyScale.z, 0.0001f)); sb.transform.localRotation = Quaternion.identity; sb.transform.localScale = Vector3.one;
        var mr = sb.GetComponent<MeshRenderer>();
        mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Dir + "TejaVideoBack.mat");
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        var icon = new GameObject("Icon"); icon.transform.SetParent(sb.transform, false);
        icon.transform.localPosition = new Vector3(0f, 0f, -0.001f);
        float k = 0.4f;   // 画面の高さに対するロゴの大きさ
        var sr = icon.AddComponent<SpriteRenderer>(); sr.sprite = IconSprite(Dir + "Standby_Logo.png"); sr.color = new Color(1f, 1f, 1f, 0.25f);
        float ppu = sr.sprite.pixelsPerUnit, sz = sr.sprite.rect.height / ppu;   // スプライト1枚の大きさ（ローカル単位）
        Vector3 ps = screen.lossyScale;
        icon.transform.localScale = new Vector3(k / sz * ps.y / ps.x, k / sz, 1f);
        // 読み込み中のくるくる（ロゴの下。ふだんは隠す。TejaVideoPlayer が出して回す）
        var sp = new GameObject("Spinner"); sp.transform.SetParent(sb.transform, false);
        sp.transform.localPosition = new Vector3(0f, -0.25f, -0.001f);
        // 外側（Spinner）で画面の縦横比を打ち消し、中の輪（Ring）は正方形のまま回す（つぶれた楕円にならないように）
        sp.transform.localScale = new Vector3(ps.y / ps.x, 1f, 1f);
        var ring = new GameObject("Ring"); ring.transform.SetParent(sp.transform, false);
        var spr = ring.AddComponent<SpriteRenderer>(); spr.sprite = IconSprite(Dir + "Standby_Spinner.png"); spr.color = new Color(1f, 1f, 1f, 0.45f);
        float ks = 0.09f, ssz = spr.sprite.rect.height / spr.sprite.pixelsPerUnit;
        ring.transform.localScale = Vector3.one * (ks / ssz);
        sp.SetActive(false);
        sb.SetActive(true);
        return sb;
    }

    // 音（左右2つ）
    public static AudioSource[] MakeSpeakers(Transform parent, Vector3[] localPos, VRCAVProVideoPlayer av, float soundNear, float soundFar)
    {
        var speakers = new AudioSource[localPos.Length];
        for (int i = 0; i < localPos.Length; i++)
        {
            var s = new GameObject(i == 0 ? "SpeakerL" : "SpeakerR"); s.transform.SetParent(parent, false);
            s.transform.localPosition = localPos[i];
            var a = s.AddComponent<AudioSource>();
            a.playOnAwake = false; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = soundNear; a.maxDistance = soundFar; a.dopplerLevel = 0f;
            var sp = s.AddComponent<VRCAVProVideoSpeaker>();
            var spSo = new SerializedObject(sp);
            spSo.FindProperty("videoPlayer").objectReferenceValue = av;
            spSo.FindProperty("mode").intValue = i == 0 ? 1 : 2;   // MonoLeft / MonoRight
            spSo.ApplyModifiedPropertiesWithoutUndo();
            var spatial = s.AddComponent<VRC.SDK3.Components.VRCSpatialAudioSource>();
            var saSo = new SerializedObject(spatial);
            var near = saSo.FindProperty("Near"); if (near != null) near.floatValue = soundNear;
            var far = saSo.FindProperty("Far"); if (far != null) far.floatValue = soundFar;
            saSo.ApplyModifiedPropertiesWithoutUndo();
            speakers[i] = a;
        }
        return speakers;
    }

    public static void Finish(TejaVideoPlayer vp, UnityEngine.SceneManagement.Scene scene)
    {
        UdonSharpEditorUtility.CopyProxyToUdon(vp);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(vp);
        // 同期は Manual（AddComponent 直後は Continuous のことがある）
        if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; EditorUtility.SetDirty(ub); }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    // 操作パネル（2段：上＝シークバー・時間、下＝ボタン・音量）。localPos は Canvas の真ん中、scale は Canvas 1単位の大きさ(m)
    public static Transform BuildControls(Transform parent, Vector3 localPos, float scale, bool withTab, TejaVideoPlayer vp, Font font)
    {
        float tabH = withTab ? TabH : 0f;
        // ----- 操作パネル（画面のすぐ下。ワールドの Canvas、レーザーで押す） -----
        var canvasGo = new GameObject("Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(parent, false);
        var cv = canvasGo.GetComponent<Canvas>(); cv.renderMode = RenderMode.WorldSpace;
        var crt = (RectTransform)canvasGo.transform;
        crt.sizeDelta = new Vector2(1920f, PanelH + tabH);   // 2段：上＝シークバー・時間（読み込み前は状態の文字）、下＝ボタン・音量（2026-10-15 てじゃ「ボリュームを上に、スリムに」）
        crt.localScale = Vector3.one * scale;
        crt.localPosition = localPos;
        crt.localRotation = Quaternion.identity;   // Canvas も Quad と同じく -Z 側（見る人の側）が表
        canvasGo.AddComponent<VRC.SDK3.Components.VRCUiShape>();
        var cbox = canvasGo.AddComponent<BoxCollider>(); cbox.size = new Vector3(1920f, PanelH + tabH, 10f); cbox.isTrigger = true;   // すり抜ける（ほかのレーザーUIと同じ。2026-10-15 てじゃ「コライダーがある」）   // レーザーが当たる板（左下基準ではなく真ん中基準）
        // パネル本体（たたむと消える）と、その下のたたむタブ（2026-10-15 てじゃ「下のUIを隠す・戻す」→ B：パネルの端の小さいタブ。画面クリックだとVRで誤操作するので）
        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image)); panelGo.transform.SetParent(canvasGo.transform, false);
        Place(panelGo, 0f, tabH, 1920f, PanelH);
        panelGo.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.2f, 0.85f);
        var pt = panelGo.transform;
        vp.panelRoot = panelGo;

        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(vp);

        // URL ボタン＝URL入力欄そのもの（オレンジのボタンの見た目）。押すとすぐ VRChat の入力が出る
        //（2026-10-15 てじゃ：ふだん広い入力欄が出ていると入力状態になりっぱなし → ボタンで出す形にしたら「URLボタン→白い枠→押す」の2手になったので、ボタン自体を入力欄に）
        var inGo = new GameObject("UrlInput", typeof(RectTransform), typeof(Image));
        inGo.transform.SetParent(pt, false);
        Place(inGo, 20f, 12f, 220f, 116f);
        inGo.GetComponent<Image>().color = new Color(0.95f, 0.55f, 0.45f);
        var field = inGo.AddComponent<VRCUrlInputField>();
        var txt = Label(inGo.transform, "Text", "", font, 28, new Color(1f, 1f, 1f, 0f), TextAnchor.MiddleCenter); Fill(txt.gameObject, 8f);   // 入れた文字はボタンの上に見せない（くさりの絵に重なるので。2026-10-15 てじゃ「ボタンにURLみえちゃう」）
        txt.supportRichText = false; txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        // 入力欄の「うすい案内の文字」は VRChat の入力フォームの中にそのまま出る（タイトルの「URL を入力」は VRChat 側で変えられない）。
        // ボタンの上には見せず（透明）、表のボタンの文字「URL」は別のラベルで出す
        var ph = Label(inGo.transform, "Placeholder", "Paste a video URL here", font, 30, new Color(1f, 1f, 1f, 0f), TextAnchor.MiddleCenter); Fill(ph.gameObject, 6f);
        IconOn(inGo.transform, "Icon_Link.png", 140f);   // 表はリンク（くさり）のマーク（2026-10-15 てじゃ「URLもなんかいいマーク」）
        var fSo = new SerializedObject(field);
        fSo.FindProperty("m_TextComponent").objectReferenceValue = txt;
        fSo.FindProperty("m_Placeholder").objectReferenceValue = ph;
        var aos = fSo.FindProperty("m_ShouldActivateOnSelect"); if (aos != null) aos.boolValue = true;   // false だと1回目の押しで選ばれるだけで、2回押さないと入力が出なかった（2026-10-15 てじゃ「URLボタンが2回おさないと反応しない」）。押す前のキーの入りこみは TejaVideoPlayer.CleanFields で消す
        fSo.ApplyModifiedPropertiesWithoutUndo();
        // 押した時の色の変化はボタンと同じくらいに
        field.targetGraphic = inGo.GetComponent<Image>();
        var cb = field.colors; cb.highlightedColor = new Color(1f, 0.9f, 0.85f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f); field.colors = cb;
        UnityEventTools.AddStringPersistentListener(field.onEndEdit, ub.SendCustomEvent, nameof(TejaVideoPlayer.OnUrlEntered));
        vp.urlField = field;

        // コピーのボタン：ふつうの文字の入力欄（URL用の欄だと VRChat のフォームの見出しが「URL を入力」になるので）。
        // 中身（今の動画のURL）はボタンの上には見せず、押すと VRChat のフォームに入った状態で出る → そこでコピー（2026-10-15 てじゃ）
        var cGo = new GameObject("CopyUrl", typeof(RectTransform), typeof(Image));
        cGo.transform.SetParent(pt, false);
        Place(cGo, 260f, 12f, 110f, 116f);   // 半分の幅（2026-10-15 てじゃ「コピーと連続再生は半分程度」）
        cGo.GetComponent<Image>().color = new Color(0.95f, 0.55f, 0.45f);
        var copy = cGo.AddComponent<InputField>();
        var ct = Label(cGo.transform, "Text", "", font, 20, new Color(1f, 1f, 1f, 0f), TextAnchor.MiddleCenter); Fill(ct.gameObject, 6f);
        ct.supportRichText = false;
        copy.textComponent = ct;
        // 案内の文字（ボタンの上には見せない＝透明）。VRChat のフォームで中身を消した時に、ここが何の欄かわかるように出る（2026-10-15 てじゃ「URL消した時、URLコピペ用っすよって英語で」）
        var cph = Label(cGo.transform, "Placeholder", "For copying the video URL (editing here does nothing)", font, 20, new Color(1f, 1f, 1f, 0f), TextAnchor.MiddleCenter); Fill(cph.gameObject, 6f);
        copy.placeholder = cph;
        copy.shouldActivateOnSelect = true;   // URL と同じく、1回で入力が出るように
        copy.targetGraphic = cGo.GetComponent<Image>();
        var ccb = copy.colors; ccb.highlightedColor = new Color(1f, 0.9f, 0.85f); ccb.pressedColor = new Color(0.8f, 0.8f, 0.8f); ccb.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f); copy.colors = ccb;
        IconOn(cGo.transform, "Icon_Copy.png", 84f);
        UnityEventTools.AddStringPersistentListener(copy.onEndEdit, ub.SendCustomEvent, nameof(TejaVideoPlayer.OnCopyEdited));
        vp.copyField = copy;

        // ボタン：再生/一時停止・再同期
        var pp = Button(pt, "PlayPause", "▶", font, 560f, 12f, 220f, 116f, ub, nameof(TejaVideoPlayer.PlayPause));
        vp.playPauseLabel = pp; pp.fontSize = 64;   // アイコンと同じくらいの大きさに（2026-10-15 てじゃ「各アイコンの大きさ自体おおきく」）
        // くり返し（再生の左、半分の幅。みんな共通、最初はオフ。OFFの時はグレーアウト）（2026-10-15 てじゃ「連続再生の切り替えスイッチ」「場所は再生の横」→「再生ボタンの左側」「オフがグレーアウト」）
        var lp = Button(pt, "Loop", "", font, 430f, 12f, 110f, 116f, ub, nameof(TejaVideoPlayer.ToggleLoop));
        var lpBtn = lp.transform.parent;
        IconOn(lpBtn, "Icon_Loop.png", 84f);
        vp.loopButtonImage = lpBtn.GetComponent<Image>();
        vp.loopIcon = lpBtn.Find("Icon").GetComponent<Image>();
        // 停止（最初に戻して止める）。ボタンは左にまとめて、くさり・コピー｜すき間｜くり返し・再生・停止・再同期（2026-10-15 てじゃ）
        var stopL = Button(pt, "Stop", "■", font, 800f, 12f, 220f, 116f, ub, nameof(TejaVideoPlayer.StopVideo));
        stopL.fontSize = 60;
        // 再同期は記号だけ（ぐるっと矢印。↻ はフォントに無くてVRChatで豆腐になるので絵で。2026-10-15 てじゃ「記号だけでいいかも」）
        var rs = Button(pt, "Resync", "", font, 1040f, 12f, 220f, 116f, ub, nameof(TejaVideoPlayer.Resync));
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)); icon.transform.SetParent(rs.transform.parent, false);
        var irt = (RectTransform)icon.transform; irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.sizeDelta = new Vector2(104f, 104f);
        var ii = icon.GetComponent<Image>(); ii.sprite = IconSprite(Dir + "Icon_Resync.png"); ii.raycastTarget = false; ii.preserveAspect = true;

        // 音量
        var slGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
        slGo.name = "Volume"; slGo.transform.SetParent(pt, false);
        Place(slGo, 1440f, 22f, 460f, 96f);
        slGo.transform.Find("Handle Slide Area/Handle").GetComponent<RectTransform>().sizeDelta = new Vector2(36f, 0f);
        var sl = slGo.GetComponent<Slider>(); sl.minValue = 0f; sl.maxValue = 1f; sl.value = vp.defaultVolume;
        UnityEventTools.AddStringPersistentListener(sl.onValueChanged, ub.SendCustomEvent, nameof(TejaVideoPlayer.OnVolumeChanged));
        vp.volumeSlider = sl;
        // スピーカーの絵＝ミュートボタン（2026-10-15 てじゃ「ワールドのボリュームみたいに、音符をミュートボタンに」「サイズもおおきく」「音符よりスピーカーマーク」）
        var muteGo = new GameObject("MuteButton", typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button)); muteGo.transform.SetParent(pt, false);
        Place(muteGo, 1300f, 8f, 124f, 124f);
        var mi = muteGo.GetComponent<Image>(); mi.preserveAspect = true; mi.color = Color.white;
        vp.iconOn = IconSprite(Dir + "Icon_Speaker.png"); vp.iconMuted = IconSprite(Dir + "Icon_SpeakerMute.png"); mi.sprite = vp.iconOn;   // 音符よりスピーカーの絵（てじゃ）
        UnityEventTools.AddStringPersistentListener(muteGo.GetComponent<UnityEngine.UI.Button>().onClick, ub.SendCustomEvent, nameof(TejaVideoPlayer.ToggleMute));
        vp.muteIcon = mi;

        // シークバー（いちばん上の段。つまんで動かすとその位置へ。ライブの時は隠す）
        var skGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
        skGo.name = "Seek"; skGo.transform.SetParent(pt, false);
        // 右上の角は隠すボタンの場所（別の Canvas で重ねる）。シークバーは短めに、時間はその左（2026-10-15 てじゃ「シークバー左に縮めて、時間の右に」）
        float topW = 1400f;
        Place(skGo, 30f, 146f, topW, 56f);
        var sk = skGo.GetComponent<Slider>(); sk.minValue = 0f; sk.maxValue = 1f; sk.value = 0f;
        var fill = skGo.transform.Find("Fill Area/Fill"); if (fill != null) fill.GetComponent<Image>().color = new Color(0.95f, 0.55f, 0.45f);
        UnityEventTools.AddStringPersistentListener(sk.onValueChanged, ub.SendCustomEvent, nameof(TejaVideoPlayer.OnSeek));
        vp.seekSlider = sk;

        // 状態・時間
        var st = Label(pt, "Status", "", font, 34, new Color(1f, 0.85f, 0.6f), TextAnchor.MiddleLeft); Place(st.gameObject, 30f, 140f, topW, 68f);
        vp.statusText = st;
        var tm = Label(pt, "Time", "", font, 36, Color.white, TextAnchor.MiddleRight); Place(tm.gameObject, 1440f, 140f, 280f, 68f);
        vp.timeText = tm;
        // 動画のタイトル（一番上の段。右はしは隠すボタン）
        var tt = Label(pt, "Title", "", font, 36, Color.white, TextAnchor.MiddleLeft); Place(tt.gameObject, 30f, 212f, 1720f, TitleH);
        tt.horizontalOverflow = HorizontalWrapMode.Wrap; tt.verticalOverflow = VerticalWrapMode.Truncate;
        vp.titleText = tt;

        // たたむタブ（パネルの右下にぶら下がる。▼でたたむ、▲で出す）。テレビに組み込む時はなし（カメラと切りかえるので）
        vp.panelTab = null; vp.panelTabLabel = null;
        if (withTab)
        {
        var tabGo = new GameObject("PanelTab", typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button)); tabGo.transform.SetParent(canvasGo.transform, false);
        Place(tabGo, 1700f, 0f, 200f, TabH - 4f);
        tabGo.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.2f, 0.85f);
        UnityEventTools.AddStringPersistentListener(tabGo.GetComponent<UnityEngine.UI.Button>().onClick, ub.SendCustomEvent, nameof(TejaVideoPlayer.TogglePanel));
        var tabL = Label(tabGo.transform, "Label", "▼", font, 44, Color.white, TextAnchor.MiddleCenter); Fill(tabL.gameObject, 4f);
        vp.panelTabLabel = tabL;
        vp.panelTab = (RectTransform)tabGo.transform; vp.panelHeight = PanelH;
        }

        TejaVideoStyle.Apply(canvasGo.transform, Style);   // 見た目（色・角の丸み）
        // キー操作で選ばれないように（デスクトップの移動キーでスライダーが動いた。2026-10-24 てじゃ「キー操作にはんのう」）
        foreach (var sel in canvasGo.GetComponentsInChildren<Selectable>(true)) { var nav = sel.navigation; nav.mode = Navigation.Mode.None; sel.navigation = nav; }
        return canvasGo.transform;
    }

    // 左下基準（Canvas の左下が (0,0)）で置く
    public static void Place(GameObject g, float x, float y, float w, float h)
    {
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
    }

    public static void Fill(GameObject g, float pad)
    {
        var rt = (RectTransform)g.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(pad, 0f); rt.offsetMax = new Vector2(-pad, 0f);
    }

    public static Text Label(Transform parent, string name, string text, Font font, int size, Color col, TextAnchor anchor)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Text)); g.transform.SetParent(parent, false);
        var t = g.GetComponent<Text>(); t.text = text; t.font = font; t.fontSize = size; t.color = col; t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; t.raycastTarget = false;
        return t;
    }

    public static Text Button(Transform parent, string name, string text, Font font, float x, float y, float w, float h, VRC.Udon.UdonBehaviour ub, string evt)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button)); g.transform.SetParent(parent, false);
        Place(g, x, y, w, h);
        g.GetComponent<Image>().color = new Color(0.95f, 0.55f, 0.45f);
        var b = g.GetComponent<UnityEngine.UI.Button>();
        UnityEventTools.AddStringPersistentListener(b.onClick, ub.SendCustomEvent, evt);
        var t = Label(g.transform, "Label", text, font, 40, Color.white, TextAnchor.MiddleCenter); Fill(t.gameObject, 6f);
        return t;
    }

    public static void IconOn(Transform parent, string file, float size)
    {
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)); icon.transform.SetParent(parent, false);
        var irt = (RectTransform)icon.transform; irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.sizeDelta = new Vector2(size, size);
        var ii = icon.GetComponent<Image>(); ii.sprite = IconSprite(Dir + file); ii.raycastTarget = false; ii.preserveAspect = true;
    }

    public static Sprite IconSprite(string path)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp != null && (imp.textureType != TextureImporterType.Sprite || !imp.alphaIsTransparency))
        {
            imp.textureType = TextureImporterType.Sprite; imp.alphaIsTransparency = true; imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static Sprite IconSprite9(string path)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp != null && (imp.textureType != TextureImporterType.Sprite || !imp.alphaIsTransparency)) { imp.textureType = TextureImporterType.Sprite; imp.alphaIsTransparency = true; imp.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    public static Font DefaultFont() { return AssetDatabase.LoadAssetAtPath<Font>(FontPath); }

    public static void EnsureProgram()
    {
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(ProgramPath) != null) return;
        var prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(ScriptPath);
        AssetDatabase.CreateAsset(prog, ProgramPath);
        AssetDatabase.SaveAssets();
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
    }
}
