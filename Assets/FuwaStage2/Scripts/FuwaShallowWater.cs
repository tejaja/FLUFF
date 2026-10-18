using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 浅瀬（ステージ2「みずのたに」、2026-10-17 てじゃ「浅瀬はプレイヤー減速（ジャンプ初速も下がる、高さは同じ）」）。
// 水の入れ物のトリガー（FuwaWater と同じ物）に付ける。自分の足が水の中にある間：
//   歩く・走る・横の速さ × moveMul、ジャンプの初速 × jumpMul、重力 × jumpMul²（＝跳べる高さは同じで、ふわっとゆっくり）。
//   水から跳んで出ても、着地するまではそのまま（空中で急に重くならないように）。
// 入った瞬間に「じゃぶ」の音と水しぶき（ほかの人が入った時も、その人の所で鳴る）
// 判定はトリガーの出入りの通知ではなく、毎フレーム足の位置で見る（蓮の葉が沈んで水の中に入った時も拾える。通知は取りこぼすこともあるので）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaShallowWater : UdonSharpBehaviour
{
    [Range(0.1f, 1f)] public float moveMul = 0.55f;
    [Range(0.1f, 1f)] public float jumpMul = 0.7f;
    [Tooltip("水面からこの高さまでに足があれば水の中（蓮の葉が水面すれすれまで沈んだ時も入るように）")]
    public float surfaceAbove = 0.03f;
    [Tooltip("着地しないまま水から出て、これだけたったら戻す（念のため）")]
    public float restoreTimeout = 3f;

    [Header("元の値（VRChatの既定値。今の値を覚えると、ほかの減速中の値を覚えて戻らなくなるので決まった値で）")]
    public float baseWalk = 2f, baseRun = 4f, baseStrafe = 2f, baseJump = 3f, baseGravity = 1f;

    [Header("音・しぶき")]
    [Tooltip("鳴らす場所（クリップは下の2つ）。clip には強い方（ザブン）を入れておく")]
    public AudioSource splashSound;
    [Tooltip("歩いて入る・軽く触れた時の小さい「ちゃぷ」（2026-10-17 てじゃ「軽くふれてもザブンなるのが違和感」）")]
    public AudioClip softClip;
    [Tooltip("下向きにこの速さ(m/s)より速く落ちてきたら「ザブン」")]
    public float strongFall = 2.5f;
    public FuwaWaterFx fx;
    public float splashStrength = 1.2f;

    private Collider _col;
    private bool _in;
    private bool _slowed;
    private float _leftAt;
    private float _lastSplash = -10f;

    private void Start()
    {
        _col = GetComponent<Collider>();
    }

    private bool FeetInWater(Vector3 f)
    {
        if (_col == null) return false;
        Bounds b = _col.bounds;
        return f.x > b.min.x && f.x < b.max.x && f.z > b.min.z && f.z < b.max.z && f.y < b.max.y + surfaceAbove && f.y > b.min.y - 0.3f;
    }

    private void Update()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || !p.IsValid()) return;
        Vector3 f = p.GetPosition();
        bool inside = FeetInWater(f);
        if (inside && !_in) Splash(new Vector3(f.x, _col.bounds.max.y, f.z), p.GetVelocity());
        if (!inside && _in) _leftAt = Time.time;
        _in = inside;
        if (inside)
        {
            // 重なったほかの水やイバラが速さを戻しても、中にいる間は遅くし直す
            if (!_slowed || p.GetWalkSpeed() > baseWalk * moveMul + 0.01f || p.GetJumpImpulse() > baseJump * jumpMul + 0.01f) Apply(p);
            // ジャンプ中は歩く速さを変えても勢いが残るので、空中でも横の速さを抑える
            if (!p.IsPlayerGrounded())
            {
                Vector3 v = p.GetVelocity(); Vector3 h = new Vector3(v.x, 0f, v.z);
                float lim = baseRun * moveMul;
                if (h.magnitude > lim) { h = h.normalized * lim; p.SetVelocity(new Vector3(h.x, v.y, h.z)); }
            }
            return;
        }
        if (_slowed && (p.IsPlayerGrounded() || Time.time - _leftAt > restoreTimeout)) Restore(p);
    }

    private void Apply(VRCPlayerApi p)
    {
        p.SetWalkSpeed(baseWalk * moveMul);
        p.SetRunSpeed(baseRun * moveMul);
        p.SetStrafeSpeed(baseStrafe * moveMul);
        p.SetJumpImpulse(baseJump * jumpMul);
        p.SetGravityStrength(baseGravity * jumpMul * jumpMul);
        if (!_slowed)
        {
            // 入った瞬間の勢いも水にからめ取られる
            Vector3 v = p.GetVelocity();
            p.SetVelocity(new Vector3(v.x * moveMul, v.y > 0f ? v.y * jumpMul : v.y * 0.6f, v.z * moveMul));
        }
        _slowed = true;
    }

    private void Restore(VRCPlayerApi p)
    {
        p.SetWalkSpeed(baseWalk); p.SetRunSpeed(baseRun); p.SetStrafeSpeed(baseStrafe);
        p.SetJumpImpulse(baseJump); p.SetGravityStrength(baseGravity);
        _slowed = false;
    }

    // ほかの人が入った時（自分は Update の足の位置で見る）
    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || player.isLocal || _col == null) return;
        Vector3 f = player.GetPosition();
        Splash(new Vector3(f.x, _col.bounds.max.y, f.z), player.GetVelocity());
    }

    // 入り方で音を分ける：落ちてきた（下向きが速い）→ザブン、歩いて入る・軽く触れる→小さい「ちゃぷ」。勢いで音量も少し変える
    private void Splash(Vector3 at, Vector3 vel)
    {
        if (Time.time - _lastSplash < 0.25f) return;
        _lastSplash = Time.time;
        float fall = -vel.y;
        bool strong = fall > strongFall || softClip == null;
        if (splashSound != null)
        {
            splashSound.transform.position = at; splashSound.pitch = Random.Range(0.92f, 1.08f);
            AudioClip c = strong ? splashSound.clip : softClip;
            float vol = strong ? Mathf.Lerp(0.7f, 1f, Mathf.InverseLerp(strongFall, strongFall * 2.5f, fall))
                               : Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(new Vector3(vel.x, 0f, vel.z).magnitude / 4f + Mathf.Max(0f, fall) / strongFall));
            if (c != null) splashSound.PlayOneShot(c, vol);
        }
        if (fx != null) fx.Visual(at, strong ? splashStrength : splashStrength * 0.35f);
    }

    private void OnDisable()
    {
        if (!_slowed) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p != null && p.IsValid()) Restore(p);
        _in = false;
    }
}
