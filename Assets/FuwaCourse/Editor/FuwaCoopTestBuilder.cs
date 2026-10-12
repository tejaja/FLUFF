using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 「みんなで」協力の試作場（2026-10-12）：巨大ふわふわをみんなで押してゴールへ（FuwaCoopGiant）。
// みんなでロビーの +Z 側 65m（色ゴールの会場 Arena の反対側）に島を浮かべる。島・キノコはみんなでロビーのものをコピー、
// 巨大ふわふわはロビーの共有ふわふわ（FuwaSharedBall）をコピーして大きくする。ロビーとの行き来はワープ床（ToSolo のコピー）。
// 何度実行しても同じ結果
public static class FuwaCoopTestBuilder
{
    const string LobbyName = "FuwaVersusLobby";
    static readonly Vector3 Offset = new Vector3(0f, 0f, 65f);
    const float GiantScale = 1.4f;          // ロビーの共有ふわふわは 0.3
    const float MushScale = 3f;             // スタートのキノコも大きく
    const string ProgramPath = "Assets/FuwaCourse/Scripts/FuwaCoopGiant.asset";

    [MenuItem("FuwaCourse/みんなで/協力の試作場（巨大ふわふわ）を作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new List<string>();
        var lobby = GameObject.Find(LobbyName).transform;
        Vector3 lc = lobby.position, ac = lc + Offset;
        var old = lobby.Find("CoopTest"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("CoopTest").transform; root.SetParent(lobby, false); root.position = ac;

        // 島
        foreach (var n in new[] { "VersusFloor", "VersusRing", "IslandUnder", "FallReturn" })
        {
            var src = lobby.Find(n);
            var g = Object.Instantiate(src.gameObject, root, true); g.name = n;
            g.transform.position = src.position + Offset;
            foreach (var t in g.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic);
        }
        float floorY = root.Find("VersusFloor").GetComponent<Renderer>().bounds.max.y;

        // スタートのキノコ（島の手前寄り）と巨大ふわふわ
        var mushSrc = lobby.Find("CenterMushroom");
        var mush = Object.Instantiate(mushSrc.gameObject, root, true); mush.name = "StartMushroom";
        mush.transform.position = mushSrc.position + Offset + new Vector3(0f, 0f, -6f);
        mush.transform.localScale = mushSrc.localScale * MushScale;   // 巨大ふわふわが隠れられる大きさに（足もとを基準に大きく）
        var fluffSrc = lobby.Find("SharedFluff");
        var giantGo = Object.Instantiate(fluffSrc.gameObject, root, true); giantGo.name = "GiantFluff";
        giantGo.transform.localScale = Vector3.one * GiantScale;
        // キノコの中の待つ位置：ロビーのふわふわのキノコの根元からの高さを、キノコを大きくしたぶん上げる
        float r1 = GiantScale * 0.5f;
        giantGo.transform.position = mush.transform.position + Vector3.up * (fluffSrc.position.y - mushSrc.position.y) * MushScale;
        var giant = giantGo.GetComponent<FuwaSharedBall>();
        var so = new SerializedObject(giant);
        so.FindProperty("popLayers").intValue = 0;                 // 床ではじけない
        so.FindProperty("hitPadding").floatValue = r1 - 0.1f;       // ふちに当たった風も効くように
        so.FindProperty("shotScale").floatValue = 0.3f;             // FuwaCoopGiant が人数で書き換える
        so.FindProperty("releasePush").floatValue = 1.2f;           // キノコから出る時も重め
        so.FindProperty("releaseUp").floatValue = 1.6f;
        so.FindProperty("fallLimit").floatValue = 8f;
        so.FindProperty("arenaCenter").objectReferenceValue = null;
        so.FindProperty("arenaRadius").floatValue = 0f;
        so.ApplyModifiedPropertiesWithoutUndo(); Sync(giant);
        foreach (var sm in mush.GetComponentsInChildren<FuwaStartMushroom>(true)) { SetU(sm, "sharedBall", giant); Sync(sm); }
        // エアガンの「みんなで吹くふわふわ」に足す
        foreach (var bl in Object.FindObjectsOfType<FuwaBlower>(true))
        {
            var bso = new SerializedObject(bl); var p = bso.FindProperty("sharedBalls");
            var list = new List<Object>();
            for (int i = 0; i < p.arraySize; i++) { var o = p.GetArrayElementAtIndex(i).objectReferenceValue; if (o != null) list.Add(o); }
            if (!list.Contains(giant)) list.Add(giant);
            p.arraySize = list.Count; for (int i = 0; i < list.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            bso.ApplyModifiedPropertiesWithoutUndo(); Sync(bl);
        }

        // ゴール：島の奥の床に光る円＋旗
        var goal = new GameObject("Goal").transform; goal.SetParent(root, false); goal.position = new Vector3(ac.x, floorY, ac.z + 8.5f);
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder); disc.name = "Circle";
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.transform.SetParent(goal, false); disc.transform.localPosition = Vector3.up * 0.02f; disc.transform.localScale = new Vector3(4.4f, 0.01f, 4.4f);
        disc.GetComponent<MeshRenderer>().sharedMaterial = GoalMat();
        var flag = GameObject.Find("FuwaCourse2").transform.Find("Goal");   // 旗の代わりにゴールの花火を借りる
        ParticleSystem fx = null;
        var fxSrc = flag != null ? flag.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GoalFx") : null;
        if (fxSrc != null) { var f = Object.Instantiate(fxSrc.gameObject, goal, false); f.name = "GoalFx"; f.transform.localPosition = Vector3.up * 0.5f; fx = f.GetComponentInChildren<ParticleSystem>(true); }

        // 看板（人数・ゴール回数）：ロビーの遊び方の看板の文字をコピー、島の奥に
        var txtSrc = lobby.Find("HowToPlayVersus/Text").GetComponent<TMPro.TextMeshPro>();
        var bRoot = new GameObject("Board").transform; bRoot.SetParent(root, false); bRoot.position = new Vector3(ac.x, floorY + 4.5f, ac.z + 11f);
        bRoot.rotation = Quaternion.LookRotation(Vector3.forward);
        var board = Object.Instantiate(txtSrc.gameObject, bRoot, false).GetComponent<TMPro.TextMeshPro>(); board.name = "Text";
        board.transform.localPosition = Vector3.zero; board.transform.localRotation = Quaternion.identity;
        board.text = "みんなで運ぼう";

        // ワープ：ロビー → 島、島 → ロビー
        var spawn = new GameObject("CoopSpawn").transform; spawn.SetParent(root, false);
        spawn.position = new Vector3(ac.x, floorY + 0.05f, ac.z - 10f); spawn.rotation = Quaternion.LookRotation(Vector3.forward);
        var gunSpot = new GameObject("CoopGunSpot").transform; gunSpot.SetParent(root, false);
        gunSpot.position = spawn.position + new Vector3(1.0f, 1.0f, 0.8f);
        var toSolo = lobby.Find("ToSolo");
        var versusSpawn = lobby.Find("VersusSpawn");
        var wGo = MakeWarp(toSolo, lobby, "ToCoopTest", toSolo.position + new Vector3(-1.6f, 0f, -3.2f), spawn, gunSpot, "協力（試作）", "Co-op (test)");
        var vGun = toSolo.GetComponentInChildren<FuwaCourseWarp>(true).gunSpot;
        var back = MakeWarp(toSolo, root, "BackToVersus", new Vector3(ac.x - 2.5f, toSolo.position.y - lc.y + ac.y, ac.z - 11.5f), versusSpawn, vGun, "もどる", "Back");

        // 動かす係
        var holder = new GameObject("CoopGiant"); holder.transform.SetParent(root, false);
        var prog = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(ProgramPath);
        if (prog == null)
        {
            prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/FuwaCoopGiant.cs");
            AssetDatabase.CreateAsset(prog, ProgramPath); AssetDatabase.SaveAssets();
        }
        var cg = UdonSharpUndo.AddComponent<FuwaCoopGiant>(holder);
        cg.giant = giant; cg.arenaCenter = root; cg.arenaRadius = 16f; cg.goal = goal; cg.goalFx = fx; cg.board = board;
        UdonSharpEditorUtility.CopyProxyToUdon(cg);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(cg);
        if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.Manual; EditorUtility.SetDirty(ub); }   // ゴールの合図（ネットワークイベント）を受けるため

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lobby.gameObject.scene);
        log.Add("coop test at " + ac + " giant " + giantGo.transform.position.ToString("F2") + " warp " + wGo.position.ToString("F1"));
        return string.Join("\n", log);
    }

