using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.SDKBase;

// シンプルな同期動画プレイヤー（2026-10-15 てじゃ「URLを入れて再生、ライブも見れる、一応同期する」「配布のソフトみたいな多機能じゃなくてよし」）。
// ・URL入力欄に貼る → 入れた人が持ち主になって「URL・動画の0秒がサーバー時刻の何秒か・一時停止中か」を配る
// ・ほかの人・後から来た人は、そこから今の位置を計算して再生。ずれが大きくなったら寄せる
// ・ライブ（長さが無限）は位置合わせをしない（ただ再生するだけ）
// ・操作は 再生/一時停止・再同期・音量 だけ。誰でも操作できる（押した人が持ち主になる）
// 同期は Manual。エリアの表示切り替えで隠れない所（FLUFFなら FuwaSystem の下）に置く（隠れている間の同期を取りこぼさないように）
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class TejaVideoPlayer : UdonSharpBehaviour
{
    [Header("部品")]
    public BaseVRCVideoPlayer player;
    public VRCUrlInputField urlField;
    public AudioSource[] speakers;
    [Header("表示（なくてもOK）")]
    public Text statusText;
    public Text timeText;
    public Text playPauseLabel;
    public Slider volumeSlider;

    [Header("調整")]
    [Tooltip("ずれがこれ（秒）を超えたら、持ち主の位置に寄せる")]
    public float maxDrift = 1.0f;
    [Tooltip("VRChat の決まり：次の読み込みまで5秒あける（少し余裕をみる）")]
    public float loadInterval = 5.5f;
    public float defaultVolume = 0.5f;

    [UdonSynced] private VRCUrl _url = VRCUrl.Empty;
    [UdonSynced] private int _ver;            // URL を入れ直すたびに +1（同じURLでも読み直す）
    [UdonSynced] private double _start = -1;  // 動画の0秒にあたるサーバー時刻（持ち主の再生が始まるまで -1）
    [UdonSynced] private bool _paused;
    [UdonSynced] private float _pausedAt;

    private int _loadedVer = -1;
    private bool _ready;
    private bool _loadQueued;
    private float _nextLoad;
    private float _nextCheck;
    private string _status = "";

    private void Start()
    {
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(defaultVolume);
        SetVolume(defaultVolume);
        SetStatus("URLを入れてね / Paste a URL");
        UpdatePlayLabel();
    }

    // ===== UI から（Button / InputField / Slider の SendCustomEvent） =====

    public void OnUrlEntered()
    {
        if (urlField == null) return;
        VRCUrl u = urlField.GetUrl();
        if (u == null || u.Get() == "") return;
        urlField.SetUrl(VRCUrl.Empty);
        Take();
        _url = u;
        _ver++;
        _start = -1;
        _paused = false;
        _pausedAt = 0f;
        RequestSerialization();
        Load();
    }

    public void PlayPause()
    {
        if (!_ready) return;
        Take();
        double now = Networking.GetServerTimeInSeconds();
        if (_paused)
        {
            _paused = false;
            if (!IsLive()) _start = now - _pausedAt;
            player.Play();
        }
        else
        {
            _paused = true;
            _pausedAt = player.GetTime();
            player.Pause();
        }
        RequestSerialization();
        UpdatePlayLabel();
    }

    // 自分の画面だけ：止まった・ずれた時に持ち主の状態へ合わせ直す（読み込めていなければ読み直す）
    public void Resync()
    {
        if (_url == null || _url.Get() == "") return;
        if (!_ready) { Load(); return; }
        ApplyState(true);
    }

    public void OnVolumeChanged()
    {
        if (volumeSlider != null) SetVolume(volumeSlider.value);
    }

    // ===== 同期 =====

    public override void OnDeserialization()
    {
        if (_ver != _loadedVer) { Load(); return; }
        ApplyState(false);
        UpdatePlayLabel();
    }

    private void Take()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
    }

    // 今の URL を読み込む。前の読み込みから5秒たっていなければ、たってから
    public void Load()
    {
        if (_url == null || _url.Get() == "") return;
        float wait = _nextLoad - Time.time;
        if (wait > 0f)
        {
            if (!_loadQueued) { _loadQueued = true; SendCustomEventDelayedSeconds(nameof(_QueuedLoad), wait + 0.05f); }
            SetStatus("まってね… / Waiting…");
            return;
        }
        _ready = false;
        _loadedVer = _ver;
        _nextLoad = Time.time + loadInterval;
        player.Stop();
        player.LoadURL(_url);
        SetStatus("よみこみ中… / Loading…");
    }

    public void _QueuedLoad()
    {
        _loadQueued = false;
        Load();
    }

    // 持ち主の状態（再生中・一時停止・位置）に合わせる。force=位置のずれが小さくても合わせる
    private void ApplyState(bool force)
    {
        if (!_ready) return;
        bool live = IsLive();
        if (_paused)
        {
            if (player.IsPlaying) player.Pause();
            if (!live) player.SetTime(_pausedAt);
            return;
        }
        if (!player.IsPlaying) player.Play();
        if (live || _start < 0) return;
        float target = (float)(Networking.GetServerTimeInSeconds() - _start);
        float dur = player.GetDuration();
        if (target < 0f) target = 0f;
        if (dur > 0f && target > dur) return;   // もう終わっている
        if (force || Mathf.Abs(player.GetTime() - target) > maxDrift) player.SetTime(target);
    }

    private bool IsLive()
    {
        float d = player.GetDuration();
        return float.IsInfinity(d) || d <= 0f;
    }

    // ===== 動画プレイヤーのイベント =====

    public override void OnVideoReady()
    {
        _ready = true;
        if (Networking.IsOwner(gameObject))
        {
            if (_paused) { ApplyState(true); }
            else player.Play();
        }
        else ApplyState(true);
        SetStatus(IsLive() ? "ライブ / Live" : "");
        UpdatePlayLabel();
    }

    public override void OnVideoStart()
    {
        // 持ち主：再生が始まった時刻から「動画の0秒のサーバー時刻」を決めて配る（最初の1回だけ。一時停止からの再開は PlayPause で決める）
        if (Networking.IsOwner(gameObject) && _start < 0 && !IsLive())
        {
            _start = Networking.GetServerTimeInSeconds() - player.GetTime();
            RequestSerialization();
        }
        UpdatePlayLabel();
    }

    public override void OnVideoEnd()
    {
        SetStatus("おわり / Ended");
        UpdatePlayLabel();
    }

    public override void OnVideoError(VideoError videoError)
    {
        _ready = false;
        if (videoError == VideoError.RateLimited)
        {
            // 読み込みが早すぎた：5秒後にもう一度
            _nextLoad = Time.time + loadInterval;
            Load();
            return;
        }
        string msg;
        if (videoError == VideoError.InvalidURL) msg = "URLがだめみたい / Invalid URL";
        else if (videoError == VideoError.AccessDenied) msg = "設定の「Allow Untrusted URLs」をオンにしてね / Enable Allow Untrusted URLs";
        else if (videoError == VideoError.PlayerError) msg = "再生できなかった / Player error";
        else msg = "エラー / Error: " + videoError.ToString();
        SetStatus(msg);
        UpdatePlayLabel();
    }

    // ===== 表示 =====

    private void Update()
    {
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + 0.5f;
        // 持ち主以外：ときどき位置のずれを見て寄せる
        if (_ready && !_paused && !Networking.IsOwner(gameObject)) ApplyState(false);
        if (timeText != null)
        {
            if (!_ready) timeText.text = "";
            else if (IsLive()) timeText.text = "LIVE";
            else timeText.text = Clock(player.GetTime()) + " / " + Clock(player.GetDuration());
        }
    }

    private string Clock(float t)
    {
        if (t < 0f || float.IsNaN(t) || float.IsInfinity(t)) t = 0f;
        int s = Mathf.FloorToInt(t);
        int h = s / 3600, m = (s / 60) % 60, sec = s % 60;
        return h > 0 ? h + ":" + m.ToString("00") + ":" + sec.ToString("00") : m + ":" + sec.ToString("00");
    }

    private void SetStatus(string s)
    {
        _status = s;
        if (statusText != null) statusText.text = s;
    }

    private void UpdatePlayLabel()
    {
        if (playPauseLabel != null) playPauseLabel.text = (_ready && !_paused && player.IsPlaying) ? "II" : "▶";
    }

    private void SetVolume(float v)
    {
        if (speakers == null) return;
        for (int i = 0; i < speakers.Length; i++) if (speakers[i] != null) speakers[i].volume = v;
    }
}
