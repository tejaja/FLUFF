using UdonSharp;
using UnityEngine;

// 2–4 はぐるまのとう：まん中の雲のバルーン（2026-11-08 てじゃ「白パイプだけにして、上にあげてでっかいバルーン状の機械に接続、上から雲がぽわぽわ」）
// ・白いパイプ（FuwaSugarPipe）からふくらみが着くたびに Puff() → バルーンが少しふくらんで戻る＋てっぺんから雲がぽわっ
// ・いつもの雲（ゆっくり）は別の粒が出しっぱなし。息をするようにゆっくり大きさが変わる
// ・2026-11-10 てじゃ「ちゃんと雲つくってふわふわ飛ばしたい」「ステージ1の背景の雲みたいなの」：
//   ふくらみが着くと（PuffColor）、煙突から背景と同じ形の雲（clouds、使い回し）がむくっと出て大きくなり、のぼりながら風で流れて、cloudLife 秒でしぼんで消える。
//   色つきのタンクから来たふくらみなら、その色のうすい雲
// 自分の画面だけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaCloudBalloon : UdonSharpBehaviour
{
    public Transform body;
    public ParticleSystem burst;
    public int burstCount = 7;
    public float swell = 0.05f, breathe = 0.015f, breathePeriod = 5f;
    [Tooltip("パイプからのふくらみが何回たまったら、ふくらんで雲を出すか（2026-11-10 てじゃ「3回くらい溜まったら」）")]
    public int puffEvery = 3;
    private int _count;

    [Header("生まれる雲")]
    public Transform[] clouds;
    public Transform cloudSpawn;
    public float cloudLife = 30f, cloudGrow = 3.5f, cloudFade = 6f, cloudMinGap = 2.5f;
    public float cloudRise = 0.7f, cloudScaleMin = 0.35f, cloudScaleMax = 0.6f;
    public Vector3 cloudWind = new Vector3(0.9f, 0f, 0.35f);
    [Tooltip("流れる向きのばらつき（度）。2026-11-10 てじゃ「2-5予定地の方向に飛ばそう」→ cloudWind は 2–5 の方へ")]
    public float cloudSpread = 20f;
    [Tooltip("出てから消えるまでに、さらに何倍まで大きくなるか（2026-11-10 てじゃ「雲もっとでかくなるようにしよ」）")]
    public float cloudGrowLate = 1.8f;
    [Tooltip("雲の色のうすさ（0=ふくらみの色そのまま、1=白）")]
    public float cloudPale = 1f;   // 2026-11-10 てじゃ「色は白だけでいいかも」→ 白（色をもどすなら 0.55）

    private float _kick;
    private float[] _age, _size, _seed;
    private Vector3[] _dir;
    private Renderer[] _rends;
    private float _last = -100f;
    private MaterialPropertyBlock _mpb;
    private bool _ready;

    private void Start()
    {
        int n = clouds != null ? clouds.Length : 0;
        _age = new float[n]; _size = new float[n]; _seed = new float[n]; _dir = new Vector3[n]; _rends = new Renderer[n];
        for (int i = 0; i < n; i++)
        {
            _age[i] = -1f;
            if (clouds[i] == null) continue;
            _rends[i] = clouds[i].GetComponent<Renderer>();
            clouds[i].gameObject.SetActive(false);
        }
        _mpb = new MaterialPropertyBlock();
        _ready = true;
    }

    public void Puff()
    {
        PuffColor(Color.white);
    }

    public void PuffColor(Color c)
    {
        _count++;
        if (_count < puffEvery) return;
        if (!_ready || clouds == null || clouds.Length == 0 || cloudSpawn == null) return;
        if (Time.time - _last < cloudMinGap) return;   // 間があいていない時は、たまったまま待つ（次に着いた時に出す）
        _count = 0;
        _last = Time.time;
        // ふくらむのは雲が出る時だけ（2026-11-10 てじゃ「膨らむのは出るときだけにしたい」）
        _kick = 1f;
        if (burst != null) burst.Emit(burstCount);
        int pick = -1; float oldest = -1f;
        for (int i = 0; i < clouds.Length; i++)
        {
            if (clouds[i] == null) continue;
            if (_age[i] < 0f) { pick = i; break; }
            if (_age[i] > oldest) { oldest = _age[i]; pick = i; }
        }
        if (pick < 0) return;
        Transform t = clouds[pick];
        _age[pick] = 0f;
        _size[pick] = Random.Range(cloudScaleMin, cloudScaleMax);
        _seed[pick] = Random.Range(0f, 100f);
        _dir[pick] = Quaternion.Euler(0f, Random.Range(-cloudSpread, cloudSpread), 0f) * cloudWind;
        t.position = cloudSpawn.position;
        t.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        t.localScale = Vector3.one * 0.001f;
        Color col = Color.Lerp(c, Color.white, cloudPale); col.a = 1f;
        Color sh = Color.Lerp(col * 0.82f, new Color(0.72f, 0.8f, 0.97f), 0.45f); sh.a = 1f;
        if (_rends[pick] != null)
        {
            _rends[pick].GetPropertyBlock(_mpb);
            _mpb.SetColor("_Color", col); _mpb.SetColor("_Shade", sh);
            _rends[pick].SetPropertyBlock(_mpb);
        }
        t.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (body != null)
        {
            _kick = Mathf.Max(0f, _kick - Time.deltaTime * 1.4f);
            float k = _kick * _kick * (3f - 2f * _kick);   // ふわっとふくらんで、ゆっくり戻る
            float s = 1f + swell * k + breathe * Mathf.Sin(Time.time * 2f * Mathf.PI / breathePeriod);
            body.localScale = new Vector3(s, 1f + (s - 1f) * 0.6f, s);
        }
        if (!_ready || clouds == null) return;
        float dt = Time.deltaTime;
        for (int i = 0; i < clouds.Length; i++)
        {
            if (_age[i] < 0f || clouds[i] == null) continue;
            float a = _age[i] + dt; _age[i] = a;
            if (a >= cloudLife) { _age[i] = -1f; clouds[i].gameObject.SetActive(false); continue; }
            float g = Mathf.Clamp01(a / cloudGrow); g = 1f - (1f - g) * (1f - g) * (1f - g);   // むくっと出て、ゆっくり大きく
            float f = Mathf.Clamp01((cloudLife - a) / cloudFade); f = f * f * (3f - 2f * f);  // 最後はしぼむ
            float wob = 1f + 0.04f * Mathf.Sin(Time.time * 0.9f + _seed[i]);
            float late = Mathf.Lerp(1f, cloudGrowLate, Mathf.Clamp01((a - cloudGrow) / Mathf.Max(0.1f, cloudLife - cloudGrow - cloudFade * 0.5f)));   // 流れながらだんだん大きく
            clouds[i].localScale = Vector3.one * (_size[i] * g * f * wob * late + 0.001f);
            // 出たてはぐっとのぼり、だんだん風まかせ
            float up = cloudRise * (0.35f + 0.65f / (1f + a * 0.25f)) + 0.12f * Mathf.Sin(Time.time * 0.6f + _seed[i]);
            float side = Mathf.Clamp01(a / 4f);
            clouds[i].position += (Vector3.up * up + _dir[i] * side) * dt;
            clouds[i].Rotate(0f, 2.5f * dt, 0f, Space.World);
        }
    }
}
