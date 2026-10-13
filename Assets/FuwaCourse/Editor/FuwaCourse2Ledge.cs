using System.Linq;
using UnityEditor;
using UnityEngine;

// いわのみち：頂上の柱ゾーン手前の崩落（谷）の、手前の切り口から突き出た足場（FuwaCourse2Collapse.Tongue で道の切り口そのものを伸ばした所）の下を、岩でささえる見た目
//（2026-10-13 てじゃ「黄色い道をこの赤枠の分伸ばして」。別の板を乗せたら「とってつけた感がすごい」→ 切り口の形を変えて、下は谷底から足場の裏まで岩がつながっている形に）。
// 見た目だけ（当たり判定なし）。何度実行してもよい
public static class FuwaCourse2Ledge
{
    static readonly Vector3 OldRootPos = new Vector3(44.312717f, 1.5f, 16.566227f);
    static readonly Quaternion OldRootRot = new Quaternion(0f, 0.6234898f, 0f, 0.78183156f);
    static readonly Vector3 OldOrigin = new Vector3(100f, 0f, 0.95f);
    static readonly Vector3 OldAxis = new Vector3(1f, 0f, 0.08f);

    [MenuItem("FuwaCourse/いわのみち：崩落の谷の足場の下の岩を作り直す")]
    public static void Menu()
    {
        Debug.Log(Run());
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
    }

    public static string Run()
    {
        var root = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaCourse2" && t.parent == null);
        var map = root.localToWorldMatrix * Matrix4x4.TRS(OldRootPos, OldRootRot, Vector3.one).inverse;
        Vector3 origin = map.MultiplyPoint3x4(OldOrigin);
        Vector3 axis = map.MultiplyVector(OldAxis.normalized); axis.y = 0f; axis.Normalize();
        Vector3 side = new Vector3(-axis.z, 0f, axis.x);   // CutJagged と同じ向き
        foreach (var n in new[] { "CollapseLedge", "CollapseLedgeRocks" }) { var o = root.Find(n); if (o != null) Object.DestroyImmediate(o.gameObject); }
        var holder = new GameObject("CollapseLedgeRocks").transform; holder.SetParent(root, false);
        var rockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Rock.mat");
        var meshes = new[] { "CrumbleRock1", "CrumbleRock2", "CrumbleRock4" }.Select(n => AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/" + n + ".asset")).ToArray();
        // 足場の裏（上面14.27 - 厚み0.3）より少し下まで。谷底（10）から積む
        const float bottom = 9.9f, top = 13.9f;
        var rnd = new System.Random(4021);
        float R(float a, float b) { return a + (float)rnd.NextDouble() * (b - a); }
        int n2 = 0;
        foreach (var (s, l, h0, h1) in new[] { (0.25f, 0.45f, bottom, top), (0.75f, 0.15f, bottom, top - 0.25f), (0.7f, 0.8f, bottom, top - 0.5f), (1.05f, 0.5f, bottom, top - 0.9f) })
        {
            var g = new GameObject("Rock" + n2++, typeof(MeshFilter), typeof(MeshRenderer)); g.transform.SetParent(holder, false);
            var mesh = meshes[rnd.Next(meshes.Length)];
            g.GetComponent<MeshFilter>().sharedMesh = mesh; g.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
            float hgt = h1 - h0;
            Vector3 c = origin + axis * s + side * l; c.y = h0 + hgt * 0.5f;
            g.transform.SetPositionAndRotation(c, Quaternion.Euler(R(-4f, 4f), R(0f, 360f), R(-4f, 4f)));
            g.transform.localScale = new Vector3(R(0.9f, 1.15f) / mesh.bounds.size.x, hgt / mesh.bounds.size.y, R(0.85f, 1.1f) / mesh.bounds.size.z);
            GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }
        return "ledge rocks " + n2;
    }
}
