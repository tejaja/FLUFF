using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// いわのみち：転がる岩が出てくる洞窟の入口（Rocks2/CaveMouth）のアーチの左わき（z の大きい側）と、とげとげの壁（AntiSkipWall）の間の細いすき間に
// ふわふわがはまって止まった（2026-10-20 てじゃ「岩ゴロゴロ洞窟の隙間、ふわふわが入るとハマっちゃうな」）。
// アーチの屋根は壁の方へ下っていて、上から落ちたふわふわが全部このすき間（幅0.1〜0.2m、ふわふわは直径0.3m）へ集まって挟まる。
// すき間を見えない斜めの当たり（CaveMouthGapFill）でふさぐ。上の面は壁ぎわほど高く、手前（-x、洞窟の外）へ下っていて、
// 乗ったふわふわは手前へ転がってアーチの前から床へ落ちる。下の面はアーチの屋根の下に隠れる高さ（z の小さい端）。
// 確認：周りの当たりを写したプレビューのシーンで、266個を上から落として全部床まで落ちた（前は66/99個がすき間で止まった）
public static class FuwaCaveMouthGapFill
{
    const string MeshPath = "Assets/FuwaCourse/Meshes/Course2_CaveMouthGapFill.asset";
    const float X0 = 104.4f, X1 = 105.7f, Z0 = 3.5f, Z1 = 4.8f, YBottom = 10.4f;   // ワールド座標

    static float Top(float x, float z) => 11.75f + (x - X0) * 0.6f + (z - 4f) * 0.8f;

    [MenuItem("FuwaCourse/済み（押す前に相談）/Course2/洞窟の入口のわきのすき間をふさぐ")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var rocks = GameObject.Find("FuwaCourse2")?.transform.Find("Rocks2"); if (!rocks) return "Rocks2 がない";
        var w = new[]
        {
            new Vector3(X0, YBottom, Z0), new Vector3(X1, YBottom, Z0), new Vector3(X1, YBottom, Z1), new Vector3(X0, YBottom, Z1),
            new Vector3(X0, Top(X0, Z0), Z0), new Vector3(X1, Top(X1, Z0), Z0), new Vector3(X1, Top(X1, Z1), Z1), new Vector3(X0, Top(X0, Z1), Z1),
        };
        var t = rocks.Find("CaveMouthGapFill");
        if (!t) { t = new GameObject("CaveMouthGapFill").transform; t.SetParent(rocks, false); Undo.RegisterCreatedObjectUndo(t.gameObject, "gap fill"); }
        t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        var v = new Vector3[w.Length]; for (int i = 0; i < w.Length; i++) v[i] = rocks.InverseTransformPoint(w[i]);
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        bool fresh = mesh == null; if (fresh) mesh = new Mesh();
        mesh.Clear(false); mesh.name = "Course2_CaveMouthGapFill";
        mesh.vertices = v;
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        if (fresh) AssetDatabase.CreateAsset(mesh, MeshPath); else EditorUtility.SetDirty(mesh);
        var mc = t.GetComponent<MeshCollider>(); if (!mc) mc = t.gameObject.AddComponent<MeshCollider>();
        mc.convex = true; mc.sharedMesh = null; mc.sharedMesh = mesh;
        var mouth = rocks.Find("CaveMouth");
        t.gameObject.layer = mouth ? mouth.gameObject.layer : 0;   // 入口の岩と同じ（地面ではない）
        t.gameObject.isStatic = true;
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(rocks.gameObject.scene);
        return "[洞窟のすき間] CaveMouthGapFill " + mc.bounds.center.ToString("F2");
    }
}
