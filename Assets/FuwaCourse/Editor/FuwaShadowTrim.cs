using System.Linq;
using UnityEditor;
using UnityEngine;

// 2026-11-17 てじゃ「影いらなさそうなやつきってくかな」：焼きに入らない（動く・静的でない）物のうち、影がほとんど見えない／意味のない物の影を切る。
//  ・小さすぎる部品（大きさ 0.35m 未満：リベット・ボルトなど）
//  ・ワープのうず・輪・ゲージ（地面にぺったり）
//  ・島や道の下の面（Under・IslandUnder：上の地面が同じ影を落とす）
//  ・軽量版（Lite：遠くに見せるだけ）
//  ・うすくて平らな物（厚み 6cm 未満：水面・溶岩の流れ・きのこのひだ・パッドなど）
//  コースを作り直すと元にもどるので、そのあとにもう一度メニューを押す
public static class FuwaShadowTrim
{
    [MenuItem("FuwaCourse/よく使う/いらない影を切る")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var rs = Object.FindObjectsOfType<Renderer>(true).Where(r => r.gameObject.scene.IsValid() && r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.On
            && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)
            && (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.ContributeGI) == 0).ToArray();
        int tiny = 0, warp = 0, under = 0, lite = 0, flat = 0, fence = 0;
        foreach (var r in rs)
        {
            string p = AnimationUtility.CalculateTransformPath(r.transform, r.transform.root);
            var b = r.bounds; bool cut = false;
            if (b.size.magnitude < 0.35f) { tiny++; cut = true; }
            else if (p.Contains("Warp") && (r.name.StartsWith("Warp") || r.name.Contains("ProgressRing") || r.name.Contains("Gauge") || r.name.Contains("Veil"))) { warp++; cut = true; }
            // 2026-11-17 島や道の下の面は切らない（てじゃ「上の広場の影がこんなかたちに」：上の面が影を出していない所があり、下の面が影を担当していた）
            // 2026-11-18 遠景用の軽量版（CourseLites など、ルートの名前に Lite）だけ。柱の LODGroup の遠い版（子の「Lite」）は遠くで本体の代わりに影を出すので切らない
            //  （てじゃ「ここらの影が、距離でスキマがあく」：2–5 の柱の影が遠いと消えていた）
            else if (r.transform.root.name.Contains("Lite")) { lite++; cut = true; }
            // 2026-11-18 床・ふた（Floor*・Lid）は平らでも切らない（てじゃ「うえの広場の影が多重の輪っかみたいに」：2–5 の結晶の広場の床の模様とドームのふたを切って、輪っか状の影になった）
            else if (b.size.y < 0.06f && b.size.magnitude > 0.3f && !r.name.StartsWith("Floor") && r.name != "Lid") { flat++; cut = true; }
            // 2026-11-19 てじゃ「ロビーとかスタートとかにある木の柵、影いらんかも、なんかやけに細くなるし」：木の柵（Fence・Fence_Far、木の材質）
            else if (r.name.StartsWith("Fence") && r.sharedMaterials.Any(m => m && m.name.Contains("Wood"))) { fence++; cut = true; }
            if (!cut) continue;
            Undo.RecordObject(r, "shadow trim");
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            EditorUtility.SetDirty(r);
        }
        // 近くだけ影を出す仕組み（FuwaFarShadows・FuwaTierShadows）は、近づくと影を On にもどすので、切った物はその一覧から外す
        var off = new System.Collections.Generic.HashSet<Renderer>(rs.Where(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off));
        int removed = 0;
        foreach (var fs in Object.FindObjectsOfType<FuwaFarShadows>(true))
        {
            if (fs.targets == null) continue;
            var keep = Enumerable.Range(0, fs.targets.Length).Where(i => !(fs.targets[i] && off.Contains(fs.targets[i]))).ToArray();
            if (keep.Length == fs.targets.Length) continue;
            removed += fs.targets.Length - keep.Length;
            int n = fs.targets.Length;
            fs.targets = keep.Select(i => fs.targets[i]).ToArray();
            if (fs.centers != null && fs.centers.Length == n) fs.centers = keep.Select(i => fs.centers[i]).ToArray();
            if (fs.radii != null && fs.radii.Length == n) fs.radii = keep.Select(i => fs.radii[i]).ToArray();
            if (fs.targetStage != null && fs.targetStage.Length == n) fs.targetStage = keep.Select(i => fs.targetStage[i]).ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(fs); EditorUtility.SetDirty(fs);
        }
        foreach (var ts in Object.FindObjectsOfType<FuwaTierShadows>(true))
        {
            if (ts.targets == null) continue;
            var keep = Enumerable.Range(0, ts.targets.Length).Where(i => !(ts.targets[i] && off.Contains(ts.targets[i]))).ToArray();
            if (keep.Length == ts.targets.Length) continue;
            removed += ts.targets.Length - keep.Length;
            int n = ts.targets.Length;
            ts.targets = keep.Select(i => ts.targets[i]).ToArray();
            if (ts.centers != null && ts.centers.Length == n) ts.centers = keep.Select(i => ts.centers[i]).ToArray();
            if (ts.radii != null && ts.radii.Length == n) ts.radii = keep.Select(i => ts.radii[i]).ToArray();
            if (ts.tiers != null && ts.tiers.Length == n) ts.tiers = keep.Select(i => ts.tiers[i]).ToArray();
            UdonSharpEditor.UdonSharpEditorUtility.CopyProxyToUdon(ts); EditorUtility.SetDirty(ts);
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        return $"[影を切る] 近くだけ影の一覧から外した {removed}／ 小さい {tiny}・ワープ {warp}・下の面 {under}・軽量版 {lite}・うすい平ら {flat}・木の柵 {fence}（のこり {rs.Length - tiny - warp - under - lite - flat - fence}）";
    }
}
