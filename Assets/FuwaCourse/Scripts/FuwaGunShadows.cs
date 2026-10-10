using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// みんなのエアガンのうそ影（本物の影は切ってある。自分の画面だけ）。
// 銃の見た目の真ん中から真下へ線を飛ばし、当たった地面の上下うすい範囲だけに塗る（FuwaCourse/BlobDecal の箱。アバターの体には塗らない）。
// 銃の前後に合わせた楕円で、銃の大きさ（アバターの大きさ）に合わせて伸び縮み、高く離れるほど薄く・少し大きく。
// 影の箱は銃（PlayerObject）の子にすると、掴める物の枠線に箱の形が出るので、ここ（シーン側）に人数ぶん並べておく
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGunShadows : UdonSharpBehaviour
{
    [Tooltip("エアガン（PlayerObject の元）")]
    public FuwaBlower blowerTemplate;
    public Renderer[] shadows;
    [Tooltip("等倍の時の影の大きさ（x=横幅, y=前後の長さ m）")]
    public Vector2 size = new Vector2(0.22f, 0.42f);
    public LayerMask groundMask = (1 << 0) | (1 << 11);
    [Tooltip("この高さ(m)まで離れると消える")]
    public float fadeHeight = 3f;
    public float alpha = 0.45f;
    [Tooltip("当たった地面から上・下に何mの範囲の表面に描くか")]
    public float above = 0.06f, below = 0.3f;
    [Tooltip("自分からこれより遠い銃の影は出さない(m)")]
    public float maxView = 40f;
    public int refreshFrames = 30;

    private VRCPlayerApi[] _players = new VRCPlayerApi[96];
    private FuwaBlower[] _blowers = new FuwaBlower[96];
    private Vector3[] _centers = new Vector3[96];
    private bool[] _centerReady = new bool[96];
    private int _count, _frame;
    private Material[] _mats;
    private float[] _alpha;

    private void Start()
    {
        int n = shadows != null ? shadows.Length : 0;
        _mats = new Material[n]; _alpha = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (shadows[i] == null) continue;
            _mats[i] = shadows[i].material;
            _alpha[i] = -1f;
            shadows[i].enabled = false;
        }
    }

    private void Refresh()
    {
        _count = VRCPlayerApi.GetPlayerCount();
        if (_players.Length < _count)
        {
            _players = new VRCPlayerApi[_count + 8]; _blowers = new FuwaBlower[_count + 8];
            _centers = new Vector3[_count + 8]; _centerReady = new bool[_count + 8];
        }
        VRCPlayerApi.GetPlayers(_players);
        for (int i = 0; i < _count; i++)
        {
            VRCPlayerApi p = _players[i];
            FuwaBlower b = null;
            if (p != null && Utilities.IsValid(p) && blowerTemplate != null)
            {
                Component c = Networking.FindComponentInPlayerObjects(p, blowerTemplate);
                if (c != null) b = (FuwaBlower)c;
            }
            if (b != _blowers[i]) _centerReady[i] = false;
            _blowers[i] = b;
        }
    }

    private void LateUpdate()
    {
        if (shadows == null) return;
        if (_frame-- <= 0) { _frame = refreshFrames; Refresh(); }
        VRCPlayerApi me = Networking.LocalPlayer;
        Vector3 mePos = me != null ? me.GetPosition() : Vector3.zero;
        int used = 0;
        for (int i = 0; i < _count && used < shadows.Length; i++)
        {
            FuwaBlower b = _blowers[i];
            if (b == null || !Utilities.IsValid(b) || !b.IsGunShown()) continue;
            Transform v = b.GetVisual();
            if (!_centerReady[i])
            {
                // 見た目の真ん中（見た目のローカル座標）を一度だけ求める
                Renderer[] rs = v.GetComponentsInChildren<Renderer>(true);
                Vector3 sum = Vector3.zero; int n = 0;
                for (int k = 0; k < rs.Length; k++) { if (rs[k] == null) continue; sum += rs[k].bounds.center; n++; }
                _centers[i] = n > 0 ? v.InverseTransformPoint(sum / n) : Vector3.zero;
                _centerReady[i] = true;
            }
            Vector3 c = v.TransformPoint(_centers[i]);
            if ((c - mePos).sqrMagnitude > maxView * maxView) continue;
            Renderer r = shadows[used];
            if (r == null) { used++; continue; }
            RaycastHit hit;
            if (!Physics.Raycast(c, Vector3.down, out hit, fadeHeight + 0.5f, groundMask, QueryTriggerInteraction.Ignore)) continue;
            float h = Mathf.Clamp01(hit.distance / Mathf.Max(0.01f, fadeHeight));
            float s = b.GetAutoScale() * (1f + h * 0.5f);
            Vector3 fwd = v.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) { fwd = v.up; fwd.y = 0f; }
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            Transform t = r.transform;
            t.SetPositionAndRotation(hit.point + Vector3.up * ((above - below) * 0.5f), Quaternion.LookRotation(fwd.normalized, Vector3.up));
            t.localScale = new Vector3(size.x * s, above + below, size.y * s);
            float a = alpha * (1f - h);
            if (_mats[used] != null && Mathf.Abs(a - _alpha[used]) > 0.01f) { _alpha[used] = a; _mats[used].SetFloat("_Alpha", a); }
            if (!r.enabled) r.enabled = true;
            used++;
        }
        for (int i = used; i < shadows.Length; i++)
            if (shadows[i] != null && shadows[i].enabled) shadows[i].enabled = false;
    }
}
