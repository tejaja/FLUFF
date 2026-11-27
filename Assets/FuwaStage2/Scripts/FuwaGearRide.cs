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
    [Tooltip("運ぶ時のめりこみ対策（2–4 の FuwaGearGuard）")]
    public FuwaGearGuard guard;
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

    // 2026-11-27 かみ合いずれ対策（FuwaClockwork と同じ）：歯車ごとの速さを「分数 × baseRate」にして、共通時計 B から計算する（wrap でくり返さない）
    [Tooltip("共通の元の速さ（度/秒）。FuwaClockwork と同じ値に（2–4 は 10/π）")]
    public float baseRate = 3.18309886f;
    private int[] _kn, _kd;
    private double _B;

    private void Start() { _kn = ExactNum(gears, ratio, speed, baseRate); _kd = ExactDen(gears, ratio, speed, baseRate); Apply(); }

    // x を分数 n/d（d ≦ 100000）にする。うまくいかなければ d = 0
    private static int RatN(double x, bool wantDen)
    {
        double h0 = 0, h1 = 1, k0 = 1, k1 = 0, v = x;
        for (int it = 0; it < 30; it++)
        {
            double fa = System.Math.Floor(v);
            double h2 = fa * h1 + h0, k2 = fa * k1 + k0;
            if (k2 > 100000) break;
            h0 = h1; h1 = h2; k0 = k1; k1 = k2;
            if (System.Math.Abs(h1 / k1 - x) <= System.Math.Abs(x) * 1e-6 + 1e-9) return wantDen ? (int)k1 : (int)h1;
            double fr = v - fa;
            if (fr < 1e-12) break;
            v = 1.0 / fr;
        }
        return 0;
    }
    public static int[] ExactNum(Transform[] g, float[] r, float spd, float rate)
    {
        int n = r != null ? r.Length : 0;
        int[] o = new int[n];
        for (int i = 0; i < n; i++) o[i] = rate > 0f ? RatN((double)r[i] * spd / rate, false) : 0;
        return o;
    }
    public static int[] ExactDen(Transform[] g, float[] r, float spd, float rate)
    {
        int n = r != null ? r.Length : 0;
        int[] o = new int[n];
        for (int i = 0; i < n; i++) o[i] = rate > 0f ? RatN((double)r[i] * spd / rate, true) : 0;
        return o;
    }
    // 歯車 i の角度（phase なし）。分数にできなかった時は fallback（前の計算）
    public static float ExactAngle(double B, int[] kn, int[] kd, int i, float fallback)
    {
        if (kn == null || kd == null || i >= kd.Length || kd[i] <= 0) return fallback;
        double m = 360.0 * kd[i];
        double x = B % m; if (x < 0) x += m;
        return (float)((kn[i] * x / kd[i]) % 360.0);
    }

    private void Update()
    {
        // 2026-11-07 時計塔（FuwaClockwork、Time.time）とかさ歯車でつながるので、同じ時間で回す
        // 2026-11-21 てじゃ「おっかけカメラで、ギミックの見た目を対象の人に合わせたい」→ 共通時計（サーバー時刻）で動かす：だれの画面でも同じタイミング
        double st = Networking.GetServerTimeInSeconds();
        _a = (float)(((speed * st) % wrap + wrap) % wrap);
        _B = st * baseRate;
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
            if (gears[i] != null) gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(ExactAngle(_B, _kn, _kd, i, ratio[i] * _a) + phase[i], 360f), 0f);
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
