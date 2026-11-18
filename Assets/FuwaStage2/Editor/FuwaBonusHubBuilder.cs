using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2026-11-18 てじゃ「おまけゴール→ゴール表示はなし、花火とかキノコは出る、ポータルが開く→おまけ本島行き」「場所は遊び場みたいにどっかに飛ばす」「B（記念の島）」
//  おまけ本島（記念の島）：ステージ2ロビーの島を大きくして別の場所に置く（FuwaAreaVisibility の area 4＝bonusObjects、ふだんは消えている）。
//  ・まわりに全10コースの記念の品（各コースの物の見た目だけを写して縮めた物）を、コースの道の色の台にのせて並べる
//  ・まん中に「Thank you for playing!」＋自己ベスト一覧のボード（おまけのゴールの ClearBanner をこちらへ移す）、左右にきんピカ銃の台・金ピカふわふわ
//  ・奥にステージ2ロビーへのポータル
//  ・おまけのゴール：文字（ClearBanner）は出さない。花火・キノコのあとに開くワープ（GoalWarp）の行き先をここに
//  ・BGM：ゴール後の曲（BGM_bonus_after）をここでもそのまま流す（starts にここの BallPark）
//  FuwaRewardIslandBuilder（ごほうびの島を作り直す）の最後からも呼ぶ。何度実行してもよい
public static class FuwaBonusHubBuilder
{
    const string RootName = "FuwaBonusHub";
    public static Vector3 Pos = new Vector3(-200f, 120f, -200f);
    public static float Scale = 1.4f, ItemRing = 8.0f, PedR = 1.1f, PedH = 0.45f, ItemSize = 1.5f, BoardY = 4.3f;

    [MenuItem("FuwaCourse/ステージ2/おまけ本島（記念の島）を作り直す")]
    public static void Menu() { Debug.Log(Build()); }

    static Transform Find(string path) { var p = path.Split(new[] { '/' }, 2); var r = GameObject.Find(p[0]); return r == null ? null : p.Length > 1 ? r.transform.Find(p[1]) : r.transform; }

