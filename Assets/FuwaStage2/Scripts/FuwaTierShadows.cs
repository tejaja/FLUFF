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
    // 2026-11-19 てじゃ「2-4 2-5の影も切れるもの増やしてもいいね」：小さめの物（radii＝囲む箱の半径がこれ未満）は smallDistance 以内だけ影
    [Tooltip("この半径(m)未満の物は smallDistance 以内だけ影（0 なら使わない）")]
    public float smallRadius = 0f;
    public float smallDistance = 12f;
    public int perFrame = 120;
    [Tooltip("2026-11-16 ポータルをのぞいている間は、向こう側の自分の位置でも影を出す（入った瞬間に影が切り替わらないように）")]
    public FuwaPortalCams portalCams;


    private int _i;
    // 2026-11-17 毎フレーム「targets == null」「targets.Length」を見るのが重かった（大きい配列。FuwaFarShadows と同じ）ので、最初に1回だけ数えておく
    private int _n = -1;
    private bool _hasPortal;

    private int TierOf(float y)
    {
        int t = 0;
        if (tierY != null) for (int k = 0; k < tierY.Length; k++) if (y >= tierY[k]) t = k + 1;
        return t;
    }

    private void Update()
    {
        if (_n < 0) { _n = targets != null ? targets.Length : 0; _hasPortal = portalCams != null; }
        if (_n == 0) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        Vector3 p = lp.GetPosition();
        bool view = _hasPortal && portalCams.viewOn;
        Vector3 q = view ? portalCams.viewPos : p;
        // 1フレームに見る数は「30フレームで1周」ぶん（前は perFrame=120 個ずつで約0.7ms。2026-11-17）
        int n = Mathf.Min(perFrame, (_n + 29) / 30);
        for (int c = 0; c < n; c++)
        {
            if (_i >= _n) _i = 0;
            Renderer r = targets[_i];
            if (r != null)
            {
                // 2026-11-26 てじゃ「ちょっと移動するだけで影がぱきっと切り替わる」：影オフにした物は深度テクスチャから外れて、自分に落ちる影もおかしくなる
                // （オフにした瞬間に、その物に落ちていた影が消える）。なので段で切るのはやめて、PC の影が出る距離（75m）の内側ではオフにしない
                float sd = Mathf.Max(radii[_i] < smallRadius ? smallDistance : shadowDistance, 75f);
                float rr = (sd + radii[_i]) * (sd + radii[_i]);
                bool on = (centers[_i] - p).sqrMagnitude < rr;
                if (!on && view) on = (centers[_i] - q).sqrMagnitude < rr;
                ShadowCastingMode want = on ? ShadowCastingMode.On : ShadowCastingMode.Off;
                if (r.shadowCastingMode != want) r.shadowCastingMode = want;
            }
            _i++;
        }
    }
}
