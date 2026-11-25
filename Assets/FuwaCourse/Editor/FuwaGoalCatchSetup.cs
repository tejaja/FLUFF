using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 妖精の輪が呼ぶ（2026-11-25 てじゃ「ゴールリングに入れる時、ハズレしたときのイライラ感がすごい」→ 案A）
//  全部のゴール（FuwaGoal）に、ふわふわ（catchBall）をつないで、リングへうずを巻いて吸い込まれる胞子（CatchSpores）を作る
public static class FuwaGoalCatchSetup
{
    [MenuItem("FuwaCourse/ゴール：妖精の輪が呼ぶ（吸い寄せ）をつける")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var ball = Object.FindObjectsOfType<FuwaBall>(true).FirstOrDefault();
        if (ball == null) return "[ゴール吸い寄せ] ふわふわが見つからない";
        int n = 0;
        foreach (var goal in Object.FindObjectsOfType<FuwaGoal>(true))
        {
            Undo.RecordObject(goal, "catch");
            goal.catchBall = ball;
            // 胞子は大きさのかかっていない所に（判定の円柱は縦につぶしてあるので）。ふだんの舞い上がる胞子と同じ親
            Transform parent = goal.ringSpores != null ? goal.ringSpores.transform.parent : goal.transform.parent;
            var old = parent.Find("CatchSpores_" + goal.transform.parent.name);
            if (old == null) old = parent.Find("CatchSpores");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var col = goal.GetComponent<Collider>();
            float groundY = col != null ? col.bounds.min.y : goal.transform.position.y - 0.35f;
            var go = new GameObject("CatchSpores");
            Undo.RegisterCreatedObjectUndo(go, "catch fx");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(goal.transform.position.x, groundY + 0.05f, goal.transform.position.z);
            go.transform.rotation = Quaternion.identity;
            goal.catchFx = MakeFx(go, goal.catchRadius);
            UdonSharpEditorUtility.CopyProxyToUdon(goal);
            EditorUtility.SetDirty(goal);
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "[ゴール吸い寄せ] " + n + " 個のゴールにつけた";
    }

    // リングのまわり（半径 r）から、うずを巻いて中心へ吸い込まれる小さな光の胞子。寄せている間だけ出す（Play/Stop）
    static ParticleSystem MakeFx(GameObject go, float r)
    {
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.0f, 2.6f);   // 2026-11-25 てじゃ「粒子を減らして速度も落とす」
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.34f);   // ぼかした大きめの粒（てじゃ「もっとボカしたもの」）
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.4f, 0.55f), new Color(1f, 0.68f, 0.45f, 0.55f));   // 草の緑・空の青の上でも見える濃いめの金〜桃
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 120;
        main.gravityModifier = 0f;
        var em = ps.emission; em.rateOverTime = 22f;
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = r * 0.92f;
        sh.radiusThickness = 0.15f;   // ふち寄りから
        sh.rotation = new Vector3(90f, 0f, 0f);   // 水平の円
        sh.position = new Vector3(0f, 0.2f, 0f);
        sh.randomPositionAmount = 0f;
        sh.arcMode = ParticleSystemShapeMultiModeValue.Random;
        // 中へ吸い込まれながら回る（ふちから中心までだいたい寿命いっぱいで）
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.radial = new ParticleSystem.MinMaxCurve(-r * 0.36f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(0.7f);
        vel.x = new ParticleSystem.MinMaxCurve(0f); vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.4f); vel.z = new ParticleSystem.MinMaxCurve(0f);
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f); vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
        // ふわっと出て、中心でふっと消える
        var colm = ps.colorOverLifetime; colm.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.9f, 0.75f), new GradientAlphaKey(0f, 1f) });
        colm.color = new ParticleSystem.MinMaxGradient(gr);
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.4f)));
        var tr = ps.trails; tr.enabled = false;   // 2026-11-25 てじゃ「スジ無しで粒子のみ」
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = SoftMat();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    // ふちまでなだらかに消える、ぼかした丸（ガウス）。Default-ParticleSystem よりずっとふんわり
    static Material SoftMat()
    {
        const string texPath = "Assets/FuwaCourse/Textures/GoalCatchSoftDot.png", matPath = "Assets/FuwaCourse/Materials/GoalCatchSpore.mat";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
        {
            System.IO.Directory.CreateDirectory("Assets/FuwaCourse/Textures");
            const int N = 64; var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f, r2 = dx * dx + dy * dy;
                float a = Mathf.Exp(-r2 * 4.5f) * Mathf.Clamp01((1f - r2) * 4f);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            System.IO.File.WriteAllBytes(texPath, t.EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true; imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        }
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        // 光るキノコの胞子と同じ設定（Particles/Standard Unlit）で、模様だけぼかした丸に
        var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/GlowMush_Spore.mat");
        if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, matPath); }
        else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
        m.mainTexture = tex; EditorUtility.SetDirty(m);
        return m;
    }
}
