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
// ・画面・音・操作パネルは、あそびばの観戦リビングのテレビに組み込む（おっかけカメラ⇄動画の切りかえ、BuildTv）。テレビの横に画面だけ置くテスト版（Build）も残す
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
    const float TabH = 72f;   // パネルの下のたたむタブの段の高さ（Canvas の単位）
    public static int Style = 2;   // 操作パネルの見た目（TejaVideoStyle：0=A シンプル、1=B FLUFF風、2=C ぽってり）

    // ===== テスト用：テレビの横に画面だけ置く（ほかのワールドで使う時の形） =====
    [MenuItem("FuwaCourse/あそびば/動画プレイヤー（テスト）をテレビの横に置く")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var room = roots.First(g => g.name == "FuwaPlayground").transform.Find("Room");
        var tv = room.Find("Monitor");
        RemoveScreens(room);
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        var core = EnsureCore(roots);
        var av = core.Item1; var vp = core.Item2;

        // 置き場所：テレビの横。画面はテレビと同じ向き（島の真ん中から見える向き）
        var root = new GameObject("VideoScreenTest").transform; root.SetParent(room, false);
        var floor = room.Find("Floor").GetComponent<Renderer>().bounds.max.y;
        Vector3 center = room.position; center.y = tv.position.y;
        Vector3 toViewer = (center - tv.position); toViewer.y = 0f; toViewer.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, toViewer);   // 見る人から見て左＝この向き。テレビの左に置く
        root.position = new Vector3(tv.position.x, floor, tv.position.z) + right * SideOffset;
        root.rotation = Quaternion.LookRotation(-toViewer, Vector3.up);   // Quad は -Z 側が表なので、+Z を奥（見る人と反対）へ

        var ts = MakeScreen(root, "Screen", new Vector3(0f, ScreenCenterY, 0f), Quaternion.identity, new Vector3(ScreenW, ScreenH, 1f), av, true);
        vp.standbyScreens = new[] { MakeStandby(ts.transform) };
        vp.loadingSpinners = new[] { vp.standbyScreens[0].transform.Find("Spinner") };
        vp.speakers = MakeSpeakers(root, new[] { new Vector3(-ScreenW * 0.5f, ScreenCenterY, 0f), new Vector3(ScreenW * 0.5f, ScreenCenterY, 0f) }, av);
        BuildControls(root, new Vector3(0f, ScreenCenterY - ScreenH * 0.5f - 0.01f - (212f + TabH) * 0.0005f, -0.01f), 0.001f, true, vp, font);
        Finish(vp, scene);
        Selection.activeGameObject = root.gameObject;
    }

    // ===== あそびばの観戦リビングのテレビに組み込む（おっかけカメラ ⇄ 動画） =====
    // 2026-10-15 てじゃ「おすすめでいこう」：見出しの横の丸ボタンで切りかえ＋だれかが再生したら自動で動画、停止・おわりでカメラ。
    // 動画の時は ME／CAM ボタンの所が動画の操作パネルに入れかわる。テレビの画面はもともと16:9（カメラの映像も1280×720）なのでそのまま重ねる
    [MenuItem("FuwaCourse/あそびば/動画プレイヤーをテレビに組み込む")]
    public static void BuildTv()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var room = roots.First(g => g.name == "FuwaPlayground").transform.Find("Room");
        var tv = room.Find("Monitor");             // テレビ全体（向き・位置の基準）
        var inner = tv.Find("Monitor");            // 画面・見出しがある方
        var tvScreen = inner.Find("Screen");
        RemoveScreens(room);
        var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        var core = EnsureCore(roots);
        var av = core.Item1; var vp = core.Item2;

        // 動画の画面：テレビの画面と同じ所・同じ大きさ（動画の時はカメラの画面を非表示にするので重ねない。2026-10-15 てじゃ「横から見ると隙間からチラ見え」）
        var vs = MakeScreen(inner, "VideoScreen", tvScreen.localPosition, tvScreen.localRotation, tvScreen.localScale, av, false);
        vs.SetActive(false);
        vp.standbyScreens = new[] { MakeStandby(vs.transform) };
        vp.loadingSpinners = new[] { vp.standbyScreens[0].transform.Find("Spinner") };
        // 音：画面の左右のふち
        Vector3 sl = tv.InverseTransformPoint(tvScreen.position);
        float hw = tvScreen.lossyScale.x * 0.5f;
        // スピーカーは本体（FuwaSystem/VideoPlayer＝エリアの切り替えで隠れない所）の下に、テレビと同じ位置・向きで置く。
        // テレビと一緒に隠れると、戻ってきた時に AVPro の音が二度と出なくなった（2026-10-15 てじゃ「チャレンジに飛んで戻ると動画の音が復活しない」）。
        // あそびばの外では audioZone で消音するので、隠さなくてよい
        var spRoot = new GameObject("VideoSpeakers").transform; spRoot.SetParent(av.transform, false);
        spRoot.SetPositionAndRotation(tv.position, tv.rotation); spRoot.localScale = Vector3.one;
        { var ls = tv.lossyScale; var pls = av.transform.lossyScale; spRoot.localScale = new Vector3(ls.x / pls.x, ls.y / pls.y, ls.z / pls.z); }
        vp.speakers = MakeSpeakers(spRoot, new[] { sl + new Vector3(-hw, 0f, 0f), sl + new Vector3(hw, 0f, 0f) }, av);

        // 動画の操作パネル：ME／CAM ボタンの所（同じ高さ・テレビの真ん中）
        var tb = tv.Find("TargetButton");
        float rowY = tb != null ? tb.localPosition.y : 0.25f, rowZ = tb != null ? tb.localPosition.z - 0.002f : -0.012f;
        var controls = BuildControls(tv, new Vector3(sl.x, rowY, rowZ), 0.00135f, false, vp, font);
        controls.name = "VideoControls";
        controls.gameObject.SetActive(false);

        // 切りかえの丸ボタン
        var title = inner.Find("Title");
        Vector3 tl = tv.InverseTransformPoint(title.position);
        var tgCv = new GameObject("VideoToggle", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        tgCv.transform.SetParent(tv, false);
        tgCv.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var trt = (RectTransform)tgCv.transform; trt.sizeDelta = new Vector2(160f, 160f); trt.localScale = Vector3.one * 0.00135f;
        // 画面の右の白いふちの下の方（見出しの横は高すぎて押しにくかった。2026-10-15 てじゃ）
        float sw2 = tvScreen.lossyScale.x * 0.5f, sh2 = tvScreen.lossyScale.y * 0.5f;
        trt.localPosition = new Vector3(sl.x + sw2 + 0.16f, sl.y - sh2 + 0.14f, rowZ - 0.004f); trt.localRotation = Quaternion.identity;
        tgCv.AddComponent<VRC.SDK3.Components.VRCUiShape>();
        var tbc = tgCv.AddComponent<BoxCollider>(); tbc.size = new Vector3(160f, 160f, 10f); tbc.isTrigger = true;
        var bGo = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button)); bGo.transform.SetParent(tgCv.transform, false);
        var brt = (RectTransform)bGo.transform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero;
        var bImg = bGo.GetComponent<Image>(); bImg.sprite = IconSprite9(Dir + "UI_Circle.png"); bImg.color = Color.white;
        var bSel = bGo.GetComponent<UnityEngine.UI.Button>();
        var cb = bSel.colors; cb.normalColor = new Color(0.62f, 0.55f, 0.86f); cb.highlightedColor = new Color(0.72f, 0.66f, 0.93f); cb.pressedColor = new Color(0.52f, 0.45f, 0.76f); cb.selectedColor = cb.normalColor; bSel.colors = cb;
        var sh = bGo.AddComponent<Shadow>(); sh.effectColor = new Color(0.36f, 0.3f, 0.55f, 0.35f); sh.effectDistance = new Vector2(0f, -6f);
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)); icon.transform.SetParent(bGo.transform, false);
        var irt = (RectTransform)icon.transform; irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.sizeDelta = new Vector2(104f, 104f);
        var iImg = icon.GetComponent<Image>(); iImg.raycastTarget = false; iImg.preserveAspect = true;
        // 「動画やってるよ」の印：右上のコーラルの点（カメラを見ている間に動画が流れていたら出る）
        var badge = new GameObject("PlayingBadge", typeof(RectTransform), typeof(Image)); badge.transform.SetParent(bGo.transform, false);
        // 白いふち＝ひと回り大きい白い丸の上にコーラルの丸（Outline だと4方向にずらして重ねるだけで、ふちがでこぼこの形になった。2026-10-15 てじゃ「縁の白が◯じゃない」）
        var bdr = (RectTransform)badge.transform; bdr.anchorMin = bdr.anchorMax = new Vector2(0.86f, 0.86f); bdr.sizeDelta = new Vector2(56f, 56f);
        var bdi = badge.GetComponent<Image>(); bdi.sprite = IconSprite9(Dir + "UI_Circle.png"); bdi.color = Color.white; bdi.raycastTarget = false;
        var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image)); dot.transform.SetParent(badge.transform, false);
        var drt = (RectTransform)dot.transform; drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f); drt.sizeDelta = new Vector2(44f, 44f);
        var ddi = dot.GetComponent<Image>(); ddi.sprite = bdi.sprite; ddi.color = new Color(1f, 0.42f, 0.36f); ddi.raycastTarget = false;
        badge.SetActive(false);

        // 操作パネルを隠す／出すボタン：画面の下のふちとパネルの間のすき間の真ん中（パネルを隠しても、ここだけ残る）
        //（2026-10-15 てじゃ「UI隠しボタン」「UIの右上あたり」→「隙間の真ん中くらいもありか」。右上の角だとパネルに隠れて見えなかった）
        const float cs = 0.00135f;
        var hdCv = new GameObject("VideoHide", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        hdCv.transform.SetParent(tv, false);
        hdCv.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var hrt = (RectTransform)hdCv.transform; hrt.sizeDelta = new Vector2(150f, 56f); hrt.localScale = Vector3.one * cs;
        float screenBottom = sl.y - tvScreen.lossyScale.y * 0.5f, panelTop = rowY + 106f * cs;
        hrt.localPosition = new Vector3(sl.x, (screenBottom + panelTop) * 0.5f, rowZ - 0.004f); hrt.localRotation = Quaternion.identity;
        hdCv.AddComponent<VRC.SDK3.Components.VRCUiShape>();
        var hbc = hdCv.AddComponent<BoxCollider>(); hbc.size = new Vector3(150f, 56f, 10f); hbc.isTrigger = true;
        var hGo = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button)); hGo.transform.SetParent(hdCv.transform, false);
        var hbrt = (RectTransform)hGo.transform; hbrt.anchorMin = Vector2.zero; hbrt.anchorMax = Vector2.one; hbrt.offsetMin = Vector2.zero; hbrt.offsetMax = Vector2.zero;
        var hImg = hGo.GetComponent<Image>(); hImg.sprite = IconSprite9(Dir + "UI_Round.png"); hImg.type = Image.Type.Sliced; hImg.color = Color.white;
        var hSel = hGo.GetComponent<UnityEngine.UI.Button>();
        var hcb = hSel.colors; hcb.normalColor = new Color(0.62f, 0.55f, 0.86f); hcb.highlightedColor = new Color(0.72f, 0.66f, 0.93f); hcb.pressedColor = new Color(0.52f, 0.45f, 0.76f); hcb.selectedColor = hcb.normalColor; hSel.colors = hcb;
        var hLab = Label(hGo.transform, "Label", "▲", font, 36, Color.white, TextAnchor.MiddleCenter); Fill(hLab.gameObject, 2f);
        hdCv.SetActive(false);

        // 切りかえの係（自分の画面だけ。テレビと一緒にエリアで隠れてよい）
        var swT = tv.Find("VideoSwitch");
        var swGo = swT != null ? swT.gameObject : new GameObject("VideoSwitch");
        swGo.transform.SetParent(tv, false);
        EnsureProgramFor("Assets/FuwaCourse/Scripts/FuwaTvVideoSwitch.cs", "Assets/FuwaCourse/Scripts/FuwaTvVideoSwitch.asset");
        var sw = swGo.GetComponent<FuwaTvVideoSwitch>(); if (sw == null) sw = UdonSharpUndo.AddComponent<FuwaTvVideoSwitch>(swGo);
        sw.video = vp;
        sw.videoScreen = vs;
        sw.videoControls = controls.gameObject;
        var tbT = tv.Find("TargetButton"); var cbT = tv.Find("CamButton");
        sw.cameraOnly = new[] { tbT != null ? tbT.gameObject : null, cbT != null ? cbT.gameObject : null };
        sw.title = title.GetComponent<TMPro.TextMeshPro>();
        sw.language = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        sw.toggleIcon = iImg;
        sw.hideButton = hdCv;
        sw.playingBadge = badge;
        sw.cameraScreen = tvScreen.GetComponent<Renderer>();
        var offT = inner.Find("Off"); sw.cameraOff = offT != null ? offT.gameObject : null;
        var nameT = inner.Find("Name"); sw.cameraName = nameT != null ? nameT.gameObject : null;
        sw.hideLabel = hLab;
        // BGM はテレビの動画モードで止める（再生中かどうかでは止めない）。動画プレイヤー本体の BGM 止めは使わない（2026-10-15 てじゃ）
        sw.bgm = vp.bgm; vp.bgm = null;
        UdonSharpEditorUtility.CopyProxyToUdon(vp);
        sw.iconToVideo = IconSprite(Dir + "Icon_TvPlay.png");
        sw.iconToCam = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FuwaCourse/Textures/Icon_Video.png");   // CAM ボタンと同じビデオカメラの絵
        iImg.sprite = sw.iconToVideo;
        var swUb = UdonSharpEditorUtility.GetBackingUdonBehaviour(sw);
        UnityEventTools.AddStringPersistentListener(bSel.onClick, swUb.SendCustomEvent, nameof(FuwaTvVideoSwitch.ToggleMode));
        UnityEventTools.AddStringPersistentListener(hSel.onClick, swUb.SendCustomEvent, nameof(FuwaTvVideoSwitch.TogglePanel));
        UdonSharpEditorUtility.CopyProxyToUdon(sw);
        if (swUb != null) { swUb.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(swUb); }

        Finish(vp, scene);
        Selection.activeGameObject = tv.gameObject;
    }

    // 前の画面（テスト・テレビ組み込み）を片付け。本体（同期する物）は作り直さず使い回す
    //（消して作り直すと、シーンのネットワークIDの一覧に古い物が残ってビルドの AssignSceneNetworkIDs で止まることがある）
    static void RemoveScreens(Transform room)
    {
        var tv = room.Find("Monitor");
        var list = new System.Collections.Generic.List<Transform> { room.Find("VideoScreenTest") };
        var sysT = room.root.gameObject.scene.GetRootGameObjects().FirstOrDefault(g => g.name == "FuwaSystem");
        if (sysT != null) list.Add(sysT.transform.Find("VideoPlayer/VideoSpeakers"));
        if (tv != null)
        {
            list.Add(tv.Find("VideoSpeakers")); list.Add(tv.Find("VideoControls")); list.Add(tv.Find("VideoToggle")); list.Add(tv.Find("VideoHide"));
            var inner = tv.Find("Monitor"); if (inner != null) list.Add(inner.Find("VideoScreen"));
            // カメラのボタンが隠れたまま残らないように
            foreach (var n in new[] { "TargetButton", "CamButton", "Monitor/Screen", "Monitor/Name" }) { var b = tv.Find(n); if (b != null) b.gameObject.SetActive(true); }
            var title = tv.Find("Monitor/Title"); var swT = tv.Find("VideoSwitch");
            if (swT != null && title != null) { var s = swT.GetComponent<FuwaTvVideoSwitch>(); if (s != null) title.GetComponent<TMPro.TextMeshPro>().text = s.camTitleJa; }
        }
        foreach (var t in list) if (t != null) Object.DestroyImmediate(t.gameObject);
    }

    static System.Tuple<VRCAVProVideoPlayer, TejaVideoPlayer> EnsureCore(GameObject[] roots)
    {
        EnsureProgram();
        var sys = roots.First(g => g.name == "FuwaSystem").transform;
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
        // 音は あそびば の中だけ、再生中は あそびば のBGMを止める（2026-10-15 てじゃ「動画の音声は遊び場のみ、再生中はあそびばのみBGMオフ」）
        vp.audioZone = roots.First(g => g.name == "FuwaPlayground").transform;
        vp.audioZoneRadius = 17f;   // 島の半径14m＋柵の外の少し
        vp.bgm = Object.FindObjectsOfType<FuwaBgm>(true).FirstOrDefault();
        return System.Tuple.Create(av, vp);
    }

    static GameObject MakeScreen(Transform parent, string name, Vector3 lp, Quaternion lr, Vector3 ls, VRCAVProVideoPlayer av, bool withBack)
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
    static GameObject MakeStandby(Transform screen)
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
    static AudioSource[] MakeSpeakers(Transform parent, Vector3[] localPos, VRCAVProVideoPlayer av)
    {
        var speakers = new AudioSource[localPos.Length];
        for (int i = 0; i < localPos.Length; i++)
        {
            var s = new GameObject(i == 0 ? "SpeakerL" : "SpeakerR"); s.transform.SetParent(parent, false);
            s.transform.localPosition = localPos[i];
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
        return speakers;
    }

    static void Finish(TejaVideoPlayer vp, UnityEngine.SceneManagement.Scene scene)
    {
        UdonSharpEditorUtility.CopyProxyToUdon(vp);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(vp);
        // 同期は Manual（AddComponent 直後は Continuous のことがある）
        if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; EditorUtility.SetDirty(ub); }
        EditorSceneManager.MarkSceneDirty(scene);
    }

    // 操作パネル（2段：上＝シークバー・時間、下＝ボタン・音量）。localPos は Canvas の真ん中、scale は Canvas 1単位の大きさ(m)
    static Transform BuildControls(Transform parent, Vector3 localPos, float scale, bool withTab, TejaVideoPlayer vp, Font font)
    {
        float tabH = withTab ? TabH : 0f;
        // ----- 操作パネル（画面のすぐ下。ワールドの Canvas、レーザーで押す） -----
        var canvasGo = new GameObject("Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(parent, false);
        var cv = canvasGo.GetComponent<Canvas>(); cv.renderMode = RenderMode.WorldSpace;
        var crt = (RectTransform)canvasGo.transform;
        crt.sizeDelta = new Vector2(1920f, 212f + tabH);   // 2段：上＝シークバー・時間（読み込み前は状態の文字）、下＝ボタン・音量（2026-10-15 てじゃ「ボリュームを上に、スリムに」）
        crt.localScale = Vector3.one * scale;
        crt.localPosition = localPos;
        crt.localRotation = Quaternion.identity;   // Canvas も Quad と同じく -Z 側（見る人の側）が表
        canvasGo.AddComponent<VRC.SDK3.Components.VRCUiShape>();
        var cbox = canvasGo.AddComponent<BoxCollider>(); cbox.size = new Vector3(1920f, 212f + tabH, 10f); cbox.isTrigger = true;   // すり抜ける（ほかのレーザーUIと同じ。2026-10-15 てじゃ「コライダーがある」）   // レーザーが当たる板（左下基準ではなく真ん中基準）
        // パネル本体（たたむと消える）と、その下のたたむタブ（2026-10-15 てじゃ「下のUIを隠す・戻す」→ B：パネルの端の小さいタブ。画面クリックだとVRで誤操作するので）
        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image)); panelGo.transform.SetParent(canvasGo.transform, false);
        Place(panelGo, 0f, tabH, 1920f, 212f);
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
        float topW = 1580f;
        Place(skGo, 30f, 146f, topW, 56f);
        var sk = skGo.GetComponent<Slider>(); sk.minValue = 0f; sk.maxValue = 1f; sk.value = 0f;
        var fill = skGo.transform.Find("Fill Area/Fill"); if (fill != null) fill.GetComponent<Image>().color = new Color(0.95f, 0.55f, 0.45f);
        UnityEventTools.AddStringPersistentListener(sk.onValueChanged, ub.SendCustomEvent, nameof(TejaVideoPlayer.OnSeek));
        vp.seekSlider = sk;

        // 状態・時間
        var st = Label(pt, "Status", "", font, 34, new Color(1f, 0.85f, 0.6f), TextAnchor.MiddleLeft); Place(st.gameObject, 30f, 140f, topW, 68f);
        vp.statusText = st;
        var tm = Label(pt, "Time", "", font, 36, Color.white, TextAnchor.MiddleRight); Place(tm.gameObject, 1620f, 140f, 280f, 68f);
        vp.timeText = tm;

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
        vp.panelTab = (RectTransform)tabGo.transform; vp.panelHeight = 212f;
        }

        TejaVideoStyle.Apply(canvasGo.transform, Style);   // 見た目（色・角の丸み）
        return canvasGo.transform;
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

    static void IconOn(Transform parent, string file, float size)
    {
        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)); icon.transform.SetParent(parent, false);
        var irt = (RectTransform)icon.transform; irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.sizeDelta = new Vector2(size, size);
        var ii = icon.GetComponent<Image>(); ii.sprite = IconSprite(Dir + file); ii.raycastTarget = false; ii.preserveAspect = true;
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

    static Sprite IconSprite9(string path)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp != null && (imp.textureType != TextureImporterType.Sprite || !imp.alphaIsTransparency)) { imp.textureType = TextureImporterType.Sprite; imp.alphaIsTransparency = true; imp.SaveAndReimport(); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void EnsureProgramFor(string script, string asset)
    {
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(asset) != null) return;
        var prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(script);
        AssetDatabase.CreateAsset(prog, asset);
        AssetDatabase.SaveAssets();
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
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
