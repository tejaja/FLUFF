using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// ゴールの目印。▼をふよふよ上下させながら回し、文字を自分の方へ向ける（同期なし）

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGoalMarker : UdonSharpBehaviour
{
    public Transform arrow;
    public Transform label;
    public float bobHeight = 0.15f;
    public float bobSpeed = 2.2f;
    public float spinSpeed = 60f;

    private Vector3 _arrowBase;

    private void Start()
    {
        if (arrow != null) _arrowBase = arrow.localPosition;
    }

    private void Update()
    {
        if (arrow != null)
        {
            arrow.localPosition = _arrowBase + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            arrow.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }
        VRCPlayerApi local = Networking.LocalPlayer;
        if (label != null && local != null && local.IsValid())
        {
            Vector3 head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            Vector3 d = label.position - head;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) label.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }
}
