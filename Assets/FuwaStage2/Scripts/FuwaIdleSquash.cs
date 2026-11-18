using UdonSharp;
using UnityEngine;

// 2026-11-18 てじゃ「おまけ本島のきのこもピョコピョコしてほしい」：ゴールのキノコの輪（FuwaGoal の Idle）と同じ「ときどき1本ずつ縦にぽよんと潰れて揺れ戻る」。
//  root の子を全部（自分の画面だけ）。大きいキノコなので潰れ方はゴールのより少しおとなしめ・ゆっくり
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaIdleSquash : UdonSharpBehaviour
{
    public Transform root;
    [Tooltip("次にぽよんとするまでの間隔（最短〜最長、秒）")]
    public Vector2 interval = new Vector2(1.5f, 5f);
    [Tooltip("ぽよんの長さ(秒)")]
    public float time = 0.8f;
    [Tooltip("縦に潰れる量（0.1 = 10%）")]
    public float squash = 0.1f;
    private Transform[] _t;
    private Vector3[] _s;
    private float[] _at;

    private void Start()
    {
        if (root == null) root = transform;
        int n = root.childCount;
        _t = new Transform[n]; _s = new Vector3[n]; _at = new float[n];
        for (int i = 0; i < n; i++)
        {
            _t[i] = root.GetChild(i); _s[i] = _t[i].localScale;
            _at[i] = Time.time + Random.Range(0.3f, interval.y);
        }
    }

    private void Update()
    {
        if (_t == null) return;
        float now = Time.time;
        for (int i = 0; i < _t.Length; i++)
        {
            float u = (now - _at[i]) / Mathf.Max(0.05f, time);
            if (u < 0f) continue;
            if (u >= 1f)
            {
                _at[i] = now + Random.Range(interval.x, interval.y);
                _t[i].localScale = _s[i];
                continue;
            }
            // 減衰しながら2回ほど揺れる（最初に潰れる）。潰れる時は横に少し広がる
            float w = -squash * Mathf.Sin(u * Mathf.PI * 4f) * (1f - u);
            _t[i].localScale = Vector3.Scale(_s[i], new Vector3(1f - w * 0.5f, 1f + w, 1f - w * 0.5f));
        }
    }
}
