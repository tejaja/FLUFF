using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2026-11-17 てじゃ「全クリしたあとになんかほしい」→「記念撮影場所とか」→「2-5 のミギがあいてる、ワープして島を置く、島はクリア前は何もない、クリア後だけ各種報酬が見える」
//  ステージ2のロビーの空いている6つめの場所（Slot5）にワープ → ロビーの外の「ごほうびの島」。島の床・柵はロビーの写し（小さく）。
//  島の中身（RewardContent）は全コースクリアした人の画面だけに出る（FuwaAllClearReveal）。今の中身：記念撮影のひな壇＋うしろの「ALL CLEAR!」の雲の看板＋乗ると紙吹雪
//  島はシーンの物（ステージ2のプレハブの外）。ステージ2のエリアの表示切りかえ（FuwaAreaVisibility.stage2Objects）に足す
public static class FuwaRewardIslandBuilder
{
    public static float Dist = 26f;
    // 2–4 のスタートの島：上の面がスタートの local y=StartTopY、島のふちが z=StartEdgeZ。ゴールの島の半径 GoalR。間の道の長さ・幅
    public static float StartTopY = 3.0f, StartEdgeZ = 2.5f, GoalR = 3.0f, PathLen = 4.5f, PathW = 3.2f, GoalScale = 1.45f;   // 島のふちは頂点から測った値（前は回った箱の大きさで測って大きすぎ、道とすき間が空いた）
    public static Color WhiteA = new Color(0.97f, 0.97f, 0.98f), WhiteB = new Color(0.86f, 0.93f, 1.0f);
    // 自己ベストのボード（ゴールの TimeLabel を広げて使う）
    public static Vector3 BoardOffset = new Vector3(0f, -1.75f, 0f); public static float BoardScale = 1f, BoardFont = 0.72f, BannerY = 4.2f; public static Vector2 BoardPanel = new Vector2(2.0f, 7.0f);   // ボードが広場の柵にかくれないよう、看板ごと上へ
    const string RootName = "FuwaRewardIsland";

