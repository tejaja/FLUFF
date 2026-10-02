using UdonSharp;
using UnityEngine;

// 巨大クモの巣の穴の大きさをまとめて動かす（自分の画面だけ、同期なし）。
// 見た目（FuwaCourse/SpiderWeb シェーダーの _HoleRadius）・金色の輪っかの大きさ・
// 通り抜けOKの判定（FuwaHazard.holeRadius）を、同じ「穴の半径」から決める。
// breathe をONにすると、穴が minRadius〜maxRadius をゆっくり行き来する（呼吸する巣）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWebHole : UdonSharpBehaviour
{
    [Tooltip("穴の半径(m)。巣の糸が描かれ始める所")]
    public float holeRadius = 0.62f;

    [Header("呼吸する巣")]
    public bool breathe = false;
    public float minRadius = 0.45f;
    public float maxRadius = 0.85f;
    [Tooltip("大きく→小さく→大きく の1周の秒数")]
    public float period = 4f;

    [Header("つなぐ物")]
    public Renderer[] webRenderers;
    public Transform ring;
    [Tooltip("輪っかのメッシュの内側の半径（スケール1の時）")]
    public float ringInnerRadius = 0.92f;
    public FuwaHazard hazard;
    [Tooltip("判定の半径 = 穴の半径 − これ（ふわふわの中心で判定するので、少し内側にしておく）")]
    public float hazardInset = 0.02f;

    private Material[] _mats;
    private float _applied = -1f;

    private void Start()
    {
        if (webRenderers != null)
        {
            _mats = new Material[webRenderers.Length];
            for (int i = 0; i < webRenderers.Length; i++)
                if (webRenderers[i] != null) _mats[i] = webRenderers[i].material;
        }
        Apply(holeRadius);
    }

    private void Update()
    {
        if (!breathe) return;
        float k = 0.5f - 0.5f * Mathf.Cos(Time.time * 6.2831853f / Mathf.Max(0.1f, period));
        Apply(Mathf.Lerp(maxRadius, minRadius, k));
    }

    // 外から大きさを変える時用（ギミックから呼ぶ）
    public void SetRadius(float r)
    {
        holeRadius = r;
        Apply(r);
    }

    private void Apply(float r)
    {
        if (Mathf.Abs(r - _applied) < 0.0005f) return;
        _applied = r;
        if (_mats != null)
            for (int i = 0; i < _mats.Length; i++)
                if (_mats[i] != null) _mats[i].SetFloat("_HoleRadius", r);
        if (ring != null) ring.localScale = Vector3.one * (r * 0.98f / ringInnerRadius);
        if (hazard != null) hazard.holeRadius = Mathf.Max(0f, r - hazardInset);
    }
}
