using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

// 2026-11-19 てじゃ「ボードとかの位置リセットボタンもほしいのと、かくしデータもボタンで展開するタイプにしよ」
//  おまけ本島のボタン（クリック）。
//  ・mode 0：位置リセット：resetObjects（VRCObjectSync つき）を、持ち主をもらってから最初の位置にもどす（みんなの画面で）
//  ・mode 1：ひらく／とじる：toggleTarget を出したり消したり（自分の画面だけ）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaBonusButton : UdonSharpBehaviour
{
    public int mode = 0;
    public GameObject[] resetObjects;
    public GameObject toggleTarget;
    public Transform pressPart;
    public AudioSource clickSound;
    public string openText = "あしあとをみる", closeText = "あしあとをとじる";
    // 2026-11-19 てじゃ「レーザーボタン式にしよ」：レーザーで押すボタン（UI の Button の onClick → SendCustomEvent("Press")）。label はボタンの文字（ひらく／とじるで切りかえ）
    [Tooltip("レーザーボタンの文字（mode 1 で ひらく／とじる に切りかえ。なくてもOK）")]
    public TMPro.TextMeshProUGUI label;
    private float _pressAt = -10f;
    private Vector3 _pressBase;

    private void Start()
    {
        if (pressPart != null) _pressBase = pressPart.localPosition;
        if (mode == 1) { if (toggleTarget != null) toggleTarget.SetActive(false); InteractionText = openText; if (label != null) label.text = openText; }
    }

    public override void Interact() { Press(); }

    public void Press()
    {
        _pressAt = Time.time;
        if (clickSound != null && clickSound.gameObject.activeInHierarchy) clickSound.Play();
        if (mode == 0)
        {
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (resetObjects == null || lp == null) return;
            for (int i = 0; i < resetObjects.Length; i++)
            {
                GameObject o = resetObjects[i]; if (o == null) continue;
                VRCPickup pu = (VRCPickup)o.GetComponent(typeof(VRCPickup));
                if (pu != null && pu.IsHeld) continue;   // だれかが持っている物はそのまま
                Networking.SetOwner(lp, o);
                VRCObjectSync sync = (VRCObjectSync)o.GetComponent(typeof(VRCObjectSync));
                if (sync != null) sync.Respawn();
            }
        }
        else if (toggleTarget != null)
        {
            bool on = !toggleTarget.activeSelf;
            toggleTarget.SetActive(on);
            InteractionText = on ? closeText : openText;
            if (label != null) label.text = on ? closeText : openText;
        }
    }

    private void Update()
    {
        if (pressPart == null) return;
        float u = (Time.time - _pressAt) / 0.25f;
        if (u < 0f || u > 1f) { if (u > 1f && u < 2f) pressPart.localPosition = _pressBase; return; }
        pressPart.localPosition = _pressBase + Vector3.down * 0.03f * Mathf.Sin(u * Mathf.PI);
    }
}
