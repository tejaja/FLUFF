using TMPro;
using UdonSharp;
using UnityEngine;

// 「◀ ○○ ▶」のボタンで、レーザー／カーソルが当たっている側の矢印だけ色を濃くする（自分だけ、同期なし）。
// 2026-10-22 てじゃ「各種◁◯◯▷のボタン、レーザーあてたときだけ対応した◁が色かわるとかできないかな」。
// 左右の押す所に付けた EventTrigger（PointerEnter / PointerExit）から呼ぶ。作るのは Editor/FuwaArrowHover.cs。
// （Button の ColorTint だと、押したあと「選択中」の色が勝ってしまい、同じボタンに当て直しても色が変わらなくなるので使わない）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaArrowHoverFx : UdonSharpBehaviour
{
    public TextMeshProUGUI arrowL;
    public TextMeshProUGUI arrowR;
    public Color normalColor = new Color(0.62f, 0.55f, 0.86f, 1f);
    public Color hoverColor = new Color(0.38f, 0.25f, 0.78f, 1f);
    [Tooltip("当たっている間の矢印の大きさ（倍）")]
    public float hoverScale = 1.3f;

    public void EnterL() { Set(arrowL, true); }
    public void ExitL() { Set(arrowL, false); }
    public void EnterR() { Set(arrowR, true); }
    public void ExitR() { Set(arrowR, false); }
    private void Set(TextMeshProUGUI a, bool on)
    {
        if (a == null) return;
        a.color = on ? hoverColor : normalColor;
        a.transform.localScale = Vector3.one * (on ? hoverScale : 1f);
    }
    // エリアが隠れて戻った時に、当たっていた色のまま残らないように
    private void OnDisable() { ExitL(); ExitR(); }
}
