using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// チュートリアルの看板の上に浮かぶ「！」（2026-10-12 てじゃ「まずは説明を読みながら進むってのを解説するなにかほしい」）
// ・看板の上でぴょんぴょん跳ねながらくるくる回る赤い▼（スタートの▼と同じ形で色だけ赤。2026-10-12 てじゃ「！より▽、スタートとかのあれ」）。自分の画面だけ、同期なし
// ・看板の近く（readDistance 以内、看板の表側）まで来たら「読んだ」として、ぽんっと縮んで消える
// ・出すのは「次に読む看板」1つだけ（全部先に見えているとくどい。てじゃ）。marks/signs は道の順。先の看板を読んだら、それより前は読んだことにする
// ・ワールドに入り直すとまた出る（保存なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaReadMarks : UdonSharpBehaviour
{
    [Tooltip("目印（▼）（marks[i] は signs[i] の上）")]
    public Transform[] marks;
    [Tooltip("看板（表が -z 側：文字が読める側）")]
    public Transform[] signs;
    [Tooltip("この距離(m)まで近づいたら読んだことにする")]
    public float readDistance = 3.2f;
    // 跳ねる（2026-10-12 てじゃ「ふわんふわんじゃなくて、もうちょい跳ねる感じ」）：
    // ぴょんっと跳んで（上がる・落ちる時は縦にのびる）、着地でむにっとつぶれて、また跳ぶ
    [Tooltip("跳ぶ高さ(m)")]
    public float hopHeight = 0.28f;
    [Tooltip("空中にいる秒数")]
    public float hopTime = 0.56f;   // てじゃ「もうちょっぴり遅く」で 0.46→0.56
    [Tooltip("着地してつぶれている秒数")]
    public float landTime = 0.24f;
    [Tooltip("着地でつぶれる強さ")]
    public float squish = 0.3f;
    [Tooltip("跳ぶ・落ちる時に縦にのびる強さ")]
    public float stretch = 0.14f;
    [Tooltip("目印の高さの半分(m)。つぶれる時に下はし（▼の先）の位置を変えないため")]
    public float radius = 0.25f;
    [Tooltip("▼の回る速さ（度/秒）。スタートの▼と同じ")]
    public float spinSpeed = 60f;
    [Tooltip("自分から遠い時は動かさない(m)")]
    public float activeRange = 30f;

    private bool[] _read;
    private float[] _goneAt;
    private Vector3[] _base;
    private Vector3[] _scale;
    private int _next;

    private void Start()
    {
        int n = marks != null ? marks.Length : 0;
        _read = new bool[n]; _goneAt = new float[n]; _base = new Vector3[n]; _scale = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            if (marks[i] == null) continue;
            _base[i] = marks[i].position; _scale[i] = marks[i].localScale; _goneAt[i] = -1f;
            marks[i].gameObject.SetActive(i == 0);
        }
        _next = 0;
    }

    private void Update()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null || marks == null) return;
        Vector3 me = lp.GetPosition();
        // どの看板でも、読んだら（前に立ったら）そこまで進める
        for (int i = _next; i < marks.Length && i < signs.Length; i++)
        {
            if (signs[i] == null || _read[i]) continue;
            Transform sg = signs[i];
            Vector3 d = me - sg.position; d.y = 0f;
            if (d.sqrMagnitude > readDistance * readDistance) continue;
            if (Vector3.Dot(d, -sg.forward) <= 0f) continue;   // 文字の読める側にいる
            for (int j = _next; j <= i; j++)
            {
                if (_read[j]) continue;
                _read[j] = true; _goneAt[j] = Time.time;
                if (j < i && marks[j] != null) marks[j].gameObject.SetActive(false);   // 飛ばした看板のはすぐ消す
            }
            _next = i + 1;
            if (_next < marks.Length && marks[_next] != null) { marks[_next].gameObject.SetActive(true); marks[_next].localScale = Vector3.zero; _goneAt[_next] = -Time.time; }
        }
        for (int i = 0; i < marks.Length; i++)
        {
            Transform m = marks[i];
            if (m == null || !m.gameObject.activeSelf) continue;
            if ((me - _base[i]).sqrMagnitude > activeRange * activeRange) continue;
            if (_read[i])
            {
                float t = (Time.time - _goneAt[i]) / 0.25f;
                if (t >= 1f) { m.gameObject.SetActive(false); continue; }
                float s = t < 0.3f ? 1f + 0.25f * (t / 0.3f) : 1.25f * (1f - (t - 0.3f) / 0.7f);   // ちょっとふくらんでから消える
                m.localScale = _scale[i] * Mathf.Max(0f, s);
            }

            // 跳ねる：1回 = 空中 hopTime ＋ 着地 landTime。高さは放物線、のび・つぶれは縦横で体積を保つ感じに
            float cyc = hopTime + landTime;
            float p = Mathf.Repeat(Time.time + i * 0.37f, cyc);
            float h = 0f, sy = 1f;
            if (p < hopTime)
            {
                float u = p / hopTime;
                h = hopHeight * 4f * u * (1f - u);
                sy = 1f + stretch * Mathf.Abs(1f - 2f * u);   // 速い時（跳んだ直後・着地直前）ほどのびる
            }
            else
            {
                float g = (p - hopTime) / landTime;
                sy = 1f - squish * Mathf.Sin(g * Mathf.PI);   // むにっ
            }
            float sx = 1f / Mathf.Sqrt(Mathf.Max(0.2f, sy));
            // 下はしの位置はそのまま（つぶれても地面から浮かない・めりこまない）
            m.position = _base[i] + Vector3.up * (h - radius * (1f - sy));
            if (!_read[i])
            {
                // 出てきた時はぽんっと大きくなる（_goneAt に出た時刻をマイナスで入れてある）
                float appear = _goneAt[i] < 0f ? Mathf.Clamp01((Time.time + _goneAt[i]) / 0.35f) : 1f;
                float pop = appear >= 1f ? 1f : Mathf.Sin(appear * Mathf.PI * 0.5f) * (1f + 0.2f * Mathf.Sin(appear * Mathf.PI));
                m.localScale = new Vector3(_scale[i].x * sx, _scale[i].y * sy, _scale[i].z * sx) * pop;
            }
            m.rotation = Quaternion.Euler(0f, Time.time * spinSpeed + i * 40f, 0f);
        }
    }
}
