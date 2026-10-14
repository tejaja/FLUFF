using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

// あそびばの投げる岩（コース2 いわのみち を誰かがクリアすると出てくる。2026-10-12 てじゃ「投げたら転がるPickup岩」「あたると人もふわふわも吹っ飛ぶ」）。
// 持って投げると重力で落ちて地面を転がる（ふつうの Rigidbody、VRCObjectSync で位置を配る）。
// 動いている岩に当たると、人は自分の画面で自分を吹き飛ばし、ふわふわ（FuwaSharedBall）はそれぞれの画面で Nudge（通信なし）。
// 速さは位置の変わり方から出す（持ち主でない画面では Rigidbody が止まっているので）。島の下へ落ちたら持ち主が元の場所へ戻す
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class FuwaThrowRock : UdonSharpBehaviour
{
    public FuwaPlaygroundSpawner spawner;
    public float radius = 0.35f;
    [Tooltip("この速さ(m/s)以上で動いている時だけ当たる")]
    public float hitSpeed = 0.9f;
    public float ballPush = 3.5f;
    public float ballUp = 2.8f;
    public float playerPush = 1.2f;
    public float playerUp = 4f;
    [Tooltip("投げた本人は、手を離してからこの秒数は当たらない（投げた直後に自分が吹っ飛んでいた。2026-10-13 てじゃ）")]
    public float throwerSafeTime = 0.6f;
    public AudioSource hitSound;
    [Tooltip("ここより下へ落ちたら戻す（ワールドの高さ）")]
    public float fallY = 80f;

    private Vector3 _prev;
    private Vector3 _vel;
    private float[] _hitAt = new float[0];
    private float _playerHitAt;
    private float _droppedAt = -10f;   // 自分の手から離した時刻（自分の画面だけ）

    [Tooltip("ぶつからずにすり抜ける物（手持ちの扇風機・うちわ。2026-10-14 てじゃ「扇風機を岩はすりぬけてほしい」）")]
    public Collider[] passThrough;

    private bool _ignored;

    private void Start() { _prev = transform.position; }

    // すり抜けの設定は、物が隠れる（エリアの切り替え）と消えるので、出てくるたびに最初の物理の1回でかけ直す
    private void OnEnable() { _ignored = false; }

    private void ApplyPassThrough()
    {
        _ignored = true;
        Collider me = (Collider)GetComponent(typeof(Collider));
        if (me == null || passThrough == null) return;
        foreach (Collider c in passThrough) if (c != null) Physics.IgnoreCollision(me, c, true);
    }

    public override void OnDrop() { _droppedAt = Time.time; }

    private void FixedUpdate()
    {
        if (!_ignored) ApplyPassThrough();
        Vector3 p = transform.position;
        Vector3 v = (p - _prev) / Mathf.Max(0.0001f, Time.fixedDeltaTime);
        _prev = p;
        _vel = Vector3.Lerp(_vel, v, 0.5f);
        if (Networking.IsOwner(gameObject) && p.y < fallY)
        {
            VRCObjectSync s = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
            if (s != null) s.Respawn();
            return;
        }
        float spd = _vel.magnitude;
        if (spd < hitSpeed) return;
        Vector3 dir = _vel / spd;
        // ふわふわ
        if (spawner != null && spawner.balls != null)
        {
            FuwaSharedBall[] balls = spawner.balls;
            if (_hitAt.Length != balls.Length) _hitAt = new float[balls.Length];
            for (int i = 0; i < balls.Length; i++)
            {
                FuwaSharedBall b = balls[i];
                if (b == null || !b.IsFlying() || Time.time - _hitAt[i] < 0.4f) continue;
                Vector3 rel = b.transform.position - p;
                float rr = radius + b.transform.lossyScale.x * 0.5f + 0.15f;   // 物理でぶつかると表面どうしで止まるので、少し広めに
                if (rel.sqrMagnitude > rr * rr) continue;
                _hitAt[i] = Time.time;
                Vector3 flat = new Vector3(rel.x, 0f, rel.z);
                Vector3 away = flat.sqrMagnitude > 0.0001f ? flat.normalized : dir;
                float k = Mathf.Clamp(spd / 4f, 0.6f, 2f);
                b.Nudge((away * ballPush + dir * spd * 0.5f + Vector3.up * ballUp) * k * Mathf.Sqrt(Mathf.Max(0.05f, b.shotScale)));
                PlayHit();
            }
        }
        // 自分（持っている時は当たらない）
        VRCPlayerApi lp = Networking.LocalPlayer;
        VRCPickup pk = (VRCPickup)GetComponent(typeof(VRCPickup));
        if (lp == null || (pk != null && pk.IsHeld) || Time.time - _playerHitAt < 0.8f || Time.time - _droppedAt < throwerSafeTime) return;
        Vector3 me = lp.GetPosition();
        Vector3 d = me - p; float dy = d.y; d.y = 0f;
        if (d.magnitude > radius + 0.35f || dy > 0.3f || dy < -1.8f) return;
        _playerHitAt = Time.time;
        // 大きい岩は転がるとすぐ遅くなるので、遅くても当たるようにして（前は1.6m/s以上だけ＝転がってきた岩にほぼ当たらなかった。2026-10-13 てじゃ）、
        // そのかわり遅い時は上に飛ぶのも弱く
        Vector3 push = (d.sqrMagnitude > 0.0001f ? d.normalized : dir) * spd * playerPush + dir * spd * 0.5f + Vector3.up * playerUp * Mathf.Clamp(spd / 3f, 0.4f, 1f);
        lp.SetVelocity(lp.GetVelocity() + push);
        PlayHit();
    }

    private void PlayHit()
    {
        if (hitSound != null) { hitSound.pitch = Random.Range(0.9f, 1.1f); hitSound.Play(); }
    }
}
