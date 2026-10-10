using UdonSharp;
using UnityEngine;
using TMPro;
using VRC.SDKBase;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;

// 「みんなで」の色ゴールの取り合い（2026-10-10 仮組み）。
// ふわふわは1個。会場のまわりに色ごとのゴール（輪っか）があって、だれが入れても入ったゴールの色に1点。
// 自分の色のゴールに入れたい（相手の色に入れると相手の点）。ひとりでも2色で遊べる（雰囲気を見たい人用）。
// 設定・メンバー・得点・試合の状態は、このオブジェクトの持ち主（＝設定できる人。最初はマスター）が持って配る。
// ほかの人のボタンは「こうしたい」を持ち主に送るだけで、持ち主が決めて配り直す。
// ゴールに入ったかは、ふわふわの持ち主の画面で見て、試合の持ち主に知らせる。
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaMatch : UdonSharpBehaviour
{
    const int MaxColors = 4;
    const int MaxMembers = 32;
    const int Any = 4;   // おまかせ（始める時に人数が少ない色へ）
    const int PhaseLobby = 0, PhaseCountdown = 1, PhasePlay = 2, PhaseResult = 3;

    [Header("会場")]
    public FuwaSharedBall ball;
    [Tooltip("今いるエリア。みんなでにいる人だけ会場へ飛ばす（ほかのエリアにいる人は飛ばさない）")]
    public FuwaAreaVisibility areas;
    [Tooltip("色ごとのゴール（輪っかの中心。+Z が輪っかの面の向き、大きさは1のまま）。順番は あか・あお・きいろ・みどり")]
    public Transform[] goals;
    public float goalRadius = 1.1f;
    [Tooltip("輪っかの面からこの厚みの中を通ったら入ったことにする(m)")]
    public float goalDepth = 0.35f;
    [Tooltip("色ごとのスタート地点")]
    public Transform[] spawns;
    [Tooltip("会場の真ん中。色の数に合わせて、ゴールとスタート地点をまわりに等間隔に並べ直す")]
    public Transform arenaCenter;
    public float goalRingRadius = 10f;
    public float goalHeight = 2.8f;
    public float spawnRadius = 6.5f;
    [Tooltip("1つ目のゴールの向き（度。0＝真ん中から +X）")]
    public float goalAngle0 = 90f;
    private int _laidOut = -1;
    [Tooltip("試合が終わったら戻る場所（チーム分けの部屋）")]
    public Transform lobbySpawn;
    [Tooltip("ゴールの得点の時の演出（色ごと、なくてもOK）")]
    public ParticleSystem[] goalFx;
    public AudioSource goalSound;

    [Header("チーム分けの部屋")]
    [Tooltip("色を選ぶ台（色の数に合わせて出し入れ）")]
    public GameObject[] colorPads;
    public TextMeshPro[] padLabels;
    public TextMeshPro lobbyBoard;
    [Tooltip("会場の得点板（なくてもOK、何枚でも）")]
    public TextMeshPro[] arenaBoards;

    [Header("ふわふわの種類（バトル用がデフォ。ひとりと同じ＝撃った時の効き・大きさがひとりモードと同じ）")]
    public float battleShotScale = 0.6f;
    public float battleSize = 0.45f;
    public float soloShotScale = 1f;
    public float soloSize = 0.3f;

    [Header("時間")]
    public float countdown = 3f;
    public float resultTime = 7f;

    private string[] _colorNames;
    private string[] _colorHex;

    // ---- 同期（持ち主が決める） ----
    [UdonSynced] private int _colorCount = 2;
    [UdonSynced] private bool _scoreMode;          // false=時間 / true=点数先取
    [UdonSynced] private int _timeLimit = 180;     // 秒
    [UdonSynced] private int _scoreTarget = 5;
    [UdonSynced] private bool _randomTeams;        // 始める時に全員の色をランダムに振り分け直す
    [UdonSynced] private bool _allEdit;            // 全員が設定を変えられる
    [UdonSynced] private bool _soloBall;           // ふわふわを「ひとりと同じ」に（デフォはバトル用）
    [UdonSynced] private int _phase;
    [UdonSynced] private double _phaseAt;
    [UdonSynced] private double _endAt;
    [UdonSynced] private int _matchId;
    [UdonSynced] private int _winner = -1;
    [UdonSynced] private int _lastGoal = -1;
    [UdonSynced] private int _goalSeq;
    [UdonSynced] private int[] _scores = new int[MaxColors];
    [UdonSynced] private int[] _memberIds = new int[MaxMembers];     // 0=空き
    [UdonSynced] private int[] _memberColors = new int[MaxMembers];

    // ---- 自分の画面だけ ----
    private bool _applied;
    private int _seenMatchId;
    private int _seenPhase = -1;
    private int _seenGoalSeq;
    private bool _inMatch;
    private float _goalCooldown;
    private float _nextBoard;

    private void Start()
    {
        _colorNames = new string[] { "あか", "あお", "きいろ", "みどり" };
        _colorHex = new string[] { "#FF5A5A", "#3F86FF", "#F2B800", "#2FB85A" };
        ApplyState();
    }

    // ================= ボタン（押した人の画面で） =================
    public void UiJoin0() { Ask(nameof(ReqJoin), 0); }
    public void UiJoin1() { Ask(nameof(ReqJoin), 1); }
    public void UiJoin2() { Ask(nameof(ReqJoin), 2); }
    public void UiJoin3() { Ask(nameof(ReqJoin), 3); }
    public void UiJoinAny() { Ask(nameof(ReqJoin), Any); }
    public void UiLeave() { Ask(nameof(ReqJoin), -1); }
    public void UiColorsMinus() { Ask(nameof(ReqSetting), 0); }
    public void UiColorsPlus() { Ask(nameof(ReqSetting), 1); }
    public void UiRuleToggle() { Ask(nameof(ReqSetting), 2); }
    public void UiValueMinus() { Ask(nameof(ReqSetting), 3); }
    public void UiValuePlus() { Ask(nameof(ReqSetting), 4); }
    public void UiRandomToggle() { Ask(nameof(ReqSetting), 5); }
    public void UiAllEditToggle() { Ask(nameof(ReqSetting), 6); }
    public void UiBallToggle() { Ask(nameof(ReqSetting), 7); }
    public void UiShuffle() { Ask(nameof(ReqSetting), 8); }
    public void UiStart() { Ask(nameof(ReqStart), 0); }
    public void UiStop() { Ask(nameof(ReqStop), 0); }
    public void UiPassAdmin() { if (Networking.IsOwner(gameObject)) PassAdmin(); }

    // 持ち主なら自分で、そうでなければ持ち主に頼む
    private void Ask(string ev, int arg)
    {
        if (Networking.IsOwner(gameObject)) SendCustomEvent("Local" + ev + (arg < 0 ? "_1" : arg.ToString()));
        else SendCustomNetworkEvent(NetworkEventTarget.Owner, ev, arg);
    }
    // 持ち主が自分のボタンを押した時の入口（SendCustomEvent は引数を渡せないので、引数ごとに用意）
    public void LocalReqJoin0() { ReqJoin(0); }
    public void LocalReqJoin1() { ReqJoin(1); }
    public void LocalReqJoin2() { ReqJoin(2); }
    public void LocalReqJoin3() { ReqJoin(3); }
    public void LocalReqJoin4() { ReqJoin(Any); }
    public void LocalReqJoin_1() { ReqJoin(-1); }
    public void LocalReqSetting0() { ReqSetting(0); }
    public void LocalReqSetting1() { ReqSetting(1); }
    public void LocalReqSetting2() { ReqSetting(2); }
    public void LocalReqSetting3() { ReqSetting(3); }
    public void LocalReqSetting4() { ReqSetting(4); }
    public void LocalReqSetting5() { ReqSetting(5); }
    public void LocalReqSetting6() { ReqSetting(6); }
    public void LocalReqSetting7() { ReqSetting(7); }
    public void LocalReqSetting8() { ReqSetting(8); }
    public void LocalReqStart0() { ReqStart(0); }
    public void LocalReqStop0() { ReqStop(0); }

    private VRCPlayerApi Caller()
    {
        return NetworkCalling.InNetworkCall ? NetworkCalling.CallingPlayer : Networking.LocalPlayer;
    }

    private bool CanEdit(VRCPlayerApi p)
    {
        if (p == null) return false;
        return _allEdit || p.isLocal;   // 持ち主の画面で動いているので、isLocal＝持ち主本人
    }

    // ================= 持ち主の画面で =================
    [NetworkCallable(10)]
    public void ReqJoin(int color)
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCPlayerApi p = Caller();
        if (p == null || _phase != PhaseLobby) return;
        if (color != -1 && color != Any && (color < 0 || color >= _colorCount)) return;
        int slot = FindMember(p.playerId);
        if (color == -1)
        {
            if (slot >= 0) { _memberIds[slot] = 0; _memberColors[slot] = 0; }
        }
        else
        {
            if (slot < 0) slot = FindMember(0);
            if (slot < 0) return;   // 満員
            _memberIds[slot] = p.playerId;
            _memberColors[slot] = color;
        }
        Commit();
    }

    // 0/1=色の数 −/+、2=時間⇔点数、3/4=時間か点数の −/+、5=ランダム振り分け、6=全員が設定OK（6は持ち主だけ）、7=ふわふわの種類、8=今のメンバーをシャッフル
    [NetworkCallable(10)]
    public void ReqSetting(int key)
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCPlayerApi p = Caller();
        if (!CanEdit(p) || _phase != PhaseLobby) return;
        if (key == 0) _colorCount = Mathf.Max(2, _colorCount - 1);
        else if (key == 1) _colorCount = Mathf.Min(MaxColorsNow(), _colorCount + 1);
        else if (key == 2) _scoreMode = !_scoreMode;
        else if (key == 3 || key == 4)
        {
            int dir = key == 3 ? -1 : 1;
            if (_scoreMode) _scoreTarget = Mathf.Clamp(_scoreTarget + dir, 1, 20);
            else _timeLimit = Mathf.Clamp(_timeLimit + dir * 30, 60, 600);
        }
        else if (key == 5) _randomTeams = !_randomTeams;
        else if (key == 6) { if (!p.isLocal) return; _allEdit = !_allEdit; }
        else if (key == 7) _soloBall = !_soloBall;
        else if (key == 8)
        {
            // 今いるメンバーを、色の人数がそろうようにランダムに振り分け直す（2026-10-10 てじゃ）
            for (int i = 0; i < MaxMembers; i++) if (_memberIds[i] != 0) _memberColors[i] = Any;
            AssignColors();
        }
        ClampColors();
        Commit();
    }

    [NetworkCallable(5)]
    public void ReqStart(int unused)
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCPlayerApi p = Caller();
        if (!CanEdit(p) || _phase != PhaseLobby) return;
        // 押した人がまだ色を選んでいなければ、おまかせで入れる
        if (FindMember(p.playerId) < 0)
        {
            int s = FindMember(0);
            if (s >= 0) { _memberIds[s] = p.playerId; _memberColors[s] = Any; }
        }
        _colorCount = Mathf.Clamp(_colorCount, 2, MaxColorsNow());
        ClampColors();
        AssignColors();
        for (int i = 0; i < MaxColors; i++) _scores[i] = 0;
        _winner = -1;
        _lastGoal = -1;
        _matchId++;
        _phase = PhaseCountdown;
        _phaseAt = Networking.GetServerTimeInSeconds();
        Commit();
    }

    [NetworkCallable(5)]
    public void ReqStop(int unused)
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (!CanEdit(Caller()) || _phase == PhaseLobby) return;
        _phase = PhaseLobby;
        _phaseAt = Networking.GetServerTimeInSeconds();
        Commit();
    }

    // ふわふわの持ち主の画面から：ゴールに入った
    [NetworkCallable(10)]
    public void ReqGoal(int goal)
    {
        if (!Networking.IsOwner(gameObject) || _phase != PhasePlay) return;
        if (goal < 0 || goal >= _colorCount) return;
        _scores[goal]++;
        _lastGoal = goal;
        _goalSeq++;
        if (_scoreMode && _scores[goal] >= _scoreTarget) Finish();
        Commit();
    }
    public void LocalGoal0() { ReqGoal(0); }
    public void LocalGoal1() { ReqGoal(1); }
    public void LocalGoal2() { ReqGoal(2); }
    public void LocalGoal3() { ReqGoal(3); }

    // 設定できる権利（このオブジェクトの持ち主）を次の人へ
    private void PassAdmin()
    {
        VRCPlayerApi me = Networking.LocalPlayer;
        int n = VRCPlayerApi.GetPlayerCount();
        if (n < 2) return;
        VRCPlayerApi[] all = new VRCPlayerApi[n];
        VRCPlayerApi.GetPlayers(all);
        // 自分の次の番号の人（いなければ一番小さい番号の人）。色を選んでいる人を優先
        VRCPlayerApi pick = PickNext(all, me.playerId, true);
        if (pick == null) pick = PickNext(all, me.playerId, false);
        if (pick == null) return;
        Networking.SetOwner(pick, gameObject);
    }

    private VRCPlayerApi PickNext(VRCPlayerApi[] all, int myId, bool membersOnly)
    {
        VRCPlayerApi best = null, lowest = null;
        foreach (VRCPlayerApi q in all)
        {
            if (q == null || q.playerId == myId) continue;
            if (membersOnly && FindMember(q.playerId) < 0) continue;
            if (q.playerId > myId && (best == null || q.playerId < best.playerId)) best = q;
            if (lowest == null || q.playerId < lowest.playerId) lowest = q;
        }
        return best != null ? best : lowest;
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        ApplyState();   // 板の「設定できる人」を更新
    }

    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (player == null || !Networking.IsOwner(gameObject)) return;
        int s = FindMember(player.playerId);
        if (s >= 0) { _memberIds[s] = 0; _memberColors[s] = 0; Commit(); }
    }

    public override void OnPlayerJoined(VRCPlayerApi player)
    {
        if (Networking.IsOwner(gameObject)) RequestSerialization();
    }

    private void Finish()
    {
        int best = -1, bestScore = -1;
        bool tie = false;
        for (int i = 0; i < _colorCount; i++)
        {
            if (_scores[i] > bestScore) { bestScore = _scores[i]; best = i; tie = false; }
            else if (_scores[i] == bestScore) tie = true;
        }
        _winner = tie ? -1 : best;
        _phase = PhaseResult;
        _phaseAt = Networking.GetServerTimeInSeconds();
    }

    // 色の数の上限：2人まで2色、3人で3色、4人以上で4色
    private int MaxColorsNow()
    {
        return Mathf.Clamp(VRCPlayerApi.GetPlayerCount(), 2, MaxColors);
    }

    // 色の数を減らした時、なくなった色の人はおまかせに
    private void ClampColors()
    {
        for (int i = 0; i < MaxMembers; i++)
            if (_memberIds[i] != 0 && _memberColors[i] != Any && _memberColors[i] >= _colorCount) _memberColors[i] = Any;
    }

    // おまかせの人を、人数が少ない色から順に入れる（ランダム振り分けなら全員おまかせ扱い）
    private void AssignColors()
    {
        int[] count = new int[MaxColors];
        for (int i = 0; i < MaxMembers; i++)
        {
            if (_memberIds[i] == 0) continue;
            if (_randomTeams) _memberColors[i] = Any;
            else if (_memberColors[i] != Any) count[_memberColors[i]]++;
        }
        // おまかせの人をランダムな順に
        int[] order = new int[MaxMembers];
        int n = 0;
        for (int i = 0; i < MaxMembers; i++) if (_memberIds[i] != 0 && _memberColors[i] == Any) order[n++] = i;
        for (int i = n - 1; i > 0; i--) { int j = Random.Range(0, i + 1); int t = order[i]; order[i] = order[j]; order[j] = t; }
        for (int k = 0; k < n; k++)
        {
            int c = 0, min = 9999;
            int start = Random.Range(0, _colorCount);
            for (int m = 0; m < _colorCount; m++)
            {
                int cc = (start + m) % _colorCount;
                if (count[cc] < min) { min = count[cc]; c = cc; }
            }
            _memberColors[order[k]] = c;
            count[c]++;
        }
    }

    private int FindMember(int playerId)
    {
        for (int i = 0; i < MaxMembers; i++) if (_memberIds[i] == playerId) return i;
        return -1;
    }

    private void Commit()
    {
        RequestSerialization();
        ApplyState();
    }

    public override void OnDeserialization()
    {
        ApplyState();
    }

    private void Update()
    {
        double now = Networking.GetServerTimeInSeconds();
        if (Networking.IsOwner(gameObject))
        {
            if (_phase == PhaseCountdown && now >= _phaseAt + countdown)
            {
                _phase = PhasePlay;
                _phaseAt = now;
                _endAt = now + _timeLimit;
                Commit();
            }
            else if (_phase == PhasePlay && !_scoreMode && now >= _endAt) { Finish(); Commit(); }
            else if (_phase == PhaseResult && now >= _phaseAt + resultTime)
            {
                _phase = PhaseLobby;
                _phaseAt = now;
                Commit();
            }
        }

        // ゴールに入ったか（ふわふわの持ち主の画面で）
        if (_phase == PhasePlay && ball != null && goals != null && Time.time > _goalCooldown
            && Networking.IsOwner(ball.gameObject) && ball.IsFlying())
        {
            Vector3 bp = ball.transform.position;
            for (int i = 0; i < _colorCount && i < goals.Length; i++)
            {
                if (goals[i] == null) continue;
                Vector3 l = goals[i].InverseTransformPoint(bp);
                if (Mathf.Abs(l.z) < goalDepth && l.x * l.x + l.y * l.y < goalRadius * goalRadius)
                {
                    _goalCooldown = Time.time + 1f;
                    ball.ForceHome();
                    if (Networking.IsOwner(gameObject)) ReqGoal(i);
                    else SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(ReqGoal), i);
                    break;
                }
            }
        }

        if (Time.time >= _nextBoard) { _nextBoard = Time.time + 0.25f; UpdateBoards(); }
    }

    // ================= 全員の画面で：届いた状態を見た目に反映 =================
    private void ApplyState()
    {
        if (_colorNames == null) return;   // Start 前
        bool first = !_applied;
        _applied = true;
        if (_laidOut != _colorCount) LayoutGoals();

        for (int i = 0; i < MaxColors; i++)
        {
            bool on = i < _colorCount;
            if (goals != null && i < goals.Length && goals[i] != null && goals[i].gameObject.activeSelf != on) goals[i].gameObject.SetActive(on);
            if (colorPads != null && i < colorPads.Length && colorPads[i] != null && colorPads[i].activeSelf != on) colorPads[i].SetActive(on);
        }
        if (ball != null)
        {
            ball.locked = _phase != PhasePlay;
            ball.shotScale = _soloBall ? soloShotScale : battleShotScale;
            float sz = _soloBall ? soloSize : battleSize;
            if (Mathf.Abs(ball.transform.localScale.x - sz) > 0.001f) ball.transform.localScale = Vector3.one * sz;
        }

        if (first)
        {
            // 入ってきたばかり：今の試合・ゴールは見送る（自分はメンバーではないので飛ばない）
            _seenMatchId = _matchId;
            _seenGoalSeq = _goalSeq;
            _seenPhase = _phase;
        }

        // 新しい試合が始まった：メンバーなら自分の色のスタート地点へ
        if (_matchId != _seenMatchId && _phase != PhaseLobby)
        {
            _seenMatchId = _matchId;
            int c = MyColor();
            bool here = areas == null || areas.GetArea() == 2;
            if (here && c >= 0 && c < MaxColors && spawns != null && c < spawns.Length && spawns[c] != null)
            {
                Networking.LocalPlayer.TeleportTo(spawns[c].position, spawns[c].rotation);
                _inMatch = true;
            }
            if (ball != null && Networking.IsOwner(ball.gameObject)) ball.ForceHome();
        }

        if (_phase != _seenPhase)
        {
            if ((_phase == PhaseResult || _phase == PhaseLobby) && ball != null && Networking.IsOwner(ball.gameObject)) ball.ForceHome();
            if (_phase == PhaseLobby && _inMatch)
            {
                _inMatch = false;
                if (lobbySpawn != null && (areas == null || areas.GetArea() == 2)) Networking.LocalPlayer.TeleportTo(lobbySpawn.position, lobbySpawn.rotation);
            }
            _seenPhase = _phase;
        }

        if (_goalSeq != _seenGoalSeq)
        {
            _seenGoalSeq = _goalSeq;
            if (_lastGoal >= 0)
            {
                if (goalFx != null && _lastGoal < goalFx.Length && goalFx[_lastGoal] != null) goalFx[_lastGoal].Play();
                if (goalSound != null) goalSound.Play();
            }
        }
        UpdateBoards();
    }

    // 色の数に合わせて、ゴールとスタート地点を真ん中のまわりに等間隔に（2色＝向かい合わせ、3色＝三角、4色＝十字）
    private void LayoutGoals()
    {
        _laidOut = _colorCount;
        if (arenaCenter == null) return;
        Vector3 c = arenaCenter.position;
        for (int i = 0; i < _colorCount && i < MaxColors; i++)
        {
            float a = (goalAngle0 + 360f * i / _colorCount) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Quaternion face = Quaternion.LookRotation(-dir, Vector3.up);   // 真ん中を向く
            if (goals != null && i < goals.Length && goals[i] != null)
                goals[i].SetPositionAndRotation(c + dir * goalRingRadius + Vector3.up * goalHeight, face);
            if (spawns != null && i < spawns.Length && spawns[i] != null)
                spawns[i].SetPositionAndRotation(c + dir * spawnRadius, face);
        }
    }

    private int MyColor()
    {
        VRCPlayerApi me = Networking.LocalPlayer;
        if (me == null) return -1;
        int s = FindMember(me.playerId);
        return s < 0 ? -1 : _memberColors[s];
    }

    private string ColorTag(int c)
    {
        if (c == Any) return "おまかせ";
        if (c < 0 || c >= MaxColors) return "-";
        return "<color=" + _colorHex[c] + ">" + _colorNames[c] + "</color>";
    }

    private string Names(int color)
    {
        string s = "";
        for (int i = 0; i < MaxMembers; i++)
        {
            if (_memberIds[i] == 0 || _memberColors[i] != color) continue;
            VRCPlayerApi p = VRCPlayerApi.GetPlayerById(_memberIds[i]);
            if (p == null) continue;
            s += (s.Length > 0 ? "、" : "") + p.displayName;
        }
        return s;
    }

    private int CountColor(int color)
    {
        int n = 0;
        for (int i = 0; i < MaxMembers; i++) if (_memberIds[i] != 0 && _memberColors[i] == color && VRCPlayerApi.GetPlayerById(_memberIds[i]) != null) n++;
        return n;
    }

    private string TimeText(double sec)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt((float)sec));
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    private void UpdateBoards()
    {
        if (_colorNames == null) return;
        double now = Networking.GetServerTimeInSeconds();
        VRCPlayerApi admin = Networking.GetOwner(gameObject);
        string adminName = admin != null ? admin.displayName : "-";

        if (lobbyBoard != null)
        {
            string t = "<size=130%><color=#FF6B5C>いろゴール</color></size>\n";
            t += "自分の色のゴールにふわふわを入れよう\n";
            t += "<size=80%>（相手の色に入れると相手の点）</size>\n\n";
            t += "色の数：" + _colorCount + "　　ルール：" + (_scoreMode ? _scoreTarget + "点 先取" : TimeText(_timeLimit) + " の時間制") + "\n";
            t += "チーム：" + (_randomTeams ? "ランダムに振り分け" : "自分で選ぶ") + "　　ふわふわ：" + (_soloBall ? "ひとりと同じ" : "バトル用") + "\n";
            t += "設定できる人：" + adminName + (_allEdit ? "（みんなOK）" : "") + "\n\n";
            for (int c = 0; c < _colorCount; c++) t += ColorTag(c) + "：" + Names(c) + "\n";
            string any = Names(Any);
            if (any.Length > 0) t += "おまかせ：" + any + "\n";
            int mine = MyColor();
            t += "\nあなた：" + (mine < 0 ? "まだ選んでない" : ColorTag(mine));
            if (_phase != PhaseLobby) t += "\n<color=#FF6B5C>試合中</color>";
            lobbyBoard.text = t;
        }
        if (padLabels != null)
            for (int c = 0; c < padLabels.Length && c < MaxColors; c++)
                if (padLabels[c] != null) padLabels[c].text = _colorNames[c] + "\n<size=70%>" + CountColor(c) + "人</size>";

        if (arenaBoards != null && arenaBoards.Length > 0)
        {
            string a = "";
            for (int c = 0; c < _colorCount; c++) a += (c > 0 ? "　" : "") + "<color=" + _colorHex[c] + ">" + _colorNames[c] + " " + _scores[c] + "</color>";
            a += "\n";
            if (_phase == PhaseCountdown) a += "<size=150%>" + Mathf.Max(1, Mathf.CeilToInt((float)(_phaseAt + countdown - now))) + "</size>";
            else if (_phase == PhasePlay) a += _scoreMode ? _scoreTarget + "点 先取" : TimeText(_endAt - now);
            else if (_phase == PhaseResult) a += _winner >= 0 ? ColorTag(_winner) + " の勝ち！" : "ひきわけ！";
            else a += "<size=70%>チーム分けの部屋でスタート</size>";
            foreach (TextMeshPro b in arenaBoards) if (b != null) b.text = a;
        }
    }
}
