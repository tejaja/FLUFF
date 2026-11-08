using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;

// 見学用の竹とんぼ立て（2026-11-08 てじゃ「竹とんぼで良い気がしてきた　一人一個持てる形式、他人には見えない」→ 傘の代わり）
//  ステージ1・2のロビーの傘立てがあった所に1本ずつ。竹とんぼは自分の画面だけの物（同期なし）。持っている間に隠れるエリアが消えても消えないように FuwaSystem/Copters の下
//  傘（FuwaSystem/Umbrellas、ロビーの UmbrellaRack）はここで消す（FuwaUmbrellaBuilder・FuwaUmbrella.cs は残す）
public static class FuwaCopterBuilder
{
    const string MeshDir = "Assets/FuwaCourse/Models/Copter";
    const string MatDir = "Assets/FuwaCourse/Materials/Copter";
    public static Vector3 StandLocal = new Vector3(-5.4f, 0f, 0.9f);   // 傘立てと同じ所（銃の台の左どなり）
    // 竹とんぼの大きさ（root は持つ所＝軸の下から 1/3。+Y が羽の方）
    const float StickLen = 0.34f, StickR = 0.014f, GripFromBottom = 0.11f, BladeSpan = 0.52f, BladeW = 0.065f, BladeT = 0.01f, BladeTwist = 16f;
    const float StandBaseH = 0.1f, StandPostH = 0.75f;

