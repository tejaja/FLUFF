using TMPro;
using UdonSharp;
using UnityEngine;

// ロビーの説明ボードとタイトルの「きれい / ふつう / かるい」ボタン（自分だけ、同期なし）。FuwaCourseLod.mode を切り替える。
// 2026-10-22 てじゃ「これゲーム内できりかえられたらいいのに」「3段かなあ」。
// 見た目は おっかけカメラのボタンと同じ「がしつ ◀ ふつう ▶」（2026-10-22 てじゃ「なんとか画質設定であるってことを付け加えたい、でも長さが足りない」
// 「がしつ：◁ふつう▷ みたいに」「おっかけカメラにつかってるやつみたいな」）。札の左半分で前、右半分で次（端まで行ったら反対の端へ）。
// 3つの名前は同じ場所に重ねて、選んでいる1つだけ出す（英語の切り替えは FuwaLanguage の UI 文字の一覧のまま）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaQualitySwitch : UdonSharpBehaviour
{
    public FuwaCourseLod lod;
    [Tooltip("きれい・ふつう・かるい の順。同じ場所に重ねてあって、選んでいる1つだけ出す")]
    public TextMeshProUGUI[] texts;

    private void Start() { SendCustomEventDelayedFrames(nameof(Refresh), 2); }   // FuwaCourseLod の Start（スマホは かるい）のあとで

    public void SetKirei() { Set(0); }
    public void SetFutsuu() { Set(1); }
    public void SetKarui() { Set(2); }
    public void Prev() { Set((Cur() + 2) % 3); }
    public void Next() { Set((Cur() + 1) % 3); }
    private int Cur() { return lod != null ? lod.GetMode() : _shown; }
    private int _shown = 1;

    // FuwaCourseLod が保存・ほかのボタンの表示合わせまでやる（ボタンはロビーの説明ボードとタイトルの2か所）
    private void Set(int m)
    {
        if (lod != null) lod.SetMode(m);
        else Show(m);
    }

    public void Refresh() { Show(lod != null ? lod.GetMode() : 1); }

    private void Show(int m)
    {
        _shown = m;
        for (int i = 0; i < 3; i++)
            if (texts != null && i < texts.Length && texts[i] != null) texts[i].enabled = i == m;
    }
}
