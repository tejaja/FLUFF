using UdonSharp;
using UnityEngine;

// トリガーの範囲内にいるふわふわに風を当て続ける（横風・上昇気流など）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWindArea : UdonSharpBehaviour
{
    [Tooltip("風の向き（このオブジェクトのローカル座標）")]
    public Vector3 localDirection = Vector3.forward;
    [Tooltip("風の強さ（加速度 m/s²）")]
    public float strength = 4f;
    [Tooltip("風下に行くほど弱くなる（上昇気流なら上ほど弱い）。0=一定、1=端でゼロ")]
    [Range(0f, 1f)] public float fadeAlongDirection = 0f;

    [Header("オン/オフの周期（offSeconds=0なら常に吹く）")]
    public float onSeconds = 2f;
    public float offSeconds = 0f;
    public float phaseOffset = 0f;
    [Tooltip("吹いている間の強さの変化（横軸0〜1=吹き始め〜終わり、縦軸=強さの倍率）")]
    public AnimationCurve gustCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f));
    [Tooltip("吹き始める何秒前から見た目を出して予告するか")]
    public float warnSeconds = 0.6f;
    [Tooltip("風が止まっている間に消す見た目（パーティクルなど）")]
    public GameObject visual;

    [Header("吹くたびに向きを反転（offSeconds>0の時だけ）")]
    public bool alternate = false;
    [Tooltip("反対向きに吹いている時の見た目")]
    public GameObject visualReverse;

    private bool _blowing = true;
    private bool _reversed;
    private float _gust = 1f;
    [Tooltip("見た目（粒）を出し入れする時のフェード秒数")]
    public float fadeSeconds = 0.35f;
    [Tooltip("吹き始めに鳴らす音（なくてもOK）")]
    public AudioSource gustSound;
    private bool _wasBlowing;

    [Header("地面のハイライト（風が吹く範囲を緑でうっすら表示。なくてもOK）")]
    public Renderer groundHighlight;
    [Tooltip("一番濃い時の不透明度")]
    public float highlightAlpha = 0.3f;
    private Material _hlMat;
    private Color _hlColor;
    private float _hlShown = -1f;
    private float _hlDir = 0f;

    private int _visualState = -1; // 0=なし 1=通常 2=反対
    private BoxCollider _box;
    private ParticleSystem _ps;
    private ParticleSystem _psRev;
    private float _rate;
    private float _rateRev;
    private float _level;
    private float _levelRev;

    private void Start()
    {
        _box = GetComponent<BoxCollider>();
        if (groundHighlight != null)
        {
            _hlMat = groundHighlight.material;
            _hlColor = _hlMat.color;
        }
        // 見た目がパーティクルなら、ON/OFFの代わりに出る量を徐々に変えてフェードさせる
        if (visual != null)
        {
            _ps = visual.GetComponent<ParticleSystem>();
            if (_ps != null) { _rate = _ps.emission.rateOverTimeMultiplier; visual.SetActive(true); }
        }
        if (visualReverse != null)
        {
            _psRev = visualReverse.GetComponent<ParticleSystem>();
            if (_psRev != null) { _rateRev = _psRev.emission.rateOverTimeMultiplier; visualReverse.SetActive(true); }
        }
    }

    private void Update()
    {
        if (offSeconds <= 0f)
        {
            _blowing = true;
            _reversed = false;
            _gust = 1f;
            SetVisual(1);
            return;
        }
        float cycle = onSeconds + offSeconds;
        float time = Time.time + phaseOffset;
        int index = Mathf.FloorToInt(time / cycle);
        float t = time - index * cycle;
        _blowing = t < onSeconds;
        _reversed = alternate && (index % 2 != 0);
        _gust = _blowing && onSeconds > 0f ? gustCurve.Evaluate(t / onSeconds) : 0f;
        if (_blowing && !_wasBlowing && gustSound != null) gustSound.Play();
        _wasBlowing = _blowing;

        if (_blowing) SetVisual(_reversed ? 2 : 1);
        else if (t > cycle - warnSeconds)
        {
            // 予告は「次に吹く向き」で出す
            bool nextReversed = alternate && ((index + 1) % 2 != 0);
            SetVisual(nextReversed ? 2 : 1);
        }
        else SetVisual(0);
    }

    private void LateUpdate()
    {
        float step = fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f;
        if (_ps != null)
        {
            _level = Mathf.MoveTowards(_level, _visualState == 1 ? 1f : 0f, step);
            ParticleSystem.EmissionModule em = _ps.emission;
            em.rateOverTimeMultiplier = _rate * _level;
        }
        if (_psRev != null)
        {
            _levelRev = Mathf.MoveTowards(_levelRev, _visualState == 2 ? 1f : 0f, step);
            ParticleSystem.EmissionModule em = _psRev.emission;
            em.rateOverTimeMultiplier = _rateRev * _levelRev;
        }
        // 地面のハイライトは粒と同じ速さでフェードイン／アウト（予告の間から出始める）
        if (_hlMat != null)
        {
            // 帯が流れる向き（反対向きの風の時は逆に流す）
            float dir = _visualState == 2 ? -1f : 1f;
            if (_visualState != 0 && dir != _hlDir)
            {
                _hlDir = dir;
                _hlMat.SetFloat("_FlowDir", dir);
            }
            float lv = Mathf.Max(_level, _levelRev);
            if (_ps == null && _psRev == null) lv = _visualState != 0 ? 1f : 0f;
            if (Mathf.Abs(lv - _hlShown) > 0.001f)
            {
                _hlShown = lv;
                Color c = _hlColor;
                c.a = highlightAlpha * lv;
                _hlMat.color = c;
                groundHighlight.enabled = lv > 0.001f;
            }
        }
    }

    private void SetVisual(int state)
    {
        if (_visualState == state) return;
        _visualState = state;
        // パーティクルはLateUpdateでフェード。パーティクル以外の見た目だけ即ON/OFF
        if (visual != null && _ps == null) visual.SetActive(state == 1);
        if (visualReverse != null && _psRev == null) visualReverse.SetActive(state == 2);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!_blowing || other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;

        float s = strength * _gust;
        if (fadeAlongDirection > 0f && _box != null)
        {
            // 箱の中で風上側の端=0、風下側の端=1
            Vector3 local = transform.InverseTransformPoint(other.transform.position) - _box.center;
            Vector3 d = localDirection.normalized;
            Vector3 half = _box.size * 0.5f;
            float extent = Mathf.Abs(d.x) * half.x + Mathf.Abs(d.y) * half.y + Mathf.Abs(d.z) * half.z;
            float f = extent > 0f ? Mathf.Clamp01((Vector3.Dot(local, d) + extent) / (2f * extent)) : 0f;
            s *= 1f - fadeAlongDirection * f;
        }
        Vector3 dir = transform.TransformDirection(localDirection).normalized;
        if (_reversed) dir = -dir;
        ball.AddWind(dir * s);
    }
}
