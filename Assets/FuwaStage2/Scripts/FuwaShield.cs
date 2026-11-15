using UdonSharp;
using UnityEngine;

// 2026-11-15 2–5 上の段：ふわふわのバリア（てじゃ「上段のワープ後に、なんらかのギミックでふわふわにバリア付与」「挙動に変化なし」「バリアっぽく」）
//  ふわふわ（FuwaSystem/FuwaBall）の子の球。バリアの門（FuwaShieldGate）をくぐると Activate、広場の出口で Deactivate
//  ふわふわがアウト・待ち（チェックポイントにもどった）・かくれた時は音なしで消える
//  ふわふわの動きは何も変えない。衝撃波（FuwaShockwave）が IsOn・Radius で影（セーフな所）を決めて、当たったら Hit で波紋
//  自分の画面だけ（ふわふわは自分のもの）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaShield : UdonSharpBehaviour
{
    public FuwaBall ball;
    public Renderer rend;
    public AudioSource onSound, offSound, blockSound;
    [Tooltip("出る・消えるのにかかる秒数")]
    public float appearTime = 0.35f, vanishTime = 0.45f;
    [Tooltip("当たりの波紋の秒数")]
    public float hitTime = 0.7f;

    private bool _on;
    private float _t;
    private float _hitAt = -10f;
    private Vector3 _hitDir = Vector3.forward;
    private MaterialPropertyBlock _mpb;

    private void Start()
    {
        _mpb = new MaterialPropertyBlock();
        if (rend != null) rend.enabled = false;
    }

    public void Activate()
    {
        if (_on) return;
        _on = true; _bounces = 0; _colNow = 0f;
        if (onSound != null) onSound.Play();
    }

    public void Deactivate()
    {
        if (!_on) return;
        _on = false;
        if (offSound != null) offSound.Play();
    }

    // 出きっている（守れる）か
    public bool IsOn() { return _on && _t > 0.5f; }
    // 球の半径（ワールド。Unity の球は直径 1）
    public float Radius() { return transform.lossyScale.x * 0.5f; }

    // dirFromSource：出どころ → ふわふわ の向き（当たるのは出どころの側）
    public void Hit(Vector3 dirFromSource)
    {
        if (dirFromSource.sqrMagnitude > 1e-6f) _hitDir = -dirFromSource.normalized;
        _hitAt = Time.time;
        if (blockSound != null) blockSound.Play();
    }

    // 2026-11-15 てじゃ「バリアの回転はふわふわより遅めに」：ふわふわの回った分の spinFollow 倍だけ回る（子なので、毎フレーム向きを上書き）
    [Tooltip("ふわふわの回転についていく割合（0＝回らない、1＝ふわふわと同じ）")]
    public float spinFollow = 0.1f;
    private Quaternion _rot = Quaternion.identity, _prevBall = Quaternion.identity;
    private bool _rotInit;

    private void LateUpdate()
    {
        if (ball == null) return;
        Quaternion br = ball.transform.rotation;
        if (!_rotInit) { _rotInit = true; _prevBall = br; _rot = br; }
        Quaternion d = br * Quaternion.Inverse(_prevBall);
        _prevBall = br;
        _rot = Quaternion.Slerp(Quaternion.identity, d, spinFollow) * _rot;
        transform.rotation = _rot;
    }

    // 2026-11-15 てじゃ「バリアは地面に接触でパリンと割れる（ふわふわが接触して無くても）」
    //  バリアの球が groundMask（道レイヤー＝道・広場の床・ふち）にふれたら割れる：すぐ消えて、かけらが飛び散る＋割れる音
    public int groundMask = 1 << 11;
    public ParticleSystem shatter;
    public int shatterCount = 46;
    public AudioSource shatterSound;

    // 2026-11-15 てじゃ「バリアが床についたら、カキンって感じでちょっと跳ねてもいいかも」「一回跳ねたらバリア黄色、2回で赤、3回でパリン」
    //  床にふれたら：上に bounceUp（横の勢いは bounceKeep 倍）で跳ねる＋カキン（回数ごとに少し高く）＋下側から波紋。1回で黄、2回で赤、3回目でパリン
    public int bouncesToBreak = 3;
    public float bounceUp = 2.6f, bounceKeep = 0.8f, bounceCool = 0.35f;
    public AudioSource bounceSound;
    public Color colorCalm = new Color(0.45f, 0.9f, 1f, 1f), colorWarn = new Color(1f, 0.85f, 0.25f, 1f), colorDanger = new Color(1f, 0.32f, 0.25f, 1f);
    private int _bounces;
    private float _bounceAt = -10f;
    private float _colNow;   // 0 水色 〜 1 黄 〜 2 赤（なめらかに追う）

    private void Bounce()
    {
        _bounceAt = Time.time;
        _bounces++;
        if (_bounces >= bouncesToBreak) { Shatter(); return; }
        Rigidbody rb = ball.GetComponent<Rigidbody>();
        Vector3 v = rb != null ? rb.velocity : Vector3.zero;
        Vector3 nv = new Vector3(v.x * bounceKeep, Mathf.Max(bounceUp, -v.y * 0.3f + bounceUp * 0.6f), v.z * bounceKeep);
        ball.LaunchTinted(nv, _bounces >= 2 ? colorDanger : colorWarn);
        _hitDir = Vector3.down; _hitAt = Time.time;
        if (bounceSound != null) { bounceSound.pitch = 1f + 0.12f * (_bounces - 1); bounceSound.Play(); }
    }

    public void Shatter()
    {
        if (!_on) return;
        _on = false; _t = 0f;
        if (shatter != null) { shatter.transform.position = transform.position; shatter.Emit(shatterCount); }
        if (shatterSound != null) shatterSound.Play();
    }

    private void Update()
    {
        if (_on && ball != null && ball.IsWaitingOrHidden()) { _on = false; _t = 0f; }   // アウト・チェックポイントにもどった：音なしですぐ消す
        if (_on && groundMask != 0 && _t > 0.3f && Time.time - _bounceAt > bounceCool && Physics.CheckSphere(transform.position, Radius() * 0.97f, groundMask, QueryTriggerInteraction.Ignore)) Bounce();
        if (_on) _colNow = Mathf.MoveTowards(_colNow, Mathf.Min(2, _bounces), Time.deltaTime * 6f);
        float target = _on ? 1f : 0f;
        if (_t < target) _t = Mathf.Min(target, _t + Time.deltaTime / Mathf.Max(0.01f, appearTime));
        else if (_t > target) _t = Mathf.Max(target, _t - Time.deltaTime / Mathf.Max(0.01f, vanishTime));
        if (rend == null) return;
        bool vis = _t > 0.001f;
        if (rend.enabled != vis) rend.enabled = vis;
        if (!vis) return;
        // 出る時はちょっと大きめからぽよんと（消える時はそのまま）
        float s = _on ? 1f + 0.18f * (1f - _t) * (1f - _t) : 1f;
        transform.localScale = Vector3.one * (baseScale * s);
        float ht = Mathf.Clamp01((Time.time - _hitAt) / Mathf.Max(0.01f, hitTime));
        rend.GetPropertyBlock(_mpb);
        _mpb.SetFloat("_Appear", _t);
        _mpb.SetVector("_HitDir", new Vector4(_hitDir.x, _hitDir.y, _hitDir.z, 0f));
        _mpb.SetFloat("_HitT", ht);
        Color rc = _colNow <= 1f ? Color.Lerp(colorCalm, colorWarn, _colNow) : Color.Lerp(colorWarn, colorDanger, _colNow - 1f);
        _mpb.SetColor("_Color", rc);
        Color fc = rc * 0.75f; fc.a = 0.08f + 0.04f * _colNow;
        _mpb.SetColor("_Fill", fc);
        rend.SetPropertyBlock(_mpb);
    }

    [Tooltip("ふわふわの中での大きさ（ふわふわのスケール 1 あたり。ふわふわの球の直径が 1）")]
    public float baseScale = 3f;
}

