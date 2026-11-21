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
using static TejaVideoPlayerBuilder;

// FLUFF 専用：Teja Player（Assets/TejaVideoPlayer）を、あそびばの観戦リビングのテレビに組み込む（おっかけカメラ⇄動画の切りかえ）。
// ・本体（AVPro の再生部品＋TejaVideoPlayer）とスピーカーは FuwaSystem/VideoPlayer の下（エリア切り替えで隠れない所）
// ・音はあそびばの中だけ（audioZone）、BGM の止め方は FuwaTvVideoSwitch（動画モード or 近くで再生中）
// 2026-10-15 てじゃ「使いまわし出来るように、ここ専用の部分省いてパッケージ化」→ プレイヤー本体の作り方（汎用）は TejaVideoPlayerBuilder に、FLUFF の組み込みだけここに分けた
public static class FuwaTvVideoBuilder
{
    const float SideOffset = 3.1f;                  // テレビの真ん中から横へ（テスト版）
    // 音の届き方：島の端から端（約28m）でも聞こえるように。15mまでそのまま、45mで0（島の外は audioZone で消す）
    //（2026-10-15 てじゃ「音量減衰きつすぎ、あそびば全体聞こえるくらい」。前は3〜20m）
    const float SoundNear = 15f, SoundFar = 45f;

    // 本体は FuwaSystem/VideoPlayer。音はあそびばの島の中だけ
    static System.Tuple<VRCAVProVideoPlayer, TejaVideoPlayer> FluffCore(GameObject[] roots)
    {
        var sys = roots.First(g => g.name == "FuwaSystem").transform;
        var core = EnsureCore(sys, "VideoPlayer");
        var vp = core.Item2;
        // 音は あそびば の中だけ（2026-10-15 てじゃ「動画の音声は遊び場のみ」）。BGM は FuwaTvVideoSwitch が止める
        vp.audioZone = roots.First(g => g.name == "FuwaPlayground").transform;
        vp.audioZoneRadius = 17f;   // 島の半径14m＋柵の外の少し
        vp.bgm = Object.FindObjectsOfType<FuwaBgm>(true).FirstOrDefault();
        vp.saveVolumeKey = "video_volume";   // 自分の動画の音量を保存（2026-11-21 てじゃ「保存してもいいかも」）
        return core;
    }

