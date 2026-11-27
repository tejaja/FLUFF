using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 同期ふわふわ（FuwaSharedBall）の足元のウソ影。コースのふわふわの影（FuwaGroundMarker）と同じ決まりで、1つのスクリプトでまとめて動かす
// （2026-10-13 てじゃ「同期フワフワの影がみえない」）。真下に線を飛ばして、当たった地面にデカール（FuwaCourse/BlobDecal の箱）でぼかした丸を描く。
// 大きさはふわふわの大きさに合わせる（あそびばの巨大など）。キノコの中で待っている・はじけている間は消す
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSharedBallShadows : UdonSharpBehaviour
{
    public FuwaSharedBall[] balls;
    [Tooltip("balls と同じ順の影（BlobDecal の箱のレンダラー）")]
    public Renderer[] markers;
    public LayerMask groundMask = (1 << 0) | (1 << 11);
    public float maxDistance = 12f;
    [Tooltip("ふわふわの大きさ（直径）0.3 の時の、高さ0／高さ farHeight での影の直径。大きさに比例させる")]
    public float nearSize = 0.45f;
    public float farSize = 0.6f;
    public float nearAlpha = 0.78f;
    public float farAlpha = 0.35f;
    public float farHeight = 4f;
    public float decalBelow = 0.35f;
    public float decalAbove = 0.06f;
    public float baseBallSize = 0.3f;
    [Header("地面のシェーダーで描く（FuwaPlayerBlob.cginc、PCだけ）")]
    // 2026-11-28 てじゃ「ふわふわもこれつかうのはよくない？」：箱のデカール（深度頼み）をやめ、地面のシェーダーが自分で暗くする（アバターに乗らない・段差で欠けない）。
    //  スマホ・Quest は今までの箱（BlobDecal の深度なし版）のまま
    public bool receiver = true;
    public float receiverUpTol = 0.2f;
    public float receiverDownTol = 0.5f;
    [Tooltip("自分からこれより遠いふわふわの影は出さない(m)")]
    public float receiverMaxView = 40f;
    private Vector3 _me;
    private Vector4[] _bb = new Vector4[16];
    private Vector4[] _bq = new Vector4[16];
    private int _idBB, _idBQ, _idBBCount;
    private bool _recv;

    private Material[] _mats;
    private float[] _shownAlpha;

    private void Start()
    {
        int n = markers != null ? markers.Length : 0;
        _mats = new Material[n];
        _shownAlpha = new float[n];
        for (int i = 0; i < n; i++) { if (markers[i] != null) _mats[i] = markers[i].material; _shownAlpha[i] = -1f; }
        _recv = receiver;
#if UNITY_ANDROID || UNITY_IOS
        _recv = false;
#endif
        if (_recv)
        {
            _idBB = VRCShader.PropertyToID("_UdonFuwaBB");
            _idBQ = VRCShader.PropertyToID("_UdonFuwaBQ");
            _idBBCount = VRCShader.PropertyToID("_UdonFuwaBBCount");
            Material m0 = n > 0 && markers[0] != null ? markers[0].sharedMaterial : null;
            Color col = m0 != null ? m0.GetColor("_Color") : new Color(0.12f, 0.08f, 0.2f, 1f);
            float fs = m0 != null ? m0.GetFloat("_FalloffStart") : 0.35f, pw = m0 != null ? m0.GetFloat("_Power") : 1f;
            VRCShader.SetGlobalColor(VRCShader.PropertyToID("_UdonFuwaBBColor"), col);
            VRCShader.SetGlobalVector(VRCShader.PropertyToID("_UdonFuwaBBParam"), new Vector4(fs, pw, receiverUpTol, receiverDownTol));
            for (int i = 0; i < n; i++) if (markers[i] != null) markers[i].enabled = false;
            VRCShader.SetGlobalVectorArray(_idBB, _bb);
            VRCShader.SetGlobalVectorArray(_idBQ, _bq);
            VRCShader.SetGlobalFloat(_idBBCount, 0f);
        }
    }

    private void LateUpdate()
    {
        if (balls == null || markers == null || _mats == null) return;
        int n = Mathf.Min(balls.Length, markers.Length);
        if (_recv)
        {
            int c = 0;
            VRCPlayerApi lp = Networking.LocalPlayer;
            _me = lp != null ? lp.GetPosition() : Vector3.zero;
            for (int i = 0; i < balls.Length && c < 16; i++) if (UpdateRecv(i, c)) c++;
            VRCShader.SetGlobalVectorArray(_idBB, _bb);
            VRCShader.SetGlobalVectorArray(_idBQ, _bq);
            VRCShader.SetGlobalFloat(_idBBCount, c);
            return;
        }
        for (int i = 0; i < n; i++) UpdateOne(i);
    }

    private bool UpdateRecv(int i, int slot)
    {
        FuwaSharedBall b = balls[i];
        if (b == null || !b.gameObject.activeInHierarchy || !b.IsFlying()) return false;
        Vector3 p = b.transform.position;
        if ((p - _me).sqrMagnitude > receiverMaxView * receiverMaxView) return false;
        RaycastHit hit;
        if (!Physics.Raycast(p, Vector3.down, out hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore)) return false;
        float k = b.transform.lossyScale.x / Mathf.Max(0.01f, baseBallSize);
        float h = Mathf.Clamp01(hit.distance / Mathf.Max(0.01f, farHeight * Mathf.Max(1f, k)));
        float size = Mathf.Lerp(nearSize, farSize, h) * k;
        float a = Mathf.Lerp(nearAlpha, farAlpha, h);
        _bb[slot] = new Vector4(hit.point.x, hit.point.y, hit.point.z, size * 0.5f);
        _bq[slot] = new Vector4(0f, 1f, 1f, a);
        return true;
    }

    private void UpdateOne(int i)
    {
        FuwaSharedBall b = balls[i];
        Renderer m = markers[i];
        if (m == null) return;
        if (b == null || !b.gameObject.activeInHierarchy || !b.IsFlying()) { if (m.enabled) m.enabled = false; return; }
        Vector3 p = b.transform.position;
        RaycastHit hit;
        if (!Physics.Raycast(p, Vector3.down, out hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (m.enabled) m.enabled = false;
            return;
        }
        float k = b.transform.lossyScale.x / Mathf.Max(0.01f, baseBallSize);
        float h = Mathf.Clamp01(hit.distance / Mathf.Max(0.01f, farHeight * Mathf.Max(1f, k)));
        float size = Mathf.Lerp(nearSize, farSize, h) * k;
        float a = Mathf.Lerp(nearAlpha, farAlpha, h);
        Transform t = m.transform;
        // 箱の y を地面の向きに合わせて、地面の少し下〜少し上の範囲を囲む（崖のふちからはみ出した所は地面が無いので描かれない）
        t.position = hit.point + hit.normal * ((decalAbove - decalBelow) * 0.5f);
        t.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
        t.localScale = new Vector3(size, decalAbove + decalBelow, size);
        if (!m.enabled) m.enabled = true;
        if (_mats[i] != null && Mathf.Abs(a - _shownAlpha[i]) > 0.01f) { _shownAlpha[i] = a; _mats[i].SetFloat("_Alpha", a); }
    }
}
