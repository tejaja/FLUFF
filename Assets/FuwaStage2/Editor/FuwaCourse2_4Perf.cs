using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 2–4 の負荷対策（2026-11-13 てじゃ「2-4 現状で軽量化できそうなとこある？」→ ①静的バッチ ②影）
//  計測（Play・2–4 のスタート）：2–4 を消すと batches 2317→740・影を出す物 2492→266。2–4 だけで 約1580 batches
//  ① 静的バッチ：FuwaStage2Static（スクリプトが参照している物の下は動く物として外す）。2–4 は「場所の基準に参照しているだけ」の欄が多く、
//     ポンプの台・気球の首まで外れていた → 動かさない欄（StillFields）を足して呼ぶ
//  ② 影：1.2m 未満の小物は影オフ＋ SmallShadows（近くだけ影）。それより大きい物は FarShadows（遠いと影オフ）に入れる
//  FuwaCourse2_4Builder.Build の最後で呼ぶ。何度実行してもOK
public static class FuwaCourse2_4Perf
{
    public static float SmallSize = 1.2f;

    [MenuItem("FuwaCourse/ステージ2/2–4 の負荷対策（静的バッチ・影）")]
    public static void Menu() { Debug.Log(Apply(GameObject.Find("FuwaCourse2_4")?.transform)); }
    // 2026-11-13 てじゃ「2-3もみてみて」→ 同じ対策を 2–3 にも（ほかのコースでも使える）
    [MenuItem("FuwaCourse/ステージ2/2–3 の負荷対策（静的バッチ・影）")]
    public static void Menu23() { Debug.Log(Apply(GameObject.Find("FuwaCourse2_3")?.transform)); }

    public static string Apply(Transform root)
    {
        if (!root) return "コースがない";
        int st = FuwaStage2Static.Apply(root);
        var sh = Shadows(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "[" + root.name + " 負荷対策] 静的バッチ +" + st + " " + sh;
    }

    static FuwaFarShadows Find(string name) => Resources.FindObjectsOfTypeAll<FuwaFarShadows>().FirstOrDefault(f => f && f.name == name && f.gameObject.scene.IsValid());

    static string Shadows(Transform root)
    {
        var small = Find("SmallShadows"); var far = Find("FarShadows");
        if (!small || !far) return "影：SmallShadows / FarShadows がない";
        var inSmall = new HashSet<Renderer>(small.targets.Where(r => r));
        var inFar = new HashSet<Renderer>(far.targets.Where(r => r));
        var addS = new List<Renderer>(); var addF = new List<Renderer>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (inSmall.Contains(r) || inFar.Contains(r)) continue;
            if (r.shadowCastingMode != ShadowCastingMode.On) continue;   // もともと影なし・影だけの物（FarShadows が On にすると見えてしまう）
            if (!r.GetComponent<MeshFilter>()) continue;
            var b = r.bounds; float m = Mathf.Max(b.size.x, b.size.y, b.size.z);
            // 2026-11-18 アーチの要石は小さくても大物あつかい（近くだけ影だと、遠いとアーチの影のまん中にすき間があいた）
            if (m < SmallSize && !FillsHole(r)) { r.shadowCastingMode = ShadowCastingMode.Off; EditorUtility.SetDirty(r); addS.Add(r); }
            else addF.Add(r);
        }
        Add(small, addS); Add(far, addF);
        return "影：小物 +" + addS.Count + "（SmallShadows " + small.targets.Length + "）・遠いと影オフ +" + addF.Count + "（FarShadows " + far.targets.Length + "）";
    }

    // 大きい物のすき間を埋める小物（これの影だけ消えると、大きい物の影に穴があく）
    public static bool FillsHole(Renderer r) => r.name == "Keystone";

    // 消えた物（作り直しで null）を詰めて、足す
    static void Add(FuwaFarShadows f, List<Renderer> add)
    {
        var t = new List<Renderer>(); var c = new List<Vector3>(); var rr = new List<float>();
        for (int i = 0; i < f.targets.Length; i++)
        {
            if (!f.targets[i]) continue;
            t.Add(f.targets[i]); c.Add(f.centers[i]); rr.Add(f.radii[i]);
        }
        foreach (var r in add) { var b = r.bounds; t.Add(r); c.Add(b.center); rr.Add(b.extents.magnitude); }
        f.targets = t.ToArray(); f.centers = c.ToArray(); f.radii = rr.ToArray();
        UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(f); EditorUtility.SetDirty(f);
    }
}
