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
    // 2026-11-19 てじゃ「2-4 2-5の影も切れるもの増やしてもいいね」：2–4 は 2.5m 未満まで近くだけ影（FarShadows にいた 1.2〜2.5m の物も移す）
    public static float SmallSize24 = 2.5f;

    [MenuItem("FuwaCourse/ステージ2/2–4 の負荷対策（静的バッチ・影）")]
    public static void Menu() { Debug.Log(Apply(GameObject.Find("FuwaCourse2_4")?.transform, SmallSize24)); }
    // 2026-11-13 てじゃ「2-3もみてみて」→ 同じ対策を 2–3 にも（ほかのコースでも使える）
    [MenuItem("FuwaCourse/ステージ2/2–3 の負荷対策（静的バッチ・影）")]
    public static void Menu23() { Debug.Log(Apply(GameObject.Find("FuwaCourse2_3")?.transform)); }

    public static string Apply(Transform root) => Apply(root, root && root.name == "FuwaCourse2_4" ? SmallSize24 : SmallSize);
    public static string Apply(Transform root, float smallSize)
    {
        if (!root) return "コースがない";
        int st = FuwaStage2Static.Apply(root);
        var sh = Shadows(root, smallSize);
        int cam = PhotoVisible(root);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "[" + root.name + " 負荷対策] 静的バッチ +" + st + " " + sh + (cam > 0 ? "・カメラに写るレイヤーへ " + cam : "");
    }

    static FuwaFarShadows Find(string name) => Resources.FindObjectsOfTypeAll<FuwaFarShadows>().FirstOrDefault(f => f && f.name == name && f.gameObject.scene.IsValid());

    static string Shadows(Transform root, float smallSize)
    {
        var small = Find("SmallShadows"); var far = Find("FarShadows");
        if (!small || !far) return "影：SmallShadows / FarShadows がない";
        var inSmall = new HashSet<Renderer>(small.targets.Where(r => r));
        var inFar = new HashSet<Renderer>(far.targets.Where(r => r));
        var addS = new List<Renderer>(); var addF = new List<Renderer>(); var moveS = new List<Renderer>();
        // 影を出さない物（近くでも）：外して影オフ
        var drop = small.targets.Where(r => r && NoShadow(r)).ToList();
        if (drop.Count > 0)
        {
            var dset = new HashSet<Renderer>(drop); var t0 = new List<Renderer>(); var c0 = new List<Vector3>(); var r0 = new List<float>();
            for (int i = 0; i < small.targets.Length; i++) { if (!small.targets[i] || dset.Contains(small.targets[i])) continue; t0.Add(small.targets[i]); c0.Add(small.centers[i]); r0.Add(small.radii[i]); }
            small.targets = t0.ToArray(); small.centers = c0.ToArray(); small.radii = r0.ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(small); EditorUtility.SetDirty(small);
            foreach (var r in drop) { r.shadowCastingMode = ShadowCastingMode.Off; EditorUtility.SetDirty(r); inSmall.Remove(r); }
        }
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (NoShadow(r)) { if (r.shadowCastingMode != ShadowCastingMode.Off) { r.shadowCastingMode = ShadowCastingMode.Off; EditorUtility.SetDirty(r); } continue; }
            if (inSmall.Contains(r)) continue;
            if (inFar.Contains(r))
            {
                // 遠いと影オフに入っている物で smallSize 未満は、近くだけ影へ移す
                var b0 = r.bounds; float m0 = Mathf.Max(b0.size.x, b0.size.y, b0.size.z);
                if (m0 < smallSize && !FillsHole(r) && r.GetComponent<MeshFilter>()) moveS.Add(r);
                continue;
            }
            if (r.shadowCastingMode != ShadowCastingMode.On) continue;   // もともと影なし・影だけの物（FarShadows が On にすると見えてしまう）
            if (!r.GetComponent<MeshFilter>()) continue;
            var b = r.bounds; float m = Mathf.Max(b.size.x, b.size.y, b.size.z);
            // 2026-11-18 アーチの要石は小さくても大物あつかい（近くだけ影だと、遠いとアーチの影のまん中にすき間があいた）
            if (m < smallSize && !FillsHole(r)) { r.shadowCastingMode = ShadowCastingMode.Off; EditorUtility.SetDirty(r); addS.Add(r); }
            else addF.Add(r);
        }
        if (moveS.Count > 0)
        {
            var mv = new HashSet<Renderer>(moveS);
            var t = new List<Renderer>(); var c = new List<Vector3>(); var rr = new List<float>();
            for (int i = 0; i < far.targets.Length; i++) { if (!far.targets[i] || mv.Contains(far.targets[i])) continue; t.Add(far.targets[i]); c.Add(far.centers[i]); rr.Add(far.radii[i]); }
            far.targets = t.ToArray(); far.centers = c.ToArray(); far.radii = rr.ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(far); EditorUtility.SetDirty(far);
            foreach (var r in moveS) { r.shadowCastingMode = ShadowCastingMode.Off; EditorUtility.SetDirty(r); }   // SmallShadows はオフから始める
            addS.AddRange(moveS);
        }
        Add(small, addS); Add(far, addF);
        return "影：小物 +" + addS.Count + "（遠いと影オフから移した " + moveS.Count + "）" + "（SmallShadows " + small.targets.Length + "）・遠いと影オフ +" + addF.Count + "（FarShadows " + far.targets.Length + "）";
    }

    // VRChat のカメラ（写真）に写らないレイヤー 2（Ignore Raycast）の見た目を、当たりの無い物だけ 0 へ
    //  2026-11-24 てじゃ「2-5のバリアゲート、VRChatカメラにうつらない」→ 2–4 のハンドル（軸のリング・ローター）も同じだった。親（当たりのある Handle）のレイヤーを受けついでいた
    public static int PhotoVisible(Transform root)
    {
        int n = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject.layer != 2 || r is ParticleSystemRenderer || r.GetComponent<Collider>()) continue;
            r.gameObject.layer = 0; EditorUtility.SetDirty(r.gameObject); n++;
        }
        return n;
    }

    // 影を出さない物：砂糖タンクのてっぺんの結晶と台（ふくらみが着くと TankCrystal ごと大きさが変わり、自分の影がチラついた。2026-11-24 てじゃ「2-4タンクの上のクリスタルと台座の影、膨らんだ時にチラついちゃうな」）
    public static bool NoShadow(Renderer r) { for (var t = r.transform; t; t = t.parent) if (t.name == "TankCrystal") return true; return false; }

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
