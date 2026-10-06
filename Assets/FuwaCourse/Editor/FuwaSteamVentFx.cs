using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// いわのみちの上昇気流（FuwaPlayerUpdraft のある穴）を「蒸気が吹き出す」見た目にする。
// 何度実行しても同じ結果になる（各穴の子 SteamFx を作り直す）。古い風の粒（UpdraftStreaks/Puffs/Swirl）は非表示にする。
// コースを作り直して古い粒が復活したら、もう一度このメニューを実行する
public static class FuwaSteamVentFx
{
    const string TexPath = "Assets/FuwaCourse/Textures/SteamPuffSheet.png";
    const string PuffMatPath = "Assets/FuwaCourse/Materials/SteamPuff.mat";
    const string GlowMatPath = "Assets/FuwaCourse/Materials/VentGlow.mat";
    const string HissPath = "Assets/FuwaCourse/Audio/sfx_steam_loop.wav";

    [MenuItem("FuwaCourse/蒸気の穴のエフェクトを作る")]
    public static void Build()
    {
        Texture2D tex = MakeTexture(false);
        Material puff = MakeMat(PuffMatPath, "FuwaCourse/SteamPuff");
        puff.SetTexture("_MainTex", tex);
        puff.SetColor("_ShadeColor", new Color(0.97f, 0.97f, 1f, 1f));   // 白っぽく（影色はほんのり）
        EditorUtility.SetDirty(puff);
        Material glow = MakeMat(GlowMatPath, "FuwaCourse/VentGlow");
        AudioClip hiss = MakeHiss(false);

        int n = 0;
        foreach (var up in Resources.FindObjectsOfTypeAll<FuwaPlayerUpdraft>())
        {
            if (!up.gameObject.scene.IsValid()) continue;
            Transform zone = up.transform;
            Transform root = zone.parent;
            Collider col = zone.GetComponent<Collider>();
            if (root == null || col == null) continue;
            float h = col.bounds.max.y - root.position.y;   // 上昇気流の高さ（穴の口から）
            BuildVent(root, h, puff, glow, hiss, n);
            n++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[FuwaSteamVentFx] vents: " + n);
    }

    static void BuildVent(Transform root, float h, Material puff, Material glow, AudioClip hiss, int index)
    {
        foreach (string old in new[] { "UpdraftStreaks", "UpdraftPuffs", "UpdraftSwirl" })
        {
            Transform t = root.Find(old);
            if (t != null) t.gameObject.SetActive(false);
        }
        Transform prev = root.Find("SteamFx");
        if (prev != null) Object.DestroyImmediate(prev.gameObject);
        var fx = new GameObject("SteamFx").transform;
        fx.SetParent(root, false);

        float k = Mathf.Clamp(h / 3f, 0.7f, 1.3f);           // 穴の強さ（高さ）に合わせた大きさ
        float pulseEvery = 2.4f + (index % 3) * 0.35f;         // 穴ごとに噴くタイミングをずらす

        // ① もくもく：昇りながらふくらんで薄れる
        {
            var ps = NewPS(fx, "Plume", Vector3.zero, puff, 90);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 1.9f);
            float v0 = Mathf.Clamp(h / (2.05f * 0.55f), 1.5f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(v0 * 1.5f, v0 * 1.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f * k, 0.85f * k);
            main.gravityModifier = -0.04f;
            var em = ps.emission;
            em.rateOverTime = 15f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.2f, 7) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 7f; sh.radius = 0.2f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.speedModifier = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.25f, 0.7f), new Keyframe(0.6f, 0.25f), new Keyframe(1f, 0.08f)));
            var noise = ps.noise;
            noise.enabled = true; noise.strength = 0.35f; noise.frequency = 0.5f; noise.scrollSpeed = 0.4f; noise.damping = true;
            SetFade(ps, 0.1f, 0.88f, 0.5f, 0.55f);
            SetGrow(ps, 0.35f, 0.9f, 2.6f);
        }
        // ② 口のところの勢いのいい噴き出し（シュー）
        {
            var ps = NewPS(fx, "Jet", Vector3.zero, puff, 60);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.34f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(9f * k, 13f * k);
            main.startSize = new ParticleSystem.MinMaxCurve(0.26f * k, 0.4f * k);
            var em = ps.emission; em.rateOverTime = 55f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.2f, 22) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 4f; sh.radius = 0.12f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.15f; noise.frequency = 1.2f;
            // 速い噴き出しは縦に伸ばして「シュッ」と勢いよく
            var jr = ps.GetComponent<ParticleSystemRenderer>();
            jr.renderMode = ParticleSystemRenderMode.Stretch; jr.velocityScale = 0.06f; jr.lengthScale = 1.6f;
            var tsa = ps.textureSheetAnimation; tsa.enabled = false;
            var jrot = ps.rotationOverLifetime; jrot.enabled = false;
            var jm = ps.main; jm.startRotation = 0f;
            SetFade(ps, 0.03f, 0.95f, 0.5f, 0.6f);
            SetGrow(ps, 0.6f, 1.2f, 1.9f);
        }
        // ③ てっぺんで横に広がって消える（どこまで上がるかの目印）
        {
            var ps = NewPS(fx, "Top", new Vector3(0f, h * 0.92f, 0f), puff, 24);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f * k, 0.95f * k);
            var em = ps.emission; em.rateOverTime = 5f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.2f + Mathf.Min(1.2f, h * 0.25f), 3) });
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.35f; sh.rotation = new Vector3(90f, 0f, 0f);
            var vol = ps.velocityOverLifetime; vol.enabled = true; vol.space = ParticleSystemSimulationSpace.World;
            vol.x = 0f; vol.y = 0.15f; vol.z = 0f;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.2f; noise.frequency = 0.4f;
            SetFade(ps, 0.15f, 0.5f, 0.6f, 0.28f);
            SetGrow(ps, 0.7f, 1.2f, 2.4f);
        }
        // 噴くリズムがループの長さとそろうように（1ループに1回ブワッ）
        foreach (var p in fx.GetComponentsInChildren<ParticleSystem>(true)) { var m = p.main; m.duration = pulseEvery; }

        // ④ 穴の奥がほんのりオレンジに光る
        {
            var g = new GameObject("Glow");
            g.transform.SetParent(fx, false);
            g.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            g.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            g.transform.localScale = Vector3.one * 1.0f;
            g.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = glow;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        // ⑤ シューという音（3D、近くでだけ聞こえる）
        if (hiss != null)
        {
            var a = new GameObject("Hiss");
            a.transform.SetParent(fx, false);
            a.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            var src = a.AddComponent<AudioSource>();
            src.clip = hiss; src.loop = true; src.playOnAwake = true;
            src.spatialBlend = 1f; src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 0.8f; src.maxDistance = 12f; src.dopplerLevel = 0f;
            src.volume = 0.22f * Mathf.Lerp(0.8f, 1.1f, Mathf.InverseLerp(1f, 5f, h));
            src.pitch = 0.92f + (index % 4) * 0.05f;
        }
    }

    static ParticleSystem NewPS(Transform parent, string name, Vector3 localPos, Material mat, int max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 3f; main.loop = true; main.prewarm = true;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = max;
        var rol = ps.rotationOverLifetime; rol.enabled = true; rol.z = new ParticleSystem.MinMaxCurve(-0.5f, 0.5f);
        var tsa = ps.textureSheetAnimation;
        tsa.enabled = true; tsa.numTilesX = 4; tsa.numTilesY = 4;
        tsa.animation = ParticleSystemAnimationType.WholeSheet;
        tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
        tsa.startFrame = new ParticleSystem.MinMaxCurve(0f, 15.99f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortMode = ParticleSystemSortMode.Distance;
        r.maxParticleSize = 3f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    // 出てすぐ濃く、だんだん薄れて消える
    static void SetFade(ParticleSystem ps, float inT, float peak, float midT, float mid)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, inT), new GradientAlphaKey(mid, midT), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(gr);
    }

    static void SetGrow(ParticleSystem ps, float a, float b, float c)
    {
        var sol = ps.sizeOverLifetime; sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, a), new Keyframe(0.3f, b), new Keyframe(1f, c)));
    }

    static Material MakeMat(string path, string shader)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        else m.shader = Shader.Find(shader);
        return m;
    }

    // ===== もくもくの形（4×4 の16パターン）。赤＝明るさ（上ほど明るい）、アルファ＝濃さ =====
    [MenuItem("FuwaCourse/蒸気のテクスチャを作り直す")]
    static void RemakeTexture() { MakeTexture(true); }

    static Texture2D MakeTexture(bool force)
    {
        if (!force)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
            if (existing != null) return existing;
        }
        const int tile = 128, grid = 4, size = tile * grid;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        var rnd = new System.Random(12345);
        for (int ty = 0; ty < grid; ty++)
        for (int tx = 0; tx < grid; tx++)
        {
            int blobs = 7 + rnd.Next(5);
            var bx = new float[blobs]; var by = new float[blobs]; var br = new float[blobs];
            for (int i = 0; i < blobs; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f, d = (float)rnd.NextDouble() * 0.2f;
                bx[i] = 0.5f + Mathf.Cos(ang) * d; by[i] = 0.48f + Mathf.Sin(ang) * d * 0.85f;
                br[i] = 0.15f + (float)rnd.NextDouble() * 0.12f;
            }
            float seed = (float)rnd.NextDouble() * 100f;
            for (int y = 0; y < tile; y++)
            for (int x = 0; x < tile; x++)
            {
                float u = (x + 0.5f) / tile, v = (y + 0.5f) / tile;
                float d = 0f;
                for (int i = 0; i < blobs; i++)
                {
                    float dx = u - bx[i], dy = v - by[i];
                    float q = 1f - (dx * dx + dy * dy) / (br[i] * br[i]);
                    if (q > 0f) d += q * q;
                }
                float nz = Fbm(u * 6f + seed, v * 6f + seed * 0.7f);
                d *= 0.55f + nz * 0.9f;
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.3f, Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f))));
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.04f, 0.85f, d)) * edge;
                float lit = Mathf.Clamp01(0.42f + (v - 0.35f) * 1.25f + (nz - 0.5f) * 0.35f);
                px[(ty * tile + y) * size + tx * tile + x] = new Color(lit, lit, lit, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(TexPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(TexPath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(TexPath);
        imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.wrapMode = TextureWrapMode.Clamp; imp.sRGBTexture = true;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
    }

    static float Hash(float x, float y)
    {
        float h = Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }
    static float Noise(float x, float y)
    {
        float ix = Mathf.Floor(x), iy = Mathf.Floor(y), fx = x - ix, fy = y - iy;
        float ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
        float a = Hash(ix, iy), b = Hash(ix + 1, iy), c = Hash(ix, iy + 1), d = Hash(ix + 1, iy + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
    }
    static float Fbm(float x, float y)
    {
        float s = 0, amp = 0.5f;
        for (int i = 0; i < 4; i++) { s += Noise(x, y) * amp; x = x * 2.03f + 17.1f; y = y * 2.03f + 9.2f; amp *= 0.5f; }
        return s / 0.9375f;
    }

    // ===== シューという蒸気の音（4秒でぴったりループ） =====
    static AudioClip MakeHiss(bool force)
    {
        if (!force)
        {
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(HissPath);
            if (existing != null) return existing;
        }
        const int rate = 44100; const float sec = 4f;
        int n = (int)(rate * sec);
        var data = new float[n];
        var rnd = new System.Random(777);
        float lp = 0f, lp2 = 0f, hpPrev = 0f, hpOut = 0f;
        // 1周目は捨てて、フィルタが落ち着いてから2周目を使う（つなぎ目でプツッとしないように）
        for (int pass = 0; pass < 2; pass++)
        for (int i = 0; i < n; i++)
        {
            float w = (float)(rnd.NextDouble() * 2 - 1);
            lp += (w - lp) * 0.55f;            // 高すぎるザラザラを少し落とす
            lp2 += (lp - lp2) * 0.75f;
            hpOut = 0.97f * (hpOut + lp2 - hpPrev); hpPrev = lp2;   // 低い音を落としてシューっぽく
            float t = i / (float)rate;
            float swell = 0.78f + 0.14f * Mathf.Sin(2f * Mathf.PI * t / sec * 2f) + 0.08f * Mathf.Sin(2f * Mathf.PI * t / sec * 5f + 1.3f);
            if (pass == 1) data[i] = hpOut * 0.5f * swell;
        }
        // つなぎ目を0.1秒クロスフェード
        int xf = rate / 10;
        for (int i = 0; i < xf; i++) { float a = i / (float)xf; data[i] = data[i] * a + data[n - xf + i] * (1 - a); }
        System.Array.Resize(ref data, n - xf);
        WriteWav(HissPath, data, rate);
        AssetDatabase.ImportAsset(HissPath);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(HissPath);
    }

    static void WriteWav(string path, float[] samples, int rate)
    {
        using (var fs = new FileStream(path, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            int bytes = samples.Length * 2;
            bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + bytes);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); bw.Write(16); bw.Write((short)1); bw.Write((short)1);
            bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
            bw.Write(System.Text.Encoding.ASCII.GetBytes("data")); bw.Write(bytes);
            foreach (float s in samples) bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }
    }
}
