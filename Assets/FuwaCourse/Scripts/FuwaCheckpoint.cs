using UdonSharp;
using UnityEngine;

// ふわふわが通過したら、次からここで再開する。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaCheckpoint : UdonSharpBehaviour
{
    [Tooltip("再開位置（空ならこのオブジェクトの位置）")]
    public Transform respawnPoint;
    [Tooltip("通過したら色を変える見た目（なくてもOK）")]
    public Renderer indicator;
    public Color reachedColor = new Color(0.4f, 1f, 0.5f, 1f);
    [Tooltip("通過したらぼわっと光らせる見た目（光るキノコなど。マテリアルの _EmissionColor を変える）")]
    public Renderer[] glowRenderers;
    [ColorUsage(false, true)] public Color glowColor = new Color(0.55f, 1.6f, 0.65f, 1f);
    [Tooltip("光り始めてから明るくなりきるまでの秒数")]
    public float glowFadeTime = 0.6f;
    [Tooltip("光った時に再生するエフェクト（胞子など）")]
    public ParticleSystem[] glowEffects;

    private Color[] _glowFrom;
    private float _glowStart = -1f;
    private Color[] _glowOrig;
    private Color _indicatorOrig = Color.white;

    private void Start()
    {
        // 光る前の色を覚えておく（コースをやり直した時に戻す）
        if (indicator != null) _indicatorOrig = indicator.material.color;
        if (glowRenderers == null) return;
        _glowOrig = new Color[glowRenderers.Length];
        for (int i = 0; i < glowRenderers.Length; i++) if (glowRenderers[i] != null) _glowOrig[i] = glowRenderers[i].material.GetColor("_EmissionColor");
    }

    // 光を消して、まだ通っていない状態に戻す（FuwaBall がスタートからやり直す時に呼ぶ）
    public void ResetGlow()
    {
        _glowStart = -1f;
        _glowFrom = null;
        if (_glowOrig != null)
            for (int i = 0; i < glowRenderers.Length; i++) if (glowRenderers[i] != null) glowRenderers[i].material.SetColor("_EmissionColor", _glowOrig[i]);
        if (glowEffects != null)
            for (int i = 0; i < glowEffects.Length; i++) if (glowEffects[i] != null) { glowEffects[i].Stop(); glowEffects[i].Clear(); }
        if (indicator != null) indicator.material.color = _indicatorOrig;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;
        ball.SetCheckpoint(respawnPoint != null ? respawnPoint : transform);
        if (indicator != null) indicator.material.color = reachedColor;
        if (glowRenderers != null && glowRenderers.Length > 0 && _glowStart < 0f)
        {
            _glowFrom = new Color[glowRenderers.Length];
            for (int i = 0; i < glowRenderers.Length; i++) if (glowRenderers[i] != null) _glowFrom[i] = glowRenderers[i].material.GetColor("_EmissionColor");
            _glowStart = Time.time;
            if (glowEffects != null)
                for (int i = 0; i < glowEffects.Length; i++) if (glowEffects[i] != null) glowEffects[i].Play();
        }
    }

    private void Update()
    {
        if (_glowStart < 0f || _glowFrom == null) return;
        float k = Mathf.Clamp01((Time.time - _glowStart) / Mathf.Max(0.01f, glowFadeTime));
        // ぼわっと：最初に少し明るすぎるくらいまで行って落ち着く
        float b = k < 1f ? Mathf.SmoothStep(0f, 1f, k) * (1f + 0.35f * Mathf.Sin(k * Mathf.PI)) : 1f;
        for (int i = 0; i < glowRenderers.Length; i++)
            if (glowRenderers[i] != null) glowRenderers[i].material.SetColor("_EmissionColor", Color.LerpUnclamped(_glowFrom[i], glowColor, b));
        if (k >= 1f) _glowFrom = null;
    }
}
