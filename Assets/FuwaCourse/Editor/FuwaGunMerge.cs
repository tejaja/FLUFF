using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// エアガン（PlayerObject の FuwaBlower）の本体・ラッパ（Accent）・持ち手・通気口を、頂点の色で1つのメッシュ・1つの材質にまとめる。
// 2026-11-21 てじゃ「銃を皆もってると負荷が凄いかも」「見た目に差がでるのやだな、そもそもの作りを軽量化しよか」：
//  4つの部品は材質が色ちがいなだけ（FuwaCourse/ToonLit、色以外の値は全部同じ）だったので、色を頂点に入れて（_UseVertexColor）1回で描く。見た目は同じ。
//  金ピカは金色の頂点の写し（Gun_Merged_Gold）と金の材質に差しかえる（FuwaBlower.goldMeshFilters / goldMeshes）。
// まとめた物は GunBody という名前のまま（ほかのビルダーが名前で探すので）。元の部品のメッシュ（Gun_Body など）はそのまま残すので、
// ラッパの首・トリガーを作り直したら（FuwaGunCollarRebuild など）、これをもう一度実行する。何度実行しても同じ結果
public static class FuwaGunMerge
{
    const string MeshDir = "Assets/FuwaCourse/Meshes/";
    const string MatDir = "Assets/FuwaCourse/Materials/";
    const string MergedPath = MeshDir + "Gun_Merged.asset";
    const string MergedGoldPath = MeshDir + "Gun_Merged_Gold.asset";
    const string MatPath = MatDir + "Fuwa_BlowerMerged.mat";
    const string MatGoldPath = MatDir + "Fuwa_BlowerMerged_Gold.mat";

    // 部品（ヒエラルキーの名前、メッシュ、ふだんの材質、金の材質）
    static readonly (string obj, string mesh, string mat, string gold)[] Parts =
    {
        ("GunBody", "Gun_Body", "Fuwa_Blower", "Fuwa_Blower_Gold"),
        ("GunAccent", "Gun_Accent", "Fuwa_BlowerMuzzle", "Fuwa_BlowerMuzzle_Gold"),
        ("GunGrip", "Gun_Grip", "Fuwa_BlowerGrip", "Fuwa_BlowerGrip_Gold"),
        ("GunVent", "Gun_Vent", "Fuwa_BlowerVent", "Fuwa_BlowerVent_Gold"),
    };

    static Material LoadMat(string name)
    {
        return AssetDatabase.FindAssets(name + " t:Material").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault(m => m != null && m.name == name);
    }

    [MenuItem("FuwaCourse/エアガン/本体の部品を1つにまとめる（頂点の色）")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var gun = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "FuwaBlower");
        if (gun == null) return "FuwaBlower がない";
        var b = gun.GetComponent<FuwaBlower>(); if (b == null || b.visualRoot == null) return "FuwaBlower の見た目がない";
        var vis = b.visualRoot.transform;

