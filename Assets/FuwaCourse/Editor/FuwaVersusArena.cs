using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 「みんなで」の色ゴールの取り合い（2026-10-10 仮組み）：会場の島と、チーム分けの部屋（今のみんなでロビー）の台を作る。
// 会場はみんなでロビーの前（スポーンから見て奥、-Z へ65m）に浮かべる。みんなでロビーの子なので、表示の切り替えも一緒。
// 島・キノコ・ふわふわはみんなでロビーのものをコピー。まわりは膜（人もふわふわも止める。ふわふわは FuwaSharedBall がはじき返す）。
public static class FuwaVersusArena
{
    const string LobbyName = "FuwaVersusLobby";
    static readonly Vector3 ArenaOffset = new Vector3(0f, 0f, -65f);   // みんなでロビーの真ん中から
    const float BarrierR = 13.3f;     // 膜の半径（島の床は半径14）
    const float BarrierH = 7f;
    const int BarrierSegs = 36;
    static readonly Color[] TeamColors =
    {
        new Color(1f, 0.35f, 0.35f), new Color(0.25f, 0.53f, 1f), new Color(0.95f, 0.72f, 0f), new Color(0.18f, 0.72f, 0.35f)
    };
    static readonly string[] TeamNames = { "Red", "Blue", "Yellow", "Green" };

