using UdonSharp;
using UnityEngine;
using TMPro;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

// あそびばの蓄音機（2026-11-20 てじゃ「あそびばのBGM変更モードみたいなのほしい」「自分だけ＋全曲、ラストのだけ全クリ後にひっそり」
//  「みためは蓄音機、台にボタン」「動画で曲を止めないスイッチも、デフォは止める」）
// ・◀ ▶ で曲をえらぶ（先頭はロビー＝いつものあそびばの曲、最後は BGMなし）。えらんだ曲は FuwaBgm.overrideTrack に入れて、あそびばにいる間だけ流れる
// ・動画の間BGMを止めるかどうかは、テレビの操作パネルの「Auto BGM OFF」（FuwaTvVideoSwitch）に移した（2026-11-21）
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

    private void Apply()
    {
        int id = (trackIds != null && _idx < trackIds.Length) ? trackIds[_idx] : -1;
        if (bgm != null) bgm.overrideTrack = id;
        if (_restored) { PlayerData.SetInt("gramo_track", id); _savedId = id; }
        Refresh();
    }

    // ===== えらんだ曲の保存（FuwaBgm.savePlayerData に合わせる。2026-11-21 てじゃ「保存してもいいかも」） =====
    // 曲の番号（FuwaBgm.tracks の番号）で持つ。並びを変えても同じ曲に戻る。おまけの曲は全クリが読み込まれてから戻す
    private bool _restored;
    private int _savedId = -99;

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || bgm == null || !bgm.savePlayerData) return;
        if (PlayerData.HasKey(player, "gramo_track")) { _savedId = PlayerData.GetInt(player, "gramo_track"); TryRestore(); }
        _restored = true;
    }

    private void TryRestore()
    {
        if (trackIds == null) return;
        for (int i = 0; i < trackIds.Length; i++)
        {
            if (trackIds[i] != _savedId) continue;
            if (Hidden(i) || i == _idx) return;
            _idx = i;
            if (bgm != null) bgm.overrideTrack = trackIds[i];
            Refresh();
            return;
        }
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
    }

    private void Update()
    {
        // 言語の切りかえ・全クリの解放に追いつく
        bool en = language != null && language.IsEnglish();
        bool un = Unlocked();
        if (en != _lastEnglish || un != _lastUnlocked)
        {
            if (Hidden(_idx)) { _idx = 0; Apply(); }
            else if (un && _savedId != -99) TryRestore();   // 全クリが後から読み込まれた：おまけの曲を戻す
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
