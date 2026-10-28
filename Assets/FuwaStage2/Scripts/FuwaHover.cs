using UdonSharp;
using UnityEngine;

// ふわふわ浮かんで、ゆっくり回る（2–1 の水源の上の「ふしぎな青い球」など、2026-10-25）。各自の時計で動く（同期なし）。見た目だけ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaHover : UdonSharpBehaviour
{
    [Tooltip("上下にゆれる大きさ（m、片側）")]
    public float amplitude = 0.15f;
    [Tooltip("ひとゆれの秒数")]
    public float period = 3.2f;
    [Tooltip("回る速さ（度/秒、縦の軸）")]
    public float spinSpeed = 25f;
    [Tooltip("ゆれのずらし（0〜1＝ひとゆれのどこから始めるか）。球ごとにずらしてそろって上下しないように（2026-10-28 てじゃ）")]
    public float phase = 0f;

    private Vector3 _base;
    private Quaternion _rot;

    private void Start()
    {
        _base = transform.localPosition;
        _rot = transform.localRotation;
    }

    private void Update()
    {
        float a = (Time.time / period + phase) * 2f * Mathf.PI;
        transform.localPosition = _base + Vector3.up * (Mathf.Sin(a) * amplitude);
        transform.localRotation = _rot * Quaternion.Euler(0f, Time.time * spinSpeed + phase * 360f, 0f);
    }
}
