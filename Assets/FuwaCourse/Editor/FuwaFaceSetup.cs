using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ふわふわに顔（お日さま顔）をつける。何度実行しても同じ結果
// ・Fuwa_BallFur（自分のふわふわ・お手本・モニター）＝いつもこっちを向く顔
// ・Fuwa_BallFurFaceRot（あそびばのふわふわ）＝玉にくっついて一緒に回る顔
// 顔は FuwaCourse/FurFace シェーダーの最後のパスで描く（毛は FuwaCourse/Fur と同じ）
public static class FuwaFaceSetup
{
    const string MatDir = "Assets/FuwaCourse/Materials/";
    const string BallMat = MatDir + "Fuwa_BallFur.mat";
    const string RotMat = MatDir + "Fuwa_BallFurFaceRot.mat";
    const string FluffPrefab = "Assets/FuwaCourse/Prefabs/Playground/Fluff.prefab";

    [MenuItem("FuwaCourse/ふわふわに顔をつける")]
    public static void Apply()
    {
        var shader = Shader.Find("FuwaCourse/FurFace");
        if (shader == null) { Debug.LogError("FuwaCourse/FurFace が見つからない"); return; }

        var ball = AssetDatabase.LoadAssetAtPath<Material>(BallMat);
        ball.shader = shader;
        ball.SetFloat("_FaceOn", 1f);
        ball.SetFloat("_FaceMode", 0f);
        EditorUtility.SetDirty(ball);

        // 一緒に回る版は Fuwa_BallFur の複製。すでにあれば中身だけそろえる（作り直すと参照が外れるので）
        var rot = AssetDatabase.LoadAssetAtPath<Material>(RotMat);
        if (rot == null) { rot = new Material(ball); AssetDatabase.CreateAsset(rot, RotMat); }
        else rot.CopyPropertiesFromMaterial(ball);
        rot.shader = shader;
        rot.SetFloat("_FaceMode", 1f);
        EditorUtility.SetDirty(rot);

        // あそびばのふわふわ：プレハブ（作り直しはここから複製される）とシーンに置いてある分
        int n = 0;
        var root = PrefabUtility.LoadPrefabContents(FluffPrefab);
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            if (r.sharedMaterial == ball) { r.sharedMaterial = rot; n++; }
        PrefabUtility.SaveAsPrefabAsset(root, FluffPrefab);
        PrefabUtility.UnloadPrefabContents(root);

        int s = 0;
        var pool = GameObject.Find("FuwaSystem/PlaygroundFluffs");
        if (pool != null)
            foreach (var r in pool.GetComponentsInChildren<Renderer>(true))
                if (r.sharedMaterial == ball) { Undo.RecordObject(r, "Fuwa face"); r.sharedMaterial = rot; s++; }
        if (s > 0) EditorSceneManager.MarkSceneDirty(pool.scene);

        AssetDatabase.SaveAssets();
        Debug.Log($"ふわふわの顔：こっち向き={BallMat} / 一緒に回る={RotMat}（プレハブ{n}・シーン{s}個）");
    }
}
