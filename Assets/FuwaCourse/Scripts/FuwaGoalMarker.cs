using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// ゴールの目印。▼をふんわり上下させながら回し（少し傾いてゆらゆら）、文字を自分の方へ向ける（同期なし）

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGoalMarker : UdonSharpBehaviour
{
    public Transform arrow;
    public Transform label;
    public float bobHeight = 0.13f;
    public float bobSpeed = 1.3f;
    public float spinSpeed = 60f;
    [Tooltip("ゆらゆら傾く角度(度)")]
    public float swayAngle = 0f;

    private Vector3 _arrowBase;
    private Quaternion _arrowRot;
    private float _spin;
    private float _phase;

    private void Start()
    {
        if (arrow != null) { _arrowBase = arrow.localPosition; _arrowRot = arrow.localRotation; }
        // 並んでいる目印が同じ動きにならないように、場所で少しずらす
        _phase = (transform.position.x * 0.37f + transform.position.z * 0.71f) % 6.283f;
    }

    private void Update()
    {
        if (arrow != null)
        {
            float t = Time.time * bobSpeed + _phase;
            // 上下：下はゆっくり沈んで、上でふわっと止まるような波（sin に2倍の波を少し足す）
            float y = Mathf.Sin(t) * 0.85f + Mathf.Sin(t * 2f - 1.2f) * 0.15f;
            arrow.localPosition = _arrowBase + Vector3.up * y * bobHeight;
            _spin = (_spin + spinSpeed * Time.deltaTime) % 360f;
            // 上下に合わせて少しずれたタイミングで、2方向にゆらゆら傾く
            Quaternion sway = Quaternion.Euler(Mathf.Sin(t * 0.9f + 0.6f) * swayAngle, 0f, Mathf.Sin(t * 0.7f + 2.1f) * swayAngle);
            arrow.localRotation = sway * Quaternion.AngleAxis(_spin, Vector3.up) * _arrowRot;
        }
        VRCPlayerApi local = Networking.LocalPlayer;
        if (label != null && local != null && local.IsValid())
        {
            Vector3 head = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            Vector3 d = label.position - head;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) label.rotation = Quaternion.LookRotation(d, Vector3.up);
        }
    }
}
