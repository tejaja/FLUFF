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
    public TextMeshPro boardText;
    public FuwaLanguage language;

    [Header("自己ベストの保存（PlayerData）")]
    [Tooltip("ONで自己ベストをVRChatに保存する。ワールド側からは保存データを消せないので、完成するまではOFF")]
    public bool savePlayerData = false;
    [Tooltip("保存のキー名（courseStarts と同じ順番）。一度決めたら変えないこと（変えると記録が消えたように見える）")]
    public string[] courseKeys;
    [Tooltip("コースの版（courseStarts と同じ順番）。コースの形を変えて記録をリセットしたい時に1つ上げる")]
    public int[] courseVersions;

    [UdonSynced] private float[] _bestTimes = new float[0];
    [UdonSynced] private string[] _bestNames = new string[0];
    private float[] _myBest = new float[0];
    private bool _restored;   // 自分の保存データが届いたか（届く前に書くと上書きされるので待つ）

    private void Start()
    {
        int n = courseStarts != null ? courseStarts.Length : 0;
        if (_bestTimes == null || _bestTimes.Length != n) { _bestTimes = new float[n]; _bestNames = new string[n]; }
        for (int i = 0; i < n; i++) if (_bestNames[i] == null) _bestNames[i] = "";
        _myBest = new float[n];
        Render();
    }

    // ゴールした時にFuwaBallから呼ばれる
    public void ReportTime(Transform start, float time)
    {
        int i = IndexOf(start);
        if (i < 0 || time <= 0f) return;
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
    }

    // 自分の保存データが届いた時（ワールドに入って少ししてから）
    public override void OnPlayerRestored(VRCPlayerApi player)
    {
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
        if (boardText == null || courseStarts == null) return;
        bool en = language != null && language.IsEnglish();
        string s = "<align=center><size=150%><color=#FF6B5C>" + (en ? "Best Times" : "さいそくタイム") + "</color></size></align>\n";
        s += "<size=65%><color=#8A7A9A>" + "<pos=44%>" + (en ? "Your best" : "自己ベスト") + "<pos=71%>" + (en ? "This instance" : "このインスタンス") + "</color></size>\n";
        for (int i = 0; i < courseStarts.Length; i++)
        {
            string name = en ? Get(courseNamesEn, i) : Get(courseNamesJa, i);
            string mine = (_myBest != null && i < _myBest.Length) ? FormatTime(_myBest[i]) : "--";
            string best = (i < _bestTimes.Length) ? FormatTime(_bestTimes[i]) : "--";
            string who = (i < _bestNames.Length && _bestTimes[i] > 0f) ? _bestNames[i] : "";
            s += "<color=#FF6B5C>●</color> " + name
               + "<pos=44%><mspace=0.5em>" + mine + "</mspace>"
               + "<pos=71%><mspace=0.5em>" + best + "</mspace>";
            // 名前の行は記録がなくても空けておく（行の間隔をそろえるため）
            s += "\n<pos=71%><size=60%><color=#8A7A9A>" + (who != "" ? who : " ") + "</color></size>";
            if (i < courseStarts.Length - 1) s += "\n";
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
