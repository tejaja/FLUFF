using UdonSharp;
using UnityEngine;

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

    private Material[] _mats;
    private float[] _shownAlpha;

    private void Start()
    {
        int n = markers != null ? markers.Length : 0;
        _mats = new Material[n];
        _shownAlpha = new float[n];
        for (int i = 0; i < n; i++) { if (markers[i] != null) _mats[i] = markers[i].material; _shownAlpha[i] = -1f; }
    }

    private void LateUpdate()
    {
        if (balls == null || markers == null || _mats == null) return;
        int n = Mathf.Min(balls.Length, markers.Length);
        for (int i = 0; i < n; i++) UpdateOne(i);
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
