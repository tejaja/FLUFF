using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// チュートリアルの課題（2026-11-24 てじゃ「課題式にするなら全部やりたい」「壁を通過不可にして、課題クリアで壁解放」）
// ・自分のふわふわが輪（このオブジェクトのトリガー）をくぐったらクリア：輪の色が変わる・音・キラキラ・看板に「クリア！」
// ・先の壁は1枚だけ・ずっとある（ふわふわは当たると割れる）。クリア前は人も通れない。
//   クリアすると壁の真ん中が扉の形にくり抜かれて通れるようになり、地面に前向きの矢印が出る（次の看板に着いたら消える）
//   （2026-11-24 てじゃ「壁は1枚を常設」「扉みたいに真ん中が切り抜かれる」「地面に前向きの矢印、次の看板にいくまで」「文字はなし」）
// ・救済：この区画で何回か失敗するか、しばらくたったら看板にコツ。さらに失敗が続いたら扉だけ開く（クリア扱いにはしない）
// ・自分にだけ（同期なし）。チュートリアルから出たら FuwaReadMarks から ResetGoal で最初の状態に戻す
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaTutorialGoal : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("この区画のためし撃ちの場所。ふわふわがここを戻り先にしている間だけ、失敗の回数と時間を数える")]
    public Transform trySpot;
    [Tooltip("まものの区画：このまものが倒れたらクリア（輪のかわり）")]
    public FuwaEnemy monster;
    [Header("輪")]
    public Renderer[] ringRenderers;
    public Material ringMat, clearMat;
    public ParticleSystem clearFx;
    public AudioSource sfxClear;
    [Header("先の壁")]
    public GameObject gateBlock;     // プレイヤーの当たり（クリアで消す）
    public GameObject gateSolid;     // 見た目：くり抜く前
    public GameObject gateDoor;      // 見た目：扉の形にくり抜いた後
    public ParticleSystem doorFx;    // くり抜いた時のぽふっ
    public AudioSource sfxOpen;
    [Header("扉がふわっと開く（2026-11-25 てじゃ「一瞬でぱっと開くけど、もっとフワっと」）")]
    [Tooltip("扉の形の板（壁と同じ材質）。開く時だけ出して、少し明るくなってから溶けるように消す")]
    public Renderer gatePlug;
    [Tooltip("扉のふちの白い帯。板が消えるのに合わせてじわっと出す")]
    public Renderer doorEdge;
    public float openTime = 1.2f;
    public float openGlowTime = 0.25f;
    public float openGlow = 1.8f;
    [Header("壁を出すタイミング（2026-11-25 てじゃ「次の壁は、前の扉くぐったあたりで出る」）")]
    [Tooltip("次の区画の課題。この扉を人かふわふわがくぐったら、その壁をふわっと出す")]
    public FuwaTutorialGoal nextGoal;
    [Tooltip("最初の区画：この課題（輪）が出た時に壁も出す")]
    public bool wallOnEnable;
    public float wallFadeTime = 0.8f;
    [Header("地面の矢印")]
    public GameObject arrows;
    [Tooltip("ここ（次の看板）に近づいたら矢印を消す")]
    public Transform arrowsHideAt;
    public float arrowsHideDist = 3f;
    [Tooltip("矢印を手前から奥へ順番にゆっくり光らせる（2026-11-24 てじゃ）")]
    public Renderer[] arrowRenderers;
    public Material arrowDim, arrowLit;
    public float arrowStep = 0.45f;
    [Header("クリアで出す物（次の看板・次の輪。2026-11-24 てじゃ「課題クリア時に出す」）")]
    public FuwaReadMarks readMarks;
    public GameObject[] revealOnClear;
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

    private float _openT = -1f;
    private MaterialPropertyBlock _mpb;
    private float _plugA, _plugL, _plugB;

    private bool _inited, _wallShown, _passed;
    private float _wallT = -1f;
    private Vector3 _gatePos, _gateFwd;
    private void Init()
    {
        if (_inited) return;
        _inited = true;
        _mpb = new MaterialPropertyBlock();
        Renderer src = gatePlug != null ? gatePlug : (gateSolid != null ? gateSolid.GetComponent<Renderer>() : null);
        if (src != null && src.sharedMaterial != null)
        {
            Material pm = src.sharedMaterial;
            _plugA = pm.GetFloat("_Alpha"); _plugL = pm.GetFloat("_LineAlpha"); _plugB = pm.GetFloat("_BandAlpha");
        }
        // 扉をくぐった判定：壁の面の、次の看板がある側
        if (gateBlock != null)
        {
            _gatePos = gateBlock.transform.position;
            _gateFwd = gateBlock.transform.forward; _gateFwd.y = 0f; _gateFwd.Normalize();
            if (arrowsHideAt != null && Vector3.Dot(arrowsHideAt.position - _gatePos, _gateFwd) < 0f) _gateFwd = -_gateFwd;
        }
    }

    private void Start()
    {
        Init();
        ApplyIdle();
    }

    private void OnEnable()
    {
        if (wallOnEnable) ShowWall();
    }

    // 最初の区画：輪が隠れたら（チュートリアルの最初に戻った時など）壁も隠す
    private void OnDisable()
    {
        if (!wallOnEnable || _opened) return;
        _wallShown = false; _wallT = -1f;
        if (gateSolid != null) gateSolid.SetActive(false);
    }

    // 壁をふわっと出す（前の区画の扉をくぐった時・最初の区画は輪が出た時）
    public void ShowWall()
    {
        Init();
        if (_wallShown) return;
        _wallShown = true;
        if (_opened) return;
        if (gateSolid != null) gateSolid.SetActive(true);
        _wallT = 0f; FadeWall(0f);
    }

    private void FadeWall(float k)
    {
        if (gateSolid == null) return;
        Renderer r = gateSolid.GetComponent<Renderer>();
        if (r == null) return;
        if (k >= 1f) { _mpb.Clear(); r.SetPropertyBlock(_mpb); return; }
        _mpb.Clear();
        _mpb.SetFloat("_Alpha", _plugA * k); _mpb.SetFloat("_LineAlpha", _plugL * k); _mpb.SetFloat("_BandAlpha", _plugB * k);
        r.SetPropertyBlock(_mpb);
    }

    // 扉の板：はじめ openGlowTime 秒で少し明るく（ふわっ）→ 残りでなめらかに消える。ふちの帯は後半でじわっと出る
    private void AnimOpen()
    {
        float t = _openT;
        float k, e;
        if (t < openGlowTime) { float u = t / Mathf.Max(0.01f, openGlowTime); u = u * u * (3f - 2f * u); k = Mathf.Lerp(1f, openGlow, u); }
        else { float u = Mathf.Clamp01((t - openGlowTime) / Mathf.Max(0.01f, openTime - openGlowTime)); u = u * u * (3f - 2f * u); k = openGlow * (1f - u); }
        { float u = Mathf.Clamp01((t - openTime * 0.3f) / Mathf.Max(0.01f, openTime * 0.7f)); e = u * u * (3f - 2f * u); }
        if (gatePlug != null)
        {
            _mpb.Clear();
            _mpb.SetFloat("_Alpha", Mathf.Min(1f, _plugA * k)); _mpb.SetFloat("_LineAlpha", Mathf.Min(1f, _plugL * k)); _mpb.SetFloat("_BandAlpha", Mathf.Min(1f, _plugB * k));
            gatePlug.SetPropertyBlock(_mpb);
        }
        SetEdge(e);
        if (t >= openTime)
        {
            _openT = -1f;
            if (gatePlug != null) gatePlug.gameObject.SetActive(false);
            SetEdge(1f);
        }
    }

    private void SetEdge(float a)
    {
        if (doorEdge == null) return;
        _mpb.Clear(); _mpb.SetColor("_Color", new Color(1f, 1f, 1f, a));
        doorEdge.SetPropertyBlock(_mpb);
    }

    // 人を止める壁にふわふわは当たらない（跳ね返らずに、奥のふわふわを割る壁で消える。2026-11-24 てじゃ「壁、反射なしでクリア前も消滅」）
    //  IgnoreCollision はどちらかが非アクティブになると消えるので、この区画にいる間は時々かけ直す
    private float _nextIgnore;
    private void IgnoreBall()
    {
        if (ball == null || gateBlock == null || !gateBlock.activeInHierarchy) return;
        Collider bc = (Collider)ball.GetComponent(typeof(Collider));
        Collider gc = (Collider)gateBlock.GetComponent(typeof(Collider));
        if (bc != null && gc != null) Physics.IgnoreCollision(bc, gc, true);
    }

    private void ApplyIdle()
    {
        if (ringRenderers != null) for (int i = 0; i < ringRenderers.Length; i++) if (ringRenderers[i] != null && ringMat != null) ringRenderers[i].sharedMaterial = ringMat;
        if (clearMark != null) clearMark.SetActive(false);
        if (hint != null) hint.SetActive(false);
        if (gateBlock != null) gateBlock.SetActive(true);
        if (gateSolid != null) gateSolid.SetActive(_wallShown);
        if (gateDoor != null) gateDoor.SetActive(false);
        if (gatePlug != null) gatePlug.gameObject.SetActive(false);
        _openT = -1f; _passed = false;
        if (_wallT < 0f) FadeWall(1f);
        SetEdge(1f);
        if (arrows != null) arrows.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_cleared || ball == null || other == null || other.gameObject != ball.gameObject) return;
        Clear();
    }

    private void Update()
    {
        if (_openT >= 0f) { _openT += Time.deltaTime; AnimOpen(); }
        if (_wallT >= 0f)
        {
            _wallT += Time.deltaTime;
            float u = Mathf.Clamp01(_wallT / Mathf.Max(0.01f, wallFadeTime)); u = u * u * (3f - 2f * u);
            if (_wallT >= wallFadeTime) { _wallT = -1f; FadeWall(1f); } else FadeWall(u);
        }
        // この扉を人かふわふわがくぐったら、次の区画の壁を出す
        if (_opened && !_passed && nextGoal != null)
        {
            VRCPlayerApi lpp = Networking.LocalPlayer;
            bool through = false;
            if (lpp != null) { Vector3 d = lpp.GetPosition() - _gatePos; if (Vector3.Dot(d, _gateFwd) > 0.3f && d.sqrMagnitude < 64f) through = true; }
            if (!through && ball != null) { Vector3 d = ball.transform.position - _gatePos; if (Vector3.Dot(d, _gateFwd) > 0.3f && d.sqrMagnitude < 64f) through = true; }
            if (through) { _passed = true; nextGoal.ShowWall(); }
        }
        if (arrows != null && arrows.activeSelf && arrowRenderers != null && arrowRenderers.Length > 0 && arrowLit != null)
        {
            int n = arrowRenderers.Length;
            int lit = Mathf.FloorToInt(Time.time / Mathf.Max(0.05f, arrowStep)) % (n + 1);   // 0..n-1 を順に、n は全部消えて間
            for (int i = 0; i < n; i++) if (arrowRenderers[i] != null) { Material m = i == lit ? arrowLit : arrowDim; if (arrowRenderers[i].sharedMaterial != m) arrowRenderers[i].sharedMaterial = m; }
        }
        if (arrows != null && arrows.activeSelf && arrowsHideAt != null)
        {
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (lp != null && Vector3.Distance(lp.GetPosition(), arrowsHideAt.position) < arrowsHideDist) arrows.SetActive(false);
        }
        // まものの区画：クリア後に戻ってきて、ふわふわがこの区画に出たら、まものもいっしょに復活（2026-11-24 てじゃ）
        if (ball != null && monster != null)
        {
            bool here = ball.GetRespawnPoint() == trySpot;
            if (here && !_wasHere && _cleared && monster.IsDefeated()) monster.Revive();
            _wasHere = here;
        }
        if (_opened || ball == null) return;
        if (ball.GetRespawnPoint() != trySpot) return;
        if (Time.time >= _nextIgnore) { _nextIgnore = Time.time + 0.5f; IgnoreBall(); }
        if (!_cleared && monster != null && monster.IsDefeated()) { if (clearFx != null) clearFx.transform.position = monster.transform.position + Vector3.up * 0.4f; Clear(); return; }
        if (_failBase < 0) _failBase = ball.GetPracticeFails();
        _time += Time.deltaTime;
        int fails = ball.GetPracticeFails() - _failBase;
        if (hint != null && !hint.activeSelf && (fails >= hintAfterFails || _time >= hintAfterTime)) hint.SetActive(true);
        if (!_cleared && (fails >= openAfterFails || _time >= openAfterTime)) Open();
    }

    private float _monRevive = -1f;
    private bool _wasHere;
    private void Clear()
    {
        _cleared = true;
        // まものの区画：クリアしたらもう復活しない（2026-11-24 てじゃ「倒しても一定時間で復活してる」）。チュートリアルを出たら戻す
        if (monster != null) { if (_monRevive < 0f) _monRevive = monster.autoReviveDelay; monster.autoReviveDelay = 0f; }
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
        if (gateBlock != null) gateBlock.SetActive(false);
        if (gateSolid != null) gateSolid.SetActive(false);
        if (gateDoor != null) gateDoor.SetActive(true);
        _wallShown = true; _wallT = -1f; FadeWall(1f);
        if (gatePlug != null) { gatePlug.gameObject.SetActive(true); _openT = 0f; AnimOpen(); }
        if (doorFx != null) doorFx.Play();
        if (arrows != null) arrows.SetActive(true);
        if (sfxOpen != null) sfxOpen.Play();
        if (revealOnClear != null) for (int i = 0; i < revealOnClear.Length; i++)
            {
                if (revealOnClear[i] == null) continue;
                if (readMarks != null) readMarks.RevealNow(revealOnClear[i]); else revealOnClear[i].SetActive(true);
            }
    }

    public bool IsCleared() { return _cleared; }

    // チュートリアルから出た時（FuwaReadMarks.ResetFromRespawn から）
    public void ResetGoal()
    {
        Init();
        _cleared = false; _opened = false; _failBase = -1; _time = 0f;
        _wallShown = false; _wallT = -1f; FadeWall(1f);
        if (monster != null && _monRevive >= 0f) { monster.autoReviveDelay = _monRevive; monster.Revive(); }
        ApplyIdle();
        if (wallOnEnable && gameObject.activeInHierarchy) ShowWall();
    }
}
