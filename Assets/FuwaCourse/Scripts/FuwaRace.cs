using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

// 対戦レース（1コースに1つ）。
// エントリー床に乗っている人が、スタートボタンで一緒にコースへワープ → 3・2・1・GO → ゴール順に順位。
// 状態・参加者・結果は全員に同期（Manual）。判定（終了など）はこのオブジェクトの持ち主が行う。
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaRace : UdonSharpBehaviour
{
    public const int MaxPlayers = 8;

    [Header("つながり")]
    public FuwaBall ball;
    public FuwaRespawnManager respawnManager;
    public FuwaLanguage language;
    [Tooltip("レースコースの、ふわふわのスタート（キノコの中）")]
    public Transform courseStart;
    [Tooltip("レースコースの、人の立ち位置")]
    public Transform coursePlayerSpot;
    [Tooltip("レースコースの銃の台の上")]
    public Transform courseGunSpot;
    [Tooltip("終わった後に戻る、たいせんロビーへのワープ")]
    public FuwaCourseWarp lobbyWarp;
    public FuwaAreaVisibility areaVisibility;

    [Header("表示")]
    public TextMeshPro boardText;
    [Tooltip("コース名（看板の見出し）")]
    public string courseNameJa = "レース1";
    public string courseNameEn = "Race 1";
    public AudioSource countSound;
    public AudioSource goSound;

    [Header("ルール")]
    [Tooltip("スタートボタンからGOまで(秒)。ワープして落ち着くまでの時間も込み")]
    public float countdownSeconds = 5f;
    [Tooltip("1位がゴールしてから、残りの人を待つ秒数")]
    public float afterFirstSeconds = 60f;
    [Tooltip("レースの最大時間(秒)")]
    public float maxRaceSeconds = 300f;
    [Tooltip("ゴールしてからたいせんロビーに戻るまで(秒)")]
    public float returnDelay = 6f;

    // ---- 同期 ----
    [UdonSynced] private int _state;              // 0=受付中 1=カウントダウン 2=レース中 3=結果
    [UdonSynced] private int _raceId;
    [UdonSynced] private int _goTimeMs;           // GOのサーバー時刻
    [UdonSynced] private int _firstFinishMs;      // 1位のゴール時刻（0=まだ）
    [UdonSynced] private int[] _entrants = new int[MaxPlayers];    // プレイヤーID（0=空き）
    [UdonSynced] private float[] _times = new float[MaxPlayers];   // 参加者ごとのタイム（0=走行中、-1=リタイア）
    [UdonSynced] private int[] _places = new int[MaxPlayers];      // 参加者ごとの順位（0=まだ）

    // ---- ローカル ----
    private int[] _onPad = new int[32];   // エントリー床に乗っている人のID（全員のPCで数える）
    private int _onPadCount;
    private int _seenRaceId = -1;
    private bool _inThisRace;             // 自分がこのレースの参加者で、まだ走っている
    private bool _finishedLocal;
    private bool _placeShown;
    private int _lastCount = -1;
    private float _returnAt = -1f;
    private float _nextRender;

    private void Start()
    {
        Render();
    }

    // ===== エントリー床（FuwaRaceEntryから） =====
    public void EntryEnter(int playerId)
    {
        for (int i = 0; i < _onPadCount; i++) if (_onPad[i] == playerId) return;
        if (_onPadCount < _onPad.Length) _onPad[_onPadCount++] = playerId;
        Render();
    }

    public void EntryExit(int playerId)
    {
        for (int i = 0; i < _onPadCount; i++)
        {
            if (_onPad[i] != playerId) continue;
            _onPad[i] = _onPad[_onPadCount - 1];
            _onPadCount--;
            break;
        }
        Render();
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (player == null) return;
        EntryExit(player.playerId);
        // 走っている人が抜けたらリタイア扱い（持ち主だけが書き換える）
        if (Networking.IsOwner(gameObject) && (_state == 1 || _state == 2))
        {
            int i = IndexOf(player.playerId);
            if (i >= 0 && _times[i] == 0f) { _times[i] = -1f; RequestSerialization(); }
        }
    }

    // ===== スタートボタン =====
    public void PressStart()
    {
        if (_state == 1 || _state == 2) return;
        if (_onPadCount <= 0) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(local, gameObject);
        for (int i = 0; i < MaxPlayers; i++)
        {
            _entrants[i] = i < _onPadCount ? _onPad[i] : 0;
            _times[i] = 0f;
            _places[i] = 0;
        }
        _raceId++;
        _state = 1;
        _goTimeMs = Networking.GetServerTimeInMilliseconds() + Mathf.RoundToInt(countdownSeconds * 1000f);
        _firstFinishMs = 0;
        RequestSerialization();
        OnDeserialization();
    }

    public override void OnDeserialization()
    {
        // 新しいレースが始まった：自分が参加者ならコースへ
        if (_raceId != _seenRaceId && _state == 1)
        {
            _seenRaceId = _raceId;
            VRCPlayerApi local = Networking.LocalPlayer;
            if (local != null && IndexOf(local.playerId) >= 0) JoinRace();
        }
        if (_raceId != _seenRaceId) _seenRaceId = _raceId;
        Render();
    }

    private void JoinRace()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        _inThisRace = true;
        _finishedLocal = false;
        _placeShown = false;
        _lastCount = -1;
        _returnAt = -1f;
        if (areaVisibility != null) areaVisibility.ShowArea(2);
        if (coursePlayerSpot != null) local.TeleportTo(coursePlayerSpot.position, coursePlayerSpot.rotation);
        if (respawnManager != null)
        {
            respawnManager.SetRespawnSpot(coursePlayerSpot);
            respawnManager.BringGun(courseGunSpot);
        }
        if (ball != null)
        {
            ball.StartCourse(courseStart, true);
            ball.activeRace = this;
            ball.raceLocked = true;
        }
    }

    // ===== ゴール・リタイア（自分のPCから持ち主へ） =====
    // FuwaBall.ReachGoal から呼ばれる
    public void LocalFinished()
    {
        if (!_inThisRace || _finishedLocal || _state != 2) return;
        _finishedLocal = true;
        float t = (Networking.GetServerTimeInMilliseconds() - _goTimeMs) / 1000f;
        SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(ReportFinish), Networking.LocalPlayer.playerId, Mathf.Max(0.01f, t));
        _returnAt = Time.time + returnDelay;
    }

    private void LocalRetire()
    {
        if (!_inThisRace) return;
        _inThisRace = false;
        if (ball != null) { ball.activeRace = null; ball.raceLocked = false; }
        if (!_finishedLocal) SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(ReportFinish), Networking.LocalPlayer.playerId, -1f);
    }

    [NetworkCallable]
    public void ReportFinish(int playerId, float time)
    {
        if (!Networking.IsOwner(gameObject) || (_state != 2 && _state != 1)) return;
        int i = IndexOf(playerId);
        if (i < 0 || _times[i] != 0f) return;
        _times[i] = time;
        if (time > 0f)
        {
            int place = 1;
            for (int k = 0; k < MaxPlayers; k++) if (k != i && _places[k] > 0) place++;
            _places[i] = place;
            if (_firstFinishMs == 0) _firstFinishMs = Networking.GetServerTimeInMilliseconds();
        }
        RequestSerialization();
        Render();
    }

    private void Update()
    {
        int now = Networking.GetServerTimeInMilliseconds();

        // 持ち主：状態を進める
        if (Networking.IsOwner(gameObject))
        {
            if (_state == 1 && now >= _goTimeMs) { _state = 2; RequestSerialization(); }
            else if (_state == 2)
            {
                bool allDone = true;
                for (int i = 0; i < MaxPlayers; i++) if (_entrants[i] != 0 && _times[i] == 0f) allDone = false;
                bool timeUp = (_firstFinishMs != 0 && now - _firstFinishMs > afterFirstSeconds * 1000f) || now - _goTimeMs > maxRaceSeconds * 1000f;
                if (allDone || timeUp) { _state = 3; RequestSerialization(); Render(); }
            }
        }

        // 参加者：カウントダウンとGO
        if (_inThisRace)
        {
            if (ball != null && ball.startPoint != courseStart && !_finishedLocal) { LocalRetire(); }
            else if (_state == 1 || (_state == 2 && ball != null && ball.raceLocked))
            {
                float left = (_goTimeMs - now) / 1000f;
                int count = Mathf.CeilToInt(left);
                if (left <= 0f)
                {
                    if (ball != null) ball.RaceGo();
                    ShowBig("GO!", "GO!", 1.2f);
                    if (goSound != null) goSound.Play();
                    _lastCount = 0;
                }
                else if (count <= 3 && count != _lastCount)
                {
                    _lastCount = count;
                    ShowBig(count.ToString(), count.ToString(), 1f);
                    if (countSound != null) countSound.Play();
                }
            }
            else if (_state == 3 && !_finishedLocal)
            {
                // 時間切れ
                ShowBig("タイムアップ！", "Time up!", 3f);
                _finishedLocal = true;
                _returnAt = Time.time + 3f;
            }
            // 自分の順位が届いたら大きく出す
            if (_finishedLocal && !_placeShown)
            {
                int p = GetMyPlace();
                if (p > 0) { _placeShown = true; ShowBig(p + "位！", p + Ordinal(p) + "!", 4f); }
            }
            if (_returnAt > 0f && Time.time >= _returnAt)
            {
                _returnAt = -1f;
                _inThisRace = false;
                if (ball != null) { ball.activeRace = null; ball.raceLocked = false; }
                if (lobbyWarp != null) lobbyWarp.WarpLocalPlayer();
            }
        }

        if (Time.time >= _nextRender) { _nextRender = Time.time + 0.5f; Render(); }
    }

    private void ShowBig(string ja, string en, float seconds)
    {
        if (ball != null) ball.ShowRaceMessage(ja, en, seconds);
    }

    // ===== 看板 =====
    public void Render()
    {
        if (boardText == null) return;
        bool en = language != null && language.IsEnglish();
        string s = "<align=center><size=140%><color=#FF6B5C>" + (en ? courseNameEn : courseNameJa) + "</color></size>\n";
        int now = Networking.GetServerTimeInMilliseconds();
        if (_state == 0 || _state == 3)
        {
            s += "<size=80%>" + (en ? "Stand on the entry pad → press START" : "エントリー床に乗って → スタート！") + "\n";
            s += (en ? "Entry: " : "エントリー：") + _onPadCount + (en ? " player(s)" : "人") + "</size></align>\n";
        }
        else if (_state == 1)
        {
            float left = Mathf.Max(0f, (_goTimeMs - now) / 1000f);
            s += "<size=80%>" + (en ? "Starting in " : "スタートまで ") + Mathf.CeilToInt(left) + (en ? "s" : "秒") + "</size></align>\n";
        }
        else
        {
            float t = (now - _goTimeMs) / 1000f;
            s += "<size=80%>" + (en ? "Racing  " : "レース中  ") + FormatTime(t) + "</size></align>\n";
        }

        // 順位（ゴールした人を順位順、その後に走行中・リタイア）
        if (_state != 0 || HasAnyEntrant())
        {
            if (_state == 3) s += "<align=center><size=90%>" + (en ? "- Results -" : "- 結果 -") + "</size></align>\n";
            for (int place = 1; place <= MaxPlayers; place++)
            {
                for (int i = 0; i < MaxPlayers; i++)
                {
                    if (_entrants[i] == 0 || _places[i] != place) continue;
                    s += "<color=#FF6B5C>" + place + (en ? Ordinal(place) : "位") + "</color>  " + NameOf(_entrants[i]) + "<pos=72%><mspace=0.55em>" + FormatTime(_times[i]) + "</mspace>\n";
                }
            }
            for (int i = 0; i < MaxPlayers; i++)
            {
                if (_entrants[i] == 0 || _places[i] > 0) continue;
                string st = _times[i] < 0f ? (en ? "Retired" : "リタイア") : (_state == 3 ? (en ? "Time up" : "時間切れ") : (en ? "Racing" : "走行中"));
                s += "<color=#8A7A9A>-</color>  " + NameOf(_entrants[i]) + "<pos=72%><color=#8A7A9A>" + st + "</color>\n";
            }
        }
        boardText.text = s;
    }

    private bool HasAnyEntrant()
    {
        for (int i = 0; i < MaxPlayers; i++) if (_entrants[i] != 0) return true;
        return false;
    }

    private int IndexOf(int playerId)
    {
        for (int i = 0; i < MaxPlayers; i++) if (_entrants[i] == playerId && playerId != 0) return i;
        return -1;
    }

    // 自分の順位（なければ0）
    public int GetMyPlace()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return 0;
        int i = IndexOf(local.playerId);
        return i >= 0 ? _places[i] : 0;
    }

    public bool IsRacing() { return _state == 2; }
    public bool IsEntrant(int playerId) { return (_state == 1 || _state == 2) && IndexOf(playerId) >= 0; }

    private string NameOf(int id)
    {
        VRCPlayerApi p = VRCPlayerApi.GetPlayerById(id);
        return (p != null && p.IsValid()) ? p.displayName : "???";
    }

    private string Ordinal(int n)
    {
        if (n == 1) return "st";
        if (n == 2) return "nd";
        if (n == 3) return "rd";
        return "th";
    }

    private string FormatTime(float t)
    {
        // タイマーと同じ：99:59.99 で止める・1/100秒の整数にしてから分ける
        int cs = Mathf.Clamp(Mathf.FloorToInt(t * 100f), 0, 599999);
        return (cs / 6000).ToString() + ":" + ((cs / 100) % 60).ToString("00") + "." + (cs % 100).ToString("00");
    }
}
