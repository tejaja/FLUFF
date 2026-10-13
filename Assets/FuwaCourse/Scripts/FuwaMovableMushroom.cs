using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;

// あそびばの跳ねるキノコ（コース5 きのこのたに を誰かがクリアすると出てくる）。持って動かせて、いつも水平のまま（2026-10-13 てじゃ「水平固定で動かせるように」）。
// あそびばのイバラ（コース4のおもちゃ）も同じ仕組みで持って動かす。
// このスクリプトは持つところ（VRCPickup＋VRCObjectSync、見た目なし）に付ける。見た目・傘の当たり・ジャンプ台（body）は持つところの子にしない
// （Rigidbody の子になるとジャンプ台のトリガーの知らせが届かなくなる。竜巻のたねと同じ）ので、毎フレーム持つところの位置と水平の向きへ合わせる。
// 傾けて置くと「横を向いた傘に当たっても真上へ跳ねる」変な動きになるので、傾きは見た目にも出さない
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaMovableMushroom : UdonSharpBehaviour
{
    public Transform body;
    [Tooltip("手を離した時に立てる地面のレイヤー")]
    public LayerMask ground = 1 << 11;
    [Tooltip("下にこれ以上地面がなければ、離したその場に浮かべる")]
    public float maxDrop = 6f;

    private Vector3 _lastPos = new Vector3(0f, -9999f, 0f);
    private float _lastYaw;

    // 持たれている間は、傘の当たり・ジャンプ台・イバラの判定を切る（全員の画面で）。
    // 乗ったまま持つと、持ち上げた傘に押し上げられて手も上がり、どんどん上へ飛んでいった（2026-10-13 てじゃ）
    private VRCPickup _pickup;
    private Collider[] _cols;
    private GameObject[] _boosts;
    private bool _inited;
    private bool _shownHeld;

    private void Init()
    {
        if (_inited) return;
        _inited = true;
        _pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        if (body == null) { _cols = new Collider[0]; _boosts = new GameObject[0]; return; }
        _cols = body.GetComponentsInChildren<Collider>(true);
        FuwaJumpBoost[] jb = body.GetComponentsInChildren<FuwaJumpBoost>(true);
        _boosts = new GameObject[jb.Length];
        for (int i = 0; i < jb.Length; i++) _boosts[i] = jb[i].gameObject;
    }

    private void UpdateHeld()
    {
        Init();
        bool held = _pickup != null && _pickup.IsHeld;
        if (held == _shownHeld) return;
        _shownHeld = held;
        // ジャンプ台はオブジェクトごと止める（OnDisable で上がったジャンプ力が元に戻る。判定だけ切ると出た知らせが来ず戻らない）
        for (int i = 0; i < _boosts.Length; i++) if (_boosts[i] != null) _boosts[i].SetActive(!held);
        for (int i = 0; i < _cols.Length; i++) if (_cols[i] != null && (_boosts.Length == 0 || _cols[i].GetComponent<FuwaJumpBoost>() == null)) _cols[i].enabled = !held;
    }

    // 手を離したら、その場の地面にまっすぐ立てる（持っていた人の画面で。位置は VRCObjectSync が配る）
    public override void OnDrop()
    {
        Vector3 p = transform.position;
        RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 0.3f, Vector3.down, maxDrop + 0.3f, ground, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 at = p;
        for (int i = 0; i < hits.Length; i++)
        {
            // 自分の傘には立たない
            if (body != null && hits[i].collider.transform.IsChildOf(body)) continue;
            if (hits[i].distance < best) { best = hits[i].distance; at = hits[i].point; }
        }
        Vector3 f = transform.forward; f.y = 0f;
        Quaternion rot = f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f.normalized) : Quaternion.identity;
        transform.SetPositionAndRotation(at, rot);
        Follow();
    }

    // 持っている手の動きは PostLateUpdate の時に反映されているので、そのあとで合わせる
    public override void PostLateUpdate() { UpdateHeld(); Follow(); }

    private void Follow()
    {
        if (body == null) return;
        Vector3 p = transform.position;
        float yaw = transform.eulerAngles.y;
        // 動いていない時は当たり判定を動かさない（止まっている当たり判定を毎フレーム動かすと重い）
        if ((p - _lastPos).sqrMagnitude < 0.000001f && Mathf.Abs(Mathf.DeltaAngle(yaw, _lastYaw)) < 0.05f) return;
        _lastPos = p; _lastYaw = yaw;
        body.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
    }
}
