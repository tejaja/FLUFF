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
        _on = true;
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

    private void Update()
    {
        if (_on && ball != null && ball.IsWaitingOrHidden()) { _on = false; _t = 0f; }   // アウト・チェックポイントにもどった：音なしですぐ消す
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
        rend.SetPropertyBlock(_mpb);
    }

    [Tooltip("ふわふわの中での大きさ（ふわふわのスケール 1 あたり。ふわふわの球の直径が 1）")]
    public float baseScale = 3f;
}

