using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 影を焼く（Hikari）ための準備。何度実行しても同じ結果。コースを作り直したら（メッシュが新しくなるので）もう一度実行する。
// ・動かない物（ToonLit / InkCap だけの物）を Static（Contribute GI）にして、ライトマップを受けるようにする
// ・動く物（転がる岩・はねるキノコ・吹き流し・敵・ゴールの飾り…）とその下の物は Static にしない
// ・ライトマップ用UV（UV2）が無いメッシュには作る（.asset は直接、FBX はインポート設定で）
public static class FuwaBakeSetup
{
    static readonly string[] Roots = { "FuwaCourse0", "FuwaCourse", "FuwaCourse2", "FuwaCourse3", "FuwaCourse4", "FuwaCourse5", "FuwaLobby" };
    static readonly HashSet<string> BakeShaders = new HashSet<string> { "FuwaCourse/ToonLit", "FuwaCourse/InkCap" };
    // 物を動かすスクリプト（自分とその下、参照している物を Static にしない）
    static readonly HashSet<string> MovingTypes = new HashSet<string> {
        "FuwaBall", "FuwaBlower", "FuwaBoulder", "FuwaEnemy", "FuwaFacePlayer", "FuwaGoal", "FuwaGoalMarker", "FuwaItemBox",
        "FuwaJumpBoost", "FuwaMama", "FuwaSharedBall", "FuwaStartMushroom", "FuwaWebHole", "FuwaWindDeco" };
    // 自分は動かず、参照している物（輪っか等）だけ動かすスクリプト
    static readonly HashSet<string> RefOnlyTypes = new HashSet<string> { "FuwaWebHole" };
    const float FarScale = 0.25f;

