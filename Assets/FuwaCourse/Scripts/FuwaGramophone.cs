using UdonSharp;
using UnityEngine;
using TMPro;

// あそびばの蓄音機（2026-11-20 てじゃ「あそびばのBGM変更モードみたいなのほしい」「自分だけ＋全曲、ラストのだけ全クリ後にひっそり」
//  「みためは蓄音機、台にボタン」「動画で曲を止めないスイッチも、デフォは止める」）
// ・◀ ▶ で曲をえらぶ（先頭はロビー＝いつものあそびばの曲、最後は BGMなし）。えらんだ曲は FuwaBgm.overrideTrack に入れて、あそびばにいる間だけ流れる
// ・「動画優先 ｜ ◀ ON/OFF ▶」：ON（ふだん）は動画の間BGMを止める、OFF は止めない（FuwaBgm.keepDuringVideo）。ボタンは蓄音機の横に単体で浮かせてある
// ・音量バーは蓄音機の下の段（FuwaBgm.volumeSliders に入っているので、ここでは何もしない）
//  （2026-11-21 てじゃ「ボタンを他のボタンみたいなビーム式に」「ほかの切り替えボタンみたいにボタンとタイトルがつながってて真ん中で区切ってるタイプに」：テレビの CAM と同じ形）
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

    [Tooltip("曲名（上の段の白い札）")]
    public TextMeshProUGUI trackText;
    [Tooltip("ON / OFF（下の段の白い札）")]
    public TextMeshProUGUI videoText;
    [Tooltip("下の段の見出し（動画優先）")]
    public TextMeshProUGUI videoHead;
    public Color activeTextColor = new Color(0.62f, 0.55f, 0.86f), idleTextColor = new Color(0.55f, 0.55f, 0.6f);
    public Transform record;
    public float spinSpeed = 45f;   // 度/秒（78回転だと速すぎてチカチカするので、ゆっくり）
    public AudioSource clickSound;

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
        if (trackText != null)
        {
            string[] names = en ? namesEn : namesJa;
            trackText.text = (names != null && _idx < names.Length) ? names[_idx] : "";
            trackText.color = activeTextColor;
        }
        bool keep = bgm != null && bgm.keepDuringVideo;
        // 「動画優先」ON＝動画の間はBGMを止める（ふだん）、OFF＝止めない（2026-11-21 てじゃ「動画優先 ON OFFとか」）
        bool videoFirst = !keep;
        if (videoHead != null) videoHead.text = en ? "Video first" : "動画優先";
        if (videoText != null) { videoText.text = videoFirst ? "ON" : "OFF"; videoText.color = videoFirst ? activeTextColor : idleTextColor; }
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
