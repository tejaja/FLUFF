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
        return Mathf.Clamp(Mathf.Abs(s.x * s.y * s.z) * 0.065f, 3f, 24f);
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

        // 前半はまっすぐ、終わりぎわほど大きく乱れて、くるっと巻いて消える
        var noise = ps.noise;
        noise.enabled = true;
        noise.separateAxes = false;
        noise.strength = new ParticleSystem.MinMaxCurve(2.2f, new AnimationCurve(new Keyframe(0f, 0.12f), new Keyframe(0.55f, 0.25f), new Keyframe(1f, 1f)));
        noise.frequency = 0.45f;
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
        tr.lifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.6f);
        tr.minVertexDistance = 0.12f;
        tr.textureMode = ParticleSystemTrailTextureMode.Stretch;
        tr.worldSpace = true;
        tr.dieWithParticles = false;
        tr.sizeAffectsWidth = false;
        tr.sizeAffectsLifetime = false;
        tr.inheritParticleColor = true;
        tr.widthOverTrail = new ParticleSystem.MinMaxCurve(0.32f, new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));

        r.renderMode = ParticleSystemRenderMode.None;
        r.trailMaterial = StreakMaterial();
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
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
