using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// ステージ2（つぎのしま）の土台を作る（2026-10-17 てじゃ「そろそろステージ2」）。
// ・場所：ステージ1の真下 Drop m（原点から200m以内。離しすぎると VR で手元が揺れる）
// ・シーンファイルが大きい（79MB、GitHub の100MB制限に近い）ので、中身はプレハブ（Assets/FuwaStage2/Prefabs/FuwaStage2.prefab）にして、
//   シーンには置くだけ。シーンの物（FuwaBall など）を参照する Udon は、ビルダーがシーンに置いた後でつなぐ
// ・ロビーはステージ1と同じ形（てじゃ）：FuwaLobby から床・柵・島・6つのワープの台と看板などの見た目を複製する。
//   いまは見た目だけ（ワープ・Udon・音は外す）。コース番号は「コース 2–1」〜「2–6」（チュートリアルなし）
public static class FuwaStage2Builder
{
    public const float Drop = 170f;                 // ステージ1のロビーからどれだけ下か(m)
    const string PrefabPath = "Assets/FuwaStage2/Prefabs/FuwaStage2.prefab";
    // ロビーで複製する物（名前）。ワープの台・看板（Slot0〜5）、床・柵・島、真ん中、銃の台の見た目、練習のキノコの見た目
    static readonly string[] LobbyParts = { "LobbyFloor", "LobbyRing", "LobbySpawn", "BallPark", "Slot0", "Slot1", "Slot2", "Slot3", "Slot4", "Slot5",
        "GunStand_Lobby", "GunStand_Lobby_Marker", "PracticeMushroom", "Fence", "FenceCollider", "IslandUnder", "CenterRim", "CenterGround" };
    // 動きもそのまま使う物（銃の台：クリックで自分の銃が戻ってくる）。中の参照はステージ2側の物につなぎ直す
    static readonly string[] KeepBehaviour = { "GunStand_Lobby" };
    // 仮のコース名（テーマ）。決まったら差し替える
    // 2026-10-19 てじゃ「ほのおのやま こおりのほら はぐるまのとう そらのいせき きのこのくに でいこう、かわるかもだけど」
    public static readonly string[] CourseJa = { "みずのこみち", "ほのおのやま", "こおりのほら", "はぐるまのとう", "そらのいせき", "きのこのくに" };
    public static readonly string[] CourseEn = { "Water Path", "Flame Mountain", "Ice Grotto", "Gear Tower", "Sky Ruins", "Mushroom Kingdom" };

