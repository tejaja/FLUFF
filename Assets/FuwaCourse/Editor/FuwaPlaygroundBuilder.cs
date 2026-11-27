using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// あそびば（FuwaPlayground）を作る。2026-10-12 に「みんなで」をチルルームにする方向で作り始め、2026-10-13 に名前を「あそびば（Playground）」にそろえた（てじゃ「Bで統一しよう、いろんな中身も」）。
// 島・キノコ・ふわふわの元は Prefabs/Playground のプレハブ（前は たいせんの部屋 FuwaVersusLobby からコピーしていた。たいせんは 2026-10-13 に削除）。
// 島の真ん中にキノコ1本：撃つたびに今の設定のふわふわが出る（FuwaPlaygroundSpawner、FuwaSharedBall を Pool 個使い回す）。
// キノコの左にスイッチ4つ（サイズ／カラー／上限数／地面に落ちたら消えるか）。設定はみんなで共通。
// スポーン・エアガンの台・ワープ・遊び方の看板は FuwaPlayground の直下にあるものをそのまま使う。何度実行しても同じ結果
public static class FuwaPlaygroundBuilder
{
    const string RootName = "FuwaPlayground";
    static readonly Vector3 Center = new Vector3(-72f, 88f, 80f);   // 島の真ん中（前は たいせんの部屋の位置 + (0,0,65)）
    const string PrefabDir = "Assets/FuwaCourse/Prefabs/Playground/";
    // 元のプレハブを置く位置（島の真ん中から。たいせんの部屋にあった時の位置関係のまま）
    static readonly (string name, Vector3 rel)[] IslandParts = { ("Floor", new Vector3(0f, 0.05f, 0f)), ("Ring", new Vector3(0f, 0.05f, 0f)), ("IslandUnder", new Vector3(0f, 0.03f, 0f)) };
    static readonly Vector3 MushRel = Vector3.zero, FluffRel = new Vector3(0f, 0.72f, 0f);
    static GameObject Spawn(string prefab, Transform parent, Vector3 pos)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + prefab + ".prefab");
        var g = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        g.transform.position = pos;
        return g;
    }
    const int Pool = 10;
    // コース（チャレンジ）のスタートのキノコと同じ大きさ（傘の直径0.8m）。
    // 2026-10-16 てじゃ「あそびばのきのこでかくね」：1.31倍は、コースのキノコの見た目の範囲（Renderer.bounds、実物より広い）を傘の大きさとまちがえて合わせていた。メッシュの大きさで比べると1倍で同じ
    //（前は1.31、その前は1.8、その前は3倍）
    const float MushScale = 1f;
    // 配置（2026-10-12 てじゃ「真ん中にキノコ、右奥にデカソファでモニタを眺めるエリア、左奥にフワフワギミックエリア（未定）」）。スポーンは手前（-Z）
    // 島の大きさ：古いみんなでロビーの島（半径14m）の何倍か（2026-10-13 てじゃ「ちょっと広い」→ 半径10.5m。手前半分がほぼ空だった）
    // 2026-10-13 また半径14m に（てじゃ「おもちゃ、もうちょい余裕もたせよ」→ 案A）。柵沿いの物は柵からの距離（IslandR - 〇）で置く
    const float IslandScale = 1.0f;
    const float IslandR = 14f * IslandScale;   // 島（床）の半径。柵はこの 0.14m 内側
    // 2026-10-13 てじゃの並べ直し：「モニタ一式は入って正面からやや左、きのこは中央、🔫はキノコの右手前。モニタの右横の正面の柵沿いに設定ボタンを丸ごと、
    // おもちゃリセットもその近く、その右横からおもちゃ（最初に扇風機とゴールリング、そこから各コースのギミックをぐるっと柵沿いに）。モニタから左側は空けておく」
    // 角度は島の真ん中から：入口から見て奥（+Z）が0°、右回り（+X 側）がプラス。入口（スポーン）は180°
    static Vector3 OnRing(float deg, float r) { return new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0f, Mathf.Cos(deg * Mathf.Deg2Rad)) * r; }
    static readonly Vector3 MushLocal = Vector3.zero;   // 島の真ん中
    // 2026-11-16 てじゃ「ふわふわ設定をテレビの左に、んで各おもちゃを詰めれば余裕できる」→ 設定一式 16° → -46°（テレビの左）
    const float PanelAngle = -46f;   // 設定ボタン一式（スイッチ4つ＋言語・音量＋人数・チャレンジ・タイトル）：正面奥の柵沿い、モニタの右
    static readonly Vector3 PanelLocal = OnRing(PanelAngle, IslandR - 1.5f);
    const float PanelYaw = PanelAngle;   // 島の真ん中から読める向き
    // 観戦リビング（ラグの中心）：正面からやや左の奥。テレビは柵側、ソファは真ん中側
    const float LivingAngle = -22f;
    const float CamZoneAngle = -8f;   // 2026-11-20 蓄音機を右に置くので少し左へ（前は -3）。11-21 てじゃ「もうちょい左、横の柵寄りに」で -5.5 → -8、柵側へ（CamZoneInset）
    const float CamZoneInset = 0.85f;   // 島のふちから内側(m)。前は 1.1
    // VRChatカメラを入れる球：テレビの右、柵ぎわ（デッキの始まり 9° の手前）
    static readonly Vector3 LivingLocal = OnRing(LivingAngle, IslandR - 4.9f);
    // おもちゃ（柵沿い、右回りに）：もどすボタン → 扇風機 → ゴールリング → コース1〜5のおもちゃ
    // 2026-11-16 てじゃ「ゴールリングを普通のゴールみたいに床に置こう」「ゴールリングの上に扇風機」「リセットボタンはカメラハックの球のよこ」
    //  → リング（床に平ら）と扇風機（その真上）は 22°、もどすボタンはカメラの球（-3°）の横 4°
    const float ResetAngle = 12f, ResetR = 0.75f, FanAngle = 22f, RingAngle = 22f;   // 2026-11-20 てじゃ「リセットは扇風機の後ろの柵あたり」（前は 4°・柵から 2.1m。そこは蓄音機に）   // もどすボタンは扇風機の真下（2026-10-13 てじゃ。前は設定ボードの右で「タイトルにもどる」にかぶって見えた）
    // 遊び方の看板の言語・音量ボタンはスイッチのボードの下（BoardButtons）
    // 島の外の物（FuwaPlayground の直下。前は手で置いていた位置×島の大きさ）：スポーン、ワープ2つ、エアガンの台（2026-10-13 てじゃ「銃の台も真ん中に」→ キノコの右手前、スイッチの反対）
    // スポーンとワープは真ん中からの距離のまま（島を広げた分は入口の後ろの柵沿いがおもちゃの場所になる）
    static readonly Vector3 SpawnLocal = new Vector3(0f, 0f, -6.6f);
    // ワープの輪2つ（ハブへ ReturnToHub・チャレンジへ ToSolo）は見た目なし（2026-10-13 てじゃ「ボタンワープあるしポータルいらんかも」）。
    // 設定ボタンの「タイトルにもどる」「チャレンジ」がこのワープの DoWarp を呼ぶので、ワープの係（FuwaCourseWarp）だけ残して見た目と踏む判定を消す
    static readonly Vector3 ReturnWarpLocal = new Vector3(-0.75f, 0f, -6.6f);
    static readonly Vector3 SoloWarpLocal = new Vector3(0.75f, 0f, -6.6f);
    static readonly Vector3 GunStandLocal = new Vector3(1.4f, 0f, -1.5f);   // キノコの右手前
    const float GunStandYaw = 304.4f;

    // 2026-11-19 てじゃ「あそびばのおもちゃ効果音でなくていいよ、弾き飛ぶ音とかはいいけど」→ いらない：2 重くなるエリアのうなり／3 ぬいぐるみの「ぴょん」「パン」／5 泡の貝の2つ／6 溶岩の「ボフッ」
    //  おもちゃの中の、この音の AudioSource を消す（それを指しているスクリプトの欄は空にする。どのスクリプトも空なら鳴らさない）。作り直しのあとにも呼ぶ
    public static readonly string[] MutedToyClips = { "sfx_gravity_loop", "sfx_enemy_jump", "sfx_burst", "sfx_bubble_pop", "sfx_bubble_wrap", "sfx_fireball_bofu2" };
    [MenuItem("FuwaCourse/あそびば/いらないおもちゃの音を消す")]
    static void MenuMuteToySounds() { Debug.Log(MuteToySounds()); }
    public static string MuteToySounds()
    {
        var toys = Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == "Toys" && t.parent != null && t.parent.name == "Room" && t.gameObject.scene.IsValid());
        if (!toys) return "おもちゃの音：Toys がない";
        var kill = toys.GetComponentsInChildren<AudioSource>(true).Where(a => a.clip && MutedToyClips.Contains(a.clip.name)).ToList();
        if (kill.Count == 0) return "おもちゃの音：消すものなし";
        int refs = 0;
        foreach (var u in toys.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true))
        {
            var so = new SerializedObject(u); var it = so.GetIterator(); bool ch = false;
            while (it.Next(true)) if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue is AudioSource a0 && kill.Contains(a0)) { it.objectReferenceValue = null; ch = true; refs++; }
            if (ch) { so.ApplyModifiedPropertiesWithoutUndo(); UdonSharpEditorUtility.CopyProxyToUdon(u); }
        }
        int n = 0;
        foreach (var a in kill)
        {
            var go = a.gameObject;   // VRCSpatialAudioSource が AudioSource を必須にしているので、先にそちらを消す（でないと AudioSource が消えない）
            bool onlySound = go.transform.childCount == 0 && go.GetComponents<Component>().All(c => c is Transform || c is AudioSource || c is VRC.SDK3.Components.VRCSpatialAudioSource);
            if (onlySound) Object.DestroyImmediate(go);
            else { foreach (var sp in go.GetComponents<VRC.SDK3.Components.VRCSpatialAudioSource>()) Object.DestroyImmediate(sp); Object.DestroyImmediate(a); }
            n++;
        }
        return "おもちゃの音を消した " + n + "（欄 " + refs + "）";
    }

    // 2026-11-19 てじゃ「SEつきでパカっと開いておもちゃが拡大してポヨンと出てくる」：FuwaPlaygroundUnlocks の音（パカッ＝sfx_puff、ポヨン＝sfx_ball_poyon）を Toys の下に置く
    [MenuItem("FuwaCourse/あそびば/おもちゃが出る時の音をつける")]
    static void MenuUnlockSfx() { Debug.Log(UnlockSfx()); }
    public static string UnlockSfx()
    {
        var toys = Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == "Toys" && t.parent != null && t.parent.name == "Room" && t.gameObject.scene.IsValid());
        var ul = toys ? toys.GetComponent<FuwaPlaygroundUnlocks>() : null;
        if (!ul) return "おもちゃの出る音：FuwaPlaygroundUnlocks がない";
        AudioSource Make(string name, string clip, float vol)
        {
            var old = toys.Find(name); if (old) Object.DestroyImmediate(old.gameObject);
            var g = new GameObject(name); g.transform.SetParent(toys, false);
            var a = g.AddComponent<AudioSource>(); a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaCourse/Audio/" + clip + ".wav");
            a.playOnAwake = false; a.spatialBlend = 1f; a.volume = vol; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 2f; a.maxDistance = 25f; a.dopplerLevel = 0f;
            return a;
        }
        ul.openSound = Make("UnlockOpenSfx", "sfx_unlock_open", 0.8f);   // 2026-11-19 てじゃ「開くときはpaka1でいいかな」（AI の paka_01、高い音を削って平均 -21dB）
        ul.popSound = Make("UnlockPopSfx", "sfx_unlock_pop", 0.9f);   // 2026-11-19 てじゃ「一旦５で」（Tools/audio/make_sfx_unlock.py の V05_B1_bright）
        Sync(ul);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(toys.gameObject.scene);
        return "おもちゃの出る音 " + (ul.openSound.clip ? "○" : "×") + (ul.popSound.clip ? "○" : "×");
    }

    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/あそびばを作り直す")]
    public static void Menu() { Debug.Log(Run()); Debug.Log(MuteToySounds()); Debug.Log(UnlockSfx()); }

    public static string Run()
    {
        var log = new List<string>();
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        var coop = roots.First(g => g.name == RootName).transform;
        var sys = roots.First(g => g.name == "FuwaSystem").transform;
        Vector3 ac = Center;
        coop.position = ac;

        // 前のあそびばを片付け
        UnregisterMonitor(coop.Find("Room"));
        foreach (var o in new[] { coop.Find("Room"), sys.Find("PlaygroundSpawner"), sys.Find("PlaygroundFluffs") }) if (o != null) Object.DestroyImmediate(o.gameObject);
        var root = new GameObject("Room").transform; root.SetParent(coop, false); root.position = ac;

        // 島（プレハブ。横だけ島の大きさに縮める）
        foreach (var (n, rel) in IslandParts)
        {
            var g = Spawn(n, root, ac + rel); g.name = n;
            g.transform.localScale = new Vector3(g.transform.localScale.x * IslandScale, g.transform.localScale.y, g.transform.localScale.z * IslandScale);
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }
        var floor = root.Find("Floor");
        float floorY = floor.GetComponent<Renderer>().bounds.max.y;
        int floorLayer = floor.gameObject.layer;
        PlaceFixtures(coop, ac, floorY);

        // キノコ
        var mush = Spawn("Mushroom", root, ac + MushRel + MushLocal); mush.name = "Mushroom";
        mush.transform.localScale = mush.transform.localScale * MushScale;
        // プレハブにはシーンの物への参照が入らないので、キノコの矢印の係（RespawnManager）を入れ直す
        // あそびばでは▼はいらない（2026-10-16）ので、矢印を外して消す
        foreach (var gm in mush.GetComponentsInChildren<FuwaGoalMarker>(true))
        {
            SetU(gm, "manager", Object.FindObjectsOfType<FuwaRespawnManager>(true).FirstOrDefault());
            var arrow = gm.arrow; SetU(gm, "arrow", null); Sync(gm);
            if (arrow != null) Object.DestroyImmediate(arrow.gameObject);
        }

        // ふわふわ（ロビーの共有ふわふわのコピー）を Pool 個。待つ位置はキノコの中
        var fluffSrc = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Fluff.prefab");
        Vector3 home = mush.transform.position + Vector3.up * (FluffRel.y - MushRel.y) * MushScale;
        // 同期するので FuwaSystem の下（あそびばの中だと、隠れている間の同期を取りこぼして入り直した人に一部の玉が見えなかった。2026-10-14）。
        // あそびばにいない間は玉が自分の画面で止まって隠れる（areas / myArea）
        var pool = new GameObject("PlaygroundFluffs").transform; pool.SetParent(sys, false);
        var areaVis = Object.FindObjectsOfType<FuwaAreaVisibility>(true).FirstOrDefault();
        var balls = new List<FuwaSharedBall>();
        for (int i = 0; i < Pool; i++)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(fluffSrc, pool); PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); go.name = "Fluff" + i;
            go.transform.position = home; go.transform.localScale = Vector3.one * 0.3f;
            // 10個が同じ所に重なっているので、最初から動かない物にしておく（でないと始まった瞬間に物理で押し合って散らばり、その位置が待つ位置になる）
            var rb = go.GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true;
            var b = go.GetComponent<FuwaSharedBall>();
            var so = new SerializedObject(b);
            so.FindProperty("variant").boolValue = true;
            so.FindProperty("playground").objectReferenceValue = null;   // 下で係を作ってから入れる
            so.FindProperty("popLayers").intValue = 1 << floorLayer;
            so.FindProperty("fallLimit").floatValue = (home.y - floorY) + 3f;   // 島の外へ落ちたら（床の 3m 下で）キノコへ
            so.FindProperty("arenaCenter").objectReferenceValue = null;
            so.FindProperty("arenaRadius").floatValue = 0f;
            so.FindProperty("shotScale").floatValue = 1f;
            so.FindProperty("areas").objectReferenceValue = areaVis;
            so.FindProperty("myArea").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo(); Sync(b);
            balls.Add(b);
        }

        // 設定の係（同期するので FuwaSystem の下）
        var spGo = new GameObject("PlaygroundSpawner"); spGo.transform.SetParent(sys, false);
        EnsureProgram("FuwaPlaygroundSpawner");
        var sp = UdonSharpUndo.AddComponent<FuwaPlaygroundSpawner>(spGo);
        sp.balls = balls.ToArray();
        foreach (var b in balls) { SetU(b, "playground", sp); Sync(b); }
        sp.language = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        foreach (var sm in mush.GetComponentsInChildren<FuwaStartMushroom>(true)) { SetU(sm, "sharedBall", null); SetU(sm, "ball", null); SetU(sm, "spawner", sp); Sync(sm); }

        // 追っかけカメラ：あそびばにいる人を映す時に追うふわふわ
        foreach (var fc in Object.FindObjectsOfType<FuwaFollowCam>(true))
        {
            var fso = new SerializedObject(fc); var fp = fso.FindProperty("playgroundBalls");
            fp.arraySize = balls.Count; for (int i = 0; i < balls.Count; i++) fp.GetArrayElementAtIndex(i).objectReferenceValue = balls[i];
            fso.FindProperty("areas").objectReferenceValue = Object.FindObjectsOfType<FuwaAreaVisibility>(true).FirstOrDefault();
            fso.ApplyModifiedPropertiesWithoutUndo(); Sync(fc);
        }

        // エアガンで吹けるふわふわ：消えた物を外して、新しい玉を足す
        foreach (var bl in Object.FindObjectsOfType<FuwaBlower>(true))
        {
            var bso = new SerializedObject(bl); var p = bso.FindProperty("sharedBalls");
            var list = new List<Object>();
            for (int i = 0; i < p.arraySize; i++) { var o = p.GetArrayElementAtIndex(i).objectReferenceValue; if (o != null) list.Add(o); }
            foreach (var b in balls) if (!list.Contains(b)) list.Add(b);
            p.arraySize = list.Count; for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            bso.ApplyModifiedPropertiesWithoutUndo(); Sync(bl);
        }

        // スイッチ4つ（2×2）：ロビーと同じレーザーで押す白いボタン（近くに来た時だけ押せる＝FuwaUiProximity がビルド前に自動登録）
        //（2026-10-12 てじゃ「押すもの全部レーザー式に」「ほかのボタンと同じかんじ、距離とかも」）
        var spawn = coop.Find("Spawn");
        var howTo = coop.Find("HowToPlay");
        var panel = new GameObject("Switches").transform; panel.SetParent(root, false);
        Vector3 pc = new Vector3(ac.x, floorY, ac.z) + PanelLocal;
        Vector3 look = spawn != null ? spawn.position : ac + Vector3.back * 9f;
        var rot = Quaternion.Euler(0f, PanelYaw, 0f);
        var labels = PlaceSwitches(panel, pc, rot, sp);
        // 支柱はなし（2026-10-13 てじゃ「柱なくそう」）
        BoardButtons(coop, panel, pc, rot, howTo);
        // カラーの文字の材質：ふつう／紺の縁どりつき（「しろ」の時）
        {
            const string op = "Assets/FuwaCourse/Materials/Playground/Playground_SwitchTextOutline.mat";
            var om = AssetDatabase.LoadAssetAtPath<Material>(op);
            if (om == null) { om = new Material(labels[1].fontSharedMaterial) { name = "Playground_SwitchTextOutline" }; AssetDatabase.CreateAsset(om, op); }
            om.CopyPropertiesFromMaterial(labels[1].fontSharedMaterial);
            om.EnableKeyword("OUTLINE_ON"); om.SetColor("_OutlineColor", SwitchText); om.SetFloat("_OutlineWidth", 0.22f); om.SetFloat("_FaceDilate", 0.12f);
            EditorUtility.SetDirty(om);
            var dup = Object.Instantiate(labels[1].gameObject, labels[1].transform.parent, false); dup.name = "TextOutlined";
            var dt = dup.GetComponent<TMPro.TextMeshProUGUI>(); dt.fontSharedMaterial = om; dup.SetActive(false);
            foreach (Transform ch in dup.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(ch.gameObject);   // TMP の SubMesh はやり直しで作られる
            sp.colorLabelOutlined = dt;
        }
        SwitchLabelLayout(sp, labels, sp.colorLabelOutlined);
        // 見出し「ふわふわせってい」（2026-10-14 てじゃ「上にふわふわせっていってかいとこ」）。スイッチの上、オレンジ（あそびかたの見出しと同じ色）
        {
            var hubLab = RootGo("FuwaHub").transform.Find("SoloGate/Label").GetComponent<TMPro.TextMeshPro>();
            var hg = Object.Instantiate(hubLab.gameObject, panel, false); hg.name = "Heading";
            foreach (Transform ch in hg.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(ch.gameObject);
            var ht = hg.GetComponent<TMPro.TextMeshPro>();
            const string hJa = "ふわふわせってい", hEn = "Fluff Settings";
            ht.text = hJa; ht.alignment = TMPro.TextAlignmentOptions.Center; ht.enableAutoSizing = false; ht.fontSize = 3.2f; ht.enableWordWrapping = false;
            ht.rectTransform.sizeDelta = new Vector2(3f, 0.5f);
            // 字はタイトルの副題「ふわっとデリバリー」と同じ（フォント・白い字に紺のふち・影の材質ごと）（2026-10-14 てじゃ「タイトルと同じ色に」「フォントを副題と同じに」）。
            // 副題のフォントは使う字だけ入った固定のフォントなので、足りない字（せ・て・い）はフォントに足してある
            {
                var subT = RootGo("FuwaHub").transform.Find("TitleBoard/Subtitle").GetComponent<TMPro.TMP_Text>();
                ht.font = subT.font; ht.fontSharedMaterial = subT.fontSharedMaterial; ht.color = subT.color;
            }
            hg.transform.SetPositionAndRotation(pc + rot * new Vector3(0f, BoardSideY[0] + 0.5f, 0f), rot); hg.transform.localScale = Vector3.one;
            var hso = new SerializedObject(Object.FindObjectsOfType<FuwaLanguage>(true).First()); AddLang(hso, "texts", "ja", "en", ht, hJa, hEn); hso.ApplyModifiedPropertiesWithoutUndo();
        }
        UdonSharpEditorUtility.CopyProxyToUdon(sp);
        var sub = UdonSharpEditorUtility.GetBackingUdonBehaviour(sp); if (sub != null) { sub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; EditorUtility.SetDirty(sub); }

        if (howTo != null)
        {
            SetHowToText(howTo.Find("Text").GetComponent<TMPro.TextMeshPro>());
            // 看板の板・文字は消して、ボタンはスイッチのボードのまわりへ（BoardButtons）
        }

        // 観戦モニター（ひとりでのロビーの写真館のモニターと、CAM・人物ボタンをコピー。2026-10-12 てじゃ「こっちでも観戦できる」）
        // テレビは観戦リビングの奥（島のふち側）、ソファの方を向く
        Vector3 L = new Vector3(ac.x, floorY, ac.z) + LivingLocal, dirL = new Vector3(LivingLocal.x, 0f, LivingLocal.z).normalized;
        log.Add(Monitor(root, L + dirL * 2.9f, L - dirL * 1.7f));
        log.Add(Decorate(root, ac, floorY, root.Find("Monitor")));
        log.Add(Fence(root, ac, floorY));
        log.Add(Deck(root, ac, floorY, floorLayer));
        log.Add(Toys(root, ac, floorY + DeckH, sp, howTo, look));   // おもちゃ・扇風機・リングは木のデッキの上

        // 落ちた人の戻し：島の下の大きな板（FuwaLobbyFall）→ スポーンへ（見えないワープ）。
        // 元はハブの「あそびばへ」ゲート（行き先エリア＝あそびば）。前はハブへのワープのコピーで、落ちると表示がハブになってあそびばが消えていた（2026-10-13 修正）
        var backWarp = RootGo("FuwaHub").transform.Find("PlaygroundGate");
        var gunSpot = coop.Find("GunStand_Playground/GunSpot") ?? spawn;
        if (backWarp != null && spawn != null)
        {
            var fallWarp = Object.Instantiate(backWarp.gameObject, root, true); fallWarp.name = "FallWarp";
            HideWarpVisuals(fallWarp);
            var fw = fallWarp.GetComponentInChildren<FuwaCourseWarp>(true); SetU(fw, "playerSpot", spawn); SetU(fw, "gunSpot", gunSpot); Sync(fw);
            var lobbyFall = Object.FindObjectsOfType<FuwaLobbyFall>(true).First(f => f.transform.parent != null && f.transform.parent.name == "FuwaHub");   // 音・暗転はハブの落下戻しと同じ
            var fall = new GameObject("Fall"); fall.transform.SetParent(root, false); fall.transform.position = ac + new Vector3(0f, -12f, 0f);
            var fbx = fall.AddComponent<BoxCollider>(); fbx.isTrigger = true; fbx.size = new Vector3(120f, 4f, 120f);
            EnsureProgram("FuwaLobbyFall");
            var nf = UdonSharpUndo.AddComponent<FuwaLobbyFall>(fall);
            nf.returnWarp = fw; nf.fade = lobbyFall.fade; nf.fallSound = lobbyFall.fallSound; UdonSharpEditorUtility.CopyProxyToUdon(nf);
            var nub = UdonSharpEditorUtility.GetBackingUdonBehaviour(nf); if (nub != null) { nub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(nub); }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(coop.gameObject.scene);
        log.Add("playground at " + ac + " home " + home.ToString("F2") + " floorY " + floorY.ToString("F2") + " balls " + balls.Count);
        log.Add(FuwaSharedBallShadowSetup.Run());   // 新しい玉に影を付け直す
        log.Add(FuwaButtonCorners.Run(coop));   // ボタンの形を端が丸いのにそろえる（2026-10-13 てじゃ）
        return string.Join("\n", log);
    }

    // ---------- おもちゃ（2026-10-12 てじゃ「エアガンとは別の手持ち扇風機、空中固定化」「出しっぱなし」）----------
    // コースのクリアで出てくるおもちゃの場所（島の真ん中から）：1竜巻 2岩 3重力 4ぬいぐるみ 5跳ねるキノコ。
    // 2026-10-13 並べ直し：ゴールリングの右から右回りに柵沿い、入口の真後ろまで。
    // 前は19°おき（2.7m）で、実際の広さ（竜巻の見た目 直径5.1m・重力の球4.4m・跳ねるキノコ2本で横6.4m・いばら＋まもの）が重なっていた（てじゃ「かぶってる」）。
    // 島を半径14m にして、外側が柵の 0.8m 内側に来る距離で、となりと 1.3m 以上あく角度を計算して決めた。まものといばらは横ではなく縦（いばらが柵側）に並べる
    // 2026-10-13 木のデッキを敷いたので、みんなデッキの幅の真ん中の線（10.4m）にそろえる（てじゃ「岩なんで奥気味？」。前は外はしを柵に寄せていて小さい岩だけ奥だった）。となりとは 1.5m あける
    // 2026-11-16 リングと扇風機を手前（22°）へ寄せたので、ステージ1のおもちゃも -38° つめる（入口のうしろ〜左手はステージ2のおもちゃ用）
    static readonly float[] UnlockAngles = { 49f, 76f, 100.5f, 125f, 153.5f };
    static readonly float[] UnlockDist = { IslandR - 3.6f, IslandR - 3.6f, IslandR - 3.6f, IslandR - 3.6f, IslandR - 3.6f };
    static readonly Vector3[] UnlockLocal = UnlockAngles.Select((a, i) => OnRing(a, UnlockDist[i])).ToArray();

    // ワープの見た目（輪・足元・文字・粒）と踏む判定を消す。ワープの係（FuwaCourseWarp）は残す（ボタン・落下から呼ぶ）
    static void HideWarpVisuals(GameObject g)
    {
        foreach (var rr in g.GetComponentsInChildren<Renderer>(true)) rr.enabled = false;
        foreach (var cc in g.GetComponentsInChildren<Collider>(true)) cc.enabled = false;
        foreach (var ps in g.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
        foreach (var t in g.GetComponentsInChildren<TMPro.TMP_Text>(true)) t.gameObject.SetActive(false);
        foreach (var a in g.GetComponentsInChildren<AudioSource>(true)) a.playOnAwake = false;
    }

    // スポーン・ワープ・エアガンの台を島の大きさに合わせて置く（向きは前と同じ関係のまま）
    static void PlaceFixtures(Transform coop, Vector3 ac, float floorY)
    {
        Vector3 Ground(Vector3 local, Transform t) { return new Vector3(ac.x + local.x, t.position.y, ac.z + local.z); }
        var spawn = coop.Find("Spawn");
        if (spawn != null) spawn.position = Ground(SpawnLocal, spawn);
        foreach (var (n, l) in new[] { ("ReturnToHub", ReturnWarpLocal), ("ToSolo", SoloWarpLocal) }) { var w = coop.Find(n); if (w != null) { w.position = Ground(l, w); HideWarpVisuals(w.gameObject); } }
        var stand = coop.Find("GunStand_Playground");
        if (stand != null && spawn != null)
        {
            stand.position = Ground(GunStandLocal, stand);
            stand.rotation = Quaternion.Euler(0f, GunStandYaw, 0f);
            // あそびばの台に▼はいらない（2026-10-16。ロビーの台と同じく目印なし）
            var gs = stand.GetComponent<FuwaGunStand>();
            if (gs != null && gs.marker != null) { SetU(gs, "marker", null); Sync(gs); }
            var marker = coop.Find("GunStand_Playground_Marker");
            if (marker != null) Object.DestroyImmediate(marker.gameObject);
        }
    }

    static string Toys(Transform root, Vector3 ac, float floorY, FuwaPlaygroundSpawner sp, Transform howTo, Vector3 look)
    {
        var toys = new GameObject("Toys").transform; toys.SetParent(root, false);
        Vector3 O = new Vector3(ac.x, floorY, ac.z);
        Vector3 G = O + OnRing(RingAngle, IslandR - 2.9f);   // ゴールリングの真ん中（床）
        Vector3 Fc = O + OnRing(FanAngle, IslandR - 2.1f);   // 扇風機の並びの真ん中
        var toCenter = (O - Fc); toCenter.y = 0f; toCenter.Normalize();
        var side = new Vector3(toCenter.z, 0f, -toCenter.x);
        var fanMeshes = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaCourse/Models/PlaygroundFan.fbx").OfType<Mesh>().ToDictionary(m => m.name);
        var uchiMeshes = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaCourse/Models/PlaygroundUchiwa.fbx").OfType<Mesh>().ToDictionary(m => m.name);
        var fanBodyM = Mat("Playground_FanBody", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(0.97f, 0.97f, 0.98f));   // 白いボディに水色の羽（2026-10-13 てじゃ）
        var fanBladeM = Mat("Playground_FanBlades", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(0.55f, 0.92f, 0.78f));   // ミントグリーン（水色は空にまぎれて見えづらかった。2026-10-13 てじゃ）
        var uFaceM = Mat("Playground_UchiwaFace", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(1f, 0.78f, 0.86f));
        var uRimM = Mat("Playground_UchiwaRim", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(1f, 0.97f, 0.9f));
        var windMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/WindParticle.mat");
        EnsureProgram("FuwaHandFan"); EnsureProgram("FuwaHandFanReset"); EnsureProgram("FuwaPlaygroundRing"); EnsureProgram("FuwaPlaygroundUnlocks");

        GameObject Mesh(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 lp)
        {
            var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); g.transform.SetParent(parent, false); g.transform.localPosition = lp;
            g.GetComponent<MeshFilter>().sharedMesh = mesh; g.GetComponent<MeshRenderer>().sharedMaterials = mats;
            g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            g.layer = 13; return g;
        }
        // 風の筋：かぜのおかの風（G2_CrossLeft/WindParticlesReverse：粒は見せず、ゆらゆらした軌跡の筋だけ）と同じ作りを小さくしたもの
        //（2026-10-13 てじゃ「風の粒子、かぜのおかの風っぽくできないかな」。前は引き伸ばした粒）
        var windSrc = RootGo("FuwaCourse").transform.Find("Gimmicks_Kaze/G2_CrossLeft/WindParticlesReverse").GetComponent<ParticleSystem>();
        ParticleSystem Streaks(Transform parent)
        {
            var g = new GameObject("Streaks", typeof(ParticleSystem)); g.transform.SetParent(parent, false);
            var ps = g.GetComponent<ParticleSystem>();
            EditorUtility.CopySerialized(windSrc, ps);
            EditorUtility.CopySerialized(windSrc.GetComponent<ParticleSystemRenderer>(), g.GetComponent<ParticleSystemRenderer>());
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.startLifetime = 0.9f; main.startSpeed = 0f; main.startSize = 0.07f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 90; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            // 筋が出る範囲は、実際に風が当たる範囲（FuwaHandFan：根元の半径0.35m、1mごとに0.25m広がる、届くのは4.5m）に合わせる（2026-10-13 てじゃ「もうちょい範囲広げよ」）
            var em = ps.emission; em.rateOverTime = 36f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14f; sh.radius = 0.3f; sh.scale = Vector3.one; sh.position = Vector3.zero; sh.rotation = Vector3.zero;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local;
            vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(0f); vel.z = new ParticleSystem.MinMaxCurve(5f);
            var nz = ps.noise; nz.strengthMultiplier = 0.35f; nz.frequency = 1.2f;
            var tr = ps.trails; tr.lifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.25f); tr.minVertexDistance = 0.04f; tr.widthOverTrailMultiplier = 0.07f;   // 筋の太さ（コースの風は0.32m。手持ちなので細く）
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }
        FuwaHandFan Held(string name, Vector3 pos, Quaternion rot, bool uchiwa)
        {
            var go = new GameObject(name); go.layer = 13; go.transform.SetParent(toys, false); go.transform.SetPositionAndRotation(pos, rot);
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var bc = go.AddComponent<BoxCollider>();
            if (uchiwa) { bc.center = new Vector3(0f, 0.3f, 0f); bc.size = new Vector3(0.38f, 0.56f, 0.06f); }
            else { bc.center = new Vector3(0f, 0.28f, -0.03f); bc.size = new Vector3(0.38f, 0.58f, 0.18f); }
            var pk = go.AddComponent<VRC.SDK3.Components.VRCPickup>();
            pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any;
            pk.InteractionText = uchiwa ? "うちわ" : "せんぷうき"; pk.UseText = uchiwa ? "あおぐ" : "オン / オフ";   // 2026-11-27 てじゃ「オンオフ式のは オン/オフに統一」 pk.proximity = 2f;
            go.AddComponent<VRC.SDK3.Components.VRCObjectSync>();
            var fan = UdonSharpUndo.AddComponent<FuwaHandFan>(go);
            var nozzle = new GameObject("Nozzle").transform; nozzle.SetParent(go.transform, false);
            if (uchiwa)
            {
                Mesh(go.transform, "Handle", uchiMeshes["Chill_UchiwaHandle"], new[] { uRimM }, Vector3.zero);
                var paddle = Mesh(go.transform, "Paddle", uchiMeshes["Chill_UchiwaPaddle"], new[] { uFaceM, uRimM }, new Vector3(0f, 0.2f, 0f)).transform;
                nozzle.localPosition = new Vector3(0f, 0.37f, 0.05f);
                fan.gustMode = true; fan.swing = paddle;
                fan.range = 3.5f; fan.radius0 = 0.3f; fan.spread = 0.35f;
            }
            else
            {
                Mesh(go.transform, "Body", fanMeshes["Chill_FanBody"], new[] { fanBodyM }, Vector3.zero);
                // 2026-11-22 軽量化：羽は Blender で格子を 1/4（Un-Subdivide 2回）にした版（7488 → 3120、Generated/Low/Chill_FanBlades_Low、元は Tools/fan_blades_low.json）
                var bladesLow = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Generated/Low/Chill_FanBlades_Low.asset");
                var bl = Mesh(go.transform, "Blades", bladesLow != null ? bladesLow : fanMeshes["Chill_FanBlades"], new[] { fanBodyM, fanBladeM }, new Vector3(0f, 0.36f, 0f)).transform;   // 1つ目は羽の真ん中のキャップ（白。2026-10-13 てじゃ）
                nozzle.localPosition = new Vector3(0f, 0.36f, 0.04f);
                fan.blades = bl;
                // ON/OFF の音（カチッ＋ふぃーん。Tools/audio/make_sfx_gun_fan.py。2026-10-14 てじゃ「扇風機のオンオフに効果音」）
                var snd = nozzle.gameObject.AddComponent<AudioSource>();
                snd.playOnAwake = false; snd.spatialBlend = 1f; snd.volume = 0.6f; snd.rolloffMode = AudioRolloffMode.Linear; snd.minDistance = 1f; snd.maxDistance = 9f; snd.dopplerLevel = 0f;
                fan.switchSound = snd;
                fan.onClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaCourse/Audio/sfx_fan_on.wav");
                fan.offClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaCourse/Audio/sfx_fan_off.wav");
            }
            fan.streaks = Streaks(nozzle);
            fan.nozzle = nozzle; fan.spawner = sp;
            UdonSharpEditorUtility.CopyProxyToUdon(fan);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(fan); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Continuous; EditorUtility.SetDirty(ub); }
            return fan;
        }
        // 扇風機3台・うちわ2本：おもちゃ広場の手前のふちに、空中に並べて浮かべておく（キノコの方を向けて）
        var face = Quaternion.LookRotation(toCenter);
        GameObject[] resetOthers = new GameObject[0];
        var list = new List<FuwaHandFan>();
        for (int i = 0; i < 3; i++) list.Add(Held("Fan" + i, new Vector3(G.x, Fc.y, G.z) + side * (-0.55f + i * 0.55f) + Vector3.up * 1.35f, Quaternion.LookRotation(-toCenter), false));   // 2026-11-16 リングの真上に浮かべる   // 風の出る正面を柵の方へ（真ん中から近づいて持つと、風が向こう向きになる。2026-10-13 てじゃ）
        // うちわはなし（2026-10-12 てじゃ「ちょっと変だからなしに、うちわっぽくするのむずそう」）。FuwaHandFan の gustMode は残してある
        // ゴール風リング：広場の真ん中に浮かべる（穴をキノコの方へ向ける）。持って動かせる
        var ringT = new GameObject("Ring").transform; ringT.gameObject.layer = 13; ringT.SetParent(toys, false);
        // 2026-11-16 てじゃ「ゴールリングを普通のゴールみたいに床に置こう」→ 床に平ら（穴が上）、持てない。くぐる判定はリングの上 0.2〜0.5m
        { var tcR = O - G; tcR.y = 0f; ringT.SetPositionAndRotation(G + Vector3.up * 0.06f, Quaternion.LookRotation(Vector3.up, -tcR.normalized)); }
        { var rb = ringT.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; }
        var gate = ringT.gameObject.AddComponent<BoxCollider>(); gate.isTrigger = true; gate.size = new Vector3(1.6f, 1.6f, 0.35f); gate.center = new Vector3(0f, 0f, 0.35f);
        var rpk = ringT.gameObject.AddComponent<VRC.SDK3.Components.VRCPickup>();
        rpk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; rpk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any; rpk.InteractionText = "リング"; rpk.proximity = 2f; rpk.pickupable = true;   // 2026-11-16 てじゃ「ゴールリングがもてなくなってる」→ 持てるように（離すと床に平ら：FuwaPlaygroundRing.OnDrop）
        ringT.gameObject.AddComponent<VRC.SDK3.Components.VRCObjectSync>();
        // 色はコースのゴールリングと同じ（Goal_Ring をそのまま使う。2026-10-12 てじゃ「本物と同じピンクに」）
        var ringM = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.FindAssets("Goal_Ring t:Material").Select(AssetDatabase.GUIDToAssetPath).First(pp => pp.EndsWith("/Goal_Ring.mat")));
        var ringR = Mesh(ringT, "RingMesh", SaveGenMesh(Torus(0.85f, 0.085f, 48, 14), "Playground_Ring"), new[] { ringM }, Vector3.zero).GetComponent<MeshRenderer>();
        var cr = UdonSharpUndo.AddComponent<FuwaPlaygroundRing>(ringT.gameObject); cr.ring = ringR;
        var fxSrc = RootGo("FuwaCourse5").transform.Find("Goal/GoalFx");
        if (fxSrc != null)
        {
            var fx = Object.Instantiate(fxSrc.gameObject, ringT, false); fx.name = "GoalFx"; fx.transform.localPosition = new Vector3(0f, 0.6f, 0f); fx.transform.localRotation = Quaternion.identity; fx.transform.localScale = Vector3.one * 0.6f;
            cr.fireworks = fx.GetComponentsInChildren<ParticleSystem>(true);
            cr.sfx = fx.GetComponentInChildren<AudioSource>(true);
            foreach (var ps in cr.fireworks) { var mm = ps.main; mm.playOnAwake = false; }
        }
        UdonSharpEditorUtility.CopyProxyToUdon(cr);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(cr); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); } }

        // コースのクリアで出てくるおもちゃの場所（広場の奥に5つ）。中身はコースごとに後で入れる。まだの間は札
        var recs = Object.FindObjectsOfType<FuwaRecords>(true).FirstOrDefault();
        if (recs != null) { SetU(recs, "playground", sp); Sync(recs); }
        var unlockToys = new GameObject[5]; var lockedSigns = new GameObject[5];
        string[] courseJa = { "かぜのおか", "いわのみち", "やみのもり", "まもののす", "きのこのたに" };
        string[] courseEn = { "Windy Hill", "Rocky Road", "Dark Forest", "Monster Den", "Mushroom Valley" };
        for (int i = 0; i < 5; i++)
        {
            // 柵沿いに並べる（重ならないように。2026-10-12 竜巻と岩が近すぎた）
            var sl = new Vector3(ac.x, floorY, ac.z) + UnlockLocal[i];
            var outDir = sl - new Vector3(ac.x, floorY, ac.z); outDir.y = 0f; outDir.Normalize();
            var slot = new GameObject("Unlock" + (i + 1)).transform; slot.SetParent(toys, false); slot.position = sl;
            slot.rotation = Quaternion.LookRotation(-outDir);
            unlockToys[i] = slot.gameObject;
            // 2026-10-17 ステージ制の番号（1–1〜）。ハイフンは遠くで消えるので en ダッシュ
            string ja = UnlockJa(1, i, courseJa[i]);
            string en = UnlockEn(1, i, courseEn[i]);
            var (lroot, lbl) = LockedSign(toys, "Locked" + (i + 1), sl, outDir, CourseAccent[i], ja);
            lockedSigns[i] = lroot;
            AttachUnlockDome(lroot, i, sp);
            var lso2 = new SerializedObject(Object.FindObjectsOfType<FuwaLanguage>(true).First()); AddLang(lso2, "texts", "ja", "en", lbl, ja, en); lso2.ApplyModifiedPropertiesWithoutUndo();
        }
        // 1 かぜのおか → 竜巻のたね
        FillTornado(unlockToys[0].transform, sp);
        // 2 いわのみち → ころころ岩（地面の輪をずっと回る）
        FillBoulder(unlockToys[1].transform, sp, G);
        // 3 やみのもり → 重力のたね（まわりのふわふわを引き寄せる）
        FillGravity(unlockToys[2].transform, sp);
        // 4 まもののす → まもののぬいぐるみ（ふわふわが当たるとぽよんと弾む）
        FillPlush(unlockToys[3].transform, sp);
        // 5 きのこのたに → 跳ねるキノコ（コース5 の傘の足場のコピー。のったふわふわを高く跳ね上げる）
        FillBounce(unlockToys[4].transform);
        // 2026-11-16 ステージ2のおもちゃ（6〜10番）
        var (t2, l2) = Stage2Slots(toys, O, sp);
        MakeStage2ToysMovable(toys);
        unlockToys = unlockToys.Concat(t2).ToArray(); lockedSigns = lockedSigns.Concat(l2).ToArray();

        // 「おもちゃを戻す」：扇風機・うちわ以外の持てるおもちゃも
        var othersList = new List<GameObject> { ringT.gameObject };
        foreach (var u in unlockToys) foreach (var os in u.GetComponentsInChildren<VRC.SDK3.Components.VRCObjectSync>(true)) othersList.Add(os.gameObject);
        foreach (var u in unlockToys) foreach (var pp in u.GetComponentsInChildren<FuwaPlaygroundPlush>(true)) if (!othersList.Contains(pp.gameObject)) othersList.Add(pp.gameObject);   // ぬいぐるみは ObjectSync なし
        resetOthers = othersList.ToArray();

        var ul = UdonSharpUndo.AddComponent<FuwaPlaygroundUnlocks>(toys.gameObject);
        ul.spawner = sp; ul.toys = unlockToys; ul.locked = lockedSigns;
        UdonSharpEditorUtility.CopyProxyToUdon(ul);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(ul); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); } }

        // 戻すボタン（並びの下）：レーザーで押すボタン
        var rsGo = new GameObject("ResetToys"); rsGo.transform.SetParent(toys, false);
        var rs = UdonSharpUndo.AddComponent<FuwaHandFanReset>(rsGo); rs.fans = list.ToArray(); rs.others = resetOthers; UdonSharpEditorUtility.CopyProxyToUdon(rs);
        var rub = UdonSharpEditorUtility.GetBackingUdonBehaviour(rs); if (rub != null) { rub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(rub); }
        var rsPos = O + OnRing(ResetAngle, IslandR - ResetR); var rsOut = rsPos - O; rsOut.y = 0f;
        var btnLbl = LaserButton(toys, "ResetToysButton", rsPos + Vector3.up * 0.55f, Quaternion.LookRotation(rsOut.normalized), 1.3f, "おもちゃをもどす", rs, "ResetAll");   // 「戻」はボタンのフォントに無くて□になっていた（2026-10-13）
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        if (lang != null) { var lso = new SerializedObject(lang); AddLang(lso, "uiTexts", "uiJa", "uiEn", btnLbl, "おもちゃをもどす", "Return toys"); lso.ApplyModifiedPropertiesWithoutUndo(); Sync(lang); }
        // 2026-11-19 てじゃ「おもちゃ全部出すボタンもういらんか」：テスト用の「おもちゃぜんぶだす」は置かない（置きたい時はメニューから）
        return "toys " + list.Count;
    }

    // ---------- まだ出ていないおもちゃの場所：「？」の台座＋ぷっくり看板 ----------
    // 2026-10-13 てじゃ「出現前の看板きえてた、そのままは味気ない」→ 案A（入口と同じぷっくりクッション看板・コースの色のふち）＋B（半透明のドームの台座に「?」）。
    // 前は遊び方の看板の板と文字をコピーしていて、遊び方の看板を隠したらコピーも隠れたまま作られていた
    // ロビーのコース看板の雲のふちと同じ色。きのこのたには 2026-10-25 空色 73AAE6 → うすい赤 E8706A（いちど E0524C も試して「いっこまえのやつで」。てじゃ「みずのこみちと青でかぶってるうえに連続」
    // 「どうせ被るなら、連続ではない赤のほうがマシかも」。オレンジはいわのみちとかぶってやめた。2026-11-04 ほのおのやま（E2625B）とかぶるので F0904A（オレンジ寄り）に。道もうすいオレンジ、ロビーまん中の丸のふちはオレンジ→赤っぽい F7A597・中はうすいピンク FDE9E4）
    static readonly Color[] CourseAccent = { Hex("7DD8AD"), Hex("D0A35B"), Hex("77435F"), Hex("8C63D5"), Hex("F0904A") };
    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    const float LockHW = 0.72f, LockHH = 0.3f, LockRound = 0.28f, LockPipe = 0.035f, LockY = 1.3f, DomeR = 0.68f;

    // 半径 0.5 の半球（y≧0）。外と内の両面（開いた時に内側が見える）。Unity の球と同じ大きさなので、今のドームの大きさのまま差しかえられる
    public static Mesh DomeHalfMesh()
    {
        const int seg = 40, rings = 12;
        var vs = new List<Vector3>(); var ns = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        for (int side = 0; side < 2; side++)
        {
            int b = vs.Count;
            for (int r = 0; r <= rings; r++)
            {
                float th = r / (float)rings * Mathf.PI * 0.5f;   // 0＝てっぺん
                for (int k = 0; k <= seg; k++)
                {
                    float ph = k / (float)seg * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    vs.Add(n * 0.5f); ns.Add(side == 0 ? n : -n); uv.Add(new Vector2(k / (float)seg, 1f - r / (float)rings));
                }
            }
            for (int r = 0; r < rings; r++)
                for (int k = 0; k < seg; k++)
                {
                    int a0 = b + r * (seg + 1) + k, a1 = a0 + 1, c0 = a0 + seg + 1, c1 = c0 + 1;
                    if (side == 0) { tri.Add(a0); tri.Add(a1); tri.Add(c0); tri.Add(a1); tri.Add(c1); tri.Add(c0); }
                    else { tri.Add(a0); tri.Add(c0); tri.Add(a1); tri.Add(a1); tri.Add(c0); tri.Add(c1); }
                }
        }
        var m = new Mesh(); m.SetVertices(vs); m.SetNormals(ns); m.SetUVs(0, uv); m.SetTriangles(tri, 0); m.RecalculateBounds();
        return SaveGenMesh(m, "Playground_DomeHalf");
    }
    [MenuItem("FuwaCourse/あそびば/ドームを半球にする（奥へ開くアニメ用）")]
    static void MenuDomeHalf()
    {
        var mesh = DomeHalfMesh(); int n = 0;
        foreach (var un in Object.FindObjectsOfType<FuwaPlaygroundUnlocks>(true))
            foreach (var l in un.locked) { var d = l ? l.transform.Find("Dome") : null; if (d) { d.GetComponent<MeshFilter>().sharedMesh = mesh; n++; } }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[あそびば] ドームを半球に " + n);
    }

    static (GameObject, TMPro.TextMeshPro) LockedSign(Transform toys, string name, Vector3 floor, Vector3 outDir, Color accent, string ja)
    {
        var root = new GameObject(name).transform; root.SetParent(toys, false);
        root.SetPositionAndRotation(floor, Quaternion.LookRotation(outDir));   // 表（-Z）が島の真ん中の方
        var hubLab = RootGo("FuwaHub").transform.Find("SoloGate/Label").GetComponent<TMPro.TextMeshPro>();
        string key = name;
        // 台座：半透明のドーム（下半分は床に埋める）＋中に「?」
        var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere); dome.name = "Dome"; Object.DestroyImmediate(dome.GetComponent<Collider>());
        dome.transform.SetParent(root, false); dome.transform.localPosition = Vector3.zero; dome.transform.localScale = new Vector3(DomeR * 2f, DomeR * 1.5f, DomeR * 2f);
        dome.GetComponent<MeshFilter>().sharedMesh = DomeHalfMesh();   // 2026-11-19 奥へパカッと開くので、床にうめた玉ではなく本当の半球（内側も見える）
        var dm = dome.GetComponent<MeshRenderer>(); dm.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Generated/MonitorCamZone.mat") ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/ItemBubble.mat");
        dm.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var ringMesh = SaveGenMesh(Torus(DomeR, 0.05f, 40, 10), "Playground_LockRing");
        var baseRing = new GameObject("Rim", typeof(MeshFilter), typeof(MeshRenderer)); baseRing.transform.SetParent(root, false);
        baseRing.transform.localPosition = new Vector3(0f, 0.02f, 0f); baseRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        baseRing.GetComponent<MeshFilter>().sharedMesh = ringMesh; baseRing.GetComponent<MeshRenderer>().sharedMaterial = Mat("Playground_Lock_" + ColorUtility.ToHtmlStringRGB(accent), "Assets/FuwaCourse/Materials/Goal_Ground.mat", accent);
        var q = Object.Instantiate(hubLab.gameObject, root, false); q.name = "Mark";
        foreach (Transform ch in q.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(ch.gameObject);
        var qt = q.GetComponent<TMPro.TextMeshPro>(); qt.text = "?"; qt.enableAutoSizing = false; qt.fontSize = 4f; qt.alignment = TMPro.TextAlignmentOptions.Center;
        qt.color = Color.Lerp(accent, Color.black, 0.1f); qt.rectTransform.sizeDelta = new Vector2(2f, 2f);
        q.transform.localRotation = Quaternion.identity; q.transform.localScale = Vector3.one;
        // 文字の実際の下はしを床から 0.08m 上に（2026-10-13 てじゃ「？が地面にめりこんでる」→ 小さくして測って置く）
        qt.ForceMeshUpdate(); q.transform.localPosition = new Vector3(0f, 0.08f - qt.bounds.min.y, 0f);   // bounds＝実際の字の形（textBounds は行の余白込み）。てっぺんはドーム（高さ0.51）の中
        // 看板：白いクッション＋コースの色のふち（入口の看板と同じ作り）。ドームの上に浮かせる
        var sign = new GameObject("Sign").transform; sign.SetParent(root, false); sign.localPosition = new Vector3(0f, LockY, 0f); sign.localRotation = Quaternion.identity;
        var body = FuwaSignStyle.Outline(LockHW, LockHH, LockRound, 12, 10f);   // 2026-11-22 軽量化：まっすぐな辺の途中の点（2cmおき）はふくらみの形に効かないのでなし（7000 → 2800 三角形、見た目は同じ）
        var cm = Mat("Playground_LockCushion", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(1f, 0.98f, 0.96f));
        cm.SetFloat("_ShadowStrength", 0.3f); cm.SetColor("_UnderColor", new Color(0.82f, 0.86f, 0.98f)); cm.SetFloat("_UnderStrength", 1f); cm.SetFloat("_UnderGrad", 0.45f); EditorUtility.SetDirty(cm);
        float zc = 0.08f;
        FuwaSignStyle.Part(sign, "Cushion", SaveGenMesh(FuwaSignStyle.Pillow(body, LockHW, LockHH, zc, 0.06f, 0.04f, 0.18f, 12), "Playground_LockCushion"), cm, Vector3.zero);
        FuwaSignStyle.Part(sign, "Piping", SaveGenMesh(FuwaSignStyle.Tube(LockHW, LockHH, LockRound, LockPipe, zc, 12), "Playground_LockPiping"), Mat("Playground_Lock_" + ColorUtility.ToHtmlStringRGB(accent), "Assets/FuwaCourse/Materials/Goal_Ground.mat", accent), Vector3.zero);
        var t = Object.Instantiate(hubLab.gameObject, sign, false); t.name = "Text";
        foreach (Transform ch in t.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(ch.gameObject);
        var tm = t.GetComponent<TMPro.TextMeshPro>(); tm.text = ja; tm.alignment = TMPro.TextAlignmentOptions.Center; tm.lineSpacing = -10f;
        tm.enableAutoSizing = true; tm.fontSizeMax = 3.2f; tm.fontSizeMin = 0.6f; tm.enableWordWrapping = false;
        tm.rectTransform.sizeDelta = new Vector2((LockHW - 0.08f) * 2f, (LockHH - 0.02f) * 2f);
        t.transform.localPosition = new Vector3(0f, 0f, 0f); t.transform.localRotation = Quaternion.identity; t.transform.localScale = Vector3.one;
        // フォントの行の余白で文字の見た目が下に寄る（2026-10-13 てじゃ「文字が下よりじゃない？」）→ 実際の文字の範囲の真ん中を板の真ん中に
        tm.ForceMeshUpdate(); t.transform.localPosition = new Vector3(0f, 0.02f - tm.textBounds.center.y, 0f);   // +0.02 は濁点などで重く見える分
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return (root.gameObject, tm);
    }

    // テスト用：クリア報酬のおもちゃを全部出すボタン（マスターにだけ見える）。2026-10-13 てじゃ「あとで消す」→ 消す時はこの呼び出しと関数を消す
    // ===== 2026-11-16 ステージ2のおもちゃ（てじゃ「あそびばのおもちゃつくろか」）=====
    //  コース2–1〜2–5 のクリアで出る（FuwaRecords の Records2.playgroundOffset=5 → おもちゃ 6〜10 番）。並びは入口の真後ろ（180°）から右回りに、設定一式の手前まで
    //  2–1 泡が出る貝 / 2–2 火炎放射の岩 / 2–3 氷の床 / 2–4 プロペラ＋歯車 / 2–5 ポータル（できたものから中身を入れる）
    static readonly float[] Unlock2Angles = { 180f, 207f, 234f, 261f, 288f };
    static readonly Color[] Course2Accent = { Hex("4A8FE8"), Hex("E2625B"), Hex("B4C2D2"), Hex("5C626E"), Hex("D0794F") };
    static (GameObject[], GameObject[]) Stage2Slots(Transform toys, Vector3 O, FuwaPlaygroundSpawner sp)
    {
        string[] ja2 = { "みずのこみち", "ほのおのやま", "こおりのみち", "はぐるまのしま", "そらのいせき" };
        string[] en2 = { "Water Path", "Flame Mountain", "Ice Road", "Gear Isles", "Sky Ruins" };
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).First();
        var ts = new GameObject[5]; var ls = new GameObject[5];
        for (int i = 0; i < 5; i++)
        {
            foreach (var n in new[] { "Unlock" + (i + 6), "Locked" + (i + 6) }) { var old = toys.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject); }
            var sl = O + OnRing(Unlock2Angles[i], IslandR - 3.6f);
            var outDir = sl - O; outDir.y = 0f; outDir.Normalize();
            var slot = new GameObject("Unlock" + (i + 6)).transform; slot.SetParent(toys, false); slot.position = sl; slot.rotation = Quaternion.LookRotation(-outDir);
            ts[i] = slot.gameObject;
            string ja = UnlockJa(2, i, ja2[i]);
            string en = UnlockEn(2, i, en2[i]);
            var (lroot, lbl) = LockedSign(toys, "Locked" + (i + 6), sl, outDir, Course2Accent[i], ja);
            ls[i] = lroot;
            AttachUnlockDome(lroot, i + 5, sp);
            var lso = new SerializedObject(lang); CleanLang(lso, "texts", "ja", "en"); AddLang(lso, "texts", "ja", "en", lbl, ja, en); lso.ApplyModifiedPropertiesWithoutUndo();
        }
        FillBubbleClam(ts[0].transform);
        FillLavaPool(ts[1].transform);   // 2026-11-16 てじゃ「（火炎放射の岩は）別のにかえるかも」「2-2はマグマ弾が真上に打ち上がる溶岩溜まりとか」
        FillIceToy(ts[2].transform);
        FillWindmill(ts[3].transform);
        FillPortals(ts[4].transform, O);
        // ステージ2の記録 → おもちゃ 6〜10 番
        var recs2 = Object.FindObjectsOfType<FuwaRecords>(true).FirstOrDefault(r => r.courseStarts != null && r.courseStarts.Length > 0 && r.courseStarts[0] != null && r.courseStarts[0].root.name == "FuwaCourse2_1");
        if (recs2 != null) { recs2.playground = sp; recs2.playgroundOffset = 5; Sync(recs2); }
        var su = Object.FindObjectsOfType<FuwaSecretUnlock>(true).FirstOrDefault(); if (su != null) { su.toyCount = 10; Sync(su); }
        Sync(lang);
        return (ts, ls);
    }
    // 2026-11-16 てじゃ「クリアフラグ持ちはおもちゃ予定地のドームをUSEすると解放できる」：看板の文とドームのさわる仕組み
    // てじゃ「クリアするとつかえるよ、がいいな」→ 2026-11-27「クリアすると アンロック」（レーザーの文字も かいほう→アンロック）
    static string UnlockJa(int stage, int i, string name) { return "コース" + stage + "–" + (i + 1) + "「" + name + "」を\nクリアすると アンロック"; }
    static string UnlockEn(int stage, int i, string name) { return "Clear Course " + stage + "–" + (i + 1) + "\n" + name + " to unlock"; }
    const string UnlockInteract = "アンロック";
    // 2026-11-27 てじゃ「看板までUSEできる判定ほしい」：ドームの上の看板（Sign）の大きさの、すり抜ける当たり（トリガー）を同じ物につける（Interact はその物の当たり全部で効く）
    public static void AddSignInteract(GameObject lroot)
    {
        foreach (var old in lroot.GetComponents<BoxCollider>()) Object.DestroyImmediate(old);
        var sign = lroot.transform.Find("Sign"); if (sign == null) return;
        var cu = sign.Find("Cushion"); var mf = cu != null ? cu.GetComponent<MeshFilter>() : null; if (mf == null || mf.sharedMesh == null) return;
        var b = mf.sharedMesh.bounds;
        var bc = lroot.AddComponent<BoxCollider>(); bc.isTrigger = true;
        bc.center = lroot.transform.InverseTransformPoint(cu.TransformPoint(b.center));
        bc.size = Vector3.Scale(b.size, cu.lossyScale) + new Vector3(0.06f, 0.06f, 0.1f);
    }

    // シーンの今の看板（Locked1〜11）だけ直す：文を アンロック に、レーザーの文字、看板の USE 判定（作り直さずに）
    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば：解放の看板を「アンロック」に＋看板にもUSE判定")]
    public static void FixUnlockSigns()
    {
        var toys = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "Toys" && t.parent != null && t.parent.name == "Room");
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).First();
        var lso = new SerializedObject(lang); var pa = lso.FindProperty("texts"); var pj = lso.FindProperty("ja"); var pe = lso.FindProperty("en");
        var hi = lso.FindProperty("hoverInteractJa"); var he = lso.FindProperty("hoverInteractEn");
        int n = 0;
        foreach (Transform lr in toys)
        {
            if (!lr.name.StartsWith("Locked")) continue;
            var tr = lr.Find("Sign/Text"); var lbl = tr != null ? tr.GetComponent<TMPro.TextMeshPro>() : null;
            if (lbl != null) { lbl.text = lbl.text.Replace("クリアすると つかえるよ", "クリアすると アンロック"); EditorUtility.SetDirty(lbl); }
            for (int a = 0; a < pa.arraySize; a++) if (lbl != null && pa.GetArrayElementAtIndex(a).objectReferenceValue == lbl)
            {
                var j = pj.GetArrayElementAtIndex(a); j.stringValue = j.stringValue.Replace("クリアすると つかえるよ", "クリアすると アンロック");
                var e = pe.GetArrayElementAtIndex(a); e.stringValue = e.stringValue.Replace(" to use", " to unlock").Replace("\nto use", "\nto unlock");
            }
            foreach (var ub in lr.GetComponents<VRC.Udon.UdonBehaviour>()) if (ub.interactText == "かいほう") { ub.interactText = UnlockInteract; EditorUtility.SetDirty(ub); }
            AddSignInteract(lr.gameObject); n++;
        }
        for (int a = 0; a < hi.arraySize; a++) if (hi.GetArrayElementAtIndex(a).stringValue == "かいほう") { hi.GetArrayElementAtIndex(a).stringValue = UnlockInteract; he.GetArrayElementAtIndex(a).stringValue = "Unlock"; }
        lso.ApplyModifiedPropertiesWithoutUndo(); Sync(lang);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(toys.gameObject.scene);
        Debug.Log("[あそびば] 解放の看板 " + n + "個：アンロック・USE判定");
    }
    static void AttachUnlockDome(GameObject lroot, int toyIdx, FuwaPlaygroundSpawner sp)
    {
        EnsureProgram("FuwaPlaygroundUnlockDome");
        var recs = Object.FindObjectsOfType<FuwaRecords>(true).ToList();
        string firstRoot = toyIdx < 5 ? "FuwaCourse" : "FuwaCourse2_1";
        var rec = recs.FirstOrDefault(r => r.courseStarts != null && r.courseStarts.Length > 0 && r.courseStarts[0] != null && r.courseStarts[0].root.name == firstRoot);
        foreach (var old in lroot.GetComponents<FuwaPlaygroundUnlockDome>()) Object.DestroyImmediate(old);
        foreach (var old in lroot.GetComponents<SphereCollider>()) Object.DestroyImmediate(old);
        var col = lroot.AddComponent<SphereCollider>(); col.radius = DomeR; col.center = Vector3.zero;
        var ud = UdonSharpUndo.AddComponent<FuwaPlaygroundUnlockDome>(lroot);
        ud.records = rec; ud.courseIndex = toyIdx % 5; ud.spawner = sp; ud.toyIndex = toyIdx; ud.dome = lroot.transform.Find("Dome");
        UdonSharpEditorUtility.CopyProxyToUdon(ud);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(ud); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; ub.interactText = UnlockInteract; EditorUtility.SetDirty(ub); } }
        AddSignInteract(lroot);
    }
    // シーンの今のドーム（Locked1〜10）に、さわる仕組みと新しい看板の文を入れる（作り直さずに）
    [MenuItem("FuwaCourse/あそびば/ドームをさわって解放にする（看板の文も）")]
    static void MenuUnlockDomes()
    {
        var toys = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "Toys" && t.parent != null && t.parent.name == "Room");
        var sp = Object.FindObjectsOfType<FuwaPlaygroundSpawner>(true).First();
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).First();
        string[] ja1 = { "かぜのおか", "いわのみち", "やみのもり", "まもののす", "きのこのたに" }, en1 = { "Windy Hill", "Rocky Road", "Dark Forest", "Monster Den", "Mushroom Valley" };
        string[] ja2 = { "みずのこみち", "ほのおのやま", "こおりのみち", "はぐるまのしま", "そらのいせき" }, en2 = { "Water Path", "Flame Mountain", "Ice Road", "Gear Isles", "Sky Ruins" };
        var lso = new SerializedObject(lang); var pa = lso.FindProperty("texts"); var pj = lso.FindProperty("ja"); var pe = lso.FindProperty("en");
        for (int k = 0; k < 10; k++)
        {
            var lr = toys.Find("Locked" + (k + 1)); if (lr == null) continue;
            AttachUnlockDome(lr.gameObject, k, sp);
            string ja = k < 5 ? UnlockJa(1, k, ja1[k]) : UnlockJa(2, k - 5, ja2[k - 5]);
            string en = k < 5 ? UnlockEn(1, k, en1[k]) : UnlockEn(2, k - 5, en2[k - 5]);
            var tr = lr.Find("Sign/Text"); var lbl = tr != null ? tr.GetComponent<TMPro.TextMeshPro>() : null;
            if (lbl == null) continue;
            lbl.text = ja; EditorUtility.SetDirty(lbl);
            for (int a = 0; a < pa.arraySize; a++) if (pa.GetArrayElementAtIndex(a).objectReferenceValue == lbl) { pj.GetArrayElementAtIndex(a).stringValue = ja; pe.GetArrayElementAtIndex(a).stringValue = en; }
        }
        lso.ApplyModifiedPropertiesWithoutUndo(); Sync(lang);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(toys.gameObject.scene);
        Debug.Log("[あそびば] ドームをさわって解放に（10個）");
    }

    // 2026-11-16 てじゃ「（あそびばのポータル）向こう見えるのはむりかな」「それでいいよ」：2–5 と同じカメラ式（FuwaPortalCams）で、A の穴に B から見た景色、B の穴に A から見た景色
    //  カメラ2台は 2–5 の物の設定を複製（レンダーテクスチャも共用：2–5 とは離れていて同時に動かない）。穴の材質は 2–5 の Ruins_PortalCam_WarpA の複製
    [MenuItem("FuwaCourse/あそびば/ポータルの向こうを映す（カメラ式）")]
    public static void MenuPortalCams()
    {
        var pp = Object.FindObjectsOfType<FuwaPlaygroundPortals>(true).FirstOrDefault();
        var src = Object.FindObjectsOfType<FuwaPortalCams>(true).FirstOrDefault(c => c.transform.root.name == "FuwaCourse2_5");
        if (pp == null || src == null) { Debug.LogWarning("[あそびば] ポータルか 2–5 の PortalCams がない"); return; }
        var old = pp.transform.Find("PortalCams"); if (old) Object.DestroyImmediate(old.gameObject);
        var g = new GameObject("PortalCams"); g.transform.SetParent(pp.transform, false);
        Camera Cam(Camera c) { var n = Object.Instantiate(c.gameObject, g.transform, false); n.name = c.name; var cam = n.GetComponent<Camera>(); cam.enabled = false; return cam; }
        var cl = Cam(src.camL); var cr = Cam(src.camR);
        var srcMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Ruins_PortalCam_WarpA.mat");
        MeshRenderer Hole(Transform p, string n)
        {
            var mr = p.Find("Swirl").GetComponent<MeshRenderer>();
            string mp = "Assets/FuwaCourse/Materials/Playground_PortalCam_" + n + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
            if (!m) { m = new Material(srcMat); AssetDatabase.CreateAsset(m, mp); } else m.CopyPropertiesFromMaterial(srcMat);
            m.SetFloat("_Active", 0f); m.SetTexture("_TexL", cl.targetTexture); m.SetTexture("_TexR", cr.targetTexture); EditorUtility.SetDirty(m);
            mr.sharedMaterial = m; return mr;
        }
        var ha = Hole(pp.a, "A"); var hb = Hole(pp.b, "B");
        var pc = UdonSharpUndo.AddComponent<FuwaPortalCams>(g);
        // 調整の数字だけ 2–5 と同じに（CopySerialized は裏の UdonBehaviour の参照までコピーしてしまうので使わない）
        foreach (var f in typeof(FuwaPortalCams).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            if (f.FieldType == typeof(float) || f.FieldType == typeof(int)) f.SetValue(pc, f.GetValue(src));
        pc.entries = new[] { pp.a, pp.b }; pc.exits = new[] { pp.b, pp.a }; pc.holes = new[] { ha, hb };
        pc.camL = cl; pc.camR = cr; pc.layerCull = new int[0];
        // 出口の裏の板もポータルのカメラから隠す（2026-11-19 てじゃ「覗いた先が裏面の板が見えちゃって真っ黒」）
        Renderer Back(Transform p) { var b = p.Find("Back"); return b ? b.GetComponent<Renderer>() : null; }
        pc.holeBacks = new[] { Back(pp.a), Back(pp.b) };
        UdonSharpEditorUtility.CopyProxyToUdon(pc);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(pc); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pp.gameObject.scene);
        Debug.Log("[あそびば] ポータルのカメラ式");
    }

    // 言語の表から、消えた看板（null）の行を取りのぞく（作り直すたびに増えないように）
    static void CleanLang(SerializedObject lso, string a, string j, string e)
    {
        var pa = lso.FindProperty(a); var pj = lso.FindProperty(j); var pe = lso.FindProperty(e);
        for (int k = pa.arraySize - 1; k >= 0; k--)
        {
            if (pa.GetArrayElementAtIndex(k).objectReferenceValue != null) continue;
            pa.GetArrayElementAtIndex(k).objectReferenceValue = null; pa.DeleteArrayElementAtIndex(k);
            if (k < pj.arraySize) pj.DeleteArrayElementAtIndex(k); if (k < pe.arraySize) pe.DeleteArrayElementAtIndex(k);
        }
    }
    [MenuItem("FuwaCourse/あそびば/ステージ2のおもちゃを作り直す")]
    static void MenuStage2Toys()
    {
        var toys = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "Toys" && t.parent != null && t.parent.name == "Room");
        var sp = Object.FindObjectsOfType<FuwaPlaygroundSpawner>(true).First();
        Vector3 O = toys.Find("Unlock1").position; { var c = toys.parent.parent.position; O = new Vector3(c.x, O.y, c.z); }
        var (t2, l2) = Stage2Slots(toys, O, sp);
        MakeStage2ToysMovable(toys);
        var ul = toys.GetComponent<FuwaPlaygroundUnlocks>();
        ul.toys = ul.toys.Take(5).Concat(t2).ToArray(); ul.locked = ul.locked.Take(5).Concat(l2).ToArray(); Sync(ul);
        // 「おもちゃをもどす」にステージ2の持てる物（ポータルの台など）も
        var rs = toys.Find("ResetToys").GetComponent<FuwaHandFanReset>();
        var others = rs.others.Where(o => o != null).ToList();
        foreach (var u in t2) foreach (var os in u.GetComponentsInChildren<VRC.SDK3.Components.VRCObjectSync>(true)) if (!others.Contains(os.gameObject)) others.Add(os.gameObject);
        rs.others = others.ToArray(); Sync(rs);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(toys.gameObject.scene);
        Debug.Log("[あそびば] ステージ2のおもちゃ 5つの場所");
        Debug.Log(MuteToySounds());
    }
    // 2026-11-16 てじゃ「2-1おもちゃ各種も接地限定で持ち運んで移動したい　デカいから岩みたいに半透明化も」→ B「おもちゃ全体を掴める。オン/オフは持ってトリガー」
    //  貝（Unlock6）・氷（Unlock8）・羽根車（Unlock9）。火の岩（Unlock7）は「別のにかえるかも」なのでそのまま。ポータル（Unlock10）は台座の持ち手
    //  跳ねるキノコと同じ FuwaMovableMushroom：持つ所（Grab：VRCPickup・VRCObjectSync・おもちゃを囲む箱、見た目なし）と、あとを追う Body（中身）を分ける
    static void MakeStage2ToysMovable(Transform toys)
    {
        var heldMat = Object.FindObjectsOfType<FuwaThrowRock>(true).First().heldMaterial;
        EnsureProgram("FuwaMovableMushroom");
        foreach (var (slotName, label) in new[] { ("Unlock6", "かい"), ("Unlock7", "ようがん"), ("Unlock8", "こおり"), ("Unlock9", "はねぐるま") })
        {
            var slot = toys.Find(slotName); if (slot == null) continue;
            var oldGrab = slot.Find("Grab"); var body = slot.Find("Body");
            if (body == null)
            {
                body = new GameObject("Body").transform; body.SetParent(slot, false);
                foreach (Transform c in slot.Cast<Transform>().ToList()) if (c != body && c != oldGrab) c.SetParent(body, true);
            }
            if (oldGrab) Object.DestroyImmediate(oldGrab.gameObject);
            // 原点：一番大きい部品の位置（床の上）。Body も同じ所へ（中身の位置は変えない）
            var main = body.Cast<Transform>().OrderByDescending(c => { var rs = c.GetComponentsInChildren<Renderer>(true); return rs.Length == 0 ? 0f : rs.Max(r => r.bounds.size.sqrMagnitude); }).First();
            Vector3 org = main.position; float yaw = main.eulerAngles.y;
            var kids = body.Cast<Transform>().ToList(); foreach (var k in kids) k.SetParent(slot, true);
            body.SetPositionAndRotation(org, Quaternion.Euler(0f, yaw, 0f)); foreach (var k in kids) k.SetParent(body, true);
            // 囲む箱（Body の座標で）。泡・広がる氷・粒は除く
            var bb = new Bounds(Vector3.zero, Vector3.zero); bool first = true;
            foreach (var r in body.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name.StartsWith("Bubble") || r.name == "IceDisc" || r.name == "BombVisual") continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var cpt = body.InverseTransformPoint(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                    if (first) { bb = new Bounds(cpt, Vector3.zero); first = false; } else bb.Encapsulate(cpt);
                }
            }
            var grab = new GameObject("Grab"); grab.layer = 13; grab.transform.SetParent(slot, false);
            grab.transform.SetPositionAndRotation(body.position, body.rotation);
            var bc = grab.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.center = bb.center; bc.size = bb.size + Vector3.one * 0.1f;
            FitGrabHull(grab.transform, body);   // 2026-11-19 箱では四隅が広すぎる → 真上から見た形の多角形の柱に
            var rb = grab.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var pk = grab.AddComponent<VRC.SDK3.Components.VRCPickup>();
            pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any; pk.proximity = 2f; pk.pickupable = true;
            pk.InteractionText = label; pk.UseText = "オン / オフ";
            grab.AddComponent<VRC.SDK3.Components.VRCObjectSync>();
            var mm = UdonSharpUndo.AddComponent<FuwaMovableMushroom>(grab);
            mm.body = body; mm.heldMaterial = null; mm.flashMaterial = null;   // 2026-11-19 てじゃ「岩いがいの半透明処理やっぱいらんかも」→ 持っている間も元の見た目（光るのは FuwaMovableMushroom の _EmissionColor）
            mm.ground = (1 << 11) | 1; mm.useEvent = "Toggle";
            UdonSharpBehaviour target = body.GetComponentsInChildren<FuwaPlaygroundBubbleClam>(true).FirstOrDefault();
            if (target == null) target = body.GetComponentsInChildren<FuwaPlaygroundIceToy>(true).FirstOrDefault();
            if (target == null) target = body.GetComponentsInChildren<FuwaPlaygroundLavaPool>(true).FirstOrDefault();
            mm.useTarget = target;
            if (target == null) pk.UseText = "";
            UdonSharpEditorUtility.CopyProxyToUdon(mm);
            { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(mm); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); } }
            // クリックでのオン/オフは切る（持ってトリガーで）
            foreach (var cl in body.GetComponentsInChildren<FuwaPlaygroundBubbleClam>(true)) { cl.clickToggle = false; UdonSharpEditorUtility.CopyProxyToUdon(cl); EditorUtility.SetDirty(cl); }
            foreach (var it in body.GetComponentsInChildren<FuwaPlaygroundIceToy>(true)) { it.clickToggle = false; UdonSharpEditorUtility.CopyProxyToUdon(it); EditorUtility.SetDirty(it); }
        }
        // 「おもちゃをもどす」に入れる
        var rs2 = toys.Find("ResetToys").GetComponent<FuwaHandFanReset>();
        var others2 = rs2.others.Where(o => o != null).ToList();
        foreach (var n in new[] { "Unlock6/Grab", "Unlock7/Grab", "Unlock8/Grab", "Unlock9/Grab" }) { var g = toys.Find(n); if (g && !others2.Contains(g.gameObject)) others2.Add(g.gameObject); }
        rs2.others = others2.ToArray(); Sync(rs2);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(toys.gameObject.scene);
        Debug.Log("[あそびば] ステージ2のおもちゃを持ち運べるように");
    }
    // 2026-11-19 てじゃ「オンオフ切り替え式のおもちゃをクリックでオンオフしたとき、一瞬白っぽく明滅するようにしよ」：FuwaMovableMushroom.flashMaterial（ちかっと切りかえる白）
    public static Color ToyFlashColor = new Color(1f, 0.97f, 0.92f);
    public static Material ToyFlashMat()
    {
        const string path = "Assets/FuwaCourse/Materials/Playground_ToyFlash.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Unlit/Color")) { name = "Playground_ToyFlash" }; AssetDatabase.CreateAsset(m, path); }
        m.color = ToyFlashColor; EditorUtility.SetDirty(m);
        return m;
    }
    [MenuItem("FuwaCourse/あそびば/オンオフのおもちゃに白い明滅をつける")]
    static void MenuToyFlash()
    {
        int n = 0;
        foreach (var mm in Object.FindObjectsOfType<FuwaMovableMushroom>(true))
        {
            if (!mm.gameObject.scene.IsValid() || mm.useTarget == null) continue;
            mm.flashMaterial = ToyFlashMat(); Sync(mm); n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[あそびば] 白い明滅 " + n);
    }

    // 2026-11-19 てじゃ「おもちゃのピックアップの判定がやたらひろいボックスなのがいくつかある」「ステージ2のがそうかも」：
    //  持つ判定を、おもちゃを真上から見た形（見た目の頂点の xz の凸包、角は最大 GrabHullSides 個）を高さ方向にのばした柱（凸の MeshCollider、トリガー）にする。
    //  泡・広がる氷・マグマ弾・粒は形に入れない。前の BoxCollider は消す
    public static int GrabHullSides = 20; public static float GrabHullPad = 0.05f;
    public static void FitGrabHull(Transform grab, Transform body)
    {
        if (!grab || !body) return;
        var pts = new List<Vector2>(); float ymin = float.MaxValue, ymax = float.MinValue;
        foreach (var mf in body.GetComponentsInChildren<MeshFilter>(true))
        {
            var r = mf.GetComponent<MeshRenderer>(); if (!r || !mf.sharedMesh) continue;
            if (r.name.StartsWith("Bubble") || r.name == "IceDisc" || r.name == "BombVisual") continue;
            var m = mf.sharedMesh; var tmp = m.isReadable ? null : Object.Instantiate(m); var vs = (tmp ? tmp : m).vertices; if (tmp) Object.DestroyImmediate(tmp);
            var l2w = mf.transform.localToWorldMatrix; int step = Mathf.Max(1, vs.Length / 600);
            for (int i = 0; i < vs.Length; i += step)
            {
                var p = grab.InverseTransformPoint(l2w.MultiplyPoint3x4(vs[i]));
                pts.Add(new Vector2(p.x, p.z)); ymin = Mathf.Min(ymin, p.y); ymax = Mathf.Max(ymax, p.y);
            }
        }
        if (pts.Count < 3) return;
        // 凸包（Andrew）
        pts = pts.Distinct().OrderBy(p => p.x).ThenBy(p => p.y).ToList();
        float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
        var hull = new List<Vector2>();
        foreach (var p in pts) { while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
        int lower = hull.Count + 1;
        for (int i = pts.Count - 2; i >= 0; i--) { var p = pts[i]; while (hull.Count >= lower && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
        hull.RemoveAt(hull.Count - 1);
        // 角を減らす：面積がいちばん小さくなる角から消していく（外に少しふくらませて、消した所が内側に入らないように GrabHullPad）
        while (hull.Count > GrabHullSides)
        {
            int best = 0; float bestA = float.MaxValue;
            for (int i = 0; i < hull.Count; i++) { var a0 = hull[(i + hull.Count - 1) % hull.Count]; var a1 = hull[i]; var a2 = hull[(i + 1) % hull.Count]; float ar = Mathf.Abs(Cross(a0, a1, a2)); if (ar < bestA) { bestA = ar; best = i; } }
            hull.RemoveAt(best);
        }
        var c = Vector2.zero; foreach (var p in hull) c += p; c /= hull.Count;
        for (int i = 0; i < hull.Count; i++) { var d = hull[i] - c; hull[i] = hull[i] + d.normalized * GrabHullPad; }
        ymin -= GrabHullPad; ymax += GrabHullPad;
        // 柱のメッシュ
        int n = hull.Count; var V = new List<Vector3>(); var T = new List<int>();
        foreach (var p in hull) V.Add(new Vector3(p.x, ymin, p.y));
        foreach (var p in hull) V.Add(new Vector3(p.x, ymax, p.y));
        for (int i = 1; i < n - 1; i++) { T.Add(0); T.Add(i); T.Add(i + 1); T.Add(n); T.Add(n + i + 1); T.Add(n + i); }
        for (int i = 0; i < n; i++) { int j = (i + 1) % n; T.Add(i); T.Add(n + i); T.Add(j); T.Add(j); T.Add(n + i); T.Add(n + j); }
        var mesh = new Mesh(); mesh.SetVertices(V); mesh.SetTriangles(T, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mesh = SaveGenMesh(mesh, "GrabHull_" + grab.parent.name + "_" + grab.name);
        foreach (var old in grab.GetComponents<BoxCollider>()) Object.DestroyImmediate(old);
        var mc = grab.GetComponent<MeshCollider>(); if (!mc) mc = grab.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh; mc.convex = true; mc.isTrigger = true;
    }
    [MenuItem("FuwaCourse/あそびば/持ち運ぶおもちゃの持つ判定を形に合わせる")]
    static void MenuFitGrabHulls()
    {
        int n = 0;
        foreach (var mm in Object.FindObjectsOfType<FuwaMovableMushroom>(true))
        {
            if (!mm.gameObject.scene.IsValid() || mm.name != "Grab") continue;   // ステージ2のおもちゃ（Grab）。跳ねるきのこは別
            FitGrabHull(mm.transform, mm.body); n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[あそびば] 持つ判定を形に " + n);
    }

    public static bool PlaygroundLamps = false;
    // 2026-11-19 てじゃ「オンオフの白点滅、フワッと光るのを一回でいいよ、SEは扇風機のを使いまわそ」「岩いがいの半透明処理やっぱいらんかも」「2本あるスタンドライトいらんかも」
    [MenuItem("FuwaCourse/あそびば/持ち運ぶおもちゃ：半透明なし・オンオフの音・ランプを消す")]
    static void MenuToyTweaks() { Debug.Log(ToyTweaks()); }
    public static string ToyTweaks()
    {
        int n = 0, snd = 0, lamps = 0;
        AudioSource Make(Transform parent, string name, string clip)
        {
            var old = parent.Find(name); if (old) Object.DestroyImmediate(old.gameObject);
            var g = new GameObject(name); g.transform.SetParent(parent, false);
            var a = g.AddComponent<AudioSource>(); a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaCourse/Audio/" + clip + ".wav");
            a.playOnAwake = false; a.spatialBlend = 1f; a.volume = 0.7f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 1.5f; a.maxDistance = 14f; a.dopplerLevel = 0f;
            return a;
        }
        foreach (var mm in Object.FindObjectsOfType<FuwaMovableMushroom>(true))
        {
            if (!mm.gameObject.scene.IsValid()) continue;
            mm.heldMaterial = null; mm.flashMaterial = null; Sync(mm); n++;
            if (mm.useTarget != null && mm.body != null)
            {
                var so = new SerializedObject(mm.useTarget);
                var on = so.FindProperty("toggleSound"); var off = so.FindProperty("offSound");
                if (on != null && off != null) { on.objectReferenceValue = Make(mm.body, "ToggleOnSfx", "sfx_fan_on"); off.objectReferenceValue = Make(mm.body, "ToggleOffSfx", "sfx_fan_off"); so.ApplyModifiedPropertiesWithoutUndo(); Sync(mm.useTarget); snd++; }
            }
        }
        if (!PlaygroundLamps)
            foreach (var t in Object.FindObjectsOfType<Transform>(true).Where(t => t.name == "Lamp" && t.gameObject.scene.IsValid() && t.root.name == "FuwaPlayground" && (t.Find("LampStem") || t.Find("Chill_LampStem"))).ToList()) { Object.DestroyImmediate(t.gameObject); lamps++; }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "おもちゃ " + n + "（半透明なし）・オンオフの音 " + snd + "・ランプを消した " + lamps;
    }

    [MenuItem("FuwaCourse/あそびば/ステージ2のおもちゃを持ち運べるように")]
    static void MenuStage2Movable() { MakeStage2ToysMovable(Object.FindObjectsOfType<Transform>(true).First(t => t.name == "Toys" && t.parent != null && t.parent.name == "Room")); }

    // 2–2 溶岩溜まり（2026-11-16）：まわりをコース2–2 の岩でかこんだ丸い溶岩の池。オンの間マグマ弾（コース2–2 の火の玉の見た目）が真上に上がって戻る
    //  弾・池の上のふわふわは燃えだす（FuwaSharedBall.Ignite）。持ち運び・持ってトリガーでオンオフは MakeStage2ToysMovable
    public static float LavaPoolR = 0.9f;
    static void FillLavaPool(Transform slot)
    {
        EnsureProgram("FuwaPlaygroundLavaPool");
        var c22 = RootGo("FuwaCourse2_2").transform;
        var sp = Object.FindObjectsOfType<FuwaPlaygroundSpawner>(true).First();
        var root = new GameObject("LavaPool").transform; root.SetParent(slot, false);
        root.localPosition = Vector3.zero; root.localRotation = Quaternion.identity;
        var lavaMat = c22.Find("LavaHoles/LavaPools").GetComponent<MeshRenderer>().sharedMaterial;
        var rockMat = c22.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).First(m => m != null && m.name == "Stage2_2_Rock");
        var rockMesh = c22.GetComponentsInChildren<MeshFilter>(true).First(m => m.sharedMesh != null && m.sharedMesh.name == "Stage2_LavaRock0").sharedMesh;
        const float lavaY = 0.14f;
        // 溶岩の面（平らな円盤）
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); disc.name = "Lava"; Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.transform.SetParent(root, false); disc.transform.localPosition = new Vector3(0f, lavaY * 0.5f, 0f); disc.transform.localScale = new Vector3(LavaPoolR * 2.1f, lavaY * 0.5f, LavaPoolR * 2.1f);
        // 運べるので模様は物の座標で（2026-11-27 てじゃ「運ぶ時に模様が違和感」）：2–2 の溶岩マテリアルを写した Playground_Lava（シェーダー FuwaCourse/LavaObject）
        var pMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Playground_Lava.mat");
        if (pMat == null) { pMat = new Material(lavaMat) { name = "Playground_Lava" }; AssetDatabase.CreateAsset(pMat, "Assets/FuwaCourse/Materials/Playground_Lava.mat"); }
        pMat.shader = Shader.Find("FuwaCourse/LavaObject"); pMat.CopyPropertiesFromMaterial(lavaMat); pMat.SetFloat("_ObjectSpace", 1f); EditorUtility.SetDirty(pMat);
        disc.GetComponent<MeshRenderer>().sharedMaterial = pMat;
        var dcol = disc.AddComponent<MeshCollider>(); dcol.sharedMesh = disc.GetComponent<MeshFilter>().sharedMesh; dcol.convex = true; disc.layer = 11;
        LavaRimRocks(root, rockMesh, rockMat);
        var surface = new GameObject("Surface").transform; surface.SetParent(root, false); surface.localPosition = new Vector3(0f, lavaY, 0f);
        // マグマ弾：コース2–2 の火の玉の見た目と尾
        var fb = c22.GetComponentsInChildren<FuwaFireball>(true).First();
        var bomb = new GameObject("Bomb"); bomb.transform.SetParent(root, false); bomb.transform.position = surface.position;
        var vis = Object.Instantiate(fb.visual, bomb.transform, false); vis.name = "BombVisual"; vis.transform.localPosition = Vector3.zero; vis.transform.localScale = fb.visual.transform.lossyScale * 0.6f; vis.SetActive(true);
        foreach (var cc in vis.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(cc);
        ParticleSystem trail = null;
        if (fb.trail != null) { var tg = Object.Instantiate(fb.trail.gameObject, root, false); tg.name = "Trail"; tg.transform.SetParent(bomb.transform, false); tg.transform.localPosition = Vector3.zero; trail = tg.GetComponent<ParticleSystem>(); }
        ParticleSystem splash = null;
        if (fb.trail != null) { var sg = Object.Instantiate(fb.trail.gameObject, root, false); sg.name = "Splash"; sg.transform.localPosition = surface.localPosition; sg.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); splash = sg.GetComponent<ParticleSystem>(); var em = splash.emission; em.rateOverTime = 0f; var mn = splash.main; mn.simulationSpace = ParticleSystemSimulationSpace.World; }
        AudioSource whoosh = null;
        if (fb.whoosh != null) { var wg = Object.Instantiate(fb.whoosh.gameObject, root, false); wg.name = "WhooshSfx"; wg.transform.localPosition = surface.localPosition; whoosh = wg.GetComponent<AudioSource>(); }
        bomb.SetActive(false);
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        var lp = UdonSharpUndo.AddComponent<FuwaPlaygroundLavaPool>(root.gameObject);
        lp.spawner = sp; lp.surface = surface; lp.bomb = bomb; lp.trail = trail; lp.splash = splash; lp.whoosh = whoosh;
        lp.poolRadius = LavaPoolR;
        UdonSharpEditorUtility.CopyProxyToUdon(lp);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(lp); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; EditorUtility.SetDirty(ub); } }
    }
    // 2–2 のおもちゃだけ作り直す（火炎放射の岩 → 溶岩溜まり）
    // まわりの岩（コース2–2 の岩を大小ずらして並べる）
    // 2026-11-19 てじゃ「溶岩溜まりの岩にスキマおおい」：11個→LavaRockCount 個、少し内側（溶岩のふちに重ねる）、角度のゆらぎを小さく
    public static int LavaRockCount = 17; public static float LavaRockIn = 0.2f, LavaRockJitter = 0.2f;
    public static void LavaRimRocks(Transform root, Mesh rockMesh, Material rockMat)
    {
        foreach (var o in root.Cast<Transform>().Where(t => t.name.StartsWith("RimRock")).ToArray()) Object.DestroyImmediate(o.gameObject);
        var rnd = new System.Random(22);
        int nRock = LavaRockCount;
        for (int i = 0; i < nRock; i++)
        {
            float a = (i + (float)rnd.NextDouble() * LavaRockJitter) / nRock * Mathf.PI * 2f;
            var rk = new GameObject("RimRock" + i); rk.transform.SetParent(root, false);
            float s0 = 0.42f + (float)rnd.NextDouble() * 0.22f;
            rk.transform.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (LavaPoolR + s0 * LavaRockIn) + Vector3.up * (s0 * 0.12f);
            rk.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 20f - 10f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 20f - 10f);
            var b = rockMesh.bounds; float k = s0 / Mathf.Max(b.size.x, b.size.z);
            rk.transform.localScale = new Vector3(k, k * 0.75f, k);
            rk.AddComponent<MeshFilter>().sharedMesh = rockMesh; rk.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
            var mc = rk.AddComponent<MeshCollider>(); mc.sharedMesh = rockMesh; mc.convex = true; rk.layer = 11;
        }
    }

    [MenuItem("FuwaCourse/あそびば/2–2のおもちゃ（溶岩溜まり）を作り直す")]
    static void MenuLavaPool()
    {
        var toys = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "Toys" && t.parent != null && t.parent.name == "Room");
        var slot = toys.Find("Unlock7");
        foreach (Transform c in slot.Cast<Transform>().ToList()) Object.DestroyImmediate(c.gameObject);
        FillLavaPool(slot);
        MakeStage2ToysMovable(toys);
        SetupSharedBallBurn();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(toys.gameObject.scene);
        Debug.Log("[あそびば] 溶岩溜まり");
        Debug.Log(MuteToySounds());
    }
    // あそびばのふわふわが燃えた時の炎（コース2–2 の FuwaHeat の炎を1つずつ複製。ふわふわの子にはしない＝転がっても炎は上向き）
    static void SetupSharedBallBurn()
    {
        var sp = Object.FindObjectsOfType<FuwaPlaygroundSpawner>(true).First();
        var heat = Object.FindObjectsOfType<FuwaHeat>(true).First();
        var holderParent = sp.balls[0].transform.parent;
        var old = holderParent.Find("BurnFx"); if (old) Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject("BurnFx").transform; holder.SetParent(holderParent, false);
        for (int i = 0; i < sp.balls.Length; i++)
        {
            var b = sp.balls[i]; if (b == null) continue;
            ParticleSystem fx = null; GameObject mesh = null; AudioSource snd = null;
            if (heat.burnFx != null) { var g = Object.Instantiate(heat.burnFx.gameObject, holder, false); g.name = "Fx" + i; fx = g.GetComponent<ParticleSystem>(); var em = fx.emission; em.rateOverTime = 0f; }
            if (heat.burnMesh != null) { mesh = Object.Instantiate(heat.burnMesh, holder, false); mesh.name = "Flame" + i; mesh.transform.localScale = heat.burnMesh.transform.lossyScale; mesh.SetActive(false); }
            if (heat.burnSound != null) { var g = Object.Instantiate(heat.burnSound.gameObject, holder, false); g.name = "Sfx" + i; snd = g.GetComponent<AudioSource>(); }
            b.burnFx = fx; b.burnMesh = mesh; b.burnSound = snd; b.burnMeshOffset = heat.burnMeshOffset;
            UdonSharpEditorUtility.CopyProxyToUdon(b); EditorUtility.SetDirty(b);
        }
    }

    // 2–1 泡が出る貝：コース2–1 の貝をコピーして大きく、口を上へ。クリックでオン／オフ（同期）
    static void FillBubbleClam(Transform slot)
    {
        var src = Object.FindObjectsOfType<FuwaClamBubbles>(true).First();
        var c0 = src.clams[0];
        EnsureProgram("FuwaPlaygroundBubbleClam");
        var clam = Object.Instantiate(c0.gameObject, slot, false); clam.name = "BubbleClam";
        // 付け根は 2–1 の貝と同じ（2026-11-27 めりこみ対策で後ろ・上へ）。写した貝が古い付け根のままならそろえる
        { var lh = clam.transform.Find("LidHinge"); if (lh != null) { var d = lh.localPosition - FuwaCourse2_1Builder.ClamHinge; lh.localPosition = FuwaCourse2_1Builder.ClamHinge; foreach (Transform ch in lh) ch.localPosition += d; } }
        foreach (var t in clam.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        foreach (var ub in clam.GetComponentsInChildren<UdonSharpBehaviour>(true)) Object.DestroyImmediate(ub);
        const float sc = 1.5f;
        clam.transform.localPosition = new Vector3(0f, 0f, 0f); clam.transform.localRotation = Quaternion.identity; clam.transform.localScale = Vector3.one * sc;
        var lid = clam.transform.Find("LidHinge");
        // 泡の出どころ：真珠の上（コースの Mouth は横へ吹く用の前のふちなので使わない）
        var pearlR = clam.transform.Find("Pearl").GetComponent<Renderer>();
        var mouth = new GameObject("BubbleOrigin").transform; mouth.SetParent(clam.transform, false); mouth.position = pearlR.bounds.center + Vector3.up * 0.55f;
        // クリックの当たり：貝のまわりの箱（ふつうの当たり＝のれる・押せる）
        var bb = new Bounds(); bool first = true;
        foreach (var r in clam.GetComponentsInChildren<Renderer>(true)) { var lb = r.bounds; if (first) { bb = lb; first = false; } else bb.Encapsulate(lb); }
        var box = clam.AddComponent<BoxCollider>(); box.center = clam.transform.InverseTransformPoint(bb.center); box.size = Vector3.Scale(bb.size, new Vector3(1f / sc, 1f / sc, 1f / sc));
        var bc = UdonSharpUndo.AddComponent<FuwaPlaygroundBubbleClam>(clam);
        bc.lid = lid; bc.mouth = mouth; bc.spawner = Object.FindObjectsOfType<FuwaPlaygroundSpawner>(true).First();
        mouth.rotation = slot.rotation;   // 泡は島の真ん中の方へ（slot の +Z）
        // 2026-11-16 てじゃ「泡はコースのみたいなの、出してほしい」→ コース2–1 の泡（Bubble3D の球）を3つ。吹いて、包んで運んで、割れる
        var srcB = src.bubbles[0];
        var bl = new List<Transform>();
        // 2026-11-27 てじゃ「泡の寿命もっと伸ばしたい」：進む距離 6→10m（約9秒）。吹く間隔 2.6秒なので同時に4つ飛ぶ → 泡を4つに
        for (int i = 0; i < PlaygroundBubbleCount; i++)
        {
            var b = Object.Instantiate(srcB.gameObject, slot, false); b.name = "Bubble" + i;
            foreach (var cc in b.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(cc);
            foreach (var ub in b.GetComponentsInChildren<UdonSharpBehaviour>(true)) Object.DestroyImmediate(ub);
            b.transform.localPosition = Vector3.zero; b.transform.localRotation = Quaternion.identity;
            foreach (var r in b.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            b.SetActive(false);
            bl.Add(b.transform);
        }
        bc.bubbles = bl.ToArray(); bc.bubbleSize = 0.95f; bc.bubbleRange = PlaygroundBubbleRange;
        // 音・粒：コース2–1 の貝のをコピー（あそびばの中の位置で鳴らす）
        AudioSource CopyAs(AudioSource a, string nm) { if (a == null) return null; var g = Object.Instantiate(a.gameObject, clam.transform, false); g.name = nm; g.transform.localPosition = Vector3.zero; foreach (Transform ch in g.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(ch.gameObject); foreach (var ub in g.GetComponents<UdonSharpBehaviour>()) Object.DestroyImmediate(ub); var au = g.GetComponent<AudioSource>(); au.playOnAwake = false; return au; }
        bc.toggleSound = CopyAs(src.blowSound, "ToggleSound"); bc.blowSound = CopyAs(src.blowSound, "BlowSound");
        bc.popSound = CopyAs(src.popSound, "PopSound"); bc.captureSound = CopyAs(src.captureSound, "CaptureSound");
        if (src.popFx != null) { var fx = Object.Instantiate(src.popFx.gameObject, slot, false); fx.name = "PopFx"; fx.transform.localPosition = Vector3.zero; bc.popFx = fx.GetComponent<ParticleSystem>(); }
        UdonSharpEditorUtility.CopyProxyToUdon(bc);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(bc); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; ub.interactText = "あわ"; EditorUtility.SetDirty(ub); } }
    }

    // 2–2 火炎放射の岩：コース2–2 の溶岩の岩＋火炎放射の炎（FlameMesh）。クリックでオン／オフ（同期）、炎は島の真ん中の方へ
    static void FillFlameRock(Transform slot)
    {
        EnsureProgram("FuwaPlaygroundFlameRock"); EnsureProgram("FuwaPlaygroundFlameZone");
        var c22 = RootGo("FuwaCourse2_2").transform;
        var rockMF = c22.GetComponentsInChildren<MeshFilter>(true).First(m => m.sharedMesh != null && m.sharedMesh.name == "Stage2_LavaRock0");
        var ft = c22.Find("Fire/Flamethrower0");
        // 2026-11-16 てじゃ「火炎放射の位置低すぎる もっと専用の大きい岩作って」→ ゴツゴツの大きい岩（高さ約2.3m）を作る。正面（島の真ん中がわ）の高さ FlameMouthY に火の出る穴
        var rockMesh = FlameRockMesh();
        var rock = new GameObject("FlameRock"); rock.transform.SetParent(slot, false);
        rock.transform.localPosition = new Vector3(0f, 0f, -0.2f); rock.transform.localRotation = Quaternion.identity;
        // 2026-11-16 てじゃ「もっと2_2の普通の岩盤みたいなみためでいいよ」→ コース2–2 の岩盤の材質（Stage2_2_Rock）
        var bedrock = c22.GetComponentsInChildren<MeshRenderer>(true).SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m != null && m.name == "Stage2_2_Rock");
        rock.AddComponent<MeshFilter>().sharedMesh = rockMesh; rock.AddComponent<MeshRenderer>().sharedMaterial = bedrock != null ? bedrock : rockMF.GetComponent<MeshRenderer>().sharedMaterial;
        var mcR = rock.AddComponent<MeshCollider>(); mcR.sharedMesh = rockMesh;
        var fr = UdonSharpUndo.AddComponent<FuwaPlaygroundFlameRock>(rock);
        // 穴の奥の暗い面と炎（上中下）
        var bodies = new List<Transform>();
        Vector3 mouthPos = Vector3.zero;
        for (int r = 0; r < FlameRows.Length; r++)
        {
            var holeD = GameObject.CreatePrimitive(PrimitiveType.Sphere); holeD.name = "Hole" + r; Object.DestroyImmediate(holeD.GetComponent<Collider>());
            holeD.transform.SetParent(rock.transform, false); holeD.transform.localPosition = new Vector3(0f, FlameRows[r], flameHoleZ[r] - 0.04f); holeD.transform.localScale = new Vector3(0.42f, 0.34f, 0.2f);
            holeD.GetComponent<MeshRenderer>().sharedMaterial = Mat("Playground_FlameRockHole", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(0.16f, 0.07f, 0.05f));
            var mp = rock.transform.TransformPoint(new Vector3(0f, FlameRows[r], flameHoleZ[r]));
            if (r == 0) mouthPos = mp;
            var body = Object.Instantiate(ft.Find("FlameBody").gameObject, slot, false); body.name = "FlameBody" + r;
            body.transform.rotation = slot.rotation * Quaternion.Euler(90f, 0f, 0f);
            body.transform.position = mp - slot.forward * 0.9f + Vector3.down * FlameBodyDrop;   // 2026-11-16 てじゃ「穴とズレてない？」→ 炎のシェーダーが上へ持ち上げる分（見た目で約0.38m）下げる
            body.SetActive(false);
            bodies.Add(body.transform);
        }
        fr.flameBodies = bodies.ToArray(); fr.bodyScale = new Vector3(0.26f, 0.28f, 0.26f);
        var roarSrc = ft.Find("RoarSfx");
        if (roarSrc != null) { var a = Object.Instantiate(roarSrc.gameObject, slot, false); a.name = "RoarSfx"; a.transform.position = mouthPos; var au = a.GetComponent<AudioSource>(); au.loop = true; au.playOnAwake = false; fr.roar = au; }
        UdonSharpEditorUtility.CopyProxyToUdon(fr);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(fr); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; ub.interactText = "ほのお"; EditorUtility.SetDirty(ub); } }
        // 炎の当たり
        var z = new GameObject("FlameZone"); z.transform.SetParent(slot, false); z.layer = 2;
        z.transform.SetPositionAndRotation(mouthPos, slot.rotation);
        float rowSpan = FlameRows[FlameRows.Length - 1] - FlameRows[0];
        var bz = z.AddComponent<BoxCollider>(); bz.isTrigger = true; bz.size = new Vector3(1.3f, rowSpan + 1.2f, 4.2f); bz.center = new Vector3(0f, rowSpan * 0.5f, 2.2f);
        var fz = UdonSharpUndo.AddComponent<FuwaPlaygroundFlameZone>(z); fz.rock = fr; UdonSharpEditorUtility.CopyProxyToUdon(fz);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(fz); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); } }
    }

    // 2026-11-16 てじゃ「岩はもっと柱みたいなのにして3列にしよ」→ ゴツゴツの柱（高さ FlameRockH）、正面に上中下3つの穴（FlameRows の高さ）
    public static float FlameRockH = 3.1f, FlameRockR = 0.72f, FlameBodyDrop = 0.38f;
    public static float[] FlameRows = { 0.75f, 1.5f, 2.25f };
    static float[] flameHoleZ = new float[3];
    static Mesh FlameRockMesh()
    {
        int nu = 20, nv = 18; var pts = new Vector3[nu, nv + 1];
        for (int i = 0; i < nu; i++) for (int j = 0; j <= nv; j++)
        {
            float th = Mathf.PI * 2f * i / nu, y = FlameRockH * j / nv;
            var d = new Vector3(Mathf.Sin(th), 0f, Mathf.Cos(th));   // th=0 が正面（+Z）
            float n = 0.13f * (Mathf.PerlinNoise(d.x * 1.6f + 3.1f, y * 0.9f + d.z * 1.2f) - 0.5f) * 2f + 0.06f * (Mathf.PerlinNoise(d.x * 4.1f + 11f, y * 2.7f + d.z * 3.3f) - 0.5f) * 2f;
            float taper = Mathf.Lerp(1.08f, 0.82f, y / FlameRockH);
            float r = FlameRockR * (1f + n) * taper;
            foreach (float hy in FlameRows)
            {   // 穴のくぼみ（正面、高さ hy）
                float dy = (y - hy) / 0.3f, da = Mathf.DeltaAngle(0f, th * Mathf.Rad2Deg) / 30f;
                float k = Mathf.Clamp01(1f - (dy * dy + da * da)); r -= 0.3f * k * k * (3f - 2f * k);
            }
            float yy = y;
            if (j == nv) yy = FlameRockH + 0.12f * (Mathf.PerlinNoise(d.x * 2f, d.z * 2f) - 0.5f);
            pts[i, j] = new Vector3(d.x * r, j == 0 ? -0.05f : yy, d.z * r);
        }
        var v = new List<Vector3>(); var t = new List<int>();
        void T(Vector3 a, Vector3 b, Vector3 c, Vector3 cen) { var nn = Vector3.Cross(b - a, c - a); if (Vector3.Dot(nn, (a + b + c) / 3f - cen) < 0f) { var x = b; b = c; c = x; } int k = v.Count; v.Add(a); v.Add(b); v.Add(c); t.Add(k); t.Add(k + 1); t.Add(k + 2); }
        for (int i = 0; i < nu; i++) for (int j = 0; j < nv; j++)
        {
            int i2 = (i + 1) % nu; var cen = new Vector3(0f, (pts[i, j].y + pts[i, j + 1].y) * 0.5f, 0f);
            T(pts[i, j], pts[i2, j], pts[i2, j + 1], cen); T(pts[i, j], pts[i2, j + 1], pts[i, j + 1], cen);
        }
        var top = new Vector3(0f, FlameRockH + 0.1f, 0f);
        for (int i = 0; i < nu; i++) T(pts[i, nv], pts[(i + 1) % nu, nv], top, new Vector3(0f, FlameRockH - 1f, 0f));
        var mesh = new Mesh { name = "Playground_FlameRock" }; mesh.SetVertices(v); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        for (int r = 0; r < FlameRows.Length; r++) { float zz = 0f; foreach (var q in v) if (Mathf.Abs(q.y - FlameRows[r]) < 0.12f && Mathf.Abs(q.x) < 0.15f && q.z > zz) zz = q.z; flameHoleZ[r] = zz; }
        return SaveGenMesh(mesh, "Playground_FlameRock");
    }

    // 2–3 氷のたね：氷の結晶3本（クリックでオン／オフ同期）＋まわり半径3mにひろがる氷の円（人が滑る FuwaIceSkate・つるつるの物理材質）
    public static int PlaygroundBubbleCount = 4; public static float PlaygroundBubbleRange = 10f;   // 貝の泡（2026-11-27 寿命をのばした）
    public static float IceToyR = 4.5f, IceDiscSkirt = 0.15f;   // 2026-11-27 てじゃ「展開範囲広げて」3→4.5m
    public static bool PortalPair = true;
    static void FillIceToy(Transform slot)
    {
        EnsureProgram("FuwaPlaygroundIceToy");
        var c23 = RootGo("FuwaCourse2_3").transform;
        var iceSrc = c23.Find("IceSkate").GetComponent<FuwaIceSkate>();
        var iceM = iceSrc.ice.First(x => x != null).GetComponent<Renderer>().sharedMaterial;
        // 結晶（六角柱＋とがった先）3本
        var toy = new GameObject("IceSeed"); toy.transform.SetParent(slot, false);
        toy.AddComponent<MeshFilter>().sharedMesh = SaveGenMesh(IceSeedMesh(), "Playground_IceSeed");
        toy.AddComponent<MeshRenderer>().sharedMaterial = iceM;
        FillIceToyRest(slot, toy, iceM, iceSrc);
    }

    // こおりのたねの結晶（2026-11-22 てじゃ「根本がちょと雑かも」）：傾いた結晶の根もとの平らな底が床から浮いて見えていた
    //  → 結晶は床の下まで伸ばして床から生えているように（傾きぶん深く）、根もとに小さいかけら（低い結晶）を数本足して継ぎ目を隠す
    public static Mesh IceSeedMesh()
    {
        var cv = new List<Vector3>(); var ct = new List<int>();
        void Crystal(Vector3 b, Quaternion q, float r, float h)
        {
            int n = 6; var ring0 = new Vector3[n]; var ring1 = new Vector3[n]; var tip = b + q * new Vector3(0f, h + r * 1.2f, 0f);
            for (int i = 0; i < n; i++) { float a = Mathf.PI * 2f * i / n; var d = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r); ring0[i] = b + q * d; ring1[i] = b + q * (d + Vector3.up * h); }
            void T(Vector3 x, Vector3 y, Vector3 z, Vector3 want) { if (Vector3.Dot(Vector3.Cross(y - x, z - x), want) < 0f) { var tmp = y; y = z; z = tmp; } int k = cv.Count; cv.Add(x); cv.Add(y); cv.Add(z); ct.Add(k); ct.Add(k + 1); ct.Add(k + 2); }
            var cen = b + q * new Vector3(0f, h * 0.5f, 0f);
            for (int i = 0; i < n; i++) { int j = (i + 1) % n; T(ring0[i], ring0[j], ring1[j], ring0[i] + ring1[j] - 2f * cen); T(ring0[i], ring1[j], ring1[i], ring0[i] + ring1[j] - 2f * cen); T(ring1[i], ring1[j], tip, (ring1[i] + ring1[j]) * 0.5f + tip - 2f * cen); T(ring0[i], ring0[j], b, b - cen); }
        }
        // 2026-11-27 てじゃ「もちあげたときに下が変、ちゃんと繋がってて下も整えたい」：結晶ごとに床の下へ伸ばした斜めの切り口が、持ち上げるとバラバラに見えていた
        //  → 結晶の横の面を、床から BaseIn の高さの水平な面まで下ろして（底は水平に切る）、全部の足もとを1つの低い氷の台座に埋める。台座の底は平らな1枚
        const float BaseIn = 0.02f, BaseH = 0.055f, BaseSide = 0.03f, BaseBevel = 0.035f, BaseMargin = 0.045f;
        var foot = new List<Vector2>();
        void Grown(Vector3 at, Quaternion q, float r, float h)
        {
            int n = 6; var up = q * Vector3.up; var ring0 = new Vector3[n]; var ring1 = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 2f * i / n; var d = q * new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                ring1[i] = at + d + up * h;
                ring0[i] = ring1[i] - up * ((ring1[i].y - BaseIn) / Mathf.Max(0.2f, up.y));   // 横の辺を台座の中の水平な面まで
                foot.Add(new Vector2(ring0[i].x, ring0[i].z));
            }
            var tip = at + up * (h + r * 1.2f);
            var b = Vector3.zero; foreach (var p in ring0) b += p; b /= n;
            var cen = (b + at + up * h) * 0.5f;
            void T(Vector3 x, Vector3 y, Vector3 z, Vector3 want) { if (Vector3.Dot(Vector3.Cross(y - x, z - x), want) < 0f) { var tmp = y; y = z; z = tmp; } int k = cv.Count; cv.Add(x); cv.Add(y); cv.Add(z); ct.Add(k); ct.Add(k + 1); ct.Add(k + 2); }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n; var sideOut = (ring0[i] + ring1[j]) * 0.5f - (b + (at + up * h)) * 0.5f;
                T(ring0[i], ring0[j], ring1[j], sideOut); T(ring0[i], ring1[j], ring1[i], sideOut);
                T(ring1[i], ring1[j], tip, (ring1[i] + ring1[j]) * 0.5f + tip - 2f * cen);
            }
            // 底のふたは台座の中なので作らない
        }
        Grown(Vector3.zero, Quaternion.identity, 0.2f, 0.75f);
        Grown(new Vector3(0.2f, 0f, 0.08f), Quaternion.Euler(0f, 0f, -24f), 0.13f, 0.45f);
        Grown(new Vector3(-0.16f, 0f, -0.1f), Quaternion.Euler(18f, 0f, 20f), 0.11f, 0.35f);
        // 根もとの小さいかけら（低くて太めの結晶をまわりに）
        Grown(new Vector3(0.06f, 0f, -0.24f), Quaternion.Euler(-30f, 40f, 0f), 0.07f, 0.12f);
        Grown(new Vector3(-0.27f, 0f, 0.1f), Quaternion.Euler(10f, 0f, 38f), 0.06f, 0.1f);
        Grown(new Vector3(0.3f, 0f, -0.12f), Quaternion.Euler(25f, 0f, -35f), 0.06f, 0.09f);
        Grown(new Vector3(-0.05f, 0f, 0.27f), Quaternion.Euler(35f, 0f, 8f), 0.05f, 0.08f);
        // 台座：結晶の足もとの点の凸包を BaseMargin 外へ広げた形。底（y=0、平ら）→ たての側面（BaseSide）→ 面取り（内へ BaseBevel、上面 BaseH）→ 平らな上面
        {
            var pts = foot.OrderBy(p => p.x).ThenBy(p => p.y).ToList();
            float Cr(Vector2 o, Vector2 a2, Vector2 b2) => (a2.x - o.x) * (b2.y - o.y) - (a2.y - o.y) * (b2.x - o.x);
            var hull = new List<Vector2>();
            foreach (var p in pts) { while (hull.Count >= 2 && Cr(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
            int lower = hull.Count + 1;
            for (int i = pts.Count - 2; i >= 0; i--) { var p = pts[i]; while (hull.Count >= lower && Cr(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1); hull.Add(p); }
            hull.RemoveAt(hull.Count - 1);   // 反時計回り（x,z で見て）
            // 近すぎる点はまとめる（細かい面が並ばないように）
            var hs = new List<Vector2>(); foreach (var p in hull) if (hs.Count == 0 || (hs[hs.Count - 1] - p).magnitude > 0.05f) hs.Add(p);
            if (hs.Count > 3 && (hs[0] - hs[hs.Count - 1]).magnitude < 0.05f) hs.RemoveAt(hs.Count - 1);
            var c2 = Vector2.zero; foreach (var p in hs) c2 += p; c2 /= hs.Count;
            int m = hs.Count;
            // 角ごとに外へ（となりの辺の外向きの平均の向き）広げる
            Vector2 Off(int i, float d)
            {
                var p = hs[i]; var a0 = hs[(i - 1 + m) % m]; var a1 = hs[(i + 1) % m];
                Vector2 N(Vector2 e) => new Vector2(e.y, -e.x).normalized;   // 反時計回りの辺の外向き
                var n0 = N(p - a0); var n1 = N(a1 - p); var nm = (n0 + n1).normalized; float cosH = Mathf.Max(0.5f, Vector2.Dot(nm, n0));
                return p + nm * (d / cosH);
            }
            var bot = new Vector3[m]; var mid = new Vector3[m]; var topR = new Vector3[m];
            for (int i = 0; i < m; i++)
            {
                var o = Off(i, BaseMargin); var o2 = Off(i, BaseMargin - BaseBevel);
                bot[i] = new Vector3(o.x, 0f, o.y); mid[i] = new Vector3(o.x, BaseSide, o.y); topR[i] = new Vector3(o2.x, BaseH, o2.y);
            }
            void T2(Vector3 x, Vector3 y, Vector3 z, Vector3 want) { if (Vector3.Dot(Vector3.Cross(y - x, z - x), want) < 0f) { var tmp = y; y = z; z = tmp; } int k = cv.Count; cv.Add(x); cv.Add(y); cv.Add(z); ct.Add(k); ct.Add(k + 1); ct.Add(k + 2); }
            var cTop = new Vector3(c2.x, BaseH, c2.y); var cBot = new Vector3(c2.x, 0f, c2.y);
            for (int i = 0; i < m; i++)
            {
                int j = (i + 1) % m;
                var outward = (bot[i] + bot[j]) * 0.5f - cBot; outward.y = 0f;
                T2(bot[i], bot[j], mid[j], outward); T2(bot[i], mid[j], mid[i], outward);                            // たての側面
                var bev = outward.normalized + Vector3.up; T2(mid[i], mid[j], topR[j], bev); T2(mid[i], topR[j], topR[i], bev);   // 面取り
                T2(topR[i], topR[j], cTop, Vector3.up);                                                            // 上面
                T2(bot[i], bot[j], cBot, Vector3.down);                                                            // 底（平ら）
            }
        }
        var cm = new Mesh { name = "Playground_IceSeed" }; cm.SetVertices(cv); cm.SetTriangles(ct, 0); cm.RecalculateNormals(); cm.RecalculateBounds();
        return cm;
    }

    // 氷の円（上の面は床の 2.5cm 上、ふちに厚み）
    static Mesh IceDiscMesh()
    {
        var dv = new List<Vector3>(); var dt = new List<int>(); int seg = 96; float R = IceToyR, H = 0.025f;
        dv.Add(new Vector3(0f, H, 0f));
        for (int i = 0; i <= seg; i++) { float a = Mathf.PI * 2f * i / seg; dv.Add(new Vector3(Mathf.Cos(a) * R, H, Mathf.Sin(a) * R)); }
        for (int i = 1; i <= seg; i++) { dt.Add(0); dt.Add(i + 1); dt.Add(i); }
        int b0 = dv.Count;
        for (int i = 0; i <= seg; i++) { float a = Mathf.PI * 2f * i / seg; dv.Add(new Vector3(Mathf.Cos(a) * R, H, Mathf.Sin(a) * R)); dv.Add(new Vector3(Mathf.Cos(a) * R, -IceDiscSkirt, Mathf.Sin(a) * R)); }   // ふちの厚み：床の高い所に合わせて持ち上げた時、低い所でも浮いて見えないように（FuwaPlaygroundIceToy.FitHeight）
        for (int i = 0; i < seg; i++) { int a = b0 + i * 2; dt.Add(a); dt.Add(a + 2); dt.Add(a + 1); dt.Add(a + 1); dt.Add(a + 2); dt.Add(a + 3); }
        var dm = new Mesh { name = "Playground_IceDisc" }; dm.SetVertices(dv); dm.SetTriangles(dt, 0); dm.RecalculateNormals(); dm.RecalculateBounds();
        // 上向きでなければ面を裏返す（真ん中の面）
        if (dm.normals[0].y < 0f) { var tt = dm.triangles; for (int i = 0; i < tt.Length; i += 3) { var x = tt[i + 1]; tt[i + 1] = tt[i + 2]; tt[i + 2] = x; } dm.triangles = tt; dm.RecalculateNormals(); }
        return dm;
    }

    // シーンのこおりのたねだけ直す（丸ごと作り直さない）：結晶の形・氷の円の大きさ・氷が出ている間に結晶を縮める設定
    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば：こおりのたねを直す（結晶の台座・氷の範囲・展開中は小さく）")]
    public static void FixIceToy()
    {
        var pg = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaPlayground" && t.parent == null);
        var seed = pg.GetComponentsInChildren<FuwaPlaygroundIceToy>(true).First();
        SaveGenMesh(IceSeedMesh(), "Playground_IceSeed");
        var dm = SaveGenMesh(IceDiscMesh(), "Playground_IceDisc");
        var mc = seed.discCollider as MeshCollider; if (mc != null) { mc.sharedMesh = null; mc.sharedMesh = dm; EditorUtility.SetDirty(mc); }
        seed.discRadius = IceToyR; seed.crystal = seed.transform; seed.crystalCollider = seed.GetComponent<BoxCollider>();
        IceGrabColliders(seed);
        UdonSharpEditorUtility.CopyProxyToUdon(seed); EditorUtility.SetDirty(seed);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pg.gameObject.scene);
        Debug.Log("こおりのたね：直した（氷の半径 " + IceToyR + "m）");
    }

    // 持つ判定（Grab）：結晶の形の凸包（大）と、縮んだ時の形（onScale 倍、小）。氷が出ている間は小さい方（FuwaPlaygroundIceToy.grabSmall）でハイライトも小さく（2026-11-27）
    public static void IceGrabColliders(FuwaPlaygroundIceToy seed)
    {
        var mm = Object.FindObjectsOfType<FuwaMovableMushroom>(true).FirstOrDefault(m => m.useTarget == UdonSharpEditorUtility.GetBackingUdonBehaviour(seed) || (m.body != null && seed.transform.IsChildOf(m.body)));
        if (mm == null) { Debug.LogWarning("[こおりのたね] 持ち手（Grab）がない"); return; }
        var grab = mm.gameObject;
        var seedMesh = seed.GetComponent<MeshFilter>().sharedMesh;
        Mesh Scaled(float k, string name)
        {
            var m = new Mesh(); var v = seedMesh.vertices;
            var toGrab = grab.transform.worldToLocalMatrix * seed.transform.parent.localToWorldMatrix * Matrix4x4.TRS(seed.transform.localPosition, seed.transform.localRotation, Vector3.one);
            for (int i = 0; i < v.Length; i++) v[i] = toGrab.MultiplyPoint3x4(v[i] * k);
            m.vertices = v; m.triangles = seedMesh.triangles; m.RecalculateBounds(); m.RecalculateNormals();
            return SaveGenMesh(m, name);
        }
        var full = Scaled(1f, "Playground_IceSeedGrab"); var small = Scaled(seed.onScale, "Playground_IceSeedGrabSmall");
        var cols = grab.GetComponents<MeshCollider>().ToList();
        while (cols.Count < 2) { var c = grab.AddComponent<MeshCollider>(); c.convex = true; c.isTrigger = cols.Count > 0 ? cols[0].isTrigger : true; cols.Add(c); }
        cols[0].sharedMesh = null; cols[0].sharedMesh = full; cols[0].convex = true; cols[0].enabled = true;
        cols[1].sharedMesh = null; cols[1].sharedMesh = small; cols[1].convex = true; cols[1].isTrigger = cols[0].isTrigger; cols[1].enabled = false;
        seed.grabFull = cols[0]; seed.grabSmall = cols[1];
        foreach (var c in cols) EditorUtility.SetDirty(c);
    }

    static void FillIceToyRest(Transform slot, GameObject toy, Material iceM, FuwaIceSkate iceSrc)
    {
        // 2026-11-23 てじゃ「こおりのたねがピックアップ判定が出にくい」：この当たり（ふつうのレイヤー）が持つ判定（Grab、結晶の形の柱）とほぼ同じ大きさで、持つためのレイがこちらに当たっていた → 結晶の内側まで小さく
        var box = toy.AddComponent<BoxCollider>(); box.center = new Vector3(0f, 0.45f, 0f); box.size = new Vector3(0.4f, 0.9f, 0.4f);
        var it = UdonSharpUndo.AddComponent<FuwaPlaygroundIceToy>(toy);
        var dm = IceDiscMesh();
        dm = SaveGenMesh(dm, "Playground_IceDisc");
        var disc = new GameObject("IceDisc"); disc.transform.SetParent(slot, false);
        disc.AddComponent<MeshFilter>().sharedMesh = dm; disc.AddComponent<MeshRenderer>().sharedMaterial = iceM;
        var mc = disc.AddComponent<MeshCollider>(); mc.sharedMesh = dm;
        var pm = AssetDatabase.LoadAssetAtPath<PhysicMaterial>("Assets/FuwaCourse/Materials/Playground_Ice.physicMaterial");
        if (pm == null) { pm = new PhysicMaterial("Playground_Ice") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicMaterialCombine.Minimum, bounciness = 0f }; AssetDatabase.CreateAsset(pm, "Assets/FuwaCourse/Materials/Playground_Ice.physicMaterial"); }
        mc.sharedMaterial = pm;
        disc.transform.localScale = new Vector3(0.001f, 1f, 0.001f); disc.SetActive(false);
        it.disc = disc.transform; it.discCollider = mc; it.discRadius = IceToyR;
        it.crystal = toy.transform; it.crystalCollider = box;   // 氷が出ている間は結晶を小さく・当たりなし（2026-11-27）
        UdonSharpEditorUtility.CopyProxyToUdon(it);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(it); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; ub.interactText = "こおり"; EditorUtility.SetDirty(ub); } }
        // 人が滑る：コース2–3 の IceSkate を写して、氷＝この円だけ
        var sk = Object.Instantiate(iceSrc.gameObject, slot, false); sk.name = "IceSkate"; sk.transform.localPosition = Vector3.zero;
        var skc = sk.GetComponent<FuwaIceSkate>(); skc.ice = new Collider[] { mc }; skc.debugLog = false; UdonSharpEditorUtility.CopyProxyToUdon(skc);
    }

    // 2–4 プロペラ＋歯車：柱の上のプロペラ（コース2–4 の風車の羽根）をエアガンで撃つと回り、床の歯車（大20枚・小12枚、コース2–4 の乗る歯車を小さく）がいっしょに回る（同期）
    public static float GearToyScale = 0.28f, GearToyRatio = 1f, GearToyLift = 0.55f;   // かさ歯車 1:1 なので小さい歯車はプロペラと同じ速さ
    // 2026-11-19 てじゃ「歯車の影がめっちゃチラつく」：回る歯車の歯の影が影の地図の細かさで点滅していた → 歯車本体は影を出さず、
    //  回らない円盤（影だけ、ShadowsOnly）を同じ所に置いて丸い影にする
    public static void GearShadowProxies(Transform root, Transform[] gears)
    {
        foreach (var old in root.Cast<Transform>().Where(t => t.name.StartsWith("GearShadow_")).ToArray()) Object.DestroyImmediate(old.gameObject);
        foreach (var g in gears)
        {
            if (!g) continue;
            var r = g.GetComponent<MeshRenderer>(); if (!r) continue;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var b = r.bounds; var mb = g.GetComponent<MeshFilter>().sharedMesh.bounds;   // 回っている歯車の AABB は斜めでふくらむので、メッシュの大きさから
            float d = Mathf.Max(mb.size.x, mb.size.z) * g.lossyScale.x * 0.92f, h = Mathf.Max(0.02f, mb.size.y * g.lossyScale.y);
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); c.name = "GearShadow_" + g.name; Object.DestroyImmediate(c.GetComponent<Collider>());
            c.transform.SetParent(root, true); c.transform.position = b.center; c.transform.rotation = root.rotation;
            var ls = root.lossyScale; c.transform.localScale = new Vector3(d / ls.x, h * 0.5f / ls.y, d / ls.z);
            var mr = c.GetComponent<MeshRenderer>(); mr.sharedMaterial = r.sharedMaterial; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly; mr.receiveShadows = false;
        }
    }
    [MenuItem("FuwaCourse/あそびば/歯車のおもちゃの影を丸い影に")]
    static void MenuGearShadows()
    {
        var u9 = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "Unlock9" && t.parent && t.parent.name == "Toys");
        var root = u9.Find("Body/GearToy");
        GearShadowProxies(root, new[] { root.Find("GearBig"), root.Find("GearSmall") });
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(u9.gameObject.scene);
        Debug.Log("[あそびば] 歯車の影を丸い影に");
    }

    static void FillWindmill(Transform slot)
    {
        EnsureProgram("FuwaPlaygroundWindmill");
        var wm = Object.FindObjectsOfType<FuwaWindmill>(true).First();
        var gr = Object.FindObjectsOfType<FuwaGearRide>(true).First().transform.parent;
        var big = gr.GetComponentsInChildren<MeshFilter>(true).First(m => m.sharedMesh.name == "GearRide_z20_t60");
        var small = gr.GetComponentsInChildren<MeshFilter>(true).First(m => m.sharedMesh.name == "GearRide_z12_t70");
        // 2026-11-16 てじゃ「プロペラの歯車、ちょっと浮かせよう 下は軸伸ばして」→ 歯車の上の面を GearToyLift、下は軸（床まで）
        float sc = GearToyScale, top = GearToyLift;
        Vector3 O = slot.position, F = slot.forward;
        float pitchBig = 0.4f * 20f / 2f, pitchSmall = 0.4f * 12f / 2f, dist = (pitchBig + pitchSmall) * sc;
        Vector3 cBig = O - F * 0.8f, cSmall = cBig + F * dist;
        var root = new GameObject("GearToy").transform; root.SetParent(slot, false);
        Transform Gear(string nm, MeshFilter src, Vector3 c)
        {
            var g = new GameObject(nm); g.transform.SetParent(root, false);
            var b = src.sharedMesh.bounds;
            g.transform.position = c + Vector3.up * (top - b.max.y * sc); g.transform.localScale = Vector3.one * sc;
            g.AddComponent<MeshFilter>().sharedMesh = src.sharedMesh; g.AddComponent<MeshRenderer>().sharedMaterials = src.GetComponent<MeshRenderer>().sharedMaterials;
            var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = src.sharedMesh;
            var rb = g.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
            return g.transform;
        }
        var gB = Gear("GearBig", big, cBig); var gS = Gear("GearSmall", small, cSmall);
        GearShadowProxies(root, new[] { gB, gS });
        foreach (var (gt, rr) in new[] { (gB, 0.12f), (gS, 0.09f) })
        {   // 床から歯車の下までの軸（歯車といっしょに回る）
            var ax = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ax.name = "Axle"; Object.DestroyImmediate(ax.GetComponent<Collider>());
            ax.transform.SetParent(gt, true); float h = top - 0.015f;   // 2026-11-16 てじゃ「ギア中心にZファイト」：軸の上の面が歯車の上の面と同じ高さだった → 1.5cm 下で止める
            ax.transform.position = new Vector3(gt.position.x, O.y + h * 0.5f, gt.position.z); ax.transform.rotation = root.rotation; ax.transform.localScale = new Vector3(rr * 2f / sc, h * 0.5f / sc, rr * 2f / sc);
            ax.GetComponent<MeshRenderer>().sharedMaterial = wm.rotor.Find("Hub").GetComponent<MeshRenderer>().sharedMaterial;
            var foot = GameObject.CreatePrimitive(PrimitiveType.Cylinder); foot.name = "AxleFoot"; Object.DestroyImmediate(foot.GetComponent<Collider>()); foot.transform.SetParent(root, true);
            foot.transform.position = new Vector3(gt.position.x, O.y + 0.04f, gt.position.z); foot.transform.localScale = new Vector3(rr * 4f, 0.04f, rr * 4f);
            foot.GetComponent<MeshRenderer>().sharedMaterial = wm.rotor.Find("Hub").GetComponent<MeshRenderer>().sharedMaterial;
        }
        // 2026-11-16 てじゃ「歯車、プロペラとどう繋がってるのこれ」→「はね、それじゃないやつ」→ コース2–4 の回転床の羽根車（たての羽根6枚＋マスト、横から撃つ）を
        //  小さい歯車の軸の上にじかに：羽根車 → 小さい歯車 → 大きい歯車（12:20）。撃った所と向きで回る向きが決まる
        var tt = Object.FindObjectsOfType<FuwaTurnTable>(true).First();
        var wGo = new GameObject("Windmill"); wGo.transform.SetParent(root, false);
        wGo.transform.position = cSmall + Vector3.up * (top + 0.6f); wGo.transform.rotation = root.rotation;
        var rotor = new GameObject("Rotor"); rotor.transform.SetParent(wGo.transform, false);
        foreach (Transform ch in tt.transform)
        {
            if (ch.GetComponent<AudioSource>() != null) continue;
            var cp = Object.Instantiate(ch.gameObject, rotor.transform, false); cp.name = ch.name;
            foreach (var cc in cp.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(cc);
            foreach (var ub in cp.GetComponentsInChildren<UdonSharpBehaviour>(true)) Object.DestroyImmediate(ub);
        }
        var axle = GameObject.CreatePrimitive(PrimitiveType.Cylinder); axle.name = "Axle"; Object.DestroyImmediate(axle.GetComponent<Collider>()); axle.transform.SetParent(rotor.transform, false);
        axle.transform.localPosition = new Vector3(0f, -0.3f, 0f); axle.transform.localScale = new Vector3(0.14f, 0.3f, 0.14f);
        axle.GetComponent<MeshRenderer>().sharedMaterial = tt.transform.Find("Hub").GetComponent<MeshRenderer>().sharedMaterial;
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        var hit = wGo.AddComponent<SphereCollider>(); hit.isTrigger = true; hit.radius = 1.0f; wGo.layer = 2;
        var pw = UdonSharpUndo.AddComponent<FuwaPlaygroundWindmill>(wGo);
        pw.rotor = rotor.transform; pw.gears = new[] { gS, gB };
        pw.gearRatio = new[] { -GearToyRatio, GearToyRatio * 12f / 20f };   // 歯車は Euler(0,-a)、羽根車は Euler(0,+turn) なので、小さい歯車はマイナスで羽根車と同じ向き
        pw.gearPhase = new[] { 0f, 0f };
        pw.hitRadius = tt.hitRadius; pw.hitHalfHeight = tt.hitHalfHeight; pw.hitCone = tt.hitCone; pw.spinPerShot = 140f; pw.maxSpin = 260f; pw.drag = 0.4f;
        var click = wm.transform.Find("SfxClick");   // 撃った時のカチッ（コース2–4 の風車の音）
        if (click != null) { var a = Object.Instantiate(click.gameObject, wGo.transform, false); a.name = "SfxClick"; a.transform.localPosition = Vector3.zero; pw.clickSound = a.GetComponent<AudioSource>(); if (pw.clickSound != null) pw.clickSound.playOnAwake = false; }
        UdonSharpEditorUtility.CopyProxyToUdon(pw);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(pw); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; EditorUtility.SetDirty(ub); } }
        // 2026-11-19 てじゃ「ギアおもちゃのデフォの向き180度かえといて」：置き場所のまん中を軸にまるごと回す
        root.RotateAround(O, Vector3.up, GearToyYaw);
    }
    public static float GearToyYaw = 180f;

    // 2–5 ポータル2つ1組：ひとつはおもちゃの場所（柵ぎわ、島の真ん中を向く）、もうひとつは島の反対がわの草地のふち（PortalBAngle）。表から入ると、もう片方の表から出る（人もふわふわも）
    public static float PortalR = 1.1f, PortalBAngle = 108f, PortalBDist = 6.6f;
    static void FillPortals(Transform slot, Vector3 O)
    {
        EnsureProgram("FuwaPlaygroundPortals");
        var swirlSh = Shader.Find("Fuwa/PortalSwirl");
        // 2026-11-16 てじゃ「ポータル、コースと見た目揃えて」→ コース2–5 のポータル（FloatFrame＝浮いたふち・足もとの光・粒）をそのまま写す。穴の中は向こうを映すカメラの代わりにうずまき
        var srcW = RootGo("FuwaCourse2_5").GetComponentsInChildren<Transform>(true).First(t => t.name == "WarpA");
        var srcFrame = srcW.Find("FloatFrame"); var srcHole = srcW.Find("PortalHole");
        // 2026-11-16 てじゃ「ポータルも下に掴めるモノつけたいね」→ 下に石の台（持ち手、VRCPickup＋ObjectSync）。ポータルはその子で、いっしょに運べる
        EnsureProgram("FuwaPlaygroundPortalHandle");
        var stoneM = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Ruins_Stone.mat");
        Transform Portal(string nm, Vector3 floor, Vector3 face, Color col)
        {
            var hd = new GameObject(nm + "Handle"); hd.transform.SetParent(slot, false); hd.layer = 13;
            hd.transform.SetPositionAndRotation(floor, Quaternion.LookRotation(face));
            var basePart = GameObject.CreatePrimitive(PrimitiveType.Cylinder); basePart.name = "Base"; Object.DestroyImmediate(basePart.GetComponent<Collider>());
            basePart.transform.SetParent(hd.transform, false); basePart.transform.localPosition = new Vector3(0f, 0.09f, 0f); basePart.transform.localScale = new Vector3(0.9f, 0.09f, 0.6f);
            basePart.GetComponent<MeshRenderer>().sharedMaterial = stoneM;
            var hb = hd.AddComponent<BoxCollider>(); hb.center = new Vector3(0f, 0.12f, 0f); hb.size = new Vector3(0.95f, 0.26f, 0.65f);
            { var rbh = hd.AddComponent<Rigidbody>(); rbh.isKinematic = true; rbh.useGravity = false; }
            var pk = hd.AddComponent<VRC.SDK3.Components.VRCPickup>(); pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any; pk.InteractionText = "ポータル"; pk.proximity = 2f;
            hd.AddComponent<VRC.SDK3.Components.VRCObjectSync>();
            var ph = UdonSharpUndo.AddComponent<FuwaPlaygroundPortalHandle>(hd); UdonSharpEditorUtility.CopyProxyToUdon(ph);
            { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(ph); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); } }
            var p = new GameObject(nm).transform; p.SetParent(hd.transform, false);
            p.localPosition = new Vector3(0f, 0.18f, 0f); p.localRotation = Quaternion.identity;
            var fr = Object.Instantiate(srcFrame.gameObject, p, false); fr.name = "FloatFrame"; fr.transform.localPosition = srcFrame.localPosition; fr.transform.localRotation = srcFrame.localRotation;
            foreach (var ub in fr.GetComponentsInChildren<UdonSharpBehaviour>(true)) Object.DestroyImmediate(ub);
            foreach (var cc in fr.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(cc);
            var hole = new GameObject("Swirl", typeof(MeshFilter), typeof(MeshRenderer)); hole.transform.SetParent(p, false);
            hole.transform.localPosition = srcHole.localPosition; hole.transform.localRotation = srcHole.localRotation;
            hole.GetComponent<MeshFilter>().sharedMesh = srcHole.GetComponent<MeshFilter>().sharedMesh;
            string mp = "Assets/FuwaCourse/Materials/Playground/Playground_PortalSwirl_" + ColorUtility.ToHtmlStringRGB(col) + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(mp); if (m == null) { m = new Material(swirlSh); AssetDatabase.CreateAsset(m, mp); }
            var hv = hole.GetComponent<MeshFilter>().sharedMesh.bounds;
            m.shader = swirlSh; m.SetColor("_Color", col); m.SetFloat("_HalfW", hv.extents.x); m.SetFloat("_HalfH", hv.extents.y); m.SetFloat("_Alpha", 0.55f); EditorUtility.SetDirty(m);
            var dr = hole.GetComponent<MeshRenderer>(); dr.sharedMaterial = m; dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PortalBack(p);
            foreach (var t in p.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            DetachPortalFx(p);
            return p;
        }
        var pa = Portal("PortalA", slot.position, slot.forward, new Color(0.35f, 0.75f, 1f));
        var bFloor = O + OnRing(PortalBAngle, PortalBDist); bFloor.y = slot.position.y - DeckH;
        var bFace = O - bFloor; bFace.y = 0f;
        var pb = Portal("PortalB", bFloor, bFace.normalized, new Color(0.35f, 0.75f, 1f));   // コースと同じく2つとも同じ色
        // 2026-11-23 てじゃ「おもちゃポータルの反対側の出現位置も調整したい」「きれいなのは（2つ並べる）」→ はじめは2つをデッキに 2.8m 間隔で並べる（どちらもまん中向き、Unlock10 の座標）
        if (PortalPair)
        {
            void Put(Transform portal, Vector3 lp, Quaternion lr)
            {
                var hd = portal.parent; hd.SetPositionAndRotation(slot.TransformPoint(lp), slot.rotation * lr);
                var fx = slot.Find(portal.name + "_FX"); if (fx) fx.SetPositionAndRotation(portal.position, portal.rotation);
            }
            Put(pa, new Vector3(1.66506708f, 0f, 0.1341604f), new Quaternion(0f, -0.08031078f, 0f, 0.996769965f));
            Put(pb, new Vector3(-1.12552226f, 0f, 0.06108308f), new Quaternion(0f, 0.0541912727f, 0f, 0.9985307f));
        }
        var go = new GameObject("Portals"); go.transform.SetParent(slot, false);
        var pp = UdonSharpUndo.AddComponent<FuwaPlaygroundPortals>(go);
        { var hv = srcHole.GetComponent<MeshFilter>().sharedMesh.bounds; pp.halfW = hv.extents.x; pp.halfH = hv.extents.y; pp.holeCenterY = srcHole.localPosition.y; }
        pp.a = pa; pp.b = pb; pp.spawner = Object.FindObjectsOfType<FuwaPlaygroundSpawner>(true).First();
        UdonSharpEditorUtility.CopyProxyToUdon(pp);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(pp); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); } }
    }

    // 2026-11-17 てじゃ「遊び場のポータル、裏面は使えないのわかりやすい用にしたい」
    //  穴の裏に石のふた（穴と同じだ円）。表からは穴の景色に隠れて見えず、裏から見ると石でふさがっている
    // 2026-11-23 てじゃ「フチにびみょーに隙間ある」：小さめ（0.985）で 3cm 奥だったので、ななめから見ると輪との間がすけていた → 穴と同じ大きさで 5mm 奥
    public static float PortalBackInset = 0.005f, PortalBackScale = 1f;
    public static Color PortalBackColor = new Color(0.2f, 0.19f, 0.26f);   // 2026-11-17 てじゃ「もっと黒っぽいほうがいいかも」
    public static void PortalBack(Transform p)
    {
        var old = p.Find("Back"); if (old) Object.DestroyImmediate(old.gameObject);
        var hole = p.Find("Swirl"); if (!hole) return;
        var g = new GameObject("Back", typeof(MeshFilter), typeof(MeshRenderer)); g.transform.SetParent(p, false);
        // 穴は +z 向き（Swirl は 180 度まわしてある）。ふたは回さずに置くと -z（裏）を向く
        g.transform.localPosition = hole.localPosition - Vector3.forward * PortalBackInset;
        g.transform.localRotation = Quaternion.identity;
        g.transform.localScale = Vector3.one * PortalBackScale;
        g.GetComponent<MeshFilter>().sharedMesh = hole.GetComponent<MeshFilter>().sharedMesh;
        var r = g.GetComponent<MeshRenderer>();
        r.sharedMaterial = PortalBackSoftMat(hole.GetComponent<MeshRenderer>().sharedMaterial) ?? PortalBackMat();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        GameObjectUtility.SetStaticEditorFlags(g, 0);
    }

    // 2026-11-17 てじゃ「インタラクト時のハイライトにデカい四角があるのはどうしようもないかな」
    //  持ち手（VRCPickup）の下にある描画物は全部ハイライトされるので、四角い板で描いている光（Rim・GroundGlow・Sparkles＝FloatFrame）を
    //  持ち手の外に出し、FuwaFollowPose でポータルについていかせる（PostLateUpdate で合わせるので持っている時も遅れない）
    public static void DetachPortalFx(Transform p)
    {
        var hd = p.parent; if (!hd) return;
        string fxName = p.name + "_FX";
        var old = hd.parent.Find(fxName);
        var fr = p.Find("FloatFrame");
        if (!fr && old) fr = old.Find("FloatFrame");
        if (!fr) return;
        var fx = old ? old : new GameObject(fxName).transform;
        if (!old) fx.SetParent(hd.parent, false);
        fx.SetPositionAndRotation(p.position, p.rotation); fx.localScale = Vector3.one;
        var lp = fr.localPosition; var lr = fr.localRotation;
        fr.SetParent(fx, false); fr.localPosition = lp; fr.localRotation = lr;
        // 足もとの光の模様は、台座を盛り上がった形にしたら突き抜けるので、あそびばのはなし（2026-11-27 てじゃ「遊び場のはなくてもいいかも」）
        { var gg = fr.Find("GroundGlow"); if (gg) Object.DestroyImmediate(gg.gameObject); }
        { var pc = fx.GetComponent<UnityEngine.Animations.ParentConstraint>(); if (pc) Object.DestroyImmediate(pc); }
        EnsureProgram("FuwaFollowPose");
        var fp = fx.GetComponent<FuwaFollowPose>(); if (!fp) fp = UdonSharpUndo.AddComponent<FuwaFollowPose>(fx.gameObject);
        fp.follow = p; UdonSharpEditorUtility.CopyProxyToUdon(fp);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(fp); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); } }
        GameObjectUtility.SetStaticEditorFlags(fx.gameObject, 0);
    }

    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/ポータルの光を持ち手の外へ（ハイライトの四角よけ）")]
    public static void MenuDetachPortalFx()
    {
        int n = 0;
        foreach (var pp in Object.FindObjectsOfType<FuwaPlaygroundPortals>(true))
            foreach (var t in new[] { pp.a, pp.b }) if (t) { DetachPortalFx(t); n++; }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[あそびば] ポータルの光を外へ " + n);
    }

    // 2026-11-23 てじゃ「裏の真っ黒板、わかりやすいんだけど なんか怖い」→ 表と同じうずまき（Fuwa/PortalCam の待ち状態 _Active 0）を白っぽいラベンダーで
    static Material PortalBackSoftMat(Material swirl)
    {
        // てじゃ「裏も渦だと分かりづらい」→ うずなしの Fuwa/PortalBackSoft（ラベンダーのグラデ＋またたく星）
        var sh = Shader.Find("Fuwa/PortalBackSoft"); if (sh == null) return null;
        string mp = MatDir + "/Playground_PortalBackSoft.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null) { m = new Material(sh) { name = "Playground_PortalBackSoft" }; AssetDatabase.CreateAsset(m, mp); }
        m.shader = sh;
        var hm = Object.FindObjectsOfType<MeshFilter>(true).FirstOrDefault(f => f.name == "Swirl" && f.sharedMesh);
        if (hm) { m.SetFloat("_HalfW", hm.sharedMesh.bounds.extents.x); m.SetFloat("_HalfH", hm.sharedMesh.bounds.extents.y); }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material PortalBackMat()
    {
        string mp = MatDir + "/Playground_PortalBack.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null) { m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Ruins_Stone.mat")); AssetDatabase.CreateAsset(m, mp); }
        m.SetColor("_Color", PortalBackColor); EditorUtility.SetDirty(m);
        return m;
    }

    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/ポータルの裏に石のふた")]
    public static void MenuPortalBacks()
    {
        int n = 0;
        foreach (var pp in Object.FindObjectsOfType<FuwaPlaygroundPortals>(true))
        {
            foreach (var t in new[] { pp.a, pp.b }) if (t) { PortalBack(t); n++; }
            // 2026-11-23 作り直した裏の板をポータルのカメラの「隠す裏の板」につなぎ直す（つながっていないと、のぞいた先に出口の裏の板が映る。てじゃ「裏が前から見えるようになった」）
            Renderer Back(Transform p) { var b = p ? p.Find("Back") : null; return b ? b.GetComponent<Renderer>() : null; }
            foreach (var pc in pp.GetComponentsInChildren<FuwaPortalCams>(true)) { pc.holeBacks = new[] { Back(pp.a), Back(pp.b) }; UdonSharpEditorUtility.CopyProxyToUdon(pc); EditorUtility.SetDirty(pc); }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[あそびば] ポータルの裏のふた " + n);
    }

    static void AddTestUnlockAll(Transform toys, FuwaPlaygroundSpawner sp)
    {
        foreach (var n in new[] { "TestUnlockAll", "TestUnlockAllButton" }) { var old = toys.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject); }
        EnsureProgram("FuwaTestUnlockAll");
        var go = new GameObject("TestUnlockAll"); go.transform.SetParent(toys, false);
        var tu = UdonSharpUndo.AddComponent<FuwaTestUnlockAll>(go); tu.spawner = sp;
        // テレビの裏の柵ぎわ（マスターにしか見えないし、公開前に消すので目立たない所。2026-10-13 てじゃ「全部出すは他所においとこ」）
        var reset = toys.Find("ResetToysButton");
        Vector3 O = toys.parent.position; Vector3 pos = O + OnRing(LivingAngle, IslandR - 0.9f) + Vector3.up * (reset.position.y - O.y);
        Vector3 outD = pos - O; outD.y = 0f;
        LaserButton(toys, "TestUnlockAllButton", pos, Quaternion.LookRotation(outD.normalized), 1.3f, "テスト おもちゃぜんぶだす", tu, "UnlockAll");
        tu.button = toys.Find("TestUnlockAllButton").gameObject;
        tu.ownerNames = new[] { "Teja" };   // マスターかつこの表示名だけ（保険、2026-10-14 てじゃ）
        UdonSharpEditorUtility.CopyProxyToUdon(tu);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(tu); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
    }

    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/床と柵だけ作り直す")]
    static void MenuFenceDeck()
    {
        var coop = RootGo(RootName).transform; var root = coop.Find("Room");
        var floor = root.Find("Floor"); float floorY = floor.GetComponent<Renderer>().bounds.max.y;
        Debug.Log(Fence(root, coop.position, floorY));
        Debug.Log(Deck(root, coop.position, floorY, floor.gameObject.layer));
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(coop.gameObject.scene);
    }

    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/テスト用：おもちゃ全部出すボタンを置く")]
    static void MenuAddTestUnlockAll()
    {
        var toys = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "Toys" && t.parent != null && t.parent.name == "Room");
        AddTestUnlockAll(toys, Object.FindObjectsOfType<FuwaPlaygroundSpawner>(true).First());
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(toys.gameObject.scene);
        Debug.Log("test unlock button placed");
    }

    // ---------- 見た目（2026-10-12 てじゃ「B（空のリビング）」「モニターもふわふわのガワ、画面だけはめ込む、ボタンも」）----------
    // 家具は Blender（FLUFF_models.blend の ChillRoom シーン）→ Models/PlaygroundProps.fbx。Blender の正面（-Y）が Unity の +Z。
    // FBX の中のメッシュ名は Blender のオブジェクト名（Chill_〇〇）のまま（変えると読み込み直しでシーンの参照が外れる）
    const string PropsPath = "Assets/FuwaCourse/Models/PlaygroundProps.fbx";
    const string MatDir = "Assets/FuwaCourse/Materials/Playground";

    static string Decorate(Transform root, Vector3 ac, float floorY, Transform monitorHolder)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Materials", "Playground");
        var meshes = AssetDatabase.LoadAllAssetsAtPath(PropsPath).OfType<Mesh>().ToDictionary(m => m.name);
        var deco = new GameObject("Deco").transform; deco.SetParent(root, false);
        Vector3 C = new Vector3(ac.x, floorY, ac.z) + LivingLocal;            // 観戦リビングの真ん中
        Vector3 dir = new Vector3(LivingLocal.x, 0f, LivingLocal.z).normalized; // リビングの奥（テレビの方）
        Vector3 perp = new Vector3(dir.z, 0f, -dir.x);

        // 床は芝に（ふちの輪は濃い芝）
        var grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Goal_Ground.mat");
        root.Find("Floor").GetComponent<Renderer>().sharedMaterial = grass;
        root.Find("Ring").GetComponent<Renderer>().sharedMaterial = Mat("Playground_GrassRim", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(0.47f, 0.68f, 0.43f));

        var cloud = Mat("Playground_Cloud", "Assets/FuwaCourse/Generated/Signs/Sign_Cloud_FFFAF5.mat", new Color(0.99f, 0.98f, 1f));
        // ラグ：床で真上から日が当たるので、看板の雲と同じ色でも真っ白に飛んでまぶしかった（2026-10-14 てじゃ）→ 色・環境光を下げて看板の雲くらいの白に（赤みを抜いて青白い雲寄り。てじゃ「肌色っぽいかも」）
        var rugM = Mat("Playground_Rug", "Assets/FuwaCourse/Generated/Signs/Sign_Cloud_FFFAF5.mat", new Color(0.88f, 0.87f, 0.86f));
        rugM.SetFloat("_UnderStrength", 0f); rugM.SetFloat("_AmbientStrength", 0.7f); EditorUtility.SetDirty(rugM);   // 雲の下の青い影はいらない
        var stemM = Mat("Playground_LampStem", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(0.96f, 0.93f, 0.86f));
        var capM = Mat("Playground_LampCap", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(1f, 0.82f, 0.55f));
        capM.SetColor("_EmissionColor", new Color(0.9f, 0.6f, 0.25f)); EditorUtility.SetDirty(capM);
        var bean = new[] { new Color(1f, 0.72f, 0.82f), new Color(0.62f, 0.83f, 1f), new Color(1f, 0.9f, 0.55f), new Color(0.68f, 0.92f, 0.72f) }
            .Select((c, i) => Mat("Playground_Beanbag" + i, "Assets/FuwaCourse/Materials/Goal_Ground.mat", c)).ToArray();
        int floorLayer = root.Find("Floor").gameObject.layer;

        GameObject Put(string mesh, Material m, Vector3 pos, Quaternion rot, bool collider, int layer = 0, string name = null)
        {
            var g = new GameObject(name ?? mesh.Replace("Chill_", ""), typeof(MeshFilter), typeof(MeshRenderer));
            g.transform.SetParent(deco, false); g.transform.SetPositionAndRotation(pos, rot);
            g.GetComponent<MeshFilter>().sharedMesh = meshes[mesh];
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (collider) g.AddComponent<MeshCollider>().sharedMesh = meshes[mesh];
            g.layer = layer;
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
            return g;
        }
        Quaternion Face(Vector3 from, Vector3 to) { var d = to - from; d.y = 0f; return Quaternion.LookRotation(d.normalized); }

        // ラグ（床と同じレイヤー＝ふわふわが落ちたらはじける床として扱う）。リビングの広さに合わせて少し小さく
        var rugG = Put("Chill_Rug", rugM, C + Vector3.up * 0.002f, Quaternion.identity, true, floorLayer);
        rugG.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        FuwaPlaygroundRugEdge.Apply(rugG);   // ふちにほんのり青い影のグラデ（2026-10-14 てじゃ）
        // 雲のソファ：リビングの手前側、テレビの方を向く（ソファの正面は +Z、弧の中心は前 4m＝テレビのあたり）
        var sofaPos = C - dir * 1.7f;
        Put("Chill_CloudSofa", cloud, sofaPos, Face(sofaPos, C + dir * 3f), true, 17);   // ふわふわだけ当たって人はすり抜ける（Walkthrough。2026-10-13 てじゃ）
        // ビーズクッション：ソファの両わき2つと、テレビ寄りの両わき2つ（テレビの方を向く）
        var beanPos = new[] { C - dir * 0.9f - perp * 2.6f, C - dir * 0.9f + perp * 2.6f, C + dir * 0.9f - perp * 2.3f, C + dir * 0.9f + perp * 2.3f };
        for (int i = 0; i < beanPos.Length; i++)
        {
            var p = beanPos[i];
            Put("Chill_Beanbag", bean[i], p, Face(p, C + dir * 3f) * Quaternion.Euler(0f, (i % 2 == 0 ? 12f : -12f), 0f), true, 17, "Beanbag" + i);   // ふわふわだけ当たって人はすり抜け（Walkthrough。2026-10-13 てじゃ「すりぬけてほしい」）
        }
        // キノコのランプ：ソファの後ろの両わき（2026-11-19 てじゃ「あそびばの2本あるスタンドライトいらんかも」→ PlaygroundLamps=false で置かない）
        if (PlaygroundLamps) foreach (var lp in new[] { C - dir * 2.7f - perp * 2.3f, C - dir * 2.7f + perp * 2.3f })
        {
            var lamp = new GameObject("Lamp").transform; lamp.SetParent(deco, false); lamp.position = lp;
            var s = Put("Chill_LampStem", stemM, lamp.position, Quaternion.identity, false); s.transform.SetParent(lamp, true);
            var c = Put("Chill_LampCap", capM, lamp.position, Quaternion.identity, false); c.transform.SetParent(lamp, true);
            // ランプ：ふわふわだけ当たって人はすり抜ける（Walkthrough。2026-10-13 てじゃ）
            lamp.gameObject.layer = 17;
            var col = lamp.gameObject.AddComponent<CapsuleCollider>(); col.center = new Vector3(0f, 0.65f, 0f); col.height = 1.3f; col.radius = 0.14f;
        }

        // 雲のテレビ：コースの看板と同じ雲（FuwaSignStyle の Scallop＋Pillow、白い雲＋後ろに一回り大きい色の雲）に画面をはめ込む
        //（2026-10-12 てじゃ「ソファとかモニタ、もっと波少なくていいかも、コースの看板みたいな」）。雲の前の平らな所に画面・題名・ボタン
        if (monitorHolder != null)
        {
            // 雲が床に埋まらないよう、モニター一式を少し持ち上げる（カメラの球は元の高さのまま）
            var zoneT = monitorHolder.Find("CamZone"); var zonePos = zoneT != null ? zoneT.position : Vector3.zero;
            monitorHolder.position += Vector3.up * 0.35f;
            if (zoneT != null) zoneT.position = zonePos;
            // カメラの球はテレビの右の柵ぎわへ（2026-10-13 てじゃ「カメラハック用のタマがソファにめりこんでる、柵沿いに」）。向きはテレビとの関係のまま島の外向きに
            if (zoneT != null)
            {
                Vector3 zl = OnRing(CamZoneAngle, IslandR - CamZoneInset), outD = new Vector3(zl.x, 0f, zl.z).normalized;
                var rel = Quaternion.Inverse(monitorHolder.rotation) * zoneT.rotation;
                zoneT.position = new Vector3(ac.x + zl.x, zonePos.y, ac.z + zl.z);
                zoneT.rotation = Quaternion.LookRotation(outD);   // 文字が島の真ん中から正面に読める向き（写真館との向きの関係を引き継ぐと横向きで斜めに見えた。2026-10-13 てじゃ「カメラ球の向きが変」）
            }
            var mon = monitorHolder.Find("Monitor");
            var scr = mon.Find("Screen");
            var sl = monitorHolder.InverseTransformPoint(scr.position);
            const float sw = 1.325f, sh = 0.745f;                  // 画面の半分の幅・高さ
            float hw = sw + 0.4f, hh = sh + 0.5f, cy = sl.y - 0.12f; // 雲：上は題名、下はボタンのぶん広く
            const float amp = 0.1f, lambda = 1.25f, shape = 0.7f, round = 0.6f, rimW = 0.14f, front = 0.16f, rise = 0.42f;
            float face = sl.z + 0.015f;                            // 雲の前の平らな所（画面のすぐ後ろ）
            var old = monitorHolder.Find("CloudTV"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var tv = new GameObject("CloudTV").transform; tv.SetParent(monitorHolder, false); tv.localPosition = new Vector3(sl.x, cy, 0f);
            var body = FuwaSignStyle.Scallop(FuwaSignStyle.Outline(hw - amp * 0.8f, hh - amp * 0.4f, round, 12, 0.03f), amp, lambda, shape);
            var bodyM = SaveGenMesh(FuwaSignStyle.Pillow(body, hw, hh, face + front, front, 0.14f, rise, 16), "Playground_TVCloud");
            var rim = FuwaSignStyle.Scallop(FuwaSignStyle.Outline(hw - amp * 0.8f + rimW, hh - amp * 0.4f + rimW, round + rimW, 12, 0.03f), amp, lambda, shape);
            var rimM = SaveGenMesh(FuwaSignStyle.Pillow(rim, hw + rimW, hh + rimW, face + front + 0.2f, 0.08f, 0.08f, 0.25f, 8), "Playground_TVCloudRim");
            var tvCloudMat = Mat("Playground_TVCloud", "Assets/FuwaCourse/Generated/Signs/Sign_Cloud_FFFAF5.mat", new Color(0.99f, 0.98f, 1f));
            var rimC = new Color(0.62f, 0.55f, 0.86f);   // 写真館のモニターのふちの紫
            var tvRimMat = Mat("Playground_TVCloudRim", "Assets/FuwaCourse/Generated/Signs/Sign_CloudRim_8C63D5.mat", rimC);
            tvRimMat.SetColor("_UnderColor", FuwaSignStyle.RimUnderColor(rimC)); EditorUtility.SetDirty(tvRimMat);
            foreach (var (n, mesh, mat) in new[] { ("Cloud", bodyM, tvCloudMat), ("CloudRim", rimM, tvRimMat) })
            {
                var g = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer)); g.transform.SetParent(tv, false);
                g.GetComponent<MeshFilter>().sharedMesh = mesh; g.GetComponent<MeshRenderer>().sharedMaterial = mat;
                GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
            }
            // 雲のテレビの当たり判定：ふわふわだけ当たって人はすり抜ける（Walkthrough レイヤー）。ボタンが埋まらないよう、画面・ボタンより奥だけ
            //（2026-10-13 てじゃ「テレビのボタンがコライダーにうまってる」「ふわふわだけ当たるように、プレイヤーは貫通で」）
            var bump = new GameObject("BallBump"); bump.layer = 17; bump.transform.SetParent(tv, false);
            var bc = bump.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0f, face + 0.25f); bc.size = new Vector3((hw + rimW) * 2f, (hh + rimW) * 2f, 0.3f);
            // 元の看板の板・ふちは消す。題名は画面の上、ボタン2つは画面の下（どちらも雲の前の平らな所）
            foreach (var n in new[] { "Panel", "Border" }) { var t = mon.Find(n); if (t != null) t.gameObject.SetActive(false); }
            var title = mon.Find("Title");
            if (title != null) title.position = monitorHolder.TransformPoint(new Vector3(sl.x, sl.y + sh + 0.2f, face - 0.02f));
            var hr = monitorHolder.rotation;
            var tb = monitorHolder.Find("TargetButton"); var cb = monitorHolder.Find("CamButton");
            if (tb != null) { tb.position = monitorHolder.TransformPoint(new Vector3(sl.x - 0.6f, sl.y - sh - 0.24f, face - 0.03f)); tb.rotation = hr; }
            // カメラアングルのボタンは右寄せ：右端を画面の右端にそろえる（2026-10-13 てじゃ「もうちょい右に」で +0.9 → 2026-10-15「右端がモニタの端とあうように」で +0.82。左の人物ボタンは左端がそろっている）
            if (cb != null) { cb.position = monitorHolder.TransformPoint(new Vector3(sl.x + 0.82f, sl.y - sh - 0.24f, face - 0.03f)); cb.rotation = hr; }
        }
        return "deco ok";
    }

    // 竜巻のたね（持てる玉）＋竜巻（コース1 のつむじ風 Whirl0 のコピー、捕まえない普通の巻き上げに）
    // 地面のうずまき：写し元（かぜのおかのつむじ風）は道の幅で切った形のメッシュなので、あそびばの床だと片側が欠けて見えた
    //（2026-10-16 てじゃ「遊び場のたつまきの地面エフェクトが欠けてる」）→ 練習のつむじ風と同じ丸ごとの板（直径2.8m）にして、床から少し浮かせる
    static void FullFloorSwirl(Transform tornado)
    {
        var fs = tornado.Find("FloorSwirl"); if (fs == null) return;
        var quad = Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == "FloorSwirl" && t.parent != null && t.parent.name == "WhirlPractice");
        if (quad == null) return;
        fs.GetComponent<MeshFilter>().sharedMesh = quad.GetComponent<MeshFilter>().sharedMesh;
        var r = fs.GetComponent<MeshRenderer>();
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/WhirlFloor_Rev.mat");
        if (r.sharedMaterial != null && r.sharedMaterial.name.EndsWith("_S")) mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/WhirlFloor.mat") ?? mat;
        r.sharedMaterial = mat;
        fs.localPosition = new Vector3(0f, 0.012f, 0f);
        fs.localScale = new Vector3(2.8f, 1f, 2.8f);
        EditorUtility.SetDirty(fs.gameObject); EditorUtility.SetDirty(r);
    }

    static void FillTornado(Transform slot, FuwaPlaygroundSpawner sp)
    {
        EnsureProgram("FuwaTornadoSeed");
        var src = RootGo("FuwaCourse").GetComponentsInChildren<FuwaWhirlwind>(true).First(w => w.name == "Whirl0");
        var tw = Object.Instantiate(src.gameObject, slot, false); tw.name = "Tornado";
        tw.transform.position = slot.position; tw.transform.rotation = slot.rotation;
        // 動きはかぜのおかのつむじ風（Whirl0）のまま＝捕まえて、溜め撃ちで抜ける（2026-10-13 てじゃ「なるべくコースと同じ動きにしたい」。
        // 前は巻き上げててっぺんから吐き出す形に変えていた）
        Sync(tw.GetComponent<FuwaWhirlwind>());
        FullFloorSwirl(tw.transform);
        foreach (var t in tw.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        // たね：ころんとした水色の玉。竜巻の上に浮かべておく
        var seed = GameObject.CreatePrimitive(PrimitiveType.Sphere); seed.name = "TornadoSeed"; seed.layer = 13;
        seed.transform.SetParent(slot, false); seed.transform.position = slot.position + Vector3.up * 1.5f; seed.transform.localScale = Vector3.one * 0.26f;
        GameObjectUtility.SetStaticEditorFlags(seed, 0);
        seed.GetComponent<MeshRenderer>().sharedMaterial = Mat("Playground_TornadoSeed", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(0.6f, 0.95f, 0.9f));
        var rb = seed.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var pk = seed.AddComponent<VRC.SDK3.Components.VRCPickup>();
        pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any; pk.InteractionText = "竜巻のたね"; pk.proximity = 2f;
        seed.AddComponent<VRC.SDK3.Components.VRCObjectSync>();
        var ts = UdonSharpUndo.AddComponent<FuwaTornadoSeed>(seed); ts.tornado = tw.transform; ts.ground = 1 << 11;
        UdonSharpEditorUtility.CopyProxyToUdon(ts);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(ts); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
    }

    // 投げる岩：コース2 の岩の見た目をコピーして、持って投げると転がる（2026-10-12 てじゃ「投げたら転がるPickup岩」「あたると人もふわふわも吹っ飛ぶ」）。大小2つ
    static void FillBoulder(Transform slot, FuwaPlaygroundSpawner sp, Vector3 G)
    {
        EnsureProgram("FuwaThrowRock");
        var src = RootGo("FuwaCourse2").GetComponentsInChildren<FuwaBoulder>(true).First();
        float srcR = src.radius;
        // コースと同じ大きさの岩を1個だけ（2026-10-13 てじゃ「小さいのいらない、コースと同じ大きさの1個」。前は半径0.36と0.26の2個）
        var sizes = new[] { srcR };
        var offs = new[] { Vector3.zero };
        for (int i = 0; i < sizes.Length; i++)
        {
            float r = sizes[i];
            var rock = new GameObject("ThrowRock" + i); rock.layer = 13; rock.transform.SetParent(slot, false);
            rock.transform.position = slot.position + slot.rotation * offs[i] + Vector3.up * (r + 0.02f);
            var vis = Object.Instantiate(src.visual.gameObject, rock.transform, false); vis.name = "Visual"; vis.transform.localPosition = Vector3.zero;
            vis.transform.localScale = src.visual.localScale * (r / srcR);
            foreach (var t in vis.GetComponentsInChildren<Transform>(true)) { GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0); t.gameObject.layer = 13; }
            foreach (var col in vis.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            var sc = rock.AddComponent<SphereCollider>(); sc.radius = r;
            var pm = AssetDatabase.LoadAssetAtPath<PhysicMaterial>("Assets/FuwaCourse/Materials/Playground_Rock.physicMaterial");
            if (pm == null) { pm = new PhysicMaterial("Playground_Rock") { bounciness = 0.15f, dynamicFriction = 0.5f, staticFriction = 0.6f }; AssetDatabase.CreateAsset(pm, "Assets/FuwaCourse/Materials/Playground_Rock.physicMaterial"); }
            sc.sharedMaterial = pm;
            var rb = rock.AddComponent<Rigidbody>(); rb.mass = 6f; rb.drag = 0.05f; rb.angularDrag = 0.4f; rb.useGravity = true; rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var pk = rock.AddComponent<VRC.SDK3.Components.VRCPickup>();
            pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.No; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any; pk.InteractionText = "いわ"; pk.proximity = 2f; pk.ThrowVelocityBoostScale = 1.3f;
            var os = rock.AddComponent<VRC.SDK3.Components.VRCObjectSync>(); os.AllowCollisionOwnershipTransfer = false;
            AudioSource hit = null;
            if (src.hitSound != null) { var h = Object.Instantiate(src.hitSound.gameObject, rock.transform, false); h.name = "HitSound"; h.transform.localPosition = Vector3.zero; hit = h.GetComponent<AudioSource>(); }
            var tr = UdonSharpUndo.AddComponent<FuwaThrowRock>(rock); tr.spawner = sp; tr.radius = r; tr.hitSound = hit; tr.fallY = slot.position.y - 6f;
            tr.visual = vis.GetComponentInChildren<Renderer>(); tr.heldMaterial = RockHeldMaterial(tr.visual != null ? tr.visual.sharedMaterial : null);
            tr.passThrough = Object.FindObjectsOfType<FuwaHandFan>(true).SelectMany(f => f.GetComponents<Collider>()).ToArray();   // 扇風機・うちわはすり抜ける
            UdonSharpEditorUtility.CopyProxyToUdon(tr);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(tr); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Continuous; EditorUtility.SetDirty(ub); }
        }
    }

    // 持っている間のいわ（半透明。自分の画面だけ）。色はいつもの岩の材質から
    static Material RockHeldMaterial(Material src)
    {
        const string path = "Assets/FuwaCourse/Materials/Playground_RockHeld.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse")); AssetDatabase.CreateAsset(m, path); }
        Color c = src != null && src.HasProperty("_Color") ? src.GetColor("_Color") : new Color(0.6f, 0.55f, 0.5f);
        c.a = 0.35f;
        m.SetColor("_Color", c);
        if (src != null && src.HasProperty("_MainTex")) m.SetTexture("_MainTex", src.GetTexture("_MainTex"));
        EditorUtility.SetDirty(m);
        return m;
    }

    // 持って置ける玉（たね）：VRCPickup＋VRCObjectSync、手を離すとその場に浮く
    static GameObject Seed(Transform slot, string name, string label, Color col, float size, float height)
    {
        var seed = GameObject.CreatePrimitive(PrimitiveType.Sphere); seed.name = name; seed.layer = 13;
        seed.transform.SetParent(slot, false); seed.transform.position = slot.position + Vector3.up * height; seed.transform.localScale = Vector3.one * size;
        GameObjectUtility.SetStaticEditorFlags(seed, 0);
        var m = Mat("Playground_" + name, "Assets/FuwaCourse/Materials/Goal_Ground.mat", col);
        m.SetColor("_EmissionColor", col * 0.35f); EditorUtility.SetDirty(m);
        seed.GetComponent<MeshRenderer>().sharedMaterial = m;
        var rb = seed.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var pk = seed.AddComponent<VRC.SDK3.Components.VRCPickup>();
        pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any; pk.InteractionText = label; pk.proximity = 2f;
        seed.AddComponent<VRC.SDK3.Components.VRCObjectSync>();
        return seed;
    }

    // 重力のたね：紫の玉。まわりにコース3と同じ「重くなる」紫の球のエリア（FuwaGravityArea、落ちるのが速くなる）
    //（2026-10-12 てじゃ「重力、コースと同じ仕様にしよ」「落ちる速度が上がるエリア」）
    static void FillGravity(Transform slot, FuwaPlaygroundSpawner sp)
    {
        const float size = 0.32f, zoneR = 2.2f;
        var seed = Seed(slot, "GravitySeed", "重力のたね", new Color(0.62f, 0.45f, 0.95f), size, 1.6f);
        var src = Object.FindObjectsOfType<FuwaGravityArea>(true).First(a => a.GetComponent<MeshFilter>() != null && a.GetComponent<MeshFilter>().sharedMesh.name == "Sphere");
        var z = Object.Instantiate(src.gameObject, seed.transform, false); z.name = "HeavyZone";
        z.transform.localPosition = Vector3.zero; z.transform.localRotation = Quaternion.identity; z.transform.localScale = Vector3.one * (zoneR * 2f / size);
        z.layer = 2;   // Ignore Raycast（たねを持つ時のじゃまにならないように）
        SplitZoneVisual(z);
        // 音はなし（コースの重いエリアのうねる低音。あそびばだとうるさい。2026-10-14 てじゃ）
        { var ga = z.GetComponent<FuwaGravityArea>(); if (ga != null) { SetU(ga, "loopSound", null); Sync(ga); } foreach (var a in z.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(a); }
        foreach (var t in z.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        var srcP = src.transform.parent.Find("HeavyParticles");
        if (srcP != null)
        {
            var pp = Object.Instantiate(srcP.gameObject, seed.transform, false); pp.name = "HeavyParticles";
            pp.transform.localPosition = Vector3.zero; pp.transform.localRotation = Quaternion.identity; pp.transform.localScale = Vector3.one * (zoneR * 2f / size) / 5.5f;
            var ps = pp.GetComponent<ParticleSystem>(); var main = ps.main; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        KeepGravityUpright(seed);
    }

    // 紫の球の見た目だけ子（Visual、ふつうのレイヤー）に分ける。当たり（トリガー）は Ignore Raycast のまま。
    // Ignore Raycast の物は VRChat のカメラ（写真）に写らなかった（2026-10-16 てじゃ「なおしたい」）
    static void SplitZoneVisual(GameObject z)
    {
        var mr = z.GetComponent<MeshRenderer>(); var mf = z.GetComponent<MeshFilter>();
        if (mr == null || mf == null) return;
        var old = z.transform.Find("Visual"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var v = new GameObject("Visual"); v.layer = 0;
        v.transform.SetParent(z.transform, false);
        GameObjectUtility.SetStaticEditorFlags(v, 0);
        v.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
        var vr = v.AddComponent<MeshRenderer>();
        EditorUtility.CopySerialized(mr, vr);
        Object.DestroyImmediate(mr);
        // MeshCollider は MeshFilter の形を使うので、外す前に当たりへ形を入れておく
        var mc = z.GetComponent<MeshCollider>(); if (mc != null && mc.sharedMesh == null) mc.sharedMesh = mf.sharedMesh;
        Object.DestroyImmediate(mf);
    }

    // たねを回しても、紫の球（グラデ）と粒の出る向きはまっすぐのまま（2026-10-13 てじゃ「回すとエフェクトも回って変だから固定に」）
    static void KeepGravityUpright(GameObject seed)
    {
        // 紫の球と粒は、たねの子にしないで（持った時の光る輪郭に6mの球まで入っていた。2026-10-13 てじゃ）、FuwaKeepUpright で位置だけついていく。
        // スクリプトはたね本体（VRCObjectSync のネットワークIDを持つ）ではなく HeavyZone に付ける（たねの部品の種類を変えない）
        EnsureProgram("FuwaKeepUpright");
        var slot = seed.transform.parent;
        Transform Get(string n) { var t = seed.transform.Find(n); if (t == null) t = slot.Find(n); return t; }
        var zone = Get("HeavyZone"); if (zone == null) return;
        var parts = Get("HeavyParticles");
        zone.SetParent(slot, true); if (parts != null) parts.SetParent(slot, true);
        var ku = zone.GetComponent<FuwaKeepUpright>();
        if (ku == null) ku = UdonSharpUndo.AddComponent<FuwaKeepUpright>(zone.gameObject);
        ku.targets = new[] { zone, parts }.Where(t => t != null).ToArray();
        ku.follow = seed.transform;
        ku.offsets = ku.targets.Select(t => t.position - seed.transform.position).ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(ku);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(ku); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
        EditorUtility.SetDirty(ku);
    }

    // 2026-11-27 てじゃ「竜巻のたね・重力のたねもオンオフ式。デフォはオフ、持ってインタラクトでオン。オフ時は球の色に近いオーラ、オンで効果が広がってオーラは消える。どちらもフェード」
    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/竜巻・重力のたねをオンオフ式に")]
    public static void SetupSeedToggles()
    {
        EnsureProgram("FuwaSeedToggle");
        var sh = Shader.Find("Fuwa/SeedAura");
        var sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        foreach (var name in new[] { "TornadoSeed", "GravitySeed" })
        {
            var seedT = Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == name && t.GetComponent<VRC.SDK3.Components.VRCPickup>() != null);
            if (seedT == null) { Debug.LogWarning("[たね] " + name + " がない"); continue; }
            var slot = seedT.parent;
            var st = seedT.GetComponent<FuwaSeedToggle>(); if (st == null) st = UdonSharpUndo.AddComponent<FuwaSeedToggle>(seedT.gameObject);
            var seedCol = seedT.GetComponent<Renderer>().sharedMaterial.HasProperty("_Color") ? seedT.GetComponent<Renderer>().sharedMaterial.GetColor("_Color") : Color.white;
            // オーラ：たねより一回り大きい球（当たりなし、影なし）
            var old = seedT.Find("Aura"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var aura = new GameObject("Aura"); aura.layer = seedT.gameObject.layer; aura.transform.SetParent(seedT, false); aura.transform.localScale = Vector3.one * 1.6f;
            aura.AddComponent<MeshFilter>().sharedMesh = sphere;
            var ar = aura.AddComponent<MeshRenderer>(); ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; ar.receiveShadows = false;
            string mp = "Assets/FuwaCourse/Materials/Playground_" + name + "Aura.mat";
            var am = AssetDatabase.LoadAssetAtPath<Material>(mp); if (am == null) { am = new Material(sh); AssetDatabase.CreateAsset(am, mp); }
            am.shader = sh; am.SetColor("_Color", Color.Lerp(seedCol, Color.white, 0.25f)); EditorUtility.SetDirty(am);
            ar.sharedMaterial = am;
            st.aura = ar;
            // 効果
            if (name == "TornadoSeed")
            {
                var ts = seedT.GetComponent<FuwaTornadoSeed>(); var tw = ts != null ? ts.tornado : slot.Find("Tornado");
                st.effects = new[] { tw };
                st.effectColliders = tw.GetComponents<Collider>();
                st.effectParticles = tw.GetComponentsInChildren<ParticleSystem>(true);
                if (ts != null) { var tub = UdonSharpEditorUtility.GetBackingUdonBehaviour(ts); if (tub != null) { tub.SyncMethod = VRC.SDKBase.Networking.SyncType.Continuous; EditorUtility.SetDirty(tub); } }
            }
            else
            {
                var zone = slot.Find("HeavyZone"); var parts = slot.Find("HeavyParticles");
                st.effects = new[] { zone };
                st.effectColliders = zone.GetComponents<Collider>();
                st.effectParticles = parts != null ? parts.GetComponentsInChildren<ParticleSystem>(true) : new ParticleSystem[0];
            }
            var pk = seedT.GetComponent<VRC.SDK3.Components.VRCPickup>(); pk.UseText = "オン / オフ"; EditorUtility.SetDirty(pk);
            // 音：扇風機のオン/オフと同じ（2026-11-19 てじゃ「SEは扇風機のを使いまわそ」）
            var fan = Object.FindObjectsOfType<FuwaPlaygroundIceToy>(true).FirstOrDefault();
            if (fan != null)
            {
                foreach (var n in new[] { "SeedOnSfx", "SeedOffSfx" }) { var o = seedT.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }
                AudioSource Copy(AudioSource a, string n) { if (a == null) return null; var g = Object.Instantiate(a.gameObject, seedT, false); g.name = n; g.transform.localPosition = Vector3.zero; foreach (Transform ch in g.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(ch.gameObject); foreach (var u in g.GetComponents<UdonSharpBehaviour>()) Object.DestroyImmediate(u); var au = g.GetComponent<AudioSource>(); au.playOnAwake = false; return au; }
                st.onSound = Copy(fan.toggleSound, "SeedOnSfx"); st.offSound = Copy(fan.offSound, "SeedOffSfx");
            }
            UdonSharpEditorUtility.CopyProxyToUdon(st);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(st); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Continuous; EditorUtility.SetDirty(ub); }   // VRCObjectSync と同じ物なので Continuous
            EditorUtility.SetDirty(st);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[たね] オンオフ式にした");
    }

    // 2026-11-27 てじゃ「ポータルもオンオフ式に、デフォはオフ、持ってUSEでオン、片方切り替えたら両方反応」
    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/ポータルをオンオフ式に")]
    public static void SetupPortalToggle()
    {
        var pp = Object.FindObjectsOfType<FuwaPlaygroundPortals>(true).First();
        var vis = new List<Transform>();
        foreach (var p in new[] { pp.a, pp.b }) foreach (Transform c in p) vis.Add(c);   // Swirl・Back
        // 浮いた輪・足もとの光・きらきら（PortalA_FX / PortalB_FX の子。FX の親は FuwaFollowPose で動かすので親は残す）
        foreach (var fxName in new[] { "PortalA_FX", "PortalB_FX" }) { var fx = pp.transform.parent.Find(fxName); if (fx != null) foreach (Transform c in fx) vis.Add(c); }
        pp.visuals = vis.ToArray();
        var cams = pp.transform.Find("PortalCams"); pp.cams = cams != null ? cams.gameObject : null;
        var fan = Object.FindObjectsOfType<FuwaPlaygroundIceToy>(true).FirstOrDefault();
        foreach (var n in new[] { "PortalOnSfx", "PortalOffSfx" }) { var o = pp.transform.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }
        AudioSource Copy(AudioSource a, string n) { if (a == null) return null; var g = Object.Instantiate(a.gameObject, pp.transform, false); g.name = n; g.transform.localPosition = Vector3.zero; foreach (Transform ch in g.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(ch.gameObject); foreach (var u in g.GetComponents<UdonSharpBehaviour>()) Object.DestroyImmediate(u); var au = g.GetComponent<AudioSource>(); au.playOnAwake = false; return au; }
        if (fan != null) { pp.onSound = Copy(fan.toggleSound, "PortalOnSfx"); pp.offSound = Copy(fan.offSound, "PortalOffSfx"); }
        UdonSharpEditorUtility.CopyProxyToUdon(pp);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(pp); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; EditorUtility.SetDirty(ub); } }
        EditorUtility.SetDirty(pp);
        foreach (var h in Object.FindObjectsOfType<FuwaPlaygroundPortalHandle>(true))
        {
            h.portals = pp; UdonSharpEditorUtility.CopyProxyToUdon(h); EditorUtility.SetDirty(h);
            var pk = h.GetComponent<VRC.SDK3.Components.VRCPickup>(); if (pk != null) { pk.UseText = "オン / オフ"; EditorUtility.SetDirty(pk); }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pp.gameObject.scene);
        Debug.Log("[ポータル] オンオフ式にした");
    }

    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/重力のたねのエフェクトをまっすぐに")]
    static void MenuKeepGravityUpright()
    {
        var seed = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "GravitySeed").gameObject;
        KeepGravityUpright(seed);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(seed.scene);
        Debug.Log("gravity seed upright");
    }

    // まもののぬいぐるみ1体＋イバラ1つ（2026-10-13 てじゃ「まもの1 いばら1」。前は大きさ違いのぬいぐるみ3体）。
    // ぬいぐるみ：コース4 のけだまの体と目と鼻ちょうちん。寝ている⇄起きて追いかける（FuwaPlaygroundPlush）。持って置ける
    // イバラ：コース4 のイバラ（SpikePatch4）。ふわふわははじける・人は足が遅くなる・ぬいぐるみは押し込まれると倒れる。持って動かせる（FuwaMovableMushroom と同じ仕組み）
    const bool WithBramble = false;
    static void FillPlush(Transform slot, FuwaPlaygroundSpawner sp)
    {
        foreach (var old in slot.Cast<Transform>().Where(t => t.name.StartsWith("Plush") || t.name.StartsWith("Bramble")).ToList()) Object.DestroyImmediate(old.gameObject);
        EnsureProgram("FuwaPlaygroundPlush"); EnsureProgram("FuwaMovableMushroom");
        var e = RootGo("FuwaCourse4").GetComponentsInChildren<FuwaEnemy>(true).First(x => x.name == "Enemy4");
        float baseR = e.GetComponent<SphereCollider>().radius * e.transform.lossyScale.x;
        Vector3 fwd = slot.forward; fwd.y = 0f; fwd.Normalize();

        // ---- イバラ（見た目と判定＝body、持つところ＝handle） ----
        // 2026-10-14 てじゃ「あそびばのいばらやっぱいらんかも」→ 作らない（WithBramble=true で元に戻せる。ぬいぐるみは bramble なしでも動く）
        Transform bBody = null; float bR = 0f;
        if (WithBramble)
        {
            var spSrc = RootGo("FuwaCourse4").GetComponentsInChildren<Transform>(true).First(t => t.name == "SpikePatch4" && t.parent != null && t.parent.name == "Spikes");
            Vector3 bPos = slot.position - fwd * 1.6f;   // ぬいぐるみと2.8m離す（起きてすぐ自分から飛び込まないように）。柵側（ぬいぐるみの後ろ）＝追いかけて真ん中へ行く時に通らない。前は横並びで場所を取った
            bBody = new GameObject("BrambleBody").transform; bBody.SetParent(slot, false); bBody.SetPositionAndRotation(bPos, Quaternion.LookRotation(fwd));
            var bg = Object.Instantiate(spSrc.gameObject, bBody, false); bg.name = "SpikePatch";
            bg.transform.localPosition = Vector3.zero; bg.transform.localRotation = Quaternion.identity; bg.transform.localScale = Vector3.one;
            foreach (var t in bBody.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            foreach (var fs in bBody.GetComponentsInChildren<FuwaSpike>(true)) { SetU(fs, "ball", null); Sync(fs); }
            var trig = bBody.GetComponentInChildren<FuwaSpike>(true).GetComponent<BoxCollider>();
            bR = Mathf.Max(trig.bounds.extents.x, trig.bounds.extents.z) * 0.8f;   // 長方形の判定を円で近似（少し内側）
            var bHandle = new GameObject("Bramble"); bHandle.layer = 13; bHandle.transform.SetParent(slot, false); bHandle.transform.SetPositionAndRotation(bPos, Quaternion.LookRotation(fwd));
            var bBox = bHandle.AddComponent<BoxCollider>(); bBox.isTrigger = true;
            bBox.size = new Vector3(trig.bounds.size.x, 0.45f, trig.bounds.size.z); bBox.center = new Vector3(0f, 0.22f, 0f);
            var bRb = bHandle.AddComponent<Rigidbody>(); bRb.isKinematic = true; bRb.useGravity = false;
            var bPk = bHandle.AddComponent<VRC.SDK3.Components.VRCPickup>();
            bPk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; bPk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any; bPk.InteractionText = "イバラ"; bPk.proximity = 2f;
            bHandle.AddComponent<VRC.SDK3.Components.VRCObjectSync>();
            var bmm = UdonSharpUndo.AddComponent<FuwaMovableMushroom>(bHandle); bmm.body = bBody; bmm.ground = 1 << 11;
            UdonSharpEditorUtility.CopyProxyToUdon(bmm);
            { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(bmm); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); } }
            GameObjectUtility.SetStaticEditorFlags(bHandle, 0);
            AddHighlightProxy(bHandle.transform, bBody);
        }

        // ---- ぬいぐるみ ----
        float r = baseR;
        // 地面からの高さはコース4のまものと同じ（毛の芯の下はしが地面すれすれ。前は r*0.72 で浮いて見えた。2026-10-13 てじゃ「もうちょい接地」）
        Physics.SyncTransforms();
        float standUp = Physics.Raycast(e.transform.position + Vector3.up * 0.6f, Vector3.down, out var eg, 5f, 1 << 11, QueryTriggerInteraction.Ignore) ? e.transform.position.y - eg.point.y : 0.2f;
        var root = new GameObject("Plush0"); root.layer = 13; root.transform.SetParent(slot, false);
        root.transform.position = slot.position + fwd * 1.2f + Vector3.up * standUp;
        root.transform.rotation = Quaternion.LookRotation(fwd);   // 顔（+Z）をキノコの方へ
        var vis = new GameObject("Visual").transform; vis.SetParent(root.transform, false);
        foreach (var n in new[] { "Body", "Eyes", "SleepBubble" })
        {
            var src = e.transform.Find(n); if (src == null) continue;
            var g = Object.Instantiate(src.gameObject, vis, false); g.name = n;
            foreach (var lg in g.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(lg);
        }
        foreach (var t in vis.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = 13; GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0); }
        // 足元のウソ影（デカールの箱）は、持った時の光る輪郭に入らないよう、ぬいぐるみの外に出して位置だけついていく（2026-10-13 てじゃ）
        var shadow = vis.Find("Body/Shadow");
        if (shadow != null)
        {
            shadow.SetParent(slot, true); shadow.name = "Plush0Shadow"; shadow.gameObject.layer = 0;
            EnsureProgram("FuwaKeepUpright");
            var sku = UdonSharpUndo.AddComponent<FuwaKeepUpright>(shadow.gameObject); sku.targets = new[] { shadow }; sku.follow = root.transform; sku.offsets = new[] { shadow.position - root.transform.position };
            UdonSharpEditorUtility.CopyProxyToUdon(sku);
            var sub = UdonSharpEditorUtility.GetBackingUdonBehaviour(sku); if (sub != null) { sub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(sub); }
        }
        var col = root.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = r;
        var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var pk = root.AddComponent<VRC.SDK3.Components.VRCPickup>();
        pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any; pk.InteractionText = "まもの";   // 2026-11-27 てじゃ「説明も冗長だから、まもの だけでいい」 pk.UseText = "オン / オフ";   // 2026-11-27 オンオフ式はそろえる（まものも） pk.proximity = 2f;
        // 位置は VRCObjectSync を使わず、FuwaPlaygroundPlush が動きの区切りを同期してみんなの画面で計算する（2026-10-14 てじゃ「オーナー以外が撃つとラグって見える」）
        AudioSource Copy(string n, string as_) { var s = e.transform.Find(n); if (s == null) return null; var a = Object.Instantiate(s.gameObject, root.transform, false); a.name = as_; return a.GetComponent<AudioSource>(); }
        var boing = Copy("JumpSfx", "Boing");
        var popSfx = Copy("PopSfx", "PopSfx");
        // 音は近くだけ聞こえるように（コース4のまものの音は立体音響なしで遠くまで同じ大きさだった。2026-10-14 てじゃ「距離減衰もうちょい強く」）
        foreach (var a in new[] { boing, popSfx })
        {
            if (a == null) continue;
            a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Logarithmic; a.minDistance = 1.5f; a.maxDistance = 18f;
            var vs = a.GetComponent<VRC.SDK3.Components.VRCSpatialAudioSource>();
            if (vs != null) { vs.EnableSpatialization = true; vs.Near = 1.5f; vs.Far = 18f; vs.Gain = 0f; }
        }
        ParticleSystem popFx = null;
        { var s = e.transform.Find("PopEffect"); if (s != null) { var g = Object.Instantiate(s.gameObject, root.transform, false); g.name = "PopEffect"; popFx = g.GetComponent<ParticleSystem>(); } }
        var pl = UdonSharpUndo.AddComponent<FuwaPlaygroundPlush>(root);
        pl.spawner = sp; pl.visual = vis; pl.radius = r; pl.boing = boing;
        pl.eyes = vis.Find("Eyes"); pl.sleepFx = vis.Find("SleepBubble");
        FuwaSleepEyesSetup.AddTo(pl.eyes);   // 寝ている時の「︶」の目
        FuwaSleepEyesSetup.SmoothEyes(pl.eyes);   // なめらかな目玉
        pl.standUp = standUp; pl.ground = 1 << 11; pl.fallY = slot.position.y - 6f;
        pl.bodyBottom = vis.Find("Body").GetComponent<Renderer>().bounds.extents.y;
        pl.bramble = bBody; pl.brambleRadius = bR;
        pl.popEffect = popFx; pl.popSound = popSfx;
        // 動き回れる範囲＝島の中（柵の内側）。置いた場所のまわりだけだと、ふわふわを飛ばしながら追うとすぐ戻っていた（2026-10-13 てじゃ）
        var roomT = slot.parent != null ? slot.parent.parent : null;
        if (roomT != null) { pl.areaCenter = roomT.position; pl.areaRadius = IslandR - 0.6f; }   // 柵（IslandR-0.14）のすぐ内側まで。前の -1.3 だと柵ぎわで止まった（2026-10-15 てじゃ）
        UdonSharpEditorUtility.CopyProxyToUdon(pl);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(pl); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Continuous; EditorUtility.SetDirty(ub); } }

        // エアガンで撃つと押し返せるように
        foreach (var bl in Object.FindObjectsOfType<FuwaBlower>(true))
        {
            var so = new SerializedObject(bl); var p = so.FindProperty("plushes");
            p.arraySize = 1; p.GetArrayElementAtIndex(0).objectReferenceValue = pl;
            so.ApplyModifiedPropertiesWithoutUndo(); Sync(bl);
        }
    }

    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/ぬいぐるみとイバラだけ作り直す")]
    static void MenuRebuildPlush()
    {
        var slot = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "Unlock4" && t.parent != null && t.parent.name == "Toys");
        // 消える持てる物（ネットワークIDを持つ）を一覧から外す
        var gone = slot.GetComponentsInChildren<VRC.SDK3.Components.VRCObjectSync>(true).Select(o => o.gameObject).Concat(slot.GetComponentsInChildren<VRC.Udon.UdonBehaviour>(true).Select(o => o.gameObject)).Distinct().ToList();
        var desc = Object.FindObjectOfType<VRC.SDK3.Components.VRCSceneDescriptor>(true);
        if (desc != null) { desc.NetworkIDCollection.RemoveAll(x => x.gameObject == null || gone.Contains(x.gameObject)); EditorUtility.SetDirty(desc); }
        FillPlush(slot, Object.FindObjectsOfType<FuwaPlaygroundSpawner>(true).First());
        // 「おもちゃを戻す」の一覧：消えた分を詰めて、新しい持てる物を足す
        var reset = Object.FindObjectsOfType<FuwaHandFanReset>(true).FirstOrDefault();
        if (reset != null)
        {
            var so = new SerializedObject(reset); var p = so.FindProperty("others");
            var list = new List<Object>();
            for (int i = 0; i < p.arraySize; i++) { var o = p.GetArrayElementAtIndex(i).objectReferenceValue; if (o != null) list.Add(o); }
            foreach (var os in slot.GetComponentsInChildren<VRC.SDK3.Components.VRCObjectSync>(true)) if (!list.Contains(os.gameObject)) list.Add(os.gameObject); foreach (var pp in slot.GetComponentsInChildren<FuwaPlaygroundPlush>(true)) if (!list.Contains(pp.gameObject)) list.Add(pp.gameObject);   // ぬいぐるみは ObjectSync なし
            p.arraySize = list.Count; for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedPropertiesWithoutUndo(); Sync(reset);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(slot.gameObject.scene);
        Debug.Log("plush and bramble rebuilt");
        Debug.Log(MuteToySounds());
    }

    // 跳ねるキノコ：きのこのたに（コース5）のギミックのキノコ（CapStep：傘がジャンプ台）を、コースと同じ大きさで2本。台座の島はなし（2026-10-13 てじゃ）。
    // 1本目はふつうのジャンプ（ジャンプ力3＝足が約46cm上がる）で乗れる高さ、2本目は1本目の上（ジャンプ台4.2＝約90cm）から乗れる高さ。
    // 低い方は柄がほとんど見えず傘だけ地面から出ている形（てじゃ「それでもいい」）。
    // 持って動かせて、いつも水平（FuwaMovableMushroom）。傘（FuwaBounce）はふわふわを強めに跳ねる
    static readonly float[] BounceCapTop = { 0.4f, 1.1f };
    const float BounceGapExtra = 0.25f;   // 2本の傘のすき間（1本目から2本目へ跳び移れる近さ）
    static void FillBounce(Transform slot)
    {
        foreach (var old in slot.Cast<Transform>().Where(t => t.name.StartsWith("BounceMushroom")).ToList()) Object.DestroyImmediate(old.gameObject);
        EnsureProgram("FuwaMovableMushroom");
        var src = RootGo("FuwaCourse5").transform.Find("Mushrooms/CapStep5");
        if (src == null) return;
        float capW = src.Find("Cap").GetComponent<Collider>().bounds.size.x;
        Vector3 fwd = slot.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 side = new Vector3(fwd.z, 0f, -fwd.x);
        Quaternion rot = Quaternion.LookRotation(fwd);
        for (int i = 0; i < BounceCapTop.Length; i++)
        {
            float H = BounceCapTop[i];
            Vector3 basePos = slot.position + side * ((i - 0.5f) * (capW + BounceGapExtra));

            // 見た目・当たり・ジャンプ台（持つところの子にしない）。CapStep は傘のてっぺんが原点なので、足元の body の上 H に置く
            var body = new GameObject("BounceMushroomBody" + i).transform; body.SetParent(slot, false); body.SetPositionAndRotation(basePos, rot);
            var g = Object.Instantiate(src.gameObject, body, false); g.name = "CapStep";
            g.transform.localPosition = Vector3.up * H; g.transform.localRotation = Quaternion.identity; g.transform.localScale = src.lossyScale;
            foreach (var n in new[] { "IslandTop", "IslandUnder" }) { var t = g.transform.Find(n); if (t != null) Object.DestroyImmediate(t.gameObject); }
            foreach (var lg in g.GetComponentsInChildren<LODGroup>(true)) Object.DestroyImmediate(lg);
            // 柄：傘の下から地面までの長さに（短すぎたら隠す）
            var stem = g.transform.Find("Stem");
            if (stem != null)
            {
                var sr = stem.GetComponent<Renderer>(); Bounds sb = sr.bounds;
                float top = sb.max.y, bottom = basePos.y - 0.02f;
                if (top - bottom < 0.05f) stem.gameObject.SetActive(false);
                else
                {
                    stem.localScale = new Vector3(stem.localScale.x, stem.localScale.y * (top - bottom) / Mathf.Max(0.01f, sb.size.y), stem.localScale.z);
                    stem.position += Vector3.up * (bottom - sr.bounds.min.y);
                }
            }
            foreach (var bo in g.GetComponentsInChildren<FuwaBounce>(true))
            {
                var so = new SerializedObject(bo); so.FindProperty("bounceSpeed").floatValue = 6f; so.FindProperty("keepTangent").floatValue = 0.6f;
                so.ApplyModifiedPropertiesWithoutUndo(); Sync(bo);
            }
            foreach (var t in body.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

            // 持つところ：見た目なし。キノコ全体を囲む箱（トリガー）を持つ
            var handle = new GameObject("BounceMushroom" + i); handle.layer = 13; handle.transform.SetParent(slot, false); handle.transform.SetPositionAndRotation(basePos, rot);
            var box = handle.AddComponent<BoxCollider>(); box.isTrigger = true;
            float hh = Mathf.Max(0.35f, H + 0.05f);
            box.size = new Vector3(capW, hh, capW); box.center = new Vector3(0f, hh * 0.5f, 0f);
            var rb = handle.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var pk = handle.AddComponent<VRC.SDK3.Components.VRCPickup>();
            pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any; pk.InteractionText = "はねるキノコ"; pk.proximity = 2f;
            handle.AddComponent<VRC.SDK3.Components.VRCObjectSync>();
            var mm = UdonSharpUndo.AddComponent<FuwaMovableMushroom>(handle); mm.body = body; mm.ground = 1 << 11;
            UdonSharpEditorUtility.CopyProxyToUdon(mm);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(mm); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
            GameObjectUtility.SetStaticEditorFlags(handle, 0);
            AddHighlightProxy(handle.transform, body);
        }
    }

    // 持った時の光る輪郭（VRChat は持てる物の中のレンダラーの形で光らせる）を、見た目（body、持つところの子ではない）と同じ形にする。
    // 見た目の形をそのまま、見えない材質（Invisible）で持つところの子に置く（2026-10-13 てじゃ「持った時のハイライトの形をオブジェクトにあわせて」）
    static void AddHighlightProxy(Transform handle, Transform body)
    {
        var old = handle.Find("HighlightShape"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var inv = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Invisible.mat");
        var holder = new GameObject("HighlightShape").transform; holder.SetParent(handle, false); holder.gameObject.layer = handle.gameObject.layer;
        foreach (var mr in body.GetComponentsInChildren<MeshRenderer>(false))
        {
            if (!mr.enabled) continue;
            var mf = mr.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
            var g = new GameObject(mr.name); g.layer = handle.gameObject.layer;
            g.transform.SetPositionAndRotation(mr.transform.position, mr.transform.rotation);
            g.transform.localScale = mr.transform.lossyScale;
            g.transform.SetParent(holder, true);
            g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = inv;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(g, 0);
        }
    }

    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/跳ねるキノコだけ作り直す")]
    static void MenuRebuildBounce()
    {
        var slot = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "Unlock5" && t.parent != null && t.parent.name == "Toys");
        FillBounce(slot);
        // 「おもちゃを戻す」の一覧：消えた分を詰めて、新しい持つところを足す
        var reset = Object.FindObjectsOfType<FuwaHandFanReset>(true).FirstOrDefault();
        if (reset != null)
        {
            var so = new SerializedObject(reset); var p = so.FindProperty("others");
            var list = new List<Object>();
            for (int i = 0; i < p.arraySize; i++) { var o = p.GetArrayElementAtIndex(i).objectReferenceValue; if (o != null) list.Add(o); }
            foreach (var os in slot.GetComponentsInChildren<VRC.SDK3.Components.VRCObjectSync>(true)) if (!list.Contains(os.gameObject)) list.Add(os.gameObject); foreach (var pp in slot.GetComponentsInChildren<FuwaPlaygroundPlush>(true)) if (!list.Contains(pp.gameObject)) list.Add(pp.gameObject);   // ぬいぐるみは ObjectSync なし
            p.arraySize = list.Count; for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedPropertiesWithoutUndo(); Sync(reset);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(slot.gameObject.scene);
        Debug.Log("bounce mushrooms rebuilt");
    }

    // 輪（XY 平面、穴は Z 向き）
    static Mesh Torus(float R, float r, int U, int V)
    {
        var vs = new List<Vector3>(); var ns = new List<Vector3>(); var tr = new List<int>();
        for (int i = 0; i <= U; i++)
            for (int j = 0; j <= V; j++)
            {
                float t = 2f * Mathf.PI * i / U, ph = 2f * Mathf.PI * j / V;
                var c = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f);
                var n = c * Mathf.Cos(ph) + Vector3.forward * Mathf.Sin(ph);
                vs.Add(c * R + n * r); ns.Add(n);
            }
        for (int i = 0; i < U; i++)
            for (int j = 0; j < V; j++)
            {
                int a = i * (V + 1) + j, b = (i + 1) * (V + 1) + j;
                tr.AddRange(new[] { a, a + 1, b, b, a + 1, b + 1 });
            }
        var m = new Mesh(); m.SetVertices(vs); m.SetNormals(ns); m.SetTriangles(tr, 0); m.RecalculateBounds();
        // 面の向き：法線と同じ向きでなければ裏返す
        var t0 = m.triangles; var v0 = m.vertices;
        var fn = Vector3.Cross(v0[t0[1]] - v0[t0[0]], v0[t0[2]] - v0[t0[0]]);
        if (Vector3.Dot(fn, ns[t0[0]]) < 0f) { for (int k = 0; k < t0.Length; k += 3) { int x = t0[k + 1]; t0[k + 1] = t0[k + 2]; t0[k + 2] = x; } m.triangles = t0; }
        return m;
    }

    // スイッチのボードのまわりのボタン（2026-10-13 てじゃ「柱なくそう、言語ボタンと音量ボタンはその下に横並び、HUDはここではいらん、チャレンジにあるワープボタンを右側に」）。
    // ・下：遊び方の看板（HowToPlay）にある言語・音量ボタンを、ボードの下に横並び（看板の板・文字・HUD は消す）
    // ・右：チャレンジのロビーの看板の右と同じ「今いるモード（押せない札）／もう一つのモード／タイトルにもどる」。
    //   ここはあそびばなので、上の札が「あそびば（人数）」、その下が「チャレンジ（人数）」＝押すとチャレンジへ飛ぶ、いちばん下が「タイトルにもどる」
    const float BoardSideX = 2.25f;
    // スイッチの文字：値を切り替えても「サイズ:」などの頭が動かないように（2026-10-14 てじゃ「切り替えると全部の文字の位置がずれる、左側は位置固定で値の部分だけ変わるように、ちょっと片側に寄ってもいい」）。
    // 大きさ自動をやめて4つとも同じ大きさに固定、左ぞろえ。頭の位置は、そのスイッチでいちばん長い値の時にちょうど真ん中に来る所（<space> タグを Spawner が先頭に足す）
    static void SwitchLabelLayout(FuwaPlaygroundSpawner sp, TMPro.TextMeshProUGUI[] labels, TMPro.TextMeshProUGUI outlined)
    {
        string[][] ja = {
            sp.sizeJa.Select(v => "サイズ: " + v).ToArray(), sp.colorJa.Select(v => "カラー: " + v).ToArray(),
            sp.limits.Select(v => "かず: " + v).ToArray(), new[] { "おちたら: きえる", "おちたら: きえない" },
            new[] { "かお: あり", "かお: なし" }, new[] { "もふもふ: あり", "もふもふ: なし" } };   // 2026-10-16 てじゃ「"け"がわかりづらい、もふもふ とかに」
        string[][] en = {
            sp.sizeEn.Select(v => "Size: " + v).ToArray(), sp.colorEn.Select(v => "Color: " + v).ToArray(),
            sp.limits.Select(v => "Max: " + v).ToArray(), new[] { "On landing: Pop", "On landing: Stay" },
            new[] { "Face: On", "Face: Off" }, new[] { "Fur: On", "Fur: Off" } };
        var t = labels[0];
        float maxFs = t.fontSizeMax, avail = t.rectTransform.rect.width - t.margin.x - t.margin.z;
        float W(string s) { t.enableAutoSizing = false; t.fontSize = maxFs; return t.GetPreferredValues(s).x; }
        // いちばん長い文字が入る大きさ（全部共通）
        float fs = maxFs;
        foreach (var set in ja.Concat(en)) foreach (var s in set) fs = Mathf.Min(fs, maxFs * avail / Mathf.Max(1f, W(s)));
        int nS = ja.Length;
        sp.padJa = new string[nS]; sp.padEn = new string[nS];
        for (int k = 0; k < nS; k++)
        {
            float wj = ja[k].Max(W) * fs / maxFs, we = en[k].Max(W) * fs / maxFs;
            sp.padJa[k] = "<space=" + ((avail - wj) * 0.5f / fs).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "em>";
            sp.padEn[k] = "<space=" + ((avail - we) * 0.5f / fs).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "em>";
        }
        foreach (var l in labels.Concat(new[] { outlined }))
        {
            if (l == null) continue;
            l.enableAutoSizing = false; l.fontSize = fs; l.alignment = TMPro.TextAlignmentOptions.Left; EditorUtility.SetDirty(l);
        }
    }

    // 右の列の高さ＝スイッチの3段とそろえる。言語・音量はその下
    //（2026-10-15 スイッチを3段に（かお・け を足した）。前は 1.76/1.24 の2段＋言語・音量 0.72 で、右の列の3つ目が言語・音量の段とそろっていた）
    static readonly float[] BoardSideY ={ 2.12f, 1.60f, 1.08f };
    const float BoardBottomY = 0.56f, BoardBottomX = 0.75f;

    // スイッチ6つ（2列×3段）：ロビーと同じレーザーで押す白いボタン（近くに来た時だけ押せる＝FuwaUiProximity がビルド前に自動登録）
    // 上から サイズ・カラー／かず・おちたら／かお・け。足りない分だけ作って、全部を決まった位置へ置く（シーンに置いてある分にも使える）
    // 右の列（あそびば・チャレンジ・タイトルにもどる）の高さ：真ん中のボタンをスイッチの真ん中の段にそろえて、
    // 上下の間隔はチャレンジのロビーの3つのボタンと同じ（2026-10-15 てじゃ「チャレンジ側と同じ隙間に、中央基準で」。前はスイッチの段に合わせて0.52m間隔）
    static float SideRowY(int row, Transform lobbyHow)
    {
        float gap = 0.36f;
        var a = lobbyHow != null ? lobbyHow.Find("SoloCount") : null; var b = lobbyHow != null ? lobbyHow.Find("TogetherButton") : null;
        if (a != null && b != null) gap = Mathf.Abs(a.position.y - b.position.y);
        return BoardSideY[1] + (1 - row) * gap;
    }

    static readonly string[] SwitchEvents = { "NextSize", "NextColor", "NextLimit", "TogglePop", "ToggleFace", "ToggleFur" };
    static TMPro.TextMeshProUGUI[] PlaceSwitches(Transform panel, Vector3 pc, Quaternion rot, FuwaPlaygroundSpawner sp)
    {
        var labels = new TMPro.TextMeshProUGUI[SwitchEvents.Length];
        for (int k = 0; k < SwitchEvents.Length; k++)
        {
            int col = k % 2, row = k / 2;
            // 大きさは下の言語・音量ボタンと同じ（幅1.36・高さ0.4。2026-10-13 てじゃ「下のボタンとサイズあわせよ」）
            var pos = pc + rot * new Vector3(col == 0 ? -BoardBottomX : BoardBottomX, BoardSideY[row], 0f);
            var t = panel.Find("Switch" + k);
            if (t == null)
            {
                LaserButton(panel, "Switch" + k, pos, rot, 0.99f, "", sp, SwitchEvents[k]);
                t = panel.Find("Switch" + k);
                OutlinedStyle(t);
            }
            t.SetPositionAndRotation(pos, rot); t.localScale = Vector3.one;
            PillCorners(t);
            labels[k] = t.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).First(l => l.name != "TextOutlined");
        }
        sp.sizeLabel = labels[0]; sp.colorLabel = labels[1]; sp.limitLabel = labels[2]; sp.popLabel = labels[3]; sp.faceLabel = labels[4]; sp.furLabel = labels[5];
        return labels;
    }

    [MenuItem("FuwaCourse/済み（押す前に相談）/あそびば/設定スイッチだけ並べ直す（かお・け の段を足す）")]
    static void MenuRelayoutSwitches()
    {
        var coop = RootGo(RootName).transform;
        var root = coop.Find("Room"); var panel = root.Find("Switches");
        var sp = Object.FindObjectsOfType<FuwaPlaygroundSpawner>(true).First();
        float floorY = root.Find("Floor").GetComponent<Renderer>().bounds.max.y;
        Vector3 ac = coop.position;
        Vector3 pc = new Vector3(ac.x, floorY, ac.z) + PanelLocal;
        var rot = Quaternion.Euler(0f, PanelYaw, 0f);
        var labels = PlaceSwitches(panel, pc, rot, sp);
        // 右の列（あそびば・チャレンジ・タイトルにもどる）と言語・音量、見出し
        string[] side = { "HereCount", "ToChallengeButton", "ToTitleButton" };
        var lobbyHow = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "HowToPlay" && t.parent != null && t.parent.name == "FuwaLobby");
        for (int r = 0; r < 3; r++) { var t = panel.Find(side[r]); if (t != null) t.position = pc + rot * new Vector3(BoardSideX, SideRowY(r, lobbyHow), 0f); }
        var howTo = coop.Find("HowToPlay");
        if (howTo != null) foreach (var n in new[] { "LanguageButton", "BgmVolume" }) { var t = howTo.Find(n); if (t != null) t.localPosition = new Vector3(n == "LanguageButton" ? -BoardBottomX : BoardBottomX, BoardBottomY, 0f); }
        var hg = panel.Find("Heading"); if (hg != null) hg.position = pc + rot * new Vector3(0f, BoardSideY[0] + 0.5f, 0f);
        SwitchLabelLayout(sp, labels, sp.colorLabelOutlined);
        UdonSharpEditorUtility.CopyProxyToUdon(sp);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);
        Debug.Log("playground switches relayout: " + labels.Length);
    }
    static void BoardButtons(Transform coop, Transform panel, Vector3 pc, Quaternion rot, Transform howTo)
    {
        var lobbyHow = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "HowToPlay" && t.parent != null && t.parent.name == "FuwaLobby");
        if (howTo != null)
        {
            howTo.SetPositionAndRotation(pc, rot);
            foreach (var n in new[] { "Panel", "Border", "Text", "HudButton" }) { var t = howTo.Find(n); if (t != null) t.gameObject.SetActive(false); }
            // 言語・音量はチャレンジのロビーと同じ大きさ（0.88×0.25m。2026-10-13 てじゃ「チャレンジのサイズに揃えよう、ふわふわ設定のはそのまま」。前はスイッチに合わせて1.4×0.4m）
            foreach (var n in new[] { "LanguageButton", "BgmVolume" })
            {
                var t = howTo.Find(n); var src = lobbyHow.Find(n); if (t == null || src == null) continue;
                var ps = t.parent.lossyScale; var ss = src.lossyScale;
                t.localScale = new Vector3(ss.x / ps.x, ss.y / ps.y, ss.z / ps.z);
                t.localPosition = new Vector3(n == "LanguageButton" ? -BoardBottomX : BoardBottomX, BoardBottomY, 0f);
                // 文字の見た目（フォント・大きさ）もチャレンジのロビーにそろえる（あそびばの方だけ別のフォント MPLUS だった。2026-10-13 てじゃ「言語選択もあわせよ」）
                foreach (var dst in t.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                {
                    string rel = dst.transform.name; for (var q = dst.transform.parent; q != null && q != t; q = q.parent) rel = q.name + "/" + rel;
                    var st = src.Find(rel); var s2 = st != null ? st.GetComponent<TMPro.TextMeshProUGUI>() : null; if (s2 == null) continue;
                    dst.font = s2.font; dst.fontSharedMaterial = s2.fontSharedMaterial; dst.fontSize = s2.fontSize; dst.fontStyle = s2.fontStyle;
                    dst.characterSpacing = s2.characterSpacing; dst.enableAutoSizing = s2.enableAutoSizing; dst.fontSizeMin = s2.fontSizeMin; dst.fontSizeMax = s2.fontSizeMax;
                    dst.margin = s2.margin; dst.alignment = s2.alignment;
                    EditorUtility.SetDirty(dst);
                }
            }
            // 角の丸みは最後に FuwaButtonCorners で全部そろえる（端が丸いボタン。音量の板も）
        }
        GameObject Copy(string src, string name, int row)
        {
            var s = lobbyHow.Find(src);
            var g = Object.Instantiate(s.gameObject, panel, false); g.name = name;
            g.transform.SetPositionAndRotation(pc + rot * new Vector3(BoardSideX, SideRowY(row, lobbyHow), 0f), rot);
            g.transform.localScale = s.lossyScale;
            return g;
        }
        void Retarget(GameObject g, string warpName)
        {
            var target = coop.Find(warpName + "/" + warpName + "_Trigger");
            var ub = target != null ? target.GetComponent<VRC.Udon.UdonBehaviour>() : null;
            var b = g.GetComponentInChildren<UnityEngine.UI.Button>(true);
            if (b == null || ub == null) return;
            var so = new SerializedObject(b);
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            for (int i = 0; i < calls.arraySize; i++) calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Target").objectReferenceValue = ub;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        var here = Copy("SoloCount", "HereCount", 0);              // あそびば（押せない札）
        var toSolo = Copy("TogetherButton", "ToChallengeButton", 1); Retarget(toSolo, "ToSolo");
        var toTitle = Copy("ModeSelectButton", "ToTitleButton", 2); Retarget(toTitle, "ReturnToHub");
        // 人数は動いてから FuwaModeCounter が書くので、エディタで見た時に分かるよう名前だけ入れておく（コピー元のままだと逆に見える）
        here.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text = "あそびば";
        toSolo.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).text = "チャレンジ";

        // 人数つきの文字は FuwaModeCounter が書く（チャレンジのロビーと同じ決まり）。消えた古い分を詰めて足す
        var mc = Object.FindObjectsOfType<FuwaModeCounter>(true).FirstOrDefault();
        if (mc != null)
        {
            var so = new SerializedObject(mc);
            var lab = so.FindProperty("uiLabels"); var area = so.FindProperty("uiLabelAreas"); var ja = so.FindProperty("uiLabelsJa"); var en = so.FindProperty("uiLabelsEn"); var col = so.FindProperty("uiCountColors");
            var keep = new List<int>();
            for (int i = 0; i < lab.arraySize; i++) if (lab.GetArrayElementAtIndex(i).objectReferenceValue != null) keep.Add(i);
            var L = keep.Select(i => lab.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
            var A = keep.Select(i => area.GetArrayElementAtIndex(i).intValue).ToList();
            var J = keep.Select(i => ja.GetArrayElementAtIndex(i).stringValue).ToList();
            var E = keep.Select(i => en.GetArrayElementAtIndex(i).stringValue).ToList();
            var C = keep.Select(i => i < col.arraySize ? col.GetArrayElementAtIndex(i).stringValue : "").ToList();
            // 札（あそびば）は数字がコーラル、ボタン（チャレンジ）は白っぽく（チャレンジのロビーと同じ）
            L.Add(here.GetComponentInChildren<TMPro.TextMeshProUGUI>(true)); A.Add(2); J.Add("あそびば"); E.Add("Playground"); C.Add("#F2786B");
            L.Add(toSolo.GetComponentInChildren<TMPro.TextMeshProUGUI>(true)); A.Add(1); J.Add("チャレンジ"); E.Add("Challenge"); C.Add("");
            // 「タイトルにもどる」にもタイトル（入口）にいる人数（2026-10-13 てじゃ「ここのボタンにも接続人数表示ほしい」）
            var titleT = toTitle.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            titleT.spriteAsset = toSolo.GetComponentInChildren<TMPro.TextMeshProUGUI>(true).spriteAsset;   // 人のマーク（PersonIcon）。無いと既定の絵文字の顔になった
            L.Add(titleT); A.Add(0); J.Add("タイトルにもどる"); E.Add("Back to Title"); C.Add("");
            lab.arraySize = L.Count; area.arraySize = L.Count; ja.arraySize = L.Count; en.arraySize = L.Count; col.arraySize = L.Count;
            for (int i = 0; i < L.Count; i++)
            {
                lab.GetArrayElementAtIndex(i).objectReferenceValue = L[i]; area.GetArrayElementAtIndex(i).intValue = A[i];
                ja.GetArrayElementAtIndex(i).stringValue = J[i]; en.GetArrayElementAtIndex(i).stringValue = E[i]; col.GetArrayElementAtIndex(i).stringValue = C[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo(); Sync(mc);
        }
        // 「タイトルにもどる」の文字は人数つきで FuwaModeCounter が言語に合わせて書く（FuwaLanguage には入れない）
    }

    // 島のまわりの木の柵（2026-10-13 てじゃ「周りに柵つけとこ」）。写真館の島の柵（FuwaPhotoStudioBuilder.StudioFence）と同じ作り：
    // 柱は地面のふちより内側、横木2本、当たり判定は横木の区間ごとの段差のない板（高さ0.65＝歩いては越えられない、ジャンプなら越えられる。横木があると足場になって登られる）。
    // 名前に Fence を含むので、ふわふわが柵のてっぺんに乗って止まったら転がり落ちる
    static string Fence(Transform root, Vector3 ac, float floorY)
    {
        var gT = root.Find("Floor");
        float groundR = 0f;
        foreach (var v in gT.GetComponent<MeshFilter>().sharedMesh.vertices) { var w = gT.TransformPoint(v) - ac; groundR = Mathf.Max(groundR, new Vector2(w.x, w.z).magnitude); }
        float r = groundR - 0.14f;
        const float postW = 0.11f, postH = 0.6f, railW = 0.06f, railH = 0.06f;
        float baseY = -0.02f;
        float[] railY = { 0.22f, 0.52f };
        int segs = Mathf.RoundToInt(Mathf.PI * 2f * r / 1.0f);
        // 角の丸みは1段（2026-10-16 軽く：2段→1段。なめらかな法線なので、3cmの丸みは1段でも丸く見える）
        var postMesh = FuwaBevelTools.RoundCube(new Vector3(postW, postH, postW), 0.03f, 1);
        var parts = new List<CombineInstance>();
        var colParts = new List<CombineInstance>();
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        Vector3 P(float ang) { return new Vector3(Mathf.Cos(ang) * r, floorY - root.position.y, Mathf.Sin(ang) * r); }
        for (int i = 0; i < segs; i++)
        {
            float a0 = Mathf.PI * 2f * i / segs, a1 = Mathf.PI * 2f * (i + 1) / segs;
            Vector3 p = P(a0), q = P(a1);
            parts.Add(new CombineInstance { mesh = postMesh, transform = Matrix4x4.TRS(p + Vector3.up * (baseY + postH * 0.5f), Quaternion.identity, new Vector3(postW, postH, postW)) });
            Vector3 mid = (p + q) * 0.5f; Vector3 d = q - p; float len = d.magnitude;
            Quaternion rr = Quaternion.LookRotation(d / len, Vector3.up);
            var railMesh = FuwaBevelTools.RoundCube(new Vector3(railW, railH, len + 0.04f), 0.02f, 1);
            foreach (float ry in railY)
                parts.Add(new CombineInstance { mesh = railMesh, transform = Matrix4x4.TRS(mid + Vector3.up * (baseY + ry), rr, new Vector3(railW, railH, len + 0.04f)) });
            colParts.Add(new CombineInstance { mesh = cube, transform = Matrix4x4.TRS(mid + Vector3.up * (baseY + 0.33f), rr, new Vector3(0.12f, 0.65f, len)) });
        }
        var vis = new Mesh { name = "Playground_Fence", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        vis.CombineMeshes(parts.ToArray(), true, true); vis.RecalculateBounds();
        var col = new Mesh { name = "Playground_FenceCollider", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        col.CombineMeshes(colParts.ToArray(), true, true); col.RecalculateBounds();
        vis = SaveFenceMesh(vis, "Playground_Fence"); col = SaveFenceMesh(col, "Playground_FenceCollider");
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course_Wood.mat");
        // もうあれば物はそのまま使ってメッシュだけ差しかえる（影の切りかえ・焼きの一覧などが物を覚えているので。2026-10-16）
        var f = root.Find("Fence")?.gameObject;
        if (f == null) { f = new GameObject("Fence"); f.transform.SetParent(root, false); f.transform.localPosition = Vector3.zero; f.AddComponent<MeshFilter>(); f.AddComponent<MeshRenderer>(); }
        f.GetComponent<MeshFilter>().sharedMesh = vis; f.GetComponent<MeshRenderer>().sharedMaterial = wood;
        GameObjectUtility.SetStaticEditorFlags(f, (StaticEditorFlags)~0);
        var fc = root.Find("FenceCollider")?.gameObject;
        if (fc == null) { fc = new GameObject("FenceCollider"); fc.transform.SetParent(root, false); fc.transform.localPosition = Vector3.zero; fc.AddComponent<MeshCollider>(); }
        fc.GetComponent<MeshCollider>().sharedMesh = col;
        GameObjectUtility.SetStaticEditorFlags(fc, (StaticEditorFlags)~0);
        return "fence r=" + r.ToString("F2") + " posts " + segs;
    }

    // ---------- 外周の木のデッキ（2026-10-13 てじゃ「外周に円の帯状の床。モニタの下のカーペットのちょっと横から反対側までぐるっと。各おもちゃがそこに乗ってる。そこそこ幅。色はとりあえず木っぽく」）----------
    // カーペットの右はし（島の真ん中から5°）のすぐ横から右回りに、ぐるっとカーペットの左はし（-49°）のすぐ横まで（てじゃ「反対側までつなげちゃって」）。内側は竜巻の見た目の内はし（7.9m）より少し内、外側は柵の手前。
    // 板は弧に沿った曲がった板（列ごとに長さをずらす）。板の間は 1.5cm のすき間から濃い色の下地が見える。高さ DeckH（床とぴったり重ねるとチラつくので少し上げる）
    const float DeckStartDeg = 9f, DeckEndDeg = 307f, DeckInR = 7.2f, DeckOutGap = 0.14f, DeckH = 0.06f;
    const float DeckRowW = 0.5f, DeckGap = 0.015f, DeckChamfer = 0.012f;
    static readonly Color[] DeckTones = { new Color(0.80f, 0.62f, 0.42f), new Color(0.75f, 0.56f, 0.37f), new Color(0.84f, 0.67f, 0.47f) };
    class DeckPart { public List<Vector3> verts = new List<Vector3>(), norms = new List<Vector3>(); public List<int> tris = new List<int>(); }
    static string Deck(Transform root, Vector3 ac, float floorY, int floorLayer)
    {
        float y0 = floorY - root.position.y;
        float rOut = IslandR - 0.14f - DeckOutGap;   // 柵の柱の内側
        float a0 = DeckStartDeg * Mathf.Deg2Rad, a1 = DeckEndDeg * Mathf.Deg2Rad;
        // 島の角度（奥が0°、右回り）→ ローカル座標
        Vector3 P(float a, float r, float h) { return new Vector3(Mathf.Sin(a) * r, y0 + h, Mathf.Cos(a) * r); }
        int rows = Mathf.Max(1, Mathf.RoundToInt((rOut - DeckInR) / DeckRowW));
        float rowW = (rOut - DeckInR) / rows;
        var rnd = new System.Random(20261013);
        var tones = DeckTones.Select(_ => new DeckPart()).ToArray();
        void Quad(DeckPart m, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            // a→b→c→d（上から見て時計回り＝表）。平らな面ごとに頂点を分ける（トゥーンで板の角をくっきり）
            var nrm = Vector3.Cross(b - a, d - a).normalized; int i = m.verts.Count;
            m.verts.AddRange(new[] { a, b, c, d }); m.norms.AddRange(new[] { nrm, nrm, nrm, nrm });
            m.tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        int boards = 0;
        for (int row = 0; row < rows; row++)
        {
            float ri = DeckInR + row * rowW + DeckGap * 0.5f, ro = DeckInR + (row + 1) * rowW - DeckGap * 0.5f;
            float rm = (ri + ro) * 0.5f;
            // 板の長さ 1.4〜2.4m、列ごとに始まりをずらす
            var cuts = new List<float> { a0 };
            float a = a0 + (float)(0.3 + rnd.NextDouble() * 1.6) / rm;
            while (a < a1 - 0.6f / rm) { cuts.Add(a); a += (float)(1.4 + rnd.NextDouble() * 1.0) / rm; }
            cuts.Add(a1);
            for (int k = 0; k + 1 < cuts.Count; k++)
            {
                float b0 = cuts[k] + (k == 0 ? 0f : DeckGap * 0.5f / rm), b1 = cuts[k + 1] - (k + 2 == cuts.Count ? 0f : DeckGap * 0.5f / rm);
                var m = tones[rnd.Next(tones.Length)];
                float c = DeckChamfer, cr = c / rm, h = DeckH, hb = 0f;
                int seg = Mathf.Max(2, Mathf.CeilToInt((b1 - b0) * rm / 0.6f));   // 2026-10-16 軽く（25cm→60cmごと。ゆるい弧なので見た目ほぼ同じ）
                for (int sIdx = 0; sIdx < seg; sIdx++)
                {
                    // 端は面取りしない（板の長い辺だけ面取り。端も面取りすると角に三角の面が出て変に見えた。2026-10-13 てじゃ「木の床の角が変」）
                    float t0 = Mathf.Lerp(b0, b1, (float)sIdx / seg), t1 = Mathf.Lerp(b0, b1, (float)(sIdx + 1) / seg);
                    float u0 = Mathf.Lerp(b0, b1, (float)sIdx / seg), u1 = Mathf.Lerp(b0, b1, (float)(sIdx + 1) / seg);
                    // 上の面（ふちは面取りの分だけ内側）
                    Quad(m, P(t0, ri + c, h), P(t0, ro - c, h), P(t1, ro - c, h), P(t1, ri + c, h));
                    // 面取り（外・内）と側面（外・内）
                    Quad(m, P(t0, ro - c, h), P(t0, ro, h - c), P(t1, ro, h - c), P(t1, ro - c, h));
                    Quad(m, P(t1, ri + c, h), P(t1, ri, h - c), P(t0, ri, h - c), P(t0, ri + c, h));
                    Quad(m, P(u0, ro, h - c), P(u0, ro, hb), P(u1, ro, hb), P(u1, ro, h - c));
                    Quad(m, P(u1, ri, h - c), P(u1, ri, hb), P(u0, ri, hb), P(u0, ri, h - c));
                }
                // 端（板の切り口）：まっすぐな縦の面（上は長い辺の面取りの形に合わせた六角形）
                Quad(m, P(b0, ri + c, h), P(b0, ri, h - c), P(b0, ro, h - c), P(b0, ro - c, h));
                Quad(m, P(b0, ri, h - c), P(b0, ri, hb), P(b0, ro, hb), P(b0, ro, h - c));
                Quad(m, P(b1, ro - c, h), P(b1, ro, h - c), P(b1, ri, h - c), P(b1, ri + c, h));
                Quad(m, P(b1, ro, h - c), P(b1, ro, hb), P(b1, ri, hb), P(b1, ri, h - c));
                boards++;
            }
        }
        // 下地（すき間から見える濃い色）：床から 2cm 上（床と同じ高さだとチラつく）
        // 板の内はし・外はし・両端より少し内側までにする（板と同じ幅だと、板の側面の途中から薄い板がはみ出して見えた。2026-10-14 てじゃ「真ん中に謎の板」）
        var under = new DeckPart();
        int useg = Mathf.CeilToInt((a1 - a0) * rOut / 0.3f);
        float uIn = DeckInR + DeckGap, uOut = rOut - DeckGap, ua0 = a0 + DeckGap / DeckInR, ua1 = a1 - DeckGap / DeckInR;
        for (int sIdx = 0; sIdx < useg; sIdx++)
        {
            float t0 = Mathf.Lerp(ua0, ua1, (float)sIdx / useg), t1 = Mathf.Lerp(ua0, ua1, (float)(sIdx + 1) / useg);
            Quad(under, P(t0, uIn, 0.02f), P(t0, uOut, 0.02f), P(t1, uOut, 0.02f), P(t1, uIn, 0.02f));
        }
        // 見た目：板（色3つ＝サブメッシュ3つ）＋下地
        var mesh = new Mesh { name = "Playground_Deck", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        var all = tones.Concat(new[] { under }).ToArray();
        var vAll = new List<Vector3>(); var nAll = new List<Vector3>(); var subs = new List<int[]>();
        foreach (var t in all) { int off = vAll.Count; vAll.AddRange(t.verts); nAll.AddRange(t.norms); subs.Add(t.tris.Select(i => i + off).ToArray()); }
        mesh.SetVertices(vAll); mesh.SetNormals(nAll); mesh.subMeshCount = subs.Count;
        for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i);
        mesh.RecalculateBounds();
        mesh = SaveFenceMesh(mesh, "Playground_Deck");
        var mats = DeckTones.Select((c, i) => Mat("Playground_DeckWood" + i, "Assets/FuwaCourse/Materials/Goal_Ground.mat", c)).ToList();
        mats.Add(Mat("Playground_DeckUnder", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(0.36f, 0.25f, 0.17f)));
        foreach (var dm in mats)
        {
            // 元の Goal_Ground の草の模様（三角の葉っぱ・ムラ）は木には要らない
            foreach (var pn in new[] { "_GroundPatch", "_GroundFine", "_TopStrength", "_StrataStrength", "_NoiseStrength" }) if (dm.HasProperty(pn)) dm.SetFloat(pn, 0f);
            EditorUtility.SetDirty(dm);
        }
        // もうあれば物はそのまま使ってメッシュだけ差しかえる（2026-10-16）
        var g = root.Find("Deck")?.gameObject;
        if (g == null) { g = new GameObject("Deck"); g.transform.SetParent(root, false); g.transform.localPosition = Vector3.zero; g.AddComponent<MeshFilter>(); g.AddComponent<MeshRenderer>(); g.AddComponent<MeshCollider>(); }
        g.layer = floorLayer;
        g.GetComponent<MeshFilter>().sharedMesh = mesh; g.GetComponent<MeshRenderer>().sharedMaterials = mats.ToArray();
        // 当たり：上の面の高さの、すき間のない帯（ふわふわが板のすき間で引っかからないように）
        var col = new Mesh { name = "Playground_DeckCollider", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        var cv = new List<Vector3>(); var ct = new List<int>();
        for (int sIdx = 0; sIdx <= useg; sIdx++)
        {
            float t = Mathf.Lerp(a0, a1, (float)sIdx / useg);
            cv.Add(P(t, DeckInR, DeckH)); cv.Add(P(t, rOut, DeckH)); cv.Add(P(t, DeckInR, -0.05f)); cv.Add(P(t, rOut, -0.05f));
            if (sIdx == 0) continue;
            int b = sIdx * 4, p = b - 4;
            ct.AddRange(new[] { p, p + 1, b + 1, p, b + 1, b });              // 上
            ct.AddRange(new[] { p + 2, p, b, p + 2, b, b + 2 });              // 内側のふち
            ct.AddRange(new[] { p + 1, p + 3, b + 3, p + 1, b + 3, b + 1 });  // 外側のふち
        }
        col.SetVertices(cv); col.SetTriangles(ct, 0); col.RecalculateNormals(); col.RecalculateBounds();
        col = SaveFenceMesh(col, "Playground_DeckCollider");
        (g.GetComponent<MeshCollider>() ?? g.AddComponent<MeshCollider>()).sharedMesh = col;
        GameObjectUtility.SetStaticEditorFlags(g, (StaticEditorFlags)~0);
        return "deck rows " + rows + " boards " + boards + " verts " + vAll.Count;
    }

    // 柵は頂点が多いので 32bit の番号で保存（SaveGenMesh は 16bit のまま上書きする）
    static Mesh SaveFenceMesh(Mesh m, string name)
    {
        const string dir = "Assets/FuwaCourse/Generated/Playground";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Playground");
        string p = dir + "/" + name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        // 前のアセットに上書き（GUID を変えない）。CopySerialized だと2つ目以降のサブメッシュが描かれない罠があるので Clear→Set 系で入れ直す
        if (old != null)
        {
            old.Clear(false); old.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            old.SetVertices(m.vertices); old.SetNormals(m.normals);
            if (m.uv.Length > 0) old.uv = m.uv;
            old.subMeshCount = m.subMeshCount; for (int s = 0; s < m.subMeshCount; s++) old.SetTriangles(m.GetTriangles(s), s);
            old.RecalculateBounds(); FuwaGunWeld.Weld(old); LightmapUV(old, name); EditorUtility.SetDirty(old);
            Object.DestroyImmediate(m);
            return old;
        }
        AssetDatabase.CreateAsset(m, p); FuwaGunWeld.Weld(m); LightmapUV(m, name);
        return m;
    }

    // 床と柵は影を焼くので、ライトマップ用UV（uv1）を作る。無いとスマホでは無い uv1 にでたらめな値が入って床がまだらになる（2026-10-19）
    static void LightmapUV(Mesh m, string name)
    {
        if (name.EndsWith("Collider")) return;
        UnwrapParam p; UnwrapParam.SetDefaults(out p); Unwrapping.GenerateSecondaryUVSet(m, p);
    }

    static Mesh SaveGenMesh(Mesh m, string name)
    {
        const string dir = "Assets/FuwaCourse/Generated/Playground";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Playground");
        string p = dir + "/" + name + ".asset"; m.name = name;
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (old == null) { AssetDatabase.CreateAsset(m, p); return m; }
        old.Clear(); old.SetVertices(m.vertices); old.SetNormals(m.normals); old.uv = m.uv; old.SetTriangles(m.triangles, 0); old.RecalculateBounds();
        EditorUtility.SetDirty(old); Object.DestroyImmediate(m); return old;
    }

    static Material Mat(string name, string basePath, Color c)
    {
        string p = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(AssetDatabase.LoadAssetAtPath<Material>(basePath)) { name = name }; AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_Color", c);
        // 元にした地面の材質（Goal_Ground）の草の模様は、地面以外には要らない（2026-10-13 てじゃ「ミニソファに芝模様はいってる？」）
        if (basePath.EndsWith("Goal_Ground.mat") && !name.Contains("Grass"))
            foreach (var pn in new[] { "_GroundPatch", "_GroundFine", "_TopStrength", "_StrataStrength", "_NoiseStrength" }) if (m.HasProperty(pn)) m.SetFloat(pn, 0f);
        EditorUtility.SetDirty(m);
        return m;
    }


    // 写真館のモニター・CAMボタン・人物ボタンを、モニターとの位置関係のままコピーして pos に置く（表をスポーンの方へ）。FuwaFollowCam に登録
    static string Monitor(Transform root, Vector3 pos, Vector3 look)
    {
        var studio = RootGo("FuwaLobby").transform.Find("PhotoStudio");
        var mon = studio.Find("Monitor"); var spot = studio.Find("MonitorSpot");
        var holder = new GameObject("Monitor").transform; holder.SetParent(root, false);
        var face = pos - look; face.y = 0f;
        holder.position = pos; holder.rotation = Quaternion.LookRotation(face.normalized);
        // 写真館のモニターの足もと（MonitorSpot）とモニターの向きを holder に合わせる
        var srcFrame = Matrix4x4.TRS(spot.position, Quaternion.LookRotation(mon.forward), Vector3.one);
        var copies = new Dictionary<string, Transform>();
        foreach (var n in new[] { "Monitor", "CamButton", "TargetButton", "CamZone" })
        {
            var src = studio.Find(n);
            var g = Object.Instantiate(src.gameObject, holder, true); g.name = n;
            var lp = srcFrame.inverse.MultiplyPoint3x4(src.position);
            var lr = Quaternion.Inverse(Quaternion.LookRotation(mon.forward)) * src.rotation;
            g.transform.position = holder.TransformPoint(lp); g.transform.rotation = holder.rotation * lr;
            copies[n] = g.transform;
        }
        var fc = Object.FindObjectsOfType<FuwaFollowCam>(true).First();
        var m = copies["Monitor"];
        var so = new SerializedObject(fc);
        void Add(SerializedProperty p, Object v) { p.arraySize++; p.GetArrayElementAtIndex(p.arraySize - 1).objectReferenceValue = v; }
        Add(so.FindProperty("extraScreens"), m.Find("Screen").GetComponent<Renderer>());
        Add(so.FindProperty("extraOffMarks"), m.Find("Off").gameObject);
        Add(so.FindProperty("extraNames"), m.Find("Name").GetComponent<TMPro.TextMeshPro>());
        // VRChatカメラを入れる球：写真館の球（fc.zone）と同じ所をコピーから探す
        var zSrc = studio.Find("CamZone"); var zc = copies["CamZone"];
        var zt = fc.zone == zSrc ? zc : (fc.zone != null && fc.zone.IsChildOf(zSrc) ? zc.Find(Rel(fc.zone, zSrc)) : null);
        var zr = fc.zoneRenderer != null && fc.zoneRenderer.transform.IsChildOf(zSrc) ? (fc.zoneRenderer.transform == zSrc ? zc : zc.Find(Rel(fc.zoneRenderer.transform, zSrc))) : null;
        Add(so.FindProperty("extraZones"), zt);
        Add(so.FindProperty("extraZoneRenderers"), zr != null ? zr.GetComponent<Renderer>() : null);
        // ボタンの文字・札（元と同じ並びで足す）
        var camSrc = studio.Find("CamButton"); var tgtSrc = studio.Find("TargetButton");
        var mt = so.FindProperty("modeTexts"); var mp = so.FindProperty("modePills"); int n0 = mt.arraySize;
        for (int i = 0; i < n0; i++)
        {
            var t = mt.GetArrayElementAtIndex(i).objectReferenceValue as Component; if (t == null || !t.transform.IsChildOf(camSrc)) continue;
            var ct = copies["CamButton"].Find(Rel(t.transform, camSrc));
            Add(mt, ct.GetComponent<TMPro.TextMeshProUGUI>());
            Component pill = i < mp.arraySize ? mp.GetArrayElementAtIndex(i).objectReferenceValue as Component : null;
            var cp = pill != null && pill.transform.IsChildOf(camSrc) ? copies["CamButton"].Find(Rel(pill.transform, camSrc)) : null;
            mp.arraySize = mt.arraySize; mp.GetArrayElementAtIndex(mp.arraySize - 1).objectReferenceValue = cp != null ? cp.GetComponent<UnityEngine.UI.Image>() : null;
        }
        var tt = so.FindProperty("targetTexts"); int t0 = tt.arraySize;
        for (int i = 0; i < t0; i++)
        {
            var t = tt.GetArrayElementAtIndex(i).objectReferenceValue as Component; if (t == null || !t.transform.IsChildOf(tgtSrc)) continue;
            Add(tt, copies["TargetButton"].Find(Rel(t.transform, tgtSrc)).GetComponent<TMPro.TextMeshProUGUI>());
        }
        so.ApplyModifiedPropertiesWithoutUndo(); Sync(fc);
        // 言語の切り替え：元の文字と同じ日本語・英語で登録
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        int added = 0;
        if (lang != null)
        {
            var lso = new SerializedObject(lang);
            foreach (var kv in copies)
            {
                var srcTop = studio.Find(kv.Key);
                foreach (var t in kv.Value.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                {
                    var srcT = srcTop.Find(Rel(t.transform, kv.Value)); if (srcT == null) continue;
                    int i = System.Array.IndexOf(lang.texts, srcT.GetComponent<TMPro.TextMeshPro>()); if (i < 0) continue;
                    AddLang(lso, "texts", "ja", "en", t, lang.ja[i], lang.en[i]); added++;
                }
                foreach (var t in kv.Value.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                {
                    var srcT = srcTop.Find(Rel(t.transform, kv.Value)); if (srcT == null) continue;
                    int i = System.Array.IndexOf(lang.uiTexts, srcT.GetComponent<TMPro.TextMeshProUGUI>()); if (i < 0) continue;
                    AddLang(lso, "uiTexts", "uiJa", "uiEn", t, lang.uiJa[i], lang.uiEn[i]); added++;
                }
            }
            lso.ApplyModifiedPropertiesWithoutUndo(); Sync(lang);
        }
        return "monitor ok, lang +" + added;
    }

    static string Rel(Transform t, Transform top) { return t == top ? "" : (t.parent == top ? t.name : Rel(t.parent, top) + "/" + t.name); }

    static void AddLang(SerializedObject lso, string a, string j, string e, Object t, string ja, string en)
    {
        foreach (var pn in new[] { a, j, e }) lso.FindProperty(pn).arraySize++;
        int k = lso.FindProperty(a).arraySize - 1;
        lso.FindProperty(a).GetArrayElementAtIndex(k).objectReferenceValue = t;
        lso.FindProperty(j).GetArrayElementAtIndex(k).stringValue = ja;
        lso.FindProperty(e).GetArrayElementAtIndex(k).stringValue = en;
    }

    // 作り直しの前に：前のチルルームのモニターを FuwaFollowCam・言語の登録から外す
    static void UnregisterMonitor(Transform oldRoot)
    {
        if (oldRoot == null) return;
        var fc = Object.FindObjectsOfType<FuwaFollowCam>(true).FirstOrDefault();
        if (fc != null)
        {
            var so = new SerializedObject(fc);
            // modeTexts と modePills は同じ並びなので、同じ番号を一緒に外す
            var mt = so.FindProperty("modeTexts"); var mp = so.FindProperty("modePills");
            for (int i = mt.arraySize - 1; i >= 0; i--)
            {
                if (!Under(mt.GetArrayElementAtIndex(i).objectReferenceValue, oldRoot)) continue;
                mt.GetArrayElementAtIndex(i).objectReferenceValue = null; mt.DeleteArrayElementAtIndex(i);
                if (i < mp.arraySize) { mp.GetArrayElementAtIndex(i).objectReferenceValue = null; mp.DeleteArrayElementAtIndex(i); }
            }
            // extraZones と extraZoneRenderers も同じ並び
            var ez = so.FindProperty("extraZones"); var ezr = so.FindProperty("extraZoneRenderers");
            for (int i = ez.arraySize - 1; i >= 0; i--)
            {
                var o = ez.GetArrayElementAtIndex(i).objectReferenceValue;
                if (o != null && !Under(o, oldRoot)) continue;
                ez.GetArrayElementAtIndex(i).objectReferenceValue = null; ez.DeleteArrayElementAtIndex(i);
                if (i < ezr.arraySize) { ezr.GetArrayElementAtIndex(i).objectReferenceValue = null; ezr.DeleteArrayElementAtIndex(i); }
            }
            foreach (var pn in new[] { "extraScreens", "extraOffMarks", "extraNames", "targetTexts" })
            {
                var p = so.FindProperty(pn);
                for (int i = p.arraySize - 1; i >= 0; i--)
                {
                    if (!Under(p.GetArrayElementAtIndex(i).objectReferenceValue, oldRoot)) continue;
                    p.GetArrayElementAtIndex(i).objectReferenceValue = null; p.DeleteArrayElementAtIndex(i);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo(); Sync(fc);
        }
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        if (lang != null)
        {
            var lso = new SerializedObject(lang);
            foreach (var (a, j, e) in new[] { ("texts", "ja", "en"), ("uiTexts", "uiJa", "uiEn") })
            {
                var pa = lso.FindProperty(a); var pj = lso.FindProperty(j); var pe = lso.FindProperty(e);
                for (int i = pa.arraySize - 1; i >= 0; i--)
                {
                    if (!Under(pa.GetArrayElementAtIndex(i).objectReferenceValue, oldRoot)) continue;
                    pa.GetArrayElementAtIndex(i).objectReferenceValue = null; pa.DeleteArrayElementAtIndex(i);
                    if (i < pj.arraySize) pj.DeleteArrayElementAtIndex(i);
                    if (i < pe.arraySize) pe.DeleteArrayElementAtIndex(i);
                }
            }
            lso.ApplyModifiedPropertiesWithoutUndo(); Sync(lang);
        }
    }

    static bool Under(Object o, Transform root)
    {
        var tr = o is Component c ? c.transform : o is GameObject g ? g.transform : null;
        return tr != null && tr.IsChildOf(root);
    }

    static GameObject RootGo(string n) { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n); }

    // レーザーで押すボタン：ロビーの「みんなで」ボタン（FuwaLobby/HowToPlay/TogetherButton）をコピーして、押したら target に evt を送る
    public static TMPro.TextMeshProUGUI LaserButton(Transform parent, string name, Vector3 pos, Quaternion rot, float widthK, string text, UdonSharpBehaviour target, string evt)
    {
        var src = RootGo("FuwaLobby").transform.Find("HowToPlay/TogetherButton");
        var g = Object.Instantiate(src.gameObject, parent, false); g.name = name;
        g.transform.SetPositionAndRotation(pos, rot); g.transform.localScale = src.lossyScale;
        var canvas = (RectTransform)g.transform.Find("Canvas");
        canvas.sizeDelta = new Vector2(canvas.sizeDelta.x * widthK, canvas.sizeDelta.y);
        var bc = canvas.GetComponent<BoxCollider>(); bc.size = new Vector3(bc.size.x * widthK, bc.size.y, bc.size.z);
        var btn = g.GetComponentInChildren<UnityEngine.UI.Button>(true);
        var so = new SerializedObject(btn);
        var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        calls.arraySize = 1;
        var c0 = calls.GetArrayElementAtIndex(0);
        c0.FindPropertyRelative("m_Target").objectReferenceValue = UdonSharpEditorUtility.GetBackingUdonBehaviour(target);
        c0.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = "VRC.Udon.UdonBehaviour, VRC.Udon";
        c0.FindPropertyRelative("m_MethodName").stringValue = "SendCustomEvent";
        c0.FindPropertyRelative("m_Mode").intValue = 5;
        c0.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = evt;
        c0.FindPropertyRelative("m_CallState").intValue = 2;
        so.ApplyModifiedPropertiesWithoutUndo();
        var t = g.GetComponentInChildren<TMPro.TextMeshProUGUI>(true); t.text = text; EditorUtility.SetDirty(t);
        return t;
    }

    // ふわふわ設定のスイッチの見た目：白いボタン＋オレンジの枠、文字は紺（2026-10-13 てじゃ「オレンジ枠に白ボタン、文字は紺にしよ」）。
    // 枠はボタンの後ろに一回り大きい角丸の板（RoundedPanel）。角の丸みは最後に FuwaButtonCorners がそろえる
    static readonly Color SwitchFrame = new Color(0.4f, 0.55f, 0.88f),   /* 枠は青（オレンジから。2026-10-13 てじゃ「枠を青にしてみて」） */ SwitchFace = new Color(1f, 0.98f, 0.96f), SwitchText = new Color(0.27f, 0.36f, 0.62f);
    const float SwitchFrameW = 26f;
    public static void OutlinedStyle(Transform sw)
    {
        var btn = sw.GetComponentInChildren<UnityEngine.UI.Button>(true); var img = btn.GetComponent<UnityEngine.UI.Image>();
        var cb = btn.colors; cb.normalColor = SwitchFace; cb.selectedColor = SwitchFace; cb.highlightedColor = new Color(1f, 0.94f, 0.9f); cb.pressedColor = new Color(1f, 0.86f, 0.8f); btn.colors = cb;
        img.color = Color.white;
        var t = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true); t.color = SwitchText;
        var old = btn.transform.parent.Find("Frame"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var fr = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
        var frt = (RectTransform)fr.transform; var brt = (RectTransform)btn.transform;
        frt.SetParent(brt.parent, false); frt.SetSiblingIndex(brt.GetSiblingIndex());
        frt.anchorMin = brt.anchorMin; frt.anchorMax = brt.anchorMax; frt.pivot = brt.pivot; frt.anchoredPosition3D = brt.anchoredPosition3D + new Vector3(0f, 0f, 6f); frt.localRotation = brt.localRotation; frt.localScale = brt.localScale;   // 少し奥へ（同じ面だとボタンの UI 材質が深さを書くのでちらついた）
        frt.sizeDelta = brt.sizeDelta + Vector2.one * SwitchFrameW * 2f;
        var fi = fr.GetComponent<UnityEngine.UI.Image>(); fi.sprite = img.sprite; fi.type = UnityEngine.UI.Image.Type.Sliced; fi.material = img.material; fi.color = SwitchFrame; fi.raycastTarget = false;
        PillCorners(sw);
        EditorUtility.SetDirty(btn); EditorUtility.SetDirty(t);
    }

    // 白い面とフチの角を、どっちも高さの半分の「まんまる」にそろえる（2026-10-16 てじゃ「フチがまるみがあってない」）。
    // 角の大きさが板より大きいと縦だけ縮められて横長のだ円の角になり、フチ（ひと回り大きい）と中心がずれて角だけフチが太く見えていた
    public static void PillCorners(Transform sw)
    {
        var btn = sw.GetComponentInChildren<UnityEngine.UI.Button>(true); if (btn == null) return;
        var imgs = new List<UnityEngine.UI.Image> { btn.GetComponent<UnityEngine.UI.Image>() };
        var fr = btn.transform.parent.Find("Frame"); if (fr != null) imgs.Add(fr.GetComponent<UnityEngine.UI.Image>());
        foreach (var im in imgs)
        {
            if (im == null || im.sprite == null || im.canvas == null) continue;
            float k = im.sprite.pixelsPerUnit / im.canvas.referencePixelsPerUnit;
            float r = im.rectTransform.rect.height * 0.5f;
            im.pixelsPerUnitMultiplier = im.sprite.border.y / (k * r);
            EditorUtility.SetDirty(im);
        }
    }

    // 遊び方の看板の板（Panel・Border）と文字（Text）をコピーした横長のボタン。文字を返す。表（-Z）を手前に
    static TMPro.TextMeshPro MakeButton(Transform parent, Transform howTo, string name, Vector3 pos, Quaternion rot)
    {
        var r = new GameObject(name).transform; r.SetParent(parent, false); r.position = pos; r.rotation = rot;
        r.localScale = Vector3.one * 0.5f;
        TMPro.TextMeshPro t = null;
        foreach (var n in new[] { "Panel", "Border", "Text" })
        {
            var src = howTo.Find(n); if (src == null) continue;
            var g = Object.Instantiate(src.gameObject, r, false); g.name = n;
            g.transform.localPosition = new Vector3(0f, 0f, src.localPosition.z); g.transform.localRotation = Quaternion.identity;
            if (n == "Text")
            {
                t = g.GetComponent<TMPro.TextMeshPro>(); t.text = name; t.alignment = TMPro.TextAlignmentOptions.Center; t.enableAutoSizing = false; t.fontSize = 3f;
                t.rectTransform.sizeDelta = new Vector2(t.rectTransform.sizeDelta.x, 1f);
            }
            else { var sr = g.GetComponent<SpriteRenderer>(); if (sr != null) sr.size = new Vector2(sr.size.x, sr.size.y * 0.32f); }
        }
        var bc = r.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(3.8f, 1.2f, 0.15f); bc.center = new Vector3(0f, 0f, 0.03f);
        // 押せるけど、人はすり抜ける（2026-10-12 てじゃ「あそびばの一部UIに当たり判定がある」）
        r.gameObject.layer = 17;   // Walkthrough
        return t;
    }

    // 遊び方の看板の文字（日本語・英語の切り替えにも登録してあるので両方書き換える）
    static void SetHowToText(TMPro.TextMeshPro t)
    {
        const string ja = "<align=center><size=150%><color=#FF6B5C>ふわふわあそびば</color></size></align>\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>キノコの傘を撃つと、ふわふわが出てくる\n<pos=1.1em>みんなで吹いて、のんびり遊ぼう\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>スイッチで、サイズ・色・出せる数・\n<pos=1.1em>落ちたら消えるかを変えられる\n<pos=1.1em>（設定はみんなで共通）\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>巨大は重いので、みんなで押そう\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>{pc,vr|エアガンを手元に呼ぶ}{mo|エアガンは右上のボタンで呼べる}\n<pos=1.1em>{pc|Fキーを押す}{vr|右スティックを下に倒し続ける}{mo|（台をタップでも呼べる）}";
        const string en = "<align=center><size=150%><color=#FF6B5C>Fluff Playground</color></size></align>\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Shoot the mushroom cap to pop out a fluff\n<pos=1.1em>Blow them around together and relax\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Switches change the size, color,\n<pos=1.1em>max count, and whether they pop on landing\n<pos=1.1em>(shared by everyone)\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Giant ones are heavy: push together!\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>{pc,vr|Summon your air gun}{mo|Call your air gun: top-right button}\n<pos=1.1em>{pc|Press the F key}{vr|Hold the right stick down}{mo|(or tap a stand)}";
        t.text = ja; EditorUtility.SetDirty(t);
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault(); if (lang == null) return;
        int i = System.Array.IndexOf(lang.texts, t); if (i < 0) return;
        var so = new SerializedObject(lang);
        so.FindProperty("ja").GetArrayElementAtIndex(i).stringValue = ja; so.FindProperty("en").GetArrayElementAtIndex(i).stringValue = en;
        so.ApplyModifiedPropertiesWithoutUndo(); Sync(lang);
    }

    static void EnsureProgram(string name)
    {
        string path = "Assets/FuwaCourse/Scripts/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path) != null) return;
        var prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/" + name + ".cs");
        AssetDatabase.CreateAsset(prog, path); AssetDatabase.SaveAssets();
    }

    static void SetU(Object o, string field, Object v) { var s = new SerializedObject(o); var p = s.FindProperty(field); if (p != null) { p.objectReferenceValue = v; s.ApplyModifiedPropertiesWithoutUndo(); } }
    static void Sync(Object o) { if (o is UdonSharpBehaviour u) UdonSharpEditorUtility.CopyProxyToUdon(u); EditorUtility.SetDirty(o); }
}
