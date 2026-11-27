using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

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
    [Tooltip("地面から浮かせる量（板の印の時だけ）")]
    public float lift = 0.015f;
    [Tooltip("デカール（FuwaCourse/BlobDecal の箱）の時：当たった地面から下・上に何mの範囲の表面に描くか。上は小さくして、ふわふわ自身に影が乗らないように")]
    public bool decal = true;
    public float decalBelow = 0.35f;
    public float decalAbove = 0.06f;

    [Header("地面のシェーダーで描く（FuwaPlayerBlob.cginc、PCだけ）")]
    // 2026-11-28 てじゃ「ふわふわもこれつかうのはよくない？」：箱のデカールをやめ、地面のシェーダーが自分で暗くする。スマホ・Quest は箱のまま
    public bool receiver = true;
    private bool _recv;
    private int _idLB, _idLQ;

    private Material _mat;
    private float _shownAlpha = -1f;

    private void Start()
    {
        if (marker != null) _mat = marker.material;
        _recv = receiver;
#if UNITY_ANDROID || UNITY_IOS
        _recv = false;
#endif
        if (_recv)
        {
            _idLB = VRCShader.PropertyToID("_UdonFuwaLB");
            _idLQ = VRCShader.PropertyToID("_UdonFuwaLQ");
            if (marker != null) marker.enabled = false;
            VRCShader.SetGlobalVector(_idLQ, Vector4.zero);
        }
    }

    private void HideRecv() { VRCShader.SetGlobalVector(_idLQ, Vector4.zero); }

    private void LateUpdate()
    {
        if (ball == null || marker == null) return;
        if (ball.IsHidden() || !ball.gameObject.activeInHierarchy) { if (_recv) HideRecv(); else if (marker.enabled) marker.enabled = false; return; }
        Vector3 p = ball.transform.position;
        RaycastHit hit;
        if (!Physics.Raycast(p, Vector3.down, out hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (_recv) HideRecv(); else if (marker.enabled) marker.enabled = false;
            return;
        }
        // 2026-11-17 てじゃ「ふわふわの偽影、サイズかわっても一定になってる」→ ふわふわの大きさに合わせる（みんなのふわふわの影と同じやり方）
        float k = ball.GetVisualSize();
        float h = Mathf.Clamp01(hit.distance / Mathf.Max(0.01f, farHeight * Mathf.Max(1f, k)));
        float size = Mathf.Lerp(nearSize, farSize, h) * k;
        float a = Mathf.Lerp(nearAlpha, farAlpha, h);
        if (_recv)
        {
            VRCShader.SetGlobalVector(_idLB, new Vector4(hit.point.x, hit.point.y, hit.point.z, size * 0.5f));
            VRCShader.SetGlobalVector(_idLQ, new Vector4(0f, 1f, 1f, a));
            return;
        }
        Transform t = marker.transform;
        if (decal)
        {
            // 箱の y を地面の向きに合わせて、地面の少し下〜少し上の範囲を囲む（崖のふちからはみ出した所は地面が無いので描かれない）
            t.position = hit.point + hit.normal * ((decalAbove - decalBelow) * 0.5f);
            t.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            t.localScale = new Vector3(size, decalAbove + decalBelow, size);
        }
        else
        {
            t.position = hit.point + hit.normal * lift;
            // Quad は -Z が表なので、法線の向きに -Z を合わせる
            t.rotation = Quaternion.LookRotation(-hit.normal, Mathf.Abs(hit.normal.y) > 0.9f ? Vector3.forward : Vector3.up);
            t.localScale = new Vector3(size, size, size);
        }
        if (!marker.enabled) marker.enabled = true;
        if (_mat != null && Mathf.Abs(a - _shownAlpha) > 0.01f) { _shownAlpha = a; _mat.SetFloat("_Alpha", a); }
    }
}
