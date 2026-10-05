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

    // 見た目の候補（てじゃが選ぶ用）。数字を変えて Run(variant) で作り直す
    public class V
    {
        public string name;
        public int puffCount = 12, puffCount2 = 5; public float puffAlpha = 0.55f, puffSize = 1f, puffGrow = 3.2f, puffSpeed = 1f;
        public int rings = 1; public float ringSize = 0.42f, ringAlpha = 0.8f, ringSpeed = 3.2f, ringLife = 0.38f;
        public int streaks = 6; public float streakAlpha = 0.75f, streakSize = 1f, streakAngle = 9f, streakLen = 4f; public bool swirl = false;
        public float tint = 0.66f;
        public bool puffOuter = false; public bool ringBold = false;   // もこもこを真ん中を避けて外側に／輪を太く濃く
    }
    public static readonly V[] Variants =
    {
        new V { name = "1_今の" },
        new V { name = "2_濃いめ", puffCount = 18, puffCount2 = 8, puffAlpha = 0.8f, puffSize = 1.2f, ringAlpha = 1f, ringSize = 0.5f, streaks = 8, streakAlpha = 0.95f, tint = 0.75f },
        new V { name = "3_輪っかメイン", puffCount = 4, puffCount2 = 2, puffAlpha = 0.4f, rings = 3, ringSize = 0.55f, ringAlpha = 1f, ringSpeed = 4.2f, ringLife = 0.45f, streaks = 2, tint = 0.75f },
        new V { name = "4_風の筋メイン", puffCount = 5, puffCount2 = 2, puffAlpha = 0.45f, puffSize = 0.8f, rings = 0, streaks = 16, streakAlpha = 0.95f, streakSize = 1.3f, streakAngle = 14f, streakLen = 6f, tint = 0.75f },
        new V { name = "5_渦巻き", puffCount = 8, puffCount2 = 3, puffAlpha = 0.5f, rings = 1, ringSize = 0.38f, streaks = 12, streakAlpha = 0.9f, streakSize = 1.2f, streakAngle = 4f, streakLen = 3.5f, swirl = true, tint = 0.72f },
        new V { name = "6_輪っか濃いめ（真ん中あき）", puffCount = 14, puffCount2 = 6, puffAlpha = 0.8f, puffSize = 1.1f, rings = 3, ringSize = 0.6f, ringAlpha = 1f, ringSpeed = 4.2f, ringLife = 0.48f, streaks = 4, streakAlpha = 0.9f, tint = 0.78f, puffOuter = true, ringBold = true },
    };
    static V cur = Variants[2];   // てじゃが選んだ「3_輪っかメイン」（2026-10-05）

    [MenuItem("FuwaCourse/エアガンの射撃エフェクトを作る")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run(int variant)
    {
        cur = Variants[Mathf.Clamp(variant, 0, Variants.Length - 1)];
        foreach (var (n, k) in new[] { ("FX_AirPuff", 1f), ("FX_AirStreak", 1.04f), ("FX_AirRing", 1.03f), ("FX_AirRingBold", 1.03f) })
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Mat + n + ".mat"); if (m == null) continue; float t = cur.tint * k;
            m.SetColor("_TintColor", new Color(t, t * 1.05f, t * 1.11f, 0.5f)); EditorUtility.SetDirty(m);
        }
        return Run() + " (" + cur.name + ")";
    }

    public static string Run()
    {
        var log = "";
        foreach (var bl in Object.FindObjectsOfType<FuwaBlower>(true))
        {
            var nz = bl.nozzle != null ? bl.nozzle : bl.transform.Find("Nozzle");
            var puff = bl.puffEffect;
            if (puff == null) continue;
            SetupPuff(puff);
            var ring = Child(puff.transform, "AirRing"); SetupRing(ring, 0.07f, cur.ringSize, cur.ringSpeed, cur.ringLife, cur.rings, cur.ringAlpha);
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
        var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f); m.startSpeed = new ParticleSystem.MinMaxCurve(2.2f * cur.puffSpeed, 5.0f * cur.puffSpeed);
        m.startSize = new ParticleSystem.MinMaxCurve(0.05f * cur.puffSize, 0.1f * cur.puffSize); m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.93f, 0.97f, 1f, 1f), new Color(1f, 1f, 1f, 1f));
        var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)cur.puffCount), new ParticleSystem.Burst(0.03f, (short)cur.puffCount2) });
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = cur.puffOuter ? 26f : 13f; sh.radius = cur.puffOuter ? 0.06f : 0.03f; sh.radiusThickness = cur.puffOuter ? 0f : 1f; sh.arc = 360f; sh.position = Vector3.zero; sh.rotation = Vector3.zero;   // 外側：口のふちの輪から外へ開くように出す（真ん中があく）
        // 飛び出してすぐ減速（空気がぶわっと押し出されて止まる感じ）
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.15f; lim.dampen = 0.12f; lim.separateAxes = false;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.6f, 0, 6f), new Keyframe(0.35f, cur.puffGrow * 0.7f), new Keyframe(1f, cur.puffGrow)));
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, cur.puffAlpha, cur.puffAlpha * 0.62f, 0.45f);
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
        var noise = ps.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 1.5f; noise.scrollSpeed = 0.5f; noise.damping = true; noise.quality = ParticleSystemNoiseQuality.Low;
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Billboard; r.maxParticleSize = 2f; r.sortMode = ParticleSystemSortMode.Distance;
    }

    // 空気の輪（口の向きに広がる）
    static void SetupRing(ParticleSystem ps, float size0, float size1, float speed, float life, int count, float alpha = 0.8f)
    {
        Common(ps, AssetDatabase.LoadAssetAtPath<Material>(Mat + (cur.ringBold ? "FX_AirRingBold.mat" : "FX_AirRing.mat")), 8);
        var m = ps.main; m.startLifetime = life; m.startSpeed = speed; m.startSize = size0; m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new Color(1f, 1f, 1f, 1f);
        // 2つ以上の時は、少しずつ遅らせて連なって飛ぶ（空気砲の連打のように）
        var bursts = new ParticleSystem.Burst[Mathf.Max(count, 0)];
        for (int i = 0; i < bursts.Length; i++) bursts[i] = new ParticleSystem.Burst(i * 0.05f, 1);
        var em = ps.emission; em.enabled = count > 0; em.SetBursts(bursts);
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 0f; sh.radius = 0.0001f; sh.radiusThickness = 1f;
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.2f; lim.dampen = 0.07f;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(size1 / size0, new AnimationCurve(new Keyframe(0, size0 / size1, 0, 5f), new Keyframe(0.4f, 0.75f), new Keyframe(1f, 1f)));
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, alpha, alpha * 0.56f, 0.5f);
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Billboard; r.alignment = ParticleSystemRenderSpace.Local; r.maxParticleSize = 3f;
    }

    // 細い風の筋
    static void SetupStreaks(ParticleSystem ps)
    {
        Common(ps, AssetDatabase.LoadAssetAtPath<Material>(Mat + "FX_AirStreak.mat"), 20);
        var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.26f) ; m.startSpeed = new ParticleSystem.MinMaxCurve(6f, 9f); m.startSize = new ParticleSystem.MinMaxCurve(0.025f * cur.streakSize, 0.04f * cur.streakSize);
        m.startColor = new Color(1f, 1f, 1f, 1f);
        var em = ps.emission; em.enabled = cur.streaks > 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, cur.streaks)) });
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = cur.streakAngle; sh.radius = cur.swirl ? 0.06f : 0.035f; sh.radiusThickness = cur.swirl ? 0f : 0.3f;
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 1f; lim.dampen = 0.08f;
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, cur.streakAlpha, cur.streakAlpha * 0.66f, 0.4f);
        // 渦巻き：筋がまわりながら前へ（軌道の速さで、ぐるっと回る）
        var vel = ps.velocityOverLifetime; vel.enabled = cur.swirl; if (cur.swirl) { vel.space = ParticleSystemSimulationSpace.Local; vel.orbitalZ = new ParticleSystem.MinMaxCurve(14f); vel.radial = new ParticleSystem.MinMaxCurve(0.6f); vel.x = 0f; vel.y = 0f; vel.z = 0f; }
        if (cur.swirl) { m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.42f); m.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 5f); }
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = cur.streakLen; r.cameraVelocityScale = 0f;
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
