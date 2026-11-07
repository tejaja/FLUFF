using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;

// 見学用の飛べる傘の傘立て（2026-11-07 てじゃ）。ステージ1・2のロビーに 6本ずつ。傘は同期物なので FuwaSystem/Umbrellas の下（隠れるエリアに置かない）
//  形は Blender（FuwaUmbrella.fbx：UmbrellaBody＋UmbrellaCanopy、シェイプキー Closed）。FBX の向き・100倍は焼きこんだメッシュを Models/Umbrella に作り直す
public static class FuwaUmbrellaBuilder
{
    const string Fbx = "Assets/FuwaCourse/Models/FuwaUmbrella.fbx";
    const string Dir = "Assets/FuwaCourse/Models/Umbrella";
    const string MatDir = "Assets/FuwaCourse/Materials/Umbrella";
    const int PerRack = 6;
    // ロビーの中心からの位置（ロビーの local）。銃の台の左どなり
    public static Vector3 RackLocal = new Vector3(-5.4f, 0f, 0.9f);
    static readonly Color[] Colors =
    {
        new Color(0.95f, 0.38f, 0.50f), new Color(0.40f, 0.66f, 0.96f), new Color(0.40f, 0.82f, 0.62f),
        new Color(0.98f, 0.80f, 0.30f), new Color(0.70f, 0.52f, 0.92f), new Color(0.98f, 0.58f, 0.30f),
    };

