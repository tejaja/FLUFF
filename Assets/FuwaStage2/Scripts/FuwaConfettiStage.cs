using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

// 2026-11-17 ごほうびの島の記念撮影ステージ：ひな壇に乗ると紙吹雪（みんなの画面に出る）。連打しないように間をあける
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]   // 紙吹雪はネットワークのイベントで（変数の同期はなし）
public class FuwaConfettiStage : UdonSharpBehaviour
{
    public ParticleSystem[] confetti;
    public AudioSource popSound;
    public float cooldown = 3f;
    private float _last = -100f;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        if (Time.time - _last < cooldown) return;
        _last = Time.time;
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(Pop));
    }

    public void Pop()
    {
        if (confetti != null) for (int i = 0; i < confetti.Length; i++) if (confetti[i] != null) confetti[i].Play();
        if (popSound != null && popSound.enabled && popSound.gameObject.activeInHierarchy) popSound.Play();
    }
}
