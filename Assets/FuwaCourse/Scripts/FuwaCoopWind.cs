using UdonSharp;
using UnityEngine;

// 「みんなで」協力のコースの風（巨大ふわふわ＝FuwaSharedBall を押す）。トリガーの箱に付ける。
// 風の向きはこの物体の +Z。onSeconds 吹いて offSeconds 止む（offSeconds=0 ならずっと）。時刻はサーバー時刻で全員そろえる
// （全員の画面で同じ風を当てる＝ FuwaSharedBall の「全員で同じ物理」と同じ考え方）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaCoopWind : UdonSharpBehaviour
{
    [Tooltip("風の強さ（加速度 m/s²）")]
    public float strength = 2.5f;
    public float onSeconds = 2.5f;
    public float offSeconds = 2f;
    [Tooltip("周期のずれ（秒）")]
    public float phase = 0f;
    [Tooltip("吹いている間だけ出す粒（なくてもOK）")]
    public ParticleSystem particles;

    private bool _on = true;

    private void Update()
    {
        bool on = IsOn();
        if (on == _on) return;
        _on = on;
        if (particles != null) { if (on) particles.Play(); else particles.Stop(); }
    }

    private bool IsOn()
    {
        if (offSeconds <= 0f) return true;
        float cyc = onSeconds + offSeconds;
        float t = Mathf.Repeat((float)(VRC.SDKBase.Networking.GetServerTimeInSeconds() % 100000.0) + phase, cyc);
        return t < onSeconds;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other == null || !_on) return;
        FuwaSharedBall sb = other.GetComponent<FuwaSharedBall>();
        if (sb != null) sb.AddWind(transform.forward * strength);
    }
}

