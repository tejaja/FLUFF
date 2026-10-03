using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// トゲ。人が触ると最後のチェックポイントへ戻され（playerSlow を使うと大幅減速だけ）、けだまが触るとアウト。
// モンスターも当たると倒れる（FuwaEnemy側で判定）

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSpike : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("人が触った時も反応するか（天井の鍾乳石などは false）")]
    public bool affectPlayers = true;
    [Tooltip("0より大きいと、人は戻されずに、中にいる間この倍率まで減速する（イバラ）")]
    [Range(0f, 1f)] public float playerSlow = 0f;
    [Tooltip("抜けてからも遅いままの秒数")]
    public float playerSlowLinger = 0.4f;

    private bool _slowed;
    private int _insideCount;
    private float _restoreAt;
    private float _walk, _run, _strafe;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (!affectPlayers) return;
        if (player == null || !player.isLocal) return;
        if (playerSlow > 0f)
        {
            _insideCount++;
            if (!_slowed)
            {
                _walk = player.GetWalkSpeed(); _run = player.GetRunSpeed(); _strafe = player.GetStrafeSpeed();
                player.SetWalkSpeed(_walk * playerSlow);
                player.SetRunSpeed(_run * playerSlow);
                player.SetStrafeSpeed(_strafe * playerSlow);
                _slowed = true;
            }
            // ジャンプの勢いもからめ取る
            Vector3 v = player.GetVelocity();
            player.SetVelocity(new Vector3(v.x * playerSlow, v.y > 0f ? v.y * playerSlow : v.y, v.z * playerSlow));
            return;
        }
        if (ball == null) return;
        // 「グサッ」→一瞬止まる→暗転→チェックポイントへ（FuwaBall側で処理）
        ball.BeginPlayerSpike("トゲ！", "Spiked!");
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (playerSlow <= 0f || player == null || !player.isLocal) return;
        _insideCount = Mathf.Max(0, _insideCount - 1);
        if (_insideCount == 0) _restoreAt = Time.time + playerSlowLinger;
    }

    // 戻すのは毎フレーム見張る（遅延イベントはまれに戻らなくなるので）
    private void Update()
    {
        if (!_slowed) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        if (_insideCount > 0)
        {
            // 中にいる間は空中でも横の速さを抑える
            if (!p.IsPlayerGrounded())
            {
                Vector3 v = p.GetVelocity();
                Vector3 h = new Vector3(v.x, 0f, v.z);
                float lim = Mathf.Max(_run, _walk) * playerSlow;
                if (h.magnitude > lim) { h = h.normalized * lim; p.SetVelocity(new Vector3(h.x, v.y, h.z)); }
            }
            return;
        }
        if (Time.time >= _restoreAt) RestoreSpeed();
    }

    private void RestoreSpeed()
    {
        if (!_slowed) return;
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
        _insideCount = 0;
        RestoreSpeed();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        FuwaBall b = other.GetComponent<FuwaBall>();
        if (b != null) b.FailBySpike();
    }
}
