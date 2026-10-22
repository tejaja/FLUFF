using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

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

    // 他のコースに飛んで近づいた時は、そのコースを本体に戻す（2026-10-22 てじゃ「横とか上から接近したとき、ある程度近づくと切り替わるように」）
    // コース全体の箱1個だと隣のコースと重なるので、床を cellSize 角のマスに分けて、マスごとに「中の床がある範囲」の箱で持つ（FuwaCourseLite が入れる）。
    // マスそのままの箱だと、端に床がかかっただけで最大10m近く見えた（コース4のスタートでコース5が戻ったまま）
    [Tooltip("床の箱（ワールド座標）の小さい側")]
    public Vector3[] cellMin;
    [Tooltip("床の箱（ワールド座標）の大きい側")]
    public Vector3[] cellMax;
    [Tooltip("cellMin/cellMax がどのコースの床か（fullRoots の番号）")]
    public int[] cellCourse;
    public float cellSize = 10f;
    [Tooltip("他のコースの床のマスからこの距離(m)以内に近づいたら本体に戻す。離れる時は +5m で軽い版に。15m なら普通にコースを進んでいる間は隣が戻らない（2026-10-22 計算）")]
    public float nearCourseDist = 15f;
    private float[] _dist;
    private bool[] _near;

    // ロビーにいる間（どのコースにも入っていない時）は、コースの見た目はそのまま、遠くの毎フレーム動く仕掛け（Udon の Update）と粒を止める
    //（2026-10-18 てじゃ「ロビーが色々見えてるから重い」「ギミックとめるのはありだな」。FuwaCourseLodLobbyBuilder が集める）
    [Tooltip("ロビーの間だけ止める仕掛け（Update で動く Udon）")]
    public VRC.Udon.UdonBehaviour[] lobbyStopBehaviours;
    [Tooltip("ロビーの間だけ隠す粒（パーティクルの物）")]
    public GameObject[] lobbyHideObjects;
    [Tooltip("ロビーの間も、自分からこの距離(m)以内の仕掛け・粒は動かす（2026-10-22 てじゃ「ロビーからの見た目はほしい」→ 近くだけ動かす案）。0なら全部止める")]
    public float lobbyNearRadius = 40f;
    [Tooltip("0=きれい（全部ふつう表示・ロビーも全部動く）、1=ふつう（他コースは軽い版・ロビーは近くだけ動く）、2=かるい（他コースは軽い版・ロビーは全部止める）。ロビーとタイトルのボタン（FuwaQualitySwitch）で切り替える。PC ははじめ1（2026-10-22 てじゃ、近づいたら本体に戻るようにしたあと「ちょーどいいかも、これならデフォをふつうにしてもいいかも」）、スマホ・Quest は始めから2")]
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

    [Tooltip("きれい・ふつう・かるい のボタン（ロビーの説明ボードとタイトルの2か所）。どれかで切り替えたら全部の表示を合わせる")]
    public FuwaQualitySwitch[] switches;

    // ロビー・タイトルのボタンから呼ぶ（自分だけ）。次の Update で全部やり直す
    public void SetMode(int m)
    {
        ApplyMode(m);
        // 選んだ設定は保存して、次に来た時もその設定で始める（2026-10-22 てじゃ「保存するようにしよう」）
        if (_restored) PlayerData.SetInt(SaveKey, mode);
    }

    private void ApplyMode(int m)
    {
        mode = Mathf.Clamp(m, 0, 2);
        _shown = -2; _lobbyState = -1; _next = 0f;
        enabled = true;
        if (switches != null) foreach (FuwaQualitySwitch s in switches) if (s != null) s.Refresh();
    }

    // ===== 保存（PlayerData）=====
    // PC と Quest・スマホで同じ保存データを読むので、キーを分ける（PC で きれい にしても Quest は かるい のまま始まる）
#if UNITY_ANDROID || UNITY_IOS
    private const string SaveKey = "quality_mobile";
#else
    private const string SaveKey = "quality_pc";
#endif
    private bool _restored;
    [Tooltip("ONで選んだ設定をVRChatに保存する。ワールド側からは保存データを消せないので、完成するまではOFF")]
    public bool savePlayerData = true;

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || !savePlayerData) return;
        _restored = true;
        if (!PlayerData.HasKey(player, SaveKey)) return;   // まだ一度も選んでいない人ははじめの設定のまま
        ApplyMode(PlayerData.GetInt(player, SaveKey));
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
        bool nearChanged = UpdateNearCourses(area == soloArea, cur);
        // エリアが変わった時は、FuwaAreaVisibility がコースの本体を出し直しているので必ずやり直す
        if (cur != _shown || area != _area || nearChanged)
        {
            _shown = cur; _area = area;
            Apply(area == soloArea, cur);
        }
        if (_lobbyState == 1) UpdateLobbyNear();
    }

    // コースの中にいる時、自分の近くにある他のコースを調べる。変わったら true
    private bool UpdateNearCourses(bool inSolo, int cur)
    {
        int n = fullRoots != null ? fullRoots.Length : 0;
        if (_near == null || _near.Length != n) _near = new bool[n];
        VRC.SDKBase.VRCPlayerApi p = VRC.SDKBase.Networking.LocalPlayer;
        bool check = inSolo && cur >= 0 && mode != 0 && p != null && cellMin != null && cellMax != null && cellCourse != null;
        if (_dist == null || _dist.Length != n) _dist = new float[n];
        for (int i = 0; i < n; i++) _dist[i] = 1e9f;
        if (check)
        {
            // コースごとに、いちばん近い床の箱（のいちばん近い点）までの距離
            Vector3 me = p.GetPosition();
            int m = Mathf.Min(Mathf.Min(cellMin.Length, cellMax.Length), cellCourse.Length);
            for (int k = 0; k < m; k++)
            {
                int i = cellCourse[k]; if (i < 0 || i >= n || i == cur) continue;
                float d = (Vector3.Max(cellMin[k], Vector3.Min(cellMax[k], me)) - me).sqrMagnitude;
                if (d < _dist[i]) _dist[i] = d;
            }
        }
        float rOn = nearCourseDist * nearCourseDist, rOff = (nearCourseDist + 5f) * (nearCourseDist + 5f);
        bool changed = false;
        for (int i = 0; i < n; i++)
        {
            bool near = check && i != cur && (_near[i] ? _dist[i] < rOff : _dist[i] < rOn);
            if (near != _near[i]) { _near[i] = near; changed = true; }
        }
        return changed;
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
            bool near = _near != null && i < _near.Length && _near[i];
            bool full = inSolo && (c < 0 || c == i || near);
            bool lite = inSolo && c >= 0 && c != i && !near;
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
