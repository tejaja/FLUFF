using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2–3 こおりのみち：シーソーのように左右へ傾く氷の板（2026-11-05 てじゃ「床の傾きを変動させるのいける？」→ 案A シーソー）。
// ・板の長い向き（ローカルの +Z）を軸に、±amplitude 度を period 秒で 1 往復（なめらかなサイン）。全員同じ角度になるようサーバー時刻で
//   （てじゃ「同期もカンペキじゃなくてもいいけど一応」「大体あってればいい」→ FuwaMovingIce と同じ整数ミリ秒の時計）
// ・人の滑り（FuwaIceSkate）は足元の面の傾きで下る向きに流れるので、傾けるだけで傾いた側へ滑る
// ・上に乗っている自分を、板が回ったぶんだけ一緒に運ぶ（横だけ。上下は床が押し上げる／重力で落ちる）
// 自分のローカルの動きだけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaTiltIce : UdonSharpBehaviour
{
    [Tooltip("いちばん傾いた時の角度（度）")]
    public float amplitude = 10f;
    public float period = 8f, phase = 0f;
    [Tooltip("乗っているかを見る、足の下のレイの長さ")]
    public float rideRay = 0.6f;
    [Tooltip("氷で滑る処理。乗っている間の床の速さを教える（VRChat の速さの読みをなおすため）")]
    public FuwaIceSkate skate;
    [Tooltip("傾いていない時の向き（作る時に入れる）")]
    public Quaternion baseRotation = Quaternion.identity;
    [Tooltip("true：傾くかわりに、たて軸でぐるぐる回る（period 秒で 1 回転。上から見て時計まわり）。2026-11-05 2–3 の回る橋")]
    public bool spin = false;

    private Quaternion _lastRot;
    private bool _init;

    private float T()
    {
        int per = Mathf.Max(1, Mathf.RoundToInt(period * 1000f));
        int ms = (Networking.GetServerTimeInMilliseconds() + Mathf.RoundToInt(phase * 1000f)) % per;
        if (ms < 0) ms += per;
        return ms / (float)per;
    }

    private void OnEnable() { _init = false; }

    private void Update()
    {
        if (spin) transform.localRotation = baseRotation * Quaternion.Euler(0f, 360f * T(), 0f);
        else
        {
            float ang = amplitude * Mathf.Sin(T() * Mathf.PI * 2f);
            transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, ang);
        }
        Quaternion now = transform.rotation;
        if (!_init) { _lastRot = now; _init = true; return; }
        Quaternion prev = _lastRot; _lastRot = now;

        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        Vector3 pos = p.GetPosition();
        RaycastHit hit;
        if (!Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out hit, 0.3f + rideRay, ~((1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore)) return;
        if (hit.collider == null || !hit.collider.transform.IsChildOf(transform)) return;
        // 前の向きで板の中の位置 → 今の向きで世界の位置。その差の横だけ運ぶ
        Vector3 c = transform.position;
        Vector3 moved = c + now * (Quaternion.Inverse(prev) * (pos - c));
        Vector3 delta = moved - pos; delta.y = 0f;
        if (delta.sqrMagnitude < 1e-10f || delta.sqrMagnitude > 1f) return;
        if (skate != null && Time.deltaTime > 0f) { skate.floorVel = delta / Time.deltaTime; skate.floorFrame = Time.frameCount; }
        VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
        p.TeleportTo(o.position + delta, o.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
    }
}