    [MenuItem("FuwaCourse/ベイクの準備（Static・ライトマップUV）")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        var log = new System.Text.StringBuilder();
        // 動く物の一覧
        var moving = new HashSet<Transform>();
        foreach (var g in roots)
            foreach (var b in g.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true))
            {
                if (!MovingTypes.Contains(b.GetType().Name)) continue;
                if (!RefOnlyTypes.Contains(b.GetType().Name)) moving.Add(b.transform);
                var so = new SerializedObject(b); var it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    var o = it.objectReferenceValue;
                    var t = o is Component c ? c.transform : o is GameObject go ? go.transform : null;
                    if (t != null && t.root != t && !(RefOnlyTypes.Contains(b.GetType().Name) && t == b.transform)) moving.Add(t);   // ルートそのもの（コース全体）を指している物は除く
                }
            }
        foreach (var g in roots) foreach (var a in g.GetComponentsInChildren<Animator>(true)) moving.Add(a.transform);
        foreach (var g in roots) foreach (var rb in g.GetComponentsInChildren<Rigidbody>(true)) if (!rb.isKinematic || rb.GetComponent<UdonSharp.UdonSharpBehaviour>() != null) moving.Add(rb.transform);
        bool IsMoving(Transform t) { for (; t != null; t = t.parent) if (moving.Contains(t)) return true; return false; }

        var meshes = new HashSet<Mesh>(); int nStatic = 0, nMoving = 0, nOther = 0; double area = 0;
        foreach (var rn in Roots)
        {
            var root = roots.FirstOrDefault(g => g.name == rn); if (root == null) continue;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                var mf = r.GetComponent<MeshFilter>();
                // 焼ける材質が1つ以上あって、全部不透明なら OK（洞窟の内側などの別シェーダーが混ざっていても、影は落とせる）
                bool shaderOk = r.sharedMaterials.Length > 0 && r.sharedMaterials.Any(m => m != null && BakeShaders.Contains(m.shader.name))
                                && r.sharedMaterials.All(m => m != null && m.renderQueue < 2450);
                bool ok = shaderOk && mf != null && mf.sharedMesh != null && !IsMoving(r.transform);
                if (ok)
                {
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags | StaticEditorFlags.ContributeGI);
                    r.receiveGI = ReceiveGI.Lightmaps;
                    r.scaleInLightmap = r.name.EndsWith("_Far") ? FarScale : 1f;
                    meshes.Add(mf.sharedMesh); nStatic++;
                    if (!r.name.EndsWith("_Far")) area += MeshArea(mf.sharedMesh, r.transform.lossyScale);
                }
                else
                {
                    if ((flags & StaticEditorFlags.ContributeGI) != 0) GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags & ~StaticEditorFlags.ContributeGI);
                    r.receiveGI = ReceiveGI.LightProbes;
                    if (shaderOk) nMoving++; else nOther++;
                }
            }
        }
        // ライトマップ用UV
        int gen = 0, fbx = 0; var importers = new HashSet<ModelImporter>();
        foreach (var m in meshes)
        {
            if (m.uv2 != null && m.uv2.Length == m.vertexCount && m.vertexCount > 0) continue;
            var path = AssetDatabase.GetAssetPath(m);
            if (path.EndsWith(".asset"))
            {
                UnwrapParam p; UnwrapParam.SetDefaults(out p);
                if (Unwrapping.GenerateSecondaryUVSet(m, p)) { EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m); gen++; }
                else log.Append("UV2作成に失敗: " + m.name + "\n");
            }
            else if (AssetImporter.GetAtPath(path) is ModelImporter mi && !mi.generateSecondaryUV) { mi.generateSecondaryUV = true; importers.Add(mi); }
            else log.Append("UV2なし（直せない）: " + m.name + " " + path + "\n");
        }
        foreach (var mi in importers) { mi.SaveAndReimport(); fbx++; }
        // UV2 が無い・壊れた値（NaN/無限）がある物は、ライトマップを受けずに影を落とすだけにする（Hikari が止まるので）
        var bad = new HashSet<Mesh>();
        foreach (var m in meshes)
        {
            var u = m.uv2;
            if (u == null || u.Length != m.vertexCount) { bad.Add(m); continue; }
            foreach (var q in u) if (float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsInfinity(q.x) || float.IsInfinity(q.y)) { bad.Add(m); break; }
        }
        // 接線・法線に壊れた値があると Hikari が止まる（接線はシェーダーで使っていないので消す）
        foreach (var m in meshes)
        {
            System.Func<Vector3, bool> nf = q => float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsInfinity(q.x) || float.IsInfinity(q.y) || float.IsInfinity(q.z);
            if (!AssetDatabase.GetAssetPath(m).EndsWith(".asset")) continue;
            bool dirty = false;
            if (m.tangents.Any(q => nf(q) || float.IsNaN(q.w))) { m.tangents = null; dirty = true; }
            var nn = m.normals; bool bn = false; for (int i = 0; i < nn.Length; i++) if (nf(nn[i])) { nn[i] = Vector3.up; bn = true; }
            if (bn) { m.normals = nn; dirty = true; }
            if (dirty) { EditorUtility.SetDirty(m); log.Append("壊れた接線/法線を修正: " + m.name + "\n"); }
        }
        int occOnly = 0;
        foreach (var rn in Roots)
        {
            var root = roots.FirstOrDefault(g => g.name == rn); if (root == null) continue;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.receiveGI != ReceiveGI.Lightmaps) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && bad.Contains(mf.sharedMesh)) { r.scaleInLightmap = 0f; occOnly++; }
            }
        }
        foreach (var m in bad) log.Append("UV2が使えない（影を落とすだけ）: " + m.name + " " + AssetDatabase.GetAssetPath(m) + "\n");
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        log.Append("影だけ " + occOnly + " / static " + nStatic + " / 動く物で除外 " + nMoving + " / 別シェーダー " + nOther + " / UV2作成 " + gen + "（FBX " + fbx + "） / 面積 " + area.ToString("F0") + "m²");
        return log.ToString();
    }

    static double MeshArea(Mesh m, Vector3 s)
    {
        var v = m.vertices; var t = m.triangles; double a = 0;
        for (int i = 0; i < t.Length; i += 3)
        {
            var p0 = Vector3.Scale(v[t[i]], s); var p1 = Vector3.Scale(v[t[i + 1]], s); var p2 = Vector3.Scale(v[t[i + 2]], s);
            a += Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5;
        }
        return a;
    }
}