    [MenuItem("FuwaCourse/よく使う/傘立て（見学用の飛ぶ傘）を作り直す")]
    public static void Build()
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
        if (!src) { Debug.LogError("[傘] FBX がない " + Fbx); return; }
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Models", "Umbrella");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Materials", "Umbrella");
        var body = Bake(src.transform.Find("UmbrellaBody"), Dir + "/UmbrellaBody.asset");
        var canopy = Bake(src.transform.Find("UmbrellaCanopy"), Dir + "/UmbrellaCanopy.asset");

        var baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Mush_CapRed.mat");
        Material M(string name, Color c)
        {
            string p = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (!m) { m = new Material(baseMat); AssetDatabase.CreateAsset(m, p); }
            else m.shader = baseMat.shader;
            m.CopyPropertiesFromMaterial(baseMat); m.SetColor("_Color", c); m.enableInstancing = true;
            EditorUtility.SetDirty(m); return m;
        }
        var wood = M("Umbrella_Wood", new Color(0.62f, 0.42f, 0.26f));
        var fluff = M("Umbrella_Fluff", new Color(1f, 0.98f, 0.96f));
        var stripe = M("Umbrella_Stripe", new Color(1f, 0.97f, 0.93f));
        var rackWood = M("Umbrella_RackWood", new Color(0.55f, 0.36f, 0.22f));
        var rackTrim = M("Umbrella_RackTrim", new Color(0.95f, 0.80f, 0.45f));
        var cans = new Material[Colors.Length]; var inners = new Material[Colors.Length];
        for (int i = 0; i < Colors.Length; i++)
        {
            cans[i] = M("Umbrella_Canopy" + i, Colors[i]);
            inners[i] = M("Umbrella_Inner" + i, Color.Lerp(Colors[i], Color.white, 0.6f));
        }

        var sys = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaSystem" && t.parent == null);
        var old = sys.Find("Umbrellas"); if (old) Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject("Umbrellas").transform; holder.SetParent(sys, false);
        var manager = Object.FindObjectsOfType<FuwaRespawnManager>(true).First();
        var prog = EnsureProgram();

        var lobbies = new[] { ("Stage1", GameObject.Find("FuwaLobby")), ("Stage2", GameObject.Find("FuwaStage2/Lobby")) };
        var log = new System.Text.StringBuilder("[傘] ");
        foreach (var (key, lobby) in lobbies)
        {
            if (!lobby) { log.Append(key + " ロビーがない "); continue; }
            var lt = lobby.transform;
            var oldRack = lt.Find("UmbrellaRack"); if (oldRack) Object.DestroyImmediate(oldRack.gameObject);
            // 床の高さ
            Vector3 w = lt.TransformPoint(RackLocal);
            float floorY = lt.position.y;
            if (Physics.Raycast(w + Vector3.up * 3f, Vector3.down, out var hit, 6f, 1 << 11 | 1, QueryTriggerInteraction.Ignore)) floorY = hit.point.y;
            var rack = new GameObject("UmbrellaRack").transform; rack.SetParent(lt, false);
            rack.position = new Vector3(w.x, floorY, w.z);
            rack.rotation = Quaternion.LookRotation(-new Vector3(w.x - lt.position.x, 0f, w.z - lt.position.z).normalized);   // 前（+Z）がロビーのまん中向き
            BuildRack(rack, rackWood, rackTrim);
            for (int i = 0; i < PerRack; i++)
            {
                float a = (i + 0.5f) / PerRack * 360f;
                var spot = rack.TransformPoint(Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 0.42f) + Vector3.up * BaseH);
                var u = MakeUmbrella(holder, key + "_Umbrella" + i, spot + Vector3.up * 0.14f, rack.rotation * Quaternion.Euler(0f, a, 0f),
                    body, canopy, new[] { wood, fluff }, new[] { cans[i % cans.Length], inners[i % inners.Length], stripe }, manager, prog);
            }
            log.Append(key + " 床 " + floorY.ToString("F2") + " ");
        }
        FixNetworkIds();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sys.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
    }

    const float BaseH = 0.12f;
    // 木の丸い台＋まん中の柱＋傘をささえる輪（6つ）
    static void BuildRack(Transform rack, Material wood, Material trim)
    {
        Cyl(rack, "Base", new Vector3(0f, BaseH / 2f, 0f), new Vector3(1.15f, BaseH / 2f, 1.15f), wood, true);
        Cyl(rack, "BaseTrim", new Vector3(0f, BaseH + 0.005f, 0f), new Vector3(1.0f, 0.005f, 1.0f), trim, false);
        Cyl(rack, "Post", new Vector3(0f, BaseH + 0.3f, 0f), new Vector3(0.09f, 0.3f, 0.09f), wood, false);
        Cyl(rack, "PostCap", new Vector3(0f, BaseH + 0.62f, 0f), new Vector3(0.16f, 0.03f, 0.16f), trim, false);
        for (int i = 0; i < PerRack; i++)
        {
            float a = (i + 0.5f) / PerRack * 360f; var q = Quaternion.Euler(0f, a, 0f);
            var arm = Box(rack, "Arm" + i, q * new Vector3(0f, 0f, 0.21f) + Vector3.up * (BaseH + 0.26f), new Vector3(0.04f, 0.03f, 0.36f), wood); arm.localRotation = q;
            // 輪（傘の軸をささえる）
            var ring = new GameObject("Ring" + i).transform; ring.SetParent(rack, false); ring.localPosition = q * new Vector3(0f, 0f, 0.42f) + Vector3.up * (BaseH + 0.26f);
            for (int k = 0; k < 8; k++)
            {
                var b = Box(ring, "Seg" + k, Quaternion.Euler(0f, k * 45f, 0f) * new Vector3(0f, 0f, 0.045f), new Vector3(0.04f, 0.03f, 0.02f), trim);
                b.localRotation = Quaternion.Euler(0f, k * 45f, 0f);
            }
        }
    }

    static GameObject MakeUmbrella(Transform holder, string name, Vector3 pos, Quaternion rot, Mesh body, Mesh canopy, Material[] bodyMats, Material[] canMats, FuwaRespawnManager manager, UdonSharp.UdonSharpProgramAsset prog)
    {
        var go = new GameObject(name); go.transform.SetParent(holder, false); go.transform.position = pos; go.transform.rotation = rot;
        go.layer = 13;   // Pickup
        var b = new GameObject("Body"); b.transform.SetParent(go.transform, false); b.layer = 13;
        b.AddComponent<MeshFilter>().sharedMesh = body; b.AddComponent<MeshRenderer>().sharedMaterials = bodyMats;
        var c = new GameObject("Canopy"); c.transform.SetParent(go.transform, false); c.layer = 13;
        var smr = c.AddComponent<SkinnedMeshRenderer>(); smr.sharedMesh = canopy; smr.sharedMaterials = canMats; smr.SetBlendShapeWeight(0, 100f);
        smr.localBounds = new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(1.2f, 0.8f, 1.2f)); smr.updateWhenOffscreen = false;
        var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var col = go.AddComponent<CapsuleCollider>(); col.direction = 1; col.center = new Vector3(0.02f, 0.35f, 0f); col.height = 1.05f; col.radius = 0.1f;
        var pk = go.AddComponent<VRCPickup>();
        pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.DisallowTheft = true; pk.pickupable = true; pk.proximity = 2f;
        pk.InteractionText = "ふわふわ傘"; pk.UseText = "飛ぶ";
        var os = go.AddComponent<VRCObjectSync>(); os.AllowCollisionOwnershipTransfer = false;
        var u = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaUmbrella>(go);
        u.manager = manager; u.canopy = smr;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(u);
        return go;
    }

    static UdonSharp.UdonSharpProgramAsset EnsureProgram()
    {
        const string p = "Assets/FuwaCourse/Scripts/FuwaUmbrella.asset";
        var a = AssetDatabase.LoadAssetAtPath<UdonSharp.UdonSharpProgramAsset>(p);
        if (a) return a;
        a = ScriptableObject.CreateInstance<UdonSharp.UdonSharpProgramAsset>();
        a.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/FuwaUmbrella.cs");
        AssetDatabase.CreateAsset(a, p); AssetDatabase.SaveAssets();
        UdonSharp.UdonSharpProgramAsset.CompileAllCsPrograms();
        return a;
    }

    static void FixNetworkIds()
    {
        System.Type tb = null; foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) { var ty = asm.GetType("FuwaCourse2_1Builder"); if (ty != null) { tb = ty; break; } }
        tb?.GetMethod("FixNetworkIDs", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.Invoke(null, null);
    }

    // FBX の子（向き・大きさつき）の形を、そのまま焼きこんだメッシュにする（シェイプキーも）
    static Mesh Bake(Transform node, string path)
    {
        var mf = node.GetComponent<MeshFilter>(); var srcM = mf ? mf.sharedMesh : node.GetComponent<SkinnedMeshRenderer>().sharedMesh;
        var mtx = Matrix4x4.TRS(Vector3.zero, node.localRotation, node.localScale);
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!m) { m = new Mesh(); AssetDatabase.CreateAsset(m, path); }
        m.Clear(false); m.name = System.IO.Path.GetFileNameWithoutExtension(path);
        var v = srcM.vertices.Select(x => mtx.MultiplyPoint3x4(x)).ToArray();
        var nrm = srcM.normals.Select(x => mtx.MultiplyVector(x).normalized).ToArray();
        m.indexFormat = v.Length > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        m.vertices = v; m.normals = nrm; if (srcM.uv != null && srcM.uv.Length == v.Length) m.uv = srcM.uv;
        m.subMeshCount = srcM.subMeshCount;
        for (int s = 0; s < srcM.subMeshCount; s++) m.SetTriangles(srcM.GetTriangles(s), s);
        for (int k = 0; k < srcM.blendShapeCount; k++)
        {
            var dv = new Vector3[v.Length]; var dn = new Vector3[v.Length]; var dt = new Vector3[v.Length];
            srcM.GetBlendShapeFrameVertices(k, 0, dv, dn, dt);
            m.AddBlendShapeFrame(srcM.GetBlendShapeName(k), 100f, dv.Select(x => mtx.MultiplyVector(x)).ToArray(), dn.Select(x => mtx.rotation * x).ToArray(), null);
        }
        m.RecalculateBounds();
        EditorUtility.SetDirty(m);
        return m;
    }

    static Transform Cyl(Transform p, string name, Vector3 lp, Vector3 ls, Material m, bool collider)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = name; g.transform.SetParent(p, false); g.transform.localPosition = lp; g.transform.localScale = ls;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>()); else g.layer = 11;
        return g.transform;
    }
    static Transform Box(Transform p, string name, Vector3 lp, Vector3 ls, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.SetParent(p, false); g.transform.localPosition = lp; g.transform.localScale = ls;
        g.GetComponent<Renderer>().sharedMaterial = m; Object.DestroyImmediate(g.GetComponent<Collider>());
        return g.transform;
    }
}
