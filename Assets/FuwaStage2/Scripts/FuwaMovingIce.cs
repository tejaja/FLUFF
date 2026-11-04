using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2–3 こおりのみち：ゆっくり行ったり来たりする氷の島（2026-11-04 てじゃ「動く氷をメインに」「かなりゆっくりの氷のほうがよさそう」）。
// ・a と b（親の中の位置）の間を、period 秒で 1 往復（はしでゆっくり止まって向きを変える）。全員同じ動きになるようサーバー時刻で（FuwaFireball と同じ整数ミリ秒）
// ・上に乗っている自分を、島が動いたぶんだけ一緒に運ぶ（毎フレームの小さい TeleportTo。FuwaIceSkate と同じ部屋ごとの平行移動）
// 自分のローカルの動きだけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaMovingIce : UdonSharpBehaviour
{
    public Vector3 a, b;
    public float period = 20f, phase = 0f;
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
        float u = 0.5f - 0.5f * Mathf.Cos(T() * Mathf.PI * 2f);
        Vector3 lp = Vector3.Lerp(a, b, u);
        transform.localPosition = lp;
        Vector3 now = transform.position;
        if (!_init) { _last = now; _init = true; return; }
        Vector3 delta = now - _last; _last = now;
        if (delta.sqrMagnitude < 1e-8f || delta.sqrMagnitude > 1f) return;   // 大きく飛んだ時（時刻の巻き戻りなど）は運ばない

        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        Vector3 pos = p.GetPosition();
        RaycastHit hit;
        if (!Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out hit, 0.3f + rideRay, ~((1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore)) return;
        if (hit.collider == null || !hit.collider.transform.IsChildOf(transform)) return;
        VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
        p.TeleportTo(o.position + new Vector3(delta.x, 0f, delta.z), o.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
    }
}
