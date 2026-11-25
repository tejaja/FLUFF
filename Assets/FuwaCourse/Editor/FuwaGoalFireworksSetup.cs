using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// ゴール花火の作り直し（2026-11-25 てじゃ「ゴール花火、もっとクオリティあげれないかな」）
//  前：同じ丸い粒が球に飛ぶだけ（大3発＋小4発）。
//  今：大玉3発を別の種類に（①色玉＋白い芯 ②傾いた輪 ③金の垂れ柳）、開いた瞬間の光、
//      勢いよく開いてふわっと止まり少し垂れる（空気抵抗）、消えぎわのキラキラ、打ち上げの火花。小玉4発も同じ作り。
//  音（sfx_fireworks.wav）のタイミング：打ち上げ 0 / 0.35 / 0.7、ドン 0.9 / 1.25 / 1.6、小さいパン 2.2 / 2.4 / 2.65 / 2.9
//  FuwaGoal.fireworks（GoalFx/Fireworks）の ParticleSystem はそのまま使い、中身と子を作り直す（Play(true) で全部出る）
public static class FuwaGoalFireworksSetup
{
    [MenuItem("FuwaCourse/ゴール：花火を作り直す（3種＋小玉）")]
    public static void Menu() { Debug.Log(Run()); }

    const float BigLife = 0.9f;     // 打ち上げ → ドン
    const float SmallLife = 0.7f;
    // 2026-11-25 てじゃ「至近距離で入れる事おおいから、派手だけど頭上になっちゃう」「範囲は前くらいでいいのかも」「柵の外でうちあがるのもありかも」
    //  → 自分から見てゴールの向こう側、柵の外（島のふちの少し下）からまっすぐ打ち上げ（FuwaGoal.fireworksAway / fireworksDrop）。開く広さは前の版くらい
    //    天井のあるゴール（2-5 の洞窟）は今まで通り真ん中から
    //  → てじゃ「とおいな」：柵のすぐ外から、自分のほうへ14°傾けて打つ（ゴールの中心の2mくらい向こう・5m上で開く。見上げて40°くらい）
    const float TowardTilt = 14f;
    const float OutsideGap = 0.3f;   // 柵からさらに外へ
    const float OutsideDrop = 1.0f;
    static float _tilt;
    //  → てじゃ「うーんとおいな」「最初の位置、高さ感けっこうよかったんだな」：前の版と同じゴールの真ん中から・同じ高さ（4.5m）に戻した。柵の外から打つ仕組みは残して切っておく
    const bool LaunchOutside = false;
    static Material _star, _flash, _trail;

    // 色玉の色（ロケットごとにどれか1色、開いた星が受けつぐ）
    static readonly Color[] Palette = {
        new Color(1f, 0.42f, 0.72f), new Color(0.35f, 0.88f, 1f), new Color(1f, 0.8f, 0.28f),
        new Color(0.62f, 1f, 0.4f), new Color(0.74f, 0.5f, 1f) };
    static readonly Color Gold = new Color(1f, 0.78f, 0.35f);
    static readonly Color WarmWhite = new Color(1f, 0.97f, 0.86f);

    // 2026-11-25 シーンが GitHub の上限（100MB）をこえた（粒の部品は1個 約76KB、花火は 21個×12ゴール＝約19MB）
    //  → 花火は1つのプレハブ（GoalFireworks.prefab）に作って、12か所はそれを置くだけ（てじゃ「おっけ～」）。中身は前と同じ
    public const string PrefabPath = "Assets/FuwaCourse/Prefabs/GoalFireworks.prefab";

