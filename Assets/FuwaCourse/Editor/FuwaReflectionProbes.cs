using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 反射プローブを置いて撮る（2026-10-10 てじゃ「反射入れてるアバターの見た目のため」）。
// Hikari の焼きは反射を作らないので、何もしないと反射は真っ黒（UnityBlackCube）になっていた。
// プローブごとに Lightmapping.BakeReflectionProbe で撮って EXR に書き出し、Custom モードでその画像を使う（ライトの焼きとは別。何度撮り直してもよい）。
// プローブの外で使う「空の反射」も、空高くで撮った画像を Lighting の Environment Reflections（Custom）に入れる。
// やみのもりは、中にいる時と同じ霧と暗い空（_UdonSkyDim）にしてから撮る。
public static class FuwaReflectionProbes
{
    const string Dir = "Assets/FuwaCourse/Reflection";
    const string RootName = "ReflectionProbes";

    struct Spot
    {
        public string name; public Vector3 center, size; public int importance; public bool yami;
        public Spot(string n, Vector3 c, Vector3 s, int imp = 1, bool y = false) { name = n; center = c; size = s; importance = imp; yami = y; }
    }

    static readonly Spot[] Spots =
    {
        new Spot("Lobby",       new Vector3(13, 10, -16),  new Vector3(32, 20, 32), 2),
        new Spot("Hub",         new Vector3(0, 160, -45),  new Vector3(44, 24, 44), 2),
        new Spot("VersusLobby", new Vector3(-80, 90, 22),  new Vector3(44, 24, 44), 2),
        new Spot("Course0",     new Vector3(-31, 6, 65),   new Vector3(100, 30, 140)),
        new Spot("Course1",     new Vector3(42, 8, 71),    new Vector3(105, 30, 140)),
        new Spot("Course2",     new Vector3(96, 10, 9),    new Vector3(126, 30, 66)),
        new Spot("Course3",     new Vector3(62, 10, -78),  new Vector3(90, 50, 110), 1, true),
        new Spot("Course4",     new Vector3(5, 6, -81),    new Vector3(40, 25, 90)),
        new Spot("Course5",     new Vector3(-49, 16, -66), new Vector3(98, 55, 100)),
    };
    static readonly Vector3 SkyPoint = new Vector3(0, 300, 0);

    [MenuItem("Tools/FuwaCourse/Reflection Probes (Place + Capture)")]
    public static void Run()
    {
        Directory.CreateDirectory(Dir);
        var root = GameObject.Find(RootName);
        if (root == null) { root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, "probes"); }

        foreach (var s in Spots)
        {
            var t = root.transform.Find(s.name);
            if (t == null) { t = new GameObject(s.name).transform; t.SetParent(root.transform, false); }
            t.position = s.center;
            var p = t.GetComponent<ReflectionProbe>();
            if (p == null) p = t.gameObject.AddComponent<ReflectionProbe>();   // ?? は Unity の「消えた物」を null と見なさないので使わない
            p.mode = ReflectionProbeMode.Custom;
            p.size = s.size;
            p.importance = s.importance;
            p.blendDistance = 4f;
            p.boxProjection = false;
            p.resolution = 128;
            p.hdr = true;
            p.nearClipPlane = 0.3f;
            p.farClipPlane = 2000f;
            p.clearFlags = ReflectionProbeClearFlags.Skybox;
            Capture(p, s.name, s.yami);
        }

        // 空の反射（プローブの外）
        var sky = new GameObject("_SkyCapture").AddComponent<ReflectionProbe>();
        sky.transform.position = SkyPoint;
        sky.mode = ReflectionProbeMode.Custom; sky.resolution = 128; sky.hdr = true; sky.farClipPlane = 2000f;
        var skyTex = Capture(sky, "Sky", false);
        Object.DestroyImmediate(sky.gameObject);
        if (skyTex != null)
        {
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = skyTex;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[FuwaReflectionProbes] done");
    }

    static Cubemap Capture(ReflectionProbe p, string name, bool yami)
    {
        string path = $"{Dir}/Probe_{name}.exr";
        float dim = Shader.GetGlobalFloat("_UdonSkyDim");
        bool fog = RenderSettings.fog; Color fc = RenderSettings.fogColor; float fs = RenderSettings.fogStartDistance, fe = RenderSettings.fogEndDistance;
        try
        {
            if (yami)
            {
                var af = Object.FindObjectOfType<FuwaAreaFog>(true);
                if (af != null)
                {
                    Shader.SetGlobalFloat("_UdonSkyDim", af.skyDim);
                    RenderSettings.fog = true; RenderSettings.fogColor = af.fogColor;
                    RenderSettings.fogStartDistance = af.areaStart; RenderSettings.fogEndDistance = 3f / Mathf.Max(0.001f, af.density);
                }
            }
            if (!Lightmapping.BakeReflectionProbe(p, path)) { Debug.LogError("bake failed " + name); return null; }
        }
        finally
        {
            Shader.SetGlobalFloat("_UdonSkyDim", dim);
            RenderSettings.fog = fog; RenderSettings.fogColor = fc; RenderSettings.fogStartDistance = fs; RenderSettings.fogEndDistance = fe;
        }
        AssetDatabase.ImportAsset(path);
        var tex = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
        p.mode = ReflectionProbeMode.Custom;
        p.customBakedTexture = tex;
        return tex;
    }
}
