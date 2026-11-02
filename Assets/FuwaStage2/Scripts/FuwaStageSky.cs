using UdonSharp;
using UnityEngine;

// ステージ2にいる間だけ空の青を濃くする（2026-11-02 てじゃ「空、青をちょっと濃くしてみて」「ステージ2だけね」）。自分の画面だけ。
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

    private float _t = -1f;
    private int _topId, _horizonId, _bottomId;

    private void Start()
    {
        _topId = VRC.SDKBase.VRCShader.PropertyToID("_TopColor");
        _horizonId = VRC.SDKBase.VRCShader.PropertyToID("_HorizonColor");
        _bottomId = VRC.SDKBase.VRCShader.PropertyToID("_BottomColor");
    }

    private void Update()
    {
        if (area == null || sky == null) return;
        float want = area.GetStage() == 2 ? 1f : 0f;
        if (_t == want) return;
        _t = _t < 0f ? want : Mathf.MoveTowards(_t, want, Time.deltaTime * speed);
        sky.SetColor(_topId, Color.Lerp(normalTop, stage2Top, _t));
        sky.SetColor(_horizonId, Color.Lerp(normalHorizon, stage2Horizon, _t));
        sky.SetColor(_bottomId, Color.Lerp(normalBottom, stage2Bottom, _t));
        RenderSettings.fogColor = Color.Lerp(normalFog, stage2Fog, _t);
    }
}
