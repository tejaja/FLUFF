using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 火炎放射の岩（FuwaPlaygroundFlameRock）の炎の当たり。このオブジェクトの +Z が炎の向き。判定はこの物体のトリガー
// 岩がオンの間：入った同期ふわふわを炎の向き＋上へ吹っ飛ばす（Nudge＝全員の画面で同じように。同じ玉は cooldown 秒に1回）、自分は押される
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPlaygroundFlameZone : UdonSharpBehaviour
{
    public FuwaPlaygroundFlameRock rock;
    public float knock = 7f, knockUp = 4f, cooldown = 0.8f;
    public float playerPush = 5f, playerUp = 2.5f, playerCooldown = 0.6f;

    private FuwaSharedBall[] _hit = new FuwaSharedBall[12];
    private float[] _hitAt = new float[12];
    private int _next;
    private float _playerAt = -10f;

    private void OnTriggerStay(Collider other)
    {
        if (other == null || rock == null || !rock.IsOn()) return;
        FuwaSharedBall b = other.GetComponent<FuwaSharedBall>();
        if (b == null || !b.IsFlying()) return;
        for (int i = 0; i < _hit.Length; i++) if (_hit[i] == b && Time.time - _hitAt[i] < cooldown) return;
        _hit[_next] = b; _hitAt[_next] = Time.time; _next = (_next + 1) % _hit.Length;
        Vector3 d = transform.forward; d.y = 0f; d = d.normalized;
        b.Nudge(d * knock + Vector3.up * knockUp);
    }

    public override void OnPlayerTriggerStay(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || rock == null || !rock.IsOn()) return;
        if (Time.time - _playerAt < playerCooldown) return;
        _playerAt = Time.time;
        Vector3 d = transform.forward; d.y = 0f; d = d.normalized;
        Vector3 v = player.GetVelocity();
        player.SetVelocity(new Vector3(d.x * playerPush, Mathf.Max(v.y, playerUp), d.z * playerPush));
    }
}
