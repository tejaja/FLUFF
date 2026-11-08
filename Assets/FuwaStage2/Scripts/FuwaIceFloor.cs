using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

// つるつるの床の試作（2026-11-03 てじゃ「滑る床、VRChat でうまく動作するだろうか、ためしてみよ」）。
// VRChat は地面に立っている間の横の速さを消すので、人を滑らせる方法を実機で比べる。自分のローカルの動きだけ（同期なし）。
//   mode 0：毎フレーム SetVelocity（歩く速さはそのまま）
//   mode 1：毎フレーム SetVelocity＋氷の上では歩く速さ 0（VRChat の歩きと取り合わないように）
//   mode 2：ふつうに歩かせて、手を離した後の勢いだけ TeleportTo で少しずつ動かす。歩いている間の勢いは実際の動きに合わせる（前の滑る向きを残さない）
//   mode 3：mode 1 と同じ動かし方で、接地は床との距離で自分で見て、ジャンプも自分で出す（SetVelocity 中は VRChat が浮いている扱いにして跳べないので）
//   mode 4：mode 3 と同じ滑り方を毎フレームの小さい TeleportTo で（SetVelocity を使わないので接地のまま＝落下モーションなし、ジャンプも VRChat のまま）。壁は SphereCast で自分で止める
// 比べやすいように VRChat の output_log に [FuwaIce] で 0.5 秒ごとの値を出す
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaIceFloor : UdonSharpBehaviour
{
    public string label = "A";
    public int mode = 0;
    public BoxCollider area;
    [Tooltip("床の面からこの高さまでに足があれば氷の上")]
    public float heightAbove = 1.5f;
    public float accel = 3f, friction = 1f, maxSpeed = 4f;
    [Tooltip("mode 3：床にいる間に下へ押す速さ。強いと接地のままになるかも")]
    public float stick = 0.2f;
    [Tooltip("mode 4：止まる壁のレイヤー")]
    public LayerMask wallMask = 1 | (1 << 11);
    public float bodyRadius = 0.25f;
    [Tooltip("mode 4：氷の上の VRChat の歩く速さ。足を動かすため。全体の速さは変わらない。0 だと歩きモーションなし")]
    public float skateSpeed = 2f;

    [Header("元の値（VRChat の既定値）")]
    public float baseWalk = 2f, baseRun = 4f, baseStrafe = 2f;

    private bool _in, _air;
    private Vector3 _v;
    private float _moveH, _moveV, _nextLog;

    public override void InputMoveHorizontal(float value, UdonInputEventArgs args) { _moveH = value; }
    public override void InputMoveVertical(float value, UdonInputEventArgs args) { _moveV = value; }

    public override void InputJump(bool value, UdonInputEventArgs args)
    {
        if (!value || mode != 3 || !_in || _air) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || Above(p.GetPosition()) > 0.2f) return;
        _air = true;
        p.SetVelocity(new Vector3(_v.x, PlaneY(_v) + p.GetJumpImpulse(), _v.z));
        Debug.Log("[FuwaIce] " + label + " jump");
    }

    // 部屋（プレイエリアの原点）ごと平行に動かす。体の向きは入れ直さない
    //（2026-11-03 VR で、体の向きで TeleportTo し続けると、ジャンプの着地で向きが変になった）
    private void Shift(VRCPlayerApi p, Vector3 step)
    {
        VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
        p.TeleportTo(o.position + step, o.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
    }

    // 足が床の面からどれだけ上か
    private float Above(Vector3 foot)
    {
        return (area.transform.InverseTransformPoint(foot) - area.center).y - area.size.y * 0.5f;
    }

    // 横の速さ h で床の面にそって進む時の上下の速さ（坂用）
    private float PlaneY(Vector3 h)
    {
        Vector3 n = area.transform.up;
        return -(n.x * h.x + n.z * h.z) / n.y;
    }

    private bool OnIce(Vector3 foot)
    {
        Vector3 lp = area.transform.InverseTransformPoint(foot) - area.center;
        Vector3 half = area.size * 0.5f;
        if (Mathf.Abs(lp.x) > half.x || Mathf.Abs(lp.z) > half.z) return false;
        return lp.y > -half.y - 0.2f && lp.y < half.y + heightAbove;
    }

    private void Restore(VRCPlayerApi p)
    {
        _in = false; _air = false;
        if ((mode != 1 && mode != 3 && mode != 4) || p == null) return;
        p.SetWalkSpeed(baseWalk); p.SetRunSpeed(baseRun); p.SetStrafeSpeed(baseStrafe);
    }

    private void OnDisable()
    {
        if (_in) Restore(Networking.LocalPlayer);
    }

    private void Update()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || area == null) return;
        if (p.GetGravityStrength() < 0.01f) { if (_in) Restore(p); return; }   // 2026-11-08 飛んでいる間（重力 0：Hキーの飛行・傘の使う中）は氷の動きをしない
        Vector3 pos = p.GetPosition();
        if (!OnIce(pos))
        {
            if (_in) { Restore(p); Debug.Log("[FuwaIce] " + label + " out"); }
            return;
        }
        Vector3 v = p.GetVelocity();
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        if (!_in)
        {
            _in = true; _v = flat;
            if (mode == 1 || mode == 3) { p.SetWalkSpeed(0f); p.SetRunSpeed(0f); p.SetStrafeSpeed(0f); }
            if (mode == 4) { p.SetWalkSpeed(skateSpeed); p.SetRunSpeed(skateSpeed); p.SetStrafeSpeed(skateSpeed); }
            Debug.Log("[FuwaIce] " + label + " in mode=" + mode + " vr=" + p.IsUserInVR());
        }
        float above = Above(pos);
        if (mode == 3)
        {
            if (_air)
            {
                if (v.y > 0f || above > 0.08f) return;   // 跳んでいる間はそのまま
                _air = false; _v = flat;
                Debug.Log("[FuwaIce] " + label + " land");
            }
        }
        else if (mode != 4 && !p.IsPlayerGrounded()) { _v = flat; return; }   // 空中はふつうの動きにまかせて、着地した時の勢いに使う

        float yaw = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation.eulerAngles.y;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 input = Vector3.ClampMagnitude(q * Vector3.forward * _moveV + q * Vector3.right * _moveH, 1f);
        float dt = Time.deltaTime;
        bool moving = input.sqrMagnitude > 0.01f;
        bool onFloor = above < 0.08f;
        if (mode == 4 && !onFloor) { }   // 空中は勢いをそのまま
        else if (mode == 2 && moving) _v = flat;
        else if (moving) _v = Vector3.MoveTowards(_v, input * maxSpeed, accel * dt);
        else _v = Vector3.MoveTowards(_v, Vector3.zero, friction * dt);

        if (mode == 4)
        {
            float speed = _v.magnitude;
            if (speed > 0.02f)
            {
                Vector3 dir = new Vector3(_v.x, onFloor ? PlaneY(_v) : 0f, _v.z);
                float len = dir.magnitude; dir /= len;
                RaycastHit hit;
                if (Physics.SphereCast(pos + Vector3.up * 0.6f, bodyRadius, dir, out hit, len * dt + 0.05f, wallMask, QueryTriggerInteraction.Ignore))
                {
                    Vector3 n = new Vector3(hit.normal.x, 0f, hit.normal.z);
                    if (n.sqrMagnitude > 0.0001f) { n.Normalize(); float into = Vector3.Dot(_v, n); if (into < 0f) _v -= n * into; }
                    dir = new Vector3(_v.x, onFloor ? PlaneY(_v) : 0f, _v.z); len = dir.magnitude; if (len > 0.0001f) dir /= len;
                }
            }
            // VRChat の歩き（flat）のぶんを引いて、足りない分だけワープで足す。足は歩いているのに、全体の動きは _v のまま
            Vector3 delta = _v - flat;
            if (delta.sqrMagnitude > 0.0004f)
                Shift(p, new Vector3(delta.x, onFloor ? PlaneY(delta) : 0f, delta.z) * dt);
        }
        else if (mode == 2)
        {
            if (!moving && _v.sqrMagnitude > 0.0025f)
                Shift(p, new Vector3(_v.x, PlaneY(_v), _v.z) * dt);
        }
        else if (mode == 3) p.SetVelocity(new Vector3(_v.x, above > 0.08f ? v.y : PlaneY(_v) - stick, _v.z));
        else p.SetVelocity(new Vector3(_v.x, v.y, _v.z));

        if (Time.time >= _nextLog)
        {
            _nextLog = Time.time + 0.5f;
            Debug.Log("[FuwaIce] " + label + " v=" + _v.ToString("F2") + " real=" + v.ToString("F2") + " input=" + input.ToString("F2") + " pos=" + pos.ToString("F2"));
        }
    }
}
