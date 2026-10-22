using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 水車エレベーター（コース2–1「みずのこみち」のラスト、2026-10-22 てじゃ「ためしにやってみよ」）。
// 水車がゆっくり回り、まわりのバケツは手前（来た方）側を上っていく。水車の下の水に浮いたふわふわがバケツに近づくと乗って運ばれ、
// てっぺんを越えた所で降ろされて、その場の空中で待つ（チェックポイントからの再開と同じ待ち方）。そこから撃ってゴールの広場へ。
// 回る角度はサーバーの時刻で決める（みんな同じに見える）。運ぶのは自分のふわふわだけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWaterwheelLift : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("回す水車（ローカル +Z が軸。見た目だけ）")]
    public Transform wheel;
    [Tooltip("バケツ（上を向いたまま輪の上を動く）")]
    public Transform[] buckets;
    [Tooltip("軸からバケツの中心までの距離(m)")]
    public float bucketRadius = 3f;
    [Tooltip("回る速さ（度/秒）")]
    public float speed = 16f;
    [Tooltip("バケツに乗った時のふわふわの高さ（バケツの中心から上へ m）")]
    public float holdUp = 0.35f;
    [Tooltip("乗れる距離（輪の面の中, m）と、軸の向きのずれ（m）")]
    public float catchRadius = 0.85f;
    public float catchHalfWidth = 1.3f;
    [Tooltip("いちばん下からこの角度（度）の中のバケツにだけ乗る")]
    public float catchArc = 75f;
    [Tooltip("てっぺんを越えてこの角度（度）で降ろす")]
    public float releasePast = 8f;
    [Tooltip("降ろしたあと、また乗るまでの秒数")]
    public float recatchDelay = 1.5f;
    public AudioSource catchSound;
    public AudioSource releaseSound;

    private int _carry = -1;
    private float _noCatchUntil;
    private Vector3 _axis, _up, _near;

    private void Start()
    {
        if (wheel == null) return;
        _axis = wheel.forward;
        _up = Vector3.up;
        // 手前（上っていく側）：水車の軸と上の向きに直角で、来た方。wheel の親から見た -Z の向きを手前にする（ビルダーがそう置く）
        Vector3 n = wheel.parent != null ? -wheel.parent.forward : Vector3.back;
        n = n - _axis * Vector3.Dot(n, _axis); n.y = 0f;
        _near = n.sqrMagnitude > 1e-4f ? n.normalized : Vector3.Cross(_axis, Vector3.up).normalized;
    }

    // 角度 a（0＝いちばん下、90＝手前の横、180＝てっぺん）のバケツの位置
    private Vector3 BucketPos(float a)
    {
        float r = a * Mathf.Deg2Rad;
        return wheel.position + (-_up * Mathf.Cos(r) + _near * Mathf.Sin(r)) * bucketRadius;
    }

    private void Update()
    {
        if (wheel == null || buckets == null || buckets.Length == 0) return;
        int n = buckets.Length;
        float baseA = (float)((Networking.GetServerTimeInSeconds() * speed) % 360.0);
        // 水車の見た目：ローカル下（-Y）がバケツ0の向き
        Vector3 d0 = (BucketPos(baseA) - wheel.position).normalized;
        wheel.rotation = Quaternion.LookRotation(_axis, -d0);
        for (int i = 0; i < n; i++)
        {
            if (buckets[i] == null) continue;
            buckets[i].position = BucketPos(baseA + i * 360f / n);
        }

        if (ball == null) return;
        // 運んでいる：バケツについていく。てっぺんを越えたら降ろして空中で待たせる
        if (_carry >= 0)
        {
            if (!ball.IsCarried()) { _carry = -1; return; }
            float a = Mathf.Repeat(baseA + _carry * 360f / n, 360f);
            Vector3 hold = buckets[_carry].position + Vector3.up * holdUp;
            ball.CarryTo(hold);
            if (a >= 180f + releasePast && a < 270f)
            {
                ball.EndCarryWaiting();
                _carry = -1;
                _noCatchUntil = Time.time + recatchDelay;
                if (releaseSound != null) { releaseSound.transform.position = hold; releaseSound.Play(); }
            }
            return;
        }
        if (Time.time < _noCatchUntil || !ball.gameObject.activeInHierarchy || ball.IsWaitingOrHidden() || ball.IsCarried()) return;
        Vector3 bp = ball.transform.position;
        for (int i = 0; i < n; i++)
        {
            float a = Mathf.Repeat(baseA + i * 360f / n, 360f);
            if (a > catchArc && a < 360f - catchArc) continue;
            Vector3 d = bp - (buckets[i].position + Vector3.up * holdUp);
            float along = Vector3.Dot(d, _axis);
            if (Mathf.Abs(along) > catchHalfWidth) continue;
            if ((d - _axis * along).magnitude > catchRadius) continue;
            if (!ball.BeginCarry()) return;
            _carry = i;
            if (catchSound != null) { catchSound.transform.position = bp; catchSound.Play(); }
            return;
        }
    }

    private void OnDisable()
    {
        // エリアが隠れた：運んでいたら、その場で待たせる
        if (_carry >= 0 && ball != null && ball.IsCarried()) ball.EndCarryWaiting();
        _carry = -1;
    }
}
