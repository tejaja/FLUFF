using UdonSharp;
using UnityEngine;

// 2026-11-23 てじゃ「ゴールしたらここ（2-5 ゴールの洞窟）のクリスタル光ったらおもろいかな」
//  「そのパターンで光った後に、ゴール後BGM始まったらBPMにあわせてバラバラにチカチカ光ったらかわいいかも」
//  ゴールした本人の画面だけ：
//   1. ゴールの瞬間、ゴールに近い順に波のように光って、ゆっくり元へ（洞窟の光もふわっと明るく）
//   2. ゴール後の曲が鳴っている間、拍ごとにいくつかずつバラバラにチカッと光る
//  クリスタルの光は FuwaCrystal シェーダーの _EmissionColor を MaterialPropertyBlock で足す（材質は他のクリスタルと共有なので材質はさわらない）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaCrystalGoalGlow : UdonSharpBehaviour
{
    [Tooltip("光らせるクリスタル（ゴールに近い順に並べる）")]
    public Renderer[] crystals;
    [Tooltip("洞窟の光（ゴールの瞬間に明るくする）")]
    public Light caveLight;
    public FuwaBgm bgm;

    [Header("ゴールの瞬間の波")]
    public Color glowColor = new Color(0.35f, 0.85f, 1f, 1f);
    public float waveGlow = 0.55f;   // 2.2 だと真っ白にとんだ
    [Tooltip("となりのクリスタルが光るまでの秒数（近い順）")]
    public float waveStep = 0.09f;
    public float waveAttack = 0.08f;
    [Tooltip("光が元へ戻る速さ（秒、大きいほどゆっくり）")]
    public float waveDecay = 0.9f;
    public float lightBoost = 3.5f;

    [Header("ゴール後の曲に合わせたチカチカ")]
    [Tooltip("ゴール後の曲のテンポ（aftergoal_pitch_introloop は 120BPM。ループの頭が拍の頭）")]
    public float bpm = 120f;
    [Tooltip("ループの頭から拍の頭までのずれ（秒）")]
    public float beatOffset = 0.025f;
    [Tooltip("1拍で光らせる数（最小・最大）")]
    public int perBeatMin = 2, perBeatMax = 4;
    public float blinkGlow = 0.45f;
    public float blinkDecay = 0.22f;
    public float blinkLight = 0.6f;

    private MaterialPropertyBlock _mpb;
    private Color _baseEm;
    private float _baseIntensity;
    private float _goalT = -1f;
    private float[] _blinkT;
    private int _lastBeat = -99999;
    private bool _heardAfter;
    private bool _dirty;

    private void Start()
    {
        _mpb = new MaterialPropertyBlock();
        int n = crystals != null ? crystals.Length : 0;
        _blinkT = new float[n];
        for (int i = 0; i < n; i++) _blinkT[i] = -100f;
        _baseEm = new Color(0.2f, 0.45f, 0.5f, 1f);
        if (n > 0 && crystals[0] != null && crystals[0].sharedMaterial != null && crystals[0].sharedMaterial.HasProperty("_EmissionColor"))
            _baseEm = crystals[0].sharedMaterial.GetColor("_EmissionColor");
        if (caveLight != null) _baseIntensity = caveLight.intensity;
    }

    // FuwaGoal.Landed から（onGoal）
    public void OnCourseGoal()
    {
        _goalT = Time.time;
        _heardAfter = false;
        _lastBeat = -99999;
        for (int i = 0; i < _blinkT.Length; i++) _blinkT[i] = -100f;
    }

    private void Update()
    {
        if (_goalT < 0f) return;
        float now = Time.time, t = now - _goalT;
        int n = crystals.Length;

        // ゴール後の曲：鳴っている間だけ拍ごとにチカチカ。一度鳴ってから止まったら（次の挑戦・ほかへ移動）おしまい
        bool after = false; float bt = -9999f;
        if (bgm != null && bgm.afterGoalTrack >= 0 && bgm.CurrentTrack() == bgm.afterGoalTrack && bgm.IsAudible())
        {
            bt = bgm.TrackBeatTime(bgm.afterGoalTrack);
            after = bt > -9000f;
        }
        if (after) _heardAfter = true;
        if (after && bpm > 0f)
        {
            int beat = Mathf.FloorToInt((bt - beatOffset) * bpm / 60f);
            if (beat != _lastBeat)
            {
                bool first = _lastBeat == -99999;
                _lastBeat = beat;
                if (!first && n > 0)
                {
                    int k = Random.Range(perBeatMin, perBeatMax + 1);
                    for (int j = 0; j < k; j++) _blinkT[Random.Range(0, n)] = now;
                }
            }
        }

        float lightAdd = 0f, maxW = 0f;
        for (int i = 0; i < n; i++)
        {
            Renderer r = crystals[i];
            if (r == null) continue;
            // 波：近い順に i×waveStep 遅れて光る
            float tw = t - i * waveStep, w = 0f;
            if (tw > 0f) w = tw < waveAttack ? tw / waveAttack : Mathf.Exp(-(tw - waveAttack) / waveDecay);
            w *= waveGlow;
            // 拍のチカッ
            float tb = now - _blinkT[i];
            if (tb >= 0f && tb < blinkDecay * 5f) w += blinkGlow * Mathf.Exp(-tb / blinkDecay);
            if (w > maxW) maxW = w;
            _mpb.SetColor("_EmissionColor", _baseEm + glowColor * w);
            r.SetPropertyBlock(_mpb);
        }
        if (t >= 0f)
        {
            float lw = t < waveAttack ? t / waveAttack : Mathf.Exp(-(t - waveAttack - n * waveStep * 0.5f) / (waveDecay * 1.5f));
            lightAdd = lightBoost * Mathf.Clamp01(lw);
        }
        if (_lastBeat != -99999 && after)
        {
            float bf = ((bt - beatOffset) * bpm / 60f) % 1f; if (bf < 0f) bf += 1f;
            lightAdd += blinkLight * Mathf.Exp(-bf * 4f);
        }
        if (caveLight != null) caveLight.intensity = _baseIntensity + lightAdd;
        _dirty = true;

        // おしまい：波がおさまって、ゴール後の曲が鳴り終わった（または 8 秒たっても鳴らない＝BGM オフ）
        bool waveDone = t > n * waveStep + waveAttack + waveDecay * 6f;
        if (waveDone && !after && (_heardAfter || t > 8f)) Clear();
    }

    private void Clear()
    {
        _goalT = -1f;
        if (!_dirty) return;
        _dirty = false;
        _mpb.Clear();   // 空のブロック＝材質のまま
        for (int i = 0; i < crystals.Length; i++) if (crystals[i] != null) crystals[i].SetPropertyBlock(_mpb);
        if (caveLight != null) caveLight.intensity = _baseIntensity;
    }

    private void OnDisable()
    {
        if (_mpb != null) Clear();
    }
}
