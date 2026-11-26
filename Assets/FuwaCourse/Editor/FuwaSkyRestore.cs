using System.Linq;
using UnityEditor;
using UnityEngine;

// 空のマテリアル（FuwaSky）は、プレイ中に FuwaSkyZone（やみのもり）や FuwaStageSky（ステージ2の白い空）が色を直接書きかえる。
// マテリアルはアセットなので、プレイをやめても最後の色（2–2 の灰色の空など）のまま残って、エディタの空がずっとそれになっていた（2026-11-26 てじゃ「ずっとエディタの空が2-2のやつのまま」）。
// → プレイをやめた時と、メニューで、ふつうの空の色（SkyZones/Yami の normal の色）にもどす。実行中は Start でいつも正しい色にするので、ゲームには関係ない
[InitializeOnLoad]
public static class FuwaSkyRestore
{
    static FuwaSkyRestore()
    {
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredEditMode) Restore(); };
    }

    [MenuItem("FuwaCourse/済み（押す前に相談）/空の色をふつうにもどす（エディタ）")]
    static void Menu() { Debug.Log(Restore()); }

    public static string Restore()
    {
        var z = Object.FindObjectsOfType<FuwaSkyZone>(true).FirstOrDefault(x => x.sky != null);
        if (z == null) return "[空] FuwaSkyZone がない";
        var m = z.sky;
        bool changed = m.GetColor("_TopColor") != z.normalTop || m.GetColor("_HorizonColor") != z.normalHorizon || m.GetColor("_BottomColor") != z.normalBottom
                    || m.GetColor("_SunColor") != z.normalSun || !Mathf.Approximately(m.GetFloat("_SunGlow"), z.normalSunGlow);
        if (!changed) return "[空] そのまま";
        m.SetColor("_TopColor", z.normalTop); m.SetColor("_HorizonColor", z.normalHorizon); m.SetColor("_BottomColor", z.normalBottom);
        m.SetColor("_SunColor", z.normalSun); m.SetFloat("_SunGlow", z.normalSunGlow);
        EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m);
        return "[空] ふつうの色にもどした";
    }
}
