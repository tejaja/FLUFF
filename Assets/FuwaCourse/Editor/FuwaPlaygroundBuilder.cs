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
    const float MushScale = 1.31f;              // コース（チャレンジ）のスタートのキノコと同じ大きさ（傘の直径1.05m。2026-10-13 てじゃ「中央のきのこまだでかい」。前は1.8、その前は3倍）
    // 配置（2026-10-12 てじゃ「真ん中にキノコ、右奥にデカソファでモニタを眺めるエリア、左奥にフワフワギミックエリア（未定）」）。スポーンは手前（-Z）
    // 島の大きさ：古いみんなでロビーの島（半径14m）の何倍か（2026-10-13 てじゃ「ちょっと広い」→ 半径10.5m。手前半分がほぼ空だった）
    const float IslandScale = 0.75f;
    // 2026-10-13 てじゃの並べ直し：「モニタ一式は入って正面からやや左、きのこは中央、🔫はキノコの右手前。モニタの右横の正面の柵沿いに設定ボタンを丸ごと、
    // おもちゃリセットもその近く、その右横からおもちゃ（最初に扇風機とゴールリング、そこから各コースのギミックをぐるっと柵沿いに）。モニタから左側は空けておく」
    // 角度は島の真ん中から：入口から見て奥（+Z）が0°、右回り（+X 側）がプラス。入口（スポーン）は180°
    static Vector3 OnRing(float deg, float r) { return new Vector3(Mathf.Sin(deg * Mathf.Deg2Rad), 0f, Mathf.Cos(deg * Mathf.Deg2Rad)) * r; }
    static readonly Vector3 MushLocal = Vector3.zero;   // 島の真ん中
    const float PanelAngle = 16f;   // 設定ボタン一式（スイッチ4つ＋言語・音量＋人数・チャレンジ・タイトル）：正面奥の柵沿い、モニタの右
    static readonly Vector3 PanelLocal = OnRing(PanelAngle, 9.0f);
    const float PanelYaw = PanelAngle;   // 島の真ん中から読める向き
    // 観戦リビング（ラグの中心）：正面からやや左の奥。テレビは柵側、ソファは真ん中側
    static readonly Vector3 LivingLocal = OnRing(-22f, 5.6f);
    // おもちゃ（柵沿い、右回りに）：もどすボタン → 扇風機 → ゴールリング → コース1〜5のおもちゃ
    const float ResetAngle = 32f, FanAngle = 45f, RingAngle = 60f;
    // 遊び方の看板の言語・音量ボタンはスイッチのボードの下（BoardButtons）
    // 島の外の物（FuwaPlayground の直下。前は手で置いていた位置×島の大きさ）：スポーン、ワープ2つ、エアガンの台（2026-10-13 てじゃ「銃の台も真ん中に」→ キノコの右手前、スイッチの反対）
    static readonly Vector3 SpawnLocal = new Vector3(0f, 0f, -8.8f) * IslandScale;
    // ワープの輪2つ（ハブへ ReturnToHub・チャレンジへ ToSolo）は見た目なし（2026-10-13 てじゃ「ボタンワープあるしポータルいらんかも」）。
    // 設定ボタンの「タイトルにもどる」「チャレンジ」がこのワープの DoWarp を呼ぶので、ワープの係（FuwaCourseWarp）だけ残して見た目と踏む判定を消す
    static readonly Vector3 ReturnWarpLocal = new Vector3(-1.0f, 0f, -8.8f) * IslandScale;
    static readonly Vector3 SoloWarpLocal = new Vector3(1.0f, 0f, -8.8f) * IslandScale;
    static readonly Vector3 GunStandLocal = new Vector3(1.4f, 0f, -1.5f);   // キノコの右手前
    const float GunStandYaw = 304.4f;

    [MenuItem("FuwaCourse/あそびば/あそびばを作り直す")]
    public static void Menu() { Debug.Log(Run()); }

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
        foreach (var o in new[] { coop.Find("Room"), sys.Find("PlaygroundSpawner") }) if (o != null) Object.DestroyImmediate(o.gameObject);
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
        foreach (var gm in mush.GetComponentsInChildren<FuwaGoalMarker>(true)) { SetU(gm, "manager", Object.FindObjectsOfType<FuwaRespawnManager>(true).FirstOrDefault()); Sync(gm); }

        // ふわふわ（ロビーの共有ふわふわのコピー）を Pool 個。待つ位置はキノコの中
        var fluffSrc = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Fluff.prefab");
        Vector3 home = mush.transform.position + Vector3.up * (FluffRel.y - MushRel.y) * MushScale;
        var pool = new GameObject("Fluffs").transform; pool.SetParent(root, false);
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
        var labels = new TMPro.TextMeshProUGUI[4];
        var events = new[] { "NextSize", "NextColor", "NextLimit", "TogglePop" };
        for (int k = 0; k < 4; k++)
        {
            int col = k % 2, row = k / 2;
            // 大きさは下の言語・音量ボタンと同じ（幅1.36・高さ0.4。2026-10-13 てじゃ「下のボタンとサイズあわせよ」）
            var pos = pc + rot * new Vector3(col == 0 ? -BoardBottomX : BoardBottomX, row == 0 ? BoardSideY[0] : BoardSideY[1], 0f);
            labels[k] = LaserButton(panel, "Switch" + k, pos, rot, 0.99f, "", sp, events[k]);
            panel.Find("Switch" + k).localScale = Vector3.one;
        }
        // 支柱はなし（2026-10-13 てじゃ「柱なくそう」）
        BoardButtons(coop, panel, pc, rot, howTo);
        sp.sizeLabel = labels[0]; sp.colorLabel = labels[1]; sp.limitLabel = labels[2]; sp.popLabel = labels[3];
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
        log.Add(Toys(root, ac, floorY, sp, howTo, look));

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
    // 2026-10-13 並べ直し：ゴールリングの右から右回りに柵沿い（76°〜152°、入口の右手前まで）。
    // となりとの間隔（19°＝弧で2.7m）はおもちゃの大きさ（竜巻の見た目2.45m・重力の球2.2m・ぬいぐるみとイバラ／跳ねるキノコは横に2.6m）から決めた
    static readonly float[] UnlockAngles = { 76f, 95f, 114f, 133f, 152f };
    const float UnlockRadius = 8.2f;
    static readonly Vector3[] UnlockLocal = UnlockAngles.Select(a => new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad)) * UnlockRadius).ToArray();

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
            var marker = coop.Find("GunStand_Playground_Marker");
            Vector3 mOff = marker != null ? marker.position - stand.position : Vector3.zero;
            Quaternion mRot = marker != null ? Quaternion.Inverse(stand.rotation) * marker.rotation : Quaternion.identity;
            stand.position = Ground(GunStandLocal, stand);
            stand.rotation = Quaternion.Euler(0f, GunStandYaw, 0f);
            if (marker != null) { marker.position = stand.position + new Vector3(0f, mOff.y, 0f); marker.rotation = stand.rotation * mRot; }
        }
    }

    static string Toys(Transform root, Vector3 ac, float floorY, FuwaPlaygroundSpawner sp, Transform howTo, Vector3 look)
    {
        var toys = new GameObject("Toys").transform; toys.SetParent(root, false);
        Vector3 O = new Vector3(ac.x, floorY, ac.z);
        Vector3 G = O + OnRing(RingAngle, 7.6f);   // ゴールリングの真下
        Vector3 Fc = O + OnRing(FanAngle, 8.4f);   // 扇風機の並びの真ん中
        var toCenter = (O - Fc); toCenter.y = 0f; toCenter.Normalize();
        var side = new Vector3(toCenter.z, 0f, -toCenter.x);
        var fanMeshes = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaCourse/Models/PlaygroundFan.fbx").OfType<Mesh>().ToDictionary(m => m.name);
        var uchiMeshes = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaCourse/Models/PlaygroundUchiwa.fbx").OfType<Mesh>().ToDictionary(m => m.name);
        var fanBodyM = Mat("Playground_FanBody", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(0.97f, 0.97f, 0.98f));   // 白いボディに水色の羽（2026-10-13 てじゃ）
        var fanBladeM = Mat("Playground_FanBlades", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(0.6f, 0.84f, 1f));
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
            pk.InteractionText = uchiwa ? "うちわ" : "せんぷうき"; pk.UseText = uchiwa ? "あおぐ" : "風 ON/OFF"; pk.proximity = 2f;
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
                var bl = Mesh(go.transform, "Blades", fanMeshes["Chill_FanBlades"], new[] { fanBodyM, fanBladeM }, new Vector3(0f, 0.36f, 0f)).transform;   // 1つ目は羽の真ん中のキャップ（白。2026-10-13 てじゃ）
                nozzle.localPosition = new Vector3(0f, 0.36f, 0.04f);
                fan.blades = bl;
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
        for (int i = 0; i < 3; i++) list.Add(Held("Fan" + i, Fc + side * (-0.55f + i * 0.55f) + Vector3.up * 1.0f, Quaternion.LookRotation(-toCenter), false));   // 風の出る正面を柵の方へ（真ん中から近づいて持つと、風が向こう向きになる。2026-10-13 てじゃ）
        // うちわはなし（2026-10-12 てじゃ「ちょっと変だからなしに、うちわっぽくするのむずそう」）。FuwaHandFan の gustMode は残してある
        // ゴール風リング：広場の真ん中に浮かべる（穴をキノコの方へ向ける）。持って動かせる
        var ringT = new GameObject("Ring").transform; ringT.gameObject.layer = 13; ringT.SetParent(toys, false);
        { var tcR = O - G; tcR.y = 0f; ringT.SetPositionAndRotation(G + Vector3.up * 1.7f, Quaternion.LookRotation(tcR.normalized)); }
        { var rb = ringT.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; }
        var gate = ringT.gameObject.AddComponent<BoxCollider>(); gate.isTrigger = true; gate.size = new Vector3(1.7f, 1.7f, 0.3f);
        var rpk = ringT.gameObject.AddComponent<VRC.SDK3.Components.VRCPickup>();
        rpk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; rpk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any; rpk.InteractionText = "リング"; rpk.proximity = 2f;
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
            var lbl = MakeButton(toys, howTo, "Locked" + (i + 1), sl + Vector3.up * 0.9f, Quaternion.LookRotation(outDir));
            string ja = "コース" + (i + 1) + "「" + courseJa[i] + "」を\nだれかがクリアすると出てくるよ";
            string en = "Appears when someone clears\nCourse " + (i + 1) + " " + courseEn[i];
            lbl.text = ja; lbl.fontSize = 1.8f;
            lockedSigns[i] = lbl.transform.parent.gameObject;
            Object.DestroyImmediate(lockedSigns[i].GetComponent<BoxCollider>());   // 押さない札なので当たり判定なし
            lbl.transform.parent.localScale = Vector3.one * 0.4f;
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

        // 「おもちゃを戻す」：扇風機・うちわ以外の持てるおもちゃも
        var othersList = new List<GameObject> { ringT.gameObject };
        foreach (var u in unlockToys) foreach (var os in u.GetComponentsInChildren<VRC.SDK3.Components.VRCObjectSync>(true)) othersList.Add(os.gameObject);
        resetOthers = othersList.ToArray();

        var ul = UdonSharpUndo.AddComponent<FuwaPlaygroundUnlocks>(toys.gameObject);
        ul.spawner = sp; ul.toys = unlockToys; ul.locked = lockedSigns;
        UdonSharpEditorUtility.CopyProxyToUdon(ul);
        { var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(ul); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); } }

        // 戻すボタン（並びの下）：レーザーで押すボタン
        var rsGo = new GameObject("ResetToys"); rsGo.transform.SetParent(toys, false);
        var rs = UdonSharpUndo.AddComponent<FuwaHandFanReset>(rsGo); rs.fans = list.ToArray(); rs.others = resetOthers; UdonSharpEditorUtility.CopyProxyToUdon(rs);
        var rub = UdonSharpEditorUtility.GetBackingUdonBehaviour(rs); if (rub != null) { rub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(rub); }
        var rsPos = O + OnRing(ResetAngle, 8.6f); var rsOut = rsPos - O; rsOut.y = 0f;
        var btnLbl = LaserButton(toys, "ResetToysButton", rsPos + Vector3.up * 0.55f, Quaternion.LookRotation(rsOut.normalized), 1.3f, "おもちゃをもどす", rs, "ResetAll");   // 「戻」はボタンのフォントに無くて□になっていた（2026-10-13）
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        if (lang != null) { var lso = new SerializedObject(lang); AddLang(lso, "uiTexts", "uiJa", "uiEn", btnLbl, "おもちゃをもどす", "Return toys"); lso.ApplyModifiedPropertiesWithoutUndo(); Sync(lang); }
        AddTestUnlockAll(toys, sp);
        return "toys " + list.Count;
    }

    // テスト用：クリア報酬のおもちゃを全部出すボタン（マスターにだけ見える）。2026-10-13 てじゃ「あとで消す」→ 消す時はこの呼び出しと関数を消す
    static void AddTestUnlockAll(Transform toys, FuwaPlaygroundSpawner sp)
    {
        foreach (var n in new[] { "TestUnlockAll", "TestUnlockAllButton" }) { var old = toys.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject); }
        EnsureProgram("FuwaTestUnlockAll");
        var go = new GameObject("TestUnlockAll"); go.transform.SetParent(toys, false);
        var tu = UdonSharpUndo.AddComponent<FuwaTestUnlockAll>(go); tu.spawner = sp;
        // 「おもちゃを戻す」の横（同じ向き、横へ1.6m・少し上）
        var reset = toys.Find("ResetToysButton");
        Vector3 pos = reset.position + reset.right * 1.6f + Vector3.up * 0.35f;
        LaserButton(toys, "TestUnlockAllButton", pos, reset.rotation, 1.3f, "テスト おもちゃぜんぶだす", tu, "UnlockAll");
        tu.button = toys.Find("TestUnlockAllButton").gameObject;
        UdonSharpEditorUtility.CopyProxyToUdon(tu);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(tu); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
    }

    [MenuItem("FuwaCourse/あそびば/テスト用：おもちゃ全部出すボタンを置く")]
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
        var rugM = Mat("Playground_Rug", "Assets/FuwaCourse/Generated/Signs/Sign_Cloud_FFFAF5.mat", new Color(0.97f, 0.95f, 1f));
        rugM.SetFloat("_UnderStrength", 0f); EditorUtility.SetDirty(rugM);   // ラグは真っ白に近く（雲の下の青い影はいらない）
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
        // キノコのランプ：ソファの後ろの両わき
        foreach (var lp in new[] { C - dir * 2.7f - perp * 2.3f, C - dir * 2.7f + perp * 2.3f })
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
            // カメラアングルのボタンは少し右（2026-10-13 てじゃ「もうちょい右に」。前は +0.6）
            if (cb != null) { cb.position = monitorHolder.TransformPoint(new Vector3(sl.x + 0.9f, sl.y - sh - 0.24f, face - 0.03f)); cb.rotation = hr; }
        }
        return "deco ok";
    }

    // 竜巻のたね（持てる玉）＋竜巻（コース1 のつむじ風 Whirl0 のコピー、捕まえない普通の巻き上げに）
    static void FillTornado(Transform slot, FuwaPlaygroundSpawner sp)
    {
        EnsureProgram("FuwaTornadoSeed");
        var src = RootGo("FuwaCourse").GetComponentsInChildren<FuwaWhirlwind>(true).First(w => w.name == "Whirl0");
        var tw = Object.Instantiate(src.gameObject, slot, false); tw.name = "Tornado";
        tw.transform.position = slot.position; tw.transform.rotation = slot.rotation;
        // 動きはかぜのおかのつむじ風（Whirl0）のまま＝捕まえて、溜め撃ちで抜ける（2026-10-13 てじゃ「なるべくコースと同じ動きにしたい」。
        // 前は巻き上げててっぺんから吐き出す形に変えていた）
        Sync(tw.GetComponent<FuwaWhirlwind>());
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
            UdonSharpEditorUtility.CopyProxyToUdon(tr);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(tr); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Continuous; EditorUtility.SetDirty(ub); }
        }
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

    [MenuItem("FuwaCourse/あそびば/重力のたねのエフェクトをまっすぐに")]
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
    static void FillPlush(Transform slot, FuwaPlaygroundSpawner sp)
    {
        foreach (var old in slot.Cast<Transform>().Where(t => t.name.StartsWith("Plush") || t.name.StartsWith("Bramble")).ToList()) Object.DestroyImmediate(old.gameObject);
        EnsureProgram("FuwaPlaygroundPlush"); EnsureProgram("FuwaMovableMushroom");
        var e = RootGo("FuwaCourse4").GetComponentsInChildren<FuwaEnemy>(true).First(x => x.name == "Enemy4");
        float baseR = e.GetComponent<SphereCollider>().radius * e.transform.lossyScale.x;
        Vector3 fwd = slot.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 side = new Vector3(fwd.z, 0f, -fwd.x);

        // ---- イバラ（見た目と判定＝body、持つところ＝handle） ----
        var spSrc = RootGo("FuwaCourse4").GetComponentsInChildren<Transform>(true).First(t => t.name == "SpikePatch4" && t.parent != null && t.parent.name == "Spikes");
        Vector3 bPos = slot.position + side * 1.6f;   // ぬいぐるみと2.8m離す（起きてすぐ自分から飛び込まないように）
        var bBody = new GameObject("BrambleBody").transform; bBody.SetParent(slot, false); bBody.SetPositionAndRotation(bPos, Quaternion.LookRotation(fwd));
        var bg = Object.Instantiate(spSrc.gameObject, bBody, false); bg.name = "SpikePatch";
        bg.transform.localPosition = Vector3.zero; bg.transform.localRotation = Quaternion.identity; bg.transform.localScale = Vector3.one;
        foreach (var t in bBody.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        foreach (var fs in bBody.GetComponentsInChildren<FuwaSpike>(true)) { SetU(fs, "ball", null); Sync(fs); }
        var trig = bBody.GetComponentInChildren<FuwaSpike>(true).GetComponent<BoxCollider>();
        float bR = Mathf.Max(trig.bounds.extents.x, trig.bounds.extents.z) * 0.8f;   // 長方形の判定を円で近似（少し内側）
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

        // ---- ぬいぐるみ ----
        float r = baseR;
        // 地面からの高さはコース4のまものと同じ（毛の芯の下はしが地面すれすれ。前は r*0.72 で浮いて見えた。2026-10-13 てじゃ「もうちょい接地」）
        Physics.SyncTransforms();
        float standUp = Physics.Raycast(e.transform.position + Vector3.up * 0.6f, Vector3.down, out var eg, 5f, 1 << 11, QueryTriggerInteraction.Ignore) ? e.transform.position.y - eg.point.y : 0.2f;
        var root = new GameObject("Plush0"); root.layer = 13; root.transform.SetParent(slot, false);
        root.transform.position = slot.position - side * 1.2f + Vector3.up * standUp;
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
        pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any; pk.InteractionText = "まもののぬいぐるみ（トリガーで起こす・寝かせる）"; pk.UseText = "起こす・寝かせる"; pk.proximity = 2f;
        root.AddComponent<VRC.SDK3.Components.VRCObjectSync>();
        AudioSource Copy(string n, string as_) { var s = e.transform.Find(n); if (s == null) return null; var a = Object.Instantiate(s.gameObject, root.transform, false); a.name = as_; return a.GetComponent<AudioSource>(); }
        var boing = Copy("JumpSfx", "Boing");
        var popSfx = Copy("PopSfx", "PopSfx");
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
        if (roomT != null) { pl.areaCenter = roomT.position; pl.areaRadius = 9.2f; }
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

    [MenuItem("FuwaCourse/あそびば/ぬいぐるみとイバラだけ作り直す")]
    static void MenuRebuildPlush()
    {
        var slot = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "Unlock4" && t.parent != null && t.parent.name == "Toys");
        // 消える持てる物（ネットワークIDを持つ）を一覧から外す
        var gone = slot.GetComponentsInChildren<VRC.SDK3.Components.VRCObjectSync>(true).Select(o => o.gameObject).ToList();
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
            foreach (var os in slot.GetComponentsInChildren<VRC.SDK3.Components.VRCObjectSync>(true)) if (!list.Contains(os.gameObject)) list.Add(os.gameObject);
            p.arraySize = list.Count; for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.ApplyModifiedPropertiesWithoutUndo(); Sync(reset);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(slot.gameObject.scene);
        Debug.Log("plush and bramble rebuilt");
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

    [MenuItem("FuwaCourse/あそびば/跳ねるキノコだけ作り直す")]
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
            foreach (var os in slot.GetComponentsInChildren<VRC.SDK3.Components.VRCObjectSync>(true)) if (!list.Contains(os.gameObject)) list.Add(os.gameObject);
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
    static readonly float[] BoardSideY = { 1.76f, 1.24f, 0.72f };   // 右の列の高さ。上2つはスイッチの2段と、いちばん下は言語・音量の段とそろえる
    const float BoardBottomY = 0.72f, BoardBottomX = 0.75f;
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
            }
            // 角の丸みは最後に FuwaButtonCorners で全部そろえる（端が丸いボタン。音量の板も）
        }
        GameObject Copy(string src, string name, int row)
        {
            var s = lobbyHow.Find(src);
            var g = Object.Instantiate(s.gameObject, panel, false); g.name = name;
            g.transform.SetPositionAndRotation(pc + rot * new Vector3(BoardSideX, BoardSideY[row], 0f), rot);
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
        var postMesh = FuwaBevelTools.RoundCube(new Vector3(postW, postH, postW), 0.03f, 2);
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
            var railMesh = FuwaBevelTools.RoundCube(new Vector3(railW, railH, len + 0.04f), 0.02f, 2);
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
        foreach (var n in new[] { "Fence", "FenceCollider" }) { var o = root.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }
        var f = new GameObject("Fence"); f.transform.SetParent(root, false); f.transform.localPosition = Vector3.zero;
        f.AddComponent<MeshFilter>().sharedMesh = vis; f.AddComponent<MeshRenderer>().sharedMaterial = wood;
        GameObjectUtility.SetStaticEditorFlags(f, (StaticEditorFlags)~0);
        var fc = new GameObject("FenceCollider"); fc.transform.SetParent(root, false); fc.transform.localPosition = Vector3.zero;
        fc.AddComponent<MeshCollider>().sharedMesh = col;
        GameObjectUtility.SetStaticEditorFlags(fc, (StaticEditorFlags)~0);
        return "fence r=" + r.ToString("F2") + " posts " + segs;
    }

    // 柵は頂点が多いので 32bit の番号で保存（SaveGenMesh は 16bit のまま上書きする）
    static Mesh SaveFenceMesh(Mesh m, string name)
    {
        const string dir = "Assets/FuwaCourse/Generated/Playground";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Playground");
        string p = dir + "/" + name + ".asset";
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (old != null) AssetDatabase.DeleteAsset(p);
        AssetDatabase.CreateAsset(m, p);
        return m;
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
        m.SetColor("_Color", c); EditorUtility.SetDirty(m);
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
        const string ja = "<align=center><size=150%><color=#FF6B5C>ふわふわあそびば</color></size></align>\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>キノコの傘を撃つと、ふわふわが出てくる\n<pos=1.1em>みんなで吹いて、のんびり遊ぼう\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>スイッチで、サイズ・色・出せる数・\n<pos=1.1em>落ちたら消えるかを変えられる\n<pos=1.1em>（設定はみんなで共通）\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>巨大は重いので、みんなで押そう\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>エアガンを手元に呼ぶ：PCはEキー\n<pos=1.1em>VRは右スティックを下に倒し続ける";
        const string en = "<align=center><size=150%><color=#FF6B5C>Fluff Playground</color></size></align>\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Shoot the mushroom cap to pop out a fluff\n<pos=1.1em>Blow them around together and relax\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Switches change the size, color,\n<pos=1.1em>max count, and whether they pop on landing\n<pos=1.1em>(shared by everyone)\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Giant ones are heavy: push together!\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Summon your air gun: E key (PC)\n<pos=1.1em>or hold the right stick down (VR)";
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
