using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 上昇気流（人用）：トリガーの中にいる自分を上へ持ち上げる。
// ふわふわも持ち上げたい時は、同じオブジェクトに FuwaWindArea（上向き）も付ける。
// ・てっぺんでふわっと止まる：トリガーの上の hoverBand(m) では上昇がだんだん弱まって、てっぺんでホバリング
// ・抜けた後ゆっくり落ちる：出てから slowFallTime 秒は重力を弱く（毎フレーム見張って戻す）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPlayerUpdraft : UdonSharpBehaviour
{
    [Tooltip("持ち上げる速さ（上向き m/s）。これより遅い時だけ上書き")]
    public float liftSpeed = 3.2f;
    [Tooltip("横の速さをどれだけ残すか（1=そのまま）")]
    public float keepHorizontal = 1f;

    [Header("てっぺんでホバリング")]
    [Tooltip("トリガーの上端からこの高さ(m)の間で、上昇をだんだん弱めて止める（0=無効）")]
    public float hoverBand = 1.2f;
    [Tooltip("てっぺんでの上下の速さの上限（ゆっくり落ちる・ゆっくり上がる）")]
    public float hoverDrift = 0.25f;

    [Header("抜けた後ゆっくり落ちる")]
    [Tooltip("気流から出た後、重力を弱くする秒数（0=無効）")]
    public float slowFallTime = 1.0f;
    [Tooltip("その間の重力の倍率")]
    public float slowFallGravity = 0.35f;

    private Collider _col;
    private float _lastInside = -10f;
    private bool _slow;
    [Tooltip("普段の重力の強さ（ワールドの設定。ほかで重力を変えていないので固定値）")]
    public float normalGravity = 1f;

    private void Start()
    {
        _col = GetComponent<Collider>();
    }

    public override void OnPlayerTriggerStay(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        _lastInside = Time.time;
        if (slowFallTime > 0f) _slow = true;
        Vector3 v = player.GetVelocity();
        float target = liftSpeed;
        if (hoverBand > 0f && _col != null)
        {
            float top = _col.bounds.max.y;
            float y = player.GetPosition().y;
            float k = Mathf.Clamp01((top - y) / hoverBand);   // 1=下の方、0=てっぺん
            target = Mathf.Lerp(0f, liftSpeed, k * k);
            // てっぺん付近：上下の速さを小さくおさえてホバリング
            if (k < 1f)
            {
                float lo = -hoverDrift, hi = Mathf.Max(target, hoverDrift);
                v.y = Mathf.Clamp(v.y, lo, hi);
                if (v.y < target) v.y = Mathf.MoveTowards(v.y, target, liftSpeed * 2f * Time.deltaTime);
                v.x *= keepHorizontal; v.z *= keepHorizontal;
                player.SetVelocity(v);
                return;
            }
        }
        if (v.y >= target) return;
        v.x *= keepHorizontal; v.z *= keepHorizontal;
        v.y = target;
        player.SetVelocity(v);
    }

    private void Update()
    {
        if (!_slow) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        float since = Time.time - _lastInside;
        if (since < 0.1f) { p.SetGravityStrength(normalGravity); return; }   // 中にいる間は普段どおり（持ち上げは速度で）
        if (since < slowFallTime && !p.IsPlayerGrounded()) { p.SetGravityStrength(normalGravity * slowFallGravity); return; }
        // 時間切れ or 着地したら元に戻す
        p.SetGravityStrength(normalGravity);
        _slow = false;
    }

    private void OnDisable()
    {
        if (!_slow) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p != null) p.SetGravityStrength(normalGravity);
        _slow = false;
    }
}
