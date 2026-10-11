using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// チュートリアルの練習場の区切りの透明な壁（2026-10-12 てじゃ「うっすらみえてて、あたったらふわふわは割れる、壁は消える、自分はリスポンなし」）
// ・自分のふわふわが当たったら、ふわふわは弾けて練習場のためし撃ちの場所に戻る（プレイヤーは動かさない）
// ・壁はふわっと消えて、もう出てこない（自分の画面だけ・同期なし・入り直すと戻る）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPopWall : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("消えるまでの秒数")]
    public float fadeTime = 0.4f;

    private Renderer _r;
    private Collider _c;
    private Material _m;
    private float _fadeStart = -1f;
    private float _baseAlpha;

    private void Start()
    {
        _r = GetComponent<Renderer>();
        _c = GetComponent<Collider>();
        if (_r != null) { _m = _r.material; _baseAlpha = _m.GetFloat("_AmbientAlpha"); }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_fadeStart >= 0f || ball == null || collision == null || collision.gameObject != ball.gameObject) return;
        ball.FailStay();
        _fadeStart = Time.time;
        if (_c != null) _c.enabled = false;
    }

    private void Update()
    {
        if (_fadeStart < 0f) return;
        float t = (Time.time - _fadeStart) / Mathf.Max(0.01f, fadeTime);
        if (_m != null) _m.SetFloat("_AmbientAlpha", _baseAlpha * Mathf.Clamp01(1f - t));
        if (t >= 1f) { gameObject.SetActive(false); }
    }
}
