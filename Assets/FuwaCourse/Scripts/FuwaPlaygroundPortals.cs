using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// あそびばのおもちゃ（コース2–5「そらのいせき」のクリアで出る）：ポータル2つ1組（2026-11-16 てじゃ「2-5 ポータル」）
// a・b は立っている輪（+forward が表＝入る側、穴は local の |x|,|y-radius| が radius の円の中）。表から裏へくぐると、もう片方の表から出てくる
//  変換：p' = 出口 · R180y · 入口⁻¹ · p（向き・速さもそのまま運ぶ）
// 人：頭が面をこえた瞬間に体ごと移す（自分の画面）。同期ふわふわ：全員の画面で同じように移す（持ち主は送り直す）
// 2026-11-27 てじゃ「ポータルもオンオフ式に、デフォはオフ、持ってUSEでオン、片方切り替えたら両方反応」：
//  _on（同期）でオン/オフ。オフの間はくぐってもワープしない。穴の見た目（visuals）は大きさ 0→1 でふわっと開く・閉じる。向こうを映すカメラ（cams）もオフの間は止める
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaPlaygroundPortals : UdonSharpBehaviour
{
    public Transform a, b;
    [Tooltip("開く・閉じる見た目（穴の渦・裏）。大きさ 0→元 で開く")]
    public Transform[] visuals;
    [Tooltip("向こうを映すカメラ一式（オフの間は止める）")]
    public GameObject cams;
    public float openSpeed = 1.6f;
    [Tooltip("くぐった判定：面からこの距離（m、奥行き）以内の時だけ")]
    public float maxDepth = 1f;
    public AudioSource onSound, offSound;
    [UdonSynced] private bool _on;
    private float _k = -1f;
    private Vector3[] _visBase;

    public void Toggle()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _on = !_on;
        RequestSerialization();
        AudioSource s = _on ? onSound : (offSound != null ? offSound : onSound);
        if (s != null) s.Play();
    }

    private void UpdateOpen()
    {
        if (_visBase == null)
        {
            int n = visuals != null ? visuals.Length : 0;
            _visBase = new Vector3[n];
            for (int i = 0; i < n; i++) if (visuals[i] != null) _visBase[i] = visuals[i].localScale;
        }
        float target = _on ? 1f : 0f;
        if (_k == target) return;
        _k = _k < 0f ? target : Mathf.MoveTowards(_k, target, openSpeed * Time.deltaTime);
        float e = _k * _k * (3f - 2f * _k);
        for (int i = 0; i < _visBase.Length; i++)
        {
            Transform t = visuals[i]; if (t == null) continue;
            bool vis = e > 0.001f;
            if (t.gameObject.activeSelf != vis) t.gameObject.SetActive(vis);
            t.localScale = _visBase[i] * Mathf.Max(0.001f, e);
        }
        if (cams != null && cams.activeSelf != (e > 0.001f)) cams.SetActive(e > 0.001f);
    }
    [Tooltip("穴（だ円）：中心の高さ・半はば・半高さ（コースのポータルと同じ形。2026-11-16 てじゃ「コースと見た目揃えて」）")]
    public float holeCenterY = 1.85f, halfW = 0.85f, halfH = 1.55f;
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
        float dx = l.x / halfW, dy = (l.y - holeCenterY) / halfH;
        // 2026-11-27 奥行きも見る（2–5 の FuwaPortal と同じ不具合：一瞬で遠くへ移った時に、面の奥の「穴の延長」にいるとくぐった扱いになっていた）
        if (Mathf.Abs(l.z) > maxDepth) return false;
        return dx * dx + dy * dy <= 1f;
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
        UpdateOpen();
        if (a == null || b == null) return;
        if (_k < 0.9f) { _prevHeadA = 1f; _prevHeadB = 1f; return; }   // オフ・開いている途中はワープしない
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
        if (warpSound != null) { warpSound.transform.position = to.position + Vector3.up * holeCenterY; warpSound.Play(); }
    }

    private void FixedUpdate()
    {
        if (a == null || b == null || spawner == null || spawner.balls == null || _prevBallA == null) return;
        int n = Mathf.Min(spawner.balls.Length, _prevBallA.Length);
        if (_k < 0.9f) { for (int i = 0; i < n; i++) { _prevBallA[i] = 1f; _prevBallB[i] = 1f; } return; }
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
