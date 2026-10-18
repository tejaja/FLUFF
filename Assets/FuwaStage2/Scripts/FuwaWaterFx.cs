using UdonSharp;
using UnityEngine;

// 水に落ちた時の「ちゃぽん」（音・しぶき・波紋）。ステージ2で1つだけ置いて、どの水（FuwaWater）からも呼ぶ（2026-10-17）。
// 自分の画面だけ（ふわふわは自分のもの）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWaterFx : UdonSharpBehaviour
{
    public ParticleSystem drops;
    public ParticleSystem ripple;
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
        if (sound != null && sound.clip != null)
        {
            sound.pitch = Random.Range(0.92f, 1.1f);
            sound.PlayOneShot(sound.clip, Mathf.Lerp(0.45f, 1f, strength));
        }
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
