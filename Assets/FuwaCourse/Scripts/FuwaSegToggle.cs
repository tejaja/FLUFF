using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// 2択のボタン（JP / EN、HUD ON / OFF）の見た目。選んでいる方だけ白い丸い札の上に、ボタンと同じ色の文字で出す。
// 選んでいない方は札なしの白い文字（少し薄く）。太字にしないので、切り替えても文字の位置はずれない。
// 押した時の処理は今まで通りボタン側（FuwaLanguage / FuwaHud）。そちらから Show を呼ぶ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSegToggle : UdonSharpBehaviour
{
    public Image pillA;
    public Image pillB;
    public TextMeshProUGUI textA;
    public TextMeshProUGUI textB;
    [Tooltip("選んでいる方の文字の色（ボタンの色）")]
    public Color activeTextColor = new Color(0.45f, 0.78f, 0.62f, 1f);
    [Tooltip("選んでいない方の文字の色")]
    public Color idleTextColor = new Color(1f, 1f, 1f, 0.75f);

    // second=false なら A（JP / ON）、true なら B（EN / OFF）を選んでいる
    public void Show(bool second)
    {
        if (pillA != null) pillA.enabled = !second;
        if (pillB != null) pillB.enabled = second;
        if (textA != null) textA.color = second ? idleTextColor : activeTextColor;
        if (textB != null) textB.color = second ? activeTextColor : idleTextColor;
    }
}
