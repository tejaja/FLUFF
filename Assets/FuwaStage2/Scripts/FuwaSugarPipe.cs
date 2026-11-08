using UdonSharp;
using UnityEngine;

// 2–4 はぐるまのとう：色ざらめのパイプ（2026-11-08 てじゃ「周りの自動で回ってる歯車の末端も、何かしらの形でつなげて」→ A 色ざらめパイプ）
// ・しかけの最後の歯車が動かすポンプ（FuwaCloudFactory）がひと押しするたびに Send() → パイプの中をざらめのふくらみ（bulges）が流れて、まん中のわたあめ機の鍋へ
// ・パイプの道すじは pts（世界の点、つなぎ目）。ふくらみは使い回し。着いたら endPuff（その色の雲の糸）を出す
// 自分の画面だけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSugarPipe : UdonSharpBehaviour
{
    public Vector3[] pts;
    public float speed = 4f;
    public Transform[] bulges;
    public ParticleSystem endPuff;
    public int endCount = 10;

    private float[] _cum;
    private float[] _at;
    private float _total;

    private void Start()
    {
        int n = pts != null ? pts.Length : 0;
        _cum = new float[Mathf.Max(1, n)];
        for (int i = 1; i < n; i++) _cum[i] = _cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);
        _total = n > 0 ? _cum[n - 1] : 0f;
        int b = bulges != null ? bulges.Length : 0;
        _at = new float[b];
        for (int i = 0; i < b; i++) { _at[i] = -1f; if (bulges[i] != null) bulges[i].gameObject.SetActive(false); }
    }

    public void Send()
    {
        if (_at == null) return;
        for (int i = 0; i < _at.Length; i++)
            if (_at[i] < 0f && bulges[i] != null) { _at[i] = 0f; bulges[i].gameObject.SetActive(true); Place(i); return; }
    }

    private void Place(int i)
    {
        float d = _at[i];
        int n = pts.Length;
        for (int k = 1; k < n; k++)
        {
            if (d <= _cum[k] || k == n - 1)
            {
                float seg = _cum[k] - _cum[k - 1];
                float t = seg > 1e-4f ? Mathf.Clamp01((d - _cum[k - 1]) / seg) : 1f;
                bulges[i].position = Vector3.Lerp(pts[k - 1], pts[k], t);
                Vector3 dir = pts[k] - pts[k - 1];
                if (dir.sqrMagnitude > 1e-6f) bulges[i].rotation = Quaternion.LookRotation(dir.normalized, Mathf.Abs(dir.normalized.y) > 0.9f ? Vector3.forward : Vector3.up);
                return;
            }
        }
    }

    private void Update()
    {
        if (_at == null || pts == null || pts.Length < 2) return;
        float dt = Time.deltaTime;
        for (int i = 0; i < _at.Length; i++)
        {
            if (_at[i] < 0f) continue;
            _at[i] += speed * dt;
            if (_at[i] >= _total)
            {
                _at[i] = -1f; bulges[i].gameObject.SetActive(false);
                if (endPuff != null) endPuff.Emit(endCount);
                continue;
            }
            Place(i);
        }
    }
}
