using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Persistence;
using VRC.SDKBase;

// 最速タイムの看板。
// ・自己ベスト：VRChatに保存（PlayerData）。次に来た時も残る（自分にだけ見える）
// ・このインスタンスの最高記録：全員に同期（タイムと名前）。インスタンスが閉じたら消える
//
// 自己ベストの保存：キーはコースごとに固定（best_<courseKey>）で、同じキーに上書きするので古いデータは溜まらない。
// 一緒に「コースの版」（bestv_<courseKey>）も保存しておき、読み込んだ時に今の版と違えば記録なし扱い。
// → コースの形を変えて記録をリセットしたい時は、courseVersions のそのコースの数字を1つ上げる。
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaRecords : UdonSharpBehaviour
{
    [Tooltip("各コースのスタート（FuwaBallのstartPointと同じもの）。並び順＝看板の並び順")]
    public Transform[] courseStarts;
    public string[] courseNamesJa;
    public string[] courseNamesEn;
    // 列の位置（右の余白を詰めるため右へ。2026-10-12 てじゃ「右の余白埋めてもいいかも」）
    private const string ColA = "49%";
    private const string ColB = "75%";
    private const string NameGap = "70%";   // タイムの行から名前の行まで（せまく）
    private const string RowGap = "135%";   // 名前の行から次のコースまで（広く）
    public TextMeshPro boardText;
    public FuwaLanguage language;

    [Header("自己ベストの保存（PlayerData）")]
    [Tooltip("ONで自己ベストをVRChatに保存する。ワールド側からは保存データを消せないので、完成するまではOFF")]
    public bool savePlayerData = false;
    [Tooltip("保存のキー名（courseStarts と同じ順番）。一度決めたら変えないこと（変えると記録が消えたように見える）")]
    public string[] courseKeys;
    [Tooltip("コースの版（courseStarts と同じ順番）。コースの形を変えて記録をリセットしたい時に1つ上げる")]
    public int[] courseVersions;

    [Tooltip("みんなでひろば：コースをクリアしたら、このインスタンスでそのコースのおもちゃを解放する（保存はしない）")]
    [UnityEngine.Serialization.FormerlySerializedAs("chill")] public FuwaPlaygroundSpawner playground;
    [Tooltip("2026-11-16 あそびばのおもちゃの番号のずらし（ステージ2の記録は 5：コース2–1 のクリアで 6 番目のおもちゃ）")]
    public int playgroundOffset = 0;

    [Tooltip("もう1枚の看板（ステージ2のロビーの看板など）。ゴールのタイム・言語の切り替えをそのまま回す（FuwaBall・FuwaLanguage は1枚目だけ知っていればいい）")]
    public FuwaRecords next;

    [UdonSynced] private float[] _bestTimes = new float[0];
    [UdonSynced] private string[] _bestNames = new string[0];
    private float[] _myBest = new float[0];
    // 2026-11-16 てじゃ「クリアフラグを保存するようにして、クリアフラグ持ちはおもちゃ予定地のドームをUSEすると解放できる」
    //  クリアしたか（自己ベストとは別に保存。コースの版が変わっても消えない）。あそびばのドーム（FuwaPlaygroundUnlockDome）が HasCleared で見る
    [Tooltip("クリアしたかを PlayerData に保存する（clear_キー）。自己ベストの保存（savePlayerData）とは別")]
    public bool saveClears = true;
    [Tooltip("ONなら前と同じく、クリアした瞬間にこのインスタンスであそびばのおもちゃを出す（OFF：ドームをさわって出す）")]
    public bool autoUnlockPlayground = false;
    private bool[] _cleared = new bool[0];
    private bool _clearRestored;
    private bool _restored;   // 自分の保存データが届いたか（届く前に書くと上書きされるので待つ）

    private bool _inited;

    private void Start()
    {
        Init();
        Render();
    }

    // Start より先に ReportTime / OnPlayerRestored が来ても配列の外を読まないように
    private void Init()
    {
        if (_inited) return;
        _inited = true;
        int n = courseStarts != null ? courseStarts.Length : 0;
        if (_bestTimes == null || _bestTimes.Length != n) { _bestTimes = new float[n]; _bestNames = new string[n]; }
        for (int i = 0; i < n; i++) if (_bestNames[i] == null) _bestNames[i] = "";
        _myBest = new float[n];
        _cleared = new bool[n];
    }

    // ゴールした時にFuwaBallから呼ばれる
    public void ReportTime(Transform start, float time)
    {
        if (next != null) next.ReportTime(start, time);
        Init();
        int i = IndexOf(start);
        if (i < 0 || time <= 0f) return;
        if (autoUnlockPlayground && playground != null) playground.Unlock(i + playgroundOffset);
        _cleared[i] = true; SaveClear(i);
        // 自己ベスト
        if (_myBest[i] <= 0f || time < _myBest[i])
        {
            _myBest[i] = time;
            SaveBest(i);
        }
        // インスタンスの最高記録
        if (_bestTimes[i] <= 0f || time < _bestTimes[i])
        {
            VRCPlayerApi local = Networking.LocalPlayer;
            if (!Networking.IsOwner(gameObject)) Networking.SetOwner(local, gameObject);
            _bestTimes[i] = time;
            _bestNames[i] = local != null ? local.displayName : "";
            RequestSerialization();
        }
        Render();
    }

    public override void OnDeserialization()
    {
        Render();
        // 届いたインスタンスの記録より自分の自己ベストの方が速いコースがあれば、少ししてから書きこむ（入った時・ほかの人と同時に書いて上書きされた時）
        if (_restored && MineIsBetter()) SendCustomEventDelayedSeconds(nameof(PushMyBest), Random.Range(1f, 3f));
    }

    // 2026-11-19 てじゃ「インスタンス最高記録、入ってきた人の最高記録呼んで更新してもいいかも」：
    //  自分の自己ベスト（保存）がインスタンスの最高記録より速いコースは、自分の名前で最高記録にする
    private bool MineIsBetter()
    {
        if (_myBest == null || _bestTimes == null) return false;
        int n = Mathf.Min(_myBest.Length, _bestTimes.Length);
        for (int i = 0; i < n; i++) if (_myBest[i] > 0f && (_bestTimes[i] <= 0f || _myBest[i] < _bestTimes[i] - 0.0005f)) return true;
        return false;
    }

    public void PushMyBest()
    {
        Init();
        if (!MineIsBetter()) return;
        VRCPlayerApi local = Networking.LocalPlayer; if (local == null) return;
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(local, gameObject);
        int n = Mathf.Min(_myBest.Length, _bestTimes.Length);
        for (int i = 0; i < n; i++)
            if (_myBest[i] > 0f && (_bestTimes[i] <= 0f || _myBest[i] < _bestTimes[i] - 0.0005f)) { _bestTimes[i] = _myBest[i]; _bestNames[i] = local.displayName; }
        RequestSerialization();
        Render();
    }

    // 自分の保存データが届いた時（ワールドに入って少ししてから）
    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        Init();
        if (player != null && player.isLocal && saveClears)
        {
            _clearRestored = true;
            int nc = courseStarts != null ? courseStarts.Length : 0;
            for (int i = 0; i < nc; i++)
            {
                string key = Key(i); if (key == "") continue;
                if (PlayerData.HasKey(player, "clear_" + key) && PlayerData.GetBool(player, "clear_" + key)) _cleared[i] = true;
            }
            for (int i = 0; i < nc; i++) if (_cleared[i]) SaveClear(i);   // 届く前にクリアした分も保存
        }
        if (player == null || !player.isLocal || !savePlayerData) return;
        _restored = true;
        int n = courseStarts != null ? courseStarts.Length : 0;
        for (int i = 0; i < n; i++)
        {
            string key = Key(i);
            if (key == "") continue;
            if (!PlayerData.HasKey(player, "best_" + key) || !PlayerData.HasKey(player, "bestv_" + key)) continue;
            if (PlayerData.GetInt(player, "bestv_" + key) != Version(i)) continue;   // 昔の版のコースの記録は使わない
            float t = PlayerData.GetFloat(player, "best_" + key);
            // この間にもう記録を出していたら、速い方
            if (t > 0f && (_myBest[i] <= 0f || t < _myBest[i])) _myBest[i] = t;
        }
        // 保存が届く前に出した記録も、ここで保存しておく
        for (int i = 0; i < n; i++) if (_myBest[i] > 0f) SaveBest(i);
        Render();
        // インスタンスの最高記録に反映（みんなの記録が届くのを少し待ってから）
        SendCustomEventDelayedSeconds(nameof(PushMyBest), Random.Range(3f, 5f));
    }

    private void SaveClear(int i)
    {
        if (!saveClears || !_clearRestored) return;
        string key = Key(i);
        if (key == "") return;
        PlayerData.SetBool("clear_" + key, true);
    }

    // 2026-11-16 てじゃ「正式版にするまでのあいだ タイトルの下に…クリアフラグと自己ベストを消す、データリセット」：自分の分だけ消す（みんなのこのインスタンスの記録は残す）
    public void ResetMine()
    {
        Init();
        VRCPlayerApi lp = Networking.LocalPlayer;
        for (int i = 0; i < _cleared.Length; i++)
        {
            _cleared[i] = false;
            _myBest[i] = 0f;
            string key = Key(i);
            if (key == "" || lp == null || !_clearRestored) continue;
            if (PlayerData.HasKey(lp, "clear_" + key)) PlayerData.SetBool("clear_" + key, false);
            if (PlayerData.HasKey(lp, "best_" + key)) PlayerData.SetFloat("best_" + key, 0f);
        }
        Render();
    }

    // タイムを計らないコース（チュートリアル）のゴール：クリアの印だけつける（2026-11-17 てじゃ「チュートリアルもクリアフラグつけて」）。FuwaBall から
    public void ReportClear(Transform start)
    {
        if (next != null) next.ReportClear(start);
        Init();
        int i = IndexOf(start);
        if (i < 0) return;
        if (autoUnlockPlayground && playground != null) playground.Unlock(i + playgroundOffset);
        _cleared[i] = true; SaveClear(i);
    }

    // テストプレイ用（2026-11-17 てじゃ「デバッグスイッチのとこに全コースクリアフラグONにするボタン」）。公開前に消す。
    // 自分の全コースのクリアの印をつけて保存する（自己ベストはそのまま）
    public void ClearAllMine()
    {
        Init();
        for (int i = 0; i < _cleared.Length; i++)
        {
            if (Key(i) == "") continue;
            _cleared[i] = true; SaveClear(i);
        }
        Render();
    }

    // あそびばのドーム用：自分がこのコースをクリアしたことがあるか（保存したクリアの印、またはこの回の記録）
    // 2026-11-17 おまけの島の「Thank you for playing!」の自己ベスト一覧用
    public float GetMyBest(int i)
    {
        Init();
        if (i < 0 || i >= _myBest.Length) return 0f;
        return _myBest[i];
    }
    public string FormatTimeOf(float t) { return FormatTime(t); }

    public bool HasCleared(int i)
    {
        Init();
        if (i < 0 || i >= _cleared.Length) return false;
        return _cleared[i] || _myBest[i] > 0f;
    }

    private void SaveBest(int i)
    {
        if (!_restored) return;
        string key = Key(i);
        if (key == "" || _myBest[i] <= 0f) return;
        PlayerData.SetFloat("best_" + key, _myBest[i]);
        PlayerData.SetInt("bestv_" + key, Version(i));
    }

    // 保存データの容量（1人1ワールド約100KB）を超えそう／超えた時。今の作りは10個ほどのキーを上書きするだけなので
    // まず起きないが、起きたら気づけるようにログだけ出す（超えると新しい書き込みが保存されない）
    public override void OnPlayerDataStorageWarning(VRCPlayerApi player)
    {
        if (player != null && player.isLocal) Debug.LogWarning("[FuwaRecords] PlayerData storage is almost full");
    }

    public override void OnPlayerDataStorageExceeded(VRCPlayerApi player)
    {
        if (player != null && player.isLocal) Debug.LogError("[FuwaRecords] PlayerData storage exceeded: new saves are not stored");
    }

    private string Key(int i)
    {
        return (courseKeys != null && i < courseKeys.Length && courseKeys[i] != null) ? courseKeys[i] : "";
    }

    private int Version(int i)
    {
        return (courseVersions != null && i < courseVersions.Length) ? courseVersions[i] : 1;
    }

    // 言語が切り替わった時にFuwaLanguageから呼ばれる
    public void Render()
    {
        if (next != null) next.Render();
        if (boardText == null || courseStarts == null) return;
        bool en = language != null && language.IsEnglish();
        string s = "<align=center><size=150%><color=#FF6B5C>" + (en ? "Best Times" : "さいそくタイム") + "</color></size></align>\n";
        s += "<size=56%><color=#8A7A9A>" + "<pos=" + ColA + ">" + (en ? "Your best" : "自己ベスト") + "<pos=" + ColB + ">" + (en ? "This instance" : "このインスタンス") + "</color></size>\n";
        for (int i = 0; i < courseStarts.Length; i++)
        {
            string name = en ? Get(courseNamesEn, i) : Get(courseNamesJa, i);
            string mine = (_myBest != null && i < _myBest.Length) ? FormatTime(_myBest[i]) : "--";
            string best = (i < _bestTimes.Length) ? FormatTime(_bestTimes[i]) : "--";
            string who = (i < _bestNames.Length && _bestTimes[i] > 0f) ? _bestNames[i] : "";
            // 英語のコース名は長い（Mushroom Valley が自己ベストの列にかぶった）ので少し小さく（2026-10-12 てじゃ）
            if (en) name = "<size=80%>" + name + "</size>";
            s += "<color=#FF6B5C>●</color> " + name
               + "<pos=" + ColA + "><mspace=0.5em>" + mine + "</mspace>"
               + "<pos=" + ColB + "><mspace=0.5em>" + best + "</mspace>";
            // 名前の行は記録がなくても空けておく（行の間隔をそろえるため）。
            // 名前はすぐ上のタイムにくっつけて、コースの間はすき間をあける（どっちのタイムの名前か分かりやすく。2026-10-12 てじゃ）
            s += "<line-height=" + NameGap + ">\n<pos=" + ColB + "><size=60%><color=#8A7A9A>" + (who != "" ? who : " ") + "</color></size></line-height>";
            if (i < courseStarts.Length - 1) s += "<line-height=" + RowGap + ">\n</line-height>";
        }
        boardText.text = s;
    }

    private int IndexOf(Transform start)
    {
        if (courseStarts == null || start == null) return -1;
        for (int i = 0; i < courseStarts.Length; i++) if (courseStarts[i] == start) return i;
        return -1;
    }

    private string Get(string[] arr, int i)
    {
        return (arr != null && i < arr.Length && arr[i] != null) ? arr[i] : "";
    }

    private string FormatTime(float t)
    {
        if (t <= 0f) return "--:--.--";
        // タイマーと同じ：99:59.99 で止める・1/100秒の整数にしてから分ける
        int cs = Mathf.Clamp(Mathf.FloorToInt(t * 100f), 0, 599999);
        return (cs / 6000).ToString() + ":" + ((cs / 100) % 60).ToString("00") + "." + (cs % 100).ToString("00");
    }
}
