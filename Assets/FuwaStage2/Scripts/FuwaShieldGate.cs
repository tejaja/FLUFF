using UdonSharp;
using UnityEngine;

// 2026-11-15 2–5 上の段：バリアの門（崩れた石のアーチ＋てっぺんの小さいクリスタル）。ふわふわがくぐると FuwaShield.Activate
//  give=false なら消す門（広場の出口。見た目なし）
//  くぐった瞬間：クリスタルが光って、ふわふわへ光の粒が飛ぶ。自分の画面だけ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaShieldGate : UdonSharpBehaviour
{
    public FuwaShield shield;
    public bool give = true;
    public Transform crystal;
    public Renderer[] glows;
    public Color glowColor = new Color(0.45f, 0.9f, 1f, 1f);
    public ParticleSystem beam;
    public int beamCount = 40;
    public float spin = 30f;

    private float _hit = -10f;
    private float _bob;
    private Vector3 _crystal0;
    private MaterialPropertyBlock _mpb;

    private void Start()
    {
        _mpb = new MaterialPropertyBlock();
        if (crystal != null) _crystal0 = crystal.localPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || shield == null) return;
        FuwaBall b = other.GetComponent<FuwaBall>();
        if (b == null) return;
        if (give)
        {
            if (shield.IsOn()) return;
            shield.Activate();
            _hit = Time.time;
            if (beam != null)
            {
                // クリスタル → ふわふわ へ飛ぶ粒（向きを合わせて出す）
                Vector3 d = other.transform.position - beam.transform.position;
                if (d.sqrMagnitude > 1e-4f) beam.transform.rotation = Quaternion.LookRotation(d.normalized);
                beam.Emit(beamCount);
            }
        }
        else shield.Deactivate();
    }

    private void Update()
    {
        if (!give) return;
        float k = Mathf.Clamp01(1f - (Time.time - _hit) / 0.9f); k = k * k;
        if (crystal != null)
        {
            crystal.Rotate(0f, (spin + 540f * k) * Time.deltaTime, 0f, Space.Self);
            _bob += Time.deltaTime;
            crystal.localPosition = _crystal0 + Vector3.up * (0.08f * Mathf.Sin(_bob * 1.7f));
        }
        if (glows != null && _mpb != null)
        {
            Color c = glowColor * (0.5f + 2.2f * k); c.a = 1f;
            for (int i = 0; i < glows.Length; i++)
            {
                if (glows[i] == null) continue;
                glows[i].GetPropertyBlock(_mpb); _mpb.SetColor("_EmissionColor", c); glows[i].SetPropertyBlock(_mpb);
            }
        }
    }
}