    [MenuItem("FuwaCourse/よく使う/竹とんぼ立て（見学用）を作り直す（傘は消す）")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(MeshDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Models", "Copter");
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Materials", "Copter");
        var baseMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Mush_CapRed.mat");
        Material M(string name, Color c)
        {
            string p = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (!m) { m = new Material(baseMat); AssetDatabase.CreateAsset(m, p); }
            m.shader = baseMat.shader; m.CopyPropertiesFromMaterial(baseMat); m.SetColor("_Color", c); m.enableInstancing = true;
            EditorUtility.SetDirty(m); return m;
        }
        var bamboo = M("Copter_Bamboo", new Color(0.86f, 0.76f, 0.5f));
        var node = M("Copter_BambooNode", new Color(0.66f, 0.55f, 0.3f));
        var blade = M("Copter_Blade", new Color(0.93f, 0.84f, 0.6f));
        var standWood = M("Copter_StandWood", new Color(0.55f, 0.36f, 0.22f));
        var standTrim = M("Copter_StandTrim", new Color(0.95f, 0.80f, 0.45f));
        var bladeMesh = BladeMesh();

        var sys = Object.FindObjectsOfType<Transform>(true).First(t => t.name == "FuwaSystem" && t.parent == null);
        // 傘を消す
        var oldU = sys.Find("Umbrellas"); if (oldU) Object.DestroyImmediate(oldU.gameObject);
        var old = sys.Find("Copters"); if (old) Object.DestroyImmediate(old.gameObject);
        var holder = new GameObject("Copters").transform; holder.SetParent(sys, false);
        var manager = Object.FindObjectsOfType<FuwaRespawnManager>(true).First();
        EnsureProgram();

        var lobbies = new[] { ("Stage1", GameObject.Find("FuwaLobby")), ("Stage2", GameObject.Find("FuwaStage2/Lobby")) };
        var log = new System.Text.StringBuilder("[竹とんぼ] ");
        foreach (var (key, lobby) in lobbies)
        {
            if (!lobby) { log.Append(key + " ロビーがない "); continue; }
            var lt = lobby.transform;
            foreach (var n in new[] { "UmbrellaRack", "CopterStand" }) { var o = lt.Find(n); if (o) Object.DestroyImmediate(o.gameObject); }
            Vector3 w = lt.TransformPoint(StandLocal);
            float floorY = lt.position.y;
            if (Physics.Raycast(w + Vector3.up * 3f, Vector3.down, out var hit, 6f, 1 << 11 | 1, QueryTriggerInteraction.Ignore)) floorY = hit.point.y;
            var st = new GameObject("CopterStand").transform; st.SetParent(lt, false);
            st.position = new Vector3(w.x, floorY, w.z);
            st.rotation = Quaternion.LookRotation(-new Vector3(w.x - lt.position.x, 0f, w.z - lt.position.z).normalized);
            BuildStand(st, standWood, standTrim);
            // 竹とんぼ：軸の下のはしが柱の上のつつの底に入る
            float cupTop = StandBaseH + StandPostH + 0.06f;
            var pos = st.TransformPoint(new Vector3(0f, cupTop - 0.05f + GripFromBottom, 0f));
            MakeCopter(holder, key + "_Copter", pos, st.rotation, bamboo, node, blade, bladeMesh, manager);
            log.Append(key + " 床 " + floorY.ToString("F2") + " ");
        }
        FixNetworkIds();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sys.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
    }

    // 木の丸い台＋柱＋上のつつ（竹とんぼの軸を立てる）
    static void BuildStand(Transform st, Material wood, Material trim)
    {
        Cyl(st, "Base", new Vector3(0f, StandBaseH / 2f, 0f), new Vector3(0.6f, StandBaseH / 2f, 0.6f), wood, true);
        Cyl(st, "BaseTrim", new Vector3(0f, StandBaseH + 0.004f, 0f), new Vector3(0.5f, 0.004f, 0.5f), trim, false);
        Cyl(st, "Post", new Vector3(0f, StandBaseH + StandPostH / 2f, 0f), new Vector3(0.07f, StandPostH / 2f, 0.07f), wood, false);
        Cyl(st, "Cup", new Vector3(0f, StandBaseH + StandPostH + 0.03f, 0f), new Vector3(0.09f, 0.03f, 0.09f), trim, false);
    }

    static GameObject MakeCopter(Transform holder, string name, Vector3 pos, Quaternion rot, Material bamboo, Material node, Material bladeM, Mesh bladeMesh, FuwaRespawnManager manager)
    {
        var go = new GameObject(name); go.transform.SetParent(holder, false); go.transform.SetPositionAndRotation(pos, rot);
        go.layer = 13;   // Pickup
        float y0 = -GripFromBottom, y1 = StickLen - GripFromBottom;
        var stick = Cyl(go.transform, "Stick", new Vector3(0f, (y0 + y1) / 2f, 0f), new Vector3(2f * StickR, StickLen / 2f, 2f * StickR), bamboo, false); stick.gameObject.layer = 13;
        foreach (float t in new[] { 0.3f, 0.7f })
        { var n = Cyl(go.transform, "Node", new Vector3(0f, Mathf.Lerp(y0, y1, t), 0f), new Vector3(2f * StickR + 0.008f, 0.006f, 2f * StickR + 0.008f), node, false); n.gameObject.layer = 13; }
        var blades = new GameObject("Blades").transform; blades.SetParent(go.transform, false); blades.localPosition = new Vector3(0f, y1, 0f); blades.gameObject.layer = 13;
        var b = new GameObject("Blade"); b.transform.SetParent(blades, false); b.layer = 13;
        b.AddComponent<MeshFilter>().sharedMesh = bladeMesh; b.AddComponent<MeshRenderer>().sharedMaterial = bladeM;
        var hub = Cyl(blades, "Hub", new Vector3(0f, 0.004f, 0f), new Vector3(0.05f, 0.012f, 0.05f), node, false); hub.gameObject.layer = 13;
        var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var col = go.AddComponent<CapsuleCollider>(); col.direction = 1; col.center = new Vector3(0f, (y0 + y1) / 2f + 0.02f, 0f); col.height = StickLen + 0.06f; col.radius = 0.07f;
        var pk = go.AddComponent<VRCPickup>(); pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any;
        pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.pickupable = true; pk.proximity = 2f;
        pk.InteractionText = "竹とんぼ"; pk.UseText = "飛ぶ";
        var u = UdonSharpEditor.UdonSharpUndo.AddComponent<FuwaBambooCopter>(go);
        u.manager = manager; u.blades = blades;
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(u);
        var ub = UdonSharpEditor.UdonSharpEditorUtility.GetBackingUdonBehaviour(u);
        if (ub) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
        return go;
    }

    // 羽：まん中から両はしへ、少しずつねじれる板（はしは丸く細く）
    static Mesh BladeMesh()
    {
        const int N = 12;
        var v = new List<Vector3>(); var t = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want)
        {
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), want) < 0f) { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
            else { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        }
        foreach (float sg in new[] { 1f, -1f })
        {
            var sec = new List<Vector3[]>();
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N, x = sg * u * BladeSpan / 2f;
                float w = BladeW * (u < 0.85f ? 1f : Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow((u - 0.85f) / 0.15f, 2f))));
                var q = Quaternion.AngleAxis(sg * BladeTwist * u, Vector3.right);   // 外ほどねじれる（両側で回る向きにそろう）
                Vector3 P(float z, float y) => new Vector3(x, 0f, 0f) + q * new Vector3(0f, y, z);
                sec.Add(new[] { P(-w / 2f, -BladeT / 2f), P(w / 2f, -BladeT / 2f), P(w / 2f, BladeT / 2f), P(-w / 2f, BladeT / 2f) });
            }
            for (int i = 0; i < N; i++)
            {
                var a = sec[i]; var c = sec[i + 1];
                Quad(a[3], a[2], c[2], c[3], Vector3.up);
                Quad(a[0], a[1], c[1], c[0], Vector3.down);
                Quad(a[1], a[2], c[2], c[1], Vector3.forward);
                Quad(a[0], a[3], c[3], c[0], Vector3.back);
            }
            var e = sec[N]; Quad(e[0], e[1], e[2], e[3], new Vector3(sg, 0f, 0f));
        }
        string path = MeshDir + "/Copter_Blade.asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!m) { m = new Mesh(); AssetDatabase.CreateAsset(m, path); }
        m.Clear(false); m.name = "Copter_Blade";
        m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
        EditorUtility.SetDirty(m);
        return m;
    }

    static UdonSharp.UdonSharpProgramAsset EnsureProgram()
    {
        const string p = "Assets/FuwaCourse/Scripts/FuwaBambooCopter.asset";
        var a = AssetDatabase.LoadAssetAtPath<UdonSharp.UdonSharpProgramAsset>(p);
        if (a) return a;
        a = ScriptableObject.CreateInstance<UdonSharp.UdonSharpProgramAsset>();
        a.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/FuwaBambooCopter.cs");
        AssetDatabase.CreateAsset(a, p); AssetDatabase.SaveAssets();
        UdonSharp.UdonSharpProgramAsset.CompileAllCsPrograms();
        return a;
    }

    static void FixNetworkIds()
    {
        System.Type tb = null; foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) { var ty = asm.GetType("FuwaCourse2_1Builder"); if (ty != null) { tb = ty; break; } }
        tb?.GetMethod("FixNetworkIDs", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.Invoke(null, null);
    }

    static Transform Cyl(Transform p, string name, Vector3 lp, Vector3 ls, Material m, bool collider)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = name; g.transform.SetParent(p, false); g.transform.localPosition = lp; g.transform.localScale = ls;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>()); else g.layer = 11;
        return g.transform;
    }
}
