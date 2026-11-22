using System.Linq;
using UnityEditor;
using UnityEngine;

// チュートリアルの見本の銃（Bay_*/Lane*/DemoGun/Visual）を、遠くでは描かない（2026-11-22 てじゃ「他になんか詰めれそうなもん」）。
// 見本の銃はプレイヤーの銃と同じメッシュ（1丁 約1.8万三角形）で 11 丁あり、チュートリアルの 38 万のうち 19 万がこれだった。
// 見本の台どうしは 10〜12m はなれているので、画面の高さの 1% より小さく（VR で 15m くらい先）なったら消す。となりの台の見本は見えたまま。
// メッシュはプレイヤーの銃と共有なので形は変えない。粒（Puff など）は LODGroup に入れない（撃った時だけ出る）。
// チュートリアルを作り直したら押し直す（何回押しても同じ）
public static class FuwaDemoGunLod
{
    public static float CullHeight = 0.01f;

    [MenuItem("FuwaCourse/済み（押す前に相談）/チュートリアルの見本の銃を遠くで消す")]
    static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        int n = 0, rs = 0;
        foreach (var demo in Object.FindObjectsOfType<Transform>(true).Where(t => t.name == "DemoGun" && t.gameObject.scene.IsValid()))
        {
            var vis = demo.Find("Visual"); if (vis == null) continue;
            var lg = vis.GetComponent<LODGroup>();   // ?? は Unity の「消えた物」に効かないので if で
            if (lg == null) lg = Undo.AddComponent<LODGroup>(vis.gameObject);
            var renderers = vis.GetComponentsInChildren<MeshRenderer>(true).Cast<Renderer>().ToArray();
            Undo.RecordObject(lg, "demo gun lod");
            lg.SetLODs(new[] { new LOD(CullHeight, renderers) });
            lg.RecalculateBounds();
            n++; rs += renderers.Length;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return $"[見本の銃] {n} 丁（描画物 {rs}）を画面の {CullHeight * 100f:F1}% より小さいと消す";
    }
}
