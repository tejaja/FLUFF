using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

// 見学用の飛べる傘（2026-11-07 てじゃ「見学とか用の飛行ギミックを全員用に」「Pickupして使う感じ」「①がよくある」「銃を持ったら消える」「並べておくやつ」）
// ・ロビーの傘立てに並べてある同期ピックアップ（VRCObjectSync）。持っている傘はみんなに見える。人が持っている傘は取れない
// ・持って「使う」（トリガー／クリック／スマホの使うボタン）を押している間、傘の向き（PC・スマホは見ている方）へ飛ぶ
// ・押していない間は、持っているだけでゆっくり降りる（落ちる速さに上限）
// ・手放す・エアガンを持つ → その場にしばらく残って（returnDelay 秒）、だれも持たなければ傘立ての元の場所へ戻る（ObjectSync の Respawn）
//   2026-11-08 てじゃ「離してもしばらく残っててほしい」
// ・傘立てでは閉じていて、持つと開く（シェイプキー Closed。開き具合は持っているかどうかの同期変数から、それぞれの画面で動かす）
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class FuwaUmbrella : UdonSharpBehaviour
{
    public FuwaRespawnManager manager;
    [Tooltip("開け閉めする布（シェイプキー 0 = Closed）")]
    public SkinnedMeshRenderer canopy;
    public float speed = 7f;
    [Tooltip("持っているだけの時、落ちる速さの上限(m/s)")]
    public float glideFall = 0.6f;   // 2026-11-08 てじゃ「落下速度もかなり遅くて良い」（1.6→0.6）
    [Tooltip("使うを離した時に残す速さの割合（小さいほどピタッと止まる）")]
    public float stopKeep = 0.12f;   // 2026-11-08 てじゃ「クリックやめたときにもっと強めに止まって」
    [Tooltip("見た目（Body・Canopy の親）。デスクトップの人が持っている間は、だれの画面でもまっすぐ立てる")]
    public Transform visual;
    [Tooltip("持つ所（傘の local。ExactGrip と同じ所）")]
    public Vector3 gripLocal = new Vector3(0f, -0.01f, 0f);
    [Tooltip("持っている人が見上げると、自分の画面でだけ布を網点で透かす（2026-11-08 てじゃ「上向くほど傘が透けるとか」）。x=透け始める視線の上向き(sin)、y=いちばん透ける上向き、z=いちばん透けた時の量")]
    public Vector3 lookUpFade = new Vector3(0.25f, 0.8f, 0.85f);
    private MaterialPropertyBlock _mpb;
    private float _shownFade = -1f;
    [Tooltip("開く・閉じるのにかかる秒数")]
    public float openTime = 0.35f;
    public AudioSource openSound;
    [Tooltip("手放してから傘立てに戻るまでの秒数")]
    public float returnDelay = 20f;

    [UdonSynced] private bool _held;
    private bool _mine, _using, _waiting;
    private float _dropTime;
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
        _mine = true; _using = false; _waiting = false;
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
        _waiting = true; _dropTime = Time.time;   // しばらくその場に残る
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
        if (p == null) return;
        p.SetGravityStrength(_gravity);
        p.SetVelocity(p.GetVelocity() * stopKeep);   // 強めに止める
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

        // デスクトップの人が持つと手の向きで真横になる（2026-11-08 てじゃ）→ 見た目だけ、持つ所を中心にまっすぐ立てる
        if (visual != null)
        {
            VRCPlayerApi o = _held ? Networking.GetOwner(gameObject) : null;
            if (o != null && !o.IsUserInVR())
            {
                Vector3 f = o.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward; f.y = 0f;
                if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
                Quaternion r = Quaternion.LookRotation(f.normalized, Vector3.up);
                visual.rotation = r;
                visual.position = transform.TransformPoint(gripLocal) - r * gripLocal;
            }
            else if (visual.localRotation != Quaternion.identity || visual.localPosition != Vector3.zero) { visual.localRotation = Quaternion.identity; visual.localPosition = Vector3.zero; }
        }

        // 見上げた時の透け（自分が持っている時だけ、自分の画面で）
        float fade = 0f;
        if (_mine)
        {
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (lp != null)
            {
                float up = (lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward).y;
                fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lookUpFade.x, lookUpFade.y, up)) * lookUpFade.z;
            }
        }
        if (Mathf.Abs(fade - _shownFade) > 0.01f || (fade == 0f && _shownFade != 0f)) ShowFade(fade);

        // 手放した傘：持ち主の画面で、だれも持たないまま returnDelay たったら傘立てへ（ほかの人が持ったら持ち主が変わるのでやめる）
        if (_waiting)
        {
            if (!Networking.IsOwner(gameObject) || _held) _waiting = false;
            else if (Time.time - _dropTime > returnDelay) { _waiting = false; if (_sync != null) _sync.Respawn(); }
        }
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
        if (!Networking.IsOwner(gameObject)) return;
        if (_pickup != null && _pickup.IsHeld) return;
        if (_held) { _held = false; RequestSerialization(); }
        // 手放したまま残っていた傘も、引きついだ人の画面で時間がたったら戻す（傘立てにある傘は戻しても同じ場所）
        if (!_waiting) { _waiting = true; _dropTime = Time.time; }
    }

    private void ShowFade(float f)
    {
        _shownFade = f;
        if (canopy == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        canopy.GetPropertyBlock(_mpb);
        _mpb.SetFloat("_DitherFade", f);
        canopy.SetPropertyBlock(_mpb);
    }

    private void ShowOpen(float o)
    {
        _shownOpen = o;
        if (canopy == null) return;
        float e = o * o * (3f - 2f * o);
        canopy.SetBlendShapeWeight(0, (1f - e) * 100f);
    }
}
