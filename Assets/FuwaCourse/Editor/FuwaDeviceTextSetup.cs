using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

// 操作の説明を端末ごとの書き方（{vr|…}{pc|…}{mo|…}）に置きかえる（2026-10-14 てじゃ「VRとデスクトップの併記、端末で表示を変えたらスッキリ」）。何度実行してもよい
// ・FuwaLanguage の ja/en（とその TMP の今の文字）を、下の表の通りに置きかえる。TMP の今の文字は PC 用に展開しておく（実行時は FuwaLanguage が端末に合わせて書く）
// ・チュートリアルのエアガン看板・あそびばの説明は、それぞれのビルダーの文字も直してある（ここでは今のシーンの分を直すだけ）
public static class FuwaDeviceTextSetup
{
    static readonly string[,] Reps =
    {
        // ロビーのあそびかた
        { "<pos=1.1em>VR：トリガー／PC：左クリック", "<pos=1.1em>{vr|トリガーで持つ}{pc|左クリックで持つ}{mo|タップで持つ}" },
        { "</size><color=#FF6B5C>●</color><pos=1.1em>エアガンを手元に呼ぶ\n<pos=1.1em>PC：Fキー／VR：右スティック下\n<pos=1.1em>台をクリックで台に戻る",
          "</size><color=#FF6B5C>●</color><pos=1.1em>{pc,vr|エアガンを手元に呼ぶ}{mo|エアガンを呼ぶ}\n<pos=1.1em>{pc|Fキーを押す}{vr|右スティックを下に倒し続ける}{mo|台をタップすると台に来る}\n<pos=1.1em>{pc|台をクリックで台に戻る}{vr|台をトリガーで台に戻る}{mo|（どの台からでも呼べる）}" },
        { "<pos=1.1em>VR: trigger / PC: left-click", "<pos=1.1em>{vr|Grab with the trigger}{pc|Left-click to grab}{mo|Tap to grab}" },
        { "</size><color=#FF6B5C>●</color><pos=1.1em>Summon your air gun\n<pos=1.1em>PC: F / VR: hold right stick ↓\n<pos=1.1em>Click a stand to call it there",
          "</size><color=#FF6B5C>●</color><pos=1.1em>{pc,vr|Summon your air gun}{mo|Call your air gun}\n<pos=1.1em>{pc|Press F}{vr|Hold the right stick down}{mo|Tap a stand to call it there}\n<pos=1.1em>{pc|Click a stand to call it there}{vr|Trigger a stand to call it there}{mo|(works at any stand)}" },
        // チュートリアルのエアガン看板（FuwaPracticeTutorial.GunSignJa/En と同じ）
        { "持つ：VR トリガー／PC 左クリック\n手元に呼ぶ：PC Fキー／VR 右スティック下\n台をクリックで台にもどる",
          "持つ：{vr|トリガー}{pc|左クリック}{mo|タップ}\n{pc|手元に呼ぶ：Fキー}{vr|手元に呼ぶ：右スティック下}{mo|どの台からでも呼べる}\n台を{pc|クリック}{vr|トリガー}{mo|タップ}で台にもどる" },
        { "Hold: VR trigger / PC left click\nCall it: PC F key / VR right stick down\nClick the stand to send it back",
          "Hold: {vr|trigger}{pc|left click}{mo|tap}\n{pc|Call it: F key}{vr|Call it: hold right stick down}{mo|Call it from any stand}\n{pc|Click}{vr|Trigger}{mo|Tap} the stand to send it back" },
        // あそびばの説明（FuwaPlaygroundBuilder と同じ）
        { "エアガンを手元に呼ぶ：PCはFキー\n<pos=1.1em>VRは右スティックを下に倒し続ける",
          "{pc,vr|エアガンを手元に呼ぶ}{mo|エアガンは台をタップで呼べる}\n<pos=1.1em>{pc|Fキーを押す}{vr|右スティックを下に倒し続ける}{mo|（どの台からでも）}" },
        { "Summon your air gun: F key (PC)\n<pos=1.1em>or hold the right stick down (VR)",
          "{pc,vr|Summon your air gun}{mo|Tap a stand to call your air gun}\n<pos=1.1em>{pc|Press the F key}{vr|Hold the right stick down}{mo|(works at any stand)}" },
        // エアガンの台の札
        { "<size=60%>クリックで呼ぶ</size>", "<size=60%>{pc|クリック}{vr|トリガー}{mo|タップ}で呼ぶ</size>" },
        { "<size=60%>Click to summon</size>", "<size=60%>{pc|Click}{vr|Trigger}{mo|Tap} to summon</size>" },
        // スマホ：右上のボタン（FuwaMobileGunButton）の案内を足す（2026-10-14）。上の行で置きかえた後の文字をさらに置きかえる
        { "{mo|台をタップすると台に来る}\n<pos=1.1em>{pc|台をクリックで台に戻る}{vr|台をトリガーで台に戻る}{mo|（どの台からでも呼べる）}", "{mo|画面右上のボタンで手元に来る}\n<pos=1.1em>{pc|台をクリックで台に戻る}{vr|台をトリガーで台に戻る}{mo|台をタップすると台に来る}" },
        { "{mo|Tap a stand to call it there}\n<pos=1.1em>{pc|Click a stand to call it there}{vr|Trigger a stand to call it there}{mo|(works at any stand)}", "{mo|Tap the top-right button}\n<pos=1.1em>{pc|Click a stand to call it there}{vr|Trigger a stand to call it there}{mo|Tap a stand to call it there}" },
        { "{mo|どの台からでも呼べる}", "{mo|手元に呼ぶ：右上のボタン}" },
        { "{mo|Call it from any stand}", "{mo|Call it: top-right button}" },
        { "{mo|エアガンは台をタップで呼べる}", "{mo|エアガンは右上のボタンで呼べる}" },
        { "{mo|（どの台からでも）}", "{mo|（台をタップでも呼べる）}" },
        { "{mo|Tap a stand to call your air gun}", "{mo|Call your air gun: top-right button}" },
        { "{mo|(works at any stand)}", "{mo|(or tap a stand)}" },
    };

