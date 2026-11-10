using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 上に立てない物（2026-11-10 2–3 の氷の岩・結晶。てじゃ「ジャンプで簡単に乗れちゃう」「上にのっても落ちるような判定にしよう」）。
// 自分が slippery のどれかの上に立ったら、物のまん中から外へ押し出して落とす（見た目はそのまま）。自分のローカルだけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSlipOff : UdonSharpBehaviour
{
    [Tooltip("上に立てない当たり")]
    public Collider[] slippery;
    public LayerMask mask = 1 | (1 << 11);
    [Tooltip("外へ押す速さ(m/s)・少し上へ")]
    public float pushSpeed = 3.5f, pushUp = 1.2f;
    [Tooltip("押したあと、次に押すまでの秒（落ちる間に何度も押さない）")]
    public float cooldown = 0.35f;

    private float _next;

    private void Update()
    {
        if (Time.time < _next || slippery == null) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || !p.IsPlayerGrounded()) return;
        Vector3 pos = p.GetPosition();
        RaycastHit h;
        if (!Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out h, 0.7f, mask, QueryTriggerInteraction.Ignore)) return;
        Collider c = h.collider; if (c == null) return;
        bool hit = false;
        for (int i = 0; i < slippery.Length; i++) if (slippery[i] == c) { hit = true; break; }
        if (!hit) return;
        Vector3 d = pos - c.bounds.center; d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) { Vector3 f = p.GetRotation() * Vector3.forward; d = new Vector3(f.x, 0f, f.z); }
        if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
        p.SetVelocity(d.normalized * pushSpeed + Vector3.up * pushUp);
        _next = Time.time + cooldown;
    }
}
