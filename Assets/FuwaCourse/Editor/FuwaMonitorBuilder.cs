using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 写真館の島のモニター（2026-10-11 てじゃ「ひとりでモードにいる誰かを映すモニター、対象の人とアングルは切り替え可能で」）
// ・名場面ボードの右に同じ高さのボード＋16:9の画面。右に人物ボタン（◀ 名前 ▶）、その下にCAMボタン（OFF→後ろ→右横→左横→上）
// ・その右に半透明の球：VRChatカメラを入れている間だけ、カメラの映像＝モニターの映像
// ・カメラは FuwaFollowCam（FuwaSystem）。他の人のふわふわは、モニターにだけ写るレイヤー（MonitorOnly）の毛玉で映し、
//   自分の画面の半透明の分身・HUD はモニターに写さない（NoMonitor レイヤー）
// FuwaPhotoStudioBuilder（写真館の島を作り直す）の最後に呼ばれる。単体でも何度でも実行できる
public static class FuwaMonitorBuilder
{
    public const int MonitorLayer = 22, GhostLayer = 23;
    const string MonitorRTPath = "Assets/FuwaCourse/Generated/MonitorFeed.renderTexture";
    const string ScreenMatPath = "Assets/FuwaCourse/Generated/MonitorScreen.mat";
    const string ZoneMatPath = "Assets/FuwaCourse/Generated/MonitorCamZone.mat";
    const string PersonIconPath = "Assets/FuwaCourse/Textures/Icon_Person.png";
    // 名場面ボードと同じ高さ・同じ写真の段の高さ（FuwaPhotoAlbumBuilder の Gallery* と合わせる）
    const float Bottom = 0.42f,   // 2026-10-12 てじゃ「ちょっと上げよう」で 0.12→0.42（名場面ボードと同じ）
         TitleH = 0.46f, Margin = 0.1f, ScreenH = 2 * 0.75375f + 0.06f, BoardScale = 0.95f;

