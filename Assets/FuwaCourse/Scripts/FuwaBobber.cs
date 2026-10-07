using UdonSharp;
using UnityEngine;

// 浮き岩の見た目をゆらゆら傾ける（いわのみちのゴール前の浮き岩など）。各自の時計で動く（同期なし）。
// この物体を「上の面のまん中」に置いて、見た目はその子にする。まん中の高さは変わらないので、立っている所はほぼズレない。
// 当たり判定は動かさない（動く床だと、沈む時に足が離れて空中あつかいになり、ジャンプできなくなるため。2026-10-07）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaBobber : UdonSharpBehaviour
{
    [Tooltip("傾きの大きさ（度）")]
    public float tiltDegrees = 3f;
    [Tooltip("ひとゆれの秒数")]
    public float period = 4f;
    [Tooltip("位相（0〜1）。岩ごとにずらす")]
    [Range(0f, 1f)] public float phase = 0f;

    private Quaternion _base;

    private void Start()
    {
        _base = transform.localRotation;
    }

    private void Update()
    {
        float a = (Time.time / period + phase) * 2f * Mathf.PI;
        // 2つの軸を少しずらした周期で揺らして、ぐるっと回るようなゆらぎに
        float x = Mathf.Sin(a) * tiltDegrees;
        float z = Mathf.Sin(a * 0.73f + 1.3f) * tiltDegrees * 0.8f;
        transform.localRotation = _base * Quaternion.Euler(x, 0f, z);
    }
}
