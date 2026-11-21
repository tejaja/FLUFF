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
    const float VolumeWidth = 1920f;     // 音量バーの横幅（曲名の段とそろえる。CAM の単位）
    // 動画優先ボタン（単体）：カメラの球（-8°）と蓄音機（4°）の間の柵ぎわ
    public const float VideoAngle = -2.1f;
    public const float VideoRadius = 13.45f;
    public const float VideoHeight = 0.62f;   // 床から(m)

    // えらべる曲（FuwaBgm.tracks の番号）。最後（おまけ）は全クリ後
    // 2026-11-21 てじゃ「おまかせってなに？」「そもそもロビーと同じ」→おまかせは消して先頭をロビーに
    // 最後は BGMなし（-2。2026-11-21 てじゃ「BGMなしもいれとこ」）。おまけ（LockedIndex）は全クリ後だけ出る
    static readonly int[] Tracks = { 0, 6, 1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 7, 14, -2 };
    const int LockedIndex = 13;
    static readonly string[] Ja = { "ロビー", "れんしゅう", "1 かぜのおか", "2 いわのみち", "3 やみのもり", "4 まもののす", "5 きのこのたに",
        "2–1 みずのこみち", "2–2 ほのおのやま", "2–3 こおりのみち", "2–4 はぐるまのしま", "2–5 そらのいせき", "ゴールのあと", "おまけ", "BGMなし" };
    static readonly string[] En = { "Lobby", "Practice", "1 Windy Hill", "2 Rock Road", "3 Dark Forest", "4 Monster Den", "5 Mushroom Valley",
        "2-1 Water Path", "2-2 Flame Mountain", "2-3 Ice Road", "2-4 Gear Isles", "2-5 Sky Ruins", "After the goal", "Bonus", "No music" };

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
        var record = Mat("Gramo_Record", new Color(0.22f, 0.22f, 0.32f));
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
        //  上の段：◀ 曲名 ▶（右半分で次、左半分で前）／下の段：音量バー／動画優先 ｜ ◀ ON/OFF ▶ は蓄音機の横に単体で（どこを押しても切りかえ。ON＝動画の間BGMを止める）
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
        // 下の段：音量バー（2026-11-21 てじゃ「ボリューム調整が遠いのがきにいらない」「動画優先の場所がボリュームがしっくりくる」）
        // あそびばの設定ボードの音量バーの写し。FuwaBgm.volumeSliders に足すので、どっちで動かしても同じ音量（ボードのも残す）
        var srcVol = pg.transform.Find("HowToPlay/BgmVolume");
        if (srcVol != null)
        {
            var v = Object.Instantiate(srcVol.gameObject, T, false).transform; v.name = "VolumeBar";
            v.SetPositionAndRotation(T.TransformPoint(new Vector3(0f, 0.165f, 0.398f)), front);
            var cv = (RectTransform)v.Find("Canvas");
            cv.sizeDelta = new Vector2(VolumeWidth, cv.sizeDelta.y);
            var box = cv.GetComponent<BoxCollider>(); if (box != null) box.size = new Vector3(cv.sizeDelta.x, cv.sizeDelta.y, box.size.z);
            float k = 0.15f / (cv.sizeDelta.y * cv.localScale.y);
            v.localScale = Vector3.one * (k / T.lossyScale.x);
            var fill = v.GetComponentsInChildren<UnityEngine.UI.Image>(true).FirstOrDefault(x => x.name == "Fill"); if (fill != null) fill.color = Theme;
            var sl = v.GetComponentInChildren<UnityEngine.UI.Slider>(true);
            if (bgm != null && sl != null)
            {
                var list = (bgm.volumeSliders ?? new UnityEngine.UI.Slider[0]).Where(x => x != null).ToList();
                if (!list.Contains(sl)) list.Add(sl);
                bgm.volumeSliders = list.ToArray();
            }
        }

        // 動画優先：蓄音機から出して、カメラの球と蓄音機の間の柵ぎわに単体で浮かせる（おもちゃをもどすボタンと同じ浮かせ方）
        //（2026-11-21 てじゃ「BGM動画優先ボタンをその間におこうかな」「柵寄りに」）。ON＝動画の間BGMを止める
        float va = VideoAngle * Mathf.Deg2Rad;
        var vOut = new Vector3(Mathf.Sin(va), 0f, Mathf.Cos(va));
        var vPos = Center + vOut * VideoRadius;
        float vFloor = Center.y;
        if (Physics.Raycast(vPos + Vector3.up * 5f, Vector3.down, out var vHit, 10f, ~0, QueryTriggerInteraction.Ignore)) vFloor = vHit.point.y;
        vPos.y = vFloor + VideoHeight;
        var videoRow = Seg("VideoButton", vPos, Quaternion.LookRotation(vOut), 0.22f, 0f, 520f, "ToggleVideoKeep", "ToggleVideoKeep");
        {
            videoRow.GetComponentsInChildren<UnityEngine.UI.Image>(true).First(x => x.name == "Icon").gameObject.SetActive(false);
            var head = Find(videoRow, "Head"); head.gameObject.SetActive(true);
            head.font = mplus; head.fontSharedMaterial = mplus.material; head.fontStyle = TMPro.FontStyles.Bold;   // 漢字（Darumadrop One は漢字がない）
            { var m = head.margin; head.margin = new Vector4(m.x, m.y, m.z, Mathf.Max(0f, m.w - 40f)); }
            head.enableAutoSizing = true; head.fontSizeMin = 60f; head.fontSizeMax = 150f; head.enableWordWrapping = false;
            head.text = "動画優先";
            gm.videoHead = head;
            gm.videoText = Find(videoRow, "TextA"); gm.videoText.text = "ON";   // 動画優先 ON がふだん
        }
        gm.activeTextColor = Theme; gm.idleTextColor = ThemeIdle;
        gm.videoText.color = gm.activeTextColor; gm.trackText.color = gm.activeTextColor;
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
