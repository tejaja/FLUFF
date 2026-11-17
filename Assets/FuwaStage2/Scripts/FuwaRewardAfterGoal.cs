using UdonSharp;
using UnityEngine;

// 2026-11-17 てじゃ「演出後は広場の横の方に金ピカ銃切り替えスイッチと金ピカフワフワオブジェクト（持てるだけ）が出てくる」
//  watch（ゴールの ClearBanner）が一度でも出たら、delay 秒後に rewards を出す（自分の画面だけ、出したらそのまま）。ぽよんと大きくなって出る
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaRewardAfterGoal : UdonSharpBehaviour
{
    public GameObject watch;
    public GameObject[] rewards;
    public float delay = 2.5f, popTime = 0.5f;
    private float _seenAt = -1f;
    private bool _done;
    private Vector3[] _scales;
    private float _popStart = -1f;

    private void Start()
    {
        if (rewards == null) return;
        _scales = new Vector3[rewards.Length];
        for (int i = 0; i < rewards.Length; i++) if (rewards[i] != null) { _scales[i] = rewards[i].transform.localScale; rewards[i].SetActive(false); }
    }

    private void Update()
    {
        if (_done && _popStart < 0f) return;
        if (!_done)
        {
            if (_seenAt < 0f) { if (watch != null && watch.activeInHierarchy) _seenAt = Time.time; return; }
            if (Time.time - _seenAt < delay) return;
            _done = true; _popStart = Time.time;
            for (int i = 0; i < rewards.Length; i++) if (rewards[i] != null) rewards[i].SetActive(true);
        }
        float k = Mathf.Clamp01((Time.time - _popStart) / popTime);
        float s = k < 1f ? (1f + 0.25f * Mathf.Sin(k * Mathf.PI)) * Mathf.SmoothStep(0f, 1f, k * 1.4f) : 1f;
        for (int i = 0; i < rewards.Length; i++) if (rewards[i] != null) rewards[i].transform.localScale = _scales[i] * s;
        if (k >= 1f) _popStart = -1f;
    }
}