    [MenuItem("FuwaCourse/写真館のモニターを作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new System.Collections.Generic.List<string>();
        EnsureLayers();
        var all = Object.FindObjectsOfType<Transform>(true);
        var lobby = all.First(t => t.name == "FuwaLobby" && t.parent == null);
        var sys = all.First(t => t.name == "FuwaSystem" && t.parent == null);
        var studio = lobby.Find("PhotoStudio");
        var follow = Object.FindObjectsOfType<FuwaFollowCam>(true).First();
        var best = all.First(t => t.name == "BestTimes" && t.GetComponentInChildren<TMPro.TextMeshPro>(true) != null);
        var srcPanel = best.Find("Panel").GetComponent<SpriteRenderer>();
        var srcText = best.Find("Text").GetComponent<TMPro.TextMeshPro>();

        // ---- 自分の目からモニター専用の物を隠す／モニターのカメラからは分身を隠す ----
        var desc = Object.FindObjectsOfType<VRC.SDKBase.VRC_SceneDescriptor>(true).First();
        if (desc.ReferenceCamera != null)
        {
            var rc = desc.ReferenceCamera.GetComponent<Camera>();
            Undo.RecordObject(rc, "mask"); rc.cullingMask &= ~(1 << MonitorLayer); EditorUtility.SetDirty(rc);
        }
        Undo.RecordObject(follow.cam, "mask");
        follow.cam.cullingMask = (follow.cam.cullingMask | (1 << MonitorLayer)) & ~(1 << GhostLayer);
        EditorUtility.SetDirty(follow.cam);
        foreach (var g in Object.FindObjectsOfType<FuwaGhost>(true))
        {
            Undo.RecordObject(g.gameObject, "layer"); g.gameObject.layer = GhostLayer;
            foreach (var n in new[] { "Front", "Back" }) { var c = g.transform.Find(n); if (c != null) c.gameObject.layer = GhostLayer; }
        }
        // HUD（エア・タイム・状態の表示）もモニターには写さない（てじゃ「モニターはHUD強制非表示」）
        foreach (var h in Object.FindObjectsOfType<FuwaHud>(true))
            foreach (var r in h.GetComponentsInChildren<Renderer>(true)) { Undo.RecordObject(r.gameObject, "layer"); r.gameObject.layer = GhostLayer; }

        // ---- 映像（モニター用は軽め。VRChatカメラに出す時は今までの高画質の FollowCamFeed）----
        var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(MonitorRTPath);
        if (rt == null) { rt = new RenderTexture(1280, 720, 24) { name = "MonitorFeed", antiAliasing = 2 }; AssetDatabase.CreateAsset(rt, MonitorRTPath); }
        var feedRT = follow.feedTexture != null ? follow.feedTexture : follow.cam.targetTexture;
        if (feedRT == rt) feedRT = AssetDatabase.FindAssets("FollowCamFeed t:RenderTexture").Select(id => AssetDatabase.LoadAssetAtPath<RenderTexture>(AssetDatabase.GUIDToAssetPath(id))).FirstOrDefault();
        var screenMat = AssetDatabase.LoadAssetAtPath<Material>(ScreenMatPath);
        if (screenMat == null) { screenMat = new Material(Shader.Find("Unlit/Texture")) { name = "MonitorScreen" }; AssetDatabase.CreateAsset(screenMat, ScreenMatPath); }
        screenMat.mainTexture = rt; EditorUtility.SetDirty(screenMat);

        // ---- モニターのボード ----
        var spot = studio.Find("MonitorSpot");
        var old = studio.Find("Monitor"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var board = new GameObject("Monitor").transform;
        board.SetParent(studio, false);
        board.SetPositionAndRotation(spot.position, spot.rotation);
        board.localScale = Vector3.one * BoardScale;
        float sw = ScreenH * 16f / 9f, W = sw + 2 * Margin, H = TitleH + ScreenH + Margin, top = Bottom + H;
        var panel = Object.Instantiate(srcPanel.gameObject, board).GetComponent<SpriteRenderer>();
        panel.name = "Panel";
        panel.transform.localPosition = new Vector3(0f, Bottom + H * 0.5f, 0.03f);
        panel.transform.localRotation = Quaternion.identity; panel.transform.localScale = Vector3.one;
        panel.size = new Vector2(W, H);
        var title = Text(board, "Title", srcText, new Vector3(0f, top - TitleH * 0.5f - 0.02f, 0f), new Vector2(W - 0.3f, 0.4f), 2.4f);
        const string titleJa = "<color=#FF6B5C>おっかけカメラ</color>", titleEn = "<color=#FF6B5C>Chase Cam</color>";   // てじゃ「モニターは分かりにくい」→「おっかけカメラいいな、かわいい」
        title.text = titleJa;
        float sy = top - TitleH - ScreenH * 0.5f;
        // 画面（映像）
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "Screen";
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(board, false);
        q.transform.localPosition = new Vector3(0f, sy, 0.005f);
        q.transform.localScale = new Vector3(sw, ScreenH, 1f);
        var qr = q.GetComponent<MeshRenderer>(); qr.sharedMaterial = screenMat;
        qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; qr.receiveShadows = false; qr.enabled = false;
        // OFF の時の画面（暗い板＋文字）
        var off = new GameObject("Off").transform; off.SetParent(board, false); off.localPosition = new Vector3(0f, sy, 0f);
        var dark = Object.Instantiate(srcPanel.gameObject, off).GetComponent<SpriteRenderer>();
        dark.name = "Dark"; dark.transform.localPosition = new Vector3(0f, 0f, 0.01f); dark.transform.localRotation = Quaternion.identity;
        dark.transform.localScale = Vector3.one * 0.12f; dark.size = new Vector2(sw, ScreenH) / 0.12f;
        dark.color = new Color(0.25f, 0.27f, 0.33f, 1f);
        var offText = Text(off, "Text", srcText, Vector3.zero, new Vector2(sw - 0.2f, ScreenH - 0.2f), 1.5f);
        // 「CAM」の文字のかわりにボタンと同じビデオカメラの絵（TMP のスプライト。tint=1 で文字の色になる）
        offText.spriteAsset = AssetDatabase.LoadAssetAtPath<TMPro.TMP_SpriteAsset>("Assets/FuwaCourse/Textures/Icon_Video.asset");
        const string offJa = "<size=160%>OFF</size>\n<sprite=0 tint=1> ボタンで映すよ", offEn = "<size=160%>OFF</size>\nPress <sprite=0 tint=1> to watch";
        offText.text = offJa; offText.color = new Color(0.85f, 0.87f, 0.92f, 1f);
        // 映している人の名前（他の人の時だけ、画面の左上）
        var nameT = Text(board, "Name", srcText, new Vector3(-sw * 0.5f + 0.1f, top - TitleH - 0.12f, -0.005f), new Vector2(sw - 0.2f, 0.2f), 1.3f);
        nameT.alignment = TMPro.TextAlignmentOptions.Left; nameT.color = Color.white; nameT.text = "";
        nameT.fontStyle = TMPro.FontStyles.Bold;
        nameT.outlineWidth = 0.2f; nameT.outlineColor = new Color32(40, 40, 60, 255);

        // ---- ボタン：人物（上）・CAM（下）----
        var bspot = studio.Find("ButtonSpot");
        var cam = studio.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "CamButton" && t.Find("Canvas") != null);
        cam.SetParent(studio, true);
        cam.SetPositionAndRotation(bspot.position + Vector3.up * 0.80f, bspot.rotation);   // ボタンの高さ 0.29m、すき間 6cm（てじゃ「やや下げる・隙間もへらそう」）
        var oldP = studio.Find("TargetButton"); if (oldP != null) Object.DestroyImmediate(oldP.gameObject);
        var person = Object.Instantiate(cam.gameObject, studio).transform;
        person.name = "TargetButton";
        person.SetPositionAndRotation(bspot.position + Vector3.up * 1.15f, bspot.rotation);
        person.localScale = cam.localScale;
        var icon = person.Find("Canvas/Button/Seg/Icon").GetComponent<Image>();
        var pimp = (TextureImporter)AssetImporter.GetAtPath(PersonIconPath);
        if (pimp.textureType != TextureImporterType.Sprite) { pimp.textureType = TextureImporterType.Sprite; pimp.mipmapEnabled = false; pimp.alphaIsTransparency = true; pimp.textureCompression = TextureImporterCompression.Uncompressed; pimp.SaveAndReimport(); }
        icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PersonIconPath);
        SetCall(person.Find("Canvas/Button").GetComponent<Button>(), "NextTarget");
        SetCall(person.Find("Canvas/Button/BackButton") != null ? person.Find("Canvas/Button/BackButton").GetComponent<Button>() : person.GetComponentsInChildren<Button>(true).First(b => b.name == "BackButton"), "PrevTarget");
        // 名前が入るので横に長く（左はしは CAMボタンにそろえて右へ伸ばす。てじゃ「名前だすならボタン短すぎる」）
        {
            const float E = 600f;   // 伸ばす幅(px)
            var cv = (RectTransform)person.Find("Canvas");
            cv.sizeDelta += new Vector2(E, 0f);
            var bc = cv.GetComponent<BoxCollider>(); bc.size = new Vector3(cv.sizeDelta.x, bc.size.y, bc.size.z);
            void Shift(string path, float dx, float dw)
            {
                var r = (RectTransform)person.Find(path); if (r == null) return;
                r.anchoredPosition += new Vector2(dx, 0f); r.sizeDelta += new Vector2(dw, 0f);
            }
            Shift("Canvas/Button/Seg/Head", -E / 2, 0f);
            Shift("Canvas/Button/Seg/Icon", -E / 2, 0f);
            Shift("Canvas/Button/Seg/ArrowL", -E / 2, 0f);
            Shift("Canvas/Button/Seg/PillA", 0f, E);
            Shift("Canvas/Button/Seg/TextA", 0f, E);
            Shift("Canvas/Button/Seg/ArrowR", E / 2, 0f);
            Shift("Canvas/BackButton", -E / 4, E / 2);
            person.position += person.right * (E / 2) * cv.lossyScale.x;
        }
        var personText = person.Find("Canvas/Button/Seg/TextA").GetComponent<TMPro.TextMeshProUGUI>();
        // 名前には漢字なども入るので、漢字のある丸ゴシック（M PLUS Rounded 1c、足りない字は Kosugi Maru）に（Darumadrop One は漢字がない）
        personText.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/FuwaCourse/Fonts/MPLUSRounded1c SDF.asset");
        personText.fontSharedMaterial = personText.font.material;
        personText.fontStyle = TMPro.FontStyles.Bold;
        personText.text = "ME"; personText.enableAutoSizing = true; personText.fontSizeMin = 60f; personText.fontSizeMax = personText.fontSize;

        // ---- VRChatカメラを入れる球 ----
        var zspot = studio.Find("ZoneSpot");
        var oldZ = studio.Find("CamZone"); if (oldZ != null) Object.DestroyImmediate(oldZ.gameObject);
        var zone = new GameObject("CamZone").transform; zone.SetParent(studio, false);
        zone.SetPositionAndRotation(zspot.position + Vector3.up * 0.9f, zspot.rotation);   // 地面寄り（てじゃ）
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); ball.name = "Ball";
        Object.DestroyImmediate(ball.GetComponent<Collider>());
        ball.transform.SetParent(zone, false); ball.transform.localScale = Vector3.one * 0.9f;
        var zmat = AssetDatabase.LoadAssetAtPath<Material>(ZoneMatPath);
        if (zmat == null)
        {
            var bubble = Object.FindObjectsOfType<FuwaGhost>(true).First().shield.GetComponent<Renderer>().sharedMaterial;   // シャボン玉の材質
            zmat = new Material(bubble) { name = "MonitorCamZone" }; AssetDatabase.CreateAsset(zmat, ZoneMatPath);
        }
        var br = ball.GetComponent<MeshRenderer>(); br.sharedMaterial = zmat;
        br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; br.receiveShadows = false;
        var hint = Text(zone, "Hint", srcText, new Vector3(0f, 0.66f, 0f), new Vector2(1.4f, 0.4f), 0.9f);   // 説明は球の上
        const string hintJa = "VRChatカメラを入れると\nおっかけカメラの映像になるよ", hintEn = "Put your VRChat camera here\nto film the Chase Cam view";
        hint.text = hintJa;
        // 球の上にカメラの絵
        var camIcon = new GameObject("Icon").AddComponent<SpriteRenderer>();
        camIcon.transform.SetParent(zone, false); camIcon.transform.localPosition = new Vector3(0f, 0f, 0f);
        camIcon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/FuwaCourse/Textures/Icon_Camera.png");
        camIcon.color = new Color(1f, 1f, 1f, 0.85f);
        camIcon.transform.localScale = Vector3.one * 0.06f;

