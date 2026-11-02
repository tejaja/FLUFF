using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 長い物（炎の壁）から鳴る音：線の上の、聞く人にいちばん近い所へ音源を動かす（2026-11-02 てじゃ「炎の壁の音の聞こえ方が違和感」）。
// 壁ごとに同じループ音を置くと重なって音が揺れ、壁のそばでも音が離れた所から聞こえていたので、音源を 1 つにした
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaLineSound : UdonSharpBehaviour
{
    [Tooltip("線分の始点と終点（親のローカル座標）")]
    public Vector3[] segA, segB;

    private void Update()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null || segA == null || segB == null) return;
        Transform basis = transform.parent;
        if (basis == null) return;
        Vector3 head = basis.InverseTransformPoint(lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position);
        float best = float.MaxValue; Vector3 nearest = transform.localPosition;
        int n = Mathf.Min(segA.Length, segB.Length);
        for (int i = 0; i < n; i++)
        {
            Vector3 a = segA[i], ab = segB[i] - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0.0001f ? Mathf.Clamp01(Vector3.Dot(head - a, ab) / len2) : 0f;
            Vector3 q = a + ab * t;
            float d = (head - q).sqrMagnitude;
            if (d < best) { best = d; nearest = q; }
        }
        transform.localPosition = nearest;
    }
}