    [MenuItem("FuwaCourse/操作の説明を端末ごとの書き方にする")]
    public static void Menu() { Debug.Log(Run()); }

    public static string Run()
    {
        int changed = 0;
        foreach (var lang in Object.FindObjectsOfType<FuwaLanguage>(true))
        {
            var so = new SerializedObject(lang);
            foreach (var (arr, txs) in new[] { ("ja", "texts"), ("en", "texts"), ("uiJa", "uiTexts"), ("uiEn", "uiTexts") })
            {
                var a = so.FindProperty(arr); var t = so.FindProperty(txs);
                for (int i = 0; i < a.arraySize; i++)
                {
                    var e = a.GetArrayElementAtIndex(i);
                    string s = e.stringValue, n = s;
                    for (int r = 0; r < Reps.GetLength(0); r++) n = n.Replace(Reps[r, 0], Reps[r, 1]);
                    if (n == s) continue;
                    e.stringValue = n; changed++;
                    // 日本語の分は、TMP の今の文字も PC 用に展開して入れておく
                    if (arr == "ja" || arr == "uiJa")
                    {
                        var tmp = i < t.arraySize ? t.GetArrayElementAtIndex(i).objectReferenceValue as TMPro.TMP_Text : null;
                        if (tmp != null) { tmp.text = Expand(n, "pc"); EditorUtility.SetDirty(tmp); }
                    }
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            UdonSharpEditorUtility.CopyProxyToUdon(lang);
            EditorUtility.SetDirty(lang);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lang.gameObject.scene);
        }
        return "changed " + changed;
    }

    // FuwaLanguage.ForDevice と同じ展開（エディタ用）
    public static string Expand(string s, string key)
    {
        var o = new System.Text.StringBuilder(); int i = 0; key = "," + key + ",";
        while (i < s.Length)
        {
            int a = s.IndexOf('{', i);
            if (a < 0) { o.Append(s.Substring(i)); break; }
            int bar = s.IndexOf('|', a), c = s.IndexOf('}', a);
            if (bar < 0 || c < 0 || bar > c) { o.Append(s.Substring(i, a - i + 1)); i = a + 1; continue; }
            o.Append(s.Substring(i, a - i));
            if (("," + s.Substring(a + 1, bar - a - 1) + ",").Contains(key)) o.Append(s.Substring(bar + 1, c - bar - 1));
            i = c + 1;
        }
        return o.ToString();
    }
}
