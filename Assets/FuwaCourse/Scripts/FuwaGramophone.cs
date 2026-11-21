using UdonSharp;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// あそびばの蓄音機（2026-11-20 てじゃ「あそびばのBGM変更モードみたいなのほしい」「自分だけ＋全曲、ラストのだけ全クリ後にひっそり」
//  「みためは蓄音機、台にボタン」「動画で曲を止めないスイッチも、デフォは止める」）
// ・◀ ▶ で曲をえらぶ（先頭はロビー＝いつものあそびばの曲、最後は BGMなし）。えらんだ曲は FuwaBgm.overrideTrack に入れて、あそびばにいる間だけ流れる
// ・「動画再生中 [ON] [OFF]」で動画中もBGMを止めないか（FuwaBgm.keepDuringVideo）。今の方が白い背景
//  （2026-11-21 てじゃ「ボタンを他のボタンみたいなビーム式に」「ONとOFFに白背景で分かる感じに」）
// ・曲が鳴っている間はレコードが回る
// 自分の画面だけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGramophone : UdonSharpBehaviour
{
    public FuwaBgm bgm;
    public FuwaLanguage language;
    [Tooltip("全クリ判定（ごほうびの島の FuwaAllClearReveal）。lockedIndex の曲はこれが true の時だけ出る")]
    public FuwaAllClearReveal allClear;

    [Tooltip("えらべる曲（FuwaBgm.tracks の番号、-2＝BGMなし）")]
    public int[] trackIds;
    public string[] namesJa;
    public string[] namesEn;
    [Tooltip("全クリしてから出る曲の並びの番号（おまけ）。-1 ならなし")]
    public int lockedIndex = -1;

    public TextMeshPro label;
    public TextMeshPro switchLabel;
    public Transform record;
    public float spinSpeed = 45f;   // 度/秒（78回転だと速すぎてチカチカするので、ゆっくり）
    public AudioSource clickSound;

    [Header("動画再生中 ON / OFF（今の方が白い背景）")]
    public Image onBg, offBg;
    public TextMeshProUGUI onText, offText;
    public Color selectedBg = Color.white, idleBg = new Color(1f, 1f, 1f, 0.2f);
    public Color selectedText = new Color(0.27f, 0.36f, 0.62f), idleText = new Color(1f, 0.96f, 0.9f);

    private int _idx;
    private float _spin;
    private bool _lastEnglish;
    private bool _lastUnlocked;

    private void Start() { Refresh(); }

    private bool Unlocked() { return allClear != null && allClear.IsAllClear(); }
    private bool Hidden(int i) { return i == lockedIndex && !Unlocked(); }

    public void Next() { Step(1); }
    public void Prev() { Step(-1); }

    private void Step(int d)
    {
        int n = trackIds != null ? trackIds.Length : 0; if (n <= 0) return;
        for (int k = 0; k < n; k++)
        {
            _idx = (_idx + d + n) % n;
            if (!Hidden(_idx)) break;
        }
        Apply();
        if (clickSound != null) clickSound.Play();
    }

    public void VideoKeepOn() { SetKeep(true); }
    public void VideoKeepOff() { SetKeep(false); }
    public void ToggleVideoKeep() { if (bgm != null) SetKeep(!bgm.keepDuringVideo); }
    private void SetKeep(bool v)
    {
        if (bgm == null) return;
        bgm.keepDuringVideo = v;
        if (clickSound != null) clickSound.Play();
        Refresh();
    }

    private void Apply()
    {
        if (bgm != null) bgm.overrideTrack = (trackIds != null && _idx < trackIds.Length) ? trackIds[_idx] : -1;
        Refresh();
    }

    private void Refresh()
    {
        bool en = language != null && language.IsEnglish();
        _lastEnglish = en;
        _lastUnlocked = Unlocked();
        if (label != null)
        {
            string[] names = en ? namesEn : namesJa;
            label.text = (names != null && _idx < names.Length) ? names[_idx] : "";
        }
        bool keep = bgm != null && bgm.keepDuringVideo;
        if (switchLabel != null) switchLabel.text = en ? "During video" : "動画再生中";
        if (onBg != null) onBg.color = keep ? selectedBg : idleBg;
        if (offBg != null) offBg.color = keep ? idleBg : selectedBg;
        if (onText != null) onText.color = keep ? selectedText : idleText;
        if (offText != null) offText.color = keep ? idleText : selectedText;
    }

    private void Update()
    {
        // 言語の切りかえ・全クリの解放に追いつく
        bool en = language != null && language.IsEnglish();
        bool un = Unlocked();
        if (en != _lastEnglish || un != _lastUnlocked)
        {
            if (Hidden(_idx)) { _idx = 0; Apply(); }
            Refresh();
        }
        if (record != null)
        {
            float goal = (bgm != null && bgm.IsAudible()) ? spinSpeed : 0f;
            _spin = Mathf.MoveTowards(_spin, goal, spinSpeed * Time.deltaTime);   // 1秒で回りはじめ・止まる
            if (_spin > 0.01f) record.Rotate(0f, _spin * Time.deltaTime, 0f, Space.Self);
        }
    }
}
