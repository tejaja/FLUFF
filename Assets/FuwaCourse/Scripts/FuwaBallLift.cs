using UdonSharp;
using UnityEngine;

// ふわふわを運ぶ上昇気流：このトリガーに自分のふわふわが入ったら、points を順にたどって運び、
// 最後の点の空中で止めて待たせる（撃つとまた動き出す）。運ばれている間はゆらゆら揺れて回る。（自分の画面だけ）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaBallLift : UdonSharpBehaviour
{
    [Tooltip("たどる点（順番に）。最後の点で止まって待つ")]
    public Transform[] points;
    [Tooltip("運ぶ速さ(m/s)")]
    public float speed = 1.6f;
    [Tooltip("運び始めに、今いる所から最初の点まで寄せる速さ(m/s)")]
    public float catchSpeed = 2.5f;
    [Tooltip("ゆらゆら揺れる幅(m)")]
    public float sway = 0.12f;

    private FuwaBall _ball;
    private int _seg = -1;          // -1=最初の点へ寄せている途中
    private float _t;               // 今の区間の進み(m)
    private Vector3 _from;
    private float _time;

    private void OnTriggerEnter(Collider other)
    {
        if (_ball != null || other == null || points == null || points.Length == 0) return;
        FuwaBall b = other.GetComponent<FuwaBall>();
        if (b == null || !b.BeginCarry()) return;
        _ball = b;
        _seg = -1;
        _t = 0f;
        _time = 0f;
        _from = b.transform.position;
    }

    private void Update()
    {
        if (_ball == null) return;
        // アウトになった・スタートからやり直した等で運ぶのが中断された
        if (!_ball.IsCarried()) { _ball = null; return; }
        _time += Time.deltaTime;

        Vector3 a, b; float spd;
        if (_seg < 0) { a = _from; b = points[0].position; spd = catchSpeed; }
        else { a = points[_seg].position; b = points[_seg + 1].position; spd = speed; }
        float len = Vector3.Distance(a, b);
        _t += spd * Time.deltaTime;
        if (_t >= len)
        {
            _t -= len;
            _seg++;
            if (_seg >= points.Length - 1)
            {
                _ball.CarryTo(points[points.Length - 1].position);
                _ball.EndCarryWaiting();
                _ball = null;
                return;
            }
            a = points[_seg].position; b = points[_seg + 1].position; len = Vector3.Distance(a, b);
        }
        float k = len > 0.0001f ? Mathf.Clamp01(_t / len) : 1f;
        k = k * k * (3f - 2f * k);   // 区間ごとに、ゆっくり出てゆっくり着く
        Vector3 p = Vector3.Lerp(a, b, k);
        // 風に吹かれているように、ゆらゆら（終わりに近づくほど小さく）
        float fade = _seg >= points.Length - 2 ? 1f - k : 1f;
        p += new Vector3(Mathf.Sin(_time * 2.3f), Mathf.Sin(_time * 3.1f) * 0.4f, Mathf.Cos(_time * 1.9f)) * sway * fade;
        _ball.CarryTo(p);
        _ball.transform.Rotate(new Vector3(0.4f, 1f, 0.2f), 140f * Time.deltaTime, Space.World);
    }
}