        // ---- 他の人のふわふわ用の毛玉（モニターのカメラにだけ写る）----
        var oldF = sys.Find("MonitorFluff"); if (oldF != null) Object.DestroyImmediate(oldF.gameObject);
        var srcBall = Object.FindObjectsOfType<FuwaBall>(true).First(b => b.name == "FuwaBall");
        var fluff = new GameObject("MonitorFluff"); fluff.layer = MonitorLayer;
        fluff.transform.SetParent(sys, false); fluff.transform.localScale = srcBall.transform.localScale;
        fluff.AddComponent<MeshFilter>().sharedMesh = srcBall.GetComponent<MeshFilter>().sharedMesh;
        var fr = fluff.AddComponent<MeshRenderer>(); fr.sharedMaterials = srcBall.GetComponent<MeshRenderer>().sharedMaterials;
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var srcTrail = srcBall.GetComponent<TrailRenderer>(); TrailRenderer ftrail = null;
        if (srcTrail != null) { ftrail = fluff.AddComponent<TrailRenderer>(); EditorUtility.CopySerialized(srcTrail, ftrail); }
        fluff.SetActive(false);

        // ---- 古い物の撤去：CAMの説明の札、たいせん・タイトル看板のCAMボタン ----
        var info = studio.Find("CamInfo"); if (info != null) { Object.DestroyImmediate(info.gameObject); log.Add("removed CamInfo"); }
        foreach (var t in Object.FindObjectsOfType<Transform>(true).Where(t => t != null && t.name == "CamButton" && t.Find("Canvas") != null && t.parent != studio).ToArray())
        { log.Add("removed CamButton under " + t.parent.name); Object.DestroyImmediate(t.gameObject); }

