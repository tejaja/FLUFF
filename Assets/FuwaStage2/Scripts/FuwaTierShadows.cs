using UdonSharp;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDKBase;

// 2026-11-15 2–5 の軽量化（てじゃ「やろ」）：段ごとの影
//  2–5 は 下の段・中段・上の段 が上下に重なっていて、上の段にいると下の段の物がみんな近い（FuwaFarShadows の 40m の中）→ 影を出す物が多かった
//  → 自分が今いる段より「下の段」の物は影を出さない（下の影はさらに下に落ちるので、上からはほとんど見えない）。上の段の物は今まで通り（自分の段に影が落ちる）
//  それに加えて FuwaFarShadows と同じく、遠い物（shadowDistance＋大きさ）は影オフ。2–5 の大きい物は FarShadows からこちらへ移してある（取り合いにならないように）
//  段は物の下のはし（tiers[]＝ビルド時に決めた番号）と、自分の足の高さ（tierY のしきい）で決める
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaTierShadows : UdonSharpBehaviour
{
    public Renderer[] targets;
    public Vector3[] centers;
    public float[] radii;
    public int[] tiers;
    [Tooltip("段のさかい目の高さ（低い順）。足の高さがこれ以上なら次の段")]
    public float[] tierY;
    public float shadowDistance = 40f;
    public int perFrame = 120;
    [Tooltip("2026-11-16 ポータルをのぞいている間は、向こう側の自分の位置でも影を出す（入った瞬間に影が切り替わらないように）")]
    public FuwaPortalCams portalCams;

    private int _i;

    private int TierOf(float y)
    {
        int t = 0;
        if (tierY != null) for (int k = 0; k < tierY.Length; k++) if (y >= tierY[k]) t = k + 1;
        return t;
    }

    private void Update()
    {
        if (targets == null || targets.Length == 0) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        Vector3 p = lp.GetPosition();
        int pt = TierOf(p.y);
        bool view = portalCams != null && portalCams.viewOn;
        Vector3 q = view ? portalCams.viewPos : p;
        int qt = TierOf(q.y);
        int n = Mathf.Min(perFrame, targets.Length);
        for (int c = 0; c < n; c++)
        {
            if (_i >= targets.Length) _i = 0;
            Renderer r = targets[_i];
            if (r != null)
            {
                float rr = (shadowDistance + radii[_i]) * (shadowDistance + radii[_i]);
                bool on = tiers[_i] >= pt && (centers[_i] - p).sqrMagnitude < rr;
                if (!on && view) on = tiers[_i] >= qt && (centers[_i] - q).sqrMagnitude < rr;
                ShadowCastingMode want = on ? ShadowCastingMode.On : ShadowCastingMode.Off;
                if (r.shadowCastingMode != want) r.shadowCastingMode = want;
            }
            _i++;
        }
    }
}
