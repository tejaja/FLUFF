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
    [Tooltip("角の手前・あとのこの長さ(m)で、ふくらみの向きを次の管へなめらかに回し、長さを縮めて丸くする（2026-11-13 てじゃ「角を通る時に青い◯が一瞬みえる」：細長いふくらみが角の玉からはみ出ていた）")]
    public float cornerW = 0.5f;
    private Vector3[] _bs;
    [Tooltip("着いたら、気球の代わりにこの管へ色を渡す（2026-11-13 てじゃ「タンクで青球はとまっていい」：回るタンクの液は砂糖タンクで止まり、そこから白いふくらみが色を運ぶ）")]
    public FuwaSugarPipe handoff;
    [Tooltip("管の輪（真ちゅうの帯）の、道すじの上の位置(m)。ここを通る時はふくらみが細くなってくぐる（2026-11-13 てじゃ「輪がふくらみに埋もれるのが変」）")]
    public float[] squeezeAt;
    [Tooltip("輪をくぐる時の太さ（ふだんの何倍）")]
    public float squeezeR = 0.7f;
    [Tooltip("輪の前後この長さ(m)で細くなる")]
    public float squeezeW = 0.45f;
    [Tooltip("出だし・着く手前のこの長さ(m)で、ふくらみがふくらむ／しぼむ（2026-11-13 てじゃ「最後についたときにパッと消えるのが気になる」）")]
    public float growW = 0.4f, endW = 0.8f;
    private Color[] _bc;
    private bool[] _bcOn;
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
        _bs = new Vector3[b];
        _bc = new Color[b]; _bcOn = new bool[b];
        for (int i = 0; i < b; i++) if (bulges[i] != null) _bs[i] = bulges[i].localScale;
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
            if (_at[i] < 0f && bulges[i] != null) { _at[i] = 0f; _bcOn[i] = false; bulges[i].gameObject.SetActive(true); Place(i); return; }
    }

    // ほかの管から色を受け取って、タンク（popAt）から流す。着いたら気球からその色の雲
    public void SendColored(Color c)
    {
        if (_at == null) return;
        _glow = 1f; _pop = 1f;
        for (int i = 0; i < _at.Length; i++)
            if (_at[i] < 0f && bulges[i] != null) { _at[i] = popAt > 0f ? popAt : 0f; _bc[i] = c; _bcOn[i] = true; bulges[i].gameObject.SetActive(true); Place(i); return; }
        if (balloon != null) balloon.PuffColor(c);   // あいているふくらみがない時は、色だけ先に
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
                if (dir.sqrMagnitude <= 1e-6f) return;
                dir = dir.normalized;
                // 角の近く：向きを角の前後の管の間へ回し、長さを縮める（角のまん中で丸＝向きは前後の半分）
                Vector3 look = dir; float near = 1f;
                float toEnd = _cum[k] - d, fromStart = d - _cum[k - 1];
                if (cornerW > 0f && k < n - 1 && toEnd < cornerW)
                {
                    Vector3 dn = pts[k + 1] - pts[k];
                    if (dn.sqrMagnitude > 1e-6f) { near = toEnd / cornerW; look = Vector3.Slerp(dir, dn.normalized, 0.5f * (1f - near)); }
                }
                else if (cornerW > 0f && k > 1 && fromStart < cornerW)
                {
                    Vector3 dp = pts[k - 1] - pts[k - 2];
                    if (dp.sqrMagnitude > 1e-6f) { near = fromStart / cornerW; look = Vector3.Slerp(dir, dp.normalized, 0.5f * (1f - near)); }
                }
                if (look.sqrMagnitude > 1e-6f) bulges[i].rotation = Quaternion.LookRotation(look.normalized, Mathf.Abs(look.normalized.y) > 0.9f ? Vector3.forward : Vector3.up);
                if (_bs != null && i < _bs.Length)
                {
                    Vector3 bs = _bs[i];
                    float zl = Mathf.Lerp(Mathf.Min(bs.x, bs.z), bs.z, near);
                    // 輪をくぐる：輪のある所でふくらみの太さが輪の内がわに収まるように細く（だ円体の、中心から x の所の太さ＝r√(1−(x/L)²)）。そのぶん少し長く
                    float sq = 1f, L = zl * 0.5f;
                    if (squeezeAt != null && L > 1e-4f)
                        for (int q = 0; q < squeezeAt.Length; q++)
                        {
                            float x = d - squeezeAt[q];
                            if (Mathf.Abs(x) < L) { float w = Mathf.Sqrt(Mathf.Max(1e-3f, 1f - x * x / (L * L))); sq = Mathf.Min(sq, squeezeR / w); }
                        }
                    // 出だし・着く手前：全体をふくらませる／しぼませる
                    float g = 1f;
                    if (growW > 0f) g = Mathf.Min(g, Mathf.Clamp01(d / growW));
                    if (endW > 0f) g = Mathf.Min(g, Mathf.Clamp01((_total - d) / endW));
                    g = g * g * (3f - 2f * g);
                    bulges[i].localScale = new Vector3(bs.x * sq * g, bs.y * sq * g, zl * (1f + (1f - sq) * 0.4f) * g);
                }
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
                if (handoff != null) handoff.SendColored(cloudColor);
                else if (balloon != null) balloon.PuffColor(_bcOn[i] ? _bc[i] : cloudColor);
                continue;
            }
            Place(i);
        }
    }
}
