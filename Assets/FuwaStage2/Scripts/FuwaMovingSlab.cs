using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2026-11-14 2–5 上の段（てじゃ「上下左右移動する石板の道はありだなぁ」）：動く石板
//  ・a と b（親の中の位置）の間を period 秒で 1 往復、はしで hold 秒ずつ止まる。全員同じ動き（サーバー時刻、FuwaMovingIce と同じ）
//  ・上に乗っている自分を、石板が動いたぶんだけ一緒に運ぶ（毎フレームの小さい TeleportTo）。2–3 の氷とちがって上下も運ぶ（上がる床にキャラが押し上げられないので）
//  同期なし（時刻で決まるので全員そろう）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaMovingSlab : UdonSharpBehaviour
{
    public Vector3 a, b;
    public float period = 8f, phase = 0f;
    [Tooltip("はし（a と b）で止まっている秒（それぞれ）。period にふくむ")]
    public float hold = 1.5f;
    [Tooltip("乗っているかを見る、足の下のレイの長さ")]
    public float rideRay = 0.6f;

    private Vector3 _last;
    private bool _init;

    private float T()
    {
        int per = Mathf.Max(1, Mathf.RoundToInt(period * 1000f));
        int ms = (Networking.GetServerTimeInMilliseconds() + Mathf.RoundToInt(phase * 1000f)) % per;
        if (ms < 0) ms += per;
        return ms / (float)per;
    }

    private void Update()
    {
        float tt = T(); float h = period > 0.01f ? Mathf.Clamp(hold / period, 0f, 0.45f) : 0f;
        float u;
        if (tt < 0.5f) { float x = Mathf.Clamp01((tt - h * 0.5f) / (0.5f - h)); u = 0.5f - 0.5f * Mathf.Cos(x * Mathf.PI); }
        else { float x = Mathf.Clamp01((tt - 0.5f - h * 0.5f) / (0.5f - h)); u = 0.5f + 0.5f * Mathf.Cos(x * Mathf.PI); }
        transform.localPosition = Vector3.Lerp(a, b, u);
        Vector3 now = transform.position;
        if (!_init) { _last = now; _init = true; return; }
        Vector3 delta = now - _last; _last = now;
        if (delta.sqrMagnitude < 1e-8f || delta.sqrMagnitude > 1f) return;

        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        if (p.GetGravityStrength() < 0.01f) return;   // 飛んでいる間（Hキーの飛行・傘）は運ばない
        Vector3 pos = p.GetPosition();
        RaycastHit hit;
        // 上がる時は足が床にめりこんでいるかもしれないので、少し上からさがす
        if (!Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out hit, 0.5f + rideRay, ~((1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore)) return;
        if (hit.collider == null || !hit.collider.transform.IsChildOf(transform)) return;
        // 下がる時は、ジャンプ中（床から離れている）なら運ばない
        if (delta.y < 0f && !p.IsPlayerGrounded()) delta.y = 0f;
        VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
        p.TeleportTo(o.position + delta, o.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
    }
}