    [MenuItem("FuwaCourse/みんなで/色ゴールの会場とチーム分けの台を作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new List<string>();
        var lobby = GameObject.Find(LobbyName).transform;
        Vector3 lc = lobby.position;
        Vector3 ac = lc + ArenaOffset;

        // ---------- 会場 ----------
        var old = lobby.Find("Arena"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var arena = new GameObject("Arena").transform; arena.SetParent(lobby, false); arena.position = ac;

        // 島（みんなでロビーの床・ふちの輪・島の下）をコピー
        foreach (var n in new[] { "VersusFloor", "VersusRing", "IslandUnder", "FallReturn" })
        {
            var src = lobby.Find(n);
            var g = Object.Instantiate(src.gameObject, arena, true); g.name = n;
            g.transform.position = src.position + ArenaOffset;
        }
        // 真ん中のキノコと、会場のふわふわ
        var mush = Object.Instantiate(lobby.Find("CenterMushroom").gameObject, arena, true); mush.name = "CenterMushroom";
        mush.transform.position += ArenaOffset;
        var fluffSrc = lobby.Find("SharedFluff");
        var fluffGo = Object.Instantiate(fluffSrc.gameObject, arena, true); fluffGo.name = "ArenaFluff";
        fluffGo.transform.position = fluffSrc.position + ArenaOffset;
        fluffGo.transform.localScale = Vector3.one * 0.45f;
        var fluff = fluffGo.GetComponent<FuwaSharedBall>();
        var so = new SerializedObject(fluff);
        so.FindProperty("popLayers").intValue = 0;            // 地面ではじけない（ポヨンと跳ねるだけ）
        so.FindProperty("shotScale").floatValue = 0.6f;
        so.FindProperty("arenaCenter").objectReferenceValue = arena;
        so.FindProperty("arenaRadius").floatValue = BarrierR - 0.1f;
        so.ApplyModifiedPropertiesWithoutUndo(); Sync(fluff);
        foreach (var sm in mush.GetComponentsInChildren<FuwaStartMushroom>(true)) { SetU(sm, "sharedBall", fluff); Sync(sm); }
        // 撃てるように、エアガン（PlayerObject のひな形）の「みんなで吹くふわふわ」に足す
        foreach (var bl in Object.FindObjectsOfType<FuwaBlower>(true))
        {
            var bso = new SerializedObject(bl); var p = bso.FindProperty("sharedBalls");
            var list = new List<Object>();
            for (int i = 0; i < p.arraySize; i++) { var o = p.GetArrayElementAtIndex(i).objectReferenceValue; if (o != null) list.Add(o); }
            if (!list.Contains(fluff)) list.Add(fluff);
            p.arraySize = list.Count; for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            bso.ApplyModifiedPropertiesWithoutUndo(); Sync(bl);
        }

        // 膜：見た目は板を輪に並べる（チュートリアルの膜と同じシェーダー、ふだんから線が見える）。当たりは Default レイヤーの箱（人もふわふわも止める）
        var barrier = new GameObject("Barrier").transform; barrier.SetParent(arena, false);
        var bmat = BarrierMat();
        float seg = 2f * BarrierR * Mathf.Sin(Mathf.PI / BarrierSegs);
        float floorY = lobby.Find("VersusFloor").GetComponent<Renderer>().bounds.max.y;
        for (int i = 0; i < BarrierSegs; i++)
        {
            float a = (i + 0.5f) * Mathf.PI * 2f / BarrierSegs;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 p = ac + dir * BarrierR * Mathf.Cos(Mathf.PI / BarrierSegs);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "Wall" + i;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(barrier, false);
            q.transform.position = new Vector3(p.x, floorY + BarrierH * 0.5f, p.z);
            q.transform.rotation = Quaternion.LookRotation(dir);
            q.transform.localScale = new Vector3(seg + 0.01f, BarrierH, 1f);
            var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = bmat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var c = new GameObject("Col" + i); c.transform.SetParent(barrier, false);
            c.transform.SetPositionAndRotation(new Vector3(p.x, floorY + 4f, p.z) + dir * 0.15f, Quaternion.LookRotation(dir));
            var bc = c.AddComponent<BoxCollider>(); bc.size = new Vector3(seg + 0.15f, 8f, 0.3f);
        }

        // ゴール（輪っか）とスタート地点。位置は FuwaMatch が色の数に合わせて並べ直す（ここでは4色の時の並び）
        var goals = new Transform[4]; var spawns = new Transform[4]; var fx = new ParticleSystem[4];
        var ring = RingMesh();
        for (int i = 0; i < 4; i++)
        {
            var g = new GameObject("Goal_" + TeamNames[i]).transform; g.SetParent(arena, false);
            var vis = new GameObject("Ring"); vis.transform.SetParent(g, false);
            vis.AddComponent<MeshFilter>().sharedMesh = ring;
            vis.AddComponent<MeshRenderer>().sharedMaterial = TeamMat(i);
            // 輪っかを支える柱（床まで）
            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pole.name = "Pole";
            Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.transform.SetParent(g, false);
            pole.transform.localPosition = new Vector3(0f, -1.1f - 0.85f, 0f);
            pole.transform.localScale = new Vector3(0.12f, 0.85f, 0.12f);
            pole.GetComponent<MeshRenderer>().sharedMaterial = TeamMat(i);
            fx[i] = MakeGoalFx(g, TeamColors[i]);
            var sp = new GameObject("Spawn_" + TeamNames[i]).transform; sp.SetParent(arena, false);
            goals[i] = g; spawns[i] = sp;
        }

        // 得点板：真ん中の上に1枚、いつも自分の方を向く（FuwaFacePlayer）
        var boardSrc = lobby.Find("HowToPlayVersus/Text").GetComponent<TMPro.TextMeshPro>();
        var boards = new List<TMPro.TextMeshPro>();
        var bRoot = new GameObject("ScoreBoards").transform; bRoot.SetParent(arena, false); bRoot.position = ac + Vector3.up * 5.2f;
        UdonSharpUndo.AddComponent<FuwaFacePlayer>(bRoot.gameObject);
        {
            var t = MakeText(bRoot, "Board", boardSrc, 7f, new Vector2(9f, 2.6f));
            t.fontStyle = TMPro.FontStyles.Bold;
            t.text = "いろゴール";
            boards.Add(t);
        }

        // ---------- チーム分けの部屋（みんなでロビーの -Z 側のふち、会場の方） ----------
        var oldP = lobby.Find("MatchPanel"); if (oldP != null) Object.DestroyImmediate(oldP.gameObject);
        var panel = new GameObject("MatchPanel").transform; panel.SetParent(lobby, false);
        panel.position = new Vector3(lc.x, floorY, lc.z - 8.5f);
        panel.rotation = Quaternion.LookRotation(Vector3.back);   // 表（文字の読める側）は +Z（スポーン側）を向く。TMP は -Z から読めるので
        // 看板（みんなでの遊び方看板をコピーして、中のボタン類を外す）
        var howSrc = lobby.Find("HowToPlayVersus");
        var board = Object.Instantiate(howSrc.gameObject, panel, false); board.name = "Board";
        foreach (Transform ch in board.transform.Cast<Transform>().ToList())
            if (ch.name != "Leg" && ch.name != "Panel" && ch.name != "Text") Object.DestroyImmediate(ch.gameObject);
        board.transform.localPosition = Vector3.zero; board.transform.localRotation = Quaternion.identity;
        var lobbyText = board.transform.Find("Text").GetComponent<TMPro.TextMeshPro>();
        foreach (var lc2 in board.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true)) Object.DestroyImmediate(lc2);
        RemoveFromLanguage(lobbyText);
        lobbyText.fontSizeMax = 2.4f; lobbyText.enableAutoSizing = true; lobbyText.fontSizeMin = 0.5f;

        // 試合の本体は FuwaSystem の下（みんなでロビーはエリアごとに非表示になるので、同期する物は置かない）
        var sys = GameObject.Find("FuwaSystem").transform;
        var oldM = sys.Find("FuwaMatch"); if (oldM != null) Object.DestroyImmediate(oldM.gameObject);
        var mGo = new GameObject("FuwaMatch"); mGo.transform.SetParent(sys, false);
        var match = UdonSharpUndo.AddComponent<FuwaMatch>(mGo);
        // 色を選ぶ台（看板の手前の床に4つ）
        var pads = new GameObject[4]; var padLabels = new TMPro.TextMeshPro[4];
        for (int i = 0; i < 4; i++)
        {
            float x = (i - 1.5f) * 1.5f;
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder); pad.name = "Pad_" + TeamNames[i];
            pad.transform.SetParent(panel, false);
            pad.transform.localPosition = new Vector3(x, 0.12f, -2.6f);
            pad.transform.localScale = new Vector3(1.1f, 0.12f, 1.1f);
            pad.GetComponent<MeshRenderer>().sharedMaterial = TeamMat(i);
            Button(pad, match, "UiJoin" + i, TeamNames[i] + " team");
            var lab = MakeText(panel, "PadLabel" + i, boardSrc, 2.2f, new Vector2(1.4f, 0.8f));
            lab.fontStyle = TMPro.FontStyles.Bold; lab.color = Color.Lerp(TeamColors[i], Color.black, 0.2f);
            lab.transform.localPosition = new Vector3(x, 0.75f, -2.6f);
            lab.transform.localRotation = Quaternion.identity;
            pads[i] = pad; padLabels[i] = lab;
            lab.transform.SetParent(pad.transform, true);   // 台と一緒に出し入れ
        }
        // ボタン（看板の右横に縦に並べる）
        string[,] btns =
        {
            { "UiJoinAny", "おまかせ" }, { "UiLeave", "ぬける" },
            { "UiColorsMinus", "色の数 −" }, { "UiColorsPlus", "色の数 ＋" },
            { "UiValueMinus", "時間/点数 −" }, { "UiValuePlus", "時間/点数 ＋" },
            { "UiRuleToggle", "時間 ⇔ 点数" }, { "UiRandomToggle", "チーム分け" },
            { "UiBallToggle", "ふわふわ" }, { "UiAllEditToggle", "みんなOK" },
            { "UiShuffle", "色シャッフル" }, { "UiPassAdmin", "設定をゆずる" },
            { "UiStop", "中止" }, { "", "" },
            { "UiStart", "スタート！" },
        };
        for (int b = 0; b < btns.GetLength(0); b++)
        {
            int col = b % 2, row = b / 2;
            if (btns[b, 0] == "") continue;   // 並びをそろえるための空き
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Btn_" + btns[b, 0];
            go.transform.SetParent(panel, false);
            bool start = btns[b, 0] == "UiStart";   // スタートだけ横長で一番下
            go.transform.localPosition = start ? new Vector3(3.375f, 4.2f - row * 0.48f - 0.12f, 0f) : new Vector3(2.75f + col * 1.25f, 4.2f - row * 0.48f, 0f);
            go.transform.localScale = start ? new Vector3(2.4f, 0.6f, 0.12f) : new Vector3(1.15f, 0.4f, 0.12f);
            go.GetComponent<MeshRenderer>().sharedMaterial = start ? TeamMat(3) : ButtonMat();
            Button(go, match, btns[b, 0], btns[b, 1]);
            var lab = MakeText(panel, "Label_" + btns[b, 0], boardSrc, start ? 2.6f : 1.6f, start ? new Vector2(2.3f, 0.55f) : new Vector2(1.08f, 0.36f));
            lab.fontStyle = TMPro.FontStyles.Bold;
            lab.transform.localPosition = go.transform.localPosition + new Vector3(0f, 0f, -0.07f);
            lab.transform.localRotation = Quaternion.identity;
            lab.color = start ? Color.white : new Color(0.3f, 0.18f, 0.12f);
            lab.text = btns[b, 1];
            lab.transform.SetParent(go.transform, true);
        }
        // ボタンの支柱
        var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder); post.name = "ButtonPost";
        Object.DestroyImmediate(post.GetComponent<Collider>());
        post.transform.SetParent(panel, false); post.transform.localPosition = new Vector3(3.37f, 1.6f, 0.12f); post.transform.localScale = new Vector3(0.1f, 1.6f, 0.1f);
        post.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course_Wood.mat");

