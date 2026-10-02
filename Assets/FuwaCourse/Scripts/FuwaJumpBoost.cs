using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

// 足場（キノコの傘など）の上に立っている間だけ、自分のジャンプ力を上げる。降りたら元に戻す。ジャンプ力と音は自分だけ、揺れは全員に見える。
// トリガー（この物体のコライダー）は判定を拾うための範囲で、実際に上げるのは
// 「足元が足場の上面（surface）の高さにあって、足場の円の内側にいる」時だけ（近くの別の床に立っている時は上げない）。
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]   // 揺れを他の人にも見せるため、ネットワークイベントだけ使う
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

    [Header("ジャンプした時のポヨヨン（見た目だけ・全員に見える）")]
    [Tooltip("揺らす見た目（当たり判定とは別の物体にしておく）")]
    public Transform wobbleTarget;
    [Tooltip("0=むにっと潰れて伸びる（傘）、1=根元を支点に板のようにしなる（サルノコシカケ）")]
    public int wobbleMode = 0;
    [Tooltip("揺れの大きさ（潰れ：割合、しなり：角度）")]
    public float wobbleAmount = 0.14f;
    public float wobbleTime = 0.7f;
    public float wobbleSpeed = 22f;

    private float _restore = -1f;
    private bool _boosted;
    private float _wobbleStart = -1f;
    private Vector3 _wobbleBaseScale;
    private Quaternion _wobbleBaseRot;
    private bool _wobbleSaved;

    public void StartWobble()
    {
        if (wobbleTarget == null) return;
        if (!_wobbleSaved) { _wobbleBaseScale = wobbleTarget.localScale; _wobbleBaseRot = wobbleTarget.localRotation; _wobbleSaved = true; }
        _wobbleStart = Time.time;
    }

    private void Update()
    {
        if (_wobbleStart < 0f || wobbleTarget == null) return;
        float t = Time.time - _wobbleStart;
        if (t >= wobbleTime)
        {
            wobbleTarget.localScale = _wobbleBaseScale; wobbleTarget.localRotation = _wobbleBaseRot;
            _wobbleStart = -1f; return;
        }
        // 最初にぐっと沈んで、ぽよんぽよんと減っていく
        float k = -Mathf.Sin(t * wobbleSpeed) * Mathf.Exp(-t * 6f);
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
        }
        else if (_restore >= 0f)
        {
            p.SetJumpImpulse(_restore);
            _restore = -1f;
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
        // 揺れは全員に（自分も含む）。音とジャンプ力は自分だけ
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(StartWobble));
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
