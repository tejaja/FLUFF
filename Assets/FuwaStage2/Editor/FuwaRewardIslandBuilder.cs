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
    public static float IslandScale = 0.65f, Dist = 30f;
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
        var root = new GameObject(RootName).transform; root.position = center + dir * Dist; root.rotation = Quaternion.LookRotation(dir, Vector3.up);   // +z が奥（ロビーと反対）、−z がロビーの方
        float s = IslandScale;
        // 島（ロビーの床・縁・下の岩・柵を小さく写す）
        foreach (var nm in new[] { "LobbyFloor", "LobbyRing", "IslandUnder", "Fence", "FenceCollider" })
        {
            var src = lobby.Find(nm); if (!src) { log.Add(nm + " なし"); continue; }
            var c = Object.Instantiate(src.gameObject, root, false); c.name = nm;
            c.transform.localPosition = Vector3.Scale(src.localPosition, new Vector3(s, 1f, s)); c.transform.localRotation = src.localRotation;
            c.transform.localScale = nm == "IslandUnder" ? src.localScale * s : Vector3.Scale(src.localScale, new Vector3(s, 1f, s));
            FuwaStage2Builder.StripBehaviour(c);
            foreach (var r in c.GetComponentsInChildren<Renderer>(true)) r.lightmapIndex = -1;   // ロビーの焼きを引きずらない（次に焼く時に入る）
        }
        float floorY = lobby.Find("LobbySpawn") ? lobby.Find("LobbySpawn").position.y - center.y : 0.1f;
        float r5 = Vector3.Distance(new Vector3(slot5.position.x, 0f, slot5.position.z), new Vector3(center.x, 0f, center.z));

        // 島の中の場所（root の local：+z が島のまん中から奥、−z がロビーの方）
        var spawn = new GameObject("IslandSpawn").transform; spawn.SetParent(root, false); spawn.localPosition = new Vector3(0f, floorY, -r5 * s + 1.6f); spawn.localRotation = Quaternion.identity;
        var ballPark = new GameObject("IslandBallPark").transform; ballPark.SetParent(root, false); ballPark.localPosition = new Vector3(-1.2f, floorY + 0.7f, -r5 * s + 1.8f);
        // 銃の台（ロビーのを動きごと写して、置き場をつなぎ直す）
        Transform gunSpot = null;
        var stSrc = lobby.Find("GunStand_Lobby");
        if (stSrc)
        {
            var st = Object.Instantiate(stSrc.gameObject, root, false); st.name = "GunStand_Island";
            st.transform.localPosition = new Vector3(1.4f, stSrc.position.y - center.y, -r5 * s + 1.6f); st.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            gunSpot = st.transform.Find("GunSpot");
            var mk = lobby.Find("GunStand_Lobby_Marker");
            GameObject mkC = null;
            if (mk) { mkC = Object.Instantiate(mk.gameObject, root, false); mkC.name = "GunStand_Island_Marker"; mkC.transform.localPosition = st.transform.localPosition + Vector3.up * (mk.position.y - stSrc.position.y); }
            var m = st.GetComponents<MonoBehaviour>().FirstOrDefault(x => x.GetType().Name == "FuwaGunStand");
            if (m)
            {
                var so = new SerializedObject(m); so.FindProperty("gunSpot").objectReferenceValue = gunSpot; if (mkC) so.FindProperty("marker").objectReferenceValue = mkC; so.ApplyModifiedProperties();
                UdonSharpEditorUtility.CopyProxyToUdon((UdonSharp.UdonSharpBehaviour)m);
            }
            foreach (var r in st.GetComponentsInChildren<Renderer>(true)) r.lightmapIndex = -1;
        }

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
            if (lab) { var g = Object.Instantiate(lab.gameObject, lck, false); g.name = "Label"; g.transform.localPosition = lab.localPosition; g.transform.localRotation = lab.localRotation; g.transform.localScale = lab.localScale; FuwaStage2Builder.StripBehaviour(g); var t = g.GetComponent<TMPro.TMP_Text>(); if (t) t.text = "<size=120%>?</size>"; foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.lightmapIndex = -1; }   // 全角の「？」は看板のフォントにない
            if (swirl)
            {
                var g = Object.Instantiate(swirl.gameObject, lck, false); g.name = "SwirlLocked"; g.transform.localPosition = swirl.localPosition; g.transform.localRotation = swirl.localRotation; g.transform.localScale = swirl.localScale;
                FuwaStage2Builder.StripBehaviour(g);
                foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { r.sharedMaterials = r.sharedMaterials.Select(m => GrayCopy(m)).ToArray(); r.lightmapIndex = -1; }
            }
        }
        // 島からロビーへ（島のロビー寄りのはし、島のまん中を向く）
        var back = new GameObject("ReturnPortal").transform; back.SetParent(root, false);
        back.position = root.position - dir * (r5 * s) + Vector3.up * (slot5.position.y - center.y); back.rotation = Quaternion.AngleAxis(180f, Vector3.up) * slot5.rotation;
        CopyWarpLook(back, "ReturnToLobby", "<size=80%>ロビーへ</size>");
        var lobSpot = new GameObject("LobbySpawn_FromReward").transform; lobSpot.SetParent(root, true);
        lobSpot.position = slot5.position - dir * 1.8f + Vector3.up * (floorY - (slot5.position.y - center.y)); lobSpot.rotation = Quaternion.LookRotation(-dir, Vector3.up);
        FuwaStage2Builder.MakeWarp(back, "ReturnToLobby", trigSrc.gameObject, gaugeSrc.gameObject, lobby.Find("BallPark"), lobSpot, lobby.Find("GunStand_Lobby/GunSpot"), 2);
        // 島の中身：全クリした人だけ
        var content = new GameObject("RewardContent").transform; content.SetParent(root, false);
        PhotoStage(content, floorY, slot4, log);
        log.Add(GoldGunSetup());
        GoldGunStand(content, floorY, slot4);
        TrophyShelf(content, floorY);
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
        ("Fuwa_BlowerMuzzle", new Color(1.00f, 0.90f, 0.55f)), ("Fuwa_BlowerVent", new Color(0.86f, 0.64f, 0.24f)) };
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
    static void GoldGunStand(Transform content, float floorY, Transform slot4)
    {
        var brass = FuwaGearTest.Mat("FanTest_Brass", new Color(0.86f, 0.68f, 0.32f));
        var light = FuwaGearTest.Mat("GearTest_GearLight", new Color(0.62f, 0.65f, 0.71f));
        var st = new GameObject("GoldGunStand").transform; st.SetParent(content, false); st.localPosition = new Vector3(-3.4f, floorY, -1.0f); st.localRotation = Quaternion.Euler(0f, 50f, 0f);
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
