using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// この範囲（子の BoxCollider の箱）に自分が入ると、空・太陽・環境光をじわっと別の色にする（自分の画面だけ）。
// 箱の外側 blendDistance(m) の間でなめらかに切り替わる。やみのもり（紫の暗さ）用。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSkyZone : UdonSharpBehaviour
{
    public BoxCollider zone;
    [Tooltip("あれば箱の代わりにこの楕円の球（大きさ1の球のメッシュを伸ばした物。やみのもりの黒いもや YamiMist）を範囲にする。ふちに入ったら暗くなり始め、innerFrac まで入ると真っ暗（2026-10-14 てじゃ「黒いもやもやの範囲に入ったら始まる感じに」）")]
    public Transform ellipsoid;
    [Tooltip("楕円の球の中心から見て、この割合（0〜1、1=ふち）まで入ったら完全にゾーンの色")]
    [Range(0.3f, 0.99f)] public float innerFrac = 0.8f;
    [Tooltip("箱の外側、この距離で元の色→ゾーンの色へ切り替わる(m)")]
    public float blendDistance = 12f;
    [Tooltip("切り替わる速さ（大きいほどすぐ）")]
    public float smooth = 2f;

    [Header("空（FuwaCourse/GradientSky のマテリアル）")]
    public Material sky;
    public Color normalTop, normalHorizon, normalBottom;
    public Color zoneTop = new Color(0.14f, 0.07f, 0.28f), zoneHorizon = new Color(0.40f, 0.24f, 0.52f), zoneBottom = new Color(0.22f, 0.12f, 0.34f);
    public Color normalSun = Color.white, zoneSun = new Color(1f, 0.82f, 0.95f);
    public float normalSunGlow = 0.6f, zoneSunGlow = 0.15f;

    [Header("太陽")]
    public Light sun;
    public Color normalLight = Color.white, zoneLight = new Color(0.62f, 0.52f, 0.9f);
    public float normalIntensity = 1f, zoneIntensity = 0.55f;

    [Header("環境光（Trilight）")]
    public Color normalAmbSky, normalAmbEq, normalAmbGround;
    public Color zoneAmbSky = new Color(0.32f, 0.24f, 0.5f), zoneAmbEq = new Color(0.30f, 0.24f, 0.42f), zoneAmbGround = new Color(0.22f, 0.16f, 0.30f);

    private float _t = -1f;
    private float _shown = -1f;

    private void Start()
    {
        _t = 0f;
        Apply(0f);
    }

    private void Update()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null || (zone == null && ellipsoid == null)) return;
        float target;
        if (ellipsoid != null)
        {
            // 楕円の球：ふち（r=1）で0、innerFrac で1
            float r = ellipsoid.InverseTransformPoint(lp.GetPosition()).magnitude;
            target = Mathf.Clamp01((1f - r) / Mathf.Max(0.01f, 1f - innerFrac));
        }
        else target = BoxTarget(lp.GetPosition());
        target = target * target * (3f - 2f * target);
        _t = Mathf.MoveTowards(_t, target, Time.deltaTime * smooth);
        if (Mathf.Abs(_t - _shown) > 0.002f) Apply(_t);
    }

    private float BoxTarget(Vector3 pos)
    {
        Vector3 local = zone.transform.InverseTransformPoint(pos) - zone.center;
        Vector3 half = zone.size * 0.5f;
        Vector3 s = zone.transform.lossyScale;
        float dx = Mathf.Max(0f, Mathf.Abs(local.x) - half.x) * s.x;
        float dy = Mathf.Max(0f, Mathf.Abs(local.y) - half.y) * s.y;
        float dz = Mathf.Max(0f, Mathf.Abs(local.z) - half.z) * s.z;
        float dist = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        return 1f - Mathf.Clamp01(dist / Mathf.Max(0.01f, blendDistance));
    }

    private void Apply(float t)
    {
        _shown = t;
        if (sky != null)
        {
            sky.SetColor("_TopColor", Color.Lerp(normalTop, zoneTop, t));
            sky.SetColor("_HorizonColor", Color.Lerp(normalHorizon, zoneHorizon, t));
            sky.SetColor("_BottomColor", Color.Lerp(normalBottom, zoneBottom, t));
            sky.SetColor("_SunColor", Color.Lerp(normalSun, zoneSun, t));
            sky.SetFloat("_SunGlow", Mathf.Lerp(normalSunGlow, zoneSunGlow, t));
        }
        if (sun != null)
        {
            sun.color = Color.Lerp(normalLight, zoneLight, t);
            sun.intensity = Mathf.Lerp(normalIntensity, zoneIntensity, t);
        }
        RenderSettings.ambientSkyColor = Color.Lerp(normalAmbSky, zoneAmbSky, t);
        RenderSettings.ambientEquatorColor = Color.Lerp(normalAmbEq, zoneAmbEq, t);
        RenderSettings.ambientGroundColor = Color.Lerp(normalAmbGround, zoneAmbGround, t);
    }
}
