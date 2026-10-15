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
        return core;
    }

    // ===== テスト用：テレビの横に画面だけ置く（ほかのワールドで使う時の形） =====
    [MenuItem("FuwaCourse/あそびば/動画プレイヤー（テスト）をテレビの横に置く")]
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
