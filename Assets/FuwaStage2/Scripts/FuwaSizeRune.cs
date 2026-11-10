using UdonSharp;
using UnityEngine;

// 2026-11-10 2–5 そらのいせき：大きさの魔法のルーン（てじゃ「途中で魔法的なギミックあり、ふわふわがちいさくなったりおおきくなったりする」）
//  ふわふわが輪（トリガー）をくぐると FuwaBall.SetSizeMode(mode)：-1 小さく、0 元に、+1 大きく
//  見た目：輪の中の魔法陣（glyph）がゆっくり回る。くぐった瞬間に光って速く回り、粒がはじける
//  自分の画面だけ（ふわふわは自分のもの）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSizeRune : UdonSharpBehaviour
{
    public int mode = -1;
    public Transform glyph;
    public Renderer[] glows;
    public ParticleSystem burst;
    public int burstCount = 24;
    public float spin = 25f, spinHit = 360f;
    public Color glowColor = new Color(0.4f, 0.8f, 1f, 1f);
    private float _hit = -10f;
    private MaterialPropertyBlock _mpb;

    private void Start() { _mpb = new MaterialPropertyBlock(); }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        FuwaBall b = other.GetComponent<FuwaBall>();
        if (b == null) return;
        if (b.GetSizeMode() == mode) return;
        b.SetSizeMode(mode);
        _hit = Time.time;
        if (burst != null) burst.Emit(burstCount);
    }

    private void Update()
    {
        float k = Mathf.Clamp01(1f - (Time.time - _hit) / 0.8f); k = k * k;
        if (glyph != null) glyph.Rotate(0f, 0f, (spin + spinHit * k) * Time.deltaTime, Space.Self);
        if (glows != null && _mpb != null)
        {
            Color c = glowColor * (1.2f + 2.5f * k); c.a = 1f;
            for (int i = 0; i < glows.Length; i++)
            {
                if (glows[i] == null) continue;
                glows[i].GetPropertyBlock(_mpb); _mpb.SetColor("_EmissionColor", c); glows[i].SetPropertyBlock(_mpb);
            }
        }
    }
}
