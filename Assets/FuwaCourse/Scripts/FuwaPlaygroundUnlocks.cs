using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// みんなでひろばのおもちゃの解放（2026-10-12 てじゃ「各コースをクリアフラグ立てた人がインスタンスにいると各コースのギミックが一個ずつ解放」）。
// このインスタンスで誰かがコースをクリアしたら（FuwaRecords → FuwaPlaygroundSpawner.Unlock、同期）、そのコースのおもちゃを出す。インスタンスが閉じるまで出たまま。
// 保存はしない（てじゃ「あえて保存なし、『竜巻解放してくるわ』みたいなことが起こる」）。まだの所は札（locked）を出す
// 2026-11-19 てじゃ「あそびばのおもちゃとか、SEつきでパカっと開いておもちゃが拡大してポヨンと出てくるとか」：
//  遊んでいる間に解放された時は、ドームが「むにっ」と縮んでから上へポンと飛んで消え（パカッの音）、おもちゃが小さい所からポヨンとはずんで出てくる（ポヨンの音）。
//  入った直後（すでに解放ずみの物）はアニメなしですぐ出す
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPlaygroundUnlocks : UdonSharpBehaviour
{
    public FuwaPlaygroundSpawner spawner;
    [Tooltip("コースの順（FuwaRecords.courseStarts と同じ）：そのコースがクリアされたら出すおもちゃ")]
    public GameObject[] toys;
    [Tooltip("toys と同じ順：まだの時に出す札")]
    public GameObject[] locked;
    [Tooltip("テスト用：全部出す")]
    public bool unlockAll;

    [Header("出てくるアニメ")]
    [Tooltip("ドームが開く音（パカッ）／おもちゃが出る音（ポヨン）。どちらも鳴らす場所へ動かしてから鳴らす。なくてもOK")]
    public AudioSource openSound, popSound;
    [Tooltip("入ってからこの秒数のうちに出た物はアニメなし（入った時にすでに解放ずみの物）")]
    public float noAnimAfterStart = 4f;
    public float squashTime = 0.12f, flyTime = 0.22f, toyDelay = 0.16f, toyTime = 0.75f;
    [Tooltip("ドームが飛ぶ高さ(m)")]
    public float flyUp = 0.7f;

    private float _next;
    private float _startAt;
    private bool[] _shown = new bool[0];
    private float[] _animAt = new float[0];
    private bool[] _toyOn = new bool[0];
    private Vector3[] _lockScale = new Vector3[0];
    private Vector3[] _lockPos = new Vector3[0];
    private Vector3[] _toyScale = new Vector3[0];
    private int _animCount;

    private void Start() { _next = 0f; _startAt = Time.time; }

    private void Update()
    {
        if (_animCount > 0) Animate();
        if (Time.time < _next) return;
        _next = Time.time + 1f;
        Refresh();
    }

    private void Refresh()
    {
        if (spawner == null || toys == null) return;
        int n = toys.Length;
        if (_shown.Length != n)
        {
            _shown = new bool[n]; _animAt = new float[n]; _toyOn = new bool[n];
            _lockScale = new Vector3[n]; _lockPos = new Vector3[n]; _toyScale = new Vector3[n];
            for (int i = 0; i < n; i++) { _animAt[i] = -1f; Set(i, false, true); }
        }
        for (int i = 0; i < n; i++)
        {
            bool on = unlockAll || spawner.Unlocked(i);
            if (on != _shown[i])
            {
                if (on && Time.time - _startAt > noAnimAfterStart) StartAnim(i);
                else Set(i, on, false);
            }
        }
    }

    private void Set(int i, bool on, bool force)
    {
        _shown[i] = on;
        if (toys[i] != null && (force || toys[i].activeSelf != on)) toys[i].SetActive(on);
        if (locked != null && i < locked.Length && locked[i] != null) locked[i].SetActive(!on);
    }

    private GameObject LockedOf(int i) { return (locked != null && i < locked.Length) ? locked[i] : null; }

    private void StartAnim(int i)
    {
        _shown[i] = true;
        if (_animAt[i] < 0f) _animCount++;
        _animAt[i] = Time.time; _toyOn[i] = false;
        GameObject l = LockedOf(i);
        if (l != null) { l.SetActive(true); _lockScale[i] = l.transform.localScale; _lockPos[i] = l.transform.localPosition; }
        if (toys[i] != null) _toyScale[i] = toys[i].transform.localScale;
        if (openSound != null && l != null) { openSound.transform.position = l.transform.position + Vector3.up * 0.5f; openSound.Play(); }
    }

    private void Animate()
    {
        for (int i = 0; i < _animAt.Length; i++)
        {
            if (_animAt[i] < 0f) continue;
            float t = Time.time - _animAt[i];
            GameObject l = LockedOf(i);
            // ドーム：むにっと縮んで（横に広がる）→ 上へポンと飛びながら小さくなって消える
            if (l != null && l.activeSelf)
            {
                Vector3 s0 = _lockScale[i];
                if (t < squashTime)
                {
                    float u = t / squashTime; float k = Mathf.Sin(u * Mathf.PI * 0.5f);
                    l.transform.localScale = new Vector3(s0.x * (1f + 0.18f * k), s0.y * (1f - 0.3f * k), s0.z * (1f + 0.18f * k));
                    l.transform.localPosition = _lockPos[i];
                }
                else
                {
                    float u = Mathf.Clamp01((t - squashTime) / flyTime);
                    float k = 1f - u * u;
                    float up = flyUp * (1f - (1f - u) * (1f - u));
                    l.transform.localScale = new Vector3(s0.x * 1.18f * k, s0.y * (0.7f + 0.5f * (1f - k)) * k, s0.z * 1.18f * k);
                    l.transform.localPosition = _lockPos[i] + l.transform.parent.InverseTransformVector(Vector3.up * up);
                    if (u >= 1f) { l.SetActive(false); l.transform.localScale = s0; l.transform.localPosition = _lockPos[i]; }
                }
            }
            // おもちゃ：少し遅れて、小さい所からポヨンとはずんで出る（行きすぎて少しゆれて止まる）
            GameObject toy = toys[i];
            float tt = t - toyDelay;
            if (toy == null) { if (t > squashTime + flyTime) { _animAt[i] = -1f; _animCount--; } continue; }
            if (tt < 0f) continue;
            if (!_toyOn[i])
            {
                _toyOn[i] = true; toy.SetActive(true);
                if (popSound != null) { popSound.transform.position = toy.transform.position + Vector3.up * 0.5f; popSound.Play(); }
            }
            float uu = Mathf.Clamp01(tt / toyTime);
            float kk = uu >= 1f ? 1f : 1f - Mathf.Exp(-7f * uu) * Mathf.Cos(uu * 17f);
            float ky = kk, kxz = kk;
            // 縦と横を少しずらして、ぷるんとした感じに
            if (uu < 1f) { float w = Mathf.Exp(-6f * uu) * Mathf.Sin(uu * 17f) * 0.12f; ky = kk + w; kxz = kk - w * 0.6f; }
            Vector3 b = _toyScale[i];
            toy.transform.localScale = new Vector3(b.x * Mathf.Max(0.001f, kxz), b.y * Mathf.Max(0.001f, ky), b.z * Mathf.Max(0.001f, kxz));
            if (uu >= 1f)
            {
                toy.transform.localScale = b;
                if (l != null && l.activeSelf) { l.SetActive(false); l.transform.localScale = _lockScale[i]; l.transform.localPosition = _lockPos[i]; }
                _animAt[i] = -1f; _animCount--;
            }
        }
    }
}
