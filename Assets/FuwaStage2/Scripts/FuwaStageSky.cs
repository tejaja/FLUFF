using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// ステージ2にいる間だけ空の青を濃くする（2026-11-02 てじゃ「空、青をちょっと濃くしてみて」「ステージ2だけね」）。自分の画面だけ。
// 2–2 のコースの箱の中では、空と雲海を白っぽくする（てじゃ「コース2-2にはいったら、空白っぽく」「雲の青みはきえない？」）。
// FuwaSkyZone（やみのもり）と同じ空のマテリアルを触るけど、やみのもりはステージ1にしかないので重ならない
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaStageSky : UdonSharpBehaviour
{
    public FuwaAreaVisibility area;
    public Material sky;
    public Color normalTop, normalHorizon, normalBottom, normalFog;
    public Color stage2Top, stage2Horizon, stage2Bottom, stage2Fog;
    [Tooltip("切り替わる速さ（1 で 1 秒）")]
    public float speed = 1f;

    [Header("2–2 の中（ワールド座標の箱）")]
    public Vector3 whiteMin, whiteMax;
    [Tooltip("箱の外側、この距離で切り替わる(m)")]
    public float whiteBlend = 5f;
    public Color whiteTop, whiteHorizon, whiteBottom, whiteFog;
    [Tooltip("雲海と遠くの島のマテリアル（色をいっしょに変える）")]
    public Material cloudSea, farIsland;
    public Color seaShade, seaHaze, whiteSeaShade, whiteSeaHaze;

    private float _t = -1f, _w = -1f;
    private int _topId, _horizonId, _bottomId, _shadeId, _hazeId;

    private void Start()
    {
        _topId = VRCShader.PropertyToID("_TopColor");
        _horizonId = VRCShader.PropertyToID("_HorizonColor");
        _bottomId = VRCShader.PropertyToID("_BottomColor");
        _shadeId = VRCShader.PropertyToID("_Shade");
        _hazeId = VRCShader.PropertyToID("_Haze");
    }

    private float WhiteTarget()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return 0f;
        Vector3 p = lp.GetPosition();
        float dx = Mathf.Max(whiteMin.x - p.x, Mathf.Max(0f, p.x - whiteMax.x));
        float dy = Mathf.Max(whiteMin.y - p.y, Mathf.Max(0f, p.y - whiteMax.y));
        float dz = Mathf.Max(whiteMin.z - p.z, Mathf.Max(0f, p.z - whiteMax.z));
        float d = Mathf.Sqrt(Mathf.Max(0f, dx) * Mathf.Max(0f, dx) + Mathf.Max(0f, dy) * Mathf.Max(0f, dy) + Mathf.Max(0f, dz) * Mathf.Max(0f, dz));
        return 1f - Mathf.Clamp01(d / Mathf.Max(0.01f, whiteBlend));
    }

    private void Update()
    {
        if (area == null || sky == null) return;
        bool inStage2 = area.GetStage() == 2;
        float want = inStage2 ? 1f : 0f, wantW = inStage2 ? WhiteTarget() : 0f;
        if (_t == want && _w == wantW) return;
        _t = _t < 0f ? want : Mathf.MoveTowards(_t, want, Time.deltaTime * speed);
        _w = _w < 0f ? wantW : Mathf.MoveTowards(_w, wantW, Time.deltaTime * speed);
        Color top = Color.Lerp(Color.Lerp(normalTop, stage2Top, _t), whiteTop, _w);
        Color horizon = Color.Lerp(Color.Lerp(normalHorizon, stage2Horizon, _t), whiteHorizon, _w);
        Color bottom = Color.Lerp(Color.Lerp(normalBottom, stage2Bottom, _t), whiteBottom, _w);
        sky.SetColor(_topId, top);
        sky.SetColor(_horizonId, horizon);
        sky.SetColor(_bottomId, bottom);
        RenderSettings.fogColor = Color.Lerp(Color.Lerp(normalFog, stage2Fog, _t), whiteFog, _w);
        Color haze = Color.Lerp(seaHaze, whiteSeaHaze, _w);
        if (cloudSea != null) { cloudSea.SetColor(_shadeId, Color.Lerp(seaShade, whiteSeaShade, _w)); cloudSea.SetColor(_hazeId, haze); }
        if (farIsland != null) farIsland.SetColor(_hazeId, haze);
    }
}
