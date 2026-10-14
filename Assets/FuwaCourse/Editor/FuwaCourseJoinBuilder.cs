using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 飛んで別のコース・ロビーに降りたら入った扱いにする FuwaCourseJoin を FuwaSystem の下に作る（2026-10-14）。何度実行しても同じ結果
public static class FuwaCourseJoinBuilder
{
    const string ProgramPath = "Assets/FuwaCourse/Scripts/FuwaCourseJoin.asset";

    [MenuItem("FuwaCourse/飛んで入ったコースの切り替えを作り直す")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new List<string>();
        var sys = GameObject.Find("FuwaSystem").transform;
        var old = sys.Find("CourseJoin"); if (old != null) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject("CourseJoin"); go.transform.SetParent(sys, false);

        var prog = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(ProgramPath);
        if (prog == null)
        {
            prog = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/FuwaCourse/Scripts/FuwaCourseJoin.cs");
            AssetDatabase.CreateAsset(prog, ProgramPath);
            AssetDatabase.SaveAssets();
        }
        var j = UdonSharpUndo.AddComponent<FuwaCourseJoin>(go);

        var ball = Object.FindObjectsOfType<FuwaBall>(true).First(b => b.checkpoints != null && b.checkpoints.Length > 0);
        var lod = Object.FindObjectsOfType<FuwaCourseLod>(true).First();
        var allWarps = Object.FindObjectsOfType<FuwaCourseWarp>(true);
        var lobby = GameObject.Find("FuwaLobby").transform;

        var roots = new List<Transform>(); var lites = new List<Transform>(); var warps = new List<FuwaCourseWarp>();
        for (int i = 0; i < lod.fullRoots.Length; i++)
        {
            var r = lod.fullRoots[i].transform;
            // ロビーのポータル（ロビーの下にあって、行き先がこのコース）
            var w = allWarps.First(x => x.transform.IsChildOf(lobby) && x.playerSpot != null && x.playerSpot.root == r);
            roots.Add(r); lites.Add(lod.liteRoots[i] != null ? lod.liteRoots[i].transform : null); warps.Add(w);
            log.Add(r.name + " <- " + w.name);
        }
        // ロビー：コースからロビーへ戻るワープ
        var lw = allWarps.First(x => x.name == "WarpToLobby_Trigger" && x.transform.root.name == "FuwaCourse" && x.transform.parent.name == "Start");
        roots.Add(lobby); lites.Add(null); warps.Add(lw);

        j.ball = ball;
        j.areaVisibility = Object.FindObjectsOfType<FuwaAreaVisibility>(true).First();
        j.roots = roots.ToArray(); j.liteRoots = lites.ToArray(); j.warps = warps.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(j);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(j);
        if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
        EditorUtility.SetDirty(j);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        return string.Join("\n", log);
    }
}
