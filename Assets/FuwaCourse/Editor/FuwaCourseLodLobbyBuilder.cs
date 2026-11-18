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
        // FuwaBoulder（いわのみちの転がる岩）は止めない（2026-11-17 てじゃ「いわのみちの岩…とまってるのもさみしい」。3つだけで軽い）
        "FuwaWindDeco", "FuwaFacePlayer", "FuwaGoalMarker", "FuwaWebHole", "FuwaSpike",
        "FuwaEnemy", "FuwaMama", "FuwaDemoStand", "FuwaDemoMonster", "FuwaReadMarks",
    };

    // 2026-11-16 てじゃ「ステージ２のロビーおもいなあ」「ステージ１のときみたいに、うどんが常時うごきすぎかな？」
    //  → ステージ2用（CourseLodStage2）も同じように集める。見た目だけ・自分の画面だけで、プレイヤーを運ぶ・後始末する物は入れない
    //  （入れない：ワープ・チェックポイント・ゴール・風の範囲・浅瀬・熱・歯車の乗り物・回る床・動く氷・すべり・ポータル・バリア・衝撃波・音・影の切り替え・ハザード）
    static readonly string[] StopTypes2 =
    {
        "FuwaFacePlayer", "FuwaGoalMarker", "FuwaWindDeco", "FuwaWebHole", "FuwaEnemy",
        // FuwaLavaRock（ほのおのやまの火山岩）・FuwaClockwork（はぐるまのしまの歯車）は止めない（2026-11-17 てじゃ「火山岩、歯車がとまってるのもさみしい」）
        "FuwaFireJet", "FuwaFireball", "FuwaClamBubbles", "FuwaGuardFish", "FuwaRiverFish",
        "FuwaGearGuard", "FuwaSugarPipe", "FuwaCloudFactory", "FuwaWindmill", "FuwaTiltIce", "FuwaSizeRune",
    };

    // そのコースにいる時だけ動かす（ステージ1・2共通。2026-11-17 てじゃ「各コースで見た目に影響しないものはコースに居ないときは動いてる意味ない」）
    //  ハザード判定（止まる前に遅くした速さを戻す）・チェックポイント（光る途中なら光りきらせる）・2–5 のワープの穴（勢いの残りを捨てる）。どれも LodStop で後片付け
    static readonly string[] CourseOnlyTypes = { "FuwaHazard", "FuwaCheckpoint", "FuwaPortal" };

    public static bool HideScriptParticles = true;

    [MenuItem("FuwaCourse/よく使う/スマホのロビーで止める物を集め直す")]
    public static void Menu() { Debug.Log(Build()); }

    [MenuItem("FuwaCourse/ステージ2/ステージ2のロビーで止める物を集め直す")]
    public static void MenuStage2() { Debug.Log(BuildStage2()); }

    public static string BuildStage2()
    {
        var lod = Resources.FindObjectsOfTypeAll<FuwaCourseLod>().FirstOrDefault(l => l && l.gameObject.scene.IsValid() && l.stage == 2); if (!lod) return "CourseLodStage2 がない";
        var main = Resources.FindObjectsOfTypeAll<FuwaCourseLod>().FirstOrDefault(l => l && l.gameObject.scene.IsValid() && l.stage <= 1);
        if (lod.lobbyNearRadius <= 0f) lod.lobbyNearRadius = main ? main.lobbyNearRadius : 40f;   // 0 だと全部止まる（ステージ1と同じく近くだけ動かす）
        return "[ステージ2] " + Build(lod, StopTypes2);
    }

    public static string Build()
    {
        var lod = Resources.FindObjectsOfTypeAll<FuwaCourseLod>().FirstOrDefault(l => l && l.gameObject.scene.IsValid() && l.stage <= 1); if (!lod) return "FuwaCourseLod がない";
        return Build(lod, StopTypes);
    }

    static string Build(FuwaCourseLod lod, string[] stopTypes)
    {
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
                string n = u.GetType().Name; if (!stopTypes.Contains(n)) continue;
                var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(u); if (!ub || !ub.enabled) continue;
                behaviours.Add(ub); counts[n] = counts.TryGetValue(n, out int k) ? k + 1 : 1;
            }
            foreach (var ps in r.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.transform.parent && ps.transform.parent.GetComponentInParent<ParticleSystem>(true)) continue;   // いちばん上の粒だけ（子は一緒に消える）
                var go = ps.gameObject;
                if (!go.activeSelf || !go.activeInHierarchy && !IsUnderInactiveRootOnly(go, r)) continue;
                var m = ps.main; if (!m.loop || !m.playOnAwake) continue;
                // 2026-11-18 てじゃ「パーティクル系もロビーからはかなり削ってもよさそう」：スクリプトが出したり止めたりする粒（炎・風の粒・チェックポイント・ジャンプ台など）も、
                //  ロビーで遠い間は消す（近づく・そのコースに入るとまた出る。出し直した直後に一瞬ずれるくらいは気にしない）
                if (!HideScriptParticles && (referenced.Contains(go) || go.GetComponentsInChildren<Transform>(true).Any(t => referenced.Contains(t.gameObject)))) continue;
                // 粒以外の役目がある物（当たり判定・Udon・音など）は消さない
                if (go.GetComponentsInChildren<Component>(true).Any(c => !(c is Transform || c is ParticleSystem || c is ParticleSystemRenderer))) continue;
                hide.Add(go);
            }
        }
        // 2026-11-17 そのコースにいる時だけ動かす物（見た目に関係しない毎フレームの仕掛け。止まる前に LodStop で後片付けする）
        var co = new List<VRC.Udon.UdonBehaviour>(); var coCourse = new List<int>();
        for (int ri = 0; ri < lod.fullRoots.Length; ri++)
        {
            var r = lod.fullRoots[ri]; if (!r) continue;
            foreach (var u in r.GetComponentsInChildren<UdonSharpBehaviour>(true))
            {
                if (!CourseOnlyTypes.Contains(u.GetType().Name)) continue;
                var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(u); if (!ub || !ub.enabled) continue;
                co.Add(ub); coCourse.Add(ri);
            }
        }
        Undo.RecordObject(lod, "lobby stop");
        lod.courseOnlyBehaviours = co.ToArray(); lod.courseOnlyCourse = coCourse.ToArray();
        lod.lobbyStopBehaviours = behaviours.ToArray();
        lod.lobbyHideObjects = hide.ToArray();
        FuwaCourseLite.SetBounds(lod);   // 集めた物がどのコースの物か（stopCourse・hideCourse）も入れ直す
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
