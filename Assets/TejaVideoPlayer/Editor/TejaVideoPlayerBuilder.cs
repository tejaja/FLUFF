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

// シンプルな同期動画プレイヤーを置く（何度実行しても同じ結果＝前のを消して作り直し）。
// ・本体（AVPro の再生部品＋TejaVideoPlayer）は FuwaSystem の下（エリア切り替えで隠れない所。隠れると同期を取りこぼす）
// ・画面・音・操作パネルは、あそびばの観戦リビングのテレビの横（テスト。2026-10-15 てじゃ「とりあえずテストとして横あたりに」「画面のみで」）
public static class TejaVideoPlayerBuilder
{
    const string Dir = "Assets/TejaVideoPlayer/";
    const string ScriptPath = Dir + "Scripts/TejaVideoPlayer.cs";
    const string ProgramPath = Dir + "Scripts/TejaVideoPlayer.asset";
    const string MatPath = Dir + "TejaVideoScreen.mat";
    const string FontPath = "Assets/FuwaCourse/Fonts/MPLUSRounded1c-Medium.ttf";

    const float ScreenW = 1.92f, ScreenH = 1.08f;   // 16:9
    const float ScreenCenterY = 1.55f;              // 床からの画面の真ん中の高さ
    const float SideOffset = 3.1f;                  // テレビの真ん中から横へ
    // 音の届き方：島の端から端（約28m）でも聞こえるように。15mまでそのまま、45mで0（島の外は audioZone で消す）
    //（2026-10-15 てじゃ「音量減衰きつすぎ、あそびば全体聞こえるくらい」。前は3〜20m）
    const float SoundNear = 15f, SoundFar = 45f;

    [MenuItem("FuwaCourse/あそびば/動画プレイヤー（テスト）を置く")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var sys = roots.First(g => g.name == "FuwaSystem").transform;
        var room = roots.First(g => g.name == "FuwaPlayground").transform.Find("Room");
        var tv = room.Find("Monitor");

        // 前の画面は片付け。本体（同期する物）は作り直さず使い回す
        //（消して作り直すと、シーンのネットワークIDの一覧に古い物が残ってビルドの AssignSceneNetworkIDs で止まることがある）
        var oldScreen = room.Find("VideoScreenTest"); if (oldScreen != null) Object.DestroyImmediate(oldScreen.gameObject);

        EnsureProgram();
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

        // ----- 本体 -----
        var coreT = sys.Find("VideoPlayer");
        var core = coreT != null ? coreT.gameObject : new GameObject("VideoPlayer");
        core.transform.SetParent(sys, false);
        var av = core.GetComponent<VRCAVProVideoPlayer>(); if (av == null) av = core.AddComponent<VRCAVProVideoPlayer>();
        var avSo = new SerializedObject(av);
        avSo.FindProperty("autoPlay").boolValue = false;
        avSo.FindProperty("loop").boolValue = false;
        avSo.FindProperty("maximumResolution").intValue = 1080;
        avSo.ApplyModifiedPropertiesWithoutUndo();
        var vp = core.GetComponent<TejaVideoPlayer>(); if (vp == null) vp = UdonSharpUndo.AddComponent<TejaVideoPlayer>(core);
        vp.player = av;

        // ----- 置き場所：テレビの横。画面はテレビと同じ向き（島の真ん中から見える向き） -----
        var root = new GameObject("VideoScreenTest").transform; root.SetParent(room, false);
        var floor = room.Find("Floor").GetComponent<Renderer>().bounds.max.y;
        // テレビの「見る人の方」：島の真ん中側
        Vector3 center = room.position; center.y = tv.position.y;
        Vector3 toViewer = (center - tv.position); toViewer.y = 0f; toViewer.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, toViewer);   // 見る人から見て左＝この向き。テレビの左に置く
        root.position = new Vector3(tv.position.x, floor, tv.position.z) + right * SideOffset;
        root.rotation = Quaternion.LookRotation(-toViewer, Vector3.up);   // Quad は -Z 側が表なので、+Z を奥（見る人と反対）へ

