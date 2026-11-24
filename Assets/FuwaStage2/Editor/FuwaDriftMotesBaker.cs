using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 漂う粒のパーティクルシステムを、シェーダーで動く板のメッシュ（FuwaCourse/DriftMotes）に置きかえる。2026-11-24 てじゃ「両方のコースの漂ってるやつ、専用Shader組めないかな？」
//  対象：2–3 DiamondDust/Dust0〜6、2–2 Fire/Embers/Holes・Zig・Volcano（ずっと漂っているだけの物）。
//  元のパーティクルの設定（箱の範囲・出る量×寿命＝いつも出ている数・寿命・大きさ・向き・回転・速度・重力・ノイズ・色と大きさの変化）を読んで同じに作る。
//  元のパーティクルは消さずに止めておく（GameObject を非表示。メニューで戻せる）。新しい物は元と同じ親に "<名前>_Motes"、元を参照していた所（CourseLodStage2 の lobbyHideObjects）も付けかえる
public static class FuwaDriftMotesBaker
{
    const string Dir = "Assets/FuwaStage2/Generated/Motes";
    static readonly (string root, string path)[] Targets = {
        ("FuwaCourse2_3", "DiamondDust/Dust0"), ("FuwaCourse2_3", "DiamondDust/Dust1"), ("FuwaCourse2_3", "DiamondDust/Dust2"), ("FuwaCourse2_3", "DiamondDust/Dust3"),
        ("FuwaCourse2_3", "DiamondDust/Dust4"), ("FuwaCourse2_3", "DiamondDust/Dust5"), ("FuwaCourse2_3", "DiamondDust/Dust6"),
        ("FuwaCourse2_2", "Fire/Embers/Holes"), ("FuwaCourse2_2", "Fire/Embers/Zig"), ("FuwaCourse2_2", "Fire/Embers/Volcano"),
        // 2026-11-24 てじゃ「そういやあそこもか、やろう」：ステージ1 やみのもりのホタル（コースの中・空のエリア）
        ("FuwaCourse3", "Fireflies"), ("SkyZones", "Yami/Fireflies"),
    };

    [MenuItem("FuwaCourse/ステージ2/漂う粒をシェーダー版にする（2–2 火の粉・2–3 ダイヤモンドダスト・やみのもりのホタル）")]
    public static void MenuBake() { Debug.Log(BakeAll()); }
    [MenuItem("FuwaCourse/ステージ2/漂う粒をパーティクル版に戻す")]
    public static void MenuRevert() { Debug.Log(Switch(false)); }

