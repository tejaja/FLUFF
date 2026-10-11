using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// チュートリアルの看板の上に浮かぶ「！」（2026-10-12 てじゃ「まずは説明を読みながら進むってのを解説するなにかほしい」）
// ・看板の上でふわふわ上下しながら、自分の方を向く（自分の画面だけ、同期なし）
// ・看板の近く（readDistance 以内、看板の表側）まで来たら「読んだ」として、ぽんっと縮んで消える
// ・出すのは「次に読む看板」1つだけ（全部先に見えているとくどい。てじゃ）。marks/signs は道の順。先の看板を読んだら、それより前は読んだことにする
// ・ワールドに入り直すとまた出る（保存なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaReadMarks : UdonSharpBehaviour
{
    [Tooltip("「！」の見た目（marks[i] は signs[i] の上）")]
    public Transform[] marks;
    [Tooltip("看板（表が -z 側：文字が読める側）")]
    public Transform[] signs;
    [Tooltip("この距離(m)まで近づいたら読んだことにする")]
    public float readDistance = 3.2f;
    public float bobHeight = 0.08f;
    public float bobSpeed = 2.2f;
    [Tooltip("ぽよぽよ（縦横につぶれて戻る）の強さと速さ")]
    public float squish = 0.12f;
    public float squishSpeed = 4.4f;
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
        Vector3 eye = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
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

            m.position = _base[i] + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + i) * bobHeight);
            if (!_read[i])
            {
                // 出てきた時はぽんっと大きくなる（_goneAt に出た時刻をマイナスで入れてある）
                float appear = _goneAt[i] < 0f ? Mathf.Clamp01((Time.time + _goneAt[i]) / 0.35f) : 1f;
                float pop = appear >= 1f ? 1f : Mathf.Sin(appear * Mathf.PI * 0.5f) * (1f + 0.2f * Mathf.Sin(appear * Mathf.PI));
                // ぽよぽよ：はずむリズムで縦につぶれて横にふくらむ
                float q = Mathf.Sin(Time.time * squishSpeed + i * 1.3f);
                q = q * Mathf.Abs(q);
                m.localScale = new Vector3(_scale[i].x * (1f + squish * q), _scale[i].y * (1f - squish * q), _scale[i].z) * pop;
            }
            Vector3 look = m.position - eye; look.y = 0f;
            if (look.sqrMagnitude > 0.0001f) m.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }
    }
}