        // 画面
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(Shader.Find("TejaVideo/Screen")); AssetDatabase.CreateAsset(mat, MatPath); }
        mat.SetFloat("_ScreenAspect", ScreenW / ScreenH);
        var screen = GameObject.CreatePrimitive(PrimitiveType.Quad); screen.name = "Screen";
        Object.DestroyImmediate(screen.GetComponent<Collider>());
        screen.transform.SetParent(root, false);
        screen.transform.localPosition = new Vector3(0f, ScreenCenterY, 0f);
        screen.transform.localScale = new Vector3(ScreenW, ScreenH, 1f);
        screen.GetComponent<MeshRenderer>().sharedMaterial = mat;
        screen.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var vs = screen.AddComponent<VRCAVProVideoScreen>();
        var vsSo = new SerializedObject(vs);
        vsSo.FindProperty("videoPlayer").objectReferenceValue = av;
        vsSo.FindProperty("materialIndex").intValue = 0;
        vsSo.FindProperty("textureProperty").stringValue = "_MainTex";
        vsSo.FindProperty("useSharedMaterial").boolValue = false;
        vsSo.ApplyModifiedPropertiesWithoutUndo();
        // 裏面（黒い板。裏から見て透けないように）
        var back = GameObject.CreatePrimitive(PrimitiveType.Quad); back.name = "Back";
        Object.DestroyImmediate(back.GetComponent<Collider>());
        back.transform.SetParent(root, false);
        back.transform.localPosition = new Vector3(0f, ScreenCenterY, 0.005f);
        back.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        back.transform.localScale = new Vector3(ScreenW, ScreenH, 1f);
        var blackMat = AssetDatabase.LoadAssetAtPath<Material>(Dir + "TejaVideoBack.mat");
        if (blackMat == null) { blackMat = new Material(Shader.Find("Unlit/Color")); blackMat.color = new Color(0.05f, 0.05f, 0.07f); AssetDatabase.CreateAsset(blackMat, Dir + "TejaVideoBack.mat"); }
        back.GetComponent<MeshRenderer>().sharedMaterial = blackMat;

        // 音（左右2つ。画面の左右のふち）
        var speakers = new AudioSource[2];
        for (int i = 0; i < 2; i++)
        {
            var s = new GameObject(i == 0 ? "SpeakerL" : "SpeakerR"); s.transform.SetParent(root, false);
            s.transform.localPosition = new Vector3((i == 0 ? -1 : 1) * ScreenW * 0.5f, ScreenCenterY, 0f);
            var a = s.AddComponent<AudioSource>();
            a.playOnAwake = false; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = SoundNear; a.maxDistance = SoundFar; a.dopplerLevel = 0f;
            var sp = s.AddComponent<VRCAVProVideoSpeaker>();
            var spSo = new SerializedObject(sp);
            spSo.FindProperty("videoPlayer").objectReferenceValue = av;
            spSo.FindProperty("mode").intValue = i == 0 ? 1 : 2;   // MonoLeft / MonoRight
            spSo.ApplyModifiedPropertiesWithoutUndo();
            var spatial = s.AddComponent<VRC.SDK3.Components.VRCSpatialAudioSource>();
            var saSo = new SerializedObject(spatial);
            var near = saSo.FindProperty("Near"); if (near != null) near.floatValue = SoundNear;
            var far = saSo.FindProperty("Far"); if (far != null) far.floatValue = SoundFar;
            saSo.ApplyModifiedPropertiesWithoutUndo();
            speakers[i] = a;
        }
        vp.speakers = speakers;
        // 音は あそびば の中だけ、再生中は あそびば のBGMを止める（2026-10-15 てじゃ「動画の音声は遊び場のみ、再生中はあそびばのみBGMオフ」）
        vp.audioZone = roots.First(g => g.name == "FuwaPlayground").transform;
        vp.audioZoneRadius = 17f;   // 島の半径14m＋柵の外の少し
        vp.bgm = Object.FindObjectsOfType<FuwaBgm>(true).FirstOrDefault();

        // ----- 操作パネル（画面のすぐ下。ワールドの Canvas、レーザーで押す） -----
        var canvasGo = new GameObject("Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(root, false);
        var cv = canvasGo.GetComponent<Canvas>(); cv.renderMode = RenderMode.WorldSpace;
        var crt = (RectTransform)canvasGo.transform;
        crt.sizeDelta = new Vector2(1920f, 340f);
        crt.localScale = Vector3.one * 0.001f;
        crt.localPosition = new Vector3(0f, ScreenCenterY - ScreenH * 0.5f - 0.2f, -0.01f);
        crt.localRotation = Quaternion.identity;   // Canvas も Quad と同じく -Z 側（見る人の側）が表
        canvasGo.AddComponent<VRC.SDK3.Components.VRCUiShape>();
        var cbox = canvasGo.AddComponent<BoxCollider>(); cbox.size = new Vector3(1920f, 340f, 10f);   // レーザーが当たる板（左下基準ではなく真ん中基準）
        var bg = canvasGo.AddComponent<Image>(); bg.color = new Color(0.12f, 0.14f, 0.2f, 0.85f);

        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(vp);

        // URL ボタン＝URL入力欄そのもの（オレンジのボタンの見た目）。押すとすぐ VRChat の入力が出る
        //（2026-10-15 てじゃ：ふだん広い入力欄が出ていると入力状態になりっぱなし → ボタンで出す形にしたら「URLボタン→白い枠→押す」の2手になったので、ボタン自体を入力欄に）
        var inGo = new GameObject("UrlInput", typeof(RectTransform), typeof(Image));
        inGo.transform.SetParent(canvasGo.transform, false);
        Place(inGo, 20f, 140f, 200f, 100f);
        inGo.GetComponent<Image>().color = new Color(0.95f, 0.55f, 0.45f);
        var field = inGo.AddComponent<VRCUrlInputField>();
        var txt = Label(inGo.transform, "Text", "", font, 28, Color.white, TextAnchor.MiddleCenter); Fill(txt.gameObject, 8f);
        txt.supportRichText = false; txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        var ph = Label(inGo.transform, "Placeholder", "URL", font, 40, Color.white, TextAnchor.MiddleCenter); Fill(ph.gameObject, 6f);
        var fSo = new SerializedObject(field);
        fSo.FindProperty("m_TextComponent").objectReferenceValue = txt;
        fSo.FindProperty("m_Placeholder").objectReferenceValue = ph;
        fSo.ApplyModifiedPropertiesWithoutUndo();
        // 押した時の色の変化はボタンと同じくらいに
        field.targetGraphic = inGo.GetComponent<Image>();
        var cb = field.colors; cb.highlightedColor = new Color(1f, 0.9f, 0.85f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f); field.colors = cb;
        UnityEventTools.AddStringPersistentListener(field.onEndEdit, ub.SendCustomEvent, nameof(TejaVideoPlayer.OnUrlEntered));
        vp.urlField = field;

        // 今のURLの表示（押すとVRChatの入力が出てコピーできる）
        var dGo = new GameObject("UrlDisplay", typeof(RectTransform), typeof(Image));
        dGo.transform.SetParent(canvasGo.transform, false);
        Place(dGo, 240f, 140f, 1040f, 100f);
        dGo.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.13f, 0.9f);
        var disp = dGo.AddComponent<VRCUrlInputField>();
        var dt = Label(dGo.transform, "Text", "", font, 30, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleLeft); Fill(dt.gameObject, 20f);
        dt.supportRichText = false; dt.horizontalOverflow = HorizontalWrapMode.Overflow;
        var dph = Label(dGo.transform, "Placeholder", "（URLを押して動画を入れてね）", font, 30, new Color(0.55f, 0.6f, 0.7f), TextAnchor.MiddleLeft); Fill(dph.gameObject, 20f);
        var dSo = new SerializedObject(disp);
        dSo.FindProperty("m_TextComponent").objectReferenceValue = dt;
        dSo.FindProperty("m_Placeholder").objectReferenceValue = dph;
        dSo.ApplyModifiedPropertiesWithoutUndo();
        disp.targetGraphic = dGo.GetComponent<Image>();
        var dcb = disp.colors; dcb.highlightedColor = new Color(0.8f, 0.85f, 1f); disp.colors = dcb;
        // 欄の外にはみ出した文字を隠す
        dGo.AddComponent<RectMask2D>();
        UnityEventTools.AddStringPersistentListener(disp.onEndEdit, ub.SendCustomEvent, nameof(TejaVideoPlayer.OnUrlDisplayEdited));
        vp.urlDisplay = disp;

        // ボタン：再生/一時停止・再同期
        var pp = Button(canvasGo.transform, "PlayPause", "▶", font, 1300f, 140f, 200f, 100f, ub, nameof(TejaVideoPlayer.PlayPause));
        vp.playPauseLabel = pp;
        // 再同期は記号だけ（ぐるっと矢印。↻ はフォントに無くてVRChatで豆腐になるので絵で。2026-10-15 てじゃ「記号だけでいいかも」）
        var rs = Button(canvasGo.transform, "Resync", "", font, 1520f, 140f, 200f, 100f, ub, nameof(TejaVideoPlayer.Resync));
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)); icon.transform.SetParent(rs.transform.parent, false);
        var irt = (RectTransform)icon.transform; irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.sizeDelta = new Vector2(76f, 76f);
        var ii = icon.GetComponent<Image>(); ii.sprite = IconSprite(Dir + "Icon_Resync.png"); ii.raycastTarget = false; ii.preserveAspect = true;

        // 音量
        var slGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
        slGo.name = "Volume"; slGo.transform.SetParent(canvasGo.transform, false);
        Place(slGo, 1410f, 25f, 490f, 90f);
        slGo.transform.Find("Handle Slide Area/Handle").GetComponent<RectTransform>().sizeDelta = new Vector2(36f, 0f);
        var sl = slGo.GetComponent<Slider>(); sl.minValue = 0f; sl.maxValue = 1f; sl.value = vp.defaultVolume;
        UnityEventTools.AddStringPersistentListener(sl.onValueChanged, ub.SendCustomEvent, nameof(TejaVideoPlayer.OnVolumeChanged));
        vp.volumeSlider = sl;
        // スピーカーの絵＝ミュートボタン（2026-10-15 てじゃ「ワールドのボリュームみたいに、音符をミュートボタンに」「サイズもおおきく」「音符よりスピーカーマーク」）
        var muteGo = new GameObject("MuteButton", typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button)); muteGo.transform.SetParent(canvasGo.transform, false);
        Place(muteGo, 1300f, 20f, 100f, 100f);
        var mi = muteGo.GetComponent<Image>(); mi.preserveAspect = true; mi.color = Color.white;
        vp.iconOn = IconSprite(Dir + "Icon_Speaker.png"); vp.iconMuted = IconSprite(Dir + "Icon_SpeakerMute.png"); mi.sprite = vp.iconOn;   // 音符よりスピーカーの絵（てじゃ）
        UnityEventTools.AddStringPersistentListener(muteGo.GetComponent<UnityEngine.UI.Button>().onClick, ub.SendCustomEvent, nameof(TejaVideoPlayer.ToggleMute));
        vp.muteIcon = mi;

        // シークバー（いちばん上の段。つまんで動かすとその位置へ。ライブの時は隠す）
        var skGo = DefaultControls.CreateSlider(new DefaultControls.Resources());
        skGo.name = "Seek"; skGo.transform.SetParent(canvasGo.transform, false);
        Place(skGo, 30f, 262f, 1860f, 56f);
        var sk = skGo.GetComponent<Slider>(); sk.minValue = 0f; sk.maxValue = 1f; sk.value = 0f;
        var fill = skGo.transform.Find("Fill Area/Fill"); if (fill != null) fill.GetComponent<Image>().color = new Color(0.95f, 0.55f, 0.45f);
        UnityEventTools.AddStringPersistentListener(sk.onValueChanged, ub.SendCustomEvent, nameof(TejaVideoPlayer.OnSeek));
        vp.seekSlider = sk;

        // 状態・時間
        var st = Label(canvasGo.transform, "Status", "", font, 34, new Color(1f, 0.85f, 0.6f), TextAnchor.MiddleLeft); Place(st.gameObject, 20f, 30f, 900f, 90f);
        vp.statusText = st;
        var tm = Label(canvasGo.transform, "Time", "", font, 36, Color.white, TextAnchor.MiddleRight); Place(tm.gameObject, 900f, 30f, 280f, 90f);
        vp.timeText = tm;

        UdonSharpEditorUtility.CopyProxyToUdon(vp);
        // 同期は Manual（AddComponent 直後は Continuous のことがある）
        if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; EditorUtility.SetDirty(ub); }
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("TejaVideoPlayer: placed at " + root.position);
    }

    // 左下基準（Canvas の左下が (0,0)）で置く
    static void Place(GameObject g, float x, float y, float w, float h)
    {
        var rt = (RectTransform)g.transform;
        rt.anchorMin = rt.anchorMax = Vector2.zero; rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(x, y); rt.sizeDelta = new Vector2(w, h);
    }

    static void Fill(GameObject g, float pad)
    {
        var rt = (RectTransform)g.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(pad, 0f); rt.offsetMax = new Vector2(-pad, 0f);
    }

    static Text Label(Transform parent, string name, string text, Font font, int size, Color col, TextAnchor anchor)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Text)); g.transform.SetParent(parent, false);
        var t = g.GetComponent<Text>(); t.text = text; t.font = font; t.fontSize = size; t.color = col; t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate; t.raycastTarget = false;
        return t;
    }

    static Text Button(Transform parent, string name, string text, Font font, float x, float y, float w, float h, VRC.Udon.UdonBehaviour ub, string evt)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button)); g.transform.SetParent(parent, false);
        Place(g, x, y, w, h);
        g.GetComponent<Image>().color = new Color(0.95f, 0.55f, 0.45f);
        var b = g.GetComponent<UnityEngine.UI.Button>();
        UnityEventTools.AddStringPersistentListener(b.onClick, ub.SendCustomEvent, evt);
        var t = Label(g.transform, "Label", text, font, 40, Color.white, TextAnchor.MiddleCenter); Fill(t.gameObject, 6f);
        return t;
    }

    static Sprite IconSprite(string path)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp != null && (imp.textureType != TextureImporterType.Sprite || !imp.alphaIsTransparency))
        {
            imp.textureType = TextureImporterType.Sprite; imp.alphaIsTransparency = true; imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void EnsureProgram()
    {
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(ProgramPath) != null) return;
        var prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(ScriptPath);
        AssetDatabase.CreateAsset(prog, ProgramPath);
        AssetDatabase.SaveAssets();
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
    }
}