    public static string Run()
    {
        _star = Mat("GoalFwStar", StarTex()); _flash = Mat("GoalFwFlash", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaCourse/Textures/GoalCatchSoftDot.png")); _trail = Mat("GoalFwTrail", TrailTex());
        if (!LaunchOutside) return RunPrefab();
        int n = 0; string log = "";
        foreach (var goal in Resources.FindObjectsOfTypeAll<FuwaGoal>())
        {
            if (EditorUtility.IsPersistent(goal) || goal.fireworks == null) continue;
            Vector3 c = goal.fireworks.transform.position;
            bool roof = Physics.Raycast(c + Vector3.up * 0.5f, Vector3.up, 20f);
            float fenceR = 0f;
            foreach (var r in goal.transform.root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name != "Fence") continue;
                Vector3 dd = r.bounds.center - c; dd.y = 0f;
                if (dd.magnitude > 3f || Mathf.Abs(r.bounds.center.y - c.y) > 4f) continue;
                fenceR = Mathf.Max(r.bounds.extents.x, r.bounds.extents.z);
            }
            bool outside = LaunchOutside && !roof && fenceR > 0f;
            Undo.RecordObject(goal, "fw");
            goal.fireworksAway = outside ? fenceR + OutsideGap : 0f;
            goal.fireworksDrop = outside ? OutsideDrop : 0f;
            _tilt = outside ? TowardTilt : 0f;
            Build(goal.fireworks, outside ? 1.15f : 0.95f);   // 真ん中からは前の版の高さ（打ち上げ 9.5m/s → 4.5mで開く）
            UdonSharpEditorUtility.CopyProxyToUdon(goal);
            EditorUtility.SetDirty(goal);
            log += "\n  " + goal.transform.root.name + (outside ? " 柵の外 " + goal.fireworksAway.ToString("F1") + "m" : (roof ? " 天井あり→真ん中" : " 柵なし→真ん中"));
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "[ゴール花火] " + n + " か所を作り直した" + log;
    }

    static string RunPrefab()
    {
        // プレハブを作る（作り直し）
        if (!AssetDatabase.IsValidFolder("Assets/FuwaCourse/Prefabs")) AssetDatabase.CreateFolder("Assets/FuwaCourse", "Prefabs");
        var tmp = new GameObject("Fireworks");
        var tps = tmp.AddComponent<ParticleSystem>();
        _tilt = 0f;
        Build(tps, 0.95f);   // 真ん中からは前の版の高さ（打ち上げ 9.5m/s → 4.5mで開く）
        var prefab = PrefabUtility.SaveAsPrefabAsset(tmp, PrefabPath);
        Object.DestroyImmediate(tmp);
        // 12か所：今の花火（GoalFx/Fireworks）をプレハブに置きかえる
        int n = 0;
        foreach (var goal in Resources.FindObjectsOfTypeAll<FuwaGoal>())
        {
            if (EditorUtility.IsPersistent(goal) || goal.fireworks == null) continue;
            var old = goal.fireworks.transform;
            ParticleSystem ps;
            if (PrefabUtility.GetCorrespondingObjectFromSource(old.gameObject) == prefab) ps = goal.fireworks;   // もうプレハブ
            else
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.parent);
                Undo.RegisterCreatedObjectUndo(inst, "fw prefab");
                inst.name = old.name;
                inst.transform.localPosition = old.localPosition; inst.transform.localRotation = old.localRotation; inst.transform.localScale = old.localScale;
                inst.transform.SetSiblingIndex(old.GetSiblingIndex());
                inst.layer = old.gameObject.layer;
                ps = inst.GetComponent<ParticleSystem>();
                Undo.DestroyObjectImmediate(old.gameObject);
            }
            Undo.RecordObject(goal, "fw");
            goal.fireworks = ps; goal.fireworksAway = 0f; goal.fireworksDrop = 0f;
            UdonSharpEditorUtility.CopyProxyToUdon(goal);
            EditorUtility.SetDirty(goal);
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "[ゴール花火] プレハブを作って " + n + " か所に置いた";
    }

