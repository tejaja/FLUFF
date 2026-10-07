using System.Globalization;
using System.IO;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// コース2「いわのみち」ゴール前の岩場（Rocks2/RockField、BlenderSource/c2field.py）に、噴気孔をばらまく（2026-10-07 夜）。
// 位置は c2field.py が岩場いっぱいにばらまいたもの（BlenderSource/course2_field_vents.json）。口のまわりのクレーターのふちは岩場の床と一体。
// 穴・ふわふわ用の風・人用の上昇気流は序盤の溝の噴気孔（Gimmick_GrooveVents/Vent1）を複製（強さも同じ。ふちの岩 Rim は使わない）。
// ただしずっと吹くのではなく、前の広場の噴気孔と同じく「ボシュッと噴く→止まる」をくり返す。噴気孔ごとにタイミングはバラバラ
// （「突然フワっとさせられる地味ないやがらせ」てじゃ案）。人も噴いている間だけ浮く。見た目は FuwaPlazaGeysers の噴き上げを低くしたもの。
// 何度実行しても同じ結果（作り直す）。作り直した後は NetworkID の整理（ConfigureNetworkIDs）をすること。
public static class FuwaRockFieldVents
{
    const string VentsJson = "BlenderSource/course2_field_vents.json";
    // 噴く秒数・止まる秒数（前の広場と同じ）
    const float OnSec = 2.0f, OffSec = 1.6f;

    [MenuItem("FuwaCourse/Course2/ゴール前の岩場の噴気孔を作る")]
    public static void Menu() { Debug.Log(Build()); }

    static Vector3[] ReadVents()
    {
        var json = File.ReadAllText(VentsJson);
        int i = json.IndexOf("\"vents\"");
        int a = json.IndexOf("[", i), depth = 0, b = a;
        for (; b < json.Length; b++) { if (json[b] == '[') depth++; else if (json[b] == ']' && --depth == 0) break; }
        var nums = json.Substring(a, b - a + 1).Replace("[", " ").Replace("]", " ")
            .Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
            .Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
        var r = new Vector3[nums.Length / 3];
        for (int k = 0; k < r.Length; k++) r[k] = new Vector3(nums[k * 3], nums[k * 3 + 1], nums[k * 3 + 2]);
        return r;
    }

    public static string Build()
    {
        var root = GameObject.Find("FuwaCourse2").transform;
        var src = root.Find("Gimmick_GrooveVents/Vent1");
        if (src == null) return "Vent1 がない";
        var vents = ReadVents();
        var old = root.Find("Gimmick_FieldVents");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var holder = new GameObject("Gimmick_FieldVents").transform;
        holder.SetParent(root, false);
        Undo.RegisterCreatedObjectUndo(holder.gameObject, "field vents");
        var puff = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/SteamPuff.mat");
        var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/FuwaCourse/Materials/VentGlow.mat");
        var hiss = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/FuwaCourse/Audio/sfx_steam_loop.wav");
        var burst = FuwaPlazaGeysers.MakeBurst(false);
        var rnd = new System.Random(12);
        float cycle = OnSec + OffSec;
        for (int k = 0; k < vents.Length; k++)
        {
            var v = Object.Instantiate(src.gameObject, holder).transform;
            v.name = "Vent" + (k + 1);
            v.localPosition = vents[k];
            v.localRotation = Quaternion.Euler(0, (float)(rnd.NextDouble() * 360.0), 0);
            // 溝の噴気孔のずっと出ている湯気・上昇の筋・ふちの岩は外して、噴く時だけの見た目にする
            foreach (var n in new[] { "SteamFx", "UpdraftStreaks", "UpdraftPuffs", "UpdraftSwirl", "Rim" })
            {
                var t = v.Find(n); if (t != null) Object.DestroyImmediate(t.gameObject);
            }
            var fx = new GameObject("Fx").transform; fx.SetParent(v, false);
            fx.localPosition = new Vector3(0f, 0.05f, 0f);
            var erupt = FuwaPlazaGeysers.MakeErupt(fx, puff);
            {
                // 風の届く高さ（2.3m）に合わせて低い噴き上げに
                var main = erupt.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(4.2f, 5.5f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
                var em = erupt.emission; em.rateOverTime = 30f;
            }
            var jet = FuwaPlazaGeysers.MakeJet(fx, puff);
            {
                var main = jet.main;
                main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 9f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
            }
            FuwaPlazaGeysers.MakeIdle(fx, puff);
            FuwaPlazaGeysers.MakeGlow(fx, glow);
            var boom = FuwaPlazaGeysers.MakeAudio(fx, "Burst", burst, false, 0.32f, 14f, 1.0f + 0.15f * (float)rnd.NextDouble());
            // シュー音は数が多いので一部だけ
            if (k % 3 == 0) FuwaPlazaGeysers.MakeAudio(fx, "Hiss", hiss, true, 0.06f, 7f, 0.9f + 0.03f * k);

            var zone = v.Find("Zone").GetComponent<FuwaWindArea>();
            zone.onSeconds = OnSec;
            zone.offSeconds = OffSec;
            zone.phaseOffset = (float)(rnd.NextDouble() * cycle);
            zone.gustCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.12f, 1f), new Keyframe(0.75f, 0.9f), new Keyframe(1f, 0f));
            zone.baseLevel = 0f; zone.calmSeconds = 0f;   // 止まっている間は完全に止む
            zone.warnSeconds = 0.5f; zone.warnLevel = 0.12f;
            zone.visual = erupt.gameObject;
            zone.extraVisuals = new[] { jet };
            zone.fadeSeconds = 0.15f; zone.visualLead = 0.12f;
            zone.gustSound = boom;
            UdonSharpEditorUtility.CopyProxyToUdon(zone);
            var lift = v.Find("PlayerLift").GetComponent<FuwaPlayerUpdraft>();
            lift.onlyWhileBlowing = zone;
            UdonSharpEditorUtility.CopyProxyToUdon(lift);
        }
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return "field vents " + vents.Length;
    }
}
