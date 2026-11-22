using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 2-3（こおりのコース）のダイヤモンドダスト（2026-11-22 てじゃ「ほのおのやまの火の粉みたいな演出がほしい、ダイヤモンドダストみたいなキラキラを浮かせたい」）。
// 2-2 の火の粉（FuwaCourse2_2Builder.CourseEmbers）と同じ作り：コースの上に箱で粒をまく。光の粒だけなので当たりなし。
//  ・十字の光（Reward_Glint）を加算で。白〜うすい水色。ゆっくり下へ漂いながら横にゆれる
//  ・ときどきキラッ：大きさと明るさを寿命の中で数回ふくらませる（粒ごとに寿命がちがうので瞬くタイミングはばらばら）
//  ・箱は道（PathBevel）にそって X 方向に区切って、道の上をおおう
public static class FuwaIceDiamondDust
{
    public static float BinX = 28f, Pad = 6f, Height = 7f, Lift = -1f;   // 箱：区切りの幅、道の外へのはみ出し、高さ、道の高さからの底
    public static float RatePerM2 = 0.03f, SizeMin = 0.22f, SizeMax = 0.5f, LifeMin = 5f, LifeMax = 9f;
    public static Color ColA = new Color(1f, 1f, 1f), ColB = new Color(0.7f, 0.9f, 1f);

    [MenuItem("FuwaCourse/ステージ2/2-3 ダイヤモンドダストを作り直す")]
    static void Menu() { Debug.Log(Run()); }

    public static Material Mat()
    {
        string p = "Assets/FuwaStage2/Materials/Stage2_3_DiamondDust.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Mobile/Particles/Additive")); AssetDatabase.CreateAsset(m, p); }
        m.shader = Shader.Find("Mobile/Particles/Additive");
        m.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaStage2/Textures/Reward_Glint.png");
        EditorUtility.SetDirty(m); return m;
    }

    public static string Run()
    {
        var root = GameObject.Find("FuwaCourse2_3").transform;
        var old = root.Find("DiamondDust"); if (old) Undo.DestroyObjectImmediate(old.gameObject);
        var g = new GameObject("DiamondDust").transform; g.SetParent(root, false); Undo.RegisterCreatedObjectUndo(g.gameObject, "dust");
        var mat = Mat();
        // 道の頂点を X で区切って、区切りごとに道をおおう箱
        var pf = root.Find("PathBevel").GetComponent<MeshFilter>(); var M = pf.transform.localToWorldMatrix;
        // 頂点だけだとまっすぐな所は点がまばら → 三角形の辺を 2m おきに区切った点も使う
        var mv = pf.sharedMesh.vertices; var tri = pf.sharedMesh.triangles; var ws = new List<Vector3>();
        for (int i = 0; i < tri.Length; i += 3)
            for (int e = 0; e < 3; e++)
            {
                var a = M.MultiplyPoint3x4(mv[tri[i + e]]); var bb = M.MultiplyPoint3x4(mv[tri[i + (e + 1) % 3]]);
                int n = Mathf.Max(1, Mathf.CeilToInt((bb - a).magnitude / 2f));
                for (int k = 0; k < n; k++) ws.Add(Vector3.Lerp(a, bb, k / (float)n));
            }
        // コースの大半は氷の島（Path の下の島・動く床など）の上 → 島の上面も 3m おきの点で足す
        var isl = root.Find("Path/Islands");
        var pathRoot = root.Find("Path");
        if (pathRoot) foreach (var r in pathRoot.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.bounds.size.x * r.bounds.size.z > 30f && r.bounds.size.y < 6f))
            {
                var bb = r.bounds;
                for (float x = bb.min.x; x <= bb.max.x; x += 3f) for (float z = bb.min.z; z <= bb.max.z; z += 3f) ws.Add(new Vector3(x, bb.max.y, z));
            }
        float x0 = ws.Min(p => p.x), x1 = ws.Max(p => p.x);
        var log = new List<string>(); int total = 0;
        for (float bx = x0; bx < x1; bx += BinX)
        {
            var pts = ws.Where(p => p.x >= bx && p.x < bx + BinX).ToList(); if (pts.Count < 3) continue;
            var b = new Bounds(pts[0], Vector3.zero); foreach (var p in pts) b.Encapsulate(p);
            b.Expand(new Vector3(Pad * 2f, 0f, Pad * 2f));
            var c = new Vector3(b.center.x, b.max.y + Lift + Height / 2f, b.center.z);
            var size = new Vector3(b.size.x, Height, b.size.z);
            float rate = Mathf.Max(4f, size.x * size.z * RatePerM2);
            Add(g, "Dust" + log.Count, root.InverseTransformPoint(c), size, rate, mat);
            total += Mathf.RoundToInt(rate * (LifeMin + LifeMax) / 2f);
            log.Add($"{c.x:F0},{c.z:F0} {size.x:F0}x{size.z:F0} rate {rate:F0}");
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        return $"[ダイヤモンドダスト] 箱 {log.Count}・粒 約{total}\n" + string.Join("\n", log);
    }

    static void Add(Transform g, string name, Vector3 lp, Vector3 size, float rate, Material mat)
    {
        var go = new GameObject(name); go.transform.SetParent(g, false); go.transform.localPosition = lp;
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main;
        m.startLifetime = new ParticleSystem.MinMaxCurve(LifeMin, LifeMax); m.startSpeed = 0f;
        m.startSize = new ParticleSystem.MinMaxCurve(SizeMin, SizeMax);
        m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        m.startColor = new ParticleSystem.MinMaxGradient(ColA, ColB);
        m.gravityModifier = 0.004f; m.simulationSpace = ParticleSystemSimulationSpace.World;
        m.maxParticles = Mathf.CeilToInt(rate * LifeMax * 1.2f); m.prewarm = true; m.loop = true;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = size;
        // ふわっと出て、寿命の中で 3 回キラッと
        var col = ps.colorOverLifetime; col.enabled = true; var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.1f), new GradientAlphaKey(1f, 0.22f), new GradientAlphaKey(0.35f, 0.34f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0.35f, 0.64f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        col.color = gr;
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        var cv = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.22f, 1.25f), new Keyframe(0.34f, 0.6f), new Keyframe(0.5f, 1.25f), new Keyframe(0.64f, 0.6f), new Keyframe(0.8f, 1.25f), new Keyframe(1f, 0.4f));
        sz.size = new ParticleSystem.MinMaxCurve(1f, cv);
        var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        // ゆっくり漂う（横にゆれて、少しずつ下へ）
        var vl = ps.velocityOverLifetime; vl.enabled = true; vl.space = ParticleSystemSimulationSpace.World;
        vl.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f); vl.y = new ParticleSystem.MinMaxCurve(-0.18f, 0.02f); vl.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        var nz = ps.noise; nz.enabled = true; nz.strength = 0.25f; nz.frequency = 0.35f; nz.scrollSpeed = 0.15f; nz.quality = ParticleSystemNoiseQuality.Low;
        var pr = go.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = mat; pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false; pr.maxParticleSize = 0.05f;
        ps.Play();
    }
}
