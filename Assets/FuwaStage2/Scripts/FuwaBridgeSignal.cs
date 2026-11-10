using UdonSharp;
using UnityEngine;

// 2–4 かみ合う2枚の回転床：橋が渡れる向きに来ているかの信号ランプ（2026-11-10 てじゃ「横から見ると遠く全然みえない、合ってるかもわからん」→ A 信号ランプ）
// ・bridgesA のどれかが dirA（space の中の向き）に tolDeg 以内で向いていて、さらに bridgesB があればそのどれかも dirB に向いている時だけ緑、ほかは赤
// ・橋の向き＝橋の local +x（FuwaTwinTableTest.Bridge）
// 自分の画面だけ（同期なし。回転床の角度はみんな同じなので、結果も同じ）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaBridgeSignal : UdonSharpBehaviour
{
    public Transform space;
    public Transform[] bridgesA;
    public Vector3 dirA = Vector3.right;
    public Transform[] bridgesB;
    public Vector3 dirB = Vector3.left;
    public float tolDeg = 8f;
    public Renderer lamp;
    public Color onColor = new Color(0.3f, 1f, 0.45f), offColor = new Color(1f, 0.3f, 0.25f);
    [ColorUsage(false, true)] public Color onEmit = new Color(0.4f, 1.6f, 0.6f), offEmit = new Color(1.4f, 0.3f, 0.25f);

    private bool _state, _init;
    private MaterialPropertyBlock _mpb;

    private void Update()
    {
        bool ok = Any(bridgesA, dirA) && (bridgesB == null || bridgesB.Length == 0 || Any(bridgesB, dirB));
        if (_init && ok == _state) return;
        _state = ok; _init = true;
        if (lamp == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        lamp.GetPropertyBlock(_mpb);
        _mpb.SetColor("_Color", ok ? onColor : offColor);
        _mpb.SetColor("_EmissionColor", ok ? onEmit : offEmit);
        lamp.SetPropertyBlock(_mpb);
    }

    private bool Any(Transform[] bs, Vector3 dLocal)
    {
        if (bs == null) return false;
        Vector3 d = space != null ? space.TransformDirection(dLocal) : dLocal; d.y = 0f; d.Normalize();
        float c = Mathf.Cos(tolDeg * Mathf.Deg2Rad);
        for (int i = 0; i < bs.Length; i++)
        {
            if (bs[i] == null) continue;
            Vector3 f = bs[i].right; f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) continue;
            if (Vector3.Dot(f.normalized, d) >= c) return true;
        }
        return false;
    }
}
