using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// あそびばのポータル（FuwaPlaygroundPortals）の持ち手（2026-11-16 てじゃ「ポータルも下に掴めるモノつけたいね」）
// このオブジェクト（VRCPickup・VRCObjectSync）を持って運ぶと、子のポータルもいっしょに動く。
// はなした時：まっすぐ立てて（向きは水平だけ残す）、真下の床に置く
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPlaygroundPortalHandle : UdonSharpBehaviour
{
    public LayerMask groundMask = 1 | (1 << 11);
    [Tooltip("持ってトリガーでオン/オフ（両方のポータルがいっしょに。2026-11-27）")]
    public FuwaPlaygroundPortals portals;
    public override void OnPickupUseDown() { if (portals != null) portals.Toggle(); }
    [Tooltip("床から持ち手の原点までの高さ")]
    public float groundOffset = 0f;

    public override void OnDrop()
    {
        Vector3 f = transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.001f) f = Vector3.forward;
        Quaternion q = Quaternion.LookRotation(f.normalized, Vector3.up);
        Vector3 p = transform.position;
        RaycastHit h;
        if (Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out h, 6f, groundMask, QueryTriggerInteraction.Ignore)) p.y = h.point.y + groundOffset;
        transform.SetPositionAndRotation(p, q);
    }
}
