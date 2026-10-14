using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

// スマホの画面に出す「エアガンを呼ぶ」ボタン（2026-10-14 てじゃ「スマホ版で銃を呼ぶボタン」→ 置き場所はA：右側、VRChatの「手」ボタンのすぐ上）
// ・スマホ（Android/iOS版で VR でない人）だけ。銃を持っていない時だけ出す。タップ（Interact）で目の前に銃を出す（FuwaGunSummon.SummonInFront）
// ・画面のカメラ（VRCCameraSettings.ScreenCamera）の向き・画角・ピクセル数から、毎フレーム同じ画面の位置に置き直す（縦画面・横画面どちらも）
// ・位置は画面の短い辺 S を単位に「右はしから rightFromEdge×S、下はしから upFromBottom×S」。VRChatの手ボタンも短い辺に合わせた大きさなので、どちらの向きでも手ボタンの上に来る
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaMobileGunButton : UdonSharpBehaviour
{
    public FuwaGunSummon summon;
    public FuwaRespawnManager manager;
    [Tooltip("見た目（Fuwa/ScreenButton の板）")]
    public Renderer visual;
    [Tooltip("タップを受ける当たり判定")]
    public Collider hit;
    [Tooltip("ボタンの中心：画面の右はしからの距離（短い辺に対する割合）")]
    public float rightFromEdge = 0.14f;
    [Tooltip("ボタンの中心：画面の下はしからの距離（短い辺に対する割合）")]
    public float upFromBottom = 0.78f;
    [Tooltip("ボタンの直径（短い辺に対する割合）")]
    public float diameter = 0.14f;
    [Tooltip("カメラからの距離(m)。近すぎると近クリップで切れる")]
    public float distance = 0.35f;
    [Tooltip("テスト用：PCでも出す")]
    public bool forceShow = false;

    private bool _mobile;
    private bool _shown = true;

    private void Start()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        bool vr = lp != null && lp.IsUserInVR();
#if UNITY_ANDROID || UNITY_IOS
        _mobile = !vr;
#else
        _mobile = false;
#endif
        Show(false);
    }

    private void Show(bool on)
    {
        if (on == _shown) return;
        _shown = on;
        if (visual != null) visual.enabled = on;
        if (hit != null) hit.enabled = on;
    }

    public override void PostLateUpdate()
    {
        if (!_mobile && !forceShow) { Show(false); return; }
        FuwaBlower gun = manager != null ? manager.GetMyBlower() : null;
        if (gun == null || gun.IsHeld()) { Show(false); return; }
        VRCCameraSettings cam = VRCCameraSettings.ScreenCamera;
        if (cam == null) { Show(false); return; }
        float w = cam.PixelWidth, h = cam.PixelHeight;
        if (w < 1f || h < 1f) { Show(false); return; }
        float s = Mathf.Min(w, h);
        // 画面の位置（ピクセル）→ -1〜1
        float nx = (w - rightFromEdge * s) / w * 2f - 1f;
        float ny = (upFromBottom * s) / h * 2f - 1f;
        float tanV = Mathf.Tan(cam.FieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * (w / h);
        float d = Mathf.Max(distance, cam.NearClipPlane * 2f);
        Quaternion rot = cam.Rotation;
        transform.SetPositionAndRotation(cam.Position + rot * new Vector3(nx * tanH * d, ny * tanV * d, d), rot);
        float size = diameter * s * (2f * d * tanV / h);   // ピクセル → m
        transform.localScale = new Vector3(size, size, size);
        Show(true);
    }

    public override void Interact()
    {
        if (summon != null) summon.SummonInFront();
    }
}