    // ===== テスト用：テレビの横に画面だけ置く（ほかのワールドで使う時の形） =====
    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/動画プレイヤー（テスト）をテレビの横に置く")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var room = roots.First(g => g.name == "FuwaPlayground").transform.Find("Room");
        var tv = room.Find("Monitor");
        RemoveScreens(room);
        var font = DefaultFont();
        var core = FluffCore(roots);
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
        vp.speakers = MakeSpeakers(root, new[] { new Vector3(-ScreenW * 0.5f, ScreenCenterY, 0f), new Vector3(ScreenW * 0.5f, ScreenCenterY, 0f) }, av, SoundNear, SoundFar);
        BuildControls(root, new Vector3(0f, ScreenCenterY - ScreenH * 0.5f - 0.01f - (PanelH + TabH) * 0.0005f, -0.01f), 0.001f, true, vp, font);
        Finish(vp, scene);
        Selection.activeGameObject = root.gameObject;
    }

    // ===== あそびばの観戦リビングのテレビに組み込む（おっかけカメラ ⇄ 動画） =====
    // 2026-10-15 てじゃ「おすすめでいこう」：見出しの横の丸ボタンで切りかえ＋だれかが再生したら自動で動画、停止・おわりでカメラ。
    // 動画の時は ME／CAM ボタンの所が動画の操作パネルに入れかわる。テレビの画面はもともと16:9（カメラの映像も1280×720）なのでそのまま重ねる
    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/動画プレイヤーをテレビに組み込む")]
    public static void BuildTv()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var room = roots.First(g => g.name == "FuwaPlayground").transform.Find("Room");
        var tv = room.Find("Monitor");             // テレビ全体（向き・位置の基準）
        var inner = tv.Find("Monitor");            // 画面・見出しがある方
        var tvScreen = inner.Find("Screen");
        RemoveScreens(room);
        var font = DefaultFont();
        var core = FluffCore(roots);
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
        vp.speakers = MakeSpeakers(spRoot, new[] { sl + new Vector3(-hw, 0f, 0f), sl + new Vector3(hw, 0f, 0f) }, av, SoundNear, SoundFar);

        // 動画の操作パネル：ME／CAM ボタンの所（同じ高さ・テレビの真ん中）
        var tb = tv.Find("TargetButton");
        float rowZ = tb != null ? tb.localPosition.z - 0.002f : -0.012f;
        // 画面のすぐ下に寄せる（画面の下のふちから PanelGap あけて、パネルの上のふち）（2026-10-15 てじゃ「UIを画面に寄せよう」。前は ME/CAM ボタンと同じ高さ）
        const float PanelGap = 0.025f;
        float rowY = (sl.y - tvScreen.lossyScale.y * 0.5f) - PanelGap - PanelH * 0.5f * 0.00135f;
        var controls = BuildControls(tv, new Vector3(sl.x, rowY, rowZ), 0.00135f, false, vp, font);
        controls.name = "VideoControls";
        controls.gameObject.SetActive(false);

        var title = inner.Find("Title");

        // 操作パネルを隠す／出すボタン：画面の下のふちとパネルの間のすき間の真ん中（パネルを隠しても、ここだけ残る）
        //（2026-10-15 てじゃ「UI隠しボタン」「UIの右上あたり」→「隙間の真ん中くらいもありか」。右上の角だとパネルに隠れて見えなかった）
        const float cs = 0.00135f;
        var hdCv = new GameObject("VideoHide", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        hdCv.transform.SetParent(tv, false);
        hdCv.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        hdCv.GetComponent<Canvas>().sortingOrder = 1;   // 操作パネル（半透明の板、Order 0）より必ず手前に描く。重なる所にあるので、距離順だと見る角度でパネルの下に沈んだ
        var hrt = (RectTransform)hdCv.transform; hrt.sizeDelta = new Vector2(140f, 60f); hrt.localScale = Vector3.one * cs;
        // 操作パネルの右上の角（時間の右）。すき間の真ん中だとシークバーを押し間違えた（2026-10-15 てじゃ「シークバー左に縮めて、時間の右に」）
        hrt.localPosition = new Vector3(sl.x, rowY, rowZ - 0.004f) + HideButtonOffset(cs); hrt.localRotation = Quaternion.identity;
        hdCv.AddComponent<VRC.SDK3.Components.VRCUiShape>();
        var hbc = hdCv.AddComponent<BoxCollider>(); hbc.size = new Vector3(140f, 60f, 10f); hbc.isTrigger = true;
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
        sw.hideButton = hdCv;
        sw.cameraScreen = tvScreen.GetComponent<Renderer>();
        var offT = inner.Find("Off"); sw.cameraOff = offT != null ? offT.gameObject : null;
        var nameT = inner.Find("Name"); sw.cameraName = nameT != null ? nameT.gameObject : null;
        sw.hideLabel = hLab;
        // BGM はテレビの動画モードで止める（再生中かどうかでは止めない）。動画プレイヤー本体の BGM 止めは使わない（2026-10-15 てじゃ）
        sw.bgm = vp.bgm; vp.bgm = null;
        UdonSharpEditorUtility.CopyProxyToUdon(vp);
        sw.iconToVideo = IconSprite(Dir + "Icon_TvPlay.png");
        sw.iconToCam = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FuwaCourse/Textures/Icon_Video.png");   // CAM ボタンと同じビデオカメラの絵
        var swUb = UdonSharpEditorUtility.GetBackingUdonBehaviour(sw);
        UnityEventTools.AddStringPersistentListener(hSel.onClick, swUb.SendCustomEvent, nameof(FuwaTvVideoSwitch.TogglePanel));
        UdonSharpEditorUtility.CopyProxyToUdon(sw);
        if (swUb != null) { swUb.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(swUb); }

        AddModeButtons(tv, sw);
        AddAutoBgmButton(tv, sw);
        Finish(vp, scene);
        Selection.activeGameObject = tv.gameObject;
    }

    // カメラ／動画の切りかえ：画面の右の白いふちに丸ボタンを2つ上下に（上＝おっかけカメラ、下＝動画）。押した方になる、選んでる方は紫で大きめ
    //（2026-11-21 てじゃ「いまはボタンが切り替わるけど、2つ上下に並べて押したほうになる感じにしたい」「選んでる方はちょっと強調」。前は1つのボタンの絵が切りかわる形）
    // 「動画やってるよ」の印（コーラルの点）は動画のボタンの右上
    [MenuItem("FuwaCourse/あそびば/テレビのカメラ・動画ボタンを2つにする")]
    public static void AddModeButtonsMenu()
    {
        var room = EditorSceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "FuwaPlayground").transform.Find("Room");
        var tv = room.Find("Monitor");
        AddModeButtons(tv, tv.Find("VideoSwitch").GetComponent<FuwaTvVideoSwitch>());
        EditorSceneManager.MarkSceneDirty(tv.gameObject.scene);
    }

    const float ModeBtn = 160f, ModeGap = 30f;   // Canvas の単位（0.00135 倍で約 0.22m）
    public static void AddModeButtons(Transform tv, FuwaTvVideoSwitch sw)
    {
        var old = tv.Find("VideoToggle"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var inner = tv.Find("Monitor"); var tvScreen = inner.Find("Screen");
        Vector3 sl = tv.InverseTransformPoint(tvScreen.position);
        float sw2 = tvScreen.lossyScale.x * 0.5f, sh2 = tvScreen.lossyScale.y * 0.5f;
        var tb = tv.Find("TargetButton");
        float rowZ = tb != null ? tb.localPosition.z - 0.002f : -0.012f;
        const float cs = 0.00135f;
        float H = ModeBtn * 2f + ModeGap;
        var cv = new GameObject("VideoToggle", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cv.transform.SetParent(tv, false);
        cv.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)cv.transform; rt.sizeDelta = new Vector2(ModeBtn, H); rt.localScale = Vector3.one * cs;
        // 下のボタンの真ん中が前の1つのボタンと同じ所（画面の右の白いふちの下の方）
        rt.localPosition = new Vector3(sl.x + sw2 + 0.16f, sl.y - sh2 + 0.14f + (H - ModeBtn) * 0.5f * cs, rowZ - 0.004f); rt.localRotation = Quaternion.identity;
        cv.AddComponent<VRC.SDK3.Components.VRCUiShape>();
        var bc = cv.AddComponent<BoxCollider>(); bc.size = new Vector3(ModeBtn, H, 10f); bc.isTrigger = true;
        var swUb = UdonSharpEditorUtility.GetBackingUdonBehaviour(sw);
        var circle = IconSprite9(Dir + "UI_Circle.png");
        var backs = new Image[2]; var icons = new Image[2];
        string[] names = { "CamButton", "VideoButton" };
        string[] evts = { nameof(FuwaTvVideoSwitch.SetCamMode), nameof(FuwaTvVideoSwitch.SetVideoMode) };
        Sprite[] pics = { AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FuwaCourse/Textures/Icon_Video.png"), IconSprite(Dir + "Icon_TvPlay.png") };
        for (int i = 0; i < 2; i++)
        {
            var bGo = new GameObject(names[i], typeof(RectTransform), typeof(Image), typeof(UnityEngine.UI.Button)); bGo.transform.SetParent(cv.transform, false);
            var brt = (RectTransform)bGo.transform; brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.sizeDelta = new Vector2(ModeBtn, ModeBtn);
            brt.anchoredPosition = new Vector2(0f, (i == 0 ? 1f : -1f) * (ModeBtn + ModeGap) * 0.5f);
            var img = bGo.GetComponent<Image>(); img.sprite = circle; img.color = Color.white;
            var sel = bGo.GetComponent<UnityEngine.UI.Button>();
            var cb = sel.colors; cb.normalColor = Color.white; cb.selectedColor = Color.white; cb.highlightedColor = new Color(0.92f, 0.92f, 0.95f); cb.pressedColor = new Color(0.8f, 0.8f, 0.85f); cb.colorMultiplier = 1f; sel.colors = cb;
            var sh = bGo.AddComponent<Shadow>(); sh.effectColor = new Color(0.36f, 0.3f, 0.55f, 0.35f); sh.effectDistance = new Vector2(0f, -6f);
            UnityEventTools.AddStringPersistentListener(sel.onClick, swUb.SendCustomEvent, evts[i]);
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)); icon.transform.SetParent(bGo.transform, false);
            var irt = (RectTransform)icon.transform; irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f); irt.sizeDelta = new Vector2(104f, 104f);
            var ii = icon.GetComponent<Image>(); ii.raycastTarget = false; ii.preserveAspect = true; ii.sprite = pics[i];
            backs[i] = img; icons[i] = ii;
        }
        // 「動画やってるよ」の印：動画のボタンの右上（白いふち＋コーラルの丸）
        var badge = new GameObject("PlayingBadge", typeof(RectTransform), typeof(Image)); badge.transform.SetParent(backs[1].transform, false);
        var bdr = (RectTransform)badge.transform; bdr.anchorMin = bdr.anchorMax = new Vector2(0.86f, 0.86f); bdr.sizeDelta = new Vector2(56f, 56f);
        var bdi = badge.GetComponent<Image>(); bdi.sprite = circle; bdi.color = Color.white; bdi.raycastTarget = false;
        var dot = new GameObject("Dot", typeof(RectTransform), typeof(Image)); dot.transform.SetParent(badge.transform, false);
        var drt = (RectTransform)dot.transform; drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f); drt.sizeDelta = new Vector2(44f, 44f);
        var ddi = dot.GetComponent<Image>(); ddi.sprite = circle; ddi.color = new Color(1f, 0.42f, 0.36f); ddi.raycastTarget = false;
        badge.SetActive(false);

        sw.toggleIcon = null; sw.playingBadge = badge;
        sw.modeOnBack = new Color(0.62f, 0.55f, 0.86f); sw.modeOffBack = new Color(0.89f, 0.86f, 0.97f); sw.modeOnIcon = Color.white; sw.modeOffIcon = new Color(0.62f, 0.55f, 0.86f); sw.modeOffScale = 0.8f;
        sw.modeBacks = backs; sw.modeIcons = icons;
        // 今の見た目（カメラを選んでいる状態）
        for (int i = 0; i < 2; i++)
        {
            bool on = i == 0;
            backs[i].color = on ? sw.modeOnBack : sw.modeOffBack; icons[i].color = on ? sw.modeOnIcon : sw.modeOffIcon;
            float k = on ? 1f : sw.modeOffScale; backs[i].transform.localScale = new Vector3(k, k, 1f);
        }
        UdonSharpEditorUtility.CopyProxyToUdon(sw); EditorUtility.SetDirty(sw);
    }

    // 「Auto BGM OFF」ボタン：操作パネルの音量バーの下、パネルの枠の外（2026-11-21 てじゃ「いっそプレイヤーのUI側に」「ボリュームの下の枠外に追加ボタン」
    //  「Auto BGM OFF って書いてあって、デフォ色がついてる、押すとグレーアウトする」）。色つき＝動画の間BGMを止める（ふだん）、グレー＝止めない。
    // 見た目は操作パネルの ■停止 ボタンの写し。パネルとは別の Canvas（パネルの当たりの外なので）で、出し入れは FuwaTvVideoSwitch がパネルと一緒にする
    [MenuItem("FuwaCourse/あそびば/テレビに Auto BGM OFF ボタンを付ける")]
    public static void AddAutoBgmButtonMenu()
    {
        var room = EditorSceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "FuwaPlayground").transform.Find("Room");
        var tv = room.Find("Monitor");
        AddAutoBgmButton(tv, tv.Find("VideoSwitch").GetComponent<FuwaTvVideoSwitch>());
        EditorSceneManager.MarkSceneDirty(tv.gameObject.scene);
    }

    const float AutoBgmW = 400f, AutoBgmH = 84f, AutoBgmGap = 0.018f;   // Canvas の単位／パネルの下のふちからのすき間(m)
    public static void AddAutoBgmButton(Transform tv, FuwaTvVideoSwitch sw)
    {
        var old = tv.Find("AutoBgmButton"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var controls = tv.Find("VideoControls"); if (controls == null || sw == null) return;
        var panel = (RectTransform)controls.Find("Panel"); var vol = (RectTransform)panel.Find("Volume"); var stop = panel.Find("Stop");
        // 置き場所：音量バーの真下（横）、パネルの下のふち（縦）
        var pc = new Vector3[4]; panel.GetWorldCorners(pc);
        var vc = new Vector3[4]; vol.GetWorldCorners(vc);
        var cvRt0 = (RectTransform)controls;
        float s = cvRt0.lossyScale.x;
        Vector3 down = -controls.up;
        Vector3 volMidX = (vc[0] + vc[3]) * 0.5f;                       // 音量バーの下のふちの真ん中
        float toPanelBottom = Vector3.Dot(pc[0] - volMidX, down);       // そこからパネルの下のふちまで
        Vector3 pos = volMidX + down * (toPanelBottom + AutoBgmGap + AutoBgmH * 0.5f * s);

        var cvGo = new GameObject("AutoBgmButton", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        cvGo.transform.SetParent(tv, false);
        cvGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rt = (RectTransform)cvGo.transform; rt.sizeDelta = new Vector2(AutoBgmW, AutoBgmH);
        rt.SetPositionAndRotation(pos, controls.rotation);
        { var ps = tv.lossyScale.x; rt.localScale = Vector3.one * (s / ps); }
        cvGo.AddComponent<VRC.SDK3.Components.VRCUiShape>();
        var bc = cvGo.AddComponent<BoxCollider>(); bc.size = new Vector3(AutoBgmW, AutoBgmH, 10f); bc.isTrigger = true;

        var b = Object.Instantiate(stop.gameObject, cvGo.transform, false); b.name = "Button";
        var brt = (RectTransform)b.transform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.pivot = new Vector2(0.5f, 0.5f);
        brt.offsetMin = Vector2.zero; brt.offsetMax = Vector2.zero; brt.anchoredPosition = Vector2.zero;
        var img = b.GetComponent<Image>();
        var sel = b.GetComponent<UnityEngine.UI.Button>();
        // 色は Image で持つ（ボタンの色かけは白に。かけるとグレーがにごる）
        var cb = sel.colors; cb.normalColor = Color.white; cb.selectedColor = Color.white; cb.highlightedColor = new Color(0.93f, 0.93f, 0.93f); cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); cb.colorMultiplier = 1f; sel.colors = cb;
        img.color = sw.autoBgmOnColor;
        sel.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        var swUb = UdonSharpEditorUtility.GetBackingUdonBehaviour(sw);
        UnityEventTools.AddStringPersistentListener(sel.onClick, swUb.SendCustomEvent, nameof(FuwaTvVideoSwitch.ToggleAutoBgm));
        // 中身：■の絵などは消して文字だけ
        foreach (Transform c in b.transform.Cast<Transform>().ToArray()) if (c.name != "Label") Object.DestroyImmediate(c.gameObject);
        var lab = b.transform.Find("Label")?.GetComponent<Text>();
        if (lab == null) { lab = Label(b.transform, "Label", "", DefaultFont(), 40, Color.white, TextAnchor.MiddleCenter); }
        Fill(lab.gameObject, 8f);
        lab.text = "Auto BGM OFF"; lab.fontSize = 40; lab.fontStyle = FontStyle.Bold; lab.alignment = TextAnchor.MiddleCenter; lab.horizontalOverflow = HorizontalWrapMode.Overflow;
        cvGo.SetActive(controls.gameObject.activeSelf);

        sw.autoBgmButton = cvGo; sw.autoBgmImage = img;
        sw.bgmMain = Object.FindObjectsOfType<FuwaBgm>(true).FirstOrDefault();
        UdonSharpEditorUtility.CopyProxyToUdon(sw); EditorUtility.SetDirty(sw);
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
            list.Add(tv.Find("VideoSpeakers")); list.Add(tv.Find("VideoControls")); list.Add(tv.Find("VideoToggle")); list.Add(tv.Find("VideoHide")); list.Add(tv.Find("AutoBgmButton"));
            var inner = tv.Find("Monitor"); if (inner != null) list.Add(inner.Find("VideoScreen"));
            // カメラのボタンが隠れたまま残らないように
            foreach (var n in new[] { "TargetButton", "CamButton", "Monitor/Screen", "Monitor/Name" }) { var b = tv.Find(n); if (b != null) b.gameObject.SetActive(true); }
            var title = tv.Find("Monitor/Title"); var swT = tv.Find("VideoSwitch");
            if (swT != null && title != null) { var s = swT.GetComponent<FuwaTvVideoSwitch>(); if (s != null) title.GetComponent<TMPro.TextMeshPro>().text = s.camTitleJa; }
        }
        foreach (var t in list) if (t != null) Object.DestroyImmediate(t.gameObject);
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
}
