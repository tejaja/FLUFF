using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

// 見学用の飛べる傘（2026-11-07 てじゃ「見学とか用の飛行ギミックを全員用に」「Pickupして使う感じ」「①がよくある」「銃を持ったら消える」「並べておくやつ」）
// ・ロビーの傘立てに並べてある同期ピックアップ（VRCObjectSync）。持っている傘はみんなに見える。人が持っている傘は取れない
// ・持って「使う」（トリガー／クリック／スマホの使うボタン）を押している間、傘の向き（PC・スマホは見ている方）へ飛ぶ
// ・押していない間は、持っているだけでゆっくり降りる（落ちる速さに上限）
// ・手放す・エアガンを持つ → 傘立ての元の場所へ戻る（ObjectSync の Respawn）
// ・傘立てでは閉じていて、持つと開く（シェイプキー Closed。開き具合は持っているかどうかの同期変数から、それぞれの画面で動かす）
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class FuwaUmbrella : UdonSharpBehaviour
{
    public FuwaRespawnManager manager;
    [Tooltip("開け閉めする布（シェイプキー 0 = Closed）")]
    public SkinnedMeshRenderer canopy;
    public float speed = 7f;
    [Tooltip("持っているだけの時、落ちる速さの上限(m/s)")]
    public float glideFall = 1.6f;
    [Tooltip("開く・閉じるのにかかる秒数")]
    public float openTime = 0.35f;
    public AudioSource openSound;

    [UdonSynced] private bool _held;
    private bool _mine, _using;
    private float _gravity = 1f, _liftUntil, _open, _shownOpen = -1f;
    private VRCPickup _pickup;
    private VRCObjectSync _sync;

    private void Start()
    {
        _pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        _sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        ShowOpen(0f);
    }

    public override void OnPickup()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        _mine = true; _using = false;
        _gravity = p.GetGravityStrength();
        _held = true; RequestSerialization();
        if (openSound != null) openSound.Play();
    }

    public override void OnDrop()
    {
        if (!_mine) return;
        EndFlight();
        _mine = false;
        _held = false; RequestSerialization();
        if (_sync != null) _sync.Respawn();   // 傘立ての元の場所へ
    }

    public override void OnPickupUseDown()
    {
        if (!_mine) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        _using = true;
        p.SetGravityStrength(0f);
        if (p.IsPlayerGrounded()) _liftUntil = Time.time + 0.25f;   // 地面に立っていると横の速さが消されるので、まず少し浮かせる
    }

    public override void OnPickupUseUp()
    {
        if (!_mine) return;
        _using = false;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p != null) p.SetGravityStrength(_gravity);
    }

    private void EndFlight()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p != null && _using) p.SetGravityStrength(_gravity);
        _using = false;
    }

    private void Update()
    {
        // 開き具合（だれの画面でも、持っているかどうかで）
        float target = _held ? 1f : 0f;
        _open = Mathf.MoveTowards(_open, target, Time.deltaTime / Mathf.Max(0.01f, openTime));
        if (_open != _shownOpen) ShowOpen(_open);

        if (!_mine) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        // エアガンを持ったら手放す
        if (manager != null)
        {
            FuwaBlower gun = manager.GetMyBlower();
            if (gun != null && gun.IsHeld()) { if (_pickup != null) _pickup.Drop(); return; }
        }
        if (_using)
        {
            Vector3 dir;
            if (p.IsUserInVR()) dir = transform.up;   // 傘の向き（布の方）
            else dir = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
            Vector3 v = dir.normalized * speed;
            if (Time.time < _liftUntil) v.y = Mathf.Max(v.y, 2f);
            p.SetVelocity(v);
        }
        else if (!p.IsPlayerGrounded())
        {
            Vector3 v = p.GetVelocity();
            if (v.y < -glideFall) { v.y = -glideFall; p.SetVelocity(v); }
        }
    }

    // 持っていた人が抜けた：残った持ち主（引きついだ人）が閉じて戻す
    public override void OnPlayerLeft(VRCPlayerApi player)
    {
        if (!_held || !Networking.IsOwner(gameObject)) return;
        if (_pickup != null && _pickup.IsHeld) return;
        _held = false; RequestSerialization();
        if (_sync != null) _sync.Respawn();
    }

    private void ShowOpen(float o)
    {
        _shownOpen = o;
        if (canopy == null) return;
        float e = o * o * (3f - 2f * o);
        canopy.SetBlendShapeWeight(0, (1f - e) * 100f);
    }
}
