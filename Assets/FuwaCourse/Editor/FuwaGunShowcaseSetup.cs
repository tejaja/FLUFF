using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 台に乗っているエアガンを目立たせる見た目（FuwaGunShowcase）を作る。何度実行しても同じ結果（作り直し）。
// ・見た目だけの銃：本物の銃（FuwaBlower）の Visual を複製して、まん中を回転の中心に
// ・後ろの後光（筋の入ったやわらかい光。ゆっくり回って明滅。テクスチャは FX_GunHalo）と、下から立ちのぼる小さい星のキラキラ
// ・各台の▼を、浮かんだ銃のすぐ上へ
// ・台を指した時の青い枠：VRChatは台の（非表示の）当たり判定用の円柱メッシュで枠を描いていたので、切り株の形のメッシュに差し替える
public static class FuwaGunShowcaseSetup
{
    const string MatDir = "Assets/FuwaCourse/Materials";
    const string MeshDir = "Assets/FuwaCourse/Meshes/GunStand";
    const float ShowScale = 1.4f;   // 台の上の見た目の銃は少し大きめに（遠くからでも形が分かるように）

    [MenuItem("FuwaCourse/エアガンの台の見た目を作る")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new System.Text.StringBuilder();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var blower = Resources.FindObjectsOfTypeAll<FuwaBlower>().First(b => b.gameObject.scene == scene);
        var summon = Resources.FindObjectsOfTypeAll<FuwaGunSummon>().First(b => b.gameObject.scene == scene);
        var sys = summon.transform.parent;

        // ---- 入れ物 ----
        var old = sys.Find("GunShowcase");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var root = new GameObject("GunShowcase"); Undo.RegisterCreatedObjectUndo(root, "showcase");
        root.transform.SetParent(sys, false);
        var content = new GameObject("Content"); content.transform.SetParent(root.transform, false);
        var spin = new GameObject("Spin"); spin.transform.SetParent(content.transform, false);
        spin.transform.localScale = Vector3.one * ShowScale;

        // 見た目の銃：本物の Visual を複製（レンダラー以外は外す）。見た目の中心を回転の中心へ
        var vis = Object.Instantiate(blower.visualRoot, spin.transform);
        vis.name = "Visual"; vis.SetActive(true);
        foreach (var c in vis.GetComponentsInChildren<Component>(true).Where(c => !(c is Transform) && !(c is MeshFilter) && !(c is MeshRenderer)).ToArray())
            Object.DestroyImmediate(c);
        foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (r.name.StartsWith("GunGauge")) r.enabled = false;   // 空気ゲージは持った時だけの表示なので出さない
        }
        vis.transform.localPosition = Vector3.zero; vis.transform.localRotation = Quaternion.identity; vis.transform.localScale = Vector3.one;
        var rs = vis.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
        vis.transform.localPosition = -vis.transform.InverseTransformPoint(bb.center);
        log.Append("gun size " + bb.size.ToString("F2") + " ");

