using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// イースターエッグ：太陽をエアガンで撃つと、空の太陽（FuwaCourse/GradientSky）の目が「＞＜」になってプルプルする。
// 空のシェーダーのグローバル _UdonSunSquint（0〜1）を動かすだけ。FuwaBlower が全員に Squint を呼ぶ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSunFace : UdonSharpBehaviour
{
    [Tooltip("太陽の向きを決めているディレクショナルライト（空のシェーダーの太陽と同じもの）")]
    public Transform sunLight;
    [Tooltip("太陽の中心から何度以内を撃ったら当たりにするか（太陽の円は半径5度、トゲまで約9度）")]
    public float hitAngle = 10f;
    [Tooltip("＞＜になっている秒数")]
    public float holdTime = 1.6f;
    public float inTime = 0.06f;
    public float outTime = 0.25f;
    [Tooltip("トゲが1回ピョンと跳ねる時間(秒)と大きさ（2026-10-07 前はずっとぷるぷるしていた）")]
    public float popTime = 0.65f;
    public float popAmount = 1.4f;

    private int _squintId;
    private int _popId;
    private float _start = -100f;
    private bool _active;

    private void Start()
    {
        _squintId = VRCShader.PropertyToID("_UdonSunSquint");
        VRCShader.SetGlobalFloat(_squintId, 0f);
        _popId = VRCShader.PropertyToID("_UdonSunPop");
        VRCShader.SetGlobalFloat(_popId, 0f);
    }

    // 撃った向きが太陽に当たっているか（撃った人のPCで判定）
    public bool IsAimAtSun(Vector3 aim)
    {
        if (sunLight == null || aim.sqrMagnitude < 0.0001f) return false;
        return Vector3.Angle(aim, -sunLight.forward) < hitAngle;
    }

    public void Squint()
    {
        _start = Time.time;
        _active = true;
    }

    private void Update()
    {
        if (!_active) return;
        float t = Time.time - _start;
        float v;
        if (t < inTime) v = t / Mathf.Max(inTime, 0.001f);
        else if (t < inTime + holdTime) v = 1f;
        else if (t < inTime + holdTime + outTime) v = 1f - (t - inTime - holdTime) / Mathf.Max(outTime, 0.001f);
        else { v = 0f; _active = false; }
        VRCShader.SetGlobalFloat(_squintId, v);
        // トゲ：外へピョンと出て、ぷるんと1回戻って止まる（空のいつもの「ときどきピョン」と同じ形を大きめに）
        float x = t / Mathf.Max(popTime, 0.05f);
        float pop = x < 1f ? (1f - x) * (1f - x) * Mathf.Sin(x * 9.4f) * popAmount : 0f;
        VRCShader.SetGlobalFloat(_popId, _active ? pop : 0f);
    }
}
