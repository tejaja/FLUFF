using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// みんなでひろばの転がる岩（コース2 いわのみち を誰かがクリアすると出てくる）。
// 広場の地面に描いた輪（center のまわり半径 loopRadius）をころころ回り続ける。位置はサーバー時刻で決めるので全員の画面で同じ。
// ふわふわ（FuwaSharedBall）に当たると、それぞれの画面で同じように弾く（Nudge＝通信なし）。プレイヤーには軽く押す（自分の画面で自分だけ）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaChillBoulder : UdonSharpBehaviour
{
    public FuwaChillSpawner spawner;
    public Transform center;
    public Transform visual;
    public float loopRadius = 2.4f;
    public float rockRadius = 0.55f;
    [Tooltip("転がる速さ m/s")]
    public float speed = 1.6f;
    [Tooltip("ゆっくり止まって、また転がりだす周期（秒、0ならずっと同じ速さ）")]
    public float pulse = 9f;
    public float ballPush = 3.2f;
    public float ballUp = 2.2f;
    public float playerPush = 2.5f;
    public AudioSource hitSound;

    private float[] _hitAt = new float[0];
    private float _playerHitAt;
    private Vector3 _vel;

    private float Dist(double t)
    {
        // 走った距離：基本 speed、pulse ごとにゆるく加速・減速（ずっと同じだと単調なので）
        float tf = (float)(t % 100000.0);
        if (pulse <= 0f) return tf * speed;
        float w = 2f * Mathf.PI / pulse;
        return tf * speed + 0.35f * speed / w * Mathf.Sin(tf * w);
    }

    private void Update()
    {
        if (center == null) return;
        double t = Networking.GetServerTimeInSeconds();
        float d = Dist(t);
        float a = d / loopRadius;
        Vector3 c = center.position;
        Vector3 pos = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * loopRadius + Vector3.up * rockRadius;
        Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
        float v = (Dist(t + 0.05) - d) / 0.05f;
        _vel = tangent * v;
        transform.position = pos;
        if (visual != null)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, tangent);
            visual.rotation = Quaternion.AngleAxis(-d / rockRadius * Mathf.Rad2Deg, axis) * Quaternion.LookRotation(tangent);
        }
        // プレイヤー（自分）に当たったら軽く押す
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp != null && Time.time - _playerHitAt > 0.6f)
        {
            Vector3 rel = lp.GetPosition() - pos; rel.y = 0f;
            if (rel.magnitude < rockRadius + 0.3f && Mathf.Abs(lp.GetPosition().y - c.y) < 1.2f)
            {
                _playerHitAt = Time.time;
                Vector3 push = (rel.sqrMagnitude > 0.0001f ? rel.normalized : tangent) * playerPush + Vector3.up * 2f;
                lp.SetVelocity(lp.GetVelocity() + push);
                if (hitSound != null) hitSound.Play();
            }
        }
    }

    private void FixedUpdate()
    {
        if (spawner == null || spawner.balls == null) return;
        FuwaSharedBall[] balls = spawner.balls;
        if (_hitAt.Length != balls.Length) _hitAt = new float[balls.Length];
        Vector3 pos = transform.position;
        for (int i = 0; i < balls.Length; i++)
        {
            FuwaSharedBall b = balls[i];
            if (b == null || !b.IsFlying() || Time.time - _hitAt[i] < 0.5f) continue;
            Vector3 rel = b.transform.position - pos;
            float rr = rockRadius + b.transform.lossyScale.x * 0.5f;
            if (rel.sqrMagnitude > rr * rr) continue;
            _hitAt[i] = Time.time;
            Vector3 flat = new Vector3(rel.x, 0f, rel.z);
            Vector3 dir = flat.sqrMagnitude > 0.0001f ? flat.normalized : _vel.normalized;
            Vector3 dv = (dir * ballPush + _vel * 0.6f + Vector3.up * ballUp) * Mathf.Sqrt(Mathf.Max(0.05f, b.shotScale));
            b.Nudge(dv);
            if (hitSound != null) hitSound.Play();
        }
    }
}
