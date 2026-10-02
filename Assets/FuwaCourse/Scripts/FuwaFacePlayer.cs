using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 看板の文字を自分（ローカルプレイヤー）の方へ向ける。左右の向きだけ回す（傾けない）。同期なし
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaFacePlayer : UdonSharpBehaviour
{
    private void Update()
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null || !local.IsValid()) return;
        Vector3 head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        Vector3 d = transform.position - head;
        d.y = 0f;
        if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
    }
}
