using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

// みんなでひろばの手持ち扇風機（2026-10-12 てじゃ「エアガンとは別の手持ち扇風機ほしいね、空中固定化」「出しっぱなしにもできたら」）。
// ・持ってトリガーで風の ON/OFF。手を離すとその場の空中に止まる（落ちない）。ON のまま置けば吹き続ける
// ・風は全員の画面で同じように当てる：扇風機の位置は VRCObjectSync、ON/OFF はこの同期変数。それぞれの画面で、前の円すいの中にいる
//   ふわふわ（FuwaSharedBall）に AddWind する（全員で同じ風。撃つたびの通信はない）
// VRCObjectSync と同じオブジェクトなので Continuous（Manual は併用できない）
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class FuwaHandFan : UdonSharpBehaviour
{
    [Tooltip("風を当てるふわふわ（チルルームの玉）")]
    public FuwaPlaygroundSpawner spawner;
    [Tooltip("風の出る所（+Z が風の向き）")]
    public Transform nozzle;
    [Tooltip("回す羽")]
    public Transform blades;
    [Tooltip("風の粒（なくてもOK）")]
    public ParticleSystem streaks;
    public AudioSource hum;

    [Header("風")]
    [Tooltip("風の届く距離(m)")]
    public float range = 4.5f;
    [Tooltip("根元の半径(m)")]
    public float radius0 = 0.35f;
    [Tooltip("広がり（距離1mあたりの半径の増え方）")]
    public float spread = 0.25f;
    [Tooltip("根元の強さ（加速度 m/s²）")]
    public float strength = 7f;

    [Header("うちわ（トリガーでひとあおぎ）")]
    [Tooltip("オンならうちわ：トリガーのたびに gustSeconds だけ強い風（ON/OFF なし）")]
    public bool gustMode;
    public float gustSeconds = 0.35f;
    public float gustStrength = 16f;
    [Tooltip("あおぐ時に振る物（うちわの面）")]
    public Transform swing;

    [UdonSynced] private bool _on;
    [UdonSynced] private int _gusts;   // あおいだ回数（増えたら全員の画面でひとあおぎ）
    private int _seenGusts;
    private float _gustStart = -10f;
    private bool _shownOn;
    private float _spin;
    private VRCObjectSync _sync;

    private void Start()
    {
        _sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        Apply();
    }

    // 持っている人：トリガーで ON/OFF（うちわはひとあおぎ）
    public override void OnPickupUseDown()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        if (gustMode) { _gusts++; _seenGusts = _gusts; _gustStart = Time.time; RequestSerialization(); return; }
        _on = !_on;
        RequestSerialization();
        Apply();
    }

    // 手を離したらその場に止める（落ちないように、動かない物に）
    public override void OnDrop()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
    }

    // 台のボタンから：元の場所へ戻して止める
    public void ResetFan()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        VRCPickup p = (VRCPickup)GetComponent(typeof(VRCPickup));
        if (p != null) p.Drop();
        _on = false;
        RequestSerialization();
        if (_sync != null) _sync.Respawn();
        Apply();
    }

    public override void OnDeserialization()
    {
        if (_gusts != _seenGusts) { _seenGusts = _gusts; _gustStart = Time.time; }
        Apply();
    }

    private bool Blowing() { return gustMode ? Time.time - _gustStart < gustSeconds : _on; }

    private void Apply()
    {
        if (_shownOn == _on) return;
        _shownOn = _on;
        if (streaks != null) { if (_on) streaks.Play(); else streaks.Stop(); }
        if (hum != null) { if (_on) hum.Play(); else hum.Stop(); }
    }

    private void Update()
    {
        if (_shownOn != _on) Apply();
        // 羽：ON で回り始めて、OFF でゆっくり止まる
        _spin = Mathf.MoveTowards(_spin, _on ? 1440f : 0f, (_on ? 1800f : 900f) * Time.deltaTime);
        if (blades != null && _spin > 0.1f) blades.Rotate(0f, 0f, _spin * Time.deltaTime, Space.Self);
        // うちわ：あおいだ時に面をぶんっと振る
        if (swing != null)
        {
            float t = (Time.time - _gustStart) / gustSeconds;
            float a = t >= 0f && t < 1.6f ? Mathf.Sin(Mathf.Clamp01(t / 1.6f) * Mathf.PI) * 35f : 0f;
            swing.localRotation = Quaternion.Euler(a, 0f, 0f);
            if (streaks != null && gustMode && t >= 0f && t < 0.05f && !streaks.isEmitting) streaks.Emit(14);
        }
    }

    private void FixedUpdate()
    {
        if (!Blowing() || spawner == null || nozzle == null) return;
        float str = gustMode ? gustStrength : strength;
        FuwaSharedBall[] balls = spawner.balls;
        if (balls == null) return;
        Vector3 o = nozzle.position, f = nozzle.forward;
        for (int i = 0; i < balls.Length; i++)
        {
            FuwaSharedBall b = balls[i];
            if (b == null || !b.IsFlying()) continue;
            Vector3 rel = b.transform.position - o;
            float along = Vector3.Dot(rel, f);
            if (along <= 0f || along > range) continue;
            float r = radius0 + along * spread + b.transform.lossyScale.x * 0.5f;
            float perp = (rel - f * along).magnitude;
            if (perp > r) continue;
            float k = (1f - along / range) * (1f - 0.5f * perp / r);
            b.AddWind(f * str * k * b.shotScale);   // 重い玉（巨大）は効きにくく
        }
    }
}