        // うそ影（FuwaGunShadows）の真ん中：まとめる前の求め方（MeshRenderer の bounds の真ん中の平均）の位置を目印に残す。まとめると平均が変わって影がずれるので
        if (vis.Find("ShadowCenter") == null)
        {
            var rs = vis.GetComponentsInChildren<MeshRenderer>(true);
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var r in rs) { if (r == null) continue; sum += r.bounds.center; n++; }
            var sc = new GameObject("ShadowCenter").transform; sc.SetParent(vis, false);
            sc.localPosition = n > 0 ? vis.InverseTransformPoint(sum / n) : Vector3.zero;
            Undo.RegisterCreatedObjectUndo(sc.gameObject, "shadow center");
        }

        // 頂点・色を集める（部品はどれも見た目の根元と同じ位置・向き。まだ残っていれば念のためその位置で）
        var body = vis.Find("GunBody");
        var V = new List<Vector3>(); var N = new List<Vector3>(); var C = new List<Color32>(); var CG = new List<Color32>(); var T = new List<int>();
        var log = new List<string>();
        foreach (var p in Parts)
        {
            var m = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + p.mesh + ".asset"); if (m == null) return p.mesh + " がない";
            var mat = LoadMat(p.mat); var gmat = LoadMat(p.gold); if (mat == null || gmat == null) return p.mat + " か金の材質がない";
            var t = vis.Find(p.obj);
            Matrix4x4 toBody = (t != null && body != null && p.obj != "GunBody") ? body.worldToLocalMatrix * t.localToWorldMatrix : Matrix4x4.identity;
            int b0 = V.Count;
            var mv = m.vertices; var mn = m.normals;
            Color32 c = (Color)mat.GetColor("_Color"), cg = (Color)gmat.GetColor("_Color");
            for (int i = 0; i < mv.Length; i++) { V.Add(toBody.MultiplyPoint3x4(mv[i])); N.Add(toBody.MultiplyVector(mn[i]).normalized); C.Add(c); CG.Add(cg); }
            foreach (var i in m.GetTriangles(0)) T.Add(b0 + i);
            log.Add(p.obj + " " + mv.Length);
        }

        Mesh Put(string path, string name, List<Color32> col)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }   // 既にあれば中身だけ入れかえる（参照を切らない）
            mesh.Clear();
            mesh.indexFormat = V.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetColors(col); mesh.subMeshCount = 1; mesh.SetTriangles(T, 0); mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }
        var merged = Put(MergedPath, "Gun_Merged", C);
        Put(MergedGoldPath, "Gun_Merged_Gold", CG);

        // 材質：ふだん・金とも、本体の材質の写しを白にして頂点の色をかける
        Material MakeMat(string path, Material src)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
            else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
            m.SetColor("_Color", Color.white);
            m.SetFloat("_UseVertexColor", 1f); m.EnableKeyword("_FUWA_VCOLOR");
            m.enableInstancing = src.enableInstancing;
            EditorUtility.SetDirty(m);
            return m;
        }
        var mat0 = MakeMat(MatPath, LoadMat("Fuwa_Blower"));
        MakeMat(MatGoldPath, LoadMat("Fuwa_Blower_Gold"));

        // シーン：GunBody をまとめた物に、ほかの3つは消す
        Undo.RecordObject(body.GetComponent<MeshFilter>(), "merge gun"); body.GetComponent<MeshFilter>().sharedMesh = merged;
        var mr = body.GetComponent<MeshRenderer>(); Undo.RecordObject(mr, "merge gun"); mr.sharedMaterials = new[] { mat0 };
        foreach (var p in Parts) { if (p.obj == "GunBody") continue; var t = vis.Find(p.obj); if (t != null) Undo.DestroyObjectImmediate(t.gameObject); }

        SetupGold(b);
        UdonSharpEditorUtility.CopyProxyToUdon(b); EditorUtility.SetDirty(b);
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gun.scene);
        return "[銃をまとめた] " + string.Join(", ", log) + " → 頂点 " + V.Count + "・三角形 " + T.Count / 3;
    }

    // 金ピカ：まとめた GunBody は「金の材質＋金色の頂点のメッシュ」に差しかえる。消した部品の分は外す。
    // キラキラの出る面は前と同じ本体（Gun_Body）だけにする（まとめた物を指すとラッパや持ち手からも出るので）
    public static void SetupGold(FuwaBlower b)
    {
        if (b == null || b.visualRoot == null) return;
        var body = b.visualRoot.transform.Find("GunBody"); if (body == null) return;
        var mf = body.GetComponent<MeshFilter>(); var mr = body.GetComponent<MeshRenderer>();
        var merged = AssetDatabase.LoadAssetAtPath<Mesh>(MergedPath);
        if (merged == null || mf.sharedMesh != merged) return;   // まだまとめていない
        var goldMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MergedGoldPath);
        var goldMat = AssetDatabase.LoadAssetAtPath<Material>(MatGoldPath);
        var rs = new List<Renderer>(); var ms = new List<Material>();
        if (b.goldRenderers != null)
            for (int i = 0; i < b.goldRenderers.Length; i++)
            {
                var r = b.goldRenderers[i]; if (r == null || r == mr) continue;
                rs.Add(r); ms.Add(b.goldMats != null && i < b.goldMats.Length ? b.goldMats[i] : null);
            }
        rs.Add(mr); ms.Add(goldMat);
        b.goldRenderers = rs.ToArray(); b.goldMats = ms.ToArray();
        b.goldMeshFilters = new[] { mf }; b.goldMeshes = new[] { goldMesh };
        if (b.goldFx != null)
        {
            var ps = b.goldFx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                var sh = ps.shape;
                if (sh.shapeType == ParticleSystemShapeType.MeshRenderer || sh.shapeType == ParticleSystemShapeType.Mesh)
                {
                    sh.shapeType = ParticleSystemShapeType.Mesh; sh.meshRenderer = null;
                    sh.mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshDir + "Gun_Body.asset");
                    // メッシュの形はキラキラの位置から見た座標なので、本体と同じ所に置く
                    b.goldFx.transform.localPosition = body.localPosition; b.goldFx.transform.localRotation = body.localRotation; b.goldFx.transform.localScale = body.localScale;
                    EditorUtility.SetDirty(ps);
                }
            }
        }
        EditorUtility.SetDirty(b);
    }
}
