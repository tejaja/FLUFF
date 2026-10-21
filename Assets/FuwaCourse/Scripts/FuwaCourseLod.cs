using UdonSharp;
using UnityEngine;

// ひとりでモードで、自分がいるコース以外のコースを「床だけの軽い版」に切り替える（自分の画面だけ）。
// ・どのコースにいるかは、ふわふわのスタート地点（ball.startPoint）が starts[i] のどれかで決める（BGM と同じ判定）
// ・ロビーにいる間（どれにも当てはまらない）は全部ふつうに表示
// ・他の人のアバターは消せないので、軽い版で床だけは見せて「空中に浮いて見える」のを防ぐ
// ・onlyMobile が ON なら、Android/iOS 向けビルド（Quest・スマホ）の時だけ働く。PC では何もしない（全部ふつう表示）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaCourseLod : UdonSharpBehaviour
{
    public FuwaBall ball;
    public FuwaAreaVisibility areaVisibility;
    [Tooltip("ひとりでモードの番号（FuwaAreaVisibility の area）")]
    public int soloArea = 1;
    [Tooltip("各コースのふわふわのスタート地点")]
    public Transform[] starts;
    [Tooltip("各コースの本体（starts と同じ順）")]
    public GameObject[] fullRoots;
    [Tooltip("各コースの床だけの軽い版（starts と同じ順）")]
    public GameObject[] liteRoots;
    [Tooltip("何秒おきに調べるか")]
    public float interval = 0.5f;
    [Tooltip("ON なら Mobile（Android/iOS ビルド）の時だけ働く")]
    public bool onlyMobile = true;

    // ロビーにいる間（どのコースにも入っていない時）は、コースの見た目はそのまま、遠くの毎フレーム動く仕掛け（Udon の Update）と粒を止める
    //（2026-10-18 てじゃ「ロビーが色々見えてるから重い」「ギミックとめるのはありだな」。FuwaCourseLodLobbyBuilder が集める）
    [Tooltip("ロビーの間だけ止める仕掛け（Update で動く Udon）")]
    public VRC.Udon.UdonBehaviour[] lobbyStopBehaviours;
    [Tooltip("ロビーの間だけ隠す粒（パーティクルの物）")]
    public GameObject[] lobbyHideObjects;
    [Tooltip("ロビーの間も、自分からこの距離(m)以内の仕掛け・粒は動かす（2026-10-22 てじゃ「ロビーからの見た目はほしい」→ 近くだけ動かす案）。0なら全部止める")]
    public float lobbyNearRadius = 40f;
    [Tooltip("0=きれい（全部ふつう表示・ロビーも全部動く）、1=ふつう（他コースは軽い版・ロビーは近くだけ動く）、2=かるい（他コースは軽い版・ロビーは全部止める）。ロビーのボタン（FuwaQualitySwitch）で切り替える。スマホ・Quest は始めから2")]
    public int mode = 1;
    private int _lobbyState = -1;   // 0=止めていない、1=止めている
    private bool[] _bRun, _oRun;

    private void Start()
    {
#if UNITY_ANDROID || UNITY_IOS
        mode = 2;
#endif
    }

    public int GetMode() { return mode; }

    // ロビーのボタンから呼ぶ（自分だけ）。次の Update で全部やり直す
    public void SetMode(int m)
    {
        mode = Mathf.Clamp(m, 0, 2);
        _shown = -2; _lobbyState = -1; _next = 0f;
        enabled = true;
    }

    private float _next;
    private int _shown = -2;   // いま反映している状態（-1=全部ふつう、i=コースiにいる）
    private int _area = -1;

    private void Update()
    {
#if !(UNITY_ANDROID || UNITY_IOS)
        if (onlyMobile) { enabled = false; return; }
#endif
        if (Time.time < _next) return;
        _next = Time.time + interval;
        int area = areaVisibility != null ? areaVisibility.GetArea() : soloArea;
        int cur = -1;
        if (area == soloArea && ball != null && starts != null)
        {
            Transform sp = ball.startPoint;
            for (int i = 0; i < starts.Length; i++) if (starts[i] != null && starts[i] == sp) { cur = i; break; }
        }
        // エリアが変わった時は、FuwaAreaVisibility がコースの本体を出し直しているので必ずやり直す
        if (cur != _shown || area != _area)
        {
            _shown = cur; _area = area;
            Apply(area == soloArea, cur);
        }
        if (_lobbyState == 1) UpdateLobbyNear();
    }

    // ロビーの間：近くの物だけ動かす。境目でちらつかないよう、出す距離と止める距離に少し差をつける
    private void UpdateLobbyNear()
    {
        VRC.SDKBase.VRCPlayerApi p = VRC.SDKBase.Networking.LocalPlayer;
        if (p == null || lobbyNearRadius <= 0f || mode != 1) return;
        Vector3 me = p.GetPosition();
        float rOn = lobbyNearRadius * lobbyNearRadius, rOff = (lobbyNearRadius + 5f) * (lobbyNearRadius + 5f);
        if (lobbyStopBehaviours != null)
            for (int i = 0; i < lobbyStopBehaviours.Length; i++)
            {
                var b = lobbyStopBehaviours[i]; if (b == null) continue;
                float d = (b.transform.position - me).sqrMagnitude;
                bool run = _bRun[i] ? d < rOff : d < rOn;
                if (run != _bRun[i]) { _bRun[i] = run; b.enabled = run; }
            }
        if (lobbyHideObjects != null)
            for (int i = 0; i < lobbyHideObjects.Length; i++)
            {
                var o = lobbyHideObjects[i]; if (o == null) continue;
                float d = (o.transform.position - me).sqrMagnitude;
                bool run = _oRun[i] ? d < rOff : d < rOn;
                if (run != _oRun[i]) { _oRun[i] = run; o.SetActive(run); }
            }
    }

    private void Apply(bool inSolo, int cur)
    {
        int n = fullRoots != null ? fullRoots.Length : 0;
        int c = mode == 0 ? -1 : cur;   // きれい：コースの中でも全部ふつう表示
        for (int i = 0; i < n; i++)
        {
            bool full = inSolo && (c < 0 || c == i);
            bool lite = inSolo && c >= 0 && c != i;
            if (fullRoots[i] != null && inSolo && fullRoots[i].activeSelf != full) fullRoots[i].SetActive(full);
            if (liteRoots != null && i < liteRoots.Length && liteRoots[i] != null && liteRoots[i].activeSelf != lite) liteRoots[i].SetActive(lite);
        }
        int lobby = mode != 0 && inSolo && cur < 0 ? 1 : 0;
        if (lobby == _lobbyState) return;
        _lobbyState = lobby;
        bool run = lobby == 0;
        int nb = lobbyStopBehaviours != null ? lobbyStopBehaviours.Length : 0, no = lobbyHideObjects != null ? lobbyHideObjects.Length : 0;
        if (_bRun == null) { _bRun = new bool[nb]; _oRun = new bool[no]; }
        for (int i = 0; i < nb; i++) { _bRun[i] = run; if (lobbyStopBehaviours[i] != null) lobbyStopBehaviours[i].enabled = run; }
        for (int i = 0; i < no; i++) { _oRun[i] = run; if (lobbyHideObjects[i] != null) lobbyHideObjects[i].SetActive(run); }
    }
}
