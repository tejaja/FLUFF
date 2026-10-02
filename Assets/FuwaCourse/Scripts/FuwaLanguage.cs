using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

// 日本語／英語の切り替え（自分の画面だけ）。このオブジェクトをクリックすると切り替わる。
// 最初はVRChatの言語設定を見て、日本語なら日本語、それ以外は英語で始める。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaLanguage : UdonSharpBehaviour
{
    [Tooltip("切り替える文字（ja / en と同じ順番）")]
    public TextMeshPro[] texts;
    [TextArea] public string[] ja;
    [TextArea] public string[] en;

    [Tooltip("「アウト！」などのメッセージを切り替えるため")]
    public FuwaBall ball;
    [Tooltip("「銃を呼ぶ」の表示を切り替えるため")]
    public FuwaGunStand[] stands;
    [Tooltip("ボタン自身の文字")]
    public TextMeshPro buttonLabel;
    [Tooltip("UIボタンの文字（レーザーポインタで押す版）")]
    public TextMeshProUGUI buttonLabelUi;
    [Tooltip("各ロビーに置いた同じボタンの文字（まとめて切り替える）")]
    public TextMeshProUGUI[] extraButtonLabelsUi;
    [Tooltip("最速タイムの看板（なくてもOK）")]
    public FuwaRecords records;

    private bool _english;
    private bool _manual;      // 一度でも自分で切り替えたら、VRChatの言語設定には従わない
    private FuwaBlower _blower;

    private void Start()
    {
        _english = !IsJapanese(VRCPlayerApi.GetCurrentLanguage());
        Apply();
    }

    public override void OnLanguageChanged(string language)
    {
        if (_manual) return;
        _english = !IsJapanese(language);
        Apply();
    }

    public override void Interact()
    {
        ToggleLanguage();
    }

    // UIボタン（レーザーポインタで押す）から呼ばれる
    public void ToggleLanguage()
    {
        _manual = true;
        _english = !_english;
        Apply();
        // 自分で選んだ言語は保存して、次に来た時もその言語で始める（保存データが届いてから）
        if (_restored) PlayerData.SetInt("lang", _english ? 1 : 0);
    }

    private bool _restored;
    [Tooltip("ONで選んだ言語をVRChatに保存する。ワールド側からは保存データを消せないので、完成するまではOFF")]
    public bool savePlayerData = false;

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || !savePlayerData) return;
        _restored = true;
        if (_manual)
        {
            // 保存が届く前にもう切り替えていたら、そっちを保存
            PlayerData.SetInt("lang", _english ? 1 : 0);
            return;
        }
        if (!PlayerData.HasKey(player, "lang")) return;   // まだ一度も選んでいない人はVRChatの言語設定のまま
        _manual = true;
        _english = PlayerData.GetInt(player, "lang") == 1;
        Apply();
    }

    // 自分の銃から呼ばれる
    public void RegisterBlower(FuwaBlower blower)
    {
        _blower = blower;
        if (_blower != null) _blower.ApplyLanguage(_english);
    }

    public bool IsEnglish() { return _english; }

    private bool IsJapanese(string language)
    {
        if (string.IsNullOrEmpty(language)) return true;
        string l = language.ToLower();
        return l.StartsWith("ja") || l.Contains("japanese") || l.Contains("日本");
    }

    private void Apply()
    {
        string[] src = _english ? en : ja;
        if (texts != null && src != null)
        {
            int n = Mathf.Min(texts.Length, src.Length);
            for (int i = 0; i < n; i++)
            {
                if (texts[i] != null) texts[i].text = src[i];
            }
        }
        if (ball != null) ball.english = _english;
        if (records != null) records.Render();
        if (stands != null)
        {
            foreach (FuwaGunStand s in stands) if (s != null) s.ApplyLanguage(_english);
        }
        if (_blower != null) _blower.ApplyLanguage(_english);
        if (buttonLabel != null) buttonLabel.text = _english ? "日本語にする" : "English";
        InteractionText = _english ? "日本語にする" : "Switch to English";
        // 「JP / EN」の2つを並べて、今の言語をはっきり、もう片方を薄く
        string jpen = _english
            ? "<color=#FFFFFF88>JP</color> / <b>EN</b>"
            : "<b>JP</b> / <color=#FFFFFF88>EN</color>";
        if (buttonLabelUi != null) buttonLabelUi.text = jpen;
        if (extraButtonLabelsUi != null) foreach (TextMeshProUGUI t in extraButtonLabelsUi) if (t != null) t.text = jpen;
    }
}