    static Transform MakeWarp(Transform src, Transform parent, string name, Vector3 pos, Transform playerSpot, Transform gunSpot, string ja, string en)
    {
        var old = parent.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject);
        var g = Object.Instantiate(src.gameObject, parent, true); g.name = name; g.transform.position = pos;
        var w = g.GetComponentInChildren<FuwaCourseWarp>(true);
        SetU(w, "playerSpot", playerSpot); SetU(w, "gunSpot", gunSpot);
        SetU(w, "courseStart", null); SetU(w, "ball", null);
        var wso = new SerializedObject(w); wso.FindProperty("homeAction").intValue = 0; wso.FindProperty("area").intValue = -1; wso.ApplyModifiedPropertiesWithoutUndo();
        Sync(w);
        var label = g.GetComponentsInChildren<TMPro.TextMeshPro>(true).FirstOrDefault();
        if (label != null) label.text = ja;
        return g.transform;
    }

    static Material GoalMat()
    {
        const string p = "Assets/FuwaCourse/Materials/CoopGoal.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/Goal_Ground.mat")) { name = "CoopGoal" }; AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_Color", new Color(1f, 0.8f, 0.35f, 1f));
        if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", new Color(0.5f, 0.35f, 0.05f, 1f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static void SetU(Object o, string field, Object v) { var s = new SerializedObject(o); var p = s.FindProperty(field); if (p != null) { p.objectReferenceValue = v; s.ApplyModifiedPropertiesWithoutUndo(); } }
    static void Sync(Object o) { if (o is UdonSharpBehaviour u) UdonSharpEditorUtility.CopyProxyToUdon(u); EditorUtility.SetDirty(o); }
}
