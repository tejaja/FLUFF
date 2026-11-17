using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;

// 2026-11-17 おまけの島の「手に持てるトロフィー」：持てる（VRCObjectSync で位置は全員に同期）。手放して returnDelay 秒たつと台にもどる
//  VRCObjectSync と同じ物なので Continuous（同期する変数はなし）
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class FuwaTrophy : UdonSharpBehaviour
{
    public float returnDelay = 20f;
    private float _dropAt = -1f;
    private bool _held;

    public override void OnPickup() { _held = true; _dropAt = -1f; }
    public override void OnDrop() { _held = false; _dropAt = Time.time; }

    private void Update()
    {
        if (_held || _dropAt < 0f) return;
        if (Time.time - _dropAt < returnDelay) return;
        _dropAt = -1f;
        if (!Networking.IsOwner(gameObject)) return;
        VRCObjectSync sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        if (sync != null) sync.Respawn();
    }
}
