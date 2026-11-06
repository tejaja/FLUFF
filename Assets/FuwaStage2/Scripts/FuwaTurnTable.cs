using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2–4：撃って回す回転床（2026-11-06 お試し。てじゃ「回転床の先に2箇所くらい橋が伸びてて、向きを合わせるギミック」）
// 2026-11-06 作り直し（てじゃ「乗ってる島が回るとフワフワの維持ムズい」→ 案B「回すのは動かない地面から」）：
//  入口の横の羽根車（の軸の小歯車）→ 床下の歯車の列 → 回転床のふちの大きい歯車。床に乗らずに向きを合わせて、合ったら渡る
// ・羽根車の角度 u（2D 反時計回り、度）を元に、歯車の角度 = ratio × u + phase。回転床も同じ（tableRatio, tablePhase）
// ・撃った所と向きで回る向きが決まる（羽根車の軸のどっち側を撃つか）。止まりかけたら回転床が detentDeg ごとにカチッと止まる
// ・自分の画面だけ（ソロ攻略用、同期なし）。乗っている自分は回転床と一緒に運ぶ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaTurnTable : UdonSharpBehaviour
{
    public Transform table;            // 回転床（ローカル Y で回る）
    public float tableRatio = -0.27f, tablePhase = 0f;
    public Transform[] gears;          // 羽根車の軸の小歯車・床下の歯車（ローカル Y で回る）
    public float[] gearRatio, gearPhase;
    public Transform handle;           // 撃つ羽根車（当たりの中心、小歯車と同じ軸）
    [Tooltip("羽根車に風がかすれば当たり（半径 m）")]
    public float hitRadius = 0.9f;
    [Tooltip("フルパワーで真横を撃った時に足す、羽根車の回る速さ（度/秒）")]
    public float spinPerShot = 260f;
    public float maxSpin = 450f;
    public float drag = 1.6f;
    [Tooltip("カチッと止まる間隔（回転床の度）")]
    public float detentDeg = 15f;
    [Tooltip("回転床がこれより遅くなったら近い目盛りへ寄せる（度/秒）")]
    public float detentSpeed = 8f;
    public float rideRay = 0.6f;

    private float _u, _w;     // 羽根車の角度・速さ（2D 反時計回り、度）
    private Quaternion _lastRot;
    private bool _init;

    [Tooltip("最初の羽根車の角度（回転床が 0° になる所）")]
    public float startU = 0f;

    private void Start() { _u = startU; Apply(); }

    public void Shot(Vector3 origin, Vector3 aim, float power)
    {
        if (handle == null) return;
        Vector3 o = origin - handle.position; o.y = 0f;
        Vector3 a = aim; a.y = 0f;
        if (a.sqrMagnitude < 1e-4f) return;
        a.Normalize();
        Vector3 c = o - a * Vector3.Dot(o, a);            // 風の線が軸にいちばん近づく所
        float lever = Mathf.Clamp01(c.magnitude / Mathf.Max(0.01f, hitRadius));
        float cy = Vector3.Cross(c.normalized, a).y;        // Unity の Y 回転の向き（+ = 上から見て時計回り）。2D ではマイナス
        _w = Mathf.Clamp(_w + (-cy) * spinPerShot * power * lever, -maxSpin, maxSpin);
    }

    private void Update()
    {
        float dt = Time.deltaTime; if (dt <= 0f) return;
        _w *= Mathf.Exp(-drag * dt);
        float tw = Mathf.Abs(_w * tableRatio);
        if (tw < detentSpeed && detentDeg > 0f && tableRatio != 0f)
        {
            float th = tableRatio * _u + tablePhase;
            float uT = (Mathf.Round(th / detentDeg) * detentDeg - tablePhase) / tableRatio;
            float rate = Mathf.Max(Mathf.Abs(_w), 6f / Mathf.Abs(tableRatio));
            _u = Mathf.MoveTowards(_u, uT, rate * dt);
            if (Mathf.Abs(_u - uT) < 0.01f) { _u = uT; _w = 0f; }
        }
        else _u += _w * dt;
        Apply();
        Carry();
    }

    private void Apply()
    {
        if (table != null) table.localRotation = Quaternion.Euler(0f, -Mathf.Repeat(tableRatio * _u + tablePhase, 360f), 0f);
        if (gears == null || gearRatio == null || gearPhase == null) return;
        for (int i = 0; i < gears.Length; i++)
            if (gears[i] != null) gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(gearRatio[i] * _u + gearPhase[i], 360f), 0f);
    }

    // 回転床に乗っている自分を一緒に運ぶ（横だけ）
    private void Carry()
    {
        if (table == null) return;
        Quaternion now = table.rotation;
        if (!_init) { _lastRot = now; _init = true; return; }
        Quaternion prev = _lastRot; _lastRot = now;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        Vector3 pos = p.GetPosition();
        RaycastHit hit;
        if (!Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out hit, 0.3f + rideRay, ~((1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore)) return;
        if (hit.collider == null || !hit.collider.transform.IsChildOf(table)) return;
        Vector3 c = table.position;
        Vector3 moved = c + now * (Quaternion.Inverse(prev) * (pos - c));
        Vector3 delta = moved - pos; delta.y = 0f;
        if (delta.sqrMagnitude < 1e-10f || delta.sqrMagnitude > 1f) return;
        VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
        p.TeleportTo(o.position + delta, o.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
    }
}
