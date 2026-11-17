using UdonSharp;
using UnityEngine;

// 2–4 の最後の坂（2026-11-17 てじゃ「最後の坂になんかほしい、上下させるとか」→ A「波の階段」）
//  坂を板に分けて、下のカム軸で順番に上下させる。板 i の高さ = base + 上 × amp × (1 − cos(θ − i·phaseStep)) / 2、θ = 2π·Time.time / period（自分の画面だけ、同期なし）
//  phaseStep を 120° にすると、板 i がいちばん上の時に板 i+1 が同じ高さ近くを上がっている → 乗り移りながら上がる
//  カム軸（shaft）は local Z まわりに θ で回す、カムは軸の子（いっしょに回る）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWavePlates : UdonSharpBehaviour
{
    public Transform[] plates;
    public Vector3[] basePos;          // 板の localPosition（いちばん下の時）
    public float amp = 0.56f, period = 4.5f, phaseStepDeg = 120f;
    public Transform shaft;
    public Quaternion shaftBase;
    private int _n;

    private void Start() { _n = plates != null ? plates.Length : 0; }

    private void Update()
    {
        if (_n == 0) return;
        float th = Mathf.Repeat(Time.time / period, 1f) * 360f;
        float step = phaseStepDeg;
        for (int i = 0; i < _n; i++)
        {
            float a = (th - i * step) * Mathf.Deg2Rad;
            plates[i].localPosition = basePos[i] + Vector3.up * (amp * (1f - Mathf.Cos(a)) * 0.5f);
        }
        if (shaft != null) shaft.localRotation = shaftBase * Quaternion.Euler(0f, 0f, -th);
    }
}
