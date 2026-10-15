using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ふわふわの見た目の材質をそろえる。何度実行しても同じ結果
// ・Fuwa_BallFur（自分のふわふわ・お手本・モニター）＝顔なし、ふつうの毛（FuwaCourse/Fur）
//   （2026-10-15 てじゃ：こっちを向く顔は「こわい」、一緒に回る顔は「かわいいけど見えない」→ ふだんは顔なし）
// ・Fuwa_BallFurFaceRot（あそびばのふわふわ）＝毛＋顔（FuwaCourse/FurFace、一緒に回る）。顔はあそびばの設定スイッチで出す（ふだんはオフ）
// ・Fuwa_BallFurPlain（あそびばの毛オフ）＝毛の層なしの球＋顔（FuwaCourse/FurPlain）。設定スイッチで切り替え
public static class FuwaFaceSetup
{
    const string MatDir = "Assets/FuwaCourse/Materials/";
    const string BallMat = MatDir + "Fuwa_BallFur.mat";
    const string RotMat = MatDir + "Fuwa_BallFurFaceRot.mat";
    const string PlainMat = MatDir + "Fuwa_BallFurPlain.mat";
    const string FluffPrefab = "Assets/FuwaCourse/Prefabs/Playground/Fluff.prefab";
    const float PlainH = 0.6f;   // 毛オフの球の大きさ（毛の長さの何割ぶんふくらませるか）。毛ありの見た目の大きさに合わせる

    [MenuItem("FuwaCourse/ふわふわの顔・毛オフの材質をそろえる")]
    public static void Apply()
    {
        var furShader = Shader.Find("FuwaCourse/Fur");
        var faceShader = Shader.Find("FuwaCourse/FurFace");
        var plainShader = Shader.Find("FuwaCourse/FurPlain");
        if (furShader == null || faceShader == null || plainShader == null) { Debug.LogError("FuwaCourse/Fur・FurFace・FurPlain のどれかが見つからない"); return; }

        var ball = AssetDatabase.LoadAssetAtPath<Material>(BallMat);
        ball.shader = furShader;
        EditorUtility.SetDirty(ball);

        // すでにあれば中身だけそろえる（作り直すと参照が外れるので）
        var rot = Ensure(RotMat, ball);
        rot.shader = faceShader;
        rot.SetFloat("_FaceOn", 0f);
        rot.SetFloat("_FaceMode", 1f);
        EditorUtility.SetDirty(rot);

        var plain = Ensure(PlainMat, ball);
        plain.shader = plainShader;
        plain.SetFloat("_FaceOn", 0f);
        plain.SetFloat("_FaceMode", 1f);
        plain.SetFloat("_PlainH", PlainH);
        plain.SetFloat("_FaceDepth", PlainH);
        EditorUtility.SetDirty(plain);

        // あそびばのふわふわ：プレハブ（作り直しはここから複製される）とシーンに置いてある分
        int n = 0;
        var root = PrefabUtility.LoadPrefabContents(FluffPrefab);
        foreach (var b in root.GetComponentsInChildren<FuwaSharedBall>(true)) { Wire(b, ball, rot, plain); n++; }
        PrefabUtility.SaveAsPrefabAsset(root, FluffPrefab);
        PrefabUtility.UnloadPrefabContents(root);

        int s = 0;
        var pool = GameObject.Find("FuwaSystem/PlaygroundFluffs");
        if (pool != null)
        {
            foreach (var b in pool.GetComponentsInChildren<FuwaSharedBall>(true)) { Wire(b, ball, rot, plain); s++; }
            EditorSceneManager.MarkSceneDirty(pool.scene);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"ふわふわの材質：ふつう={BallMat} / あそびば={RotMat}・毛オフ={PlainMat}（プレハブ{n}・シーン{s}個）");
    }

    static Material Ensure(string path, Material src)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
        else m.CopyPropertiesFromMaterial(src);
        return m;
    }

    static void Wire(FuwaSharedBall b, Material ball, Material rot, Material plain)
    {
        foreach (var r in b.GetComponentsInChildren<Renderer>(true))
            if (r.sharedMaterial == ball || r.sharedMaterial == rot) r.sharedMaterial = rot;
        var so = new SerializedObject(b);
        so.FindProperty("plainMaterial").objectReferenceValue = plain;
        so.ApplyModifiedPropertiesWithoutUndo();
        UdonSharpEditorUtility.CopyProxyToUdon(b);
    }
}