        // ---- 言語の切り替え（タイトル・OFF・球の説明）。消えた札は詰める ----
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        if (lang != null)
        {
            var so = new SerializedObject(lang);
            var tx = so.FindProperty("texts"); var pj = so.FindProperty("ja"); var pe = so.FindProperty("en");
            for (int i = tx.arraySize - 1; i >= 0; i--)
                if (tx.GetArrayElementAtIndex(i).objectReferenceValue == null)
                { tx.DeleteArrayElementAtIndex(i); if (i < pj.arraySize) pj.DeleteArrayElementAtIndex(i); if (i < pe.arraySize) pe.DeleteArrayElementAtIndex(i); }
            void Add(Object t, string ja, string en)
            {
                int n = tx.arraySize; tx.arraySize = n + 1; pj.arraySize = n + 1; pe.arraySize = n + 1;
                tx.GetArrayElementAtIndex(n).objectReferenceValue = t; pj.GetArrayElementAtIndex(n).stringValue = ja; pe.GetArrayElementAtIndex(n).stringValue = en;
            }
            Add(title, titleJa, titleEn);
            Add(offText, offJa, offEn);
            Add(hint, hintJa, hintEn);
            so.ApplyModifiedPropertiesWithoutUndo();
            UdonSharpEditorUtility.CopyProxyToUdon(lang); EditorUtility.SetDirty(lang);
        }

