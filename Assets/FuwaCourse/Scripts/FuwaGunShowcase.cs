using UdonSharp;
using UnityEngine;

// 台に乗っている自分のエアブラスターを目立たせる見た目（自分にだけ見える）。
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
        ApplyGold(gun.IsGold());
        float s = gun.GetAutoScale();
        transform.position = gun.GetDockPoint();
        transform.localScale = Vector3.one * s;
        // VRで目の前に呼び出した時（見えない台 plainSpot）：回さず光らせず、デスクトップの召喚みたいにまっすぐ止めて見せる（2026-11-11 てじゃ）
        bool plain = plainSpot != null && dock == plainSpot;
        if (!_baseRotSet) { _baseRot = transform.rotation; _baseRotSet = true; if (spin != null) _spinScale = spin.localScale; }
        if (!plain && _wasPlain) { transform.rotation = _baseRot; if (spin != null) spin.localScale = _spinScale; }
        _wasPlain = plain;
        if (halo != null && halo.activeSelf == plain) halo.SetActive(!plain);
        if (sparkles != null && sparkles.activeSelf == plain) sparkles.SetActive(!plain);
        if (plain)
        {
            transform.rotation = dock.rotation;
            // 本物の銃とぴったり同じ大きさ・位置に（台の見た目は1.4倍で少しずらしてあるので打ち消す）。離した銃をそのまま置いた時に大きさが変わらないように
            if (spin != null)
            {
                spin.localScale = Vector3.one;
                spin.localRotation = Quaternion.identity;
                spin.localPosition = shownVisual != null ? -shownVisual.localPosition : Vector3.zero;
            }
        }
        else if (spin != null)
        {
            spin.localPosition = Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobAmp;
            spin.localRotation = Quaternion.Euler(0f, Time.time * spinSpeed, 0f) * Quaternion.Euler(tilt);
        }
        // 本物の銃の枠線用ダミーを、見た目の銃に重ねる（近づけた時・指した時に回っている銃のまわりに枠が出る）
        if (shownVisual != null) gun.SetDockHighlightPose(shownVisual);
    }

    [Tooltip("見た目の銃（Spin の下の Visual）。本物の銃の枠線用ダミーをここに重ねる")]
    public Transform shownVisual;
    [Tooltip("VRで目の前に呼び出した時の見えない台（FuwaGunSummon.vrFrontSpot）。ここに乗っている時は回さない・光らせない")]
    public Transform plainSpot;
    [Tooltip("後ろのやわらかい光・下のキラキラ（plainSpot の時は消す）")]
    public GameObject halo;
    public GameObject sparkles;
    private Quaternion _baseRot; private bool _baseRotSet, _wasPlain; private Vector3 _spinScale = Vector3.one;

    // 2026-11-18 金ピカの銃：台の上の見た目の銃も、本物が金ピカなら金ピカで回す（前は元の色で回っていた）。
    //  goldRenderers と goldMats は同じ順（FuwaBonusHubBuilder が本物の銃の金色の材質から名前でそろえる）
    [Tooltip("見た目の銃のうち、金ピカにする所")]
    public Renderer[] goldRenderers;
    public Material[] goldMats;
    [Tooltip("金ピカの間だけ出すキラキラ（なくてもOK）")]
    public GameObject goldFx;
    [Tooltip("金ピカの時にメッシュも差しかえる所（頂点の色でまとめた銃。FuwaGunMerge）。goldMeshes と同じ順")]
    public MeshFilter[] goldMeshFilters;
    public Mesh[] goldMeshes;
    private Material[] _normalMats;
    private Mesh[] _normalMeshes;
    private int _goldShown = -1;
    private void ApplyGold(bool on)
    {
        int s = on ? 1 : 0;
        if (s == _goldShown || goldRenderers == null || goldMats == null) return;
        if (_normalMats == null)
        {
            _normalMats = new Material[goldRenderers.Length];
            for (int i = 0; i < goldRenderers.Length; i++) if (goldRenderers[i] != null) _normalMats[i] = goldRenderers[i].sharedMaterial;
        }
        _goldShown = s;
        for (int i = 0; i < goldRenderers.Length; i++)
        {
            Renderer r = goldRenderers[i]; if (r == null) continue;
            r.sharedMaterial = (on && i < goldMats.Length && goldMats[i] != null) ? goldMats[i] : _normalMats[i];
        }
        if (goldMeshFilters != null && goldMeshes != null)
        {
            if (_normalMeshes == null)
            {
                _normalMeshes = new Mesh[goldMeshFilters.Length];
                for (int i = 0; i < goldMeshFilters.Length; i++) if (goldMeshFilters[i] != null) _normalMeshes[i] = goldMeshFilters[i].sharedMesh;
            }
            for (int i = 0; i < goldMeshFilters.Length; i++)
            {
                MeshFilter f = goldMeshFilters[i]; if (f == null) continue;
                Mesh mm = (on && i < goldMeshes.Length && goldMeshes[i] != null) ? goldMeshes[i] : _normalMeshes[i];
                if (mm != null) f.sharedMesh = mm;
            }
        }
        if (goldFx != null) goldFx.SetActive(on);
    }
}
