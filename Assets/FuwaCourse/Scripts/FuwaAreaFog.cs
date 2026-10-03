using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 範囲（このオブジェクトのトリガー）の中に自分がいる間だけ、うっすら霧をかける（自分の画面だけ）。
// 出入りでふわっと濃さを変える。霧に対応したシェーダー（ToonLit など）にかかる
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaAreaFog : UdonSharpBehaviour
{
    public Color fogColor = new Color(0.84f, 0.83f, 0.9f, 1f);
    [Tooltip("霧の濃さ（Exponential）。0.008 で 50m 先が 3割くらいかすむ")]
    public float density = 0.008f;
    public float fadeSeconds = 1.2f;

    private int _count;   // 複数の箱（BoxCollider）でできた範囲：入っている箱の数
    private float _level;

    private void Start()
    {
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = 0f;
        RenderSettings.fog = false;
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
        _level = Mathf.MoveTowards(_level, target, Time.deltaTime / Mathf.Max(0.01f, fadeSeconds));
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = density * _level;
        RenderSettings.fog = _level > 0.001f;
    }

    private void OnDisable()
    {
        _count = 0; _level = 0f;
        RenderSettings.fog = false;
    }
}
