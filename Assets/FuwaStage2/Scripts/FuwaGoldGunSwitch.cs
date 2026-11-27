using UdonSharp;
using UnityEngine;
using TMPro;

// 2026-11-17 おまけの島：クリックで自分のエアブラスターを金ピカに（もう一度でもどす）。金ピカかどうかは銃が同期するので、ほかの人からも見える
// 2026-11-18 てじゃ「スイッチは分かりづらいな、台クリックで有効化、有効化中は台の銃が通常カラーになる、戻すときは逆」：
//  台の上の銃の像は「クリックするとこうなる」色（自分の銃が元の色なら金ピカ、金ピカなら元の色）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGoldGunSwitch : UdonSharpBehaviour
{
    public FuwaRespawnManager manager;
    public TextMeshPro label;
    public string onText = "きんピカ ON", offText = "きんピカ OFF";
    public AudioSource clickSound;
    [Tooltip("台の上の像：金ピカ（なくてもOK）")]
    public GameObject goldModel;
    [Tooltip("台の上の像：元の色（なくてもOK）")]
    public GameObject normalModel;
    // 2026-11-19 てじゃ「切り替えて台に通常銃が乗ってるときはキラキラ消そう」：台のキラキラは金ピカの像が出ている時だけ
    [Tooltip("台のキラキラ（金ピカの像の時だけ出す。なくてもOK）")]
    public GameObject sparkles;
    public string toGoldText = "きんピカにする", toNormalText = "もとにもどす";
    // 2026-11-19 てじゃ「全部英語版もほしいな」
    [Tooltip("言語の切りかえ（なければ日本語だけ）")]
    public FuwaLanguage language;
    public string toGoldTextEn = "Make it golden", toNormalTextEn = "Back to normal";
    private int _shown = -1;
    private float _next;

    private void Start() { Refresh(); }
    private void OnEnable() { _shown = -1; Refresh(); }

    public override void Interact()
    {
        FuwaBlower b = manager != null ? manager.GetMyBlower() : null;
        if (b == null) return;
        b.SetGold(!b.IsGold());
        if (clickSound != null && clickSound.enabled && clickSound.gameObject.activeInHierarchy) clickSound.Play();
        Refresh();
    }

    // 銃が入れかわった時なども追いかける
    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + 0.3f;
        Refresh();
    }

    private void Refresh()
    {
        FuwaBlower b = manager != null ? manager.GetMyBlower() : null;
        bool gold = b != null && b.IsGold();
        bool en = language != null && language.IsEnglish();
        int s = (gold ? 1 : 0) + (en ? 2 : 0);
        if (s == _shown) return;
        _shown = s;
        if (label != null) label.text = gold ? onText : offText;
        if (goldModel != null) goldModel.SetActive(!gold);
        if (normalModel != null) normalModel.SetActive(gold);
        if (sparkles != null) sparkles.SetActive(!gold);
        InteractionText = gold ? (en ? toNormalTextEn : toNormalText) : (en ? toGoldTextEn : toGoldText);
    }
}
