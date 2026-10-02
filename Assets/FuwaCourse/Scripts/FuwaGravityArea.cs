using UdonSharp;
using UnityEngine;

// トリガーの範囲内にいる間だけ、ふわふわが重くなる（速く落ちる）。
// ふわふわが中にいる間は、うねる低音（ウォン…ウォン…）をループで鳴らす（自分の画面だけ）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGravityArea : UdonSharpBehaviour
{
    [Tooltip("落ちる加速度と落下速度の上限を何倍にするか")]
    public float gravityScale = 2.5f;

    [Header("中にいる間の音")]
    [Tooltip("ループで鳴らす音（Loop ON・Play On Awake OFF にしておく。なくてもOK）")]
    public AudioSource loopSound;
    [Tooltip("一番大きい時の音量")]
    public float maxVolume = 0.35f;
    [Tooltip("入った時に大きくなるまでの秒数")]
    public float fadeIn = 0.3f;
    [Tooltip("出た時に消えるまでの秒数")]
    public float fadeOut = 0.6f;

    private float _lastSeen = -10f;
    private float _volume;

    private void OnTriggerStay(Collider other)
    {
        if (other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;
        ball.SetGravityScale(gravityScale);
        _lastSeen = Time.time;
    }

    private void Update()
    {
        if (loopSound == null) return;
        // 物理の判定は毎フレームではないので、少し余裕を見て「中にいる」とする
        bool inside = Time.time - _lastSeen < 0.2f;
        if (!inside && _volume <= 0f) return;
        float target = inside ? maxVolume : 0f;
        float speed = maxVolume / Mathf.Max(0.01f, inside ? fadeIn : fadeOut);
        _volume = Mathf.MoveTowards(_volume, target, speed * Time.deltaTime);
        loopSound.volume = _volume;
        if (_volume > 0f && !loopSound.isPlaying) loopSound.Play();
        else if (_volume <= 0f && loopSound.isPlaying) loopSound.Stop();
    }

    // モードの切り替えなどでエリアごと消えた時は、音も止める
    private void OnDisable()
    {
        _volume = 0f;
        _lastSeen = -10f;
        if (loopSound != null) loopSound.Stop();
    }
}
