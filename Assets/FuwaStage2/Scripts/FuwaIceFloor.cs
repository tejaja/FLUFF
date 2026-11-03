using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

// つるつるの床の試作（2026-11-03 てじゃ「滑る床、VRChat でうまく動作するだろうか、ためしてみよ」）。
// VRChat は地面に立っている間の横の速さを消すので、人を滑らせる方法を実機で比べる。自分のローカルの動きだけ（同期なし）。
//   mode 0：毎フレーム SetVelocity（歩く速さはそのまま）
//   mode 1：毎フレーム SetVelocity＋氷の上では歩く速さ 0（VRChat の歩きと取り合わないように）
//   mode 2：ふつうに歩かせて、手を離した後の勢いだけ TeleportTo で少しずつ動かす
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

    [Header("元の値（VRChat の既定値）")]
    public float baseWalk = 2f, baseRun = 4f, baseStrafe = 2f;

    private bool _in;
    private Vector3 _v;
    private float _moveH, _moveV, _nextLog;

    public override void InputMoveHorizontal(float value, UdonInputEventArgs args) { _moveH = value; }
    public override void InputMoveVertical(float value, UdonInputEventArgs args) { _moveV = value; }

    private bool OnIce(Vector3 foot)
    {
        Vector3 lp = area.transform.InverseTransformPoint(foot) - area.center;
        Vector3 half = area.size * 0.5f;
        if (Mathf.Abs(lp.x) > half.x || Mathf.Abs(lp.z) > half.z) return false;
        return lp.y > -half.y - 0.2f && lp.y < half.y + heightAbove;
    }

    private void Restore(VRCPlayerApi p)
    {
        _in = false;
        if (mode != 1 || p == null) return;
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
            if (mode == 1) { p.SetWalkSpeed(0f); p.SetRunSpeed(0f); p.SetStrafeSpeed(0f); }
            Debug.Log("[FuwaIce] " + label + " in mode=" + mode + " vr=" + p.IsUserInVR());
        }
        bool grounded = p.IsPlayerGrounded();
        if (!grounded) { _v = flat; return; }   // 空中はふつうの動きにまかせて、着地した時の勢いに使う

        float yaw = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation.eulerAngles.y;
        Quaternion q = Quaternion.Euler(0f, yaw, 0f);
        Vector3 input = Vector3.ClampMagnitude(q * Vector3.forward * _moveV + q * Vector3.right * _moveH, 1f);
        float dt = Time.deltaTime;
        if (input.sqrMagnitude > 0.01f) _v = Vector3.MoveTowards(_v, input * maxSpeed, accel * dt);
        else _v = Vector3.MoveTowards(_v, Vector3.zero, friction * dt);

        if (mode == 2)
        {
            if (input.sqrMagnitude <= 0.01f && _v.sqrMagnitude > 0.0025f)
                p.TeleportTo(pos + _v * dt, p.GetRotation(), VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, true);
        }
        else p.SetVelocity(new Vector3(_v.x, v.y, _v.z));

        if (Time.time >= _nextLog)
        {
            _nextLog = Time.time + 0.5f;
            Debug.Log("[FuwaIce] " + label + " v=" + _v.ToString("F2") + " real=" + v.ToString("F2") + " input=" + input.ToString("F2") + " pos=" + pos.ToString("F2"));
        }
    }
}
