using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

// あそびばの蓄音機（2026-11-20 てじゃ「あそびばのBGM変更モードみたいなのほしい」「自分だけ＋全曲、ラストのだけ全クリ後にひっそり」「みためは蓄音機、台にボタン」）
// 2026-11-21 てじゃの仕様変更：
//  ・デフォは停止。◀ ▶ で曲をえらぶだけでは鳴らない（ロビーの曲は最後、BGMなしは削除）
//  ・▶再生 を押すと、えらんだ曲がロビーの曲の代わりに流れる（あそびばにいる間だけ）。■停止 でロビーの曲に戻る。再生中に曲を変えたらそのまま切りかえ
//  ・動画が流れている間、ロビーの曲は止まるが蓄音機の曲は止まらない（FuwaBgm.GramoActive）
//  ・音量は蓄音機専用のバー（ワールドのBGM音量とは別）。えらんだ曲と音量は保存、再生中かどうかは保存しない（来るたび停止から）
//  ・ボタンはテレビの CAM と同じ形（てじゃ「ほかの切り替えボタンみたいに」）。今の状態（再生中／停止中）の方のボタンを塗る
// ・蓄音機の曲が鳴っている間はレコードが回る
// 自分の画面だけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGramophone : UdonSharpBehaviour
{
    public FuwaBgm bgm;
    public FuwaLanguage language;
    [Tooltip("全クリ判定（ごほうびの島の FuwaAllClearReveal）。lockedIndex の曲はこれが true の時だけ出る")]
    public FuwaAllClearReveal allClear;

    [Tooltip("えらべる曲（FuwaBgm.tracks の番号）")]
    public int[] trackIds;
    public string[] namesJa;
    public string[] namesEn;
    [Tooltip("全クリしてから出る曲の並びの番号（おまけ）。-1 ならなし")]
    public int lockedIndex = -1;

    [Tooltip("曲名（上の段の白い札）")]
    public TextMeshProUGUI trackText;
    public Color activeTextColor = new Color(0.62f, 0.55f, 0.86f), idleTextColor = new Color(0.55f, 0.55f, 0.6f);

    [Header("▶再生・■停止（今の状態の方を塗る）")]
    public Image playBack, stopBack;
    public Graphic playIcon, stopIcon;
    public Color onBack = new Color(0.66f, 0.45f, 0.32f), offBack = new Color(0.93f, 0.88f, 0.84f);
    public Color onIcon = Color.white, offIcon = new Color(0.66f, 0.45f, 0.32f);

    [Header("蓄音機の音量（0〜1、ワールドのBGM音量とは別）")]
    public Slider volumeSlider;
    public float defaultVolume = 0.6f;

    public Transform record;
    public float spinSpeed = 45f;   // 度/秒（78回転だと速すぎてチカチカするので、ゆっくり）
    public AudioSource clickSound;

    private int _idx;
    private float _spin;
    private bool _lastEnglish;
    private bool _lastUnlocked;
    private float _vol;

    // あそびばは隠れている間このスクリプトも止まっていて、入った時の OnPlayerRestored を受け取れない。
    // 保存の読み込みはいつも動いている FuwaBgm がして（gramoSavedTrack / gramoVolume）、ここは Start と読み込みの後にそれを写す
    private bool _synced;
    private void Start()
    {
        if (bgm != null && !bgm.IsRestored()) bgm.gramoVolume = defaultVolume;
        SyncFromBgm();
    }

    private void SyncFromBgm()
    {
        if (bgm == null) { Apply(); return; }
        _synced = bgm.IsRestored() || !bgm.savePlayerData;
        _vol = Mathf.Clamp01(bgm.gramoVolume);
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(_vol);
        _savedId = bgm.gramoSavedTrack;
        if (!TryRestore()) Apply();
    }

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
        if (Saving()) { PlayerData.SetInt("gramo_track", trackIds[_idx]); _savedId = trackIds[_idx]; bgm.gramoSavedTrack = _savedId; }
        Click();
    }

    public void PlayGramo() { SetPlaying(true); }
    public void StopGramo() { SetPlaying(false); }
    private void SetPlaying(bool v)
    {
        if (bgm == null) return;
        bgm.gramoPlaying = v;
        Click();
        Refresh();
    }

    // 音量バーが動いた
    public void OnVolume()
    {
        if (volumeSlider == null) return;
        _vol = Mathf.Clamp01(volumeSlider.value);
        if (bgm != null) bgm.gramoVolume = _vol;
        if (Saving() && !_volSavePending) { _volSavePending = true; SendCustomEventDelayedSeconds(nameof(_SaveVolume), 1f); }
    }

    public void _SaveVolume()
    {
        _volSavePending = false;
        if (Saving()) PlayerData.SetFloat("gramo_volume", _vol);
    }

    private void Click() { if (clickSound != null) clickSound.Play(); }

    private void Apply()
    {
        int id = (trackIds != null && _idx < trackIds.Length) ? trackIds[_idx] : -1;
        if (bgm != null) bgm.overrideTrack = id;
        Refresh();
    }

    // ===== 保存（FuwaBgm.savePlayerData に合わせる）：えらんだ曲（曲の番号）と音量。読み込みは FuwaBgm.OnPlayerRestored =====
    // 曲は FuwaBgm.tracks の番号で持つので、並びを変えても同じ曲に戻る。おまけの曲は全クリが読み込まれてから戻す
    private bool _volSavePending;
    private int _savedId = -1;
    private bool Saving() { return bgm != null && bgm.savePlayerData && bgm.IsRestored(); }

    // 保存してあった曲にする（見つかって出せる時 true）
    private bool TryRestore()
    {
        if (trackIds == null || _savedId < 0) return false;
        for (int i = 0; i < trackIds.Length; i++)
        {
            if (trackIds[i] != _savedId) continue;
            if (Hidden(i)) return false;
            _idx = i;
            Apply();
            return true;
        }
        return false;
    }

    private void Refresh()
    {
        bool en = language != null && language.IsEnglish();
        _lastEnglish = en;
        _lastUnlocked = Unlocked();
        if (trackText != null)
        {
            string[] names = en ? namesEn : namesJa;
            // 番号は見えている曲だけで 1 から（2026-11-21 てじゃ「順番に1から数字つけるだけ」）
            int no = 0;
            for (int i = 0; i <= _idx && trackIds != null && i < trackIds.Length; i++) if (!Hidden(i)) no++;
            trackText.text = (names != null && _idx < names.Length) ? no.ToString("00") + "  " + names[_idx] : "";   // 2桁にそろえる（てじゃ「1は01みたいなかんじで」）
            trackText.color = activeTextColor;
        }
        bool playing = bgm != null && bgm.gramoPlaying;
        if (playBack != null) playBack.color = playing ? onBack : offBack;
        if (stopBack != null) stopBack.color = playing ? offBack : onBack;
        if (playIcon != null) playIcon.color = playing ? onIcon : offIcon;
        if (stopIcon != null) stopIcon.color = playing ? offIcon : onIcon;
    }

    private void Update()
    {
        // 言語の切りかえ・全クリの解放に追いつく
        bool en = language != null && language.IsEnglish();
        bool un = Unlocked();
        if (en != _lastEnglish || un != _lastUnlocked)
        {
            if (Hidden(_idx)) { _idx = 0; Apply(); }
            else if (un) TryRestore();   // 全クリが後から読み込まれた：おまけの曲を戻す
            Refresh();
        }
        if (!_synced && bgm != null && bgm.IsRestored()) SyncFromBgm();   // 保存の読み込みが後から届いた
        if (record != null)
        {
            float goal = (bgm != null && bgm.IsGramoAudible()) ? spinSpeed : 0f;
            _spin = Mathf.MoveTowards(_spin, goal, spinSpeed * Time.deltaTime);   // 1秒で回りはじめ・止まる
            if (_spin > 0.01f) record.Rotate(0f, _spin * Time.deltaTime, 0f, Space.Self);
        }
    }
}
