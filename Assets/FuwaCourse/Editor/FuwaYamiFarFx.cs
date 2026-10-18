using UnityEditor;
using UnityEngine;

// 遠くから見たやみのもり（2026-10-18 てじゃ「遠くからみたあのエフェクト、主張つよいからもっとささやかに」）。
// 暗いもや（YamiMist.mat、シェーダー FuwaCourse/DarkMist）を薄く・少し明るい紫にして、もやの中で大きな濃淡がゆら〜っと流れるように（_Drift）。
// 粒（もやのかたまり・ほたる）も試したが、描く物が増えるのでやめた（てじゃ「やっぱ増えるのやだな」）→ もや1つのまま
public static class FuwaYamiFarFx
{
    public static float MistDensity = 0.033f, MistMaxAlpha = 0.55f, Drift = 0.85f, DriftPeak = 2.4f, DriftScale = 0.085f, DriftSpeed = 0.035f;   // 濃い所 1.7→2.4（てじゃ「濃いところもうちょい濃く」）。模様 0.035→0.085（もや全体が1つの模様に入って「全部消える・全部見える」時があった）
    public static Color MistColor = new Color(0.42f, 0.38f, 0.58f, 1f);

    [MenuItem("FuwaCourse/よく使う/遠くのやみのもり：もやを薄く＋ゆらぐ濃淡")]
    public static void Menu() { Debug.Log(Build()); }

    public static string Build()
    {
        var mist = GameObject.Find("FuwaCourse3")?.transform.Find("YamiMist");
        if (!mist) return "FuwaCourse3/YamiMist がない";
        var mm = mist.GetComponent<MeshRenderer>().sharedMaterial;
        Undo.RecordObject(mm, "yami far");
        mm.SetFloat("_Density", MistDensity); mm.SetFloat("_MaxAlpha", MistMaxAlpha); mm.SetColor("_Color", MistColor); mm.SetFloat("_Drift", Drift); mm.SetFloat("_DriftPeak", DriftPeak); mm.SetFloat("_DriftScale", DriftScale); mm.SetFloat("_DriftSpeed", DriftSpeed);
        EditorUtility.SetDirty(mm);
        // 前に試した粒は消す
        int removed = 0;
        foreach (var n in new[] { "FarWisps", "FarFireflies" }) { var t = mist.Find(n); if (t) { Undo.DestroyObjectImmediate(t.gameObject); removed++; } }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mist.gameObject.scene);
        return "[遠くのやみのもり] もや 濃さ" + MistDensity + "・最大" + MistMaxAlpha + "・ゆらぎ" + Drift + (removed > 0 ? "、粒 " + removed + " 個を消した" : "");
    }
}
