using UdonSharp;
using UnityEngine;

// ふわふわの足元の影（本物の影の代わり）。真下に線を飛ばして、当たった地面にぼかした丸を描く。
// 本物の影と違って洞窟の中（日なたじゃない所）でも出るので、位置がわかる。高いほど少し大きく薄く。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGroundMarker : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("印のレンダラー（Quad、FuwaCourse/GroundMarker）")]
    public Renderer marker;
    [Tooltip("当たる地面のレイヤー")]
    public LayerMask groundMask = (1 << 0) | (1 << 11);
    public float maxDistance = 12f;
    [Tooltip("高さ0の時の直径 / 高さ farHeight の時の直径")]
    public float nearSize = 0.36f;
    public float farSize = 0.5f;
    public float nearAlpha = 0.6f;
    public float farAlpha = 0.22f;
    public float farHeight = 4f;
    [Tooltip("地面から浮かせる量")]
    public float lift = 0.015f;

    private Material _mat;
    private float _shownAlpha = -1f;

    private void Start()
    {
        if (marker != null) _mat = marker.material;
    }

    private void LateUpdate()
    {
        if (ball == null || marker == null) return;
        if (ball.IsHidden() || !ball.gameObject.activeInHierarchy) { if (marker.enabled) marker.enabled = false; return; }
        Vector3 p = ball.transform.position;
        RaycastHit hit;
        if (!Physics.Raycast(p, Vector3.down, out hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (marker.enabled) marker.enabled = false;
            return;
        }
        float h = Mathf.Clamp01(hit.distance / Mathf.Max(0.01f, farHeight));
        float size = Mathf.Lerp(nearSize, farSize, h);
        float a = Mathf.Lerp(nearAlpha, farAlpha, h);
        Transform t = marker.transform;
        t.position = hit.point + hit.normal * lift;
        // Quad は -Z が表なので、法線の向きに -Z を合わせる
        t.rotation = Quaternion.LookRotation(-hit.normal, Mathf.Abs(hit.normal.y) > 0.9f ? Vector3.forward : Vector3.up);
        t.localScale = new Vector3(size, size, size);
        if (!marker.enabled) marker.enabled = true;
        if (_mat != null && Mathf.Abs(a - _shownAlpha) > 0.01f) { _shownAlpha = a; _mat.SetFloat("_Alpha", a); }
    }
}
