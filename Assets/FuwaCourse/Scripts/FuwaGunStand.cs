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
    [Tooltip("等倍の時の、台の上から銃のてっぺんまでの高さ(m)（浮かせる高さ0.18＋銃の上半分0.1）。銃が大きくなった分▼を上げるのに使う")]
    public float gunTopHeight = 0.3f;
    private float _nextCheck;
    private Transform _arrow;
    private Vector3 _arrowBase;

    private void Start()
    {
        if (marker == null) return;
        _arrow = marker.transform.Find("Arrow");
        if (_arrow != null) _arrowBase = _arrow.localPosition;
    }

    // 取った・召喚した（手元に移った）ら消え、台に戻ったらまた出る。見るのは少しずつ間をあけて
    private void Update()
    {
        if (marker == null || Time.time < _nextCheck) return;
        _nextCheck = Time.time + 0.15f;
        bool show = false;
        if (manager != null && gunSpot != null)
        {
            FuwaBlower gun = manager.GetMyBlower();
            if (gun != null) show = gun.GetDock() == gunSpot;   // 自分の銃がこの台に乗っている（持つと外れる）
            // アバターが大きいと銃も大きくなる（台から浮かせる高さ dockHover も一緒に）ので、▼が銃に埋もれないよう、
            // 銃のてっぺんが上がった分だけ▼も上げる。すき間は等倍の時のまま（2026-10-11 てじゃ）
            if (show && _arrow != null)
            {
                float k = Mathf.Max(1f, gun.transform.localScale.x);
                _arrow.localPosition = new Vector3(_arrowBase.x, _arrowBase.y + gunTopHeight * (k - 1f), _arrowBase.z);
            }
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
