using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 「みんなで」のチルルーム（2026-10-12 てじゃ「みんなでをチルルームにする方向性で固めよう」）。協力の試作（FuwaCoopTestBuilder の CoopTest）と置き換える。
// 島の真ん中にキノコ1本：撃つたびに今の設定のふわふわが出る（FuwaChillSpawner、FuwaSharedBall を Pool 個使い回す）。
// キノコの左にスイッチ4つ（サイズ／カラー／上限数／地面に落ちたら消えるか）。設定はみんなで共通。
// スポーン・エアガンの台・ワープ・遊び方の看板は FuwaCoop の直下にあるものをそのまま使う。何度実行しても同じ結果
public static class FuwaChillRoomBuilder
{
    const string LobbyName = "FuwaVersusLobby";
    static readonly Vector3 Offset = new Vector3(0f, 0f, 65f);   // 古いみんなでロビーからの島の位置（協力の試作と同じ）
    const int Pool = 10;
    const float MushScale = 3f;                 // 巨大（1.4）も隠れられる大きさ
    static readonly Vector3 MushLocal = new Vector3(0f, 0f, 2f);
    static readonly Vector3 PanelLocal = new Vector3(-4.6f, 0f, -0.6f);   // スイッチ（キノコの左手前）

    [MenuItem("FuwaCourse/みんなで/チルルームを作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new List<string>();
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        var lobby = roots.First(g => g.name == LobbyName).transform;
        var coop = roots.First(g => g.name == "FuwaCoop").transform;
        var sys = roots.First(g => g.name == "FuwaSystem").transform;
        Vector3 ac = lobby.position + Offset;
        coop.position = ac;

        // 協力の試作・前のチルルームを片付け
        var oldGiant = coop.Find("CoopTest/GiantFluff");
        foreach (var o in new[] { coop.Find("CoopTest"), coop.Find("ChillRoom"), sys.Find("CoopCourse"), sys.Find("ChillSpawner") }) if (o != null) Object.DestroyImmediate(o.gameObject);
        var root = new GameObject("ChillRoom").transform; root.SetParent(coop, false); root.position = ac;

        // 島（古いみんなでロビーのコピー）
        foreach (var n in new[] { "VersusFloor", "VersusRing", "IslandUnder" })
        {
            var src = lobby.Find(n);
            var g = Object.Instantiate(src.gameObject, root, true); g.name = n;
            g.transform.position = src.position + Offset;
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }
        var floor = root.Find("VersusFloor");
        float floorY = floor.GetComponent<Renderer>().bounds.max.y;
        int floorLayer = floor.gameObject.layer;

        // キノコ
        var mushSrc = lobby.Find("CenterMushroom");
        var mush = Object.Instantiate(mushSrc.gameObject, root, true); mush.name = "Mushroom";
        mush.transform.position = new Vector3(ac.x, mushSrc.position.y, ac.z) + MushLocal;
        mush.transform.localScale = mushSrc.localScale * MushScale;

        // ふわふわ（ロビーの共有ふわふわのコピー）を Pool 個。待つ位置はキノコの中
        var fluffSrc = lobby.Find("SharedFluff");
        Vector3 home = mush.transform.position + Vector3.up * (fluffSrc.position.y - mushSrc.position.y) * MushScale;
        var pool = new GameObject("Fluffs").transform; pool.SetParent(root, false);
        var balls = new List<FuwaSharedBall>();
        for (int i = 0; i < Pool; i++)
        {
            var go = Object.Instantiate(fluffSrc.gameObject, pool, true); go.name = "Fluff" + i;
            go.transform.position = home; go.transform.localScale = Vector3.one * 0.3f;
            // 10個が同じ所に重なっているので、最初から動かない物にしておく（でないと始まった瞬間に物理で押し合って散らばり、その位置が待つ位置になる）
            var rb = go.GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true;
            var b = go.GetComponent<FuwaSharedBall>();
            var so = new SerializedObject(b);
            so.FindProperty("variant").boolValue = true;
            so.FindProperty("chill").objectReferenceValue = null;   // 下で係を作ってから入れる
            so.FindProperty("popLayers").intValue = 1 << floorLayer;
            so.FindProperty("fallLimit").floatValue = (home.y - floorY) + 3f;   // 島の外へ落ちたら（床の 3m 下で）キノコへ
            so.FindProperty("arenaCenter").objectReferenceValue = null;
            so.FindProperty("arenaRadius").floatValue = 0f;
            so.FindProperty("shotScale").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo(); Sync(b);
            balls.Add(b);
        }

        // 設定の係（同期するので FuwaSystem の下）
        var spGo = new GameObject("ChillSpawner"); spGo.transform.SetParent(sys, false);
        EnsureProgram("FuwaChillSpawner");
        var sp = UdonSharpUndo.AddComponent<FuwaChillSpawner>(spGo);
        sp.balls = balls.ToArray();
        foreach (var b in balls) { SetU(b, "chill", sp); Sync(b); }
        sp.language = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault();
        foreach (var sm in mush.GetComponentsInChildren<FuwaStartMushroom>(true)) { SetU(sm, "sharedBall", null); SetU(sm, "ball", null); SetU(sm, "spawner", sp); Sync(sm); }

        // エアガンで吹けるふわふわ：消えた物を外して、新しい玉を足す
        foreach (var bl in Object.FindObjectsOfType<FuwaBlower>(true))
        {
            var bso = new SerializedObject(bl); var p = bso.FindProperty("sharedBalls");
            var list = new List<Object>();
            for (int i = 0; i < p.arraySize; i++) { var o = p.GetArrayElementAtIndex(i).objectReferenceValue; if (o != null && o != oldGiant) list.Add(o); }
            foreach (var b in balls) if (!list.Contains(b)) list.Add(b);
            p.arraySize = list.Count; for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            bso.ApplyModifiedPropertiesWithoutUndo(); Sync(bl);
        }

        // スイッチ4つ（2×2）。遊び方の看板の板をコピーした横長の看板を押す
        var spawn = coop.Find("CoopSpawn");
        var howTo = coop.Find("HowToPlayVersus");
        var panel = new GameObject("Switches").transform; panel.SetParent(root, false);
        Vector3 pc = new Vector3(ac.x, floorY, ac.z) + PanelLocal;
        Vector3 look = spawn != null ? spawn.position : ac + Vector3.back * 9f;
        var face = pc - look; face.y = 0f; var rot = Quaternion.LookRotation(face.normalized);
        var labels = new TMPro.TextMeshPro[4];
        for (int k = 0; k < 4; k++)
        {
            int col = k % 2, row = k / 2;
            var pos = pc + rot * new Vector3(col == 0 ? -1.02f : 1.02f, row == 0 ? 1.75f : 1.08f, 0f);
            var lbl = MakeButton(panel, howTo, "Switch" + k, pos, rot);
            labels[k] = lbl;
            var btn = lbl.transform.parent.gameObject;
            EnsureProgram("FuwaChillButton");
            var cb = UdonSharpUndo.AddComponent<FuwaChillButton>(btn); cb.spawner = sp; cb.kind = k; UdonSharpEditorUtility.CopyProxyToUdon(cb);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(cb);
            if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; ub.interactText = new[] { "サイズを変える", "色を変える", "上限を変える", "落ちたら消えるか" }[k]; EditorUtility.SetDirty(ub); }
        }
        // 支柱（木の棒）2本
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Course_Wood.mat");
        foreach (float sx in new[] { -2.05f, 2.05f })
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder); post.name = "Post";
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.transform.SetParent(panel, false);
            post.transform.position = pc + rot * new Vector3(sx, 1.05f, 0.08f); post.transform.rotation = rot;
            post.transform.localScale = new Vector3(0.09f, 1.05f, 0.09f);
            post.GetComponent<MeshRenderer>().sharedMaterial = wood;
        }
        sp.sizeLabel = labels[0]; sp.colorLabel = labels[1]; sp.limitLabel = labels[2]; sp.popLabel = labels[3];
        UdonSharpEditorUtility.CopyProxyToUdon(sp);
        var sub = UdonSharpEditorUtility.GetBackingUdonBehaviour(sp); if (sub != null) { sub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; EditorUtility.SetDirty(sub); }

        if (howTo != null) SetHowToText(howTo.Find("Text").GetComponent<TMPro.TextMeshPro>());

        // 落ちた人の戻し：島の下の大きな板（FuwaLobbyFall）→ スポーンへ（見えないワープ）
        var backWarp = coop.Find("ReturnToHub");
        var gunSpot = coop.Find("GunStand_Versus/GunSpot") ?? spawn;
        if (backWarp != null && spawn != null)
        {
            var fallWarp = Object.Instantiate(backWarp.gameObject, root, true); fallWarp.name = "ChillFallWarp";
            foreach (var rr in fallWarp.GetComponentsInChildren<Renderer>(true)) rr.enabled = false;
            foreach (var cc in fallWarp.GetComponentsInChildren<Collider>(true)) cc.enabled = false;
            foreach (var ps in fallWarp.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
            foreach (var t in fallWarp.GetComponentsInChildren<TMPro.TextMeshPro>(true)) t.gameObject.SetActive(false);
            var fw = fallWarp.GetComponentInChildren<FuwaCourseWarp>(true); SetU(fw, "playerSpot", spawn); SetU(fw, "gunSpot", gunSpot); Sync(fw);
            var lobbyFall = lobby.Find("FallReturn").GetComponent<FuwaLobbyFall>();
            var fall = new GameObject("ChillFall"); fall.transform.SetParent(root, false); fall.transform.position = ac + new Vector3(0f, -12f, 0f);
            var fbx = fall.AddComponent<BoxCollider>(); fbx.isTrigger = true; fbx.size = new Vector3(120f, 4f, 120f);
            EnsureProgram("FuwaLobbyFall");
            var nf = UdonSharpUndo.AddComponent<FuwaLobbyFall>(fall);
            nf.returnWarp = fw; nf.fade = lobbyFall.fade; nf.fallSound = lobbyFall.fallSound; UdonSharpEditorUtility.CopyProxyToUdon(nf);
            var nub = UdonSharpEditorUtility.GetBackingUdonBehaviour(nf); if (nub != null) { nub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(nub); }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(coop.gameObject.scene);
        log.Add("chill room at " + ac + " home " + home.ToString("F2") + " floorY " + floorY.ToString("F2") + " balls " + balls.Count);
        return string.Join("\n", log);
    }

    // 遊び方の看板の板（Panel・Border）と文字（Text）をコピーした横長のボタン。文字を返す。表（-Z）を手前に
    static TMPro.TextMeshPro MakeButton(Transform parent, Transform howTo, string name, Vector3 pos, Quaternion rot)
    {
        var r = new GameObject(name).transform; r.SetParent(parent, false); r.position = pos; r.rotation = rot;
        r.localScale = Vector3.one * 0.5f;
        TMPro.TextMeshPro t = null;
        foreach (var n in new[] { "Panel", "Border", "Text" })
        {
            var src = howTo.Find(n); if (src == null) continue;
            var g = Object.Instantiate(src.gameObject, r, false); g.name = n;
            g.transform.localPosition = new Vector3(0f, 0f, src.localPosition.z); g.transform.localRotation = Quaternion.identity;
            if (n == "Text")
            {
                t = g.GetComponent<TMPro.TextMeshPro>(); t.text = name; t.alignment = TMPro.TextAlignmentOptions.Center; t.enableAutoSizing = false; t.fontSize = 3f;
                t.rectTransform.sizeDelta = new Vector2(t.rectTransform.sizeDelta.x, 1f);
            }
            else { var sr = g.GetComponent<SpriteRenderer>(); if (sr != null) sr.size = new Vector2(sr.size.x, sr.size.y * 0.32f); }
        }
        var bc = r.gameObject.AddComponent<BoxCollider>(); bc.size = new Vector3(3.8f, 1.2f, 0.15f); bc.center = new Vector3(0f, 0f, 0.03f);
        return t;
    }

    // 遊び方の看板の文字（日本語・英語の切り替えにも登録してあるので両方書き換える）
    static void SetHowToText(TMPro.TextMeshPro t)
    {
        const string ja = "<align=center><size=150%><color=#FF6B5C>みんなでひろば</color></size></align>\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>キノコの傘を撃つと、ふわふわが出てくる\n<pos=1.1em>みんなで吹いて、のんびり遊ぼう\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>スイッチで、サイズ・色・出せる数・\n<pos=1.1em>落ちたら消えるかを変えられる\n<pos=1.1em>（設定はみんなで共通）\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>巨大は重いので、みんなで押そう\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>エアガンを手元に呼ぶ：PCはEキー\n<pos=1.1em>VRは右スティックを下に倒し続ける";
        const string en = "<align=center><size=150%><color=#FF6B5C>Fluff Plaza</color></size></align>\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Shoot the mushroom cap to pop out a fluff\n<pos=1.1em>Blow them around together and relax\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Switches change the size, color,\n<pos=1.1em>max count, and whether they pop on landing\n<pos=1.1em>(shared by everyone)\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Giant ones are heavy: push together!\n<size=45%>\n</size><color=#FF6B5C>●</color><pos=1.1em>Summon your air gun: E key (PC)\n<pos=1.1em>or hold the right stick down (VR)";
        t.text = ja; EditorUtility.SetDirty(t);
        var lang = Object.FindObjectsOfType<FuwaLanguage>(true).FirstOrDefault(); if (lang == null) return;
        int i = System.Array.IndexOf(lang.texts, t); if (i < 0) return;
        var so = new SerializedObject(lang);
        so.FindProperty("ja").GetArrayElementAtIndex(i).stringValue = ja; so.FindProperty("en").GetArrayElementAtIndex(i).stringValue = en;
        so.ApplyModifiedPropertiesWithoutUndo(); Sync(lang);
    }

    static void EnsureProgram(string name)
    {
        string path = "Assets/FuwaCourse/Scripts/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(path) != null) return;
        var prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
        prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/" + name + ".cs");
        AssetDatabase.CreateAsset(prog, path); AssetDatabase.SaveAssets();
    }

    static void SetU(Object o, string field, Object v) { var s = new SerializedObject(o); var p = s.FindProperty(field); if (p != null) { p.objectReferenceValue = v; s.ApplyModifiedPropertiesWithoutUndo(); } }
    static void Sync(Object o) { if (o is UdonSharpBehaviour u) UdonSharpEditorUtility.CopyProxyToUdon(u); EditorUtility.SetDirty(o); }
}
