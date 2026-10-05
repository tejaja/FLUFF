using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

// 足場（キノコの傘など）の上に立っている間だけ、自分のジャンプ力を上げる。降りたら元に戻す。ジャンプ力・音・揺れ・光は全部自分の画面だけ。
// トリガー（この物体のコライダー）は判定を拾うための範囲で、実際に上げるのは
// 「足元が足場の上面（surface）の高さにあって、足場の円の内側にいる」時だけ（近くの別の床に立っている時は上げない）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaJumpBoost : UdonSharpBehaviour
{
    [Tooltip("上に立っている間のジャンプ力（VRChatの標準は3）")]
    public float jumpImpulse = 4.2f;
    [Tooltip("ここでジャンプした時の音（スタートのキノコと同じボヨン）")]
    public AudioSource jumpSound;
    [Tooltip("足場の上面（この高さに足元がある時だけ上げる）。未設定なら親の位置")]
    public Transform surface;
    [Tooltip("足場の半径(m)。この内側に立っている時だけ上げる")]
    public float radius = 1.1f;
    [Tooltip("上面からどれだけ下/上まで「立っている」とみなすか(m)")]
    public float below = 0.12f;
    public float above = 0.25f;

    [Header("ジャンプした時のポヨヨン（見た目だけ・自分の画面だけ）")]
    [Tooltip("揺らす見た目（当たり判定とは別の物体にしておく）")]
    public Transform wobbleTarget;
    [Tooltip("0=むにっと潰れて伸びる（傘）、1=根元を支点に板のようにしなる（サルノコシカケ）")]
    public int wobbleMode = 0;
    [Tooltip("揺れの大きさ（潰れ：割合、しなり：角度）")]
    public float wobbleAmount = 0.14f;
    [Tooltip("（使っていない：ばね式になったので、おさまるまでの時間は wobbleDamping で決まる）")]
    public float wobbleTime = 0.7f;
    public float wobbleSpeed = 22f;
    [Tooltip("揺れのおさまる速さ（大きいほどすぐ止まる）")]
    public float wobbleDamping = 6f;

    [Header("乗っている間：力を溜めている感じ（自分の画面だけ）")]
    [Tooltip("乗った時に鳴らす「ためる」音")]
    public AudioSource chargeSound;
    [Tooltip("ほんのり光らせる見た目（空なら wobbleTarget の中のレンダラー全部）")]
    public Renderer[] glowRenderers;
    public Color glowColor = new Color(1f, 0.8f, 0.35f, 1f);
    [Tooltip("光の強さ（いちばん明るい時）")]
    public float glowMax = 0.55f;
    [Tooltip("乗ってから光りきるまでの秒数")]
    public float glowRise = 0.35f;
    [Tooltip("乗っている間、傘のふちから上へ広がって消える光の輪（主観視点でも見えるように）。なくてもOK")]
    public ParticleSystem boostRings;

    private float _glow;
    private MaterialPropertyBlock _mpb;

    private float _restore = -1f;
    private bool _boosted;
    private Vector3 _wobbleBaseScale;
    private Quaternion _wobbleBaseRot;
    private bool _wobbleSaved;

    // ばねの揺れ（_wx = 揺れの量 -1〜1、_wv = その速さ）。ジャンプのたびに勢いを足すので、続けて跳ぶと揺れがつながる。大きさの上限は1回分の揺れと同じくらい
    private float _wx, _wv;
    private bool _wobbling;

    public void StartWobble()
    {
        if (wobbleTarget == null) return;
        if (!_wobbleSaved) { _wobbleBaseScale = wobbleTarget.localScale; _wobbleBaseRot = wobbleTarget.localRotation; _wobbleSaved = true; }
        // 最初にぐっと沈む向きの勢い（1回のジャンプで、前と同じくらいの揺れになる量）
        _wv = Mathf.Clamp(_wv - wobbleSpeed, -wobbleSpeed * 1.2f, wobbleSpeed * 1.2f);
        _wobbling = true;
    }

    private void Update()
    {
        UpdateGlow();
        if (!_wobbling || wobbleTarget == null) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        // 減衰つきのばね（wobbleSpeed＝揺れの速さ、wobbleDamping＝おさまる速さ）
        float acc = -wobbleSpeed * wobbleSpeed * _wx - 2f * wobbleDamping * _wv;
        _wv += acc * dt;
        _wx += _wv * dt;
        // 上限は1回ジャンプした時のいちばん大きい揺れと同じくらい（何回続けて跳んでもそれ以上は大きくならない）
        if (_wx > 0.68f) { _wx = 0.68f; if (_wv > 0f) _wv = 0f; }
        if (_wx < -0.68f) { _wx = -0.68f; if (_wv < 0f) _wv = 0f; }
        if (Mathf.Abs(_wx) < 0.002f && Mathf.Abs(_wv) < 0.02f)
        {
            _wx = 0f; _wv = 0f; _wobbling = false;
            wobbleTarget.localScale = _wobbleBaseScale; wobbleTarget.localRotation = _wobbleBaseRot;
            return;
        }
        float k = _wx;
        if (wobbleMode == 1)
        {
            wobbleTarget.localRotation = _wobbleBaseRot * Quaternion.Euler(-k * wobbleAmount, 0f, 0f);
        }
        else
        {
            float a = k * wobbleAmount;
            wobbleTarget.localScale = new Vector3(_wobbleBaseScale.x * (1f - a * 0.6f), _wobbleBaseScale.y * (1f + a), _wobbleBaseScale.z * (1f - a * 0.6f));
        }
    }

    // 乗っている間はふわっと光って、ゆっくり脈打つ（降りたらすっと消える）
    private void UpdateGlow()
    {
        float target = _boosted ? 1f : 0f;
        if (target == 0f && _glow <= 0f) return;
        float rate = _boosted ? 1f / Mathf.Max(0.05f, glowRise) : 4f;
        _glow = Mathf.MoveTowards(_glow, target, rate * Time.deltaTime);
        if (glowRenderers == null || glowRenderers.Length == 0)
        {
            if (wobbleTarget == null) return;
            glowRenderers = wobbleTarget.GetComponentsInChildren<Renderer>();
        }
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        float pulse = 0.82f + 0.18f * Mathf.Sin(Time.time * 6f);
        float k = _glow * glowMax * (_boosted ? pulse : 1f);
        for (int i = 0; i < glowRenderers.Length; i++)
        {
            Renderer r = glowRenderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", new Color(glowColor.r * k, glowColor.g * k, glowColor.b * k, 1f));
            r.SetPropertyBlock(_mpb);
        }
    }

    private bool OnTop(VRCPlayerApi p)
    {
        Transform s = surface != null ? surface : transform.parent;
        if (s == null) s = transform;
        Vector3 d = p.GetPosition() - s.position;
        if (d.y < -below || d.y > above) return false;
        return d.x * d.x + d.z * d.z <= radius * radius;
    }

    private void SetBoost(VRCPlayerApi p, bool on)
    {
        if (on == _boosted) return;
        _boosted = on;
        if (on)
        {
            _restore = p.GetJumpImpulse();
            p.SetJumpImpulse(Mathf.Max(jumpImpulse, _restore));
            if (chargeSound != null && chargeSound.clip != null) { chargeSound.pitch = Random.Range(0.97f, 1.04f); chargeSound.PlayOneShot(chargeSound.clip, 0.7f); }
            if (boostRings != null) boostRings.Play();
        }
        else
        {
            if (_restore >= 0f) { p.SetJumpImpulse(_restore); _restore = -1f; }
            if (boostRings != null) boostRings.Stop();
        }
    }

    // 上に立っていて、地面にいる時にジャンプを押したら音を鳴らす
    public override void InputJump(bool value, VRC.Udon.Common.UdonInputEventArgs args)
    {
        if (!value || !_boosted) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || !p.IsPlayerGrounded()) return;
        if (jumpSound != null)
        {
            jumpSound.pitch = Random.Range(0.95f, 1.08f);
            jumpSound.PlayOneShot(jumpSound.clip);
        }
        // 揺れも自分の画面だけ（複数人で乗ると揺れや光が重なってわかりにくいので、同期しない）
        StartWobble();
    }

    public override void OnPlayerTriggerStay(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        SetBoost(player, OnTop(player));
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        SetBoost(player, false);
    }
}
