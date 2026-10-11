using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 名場面アルバムのフォトポイント（FuwaPhotoPoint）を各コースに仮置きする（2026-10-11 てじゃ「いっかい仮できめちゃって、あとで変えていこう」）。
// ふわふわが必ず通る所＝各チェックポイントのゲートと、ゴールの手前（とスタート直後）から、各コース3か所。
// カメラは通る向きの先の斜め上から振り返って、飛んでくるふわふわと後ろで構える自分を撮る位置に置く（地面や壁で隠れる位置はよける）。
// すでにフォトポイントがあるコースは触らない（てじゃが動かした位置・構図を消さないため）。
// 作り直したいコースは、そのコースの PhotoPoints を消してからメニューを実行する
public static class FuwaPhotoPointBuilder
{
    const string ProgramPath = "Assets/FuwaCourse/Scripts/FuwaPhotoPoint.asset";
    const string ScriptPath = "Assets/FuwaCourse/Scripts/FuwaPhotoPoint.cs";
    static readonly string[] Courses = { "FuwaCourse", "FuwaCourse2", "FuwaCourse3", "FuwaCourse4", "FuwaCourse5" };

    [MenuItem("FuwaCourse/フォトポイントを置く（ないコースだけ）")]
    public static void Menu()
    {
        var album = Object.FindObjectsOfType<FuwaPhotoAlbum>(true).FirstOrDefault();
        Debug.Log(Run(album));
    }

