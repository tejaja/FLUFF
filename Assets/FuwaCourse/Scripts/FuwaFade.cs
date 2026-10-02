using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Rendering;

// 暗転（自分の画面だけ）。頭の位置に板を置き、シェーダー(Fuwa/ScreenFade)で画面全体に描いて不透明度でフェードする。
// 本人の目にだけ描くので、VRChatのカメラ・ミラー・第三者視点には映らない。
// Play(待ち, 暗くなる秒数, 真っ暗の秒数, 明ける秒数)
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaFade : UdonSharpBehaviour
{
    public Renderer fadeRenderer;
    public Color color = Color.black;

    private Material _mat;
    private float _start = -100f;
    private float _delay, _outTime, _hold, _inTime;
    private bool _active;
    private Color _tint;
    private float _maxAlpha = 1f;

    private void Start()
    {
        if (fadeRenderer != null)
        {
            _mat = fadeRenderer.material;
            fadeRenderer.enabled = false;
        }
    }

    public void Play(float delay, float outTime, float hold, float inTime)
    {
        _tint = color;
        _maxAlpha = 1f;
        Begin(delay, outTime, hold, inTime);
    }

    // 色つきで少しだけ覆う（アイテムの「きりのたね」のモヤなど）。maxAlpha＝一番濃い時の不透明度
    public void PlayTint(Color tint, float maxAlpha, float inTime, float hold, float outTime)
    {
        // 暗転中は上書きしない
        if (_active && _maxAlpha >= 1f) return;
        _tint = tint;
        _maxAlpha = Mathf.Clamp01(maxAlpha);
        Begin(0f, inTime, hold, outTime);
    }

    private void Begin(float delay, float outTime, float hold, float inTime)
    {
        _start = Time.time;
        _delay = delay;
        _outTime = Mathf.Max(0.01f, outTime);
        _hold = hold;
        _inTime = Mathf.Max(0.01f, inTime);
        _active = true;
    }

    // 暗転の間、自分を動けなくする（ワープ・リスポーン用）。続けて呼ばれたら、あとの方の時間まで。
    // 戻すのは毎フレームの見張り（PostLateUpdate）で行う。遅延イベントで戻すと、ワープ直後のカクつきで
    // Time.time とイベントの時計がずれて「まだ早い」と判定され、二度と戻らないことがあった。
    private float _freezeUntil;
    private bool _frozen;
    [Tooltip("動けなくする最長の秒数（何があってもこれを過ぎたら戻す）")]
    public float maxFreezeSeconds = 3f;
    private float _frozenSince;

    public void FreezeFor(float seconds)
    {
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null) return;
        local.SetVelocity(Vector3.zero);
        local.Immobilize(true);
        if (!_frozen) _frozenSince = Time.time;
        _frozen = true;
        _freezeUntil = Mathf.Max(_freezeUntil, Time.time + seconds);
    }

    // すぐ動けるように戻す
    public void ReleaseFreeze()
    {
        _frozen = false;
        _freezeUntil = 0f;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local != null) local.Immobilize(false);
    }

    public override void PostLateUpdate()
    {
        if (_frozen && (Time.time >= _freezeUntil || Time.time - _frozenSince > maxFreezeSeconds)) ReleaseFreeze();
        if (!_active || fadeRenderer == null) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local != null && local.IsValid())
        {
            // デスクトップの三人称視点ではカメラが頭から離れるので、画面のカメラの位置に置く（シェーダーは「カメラの近く」でだけ描く）
            VRCCameraSettings cam = VRCCameraSettings.ScreenCamera;
            if (!local.IsUserInVR() && cam != null) transform.position = cam.Position;
            else transform.position = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        }
        float t = Time.time - _start - _delay;
        float a;
        if (t < 0f) a = 0f;
        else if (t < _outTime) a = t / _outTime;
        else if (t < _outTime + _hold) a = 1f;
        else if (t < _outTime + _hold + _inTime) a = 1f - (t - _outTime - _hold) / _inTime;
        else { a = 0f; _active = false; }
        a *= _maxAlpha;
        fadeRenderer.enabled = a > 0.001f;
        if (_mat != null)
        {
            Color c = _tint;
            c.a = a;
            _mat.color = c;
        }
    }
}

