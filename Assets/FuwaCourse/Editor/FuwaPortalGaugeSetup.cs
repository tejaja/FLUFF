using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// ポータル（FuwaCourseWarp）に「目の前の丸いゲージ」を付ける（2026-10-12 てじゃ「しゃらんでふわっと目の前にヴェール→周囲に、目の前にサークルのゲージ」）
// ゲージは縦の板（FuwaCourse/ProgressRing）。置く場所・向きは乗った瞬間にスクリプトが決めるので、ここでは作って登録するだけ。何度実行しても同じ
public static class FuwaPortalGaugeSetup
{
    const string MatPath = "Assets/FuwaCourse/Materials/PortalGauge.mat";

    [MenuItem("FuwaCourse/ポータルに目の前のゲージを付ける")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        int n = 0;
        foreach (var w in Object.FindObjectsOfType<FuwaCourseWarp>(true))
        {
            if (w.progressRing == null) continue;
            if (mat == null)
            {
                mat = new Material(w.progressRing.sharedMaterial) { name = "PortalGauge" };
                mat.SetColor("_BgColor", new Color(1f, 1f, 1f, 0.28f));
                mat.SetFloat("_Inner", 0.36f); mat.SetFloat("_Outer", 0.47f);
                mat.SetFloat("_Soft", 0f); mat.SetFloat("_Start", 0f); mat.SetFloat("_Fill", 0f);
                mat.renderQueue = 3020;   // 幕（Transparent+10）より手前に
                AssetDatabase.CreateAsset(mat, MatPath);
            }
            var parent = w.transform.parent;
            var old = parent.Find(w.name + "_Gauge"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = w.name + "_Gauge";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            q.transform.localScale = Vector3.zero;
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            r.enabled = false;
            Undo.RecordObject(w, "gauge");
            w.gauge = r;
            UdonSharpEditorUtility.CopyProxyToUdon(w);
            EditorUtility.SetDirty(w);
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "portal gauges: " + n;
    }
}
