using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 風の筋（FuwaCourse/WindStreak のトレイル）を、正面（風の向きに沿って）から見た時に消すため、風ごとの向き（_WindDir）を入れたマテリアルにする。
// 2026-11-26 てじゃ「1-1最初の追い風を正面から見てる時、真正面にある風エフェクトがちょと変」→ 筋が縮んで丸いもやの塊に見えていた。
//  ・向きはパーティクルの速さ（Velocity over Lifetime、なければ Start Speed＋形の向き）から
//  ・同じ元のマテリアル・同じ向き（10°ごと）の風は同じマテリアルを使う（Generated/WindStreakDir/）。元のマテリアルの設定は毎回写し直す
//  ・風のしかけを作り直したら元のマテリアルに戻るので、もう一度このメニューを押す
public static class FuwaWindStreakDir
{
    const string Dir = "Assets/FuwaCourse/Generated/WindStreakDir";

    [MenuItem("FuwaCourse/済み（押す前に相談）/風の筋：正面から見た時に消す（向きを入れる）")]
    static void Menu() { Debug.Log(Apply()); }

    public static string Apply()
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/FuwaCourse/Generated", "WindStreakDir");
        int n = 0; var made = new HashSet<string>();
        foreach (var r in Object.FindObjectsOfType<ParticleSystemRenderer>(true))
        {
            var ps = r.GetComponent<ParticleSystem>();
            if (ps == null || !ps.trails.enabled || r.trailMaterial == null || !r.trailMaterial.shader.name.Contains("WindStreak")) continue;
            var baseMat = BaseOf(r.trailMaterial);
            // 2026-11-28 てじゃ「いわのみち奥の追い風がぜんぜんみえない」：道に沿って吹くので後ろから見ると消えていた。追い風の中は走らないので正面消しなし（元のマテリアル）
            if (NoHeadOnFade(r.transform))
            {
                if (r.trailMaterial != baseMat) { Undo.RecordObject(r, "wind dir"); r.trailMaterial = baseMat; EditorUtility.SetDirty(r); }
                continue;
            }
            Vector3 d = WindDir(ps);
            if (d.sqrMagnitude < 1e-6f) continue;
            // 10°ごとにまとめる
            float yaw = Mathf.Round(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg / 10f) * 10f;
            float pitch = Mathf.Round(Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg / 10f) * 10f;
            Vector3 q = Quaternion.Euler(-pitch, yaw, 0f) * Vector3.forward;
            string path = Dir + "/" + baseMat.name + "_y" + ((int)yaw).ToString() + "_p" + ((int)pitch).ToString() + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(baseMat); AssetDatabase.CreateAsset(m, path); }
            if (made.Add(path))
            {
                m.shader = baseMat.shader; m.CopyPropertiesFromMaterial(baseMat); m.renderQueue = baseMat.renderQueue; m.enableInstancing = baseMat.enableInstancing;
                m.SetVector("_WindDir", new Vector4(q.x, q.y, q.z, 1f));
                EditorUtility.SetDirty(m);
            }
            if (r.trailMaterial != m) { Undo.RecordObject(r, "wind dir"); r.trailMaterial = m; EditorUtility.SetDirty(r); }
            n++;
        }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        return "[風の筋] " + n + " 個の風に向きを入れた（マテリアル " + made.Count + " 個）";
    }

    static readonly string[] NoFadeParents = { "FuwaCourse2/Gimmick_Tailwind" };
    static bool NoHeadOnFade(Transform t)
    {
        string p = t.name; for (var x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
        return NoFadeParents.Any(s => p.StartsWith(s + "/"));
    }

    // Generated の向きつきマテリアルなら、元のマテリアル（名前の _y.. より前）
    static Material BaseOf(Material m)
    {
        string p = AssetDatabase.GetAssetPath(m);
        if (!p.StartsWith(Dir)) return m;
        string name = m.name; int i = name.LastIndexOf("_y"); if (i > 0) name = name.Substring(0, i);
        var guid = AssetDatabase.FindAssets(name + " t:Material").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(x => !x.StartsWith(Dir) && System.IO.Path.GetFileNameWithoutExtension(x) == name);
        return guid != null ? AssetDatabase.LoadAssetAtPath<Material>(guid) : m;
    }

    static Vector3 WindDir(ParticleSystem ps)
    {
        var vol = ps.velocityOverLifetime;
        Vector3 v = Vector3.zero;
        if (vol.enabled)
        {
            v = new Vector3(vol.x.constantMax, vol.y.constantMax, vol.z.constantMax);
            if (vol.x.mode == ParticleSystemCurveMode.TwoConstants || vol.x.mode == ParticleSystemCurveMode.Constant) v.x = (vol.x.constantMin + vol.x.constantMax) * (vol.x.mode == ParticleSystemCurveMode.TwoConstants ? 0.5f : 1f);
            if (vol.y.mode == ParticleSystemCurveMode.TwoConstants || vol.y.mode == ParticleSystemCurveMode.Constant) v.y = (vol.y.constantMin + vol.y.constantMax) * (vol.y.mode == ParticleSystemCurveMode.TwoConstants ? 0.5f : 1f);
            if (vol.z.mode == ParticleSystemCurveMode.TwoConstants || vol.z.mode == ParticleSystemCurveMode.Constant) v.z = (vol.z.constantMin + vol.z.constantMax) * (vol.z.mode == ParticleSystemCurveMode.TwoConstants ? 0.5f : 1f);
            v = vol.space == ParticleSystemSimulationSpace.Local ? ps.transform.TransformDirection(v) : v;
        }
        if (v.sqrMagnitude < 1e-4f)
        {
            var sh = ps.shape;
            v = ps.transform.TransformDirection(Quaternion.Euler(sh.rotation) * Vector3.forward) * ps.main.startSpeed.constantMax;
        }
        return v.normalized;
    }
}