    public static string Run(FuwaPhotoAlbum album)
    {
        if (album == null) return "FuwaPhotoAlbum がない";
        EnsureProgram();
        var log = new List<string>();
        var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var cname in Courses)
        {
            var root = roots.FirstOrDefault(g => g.name == cname);
            if (root == null) continue;
            var holder = root.transform.Find("PhotoPoints");
            if (holder != null && holder.childCount > 0)
            {
                // つなぎ先だけ直す
                foreach (var pp in holder.GetComponentsInChildren<FuwaPhotoPoint>(true))
                    if (pp.album != album) { pp.album = album; UdonSharpEditorUtility.CopyProxyToUdon(pp); EditorUtility.SetDirty(pp); }
                log.Add(cname + ": keep " + holder.childCount);
                continue;
            }
            bool wasActive = root.activeSelf;
            root.SetActive(true);
            Physics.SyncTransforms();
            try { log.Add(cname + ": " + Build(root.transform, album)); }
            finally { root.SetActive(wasActive); }
        }
        return string.Join("\n", log);
    }

    static string Build(Transform course, FuwaPhotoAlbum album)
    {
        var start = course.Find("Start/BallStart");
        var goal = course.GetComponentsInChildren<FuwaGoal>(true).FirstOrDefault();
        var cps = course.GetComponentsInChildren<FuwaCheckpoint>(true)
            .Where(c => c.GetComponent<BoxCollider>() != null)
            .OrderBy(c => c.transform.parent.name, System.StringComparer.Ordinal).ToList();
        if (start == null || goal == null) return "no start/goal";

        // 通る順の点：スタート → チェックポイント → ゴール
        var way = new List<Vector3> { start.position };
        way.AddRange(cps.Select(c => c.GetComponent<BoxCollider>().bounds.center));
        way.Add(goal.transform.position);

        var holder = course.Find("PhotoPoints");
        if (holder == null) { holder = new GameObject("PhotoPoints").transform; holder.SetParent(course, false); }
        int layer = cps.Count > 0 ? cps[0].gameObject.layer : goal.gameObject.layer;

        // 候補：各チェックポイントのゲートと、ゴールの手前。3か所にそろえる（多ければ等間隔に間引き、少なければスタート直後を足す）
        var cand = new List<(Vector3 center, Vector3 dir, float width, string ja, string en)>();
        for (int i = 0; i < cps.Count; i++)
        {
            var box = cps[i].GetComponent<BoxCollider>();
            Vector3 center = box.bounds.center;
            // ゲートの薄い方の向き＝通る向き。次の点の方を前にする
            Transform bt = box.transform;
            Vector3 size = Vector3.Scale(box.size, bt.lossyScale);
            Vector3 dir = Mathf.Abs(size.x) < Mathf.Abs(size.z) ? bt.right : bt.forward;
            dir.y = 0f; dir.Normalize();
            if (Vector3.Dot(dir, way[i + 2] - center) < 0f) dir = -dir;
            float width = Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.z)) + 2f;
            cand.Add((center, dir, width, "チェックポイント" + (i + 1), "Checkpoint " + (i + 1)));
        }
        {
            Vector3 g = goal.transform.position;
            Vector3 prev = way[way.Count - 2];
            Vector3 dir = g - prev; dir.y = 0f; dir.Normalize();
            cand.Add((g - dir * 7f + Vector3.up * 1f, dir, 8f, "ゴール前", "Final Stretch"));
        }
        if (cand.Count < 3)
        {
            // スタート直後（キノコから飛び出したふわふわが必ず通る、スタートの少し先）
            Vector3 dir = way[1] - way[0]; dir.y = 0f; dir.Normalize();
            cand.Insert(0, (way[0] + dir * 3f + Vector3.up * 1.5f, dir, 7f, "スタート", "Start"));
        }
        var pick = new List<int>();
        for (int i = 0; i < 3 && i < cand.Count; i++) pick.Add(Mathf.RoundToInt(i * (cand.Count - 1) / 2f));
        var made = new List<string>();
        for (int i = 0; i < pick.Count; i++)
        {
            var c = cand[pick[i]];
            made.Add(Make(holder, "PhotoPoint" + (i + 1), i, c.center, c.dir, c.width, layer, album, c.ja, c.en));
        }
        return string.Join(", ", made);
    }

    static string Make(Transform holder, string name, int order, Vector3 center, Vector3 dir, float width, int layer, FuwaPhotoAlbum album, string ja, string en)
    {
        var go = new GameObject(name);
        go.layer = layer;
        go.transform.SetParent(holder, false);
        go.transform.position = center;
        go.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(width, 8f, 3f);
        var pp = UdonSharpUndo.AddComponent<FuwaPhotoPoint>(go);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(pp);
        if (ub != null && ub.SyncMethod != VRC.SDKBase.Networking.SyncType.None)
        {
            var so = new SerializedObject(ub); so.FindProperty("_syncMethod").intValue = (int)VRC.SDKBase.Networking.SyncType.None; so.ApplyModifiedPropertiesWithoutUndo();
        }

        // カメラ：通る向きの先の、斜め上。いくつか試して、ふわふわの通る所と後ろの自分が見える所
        var side = Vector3.Cross(Vector3.up, dir).normalized;
        var tries = new[] { new Vector3(3f, 1.8f, 5.5f), new Vector3(-3f, 1.8f, 5.5f), new Vector3(4.5f, 2.5f, 3.5f), new Vector3(-4.5f, 2.5f, 3.5f),
                            new Vector3(1.5f, 1.2f, 7f), new Vector3(-1.5f, 1.2f, 7f), new Vector3(2f, 4f, 3f), new Vector3(-2f, 4f, 3f) };
        Vector3 player = center - dir * 4f;
        Vector3 camPos = center + Vector3.up * 5f + dir * 2f;
        string how = "fallback";
        int mask = (1 << 0) | (1 << 11);
        foreach (var t in tries)
        {
            var p = center + side * t.x + Vector3.up * t.y + dir * t.z;
            if (Physics.CheckSphere(p, 0.4f, mask, QueryTriggerInteraction.Ignore)) continue;
            if (Physics.Linecast(p, center, mask, QueryTriggerInteraction.Ignore)) continue;
            if (Physics.Linecast(p, player, mask, QueryTriggerInteraction.Ignore)) continue;
            camPos = p; how = t.ToString("F1");
            break;
        }
        var cam = new GameObject("CamPose").transform;
        cam.SetParent(go.transform, false);
        cam.position = camPos;
        cam.rotation = Quaternion.LookRotation((center - dir * 1.5f) - camPos, Vector3.up);

        pp.album = album;
        pp.camPose = cam;
        pp.fieldOfView = 50f;
        pp.order = order;
        pp.nameJa = ja;
        pp.nameEn = en;
        UdonSharpEditorUtility.CopyProxyToUdon(pp);
        EditorUtility.SetDirty(pp);
        return name + "(" + how + ")";
    }

    static void EnsureProgram()
    {
        if (AssetDatabase.LoadAssetAtPath<UdonSharp.UdonSharpProgramAsset>(ProgramPath) != null) return;
        var prog = ScriptableObject.CreateInstance<UdonSharp.UdonSharpProgramAsset>();
        prog.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(ScriptPath);
        AssetDatabase.CreateAsset(prog, ProgramPath);
        AssetDatabase.SaveAssets();
        UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
    }
}
