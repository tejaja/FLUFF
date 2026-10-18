using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// スマホ・Quest でロビーにいる間だけ止める物を FuwaCourseLod に集める（2026-10-18 てじゃ「ロビーが色々見えてるから重い」→ 案B「見た目は残して動く物だけ止める」）。
// ・仕掛け：見た目だけで動いていて、プレイヤーやふわふわに力をかけない Update の Udon（StopTypes）。
//   風・重力・上昇気流・ジャンプ台・霧・ワープ・チェックポイント・ゴールなどは、出た時の後始末を Update でしているかもしれないので止めない
// ・粒：いつも出ている粒（ループ・自動再生・今出ている）で、ほかの Udon から参照されていない物（スクリプトが出したり止めたりする粒は止めない）
// コースの仕掛けや粒を足したら、このメニューをもう一度押す
public static class FuwaCourseLodLobbyBuilder
{
    static readonly string[] StopTypes =
    {
        "FuwaWindDeco", "FuwaFacePlayer", "FuwaGoalMarker", "FuwaWebHole", "FuwaSpike", "FuwaBoulder",
        "FuwaEnemy", "FuwaMama", "FuwaDemoStand", "FuwaDemoMonster", "FuwaReadMarks",
    };

    [MenuItem("FuwaCourse/よく使う/スマホのロビーで止める物を集め直す")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var lod = Object.FindObjectOfType<FuwaCourseLod>(true); if (!lod) return "FuwaCourseLod がない";
        var roots = lod.fullRoots.Where(r => r).ToList();

        // Udon（U#）から参照されている物（粒を出したり止めたりしている物は外す）
        var referenced = new HashSet<GameObject>();
        foreach (var u in Object.FindObjectsOfType<UdonSharpBehaviour>(true))
        {
            if (u is FuwaCourseLod) continue;   // 自分の一覧（前に集めた粒）を「参照されている」と数えて、2回目に粒が全部外れた（2026-10-18）
            var so = new SerializedObject(u); var it = so.GetIterator();
            while (it.Next(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue)
                {
                    var o = it.objectReferenceValue;
                    var go = o is GameObject g ? g : o is Component c ? c.gameObject : null;
                    if (go) referenced.Add(go);
                }
        }

        var behaviours = new List<VRC.Udon.UdonBehaviour>(); var counts = new Dictionary<string, int>();
        var hide = new List<GameObject>();
        foreach (var r in roots)
        {
            foreach (var u in r.GetComponentsInChildren<UdonSharpBehaviour>(true))
            {
                string n = u.GetType().Name; if (!StopTypes.Contains(n)) continue;
                var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(u); if (!ub || !ub.enabled) continue;
                behaviours.Add(ub); counts[n] = counts.TryGetValue(n, out int k) ? k + 1 : 1;
            }
            foreach (var ps in r.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.transform.parent && ps.transform.parent.GetComponentInParent<ParticleSystem>(true)) continue;   // いちばん上の粒だけ（子は一緒に消える）
                var go = ps.gameObject;
                if (!go.activeSelf || !go.activeInHierarchy && !IsUnderInactiveRootOnly(go, r)) continue;
                var m = ps.main; if (!m.loop || !m.playOnAwake) continue;
                if (referenced.Contains(go) || go.GetComponentsInChildren<Transform>(true).Any(t => referenced.Contains(t.gameObject))) continue;
                // 粒以外の役目がある物（当たり判定・Udon・音など）は消さない
                if (go.GetComponentsInChildren<Component>(true).Any(c => !(c is Transform || c is ParticleSystem || c is ParticleSystemRenderer))) continue;
                hide.Add(go);
            }
        }
        Undo.RecordObject(lod, "lobby stop");
        lod.lobbyStopBehaviours = behaviours.ToArray();
        lod.lobbyHideObjects = hide.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(lod);
        EditorUtility.SetDirty(lod);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lod.gameObject.scene);
        return "[スマホのロビーで止める] 仕掛け " + behaviours.Count + "（" + string.Join("、", counts.Select(kv => kv.Key + "×" + kv.Value)) + "）、粒 " + hide.Count;
    }

    // コースの本体がエリアの切り替えで今は隠れているだけ（中の物は出ている）なら数える
    static bool IsUnderInactiveRootOnly(GameObject go, GameObject root)
    {
        for (var t = go.transform; t != null && t != root.transform; t = t.parent) if (!t.gameObject.activeSelf) return false;
        return true;
    }
}
