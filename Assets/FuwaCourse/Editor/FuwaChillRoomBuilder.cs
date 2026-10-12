using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 「みんなで」のチルルーム（2026-10-12 てじゃ「みんなでをチルルームにする方向性で固めよう」）。協力の試作（FuwaCoopTestBuilder の CoopTest）と置き換える。
// 島の真ん中にキノコ1本：撃つたびに今の設定のふわふわが出る（FuwaChillSpawner、FuwaSharedBall を Pool 個使い回す）。
// キノコの左にスイッチ4つ（サイズ／カラー／上限数／地面に落ちたら消えるか）。設定はみんなで共通。
// スポーン・エアガンの台・ワープ・遊び方の看板は FuwaCoop の直下にあるものをそのまま使う。何度実行しても同じ結果
public static class FuwaChillRoomBuilder
{
    const string LobbyName = "FuwaVersusLobby";
    static readonly Vector3 Offset = new Vector3(0f, 0f, 65f);   // 古いみんなでロビーからの島の位置（協力の試作と同じ）
    const int Pool = 10;
    const float MushScale = 1.8f;               // 待っている玉は見えないので、巨大（1.4）が隠れなくてもよい（3倍は大きすぎた。てじゃ「キノコデカいままかも」）
    // 配置（2026-10-12 てじゃ「真ん中にキノコ、右奥にデカソファでモニタを眺めるエリア、左奥にフワフワギミックエリア（未定）」）。スポーンは手前（-Z）
    static readonly Vector3 MushLocal = new Vector3(0f, 0f, 0f);          // 島の真ん中
    static readonly Vector3 PanelLocal = new Vector3(-2.9f, 0f, -2.0f);   // スイッチ（キノコの左手前、スポーンの方を向く）
    static readonly Vector3 LivingLocal = new Vector3(5.3f, 0f, 5.3f);    // 右奥の観戦リビング（ラグの中心）
    static readonly Vector3 HowToLocal = new Vector3(-5.6f, -0.12f, -6.4f);   // 遊び方の看板（スポーンの左前）
    // 左奥（-5.5, 0, 5.5）あたりはギミック用に空けておく

