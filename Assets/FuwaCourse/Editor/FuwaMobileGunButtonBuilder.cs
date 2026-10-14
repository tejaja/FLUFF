using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// スマホの「エアガンを呼ぶ」ボタン（FuwaMobileGunButton）を FuwaSystem の下に作る（2026-10-14）。何度実行しても同じ結果
public static class FuwaMobileGunButtonBuilder
{
    const string ProgramPath = "Assets/FuwaCourse/Scripts/FuwaMobileGunButton.asset";
    const string TexPath = "Assets/FuwaCourse/Textures/Icons/MobileGunButton.png";
    const string MatPath = "Assets/FuwaCourse/Materials/MobileGunButton.mat";

    [MenuItem("FuwaCourse/スマホのエアガン呼び出しボタンを作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var sys = GameObject.Find("FuwaSystem").transform;
        foreach (var n in new[] { "MobileGunButton", "MobileGunButtonVisual" }) { var old = sys.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject); }

        var ti = (TextureImporter)AssetImporter.GetAtPath(TexPath);
        if (ti.alphaIsTransparency == false || ti.wrapMode != TextureWrapMode.Clamp || ti.maxTextureSize != 256)
        {
            ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = false; ti.maxTextureSize = 256; ti.SaveAndReimport();
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(Shader.Find("Fuwa/ScreenButton")); AssetDatabase.CreateAsset(mat, MatPath); }
        mat.shader = Shader.Find("Fuwa/ScreenButton");
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath));
        EditorUtility.SetDirty(mat);

        var go = new GameObject("MobileGunButton");
        go.transform.SetParent(sys, false);
        // 見た目は別の物に分ける（当たり判定と同じ物・その子に描くと、VRChat の「押せる」ハイライトで青い四角が出る。2026-10-14 てじゃ）
        var vis = new GameObject("MobileGunButtonVisual"); vis.transform.SetParent(sys, false);
        vis.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var mr = vis.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        // タップは UI のボタンで受ける（Interact の当たり判定だと、スマホでは押せる物の青い枠がずっと出る。2026-10-14 てじゃ）
        // Canvas は 100×100、0.01倍 → 親（ボタンの大きさに毎フレーム合わせる）の 1×1。VRCUiShape と同じ物にトリガーの BoxCollider（無いと実機で反応しない）
        var cgo = new GameObject("MobileGunButtonUI", typeof(RectTransform));
        cgo.transform.SetParent(go.transform, false);
        var crt = (RectTransform)cgo.transform;
        crt.sizeDelta = new Vector2(100f, 100f); crt.localScale = Vector3.one * 0.01f;
        var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        cgo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        cgo.AddComponent<VRC.SDK3.Components.VRCUiShape>();
        var bc = cgo.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(100f, 100f, 1f);
        var igo = new GameObject("Tap", typeof(RectTransform));
        igo.transform.SetParent(cgo.transform, false);
        var irt = (RectTransform)igo.transform;
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
        var img = igo.AddComponent<UnityEngine.UI.Image>(); img.color = new Color(1f, 1f, 1f, 0f);
        var btn = igo.AddComponent<UnityEngine.UI.Button>();
        btn.transition = UnityEngine.UI.Selectable.Transition.None;
        btn.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };

        var prog = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(ProgramPath);
        if (prog == null)
        {
            prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/FuwaMobileGunButton.cs");
            AssetDatabase.CreateAsset(prog, ProgramPath);
            AssetDatabase.SaveAssets();
        }
        var b = UdonSharpUndo.AddComponent<FuwaMobileGunButton>(go);
        b.summon = Object.FindObjectsOfType<FuwaGunSummon>(true).First();
        b.manager = Object.FindObjectsOfType<FuwaRespawnManager>(true).First();
        b.visual = mr; b.ui = cgo;
        UdonSharpEditorUtility.CopyProxyToUdon(b);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
        if (ub != null)
        {
            ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub);
            UnityEditor.Events.UnityEventTools.AddStringPersistentListener(btn.onClick, ub.SendCustomEvent, nameof(FuwaMobileGunButton.OnTap));
            EditorUtility.SetDirty(btn);
        }
        EditorUtility.SetDirty(b);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        return "ok";
    }
}