        // ---- カメラの設定 ----
        follow.monitorScreen = qr;
        follow.monitorOffMark = off.gameObject;
        follow.monitorTexture = rt;
        follow.feedTexture = feedRT;
        follow.zone = zone;
        follow.zoneRadius = 0.45f;
        follow.zoneRenderer = br;
        follow.targetTexts = new[] { personText };
        follow.monitorName = nameT;
        follow.monitorFluff = fluff.transform;
        follow.monitorFluffTrail = ftrail;
        follow.modeTexts = new[] { cam.Find("Canvas/Button/Seg/TextA").GetComponent<TMPro.TextMeshProUGUI>() };
        follow.modePills = new[] { cam.Find("Canvas/Button/Seg/PillA").GetComponent<Image>() };
        follow.runWithoutPhotoCamera = false;
        follow.cam.targetTexture = rt;
        UdonSharpEditorUtility.CopyProxyToUdon(follow); EditorUtility.SetDirty(follow);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        log.Add("monitor at " + board.position.ToString("F2"));
        return string.Join("\n", log);
    }

    static void SetCall(Button b, string evt)
    {
        var so = new SerializedObject(b);
        var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        for (int i = 0; i < calls.arraySize; i++)
        {
            var e = calls.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("m_MethodName").stringValue == "SendCustomEvent") e.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = evt;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static TMPro.TextMeshPro Text(Transform parent, string name, TMPro.TextMeshPro src, Vector3 pos, Vector2 size, float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TMPro.TextMeshPro>();
        t.font = src.font; t.fontSharedMaterial = src.fontSharedMaterial; t.color = src.color;
        t.fontSize = fontSize; t.alignment = TMPro.TextAlignmentOptions.Center;
        t.enableWordWrapping = false; t.overflowMode = TMPro.TextOverflowModes.Overflow;
        t.rectTransform.sizeDelta = size;
        go.transform.localPosition = pos; go.transform.localRotation = Quaternion.identity;
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return t;
    }

    // レイヤー名を付ける（22 MonitorOnly：モニターのカメラにだけ写る、23 NoMonitor：分身・HUD。自分の目には写る・モニターには写らない）
    static void EnsureLayers()
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        void Set(int i, string n) { var e = layers.GetArrayElementAtIndex(i); if (e.stringValue != n) e.stringValue = n; }
        Set(MonitorLayer, "MonitorOnly"); Set(GhostLayer, "NoMonitor");
        tm.ApplyModifiedPropertiesWithoutUndo();
    }
}
