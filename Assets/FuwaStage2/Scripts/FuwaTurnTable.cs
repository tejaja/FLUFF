using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2–4：撃って回す回転床（2026-11-06 お試し。てじゃ「回転床の先に2箇所くらい橋が伸びてて、向きを合わせるギミック」）
// まん中に動かない太陽歯車、回転床に乗った遊星歯車（上に撃つ用の羽根車）。羽根車を撃つと遊星歯車が回って、太陽歯車の上を転がる → 回転床が回る
// ・回転床から見ると、太陽歯車が -θc 回り、遊星歯車は外歯車どうしで逆回り：φ = (zs/zp)·θc + phase（2D 反時計回り）
// ・撃った向きと当たった所で回る向きが決まる（羽根車の軸のどっち側を撃つか）。止まりかけたら detentDeg ごとにカチッと止まる
// ・自分の画面だけ（ソロ攻略用、同期なし）。乗っている自分を回転床と一緒に運ぶ（FuwaTiltIce と同じやり方）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaTurnTable : UdonSharpBehaviour
{
    public Transform table;      // 回転床（ワールドのたて軸で回る）
    public Transform planet;     // 遊星歯車（table の子、ローカル Y で回る）
    public Transform handle;     // 撃つ羽根車（planet と同じ軸。当たりの中心）
    public int sunTeeth = 24, planetTeeth = 12;
    public float planetPhase = 0f;   // 度
    [Tooltip("羽根車に風がかすれば当たり（半径 m）")]
    public float hitRadius = 0.9f;
    [Tooltip("フルパワーで真横を撃った時に足す、回転床の回る速さ（度/秒）")]
    public float spinPerShot = 70f;
    public float maxSpin = 120f;
    public float drag = 1.6f;
    [Tooltip("カチッと止まる間隔（回転床の度）")]
    public float detentDeg = 15f;
    public float detentSpeed = 8f;   // これより遅くなったら近い目盛りへ寄せる（度/秒）
    public float rideRay = 0.6f;
    public float startDeg = 0f;

    private float _th, _w;   // 回転床の角度・速さ（2D 反時計回り、度）
    private Quaternion _lastRot;
    private bool _init;

    private void Start() { _th = startDeg; Apply(); }

    public void Shot(Vector3 origin, Vector3 aim, float power)
    {
        if (handle == null) return;
        Vector3 o = origin - handle.position; o.y = 0f;
        Vector3 a = aim; a.y = 0f;
        if (a.sqrMagnitude < 1e-4f) return;
        a.Normalize();
        Vector3 c = o - a * Vector3.Dot(o, a);            // 風の線が軸にいちばん近づく所
        float lever = Mathf.Clamp01(c.magnitude / Mathf.Max(0.01f, hitRadius));
        float cy = Vector3.Cross(c.normalized, a).y;        // Unity の Y 回転の向き（+ = 上から見て時計回り）
        // 羽根車（遊星歯車）が Unity で cy の向きに回る → 2D では -cy。回転床は φ = (zs/zp)θc なので同じ向き
        _w = Mathf.Clamp(_w + (-cy) * spinPerShot * power * lever, -maxSpin, maxSpin);
    }

    private void Update()
    {
        float dt = Time.deltaTime; if (dt <= 0f) return;
        _w *= Mathf.Exp(-drag * dt);
        if (Mathf.Abs(_w) < detentSpeed && detentDeg > 0f)
        {
            float target = Mathf.Round(_th / detentDeg) * detentDeg;
            _th = Mathf.MoveTowards(_th, target, Mathf.Max(Mathf.Abs(_w), 6f) * dt);
            if (Mathf.Abs(_th - target) < 0.01f) { _th = target; _w = 0f; }
            else _th += _w * dt * 0.3f;
        }
        else _th += _w * dt;
        Apply();
        Carry();
    }

    private void Apply()
    {
        if (table != null) table.localRotation = Quaternion.Euler(0f, -_th, 0f);
        float phi = (float)sunTeeth / planetTeeth * _th + planetPhase;
        if (planet != null) planet.localRotation = Quaternion.Euler(0f, -Mathf.Repeat(phi, 360f), 0f);
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
