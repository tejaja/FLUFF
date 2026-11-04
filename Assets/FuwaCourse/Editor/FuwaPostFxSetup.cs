using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

// ポストプロセス（2026-11-04 てじゃ「軽いのと中くらいのためしてみたい」）：色味（カラーグレーディング）・ビネット・ブルーム（弱め）。
//  VRChat はシーンの Reference Camera（Main Camera）の PostProcessLayer を使う。ボリュームは専用レイヤー 24「PostProcessing」に置いた全体用 1 個。
//  PC だけで効く（Quest/スマホでは無視される）。何度実行してもよい。数字はここで直して入れ直す
public static class FuwaPostFxSetup
{
    const int Layer = 24;
    const string LayerName = "PostProcessing";
    const string ProfilePath = "Assets/FuwaCourse/Generated/FuwaPostFx.asset";

    public static bool UseColor = true, UseVignette = false, UseBloom = true;   // ビネットは 2026-11-04 てじゃ「隅が黒いのはいらない」
    // 色味：ほんの少し鮮やか・コントラストを上げる
    public static float Saturation = 25f, Contrast = 15f, Temperature = 2f, PostExposure = 0f;
    // ビネット：画面のすみを少しだけ暗く（VR だと目のまわりが暗く見えるので弱く）
    public static float VignetteIntensity = 0.35f, VignetteSmooth = 0.45f;
    // ブルーム：明るい所だけ少しにじむ。fastMode で軽く
    public static float BloomIntensity = 2.0f, BloomThreshold = 0.8f, BloomSoftKnee = 0.5f, BloomDiffusion = 6f;

    [MenuItem("FuwaCourse/よく使う/ポストプロセスを入れ直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        // レイヤー名
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        var lp = layers.GetArrayElementAtIndex(Layer);
        if (string.IsNullOrEmpty(lp.stringValue)) { lp.stringValue = LayerName; tm.ApplyModifiedProperties(); }
        else if (lp.stringValue != LayerName) return "レイヤー " + Layer + " は " + lp.stringValue + " に使われている";

        // プロファイル
        var prof = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(ProfilePath);
        if (prof == null) { prof = ScriptableObject.CreateInstance<PostProcessProfile>(); AssetDatabase.CreateAsset(prof, ProfilePath); }
        foreach (var s in prof.settings.ToArray()) { prof.RemoveSettings(s.GetType()); Object.DestroyImmediate(s, true); }
        if (UseColor)
        {
            var c = prof.AddSettings<ColorGrading>(); AssetDatabase.AddObjectToAsset(c, prof);
            c.gradingMode.Override(GradingMode.HighDefinitionRange);
            c.tonemapper.Override(Tonemapper.None);
            c.saturation.Override(Saturation); c.contrast.Override(Contrast); c.temperature.Override(Temperature); c.postExposure.Override(PostExposure);
        }
        if (UseVignette)
        {
            var v = prof.AddSettings<Vignette>(); AssetDatabase.AddObjectToAsset(v, prof);
            v.intensity.Override(VignetteIntensity); v.smoothness.Override(VignetteSmooth);
        }
        if (UseBloom)
        {
            var b = prof.AddSettings<Bloom>(); AssetDatabase.AddObjectToAsset(b, prof);
            b.intensity.Override(BloomIntensity); b.threshold.Override(BloomThreshold); b.softKnee.Override(BloomSoftKnee); b.diffusion.Override(BloomDiffusion);
            b.fastMode.Override(true);
        }
        foreach (var s in prof.settings) s.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
        EditorUtility.SetDirty(prof); AssetDatabase.SaveAssets();

        // ボリューム（全体用）
        var go = GameObject.Find("FuwaPostFx") ?? new GameObject("FuwaPostFx");
        go.layer = Layer;
        var vol = go.GetComponent<PostProcessVolume>() ?? go.AddComponent<PostProcessVolume>();
        vol.isGlobal = true; vol.priority = 0; vol.weight = 1f; vol.sharedProfile = prof;
        EditorUtility.SetDirty(vol);

        // カメラ（Reference Camera）
        var d = Object.FindObjectOfType<VRC.SDK3.Components.VRCSceneDescriptor>();
        if (d == null || d.ReferenceCamera == null) return "Reference Camera がない";
        var cam = d.ReferenceCamera;
        var layer = cam.GetComponent<PostProcessLayer>() ?? cam.AddComponent<PostProcessLayer>();
        layer.volumeLayer = 1 << Layer; layer.volumeTrigger = cam.transform;
        layer.antialiasingMode = PostProcessLayer.Antialiasing.None;   // MSAA のまま（FXAA/TAA は足さない）
        var res = AssetDatabase.LoadAssetAtPath<PostProcessResources>("Packages/com.unity.postprocessing/PostProcessing/PostProcessResources.asset");
        if (res != null) layer.Init(res);
        EditorUtility.SetDirty(layer);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        return "ポストプロセス：色味=" + UseColor + " ビネット=" + UseVignette + " ブルーム=" + UseBloom + "（" + cam.name + "）";
    }

    [MenuItem("FuwaCourse/よく使う/ポストプロセスを外す")]
    public static void RemoveMenu()
    {
        var go = GameObject.Find("FuwaPostFx"); if (go != null) Object.DestroyImmediate(go);
        var d = Object.FindObjectOfType<VRC.SDK3.Components.VRCSceneDescriptor>();
        if (d != null && d.ReferenceCamera != null) { var l = d.ReferenceCamera.GetComponent<PostProcessLayer>(); if (l != null) Object.DestroyImmediate(l); }
        Debug.Log("ポストプロセスを外した");
    }
}
