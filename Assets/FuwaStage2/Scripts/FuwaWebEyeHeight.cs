using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2026-11-15 2–5 中段：田の字のクモの巣の門（WebGates）の「まん中の線」の高さを、自分のアバターの目の高さに合わせる
//  てじゃ「クモの巣の上を通るところ、アバターの大きさで難易度変わるのがヤだな」「チビだとつらい」「いっそアバターの高さでクモの巣の位置かえちゃうとか」
//  まん中の線 = 目の高さ + midAboveEye（minMid〜maxMid）。下のマスの巣は 0〜線、上のマスの巣は 線〜hTop に縦をのばす・ちぢめる
//  自分の画面だけ（ふわふわは自分の物なので当たりも自分の画面の位置でいい）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWebEyeHeight : UdonSharpBehaviour
{
    public Transform[] lower;      // 下のマスの巣（0〜hMid）
    public Transform[] upper;      // 上のマスの巣（hMid〜hTop）
    public float baseY;            // 床の高さ（親の座標）
    public float hMid = 2.7f;      // 作った時のまん中の線
    public float hTop = 5.3f;
    public float midAboveEye = 1.1f;
    public float minMid = 1.5f, maxMid = 3.2f;

    private float[] _lowS, _upS;
    private bool _ready;

    private void Start()
    {
        _lowS = new float[lower.Length]; _upS = new float[upper.Length];
        for (int i = 0; i < lower.Length; i++) if (lower[i] != null) _lowS[i] = lower[i].localScale.y;
        for (int i = 0; i < upper.Length; i++) if (upper[i] != null) _upS[i] = upper[i].localScale.y;
        _ready = true;
        Apply();
        // アバターの読み込みが終わる前は目の高さがまだ違うことがあるので、少しあとにもう一度
        SendCustomEventDelayedSeconds(nameof(Apply), 3f);
    }

    // エリアが隠れている間にアバターを変えた時のため、出てきた時にも合わせる
    private void OnEnable()
    {
        if (_ready) Apply();
    }

    public override void OnAvatarEyeHeightChanged(VRCPlayerApi player, float prevEyeHeightAsMeters)
    {
        if (player != null && player.isLocal) Apply();
    }

    public override void OnAvatarChanged(VRCPlayerApi player)
    {
        if (player != null && player.isLocal) SendCustomEventDelayedSeconds(nameof(Apply), 1f);
    }

    public void Apply()
    {
        if (!_ready) return;
        var lp = Networking.LocalPlayer;
        if (lp == null) return;
        float eye = lp.GetAvatarEyeHeightAsMeters();
        if (eye <= 0.01f) return;
        float m = Mathf.Clamp(eye + midAboveEye, minMid, Mathf.Min(maxMid, hTop - 1.2f));
        float kl = m / hMid, ku = (hTop - m) / (hTop - hMid);
        for (int i = 0; i < lower.Length; i++)
        {
            var t = lower[i]; if (t == null) continue;
            var s = t.localScale; s.y = _lowS[i] * kl; t.localScale = s;
            var p = t.localPosition; p.y = baseY + m * 0.5f; t.localPosition = p;
        }
        for (int i = 0; i < upper.Length; i++)
        {
            var t = upper[i]; if (t == null) continue;
            var s = t.localScale; s.y = _upS[i] * ku; t.localScale = s;
            var p = t.localPosition; p.y = baseY + (m + hTop) * 0.5f; t.localPosition = p;
        }
    }
}
