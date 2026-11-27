using System.Collections.Generic;
using System.Linq;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 2026-11-23 英語チェック（てじゃ「英語もチェックかな」）：ホバーの文字（持てる物の InteractionText / UseText、クリックする物の Interact の文字）を
//  FuwaLanguage の hoverPickups / hoverInteracts に集めて、日本語→英語の表で英語を入れる。
//  自分で切り替えている物（銃・銃の台・きんピカ・おまけのボタン）は入れない。新しいおもちゃを足したら、表に足してもう一度押す
public static class FuwaHoverTextLang
{
    static readonly Dictionary<string, string> En = new Dictionary<string, string>
    {
        { "せんぷうき", "Fan" }, { "風 ON/OFF", "Wind ON/OFF" },
        { "ふわふわ", "Fluff" }, { "ひょうじょう", "Face" },
        { "ポータル", "Portal" }, { "はねるキノコ", "Bouncy Mushroom" }, { "いわ", "Rock" }, { "リング", "Ring" },
        { "重力のたね", "Gravity Seed" }, { "竜巻のたね", "Tornado Seed" }, { "ようがん", "Lava" }, { "こおり", "Ice" },
        { "かい", "Clam" }, { "あわ", "Bubbles" }, { "はねぐるま", "Pinwheel" }, { "きんピカふわふわ", "Golden Fluff" },
        { "まもののぬいぐるみ（トリガーで起こす・寝かせる）", "Monster Plush (trigger to wake / sleep)" }, { "起こす・寝かせる", "Wake / Sleep" }, { "まもの", "Monster" },
        { "オン / オフ", "ON / OFF" }, { "かいほう", "Unlock" }, { "アンロック", "Unlock" },
        { "竹とんぼ", "Copter" }, { "飛ぶ", "Fly" }, { "竹とんぼをもどす", "Return copter" },
        { "ボード", "Board" }, { "もじ", "Text" },
    };

    static bool JP(string s) => !string.IsNullOrEmpty(s) && s.Any(c => (c >= 0x3040 && c <= 0x30ff) || (c >= 0x4e00 && c <= 0x9fff));

    [MenuItem("FuwaCourse/英語/ホバーの文字を登録")]
    public static void Run()
    {
        var lang = Resources.FindObjectsOfTypeAll<FuwaLanguage>().FirstOrDefault(l => l && l.gameObject.scene.IsValid());
        if (!lang) { Debug.LogError("FuwaLanguage がない"); return; }
        var missing = new HashSet<string>();
        string E(string ja) { if (string.IsNullOrEmpty(ja)) return ""; if (!JP(ja)) return ja; if (En.TryGetValue(ja, out var e)) return e; missing.Add(ja); return ""; }

        // 持てる物：銃（FuwaBlower が自分で切り替え）は除く
        var pks = new List<VRC.SDK3.Components.VRCPickup>(); var pj = new List<string>(); var pe = new List<string>(); var uj = new List<string>(); var ue = new List<string>();
        // 前に登録した日本語（今の文字が英語になっていても日本語で登録しなおす）
        var oldP = new Dictionary<VRC.SDK3.Components.VRCPickup, (string, string)>();
        if (lang.hoverPickups != null) for (int i = 0; i < lang.hoverPickups.Length; i++) if (lang.hoverPickups[i]) oldP[lang.hoverPickups[i]] = (lang.hoverPickupJa[i], lang.hoverUseJa[i]);
        foreach (var p in Resources.FindObjectsOfTypeAll<VRC.SDK3.Components.VRCPickup>())
        {
            if (!p || !p.gameObject.scene.IsValid() || p.GetComponent<FuwaBlower>()) continue;
            string ij = p.InteractionText, us = p.UseText;
            if (oldP.TryGetValue(p, out var o)) { ij = o.Item1; us = o.Item2; }
            if (!JP(ij) && !JP(us)) continue;
            pks.Add(p); pj.Add(ij); pe.Add(E(ij)); uj.Add(us); ue.Add(E(us));
        }
        // クリックする物：自分で切り替えている物は除く
        var skip = new[] { typeof(FuwaGunStand), typeof(FuwaLanguage), typeof(FuwaGoldGunSwitch), typeof(FuwaBonusButton) };
        var its = new List<UdonSharpBehaviour>(); var ij2 = new List<string>(); var ie2 = new List<string>();
        var oldI = new Dictionary<UdonSharpBehaviour, string>();
        if (lang.hoverInteracts != null) for (int i = 0; i < lang.hoverInteracts.Length; i++) if (lang.hoverInteracts[i]) oldI[lang.hoverInteracts[i]] = lang.hoverInteractJa[i];
        foreach (var ub in Resources.FindObjectsOfTypeAll<VRC.Udon.UdonBehaviour>())
        {
            if (!ub || !ub.gameObject.scene.IsValid()) continue;
            var us = UdonSharpEditorUtility.GetProxyBehaviour(ub); if (!us || skip.Contains(us.GetType())) continue;
            string t = ub.interactText; if (oldI.TryGetValue(us, out var oj)) t = oj;
            if (!JP(t)) continue;
            its.Add(us); ij2.Add(t); ie2.Add(E(t));
        }
        Undo.RecordObject(lang, "hover text");
        lang.hoverPickups = pks.ToArray(); lang.hoverPickupJa = pj.ToArray(); lang.hoverPickupEn = pe.ToArray(); lang.hoverUseJa = uj.ToArray(); lang.hoverUseEn = ue.ToArray();
        lang.hoverInteracts = its.ToArray(); lang.hoverInteractJa = ij2.ToArray(); lang.hoverInteractEn = ie2.ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(lang); EditorUtility.SetDirty(lang);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lang.gameObject.scene);
        Debug.Log("[英語] ホバーの文字：持てる物 " + pks.Count + "・クリックする物 " + its.Count + (missing.Count > 0 ? "　英語がない：" + string.Join("、", missing) : "　英語は全部あり"));
    }
}