    public static string Build()
    {
        var log = new System.Text.StringBuilder("[おまけ本島] ");
        var lobby = Find("FuwaStage2/Lobby"); var reward = Find("FuwaRewardIsland");
        if (!lobby || !reward) return "ステージ2ロビー／ごほうびの島がない";
        var old = GameObject.Find(RootName);
        var root = new GameObject(RootName).transform; root.position = Pos;
        var lc = lobby.Find("LobbySpawn").position; float floorTop = lobby.Find("LobbyFloor").GetComponent<Renderer>().bounds.max.y;

        // ---- 島（ステージ2ロビーの床・ふち・柵・下の岩・まん中の丸を、ロビーのまん中を原点に Scale 倍）----
        var island = new GameObject("Island").transform; island.SetParent(root, false);
        foreach (var nm in new[] { "LobbyFloor", "LobbyRing", "Fence", "FenceCollider", "IslandUnder", "CenterRim", "CenterGround" })
        {
            var src = lobby.Find(nm); if (!src) continue;
            var g = Object.Instantiate(src.gameObject, island, true); g.name = nm;
            FuwaStage2Builder.StripBehaviour(g);
            var off = src.position - new Vector3(lc.x, floorTop, lc.z);
            g.transform.position = Pos + off * Scale; g.transform.localScale = src.lossyScale * Scale;
            foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.lightmapIndex = -1;
        }
        float R = 8f * Scale;   // 床の半径（ロビーは16m）

        // ---- 立ち位置・ふわふわ・銃 ----
        var spawn = new GameObject("Spawn").transform; spawn.SetParent(root, false);
        spawn.position = Pos + Vector3.back * (R - 2.2f) + Vector3.up * 0.1f; spawn.rotation = Quaternion.LookRotation(Vector3.forward);
        var ballPark = new GameObject("BallPark").transform; ballPark.SetParent(root, false);
        ballPark.position = Pos + Vector3.back * (R - 3.4f) + Vector3.right * 1.2f + Vector3.up * 1.2f;
        var gunSpot = new GameObject("GunSpot").transform; gunSpot.SetParent(root, false);
        gunSpot.position = Pos + Vector3.back * (R - 2.6f) + Vector3.left * 1.0f + Vector3.up * 1.0f; gunSpot.rotation = spawn.rotation;

        // ---- まん中：ボード（おまけのゴールの ClearBanner を移す）・きんピカ台・金ピカふわふわ ----
        var goal = reward.Find("Goal");
        var oldBoard = old ? old.transform.Find("Board") : null;
        var banner = goal ? goal.Find("ClearBanner") : null;
        if (!banner && oldBoard) banner = oldBoard;
        if (banner)
        {
            banner.SetParent(root, true); banner.name = "Board";
            banner.position = Pos + Vector3.forward * 1.5f + Vector3.up * BoardY; banner.rotation = Quaternion.LookRotation(Vector3.forward);
            banner.gameObject.SetActive(true);
            var g = goal ? goal.GetComponentInChildren<FuwaGoal>(true) : null;
            if (g) { var so = new SerializedObject(g); so.FindProperty("clearBanner").objectReferenceValue = null; so.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(g); }
            log.Append("ボード ");
        }
        // ボードがこっちを向いているか（ゴールの看板は裏から見る向きのことがある）は撮って確認
        var ra = goal ? goal.GetComponent<FuwaRewardAfterGoal>() : null;
        var stands = new List<Transform>();
        foreach (var nm in new[] { "GoldGunStand", "GoldFluffStand" })
        {
            var t = goal ? goal.Find(nm) : null; if (!t && old) t = old.transform.Find(nm);
            if (!t) continue;
            t.SetParent(root, true); t.gameObject.SetActive(true); stands.Add(t);
        }
        if (stands.Count > 0) { stands[0].position = Pos + new Vector3(-3.2f, 0f, -0.6f); stands[0].rotation = Quaternion.LookRotation(Vector3.back); }
        if (stands.Count > 1) { stands[1].position = Pos + new Vector3(3.2f, 0f, -0.6f); stands[1].rotation = Quaternion.LookRotation(Vector3.back); }
        if (ra) Undo.DestroyObjectImmediate(ra);   // ゴールの後に台を出す係はいらない（台はいつもここにある）

        // ---- 奥：ステージ2ロビーへのポータル（ごほうびの島のスタートのワープを写す）----
        var st = reward.Find("Start");
        var portal = new GameObject("ToLobby").transform; portal.SetParent(root, false);
        portal.position = Pos + Vector3.forward * (R - 2.4f); portal.rotation = Quaternion.LookRotation(Vector3.back);
        if (st)
        {
            var vis = st.Cast<Transform>().Where(t => t.name.StartsWith("WarpToLobby") && !t.name.EndsWith("_Trigger") && !t.name.EndsWith("_Trigger_Gauge")).ToList();
            var refT = st.Find("WarpToLobby");
            foreach (var v in vis)
            {
                var g = Object.Instantiate(v.gameObject, portal, false); g.name = v.name;
                g.transform.localPosition = v.localPosition - (refT ? refT.localPosition : Vector3.zero); g.transform.localRotation = v.localRotation; g.transform.localScale = v.localScale;
                FuwaStage2Builder.StripBehaviour(g);
            }
            var trig = st.Find("WarpToLobby_Trigger"); var gauge = st.Find("WarpToLobby_Trigger_Gauge");
            if (trig && gauge)
            {
                var w = (FuwaCourseWarp)FuwaStage2Builder.MakeWarp(portal, "WarpToLobby", trig.gameObject, gauge.gameObject,
                    lobby.Find("BallPark"), reward.Find("LobbySpawn_FromReward") ?? lobby.Find("LobbySpawn"), lobby.Find("GunStand_Lobby/GunSpot"), 2);
                foreach (var n in new[] { "WarpToLobby_Trigger", "WarpToLobby_Trigger_Gauge" })
                {
                    var c = portal.Find(n); var s = st.Find(n); if (!c || !s) continue;
                    c.localPosition = s.localPosition - (refT ? refT.localPosition : Vector3.zero);
                }
                // 足もとの台（ごほうびの島のワープの台）。うずはその上に（床とチラつかないように）
                var wg = st.Find("StartArea/WarpGround");
                if (wg && refT)
                {
                    var g = Object.Instantiate(wg.gameObject, portal, true); g.name = "WarpGround"; FuwaStage2Builder.StripBehaviour(g);
                    float top = wg.GetComponent<Renderer>().bounds.max.y, swirlY = refT.position.y;
                    g.transform.position = portal.position + (wg.position - refT.position);
                    foreach (var r in g.GetComponentsInChildren<Renderer>(true)) r.lightmapIndex = -1;
                    // 台の上面（＝うずの高さの近く）が島の床より 15cm 上に出るように、ポータルごと持ち上げる
                    float rise = Pos.y + 0.15f - (portal.position.y + (top - swirlY));
                    if (rise > 0f) portal.position += Vector3.up * rise;
                }
                log.Append("戻りポータル ");
            }
        }

        // ---- 記念の品（コースの見た目だけ写して縮める）----
        var items = new (string course, string src, float size, string pathRoot)[] {
            ("かぜのおか", "FuwaCourse/Deco/DandelionIslands/Island1/DandelionLOD", 2.4f, "FuwaCourse"),
            ("いわのみち", "FuwaCourse2/Gimmick_Boulders/Boulder0", 1.3f, "FuwaCourse2"),
            ("やみのもり", "@FuwaCourse3/InkCap", 2.4f, "FuwaCourse3"),
            ("まもののす", "FuwaCourse4/Enemies/Enemy1", 1.3f, "FuwaCourse4"),
            ("きのこのたに", "FuwaCourse5/Mushrooms/CapStep1", 2.2f, "FuwaCourse5"),
            ("2–1", "@FuwaCourse2_1/ShellTop", 1.6f, "2_1"),
            ("2–2", "FuwaCourse2_2/Fire/LavaRock0/Rock", 1.4f, "2_2"),
            ("2–3", "FuwaCourse2_3/Obstacles/IceRock1", 1.6f, "2_3"),
            ("2–4", "FuwaCourse2_4/Gimmicks/HoleGears/HoleGears_Drive_z18_Frame/HoleGears_Drive_z18", 2.0f, "2_4"),
            ("2–5", "FuwaCourse2_5/Islands/I1_StatueHill/Run1/ArchGate/ColumnL", 2.4f, "2_5"),
        };
        var holder = new GameObject("Souvenirs").transform; holder.SetParent(root, false);
        var angs = new List<float>();
        for (int k = 0; k < 5; k++) angs.Add(Mathf.Lerp(-150f, -30f, k / 4f));   // 左側（スタートから見て）
        for (int k = 0; k < 5; k++) angs.Add(Mathf.Lerp(30f, 150f, k / 4f));     // 右側
        var cyl = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            Transform src;
            if (it.src.StartsWith("@")) { var p = it.src.Substring(1).Split('/'); var r0 = GameObject.Find(p[0]); src = r0 ? r0.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == p[1] && t.GetComponent<MeshRenderer>()) : null; if (src && it.src.Contains("ShellTop")) src = src.parent.parent; }
            else src = Find(it.src);
            float a = angs[i] * Mathf.Deg2Rad;
            var at = Pos + new Vector3(Mathf.Sin(a), 0f, -Mathf.Cos(a)) * ItemRing;
            var slot = new GameObject("Item" + (i + 1)).transform; slot.SetParent(holder, false);
            slot.position = at; var face = Pos - at; face.y = 0; slot.rotation = Quaternion.LookRotation(face.normalized);
            // 台（コースの道の色）
            var ped = new GameObject("Pedestal", typeof(MeshFilter), typeof(MeshRenderer), typeof(CapsuleCollider)).transform; ped.SetParent(slot, false);
            ped.localPosition = Vector3.up * PedH * 0.5f;
            var cb = cyl.bounds.size; ped.localScale = new Vector3(PedR * 2f / cb.x, PedH / cb.y, PedR * 2f / cb.z);
            ped.GetComponent<MeshFilter>().sharedMesh = cyl;
            ped.GetComponent<MeshRenderer>().sharedMaterial = PathMat(it.pathRoot);
            var cc = ped.GetComponent<CapsuleCollider>(); cc.direction = 1; cc.radius = cb.x * 0.5f; cc.height = cb.y; cc.center = cyl.bounds.center;
            ped.gameObject.layer = 11;
            if (!src) { log.Append("（" + it.course + " の元がない）"); continue; }
            var vis = CopyVisual(src, slot);
            Fit(vis, slot, it.size * ItemSize, PedH);
            log.Append(it.course + " ");
        }

        // ---- エリアの出し入れ・ワープ・BGM ----
        var av = Object.FindObjectsOfType<FuwaAreaVisibility>(true).First();
        {
            var so = new SerializedObject(av); var arr = so.FindProperty("bonusObjects");
            for (int i = arr.arraySize - 1; i >= 0; i--) if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null || arr.GetArrayElementAtIndex(i).objectReferenceValue == (old ? old : null)) arr.DeleteArrayElementAtIndex(i);
            arr.arraySize++; arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = root.gameObject;
            so.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(av);
        }
        var gw = goal ? goal.Find("GoalWarp") : null;
        var gwt = gw ? gw.GetComponentsInChildren<FuwaCourseWarp>(true).FirstOrDefault() : null;
        if (gwt)
        {
            var so = new SerializedObject(gwt);
            so.FindProperty("courseStart").objectReferenceValue = ballPark;
            so.FindProperty("playerSpot").objectReferenceValue = spawn;
            so.FindProperty("gunSpot").objectReferenceValue = gunSpot;
            so.FindProperty("area").intValue = 4; so.FindProperty("stage").intValue = 0; so.FindProperty("homeAction").intValue = 0; so.FindProperty("timed").boolValue = false;
            so.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(gwt);
            foreach (var t in gw.GetComponentsInChildren<TMPro.TMP_Text>(true)) { t.text = "<size=80%>きねんのしま</size>"; EditorUtility.SetDirty(t); }
            log.Append("ゴールのワープ→本島 ");
        }
        var bgm = Object.FindObjectsOfType<FuwaBgm>(true).FirstOrDefault();
        if (bgm)
        {
            var so = new SerializedObject(bgm); var tracks = so.FindProperty("tracks"); var starts = so.FindProperty("starts");
            for (int i = 0; i < tracks.arraySize && i < starts.arraySize; i++)
            {
                var a = tracks.GetArrayElementAtIndex(i).objectReferenceValue as AudioSource;
                if (a && a.name == "BGM_bonus_after") { starts.GetArrayElementAtIndex(i).objectReferenceValue = ballPark; log.Append("BGM "); }
            }
            so.ApplyModifiedProperties(); UdonSharpEditorUtility.CopyProxyToUdon(bgm);
        }
        if (old) Object.DestroyImmediate(old);
        root.gameObject.SetActive(false);   // ふだんは消えている（area 4 の時だけ）
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return log.ToString();
    }

    static Material PathMat(string key)
    {
        if (key.Contains("_")) return AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course" + key.Replace("2_", "2_") + "_PathA.mat") ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaStage2/Materials/Course2_" + key.Substring(2) + "_PathA.mat");
        var pm = GameObject.Find(key)?.transform.Find("PathMesh")?.GetComponent<Renderer>();
        return pm ? pm.sharedMaterial : AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course1_PathA.mat");
    }

    // 見た目だけを写す：形のある部品（MeshRenderer）だけ、LODは一番細かい物だけ、影・模様・ベール・軽い版などは除く
    static Transform CopyVisual(Transform src, Transform parent)
    {
        var holder = new GameObject("Visual").transform; holder.SetParent(parent, false);
        holder.SetPositionAndRotation(src.position, src.rotation);
        var lodOut = new HashSet<Renderer>();
        foreach (var lg in src.GetComponentsInChildren<LODGroup>(true)) { var lods = lg.GetLODs(); for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r) lodOut.Add(r); }
        string[] badName = { "IslandTop", "IslandUnder", "Lite", "_Far", "Shadow", "Decal", "Highlight", "Veil", "Trigger", "Gauge", "Marker", "Bubble", "Alert", "Sleep", "Text", "Label", "Halo" };
        string[] badShader = { "Decal", "Veil", "Highlight", "Invisible", "Shadow", "Glow", "Mist" };
        foreach (var r in src.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!r.enabled || lodOut.Contains(r)) continue;
            if (badName.Any(b => r.name.Contains(b))) continue;
            if (r.sharedMaterials.Any(m => m && badShader.Any(b => m.shader.name.Contains(b)))) continue;
            // 非表示の物（寝てる目など）は除く。src から上の非表示（エリアの出し入れ）は気にしない
            bool hidden = false; for (var t = r.transform; t && t != src.parent; t = t.parent) if (!t.gameObject.activeSelf) { hidden = true; break; }
            if (hidden) continue;
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
            var g = new GameObject(r.name, typeof(MeshFilter), typeof(MeshRenderer));
            g.transform.SetParent(holder, true);
            g.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
            g.transform.localScale = DivideScale(r.transform.lossyScale, holder.lossyScale);
            g.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var nr = g.GetComponent<MeshRenderer>(); nr.sharedMaterials = r.sharedMaterials;
            nr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; nr.receiveShadows = r.receiveShadows;
            nr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        }
        return holder;
    }
    static Vector3 DivideScale(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);

    // いちばん長い辺が size になるよう縮めて、台の上（slot の上 pedH）に底をのせ、真ん中をそろえる
    static void Fit(Transform vis, Transform slot, float size, float pedH)
    {
        var rs = vis.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
        Bounds B() { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
        var b0 = B(); float m = Mathf.Max(b0.size.x, b0.size.y, b0.size.z);
        float k = size / Mathf.Max(0.01f, m);
        // 向き：元の向きのまま、台の向き（まん中を向く）に合わせて回す
        vis.SetParent(slot, true);
        vis.localScale = vis.localScale * k;
        var b = B();
        vis.position += new Vector3(slot.position.x - b.center.x, slot.position.y + pedH - b.min.y, slot.position.z - b.center.z);
    }
}
