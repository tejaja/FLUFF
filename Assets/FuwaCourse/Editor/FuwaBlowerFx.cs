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
            // 満タンの合図：口のまわりで星がきらっ（撃った時の輪とは別）
            var sp = Child(nz, "ChargedSparkle"); SetupSparkle(sp);
            Undo.RecordObject(bl, "fx"); bl.chargedSparkle = sp; UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(bl);
            EditorUtility.SetDirty(bl);
            log += bl.name + " ";
        }
        // ソフトパーティクル：VRChatでは有効で、後ろに物があると近いほど透明になる（初期値だと1mもぼける）。
        // 銃のすぐ手前の粒が銃にかき消されて「はみ出た所しか見えない」ので、ぼかす距離を2cm弱にする（エディタでは無効なので気づきにくい）
        foreach (var bl in Object.FindObjectsOfType<FuwaBlower>(true))
            foreach (var pr in bl.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var pm = pr.sharedMaterial; if (pm == null || !pm.HasProperty("_InvFade")) continue;
                pm.SetFloat("_InvFade", 60f); EditorUtility.SetDirty(pm);
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
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = cur.streakAngle; sh.radius = cur.swirl ? 0.06f : 0.035f; sh.radiusThickness = cur.swirl ? 0f : 0.3f; sh.position = new Vector3(0f, 0f, 0.06f);   // 口より少し前から出す
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 1f; lim.dampen = 0.08f;
        var col = ps.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, cur.streakAlpha, cur.streakAlpha * 0.66f, 0.4f);
        // 引き伸ばした筋は粒の後ろへ尾を引くので、出た瞬間に大きいと尾が銃の後ろ（風船のあたり）まで突き抜ける。
        // 出た直後は小さくして、口から前へ離れてから伸ばす
        var ssz = ps.sizeOverLifetime; ssz.enabled = true; ssz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(0.35f, 1f, 0f, 0f), new Keyframe(1f, 1f)));
        // 渦巻き：筋がまわりながら前へ（軌道の速さで、ぐるっと回る）
        var vel = ps.velocityOverLifetime; vel.enabled = cur.swirl; if (cur.swirl) { vel.space = ParticleSystemSimulationSpace.Local; vel.orbitalZ = new ParticleSystem.MinMaxCurve(14f); vel.radial = new ParticleSystem.MinMaxCurve(0.6f); vel.x = 0f; vel.y = 0f; vel.z = 0f; }
        if (cur.swirl) { m.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.42f); m.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 5f); }
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0f; r.lengthScale = cur.streakLen * 1.8f; r.cameraVelocityScale = 0f;
        // 速さで伸ばす分（velocityScale）は大きさの曲線が効かず、出た瞬間から 0.4m 以上の尾が銃を貫いて手前まで伸びていた（VRChat主観で確認 2026-10-05）。
        // 長さは大きさだけで決める（出た直後は小さい→尾も短い）
    }

    // 満タンになった瞬間の合図：ラッパの口のふちに、4つ角の小さな星が6つ。ぱっと出て、少し外へ広がって、きらっと瞬いて消える
    static void SetupSparkle(ParticleSystem ps)
    {
        Common(ps, StarMaterial(), 16);
        var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.22f); m.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.6f); m.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.11f);
        m.startRotation = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f); m.simulationSpace = ParticleSystemSimulationSpace.Local;
        m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.45f, 1f), new Color(1f, 1f, 0.75f, 1f));
        var em = ps.emission; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 6) });
        var sh = ps.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.065f; sh.radiusThickness = 0f; sh.arc = 360f; sh.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread; sh.rotation = Vector3.zero;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.2f), new Keyframe(0.15f, 1.3f), new Keyframe(0.45f, 0.9f), new Keyframe(1f, 0f)));
        var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) }); col.color = g;
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.05f; lim.dampen = 0.15f;
        var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Billboard;
        // 口の所で一瞬だけ白くぱっと光る
        var fl = Child(ps.transform, "Flash"); Common(fl, StarMaterial(), 2);
        var fm = fl.main; fm.startLifetime = 0.13f; fm.startSpeed = 0f; fm.startSize = 0.2f; fm.simulationSpace = ParticleSystemSimulationSpace.Local; fm.startColor = new Color(1f, 1f, 0.92f, 1f);
        var fe = fl.emission; fe.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        var fs = fl.shape; fs.enabled = false;
        var fz = fl.sizeOverLifetime; fz.enabled = true; fz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.3f, 1.2f), new Keyframe(1f, 0.6f)));
        var fc = fl.colorOverLifetime; fc.enabled = true; var fg = new Gradient(); fg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.3f), new GradientAlphaKey(0f, 1f) }); fc.color = fg;
        fl.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.Billboard;
    }

    static Material StarMaterial()
    {
        string tp = "Assets/FuwaCourse/Textures/FX/FX_Star.png";
        if (true)
        {
            int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float u = (x + 0.5f) / S * 2 - 1, v = (y + 0.5f) / S * 2 - 1; float r = Mathf.Sqrt(u * u + v * v);
                // 4つ角の星（細い十字）＋まん中のやわらかい光
                float cross = Mathf.Max(Mathf.Clamp01(1 - Mathf.Abs(u) / (0.09f * (1 - Mathf.Abs(v)) + 0.01f)) * Mathf.Clamp01(1 - Mathf.Abs(v)),
                                        Mathf.Clamp01(1 - Mathf.Abs(v) / (0.09f * (1 - Mathf.Abs(u)) + 0.01f)) * Mathf.Clamp01(1 - Mathf.Abs(u)));
                float core = Mathf.Clamp01(1 - r / 0.42f); core = Mathf.Sqrt(core) * core;   // まん中を大きく明るく
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(Mathf.Max(cross, core * 0.9f))));
            }
            t.Apply(); System.IO.File.WriteAllBytes(tp, t.EncodeToPNG()); AssetDatabase.ImportAsset(tp);
            var ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
        }
        string mp = Mat + "FX_Star.mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null) { mat = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended")); AssetDatabase.CreateAsset(mat, mp); }
        // 明るい空の上でも見えるよう、加算ではなく普通の半透明で、こい黄色
        mat.shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(tp); mat.SetColor("_TintColor", new Color(0.8f, 0.76f, 0.6f, 0.5f)); EditorUtility.SetDirty(mat);
        return mat;
    }

    // 満タンで撃った時：強すぎて、ラッパの裏の通気口（首まわりの輪のすき間）から空気が後ろへバシュッと漏れる
    // ・LeakSheet：すき間から開くスカート状の空気の膜（テクスチャのコマ送りで、筋が根元から外へ走って消える）
    // ・LeakJets：膜にそって一周、細いジェットがシュッ
    // ＋前には大きい輪（2枚少しずらす）
    const float VentZ = -0.0795f, VentR = 0.018f;   // FullBurst から見た通気口の位置（銃の z=0.1405、半径 0.015〜0.0205 のすき間）
    static void SetupFullBurst(ParticleSystem ps)
    {
        Common(ps, AssetDatabase.LoadAssetAtPath<Material>(Mat + "FX_AirStreak.mat"), 1);
        var em = ps.emission; em.enabled = false;   // 本体は何も出さない（子を一緒に再生するだけ）
        var shp = ps.shape; shp.enabled = false;
        var oldSz = ps.sizeOverLifetime; oldSz.enabled = false; var oldCol = ps.colorOverLifetime; oldCol.enabled = false; var oldLim = ps.limitVelocityOverLifetime; oldLim.enabled = false;

        // 蒸気が漏れるみたいな、プシュッとした白いもや：すき間の輪から小さく薄いもやが一瞬ぶわっと出て、すぐ溶けて消える。
        // 丸い泡に見えないよう、もやのテクスチャは形のはっきりしない雲（ノイズ）にして、向きも大きさもばらばらに
        // （前に試した膜・ジェットは LeakSheet/LeakJets。消しておく。作る関数は戻せるように残してある）
        foreach (var oldName in new[] { "LeakSheet", "LeakJets" }) { var o = ps.transform.Find(oldName); if (o != null) Undo.DestroyObjectImmediate(o.gameObject); }
        var mist = Child(ps.transform, "LeakMist"); Common(mist, MistMaterial(), 48);
        var mm = mist.main; mm.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.3f); mm.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
        mm.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.034f); mm.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        mm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.7f), new Color(0.95f, 0.98f, 1f, 0.95f));
        var me = mist.emission; me.SetBursts(new[] { new ParticleSystem.Burst(0f, 30), new ParticleSystem.Burst(0.02f, 14) });
        var ms = mist.shape; ms.enabled = true; ms.shapeType = ParticleSystemShapeType.Cone; ms.angle = 40f; ms.radius = VentR; ms.radiusThickness = 0f; ms.arc = 360f; ms.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread; ms.randomDirectionAmount = 0.15f;   // 一周に均等に
        ms.position = new Vector3(0f, 0f, VentZ); ms.rotation = new Vector3(180f, 0f, 0f);
        // 勢いよく出て、すぐ止まる（漏れた空気は遠くまで飛ばない）
        var ml = mist.limitVelocityOverLifetime; ml.enabled = true; ml.limit = 0.08f; ml.dampen = 0.22f;
        // 出た瞬間は小さく濃く、ふくらみながら薄くなって溶ける
        var mz = mist.sizeOverLifetime; mz.enabled = true; mz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.4f, 0f, 6f), new Keyframe(0.35f, 2f), new Keyframe(1f, 3f)));
        var mc = mist.colorOverLifetime; mc.enabled = true; var mg = new Gradient();
        mg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.08f), new GradientAlphaKey(0.25f, 0.4f), new GradientAlphaKey(0f, 1f) }); mc.color = mg;
        var mr = mist.rotationOverLifetime; mr.enabled = true; mr.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
        var mn = mist.noise; mn.enabled = true; mn.strength = 0.06f; mn.frequency = 25f;   // 流れの向きがそろうと片側に寄るので、細かく弱く mn.scrollSpeed = 1f; mn.damping = true; mn.quality = ParticleSystemNoiseQuality.Low;
        var mrr = mist.GetComponent<ParticleSystemRenderer>(); mrr.renderMode = ParticleSystemRenderMode.Billboard; mrr.sortMode = ParticleSystemSortMode.Distance;

        // 前の作り（泡と筋）が残っていたら消す
        var old = ps.transform.Find("LeakStreaks"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var big = Child(ps.transform, "BigRing"); SetupRing(big, 0.1f, 0.75f, 3.8f, 0.5f, 1);
        var big2 = Child(ps.transform, "BigRing2"); SetupRing(big2, 0.08f, 0.55f, 2.6f, 0.45f, 1);
        var em2 = big2.emission; em2.SetBursts(new[] { new ParticleSystem.Burst(0.06f, 1) });
    }

    // スカート状の膜：根元はすき間の輪（半径 VentR）、後ろへ 6cm 進みながら半径 4.2cm まで開く。uv.x=ぐるり一周、uv.y=根元0→ふち1
    static Mesh SkirtMesh()
    {
        string p = "Assets/FuwaCourse/Meshes/FX_LeakSkirt.asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (mesh == null) { mesh = new Mesh { name = "FX_LeakSkirt" }; AssetDatabase.CreateAsset(mesh, p); }
        int N = 48, R = 6; var vs = new System.Collections.Generic.List<Vector3>(); var uv = new System.Collections.Generic.List<Vector2>(); var ts = new System.Collections.Generic.List<int>();
        for (int j = 0; j <= R; j++)
        {
            float v = (float)j / R; float rr = VentR + (0.042f - VentR) * Mathf.Pow(v, 0.85f); float z = -0.06f * Mathf.Pow(v, 0.9f);
            for (int i = 0; i <= N; i++) { float a = i * Mathf.PI * 2f / N; vs.Add(new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, z)); uv.Add(new Vector2((float)i / N, v)); }
        }
        for (int j = 0; j < R; j++) for (int i = 0; i < N; i++) { int a = j * (N + 1) + i, b = a + 1, c = a + N + 1, d = c + 1; ts.AddRange(new[] { a, c, b, b, c, d }); }
        mesh.Clear(); mesh.SetVertices(vs); mesh.SetUVs(0, uv); mesh.SetTriangles(ts, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // もや用：形のはっきりしない雲。ふちはふわっと消え、中もむらがある（丸い泡に見えないように）
    static Material MistMaterial()
    {
        string tp = "Assets/FuwaCourse/Textures/FX/FX_LeakMist.png";
        int S = 128; var t = new Texture2D(S, S, TextureFormat.RGBA32, false); var px = new Color[S * S];
        System.Func<float, float, float> vnoise = (x, y) =>
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float xf = x - xi, yf = y - yi;
            System.Func<int, int, float> h = (a, b) => Hash(((a % 8 + 8) % 8) * 57 + ((b % 8 + 8) % 8) * 131 + 7);
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            return Mathf.Lerp(Mathf.Lerp(h(xi, yi), h(xi + 1, yi), u), Mathf.Lerp(h(xi, yi + 1), h(xi + 1, yi + 1), u), v);
        };
        for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
        {
            float u = (x + 0.5f) / S, v = (y + 0.5f) / S;
            float n = 0.5f * vnoise(u * 4, v * 4) + 0.3f * vnoise(u * 8, v * 8) + 0.2f * vnoise(u * 16, v * 16);   // 8の周期で継ぎ目なし
            float dx = u * 2 - 1, dy = v * 2 - 1; float r = Mathf.Sqrt(dx * dx + dy * dy) + (n - 0.5f) * 0.55f;   // ふちをノイズでくずす
            float fall = Mathf.Clamp01(1f - r); fall = fall * fall;
            float a = Mathf.Clamp01(fall * Mathf.Lerp(0.45f, 1.25f, n));
            px[y * S + x] = new Color(1, 1, 1, a);
        }
        t.SetPixels(px); t.Apply(); System.IO.File.WriteAllBytes(tp, t.EncodeToPNG()); AssetDatabase.ImportAsset(tp);
        var ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
        string mp = Mat + "FX_LeakMist.mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null) { mat = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended")); AssetDatabase.CreateAsset(mat, mp); }
        mat.shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(tp); mat.SetColor("_TintColor", new Color(0.8f, 0.84f, 0.9f, 0.5f)); EditorUtility.SetDirty(mat);
        return mat;
    }

    static float Hash(int i) { float x = Mathf.Sin(i * 127.1f + 311.7f) * 43758.5453f; return x - Mathf.Floor(x); }

    // 漏れ用のテクスチャとマテリアル
    // sheet=true：4×4コマ。各コマで、細かい筋（一周で継ぎ目なし）の明るい先頭が根元から外へ走り、だんだん薄くなる
    // sheet=false：ジェット1本（先が明るく、後ろへ細く消える）
    static Material LeakMaterial(string name, bool sheet)
    {
        string tp = "Assets/FuwaCourse/Textures/FX/" + name + ".png";
        int W = sheet ? 2048 : 64, H = sheet ? 512 : 64; var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
        var px = new Color[W * H];
        if (sheet)
        {
            int TW = W / 4, TH = H / 4;
            // 膜に見えないよう、一枚の面ではなく、ばらばらの細い筋（18本）にする。
            // 筋ごとに位置・太さ・明るさ・走る速さ・出だしの遅れを変えて、すき間から放射状にシュッと走って消える
            const int NS = 18;
            var sc = new float[NS]; var sw = new float[NS]; var sb = new float[NS]; var ss = new float[NS]; var sd = new float[NS];
            for (int i = 0; i < NS; i++)
            {
                sc[i] = (i + 0.2f + 0.6f * Hash(i * 5 + 1)) / NS; sw[i] = Mathf.Lerp(0.004f, 0.009f, Hash(i * 5 + 2));
                sb[i] = Mathf.Lerp(0.45f, 1f, Hash(i * 5 + 3)); ss[i] = Mathf.Lerp(0.75f, 1.15f, Hash(i * 5 + 4)); sd[i] = 0.18f * Hash(i * 5 + 5);
            }
            for (int f = 0; f < 16; f++)
            {
                int tx = f % 4, ty = 3 - f / 4;   // コマは左上から右へ、下へ
                float tt = f / 15f, fade = Mathf.Lerp(1f, 0.4f, tt);
                for (int y = 0; y < TH; y++)
                {
                    float v = (y + 0.5f) / TH;
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.04f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - v) / 0.7f));
                    for (int x = 0; x < TW; x++)
                    {
                        float u = (x + 0.5f) / TW, a = 0f;
                        for (int i = 0; i < NS; i++)
                        {
                            float du = Mathf.Abs(u - sc[i]); du = Mathf.Min(du, 1f - du);   // 一周つながる
                            float w = sw[i] * (1f + 0.8f * v);   // 外ほど少し太く
                            if (du > w * 3f) continue;
                            float k = Mathf.Max(0f, tt - sd[i]) / (1f - sd[i]); if (k <= 0f) continue;
                            float head = Mathf.Min(1.1f, (0.1f + Mathf.Sqrt(k)) * ss[i]);
                            // 先頭が明るく、根元までは細くつながる。先頭の外はすぐ消える
                            float body = v <= head ? Mathf.Max(Mathf.Exp(-(head - v) / 0.2f), 0.3f) : Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((v - head) / 0.25f));   // 先頭はぼかして、くっきりした縁を作らない
                            a = Mathf.Max(a, Mathf.Exp(-(du * du) / (w * w)) * body * sb[i]);
                        }
                        px[(ty * TH + y) * W + tx * TW + x] = new Color(1, 1, 1, Mathf.Clamp01(a * edge * fade * 1.2f));
                    }
                }
            }
        }
        else
        {
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W, v = (y + 0.5f) / H;
                float w = Mathf.Lerp(0.08f, 0.42f, u);   // 片側が太い
                float a = Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) / w) * Mathf.SmoothStep(0, 1, u / 0.85f) * Mathf.Clamp01((1f - u) / 0.15f);
                px[y * W + x] = new Color(1, 1, 1, a);
            }
        }
        t.SetPixels(px); t.Apply(); System.IO.File.WriteAllBytes(tp, t.EncodeToPNG()); AssetDatabase.ImportAsset(tp);
        var ti = (TextureImporter)AssetImporter.GetAtPath(tp); ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = !sheet; ti.SaveAndReimport();
        string mp = Mat + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null) { mat = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended")); AssetDatabase.CreateAsset(mat, mp); }
        mat.shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(tp); mat.SetColor("_TintColor", new Color(0.78f, 0.82f, 0.88f, 0.5f)); EditorUtility.SetDirty(mat);
        return mat;
    }
}
