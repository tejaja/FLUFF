using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase.Editor.BuildPipeline;

// 2026-11-23 てじゃ「いわのみちの噴気孔の音がステージ１中にきこえてるかも」
//  VRChat は VRCSpatialAudioSource が付いていない音に、実行時に既定の設定（Far 40m・Gain 10dB）を付ける。
//  そのため AudioSource の maxDistance（湯気 12m など）が効かず、40m 先まで聞こえていた。
//  → 3D の音（spatialBlend 0.5 以上）で VRCSpatialAudioSource が無い物に付けて、Far を AudioSource の maxDistance に合わせる。
//    Gain は既定と同じ 10（今までの近くの大きさのまま）、Near は minDistance。
//  ビルドの前に毎回自動で（作り直しで付け忘れても大丈夫）。メニューからも
public class FuwaSpatialAudioFix : IVRCSDKBuildRequestedCallback
{
    public int callbackOrder => 0;
    public bool OnBuildRequested(VRCSDKRequestedBuildType requestedBuildType)
    {
        if (requestedBuildType == VRCSDKRequestedBuildType.Scene) { Debug.Log(Run()); Debug.Log(MobileShadowFix()); }
        return true;
    }

    // 2026-11-24 11/23 にこのスクリプトが付けた分（UseAudioSourceVolumeCurve=false で付いていた）を AudioSource の曲線に切りかえる。
    //  11/23 より前からあった物（Library/spatial_before_ids.txt＝その時のシーンの GameObject の fileID）は、手で調整した物なのでさわらない
    [MenuItem("FuwaCourse/音/11-23に付けた立体音響をUnityの曲線に戻す")]
    public static void MigrateMenu() { Debug.Log(Migrate()); }
    public static string Migrate()
    {
        var path = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Library/spatial_before_ids.txt");
        var before = new System.Collections.Generic.HashSet<ulong>(System.IO.File.Exists(path) ? System.IO.File.ReadAllLines(path).Where(l => l.Trim().Length > 0).Select(l => unchecked((ulong)long.Parse(l.Trim()))) : new ulong[0]);
        if (before.Count == 0) return "[音] 前の一覧がない";
        int n = 0, keep = 0;
        foreach (var sp in Resources.FindObjectsOfTypeAll<VRCSpatialAudioSource>())
        {
            if (!sp || !sp.gameObject.scene.IsValid()) continue; var a = sp.GetComponent<AudioSource>(); if (!a) continue;
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(sp.gameObject);
            if (before.Contains(gid.targetObjectId)) { keep++; continue; }
            if (sp.UseAudioSourceVolumeCurve || Mathf.Abs(sp.Gain - 10f) > 0.01f) continue;
            Undo.RecordObject(sp, "curve"); sp.UseAudioSourceVolumeCurve = true; EditorUtility.SetDirty(sp); n++;
        }
        if (n > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        return "[音] Unity の曲線に切りかえ " + n + "・前からの物 " + keep;
    }

    [MenuItem("FuwaCourse/音/3Dの音の聞こえる範囲を maxDistance に合わせる")]
    static void Menu() { Debug.Log(Run()); }

    // 2026-11-23 てじゃ「2-1の入口の岩肌あたりが、スマホだとZファイトに似てるちらつき」（軽い版の重なり消しでは変わらず）
    //  ワールドで1つだけ影を両面で落としている 2-1 の岩山（CaveRock、PC で光もれを防ぐため）が、スマホの粗い影で自分に縞の影（シャドウアクネ）を落としていた疑い
    //  → Android のビルドの時だけ片面の影に（PC はそのまま両面）
    public static string MobileShadowFix()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android && EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS) return "[影] PC なのでそのまま";
        int n = 0;
        foreach (var r in Resources.FindObjectsOfTypeAll<MeshRenderer>())
        {
            if (!r || !r.gameObject.scene.IsValid() || r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.TwoSided) continue;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; EditorUtility.SetDirty(r); n++;
        }
        return "[影] スマホ：両面の影を片面に " + n;
    }

    public static string Run()
    {
        int n = 0;
        foreach (var a in Resources.FindObjectsOfTypeAll<AudioSource>())
        {
            if (!a || !a.gameObject.scene.IsValid() || a.spatialBlend < 0.5f) continue;
            if (a.GetComponent<VRCSpatialAudioSource>()) continue;
            var sp = Undo.AddComponent<VRCSpatialAudioSource>(a.gameObject);
            sp.Gain = 10f; sp.Near = Mathf.Max(0f, a.minDistance); sp.Far = Mathf.Max(sp.Near + 0.1f, a.maxDistance);
            // 2026-11-24 てじゃ「クリスタルのチャージとか攻撃の音がめっちゃちいさくなってる」→ VRChat 式の小さくなり方（距離の2乗）だと、
            //  Unity で調整した AudioSource の小さくなり方（Linear 等）が効かずに近くでも小さかった → AudioSource の曲線を使う（Far の制限は maxDistance で効く）
            sp.EnableSpatialization = true; sp.UseAudioSourceVolumeCurve = true;
            EditorUtility.SetDirty(sp); n++;
        }
        int lg = LogToFade();
        if (n > 0 || lg > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        return "[音] VRCSpatialAudioSource を付けた（Far＝maxDistance）：" + n + "・Logarithmic を maxDistance で消える曲線に " + lg;
    }

    // 2026-11-24 てじゃ「いわのみちの噴気孔がロビーまで聞こえてくる」：AudioSource の曲線を使うようにしたら、Logarithmic の音は
    //  Unity の仕様で maxDistance より先も（max の所の大きさのまま）ずっと鳴る → 同じ形（min/d）のまま maxDistance で 0 になる Custom の曲線に
    public static int LogToFade()
    {
        int n = 0;
        foreach (var sp in Resources.FindObjectsOfTypeAll<VRCSpatialAudioSource>())
        {
            if (!sp || !sp.gameObject.scene.IsValid() || !sp.UseAudioSourceVolumeCurve) continue;
            var a = sp.GetComponent<AudioSource>(); if (!a || a.rolloffMode != AudioRolloffMode.Logarithmic || a.spatialBlend < 0.01f) continue;
            float mn = Mathf.Max(0.01f, a.minDistance), mx = Mathf.Max(mn + 0.1f, a.maxDistance);
            var keys = new System.Collections.Generic.List<Keyframe>();
            const int N = 24;
            for (int i = 0; i <= N; i++)
            {
                float t = (float)i / N, d = t * mx;
                float v = Mathf.Min(1f, mn / Mathf.Max(1e-3f, d)) * (1f - t * t);   // 対数の小さくなり方 × 最後は 0 へ
                keys.Add(new Keyframe(t, i == N ? 0f : v));
            }
            var c = new AnimationCurve(keys.ToArray());
            for (int i = 0; i < c.length; i++) c.SmoothTangents(i, 0f);
            Undo.RecordObject(a, "fade"); a.rolloffMode = AudioRolloffMode.Custom; a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, c); EditorUtility.SetDirty(a); n++;
        }
        return n;
    }
}