    [MenuItem("FuwaCourse/みんなで/チルルームを作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new List<string>();
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        var lobby = roots.First(g => g.name == LobbyName).transform;
        var coop = roots.First(g => g.name == "FuwaCoop").transform;
        var sys = roots.First(g => g.name == "FuwaSystem").transform;
        Vector3 ac = lobby.position + Offset;
        coop.position = ac;

        // 協力の試作・前のチルルームを片付け
        var oldGiant = coop.Find("CoopTest/GiantFluff");
        UnregisterMonitor(coop.Find("ChillRoom"));
        foreach (var o in new[] { coop.Find("CoopTest"), coop.Find("ChillRoom"), sys.Find("CoopCourse"), sys.Find("ChillSpawner") }) if (o != null) Object.DestroyImmediate(o.gameObject);
        var root = new GameObject("ChillRoom").transform; root.SetParent(coop, false); root.position = ac;

        // 島（古いみんなでロビーのコピー）
        foreach (var n in new[] { "VersusFloor", "VersusRing", "IslandUnder" })
        {
            var src = lobby.Find(n);
            var g = Object.Instantiate(src.gameObject, root, true); g.name = n;
            g.transform.position = src.position + Offset;
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }
        var floor = root.Find("VersusFloor");
        float floorY = floor.GetComponent<Renderer>().bounds.max.y;
        int floorLayer = floor.gameObject.layer;

        // キノコ
        var mushSrc = lobby.Find("CenterMushroom");
        var mush = Object.Instantiate(mushSrc.gameObject, root, true); mush.name = "Mushroom";
        mush.transform.position = new Vector3(ac.x, mushSrc.position.y, ac.z) + MushLocal;
        mush.transform.localScale = mushSrc.localScale * MushScale;

        // ふわふわ（ロビーの共有ふわふわのコピー）を Pool 個。待つ位置はキノコの中
        var fluffSrc = lobby.Find("SharedFluff");
        Vector3 home = mush.transform.position + Vector3.up * (fluffSrc.position.y - mushSrc.position.y) * MushScale;
        var pool = new GameObject("Fluffs").transform; pool.SetParent(root, false);
        var balls = new List<FuwaSharedBall>();
        for (int i = 0; i < Pool; i++)
        {
            var go = Object.Instantiate(fluffSrc.gameObject, pool, true); go.name = "Fluff" + i;
            go.transform.position = home; go.transform.localScale = Vector3.one * 0.3f;
            // 10個が同じ所に重なっているので、最初から動かない物にしておく（でないと始まった瞬間に物理で押し合って散らばり、その位置が待つ位置になる）
            var rb = go.GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true;
            var b = go.GetComponent<FuwaSharedBall>();
            var so = new SerializedObject(b);
            so.FindProperty("variant").boolValue = true;
            so.FindProperty("chill").objectReferenceValue = null;   // 下で係を作ってから入れる
            so.FindProperty("popLayers").intValue = 1 << floorLayer;
            so.FindProperty("fallLimit").floatValue = (home.y - floorY) + 3f;   // 島の外へ落ちたら（床の 3m 下で）キノコへ
            so.FindProperty("arenaCenter").objectReferenceValue = null;
            so.FindProperty("arenaRadius").floatValue = 0f;
            so.FindProperty("shotScale").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo(); Sync(b);
            balls.Add(b);
        }

        // 設定の係（同期するので FuwaSystem の下）
        var spGo = new GameObject("ChillSpawner"); spGo.transform.SetParent(sys, false);
        EnsureProgram("FuwaChillSpawner");
        var sp = UdonSharpUndo.AddComponent<FuwaChillSpawner>(spGo);
        sp.balls = balls.ToArray();
        foreach (var b in balls) { SetU(b, "chill", sp); Sync(b); }
        sp.language = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        foreach (var sm in mush.GetComponentsInChildren<FuwaStartMushroom>(true)) { SetU(sm, "sharedBall", null); SetU(sm, "ball", null); SetU(sm, "spawner", sp); Sync(sm); }

        // エアガンで吹けるふわふわ：消えた物を外して、新しい玉を足す
        foreach (var bl in Object.FindObjectsOfType<FuwaBlower>(true))
        {
            var bso = new SerializedObject(bl); var p = bso.FindProperty("sharedBalls");
            var list = new List<Object>();
            for (int i = 0; i < p.arraySize; i++) { var o = p.GetArrayElementAtIndex(i).objectReferenceValue; if (o != null && o != oldGiant) list.Add(o); }
            foreach (var b in balls) if (!list.Contains(b)) list.Add(b);
            p.arraySize = list.Count; for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            bso.ApplyModifiedPropertiesWithoutUndo(); Sync(bl);
        }

        // スイッチ4つ（2×2）。遊び方の看板の板をコピーした横長の看板を押す
        var spawn = coop.Find("CoopSpawn");
        var howTo = coop.Find("HowToPlayVersus");
        var panel = new GameObject("Switches").transform; panel.SetParent(root, false);
        Vector3 pc = new Vector3(ac.x, floorY, ac.z) + PanelLocal;
        Vector3 look = spawn != null ? spawn.position : ac + Vector3.back * 9f;
        var face = pc - look; face.y = 0f; var rot = Quaternion.LookRotation(face.normalized);
        var labels = new TMPro.TextMeshPro[4];
        for (int k = 0; k < 4; k++)
        {
            int col = k % 2, row = k / 2;
            var pos = pc + rot * new Vector3(col == 0 ? -1.02f : 1.02f, row == 0 ? 1.75f : 1.08f, 0f);
            var lbl = MakeButton(panel, howTo, "Switch" + k, pos, rot);
            labels[k] = lbl;
            var btn = lbl.transform.parent.gameObject;
            EnsureProgram("FuwaChillButton");
            var cb = UdonSharpUndo.AddComponent<FuwaChillButton>(btn); cb.spawner = sp; cb.kind = k; UdonSharpEditorUtility.CopyProxyToUdon(cb);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(cb);
            if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; ub.interactText = new[] { "サイズを変える", "色を変える", "上限を変える", "落ちたら消えるか" }[k]; EditorUtility.SetDirty(ub); }
        }
        // 支柱（木の棒）2本
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course_Wood.mat");
        foreach (float sx in new[] { -2.05f, 2.05f })
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder); post.name = "Post";
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.transform.SetParent(panel, false);
            post.transform.position = pc + rot * new Vector3(sx, 1.05f, 0.08f); post.transform.rotation = rot;
            post.transform.localScale = new Vector3(0.09f, 1.05f, 0.09f);
            post.GetComponent<MeshRenderer>().sharedMaterial = wood;
        }
        sp.sizeLabel = labels[0]; sp.colorLabel = labels[1]; sp.limitLabel = labels[2]; sp.popLabel = labels[3];
        UdonSharpEditorUtility.CopyProxyToUdon(sp);
        var sub = UdonSharpEditorUtility.GetBackingUdonBehaviour(sp); if (sub != null) { sub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; EditorUtility.SetDirty(sub); }

        if (howTo != null)
        {
            SetHowToText(howTo.Find("Text").GetComponent<TMPro.TextMeshPro>());
            var hp = ac + HowToLocal; var hd = hp - look; hd.y = 0f;
            howTo.position = hp; howTo.rotation = Quaternion.LookRotation(hd.normalized);   // 表（-Z）をスポーンへ
        }

        // 観戦モニター（ひとりでのロビーの写真館のモニターと、CAM・人物ボタンをコピー。2026-10-12 てじゃ「こっちでも観戦できる」）
        // テレビは観戦リビングの奥（島のふち側）、ソファの方を向く
        Vector3 L = new Vector3(ac.x, floorY, ac.z) + LivingLocal, dirL = new Vector3(LivingLocal.x, 0f, LivingLocal.z).normalized;
        log.Add(Monitor(root, L + dirL * 2.9f, L - dirL * 1.7f));
        log.Add(Decorate(root, ac, floorY, root.Find("Monitor")));
        log.Add(Toys(root, ac, floorY, sp, howTo, look));

        // 落ちた人の戻し：島の下の大きな板（FuwaLobbyFall）→ スポーンへ（見えないワープ）
        var backWarp = coop.Find("ReturnToHub");
        var gunSpot = coop.Find("GunStand_Versus/GunSpot") ?? spawn;
        if (backWarp != null && spawn != null)
        {
            var fallWarp = Object.Instantiate(backWarp.gameObject, root, true); fallWarp.name = "ChillFallWarp";
            foreach (var rr in fallWarp.GetComponentsInChildren<Renderer>(true)) rr.enabled = false;
            foreach (var cc in fallWarp.GetComponentsInChildren<Collider>(true)) cc.enabled = false;
            foreach (var ps in fallWarp.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            foreach (var t in fallWarp.GetComponentsInChildren<TMPro.TextMeshPro>(true)) t.gameObject.SetActive(false);
            var fw = fallWarp.GetComponentInChildren<FuwaCourseWarp>(true); SetU(fw, "playerSpot", spawn); SetU(fw, "gunSpot", gunSpot); Sync(fw);
            var lobbyFall = lobby.Find("FallReturn").GetComponent<FuwaLobbyFall>();
            var fall = new GameObject("ChillFall"); fall.transform.SetParent(root, false); fall.transform.position = ac + new Vector3(0f, -12f, 0f);
            var fbx = fall.AddComponent<BoxCollider>(); fbx.isTrigger = true; fbx.size = new Vector3(120f, 4f, 120f);
            EnsureProgram("FuwaLobbyFall");
            var nf = UdonSharpUndo.AddComponent<FuwaLobbyFall>(fall);
            nf.returnWarp = fw; nf.fade = lobbyFall.fade; nf.fallSound = lobbyFall.fallSound; UdonSharpEditorUtility.CopyProxyToUdon(nf);
            var nub = UdonSharpEditorUtility.GetBackingUdonBehaviour(nf); if (nub != null) { nub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(nub); }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(coop.gameObject.scene);
        log.Add("chill room at " + ac + " home " + home.ToString("F2") + " floorY " + floorY.ToString("F2") + " balls " + balls.Count);
        return string.Join("\n", log);
    }

    // ---------- おもちゃ広場（左奥。2026-10-12 てじゃ「エアガンとは別の手持ち扇風機、空中固定化」「出しっぱなし」「うちわもいいね」）----------
    static readonly Vector3 ToysLocal = new Vector3(-5.5f, 0f, 5.5f);

    static string Toys(Transform root, Vector3 ac, float floorY, FuwaChillSpawner sp, Transform howTo, Vector3 look)
    {
        var toys = new GameObject("Toys").transform; toys.SetParent(root, false);
        Vector3 G = new Vector3(ac.x, floorY, ac.z) + ToysLocal;
        var toCenter = (new Vector3(ac.x, floorY, ac.z) - G); toCenter.y = 0f; toCenter.Normalize();
        var side = new Vector3(toCenter.z, 0f, -toCenter.x);
        var fanMeshes = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaCourse/Models/ChillFan.fbx").OfType<Mesh>().ToDictionary(m => m.name);
        var uchiMeshes = AssetDatabase.LoadAllAssetsAtPath("Assets/FuwaCourse/Models/ChillUchiwa.fbx").OfType<Mesh>().ToDictionary(m => m.name);
        var fanBodyM = Mat("Chill_FanBody", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(0.66f, 0.84f, 1f));
        var fanBladeM = Mat("Chill_FanBlades", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(1f, 0.8f, 0.87f));
        var uFaceM = Mat("Chill_UchiwaFace", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(1f, 0.78f, 0.86f));
        var uRimM = Mat("Chill_UchiwaRim", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(1f, 0.97f, 0.9f));
        var windMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/WindParticle.mat");
        EnsureProgram("FuwaHandFan"); EnsureProgram("FuwaHandFanReset");

        GameObject Mesh(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 lp)
        {
            var g = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); g.transform.SetParent(parent, false); g.transform.localPosition = lp;
            g.GetComponent<MeshFilter>().sharedMesh = mesh; g.GetComponent<MeshRenderer>().sharedMaterials = mats;
            g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            g.layer = 13; return g;
        }
        ParticleSystem Streaks(Transform parent)
        {
            var g = new GameObject("Streaks", typeof(ParticleSystem)); g.transform.SetParent(parent, false);
            var ps = g.GetComponent<ParticleSystem>();
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.startLifetime = 0.55f; main.startSpeed = 5f; main.startSize = 0.06f;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 80; main.startColor = new Color(1f, 1f, 1f, 0.8f);
            var em = ps.emission; em.rateOverTime = 28f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.12f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = gr;
            var r = g.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.08f; r.lengthScale = 2f; r.sharedMaterial = windMat;
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
                var bl = Mesh(go.transform, "Blades", fanMeshes["Chill_FanBlades"], new[] { fanBladeM }, new Vector3(0f, 0.36f, 0f)).transform;
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
        var list = new List<FuwaHandFan>();
        for (int i = 0; i < 3; i++) list.Add(Held("Fan" + i, G + toCenter * 2.4f + side * (-1.6f + i * 0.55f) + Vector3.up * 1.0f, face, false));
        for (int i = 0; i < 2; i++) list.Add(Held("Uchiwa" + i, G + toCenter * 2.4f + side * (0.6f + i * 0.5f) + Vector3.up * 1.0f, face, true));
        // 戻すボタン（並びの下）
        var btnLbl = MakeButton(toys, howTo, "ResetToys", G + toCenter * 2.4f + Vector3.up * 0.45f, Quaternion.LookRotation(-toCenter));
        btnLbl.text = "おもちゃを戻す";
        var bgo = btnLbl.transform.parent.gameObject;
        var rs = UdonSharpUndo.AddComponent<FuwaHandFanReset>(bgo); rs.fans = list.ToArray(); UdonSharpEditorUtility.CopyProxyToUdon(rs);
        var rub = UdonSharpEditorUtility.GetBackingUdonBehaviour(rs); if (rub != null) { rub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; rub.interactText = "戻す"; EditorUtility.SetDirty(rub); }
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        if (lang != null) { var lso = new SerializedObject(lang); AddLang(lso, "texts", "ja", "en", btnLbl, "おもちゃを戻す", "Return toys"); lso.ApplyModifiedPropertiesWithoutUndo(); Sync(lang); }
        return "toys " + list.Count;
    }

    // ---------- 見た目（2026-10-12 てじゃ「B（空のリビング）」「モニターもふわふわのガワ、画面だけはめ込む、ボタンも」）----------
    // 家具は Blender（FLUFF_models.blend の ChillRoom シーン）→ Models/ChillRoomProps.fbx。Blender の正面（-Y）が Unity の +Z
    const string PropsPath = "Assets/FuwaCourse/Models/ChillRoomProps.fbx";
    const string MatDir = "Assets/FuwaCourse/Materials/Chill";

    static string Decorate(Transform root, Vector3 ac, float floorY, Transform monitorHolder)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Materials", "Chill");
        var meshes = AssetDatabase.LoadAllAssetsAtPath(PropsPath).OfType<Mesh>().ToDictionary(m => m.name);
        var deco = new GameObject("Deco").transform; deco.SetParent(root, false);
        Vector3 C = new Vector3(ac.x, floorY, ac.z) + LivingLocal;            // 観戦リビングの真ん中
        Vector3 dir = new Vector3(LivingLocal.x, 0f, LivingLocal.z).normalized; // リビングの奥（テレビの方）
        Vector3 perp = new Vector3(dir.z, 0f, -dir.x);

        // 床は芝に（ふちの輪は濃い芝）
        var grass = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Goal_Ground.mat");
        root.Find("VersusFloor").GetComponent<Renderer>().sharedMaterial = grass;
        root.Find("VersusRing").GetComponent<Renderer>().sharedMaterial = Mat("Chill_GrassRim", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(0.47f, 0.68f, 0.43f));

        var cloud = Mat("Chill_Cloud", "Assets/FuwaCourse/Generated/Signs/Sign_Cloud_FFFAF5.mat", new Color(0.99f, 0.98f, 1f));
        var rugM = Mat("Chill_Rug", "Assets/FuwaCourse/Generated/Signs/Sign_Cloud_FFFAF5.mat", new Color(0.97f, 0.95f, 1f));
        rugM.SetFloat("_UnderStrength", 0f); EditorUtility.SetDirty(rugM);   // ラグは真っ白に近く（雲の下の青い影はいらない）
        var stemM = Mat("Chill_LampStem", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(0.96f, 0.93f, 0.86f));
        var capM = Mat("Chill_LampCap", "Assets/FuwaCourse/Materials/Goal_Ground.mat", new Color(1f, 0.82f, 0.55f));
        capM.SetColor("_EmissionColor", new Color(0.9f, 0.6f, 0.25f)); EditorUtility.SetDirty(capM);
        var bean = new[] { new Color(1f, 0.72f, 0.82f), new Color(0.62f, 0.83f, 1f), new Color(1f, 0.9f, 0.55f), new Color(0.68f, 0.92f, 0.72f) }
            .Select((c, i) => Mat("Chill_Beanbag" + i, "Assets/FuwaCourse/Materials/Goal_Ground.mat", c)).ToArray();
        int floorLayer = root.Find("VersusFloor").gameObject.layer;

        GameObject Put(string mesh, Material m, Vector3 pos, Quaternion rot, bool collider, int layer = 0, string name = null)
        {
            var g = new GameObject(name ?? mesh, typeof(MeshFilter), typeof(MeshRenderer));
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
        Put("Chill_CloudSofa", cloud, sofaPos, Face(sofaPos, C + dir * 3f), true);
        // ビーズクッション：ソファの両わき2つと、テレビ寄りの両わき2つ（テレビの方を向く）
        var beanPos = new[] { C - dir * 0.9f - perp * 2.6f, C - dir * 0.9f + perp * 2.6f, C + dir * 0.9f - perp * 2.3f, C + dir * 0.9f + perp * 2.3f };
        for (int i = 0; i < beanPos.Length; i++)
        {
            var p = beanPos[i];
            Put("Chill_Beanbag", bean[i], p, Face(p, C + dir * 3f) * Quaternion.Euler(0f, (i % 2 == 0 ? 12f : -12f), 0f), true, 0, "Beanbag" + i);
        }
        // キノコのランプ：ソファの後ろの両わき
        foreach (var lp in new[] { C - dir * 2.7f - perp * 2.3f, C - dir * 2.7f + perp * 2.3f })
        {
            var lamp = new GameObject("Lamp").transform; lamp.SetParent(deco, false); lamp.position = lp;
            var s = Put("Chill_LampStem", stemM, lamp.position, Quaternion.identity, false); s.transform.SetParent(lamp, true);
            var c = Put("Chill_LampCap", capM, lamp.position, Quaternion.identity, false); c.transform.SetParent(lamp, true);
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
            var bodyM = SaveGenMesh(FuwaSignStyle.Pillow(body, hw, hh, face + front, front, 0.14f, rise, 16), "Chill_TVCloud");
            var rim = FuwaSignStyle.Scallop(FuwaSignStyle.Outline(hw - amp * 0.8f + rimW, hh - amp * 0.4f + rimW, round + rimW, 12, 0.03f), amp, lambda, shape);
            var rimM = SaveGenMesh(FuwaSignStyle.Pillow(rim, hw + rimW, hh + rimW, face + front + 0.2f, 0.08f, 0.08f, 0.25f, 8), "Chill_TVCloudRim");
            var tvCloudMat = Mat("Chill_TVCloud", "Assets/FuwaCourse/Generated/Signs/Sign_Cloud_FFFAF5.mat", new Color(0.99f, 0.98f, 1f));
            var rimC = new Color(0.62f, 0.55f, 0.86f);   // 写真館のモニターのふちの紫
            var tvRimMat = Mat("Chill_TVCloudRim", "Assets/FuwaCourse/Generated/Signs/Sign_CloudRim_8C63D5.mat", rimC);
            tvRimMat.SetColor("_UnderColor", FuwaSignStyle.RimUnderColor(rimC)); EditorUtility.SetDirty(tvRimMat);
            foreach (var (n, mesh, mat) in new[] { ("Cloud", bodyM, tvCloudMat), ("CloudRim", rimM, tvRimMat) })
            {
                var g = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer)); g.transform.SetParent(tv, false);
                g.GetComponent<MeshFilter>().sharedMesh = mesh; g.GetComponent<MeshRenderer>().sharedMaterial = mat;
                GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic);
            }
            var bc = tv.gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0f, face + 0.15f); bc.size = new Vector3((hw + rimW) * 2f, (hh + rimW) * 2f, 0.4f);
            // 元の看板の板・ふちは消す。題名は画面の上、ボタン2つは画面の下（どちらも雲の前の平らな所）
            foreach (var n in new[] { "Panel", "Border" }) { var t = mon.Find(n); if (t != null) t.gameObject.SetActive(false); }
            var title = mon.Find("Title");
            if (title != null) title.position = monitorHolder.TransformPoint(new Vector3(sl.x, sl.y + sh + 0.2f, face - 0.02f));
            var hr = monitorHolder.rotation;
            var tb = monitorHolder.Find("TargetButton"); var cb = monitorHolder.Find("CamButton");
            if (tb != null) { tb.position = monitorHolder.TransformPoint(new Vector3(sl.x - 0.6f, sl.y - sh - 0.24f, face - 0.03f)); tb.rotation = hr; }
            if (cb != null) { cb.position = monitorHolder.TransformPoint(new Vector3(sl.x + 0.6f, sl.y - sh - 0.24f, face - 0.03f)); cb.rotation = hr; }
        }
        return "deco ok";
    }

    static Mesh SaveGenMesh(Mesh m, string name)
    {
        const string dir = "Assets/FuwaCourse/Generated/Chill";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "Chill");
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
        return t;
    }

    // 遊び方の看板の文字（日本語・英語の切り替えにも登録してあるので両方書き換える）
    static void SetHowToText(TMPro.TextMeshPro t)
    {
        const string ja = "<align=center><size=150%><color=#FF6B5C>みんなでひろば</color></size></align>\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>キノコの傘を撃つと、ふわふわが出てくる\n<pos=1.1em>みんなで吹いて、のんびり遊ぼう\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>スイッチで、サイズ・色・出せる数・\n<pos=1.1em>落ちたら消えるかを変えられる\n<pos=1.1em>（設定はみんなで共通）\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>巨大は重いので、みんなで押そう\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>エアガンを手元に呼ぶ：PCはEキー\n<pos=1.1em>VRは右スティックを下に倒し続ける";
        const string en = "<align=center><size=150%><color=#FF6B5C>Fluff Plaza</color></size></align>\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Shoot the mushroom cap to pop out a fluff\n<pos=1.1em>Blow them around together and relax\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Switches change the size, color,\n<pos=1.1em>max count, and whether they pop on landing\n<pos=1.1em>(shared by everyone)\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Giant ones are heavy: push together!\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Summon your air gun: E key (PC)\n<pos=1.1em>or hold the right stick down (VR)";
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
