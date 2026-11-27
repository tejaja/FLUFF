using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// あそびばの竜巻のたね・重力のたねのオン/オフ（2026-11-27 てじゃ「オンオフ式にしたい。デフォはオフ、持ってインタラクトでオン。
//  オフ時は球の周囲に球近い色のオーラ、オンでエフェクトが展開されるとオーラは消える。オーラもエフェクトも出し入れ時にフェード」）
// たね本体（VRCPickup・VRCObjectSync）につける。持ってトリガー（Use）でオン/オフ（みんなに同期）。
// 効果（竜巻・重い球）は大きさ 0→1 で広がり、粒の出る量も合わせる。当たりは半分以上広がってから入る。オーラは逆にうすくなって消える
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaSeedToggle : UdonSharpBehaviour
{
    [Tooltip("広げる・しまう物（大きさ 0→元 で広がる）")]
    public Transform[] effects;
    [Tooltip("効果の当たり（半分以上広がった時だけ入る）")]
    public Collider[] effectColliders;
    [Tooltip("効果の粒（出る量を広がりに合わせる）")]
    public ParticleSystem[] effectParticles;
    [Tooltip("オフの時のオーラ（Fuwa/SeedAura の _Alpha でフェード）")]
    public Renderer aura;
    public float auraAlpha = 1f;
    [Tooltip("広がる・しまう速さ（1/秒）")]
    public float speed = 1.6f;
    public AudioSource onSound, offSound;

    [UdonSynced] private bool _on;
    private float _k = -1f;
    private Vector3[] _base;
    private float[] _rate;
    private Material _auraMat;

    private void Start()
    {
        int n = effects != null ? effects.Length : 0;
        _base = new Vector3[n];
        for (int i = 0; i < n; i++) if (effects[i] != null) _base[i] = effects[i].localScale;
        int m = effectParticles != null ? effectParticles.Length : 0;
        _rate = new float[m];
        for (int i = 0; i < m; i++) if (effectParticles[i] != null) _rate[i] = effectParticles[i].emission.rateOverTimeMultiplier;
        if (aura != null) _auraMat = aura.material;
        Apply(_on ? 1f : 0f);
    }

    public override void OnPickupUseDown() { Toggle(); }

    public void Toggle()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _on = !_on;
        RequestSerialization();
        AudioSource s = _on ? onSound : (offSound != null ? offSound : onSound);
        if (s != null) s.Play();
    }

    private void Update()
    {
        float target = _on ? 1f : 0f;
        if (_k == target) return;
        Apply(Mathf.MoveTowards(_k < 0f ? target : _k, target, speed * Time.deltaTime));
    }

    private void Apply(float k)
    {
        _k = k;
        float e = k * k * (3f - 2f * k);
        if (effects != null)
            for (int i = 0; i < effects.Length; i++)
            {
                Transform t = effects[i]; if (t == null) continue;
                bool vis = e > 0.001f;
                if (t.gameObject.activeSelf != vis) t.gameObject.SetActive(vis);
                t.localScale = _base[i] * Mathf.Max(0.001f, e);
            }
        if (effectColliders != null) for (int i = 0; i < effectColliders.Length; i++) if (effectColliders[i] != null) effectColliders[i].enabled = e > 0.5f;
        if (effectParticles != null)
            for (int i = 0; i < effectParticles.Length; i++)
            {
                ParticleSystem ps = effectParticles[i]; if (ps == null) continue;
                var em = ps.emission; em.rateOverTimeMultiplier = _rate[i] * e;
            }
        if (aura != null)
        {
            float a = (1f - e) * auraAlpha;
            bool av = a > 0.001f;
            if (aura.enabled != av) aura.enabled = av;
            if (_auraMat != null) _auraMat.SetFloat("_Alpha", a);
        }
    }
}
