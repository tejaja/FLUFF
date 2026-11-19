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
    // 2026-11-19 てじゃ「後ろにドームをパカっと開く感じにできないかな」：ドームの奥（ひろばのまん中から見て向こう側）のふちを軸に、ふたのように奥へ開く → 開いたまま少し止まって → 小さくなって消える
    //  → 同日「もうちょいゆっくりでいい」「最後に床に貫通してるのをどうにかしたい」：時間をのばし、開く角度は 90° をこえない（こえると軸の向こうで床にもぐる）
    public float squashTime = 0.16f, openTime = 0.55f, holdTime = 0.45f, vanishTime = 0.35f, toyDelay = 0.4f, toyTime = 1.0f;
    [Tooltip("ドームが開く角度（行きすぎ分をたしても 90° をこえないように）")]
    public float openAngle = 80f;
    [Tooltip("ふちの輪の太さ（半径）・高さと、開いた時に持ち上げる高さ（m）")]
    public float rimTube = 0.05f, rimY = 0.02f, openLift = 0.05f;

    private float _next;
    private float _startAt;
    private bool[] _shown = new bool[0];
    private float[] _animAt = new float[0];
    private bool[] _toyOn = new bool[0];
    private bool[] _openPlayed = new bool[0];   // パカッの音は、むにっと縮んだあと開き始める時に鳴らす
    // 2026-11-19 てじゃ「岩が解放時にやたら上から落ちてくる」：物理で動く物（重力つきの Rigidbody）が入っているおもちゃは、大きくするアニメをしない（親ごと拡大すると物理の位置がずれて飛ぶ）
    private bool[] _noScale = new bool[0];
    private Vector3[] _lockScale = new Vector3[0];
    private Vector3[] _lockPos = new Vector3[0];
    private Vector3[] _toyScale = new Vector3[0];
    private Transform[] _dome = new Transform[0];
    private Vector3[] _domePos = new Vector3[0], _domeScale = new Vector3[0];
    private Quaternion[] _domeRot = new Quaternion[0];
    // ドームのふちの輪（Rim）もふたといっしょに開く（てじゃの確認用に撮ったら、輪があるとふたが開くのが分かりやすかった）
    private Transform[] _rim = new Transform[0];
    private Vector3[] _rimPos = new Vector3[0], _rimScale = new Vector3[0];
    private Quaternion[] _rimRot = new Quaternion[0];
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
            _shown = new bool[n]; _animAt = new float[n]; _toyOn = new bool[n]; _openPlayed = new bool[n]; _noScale = new bool[n];
            _lockScale = new Vector3[n]; _lockPos = new Vector3[n]; _toyScale = new Vector3[n];
            _dome = new Transform[n]; _domePos = new Vector3[n]; _domeScale = new Vector3[n]; _domeRot = new Quaternion[n];
            _rim = new Transform[n]; _rimPos = new Vector3[n]; _rimScale = new Vector3[n]; _rimRot = new Quaternion[n];
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
        _animAt[i] = Time.time; _toyOn[i] = false; _openPlayed[i] = false;
        GameObject l = LockedOf(i);
        if (l != null)
        {
            l.SetActive(true); _lockScale[i] = l.transform.localScale; _lockPos[i] = l.transform.localPosition;
            Transform d = l.transform.Find("Dome"); _dome[i] = d;
            if (d != null) { _domePos[i] = d.localPosition; _domeRot[i] = d.localRotation; _domeScale[i] = d.localScale; }
            Transform rm = l.transform.Find("Rim"); _rim[i] = rm;
            if (rm != null) { _rimPos[i] = rm.localPosition; _rimRot[i] = rm.localRotation; _rimScale[i] = rm.localScale; }
        }
        if (toys[i] != null)
        {
            _toyScale[i] = toys[i].transform.localScale;
            Rigidbody[] rbs = toys[i].GetComponentsInChildren<Rigidbody>(true);
            bool phys = false;
            for (int k = 0; k < rbs.Length; k++) if (rbs[k] != null && !rbs[k].isKinematic) { phys = true; break; }
            _noScale[i] = phys;
        }
    }

    private void Animate()
    {
        for (int i = 0; i < _animAt.Length; i++)
        {
            if (_animAt[i] < 0f) continue;
            float t = Time.time - _animAt[i];
            GameObject l = LockedOf(i);
            // ドーム：むにっと縮んで → 奥のふちを軸に、ふたのように奥へパカッと開く（行きすぎて戻る）→ 少し止まって小さくなって消える。ほかの部品（札・看板・ふち）は開くと同時に縮む
            if (l != null && l.activeSelf)
            {
                Transform lt = l.transform;
                Transform d = _dome[i]; Transform rm = _rim[i];
                if (!_openPlayed[i] && t >= squashTime)
                {
                    _openPlayed[i] = true;
                    if (openSound != null) { openSound.transform.position = lt.position + Vector3.up * 0.5f; openSound.Play(); }
                }
                float tOpen = t - squashTime, tVan = t - squashTime - openTime - holdTime;
                // ほかの部品：開き始めから openTime で縮んで消える（ドームは別）
                float ko = tOpen <= 0f ? 1f : 1f - Mathf.Clamp01(tOpen / openTime);
                for (int c = 0; c < lt.childCount; c++)
                {
                    Transform ch = lt.GetChild(c); if (ch == d || ch == rm) continue;
                    ch.localScale = Vector3.one * Mathf.Max(0.001f, ko * ko);
                }
                if (d != null)
                {
                    Vector3 p0 = _domePos[i]; Quaternion r0 = _domeRot[i]; Vector3 s0 = _domeScale[i];
                    if (t < squashTime)
                    {
                        float u = t / squashTime; float k = Mathf.Sin(u * Mathf.PI * 0.5f);
                        d.localScale = new Vector3(s0.x * (1f + 0.1f * k), s0.y * (1f - 0.18f * k), s0.z * (1f + 0.1f * k));
                    }
                    else
                    {
                        // 奥の向き（ひろばのまん中 → このドーム）と、奥の下のふち（軸）
                        Vector3 back = lt.position - transform.position; back.y = 0f;
                        back = back.sqrMagnitude > 1e-4f ? back.normalized : lt.forward;
                        Vector3 axis = Vector3.Cross(Vector3.up, back);
                        float rad = d.lossyScale.x * 0.5f;
                        Vector3 baseW = lt.TransformPoint(p0);
                        // 軸はふちの輪のいちばん外・いちばん下（2026-11-19 てじゃ「蓋の縁がまだ床にちょっとうまってる、あいたときね」：
                        //  ドームの底のふちを軸にすると、輪の太さの分だけ軸の外・下にはみ出た所が床にもぐった）。開くにつれてふた全体を少し持ち上げる
                        float sy = lt.lossyScale.y;
                        Vector3 hinge = baseW + back * (rad + rimTube * sy) + Vector3.up * (rimY - rimTube) * sy;
                        float u = Mathf.Clamp01(tOpen / openTime);
                        float c1 = 1.70158f, c3 = c1 + 1f, v = u - 1f;   // 行きすぎは約 10%
                        float ang = Mathf.Min(89f, openAngle * (1f + c3 * v * v * v + c1 * v * v));
                        Quaternion q = Quaternion.AngleAxis(ang, axis);
                        Vector3 lift = Vector3.up * (openLift * sy * Mathf.Clamp01(u * 1.5f));
                        d.position = hinge + q * (baseW - hinge) + lift;
                        d.rotation = q * (lt.rotation * r0);
                        float kv = tVan <= 0f ? 1f : 1f - Mathf.Clamp01(tVan / vanishTime);
                        d.localScale = s0 * Mathf.Max(0.001f, kv);
                        if (rm != null)
                        {
                            Vector3 rw = lt.TransformPoint(_rimPos[i]);
                            rm.position = hinge + q * (rw - hinge) + lift;
                            rm.rotation = q * (lt.rotation * _rimRot[i]);
                            rm.localScale = _rimScale[i] * Mathf.Max(0.001f, kv);
                        }
                        if (tVan >= vanishTime)
                        {
                            l.SetActive(false);
                            d.localPosition = p0; d.localRotation = r0; d.localScale = s0;
                            if (rm != null) { rm.localPosition = _rimPos[i]; rm.localRotation = _rimRot[i]; rm.localScale = _rimScale[i]; }
                            for (int c = 0; c < lt.childCount; c++) { Transform ch = lt.GetChild(c); if (ch != d && ch != rm) ch.localScale = Vector3.one; }
                        }
                    }
                }
                else if (tOpen >= openTime) l.SetActive(false);
            }
            // おもちゃ：少し遅れて、小さい所からポヨンとはずんで出る（行きすぎて少しゆれて止まる）
            GameObject toy = toys[i];
            float tt = t - toyDelay;
            if (toy == null) { if (t > squashTime + openTime + holdTime + vanishTime) { _animAt[i] = -1f; _animCount--; } continue; }
            if (tt < 0f) continue;
            if (!_toyOn[i])
            {
                _toyOn[i] = true; toy.SetActive(true);
                if (popSound != null) { popSound.transform.position = toy.transform.position + Vector3.up * 0.5f; popSound.Play(); }
            }
            float uu = _noScale[i] ? 1f : Mathf.Clamp01(tt / toyTime);
            float kk = uu >= 1f ? 1f : 1f - Mathf.Exp(-7f * uu) * Mathf.Cos(uu * 17f);
            float ky = kk, kxz = kk;
            // 縦と横を少しずらして、ぷるんとした感じに
            if (uu < 1f) { float w = Mathf.Exp(-6f * uu) * Mathf.Sin(uu * 17f) * 0.12f; ky = kk + w; kxz = kk - w * 0.6f; }
            Vector3 b = _toyScale[i];
            toy.transform.localScale = new Vector3(b.x * Mathf.Max(0.001f, kxz), b.y * Mathf.Max(0.001f, ky), b.z * Mathf.Max(0.001f, kxz));
            if (uu >= 1f)
            {
                toy.transform.localScale = b;
                if (t >= squashTime + openTime + holdTime + vanishTime) { _animAt[i] = -1f; _animCount--; }
            }
        }
    }
}
