using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// ステージ2のコースのスタート・ゴール（ステージ1のコース1の Start・Goal を写した物）の飾りの影（2026-11-11 てじゃ「ステージ２の各ゴールのミニキノコ、かげがない」）
//  ステージ1の物は「近くに来たら影を出す」一覧（FuwaFarShadows の targets）に入っているけど、写した物は入っていなかった（ふだんは影オフ、近くで FarShadows がオンにする）
//  → 元が一覧に入っている物の、写した先（同じ相対パス）を同じ一覧に足す。位置（centers）は今の位置で。各コースを作り直したら、またこれを呼ぶ
public static class FuwaStage2CopyShadows
{
    [MenuItem("FuwaCourse/ステージ2/スタート・ゴールの飾りの影を登録しなおす")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var srcs = new[] { "Start", "Goal" }.Select(n => (n, t: GameObject.Find("FuwaCourse/" + n)?.transform)).Where(x => x.t).ToList();
        var courses = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name.StartsWith("FuwaCourse2_")).Select(g => g.transform).ToList();
        var log = new List<string>();
        foreach (var f in Object.FindObjectsOfType<FuwaFarShadows>(true))
        {
            if (f.targets == null) continue;
            var list = f.targets.Where(r => r != null).ToList(); var set = new HashSet<Renderer>(list); int added = 0;
            foreach (var r in f.targets)
            {
                if (r == null) continue;
                foreach (var (n, st) in srcs)
                {
                    if (!r.transform.IsChildOf(st)) continue;
                    string rel = Rel(r.transform, st);
                    foreach (var c in courses)
                    {
                        var dst = c.Find(n); if (!dst) continue;
                        var t = rel == "" ? dst : dst.Find(rel); if (!t) continue;
                        var rr = t.GetComponent<Renderer>(); if (!rr || set.Contains(rr)) continue;
                        rr.shadowCastingMode = r.shadowCastingMode;   // 一覧の物はふだんオフ（FarShadows がつける）
                        set.Add(rr); list.Add(rr); added++;
                    }
                }
            }
            Undo.RecordObject(f, "copy shadows");
            f.targets = list.ToArray();
            f.centers = list.Select(x => x.bounds.center).ToArray();
            f.radii = list.Select(x => x.bounds.extents.magnitude).ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(f); EditorUtility.SetDirty(f);
            log.Add(f.name + " +" + added);
        }
        FuwaFarViewSetup.RefreshShadowCenters();   // 非表示の物の位置もメッシュから出し直す
        foreach (var f in Object.FindObjectsOfType<FuwaFarShadows>(true)) { UdonSharpEditorUtility.CopyProxyToUdon(f); EditorUtility.SetDirty(f); }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        return "[ステージ2のスタート・ゴールの影] " + string.Join(" / ", log);
    }

    static string Rel(Transform t, Transform top) => t == top ? "" : (t.parent == top ? t.name : Rel(t.parent, top) + "/" + t.name);
}
