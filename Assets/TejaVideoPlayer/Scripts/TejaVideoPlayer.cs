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
    [Tooltip("コピーのボタン（ふつうの文字の入力欄。中に今の動画のURLが入っていて、押すとVRChatの入力が出てそこからコピーできる。Enterしても再生は変わらない）。2026-10-15 てじゃ")]
    public InputField copyField;
    [Tooltip("操作パネル（たたむタブで出す／隠す。自分の画面だけ）")]
    public GameObject panelRoot;
    public Text panelTabLabel;
    [Tooltip("たたむタブ。たたんだ時は画面のすぐ下まで上げる（パネルの高さぶん）")]
    public RectTransform panelTab;
    [Tooltip("隠すボタンで出し入れする物（空なら panelRoot）。操作パネルの Canvas まるごとにすると、隠している間はレーザーも当たらない")]
    public GameObject panelHideTarget;
    public float panelHeight = 212f;
    public AudioSource[] speakers;
    [Header("表示（なくてもOK）")]
    public Text statusText;
    public Text timeText;
    public Text playPauseLabel;
    public Slider volumeSlider;
    public Slider seekSlider;
    [Tooltip("スピーカーのアイコン（押すと自分だけミュート）")]
    public Image muteIcon;
    public Sprite iconOn;
    public Sprite iconMuted;
    [Tooltip("くり返しのボタンの絵（OFFの時はグレーアウト。2026-10-15 てじゃ「オフがグレーアウト」）")]
    public Image loopButtonImage;
    public Image loopIcon;
    [Tooltip("OFFの時のボタンの絵の色（コピーの押せない時と同じ見た目。見た目の係が決める）")]
    public Color loopOffTint = new Color(0.89f, 0.86f, 0.98f, 0.7f);
    [Tooltip("ONの時のボタンの絵の色（緑。2026-10-15 てじゃ「繰り返しオンを緑に」）")]
    public Color loopOnColor = new Color(0.45f, 0.84f, 0.6f, 1f);
    public float loopOffIconAlpha = 0.55f;

    [Tooltip("待機画面（画面の手前の板）。■で止まっている間・読み込み中・最後まで再生した時に出す（2026-10-15 てじゃ「停止して止まってる時だけ別の画面、待機画面の真っ黒とか」）")]
    public GameObject[] standbyScreens;
    [Tooltip("読み込み中に出すマーク（待機画面の中。出し入れはこれ、回すのは最初の子）（2026-10-15 てじゃ「ロード待ちの時に画面にもローディング」）")]
    public Transform[] loadingSpinners;
    public float spinnerSpeed = 300f;

    [Header("音の範囲・BGM（なくてもOK）")]
    [Tooltip("この中心から audioZoneRadius 以内に自分がいる時だけ動画の音を出す（空なら どこでも）")]
    public Transform audioZone;
    public float audioZoneRadius = 17f;
    public float audioZoneHeight = 20f;
    [Tooltip("範囲の中で動画が再生中の間、このBGMの係の bgmSilenceVariable を true にして止めてもらう（止まったら false で元の音量に戻る）")]
    public UdonSharpBehaviour bgm;
    public string bgmSilenceVariable = "externalSilence";

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
    [UdonSynced] private bool _loop;          // くり返し再生（みんな共通。2026-10-15 てじゃ「連続再生の切り替えスイッチ」）

    private int _loadedVer = -1;
    private bool _error;      // 読み込みに失敗した（読み込み直すまで）
    private bool _ready;
    private bool _loadQueued;
    private float _nextLoad;
    private float _nextCheck;
    private bool _ended;      // 最後まで再生した（自分の画面）
    private bool _inZone = true;
    private bool _userMuted;   // スピーカーのボタンで自分だけミュート
    private bool _silencing;
    private float _nextZone;

    private void Start()
    {
        ShowUrl();
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(defaultVolume);
        SetVolume(defaultVolume);
        SetStatus("Add a video with the link button");
        UpdatePlayLabel();
        ApplyLoop();
    }

    // ===== UI から（Button / InputField / Slider の SendCustomEvent） =====

    public void OnUrlEntered()
    {
        if (urlField == null) return;
        VRCUrl u = urlField.GetUrl();
        if (u == null || u.Get() == "") return;
        urlField.SetUrl(VRCUrl.Empty);
        Log(Me() + " entered URL: " + u.Get());
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
        Log(Me() + (_paused ? " pressed play" : " pressed pause") + " at " + player.GetTime().ToString("0.0") + "s");
        Take();
        double now = Networking.GetServerTimeInSeconds();
        if (_paused)
        {
            _paused = false;
            _ended = false;
            if (!IsLive())
            {
                _start = now - _pausedAt;
                player.SetTime(_pausedAt);   // 最後まで行った後などは実際の位置が _pausedAt と違うことがある（ログで 0 秒のはずが 279.6 秒から再生していた）。そろえてから再生
            }
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

    // 停止：最初に戻して止める（押した人が持ち主になって配る）。もう一度▶で頭から
    public void StopVideo()
    {
        if (!_ready) return;
        Log(Me() + " pressed stop");
        Take();
        _paused = true;
        _pausedAt = 0f;
        player.Pause();
        if (!IsLive()) player.SetTime(0f);
        RequestSerialization();
        UpdatePlayLabel();
    }

    // くり返しのオン／オフ（押した人が持ち主になって配る）。最後まで終わって止まっている時にオンにしたら、頭からもう一度
    public void ToggleLoop()
    {
        Take();
        _loop = !_loop;
        Log(Me() + " turned loop " + (_loop ? "ON" : "OFF"));
        if (_loop && _ended && _ready && !IsLive()) Restart();
        RequestSerialization();
        ApplyLoop();
    }

    private void ApplyLoop()
    {
        if (player != null && player.Loop != _loop) player.Loop = _loop;
        if (loopButtonImage != null) loopButtonImage.color = _loop ? loopOnColor : loopOffTint;
        if (loopIcon != null) { Color c = loopIcon.color; c.a = _loop ? 1f : loopOffIconAlpha; loopIcon.color = c; }
    }

    // 持ち主：頭から再生し直して配る
    private void Restart()
    {
        _ended = false;
        _paused = false;
        _pausedAt = 0f;
        _start = Networking.GetServerTimeInSeconds();
        player.SetTime(0f);
        player.Play();
    }

    // 自分の画面だけ：止まった・ずれた時に持ち主の状態へ合わせ直す（読み込めていなければ読み直す）
    public void Resync()
    {
        if (_url == null || _url.Get() == "") return;
        Log("resync pressed (ready=" + _ready + ")");
        if (!_ready) { Load(); return; }
        ApplyState(true);
    }

    // シークバーを動かした：その位置へ（動かした人が持ち主になって配る）。バーを表示に合わせて動かす時は SetValueWithoutNotify なのでここは来ない
    public void OnSeek()
    {
        if (!_ready || seekSlider == null || IsLive()) return;
        float dur = player.GetDuration();
        if (dur <= 0f) return;
        float target = Mathf.Clamp01(seekSlider.value) * dur;
        Log(Me() + " seeked to " + target.ToString("0.0") + "s");
        Take();
        if (_paused) _pausedAt = target;
        else _start = Networking.GetServerTimeInSeconds() - target;
        if (target < dur - 0.5f) _ended = false;   // 最後まで行った後にシークで戻した
        player.SetTime(target);
        RequestSerialization();
        _nextCheck = Time.time + 0.5f;   // 直後にバーを古い位置へ戻さない
    }

    // 隠すボタン・たたむタブ：操作パネルを出す／隠す（自分の画面だけ）。
    // panelHideTarget があればそれ（操作パネルの Canvas まるごと）、なければ panelRoot を出し入れする
    public void TogglePanel()
    {
        GameObject target = panelHideTarget != null ? panelHideTarget : panelRoot;
        if (target == null) return;
        bool show = !target.activeSelf;
        target.SetActive(show);
        // 出ている時▲（上に隠す）、隠れている時▼（下に出す）（2026-10-15 てじゃ「上に隠して下にだすほうがイメージ近い」）
        if (panelTabLabel != null) panelTabLabel.text = show ? "▲" : "▼";
        if (panelTab != null) { Vector2 a = panelTab.anchoredPosition; a.y = show ? 0f : panelHeight; panelTab.anchoredPosition = a; }
    }

    // スピーカーのボタン：自分だけミュート／解除
    public void ToggleMute()
    {
        _userMuted = !_userMuted;
        ApplyMute();
    }

    private void ApplyMute()
    {
        bool m = _userMuted || !_inZone;
        if (speakers != null) for (int i = 0; i < speakers.Length; i++) if (speakers[i] != null) speakers[i].mute = m;
        if (muteIcon != null) muteIcon.sprite = _userMuted ? iconMuted : iconOn;
    }

    // コピーの欄でEnterされた：書き換えられていても今のURLに戻すだけ（再生は変えない）
    public void OnCopyEdited()
    {
        ShowUrl();
    }

    private void ShowUrl()
    {
        if (copyField == null) return;
        bool has = _url != null && _url.Get() != "";
        copyField.text = has ? _url.Get() : "";
        copyField.interactable = has;   // 何も流れていない時は押せない（空のフォームが出るだけなので）
    }

    public void OnVolumeChanged()
    {
        if (volumeSlider != null) SetVolume(volumeSlider.value);
    }

    // ===== 同期 =====

    public override void OnDeserialization()
    {
        VRCPlayerApi o = Networking.GetOwner(gameObject);
        Log("received state from " + (o != null ? o.displayName : "?") + ": ver=" + _ver + " paused=" + _paused + " pausedAt=" + _pausedAt.ToString("0.0") + " start=" + _start.ToString("0.0") + " loop=" + _loop + " url=" + (_url != null ? _url.Get() : ""));
        ApplyLoop();
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
            SetStatus("Waiting...");
            Log("load waits " + wait.ToString("0.0") + "s (VRChat 5s rule)");
            return;
        }
        _ready = false;
        _ended = false;
        _error = false;
        _loadedVer = _ver;
        ShowUrl();
        _nextLoad = Time.time + loadInterval;
        player.Stop();
        player.LoadURL(_url);
        SetStatus("Loading...");
        Log("loading ver=" + _ver + ": " + _url.Get());
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
        _ended = false;   // 持ち主が再生中＝もう「最後まで行った」状態ではない
        if (!player.IsPlaying) player.Play();
        if (live || _start < 0) return;
        float target = (float)(Networking.GetServerTimeInSeconds() - _start);
        float dur = player.GetDuration();
        if (target < 0f) target = 0f;
        if (dur > 0f && target > dur)
        {
            if (!_loop) return;   // もう終わっている
            target = target % dur;  // くり返し中：何周目かのどこか
        }
        float drift = player.GetTime() - target;
        if (force || Mathf.Abs(drift) > maxDrift)
        {
            if (!force) Log("drift " + drift.ToString("0.00") + "s -> jump to " + target.ToString("0.0") + "s");
            player.SetTime(target);
        }
    }

    // ほかの係（テレビの切りかえなど）から：今まさに再生中か
    public bool IsPlayingNow() { return _ready && player.IsPlaying; }
    // 動画なし・■停止（最初で止まっている）・最後まで再生した、のどれか（一時停止は含まない）
    public bool IsIdle()
    {
        if (_url == null || _url.Get() == "") return true;
        if (_ended) return true;
        return _paused && _pausedAt <= 0.01f && !IsLive();
    }

    // 自分が動画の音の聞こえる範囲にいるか
    public bool IsListenerInZone() { return _inZone; }

    // 動画なし・最後まで再生した（■停止は含まない）
    public bool IsEndedOrEmpty()
    {
        if (_url == null || _url.Get() == "") return true;
        return _ended;
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
        Log("ready: duration=" + player.GetDuration().ToString("0.0") + "s" + (IsLive() ? " (live)" : "") + " owner=" + Networking.IsOwner(gameObject));
        if (Networking.IsOwner(gameObject))
        {
            if (_paused) { ApplyState(true); }
            else player.Play();
        }
        else ApplyState(true);
        SetStatus(IsLive() ? "Live" : "");
        UpdatePlayLabel();
    }

    public override void OnVideoStart()
    {
        _ended = false;
        Log("started at " + player.GetTime().ToString("0.0") + "s");
        // 持ち主：再生が始まった時刻から「動画の0秒のサーバー時刻」を決めて配る（最初の1回だけ。一時停止からの再開は PlayPause で決める）
        if (Networking.IsOwner(gameObject) && _start < 0 && !IsLive())
        {
            _start = Networking.GetServerTimeInSeconds() - player.GetTime();
            RequestSerialization();
        }
        UpdatePlayLabel();
    }

    // くり返しで頭に戻った：持ち主は「0秒のサーバー時刻」を今の周に合わせ直して配る（読み込みの待ちでずれた分をほかの人にも反映）
    public override void OnVideoLoop()
    {
        _ended = false;
        Log("looped");
        if (Networking.IsOwner(gameObject) && !IsLive() && !_paused)
        {
            _start = Networking.GetServerTimeInSeconds() - player.GetTime();
            RequestSerialization();
        }
    }

    // 最後まで再生した：■停止と同じ（頭に戻して止める、待機画面）。持ち主が配る。▶でまた頭から
    //（2026-10-15 てじゃ「時間で終了したときもおっかけカメラにもどっちゃう、停止と同じ状態に」）
    public override void OnVideoEnd()
    {
        _ended = true;
        Log("ended");
        if (!IsLive())
        {
            player.SetTime(0f);
            if (Networking.IsOwner(gameObject))
            {
                _paused = true;
                _pausedAt = 0f;
                RequestSerialization();
            }
        }
        SetStatus("");
        UpdatePlayLabel();
    }

    public override void OnVideoError(VideoError videoError)
    {
        _ready = false;
        Log("error: " + videoError.ToString());
        if (videoError == VideoError.RateLimited)
        {
            // 読み込みが早すぎた：5秒後にもう一度
            _nextLoad = Time.time + loadInterval;
            Load();
            return;
        }
        _error = true;
        if (videoError == VideoError.InvalidURL) SetStatus("Invalid URL");
        else if (videoError == VideoError.AccessDenied) SetStatus("Turn on \"Allow Untrusted URLs\" in VRChat settings");
        else if (videoError == VideoError.PlayerError) SetStatus("Couldn't play this video");
        else SetStatus("Error: " + videoError.ToString());
        UpdatePlayLabel();
    }

    // ===== 表示 =====

    private void Update()
    {
        UpdateZone();
        CleanFields();
        UpdateStandby();
        if (Time.time < _nextCheck) return;
        _nextCheck = Time.time + 0.5f;
        // 持ち主以外：ときどき位置のずれを見て寄せる
        if (_ready && !_paused && !Networking.IsOwner(gameObject)) ApplyState(false);
        if (seekSlider != null)
        {
            bool show = _ready && !IsLive();
            if (seekSlider.gameObject.activeSelf != show) seekSlider.gameObject.SetActive(show);
            if (statusText != null) statusText.enabled = !show;   // 状態の文字はシークバーと同じ段（バーが出ている間は隠す）
            if (show) { float d = player.GetDuration(); seekSlider.SetValueWithoutNotify(d > 0f ? Mathf.Clamp01(player.GetTime() / d) : 0f); }
        }
        if (timeText != null)
        {
            if (!_ready) timeText.text = "";
            else if (IsLive()) timeText.text = "LIVE";
            else timeText.text = Clock(player.GetTime()) + " / " + Clock(player.GetDuration());
        }
    }

    // 入力欄に、押す前のキー入力（デスクトップの移動キーなど）が入りこむことがある（2026-10-15 てじゃ「URLボタンおすまえから、入力したキーが入っちゃってる」）。
    // URL欄は送信（Enter）の時に読むだけで、ふだんは空のはず → 何か入っていたら消す。コピー欄はいつも今のURLに戻す
    private float _nextClean;
    private void CleanFields()
    {
        if (Time.time < _nextClean) return;
        _nextClean = Time.time + 0.2f;
        if (urlField != null)
        {
            VRCUrl u = urlField.GetUrl();
            if (u != null && u.Get() != "") urlField.SetUrl(VRCUrl.Empty);
        }
        if (copyField != null)
        {
            string want = (_url != null) ? _url.Get() : "";
            if (copyField.text != want) copyField.text = want;
        }
    }

    // 待機画面：まだ映っていない（動画なし・読み込み中・エラー）、■で頭に戻して止まっている、最後まで再生した → 出す
    private bool _standby;
    private bool _standbyInit;
    private void UpdateStandby()
    {
        bool sb;
        if (_ready && player.IsPlaying) { sb = false; _ended = false; }   // 実際に流れている時は絶対に出さない（2026-10-15 てじゃ「シークをいじってると待機画面で固定、音は流れてる」）
        else if (!_ready) sb = true;
        else if (_ended) sb = true;
        else sb = _paused && _pausedAt <= 0.01f && !IsLive();
        // 読み込み中（URLがあって、まだ映らず、エラーでもない）はくるくる
        bool loading = !_ready && !_error && _url != null && _url.Get() != "";
        if (loadingSpinners != null)
        {
            for (int i = 0; i < loadingSpinners.Length; i++)
            {
                Transform s = loadingSpinners[i];
                if (s == null) continue;
                if (s.gameObject.activeSelf != loading) s.gameObject.SetActive(loading);
                if (loading && s.childCount > 0) s.GetChild(0).Rotate(0f, 0f, spinnerSpeed * Time.deltaTime, Space.Self);   // 回すのは中の輪（外側は画面の縦横比の打ち消し）
            }
        }
        if (sb == _standby && _standbyInit) return;
        _standby = sb; _standbyInit = true;
        if (standbyScreens == null) return;
        for (int i = 0; i < standbyScreens.Length; i++) if (standbyScreens[i] != null) standbyScreens[i].SetActive(sb);
    }

    // 範囲の外では動画の音を消す。範囲の中で再生中ならBGMを止めてもらう
    private void UpdateZone()
    {
        if (Time.time < _nextZone) return;
        _nextZone = Time.time + 0.25f;
        bool inZone = true;
        if (audioZone != null)
        {
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (lp != null)
            {
                Vector3 d = lp.GetPosition() - audioZone.position;
                inZone = Mathf.Abs(d.y) < audioZoneHeight && new Vector2(d.x, d.z).magnitude < audioZoneRadius;
            }
        }
        if (inZone != _inZone || Time.frameCount < 5)
        {
            _inZone = inZone;
            ApplyMute();
        }
        bool silence = inZone && _ready && player.IsPlaying;
        if (silence != _silencing)
        {
            _silencing = silence;
            if (bgm != null) bgm.SetProgramVariable(bgmSilenceVariable, silence);
        }
    }

    private string Clock(float t)
    {
        if (t < 0f || float.IsNaN(t) || float.IsInfinity(t)) t = 0f;
        int s = Mathf.FloorToInt(t);
        int h = s / 3600, m = (s / 60) % 60, sec = s % 60;
        return h > 0 ? h + ":" + m.ToString("00") + ":" + sec.ToString("00") : m + ":" + sec.ToString("00");
    }

    // 表示はぜんぶ英語（ほかのワールドでも使い回せるように。2026-10-15 てじゃ「全部英語でもいいかも」）
    // 動作のログ：VRChat のログ（output_log）に [TejaPlayer] つきで書く。困った時にあとから追えるように（2026-10-15 てじゃ「なんかあったときように動作のログ」）
    private void Log(string m)
    {
        Debug.Log("[TejaPlayer] " + m);
    }

    private string Me()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        return p != null ? p.displayName : "?";
    }

    private void SetStatus(string s)
    {
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
