using UdonSharp;
using UnityEngine;

// 泡の柱（ステージ2「みずのたに」、2026-10-17）。水の底の吹き出し口から泡が立ちのぼる柱（トリガーの範囲）。
// ふわふわが入るとシャボン玉みたいな泡に包まれて、柱の真ん中へ寄りながらゆっくり上へ。柱のてっぺん（トリガーの上）で
// 「ぱちん」と割れて、exitPush の向きへ放り出される（上の段へ）。途中で撃つと泡が割れて、そこから落ちる。自分の画面だけ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaBubbleColumn : UdonSharpBehaviour
{
    [Tooltip("包まれて上がる速さ(m/s)")]
    public float riseSpeed = 1.1f;
    [Tooltip("上がる速さに寄せる強さ（1/s）")]
    public float riseResponse = 4f;
    [Tooltip("柱の真ん中へ寄せる強さ（1/s²、ずれ1mあたり）と横の揺れの止まりやすさ")]
    public float centerPull = 6f;
    public float sideDamping = 3f;
    [Tooltip("てっぺんで割れた時に放り出す速さ（このオブジェクトのローカル。+Z 前、+Y 上）")]
    public Vector3 exitPush = new Vector3(0f, 0.8f, 3.6f);   // 1.2だと崖のふちに落ちた。3.0で崖の先0.5m、3.6で約1mに着水（2026-10-17）
    [Tooltip("割れた後、また包まれるまでの秒数（撃って割った時に、すぐまた捕まらないように）")]
    public float recaptureDelay = 1.2f;

    [Header("見た目・音")]
    [Tooltip("ふわふわを包む泡（球。ふだんは隠しておく）")]
    public Transform bubble;
    [Tooltip("泡の大きさ（直径 m）")]
    public float bubbleSize = 0.62f;
    [Tooltip("割れた時の粒")]
    public ParticleSystem popFx;
    public AudioSource popSound;
    [Tooltip("包まれた時の音（なくてもOK）")]
    public AudioSource captureSound;

    private FuwaBall _ball;
    private bool _captured;
    private float _lastSeen;
    private float _noCatchUntil;
    private float _wobble;
    private float _topY;

    private void Start()
    {
        Collider c = GetComponent<Collider>();
        _topY = c != null ? c.bounds.max.y : transform.position.y + 3f;
    }

    private void OnTriggerStay(Collider other)
    {
        if (other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;
        _ball = ball;
        _lastSeen = Time.time;
        if (!_captured)
        {
            if (Time.time < _noCatchUntil) return;
            _captured = true;
            _wobble = 0f;
            if (bubble != null) bubble.gameObject.SetActive(true);
            if (captureSound != null) { captureSound.pitch = Random.Range(0.95f, 1.08f); captureSound.Play(); }
        }
        // 包まれた直後に撃った（撃つと泡が割れる）
        if (ball.LastShotAge() < 0.12f && Time.time > _noCatchUntil) { Pop(false); return; }
        Vector3 v = ball.GetVelocity();
        Vector3 p = other.transform.position;
        Vector3 d = transform.position - p; d.y = 0f;
        float ay = ball.GetFallAcceleration() + (riseSpeed - v.y) * riseResponse;
        Vector3 ah = d * centerPull - new Vector3(v.x, 0f, v.z) * sideDamping;
        ball.AddWater(new Vector3(ah.x, ay, ah.z));
    }

    private void Update()
    {
        if (!_captured) return;
        // トリガーから出た（＝てっぺんまで上がった）。物理の判定は毎フレームではないので少し余裕を見る
        if (Time.time - _lastSeen > 0.12f)
        {
            bool top = _ball != null && _ball.transform.position.y > _topY - 0.4f;
            Pop(top);
            return;
        }
        if (bubble != null && _ball != null)
        {
            _wobble += Time.deltaTime;
            float s = bubbleSize * (1f + 0.04f * Mathf.Sin(_wobble * 9f));
            bubble.position = _ball.transform.position;
            bubble.localScale = new Vector3(s * (1f + 0.03f * Mathf.Sin(_wobble * 7f)), s, s * (1f - 0.03f * Mathf.Sin(_wobble * 7f)));
        }
    }

    // 割れる。top＝てっぺんまで上がって割れた（前へ放り出す）
    private void Pop(bool top)
    {
        _captured = false;
        _noCatchUntil = Time.time + recaptureDelay;
        Vector3 at = _ball != null ? _ball.transform.position : transform.position;
        if (bubble != null) bubble.gameObject.SetActive(false);
        if (popFx != null) { popFx.transform.position = at; popFx.Emit(18); }
        if (popSound != null) { popSound.transform.position = at; popSound.pitch = Random.Range(0.95f, 1.1f); popSound.Play(); }
        if (top && _ball != null) _ball.AddWater(transform.TransformDirection(exitPush) / Mathf.Max(0.001f, Time.fixedDeltaTime));
    }
}
