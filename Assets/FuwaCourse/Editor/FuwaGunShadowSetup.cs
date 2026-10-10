using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// エアガンの影をうそ影にする（2026-10-11 てじゃ「銃の影自体本物も含めて偽影にしちゃおう、スケール変更対応」）。
// ・みんなの銃（PlayerObject の FuwaBlower）：本物の影を切って、FuwaSystem/GunShadows（FuwaGunShadows）が真下にうそ影を置く
// ・チュートリアルの見本の銃（DemoGun）：本物の影を切って、真下の地面に動かないうそ影の箱（Lane の子 GunShadow）
// ・呼び寄せの時に飛ぶ銃（GunDecoy）：本物の影を切るだけ
// 何度実行してもよい
public static class FuwaGunShadowSetup
{
    const string MatPath = "Assets/FuwaCourse/Materials/GunShadowDecal.mat";
    const int Count = 24;
    static readonly Vector2 Size = new Vector2(0.22f, 0.42f);
    const float Alpha = 0.45f;

    [MenuItem("FuwaCourse/エアガンの影をうそ影にする")]
    public static void Menu() { Debug.Log(Run()); }

    public static Material Mat()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/BallShadowDecal.mat"));
            mat.name = "GunShadowDecal";
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        mat.SetFloat("_Alpha", Alpha);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    public static string Run()
    {
        var mat = Mat();
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        var log = new System.Text.StringBuilder();
        var tmpl = Object.FindObjectsOfType<FuwaBlower>(true).First(b => b.transform.parent == null && b.name == "FuwaBlower");
        // 本物の影を切る
        int off = 0;
        var gunRends = new System.Collections.Generic.HashSet<Renderer>();
        void NoCast(Transform root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                gunRends.Add(r);
                if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off) continue;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; EditorUtility.SetDirty(r); off++;
            }
        }
        NoCast(tmpl.visualRoot != null ? tmpl.visualRoot.transform : tmpl.transform);
        foreach (var t in Object.FindObjectsOfType<Transform>(true).Where(t => t.name == "DemoGun" || t.name == "GunDecoy")) NoCast(t);
        // 銃の部品は FuwaFarShadows の対象からも外す（外さないと近づいた時に On に戻される）。
        // ※ 対象のほかの物は、近い時だけ影を出す設定（SmallShadows）でふだん Off のものもあるので、影の設定では選ばない
        foreach (var fs in Object.FindObjectsOfType<FuwaFarShadows>(true))
        {
            var keep = Enumerable.Range(0, fs.targets.Length).Where(i => !(fs.targets[i] != null && gunRends.Contains(fs.targets[i]))).ToList();
            if (keep.Count == fs.targets.Length) continue;
            log.Append(fs.name + " targets " + fs.targets.Length + "->" + keep.Count + " ");
            fs.targets = keep.Select(i => fs.targets[i]).ToArray();
            if (fs.centers != null && fs.centers.Length > 0) fs.centers = keep.Select(i => fs.centers[i]).ToArray();
            if (fs.radii != null && fs.radii.Length > 0) fs.radii = keep.Select(i => fs.radii[i]).ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(fs);
        }
        log.Append("cast off " + off + " ");

        // みんなの銃のうそ影（シーン側に人数ぶん）
        var sys = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaSystem" && t.parent == null);
        var old = sys.Find("GunShadows"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject("GunShadows"); holder.transform.SetParent(sys, false);
        var rs = new Renderer[Count];
        for (int i = 0; i < Count; i++) rs[i] = Box(holder.transform, "Shadow" + i, cube, mat);
        var gs = UdonSharpUndo.AddComponent<FuwaGunShadows>(holder);
        gs.blowerTemplate = tmpl; gs.shadows = rs; gs.size = Size; gs.alpha = Alpha;
        UdonSharpEditorUtility.CopyProxyToUdon(gs);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(gs);
        var so = new SerializedObject(ub); so.FindProperty("_syncMethod").intValue = (int)VRC.SDKBase.Networking.SyncType.None; so.ApplyModifiedPropertiesWithoutUndo();

        log.Append("demo " + DemoShadows());
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sys.gameObject.scene);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // チュートリアルの見本の銃：真下の地面に動かないうそ影（銃は向きを変えるだけで動かないので、Lane の子に置く）
    public static string DemoShadows()
    {
        var mat = Mat();
        var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        Physics.SyncTransforms();
        int n = 0;
        foreach (var g in Object.FindObjectsOfType<Transform>(true).Where(t => t.name == "DemoGun").ToList())
        {
            var lane = g.parent;
            var oldS = lane.Find("GunShadow"); if (oldS != null) Object.DestroyImmediate(oldS.gameObject);
            var rends = g.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer) && !r.name.EndsWith("_Far")).ToArray();
            if (rends.Length == 0) continue;
            Vector3 c = rends.Aggregate(Vector3.zero, (s, r) => s + r.bounds.center) / rends.Length;
            // 見た目の真ん中の真下に地面がない時（上向きの銃が島のふちから出ている等）は、銃の根元の真下
            if (!Physics.Raycast(c, Vector3.down, out var hit, 4f, (1 << 0) | (1 << 11), QueryTriggerInteraction.Ignore))
            {
                c = g.position;
                if (!Physics.Raycast(c, Vector3.down, out hit, 4f, (1 << 0) | (1 << 11), QueryTriggerInteraction.Ignore))
                {
                    // 当たり判定がない見た目だけの床（見本島の床）：真下にある床の見た目の上面
                    var floor = lane.parent.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer) && r.bounds.max.y < c.y
                        && c.x > r.bounds.min.x && c.x < r.bounds.max.x && c.z > r.bounds.min.z && c.z < r.bounds.max.z && !r.transform.IsChildOf(g)).OrderByDescending(r => r.bounds.max.y).FirstOrDefault();
                    if (floor == null) continue;
                    hit.point = new Vector3(c.x, floor.bounds.max.y, c.z); hit.distance = c.y - floor.bounds.max.y;
                }
            }
            var box = Box(lane, "GunShadow", cube, mat);
            Vector3 fwd = g.forward; fwd.y = 0f; if (fwd.sqrMagnitude < 1e-4f) fwd = lane.forward;
            const float above = 0.06f, below = 0.3f;
            box.transform.SetPositionAndRotation(hit.point + Vector3.up * ((above - below) * 0.5f), Quaternion.LookRotation(fwd.normalized, Vector3.up));
            float k = Mathf.Clamp01(hit.distance / 3f), s = 1f + k * 0.5f;
            var ls = lane.lossyScale;
            box.transform.localScale = new Vector3(Size.x * s / ls.x, (above + below) / ls.y, Size.y * s / ls.z);
            box.enabled = true;
            n++;
        }
        return n.ToString();
    }

    static Renderer Box(Transform parent, string name, Mesh cube, Material mat)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = cube;
        var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        mr.enabled = false;
        return mr;
    }
}
