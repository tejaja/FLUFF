using UdonSharp;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDKBase;

// 自分から遠い物は影を落とさないようにする（自分の画面だけ）。
// 対象と中心・半径はエディタの「FuwaCourse/遠景の軽量化を設定」で詰める。毎フレーム少しずつ見て回る。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaFarShadows : UdonSharpBehaviour
{
    [Tooltip("自分からこの距離(m)より遠い物は影を落とさない（物のふちまでの距離）")]
    public float shadowDistance = 40f;
    [Tooltip("1フレームに見る数")]
    public int perFrame = 120;
    public Renderer[] targets;
    public Vector3[] centers;
    public float[] radii;

    private bool[] _off;
    private int _next;

    private void Start()
    {
        if (targets == null) return;
        _off = new bool[targets.Length];
    }

    private void Update()
    {
        if (targets == null || _off == null || targets.Length == 0) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null || !local.IsValid()) return;
        Vector3 me = local.GetPosition();
        int n = targets.Length;
        int count = Mathf.Min(perFrame, n);
        for (int k = 0; k < count; k++)
        {
            int i = _next;
            _next = (_next + 1) % n;
            Renderer r = targets[i];
            if (r == null) continue;
            bool far = Vector3.Distance(me, centers[i]) - radii[i] > shadowDistance;
            if (far == _off[i]) continue;
            _off[i] = far;
            r.shadowCastingMode = far ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }
    }
}
