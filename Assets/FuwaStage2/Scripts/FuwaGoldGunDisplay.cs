using UdonSharp;
using UnityEngine;

// 2026-11-18 てじゃ「台の上に金ピカの銃が光って回ってて、手に取ると元の銃が台に写って手持ちが金ピカ、それ以降は召喚や通常の台のも金ピカになる。
//  またここにある元の銃取ると戻る」：
//  台の上で見本の銃（自分の銃と反対の色：今が元の色なら金ピカ、金ピカなら元の色）が光って回る。
//  見本をクリック → 自分の銃を金ピカ／元の色に変えて、この台に呼ぶ（見本は消え、自分の銃が台の上で回る）。持っていくとまた見本が出る。
//  金ピカかどうかは自分の銃が覚えている（同期）ので、ほかの台・召喚でも金ピカのまま
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGoldGunDisplay : UdonSharpBehaviour
{
    public FuwaRespawnManager manager;
    [Tooltip("自分の銃を呼ぶ位置（台の上）")]
    public Transform gunSpot;
    [Tooltip("見本：金ピカの銃")]
    public GameObject goldModel;
    [Tooltip("見本：元の色の銃")]
    public GameObject normalModel;
    [Tooltip("回して上下させる所")]
    public Transform spin;
    public float spinSpeed = 70f, bobAmp = 0.035f, bobSpeed = 1.8f;
    public Vector3 tilt = new Vector3(-12f, 0f, 0f);
    public AudioSource clickSound;
    public string toGoldText = "きんピカにする", toNormalText = "もとにもどす";
    private int _state = -1;

    public override void Interact()
    {
        FuwaBlower b = manager != null ? manager.GetMyBlower() : null;
        if (b == null) return;
        b.SetGold(!b.IsGold());
        manager.BringGun(gunSpot);
        if (clickSound != null && clickSound.enabled && clickSound.gameObject.activeInHierarchy) clickSound.Play();
        _state = -1;
    }

    private void Update()
    {
        FuwaBlower b = manager != null ? manager.GetMyBlower() : null;
        bool docked = b != null && gunSpot != null && b.GetDock() == gunSpot;
        bool gold = b != null && b.IsGold();
        // 0＝見本なし（自分の銃がこの台にある）、1＝金ピカの見本、2＝元の色の見本
        int s = docked ? 0 : (gold ? 2 : 1);
        if (s != _state)
        {
            _state = s;
            if (goldModel != null) goldModel.SetActive(s == 1);
            if (normalModel != null) normalModel.SetActive(s == 2);
            DisableInteractive = s == 0;
            InteractionText = s == 2 ? toNormalText : toGoldText;
        }
        if (s != 0 && spin != null)
        {
            spin.localPosition = Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobAmp;
            spin.localRotation = Quaternion.Euler(0f, Time.time * spinSpeed, 0f) * Quaternion.Euler(tilt);
        }
    }
}
