using System.Linq;
using UnityEditor;
using UnityEngine;

// 2026-11-17 てじゃ「よくかんがえたら洞窟に影ないの違和感あるな」→「ちゃんとした影ってむり？」「ほんものだけでいいよ」
//  いわのみち（FuwaCourse2）の洞窟（Rocks2/Tunnel）は焼き込み用の静的物。Shadowmask だと静的物はリアルタイムの影を出さず、
//  道（静的でない）には洞窟の影が落ちない。→ 洞窟と同じメッシュの「影だけ出す」コピー（静的でない）を置く。
//  （最初はうそ影＝BlobDecal の楕円にしていたが、本物の影にしたので消す）
public static class FuwaFakeCaveShadow
{
    [MenuItem("FuwaCourse/ステージ1/洞窟の影を付け直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var root = Object.FindObjectsOfType<Transform>(true).FirstOrDefault(t => t.name == "FuwaCourse2" && t.parent == null); if (!root) return "FuwaCourse2 がない";
        var tun = root.Find("Rocks2/Tunnel"); if (!tun) return "洞窟がない";
        foreach (var n in new[] { "TunnelShadow", "TunnelShadowCaster" }) { var old = tun.parent.Find(n); if (old) Object.DestroyImmediate(old.gameObject); }
        var g = new GameObject("TunnelShadowCaster", typeof(MeshFilter), typeof(MeshRenderer));
        g.transform.SetParent(tun.parent, false);
        g.transform.localPosition = tun.localPosition; g.transform.localRotation = tun.localRotation; g.transform.localScale = tun.localScale;
        g.GetComponent<MeshFilter>().sharedMesh = tun.GetComponent<MeshFilter>().sharedMesh;
        var r = g.GetComponent<MeshRenderer>();
        r.sharedMaterials = tun.GetComponent<MeshRenderer>().sharedMaterials;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly; r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        GameObjectUtility.SetStaticEditorFlags(g, 0); g.layer = tun.gameObject.layer;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(g.scene);
        return "[洞窟の影] 影だけ出すコピーを置いた";
    }
}
