using UdonSharp;
using UnityEngine;

// 2026-11-18 置き場所さがし用（一時的）：持てる物を離した時に、basis（島の親）から見た位置・向きを VRChat のログに書く。
//  VRChat のテストモードでも output_log から読める（"[FuwaPosLog]" で探す）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPosLogger : UdonSharpBehaviour
{
    public Transform basis;
    public string label = "Board";

    public override void OnDrop() { Log(); }
    public override void OnPickupUseDown() { Log(); }

    public void Log()
    {
        Vector3 p = basis != null ? basis.InverseTransformPoint(transform.position) : transform.position;
        Quaternion q = basis != null ? Quaternion.Inverse(basis.rotation) * transform.rotation : transform.rotation;
        Vector3 e = q.eulerAngles;
        Debug.Log("[FuwaPosLog] " + label + " local=(" + p.x.ToString("F3") + ", " + p.y.ToString("F3") + ", " + p.z.ToString("F3") + ") euler=(" + e.x.ToString("F2") + ", " + e.y.ToString("F2") + ", " + e.z.ToString("F2") + ") world=" + transform.position.ToString("F3"));
    }
}
