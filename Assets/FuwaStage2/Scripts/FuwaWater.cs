using UdonSharp;
using UnityEngine;

// 水（ステージ2「みずのたに」、2026-10-17）。トリガーの範囲が水。ふわふわは水に浮く＝水はいつでもセーフ（てじゃ）。
// ・浮力：ふわふわの真ん中を水面より floatHeight だけ上に保つ（ばねと水の抵抗で、ぷかぷか揺れておさまる）
// ・流れ：水面の向き flowDirection（このオブジェクトのローカル）へ flowSpeed m/s で運ぶ。横の速さを流れの速さへ寄せる
// ・水面の高さは surface（なければトリガーの上の面）。水の底は地面（地面に当たるとアウトなので、浮力で底には届かない深さにする）
// ・人には何もしない（浅瀬は歩ける。深い所は下の落下キャッチで）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWater : UdonSharpBehaviour
{
    [Tooltip("水面（この高さ）。空ならトリガーの上の面")]
    public Transform surface;
    [Tooltip("ふわふわの真ん中を水面からどれだけ上に浮かせるか（半径に対する割合。0=半分沈む、1=水面にちょうど乗る）")]
    [Range(-0.5f, 1.5f)] public float floatHeight = 0.85f;   // 2026-10-17 0.35 だと毛の下半分が水に沈んで見えた→てじゃ「もっと浮いていい」（毛の先まで入れた見た目の下2割くらいが水の中）
    [Tooltip("浮力のばねの強さ（1/s²）。大きいほど早く水面へ戻る")]
    public float buoyancy = 14f;
    [Tooltip("上下の揺れの止まりやすさ（1/s）")]
    public float verticalDamping = 3.5f;

    [Header("流れ（flowSpeed=0 なら止まった水）")]
    [Tooltip("流れの向き（このオブジェクトのローカル。水平に直して使う）")]
    public Vector3 flowDirection = Vector3.forward;
    [Tooltip("流れの速さ(m/s)")]
    public float flowSpeed = 0f;
    [Tooltip("横の速さを流れに寄せる強さ（1/s）。止まった水でも横の動きを少しずつ止める（水の抵抗）")]
    public float flowResponse = 1.2f;

    [Header("落ちた時の「ちゃぽん」（ステージ2で1つの FuwaWaterFx。なくてもOK）")]
    public FuwaWaterFx fx;
    [Tooltip("この速さ(m/s)以上で下向きに入った時だけ鳴らす")]
    public float splashMinSpeed = 0.15f;

    private Collider _col;

    private void Start()
    {
        _col = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (fx == null || other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;
        float vy = ball.GetVelocity().y;
        if (vy > -splashMinSpeed) return;
        Vector3 p = other.transform.position;
        // 落ちる速さの上限（ふわふわは 0.7m/s）でいっぱい。滝で叩きつけられた時はもっと速い
        fx.Splash(new Vector3(p.x, SurfaceY() + 0.02f, p.z), Mathf.InverseLerp(splashMinSpeed, 1.2f, -vy));
    }

    private float SurfaceY()
    {
        if (surface != null) return surface.position.y;
        if (_col != null) return _col.bounds.max.y;
        return transform.position.y;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;
        float r = 0.3f;
        SphereCollider sc = other.GetComponent<SphereCollider>();
        if (sc != null) r = sc.radius * other.transform.lossyScale.x;
        Vector3 p = other.transform.position;
        Vector3 v = ball.GetVelocity();
        float sy = SurfaceY();
        float target = sy + r * floatHeight;
        float depth = target - p.y;   // 正なら沈みすぎ
        // 水に触れている割合：玉の下のはしが水面＝0、浮かせたい高さ＝1（それより沈めば1のまま）。水より上は何もしない
        float touch = Mathf.Clamp01((sy + r - p.y) / Mathf.Max(0.01f, r * (1f - floatHeight)));
        if (touch <= 0f) return;
        // 重力を打ち消す（浮かせたい高さでちょうど釣り合う）＋ばねで戻す＋上下の揺れを止める
        float ay = ball.GetFallAcceleration() * touch + buoyancy * depth * touch - verticalDamping * v.y * touch;
        Vector3 flow = transform.TransformDirection(flowDirection); flow.y = 0f;
        Vector3 flowVel = flow.sqrMagnitude > 0.0001f ? flow.normalized * flowSpeed : Vector3.zero;
        Vector3 vh = new Vector3(v.x, 0f, v.z);
        // 玉の空気抵抗（Rigidbody.drag）が流れに逆らうので、水の中では横のぶんを打ち消して、流れの速さどおりに運ぶ
        Vector3 ah = ((flowVel - vh) * flowResponse + vh * ball.GetDrag()) * touch;
        ball.AddWater(new Vector3(ah.x, ay, ah.z));
    }
}

