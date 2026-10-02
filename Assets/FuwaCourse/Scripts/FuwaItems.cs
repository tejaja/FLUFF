using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

// 対戦アイテム（シーンに1つ、自分のPCだけで動く）。
// アイテムボックスに触る → ルーレット → 自動で発動。
// 相手にかける効果は、相手の分身（FuwaGhost、持ち主＝相手）へネットワークイベントを送り、相手のPCで実行する。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaItems : UdonSharpBehaviour
{
    public const int Heavy = 0, Tailwind = 1, Thread = 2, Fog = 3, Bubble = 4;

    public FuwaBall ball;
    public FuwaFade fade;
    [Tooltip("対戦レース（アイテムが使えるのは、このどれかに参加して走っている間だけ）")]
    public FuwaRace[] races;

    [Header("効果の長さなど")]
    public float heavySeconds = 3f;
    public float tailwindSeconds = 2f;
    public float threadSeconds = 10f;      // 置いたクモの糸が残る時間
    public float threadStick = 0.5f;       // 触った人がくっつく時間
    public float threadRadius = 0.6f;
    public float fogSeconds = 2f;
    public Color fogColor = new Color(0.93f, 0.9f, 0.97f, 1f);
    [Range(0f, 1f)] public float fogAlpha = 0.8f;
    public float rouletteSeconds = 1.0f;
    [Tooltip("シャボン玉が残る秒数（攻撃を受けなくても、この時間で消える）")]
    public float bubbleSeconds = 5f;

    [Header("音（なくてもOK）")]
    public AudioSource getSound;
    public AudioSource useSound;
    public AudioSource hitSound;
    public AudioSource shieldPopSound;

    private bool _rolling;
    private float _rollEnd;
    private int _rollItem;
    private float _nextRollText;
    private bool _shield;
    private float _shieldUntil;
    private int[] _hitTrapEnd = new int[128];   // プレイヤーIDごとに、最後にくっついたクモの糸（同じ糸で何度もくっつかない）

    // ===== アイテムボックスから =====
    public bool CanReceive()
    {
        return !_rolling && GetMyRace() != null && ball != null && !ball.IsWaitingOrHidden();
    }

    public void GiveRandom()
    {
        if (!CanReceive()) return;
        _rolling = true;
        _rollEnd = Time.time + rouletteSeconds;
        _rollItem = PickItem();
        if (getSound != null) getSound.Play();
    }

    private void Update()
    {
        if (ball == null) return;
        if (_rolling)
        {
            if (Time.time >= _rollEnd)
            {
                _rolling = false;
                Use(_rollItem);
            }
            else if (Time.time >= _nextRollText)
            {
                _nextRollText = Time.time + 0.08f;
                int r = Random.Range(0, 5);
                ball.ShowInfo("？ " + NameJa(r) + " ？", "? " + NameEn(r) + " ?", 0.3f);
            }
        }
        CheckTraps();
        // レースが終わった・時間切れでシャボン玉は消える
        if (_shield && (GetMyRace() == null || Time.time >= _shieldUntil))
        {
            SetShield(false);
            if (shieldPopSound != null) shieldPopSound.Play();
        }
    }

    // ===== 抽選：後ろの順位ほど強いアイテム =====
    private int PickItem()
    {
        UpdateMyRank();
        int rank = _rank, total = _total;
        // 0=先頭 〜 1=最後
        float back = total > 1 ? (rank - 1) / (float)(total - 1) : 0.5f;
        float wHeavy = Mathf.Lerp(0.5f, 3f, back);
        float wTail = Mathf.Lerp(1f, 3f, back);
        float wThread = Mathf.Lerp(3f, 1f, back);
        float wFog = Mathf.Lerp(0f, 3f, back);
        float wBubble = Mathf.Lerp(3f, 0.3f, back);
        if (total <= 1) { wHeavy = 0f; wFog = 0f; }   // 相手がいない時は自分用だけ
        float sum = wHeavy + wTail + wThread + wFog + wBubble;
        float x = Random.Range(0f, sum);
        if (x < wHeavy) return Heavy;
        x -= wHeavy;
        if (x < wTail) return Tailwind;
        x -= wTail;
        if (x < wThread) return Thread;
        x -= wThread;
        if (x < wFog) return Fog;
        return Bubble;
    }

    // ===== 発動 =====
    private void Use(int item)
    {
        if (useSound != null) useSound.Play();
        FuwaRace race = GetMyRace();
        if (item == Heavy)
        {
            FuwaGhost target = FindAhead(false);
            if (target == null) { ball.ShowInfo("おもりキノコ… 前に誰もいない！", "Heavy spore... nobody ahead!", 2f); return; }
            target.SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(FuwaGhost.HitHeavy));
            ball.ShowInfo("おもりキノコ！ " + OwnerName(target) + " を重くした", "Heavy spore on " + OwnerName(target) + "!", 2f);
        }
        else if (item == Fog)
        {
            int n = SendToAllAhead();
            if (n == 0) ball.ShowInfo("きりのたね… 前に誰もいない！", "Fog seed... nobody ahead!", 2f);
            else ball.ShowInfo("きりのたね！ 前の " + n + "人をモヤモヤに", "Fog seed! " + n + " ahead fogged", 2f);
        }
        else if (item == Tailwind)
        {
            ball.ApplyTailwind(CourseForward(race), tailwindSeconds);
            ball.ShowInfo("おいかぜ！", "Tailwind!", 2f);
        }
        else if (item == Thread)
        {
            FuwaGhost mine = GetMyGhost();
            if (mine != null)
            {
                Vector3 back = -CourseForward(race) * 1.2f;
                mine.PlaceTrap(ball.transform.position + back, threadSeconds);
            }
            ball.ShowInfo("クモの糸を置いた！", "Placed a web!", 2f);
        }
        else if (item == Bubble)
        {
            SetShield(true);
            _shieldUntil = Time.time + bubbleSeconds;
            ball.ShowInfo("シャボン玉！ " + Mathf.RoundToInt(bubbleSeconds) + "秒間 攻撃を1回ふせぐ", "Bubble! Blocks one attack for " + Mathf.RoundToInt(bubbleSeconds) + "s", 2f);
        }
    }

    // ===== 相手から届いた効果（FuwaGhost経由で自分のPCで呼ばれる） =====
    public void ReceiveHeavy()
    {
        if (GetMyRace() == null || ball == null) return;
        if (UseShield()) return;
        ball.ApplyItemHeavy(heavySeconds);
        ball.ShowInfo("おもりキノコをくらった！", "Hit by a heavy spore!", 2f);
        if (hitSound != null) hitSound.Play();
    }

    public void ReceiveFog()
    {
        if (GetMyRace() == null) return;
        if (UseShield()) return;
        if (fade != null) fade.PlayTint(fogColor, fogAlpha, 0.25f, fogSeconds, 0.6f);
        if (ball != null) ball.ShowInfo("きりでモヤモヤ…！", "Fogged!", 2f);
        if (hitSound != null) hitSound.Play();
    }

    // 他の人が置いたクモの糸に、自分のふわふわが触れたか
    private void CheckTraps()
    {
        if (GetMyRace() == null || ball == null || ball.IsWaitingOrHidden()) return;
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);
        Vector3 bp = ball.transform.position;
        foreach (VRCPlayerApi p in players)
        {
            if (p == null || !p.IsValid() || p.isLocal) continue;
            FuwaGhost g = GhostOf(p);
            if (g == null || !g.TrapActive()) continue;
            if (Vector3.Distance(bp, g.GetTrapPos()) > threadRadius) continue;
            int id = p.playerId % _hitTrapEnd.Length;
            if (_hitTrapEnd[id] == g.GetTrapEnd()) continue;
            _hitTrapEnd[id] = g.GetTrapEnd();
            if (UseShield()) continue;
            ball.StickSoft(threadStick);
            ball.ShowInfo("クモの糸にひっかかった！", "Caught in a web!", 1.5f);
        }
    }

    // ===== シャボン玉 =====
    private bool UseShield()
    {
        if (!_shield) return false;
        SetShield(false);
        if (shieldPopSound != null) shieldPopSound.Play();
        if (ball != null) ball.ShowInfo("シャボン玉でふせいだ！", "Bubble blocked it!", 1.5f);
        return true;
    }

    private void SetShield(bool on)
    {
        _shield = on;
        FuwaGhost mine = GetMyGhost();
        if (mine != null) mine.SetShielded(on);
    }

    // ===== 順位・相手さがし =====
    public FuwaRace GetMyRace()
    {
        if (races == null || ball == null) return null;
        foreach (FuwaRace r in races)
        {
            if (r != null && ball.activeRace == r && r.IsRacing()) return r;
        }
        return null;
    }

    private Vector3 CourseForward(FuwaRace race)
    {
        if (race != null && race.courseStart != null)
        {
            // コースは各ルートの +z 方向に伸びている
            return race.courseStart.root.forward;
        }
        return ball.transform.forward;
    }

    private float Progress(FuwaRace race, Vector3 pos)
    {
        if (race == null || race.courseStart == null) return 0f;
        return race.courseStart.root.InverseTransformPoint(pos).z;
    }

    private int _rank = 1, _total = 1;

    // 自分の順位（コースの進み具合で比べる）を _rank / _total に入れる
    private void UpdateMyRank()
    {
        FuwaRace race = GetMyRace();
        _rank = 1; _total = 1;
        if (race == null) return;
        float mine = Progress(race, ball.transform.position);
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);
        foreach (VRCPlayerApi p in players)
        {
            if (p == null || !p.IsValid() || p.isLocal || !race.IsEntrant(p.playerId)) continue;
            FuwaGhost g = GhostOf(p);
            if (g == null) continue;
            _total++;
            if (Progress(race, g.transform.position) > mine) _rank++;
        }
    }

    // 自分より前にいる中で一番近い人（all=falseの時）
    private FuwaGhost FindAhead(bool all)
    {
        FuwaRace race = GetMyRace();
        if (race == null) return null;
        float mine = Progress(race, ball.transform.position);
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);
        FuwaGhost best = null;
        float bestP = float.MaxValue;
        foreach (VRCPlayerApi p in players)
        {
            if (p == null || !p.IsValid() || p.isLocal || !race.IsEntrant(p.playerId)) continue;
            FuwaGhost g = GhostOf(p);
            if (g == null) continue;
            float pr = Progress(race, g.transform.position);
            if (pr > mine && pr < bestP) { bestP = pr; best = g; }
        }
        return best;
    }

    private int SendToAllAhead()
    {
        FuwaRace race = GetMyRace();
        if (race == null) return 0;
        float mine = Progress(race, ball.transform.position);
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);
        int n = 0;
        foreach (VRCPlayerApi p in players)
        {
            if (p == null || !p.IsValid() || p.isLocal || !race.IsEntrant(p.playerId)) continue;
            FuwaGhost g = GhostOf(p);
            if (g == null) continue;
            if (Progress(race, g.transform.position) <= mine) continue;
            g.SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(FuwaGhost.HitFog));
            n++;
        }
        return n;
    }

    private FuwaGhost GhostOf(VRCPlayerApi p)
    {
        GameObject[] objs = Networking.GetPlayerObjects(p);
        if (objs == null) return null;
        foreach (GameObject o in objs)
        {
            if (o == null) continue;
            FuwaGhost g = o.GetComponent<FuwaGhost>();
            if (g != null) return g;
        }
        return null;
    }

    private FuwaGhost GetMyGhost()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        return local != null ? GhostOf(local) : null;
    }

    private string OwnerName(FuwaGhost g)
    {
        VRCPlayerApi o = Networking.GetOwner(g.gameObject);
        return o != null ? o.displayName : "???";
    }

    private string NameJa(int i)
    {
        if (i == Heavy) return "おもりキノコ";
        if (i == Tailwind) return "おいかぜ";
        if (i == Thread) return "クモの糸";
        if (i == Fog) return "きりのたね";
        return "シャボン玉";
    }

    private string NameEn(int i)
    {
        if (i == Heavy) return "Heavy spore";
        if (i == Tailwind) return "Tailwind";
        if (i == Thread) return "Web";
        if (i == Fog) return "Fog seed";
        return "Bubble";
    }
}
