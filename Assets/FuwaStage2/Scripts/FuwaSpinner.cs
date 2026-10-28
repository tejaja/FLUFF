using UdonSharp;
using UnityEngine;

// 回る羽根などのじゃま物（2026-10-28 2–2 試作）：ローカルの axis のまわりに一定の速さで回る。当たり判定ごと回る（キネマティックの Rigidbody で）
// 全員同じ見え方になるように、時刻（サーバー時刻）から角度を決める
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSpinner : UdonSharpBehaviour
{
    public Vector3 axis = Vector3.right;
    [Tooltip("1秒に何度回るか")]
    public float degPerSec = 60f;
    public float phase = 0f;
    private Rigidbody _rb;
    private Quaternion _base;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _base = transform.localRotation;
    }

    private void FixedUpdate()
    {
        float a = (float)((VRC.SDKBase.Networking.GetServerTimeInSeconds() * degPerSec + phase) % 360.0);
        var q = _base * Quaternion.AngleAxis(a, axis);
        if (_rb != null && transform.parent != null) _rb.MoveRotation(transform.parent.rotation * q);
        else transform.localRotation = q;
    }
}