    // up：打ち上げの速さの倍率（柵の外は島のふちの下から上がるので強め）
    static void Build(ParticleSystem root, float up)
    {
        var go = root.gameObject;
        for (int i = go.transform.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(go.transform.GetChild(i).gameObject);
        Undo.RecordObject(root, "fw");

        // ① 色玉（外は色、中に白い芯）
        Rocket(root, 0f, new[] { 0f }, 10f * up, BigLife, 0f, new ParticleSystem.MinMaxGradient(Palette[0], Palette[4]), out var a);   // 桃〜紫
        Shell(a, "Peony", 56, 3.91f, 2.2f, 0.25f, new Vector2(1.3f, 1.7f), 0.39f, 0.14f, true, 2, ShellShape.Sphere);
        Shell(a, "Pistil", 20, 1.82f, 2.2f, 0.25f, new Vector2(0.9f, 1.2f), 0.32f, 0.1f, false, 0, ShellShape.Sphere, WarmWhite);
        Flash(a, 3.2f);

        // ② 傾いた輪（中に小さい白い輪）
        var b = Child(root, "RocketRing");
        Rocket(b, 0.35f, new[] { 0f }, 10.3f * up, BigLife, 9f, new ParticleSystem.MinMaxGradient(Palette[1], Palette[3]), out _);   // 水色〜黄緑
        Shell(b, "Ring", 40, 3.91f, 2.2f, 0.25f, new Vector2(1.3f, 1.6f), 0.37f, 0.14f, true, 2, ShellShape.Ring);
        Shell(b, "RingInner", 20, 1.96f, 2.2f, 0.25f, new Vector2(1.0f, 1.3f), 0.30f, 0.1f, false, 0, ShellShape.Ring, WarmWhite);
        Flash(b, 3.0f);

        // ③ 金の垂れ柳（長い尾をひいて、ゆっくり垂れる）
        var c = Child(root, "RocketWillow");
        Rocket(c, 0.7f, new[] { 0f }, 10.3f * up, BigLife, -9f, new ParticleSystem.MinMaxGradient(Gold), out _);
        Shell(c, "Willow", 50, 2.59f, 1.6f, 0.12f, new Vector2(2.0f, 2.4f), 0.30f, 0.5f, true, 3, ShellShape.Sphere);
        Flash(c, 3.4f);

        // 小玉4発（低めで横に散る）
        var s = Child(root, "RocketsSmall");
        Rocket(s, 1.5f, new[] { 0f, 0.2f, 0.45f, 0.7f }, 8.5f * up, SmallLife, 0f, RandomPalette(), out _, 26f);
        Shell(s, "SmallStars", 26, 2.65f, 2.6f, 0.25f, new Vector2(0.9f, 1.2f), 0.29f, 0.12f, true, 1, ShellShape.Sphere);
        Flash(s, 1.8f);

        EditorUtility.SetDirty(go);
    }

    // ---- 打ち上げ ----
    static void Rocket(ParticleSystem ps, float delay, float[] bursts, float speed, float life, float tiltZ, ParticleSystem.MinMaxGradient col, out ParticleSystem self, float coneAngle = 4f)
    {
        self = ps;
        ps.transform.localEulerAngles = new Vector3(0f, 0f, tiltZ);
        Base(ps, 10, _star);
        var m = ps.main;
        m.duration = 1.2f; m.startDelay = delay;
        m.startLifetime = life; m.startSpeed = speed; m.gravityModifier = 1f;
        m.startSize = 0.16f; m.startColor = col;
        var em = ps.emission;
        var bs = new ParticleSystem.Burst[bursts.Length];
        for (int i = 0; i < bursts.Length; i++) bs[i] = new ParticleSystem.Burst(bursts[i], 1);
        em.SetBursts(bs);
        var sh = ps.shape; sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = coneAngle; sh.radius = 0.1f; sh.rotation = new Vector3(-90f + _tilt, 0f, 0f);   // 自分のほうへ傾けて打つ（FuwaGoal が花火を自分のほうへ向ける＝+Z が自分）
        Trails(ps, 0.22f, 1f);
        // 火花（打ち上げ中ずっとこぼれる）
        var sp = Child(ps, "Sparks");
        Base(sp, 200, _star);
        var sm = sp.main;
        sm.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f); sm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.9f);
        sm.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f); sm.gravityModifier = 0.5f;
        sm.startColor = new ParticleSystem.MinMaxGradient(Gold, WarmWhite);
        var sem = sp.emission; sem.rateOverTime = 45f;
        var ssh = sp.shape; ssh.enabled = true; ssh.shapeType = ParticleSystemShapeType.Sphere; ssh.radius = 0.02f;
        Fade(sp, 0f);
        Twinkle(sp, 0f);
        AddSub(ps, sp, ParticleSystemSubEmitterType.Birth, ParticleSystemSubEmitterProperties.InheritNothing);
    }

    enum ShellShape { Sphere, Ring }

    // ---- 開いた星 ----
    //  drag：勢いよく開いてふわっと止まる（広がり ≒ speed / drag）
    static void Shell(ParticleSystem rocket, string name, int count, float speed, float drag, float grav, Vector2 life, float size, float trail, bool inheritColor, int crackle, ShellShape shape, Color? fixedColor = null)
    {
        var ps = Child(rocket, name);
        Base(ps, count * 4 + 20, _star);
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        m.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.92f, speed);
        m.startSize = new ParticleSystem.MinMaxCurve(size * 0.85f, size);
        m.gravityModifier = grav;
        m.startColor = new ParticleSystem.MinMaxGradient(fixedColor ?? Color.white);
        var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var sh = ps.shape; sh.enabled = true;
        if (shape == ShellShape.Sphere) { sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.05f; sh.radiusThickness = 0f; }
        else
        {
            sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.05f; sh.radiusThickness = 0f;
            sh.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;   // 輪の上に等間隔
            sh.rotation = new Vector3(90f, 0f, 0f);   // 水平の輪：ゴールの近くから見上げると、どの向きからでも楕円の輪に見える（サブエミッターの形は親の回転を受けつがず、ワールドの向きのまま）
        }
        var lv = ps.limitVelocityOverLifetime; lv.enabled = true;
        lv.limit = 100f; lv.drag = drag; lv.multiplyDragByParticleSize = false; lv.multiplyDragByParticleVelocity = false;
        Fade(ps, 0.55f);
        Twinkle(ps, 0.55f);
        if (trail > 0f) Trails(ps, trail, 0.8f);
        AddSub(rocket, ps, ParticleSystemSubEmitterType.Death, inheritColor ? ParticleSystemSubEmitterProperties.InheritColor : ParticleSystemSubEmitterProperties.InheritNothing);
        // 消えぎわのキラキラ（パチパチ）
        if (crackle > 0)
        {
            var ck = Child(ps, "Crackle");
            Base(ck, count * crackle + 20, _star);
            var cm = ck.main;
            cm.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.65f); cm.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.1f);
            cm.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f); cm.gravityModifier = 0.25f;
            cm.startColor = new ParticleSystem.MinMaxGradient(WarmWhite, Gold);
            var cem = ck.emission; cem.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)crackle) });
            var csh = ck.shape; csh.enabled = true; csh.shapeType = ParticleSystemShapeType.Sphere; csh.radius = 0.03f;
            Fade(ck, 0f);
            Twinkle(ck, 0f, 3f);
            AddSub(ps, ck, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
        }
    }

    // ---- 開いた瞬間の光 ----
    static void Flash(ParticleSystem rocket, float size)
    {
        var ps = Child(rocket, "Flash");
        Base(ps, 8, _flash);
        var m = ps.main;
        m.startLifetime = 0.22f; m.startSpeed = 0f; m.startSize = size; m.gravityModifier = 0f;
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.3f));   // 昼の空だとベタっと丸く見えるので、うっすら
        var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        var sh = ps.shape; sh.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.35f, 0.4f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.15f)));
        AddSub(rocket, ps, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritColor);
    }

    // ---- 共通 ----
    static ParticleSystem Child(Component parent, string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "fw");
        go.transform.SetParent(parent.transform, false);
        return go.AddComponent<ParticleSystem>();
    }

    static void Base(ParticleSystem ps, int max, Material mat)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.playOnAwake = false; m.loop = false; m.duration = 1.2f; m.startDelay = 0f;
        m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.maxParticles = max; m.startSpeed = 0f;
        m.startRotation = 0f; m.startSize3D = false;
        var em = ps.emission; em.enabled = true; em.rateOverTime = 0f; em.rateOverDistance = 0f; em.SetBursts(new ParticleSystem.Burst[0]);
        var sh = ps.shape; sh.enabled = false;
        var col = ps.colorOverLifetime; col.enabled = false;
        var sz = ps.sizeOverLifetime; sz.enabled = false;
        var lv = ps.limitVelocityOverLifetime; lv.enabled = false;
        var nz = ps.noise; nz.enabled = false;
        var tr = ps.trails; tr.enabled = false;
        var se = ps.subEmitters; se.enabled = false;
        while (se.subEmittersCount > 0) se.RemoveSubEmitter(0);
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat; r.trailMaterial = _trail;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.sortMode = ParticleSystemSortMode.None;
    }

    static void AddSub(ParticleSystem parent, ParticleSystem child, ParticleSystemSubEmitterType type, ParticleSystemSubEmitterProperties props)
    {
        var se = parent.subEmitters; se.enabled = true;
        se.AddSubEmitter(child, type, props);
    }

    // 寿命の間の明るさ：ぱっと出て、fadeFrom から消えていく
    static void Fade(ParticleSystem ps, float fadeFrom)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, Mathf.Max(0.05f, fadeFrom)), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }

    // 大きさ：はじめ少し大きく → 後半（from から）粒ごとにバラバラに瞬きながら小さく
    static void Twinkle(ParticleSystem ps, float from, float speedMul = 1f)
    {
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, TwCurve(from, 4.3f * speedMul, 0f), TwCurve(from, 6.1f * speedMul, 0.5f));
    }

    static AnimationCurve TwCurve(float from, float cycles, float phase)
    {
        var c = new AnimationCurve();
        const int N = 48;
        for (int i = 0; i <= N; i++)
        {
            float t = i / (float)N;
            float env = t < 0.08f ? Mathf.Lerp(1.3f, 1f, t / 0.08f) : (t < from ? 1f : Mathf.Lerp(1f, 0.35f, (t - from) / (1f - from)));
            float k = t < from ? 0f : Mathf.Clamp01((t - from) / 0.15f);   // 瞬きは後半だけ
            float w = 0.5f + 0.5f * Mathf.Sin((t * cycles + phase) * Mathf.PI * 2f);
            c.AddKey(t, env * Mathf.Lerp(1f, Mathf.Lerp(0.35f, 1.15f, w * w), k));
        }
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        return c;
    }

    static void Trails(ParticleSystem ps, float life, float width)
    {
        var tr = ps.trails; tr.enabled = true;
        tr.mode = ParticleSystemTrailMode.PerParticle;
        tr.ratio = 1f; tr.lifetime = life; tr.minVertexDistance = 0.08f;
        tr.worldSpace = true; tr.dieWithParticles = false;
        tr.textureMode = ParticleSystemTrailTextureMode.Stretch;
        tr.sizeAffectsWidth = true; tr.inheritParticleColor = true;
        tr.widthOverTrail = new ParticleSystem.MinMaxCurve(width * 0.45f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorOverTrail = g;
    }

    static ParticleSystem.MinMaxGradient RandomPalette()
    {
        var g = new Gradient(); g.mode = GradientMode.Fixed;
        var keys = new GradientColorKey[Palette.Length];
        for (int i = 0; i < Palette.Length; i++) keys[i] = new GradientColorKey(Palette[i], (i + 1f) / Palette.Length);
        g.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        var mm = new ParticleSystem.MinMaxGradient(g); mm.mode = ParticleSystemGradientMode.RandomColor;
        return mm;
    }

    // ---- マテリアルと模様 ----
    static Material Mat(string name, Texture2D tex)
    {
        string path = "Assets/FuwaCourse/Materials/" + name + ".mat";
        var src = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");   // 今までの花火と同じ（Particles/Standard Unlit・Fade）
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
        else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
        m.mainTexture = tex; EditorUtility.SetDirty(m);
        return m;
    }

    // 明るい芯＋うすいにじみ
    static Texture2D StarTex()
    {
        return MakeTex("Assets/FuwaCourse/Textures/GoalFwStar.png", 64, 64, (u, v) =>
        {
            float dx = u * 2f - 1f, dy = v * 2f - 1f, r2 = dx * dx + dy * dy;
            return Mathf.Min(1f, 1.1f * Mathf.Exp(-r2 * 14f) + 0.3f * Mathf.Exp(-r2 * 3.5f)) * Mathf.Clamp01((1f - r2) * 4f);
        });
    }

    // 尾：幅方向だけぼかす（長さ方向の消え方はトレイルの色で）
    static Texture2D TrailTex()
    {
        return MakeTex("Assets/FuwaCourse/Textures/GoalFwTrail.png", 16, 32, (u, v) =>
        {
            float d = v * 2f - 1f;
            return Mathf.Exp(-d * d * 5f) * Mathf.Clamp01((1f - Mathf.Abs(d)) * 5f);
        });
    }

    static Texture2D MakeTex(string path, int w, int h, System.Func<float, float, float> alpha)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex != null) return tex;
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            t.SetPixel(x, y, new Color(1f, 1f, 1f, alpha((x + 0.5f) / w, (y + 0.5f) / h)));
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true; imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