    static Transform Find(string root, string path) { var r = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == root); return r ? r.transform.Find(path) : null; }

    public static string BakeAll()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaStage2/Generated", "Motes");
        var sb = new System.Text.StringBuilder();
        foreach (var (rn, path) in Targets)
        {
            var t = Find(rn, path); if (!t) { sb.Append("ない " + rn + "/" + path + "\n"); continue; }
            sb.Append(Bake(t.GetComponent<ParticleSystem>(), rn + "_" + t.name) + "\n");
        }
        sb.Append(Switch(true));
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return sb.ToString();
    }

    // 有効：シェーダー版を出して元のパーティクルを隠す／無効：その逆
    public static string Switch(bool motes)
    {
        int n = 0;
        foreach (var (rn, path) in Targets)
        {
            var t = Find(rn, path); if (!t) continue; var m = t.parent.Find(t.name + "_Motes"); if (!m) continue;
            t.gameObject.SetActive(!motes); m.gameObject.SetActive(motes); n++;
            // 元を参照している所を付けかえ（ロビーで隠す一覧など）
            Relink(motes ? t.gameObject : m.gameObject, motes ? m.gameObject : t.gameObject);
        }
        return (motes ? "シェーダー版に " : "パーティクル版に ") + n + " 個";
    }

    static void Relink(GameObject from, GameObject to)
    {
        foreach (var mb in Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            if (mb == null || mb.GetType().Name == "UdonBehaviour") continue;
            var so = new SerializedObject(mb); var it = so.GetIterator(); bool ch = false;
            while (it.NextVisible(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == from) { it.objectReferenceValue = to; ch = true; }
            if (ch)
            {
                so.ApplyModifiedProperties();
                if (mb is UdonSharp.UdonSharpBehaviour usb) UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(usb);
            }
        }
    }

    static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
    static float Rand(System.Random r, ParticleSystem.MinMaxCurve c, float t = 0f)
    {
        switch (c.mode)
        {
            case ParticleSystemCurveMode.Constant: return c.constant;
            case ParticleSystemCurveMode.TwoConstants: return Rand(r, c.constantMin, c.constantMax);
            default: return c.Evaluate(t, (float)r.NextDouble());
        }
    }

    // ノイズで動く量：パーティクルを実際に動かして、1粒あたりの速さの平均から sin のゆれ幅を決める（ゆれの速さ＝幅×角速度）
    static float NoiseAmplitude(ParticleSystem ps)
    {
        // 自分と親で隠れている物を一時的に出す（隠れていると動かせない）
        var off = new List<GameObject>(); for (var tt = ps.transform; tt; tt = tt.parent) if (!tt.gameObject.activeSelf) { off.Add(tt.gameObject); tt.gameObject.SetActive(true); }
        var vel = ps.velocityOverLifetime; float vAvg = 0f;
        if (vel.enabled) vAvg = new Vector3((Mathf.Abs(vel.x.constantMin) + Mathf.Abs(vel.x.constantMax)) * 0.5f, (Mathf.Abs(vel.y.constantMin) + Mathf.Abs(vel.y.constantMax)) * 0.5f, (Mathf.Abs(vel.z.constantMin) + Mathf.Abs(vel.z.constantMax)) * 0.5f).magnitude;
        ps.Simulate(6f, true, true);
        var a = new ParticleSystem.Particle[ps.particleCount]; ps.GetParticles(a);
        var p0 = new Dictionary<uint, Vector3>(); foreach (var q in a) p0[q.randomSeed] = q.position;
        const float dt = 0.5f; ps.Simulate(dt, true, false);
        var b = new ParticleSystem.Particle[ps.particleCount]; ps.GetParticles(b);
        float sum = 0f; int n = 0; foreach (var q in b) if (p0.TryGetValue(q.randomSeed, out var o)) { sum += (q.position - o).magnitude / dt; n++; }
        ps.Clear(true); foreach (var g in off) g.SetActive(false);
        float s = n > 0 ? sum / n : 0f;
        float sNoise = Mathf.Sqrt(Mathf.Max(0f, s * s - vAvg * vAvg));
        float w = ps.noise.frequency * 6.2831853f * 0.8f;   // シェーダーの3つの sin の平均の角速度
        return w > 1e-4f ? sNoise / w : 0f;
    }

    public static string Bake(ParticleSystem ps, string assetName)
    {
        var t = ps.transform; var main = ps.main; var sh = ps.shape; var vel = ps.velocityOverLifetime; var noise = ps.noise;
        var rnd = new System.Random(assetName.GetHashCode());
        float lifeMin = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants ? main.startLifetime.constantMin : main.startLifetime.constant;
        float lifeMax = main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants ? main.startLifetime.constantMax : main.startLifetime.constant;
        float rate = ps.emission.rateOverTime.constantMax;
        int count = Mathf.Clamp(Mathf.RoundToInt(rate * (lifeMin + lifeMax) * 0.5f), 1, main.maxParticles);   // いつも出ている数
        var shapeM = Matrix4x4.TRS(sh.position, Quaternion.Euler(sh.rotation), sh.scale);
        // 家の置き場所（箱の中）。メッシュは粒システムの位置を原点・ワールドの向きで持つ（向きは回さない）
        var holder = t.parent.Find(t.name + "_Motes");
        if (!holder) { holder = new GameObject(t.name + "_Motes").transform; holder.SetParent(t.parent, false); }
        holder.position = t.position; holder.rotation = Quaternion.identity; holder.localScale = Vector3.one;
        holder.gameObject.isStatic = false; GameObjectUtility.SetStaticEditorFlags(holder.gameObject, 0);
        holder.gameObject.layer = t.gameObject.layer;
        var v = new List<Vector3>(); var uv0 = new List<Vector2>(); var uv1 = new List<Vector2>(); var uv2 = new List<Vector3>(); var uv3 = new List<Vector4>(); var col = new List<Color>(); var tri = new List<int>();
        var b = new Bounds(); bool fb = false; float maxSpeed = 0f;
        bool velWorld = vel.space == ParticleSystemSimulationSpace.World;
        for (int i = 0; i < count; i++)
        {
            Vector3 lp;
            if (sh.shapeType == ParticleSystemShapeType.Box) lp = shapeM.MultiplyPoint3x4(new Vector3(Rand(rnd, -0.5f, 0.5f), Rand(rnd, -0.5f, 0.5f), Rand(rnd, -0.5f, 0.5f)));
            else lp = shapeM.MultiplyPoint3x4(Random.insideUnitSphere * sh.radius);
            var wp = t.TransformPoint(lp); var hp = holder.InverseTransformPoint(wp);
            float life = Rand(rnd, lifeMin, lifeMax), size = Rand(rnd, main.startSize), rot0 = Rand(rnd, main.startRotation);
            float rotSpd = ps.rotationOverLifetime.enabled ? Rand(rnd, ps.rotationOverLifetime.z) : 0f;
            var vv = Vector3.zero;
            if (vel.enabled) { vv = new Vector3(Rand(rnd, vel.x), Rand(rnd, vel.y), Rand(rnd, vel.z)); if (!velWorld) vv = t.TransformDirection(vv); }
            maxSpeed = Mathf.Max(maxSpeed, vv.magnitude * lifeMax);
            Color c0 = main.startColor.mode == ParticleSystemGradientMode.TwoColors ? Color.Lerp(main.startColor.colorMin, main.startColor.colorMax, (float)rnd.NextDouble()) : main.startColor.color;
            var cl = c0.linear; cl.a = c0.a;
            float seed = (float)rnd.NextDouble(), phase = (float)rnd.NextDouble();
            int k = v.Count;
            foreach (var cr in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
            { v.Add(hp); uv0.Add(cr); uv1.Add(new Vector2(seed, rotSpd)); uv2.Add(vv); uv3.Add(new Vector4(life, phase, size, rot0)); col.Add(cl); }
            tri.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
            if (!fb) { b = new Bounds(hp, Vector3.zero); fb = true; } else b.Encapsulate(hp);
        }
        float jitter = 1.5f, nstr = noise.enabled ? NoiseAmplitude(ps) : 0f;
        b.Expand(2f * (jitter + maxSpeed + nstr + 1f) + Mathf.Abs(main.gravityModifier.constantMax) * 9.81f * lifeMax * lifeMax);
        var meshPath = Dir + "/" + assetName + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh) mesh.Clear(false); else { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, meshPath); }
        mesh.name = assetName; if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v); mesh.SetUVs(0, uv0); mesh.SetUVs(1, uv1); mesh.SetUVs(2, uv2); mesh.SetUVs(3, uv3); mesh.SetColors(col); mesh.SetTriangles(tri, 0);
        mesh.bounds = b; EditorUtility.SetDirty(mesh);
        // 寿命ごとの色・濃さ・大きさ（64×2、リニアで入れる）
        var tex = new Texture2D(64, 2, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = assetName + "_Ramp" };
        var col0 = ps.colorOverLifetime.enabled ? ps.colorOverLifetime.color.gradient : null;
        var sz = ps.sizeOverLifetime.enabled ? ps.sizeOverLifetime.size : new ParticleSystem.MinMaxCurve(1f);
        for (int x = 0; x < 64; x++)
        {
            float a = x / 63f; var c = col0 != null ? col0.Evaluate(a) : Color.white; var cl = c.linear; cl.a = c.a;
            tex.SetPixel(x, 0, cl);
            float s = sz.mode == ParticleSystemCurveMode.Constant ? sz.constant : sz.Evaluate(a, 0.5f);
            tex.SetPixel(x, 1, new Color(Mathf.Clamp01(s * 0.5f), 0, 0, 1));
        }
        tex.Apply(false, false);
        var texPath = Dir + "/" + assetName + "_Ramp.png";
        System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(texPath); ti.sRGBTexture = false; ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Clamp; ti.filterMode = FilterMode.Bilinear; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.npotScale = TextureImporterNPOTScale.None;
        ti.SaveAndReimport();
        // 材質
        var srcMat = ps.GetComponent<ParticleSystemRenderer>().sharedMaterial;
        var matPath = Dir + "/" + assetName + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (!mat) { mat = new Material(Shader.Find("FuwaCourse/DriftMotes")); AssetDatabase.CreateAsset(mat, matPath); }
        mat.shader = Shader.Find("FuwaCourse/DriftMotes");
        mat.SetTexture("_MainTex", srcMat.mainTexture); mat.SetTexture("_Ramp", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        // 混ぜ方：材質に _SrcBlend/_DstBlend があればそれ（Particles/Standard Unlit の加算モードなど）、なければ名前で
        bool additive = srcMat.HasProperty("_DstBlend") ? Mathf.RoundToInt(srcMat.GetFloat("_DstBlend")) == (int)UnityEngine.Rendering.BlendMode.One : srcMat.shader.name.Contains("Additive");
        Color tint = Color.white;
        if (srcMat.HasProperty("_TintColor")) tint = srcMat.GetColor("_TintColor") * 2f; else if (srcMat.HasProperty("_Color")) tint = srcMat.GetColor("_Color");
        mat.SetColor("_Tint", tint);
        mat.SetFloat("_SrcBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_Additive", additive ? 1f : 0f);
        mat.SetFloat("_Jitter", jitter);
        mat.SetFloat("_Gravity", main.gravityModifier.constantMax);
        mat.SetFloat("_NoiseStr", nstr); mat.SetFloat("_NoiseFreq", noise.enabled ? noise.frequency : 0.3f);
        mat.SetFloat("_MaxScreen", ps.GetComponent<ParticleSystemRenderer>().maxParticleSize);
        mat.renderQueue = srcMat.renderQueue;
        EditorUtility.SetDirty(mat);
        var mf = holder.GetComponent<MeshFilter>(); if (!mf) mf = holder.gameObject.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
        var mr = holder.GetComponent<MeshRenderer>(); if (!mr) mr = holder.gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return assetName + "：粒 " + count + "（元は rate " + rate.ToString("F1") + "×寿命 " + lifeMin + "〜" + lifeMax + "）" + (additive ? " 加算" : " 半透明");
    }
}
