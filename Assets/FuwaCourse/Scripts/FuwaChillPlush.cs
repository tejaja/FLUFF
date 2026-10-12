using UdonSharp;
using UnityEngine;

// みんなでひろばのまもののぬいぐるみ（コース4 まもののす を誰かがクリアすると出てくる。てじゃ「当たるとちょっと弾む」）。
// ふわふわ（FuwaSharedBall）がぶつかると、ぬいぐるみがぽよんとつぶれて、ふわふわを跳ね返す（それぞれの画面で同じように Nudge）。
// 持って好きな所に置ける（手を離すとその場に浮く、VRCObjectSync）。当たり判定はトリガー（持つため）で、跳ね返しはこのスクリプト
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaChillPlush : UdonSharpBehaviour
{
    public FuwaChillSpawner spawner;
    public Transform visual;
    public float radius = 0.4f;
    public float bounce = 1.25f;
    public float minOut = 1.6f;
    public AudioSource boing;

    private Vector3 _baseScale = Vector3.one;
    private float _squashAt = -10f;
    private float _squashK;
    private Vector3 _squashN = Vector3.up;
    private float[] _hitAt = new float[0];

    private void Start() { if (visual != null) _baseScale = visual.localScale; }

    private void FixedUpdate()
    {
        if (spawner == null || spawner.balls == null) return;
        FuwaSharedBall[] balls = spawner.balls;
        if (_hitAt.Length != balls.Length) _hitAt = new float[balls.Length];
        Vector3 c = transform.position;
        for (int i = 0; i < balls.Length; i++)
        {
            FuwaSharedBall b = balls[i];
            if (b == null || !b.IsFlying() || Time.time - _hitAt[i] < 0.25f) continue;
            Vector3 rel = b.transform.position - c;
            float rr = radius + b.transform.lossyScale.x * 0.5f;
            float d = rel.magnitude;
            if (d > rr || d < 0.0001f) continue;
            Vector3 n = rel / d;
            Rigidbody rb = b.GetComponent<Rigidbody>();
            Vector3 v = rb != null ? rb.velocity : Vector3.zero;
            float vn = Vector3.Dot(v, n);
            if (vn > 0.2f) continue;   // もう離れていく所
            _hitAt[i] = Time.time;
            // 法線方向の速さを跳ね返す（少し強め）＋最低これだけは外へ
            float outSpeed = Mathf.Max(minOut, -vn * bounce);
            b.Nudge(n * (outSpeed - vn) + Vector3.up * 0.6f);
            Squash(Mathf.Clamp01(-vn / 4f + 0.35f), n);
        }
    }

    private void Squash(float k, Vector3 n)
    {
        _squashAt = Time.time; _squashK = k; _squashN = n;
        if (boing != null && boing.clip != null) { boing.pitch = Random.Range(0.95f, 1.15f); boing.PlayOneShot(boing.clip, 0.4f + 0.6f * k); }
    }

    private void Update()
    {
        if (visual == null) return;
        float t = Time.time - _squashAt;
        if (t > 1f) { if (visual.localScale != _baseScale) visual.localScale = _baseScale; return; }
        float w = _squashK * 0.3f * Mathf.Exp(-t * 6f) * Mathf.Cos(t * 22f);
        // ぶつかった向きにつぶれて、横にふくらむ（上下だけで近似）
        visual.localScale = new Vector3(_baseScale.x * (1f + w * 0.6f), _baseScale.y * (1f - w), _baseScale.z * (1f + w * 0.6f));
    }
}
