using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2–4 はぐるまのとう：ルーバー（羽板）の風の道（2026-11-06 お試し）
// モーターの小歯車 → クランクの歯車 → クランクの棒（ピストンと同じ動き）→ ラック → 羽板ごとの小歯車 → 羽板が回る
// ・羽板が開いている（風と平行）ほど風が強い。閉じると羽板が壁になって、風も止まる
// ・みんな同じ見え方になるようにサーバー時刻で（風は自分のふわふわにだけ効く）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaLouver : UdonSharpBehaviour
{
    public Transform[] slats;        // 羽板（ローカル Y 軸で回る。0°で閉じる）
    public Transform rack;           // ラック（ローカル X に動く）
    public Transform crankWheel;     // クランクの歯車（ローカル Y で回る）
    public Transform motorPinion;    // モーターの小歯車
    public Transform crankPin;       // クランクのピン（crankWheel の子）
    public Transform rackEnd;        // 棒の、ラック側のピン（rack の子）
    public Transform rod;            // 棒（長さ方向はローカル Z）
    public FuwaWindArea wind;
    public float maxStrength = 6f;
    [Tooltip("クランクが1回転する秒数（＝1回開いて閉じる）")]
    public float period = 9f;
    public float crankR = 0.165f, rodL = 0.6f, pinionR = 0.2f;
    public Vector3 rackClosedLocal;
    [Tooltip("歯車の角度（2D 反時計回り）= ratio × φ + phase（度）。クランクのピンは φ が増えると時計回り＝ratio -1")]
    public float crankRatio = -1f, motorRatio = 2f;
    public float crankPhase = 0f, motorPhase = 0f;

    private void Update()
    {
        int per = Mathf.Max(1, Mathf.RoundToInt(period * 1000f));
        int ms = Networking.GetServerTimeInMilliseconds() % per;
        if (ms < 0) ms += per;
        Apply(2f * Mathf.PI * ms / per);
    }

    public void Apply(float phi)
    {
        // クランク：ピストンの位置 x = c cosφ + √(l² − c² sin²φ)。閉じている時（φ=0）がいちばん遠い
        float sn = Mathf.Sin(phi);
        float x = crankR * Mathf.Cos(phi) + Mathf.Sqrt(Mathf.Max(0f, rodL * rodL - crankR * crankR * sn * sn));
        float s = (crankR + rodL) - x;            // ラックの動いた量（0〜2c）
        float th = s / pinionR;                    // 羽板の回る角度（ラジアン、0〜90°）
        if (crankWheel != null) crankWheel.localRotation = Quaternion.Euler(0f, -(phi * Mathf.Rad2Deg * crankRatio + crankPhase), 0f);
        if (motorPinion != null) motorPinion.localRotation = Quaternion.Euler(0f, -(phi * Mathf.Rad2Deg * motorRatio + motorPhase), 0f);
        if (rack != null) rack.localPosition = rackClosedLocal + Vector3.right * s;
        float deg = th * Mathf.Rad2Deg;
        if (slats != null) for (int i = 0; i < slats.Length; i++) if (slats[i] != null) slats[i].localRotation = Quaternion.Euler(0f, -deg, 0f);
        float open = Mathf.Sin(Mathf.Clamp(th, 0f, Mathf.PI / 2f));
        if (wind != null) wind.strength = maxStrength * open;
        if (rod != null && crankPin != null && rackEnd != null)
        {
            Vector3 a = crankPin.position, b = rackEnd.position, d = b - a;
            rod.position = (a + b) * 0.5f;
            if (d.sqrMagnitude > 1e-6f) rod.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            Vector3 sc = rod.localScale; sc.z = d.magnitude; rod.localScale = sc;
        }
    }
}
