using UdonSharp;
using UnityEngine;

// 水に落ちた時の「ちゃぽん」（音・しぶき・波紋）。ステージ2で1つだけ置いて、どの水（FuwaWater）からも呼ぶ（2026-10-17）。
// 自分の画面だけ（ふわふわは自分のもの）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWaterFx : UdonSharpBehaviour
{
    public ParticleSystem drops;
    public ParticleSystem ripple;
    [Tooltip("落ちた時だけの大きい波紋（2026-10-25 てじゃ「着水の波紋を大きく・はっきり」）。なくてもOK")]
    public ParticleSystem rippleBig;
    public AudioSource sound;
    [Tooltip("続けて鳴らさない間隔(秒)")]
    public float minInterval = 0.25f;
    [Tooltip("しぶきの粒の数（いちばん強い時）")]
    public int maxDrops = 14;
    private float _next;

    // p＝水面の上の位置、strength＝0〜1（落ちてきた速さ）
    public void Splash(Vector3 p, float strength)
    {
        if (Time.time < _next) return;
        _next = Time.time + minInterval;
        strength = Mathf.Clamp01(strength);
        Visual(p, strength);
        if (rippleBig != null) rippleBig.Emit(1);
        if (sound != null && sound.clip != null)
        {
            sound.pitch = Random.Range(0.92f, 1.1f);
            sound.PlayOneShot(sound.clip, Mathf.Lerp(0.45f, 1f, strength));
        }
    }

    // 浮いている間ずっと、ふわふわのまわりに波紋（2026-10-25 てじゃ「水にふわふわがついたときに波紋だそ」「ついてるあいだずっと」）。FuwaWater から毎物理ステップ呼ばれる
    [Tooltip("浮いている間の波紋の間隔(秒)")]
    public float floatRippleInterval = 0.6f;
    private float _nextFloat;
    [Tooltip("浮いている間、ふわふわのまわりにずっと出ている輪（水面に寝かせた板）。てじゃ「ふわふわの周りにもずっと出ててほしい」")]
    public Transform floatRing;
    public float floatRingSize = 1.0f;
    private Vector3 _ringPos;
    private float _ringSeen = -10f, _ringK;

    private void Update()
    {
        if (floatRing == null) return;
        bool on = Time.time - _ringSeen < 0.2f;
        _ringK = Mathf.MoveTowards(_ringK, on ? 1f : 0f, Time.deltaTime * 5f);
        if (_ringK <= 0.001f) { if (floatRing.gameObject.activeSelf) floatRing.gameObject.SetActive(false); return; }
        if (!floatRing.gameObject.activeSelf) floatRing.gameObject.SetActive(true);
        floatRing.position = _ringPos;
        float s = floatRingSize * _ringK * (1f + 0.06f * Mathf.Sin(Time.time * 3f));
        floatRing.localScale = new Vector3(s, s, s);
    }

    public void FloatRipple(Vector3 p)
    {
        _ringPos = p; _ringSeen = Time.time;
        if (ripple == null || Time.time < _nextFloat) return;
        _nextFloat = Time.time + floatRippleInterval;
        transform.position = p;
        ripple.Emit(1);
    }

    // しぶきと波紋だけ（音は呼ぶ側で。プレイヤーが入った時の「じゃぶ」用、FuwaShallowWater）
    public void Visual(Vector3 p, float strength)
    {
        strength = Mathf.Clamp01(strength);
        transform.position = p;
        if (drops != null) drops.Emit(Mathf.Max(3, Mathf.RoundToInt(maxDrops * strength)));
        if (ripple != null) ripple.Emit(strength > 0.5f ? 2 : 1);
    }
}
