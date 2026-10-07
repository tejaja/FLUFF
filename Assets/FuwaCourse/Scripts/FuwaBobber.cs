using UdonSharp;
using UnityEngine;

// 上下にゆっくり動く足場（いわのみちのゴール前の浮き岩など）。各自の時計で動く（同期なし）。
// 乗っている自分のプレイヤーも一緒に上下させる（足場だけ動くと、下がる時に置いていかれてガタつくので）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaBobber : UdonSharpBehaviour
{
    [Tooltip("上下の幅（片側 m）")]
    public float amplitude = 0.35f;
    [Tooltip("1往復の秒数")]
    public float period = 4f;
    [Tooltip("位相（0〜1）。足場ごとにずらす")]
    [Range(0f, 1f)] public float phase = 0f;
    [Tooltip("上に乗っているとみなす範囲（足場の中心からの水平距離 m）")]
    public float rideRadius = 1.0f;

    private Vector3 _base;
    private float _lastY;
    private Collider _col;

    private void Start()
    {
        _base = transform.localPosition;
        _col = GetComponent<Collider>();
        _lastY = Offset();
    }

    private float Offset()
    {
        return Mathf.Sin((Time.time / period + phase) * 2f * Mathf.PI) * amplitude;
    }

    private void Update()
    {
        float y = Offset();
        float dy = y - _lastY;
        _lastY = y;
        transform.localPosition = _base + new Vector3(0f, y, 0f);

        // 乗っている自分を一緒に動かす：下がる時だけ（上がる時は足場が押し上げてくれる）
        if (dy >= 0f || _col == null) return;
        VRC.SDKBase.VRCPlayerApi p = VRC.SDKBase.Networking.LocalPlayer;
        if (p == null || !p.IsPlayerGrounded()) return;
        Vector3 pos = p.GetPosition();
        Bounds b = _col.bounds;
        Vector3 c = b.center;
        float dx = pos.x - c.x, dz = pos.z - c.z;
        if (dx * dx + dz * dz > rideRadius * rideRadius) return;
        if (Mathf.Abs(pos.y - b.max.y) > 0.25f) return;
        // 下向きの速さを足場に合わせる（瞬間移動はしない）
        Vector3 v = p.GetVelocity();
        float want = dy / Mathf.Max(Time.deltaTime, 0.0001f);
        if (v.y > want) { v.y = want; p.SetVelocity(v); }
    }
}
