using UdonSharp;
using UnityEngine;

// 胞子（ふわふわ）がリングの中に入って、地面に着地したらゴール（空中を通過しただけではゴールにならない）。
// 判定はリングと同じ大きさの円柱なので、ふわふわがリングにかすっていればゴール。
// ゴールした瞬間：目印（▼とゴールの文字）が消えて、リングがパッと光ってフェードアウト。
// リングに沿ってキノコが時間差で生えて、フェアリーリングになる。
// ワンテンポ遅れて、リングの中心だった所に「ロビーへ」ワープがぽんっと出てくる。
// 同じコースでもう一度スタートしたら、リングと目印は戻る（キノコは生えたまま）。
// 生えたキノコは、ロビーなど別の場所に移る（ふわふわのスタート地点が変わる）まで生えたまま。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGoal : UdonSharpBehaviour
{
    [Tooltip("ゴール時に再生する演出（なくてもOK）")]
    public ParticleSystem celebration;
    [Tooltip("ゴールのたびに上がる胞子の花火（自分の画面だけ）")]
    public ParticleSystem fireworks;
    [Tooltip("ゴールのたびに、キノコの輪から光る胞子がふわ〜っと舞い上がる")]
    public ParticleSystem ringSpores;
    [Tooltip("花火の音（打ち上げ〜破裂まで1本の音）")]
    public AudioSource fireworksSound;
    [Tooltip("フェアリーリングのキノコをまとめた親（子のキノコが順番に生える）")]
    public Transform fairyRing;
    [Tooltip("1本のキノコが生えきるまでの秒数")]
    public float growTime = 0.45f;
    [Tooltip("次のキノコが生え始めるまでの間隔")]
    public float stagger = 0.08f;

    [Tooltip("ゴールした時だけ出す「ロビーへ」ワープ（自分の画面だけ）")]
    public GameObject lobbyWarp;
    [Tooltip("ゴールしてからワープが出るまでの秒数")]
    public float warpDelay = 1.0f;
    [Tooltip("ワープがぽんっと出てくる秒数")]
    public float warpPopTime = 0.35f;
    [Tooltip("ワープの「ロビーにもどる」看板（ワープより遅れて出す）")]
    public Transform warpLabel;
    [Tooltip("ワープが出てから看板が出るまでの秒数")]
    public float labelDelay = 1.2f;

    [Header("CLEAR! の文字とクリアタイム（どの方向からでも見える）")]
    [Tooltip("「CLEAR!」と、その上のクリアタイムをまとめた親（普段は非表示）")]
    public Transform clearBanner;
    [Tooltip("クリアタイムの文字（タイムを計らないコースでは出さない）")]
    public TMPro.TextMeshPro clearTimeText;
    [Tooltip("ゴールしてから CLEAR! が出るまでの秒数")]
    public float clearDelay = 0.2f;
    [Tooltip("CLEAR! がぽんっと出てくる秒数")]
    public float clearPopTime = 0.5f;

    [Tooltip("ゴールのリング（FuwaCourse/GoalRing シェーダー）")]
    public Renderer ringRenderer;
    [Tooltip("リングが光った時のまわりのぼんやりした光（FuwaCourse/GoalRingHalo、普段は非表示）")]
    public Renderer ringHalo;
    [Tooltip("ゴールの目印（▼とゴールの文字）。ゴールした瞬間に消える")]
    public GameObject marker;
    [Tooltip("リングが光ってから消えきるまでの秒数")]
    public float flashTime = 0.9f;
    [Tooltip("光りながら広がる大きさ（倍）")]
    public float flashScale = 1.15f;

    private Vector3[] _scales;
    private int _count;
    private float _startTime = -1f;
    private float _retractTime = -1f;
    private FuwaBall _ball;
    private Transform _goalStart;

    private Material _ringMat;
    private Material _haloMat;
    private Vector3 _ringScale;
    private float _flashStart = -1f;
    private bool _ringGone;
    private float _warpAt = -1f;
    private float _warpPopStart = -1f;
    private Vector3 _labelScale = Vector3.one;
    private float _labelAt = -1f;
    private float _labelPopStart = -1f;
    private Vector3 _clearScale = Vector3.one;
    private float _clearAt = -1f;
    private float _clearPopStart = -1f;

    private void Start()
    {
        if (lobbyWarp != null) lobbyWarp.SetActive(false);
        if (clearBanner != null)
        {
            _clearScale = clearBanner.localScale;
            clearBanner.gameObject.SetActive(false);
        }
        if (warpLabel != null) _labelScale = warpLabel.localScale;
        if (ringRenderer != null)
        {
            _ringMat = ringRenderer.material;
            _ringScale = ringRenderer.transform.localScale;
        }
        if (ringHalo != null)
        {
            _haloMat = ringHalo.material;
            ringHalo.enabled = false;
        }
        if (fairyRing == null) return;
        _count = fairyRing.childCount;
        _scales = new Vector3[_count];
        for (int i = 0; i < _count; i++)
        {
            Transform c = fairyRing.GetChild(i);
            _scales[i] = c.localScale;
            c.localScale = Vector3.zero;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball != null) ball.SetGoalZone(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball != null) ball.LeaveGoalZone(this);
    }

    // リングの中で胞子が着地した時に、胞子から呼ばれる
    public void Landed(FuwaBall ball)
    {
        if (ball == null || _ringGone) return;
        ball.ReachGoal();
        _ball = ball;
        _goalStart = ball.startPoint;

        // 目印を消して、リングを光らせて消す。ワープはワンテンポ後
        if (marker != null) marker.SetActive(false);
        _ringGone = true;
        _flashStart = Time.time;
        _warpAt = Time.time + warpDelay;
        if (lobbyWarp != null) lobbyWarp.SetActive(false);

        // お祝い：胞子の花火と、キノコの輪から舞い上がる胞子（ゴールのたびに）
        if (fireworks != null) { fireworks.Clear(true); fireworks.Play(true); }
        if (ringSpores != null) { ringSpores.Clear(true); ringSpores.Play(true); }
        if (fireworksSound != null) { fireworksSound.Stop(); fireworksSound.Play(); }

        // CLEAR! とクリアタイム
        if (clearTimeText != null)
        {
            float t = ball.lastGoalTime;
            clearTimeText.gameObject.SetActive(t > 0f);
            if (t > 0f) clearTimeText.text = "<mspace=0.6em>" + ball.FormatTimeText(t) + "</mspace>";
        }
        if (clearBanner != null) clearBanner.gameObject.SetActive(false);
        _clearPopStart = -1f;
        _clearAt = Time.time + clearDelay;

        if (_startTime >= 0f) return;   // キノコはもう生えてる
        _retractTime = -1f;
        if (celebration != null) celebration.Play();
        _startTime = Time.time;
    }

    private void Update()
    {
        if (_startTime < 0f) return;
        UpdateFlash();
        UpdateWarp();
        UpdateClear();

        // 同じコースでもう一度スタートしたら、リングと目印を戻す（キノコはそのまま）
        if (_ringGone && _flashStart < 0f && _ball != null && _ball.startPoint == _goalStart && _ball.IsRunning())
            RestoreRing();

        if (fairyRing == null) return;
        // ロビーや別のコースに移ったら引っ込める
        if (_retractTime < 0f && _ball != null && _ball.startPoint != _goalStart) _retractTime = Time.time;
        float t = Time.time - _startTime;
        bool done = true;
        for (int i = 0; i < _count; i++)
        {
            float k;
            if (_retractTime < 0f)
            {
                // 生える：少し行き過ぎてから戻る（ぽよん）。生えきったらそのまま
                float x = Mathf.Clamp01((t - i * stagger) / growTime);
                k = x < 1f ? Mathf.Sin(x * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(x * Mathf.PI)) : 1f;
                done = false;
                if (x >= 1f)
                {
                    // 生えきった後は、ときどき1本ずつ「ぽよん」と縦に潰れて揺れ戻る（まばらに）
                    fairyRing.GetChild(i).localScale = Vector3.Scale(_scales[i], Idle(i));
                    continue;
                }
            }
            else
            {
                // 引っ込む
                float y = Mathf.Clamp01((Time.time - _retractTime - i * stagger * 0.5f) / growTime);
                k = 1f - y;
                if (y < 1f) done = false;
            }
            fairyRing.GetChild(i).localScale = _scales[i] * k;
        }
        if (done) ResetRing();
    }

    [Header("生えた後のぽよぽよ（まばらに）")]
    [Tooltip("次にぽよんとするまでの間隔（最短〜最長、秒）")]
    public Vector2 idleInterval = new Vector2(1.5f, 5f);
    [Tooltip("ぽよんの長さ(秒)")]
    public float idleTime = 0.6f;
    [Tooltip("ぽよんで縦に潰れる量（0.15 = 15%）")]
    public float idleSquash = 0.15f;
    private float[] _idleAt;

    // 1本分の「縦・横」の倍率。潰れる時は横に少し広がる
    private Vector3 Idle(int i)
    {
        if (_idleAt == null || _idleAt.Length != _count)
        {
            _idleAt = new float[_count];
            for (int j = 0; j < _count; j++) _idleAt[j] = Time.time + Random.Range(0.3f, idleInterval.y);
        }
        float u = (Time.time - _idleAt[i]) / Mathf.Max(0.05f, idleTime);
        if (u < 0f) return Vector3.one;
        if (u >= 1f)
        {
            _idleAt[i] = Time.time + Random.Range(idleInterval.x, idleInterval.y);
            return Vector3.one;
        }
        // 減衰しながら2回ほど揺れる（最初に潰れる）
        float w = -idleSquash * Mathf.Sin(u * Mathf.PI * 4f) * (1f - u);
        return new Vector3(1f - w * 0.5f, 1f + w, 1f - w * 0.5f);
    }

    // リング：一瞬でパッと光る → 広がりながら薄くなって消える
    private void UpdateFlash()
    {
        if (_flashStart < 0f || ringRenderer == null) return;
        float t = Time.time - _flashStart;
        float rise = 0.08f;
        float glow = t < rise ? t / rise : 1f - Mathf.Clamp01((t - rise) / (flashTime - rise));
        float fade = Mathf.Clamp01((t - rise) / (flashTime - rise));
        float alpha = 1f - fade * fade * (3f - 2f * fade);
        float grow = 1f - (1f - fade) * (1f - fade);
        if (_ringMat != null)
        {
            _ringMat.SetFloat("_Glow", glow * 3f);
            _ringMat.SetFloat("_Alpha", alpha);
        }
        float k = 1f + (flashScale - 1f) * grow;
        ringRenderer.transform.localScale = new Vector3(_ringScale.x * k, _ringScale.y, _ringScale.z * k);
        if (ringHalo != null)
        {
            ringHalo.enabled = true;
            ringHalo.transform.localScale = new Vector3(k, 1f, k);
            if (_haloMat != null) _haloMat.SetFloat("_Glow", glow * 3f);
        }
        if (t >= flashTime)
        {
            ringRenderer.enabled = false;
            if (ringHalo != null) ringHalo.enabled = false;
            _flashStart = -1f;
        }
    }

    // ワープ：ワンテンポ遅れて、ぽんっと少し大きくなってから落ち着く
    private void UpdateWarp()
    {
        if (lobbyWarp == null) return;
        if (_warpAt > 0f && Time.time >= _warpAt)
        {
            _warpAt = -1f;
            _warpPopStart = Time.time;
            lobbyWarp.transform.localScale = Vector3.zero;
            lobbyWarp.SetActive(true);
            // 看板は labelDelay 秒遅れて出す（0ならワープと同時）
            if (warpLabel != null)
            {
                warpLabel.localScale = Vector3.zero;
                _labelAt = Time.time + labelDelay;
            }
        }
        if (_warpPopStart >= 0f)
        {
            float x = Mathf.Clamp01((Time.time - _warpPopStart) / warpPopTime);
            lobbyWarp.transform.localScale = Vector3.one * Soft(x);
            if (x >= 1f) { lobbyWarp.transform.localScale = Vector3.one; _warpPopStart = -1f; }
        }
        if (warpLabel == null) return;
        if (_labelAt > 0f && Time.time >= _labelAt) { _labelAt = -1f; _labelPopStart = Time.time; }
        if (_labelPopStart >= 0f)
        {
            float y = Mathf.Clamp01((Time.time - _labelPopStart) / warpPopTime);
            warpLabel.localScale = _labelScale * Soft(y);
            if (y >= 1f) { warpLabel.localScale = _labelScale; _labelPopStart = -1f; }
        }
    }

    private void UpdateClear()
    {
        if (clearBanner == null) return;
        if (_clearAt > 0f && Time.time >= _clearAt)
        {
            _clearAt = -1f;
            _clearPopStart = Time.time;
            clearBanner.localScale = Vector3.zero;
            clearBanner.gameObject.SetActive(true);
        }
        if (_clearPopStart >= 0f)
        {
            float x = Mathf.Clamp01((Time.time - _clearPopStart) / Mathf.Max(0.01f, clearPopTime));
            clearBanner.localScale = _clearScale * Pop(x);
            if (x >= 1f) { clearBanner.localScale = _clearScale; _clearPopStart = -1f; }
        }
    }

    // 0→1で「ふわっと」広がる大きさ（はじめ速く、最後はゆっくり止まる。行き過ぎない）
    private float Soft(float x)
    {
        float k = 1f - x;
        return 1f - k * k * k;
    }

    // 0→1で「少し大きくなってから落ち着く」大きさ
    private float Pop(float x)
    {
        return Mathf.Sin(x * Mathf.PI * 0.5f) * (1f + 0.2f * Mathf.Sin(x * Mathf.PI));
    }

    private void RestoreRing()
    {
        _ringGone = false;
        _flashStart = -1f;
        _warpAt = -1f;
        _warpPopStart = -1f;
        _labelAt = -1f;
        _labelPopStart = -1f;
        if (warpLabel != null) warpLabel.localScale = _labelScale;
        _clearAt = -1f;
        _clearPopStart = -1f;
        if (clearBanner != null) { clearBanner.localScale = _clearScale; clearBanner.gameObject.SetActive(false); }
        if (ringRenderer != null)
        {
            ringRenderer.transform.localScale = _ringScale;
            ringRenderer.enabled = true;
        }
        if (ringHalo != null) ringHalo.enabled = false;
        if (_ringMat != null)
        {
            _ringMat.SetFloat("_Glow", 0f);
            _ringMat.SetFloat("_Alpha", 1f);
        }
        if (marker != null) marker.SetActive(true);
        if (lobbyWarp != null)
        {
            lobbyWarp.SetActive(false);
            lobbyWarp.transform.localScale = Vector3.one;
        }
    }

    // モードの切り替えなどでコースごと消えた時は、すぐ元に戻しておく
    private void OnDisable()
    {
        if (_startTime >= 0f) ResetRing();
    }

    private void ResetRing()
    {
        _startTime = -1f;
        _retractTime = -1f;
        RestoreRing();
        _ball = null;
        if (fairyRing == null || _scales == null) return;
        for (int i = 0; i < _count; i++) fairyRing.GetChild(i).localScale = Vector3.zero;
    }
}
