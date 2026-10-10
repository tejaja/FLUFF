using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// やみのもりの木の葉の奥からのぞく目。黒目が自分（ローカルプレイヤーの頭）を目で追う。ときどき両目でまばたき。
// ルートの +Z が外向き（見る人の側）。光る目（glows）は横長の楕円体、黒目（pupils）はその表面に沿って動く。
// 見た目だけなので同期なし。遠い時（farRange より外）は何もしない
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWatchEyes : UdonSharpBehaviour
{
    public Transform[] glows;
    public Transform[] pupils;
    [Tooltip("これより近いと目で追う(m)")]
    public float watchRange = 40f;
    [Tooltip("これより遠いと動かさない(m)")]
    public float farRange = 70f;
    [Tooltip("光る目の半径（ルートの座標で 横・縦・奥行き）")]
    public Vector3 glowHalf = new Vector3(0.07f, 0.045f, 0.0175f);
    [Tooltip("黒目の半径（横・縦）")]
    public Vector2 pupilHalf = new Vector2(0.015f, 0.036f);
    [Tooltip("黒目が向きに合わせて動く速さ")]
    public float follow = 6f;
    public float blinkMin = 2.5f, blinkMax = 6.5f, blinkTime = 0.2f;

    private Vector3[] _center;
    private Vector3[] _glowScale, _pupilScale;
    private Vector2 _look;
    private float _nextBlink, _blinkStart = -10f;
    private Vector2 _idle; private float _nextIdle;

    void Start()
    {
        int n = glows.Length;
        _center = new Vector3[n]; _glowScale = new Vector3[n]; _pupilScale = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            _center[i] = glows[i].localPosition;
            _glowScale[i] = glows[i].localScale;
            _pupilScale[i] = pupils[i].localScale;
        }
        _nextBlink = Time.time + Random.Range(blinkMin, blinkMax);
        Apply(1f);
    }

    void Update()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        Vector3 head = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        float dist = Vector3.Distance(head, transform.position);
        if (dist > farRange) return;

        // 見たい向き（ルートの座標で、外向き +Z を正面として横・縦）
        Vector2 want;
        if (dist < watchRange)
        {
            Vector3 d = transform.InverseTransformPoint(head).normalized;
            want = new Vector2(d.x, d.y) / Mathf.Max(0.35f, d.z + 0.35f);
        }
        else
        {
            // 遠い時はきょろきょろ
            if (Time.time > _nextIdle) { _idle = new Vector2(Random.Range(-1f, 1f), Random.Range(-0.6f, 0.6f)); _nextIdle = Time.time + Random.Range(1.2f, 3f); }
            want = _idle;
        }
        _look = Vector2.Lerp(_look, want, 1f - Mathf.Exp(-follow * Time.deltaTime));

        // まばたき（両目いっしょ）
        float open = 1f;
        if (Time.time > _nextBlink) { _blinkStart = Time.time; _nextBlink = Time.time + Random.Range(blinkMin, blinkMax); }
        float p = (Time.time - _blinkStart) / blinkTime;
        if (p >= 0f && p < 1f) open = 1f - 0.92f * Mathf.Sin(p * Mathf.PI);
        Apply(open);
    }

    private void Apply(float open)
    {
        float mx = glowHalf.x - pupilHalf.x - 0.006f, my = Mathf.Max(0.002f, glowHalf.y - pupilHalf.y);
        float ox = _look.x * mx, oy = _look.y * my;
        float e = (ox * ox) / (mx * mx) + (oy * oy) / (my * my);
        if (e > 1f) { float k = 1f / Mathf.Sqrt(e); ox *= k; oy *= k; }
        oy *= open;
        float u = (ox * ox) / (glowHalf.x * glowHalf.x) + (oy * oy) / (glowHalf.y * glowHalf.y);
        float zs = glowHalf.z * Mathf.Sqrt(Mathf.Max(0f, 1f - u)) + 0.002f;
        for (int i = 0; i < glows.Length; i++)
        {
            Vector3 gs = _glowScale[i]; gs.y *= open; glows[i].localScale = gs;
            Vector3 ps = _pupilScale[i]; ps.y *= open; pupils[i].localScale = ps;
            pupils[i].localPosition = _center[i] + new Vector3(ox, oy, zs);
        }
    }
}
