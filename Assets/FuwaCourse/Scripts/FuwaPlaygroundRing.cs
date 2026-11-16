using UdonSharp;
using UnityEngine;

// みんなでひろばのゴール風リング（2026-10-12 てじゃ「ゴール風リング、通ると光って花火が上がる」）。
// ふわふわ（FuwaSharedBall）がリングの穴（薄いトリガー）をくぐると、リングが光って花火。それぞれの画面で（玉は全員の画面で同じように動くので、ほぼ同時に上がる）。
// 扇風機と同じく持って動かせて、手を離すとその場に止まる（VRCObjectSync。同期する変数はないので None）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPlaygroundRing : UdonSharpBehaviour
{
    public Renderer ring;
    public ParticleSystem[] fireworks;
    public AudioSource sfx;
    [Tooltip("光らせる強さ（リングのマテリアルの _Glow。コースのゴールリング FuwaCourse/GoalRing と同じ）")]
    public float glowMax = 1f;
    public float glowSeconds = 1.2f;
    public float cooldown = 1.5f;

    private float _last = -10f;
    private Material _mat;
    private float _baseGlow;

    // 2026-11-16 てじゃ「ゴールリングがもてなくなってる」：床に置いた形のまま持てるように。離したら真下の床に平ら（穴が上）に置く
    public LayerMask groundMask = 1 | (1 << 11);
    public float groundOffset = 0.06f;

    public override void OnDrop()
    {
        Vector3 d = transform.up; d.y = 0f;
        if (d.sqrMagnitude < 0.001f) { d = transform.forward; d.y = 0f; }
        if (d.sqrMagnitude < 0.001f) d = Vector3.forward;
        Vector3 p = transform.position;
        RaycastHit h;
        if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out h, 6f, groundMask, QueryTriggerInteraction.Ignore)) p.y = h.point.y + groundOffset;
        transform.SetPositionAndRotation(p, Quaternion.LookRotation(Vector3.up, d.normalized));
    }

    private void Start()
    {
        if (ring != null) { _mat = ring.material; _baseGlow = _mat.GetFloat("_Glow"); }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || Time.time - _last < cooldown) return;
        FuwaSharedBall b = other.GetComponent<FuwaSharedBall>();
        if (b == null || !b.IsFlying()) return;
        _last = Time.time;
        if (fireworks != null) foreach (ParticleSystem p in fireworks) if (p != null) { p.Clear(); p.Play(); }
        if (sfx != null && sfx.clip != null) sfx.PlayOneShot(sfx.clip);
    }

    private void Update()
    {
        if (_mat == null) return;
        float t = (Time.time - _last) / glowSeconds;
        if (t > 1.2f) return;
        float k = t < 0f || t > 1f ? 0f : Mathf.Sin(t * Mathf.PI) * (0.6f + 0.4f * Mathf.Cos(t * 30f));
        _mat.SetFloat("_Glow", Mathf.Lerp(_baseGlow, glowMax, k));
    }
}
