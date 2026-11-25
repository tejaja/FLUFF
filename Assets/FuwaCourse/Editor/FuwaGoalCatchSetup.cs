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
        // 2026-11-25 てじゃ「チープ感」→ バラまきをやめて、3本の腕で中心へ巻き込む渦に。粒は芯＋薄いぼかしの1枚絵、色と大きさにばらつき、ちらっと瞬く
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 2.7f);   // ふちから中心までちょうど（そろえると腕がきれいに出る）
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);   // 芯は小さく、まわりのぼかしは広く
        var cg = new Gradient();
        cg.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.86f, 0.42f), 0f), new GradientColorKey(new Color(1f, 0.97f, 0.85f), 0.45f), new GradientColorKey(new Color(1f, 0.7f, 0.55f), 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        var sc = new ParticleSystem.MinMaxGradient(cg); sc.mode = ParticleSystemGradientMode.RandomColor;   // 金・白っぽい金・桃からランダム
        main.startColor = sc;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 120;
        main.gravityModifier = 0f;
        var em = ps.emission; em.rateOverTime = 24f;   // 腕1本あたり8個/秒
        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Circle;
        sh.radius = r * 0.75f;   // キノコの輪のすぐ外から
        sh.radiusThickness = 0f;   // ふちの上から
        sh.rotation = new Vector3(90f, 0f, 0f);   // 水平の円
        sh.position = new Vector3(0f, 0.15f, 0f);
        sh.randomPositionAmount = 0.12f;   // 腕に少し幅
        sh.arcMode = ParticleSystemShapeMultiModeValue.Random;
        sh.arcSpread = 1f / 3f;   // 0°・120°・240°の3か所からだけ出す → 3本の腕
        // 中へ吸い込まれながら回る（ふちから中心までだいたい寿命いっぱいで）
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.radial = new ParticleSystem.MinMaxCurve(-r * 0.29f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(0.9f);
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.y = new ParticleSystem.MinMaxCurve(0.03f, 0.12f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);   // x・y・zは同じ形式でないと警告
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f); vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f);
        // ふわっと出て、中心でふっと消える
        var colm = ps.colorOverLifetime; colm.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.9f, 0.75f), new GradientAlphaKey(0f, 1f) });
        colm.color = new ParticleSystem.MinMaxGradient(gr);
        var sz = ps.sizeOverLifetime; sz.enabled = true;
        // ちらっと瞬く：粒ごとにバラバラ（てじゃ「チカチカが揃ってる？」→ ノイズは近い粒が同じにゆれるのでやめた）
        //  2本のゆらぎ曲線（速さも位相もちがう）の間を粒ごとにランダムで取る＋粒ごとに年齢がちがう → そろわない
        sz.size = new ParticleSystem.MinMaxCurve(1f, TwinkleCurve(3.7f, 0f), TwinkleCurve(5.3f, 0.5f));
        var nz = ps.noise; nz.enabled = false;
        var tr = ps.trails; tr.enabled = false;   // 2026-11-25 てじゃ「スジ無しで粒子のみ」
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = SoftMat();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        return ps;
    }

    // 寿命の間の大きさ：ふわっと出て中心で小さく ×（cycles 回のゆらぎ、phase は 0〜1）
    static AnimationCurve TwinkleCurve(float cycles, float phase)
    {
        var c = new AnimationCurve();
        const int N = 40;
        for (int i = 0; i <= N; i++)
        {
            float t = i / (float)N;
            float env = t < 0.3f ? Mathf.Lerp(0.6f, 1f, t / 0.3f) : Mathf.Lerp(1f, 0.4f, (t - 0.3f) / 0.7f);
            float w = 0.5f + 0.5f * Mathf.Sin((t * cycles + phase) * Mathf.PI * 2f);
            c.AddKey(t, env * Mathf.Lerp(0.55f, 1.1f, w * w));   // 2乗で、明るい瞬間を短く
        }
        for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
        return c;
    }

    // ふちまでなだらかに消える、ぼかした丸（ガウス）。Default-ParticleSystem よりずっとふんわり
    static Material SoftMat()
    {
        const string texPath = "Assets/FuwaCourse/Textures/GoalCatchGlowDot.png", matPath = "Assets/FuwaCourse/Materials/GoalCatchSpore.mat";
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
        {
            System.IO.Directory.CreateDirectory("Assets/FuwaCourse/Textures");
            const int N = 64; var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f, r2 = dx * dx + dy * dy;
                // 小さく明るい芯 ＋ 大きく薄いぼかし
                float a = Mathf.Min(1f, Mathf.Exp(-r2 * 45f) + 0.4f * Mathf.Exp(-r2 * 6f)) * Mathf.Clamp01((1f - r2) * 4f);
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
