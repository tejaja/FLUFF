using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// あそびばのおもちゃ（コース2–5「そらのいせき」のクリアで出る）：ポータル2つ1組（2026-11-16 てじゃ「2-5 ポータル」）
// a・b は立っている輪（+forward が表＝入る側、穴は local の |x|,|y-radius| が radius の円の中）。表から裏へくぐると、もう片方の表から出てくる
//  変換：p' = 出口 · R180y · 入口⁻¹ · p（向き・速さもそのまま運ぶ）
// 人：頭が面をこえた瞬間に体ごと移す（自分の画面）。同期ふわふわ：全員の画面で同じように移す（持ち主は送り直す）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPlaygroundPortals : UdonSharpBehaviour
{
    public Transform a, b;
    [Tooltip("穴の半径（輪の中心は local の (0, radius, 0)）")]
    public float radius = 1.1f;
    public FuwaPlaygroundSpawner spawner;
    public AudioSource warpSound;

    private float _prevHeadA = 1f, _prevHeadB = 1f;
    private float[] _prevBallA, _prevBallB;
    private float _cool;

    private void Start()
    {
        int n = spawner != null && spawner.balls != null ? spawner.balls.Length : 0;
        _prevBallA = new float[n]; _prevBallB = new float[n];
        for (int i = 0; i < n; i++) { _prevBallA[i] = 1f; _prevBallB[i] = 1f; }
    }

    private bool InHole(Transform p, Vector3 w)
    {
        Vector3 l = p.InverseTransformPoint(w);
        float dx = l.x, dy = l.y - radius;
        return dx * dx + dy * dy <= radius * radius;
    }

    private Vector3 MapPoint(Transform from, Transform to, Vector3 w)
    {
        Vector3 l = from.InverseTransformPoint(w);
        l = new Vector3(-l.x, l.y, -l.z);
        return to.TransformPoint(l);
    }

    private Vector3 MapDir(Transform from, Transform to, Vector3 d)
    {
        Vector3 l = from.InverseTransformDirection(d);
        l = new Vector3(-l.x, l.y, -l.z);
        return to.TransformDirection(l);
    }

    private void Update()
    {
        if (a == null || b == null) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        Vector3 head = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        float sa = Vector3.Dot(head - a.position, a.forward), sb = Vector3.Dot(head - b.position, b.forward);
        if (Time.time > _cool)
        {
            if (_prevHeadA > 0f && sa <= 0f && InHole(a, head)) { WarpPlayer(p, a, b); sa = 1f; sb = 1f; }
            else if (_prevHeadB > 0f && sb <= 0f && InHole(b, head)) { WarpPlayer(p, b, a); sa = 1f; sb = 1f; }
        }
        _prevHeadA = sa; _prevHeadB = sb;
    }

    private void WarpPlayer(VRCPlayerApi p, Transform from, Transform to)
    {
        Vector3 pos = MapPoint(from, to, p.GetPosition()) + to.forward * 0.15f;
        Vector3 fwd = MapDir(from, to, p.GetRotation() * Vector3.forward); fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = to.forward;
        Vector3 v = MapDir(from, to, p.GetVelocity());
        p.TeleportTo(pos, Quaternion.LookRotation(fwd.normalized), VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, false);
        p.SetVelocity(v);
        _cool = Time.time + 0.3f;
        if (warpSound != null) { warpSound.transform.position = to.position + Vector3.up * radius; warpSound.Play(); }
    }

    private void FixedUpdate()
    {
        if (a == null || b == null || spawner == null || spawner.balls == null || _prevBallA == null) return;
        int n = Mathf.Min(spawner.balls.Length, _prevBallA.Length);
        for (int i = 0; i < n; i++)
        {
            FuwaSharedBall ball = spawner.balls[i];
            if (ball == null || !ball.IsFlying()) { _prevBallA[i] = 1f; _prevBallB[i] = 1f; continue; }
            Rigidbody rb = ball.GetComponent<Rigidbody>(); if (rb == null) continue;
            Vector3 w = rb.position;
            float sa = Vector3.Dot(w - a.position, a.forward), sb = Vector3.Dot(w - b.position, b.forward);
            Transform from = null, to = null;
            if (_prevBallA[i] > 0f && sa <= 0f && InHole(a, w)) { from = a; to = b; }
            else if (_prevBallB[i] > 0f && sb <= 0f && InHole(b, w)) { from = b; to = a; }
            if (from != null)
            {
                rb.position = MapPoint(from, to, w) + to.forward * 0.1f;
                ball.transform.position = rb.position;
                rb.velocity = MapDir(from, to, rb.velocity);
                ball.Nudge(Vector3.zero);   // 持ち主はすぐ送り直す
                sa = 1f; sb = 1f;
            }
            _prevBallA[i] = sa; _prevBallB[i] = sb;
        }
    }
}