    [MenuItem("FuwaCourse/ステージ2/ごほうびの島を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    static Transform FindRoot(string n) => Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == n && t.parent == null);

    public static string Build()
    {
        var s2 = FindRoot("FuwaStage2"); if (!s2) return "FuwaStage2 がない";
        var lobby = s2.Find("Lobby"); var slot5 = lobby ? lobby.Find("Slot5") : null; var slot4 = lobby ? lobby.Find("Slot4") : null;
        if (!slot5 || !slot4) return "Slot4/5 がない";
        var old = FindRoot(RootName); if (old) Object.DestroyImmediate(old.gameObject);
        foreach (Transform c in slot5.Cast<Transform>().ToList()) Object.DestroyImmediate(c.gameObject);
        var log = new List<string>();

        Vector3 center = lobby.position, dir = slot5.position - center; dir.y = 0f; dir.Normalize();
        // 2026-11-17 てじゃ「やっぱやめ、まずいつものスタート地点を置く、その先にちょっとした広場とゴールリング、ここにフワフワを落とすとゴール演出が出るけど、
        //  GOAL じゃなくて Thank you for playing! ってだしながら各コースの自己ベストタイム一覧がドンとボードに出てくる」「演出後は広場の横の方に金ピカ銃切り替えスイッチと金ピカフワフワオブジェクト（持てるだけ）」
        //  → 2–4 のスタート（島ごと）とゴール（島ごと）を写して、間を道でつなぐ。root の +z がロビーと反対（スタート → ゴールの向き）
        var c24 = FindRoot("FuwaCourse2_4"); var srcStart = c24 ? c24.Find("Start") : null; var srcGoal = c24 ? c24.Find("Goal") : null;
        if (!srcStart || !srcGoal) return "2–4 のスタート・ゴールがない";
        var root = new GameObject(RootName).transform; root.position = center + dir * Dist; root.rotation = Quaternion.LookRotation(dir, Vector3.up);
        float floorY = lobby.Find("LobbySpawn") ? lobby.Find("LobbySpawn").position.y - center.y - 0.1f : 0f;
        var st = Object.Instantiate(srcStart.gameObject, root, false).transform; st.name = "Start";
        st.localPosition = new Vector3(0f, floorY - StartTopY, 0f); st.localRotation = Quaternion.identity; st.localScale = Vector3.one;
        float gR = GoalR * GoalScale;   // 広げたゴールの島の半径
        float goalZ = StartEdgeZ + PathLen + gR;
        var gl = Object.Instantiate(srcGoal.gameObject, root, false).transform; gl.name = "Goal";
        gl.localPosition = new Vector3(0f, floorY, goalZ); gl.localRotation = Quaternion.identity; gl.localScale = Vector3.one;
        // 2026-11-17 てじゃ「ゴール島もうちょい広く」：島の地面・下の岩・柵だけ横に広げる（リング・きのこ・看板はそのまま）
        foreach (var nm in new[] { "GoalGround", "IslandUnder", "Fence", "FenceCollider" })
        {
            var t = gl.Find(nm); if (!t) continue;
            t.localPosition = Vector3.Scale(t.localPosition, new Vector3(GoalScale, 1f, GoalScale));
            t.localScale = nm == "IslandUnder" ? t.localScale * GoalScale : Vector3.Scale(t.localScale, new Vector3(GoalScale, 1f, GoalScale));
        }
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.lightmapIndex = -1;   // 2–4 の焼きを引きずらない（次に焼く時に入る）
        // スタートの看板
        foreach (var t in st.GetComponentsInChildren<TMPro.TMP_Text>(true))
            if (t.text.Contains("2–4") || t.text.Contains("はぐるま")) t.text = "<size=90%>おまけ</size>";
        // 間の道（2–4 の道のシマ）
        {
            // 2026-11-17 てじゃ「道と看板の色は白、普通の白とほんのちょっと水色が強い白」（名前が _PathA/_PathB でないのでベベルにまとめられない）
            var pa = BonusPathMat("Bonus_PathWhite", WhiteA); var pbm = BonusPathMat("Bonus_PathBlue", WhiteB);
            var pathT = new GameObject("Path").transform; pathT.SetParent(root, false);
            // 島の丸いふちとのすき間がないように島の中までのばす（上の面は島より 4mm 下げて、重なりでチラつかないように）
            float z0 = StartEdgeZ - 0.8f, z1 = goalZ - gR + 0.8f; int n = Mathf.Max(2, Mathf.RoundToInt((z1 - z0) / 1.2f)); float L = (z1 - z0) / n;
            for (int k = 0; k < n; k++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = "Stripe" + k; g.transform.SetParent(pathT, false); g.layer = 11;
                g.transform.localPosition = new Vector3(0f, floorY - 0.004f - 0.3f, z0 + L * (k + 0.5f)); g.transform.localScale = new Vector3(PathW, 0.6f, L);
                g.GetComponent<MeshRenderer>().sharedMaterial = k % 2 == 0 ? pa : pbm;
            }
        }
        var spawn = st.Find("PlayerSpot"); var ballPark = st.Find("BallStart");
        Transform gunSpot = st.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GunSpot");
        // ワープの見た目（Slot4 の看板・うず・粒・輪）を写す
        Transform CopyWarpLook(Transform parent, string prefix, string ja)
        {
            foreach (Transform c in slot4)
            {
                if (c.name.EndsWith("_Trigger") || c.name.EndsWith("_Trigger_Gauge") || c.name == "ClearMushrooms") continue;
                var g = Object.Instantiate(c.gameObject, parent, false); g.name = c.name.Replace("WarpToCourse4", prefix);
                g.transform.localPosition = c.localPosition; g.transform.localRotation = c.localRotation; g.transform.localScale = c.localScale;
                FuwaStage2Builder.StripBehaviour(g);
                foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.lightmapIndex = -1;
            }
            var lab = parent.Find("Label")?.GetComponent<TMPro.TMP_Text>(); if (lab) lab.text = ja;
            return parent;
        }
        // 2026-11-17 てじゃ「ワープもグレーになってクリアするまでできないように、看板も『？』で、クリア後は『おまけ』にかわる」
        //  Slot5 の中：Unlocked（看板「おまけ」・うず・粒・輪・ワープ）と Locked（看板「？」・灰色のうず）。全クリで入れかわる（FuwaAllClearReveal）
        var unl = new GameObject("Unlocked").transform; unl.SetParent(slot5, false);
        var lck = new GameObject("Locked").transform; lck.SetParent(slot5, false);
        CopyWarpLook(unl, "WarpToReward", "<size=90%>おまけ</size>");
        var trigSrc = slot4.Find("WarpToCourse4_Trigger"); var gaugeSrc = slot4.Find("WarpToCourse4_Trigger_Gauge");
        FuwaStage2Builder.MakeWarp(unl, "WarpToReward", trigSrc.gameObject, gaugeSrc.gameObject, ballPark, spawn, gunSpot, 2);
        {
            var lab = slot4.Find("Label"); var swirl = slot4.Find("WarpToCourse4");
            if (lab) { var g = Object.Instantiate(lab.gameObject, lck, false); g.name = "Label"; g.transform.localPosition = lab.localPosition; g.transform.localRotation = lab.localRotation; g.transform.localScale = lab.localScale; FuwaStage2Builder.StripBehaviour(g); var t = g.GetComponent<TMPro.TMP_Text>(); if (t) { t.text = "<size=130%>?</size>"; var cb = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/FuwaCourse/Fonts/CherryBombOne SDF.asset"); if (cb) { t.font = cb; t.fontSharedMaterial = cb.material; t.color = new Color(0.45f, 0.52f, 0.72f); } } foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.lightmapIndex = -1; }   // 全角の「？」は看板のフォントにない   // ダルマドロップの「?」はゆがんでいるのでチェリーボムの字に
            if (swirl)
            {
                var g = Object.Instantiate(swirl.gameObject, lck, false); g.name = "SwirlLocked"; g.transform.localPosition = swirl.localPosition; g.transform.localRotation = swirl.localRotation; g.transform.localScale = swirl.localScale;
                FuwaStage2Builder.StripBehaviour(g);
                foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterials = r.sharedMaterials.Select(m => GrayCopy(m)).ToArray(); r.lightmapIndex = -1; }
            }
        }
        // ロビーへもどるワープ（スタート・ゴールにある 2–4 の物）の行き先を、おまけの場所の前に
        var lobSpot = new GameObject("LobbySpawn_FromReward").transform; lobSpot.SetParent(root, true);
        lobSpot.position = new Vector3(slot5.position.x, center.y + floorY + 0.1f, slot5.position.z) - dir * 1.8f; lobSpot.rotation = Quaternion.LookRotation(-dir, Vector3.up);
        foreach (var w in root.GetComponentsInChildren<FuwaCourseWarp>(true))
        {
            var so = new SerializedObject(w);
            so.FindProperty("playerSpot").objectReferenceValue = lobSpot;
            so.FindProperty("courseStart").objectReferenceValue = lobby.Find("BallPark");
            so.FindProperty("gunSpot").objectReferenceValue = lobby.Find("GunStand_Lobby/GunSpot");
            so.FindProperty("stage").intValue = 2; so.FindProperty("area").intValue = 1;
            so.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(w);
        }
        // ゴール：「GOAL!」→「Thank you for playing!」、タイムの代わりに自己ベスト一覧（FuwaThanksBoard）
        var goal = gl.GetComponentInChildren<FuwaGoal>(true);
        var banner = gl.Find("ClearBanner");
        if (goal && banner)
        {
            banner.localPosition = new Vector3(banner.localPosition.x, BannerY, banner.localPosition.z);
            var cl = banner.Find("ClearLabel")?.GetComponent<TMPro.TMP_Text>();
            if (cl) cl.text = "<size=62%><color=#FF8FA3>T</color><color=#FFC36B>h</color><color=#7FD7A8>a</color><color=#7FB8F0>n</color><color=#B79BF0>k</color> <color=#FF8FA3>y</color><color=#FFC36B>o</color><color=#7FD7A8>u</color>\n<color=#7FB8F0>f</color><color=#B79BF0>o</color><color=#FF8FA3>r</color> <color=#FFC36B>p</color><color=#7FD7A8>l</color><color=#7FB8F0>a</color><color=#B79BF0>y</color><color=#FF8FA3>i</color><color=#FFC36B>n</color><color=#7FD7A8>g</color><color=#7FB8F0>!</color></size>";
            var tl = banner.Find("TimeLabel");
            var gso = new SerializedObject(goal); gso.FindProperty("clearTimeText").objectReferenceValue = null; gso.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(goal);
            if (tl)
            {
                tl.gameObject.SetActive(true);
                tl.localPosition += BoardOffset; tl.localScale *= BoardScale;
                var tmp = tl.GetComponent<TMPro.TextMeshPro>(); tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.enableWordWrapping = false; tmp.rectTransform.sizeDelta = new Vector2(4f, 4f); tmp.fontSize *= BoardFont;
                var panel = tl.Find("Panel")?.GetComponent<SpriteRenderer>();
                if (panel && panel.drawMode != SpriteDrawMode.Simple) panel.size = new Vector2(panel.size.x * BoardPanel.x, panel.size.y * BoardPanel.y);
                else if (panel) panel.transform.localScale = Vector3.Scale(panel.transform.localScale, new Vector3(BoardPanel.x, BoardPanel.y, 1f));
                var tb = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaThanksBoard>(tl.gameObject);
                tb.records = Object.FindObjectsOfType<FuwaRecords>(true).Where(x => x.name == "Records" || x.name == "Records2").OrderBy(x => x.name).ToArray(); tb.text = tmp;   // ステージ1 → 2 の順
                UdonSharpEditorUtility.CopyProxyToUdon(tb); FuwaStage2WaterTest.NoSync(tb);
                tmp.text = "<size=120%>じこベスト</size>\n\n1 かぜのおか  0:42.15";
            }
        }
        // BGM（2026-11-17 てじゃ「BGMはコースインしたら無音、ゴール後は専用曲の予定（まだないので一旦ゴール曲で）」）：
        //  このスタートの曲は「なし」（音のない track）。ゴールで鳴るファンファーレのあとは、ほかのコースと同じゴール後の曲（FuwaBgm.afterGoalTrack）
        //  タイムなしのコースでもゴールでファンファーレを鳴らす・アウトでプレイヤーも戻すように、ふわふわの returnPlayerStarts に足す
        FuwaStage2Bgm.Register(root, "BGM_bonus", "BGM_course2_5", "", 1f, 0f, 0f);
        // 2026-11-18 ゴール後は専用曲「futuristic synthwave game ending theme」120BPM、D案 頭15.49／終わり175.49（曲のラスト→イントロ明けの静かな所、80小節）、
        //  gain 1.5（RMS -17.9dB。前の Cozy 90s は -16.0dB で 1.2 だった）
        FuwaStage2Bgm.RegisterAfterGoal("BGM_bonus", "BGM_bonus_after", "BGM_aftergoal", "Assets/FuwaCourse/Audio/BGM/bonus_synth_introloop.wav", 1.5f, 15.49f, 175.49f);
        foreach (var fb in Object.FindObjectsOfType<FuwaBall>(true))
        {
            var bso = new SerializedObject(fb); var arr = bso.FindProperty("returnPlayerStarts");
            for (int i = arr.arraySize - 1; i >= 0; i--) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(i);
            arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = ballPark;
            bso.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(fb);
        }
        // 演出のあと：広場の横にきんピカ台と金ピカふわふわ（FuwaRewardAfterGoal）
        log.Add(GoldGunSetup());
        var rewards = new List<GameObject>();
        rewards.Add(GoldGunStand(gl, 0f, slot4, new Vector3(-gR + 1.6f, 0f, 0.6f), 70f));
        rewards.Add(GoldFluff(gl, new Vector3(gR - 1.6f, 0f, 0.6f)));
        if (banner)
        {
            var ra = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaRewardAfterGoal>(gl.gameObject);
            ra.watch = banner.gameObject; ra.rewards = rewards.ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(ra); FuwaStage2WaterTest.NoSync(ra);
        }
        var rv = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaAllClearReveal>(root.gameObject);
        rv.records = Object.FindObjectsOfType<FuwaRecords>(true).Where(x => x.name == "Records" || x.name == "Records2").ToArray();
        rv.showIfClear = new[] { unl.gameObject };   // 島の中身はいつも出す（島へは全クリしないと行けない）
        rv.hideIfClear = new[] { lck.gameObject };
        unl.gameObject.SetActive(false); lck.gameObject.SetActive(true);   // エディタでは「クリア前」の見た目
        UdonSharpEditorUtility.CopyProxyToUdon(rv); FuwaStage2WaterTest.NoSync(rv);
        // エリアの表示切りかえ（ステージ2）
        var visb = Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(m => m && m.gameObject.scene.IsValid() && m.GetType().Name == "FuwaAreaVisibility");
        if (visb)
        {
            var so = new SerializedObject(visb); var arr = so.FindProperty("stage2Objects");
            for (int i = arr.arraySize - 1; i >= 0; i--) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null) arr.DeleteArrayElementAtIndex(i);
            arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = root.gameObject;
            so.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)visb);
            root.gameObject.SetActive(s2.gameObject.activeSelf);
            log.Add("エリア：ステージ2に追加");
        }
        // 看板の色（白）：雲の看板のふち・板の看板の縁を水色よりの白に
        WhiteSigns(root); WhiteSigns(slot5);
        // 2026-11-17 てじゃ「看板の角度ちゃんと柵にあわせて」：Slot5 は Slot4 と向きがちがうので、看板をほかの場所と同じ「ロビーのまん中から外向き・同じ距離」に置き直す
        {
            var lab4 = slot4.Find("Label");
            if (lab4)
            {
                var c4 = lab4.position - center; c4.y = 0f; float rl = c4.magnitude;
                float a4 = Mathf.Atan2(c4.x, c4.z) * Mathf.Rad2Deg, rel4 = Mathf.DeltaAngle(a4, lab4.eulerAngles.y);
                var d5 = slot5.position - center; d5.y = 0f; float a5 = Mathf.Atan2(d5.x, d5.z) * Mathf.Rad2Deg;
                foreach (var lb in slot5.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Label"))
                {
                    var dn = Quaternion.Euler(0f, a5, 0f) * Vector3.forward;
                    lb.position = new Vector3(center.x + dn.x * rl, lab4.position.y, center.z + dn.z * rl);
                    lb.rotation = Quaternion.Euler(lab4.eulerAngles.x, a5 + rel4, lab4.eulerAngles.z);
                }
            }
        }
        FuwaCourse2_1Builder.FixNetworkIDs();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "[ごほうびの島] " + root.position.ToString("F1") + " " + string.Join("、", log);
    }

    // 記念撮影のひな壇（2段）＋うしろの大きな雲の看板「ALL CLEAR!」＋乗ると紙吹雪（FuwaConfettiStage）＋撮る人の立ち位置の目印
    static void PhotoStage(Transform content, float floorY, Transform slot4, List<string> log)
    {
        var light = FuwaGearTest.Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f));
        var brass = FuwaGearTest.Mat("FanTest_Brass", new Color(0.86f, 0.68f, 0.32f));
        var stage = new GameObject("PhotoStage").transform; stage.SetParent(content, false); stage.localPosition = new Vector3(0f, floorY, 2.2f);
        Transform Disc(string n, float r, float y0, float h, Material m, bool col)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = n; Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(stage, false); g.transform.localPosition = new Vector3(0f, y0 + h / 2f, 0f); g.transform.localScale = new Vector3(2f * r, h / 2f, 2f * r);
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (col) { var mc = g.AddComponent<MeshCollider>(); mc.sharedMesh = g.GetComponent<MeshFilter>().sharedMesh; mc.convex = true; }
            return g.transform;
        }
        Disc("Step1", 2.1f, 0f, 0.22f, light, true); Disc("Step1Rim", 2.14f, 0.2f, 0.03f, brass, false);
        Disc("Step2", 1.45f, 0.22f, 0.22f, light, true); Disc("Step2Rim", 1.49f, 0.42f, 0.03f, brass, false);
        // うしろの看板（Slot4 の雲の看板を大きく）
        var lab = slot4.Find("Label");
        if (lab)
        {
            var g = Object.Instantiate(lab.gameObject, stage, false); g.name = "AllClearSign";
            g.transform.localPosition = new Vector3(0f, 2.9f, 1.9f); g.transform.localRotation = lab.localRotation; g.transform.localScale = lab.localScale * 2.2f;
            FuwaStage2Builder.StripBehaviour(g);
            var t = g.GetComponent<TMPro.TMP_Text>(); if (t) t.text = "<size=95%>ALL CLEAR!</size>";
            foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.lightmapIndex = -1;
            // 看板の柱2本
            foreach (int sg in new[] { 1, -1 })
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder); p.name = "SignPost"; Object.DestroyImmediate(p.GetComponent<Collider>());
                p.transform.SetParent(stage, false); p.transform.localPosition = new Vector3(sg * 1.3f, 1.2f, 1.95f); p.transform.localScale = new Vector3(0.12f, 1.2f, 0.12f);
                p.GetComponent<MeshRenderer>().sharedMaterial = brass;
            }
        }
        else log.Add("看板の見本なし");
        // 紙吹雪（左右から上へ）
        var cmat = ConfettiMat();
        var ps = new List<ParticleSystem>();
        foreach (int sg in new[] { 1, -1 })
        {
            var go = new GameObject("Confetti" + (sg > 0 ? "R" : "L")); go.transform.SetParent(stage, false);
            go.transform.localPosition = new Vector3(sg * 2.3f, 0.3f, 0.6f); go.transform.localRotation = Quaternion.Euler(-70f, sg * -15f, 0f);
            var p = go.AddComponent<ParticleSystem>(); p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = p.main; main.playOnAwake = false; main.loop = false; main.duration = 1f; main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 8f); main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.13f); main.gravityModifier = 0.35f;
            main.startRotation3D = false; main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f); main.maxParticles = 300;
            var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.55f, 0.65f), 0f), new GradientColorKey(new Color(1f, 0.85f, 0.35f), 0.33f), new GradientColorKey(new Color(0.45f, 0.8f, 1f), 0.66f), new GradientColorKey(new Color(0.6f, 0.95f, 0.6f), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(grad) { mode = ParticleSystemGradientMode.RandomColor };
            var em = p.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 120) });
            var sh = p.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 22f; sh.radius = 0.15f;
            var rot = p.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);
            var drag = p.limitVelocityOverLifetime; drag.enabled = true; drag.dampen = 0.08f; drag.limit = 2.5f;
            var col = p.colorOverLifetime; col.enabled = true; var fade = new Gradient(); fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) }); col.color = fade;
            var pr = go.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = cmat; pr.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Add(p);
        }
        // 乗ると紙吹雪（ひな壇の上の当たり）
        var tg = new GameObject("StageTrigger"); tg.transform.SetParent(stage, false); tg.transform.localPosition = new Vector3(0f, 1.2f, 0f); tg.layer = 2;
        var bc = tg.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(2.6f, 1.8f, 2.6f);
        var cs = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaConfettiStage>(tg); cs.confetti = ps.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(cs);
        // 撮る人の立ち位置（ひな壇の前の真ちゅうの輪）
        var mark = new GameObject("CameraSpot").transform; mark.SetParent(content, false); mark.localPosition = new Vector3(0f, floorY + 0.01f, -1.6f);
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); ring.name = "Ring"; Object.DestroyImmediate(ring.GetComponent<Collider>());
        ring.transform.SetParent(mark, false); ring.transform.localScale = new Vector3(0.7f, 0.006f, 0.7f); ring.GetComponent<MeshRenderer>().sharedMaterial = brass;
    }

    // ===== 金ピカの銃（2026-11-17 てじゃ「銃金ピカ化スイッチ」）=====
    //  銃（FuwaBlower）の見た目のうち、本体・持ち手・口・通気口の材質を金色の写しに差しかえる用意（FuwaBlower.goldRenderers / goldMats）と、金ピカの間のキラキラ
    static readonly (string src, Color col)[] GoldOf = {
        ("Fuwa_Blower", new Color(0.98f, 0.78f, 0.30f)), ("Fuwa_BlowerGrip", new Color(0.78f, 0.55f, 0.20f)),
        ("Fuwa_BlowerMuzzle", new Color(1.00f, 0.90f, 0.55f)), ("Fuwa_BlowerVent", new Color(0.86f, 0.64f, 0.24f)),
        // 2026-11-18 てじゃ「エフェクトとかゲージ以外全パーツ金ピカにして」：ポンプの丸いところ・銃口の穴の中も
        ("Fuwa_BlowerBulb", new Color(1.00f, 0.84f, 0.40f)), ("GunMuzzleShaft", new Color(0.50f, 0.36f, 0.12f)) };
    public static string GoldGunSetup()
    {
        int n = 0;
        foreach (var b in Object.FindObjectsOfType<FuwaBlower>(true))
        {
            if (!b.visualRoot) continue;
            var rs = new List<Renderer>(); var ms = new List<Material>();
            foreach (var r in b.visualRoot.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial; if (!m) continue;
                string bn = m.name.Replace("_Gold", "");
                var g = GoldOf.FirstOrDefault(x => x.src == bn); if (g.src == null) continue;
                var baseM = AssetDatabase.FindAssets(bn + " t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault(x => x && x.name == bn) ?? m;
                rs.Add(r); ms.Add(GoldMat(baseM, g.col));
            }
            // キラキラ（見た目の子、金ピカの間だけ）
            var old = b.visualRoot.transform.Find("GoldSparkle"); if (old) Object.DestroyImmediate(old.gameObject);
            var fx = new GameObject("GoldSparkle"); fx.transform.SetParent(b.visualRoot.transform, false); fx.transform.localPosition = new Vector3(0f, 0.02f, 0.05f);
            var p = fx.AddComponent<ParticleSystem>(); p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = p.main; main.loop = true; main.playOnAwake = true; main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f); main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f); main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.93f, 0.6f), new Color(1f, 1f, 0.9f)); main.maxParticles = 30; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = p.emission; em.rateOverTime = 7f;
            var sh = p.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(0.12f, 0.12f, 0.35f);
            var sz = p.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
            var pr = fx.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = SparkleMat();
            fx.SetActive(false);
            b.goldRenderers = rs.ToArray(); b.goldMats = ms.ToArray(); b.goldFx = fx;
            UdonSharpEditorUtility.CopyProxyToUdon(b); EditorUtility.SetDirty(b); n++;
        }
        return "金ピカの銃 " + n;
    }
    static Material GoldMat(Material src, Color col)
    {
        string path = "Assets/FuwaStage2/Materials/" + src.name + "_Gold.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(src); AssetDatabase.CreateAsset(m, path); } else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
        m.name = src.name + "_Gold";
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        if (m.HasProperty("_Top")) { m.SetColor("_Top", col); m.SetColor("_Bottom", new Color(col.r * 0.12f, col.g * 0.12f, col.b * 0.12f, 1f)); }   // 銃口の穴の中（VentShaft：上の色→奥の暗い色）
        if (m.HasProperty("_MetalEnv")) { m.SetFloat("_MetalEnv", 0.65f); m.SetFloat("_MetalSpec", 0.35f); m.SetFloat("_MetalSize", 0.93f); }
        EditorUtility.SetDirty(m); return m;
    }
    static Material SparkleMat()
    {
        string path = "Assets/FuwaStage2/Materials/Reward_Sparkle.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { var sh = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Unlit/Color"); m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        var tex = AssetDatabase.FindAssets("Default-Particle t:Texture").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault();
        if (tex && m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 4f);   // 加算
        EditorUtility.SetDirty(m); return m;
    }
    // 島の金ピカの台：クリックで自分の銃を金ピカに（FuwaGoldGunSwitch）。台の上に金のメダル、前に「金ピカ ON/OFF」
    static GameObject GoldGunStand(Transform content, float floorY, Transform slot4, Vector3 lp, float yaw)
    {
        var brass = FuwaGearTest.Mat("FanTest_Brass", new Color(0.86f, 0.68f, 0.32f));
        var light = FuwaGearTest.Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f));
        var st = new GameObject("GoldGunStand").transform; st.SetParent(content, false); st.localPosition = lp + Vector3.up * floorY; st.localRotation = Quaternion.Euler(0f, yaw, 0f);
        Transform P(PrimitiveType t, string n, Vector3 lp, Vector3 ls, Material m, Quaternion? lr = null)
        { var g = GameObject.CreatePrimitive(t); g.name = n; Object.DestroyImmediate(g.GetComponent<Collider>()); g.transform.SetParent(st, false); g.transform.localPosition = lp; g.transform.localScale = ls; if (lr.HasValue) g.transform.localRotation = lr.Value; g.GetComponent<MeshRenderer>().sharedMaterial = m; return g.transform; }
        P(PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.9f, 0.05f, 0.9f), light);
        P(PrimitiveType.Cylinder, "Pillar", new Vector3(0f, 0.5f, 0f), new Vector3(0.45f, 0.4f, 0.45f), light);
        P(PrimitiveType.Cylinder, "Top", new Vector3(0f, 0.92f, 0f), new Vector3(0.7f, 0.03f, 0.7f), brass);
        var gold = GoldMat(brass, new Color(1f, 0.82f, 0.32f));
        P(PrimitiveType.Cylinder, "Medal", new Vector3(0f, 1.25f, 0f), new Vector3(0.42f, 0.03f, 0.42f), gold, Quaternion.Euler(90f, 0f, 0f));
        P(PrimitiveType.Cylinder, "MedalRim", new Vector3(0f, 1.25f, 0f), new Vector3(0.46f, 0.02f, 0.46f), brass, Quaternion.Euler(90f, 0f, 0f));
        // 文字（看板と同じフォント）
        var lt = new GameObject("Label"); lt.transform.SetParent(st, false); lt.transform.localPosition = new Vector3(0f, 0.6f, -0.235f); lt.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        var tmp = lt.AddComponent<TMPro.TextMeshPro>(); tmp.text = "きんピカ OFF"; tmp.fontSize = 3.2f;   // 「金」は看板のフォントにない tmp.alignment = TMPro.TextAlignmentOptions.Center; tmp.color = new Color(0.45f, 0.42f, 0.55f);
        var srcT = slot4.Find("Label")?.GetComponent<TMPro.TMP_Text>(); if (srcT) { tmp.font = srcT.font; tmp.fontSharedMaterial = srcT.fontSharedMaterial; }
        tmp.rectTransform.sizeDelta = new Vector2(3f, 0.8f); lt.transform.localScale = Vector3.one * 0.3f;
        // クリックの当たり＋スクリプト
        var bc = st.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.7f, 0f); bc.size = new Vector3(0.9f, 1.4f, 0.9f);
        var sw = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaGoldGunSwitch>(st.gameObject);
        sw.manager = Object.FindObjectsOfType<FuwaRespawnManager>(true).FirstOrDefault(); sw.label = tmp; sw.onText = "きんピカ ON"; sw.offText = "きんピカ OFF";
        UdonSharpEditorUtility.CopyProxyToUdon(sw);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(sw); if (ub) { ub.interactText = "きんピカ"; EditorUtility.SetDirty(ub); }
        return st.gameObject;
    }

    // 金ピカふわふわ（持てるだけ、自分の画面だけ）：ふわふわと同じ毛の材質を金色に
    static GameObject GoldFluff(Transform parent, Vector3 lp)
    {
        var ball = Object.FindObjectsOfType<FuwaBall>(true).FirstOrDefault(); if (!ball || !ball.furRenderer) return null;
        var src = ball.furRenderer.sharedMaterial;
        string path = "Assets/FuwaStage2/Materials/Reward_GoldFluffFur.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (!m) { m = new Material(src); AssetDatabase.CreateAsset(m, path); } else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
        m.SetColor("_Color", new Color(1f, 0.74f, 0.25f)); m.SetColor("_TipColor", new Color(1f, 0.95f, 0.65f)); if (m.HasProperty("_GlowColor")) m.SetColor("_GlowColor", new Color(1f, 0.85f, 0.4f, 0.6f));
        EditorUtility.SetDirty(m);
        // 台
        var holder = new GameObject("GoldFluffStand"); holder.transform.SetParent(parent, false); holder.transform.localPosition = lp;
        var light = FuwaGearTest.Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f)); var brass = FuwaGearTest.Mat("FanTest_Brass", new Color(0.86f, 0.68f, 0.32f));
        foreach (var (n, y, d, h, mt) in new[] { ("Base", 0.05f, 0.9f, 0.05f, light), ("Pillar", 0.45f, 0.4f, 0.35f, light), ("Top", 0.82f, 0.62f, 0.03f, brass) })
        { var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = n; Object.DestroyImmediate(g.GetComponent<Collider>()); g.transform.SetParent(holder.transform, false); g.transform.localPosition = new Vector3(0f, y, 0f); g.transform.localScale = new Vector3(d, h, d); g.GetComponent<MeshRenderer>().sharedMaterial = mt; }
        var col = holder.AddComponent<BoxCollider>(); col.center = new Vector3(0f, 0.43f, 0f); col.size = new Vector3(0.62f, 0.86f, 0.62f);
        // ふわふわ（持てる。自分の画面だけなので同期なし、手放したらその場に浮く）
        var f = new GameObject("GoldFluff"); f.transform.SetParent(holder.transform, false); f.transform.localPosition = new Vector3(0f, 1.15f, 0f); f.transform.localScale = ball.transform.lossyScale * 1.3f;
        f.AddComponent<MeshFilter>().sharedMesh = ball.furRenderer.GetComponent<MeshFilter>().sharedMesh; f.AddComponent<MeshRenderer>().sharedMaterial = m;
        var sc = f.AddComponent<SphereCollider>(); sc.radius = 0.5f;
        var rb = f.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var pk = f.AddComponent<VRC.SDK3.Components.VRCPickup>(); pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any; pk.pickupable = true; pk.proximity = 1.5f; pk.InteractionText = "きんピカふわふわ";
        return holder;
    }

    // ===== 手に持てるトロフィー（2026-11-17 てじゃ「手に持てるトロフィーみたいなやつ」）=====
    //  Blender の Reward_Trophy（金のカップ・取っ手・2段の台・てっぺんにふわふわ）。島の右の長い台に3つ。持てる（VRCPickup＋VRCObjectSync で全員に同期）、
    //  手放して 20 秒で台にもどる（FuwaTrophy）。浮いたまま置ける（重力なし）
    public static int TrophyCount = 3;
    static void TrophyShelf(Transform content, float floorY)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaStage2/Meshes/Reward_Trophy.asset"); if (!mesh) return;
        var brass = FuwaGearTest.Mat("FanTest_Brass", new Color(0.86f, 0.68f, 0.32f));
        var light = FuwaGearTest.Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f));
        var baseM = FuwaGearTest.Mat("GearTest_Gear", new Color(0.36f, 0.38f, 0.43f));
        var goldM = GoldMat(brass, new Color(1f, 0.8f, 0.3f));
        var fluffM = FuwaGearTest.Mat("Reward_TrophyFluff", new Color(1f, 0.93f, 0.96f));
        var sh = new GameObject("TrophyShelf").transform; sh.SetParent(content, false); sh.localPosition = new Vector3(3.4f, floorY, -1.0f); sh.localRotation = Quaternion.Euler(0f, -50f, 0f);
        Transform P(PrimitiveType t, string n, Vector3 lp, Vector3 ls, Material m)
        { var g = GameObject.CreatePrimitive(t); g.name = n; Object.DestroyImmediate(g.GetComponent<Collider>()); g.transform.SetParent(sh, false); g.transform.localPosition = lp; g.transform.localScale = ls; g.GetComponent<MeshRenderer>().sharedMaterial = m; return g.transform; }
        float w = 0.55f * TrophyCount + 0.3f;
        P(PrimitiveType.Cube, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(w + 0.1f, 0.1f, 0.7f), light);
        P(PrimitiveType.Cube, "Body", new Vector3(0f, 0.45f, 0f), new Vector3(w, 0.7f, 0.6f), light);
        P(PrimitiveType.Cube, "Top", new Vector3(0f, 0.82f, 0f), new Vector3(w + 0.08f, 0.04f, 0.68f), brass);
        var col = sh.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0f, 0.42f, 0f); col.size = new Vector3(w + 0.1f, 0.84f, 0.7f);
        for (int i = 0; i < TrophyCount; i++)
        {
            float x = -0.55f * (TrophyCount - 1) / 2f + 0.55f * i;
            var t = new GameObject("Trophy" + i); t.transform.SetParent(sh, false); t.transform.localPosition = new Vector3(x, 0.84f, 0f);
            t.AddComponent<MeshFilter>().sharedMesh = mesh; t.AddComponent<MeshRenderer>().sharedMaterials = new[] { baseM, goldM, fluffM };
            var bc = t.AddComponent<BoxCollider>(); bc.center = mesh.bounds.center; bc.size = mesh.bounds.size;
            var rb = t.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var pk = t.AddComponent<VRC.SDK3.Components.VRCPickup>(); pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any; pk.pickupable = true; pk.InteractionText = "\u30c8\u30ed\u30d5\u30a3\u30fc"; pk.proximity = 1.5f;
            t.AddComponent<VRC.SDK3.Components.VRCObjectSync>();
            var tr = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaTrophy>(t);
            UdonSharpEditorUtility.CopyProxyToUdon(tr);
            // 台の上の小さい札
            P(PrimitiveType.Cylinder, "Spot" + i, new Vector3(x, 0.845f, 0f), new Vector3(0.26f, 0.004f, 0.26f), goldM);
        }
    }

    static Material BonusPathMat(string name, Color c)
    {
        string path = "Assets/FuwaStage2/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_4_PathA.mat");
        if (!m) { m = src ? new Material(src) : new Material(Shader.Find("FuwaCourse/ToonLit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", c); if (m.HasProperty("_UnderColor")) m.SetColor("_UnderColor", Color.Lerp(c, Color.white, 0.3f));
        EditorUtility.SetDirty(m); return m;
    }
    static void WhiteSigns(Transform t)
    {
        Material rim = null;
        foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true).Where(x => x.name == "CloudRim").ToArray())
        {
            if (!rim)
            {
                string path = "Assets/FuwaStage2/Materials/Bonus_SignRim.mat";
                rim = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!rim) { rim = new Material(r.sharedMaterial); AssetDatabase.CreateAsset(rim, path); }
                foreach (var pn in new[] { "_Color", "_BaseColor" }) if (rim.HasProperty(pn)) rim.SetColor(pn, WhiteB);
                EditorUtility.SetDirty(rim);
            }
            r.sharedMaterial = rim;
        }
        foreach (var sr in t.GetComponentsInChildren<SpriteRenderer>(true).Where(x => x.name == "Border")) sr.color = WhiteB;
    }

    // うずの材質の灰色版（色を灰色に）
    static Material GrayCopy(Material src)
    {
        if (!src) return src;
        string path = "Assets/FuwaStage2/Materials/Reward_Locked_" + src.name.Replace(" ", "_") + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(src); AssetDatabase.CreateAsset(m, path); } else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
        if (m.HasProperty("_Speed")) m.SetFloat("_Speed", m.GetFloat("_Speed") * 0.3f);   // 止まりかけ
        foreach (var pn in new[] { "_Color", "_TintColor", "_BaseColor", "_EmissionColor", "_GlowColor", "_ColorA", "_ColorB", "_ColorC" })
            if (m.HasProperty(pn)) { var c = m.GetColor(pn); float g = (c.r + c.g + c.b) / 3f * 0.6f; m.SetColor(pn, new Color(g, g, g * 1.04f, c.a)); }
        EditorUtility.SetDirty(m); return m;
    }

    static Material ConfettiMat()
    {
        string path = "Assets/FuwaStage2/Materials/Reward_Confetti.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            var sh = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Unlit/Color");
            m = new Material(sh); AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(m); return m;
    }
}
