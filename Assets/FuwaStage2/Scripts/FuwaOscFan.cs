using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2–4 はぐるまのとう：首ふり扇風機（2026-11-06 お試し）
// 本物の首ふり扇風機と同じしくみ：羽根の軸のウォーム → ウォームホイール（歯数 wheelTeeth で減速）→ ホイールのクランク → 棒（台に固定）で首が左右にふれる
// ・首の角度はホイールの角度から決まる（4節リンク）。作る時に計算した表 yawTable（1°ごと）から引く
// ・みんな同じ見え方になるようにサーバー時刻で（風は自分のふわふわにだけ効く＝いつもの FuwaWindArea）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaOscFan : UdonSharpBehaviour
{
    public Transform head;        // 首（台の上の軸でたて回転）
    public Transform wheel;       // ウォームホイール（首の中、たて軸）
    public Transform worm;        // ウォーム（羽根と同じ軸、ローカル Z で回る）
    public Transform blades;      // 羽根（ローカル Z で回る）
    public Transform crankPin;    // ホイールについたクランクのピン（ホイールの子）
    public Transform anchor;      // 棒の台側のピン（動かない）
    public Transform link;        // 棒（ピンとピンの間にのばす。長さ方向はローカル Z）
    [Tooltip("ホイールが1回転する秒数（＝首が1往復する秒数）")]
    public float period = 10f;
    public int wheelTeeth = 30;
    [Tooltip("ウォームの回る度 / ホイールの回る度（向きの符号つき）")]
    public float wormPerWheel = -30f;
    [Tooltip("ホイールの角度 0〜359°（1°ごと）に対する首の角度（度、2D 反時計回り）")]
    public float[] yawTable;
    public float wheelPhase = 0f, wormPhase = 0f;

    private void Update()
    {
        if (yawTable == null || yawTable.Length < 2) return;
        int per = Mathf.Max(1, Mathf.RoundToInt(period * 1000f));
        int ms = Networking.GetServerTimeInMilliseconds() % per;
        if (ms < 0) ms += per;
        float th = 360f * ms / per;   // ホイールの角度（2D 反時計回り）
        int n = yawTable.Length;
        float f = th / 360f * n;
        int i0 = Mathf.FloorToInt(f) % n;
        int i1 = (i0 + 1) % n;
        float yaw = Mathf.LerpAngle(yawTable[i0], yawTable[i1], f - Mathf.Floor(f));
        if (head != null) head.localRotation = Quaternion.Euler(0f, -yaw, 0f);
        if (wheel != null) wheel.localRotation = Quaternion.Euler(0f, -(th + wheelPhase), 0f);
        // ウォームは 1 歯ぶん＝1 回転。360 で割った余りだけ使う（大きい角度で精度が落ちないように）
        float wa = Mathf.Repeat(th * wormPerWheel + wormPhase, 360f);
        if (worm != null) worm.localRotation = Quaternion.Euler(0f, 0f, wa);
        if (blades != null) blades.localRotation = Quaternion.Euler(0f, 0f, wa);
        if (link != null && crankPin != null && anchor != null)
        {
            Vector3 a = crankPin.position, b = anchor.position;
            Vector3 d = b - a;
            link.position = (a + b) * 0.5f;
            if (d.sqrMagnitude > 1e-6f) link.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            Vector3 s = link.localScale; s.z = d.magnitude; link.localScale = s;
        }
    }
}