    [MenuItem("FuwaCourse/ステージ2/土台を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var lobby1 = Find("FuwaLobby"); if (!lobby1) return "FuwaLobby がない";
        var old = Find("FuwaStage2"); if (old) Object.DestroyImmediate(old);
        var root = new GameObject("FuwaStage2").transform;
        root.position = new Vector3(0f, -Drop, 0f);
        var lobby = new GameObject("Lobby").transform; lobby.SetParent(root, false);
        lobby.position = lobby1.transform.position + Vector3.down * Drop; lobby.rotation = lobby1.transform.rotation;
        int n = 0;
        foreach (var name in LobbyParts)
        {
            var src = lobby1.transform.Find(name); if (!src) { Debug.LogWarning("[ステージ2] " + name + " がない"); continue; }
            var c = Object.Instantiate(src.gameObject, lobby, false); c.name = name;
            c.transform.localPosition = src.localPosition; c.transform.localRotation = src.localRotation; c.transform.localScale = src.localScale;
            if (!KeepBehaviour.Contains(name)) StripBehaviour(c);
            n++;
        }
        // 芝の色：ステージ1より少し青寄り（2026-10-17 てじゃ「芝の色変えよう、ちょっと青系寄りに」）
        var floor = lobby.Find("LobbyFloor");
        if (floor) { var r = floor.GetComponent<Renderer>(); r.sharedMaterial = Stage2Ground(r.sharedMaterial); }
        // 銃の台：置く位置と目印をステージ2側に
        var stand = lobby.Find("GunStand_Lobby");
        if (stand)
        {
            var m = stand.GetComponents<MonoBehaviour>().FirstOrDefault(x => x.GetType().Name == "FuwaGunStand");
            if (m)
            {
                var so = new SerializedObject(m);
                so.FindProperty("gunSpot").objectReferenceValue = stand.Find("GunSpot");
                var mk = lobby.Find("GunStand_Lobby_Marker"); if (mk) so.FindProperty("marker").objectReferenceValue = mk.gameObject;
                so.ApplyModifiedProperties();
                UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)m);
            }
        }
        var langTexts = new List<(TMPro.TMP_Text t, string ja, string en)>();
        // 看板：コース 2–1〜2–6（Slot0 はステージ1ではチュートリアルの位置。ステージ2はチュートリアルなしなので 2–1 から6つ）
        for (int i = 0; i < 6; i++)
        {
            var lab = lobby.Find("Slot" + i + "/Label"); if (!lab) continue;
            var t = lab.GetComponent<TMPro.TMP_Text>(); if (!t) continue;
            string ja = "<voffset=0.12em><size=60%>コース</size> 2–" + (i + 1) + "</voffset>\n<size=51%>" + CourseJa[i] + "</size>";
            string en = "<voffset=0.12em><size=60%>Course</size> 2–" + (i + 1) + "</voffset>\n<size=51%>" + CourseEn[i] + "</size>";
            t.text = ja; langTexts.Add((t, ja, en));
        }
        var log = new List<string>();
        // ---- 行き来のワープ（2026-10-17）----
        // 見本は入口の「ひとりで」の門（SoloGate）：乗って1.5秒待つと暗転してワープ、手動リスポーンの戻り先（home）もそこに
        var gateTrig = Find("FuwaHub/SoloGate/SoloGate_Trigger"); var gateGauge = Find("FuwaHub/SoloGate/SoloGate_Trigger_Gauge");
        var portal1 = lobby1.transform.Find("Stage2Portal");
        if (gateTrig && gateGauge && portal1)
        {
            // ステージ2のロビーに「ステージ1へ」の門（ステージ1のロビーの「つぎのしま」の門と同じ場所・同じ見た目）
            var portal2 = Object.Instantiate(portal1.gameObject, lobby, false).transform; portal2.name = "Stage1Portal";
            portal2.localPosition = portal1.localPosition; portal2.localRotation = portal1.localRotation; portal2.localScale = portal1.localScale;
            foreach (Transform c in portal2.Cast<Transform>().ToList()) if (c.name.EndsWith("_Trigger") || c.name.EndsWith("_Trigger_Gauge")) Object.DestroyImmediate(c.gameObject);
            StripBehaviour(portal2.gameObject); portal2.gameObject.SetActive(true);
            var lab2 = portal2.Find("Label")?.GetComponent<TMPro.TMP_Text>(); if (lab2) { lab2.text = "<size=90%>ステージ1へ</size>"; langTexts.Add((lab2, "<size=90%>ステージ1へ</size>", "<size=78%>To Stage 1</size>")); }   // 2026-10-23 てじゃ「ステージ２へ、とステージ１へでいいかも」（前は まえのしま）。雲からはみ出さないよう文字は少し小さく（英語は78%）
            var lab1 = portal1.Find("Label")?.GetComponent<TMPro.TMP_Text>(); if (lab1) { lab1.text = "<size=90%>ステージ2へ</size>"; langTexts.Add((lab1, "<size=90%>ステージ2へ</size>", "<size=78%>To Stage 2</size>")); }   // 前は つぎのしま
            // ステージ1 → 2（ステージ1のロビーの門。シーンの物なので作り直しのたびに付け直す）
            foreach (Transform c in portal1.Cast<Transform>().ToList()) if (c.name == "Stage2Portal_Trigger" || c.name == "Stage2Portal_Trigger_Gauge") Object.DestroyImmediate(c.gameObject);
            var gun2 = lobby.Find("GunStand_Lobby/GunSpot");
            var to2 = MakeWarp(portal1, "Stage2Portal", gateTrig, gateGauge, lobby.Find("BallPark"), lobby.Find("LobbySpawn"), gun2, 2);
            portal1.gameObject.SetActive(true);
            // ステージ2 → 1
            MakeWarp(portal2, "Stage1Portal", gateTrig, gateGauge, lobby1.transform.Find("BallPark"), lobby1.transform.Find("LobbySpawn"), lobby1.transform.Find("GunStand_Lobby/GunSpot"), 1);
            log.Add("ワープ：ステージ1⇄2");
            // ロビーから落ちたらロビーへ（ステージ1の FallReturn と同じ。戻し先はステージ2へのワープ）
            var fr1 = lobby1.transform.Find("FallReturn");
            if (fr1 && to2)
            {
                var fr2 = Object.Instantiate(fr1.gameObject, lobby, false); fr2.name = "FallReturn";
                fr2.transform.localPosition = fr1.localPosition; fr2.transform.localRotation = fr1.localRotation; fr2.transform.localScale = fr1.localScale;
                SetProxy(fr2, "FuwaLobbyFall", "returnWarp", to2);
            }
            // 入口・あそびばから「ひとりで」へ入るワープはステージ1へ
            foreach (var p in new[] { "FuwaHub/SoloGate/SoloGate_Trigger", "FuwaPlayground/ToSolo/ToSolo_Trigger" })
            { var g = Find(p); if (g) SetProxyInt(g, "FuwaCourseWarp", "stage", 1); }
        }
        else log.Add("（ワープの見本 SoloGate / Stage2Portal が見つからない）");
        // ステージ2の下の大きい落下キャッチ（ステージ1の WorldFallCatcher と同じ物を、ステージ2のロビーの18m下に）
        var wfc = Find("FuwaSystem/WorldFallCatcher");
        if (wfc)
        {
            var c = Object.Instantiate(wfc, root, false); c.name = "Stage2FallCatcher";
            c.transform.position = wfc.transform.position + Vector3.down * Drop; c.transform.rotation = wfc.transform.rotation; c.transform.localScale = wfc.transform.localScale;
        }
        // 表示の切り替え：ひとりでモードの間、ステージ2にいる時だけ出す
        var vis = Find("FuwaSystem/AreaVisibility");
        if (vis)
        {
            var ub = vis.GetComponents<MonoBehaviour>().First(m => m.GetType().Name == "FuwaAreaVisibility");
            var so = new SerializedObject(ub); var arr = so.FindProperty("stage2Objects"); arr.arraySize = 1; arr.GetArrayElementAtIndex(0).objectReferenceValue = root.gameObject; so.ApplyModifiedProperties();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)ub);
        }
        // 日本語・英語の切り替え（FuwaLanguage）に看板を登録。前に作った分（消えて空になった所）は片付ける。銃の台も
        var langGo = Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(x => x && x.gameObject.scene.IsValid() && x.GetType().Name == "FuwaLanguage");
        if (langGo)
        {
            var so = new SerializedObject(langGo);
            var texts = so.FindProperty("texts"); var ja = so.FindProperty("ja"); var en = so.FindProperty("en");
            // いまの並びを読んで、消えた物・今回登録する物を除いてから書き直す（3つの配列の並びをそろえたまま）
            var keep = new List<(Object t, string ja, string en)>();
            for (int i = 0; i < texts.arraySize; i++)
            {
                var o = texts.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o == null || langTexts.Any(x => x.t == o)) continue;
                keep.Add((o, i < ja.arraySize ? ja.GetArrayElementAtIndex(i).stringValue : "", i < en.arraySize ? en.GetArrayElementAtIndex(i).stringValue : ""));
            }
            foreach (var x in langTexts) keep.Add((x.t, x.ja, x.en));
            texts.arraySize = ja.arraySize = en.arraySize = keep.Count;
            for (int i = 0; i < keep.Count; i++)
            {
                texts.GetArrayElementAtIndex(i).objectReferenceValue = keep[i].t; ja.GetArrayElementAtIndex(i).stringValue = keep[i].ja; en.GetArrayElementAtIndex(i).stringValue = keep[i].en;
            }
            var stands = so.FindProperty("stands");
            var st = new List<Object>();
            for (int i = 0; i < stands.arraySize; i++) { var o = stands.GetArrayElementAtIndex(i).objectReferenceValue; if (o != null) st.Add(o); }
            var gs = stand ? stand.GetComponents<MonoBehaviour>().FirstOrDefault(x => x.GetType().Name == "FuwaGunStand") : null;
            if (gs && !st.Contains(gs)) st.Add(gs);
            stands.arraySize = st.Count; for (int i = 0; i < st.Count; i++) stands.GetArrayElementAtIndex(i).objectReferenceValue = st[i];
            so.ApplyModifiedProperties();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)langGo);
            log.Add("言語の看板 " + langTexts.Count + " 個");
        }
        // VRChat の「この高さより下は即リスポーン」をステージ2より下へ（ステージ1は WorldFallCatcher が先に拾うので影響なし）
        foreach (var d in Resources.FindObjectsOfTypeAll<MonoBehaviour>().Where(m => m && m.gameObject.scene.IsValid() && m.GetType().Name == "VRCSceneDescriptor"))
        {
            var so = new SerializedObject(d); var p = so.FindProperty("RespawnHeightY");
            if (p != null && p.floatValue > -Drop - 200f) { p.floatValue = -Drop - 230f; so.ApplyModifiedProperties(); log.Add("RespawnHeightY → " + p.floatValue); }
        }

        // コースごとの色（ロビーの道・看板の後ろの雲）
        log.Add(FuwaStage2LobbyCourses.Run());
        // プレハブにしてシーンにはそれを置く（シーンの物への参照は、置いた物の上書きとして残る）
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, PrefabPath, InteractionMode.AutomatedAction);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "[ステージ2] 土台：ロビーの部品 " + n + " 個、" + string.Join("、", log) + "、プレハブ " + PrefabPath + (prefab ? "" : "（保存失敗）") +
               "。Udon を足したので「ネットワークIDを設定し直す」を2回";
    }

    public static Color GrassColor = new Color(0.50f, 0.79f, 0.70f), GrassTintA = new Color(0.97f, 1.07f, 1.05f), GrassTintB = new Color(0.64f, 0.82f, 0.86f);
    const string GroundMatPath = "Assets/FuwaStage2/Materials/Stage2_LobbyGround.mat";
    static Material Stage2Ground(Material src)
    {
        if (!AssetDatabase.IsValidFolder("Assets/FuwaStage2/Materials")) AssetDatabase.CreateFolder("Assets/FuwaStage2", "Materials");
        var m = AssetDatabase.LoadAssetAtPath<Material>(GroundMatPath);
        if (!m) { m = new Material(src); AssetDatabase.CreateAsset(m, GroundMatPath); }
        else m.CopyPropertiesFromMaterial(src);
        m.SetColor("_Color", GrassColor); m.SetColor("_GroundTintA", GrassTintA); m.SetColor("_GroundTintB", GrassTintB);
        EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
        return m;
    }

    static GameObject Find(string path)
    {
        var parts = path.Split('/');
        var r = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == parts[0]);
        if (!r) return null;
        var t = r.transform;
        for (int i = 1; i < parts.Length; i++) { t = t.Find(parts[i]); if (!t) return null; }
        return t.gameObject;
    }

    // 門（portal）にワープの判定とゲージを付ける。見本（SoloGate）の FuwaCourseWarp を複製して行き先だけ変える
    static Component MakeWarp(Transform portal, string prefix, GameObject trigSrc, GameObject gaugeSrc, Transform start, Transform spot, Transform gun, int stage)
    {
        var trig = Object.Instantiate(trigSrc, portal, false); trig.name = prefix + "_Trigger";
        trig.transform.localPosition = trigSrc.transform.localPosition; trig.transform.localRotation = trigSrc.transform.localRotation; trig.transform.localScale = trigSrc.transform.localScale;
        var gauge = Object.Instantiate(gaugeSrc, portal, false); gauge.name = prefix + "_Trigger_Gauge";
        gauge.transform.localPosition = gaugeSrc.transform.localPosition; gauge.transform.localRotation = gaugeSrc.transform.localRotation; gauge.transform.localScale = gaugeSrc.transform.localScale;
        var ring = portal.Cast<Transform>().FirstOrDefault(t => t.name.EndsWith("_ProgressRing"));
        var w = trig.GetComponents<MonoBehaviour>().First(m => m.GetType().Name == "FuwaCourseWarp");
        var so = new SerializedObject(w);
        so.FindProperty("courseStart").objectReferenceValue = start;
        so.FindProperty("playerSpot").objectReferenceValue = spot;
        so.FindProperty("gunSpot").objectReferenceValue = gun;
        so.FindProperty("homeAction").intValue = 1;
        so.FindProperty("area").intValue = 1;
        so.FindProperty("stage").intValue = stage;
        so.FindProperty("timed").boolValue = false;
        so.FindProperty("progressRing").objectReferenceValue = ring ? ring.GetComponent<Renderer>() : null;
        so.FindProperty("progressVeil").objectReferenceValue = ring && ring.Find("WarpVeil") ? ring.Find("WarpVeil").GetComponent<Renderer>() : null;
        so.FindProperty("gauge").objectReferenceValue = gauge.GetComponent<Renderer>();
        so.FindProperty("chargeSound").objectReferenceValue = trig.GetComponent<AudioSource>();
        so.ApplyModifiedProperties();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)w);
        return w;
    }

    static void SetProxy(GameObject g, string type, string field, Object value)
    {
        var m = g.GetComponents<MonoBehaviour>().FirstOrDefault(x => x.GetType().Name == type); if (!m) return;
        var so = new SerializedObject(m); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedProperties();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)m);
    }
    static void SetProxyInt(GameObject g, string type, string field, int value)
    {
        var m = g.GetComponents<MonoBehaviour>().FirstOrDefault(x => x.GetType().Name == type); if (!m) return;
        Undo.RecordObject(m, "stage");
        var so = new SerializedObject(m); so.FindProperty(field).intValue = value; so.ApplyModifiedProperties();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)m);
    }

    // 複製した物から、動き（Udon）・音・ワープの当たり判定を外して見た目だけにする
    static void StripBehaviour(GameObject g)
    {
        foreach (var ub in g.GetComponentsInChildren<VRC.Udon.UdonBehaviour>(true).ToList())
        {
            var proxy = UdonSharpEditor.UdonSharpEditorUtility.GetProxyBehaviour(ub);
            if (proxy) Object.DestroyImmediate(proxy);
            Object.DestroyImmediate(ub);
        }
        // VRCSpatialAudioSource が AudioSource に依存しているので先に
        foreach (var c in g.GetComponentsInChildren<Component>(true).ToList())
            if (c && c.GetType().Name == "VRCSpatialAudioSource") Object.DestroyImmediate(c);
        foreach (var c in g.GetComponentsInChildren<AudioSource>(true).ToList()) Object.DestroyImmediate(c);
        // ワープのトリガー（乗る判定）は外す
        foreach (var col in g.GetComponentsInChildren<Collider>(true).ToList())
            if (col.isTrigger) Object.DestroyImmediate(col);
    }
}
