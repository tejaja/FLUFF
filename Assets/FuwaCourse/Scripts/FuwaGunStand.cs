using UdonSharp;
using UnityEngine;

// 銃の台。クリック（Interact）すると、どこに置いた自分の銃でもこの台の上に戻ってくる。
// ワープした時はFuwaCourseWarp側で自動的に呼び寄せる。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGunStand : UdonSharpBehaviour
{
    public FuwaRespawnManager manager;
    [Tooltip("銃を置く位置（台の上）")]
    public Transform gunSpot;

    public override void Interact()
    {
        if (manager != null) manager.BringGun(gunSpot);
    }

    // FuwaLanguageから呼ばれる
    public void ApplyLanguage(bool english)
    {
        InteractionText = english ? "Summon air gun" : "エアガンを呼ぶ";
    }
}
