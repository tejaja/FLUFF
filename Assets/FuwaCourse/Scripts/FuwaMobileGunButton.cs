using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

// スマホの画面に出す「エアガンを呼ぶ」ボタン（2026-10-14 てじゃ「スマホ版で銃を呼ぶボタン」→ はじめは手ボタンの上 → 手ボタンは銃を持ってない時は出ず半端に見えたので、右上固定・少し大きく）
// ・見た目（visual）は別の物の板（このスクリプトが一緒に動かす）
// ・タップは UI のボタン（子の Canvas＋VRCUiShape、透明な Button → OnTap）で受ける。Interact 方式だとスマホでは押せる物の青い枠が当たり判定の形でずっと出ていた（2026-10-14 てじゃ）
// ・スマホ（Android/iOS版で VR でない人）だけ。銃を持っていない時だけ出す。タップで目の前に銃を出す（FuwaGunSummon.SummonInFront）
// ・画面のカメラ（VRCCameraSettings.ScreenCamera）の向き・画角・ピクセル数から、毎フレーム同じ画面の位置に置き直す（縦画面・横画面どちらも）
// ・位置は画面の短い辺 S を単位に「右はしから rightFromEdge×S、上はしから downFromTop×S」（縦横どちらでも同じ見た目の位置・大きさ）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaMobileGunButton : UdonSharpBehaviour
{
    public FuwaGunSummon summon;
    public FuwaRespawnManager manager;
    [Tooltip("見た目（Fuwa/ScreenButton の板）")]
    public Renderer visual;
    [Tooltip("タップを受ける UI（Canvas の物。表示中だけ有効）")]
    public GameObject ui;
    [Tooltip("ボタンの中心：画面の右はしからの距離（短い辺に対する割合）")]
    public float rightFromEdge = 0.12f;
    [Tooltip("ボタンの中心：画面の上はしからの距離（短い辺に対する割合）")]
    public float downFromTop = 0.13f;
    [Tooltip("ボタンの直径（短い辺に対する割合）")]
    public float diameter = 0.16f;
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
        if (ui != null) ui.SetActive(on);
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
        float ny = (h - downFromTop * s) / h * 2f - 1f;
        float tanV = Mathf.Tan(cam.FieldOfView * 0.5f * Mathf.Deg2Rad);
        float tanH = tanV * (w / h);
        float d = Mathf.Max(distance, cam.NearClipPlane * 2f);
        Quaternion rot = cam.Rotation;
        Vector3 pos = cam.Position + rot * new Vector3(nx * tanH * d, ny * tanV * d, d);
        float size = diameter * s * (2f * d * tanV / h);   // ピクセル → m
        Vector3 sc = new Vector3(size, size, size);
        transform.SetPositionAndRotation(pos, rot);
        transform.localScale = sc;
        if (visual != null) { Transform vt = visual.transform; vt.SetPositionAndRotation(pos, rot); vt.localScale = sc; }
        Show(true);
    }

    public void OnTap()
    {
        if (summon != null) summon.SummonInFront();
    }
}
