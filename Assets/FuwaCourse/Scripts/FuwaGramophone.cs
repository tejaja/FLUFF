using UdonSharp;
using UnityEngine;
using TMPro;

// あそびばの蓄音機（2026-11-20 てじゃ「あそびばのBGM変更モードみたいなのほしい」「自分だけ＋全曲、ラストのだけ全クリ後にひっそり」
//  「みためは蓄音機、台にボタン」「動画で曲を止めないスイッチも、デフォは止める」）
// ・◀ ▶ で曲をえらぶ（0＝おまかせ＝いつものあそびばの曲）。えらんだ曲は FuwaBgm.overrideTrack に入れて、あそびばにいる間だけ流れる
// ・レバーで「動画中もBGMを止めない」（FuwaBgm.keepDuringVideo）
// ・曲が鳴っている間はレコードが回る
// 自分の画面だけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGramophone : UdonSharpBehaviour
{
    public FuwaBgm bgm;
    public FuwaLanguage language;
    [Tooltip("全クリ判定（ごほうびの島の FuwaAllClearReveal）。最後の曲（lockedLast）はこれが true の時だけ出る")]
    public FuwaAllClearReveal allClear;

    [Tooltip("えらべる曲（FuwaBgm.tracks の番号）。先頭は -1（おまかせ）")]
    public int[] trackIds;
    public string[] namesJa;
    public string[] namesEn;
    [Tooltip("true なら最後の曲は全クリしてから")]
    public bool lockedLast = true;

    public TextMeshPro label;
    public TextMeshPro switchLabel;
    public Transform record;
    public float spinSpeed = 45f;   // 度/秒（78回転だと速すぎてチカチカするので、ゆっくり）
    public Transform lever;
    public Vector3 leverOffPos, leverOnPos;
    public AudioSource clickSound;

    private int _idx;
    private float _spin;
    private bool _lastEnglish;
    private bool _lastUnlocked;

    private void Start() { Refresh(); }

    private bool Unlocked() { return !lockedLast || (allClear != null && allClear.IsAllClear()); }
    private int Count()
    {
        int n = trackIds != null ? trackIds.Length : 0;
        if (n > 1 && !Unlocked()) n--;
        return n;
    }

    public void Next() { Step(1); }
    public void Prev() { Step(-1); }

    private void Step(int d)
    {
        int n = Count(); if (n <= 0) return;
        _idx = (_idx + d + n) % n;
        Apply();
        if (clickSound != null) clickSound.Play();
    }

    public void ToggleVideoKeep()
    {
        if (bgm == null) return;
        bgm.keepDuringVideo = !bgm.keepDuringVideo;
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
        if (switchLabel != null)
            switchLabel.text = en ? (keep ? "Music during video: ON" : "Music during video: OFF") : (keep ? "動画中もBGM：ON" : "動画中もBGM：OFF");
        if (lever != null) lever.localPosition = keep ? leverOnPos : leverOffPos;
    }

    private void Update()
    {
        // 言語の切りかえ・全クリの解放に追いつく（全クリ前に最後の曲を選んでいることはないが、念のため範囲に戻す）
        bool en = language != null && language.IsEnglish();
        bool un = Unlocked();
        if (en != _lastEnglish || un != _lastUnlocked)
        {
            int n = Count(); if (_idx >= n) { _idx = 0; Apply(); }
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
