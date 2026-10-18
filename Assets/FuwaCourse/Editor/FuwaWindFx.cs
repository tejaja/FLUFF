using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 風（横風・追い風）の見た目を「風の筋（やわらかい白い帯が流れて、終わりぎわに乱れて消える）＋舞う葉っぱ」にする。
// 今ある粒（FuwaWindArea.visual / visualReverse）の設定を書き換え、葉っぱは子に足して extraVisuals に登録する。
// 粒の出る範囲は今の粒のまま、向きは風の向き、速さは風の強さ、量は範囲の大きさから決める。何度実行しても同じ結果になる。
// 上昇気流・斜めの突風は見た目が別物なので対象外（visual に粒が登録されている風だけ）。
public static class FuwaWindFx
{
    const string MatDir = "Assets/FuwaCourse/Materials";
    const string MeshDir = "Assets/FuwaCourse/Meshes";

    [MenuItem("FuwaCourse/風のエフェクトを作り直す")]
    public static void RunAll()
    {
        int n = 0;
        foreach (var wa in Object.FindObjectsOfType<FuwaWindArea>(true))
        {
            if (wa.transform.root.name == "FuwaDebug") continue;
            if (Mathf.Abs(wa.transform.TransformDirection(wa.localDirection).normalized.y) > 0.5f) continue;   // 上向きの風（上昇気流など）は別
            if (Apply(wa)) n++;
        }
        Debug.Log($"[風] {n}か所を作り直した");
    }

    public static bool Apply(FuwaWindArea wa)
    {
        bool any = false;
        var so = new SerializedObject(wa);
        Vector3 worldDir = wa.transform.TransformDirection(wa.localDirection).normalized;
        // 筋の速さは風の強さから（強い横風ほど速く流れる）
        float speed = Mathf.Clamp(wa.strength * 1.5f, 5f, 9f);
        if (wa.visual != null && wa.visual.GetComponent<ParticleSystem>() != null)
        {
            var ps = wa.visual.GetComponent<ParticleSystem>();
            Vector3 dir = ps.transform.InverseTransformDirection(worldDir).normalized;
            SetupStreaks(ps, dir, speed);
            so.FindProperty("extraVisuals").arraySize = 1;
            so.FindProperty("extraVisuals").GetArrayElementAtIndex(0).objectReferenceValue = SetupLeaves(ps, dir, speed);
            any = true;
        }
        if (wa.visualReverse != null && wa.visualReverse.GetComponent<ParticleSystem>() != null)
        {
            var ps = wa.visualReverse.GetComponent<ParticleSystem>();
            Vector3 dir = ps.transform.InverseTransformDirection(-worldDir).normalized;
            SetupStreaks(ps, dir, speed);
            so.FindProperty("extraVisualsReverse").arraySize = 1;
            so.FindProperty("extraVisualsReverse").GetArrayElementAtIndex(0).objectReferenceValue = SetupLeaves(ps, dir, speed);
            any = true;
        }
        if (!any) return false;
        so.FindProperty("warnLevel").floatValue = 0.3f;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(wa);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(wa.gameObject.scene);
        return true;
    }

    // 範囲の大きさに合わせた、1秒に出す筋の数（大きい横風で18本くらい）
    static float StreakRate(ParticleSystem ps)
    {
        Vector3 s = ps.shape.scale;
        return Mathf.Clamp(Mathf.Abs(s.x * s.y * s.z) * 0.065f, 3f, 24f) * RateScale(ps);
    }

    // かぜのおかの密度（2026-10-18 てじゃ「最初の追い風は密度ちょっとさげる、横風はもうちょいだけ多く」）
    public static float RateScale(ParticleSystem ps)
    {
        var p = ps.transform.parent; string n = p != null ? p.name : "";
        if (n == "G1_Tailwind") return 0.5f;   // 0.7 →「おいかぜだけもうちょい密度低めに」
        if (n == "G2_CrossLeft" || n == "G3_CrossRight") return 1.2f;
        return 1f;
    }

