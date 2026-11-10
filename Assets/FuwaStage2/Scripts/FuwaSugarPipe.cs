using UdonSharp;
using UnityEngine;

// 2–4 はぐるまのとう：色ざらめのパイプ（2026-11-08 てじゃ「周りの自動で回ってる歯車の末端も、何かしらの形でつなげて」→ A 色ざらめパイプ）
// ・しかけの最後の歯車が動かすポンプ（FuwaCloudFactory）がひと押しするたびに Send() → パイプの中をざらめのふくらみ（bulges）が流れて、まん中の雲のバルーンへ（はじめはわたあめ機の鍋）
// ・パイプの道すじは pts（世界の点、つなぎ目）。ふくらみは使い回し。着いたら endPuff（その色の雲の糸）を出す
// 自分の画面だけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSugarPipe : UdonSharpBehaviour
{
    public Vector3[] pts;
    public float speed = 4f;
    public Transform[] bulges;
    public ParticleSystem endPuff;
    [Tooltip("着いたら Puff() を呼ぶ（まん中の雲のバルーン、2026-11-08）")]
    public FuwaCloudBalloon balloon;
    [Tooltip("途中のタンクのクリスタル（ふくらみが popAt を通るときらっと回ってふくらむ、2026-11-08）")]
    public Transform pop;
    public float popAt = -1f;
    public float popSpin = 25f;
    [Tooltip("ポンプが押しこむたび（Send）に光る物（タンクのクリスタル・窓、2026-11-08 てじゃ「押し込むたびに光らせよう」）")]
    public Renderer[] glows;
    [ColorUsage(false, true)] public Color glowColor = new Color(0.9f, 1.6f, 2.6f);
    public float glowFade = 1.6f;
    public int endCount = 10;
    [Tooltip("0より大きいと、この秒ごとに自分で Send（2–4 の回るタンクから気球へのパイプ、2026-11-10）")]
    public float autoInterval = 0f;
    [Tooltip("ふくらみがタンクに入ると振れる圧力計の針（local z まわり、2026-11-10）")]
    public Transform needle;
    [Tooltip("着いた時に気球から出る雲の色（2026-11-10、色つきタンクの管はその液の色）")]
    public Color cloudColor = Color.white;
    public float needleKick = 70f;
    private Quaternion _needleBase;
    private float _autoT;

    private float[] _cum;
    private float[] _at;
    private float _total;
    private float _pop;
    private Vector3 _popScale;
    private float _glow;
    private bool _glowOn;
    private Color[] _glowBase;
    private MaterialPropertyBlock _mpb;

    private void Start()
    {
        int n = pts != null ? pts.Length : 0;
        _cum = new float[Mathf.Max(1, n)];
        for (int i = 1; i < n; i++) _cum[i] = _cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
        _total = n > 0 ? _cum[n - 1] : 0f;
        int b = bulges != null ? bulges.Length : 0;
        _at = new float[b];
        if (pop != null) _popScale = pop.localScale;
        if (needle != null) _needleBase = needle.localRotation;
        int g = glows != null ? glows.Length : 0;
        _glowBase = new Color[g];
        for (int i = 0; i < g; i++) if (glows[i] != null && glows[i].sharedMaterial != null && glows[i].sharedMaterial.HasProperty("_EmissionColor")) _glowBase[i] = glows[i].sharedMaterial.GetColor("_EmissionColor");
        _mpb = new MaterialPropertyBlock();
        for (int i = 0; i < b; i++) { _at[i] = -1f; if (bulges[i] != null) bulges[i].gameObject.SetActive(false); }
    }

    public void Send()
    {
        if (_at == null) return;
        _glow = 1f;
        for (int i = 0; i < _at.Length; i++)
            if (_at[i] < 0f && bulges[i] != null) { _at[i] = 0f; bulges[i].gameObject.SetActive(true); Place(i); return; }
    }

    private void Place(int i)
    {
        float d = _at[i];
        int n = pts.Length;
        for (int k = 1; k < n; k++)
        {
            if (d <= _cum[k] || k == n - 1)
            {
                float seg = _cum[k] - _cum[k - 1];
                float t = seg > 1e-4f ? Mathf.Clamp01((d - _cum[k - 1]) / seg) : 1f;
                bulges[i].position = Vector3.Lerp(pts[k - 1], pts[k], t);
                Vector3 dir = pts[k] - pts[k - 1];
                if (dir.sqrMagnitude > 1e-6f) bulges[i].rotation = Quaternion.LookRotation(dir.normalized, Mathf.Abs(dir.normalized.y) > 0.9f ? Vector3.forward : Vector3.up);
                return;
            }
        }
    }

    private void Update()
    {
        if (_at == null || pts == null || pts.Length < 2) return;
        float dt = Time.deltaTime;
        if (autoInterval > 0f) { _autoT += dt; if (_autoT >= autoInterval) { _autoT -= autoInterval; Send(); } }
        if (glows != null && (_glow > 0f || _glowOn))
        {
            _glow = Mathf.Max(0f, _glow - dt * glowFade);
            float gk = _glow * _glow;
            for (int i = 0; i < glows.Length; i++)
            {
                if (glows[i] == null) continue;
                glows[i].GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", _glowBase[i] + glowColor * gk);
                glows[i].SetPropertyBlock(_mpb);
            }
            _glowOn = _glow > 0f;
        }
        if (pop != null)
        {
            _pop = Mathf.Max(0f, _pop - dt * 1.2f);
            float k = _pop * _pop;
            pop.Rotate(0f, (popSpin + 600f * k) * dt, 0f, Space.Self);
            pop.localScale = _popScale * (1f + 0.3f * k);
            if (needle != null) needle.localRotation = _needleBase * Quaternion.Euler(0f, 0f, -needleKick * k);
        }
        for (int i = 0; i < _at.Length; i++)
        {
            if (_at[i] < 0f) continue;
            float was = _at[i];
            _at[i] += speed * dt;
            if (popAt >= 0f && was < popAt && _at[i] >= popAt) _pop = 1f;
            if (_at[i] >= _total)
            {
                _at[i] = -1f; bulges[i].gameObject.SetActive(false);
                if (endPuff != null) endPuff.Emit(endCount);
                if (balloon != null) balloon.PuffColor(cloudColor);
                continue;
            }
            Place(i);
        }
    }
}
