using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// やみのもりの木（DecoForest/ForestTree）の葉のかたまりに、道の方をのぞく目（FuwaWatchEyes）を置く。
// 道から見える葉の面にめりこませて、うしろに暗いすき間（Hollow）。黒目はプレイヤーを目で追う（2026-10-11 てじゃ
// 「目が浮いてるのがきになる、どうせならこっち見てるギミック」）。何度実行しても同じ結果
public static class FuwaYamiEyes
{
    const string Course = "FuwaCourse3";
    const float S = 2.2f;   // 目のまとまり全体の大きさ（ルートの倍率）
    static readonly Vector3[] OldEyes =
    {
        new Vector3(50.78f, 15.26f, -60.65f), new Vector3(46.13f, 13.03f, -53.89f), new Vector3(65.84f, 7.67f, -100.22f),
        new Vector3(69.77f, 5.31f, -76.78f), new Vector3(50.54f, 10.01f, -65.27f), new Vector3(68.49f, 16.45f, -62.71f),
    };

    [MenuItem("FuwaCourse/やみのもり/のぞく目を作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var course = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == Course).transform;
        var forest = course.Find("DecoForest");
        var trees = forest.Cast<Transform>().Where(t => t.name == "ForestTree").ToList();
        // 目を付ける木：前の目（2026-10-04 の DecoForest/Eyes）があった所にいちばん近い6本
        var chosen = new List<Transform>();
        foreach (var at in OldEyes)
        {
            var near = trees.OrderBy(t => Vector3.Distance(t.Find("Tree").GetComponent<Renderer>().bounds.ClosestPoint(at), at)).First();
            if (!chosen.Contains(near)) chosen.Add(near);
        }
        foreach (var old in forest.Cast<Transform>().Where(t => t.name == "Eyes").ToList()) Object.DestroyImmediate(old.gameObject);
        // 道（ふわふわ・人の乗る所）：コースの当たり判定のうち、飾りの森以外
        var paths = course.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger && c.enabled && c.gameObject.activeInHierarchy && c.gameObject.layer == 11 && !c.transform.IsChildOf(forest)).ToList();
        var glowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/YamiEye.mat");
        var pupilMat = UnlitMat("YamiPupil", new Color(0.05f, 0.03f, 0.08f));
        var hollowMat = UnlitMat("YamiHollow", new Color(0.07f, 0.05f, 0.1f));
        var sphere = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/FuwaCourse/Meshes/LowSphere.asset");
        var log = new List<string>();
        foreach (var tree in chosen)
        {
            var old = tree.Find("PeekEyes"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var tr = tree.Find("Tree"); var b = tr.GetComponent<Renderer>().bounds;
            // 葉のかたまりのまん中（上の45%の頂点の重心。幹は入れない）
            var tm = tr.GetComponent<MeshFilter>().sharedMesh; Vector3 sum = Vector3.zero; int cnt = 0;
            foreach (var v in tm.vertices) { var w = tr.TransformPoint(v); if (w.y > b.max.y - b.size.y * 0.45f) { sum += w; cnt++; } }
            Vector3 aim = cnt > 0 ? sum / cnt : b.center;
            // いちばん近い道の上（目の高さ）から見る
            Vector3 view = Vector3.zero; float best = 1e9f;
            foreach (var c in paths)
            {
                // 凹のメッシュの当たり判定は ClosestPoint が使えない（点をそのまま返す）ので、上を向いた頂点から探す
                if (c is MeshCollider m && !m.convex && m.sharedMesh != null)
                {
                    var vs = m.sharedMesh.vertices; var ns = m.sharedMesh.normals;
                    for (int i = 0; i < vs.Length; i++)
                    {
                        if (ns.Length == vs.Length && c.transform.TransformDirection(ns[i]).y < 0.7f) continue;
                        var p = c.transform.TransformPoint(vs[i]); float d = (p - aim).sqrMagnitude; if (d < best) { best = d; view = p; }
                    }
                }
                else { var p = c.ClosestPoint(aim); if (p.y < c.bounds.max.y - 0.05f) p.y = c.bounds.max.y; float d = (p - aim).sqrMagnitude; if (d < best) { best = d; view = p; } }
            }
            view += Vector3.up * 1.6f;
            var mc = tr.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = tr.GetComponent<MeshFilter>().sharedMesh;
            Physics.SyncTransforms();
            bool ok = mc.Raycast(new Ray(view, (aim - view).normalized), out var hit, 200f);
            Object.DestroyImmediate(mc);
            if (!ok) { log.Add(tree.name + " no hit"); continue; }
            Vector3 toView = (view - hit.point).normalized;
            Vector3 fwd = (hit.normal + toView).normalized;   // 葉の面の向きと見る人の向きのあいだ
            var root = new GameObject("PeekEyes").transform; root.SetParent(tree, true);
            root.position = hit.point - hit.normal * 0.025f * S;
            root.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            root.localScale = Vector3.one * S / tree.lossyScale.x;
            Part(root, "Hollow", sphere, hollowMat, new Vector3(0f, 0f, -0.01f), new Vector3(0.46f, 0.22f, 0.07f));
            var glows = new Transform[2]; var pupils = new Transform[2];
            for (int k = 0; k < 2; k++)
            {
                float x = k == 0 ? -0.11f : 0.11f;
                glows[k] = Part(root, "Glow", sphere, glowMat, new Vector3(x, 0f, 0.005f), new Vector3(0.14f, 0.09f, 0.035f));
                pupils[k] = Part(root, "Pupil", sphere, pupilMat, new Vector3(x, 0f, 0.0245f), new Vector3(0.03f, 0.072f, 0.012f));
            }
            var we = UdonSharpUndo.AddComponent<FuwaWatchEyes>(root.gameObject);
            we.glows = glows; we.pupils = pupils;
            UdonSharpEditorUtility.CopyProxyToUdon(we);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(we);
            var so = new SerializedObject(ub); so.FindProperty("_syncMethod").intValue = (int)VRC.SDKBase.Networking.SyncType.None; so.ApplyModifiedPropertiesWithoutUndo();
            log.Add(tree.name + " eyes at " + root.position.ToString("F1") + " view " + view.ToString("F1") + " dist " + Vector3.Distance(view, hit.point).ToString("F1"));
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(course.gameObject.scene);
        return string.Join("\n", log);
    }

    static Transform Part(Transform root, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale)
    {
        var g = new GameObject(name); g.transform.SetParent(root, false);
        g.transform.localPosition = pos; g.transform.localScale = scale;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        GameObjectUtility.SetStaticEditorFlags(g, 0);
        return g.transform;
    }

    static Material UnlitMat(string name, Color c)
    {
        string path = "Assets/FuwaCourse/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Unlit/Color")) { name = name }; AssetDatabase.CreateAsset(m, path); }
        m.color = c; EditorUtility.SetDirty(m);
        return m;
    }
}