    static void SetupStreaks(ParticleSystem ps, Vector3 dir, float newSpeed)
    {
        Undo.RecordObject(ps, "wind fx");
        var r = ps.GetComponent<ParticleSystemRenderer>();
        Undo.RecordObject(r, "wind fx");
        float rate = StreakRate(ps);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.5f);
        main.startSpeed = 0f;
        main.startSize = 0.2f;
        main.maxParticles = Mathf.CeilToInt(rate * 2.5f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.75f), new Color(0.92f, 0.97f, 1f, 0.6f));

        var em = ps.emission;
        em.rateOverTime = rate;

        // 出る場所は今の範囲のまま、風上側に寄せる（風下へ流れていく分）
        var sh = ps.shape;
        float travel = newSpeed * 1.3f;
        Vector3 half = sh.scale * 0.5f;
        float extent = Mathf.Abs(dir.x) * half.x + Mathf.Abs(dir.y) * half.y + Mathf.Abs(dir.z) * half.z;
        sh.position = -dir * Mathf.Min(travel * 0.35f, 4f, extent * 0.8f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(dir.x * newSpeed);
        vel.y = new ParticleSystem.MinMaxCurve(dir.y * newSpeed);
        vel.z = new ParticleSystem.MinMaxCurve(dir.z * newSpeed);

        // 出てすぐからゆったり波打って、終わりぎわほど大きく揺れて消える（Vketブースの風と同じ形。2026-10-18）
        var noise = ps.noise;
        noise.enabled = true;
        noise.separateAxes = false;
        noise.strength = new ParticleSystem.MinMaxCurve(2.2f * SleekNoise, SwayCurve());
        noise.frequency = 0.45f * SleekFreq;
        noise.scrollSpeed = 0.6f;
        noise.damping = true;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sol = ps.sizeOverLifetime; sol.enabled = false;

        var tr = ps.trails;
        tr.enabled = true;
        tr.mode = ParticleSystemTrailMode.PerParticle;
        tr.ratio = 1f;
        tr.lifetime = new ParticleSystem.MinMaxCurve(0.45f * SleekTrail, 0.6f * SleekTrail);
        tr.minVertexDistance = 0.1f;
        tr.textureMode = ParticleSystemTrailTextureMode.Stretch;
        tr.worldSpace = true;
        tr.dieWithParticles = false;
        tr.sizeAffectsWidth = false;
        tr.sizeAffectsLifetime = false;
        tr.inheritParticleColor = true;
        tr.widthOverTrail = new ParticleSystem.MinMaxCurve(0.32f * SleekWidth, WidthCurve());

        r.renderMode = ParticleSystemRenderMode.None;
        r.trailMaterial = BandMaterial();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // 風の筋を長く、ゆらゆら（2026-10-18 てじゃ「Vketのに近くしたい」）。速さは風の強さに合わせてあるのでそのまま
    // 揺れは 1.3 倍＋出てすぐから大きく揺れる形だと「ゆらゆらが目立ちすぎ」（てじゃ）→ 0.85 倍、出てすぐは控えめに → 0.6 倍（てじゃ）
    // 形は細くとがった筋（0.56 倍）から「もっと帯みたいな形」→ 同じ幅で続く帯・太め（1.25 倍）＋ふちくっきり・少し薄く（帯の材質）
    // 筋の長さ 1.6 倍 →「もっと長くても良いかも」で 2.24 倍（1.6×1.4）
    public const float SleekWidth = 1.25f, SleekTrail = 2.24f, SleekNoise = 0.6f, SleekFreq = 0.78f;
    static AnimationCurve SwayCurve() => new AnimationCurve(new Keyframe(0f, 0.22f), new Keyframe(0.5f, 0.4f), new Keyframe(1f, 1f));
    static AnimationCurve WidthCurve() => new AnimationCurve(new Keyframe(0f, 0.7f), new Keyframe(0.1f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0.6f));   // 先も後ろも同じ幅の帯（両端は材質でうすく）

    // 帯の材質（WindStreak の複製。突風・つむじ風・あそびばの扇風機は元の WindStreak のまま）
    public static Material BandMaterial()
    {
        string path = MatDir + "/WindStreakBand.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(StreakMaterial()); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_EdgeSoft", 0.4f);
        var c = m.GetColor("_Color"); c.a = 0.62f; m.SetColor("_Color", c);
        EditorUtility.SetDirty(m);
        return m;
    }

    // シーンで風ごとに寿命・筋の長さを調整してある所があるので、作り直さずに今の設定へ掛け算で入れる（帯の材質で済みか判定、何度実行しても同じ）
    [MenuItem("FuwaCourse/風の筋を細長くゆらゆらに（今の調整のまま）")]
    public static string SleekAll()
    {
        var list = new List<ParticleSystem>();
        foreach (var wa in Object.FindObjectsOfType<FuwaWindArea>(true))
        {
            if (wa.transform.root.name == "FuwaDebug") continue;
            // 斜めの突風（かぜのおか2か所・きのこのたにのデッキ）の基本の筋も（2026-10-18 てじゃ「一個だけかわってた、全部かえていいかも」）。速い筋・ゆっくりの筋はそのまま
            bool gust = wa.visual != null && wa.visual.name == "GustStreaks";
            if (!gust && Mathf.Abs(wa.transform.TransformDirection(wa.localDirection).normalized.y) > 0.5f) continue;
            foreach (var v in new[] { wa.visual, wa.visualReverse }) if (v != null && v.GetComponent<ParticleSystem>() != null) list.Add(v.GetComponent<ParticleSystem>());
        }
        var top = GameObject.Find("FuwaCourse2/Gimmick_Tailwind/TailwindParticlesTop"); if (top != null) list.Add(top.GetComponent<ParticleSystem>());
        string log = "";
        var band = BandMaterial();
        foreach (var ps in list)
        {
            var tr = ps.trails;
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            if (pr.trailMaterial == band) continue;   // もう帯にしてある
            Undo.RecordObject(pr, "sleek wind"); pr.trailMaterial = band;
            Undo.RecordObject(ps, "sleek wind");
            float life = ps.main.startLifetime.constantMin;
            tr.widthOverTrail = new ParticleSystem.MinMaxCurve(tr.widthOverTrailMultiplier * SleekWidth, WidthCurve());
            // 筋の長さは粒の寿命まで（粒が消えた後はしっぽが追いついて縮むだけ）
            float t0 = Mathf.Min(tr.lifetime.constantMin * SleekTrail, life), t1 = Mathf.Min(tr.lifetime.constantMax * SleekTrail, life);
            tr.lifetime = new ParticleSystem.MinMaxCurve(t0, Mathf.Max(t0, t1));
            tr.minVertexDistance = Mathf.Min(tr.minVertexDistance, 0.1f);
            var nz = ps.noise;
            nz.strength = new ParticleSystem.MinMaxCurve(nz.strengthMultiplier * SleekNoise, SwayCurve());
            nz.frequency *= SleekFreq;
            EditorUtility.SetDirty(ps); EditorUtility.SetDirty(pr);
            log += ps.transform.parent.name + "/" + ps.name + "\n";
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        return log;
    }

    static ParticleSystem SetupLeaves(ParticleSystem streaks, Vector3 dir, float speed)
    {
        var t = streaks.transform.Find("WindLeaves");
        GameObject go;
        if (t == null)
        {
            go = new GameObject("WindLeaves");
            Undo.RegisterCreatedObjectUndo(go, "leaves");
            go.transform.SetParent(streaks.transform, false);
            go.AddComponent<ParticleSystem>();
        }
        else go = t.gameObject;
        var ps = go.GetComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();
        Undo.RecordObject(ps, "leaves"); Undo.RecordObject(r, "leaves");

        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.3f, 1.8f);
        main.startSpeed = 0f;
        main.startSize3D = false;
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 30;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.05f;
        // 草の緑2色と、花びらの白・黄・ピンク
        var cg = new Gradient();
        cg.mode = GradientMode.Fixed;
        cg.SetKeys(new[] {
            new GradientColorKey(new Color(0.55f, 0.82f, 0.48f), 0.3f),
            new GradientColorKey(new Color(0.42f, 0.7f, 0.42f), 0.55f),
            new GradientColorKey(new Color(1f, 0.74f, 0.84f), 0.72f),
            new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0.86f),
            new GradientColorKey(new Color(0.98f, 0.97f, 0.94f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        main.startColor = new ParticleSystem.MinMaxGradient(cg) { mode = ParticleSystemGradientMode.RandomColor };

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = Mathf.Max(1.5f, StreakRate(streaks) * 0.4f);

        var sh = ps.shape;
        var src = streaks.shape;
        sh.enabled = true;
        sh.shapeType = src.shapeType;
        sh.scale = src.scale;
        sh.position = src.position;
        sh.rotation = src.rotation;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        float s = speed * 0.85f;
        vel.x = new ParticleSystem.MinMaxCurve(dir.x * s * 0.85f, dir.x * s * 1.1f);
        vel.y = new ParticleSystem.MinMaxCurve(dir.y * s * 0.85f + 0.2f, dir.y * s * 1.1f + 0.9f);
        vel.z = new ParticleSystem.MinMaxCurve(dir.z * s * 0.85f, dir.z * s * 1.1f);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-9f, 9f);
        rot.y = new ParticleSystem.MinMaxCurve(-6f, 6f);
        rot.z = new ParticleSystem.MinMaxCurve(-12f, 12f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 1.2f;
        noise.frequency = 0.6f;
        noise.scrollSpeed = 0.8f;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Low;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.1f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1f, 0f)));

        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = LeafMesh();
        r.sharedMaterial = LeafMaterial();
        r.alignment = ParticleSystemRenderSpace.World;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.enableGPUInstancing = false;   // 自作シェーダーは粒のインスタンシングに対応していない（色が来なくなる）
        return ps;
    }

    static Material StreakMaterial()
    {
        string path = MatDir + "/WindStreak.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("FuwaCourse/WindStreak")); AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    static Material LeafMaterial()
    {
        string path = MatDir + "/WindLeaf.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("FuwaCourse/WindLeaf")); m.enableInstancing = true; AssetDatabase.CreateAsset(m, path); }
        return m;
    }

    // ひし形の葉っぱ（長さ1、真ん中の筋で少し折れている）
    static Mesh LeafMesh()
    {
        string path = MeshDir + "/WindLeaf.asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (m != null && m.uv.Length > 0 && m.colors.Length == 0) return m;
        var tip = new Vector3(0, 0, 0.5f); var tail = new Vector3(0, 0, -0.5f);
        var left = new Vector3(-0.24f, 0.06f, 0.05f); var right = new Vector3(0.24f, 0.06f, 0.05f);
        var V = new List<Vector3> { tail, left, tip, tail, tip, right };
        var nl = Vector3.Cross(left - tail, tip - tail).normalized;
        var nr = Vector3.Cross(tip - tail, right - tail).normalized;
        var N = new List<Vector3> { nl, nl, nl, nr, nr, nr };
        // 頂点カラーは入れない（メッシュに色があると、粒の色が正しく届かず真っ黒っぽくなる）
        var U = new List<Vector2> { new Vector2(0.5f, 0), new Vector2(0, 0.5f), new Vector2(0.5f, 1), new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(1, 0.5f) };
        var nm = new Mesh { name = "WindLeaf" };
        nm.SetVertices(V); nm.SetNormals(N); nm.SetUVs(0, U); nm.SetTriangles(new[] { 0, 1, 2, 3, 4, 5 }, 0);
        nm.RecalculateBounds();
        if (m != null) { m.Clear(); EditorUtility.CopySerialized(nm, m); }
        else { m = nm; AssetDatabase.CreateAsset(m, path); }
        return m;
    }
}
