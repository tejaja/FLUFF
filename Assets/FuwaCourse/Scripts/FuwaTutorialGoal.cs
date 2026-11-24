using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// チュートリアルの課題（2026-11-24 てじゃ「課題式にするなら全部やりたい」「壁を通過不可にして、課題クリアで壁解放」）
// ・自分のふわふわが輪（このオブジェクトのトリガー）をくぐったらクリア：輪の色が変わる・音・キラキラ・看板に ✔
// ・クリアすると先の壁（プレイヤーが通れない）が下へ沈んで開く
// ・救済：この区画で何回か失敗するか、しばらくたったら看板にコツ。さらに失敗が続いたら壁だけ開く（クリア扱いにはしない）
// ・自分にだけ（同期なし）。チュートリアルから出たら FuwaReadMarks から ResetGoal で最初の状態に戻す
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaTutorialGoal : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("この区画のためし撃ちの場所。ふわふわがここを戻り先にしている間だけ、失敗の回数と時間を数える")]
    public Transform trySpot;
    [Header("輪")]
    public Renderer[] ringRenderers;
    public Material ringMat, clearMat;
    public ParticleSystem clearFx;
    public AudioSource sfxClear;
    [Header("先の壁")]
    public GameObject gateBlock;     // プレイヤーの当たり
    public Transform gateVisual;     // 見た目（開くと下へ沈んで消える）
    public AudioSource sfxOpen;
    public float openTime = 0.8f;
    [Header("看板")]
    public GameObject clearMark;
    public GameObject hint;
    [Header("救済")]
    public int hintAfterFails = 3;
    public float hintAfterTime = 45f;
    public int openAfterFails = 10;
    public float openAfterTime = 150f;

    private bool _cleared, _opened;
    private int _failBase = -1;
    private float _time;
    private float _openStart = -1f;
    private Vector3 _gatePos;
    private bool _gateInit;

    private void Start()
    {
        InitGate();
        ApplyIdle();
    }

    private void InitGate()
    {
        if (_gateInit || gateVisual == null) return;
        _gatePos = gateVisual.localPosition; _gateInit = true;
    }

    private void ApplyIdle()
    {
        if (ringRenderers != null) for (int i = 0; i < ringRenderers.Length; i++) if (ringRenderers[i] != null && ringMat != null) ringRenderers[i].sharedMaterial = ringMat;
        if (clearMark != null) clearMark.SetActive(false);
        if (hint != null) hint.SetActive(false);
        if (gateBlock != null) gateBlock.SetActive(true);
        if (gateVisual != null) { gateVisual.gameObject.SetActive(true); if (_gateInit) gateVisual.localPosition = _gatePos; }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_cleared || ball == null || other == null || other.gameObject != ball.gameObject) return;
        Clear();
    }

    private void Update()
    {
        if (_openStart >= 0f && gateVisual != null)
        {
            float k = Mathf.Clamp01((Time.time - _openStart) / Mathf.Max(0.05f, openTime));
            float e = k * k * (3f - 2f * k);
            gateVisual.localPosition = _gatePos + Vector3.down * e * gateHeight;
            if (k >= 1f) { gateVisual.gameObject.SetActive(false); _openStart = -1f; }
        }
        if (_opened || ball == null) return;
        if (ball.GetRespawnPoint() != trySpot) return;
        if (_failBase < 0) _failBase = ball.GetPracticeFails();
        _time += Time.deltaTime;
        int fails = ball.GetPracticeFails() - _failBase;
        if (hint != null && !hint.activeSelf && (fails >= hintAfterFails || _time >= hintAfterTime)) hint.SetActive(true);
        if (!_cleared && (fails >= openAfterFails || _time >= openAfterTime)) Open();
    }

    [Tooltip("開く時に沈む深さ（壁の見た目の高さ）")]
    public float gateHeight = 4.5f;

    private void Clear()
    {
        _cleared = true;
        if (ringRenderers != null) for (int i = 0; i < ringRenderers.Length; i++) if (ringRenderers[i] != null && clearMat != null) ringRenderers[i].sharedMaterial = clearMat;
        if (clearMark != null) clearMark.SetActive(true);
        if (hint != null) hint.SetActive(false);
        if (clearFx != null) clearFx.Play();
        if (sfxClear != null) sfxClear.Play();
        Open();
    }

    private void Open()
    {
        if (_opened) return;
        _opened = true;
        InitGate();
        if (gateBlock != null) gateBlock.SetActive(false);
        _openStart = Time.time;
        if (sfxOpen != null) sfxOpen.Play();
    }

    public bool IsCleared() { return _cleared; }

    // チュートリアルから出た時（FuwaReadMarks.ResetFromRespawn から）
    public void ResetGoal()
    {
        _cleared = false; _opened = false; _failBase = -1; _time = 0f; _openStart = -1f;
        InitGate();
        ApplyIdle();
    }
}
