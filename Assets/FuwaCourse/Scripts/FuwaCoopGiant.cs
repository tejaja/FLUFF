using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;

// 「みんなで」協力：巨大ふわふわをみんなで運ぶ（2026-10-12 試作。てじゃ「協力プレイにしたほうがよさそう」「巨大はおもろそう」）
// ・ふわふわ本体は FuwaSharedBall（みんなの風が足し算される・持ち主の画面で動かして同期）
// ・重さは島にいる人数で自動：1人ぶんの風の効きを shotScale で決める。fullPlayers 人までは1人ぶん同じ（人数が増えるほど速く進む）、
//   それより多いと1人ぶんを軽くして、全体の強さが fullPlayers 人ぶんで頭打ちになるように（大人数で簡単になりすぎない）
// ・ゴール：ふわふわが goal の円の上（半径 goalRadius、床から goalHeight まで）に入ったら、持ち主の画面で判定してみんなに「ゴール！」
// 人数は自分の画面だけで数える。ゴールの合図だけネットワークイベント（同期なし＝None だとイベントが届かないので Manual。同期する変数はない）
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaCoopGiant : UdonSharpBehaviour
{
    public FuwaSharedBall giant;
    [Tooltip("島の真ん中（人数を数える範囲の中心）")]
    public Transform arenaCenter;
    public float arenaRadius = 16f;
    [Tooltip("1人ぶんの風の効き（1人の時）。小さいほど重い")]
    public float baseShotScale = 0.3f;
    [Tooltip("この人数までは1人ぶんの効きは同じ。それより多いと1人ぶんを軽くする")]
    public int fullPlayers = 4;

    [Header("ゴール")]
    public Transform goal;
    public float goalRadius = 2.2f;
    public float goalHeight = 3f;
    public ParticleSystem goalFx;
    public AudioSource goalSound;
    [Tooltip("人数・ゴール回数を出す看板（なくてもOK）")]
    public TextMeshPro board;

    private float _next;
    private int _count;
    private int _goals;
    private float _goalCooldown;

    private void Update()
    {
        if (giant == null) return;
        if (Time.time >= _next)
        {
            _next = Time.time + 0.5f;
            _count = CountPlayers();
            int n = Mathf.Max(1, _count);
            giant.shotScale = baseShotScale * (n <= fullPlayers ? 1f : (float)fullPlayers / n);
            Render();
        }
        // ゴール判定は持ち主の画面だけ
        if (goal != null && Time.time >= _goalCooldown && Networking.IsOwner(giant.gameObject) && giant.IsFlying())
        {
            Vector3 d = giant.transform.position - goal.position;
            if (d.y > -0.5f && d.y < goalHeight && new Vector2(d.x, d.z).sqrMagnitude < goalRadius * goalRadius)
            {
                _goalCooldown = Time.time + 3f;
                giant.ForceHome();
                SendCustomNetworkEvent(VRC.Udon.Common.Interfaces.NetworkEventTarget.All, nameof(Goal));
            }
        }
    }

    [NetworkCallable]
    public void Goal()
    {
        _goals++;
        if (goalFx != null) { goalFx.Clear(); goalFx.Play(); }
        if (goalSound != null) goalSound.Play();
        Render();
    }

    private int CountPlayers()
    {
        if (arenaCenter == null) return 1;
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);
        int c = 0;
        Vector3 ac = arenaCenter.position;
        foreach (VRCPlayerApi p in players)
        {
            if (p == null || !p.IsValid()) continue;
            Vector3 d = p.GetPosition() - ac; d.y = 0f;
            if (d.sqrMagnitude < arenaRadius * arenaRadius) c++;
        }
        return c;
    }

    private void Render()
    {
        if (board == null) return;
        board.text = "<size=130%><color=#FF6B5C>みんなで運ぼう</color></size>\n" + Mathf.Max(1, _count) + "人で押してる"
            + "\n<size=80%>ゴール " + _goals + "回</size>";
    }
}
