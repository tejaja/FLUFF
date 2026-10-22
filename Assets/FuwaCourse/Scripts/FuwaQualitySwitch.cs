using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// ロビーの説明ボードとタイトルの「きれい / ふつう / かるい」ボタン（自分だけ、同期なし）。FuwaCourseLod.mode を切り替える。
// 2026-10-22 てじゃ「これゲーム内できりかえられたらいいのに」「3段かなあ」。見た目は FuwaSegToggle と同じ（選んでいる方だけ白い札＋ボタンの色の文字）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaQualitySwitch : UdonSharpBehaviour
{
    public FuwaCourseLod lod;
    [Tooltip("きれい・ふつう・かるい の順")]
    public Image[] pills;
    public TextMeshProUGUI[] texts;
    public Color activeTextColor = new Color(0.45f, 0.78f, 0.62f, 1f);
    public Color idleTextColor = new Color(1f, 1f, 1f, 0.75f);

    private void Start() { SendCustomEventDelayedFrames(nameof(Refresh), 2); }   // FuwaCourseLod の Start（スマホは かるい）のあとで

    public void SetKirei() { Set(0); }
    public void SetFutsuu() { Set(1); }
    public void SetKarui() { Set(2); }

    // FuwaCourseLod が保存・ほかのボタンの表示合わせまでやる（ボタンはロビーの説明ボードとタイトルの2か所）
    private void Set(int m)
    {
        if (lod != null) lod.SetMode(m);
        else Show(m);
    }

    public void Refresh() { Show(lod != null ? lod.GetMode() : 1); }

    private void Show(int m)
    {
        for (int i = 0; i < 3; i++)
        {
            if (pills != null && i < pills.Length && pills[i] != null) pills[i].enabled = i == m;
            if (texts != null && i < texts.Length && texts[i] != null) texts[i].color = i == m ? activeTextColor : idleTextColor;
        }
    }
}
