using UdonSharp;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDKBase;

// 自分から遠い物は影を落とさないようにする（自分の画面だけ）。小物用に、近い時だけ影を出す別の設定（shadowDistance 短め）でも使う。
// 対象と中心・半径はエディタの「FuwaCourse/遠景の軽量化を設定」で詰める。毎フレーム少しずつ見て回る。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaFarShadows : UdonSharpBehaviour
{
    [Tooltip("自分からこの距離(m)より遠い物は影を落とさない（物のふちまでの距離）")]
    public float shadowDistance = 40f;
    [Tooltip("1フレームに見る数")]
    public int perFrame = 120;
    public Renderer[] targets;
    public Vector3[] centers;
    public float[] radii;
    [Tooltip("2026-11-16 ポータルをのぞいている間は、向こう側の自分の位置から近い物も影を出す（空ならなし）")]
    public FuwaPortalCams portalCams;

    private bool[] _off;
    private int _next;

    // 毎フレーム「targets == null」と比べるだけで 0.7ms かかっていた（1400個の Renderer の配列。2026-11-17 計測）ので、準備できたかは bool で持つ
    private bool _ready;

    private void Start()
    {
        if (targets == null) return;
        _off = new bool[targets.Length];
        // 最初から影オフにしてある物（近くだけ影を出す小物用）は、オフの状態から始める
        for (int i = 0; i < targets.Length; i++)
            if (targets[i] != null) _off[i] = targets[i].shadowCastingMode == ShadowCastingMode.Off;
        _ready = targets.Length > 0;
    }

    // 2026-11-17 軽くした（ステージ2のロビーで、FarShadows と SmallShadows の2つだけで Udon の Update が約4.3ms/フレーム＝全体の4割だった）：
    // ・今いるステージの物だけ見る（targetStage：1=ステージ1、2=ステージ2、0=どちらでもない物はいつも見る。FuwaFarViewSetup が入れる）
    // ・1フレームに見る数は「一覧を cycleFrames フレームで1周する」だけ（前は毎フレーム perFrame 個＝80〜120個）
    // ・距離は平方根なしで比べる（しきい値の2乗を先に作っておく）
    [Tooltip("各 targets がどのステージの物か（1/2、0=どちらでもない）。FuwaFarViewSetup が入れる")]
    public int[] targetStage;
    public FuwaAreaVisibility areaVisibility;
    [Tooltip("今いるステージの一覧を何フレームで1周するか")]
    public int cycleFrames = 30;
    private float[] _thr2;
    private int[] _list1, _list2;
    private int _shownStage = -1;
    private int[] _list;
    private int _listN;
    private bool _built, _hasArea, _hasPortal;

    private void BuildLists()
    {
        _hasArea = areaVisibility != null; _hasPortal = portalCams != null;
        int n = targets.Length;
        _thr2 = new float[n];
        int c1 = 0, c2 = 0;
        for (int i = 0; i < n; i++)
        {
            float t = shadowDistance + radii[i]; _thr2[i] = t * t;
            int s = targetStage != null && i < targetStage.Length ? targetStage[i] : 0;
            if (s != 2) c1++;
            if (s != 1) c2++;
        }
        _list1 = new int[c1]; _list2 = new int[c2]; c1 = 0; c2 = 0;
        for (int i = 0; i < n; i++)
        {
            int s = targetStage != null && i < targetStage.Length ? targetStage[i] : 0;
            if (s != 2) { _list1[c1] = i; c1++; }
            if (s != 1) { _list2[c2] = i; c2++; }
        }
    }

    private void Update()
    {
        if (!_ready) return;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null || !local.IsValid()) return;
        if (!_built) { BuildLists(); _built = true; }
        int stage = _hasArea ? areaVisibility.GetStage() : 1;
        if (stage != _shownStage) { _shownStage = stage; _list = stage == 2 ? _list2 : _list1; _listN = _list.Length; _next = 0; }
        int n = _listN; if (n == 0) return;
        Vector3 me = local.GetPosition();
        bool view = _hasPortal && portalCams.viewOn;
        Vector3 q = view ? portalCams.viewPos : me;
        int cf = cycleFrames > 0 ? cycleFrames : 30;   // シーンに前から置いてある物は 0 のことがある
        int count = (n + cf - 1) / cf;
        if (count > n) count = n;
        for (int k = 0; k < count; k++)
        {
            if (_next >= n) _next = 0;
            int i = _list[_next]; _next++;
            Renderer r = targets[i];
            if (r == null) continue;
            bool far = (me - centers[i]).sqrMagnitude > _thr2[i];
            if (far && view) far = (q - centers[i]).sqrMagnitude > _thr2[i];
            if (far == _off[i]) continue;
            _off[i] = far;
            r.shadowCastingMode = far ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }
    }
}
