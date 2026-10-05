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
    [Tooltip("自分の銃がこの台に乗っている時だけ出す目印（▼と足元の輪。なくてもOK）")]
    public GameObject marker;
    [Tooltip("銃がこの距離（m）以内にあれば「台に乗っている」とみなす")]
    public float onStandDistance = 0.3f;
    private float _nextCheck;

    // 取った・召喚した（手元に移った）ら消え、台に戻ったらまた出る。見るのは少しずつ間をあけて
    private void Update()
    {
        if (marker == null || Time.time < _nextCheck) return;
        _nextCheck = Time.time + 0.15f;
        bool show = false;
        if (manager != null && gunSpot != null)
        {
            FuwaBlower gun = manager.GetMyBlower();
            if (gun != null && !gun.IsHeld() && !gun.IsSummonHidden())
                show = (gun.transform.position - gunSpot.position).sqrMagnitude < onStandDistance * onStandDistance;
        }
        if (marker.activeSelf != show) marker.SetActive(show);
    }

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
