using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2026-11-16 てじゃ「各コースのロビーのポータル、クリア済みならまわりにミニキノコ生やしてみよ」「道のところは無し」「ローカルね」
//  ステージ1のロビー（FuwaLobby）とステージ2のロビー（FuwaStage2/Lobby）の、コースへのワープのまわりにミニキノコの輪（ゴールの妖精の輪のキノコ）。
//  ロビーのまん中の方（道が来る方）はあける。出す・出さないは FuwaClearMarks（自分がクリアしたことがあれば）
public static class FuwaClearMarksBuilder
{
    public static float RingR = 1.5f, GapDeg = 40f, StepDeg = 40f;   // 25 → 40（てじゃ「もっとすくなくてもいいかも」）

    [MenuItem("FuwaCourse/よく使う/ロビーのクリアの印（ミニキノコ）を作り直す")]
    public static void Run()
    {
        var src = GameObject.Find("FuwaCourse5").transform.Find("Goal/FairyRing");
        var shrooms = src.Cast<Transform>().Where(t => t.name.StartsWith("Mushroom")).ToList();
        var recs = Object.FindObjectsOfType<FuwaRecords>(true).ToList();
        var log = new List<string>();
        foreach (var lobby in new[] { GameObject.Find("FuwaLobby")?.transform, GameObject.Find("FuwaStage2/Lobby")?.transform })
        {
            if (lobby == null) continue;
            var warps = lobby.GetComponentsInChildren<FuwaCourseWarp>(true).Where(w => w.courseStart != null && w.name.StartsWith("WarpToCourse")).ToList();
            var center = Vector3.zero; foreach (var w in warps) center += w.transform.position; center /= Mathf.Max(1, warps.Count);
            var rl = new List<FuwaRecords>(); var il = new List<int>(); var ml = new List<GameObject>();
            foreach (var w in warps)
            {
                FuwaRecords rec = null; int idx = -1;
                foreach (var r in recs) { if (r.courseStarts == null) continue; for (int i = 0; i < r.courseStarts.Length; i++) if (r.courseStarts[i] != null && r.courseStarts[i].root == w.courseStart.root) { rec = r; idx = i; } }
                if (rec == null) continue;   // チュートリアルなど記録のないコース
                var slot = w.transform.parent;
                var old = slot.Find("ClearMushrooms"); if (old) Object.DestroyImmediate(old.gameObject);
                var pad = slot.Cast<Transform>().FirstOrDefault(t => t.name == w.name.Replace("_Trigger", "")) ?? slot;
                var g = new GameObject("ClearMushrooms").transform; g.SetParent(slot, false);
                Vector3 c = pad.position; g.position = c; g.rotation = Quaternion.identity;
                Vector3 toC = center - c; toC.y = 0f; float gapA = Mathf.Atan2(toC.x, toC.z) * Mathf.Rad2Deg;
                var rnd = new System.Random(slot.name.GetHashCode() ^ lobby.name.GetHashCode());
                // 2026-11-16 てじゃ「ゴールの時ってこのくらいあったっけ」→ ゴールの妖精の輪（14本・約25°おき・半径1.2〜1.3）と同じく、等間隔の輪（道の側はあける）
                int made = 0;
                for (float a = gapA + GapDeg; a <= gapA + 360f - GapDeg + 0.1f; a += StepDeg)
                {
                    float aa = a + ((float)rnd.NextDouble() - 0.5f) * 6f;
                    float r = RingR + ((float)rnd.NextDouble() - 0.5f) * 0.12f;
                    var p = c + new Vector3(Mathf.Sin(aa * Mathf.Deg2Rad), 0f, Mathf.Cos(aa * Mathf.Deg2Rad)) * r;
                    var m = Object.Instantiate(shrooms[rnd.Next(shrooms.Count)].gameObject, g, true); m.name = "Mushroom" + made;
                    m.transform.position = p; m.transform.rotation = Quaternion.Euler(((float)rnd.NextDouble() - 0.5f) * 10f, (float)rnd.NextDouble() * 360f, ((float)rnd.NextDouble() - 0.5f) * 10f);
                    m.transform.localScale = Vector3.one * (0.9f + 0.55f * (float)rnd.NextDouble());
                    foreach (var cc in m.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(cc);
                    foreach (var t in m.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                    made++;
                }
                g.gameObject.SetActive(false);
                rl.Add(rec); il.Add(idx); ml.Add(g.gameObject);
            }
            var holder = lobby.Find("ClearMarks"); if (holder) Object.DestroyImmediate(holder.gameObject);
            var hg = new GameObject("ClearMarks"); hg.transform.SetParent(lobby, false);
            var cm = UdonSharpUndo.AddComponent<FuwaClearMarks>(hg); cm.records = rl.ToArray(); cm.courseIndex = il.ToArray(); cm.marks = ml.ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(cm);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(cm); if (ub != null) { ub.SyncMethod = VRC.SDKBase.Networking.SyncType.None; EditorUtility.SetDirty(ub); }
            log.Add(lobby.name + " " + ml.Count);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[クリアの印] " + string.Join(", ", log));
    }
}
