using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// スマホの竹とんぼ ↑↓ ボタン（FuwaMobileCopterButtons）を FuwaSystem の下に作る（2026-11-21）。何度実行しても同じ結果。
// 作り方はエアガン呼び出しボタン（FuwaMobileGunButtonBuilder）と同じ：見た目の板は別の物、タップは透明な UI ボタン（VRCUiShape＋トリガーの BoxCollider）
public static class FuwaMobileCopterButtonsBuilder
{
    const string ProgramPath = "Assets/FuwaCourse/Scripts/FuwaMobileCopterButtons.asset";
    const string Root = "MobileCopterButtons";

    [MenuItem("FuwaCourse/スマホの竹とんぼ↑↓ボタンを作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    static Material Mat(string name)
    {
        string tp = "Assets/FuwaCourse/Textures/Icons/" + name + ".png", mp = "Assets/FuwaCourse/Materials/" + name + ".mat";
        var ti = (TextureImporter)AssetImporter.GetAtPath(tp);
        if (ti.alphaIsTransparency == false || ti.wrapMode != TextureWrapMode.Clamp || ti.maxTextureSize != 256 || ti.mipmapEnabled)
        {
            ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp;
            ti.mipmapEnabled = false; ti.maxTextureSize = 256; ti.SaveAndReimport();
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (mat == null) { mat = new Material(Shader.Find("Fuwa/ScreenButton")); AssetDatabase.CreateAsset(mat, mp); }
        mat.shader = Shader.Find("Fuwa/ScreenButton");
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static (Transform root, MeshRenderer vis, GameObject ui, UnityEngine.UI.Button btn) Make(Transform parent, Transform sys, string name, Material mat)
    {
        var vis = new GameObject(name + "Visual"); vis.transform.SetParent(sys, false);
        vis.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        var mr = vis.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        var root = new GameObject(name); root.transform.SetParent(parent, false);
        var cgo = new GameObject(name + "UI", typeof(RectTransform)); cgo.transform.SetParent(root.transform, false);
        var crt = (RectTransform)cgo.transform; crt.sizeDelta = new Vector2(100f, 100f); crt.localScale = Vector3.one * 0.01f;
        var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        cgo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        cgo.AddComponent<VRC.SDK3.Components.VRCUiShape>();
        var bc = cgo.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(100f, 100f, 1f);
        var igo = new GameObject("Tap", typeof(RectTransform)); igo.transform.SetParent(cgo.transform, false);
        var irt = (RectTransform)igo.transform; irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;
        var img = igo.AddComponent<UnityEngine.UI.Image>(); img.color = new Color(1f, 1f, 1f, 0f);
        var btn = igo.AddComponent<UnityEngine.UI.Button>();
        btn.transition = UnityEngine.UI.Selectable.Transition.None;
        btn.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        return (root.transform, mr, cgo, btn);
    }

    public static string Run()
    {
        var sys = GameObject.Find("FuwaSystem").transform;
        foreach (var n in new[] { Root, "CopterUpVisual", "CopterDownVisual" }) { var old = sys.Find(n); if (old != null) Object.DestroyImmediate(old.gameObject); }
        var go = new GameObject(Root); go.transform.SetParent(sys, false);
        var up = Make(go.transform, sys, "CopterUp", Mat("MobileCopterUp"));
        var dn = Make(go.transform, sys, "CopterDown", Mat("MobileCopterDown"));

        var prog = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(ProgramPath);
        if (prog == null)
        {
            prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/FuwaMobileCopterButtons.cs");
            AssetDatabase.CreateAsset(prog, ProgramPath);
            AssetDatabase.SaveAssets();
        }
        var b = UdonSharpUndo.AddComponent<FuwaMobileCopterButtons>(go);
        b.copters = Object.FindObjectsOfType<FuwaBambooCopter>(true).ToArray();
        b.upRoot = up.root; b.upVisual = up.vis; b.upUi = up.ui;
        b.downRoot = dn.root; b.downVisual = dn.vis; b.downUi = dn.ui;
        UdonSharpEditorUtility.CopyProxyToUdon(b);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
        if (ub != null)
        {
            ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub);
            UnityEditor.Events.UnityEventTools.AddStringPersistentListener(up.btn.onClick, ub.SendCustomEvent, nameof(FuwaMobileCopterButtons.OnTapUp));
            UnityEditor.Events.UnityEventTools.AddStringPersistentListener(dn.btn.onClick, ub.SendCustomEvent, nameof(FuwaMobileCopterButtons.OnTapDown));
            EditorUtility.SetDirty(up.btn); EditorUtility.SetDirty(dn.btn);
        }
        EditorUtility.SetDirty(b);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        return "竹とんぼ↑↓ボタン ok（竹とんぼ " + b.copters.Length + " 本）";
    }
}
