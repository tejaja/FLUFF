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
    [Tooltip("元の角度をこの値でくり返す（全部の ratio × wrap が 360 の倍数になる値。精度のため小さめ）")]
    public float wrap = 36000f;

    [Header("動力：蒸気機関（クランク軸＝ウォーム軸、ローカル Z で回る）")]
    public Transform crank;
    public float crankRatio, crankPhase;
    public Transform engineMount;   // クランク軸の向き（ローカル Z）。ピストンはローカル +Y の線を動く
    public Transform crankPin, piston, rod;
    public float rodLen = 1.4f;
    public ParticleSystem puff;      // ピストンが上がりきるたびに煙
    public int puffCount = 6;
    private float _lastCrank;

    [Header("オルゴール：シリンダーの歯車（gears[musicGear]）が1周＝曲が1ループ")]
    public AudioSource music;
    public int musicGear = -1;
    public float musicLen = 31f;
    [Tooltip("音と歯車がこれよりずれたら合わせ直す（秒）")]
    public float musicResync = 0.08f;

    private float _a;
    private Quaternion[] _last;
    private bool _init;

    private void Start() { Apply(); }

    private void Update()
    {
        _a = Mathf.Repeat(_a + speed * Time.deltaTime, wrap);
        Apply();
        Carry();
        SyncMusic();
    }

    // 曲の今の位置＝シリンダーの角度（回る向きに進む）／360 × ループの長さ
    private void SyncMusic()
    {
        if (music == null || musicGear < 0 || ratio == null || musicGear >= ratio.Length || musicLen <= 0f || music.clip == null) return;
        float r = ratio[musicGear];
        float phi = r * _a + phase[musicGear];
        float want = Mathf.Repeat((r >= 0f ? phi : -phi) / 360f, 1f) * musicLen;
        if (want >= music.clip.length) want = 0f;
        if (!music.isPlaying) { music.time = want; music.Play(); return; }
        float d = Mathf.Abs(music.time - want);
        d = Mathf.Min(d, musicLen - d);
        if (d > musicResync) music.time = want;
    }

    private void Apply()
    {
        if (gears == null || ratio == null || phase == null) return;
        for (int i = 0; i < gears.Length; i++)
            if (gears[i] != null) gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(ratio[i] * _a + phase[i], 360f), 0f);
        if (crank == null) return;
        float ca = Mathf.Repeat(crankRatio * _a + crankPhase, 360f);
        crank.localRotation = Quaternion.Euler(0f, 0f, ca);
        if (engineMount != null && crankPin != null && piston != null)
        {
            Vector3 pl = engineMount.InverseTransformPoint(crankPin.position);
            float y = pl.y + Mathf.Sqrt(Mathf.Max(0f, rodLen * rodLen - pl.x * pl.x));
            piston.localPosition = new Vector3(0f, y, pl.z);
            if (rod != null)
            {
                Vector3 a = crankPin.position, b = piston.position;
                rod.position = (a + b) * 0.5f;
                rod.rotation = Quaternion.LookRotation((b - a).normalized, engineMount.forward);
            }
        }
        // 上がりきり（ピンがまうえ＝クランクの角度 0° を通った時）に煙
        if (puff != null)
        {
            float d = Mathf.DeltaAngle(_lastCrank, ca);
            float rel0 = Mathf.DeltaAngle(0f, _lastCrank), rel1 = rel0 + d;
            if ((rel0 < 0f && rel1 >= 0f) || (rel0 > 0f && rel1 <= 0f)) puff.Emit(puffCount);
            _lastCrank = ca;
        }
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
