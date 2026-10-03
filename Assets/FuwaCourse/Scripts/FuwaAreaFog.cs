using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 範囲（このオブジェクトのトリガー）の中に自分がいる間だけ、霧をかけて空も暗くする（自分の画面だけ）。
// 出入りでふわっと切り替える。霧に対応したシェーダー（ToonLit など）にかかる。
// 空は FuwaCourse/GradientSky のグローバル _UdonSkyDim（0〜1）で暗い空色に寄せる
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaAreaFog : UdonSharpBehaviour
{
    public Color fogColor = new Color(0.2f, 0.19f, 0.29f, 1f);
    [Tooltip("霧の濃さ（Exponential）。0.05 で 20m 先が 6割くらいかすむ")]
    public float density = 0.05f;
    [Tooltip("空の暗さ（0=いつもの空、1=暗い空色）")]
    [Range(0f, 1f)] public float skyDim = 1f;
    public float fadeSeconds = 1.2f;

    private int _count;   // 複数の箱（BoxCollider）でできた範囲：入っている箱の数
    private float _level = -1f;
    private int _skyDimId;

    private void Start()
    {
        _skyDimId = VRCShader.PropertyToID("_UdonSkyDim");
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = fogColor;
        Apply(0f);
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player != null && player.isLocal) _count++;
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player != null && player.isLocal) _count = Mathf.Max(0, _count - 1);
    }

    private void Update()
    {
        float target = _count > 0 ? 1f : 0f;
        if (Mathf.Approximately(_level, target)) return;
        Apply(Mathf.MoveTowards(Mathf.Max(0f, _level), target, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds)));
    }

    private void Apply(float level)
    {
        _level = level;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = density * level;
        RenderSettings.fog = level > 0.001f;
        VRCShader.SetGlobalFloat(_skyDimId, skyDim * Mathf.SmoothStep(0f, 1f, level));
    }

    private void OnDisable()
    {
        _count = 0;
        Apply(0f);
    }
}