        // つなぐ
        var mso = new SerializedObject(match);
        void Arr(string prop, Object[] v) { var p = mso.FindProperty(prop); p.arraySize = v.Length; for (int i = 0; i < v.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = v[i]; }
        mso.FindProperty("ball").objectReferenceValue = fluff;
        mso.FindProperty("areas").objectReferenceValue = Object.FindObjectOfType<FuwaAreaVisibility>(true);
        Arr("goals", goals); Arr("spawns", spawns); Arr("goalFx", fx);
        mso.FindProperty("arenaCenter").objectReferenceValue = arena;
        mso.FindProperty("lobbySpawn").objectReferenceValue = lobby.Find("VersusSpawn");
        Arr("colorPads", pads); Arr("padLabels", padLabels);
        mso.FindProperty("lobbyBoard").objectReferenceValue = lobbyText;
        Arr("arenaBoards", boards.ToArray());
        mso.ApplyModifiedPropertiesWithoutUndo(); Sync(match);

        // エディタでも4色の並びで置いておく（実行時は色の数に合わせて並べ直す）
        for (int i = 0; i < 4; i++)
        {
            float a = (90f + 90f * i) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            goals[i].SetPositionAndRotation(ac + dir * 10f + Vector3.up * 2.8f, Quaternion.LookRotation(-dir));
            spawns[i].SetPositionAndRotation(ac + dir * 6.5f, Quaternion.LookRotation(-dir));
        }
        // 動く物（ゴール）と新しく足した物は static にしない
        foreach (var t in arena.GetComponentsInChildren<Transform>(true))
            if (t.GetComponentInParent<FuwaSharedBall>() != null || goals.Any(g => t.IsChildOf(g)) || t.IsChildOf(bRoot)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

        // 新しく足した Udon の同期の種類を、スクリプトの指定（Manual / None）に合わせる（足した直後は Continuous のままになっていた）
        FixSyncModes(arena); FixSyncModes(panel); FixSyncModes(mGo.transform);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        log.Add("arena at " + ac + ", panel at " + panel.position);
        return string.Join("\n", log);
    }

    static void FixSyncModes(Transform root)
    {
        foreach (var ub in root.GetComponentsInChildren<VRC.Udon.UdonBehaviour>(true))
        {
            var u = UdonSharpEditorUtility.GetProxyBehaviour(ub);
            if (u == null) continue;
            var attr = (UdonSharp.UdonBehaviourSyncModeAttribute)System.Attribute.GetCustomAttribute(u.GetType(), typeof(UdonSharp.UdonBehaviourSyncModeAttribute));
            if (attr == null || attr.behaviourSyncMode == UdonSharp.BehaviourSyncMode.Any) continue;
            var want = attr.behaviourSyncMode == UdonSharp.BehaviourSyncMode.Manual ? VRC.SDKBase.Networking.SyncType.Manual
                : attr.behaviourSyncMode == UdonSharp.BehaviourSyncMode.Continuous ? VRC.SDKBase.Networking.SyncType.Continuous : VRC.SDKBase.Networking.SyncType.None;
            if (ub.SyncMethod == want) continue;
            var so = new SerializedObject(ub); so.FindProperty("_syncMethod").intValue = (int)want; so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void Button(GameObject go, FuwaMatch match, string ev, string text)
    {
        var b = UdonSharpUndo.AddComponent<FuwaMatchButton>(go);
        var so = new SerializedObject(b);
        so.FindProperty("match").objectReferenceValue = match;
        so.FindProperty("eventName").stringValue = ev;
        so.ApplyModifiedPropertiesWithoutUndo(); Sync(b);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
        var uso = new SerializedObject(ub); var it = uso.FindProperty("interactText");
        if (it != null) { it.stringValue = text; uso.ApplyModifiedPropertiesWithoutUndo(); }
    }

    static TMPro.TextMeshPro MakeText(Transform parent, string name, TMPro.TextMeshPro like, float size, Vector2 rect)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var t = go.AddComponent<TMPro.TextMeshPro>();
        t.font = like.font; t.fontSharedMaterial = like.fontSharedMaterial;
        t.fontSize = size; t.enableAutoSizing = true; t.fontSizeMax = size; t.fontSizeMin = size * 0.4f;
        t.alignment = TMPro.TextAlignmentOptions.Center; t.color = like.color;
        t.rectTransform.sizeDelta = rect;
        t.richText = true;
        return t;
    }

    // みんなでロビーの言語切り替えの一覧から、コピーした看板の文字を外す（元の看板の文字だけ切り替える）
    static void RemoveFromLanguage(TMPro.TextMeshPro t)
    {
        foreach (var lang in Object.FindObjectsOfType<FuwaLanguage>(true))
        {
            var so = new SerializedObject(lang);
            var tx = so.FindProperty("texts"); var pj = so.FindProperty("ja"); var pe = so.FindProperty("en");
            for (int i = tx.arraySize - 1; i >= 0; i--)
            {
                if (tx.GetArrayElementAtIndex(i).objectReferenceValue != t) continue;
                tx.DeleteArrayElementAtIndex(i); pj.DeleteArrayElementAtIndex(i); pe.DeleteArrayElementAtIndex(i);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static ParticleSystem MakeGoalFx(Transform goal, Color c)
    {
        var go = new GameObject("GoalFx"); go.transform.SetParent(goal, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.playOnAwake = false; main.loop = false; main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(c, Color.white);
        main.gravityModifier = 0.6f; main.maxParticles = 200;
        var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 80) });
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.6f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = AssetDatabase.FindAssets("t:Material Sparkle").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault()
            ?? AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        return ps;
    }

    // 輪っか（太さ0.12、半径1.1。+Z 向きの面）
    static Mesh RingMesh()
    {
        const string path = "Assets/FuwaCourse/Meshes/Match_GoalRing.asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m == null) { m = new Mesh(); AssetDatabase.CreateAsset(m, path); }
        m.Clear(); m.name = "Match_GoalRing";
        const int N = 48, M = 12; const float R = 1.1f, r = 0.12f;
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        for (int i = 0; i <= N; i++)
        {
            float a = i * Mathf.PI * 2f / N; Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            for (int j = 0; j <= M; j++)
            {
                float b = j * Mathf.PI * 2f / M; Vector3 nn = d * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                v.Add(d * R + nn * r); n.Add(nn);
            }
        }
        for (int i = 0; i < N; i++) for (int j = 0; j < M; j++)
        {
            int a0 = i * (M + 1) + j, a1 = a0 + M + 1;
            t.AddRange(new[] { a0, a0 + 1, a1, a1, a0 + 1, a1 + 1 });
        }
        m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material TeamMat(int i)
    {
        string path = "Assets/FuwaCourse/Materials/Match_" + TeamNames[i] + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Mush_CapRed.mat")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", TeamColors[i]);
        m.SetColor("_EmissionColor", TeamColors[i] * 0.25f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material ButtonMat()
    {
        const string path = "Assets/FuwaCourse/Materials/Match_Button.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Mush_CapRed.mat")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", new Color(1f, 0.96f, 0.9f));
        m.SetColor("_EmissionColor", Color.black);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material BarrierMat()
    {
        const string path = "Assets/FuwaCourse/Materials/Match_Barrier.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/SoftBarrier.mat")); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_LineAlpha", 0.6f);
        m.SetFloat("_FilmAlpha", 0.06f);
        m.SetFloat("_LineFar", 40f);
        m.SetFloat("_LineHeight", 1.2f);
        m.SetFloat("_WallTop", 4f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SetU(Component c, string prop, Object v)
    {
        var so = new SerializedObject(c); var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning("no prop " + prop + " on " + c.GetType().Name); return; }
        p.objectReferenceValue = v; so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Sync(Component c)
    {
        if (c is UdonSharp.UdonSharpBehaviour u) UdonSharpEditorUtility.CopyProxyToUdon(u);
    }
}
