using UdonSharp;
using UnityEngine;
using TMPro;

// 2026-11-17 おまけの島：クリックで自分のエアガンを金ピカに（もう一度でもどす）。金ピカかどうかは銃が同期するので、ほかの人からも見える
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGoldGunSwitch : UdonSharpBehaviour
{
    public FuwaRespawnManager manager;
    public TextMeshPro label;
    public string onText = "きんピカ ON", offText = "きんピカ OFF";
    public AudioSource clickSound;

    private void Start() { Refresh(); }
    private void OnEnable() { Refresh(); }

    public override void Interact()
    {
        FuwaBlower b = manager != null ? manager.GetMyBlower() : null;
        if (b == null) return;
        b.SetGold(!b.IsGold());
        if (clickSound != null && clickSound.enabled && clickSound.gameObject.activeInHierarchy) clickSound.Play();
        Refresh();
    }

    private void Refresh()
    {
        if (label == null) return;
        FuwaBlower b = manager != null ? manager.GetMyBlower() : null;
        label.text = (b != null && b.IsGold()) ? onText : offText;
    }
}
