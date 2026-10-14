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
        // 当たり判定はトリガー（カメラの目の前に置くので、ふつうの当たり判定だとプレイヤーを押してしまう）
        var col = go.AddComponent<SphereCollider>(); col.isTrigger = true; col.radius = 0.5f;

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
        b.visual = mr; b.hit = col;
        UdonSharpEditorUtility.CopyProxyToUdon(b);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(b);
        if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; ub.interactText = "エアガンを呼ぶ"; ub.proximity = 3f; EditorUtility.SetDirty(ub); }
        EditorUtility.SetDirty(b);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        return "ok";
    }
}
