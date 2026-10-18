using System.Linq;
using UnityEditor;
using UnityEngine;

// まものの足元の丸い影（2026-10-09 てじゃ「まものの影がない」）。ふわふわ・プレイヤーと同じ FuwaCourse/BlobDecal の見えない箱。
// 箱は見た目（visual）の子にするので、倒されて見た目が消えると影も消える。巣にもぐると箱より上に地面が来て影が出ない。
// 箱は「地面の少し上〜下 ShadowBelow m」まで。跳ねて高く上がると地面が箱の下のふちに近づいて、だんだん薄くなって消える。
// スクリプトは足さない（毎フレームの計算なし）。何度実行してもよい（前の影は作り直す）
public static class FuwaEnemyShadows
{
    const string MatPath = "Assets/FuwaCourse/Materials/EnemyShadowDecal.mat";
    const float ShadowAbove = 0.6f, ShadowBelow = 2.6f;
    const float SizeK = 1.15f;   // 体の幅に対する影の直径

    [MenuItem("FuwaCourse/済み（押す前に相談）/まものの影をつける")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/BallShadowDecal.mat"));
            mat.SetFloat("_Alpha", 0.6f);
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        int n = 0; var log = new System.Text.StringBuilder();
        void Add(Transform root, Transform visual)
        {
            if (visual == null) return;
            var old = visual.Find("Shadow"); if (old != null) Object.DestroyImmediate(old.gameObject);
            // 体の幅（LODの軽い版は除いて、見た目のいちばん大きい物で測る）
            var rs = visual.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            // 地面：自分の当たり判定は無視して真下へ
            var cols = root.GetComponentsInChildren<Collider>(true);
            var hits = Physics.RaycastAll(b.center + Vector3.up * 0.2f, Vector3.down, 6f, (1 << 0) | (1 << 11), QueryTriggerInteraction.Ignore)
                .Where(h => !cols.Contains(h.collider)).OrderBy(h => h.distance).ToArray();
            float groundY = hits.Length > 0 ? hits[0].point.y : b.min.y;
            float size = Mathf.Max(b.size.x, b.size.z) * SizeK;
            var go = new GameObject("Shadow", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(visual, false);
            var mr = go.GetComponent<MeshRenderer>();
            go.GetComponent<MeshFilter>().sharedMesh = cube; mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            go.layer = visual.gameObject.layer;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            // ワールドで：中心は地面から (上-下)/2、高さ 上+下、幅 size。見た目の拡大・回転を打ち消して置く
            go.transform.position = new Vector3(b.center.x, groundY + (ShadowAbove - ShadowBelow) * 0.5f, b.center.z);
            go.transform.rotation = Quaternion.identity;
            var ls = visual.lossyScale;
            go.transform.localScale = new Vector3(size / ls.x, (ShadowAbove + ShadowBelow) / ls.y, size / ls.z);
            n++;
            log.Append(root.name + "(" + size.ToString("F2") + "m, 地面まで " + (b.min.y - groundY).ToString("F2") + ") ");
        }
        foreach (var e in Object.FindObjectsOfType<FuwaEnemy>(true)) Add(e.transform, e.visual);
        foreach (var m in Object.FindObjectsOfType<FuwaMama>(true)) Add(m.transform, m.visual);
        foreach (var d in Object.FindObjectsOfType<FuwaDemoMonster>(true)) Add(d.transform, d.transform.Find("Body"));
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "まものの影 " + n + "体: " + log;
    }
}
