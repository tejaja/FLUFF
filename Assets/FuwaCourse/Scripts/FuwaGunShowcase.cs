using UdonSharp;
using UnityEngine;

// 台に乗っている自分のエアガンを目立たせる見た目（自分にだけ見える）。
// 本物の銃は台の上では見えなくしてあるので（FuwaBlower.Dock）、代わりに見た目だけの銃をふわふわ浮かせてゆっくり回し、
// 後ろにやわらかい光、下から小さいキラキラを出す。乗っている台へ毎フレーム移動する（1個を使い回し）。
// 掴むのは本物の銃（VRは手を近づけると握る位置で手についてくる：FuwaGunSummon）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGunShowcase : UdonSharpBehaviour
{
    public FuwaRespawnManager manager;
    [Tooltip("中身（見た目の銃・光・キラキラ）。台に乗っていない時はまとめて消す")]
    public GameObject content;
    [Tooltip("回して上下させる見た目の銃")]
    public Transform spin;
    [Tooltip("1秒に回る角度")]
    public float spinSpeed = 70f;
    [Tooltip("上下にふわふわする幅(m)と速さ")]
    public float bobAmp = 0.035f;
    public float bobSpeed = 1.8f;
    [Tooltip("見た目の銃の傾き（少し銃口を上げて、横から形が分かるように）")]
    public Vector3 tilt = new Vector3(-12f, 0f, 0f);

    private void Update()
    {
        FuwaBlower gun = manager != null ? manager.GetMyBlower() : null;
        Transform dock = gun != null ? gun.GetDock() : null;
        bool show = dock != null;
        if (content != null && content.activeSelf != show) content.SetActive(show);
        if (!show) return;
        float s = gun.GetAutoScale();
        transform.position = gun.GetDockPoint();
        transform.localScale = Vector3.one * s;
        if (spin != null)
        {
            spin.localPosition = Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobAmp;
            spin.localRotation = Quaternion.Euler(0f, Time.time * spinSpeed, 0f) * Quaternion.Euler(tilt);
        }
        // 本物の銃の枠線用ダミーを、見た目の銃に重ねる（近づけた時・指した時に回っている銃のまわりに枠が出る）
        if (shownVisual != null) gun.SetDockHighlightPose(shownVisual);
    }

    [Tooltip("見た目の銃（Spin の下の Visual）。本物の銃の枠線用ダミーをここに重ねる")]
    public Transform shownVisual;
}
