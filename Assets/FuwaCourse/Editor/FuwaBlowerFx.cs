using UnityEditor;
using UnityEngine;

// エアガンの撃った時の見た目（空気っぽく）を作る。何度実行しても同じ結果。
// ・Puff（もとからある、FuwaBlower.puffEffect）：もこもこの空気のかたまり。前へ飛びながらふくらんで、減速して消える
//   （FuwaBlower がチャージに合わせて startSpeedMultiplier を変えるので、届く距離はチャージで変わる）
// ・Puff/AirRing：ラッパの口から前へ飛んで広がる空気の輪（Puff と一緒に再生される）
// ・Puff/AirStreaks：シュッと前へ伸びる細い風の筋
// ・FullBurst：満タンで撃った時の、大きめの輪＋ちいさな綿毛
public static class FuwaBlowerFx
{
    const string Mat = "Assets/FuwaCourse/Materials/";

    [MenuItem("FuwaCourse/エアガンの射撃エフェクトを作る")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = "";
        foreach (var bl in Object.FindObjectsOfType<FuwaBlower>(true))
        {
            var nz = bl.nozzle != null ? bl.nozzle : bl.transform.Find("Nozzle");
            var puff = bl.puffEffect;
            if (puff == null) continue;
            SetupPuff(puff);
            var ring = Child(puff.transform, "AirRing"); SetupRing(ring, 0.07f, 0.42f, 3.2f, 0.38f, 1);
            var streaks = Child(puff.transform, "AirStreaks"); SetupStreaks(streaks);
            if (bl.fullBurst != null) SetupFullBurst(bl.fullBurst);
            EditorUtility.SetDirty(bl);
            log += bl.name + " ";
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "blower fx: " + log;
    }

    static ParticleSystem Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); Undo.RegisterCreatedObjectUndo(t.gameObject, "fx"); }
        t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        var ps = t.GetComponent<ParticleSystem>(); if (ps == null) ps = t.gameObject.AddComponent<ParticleSystem>();
        return ps;
    }

    static void Common(ParticleSystem ps, Material mat, int max)
    {
        var m = ps.main; m.playOnAwake = false; m.loop = false; m.duration = 0.3f; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = max; m.gravityModifier = 0f; m.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; r.sortingFudge = 0f;
        foreach (var mod in new[] { "velocityOverLifetime", "noise", "trails", "lights", "subEmitters", "textureSheetAnimation", "collision", "externalForces", "inheritVelocity", "customData" }) { }
        var noise = ps.noise; noise.enabled = false; var vel = ps.velocityOverLifetime; vel.enabled = false; var rot = ps.rotationOverLifetime; rot.enabled = false;
    }

    static Gradient Fade(Color c0, float a0, float aMid, float midT)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c0, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(a0, 0.06f), new GradientAlphaKey(aMid, midT), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    // もこもこの空気（元の Puff を作り直す）
    static void SetupPuff(ParticleSystem ps)
    {
        Common(ps, AssetDatabase.LoadAssetAtPath<Material>(Mat + "FX_AirPuff.mat"), 120);
        var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f); m.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 5.0f);
        m.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.93f, 0.97f, 1f, 1f), new Color(1f, 1f, 1f, 1f));
        var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12), new ParticleSystem.Burst(0.03f, 5) });
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 13f; sh.radius = 0.03f; sh.radiusThickness = 1f; sh.arc = 360f; sh.position = Vector3.zero; sh.rotation = Vector3.zero;
        // 飛び出してすぐ減速（空気がぶわっと押し出されて止まる感じ）
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.15f; lim.dampen = 0.12f; lim.separateAxes = false;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f, 0, 6f), new Keyframe(0.35f, 2.2f), new Keyframe(1f, 3.2f)));
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, 0.55f, 0.35f, 0.45f);
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 1.5f; noise.scrollSpeed = 0.5f; noise.damping = true; noise.quality = ParticleSystemNoiseQuality.Low;
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Billboard; r.maxParticleSize = 2f; r.sortMode = ParticleSystemSortMode.Distance;
    }

    // 空気の輪（口の向きに広がる）
    static void SetupRing(ParticleSystem ps, float size0, float size1, float speed, float life, int count)
    {
        Common(ps, AssetDatabase.LoadAssetAtPath<Material>(Mat + "FX_AirRing.mat"), 6);
        var m = ps.main; m.startLifetime = life; m.startSpeed = speed; m.startSize = size0; m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new Color(1f, 1f, 1f, 1f);
        var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 0f; sh.radius = 0.0001f; sh.radiusThickness = 1f;
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.2f; lim.dampen = 0.07f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(size1 / size0, new AnimationCurve(new Keyframe(0, size0 / size1, 0, 5f), new Keyframe(0.4f, 0.75f), new Keyframe(1f, 1f)));
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, 0.8f, 0.45f, 0.5f);
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Billboard; r.alignment = ParticleSystemRenderSpace.Local; r.maxParticleSize = 3f;
    }

    // 細い風の筋
    static void SetupStreaks(ParticleSystem ps)
    {
        Common(ps, AssetDatabase.LoadAssetAtPath<Material>(Mat + "FX_AirStreak.mat"), 20);
        var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.26f); m.startSpeed = new ParticleSystem.MinMaxCurve(6f, 9f); m.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.04f);
        m.startColor = new Color(1f, 1f, 1f, 1f);
        var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 9f; sh.radius = 0.035f; sh.radiusThickness = 0.3f;
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 1f; lim.dampen = 0.08f;
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, 0.75f, 0.5f, 0.4f);
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 4f; r.cameraVelocityScale = 0f;
    }

    // 満タンで撃った時：大きい輪（2枚少しずらす）＋ちいさな綿毛がふわっと
    static void SetupFullBurst(ParticleSystem ps)
    {
        Common(ps, AssetDatabase.LoadAssetAtPath<Material>(Mat + "FX_AirPuff.mat"), 40);
        var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f); m.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.8f, 1f), new Color(1f, 1f, 1f, 1f)); m.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 40f; sh.radius = 0.04f;
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.2f; lim.dampen = 0.08f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.8f)));
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, 0.9f, 0.7f, 0.6f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.4f; noise.frequency = 2f; noise.damping = true; noise.quality = ParticleSystemNoiseQuality.Low;
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Billboard;
        var big = Child(ps.transform, "BigRing"); SetupRing(big, 0.1f, 0.75f, 3.8f, 0.5f, 1);
        var big2 = Child(ps.transform, "BigRing2"); SetupRing(big2, 0.08f, 0.55f, 2.6f, 0.45f, 1);
        var em2 = big2.emission; em2.SetBursts(new[] { new ParticleSystem.Burst(0.06f, 1) });
    }
}
