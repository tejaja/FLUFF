using UdonSharp;
using UnityEngine;

// 2–4 はぐるまのとう：ふわふわを運ぶ鳥（2026-11-06 仮。てじゃ「ふわふわは何かに上空で運ばれる、急いで橋を渡して追いかける」）
// ・止まり木で待っていて、自分のふわふわが足の近くに来たらつかむ → 決まった道すじ（path）を飛んで、終わりで放す → 止まり木へ戻る
// ・自分の画面だけ（同期なし）。ふわふわも自分の物だけ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaBird : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("動かす見た目のまとまり（体の中心）")]
    public Transform body;
    public Transform wingL, wingR;
    [Tooltip("飛ぶ道すじ（ワールドの点）。0 番目が止まり木、最後が放す所")]
    public Vector3[] path;
    [Tooltip("ふわふわを運ぶ速さ (m/s)")]
    public float carrySpeed = 1.7f;
    public float returnSpeed = 5f;
    [Tooltip("つかめる距離（足から）")]
    public float grabRadius = 1.1f;
    [Tooltip("足の位置（体から）")]
    public Vector3 footOffset = new Vector3(0f, -0.85f, 0f);
    [Tooltip("放した時のふわふわの速さ")]
    public Vector3 dropVelocity = new Vector3(0f, -0.2f, 0f);
    [Tooltip("戻ってから次につかめるまで（秒）")]
    public float cooldown = 1.0f;

    private int _state;      // 0 待つ 1 運ぶ 2 戻る
    private float _s, _len, _readyAt;
    private float[] _acc;

    private void Start()
    {
        if (path == null || path.Length < 2) return;
        _acc = new float[path.Length];
        for (int i = 1; i < path.Length; i++) _acc[i] = _acc[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        _len = _acc[path.Length - 1];
        Place(0f, 0f);
    }

    private Vector3 At(float s)
    {
        s = Mathf.Clamp(s, 0f, _len);
        for (int i = 1; i < path.Length; i++)
        {
            if (s <= _acc[i] || i == path.Length - 1)
            {
                float seg = _acc[i] - _acc[i - 1];
                float t = seg > 1e-5f ? (s - _acc[i - 1]) / seg : 0f;
                return Vector3.Lerp(path[i - 1], path[i], t);
            }
        }
        return path[path.Length - 1];
    }

    // s の位置に置いて、進む向きを向く（dir = +1 行き、-1 帰り）
    private void Place(float s, float dir)
    {
        if (body == null) return;
        Vector3 p = At(s);
        body.position = p;
        Vector3 f = dir == 0f ? At(0.5f) - At(0f) : (At(s + 0.3f * dir) - At(s - 0.3f * dir));
        f.y = 0f;
        if (f.sqrMagnitude > 1e-6f) body.rotation = Quaternion.Slerp(body.rotation, Quaternion.LookRotation(f.normalized, Vector3.up), dir == 0f ? 1f : 0.2f);
    }

    private void Update()
    {
        if (path == null || _acc == null || body == null) return;
        float dt = Time.deltaTime;
        // 羽ばたき：待つ時はゆっくり小さく、飛ぶ時は速く大きく
        float flap = _state == 0 ? Mathf.Sin(Time.time * 3f) * 8f : Mathf.Sin(Time.time * 14f) * 38f;
        if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, 0f, 10f + flap);
        if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, 0f, -10f - flap);

        if (_state == 0)
        {
            Vector3 p = At(0f) + Vector3.up * (Mathf.Sin(Time.time * 1.6f) * 0.05f);
            body.position = p;
            if (ball == null || Time.time < _readyAt || !ball.gameObject.activeInHierarchy || ball.IsCarried()) return;
            Vector3 foot = body.position + footOffset;
            if (Vector3.Distance(ball.transform.position, foot) > grabRadius) return;
            if (!ball.BeginCarry()) return;
            _state = 1; _s = 0f;
            return;
        }
        if (_state == 1)
        {
            // まだ運んでいるか（アウト・リセットで離れたら戻る）
            if (ball == null || !ball.IsCarried()) { _state = 2; return; }
            _s += carrySpeed * dt;
            // ふわっと上下
            Place(_s, 1f);
            body.position += Vector3.up * Mathf.Sin(_s * 1.3f) * 0.25f;
            ball.CarryTo(body.position + footOffset);
            if (_s >= _len)
            {
                ball.EndCarryDrop(dropVelocity);
                _state = 2;
            }
            return;
        }
        // 戻る
        _s -= returnSpeed * dt;
        Place(_s, -1f);
        if (_s <= 0f) { _s = 0f; _state = 0; _readyAt = Time.time + cooldown; Place(0f, 0f); }
    }
}
