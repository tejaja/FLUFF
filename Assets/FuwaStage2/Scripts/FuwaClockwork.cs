using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2–4 はぐるまのとう：時計塔が動かす、道ごとの自動の歯車じかけ（2026-11-07 てじゃ「各道に、なんかしら自動の歯車の仕掛けを追加しよう」）
// ・塔から空中の軸がのびて、かさ歯車でたての軸 → 歯車 → クランク → 押し出し壁・回るアーム・シャッター
// ・元の角度 a = speed × 時間（Time.time。だれの画面でもいつも同じリズムになるわけではないが、自分の画面では一定。同期なし）
//   歯車 i の角度（local Y まわり、2D 反時計回り、度）= ratio[i] × a + phase[i]。Unity の Y 回転はマイナス（ほかの歯車と同じ）
// ・クランクとスライダー：クランクの歯車 sliderGear[k] の local (pinR, 0, 0) がピン。スライダー（ピンの所が原点）は
//   クランクの中心から axis の向きの線の上で、ピンから rodLen の所。棒（rod）はピンとスライダーの間（local Z が棒の向き、長さ 1 の箱を rodLen にのばしておく）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaClockwork : UdonSharpBehaviour
{
    public float speed = 10f;
    public float wrap = 36000f;
    public Transform[] gears;
    public float[] ratio, phase;

    public int[] sliderGear;
    public Transform[] sliders;
    public Vector3[] sliderAxis;    // クランクの歯車の親の中での向き
    public float[] pinR, pinY, rodLen;   // ピンはクランクの歯車の local (pinR, pinY, 0)、線の中心は local (0, pinY, 0)
    public Transform[] rods;
    [Tooltip("歯車の足場に乗っている自分を一緒に運ぶ（足もとを見る長さ）")]
    public float rideRay = 0.6f;
    [Tooltip("運ぶ時のめりこみ対策（2–4 の FuwaGearGuard）")]
    public FuwaGearGuard guard;
    private Quaternion[] _last;
    private bool _init;

    private void Update()
    {
        float a = Mathf.Repeat(speed * Time.time, wrap);
        if (gears != null && ratio != null && phase != null)
            for (int i = 0; i < gears.Length; i++)
                if (gears[i] != null) gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(ratio[i] * a + phase[i], 360f), 0f);
        Carry();
        if (sliders == null || sliderGear == null) return;
        for (int k = 0; k < sliders.Length; k++)
        {
            Transform s = sliders[k]; if (s == null) continue;
            Transform g = gears[sliderGear[k]]; if (g == null) continue;
            float py = pinY != null && k < pinY.Length ? pinY[k] : 0f;
            Vector3 c = g.TransformPoint(new Vector3(0f, py, 0f));
            Vector3 pin = g.TransformPoint(new Vector3(pinR[k], py, 0f));
            Vector3 ax = g.parent.TransformDirection(sliderAxis[k]).normalized;
            Vector3 d = pin - c;
            float along = Vector3.Dot(d, ax);
            Vector3 side = d - ax * along;
            float L = rodLen[k];
            float sq = L * L - side.sqrMagnitude;
            Vector3 sp = c + ax * (along + Mathf.Sqrt(Mathf.Max(0f, sq)));
            s.position = sp;
            if (rods != null && k < rods.Length && rods[k] != null)
            {
                rods[k].position = (pin + sp) * 0.5f;
                rods[k].rotation = Quaternion.LookRotation((sp - pin).normalized, g.up);
            }
        }
    }

    // 足場の歯車（当たりのある物）に乗っている自分を、その歯車の回りに合わせて運ぶ（FuwaGearRide と同じ）
    private void Carry()
    {
        if (gears == null) return;
        int n = gears.Length;
        if (!_init) { _last = new Quaternion[n]; for (int i = 0; i < n; i++) if (gears[i] != null) _last[i] = gears[i].rotation; _init = true; return; }
        VRCPlayerApi p = Networking.LocalPlayer;
        int on = -1; Vector3 pos = Vector3.zero;
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
                // 2026-11-08 運ぶ先が壁・ほかの物ならやめる（FuwaGearGuard。巻き込まれてめりこむ対策）
                if (guard != null) guard.CarryPlayer(delta, g);
                else
                {
                    VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
                p.TeleportTo(o.position + delta, o.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
                }
            }
        }
        for (int i = 0; i < n; i++) if (gears[i] != null) _last[i] = gears[i].rotation;
    }
}
