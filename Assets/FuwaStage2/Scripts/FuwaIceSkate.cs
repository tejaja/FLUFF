using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

// 2–3 こおりのほら：氷の道の上で人が滑る（2026-11-03、氷のテスト場 FuwaIceFloor の mode 4 をコース用にしたもの。てじゃ実機で「Cいいね」）。
// ・足の下を下向きのレイで見て、当たったのが ice の当たりなら氷の上。曲がった道・坂でも使える
// ・動かし方：毎フレームの小さい TeleportTo（部屋ごと平行に動かす）。SetVelocity だと VRChat が浮いている扱いにして落下モーションが出て跳べない
// ・歩く速さ skateSpeed は残して足を動かし、VRChat の歩きのぶんを引いて足りない分だけワープで足す
// ・坂では下る向きに少しずつ流される（slopeSlide）
// 自分のローカルの動きだけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaIceSkate : UdonSharpBehaviour
{
    [Tooltip("氷の道の当たり（この中のどれかに足が乗っていたら滑る）")]
    public Collider[] ice;
    [Tooltip("足の下を見るレイのレイヤー（道・壁）")]
    public LayerMask groundMask = 1 | (1 << 11);
    [Tooltip("滑っている時に止まる壁のレイヤー")]
    public LayerMask wallMask = 1 | (1 << 11);
    public float accel = 3f, friction = 1f, maxSpeed = 4f, skateSpeed = 2f, bodyRadius = 0.25f;
    [Tooltip("坂で下る向きに流される強さ（重力×sin(傾き)×これ）")]
    public float slopeSlide = 0.5f;

    [Header("元の値（VRChat の既定値）")]
    public float baseWalk = 2f, baseRun = 4f, baseStrafe = 2f;

    private bool _in;
    private Vector3 _v, _lastPos;
    private float _moveH, _moveV;

    public override void InputMoveHorizontal(float value, UdonInputEventArgs args) { _moveH = value; }
    public override void InputMoveVertical(float value, UdonInputEventArgs args) { _moveV = value; }

    private bool IsIce(Collider c)
    {
        if (c == null || ice == null) return false;
        for (int i = 0; i < ice.Length; i++) if (ice[i] == c) return true;
        return false;
    }

    private void Enter(VRCPlayerApi p, Vector3 flat)
    {
        _in = true; _v = flat;
        p.SetWalkSpeed(skateSpeed); p.SetRunSpeed(skateSpeed); p.SetStrafeSpeed(skateSpeed);
    }

    private void Exit(VRCPlayerApi p)
    {
        _in = false;
        if (p != null) { p.SetWalkSpeed(baseWalk); p.SetRunSpeed(baseRun); p.SetStrafeSpeed(baseStrafe); }
    }

    private void OnDisable()
    {
        if (_in) Exit(Networking.LocalPlayer);
    }

    // 部屋（プレイエリアの原点）ごと平行に動かす。体の向きは入れ直さない（VR で着地の向きが変になった）
    private void Shift(VRCPlayerApi p, Vector3 step)
    {
        VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
        p.TeleportTo(o.position + step, o.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
    }

    private void Update()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        Vector3 pos = p.GetPosition();
        Vector3 v = p.GetVelocity();
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        float dt = Time.deltaTime;
        // ワープ・リスポーンで大きく動いたら、勢いを捨てて氷から出る
        if (_in && (pos - _lastPos).sqrMagnitude > 4f) Exit(p);
        _lastPos = pos;

        RaycastHit g;
        bool hitGround = Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out g, 0.45f, groundMask, QueryTriggerInteraction.Ignore);
        bool onFloor = hitGround && g.distance < 0.38f;
        if (hitGround)
        {
            bool iceHere = IsIce(g.collider);
            if (iceHere && !_in) Enter(p, flat);
            else if (!iceHere && _in) { Exit(p); return; }
        }
        if (!_in) return;   // 空中は、氷から跳んだ・落ちた時だけ勢いをそのまま

        Vector3 n = onFloor ? g.normal : Vector3.up;
        float yaw = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation.eulerAngles.y;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 input = Vector3.ClampMagnitude(q * Vector3.forward * _moveV + q * Vector3.right * _moveH, 1f);
        if (onFloor)
        {
            if (input.sqrMagnitude > 0.01f) _v = Vector3.MoveTowards(_v, input * maxSpeed, accel * dt);
            else _v = Vector3.MoveTowards(_v, Vector3.zero, friction * dt);
            // 坂：下る向きへ流される
            Vector3 down = Vector3.ProjectOnPlane(Vector3.down, n); down.y = 0f;
            _v += down * (9.81f * slopeSlide * dt);
        }

        if (_v.sqrMagnitude > 0.0004f)
        {
            Vector3 dir = new Vector3(_v.x, 0f, _v.z).normalized;
            RaycastHit w;
            if (Physics.SphereCast(pos + Vector3.up * 0.6f, bodyRadius, dir, out w, _v.magnitude * dt + 0.05f, wallMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 wn = new Vector3(w.normal.x, 0f, w.normal.z);
                if (wn.sqrMagnitude > 0.0001f) { wn.Normalize(); float into = Vector3.Dot(_v, wn); if (into < 0f) _v -= wn * into; }
            }
        }
        // VRChat の歩き（flat）のぶんを引いて、足りない分だけワープで足す
        Vector3 delta = _v - flat;
        if (delta.sqrMagnitude > 0.0004f)
        {
            float dy = onFloor ? -(n.x * delta.x + n.z * delta.z) / n.y : 0f;   // 坂の面にそって
            Shift(p, new Vector3(delta.x, dy, delta.z) * dt);
        }
    }
}
