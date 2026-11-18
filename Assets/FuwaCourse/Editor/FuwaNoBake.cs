using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

// 2026-11-18 てじゃ「いま光焼いてるけど、これこのワールドで意味あんのかな」→ ライトマップを外して撮り比べたら、ほぼ差がなかった
//  （太陽が真上なので焼いた影は物の真下に落ちて見えない・トゥーンのシェーダーは焼いた光の色をほぼ使わない）→ 焼きをやめる。
// ・焼き用の目印（Static の Contribute GI）を外し、焼いてない物と同じ受け方（環境光、光プローブなし）にする
// ・焼いてた物の影はリアルタイムの影に：1.2m 未満は影オフ＋ SmallShadows（近くだけ影）、それより大きい物は FarShadows（遠いと影オフ）
//   （今影を出していない物はそのまま出さない）
// ・ライトを Mixed → Realtime、焼きの設定（Baked GI）オフ、ライトマップと Hikari を外す
// ・コースを作り直すと isStatic = true などで目印がまた付くので、ビルドの時にも外す（下の NoBakeOnBuild）。作り直した後は、もう一度このメニューを押すと影の切り替えにも入る
// 何度実行してもよい
public static class FuwaNoBake
{
    const float SmallSize = 1.2f;

    [MenuItem("FuwaCourse/よく使う/焼きをやめる（リアルタイムの影だけ）")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        var log = new System.Text.StringBuilder("[焼きをやめる] ");
        var small = Resources.FindObjectsOfTypeAll<FuwaFarShadows>().FirstOrDefault(f => f && f.name == "SmallShadows" && f.gameObject.scene.IsValid());
        var far = Resources.FindObjectsOfTypeAll<FuwaFarShadows>().FirstOrDefault(f => f && f.name == "FarShadows" && f.gameObject.scene.IsValid());
        var listed = new HashSet<Renderer>();
        foreach (var f in Resources.FindObjectsOfTypeAll<FuwaFarShadows>().Where(f => f && f.gameObject.scene.IsValid() && f.targets != null)) foreach (var r in f.targets) if (r) listed.Add(r);
        foreach (var t in Resources.FindObjectsOfTypeAll<FuwaTierShadows>().Where(f => f && f.gameObject.scene.IsValid() && f.targets != null)) foreach (var r in t.targets) if (r) listed.Add(r);

        int unflag = 0; var addS = new List<Renderer>(); var addF = new List<Renderer>();
        foreach (var r in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            if (!r.gameObject.scene.IsValid()) continue;
            var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
            bool wasBaked = (flags & StaticEditorFlags.ContributeGI) != 0;
            if (!wasBaked) continue;
            Undo.RecordObject(r.gameObject, "no bake"); Undo.RecordObject(r, "no bake");
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, flags & ~StaticEditorFlags.ContributeGI);
            r.receiveGI = ReceiveGI.LightProbes;
            r.lightProbeUsage = LightProbeUsage.Off;   // 焼いてない物と同じ（光プローブは無いので環境光）
            var so = new SerializedObject(r);
            so.FindProperty("m_LightmapIndex").intValue = 0xFFFF;
            so.FindProperty("m_LightmapTilingOffset").vector4Value = new Vector4(1, 1, 0, 0);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(r);
            unflag++;
            // 影：今影を出していない物はそのまま。出している物は影の切り替えに入れる（もう入っていればそのまま）
            if (r.shadowCastingMode != ShadowCastingMode.On || listed.Contains(r)) continue;
            if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
            var b = r.bounds; float m = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (m < SmallSize) { r.shadowCastingMode = ShadowCastingMode.Off; addS.Add(r); } else addF.Add(r);
        }
        if (small) Add(small, addS); if (far) Add(far, addF);
        log.Append("焼きの目印を外した " + unflag + "・近くだけ影 +" + addS.Count + "・遠いと影オフ +" + addF.Count + " ");

        // ライト
        foreach (var l in Object.FindObjectsOfType<Light>(true))
        {
            if (!l.gameObject.scene.IsValid() || l.lightmapBakeType == LightmapBakeType.Realtime) continue;
            Undo.RecordObject(l, "no bake"); l.lightmapBakeType = LightmapBakeType.Realtime; EditorUtility.SetDirty(l);
            log.Append("ライト " + l.name + " → Realtime ");
        }
        // 焼きの設定・ライトマップ・Hikari
        var ls = Lightmapping.lightingSettings;
        if (ls != null) { ls.bakedGI = false; ls.realtimeGI = false; EditorUtility.SetDirty(ls); }
        LightmapSettings.lightmaps = new LightmapData[0];
        Lightmapping.lightingDataAsset = null;
        var hk = GameObject.Find("HikariLightmapper");
        if (hk) { Undo.DestroyObjectImmediate(hk); log.Append("Hikari を外した "); }

        FuwaFarViewSetup.RefreshShadowCenters();   // 足した物の位置・どのステージか
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        return log.ToString();
    }

    static void Add(FuwaFarShadows f, List<Renderer> add)
    {
        if (add.Count == 0) return;
        Undo.RecordObject(f, "no bake");
        f.targets = f.targets.Where(r => r).Concat(add).ToArray();
        f.centers = new Vector3[f.targets.Length]; f.radii = new float[f.targets.Length];   // RefreshShadowCenters が入れ直す
        EditorUtility.SetDirty(f);
    }

    // ビルドの時：作り直しで付いた焼きの目印を外す（焼きはしないので、付いていても意味がなく、静的な物のリアルタイムの影の扱いが変わるだけ）
    class NoBakeOnBuild : IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
        {
            if (report == null) return;   // Play では何もしない
            int n = 0;
            foreach (var g in scene.GetRootGameObjects())
                foreach (var r in g.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var f = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                    if ((f & StaticEditorFlags.ContributeGI) == 0) continue;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, f & ~StaticEditorFlags.ContributeGI);
                    r.receiveGI = ReceiveGI.LightProbes; r.lightProbeUsage = LightProbeUsage.Off; n++;
                }
            if (n > 0) Debug.LogWarning("[焼きをやめる] ビルドで焼きの目印を外した " + n + "（作り直した後は「焼きをやめる」メニューを押すと、影の切り替えにも入る）");
        }
    }
}
