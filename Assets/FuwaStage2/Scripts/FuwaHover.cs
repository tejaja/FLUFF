using UdonSharp;
using VRC.SDKBase;
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
        // 2026-11-21 てじゃ「おっかけカメラで、ギミックの見た目を対象の人に合わせたい」→ 共通時計（サーバー時刻）で動かす：だれの画面でも同じタイミング
        double st = Networking.GetServerTimeInSeconds();
        float a = ((float)((st % period) / period) + phase) * 2f * Mathf.PI;
        transform.localPosition = _base + Vector3.up * (Mathf.Sin(a) * amplitude);
        transform.localRotation = _rot * Quaternion.Euler(0f, (float)((st * spinSpeed) % 360.0) + phase * 360f, 0f);
    }
}
