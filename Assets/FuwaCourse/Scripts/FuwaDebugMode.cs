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

    private void Start() { on = false; Show(); }

    // ボタン（UI の Button.onClick → SendCustomEvent）から
    public void Toggle() { on = !on; Show(); }

    private void Show() { if (seg != null) seg.Show(on); }
}