        // ---- 後ろのやわらかい光 ----
        var haloMat = Mat("GunShowcase_Halo", "Legacy Shaders/Particles/Additive", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FuwaCourse/Textures/FX_GunHalo.png"), new Color(0.62f, 0.54f, 0.34f, 0.5f));   // 足し算で光らせる（半透明の重ね塗りだと砂の上で茶色くにごる）
        var halo = NewPs(content.transform, "Halo", haloMat);
        {
            var main = halo.main; main.loop = true; main.startLifetime = 2.4f; main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); main.startSpeed = 0f; main.startSize = 1.15f;
            main.startColor = new Color(1f, 0.86f, 0.35f, 1f); main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 4; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.prewarm = true;
            var em = halo.emission; em.rateOverTime = 1.25f;
            var sh = halo.shape; sh.enabled = false;
            var col = halo.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = halo.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.85f, 1f, 1.15f));
            var rot = halo.rotationOverLifetime; rot.enabled = true; rot.z = 0.35f;   // 後光の筋がゆっくり回る
            var pr = halo.GetComponent<ParticleSystemRenderer>(); pr.sortingFudge = 10f;   // 銃より奥に描く
        }

        // ---- 下から立ちのぼるキラキラ ----
        var starMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/FX_Star.mat");
        var spark = NewPs(content.transform, "Sparkles", starMat);
        {
            spark.transform.localPosition = Vector3.down * 0.2f;
            var main = spark.main; main.loop = true; main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.6f); main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f); main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.5f, 1f), new Color(1f, 1f, 0.9f, 1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 40; main.scalingMode = ParticleSystemScalingMode.Hierarchy; main.prewarm = true;
            var em = spark.emission; em.rateOverTime = 14f;
            var sh = spark.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.2f; sh.rotation = new Vector3(90f, 0f, 0f);
            var vel = spark.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f); vel.y = new ParticleSystem.MinMaxCurve(0.18f, 0.32f); vel.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
            var col = spark.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var sz = spark.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.4f));
            var rot = spark.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
        }

        // ---- スクリプト ----
        var sc = UdonSharpUndo.AddComponent<FuwaGunShowcase>(root);
        sc.manager = summon.manager; sc.content = content; sc.spin = spin.transform;
        UdonSharpEditorUtility.CopyProxyToUdon(sc);
        content.SetActive(false);

        // ---- 各台：▼を浮かんだ銃のすぐ上へ、指した時の枠を切り株の形に ----
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Meshes", "GunStand");
        foreach (var st in Resources.FindObjectsOfTypeAll<FuwaGunStand>().Where(s => s.gameObject.scene == scene))
        {
            if (st.marker != null && st.gunSpot != null)
            {
                var arrow = st.marker.transform.Find("Arrow");
                if (arrow != null) { Undo.RecordObject(arrow, "arrow"); arrow.position = st.gunSpot.position + Vector3.up * (blower.dockHover + 0.5f); }
            }
            var stump = st.transform.Find("StumpVisual");
            var mf = st.GetComponent<MeshFilter>();
            if (stump != null && mf != null)
            {
                var src = stump.GetComponent<MeshFilter>().sharedMesh;
                var m2l = st.transform.worldToLocalMatrix * stump.localToWorldMatrix;
                var m = new Mesh { name = "GunStandHighlight_" + st.name };
                m.vertices = src.vertices.Select(v => m2l.MultiplyPoint3x4(v)).ToArray();
                m.normals = src.normals.Select(n => m2l.MultiplyVector(n).normalized).ToArray();
                m.subMeshCount = 1;
                m.SetTriangles(Enumerable.Range(0, src.subMeshCount).SelectMany(i => src.GetTriangles(i)).ToArray(), 0);
                m.RecalculateBounds();
                string ap = MeshDir + "/" + m.name + ".asset";
                if (AssetDatabase.LoadAssetAtPath<Mesh>(ap) != null) AssetDatabase.DeleteAsset(ap);
                AssetDatabase.CreateAsset(m, ap);
                Undo.RecordObject(mf, "highlight"); mf.sharedMesh = m;   // レンダラーは非表示のまま（描画は StumpVisual）。枠の形にだけ使われる
            }
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        return log.ToString();
    }

    static Material Mat(string name, string shader, Texture2D tex, Color tint)
    {
        string p = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, p); }
        m.shader = Shader.Find(shader);
        if (tex != null) m.mainTexture = tex;
        m.SetColor("_TintColor", tint);
        if (m.HasProperty("_InvFade")) m.SetFloat("_InvFade", 3f);   // ソフトパーティクルで手前の物の近くが消えないように
        EditorUtility.SetDirty(m);
        return m;
    }

    static ParticleSystem NewPs(Transform parent, string name, Material mat)
    {
        var go = new GameObject(name, typeof(ParticleSystem)); go.transform.SetParent(parent, false);
        var ps = go.GetComponent<ParticleSystem>();
        var pr = go.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = mat; pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
        return ps;
    }
}
