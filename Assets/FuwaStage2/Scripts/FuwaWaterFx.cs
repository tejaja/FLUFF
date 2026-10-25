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

    // 浮いている間ずっと、ふわふわから波紋が広がり続ける（2026-10-25 てじゃ「水にふわふわがついたときに波紋だそ」「ついてるあいだずっと」
    // 「同じ丸が固定じゃなくてふわふわから波紋が広がる感じのをつづけてほしい」→ 一度入れた、ずっと出ている輪はやめた）。FuwaWater から毎物理ステップ呼ばれる
    [Tooltip("浮いている間の波紋の間隔(秒)")]
    public float floatRippleInterval = 0.3f;
    private float _nextFloat;

    public void FloatRipple(Vector3 p)
    {
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
