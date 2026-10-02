using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 触れたらアウトになる障害物。トリガーでも普通のコライダーでもOK。
// sticky（クモの巣）なら、ふわふわが少しの間ピタッとくっついてから破裂する。
// holeCenter を入れると、そこを中心にした丸い穴（輪っか）の中だけは通り抜けられる（巨大クモの巣用）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaHazard : UdonSharpBehaviour
{
    [Tooltip("クモの巣：くっついてから破裂する")]
    public bool sticky = false;
    [Tooltip("くっついている秒数")]
    public float stickTime = 0.6f;

    [Header("通り抜けられる穴（なくてもOK）")]
    [Tooltip("穴の中心（この物体のZ軸＝クモの巣の面に垂直な向き）")]
    public Transform holeCenter;
    [Tooltip("穴の半径(m)。ふわふわの中心がこれより内側なら通り抜けられる")]
    public float holeRadius = 0.8f;

    [Header("プレイヤーが通り抜ける時（巨大クモの巣用）")]
    [Tooltip("中にいる間の移動速度の倍率（1なら何もしない）")]
    [Range(0.05f, 1f)] public float playerSlow = 1f;
    [Tooltip("抜けてからも遅いままの秒数（糸がからまってる感じ）")]
    public float playerSlowLinger = 0.6f;

    private bool _slowed;
    private float _walk, _run, _strafe;
    private int _exitCount;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (playerSlow >= 0.999f || player == null || !player.isLocal) return;
        _exitCount++;   // 抜ける途中でまた入った時、前の「元に戻す」予約を無効にする
        if (_slowed) return;
        _walk = player.GetWalkSpeed(); _run = player.GetRunSpeed(); _strafe = player.GetStrafeSpeed();
        player.SetWalkSpeed(_walk * playerSlow);
        player.SetRunSpeed(_run * playerSlow);
        player.SetStrafeSpeed(_strafe * playerSlow);
        _slowed = true;
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (!_slowed || player == null || !player.isLocal) return;
        _exitCount++;
        int mine = _exitCount;
        _pendingExit = mine;
        SendCustomEventDelayedSeconds(nameof(RestoreSpeed), playerSlowLinger);
    }

    private int _pendingExit;

    public void RestoreSpeed()
    {
        // 予約のあとにまた入っていたら戻さない
        if (!_slowed || _pendingExit != _exitCount) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p != null)
        {
            p.SetWalkSpeed(_walk);
            p.SetRunSpeed(_run);
            p.SetStrafeSpeed(_strafe);
        }
        _slowed = false;
    }

    private void OnDisable()
    {
        // モード切り替えなどで消えた時に、遅いまま残らないように
        if (!_slowed) return;
        _pendingExit = _exitCount;
        RestoreSpeed();
    }

    private void OnTriggerEnter(Collider other)
    {
        CheckBall(other);
    }

    // 穴があると、穴の中から入って巣の中で外へずれた時も判定したいので、中にいる間ずっと見る
    private void OnTriggerStay(Collider other)
    {
        if (holeCenter == null) return;
        CheckBall(other);
    }

    private void CheckBall(Collider other)
    {
        if (other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;
        if (holeCenter != null)
        {
            // 巣の面の上での、穴の中心からの距離（奥行きは無視）
            Vector3 d = holeCenter.InverseTransformPoint(ball.transform.position);
            d.z = 0f;
            if (d.magnitude < holeRadius) return;
        }
        HitBall(ball);
    }

    // ふわふわが触れた時（普通のコライダーの場合はFuwaBall側から呼ばれる）
    public void HitBall(FuwaBall ball)
    {
        if (ball == null) return;
        if (sticky)
        {
            // 破裂で膨らむ時は、巣にくっついている側を動かさない（巣の面＝この物体のZ軸に垂直）
            Vector3 n = transform.forward;
            float side = Vector3.Dot(ball.transform.position - transform.position, n);
            ball.SetInflateAnchor(side >= 0f ? -n : n);
            ball.Stick(stickTime);
        }
        else ball.Fail();
    }
}
