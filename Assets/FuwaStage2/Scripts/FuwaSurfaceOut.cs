using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2026-11-26 てじゃ「2-2の途中、まものエリア付近でデカい山のほうに落下すると、山に乗っちゃう」：
// 当たり判定のある飾り（2–2 の火山）に付ける。自分がその上に「立った」ら落ちた扱い（FuwaPlayerCatcher と同じ「落ちた！」）。
// ・足が maxFeetY より下の時だけ（2026-11-26 てじゃ「2-2の道の一番高い位置より下は全部アウト」）。竹とんぼを持っている間は無効
// ・道のふちで山肌にかすっただけではアウトにしないよう、足の真下の一番近い当たりがこの物の時だけ
// ・OnPlayerCollision〜は立っているだけだと来なかった（ClientSim）ので、毎フレーム見る。高さと箱の外ならすぐ戻るので軽い
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSurfaceOut : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("足（GetPosition）がこの高さ（ワールド y）より下の時だけアウト")]
    public float maxFeetY = 100000f;
    [Tooltip("2026-11-26 てじゃ「基準ラインを道がグルっと上がってくのに沿わせて」：center から見た方向ごと（lineStep 度きざみ、0°=+x、+z 回り）のアウトの高さ。空なら maxFeetY だけ")]
    public float[] lineY;
    public Vector3 center;
    public float lineStep = 5f;
    [Tooltip("足の真下を調べる長さ(m)")]
    public float probe = 1.2f;
    [Tooltip("この秒数つづけて乗っていたらアウト（一瞬かすっただけは無視）")]
    public float holdTime = 0.15f;

    [Tooltip("この箱（ワールド座標）の中にいる時だけ調べる。ビルダーが火山の当たりの大きさ＋2m で入れる（実行中に Collider.bounds を取ると、ステージ2が隠れていて大きさ0だった。2026-11-26）")]
    public Vector3 areaMin, areaMax;

    [Tooltip("2026-11-26 てじゃ「竹とんぼもってるときは無効で」：自分が持っている竹とんぼ（FuwaPlayerCatcher と同じ見方）")]
    public FuwaBambooCopter[] copters;

    private Collider _col;
    private float _since = -1f;
    private int _mask;

    private void Start()
    {
        _col = GetComponent<Collider>();
        // プレイヤーの層（9 Player, 10 PlayerLocal）とトリガーは見ない
        _mask = ~((1 << 9) | (1 << 10));
    }

    private void Update()
    {
        if (_col == null || ball == null) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        Vector3 p = lp.GetPosition();
        if (p.y >= maxFeetY || p.x < areaMin.x || p.x > areaMax.x || p.z < areaMin.z || p.z > areaMax.z || p.y < areaMin.y) { _since = -1f; return; }
        if (lineY != null && lineY.Length > 0)
        {
            float a = Mathf.Atan2(p.z - center.z, p.x - center.x) * Mathf.Rad2Deg;
            if (a < 0f) a += 360f;
            float f = a / lineStep; int n = lineY.Length;
            int i0 = (int)Mathf.Floor(f) % n; int i1 = (i0 + 1) % n;
            if (p.y >= Mathf.Lerp(lineY[i0], lineY[i1], f - Mathf.Floor(f))) { _since = -1f; return; }
        }
        RaycastHit hit;
        bool onMe = Physics.Raycast(p + Vector3.up * 0.4f, Vector3.down, out hit, probe, _mask, QueryTriggerInteraction.Ignore) && hit.collider == _col;
        if (!onMe) { _since = -1f; return; }
        if (_since < 0f) { _since = Time.time; return; }
        if (Time.time - _since < holdTime) return;
        _since = -1f;
        if (HoldingCopter(lp)) return;
        ball.BeginPlayerMiss("落ちた！", "You fell!");
    }

    private bool HoldingCopter(VRCPlayerApi p)
    {
        if (copters != null) for (int i = 0; i < copters.Length; i++) if (copters[i] != null && copters[i].IsMine()) return true;
        VRC_Pickup a = p.GetPickupInHand(VRC_Pickup.PickupHand.Left), b = p.GetPickupInHand(VRC_Pickup.PickupHand.Right);
        if (a != null && a.GetComponent<FuwaBambooCopter>() != null) return true;
        if (b != null && b.GetComponent<FuwaBambooCopter>() != null) return true;
        return false;
    }
}
