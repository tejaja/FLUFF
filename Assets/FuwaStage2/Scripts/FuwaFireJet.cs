using UdonSharp;
using UnityEngine;

// 定期的に噴き出す火柱（2026-10-28 2–2 試作、てじゃ「火柱が定期的にでるとかできそう」）。
// 吹いている間だけ、柱の中に入ったふわふわを温める（FuwaHeat で止まる）。止まっている時に入っても何もない＝落ちる。
// 吹く前に warnTime 秒、小さいゆらぎ（前ぶれ）を出す。全員同じ時に吹くようにサーバー時刻で
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaFireJet : UdonSharpBehaviour
{
    public FuwaHeat heat;
    public float period = 3f, onTime = 1.2f, phase = 0f, warnTime = 0.6f;
    [Tooltip("吹いている間の炎")]
    public ParticleSystem flame;
    [Tooltip("前ぶれ（吹く前のちらちら）")]
    public ParticleSystem warn;
    public float flameRate = 80f, warnRate = 25f;
    public AudioSource roar;
    private bool _wasOn;

    public bool IsOn() { return T() < onTime; }
    private float T()
    {
        double t = VRC.SDKBase.Networking.GetServerTimeInSeconds() + phase;
        return (float)(t % period);
    }

    private void Update()
    {
        float t = T(); bool on = t < onTime; bool w = !on && t > period - warnTime;
        if (flame != null) { var e = flame.emission; e.rateOverTime = on ? flameRate : 0f; }
        if (warn != null) { var e = warn.emission; e.rateOverTime = w ? warnRate : 0f; }
        if (on && !_wasOn && roar != null && roar.clip != null) roar.PlayOneShot(roar.clip);
        _wasOn = on;
    }

    private void OnTriggerStay(Collider other)
    {
        if (heat == null || other == null || !IsOn()) return;
        if (other.GetComponent<FuwaBall>() == null) return;
        heat.Heat();
    }
}
