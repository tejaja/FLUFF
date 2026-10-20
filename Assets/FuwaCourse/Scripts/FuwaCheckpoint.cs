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

    private bool _inited;

    private void Start()
    {
        Init();
    }

    // コースは普段隠してあって Start がまだのうちに ResetGlow が来ることがあるので、先にここで元の色を覚える
    private void Init()
    {
        if (_inited) return;
        _inited = true;
        // 光る前の色を覚えておく（コースをやり直した時に戻す）
        if (indicator != null) _indicatorOrig = indicator.material.color;
        if (glowRenderers == null) return;
        _glowOrig = new Color[glowRenderers.Length];
        for (int i = 0; i < glowRenderers.Length; i++) if (glowRenderers[i] != null) _glowOrig[i] = glowRenderers[i].material.GetColor("_EmissionColor");
    }

    // byPlayer：自分がこのコースを走っている間、チェックポイントの線を越えたら更新（1回だけ。やり直しで ResetGlow が戻す）
    private void CheckPlayer()
    {
        if (_playerPassed || playerBall == null) return;
        if (playerBall.startPoint == null || playerBall.startPoint.root != transform.root) return;   // 今このコースにいない
        VRC.SDKBase.VRCPlayerApi lp = VRC.SDKBase.Networking.LocalPlayer;
        if (lp == null) return;
        Transform rp = respawnPoint != null ? respawnPoint : transform;
        Vector3 d = lp.GetPosition() - transform.position;
        if (Mathf.Abs(d.y) > 6f) return;
        Vector3 fwd = rp.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 flat = d; flat.y = 0f;
        if (flat.magnitude > playerRadius || Vector3.Dot(flat, fwd) < 0f) return;
        _playerPassed = true;
        Reach(playerBall);
    }

    // 光を消して、まだ通っていない状態に戻す（FuwaBall がスタートからやり直す時に呼ぶ）
    public void ResetGlow()
    {
        Init();
        _playerPassed = false;
        _glowStart = -1f;
        _glowFrom = null;
        if (_glowOrig != null)
            for (int i = 0; i < glowRenderers.Length; i++) if (glowRenderers[i] != null) glowRenderers[i].material.SetColor("_EmissionColor", _glowOrig[i]);
        if (glowEffects != null)
            for (int i = 0; i < glowEffects.Length; i++) if (glowEffects[i] != null) { glowEffects[i].Stop(); glowEffects[i].Clear(); }
        if (indicator != null) indicator.material.color = _indicatorOrig;
    }

    [Header("プレイヤーが通ったら更新（ふわふわではなく）")]
    [Tooltip("チュートリアルの前半など：ふわふわが先に飛んでチェックポイントを通り、本人はスタート付近で落ちたのに先へ戻される、を防ぐ（2026-10-20 てじゃ）")]
    public bool byPlayer;
    [Tooltip("byPlayer の時に使う、自分のふわふわ")]
    public FuwaBall playerBall;
    [Tooltip("byPlayer の時：チェックポイントの横 この距離(m)以内で、再開位置の向きの線を越えたら通ったことにする（トリガーより道が広いので）")]
    public float playerRadius = 9f;
    private bool _playerPassed;

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || byPlayer) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;
        Reach(ball);
    }

    private void Reach(FuwaBall ball)
    {
        Init();
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
        if (byPlayer) CheckPlayer();
        if (_glowStart < 0f || _glowFrom == null) return;
        float k = Mathf.Clamp01((Time.time - _glowStart) / Mathf.Max(0.01f, glowFadeTime));
        // ぼわっと：最初に少し明るすぎるくらいまで行って落ち着く
        float b = k < 1f ? Mathf.SmoothStep(0f, 1f, k) * (1f + 0.35f * Mathf.Sin(k * Mathf.PI)) : 1f;
        for (int i = 0; i < glowRenderers.Length; i++)
            if (glowRenderers[i] != null) glowRenderers[i].material.SetColor("_EmissionColor", Color.LerpUnclamped(_glowFrom[i], glowColor, b));
        if (k >= 1f) _glowFrom = null;
    }
}
