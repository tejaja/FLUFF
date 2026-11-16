using UdonSharp;
using UnityEngine;

// タイトルの下の「デバッグモード」（2026-11-16 てじゃ「正式版にするまでのあいだ タイトルの下にボタンふやしといて」
//  「デバッグモードの飛行とふわふわ召喚を使えるようにする」「私含めデフォではデバッグモードなしで」）。公開前に消す。
// 自分だけ（同期なし・保存なし）。オンの間だけ FuwaDebugFly（H / 両トリガー長押しで飛ぶ）と FuwaDebugBallSummon（G でふわふわ）が使える
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaDebugMode : UdonSharpBehaviour
{
    [HideInInspector] public bool on;
    public FuwaSegToggle seg;
    [Tooltip("てじゃ「デバッグ、日本語はカタカナにしといて」：日本語の時は「デバッグ」、英語の時は「DEBUG」")]
    public FuwaLanguage language;

    private int _shownEn = -1;

    private void Start() { on = false; Show(); }

    // ボタン（UI の Button.onClick → SendCustomEvent）から
    public void Toggle() { on = !on; Show(); }

    private void Show() { if (seg != null) seg.Show(on); }

    private void Update()
    {
        if (seg == null || seg.textB == null) return;
        int en = language != null && language.IsEnglish() ? 1 : 0;
        if (en == _shownEn) return;
        _shownEn = en;
        seg.textB.text = en == 1 ? "DEBUG" : "デバッグ";
    }
}
