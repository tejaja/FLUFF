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
    [Tooltip("当たりの半径（m）。風の芯がこの円の中を通った時だけ回る（羽根の先 0.8m）")]
    public float hitRadius = 0.8f;
    [Tooltip("当たりの高さ（羽根の真ん中から上下 m）")]
    public float hitHalfHeight = 0.3f;
    [Tooltip("遠くから撃った時だけ少し広げる角度（度）。0 で銃口からまっすぐの線だけ")]
    public float hitCone = 3f;
    [Tooltip("フルパワーで真横を撃った時に足す、羽根車の回る速さ（度/秒）")]
    public float spinPerShot = 260f;
    public float maxSpin = 450f;
    public float drag = 1.6f;
    [Tooltip("カチッと止まる間隔（回転床の度）")]
    public float detentDeg = 15f;
    [Tooltip("回転床がこれより遅くなったら近い目盛りへ寄せる（度/秒）")]
    public float detentSpeed = 8f;
    public float rideRay = 0.6f;

    // 2026-11-07 ラック＆ピニオンで橋がせり出すお試し（FuwaRackBridgeTest）。table を使わず、rack を羽根車の角度に合わせて動かす
    [Tooltip("羽根車の角度 u に合わせてまっすぐ動く物（ラックのついた橋）。null で使わない")]
    public Transform rack;
    public Vector3 rackBase, rackAxis = Vector3.right;
    [Tooltip("羽根車 1° あたりの移動（m）。位置 = rackBase + rackAxis × rackPerU × (u − startU)")]
    public float rackPerU = 0f;
    [Tooltip("羽根車の角度の範囲（橋の行き止まり）。uMin < uMax の時だけ")]
    public float uMin = 0f, uMax = 0f;
    // 2026-11-07 てじゃ「両端で伸びるようにして、徐々にもどるように」：2本目のラック（向こう岸の橋）と、撃たないと startU へ戻る
    public Transform rack2;
    public Vector3 rack2Base, rack2Axis = Vector3.right;
    public float rack2PerU = 0f;
    [Tooltip("撃ってから戻り始めるまで（秒）")]
    public float holdTime = 2.5f;
    [Tooltip("戻る速さ（羽根車の度/秒）。0 で戻らない")]
    public float returnSpeed = 0f;
    private float _lastShot = -100f;
    private Vector3 _lastRack2Pos;

    private float _u, _w;     // 羽根車の角度・速さ（2D 反時計回り、度）
    private Quaternion _lastRot;
    private Vector3 _lastRackPos;
    private bool _init;

    [Tooltip("最初の羽根車の角度（回転床が 0° になる所）")]
    public float startU = 0f;

    [Header("音（2026-11-10 てじゃ「しかけの音」）")]
    [Tooltip("clickDeg ごとにカチッ（回転床は床の角度、橋は羽根車の角度 u で数える）")]
    public AudioSource clickSound;
    public float clickDeg = 15f;
    [Tooltip("動いている間のループ（速さで音量）。loopFullSpeed（u の度/秒）で loopVolume")]
    public AudioSource loopSound;
    public float loopFullSpeed = 120f, loopVolume = 0.6f;
    [Tooltip("行き止まり（uMin、橋がつながった所）に着いた時")]
    public AudioSource endSound;
    private float _prevU; private int _lastClick; private float _loopV;

    private void Start() { _u = startU; _prevU = _u; Apply(); _lastClick = ClickIndex(); }

    private int ClickIndex()
    {
        float a = table != null && tableRatio != 0f ? tableRatio * _u : _u;
        return Mathf.FloorToInt(a / Mathf.Max(0.1f, clickDeg));
    }

    private void Sounds(float dt)
    {
        float du = _u - _prevU;
        if (clickSound != null) { int ci = ClickIndex(); if (ci != _lastClick) { _lastClick = ci; clickSound.pitch = 0.92f + Random.value * 0.16f; clickSound.Play(); } }
        if (loopSound != null)
        {
            float target = Mathf.Clamp01(Mathf.Abs(du) / dt / Mathf.Max(1f, loopFullSpeed)) * loopVolume;
            _loopV = Mathf.MoveTowards(_loopV, target, dt * 3f);
            loopSound.volume = _loopV;
            if (_loopV > 0.005f) { if (!loopSound.isPlaying) loopSound.Play(); }
            else if (loopSound.isPlaying) loopSound.Pause();
        }
        if (endSound != null && uMin < uMax && _u <= uMin + 1e-3f && _prevU > uMin + 1e-3f) endSound.Play();
        _prevU = _u;
    }

    // アウトで戻った時（FuwaBall.resetOnOut）：最初の向きへ。乗っている人を運ばないように、運ぶ用の前の向きも合わせる
    public void ResetGimmick()
    {
        _u = startU; _w = 0f; _lastShot = -100f;
        Apply(); _prevU = _u; _lastClick = ClickIndex();
        if (table != null) _lastRot = table.rotation;
        if (rack != null) _lastRackPos = rack.position;
        if (rack2 != null) _lastRack2Pos = rack2.position;
    }

    public void Shot(Vector3 origin, Vector3 aim, float power)
    {
        if (handle == null) return;
        _lastShot = Time.time;
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
        if (table != null && tw < detentSpeed && detentDeg > 0f && tableRatio != 0f)
        {
            float th = tableRatio * _u + tablePhase;
            float uT = (Mathf.Round(th / detentDeg) * detentDeg - tablePhase) / tableRatio;
            float rate = Mathf.Max(Mathf.Abs(_w), 6f / Mathf.Abs(tableRatio));
            _u = Mathf.MoveTowards(_u, uT, rate * dt);
            if (Mathf.Abs(_u - uT) < 0.01f) { _u = uT; _w = 0f; }
        }
        else _u += _w * dt;
        if (returnSpeed > 0f && Time.time - _lastShot > holdTime) _u = Mathf.MoveTowards(_u, startU, returnSpeed * dt);
        if (uMin < uMax)
        {
            // 行き止まり：止まる（はね返らない）
            if (_u < uMin) { _u = uMin; if (_w < 0f) _w = 0f; }
            if (_u > uMax) { _u = uMax; if (_w > 0f) _w = 0f; }
        }
        Apply();
        Carry();
        Sounds(dt);
    }

    private void Apply()
    {
        if (table != null) table.localRotation = Quaternion.Euler(0f, -Mathf.Repeat(tableRatio * _u + tablePhase, 360f), 0f);
        if (rack != null) rack.localPosition = rackBase + rackAxis * (rackPerU * (_u - startU));
        if (rack2 != null) rack2.localPosition = rack2Base + rack2Axis * (rack2PerU * (_u - startU));
        if (gears == null || gearRatio == null || gearPhase == null) return;
        for (int i = 0; i < gears.Length; i++)
            if (gears[i] != null) gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(gearRatio[i] * _u + gearPhase[i], 360f), 0f);
    }

    // 回転床・橋に乗っている自分を一緒に運ぶ（横だけ）
    private void Carry()
    {
        if (table == null && rack == null && rack2 == null) return;
        Quaternion now = table != null ? table.rotation : Quaternion.identity;
        Vector3 rackNow = rack != null ? rack.position : Vector3.zero;
        Vector3 rack2Now = rack2 != null ? rack2.position : Vector3.zero;
        if (!_init) { _lastRot = now; _lastRackPos = rackNow; _lastRack2Pos = rack2Now; _init = true; return; }
        Quaternion prev = _lastRot; _lastRot = now;
        Vector3 rackPrev = _lastRackPos; _lastRackPos = rackNow;
        Vector3 rack2Prev = _lastRack2Pos; _lastRack2Pos = rack2Now;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        Vector3 pos = p.GetPosition();
        RaycastHit hit;
        if (!Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out hit, 0.3f + rideRay, ~((1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore)) return;
        if (hit.collider == null) return;
        Vector3 delta;
        if (table != null && hit.collider.transform.IsChildOf(table))
        {
            Vector3 c = table.position;
            delta = c + now * (Quaternion.Inverse(prev) * (pos - c)) - pos;
        }
        else if (rack != null && hit.collider.transform.IsChildOf(rack)) delta = rackNow - rackPrev;
        else if (rack2 != null && hit.collider.transform.IsChildOf(rack2)) delta = rack2Now - rack2Prev;
        else return;
        delta.y = 0f;
        if (delta.sqrMagnitude < 1e-10f || delta.sqrMagnitude > 1f) return;
        VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
        p.TeleportTo(o.position + delta, o.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
    }
}
