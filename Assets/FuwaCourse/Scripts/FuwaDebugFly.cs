using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

// テスト用の飛行（2026-10-31 てじゃ「飛行アイテムが明日で消える、デバッグ用の飛行ギミックを」。公開前に消す）。ownerNames の人だけ使える。消す時は FuwaSystem/DebugFly を消すだけ。
// PC：H で オン/オフ。向いている方へ WASD、Space で上、LeftCtrl で下、LeftShift で速く。
// VR：両手のグリップを 1 秒にぎると オン/オフ。向いている方へスティック、右トリガーで速く。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaDebugFly : UdonSharpBehaviour
{
    public KeyCode key = KeyCode.H;
    [Tooltip("この表示名の人だけ使える（空なら誰も使えない）")]
    public string[] ownerNames;
    public float speed = 7f, fastSpeed = 18f, gripHold = 1f;

    private bool _can, _flying, _gripDone;
    private float _moveH, _moveV, _gripTime, _gravity = 1f, _liftUntil;

    private void Start()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || ownerNames == null) return;
        for (int i = 0; i < ownerNames.Length; i++) if (ownerNames[i] == p.displayName) _can = true;
    }

    public override void InputMoveHorizontal(float value, UdonInputEventArgs args) { _moveH = value; }
    public override void InputMoveVertical(float value, UdonInputEventArgs args) { _moveV = value; }

    private void Update()
    {
        if (!_can) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        bool vr = p.IsUserInVR();
        if (vr)
        {
            bool both = Input.GetAxisRaw("Oculus_CrossPlatform_PrimaryHandTrigger") > 0.7f && Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryHandTrigger") > 0.7f;
            _gripTime = both ? _gripTime + Time.deltaTime : 0f;
            if (!both) _gripDone = false;
            if (both && !_gripDone && _gripTime >= gripHold) { _gripDone = true; Toggle(p); }
        }
        else if (Input.GetKeyDown(key)) Toggle(p);
        if (!_flying) return;

        Quaternion head = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
        Vector3 dir = head * Vector3.forward * _moveV + head * Vector3.right * _moveH;
        bool fast;
        if (vr) fast = Input.GetAxisRaw("Oculus_CrossPlatform_SecondaryIndexTrigger") > 0.5f;
        else
        {
            fast = Input.GetKey(KeyCode.LeftShift);
            if (Input.GetKey(KeyCode.Space)) dir += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl)) dir += Vector3.down;
        }
        dir = Vector3.ClampMagnitude(dir, 1f) * (fast ? fastSpeed : speed);
        if (Time.time < _liftUntil) dir.y = Mathf.Max(dir.y, 2f);   // 地面に立っていると横の速さが消されるので、まず少し浮かせる
        p.SetVelocity(dir);
    }

    private void Toggle(VRCPlayerApi p)
    {
        _flying = !_flying;
        if (_flying) { _gravity = p.GetGravityStrength(); p.SetGravityStrength(0f); _liftUntil = Time.time + 0.25f; }
        else p.SetGravityStrength(_gravity);
    }
}
