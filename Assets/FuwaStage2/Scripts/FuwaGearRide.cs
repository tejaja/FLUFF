using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2–4 序盤：乗って渡る、ずっとゆっくり回っている歯車（2026-11-06 お試し。てじゃ「序盤は歯車乗るのがベタでいい」「かなりゆっくり」）
// ・元の歯車の角度 a = speed × 時間。i 番目の歯車の角度 = ratio[i] × a + phase[i]（2D 反時計回り、度。Unity の Y 回転はマイナス）
//   ratio と phase はビルダーが Mesh2 で出す（かみ合う所は歯の表面が同じ向き・同じ速さ）
// ・自分の画面だけ（同期なし）。時間は自分が入ってからの時間
// ・乗っている自分だけ歯車と一緒に運ぶ（ふわふわは運ばない）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGearRide : UdonSharpBehaviour
{
    public Transform[] gears;
    public float[] ratio, phase;
    [Tooltip("元の歯車の回る速さ（2D 反時計回り、度/秒）")]
    public float speed = 2f;
    public float rideRay = 0.6f;

    private float _a;
    private Quaternion[] _last;
    private bool _init;

    private void Start() { Apply(); }

    private void Update()
    {
        _a = Mathf.Repeat(_a + speed * Time.deltaTime, 360000f);
        Apply();
        Carry();
    }

    private void Apply()
    {
        if (gears == null || ratio == null || phase == null) return;
        for (int i = 0; i < gears.Length; i++)
            if (gears[i] != null) gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(ratio[i] * _a + phase[i], 360f), 0f);
    }

    private void Carry()
    {
        if (gears == null) return;
        int n = gears.Length;
        if (!_init) { _last = new Quaternion[n]; for (int i = 0; i < n; i++) if (gears[i] != null) _last[i] = gears[i].rotation; _init = true; return; }
        VRCPlayerApi p = Networking.LocalPlayer;
        int on = -1;
        Vector3 pos = Vector3.zero;
        if (p != null)
        {
            pos = p.GetPosition();
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out hit, 0.3f + rideRay, ~((1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore) && hit.collider != null)
                for (int i = 0; i < n; i++) if (gears[i] != null && hit.collider.transform.IsChildOf(gears[i])) { on = i; break; }
        }
        if (on >= 0)
        {
            Transform g = gears[on];
            Vector3 c = g.position;
            Vector3 moved = c + g.rotation * (Quaternion.Inverse(_last[on]) * (pos - c));
            Vector3 delta = moved - pos; delta.y = 0f;
            if (delta.sqrMagnitude > 1e-10f && delta.sqrMagnitude < 1f)
            {
                VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
                p.TeleportTo(o.position + delta, o.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
            }
        }
        for (int i = 0; i < n; i++) if (gears[i] != null) _last[i] = gears[i].rotation;
    }
}
