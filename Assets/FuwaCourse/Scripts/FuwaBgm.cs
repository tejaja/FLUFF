using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

// BGM（自分にだけ聞こえる）。今いるコース（ふわふわのスタート地点）に合わせて曲をクロスフェードで切り替える。
// tracks[i] と starts[i] が対応。どれにも当てはまらない時は tracks[0]（ロビー）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaBgm : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("曲（ループ設定のAudioSource）。0番はロビー")]
    public AudioSource[] tracks;
    [Tooltip("各曲に対応するふわふわのスタート地点（FuwaBall.startPointと同じもの）")]
    public Transform[] starts;
    [Range(0f, 1f)] public float volume = 0.25f;
    [Tooltip("曲の切り替えにかける秒数")]
    public float fadeTime = 1.5f;
    [Tooltip("前の曲が消え始めてから、次の曲が鳴り始めるまでの間(秒)")]
    public float startDelay = 1.0f;

    [Tooltip("BGMオン/オフのボタンの文字（なくてもOK）")]
    public TextMeshProUGUI buttonLabel;
    [Tooltip("各ロビーに置いた同じボタンの文字（まとめて切り替える）")]
    public TextMeshProUGUI[] extraButtonLabels;

    private int _current = -1;
    private float _startAt;
    private bool _muted;

    // BGMボタン（レーザーで押すUIボタン）から呼ばれる。自分だけ
    public void ToggleBgm()
    {
        _muted = !_muted;
        // オンに戻した時は、今の場所の曲を頭から（ワンテンポ置かずに）
        if (!_muted) { _current = -1; }
        UpdateLabel();
        if (_restored) PlayerData.SetBool("bgm_muted", _muted);
    }

    // ===== 保存（PlayerData）：BGMのON/OFFと音量バー =====
    private bool _restored;
    private bool _volumeSavePending;
    [Tooltip("ONでBGMのON/OFFと音量をVRChatに保存する。ワールド側からは保存データを消せないので、完成するまではOFF")]
    public bool savePlayerData = false;

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || !savePlayerData) return;
        _restored = true;
        if (PlayerData.HasKey(player, "bgm_muted"))
        {
            bool m = PlayerData.GetBool(player, "bgm_muted");
            if (m != _muted) ToggleBgm();
        }
        if (PlayerData.HasKey(player, "bgm_volume"))
        {
            _userPercent = Mathf.Clamp(PlayerData.GetFloat(player, "bgm_volume"), 0f, 100f);
            if (volumeSliders != null)
                foreach (UnityEngine.UI.Slider sl in volumeSliders) if (sl != null) sl.SetValueWithoutNotify(_userPercent);
            UpdateVolumeLabels();
        }
    }

    // 音量バーはドラッグ中に何度も呼ばれるので、動かし終わって少ししてから1回だけ保存する
    private void QueueVolumeSave()
    {
        if (!_restored || _volumeSavePending) return;
        _volumeSavePending = true;
        SendCustomEventDelayedSeconds(nameof(SaveVolume), 1.0f);
    }

    public void SaveVolume()
    {
        _volumeSavePending = false;
        if (_restored) PlayerData.SetFloat("bgm_volume", _userPercent);
    }

    private void UpdateLabel()
    {
        string s = _muted ? "BGM: OFF" : "BGM: ON";
        if (buttonLabel != null) buttonLabel.text = s;
        if (extraButtonLabels != null) foreach (TextMeshProUGUI t in extraButtonLabels) if (t != null) t.text = s;
    }
    private float[] _vol;

    [Header("音量バー（レーザーで動かすUIスライダー、0〜100）")]
    [Tooltip("各ロビーに置いた音量バー。どれを動かしても全部そろう")]
    public UnityEngine.UI.Slider[] volumeSliders;
    [Tooltip("音量バーの横の数字（なくてもOK）")]
    public TextMeshProUGUI[] volumeLabels;
    private float _userPercent = 100f;

    // 実際の音量（基準の音量 × 音量バー）
    private float Vol() { return volume * _userPercent / 100f; }

    // 音量バーが動かされた時に呼ばれる（どのバーかは、今の値と違うものを探す）
    public void OnVolumeSlider()
    {
        if (volumeSliders == null) return;
        foreach (UnityEngine.UI.Slider sl in volumeSliders)
        {
            if (sl == null) continue;
            if (Mathf.Abs(sl.value - _userPercent) > 0.01f) { _userPercent = Mathf.Clamp(sl.value, 0f, 100f); break; }
        }
        foreach (UnityEngine.UI.Slider sl in volumeSliders)
        {
            // 普通に value を入れると、そのバーの「動いた」イベントがまたこのメソッドを呼んで止まるので、通知なしで入れる
            if (sl != null && Mathf.Abs(sl.value - _userPercent) > 0.01f) sl.SetValueWithoutNotify(_userPercent);
        }
        UpdateVolumeLabels();
        if (fanfare != null && fanfare.isPlaying) fanfare.volume = Mathf.Clamp01(Vol() * fanfareVolume);
        QueueVolumeSave();
    }

    private void UpdateVolumeLabels()
    {
        if (volumeLabels == null) return;
        string s = "BGM " + Mathf.RoundToInt(_userPercent);
        foreach (TextMeshProUGUI t in volumeLabels) if (t != null) t.text = s;
    }

    [Header("ゴールのファンファーレ")]
    public AudioSource fanfare;
    [Tooltip("ファンファーレの音量（BGMの音量に対する倍率）")]
    public float fanfareVolume = 1.6f;
    [Tooltip("ファンファーレの間、BGMを消す速さ(秒)")]
    public float fanfareFadeOut = 0.25f;
    private float _fanfareUntil;
    [Tooltip("ファンファーレの後に流す軽い曲（tracks の何番か。-1ならなし）")]
    public int afterGoalTrack = -1;
    private bool _afterGoal;
    private Transform _afterGoalStart;

    // ゴールした時に呼ばれる：BGMをすっと消してファンファーレ、終わったら今の場所の曲を頭から
    public void PlayFanfare()
    {
        // ゴール後は、次のスタート（またはコースの移動）まで軽い曲にする
        _afterGoal = afterGoalTrack >= 0;
        _afterGoalStart = ball != null ? ball.startPoint : null;
        if (_muted || fanfare == null || fanfare.clip == null) return;
        _fanfareUntil = Time.time + fanfare.clip.length;
        fanfare.volume = Mathf.Clamp01(Vol() * fanfareVolume);
        fanfare.Play();
    }

    private void Start()
    {
        int n = tracks != null ? tracks.Length : 0;
        _vol = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (tracks[i] == null) continue;
            tracks[i].loop = true;
            tracks[i].volume = 0f;
            tracks[i].Stop();
        }
        UpdateLabel();
    }

    private void Update()
    {
        if (tracks == null || _vol == null) return;
        // ファンファーレ中：BGMは全部すばやく消す。終わったら今の場所の曲を頭から鳴らし直す
        if (_fanfareUntil > 0f)
        {
            if (Time.time < _fanfareUntil)
            {
                float fstep = fanfareFadeOut > 0.001f ? Time.deltaTime / fanfareFadeOut : 1f;
                for (int i = 0; i < tracks.Length; i++)
                {
                    AudioSource s = tracks[i];
                    if (s == null) continue;
                    _vol[i] = Mathf.MoveTowards(_vol[i], 0f, fstep);
                    s.volume = _vol[i] * _vol[i] * Vol();
                    if (_vol[i] <= 0f && s.isPlaying) s.Stop();
                }
                return;
            }
            _fanfareUntil = 0f;
            _current = -1;   // 頭から、待たずに
        }
        int target = FindTrack();
        if (target != _current)
        {
            // ワールドに入った直後の最初の曲だけは待たずに鳴らす
            _startAt = _current < 0 ? Time.time : Time.time + startDelay;
            _current = target;
        }
        float step = fadeTime > 0.001f ? Time.deltaTime / fadeTime : 1f;
        for (int i = 0; i < tracks.Length; i++)
        {
            AudioSource s = tracks[i];
            if (s == null) continue;
            // 止まっている曲は、ワンテンポ置いてから頭から普通の音量で鳴らす（フェードインすると曲の頭が
            // 聞こえないので。前の曲だけフェードアウトする）。フェードアウト中の曲に戻る時は続きからフェードイン
            if (i == _current && !s.isPlaying && !_muted)
            {
                if (Time.time < _startAt) continue;
                _vol[i] = 1f;
                s.volume = Vol();
                s.Play();
            }
            float goal = (i == _current && !_muted) ? 1f : 0f;
            _vol[i] = Mathf.MoveTowards(_vol[i], goal, step);
            s.volume = _vol[i] * _vol[i] * Vol();   // 2乗で聞こえ方の変化をなめらかに
            if (_vol[i] <= 0f && s.isPlaying) s.Stop();
        }
    }

    private int FindTrack()
    {
        if (ball == null || starts == null) return 0;
        if (_afterGoal)
        {
            // 次の挑戦を始めた（タイム計測が始まった）か、別の場所に移ったら元の曲へ
            if (ball.IsRunning() || ball.startPoint != _afterGoalStart) _afterGoal = false;
            else if (afterGoalTrack < tracks.Length) return afterGoalTrack;
        }
        Transform sp = ball.startPoint;
        for (int i = 0; i < starts.Length && i < tracks.Length; i++)
        {
            if (starts[i] == sp) return i;
        }
        return 0;
    }
}
