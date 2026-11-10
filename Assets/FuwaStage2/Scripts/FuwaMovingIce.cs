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
    [Tooltip("はし（a と b）で止まっている秒（それぞれ）。period にふくむ（2026-11-10 てじゃ「繋がった状態で短時間止まるように」）")]
    public float hold = 0f;
    [Tooltip("乗っているかを見る、足の下のレイの長さ")]
    public float rideRay = 0.6f;
    [Tooltip("氷で滑る処理。乗っている間の床の速さを教える（VRChat の速さの読みをなおすため）")]
    public FuwaIceSkate skate;

    private Vector3 _last;
    private bool _init;
    // 調べる用のログ（2026-11-04 てじゃ「棒立ちで床の移動方向に加速」。ClientSim では再現しない）：乗っている間 0.5 秒ごとに、床と自分の動いた量・VRChat の速さを出す
    public bool debugLog = false;
    private Vector3 _dbgFloor, _dbgPlayer, _dbgPrevPos; private float _dbgT; private bool _dbgRiding; private int _dbgFrames;

    private float T()
    {
        int per = Mathf.Max(1, Mathf.RoundToInt(period * 1000f));
        int ms = (Networking.GetServerTimeInMilliseconds() + Mathf.RoundToInt(phase * 1000f)) % per;
        if (ms < 0) ms += per;
        return ms / (float)per;
    }

    private void Update()
    {
        // はしで hold 秒止まって、あいだはなめらかに動く（hold 0 なら前と同じ 0.5-0.5cos）
        float tt = T(); float h = period > 0.01f ? Mathf.Clamp(hold / period, 0f, 0.45f) : 0f;
        float u;
        if (tt < 0.5f) { float x = Mathf.Clamp01((tt - h * 0.5f) / (0.5f - h)); u = 0.5f - 0.5f * Mathf.Cos(x * Mathf.PI); }
        else { float x = Mathf.Clamp01((tt - 0.5f - h * 0.5f) / (0.5f - h)); u = 0.5f + 0.5f * Mathf.Cos(x * Mathf.PI); }
        Vector3 lp = Vector3.Lerp(a, b, u);
        transform.localPosition = lp;
        Vector3 now = transform.position;
        if (!_init) { _last = now; _init = true; return; }
        Vector3 delta = now - _last; _last = now;
        if (delta.sqrMagnitude < 1e-8f || delta.sqrMagnitude > 1f) return;   // 大きく飛んだ時（時刻の巻き戻りなど）は運ばない

        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        if (p.GetGravityStrength() < 0.01f) { _dbgRiding = false; return; }   // 2026-11-08 飛んでいる間（重力 0：Hキーの飛行・傘の使う中）は運ばない
        Vector3 pos = p.GetPosition();
        RaycastHit hit;
        if (!Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out hit, 0.3f + rideRay, ~((1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore)) { _dbgRiding = false; return; }
        if (hit.collider == null || !hit.collider.transform.IsChildOf(transform)) { _dbgRiding = false; return; }
        if (debugLog)
        {
            if (!_dbgRiding) { _dbgRiding = true; _dbgFloor = Vector3.zero; _dbgPlayer = Vector3.zero; _dbgPrevPos = pos; _dbgT = Time.time; _dbgFrames = 0; }
            _dbgFloor += delta; _dbgPlayer += pos - _dbgPrevPos; _dbgPrevPos = pos; _dbgFrames++;
            if (Time.time - _dbgT >= 0.5f)
            {
                Vector3 v = p.GetVelocity();
                Debug.Log("[FuwaMovingIce] " + name + " frames=" + _dbgFrames + " floor=" + _dbgFloor.ToString("F3") + " player=" + _dbgPlayer.ToString("F3") + " vel=" + v.ToString("F2") + " grounded=" + p.IsPlayerGrounded());
                _dbgFloor = Vector3.zero; _dbgPlayer = Vector3.zero; _dbgT = Time.time; _dbgFrames = 0;
            }
        }
        if (skate != null && Time.deltaTime > 0f) { skate.floorVel = delta / Time.deltaTime; skate.floorFrame = Time.frameCount; skate.shiftFrame = Time.frameCount; }
        VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
        p.TeleportTo(o.position + new Vector3(delta.x, 0f, delta.z), o.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
    }
}
