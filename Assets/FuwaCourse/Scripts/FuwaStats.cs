using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Persistence;

// 2026-11-18 てじゃ「おまけ本島の隅っこに、各コースの隠し記録的な感じで閲覧する場所つくろ」：自分だけの記録（同期しない、PlayerData に保存）
//  コースごと：さいしょうアウト（タイム開始〜ゴールのアウト回数のいちばん少ない記録）／さいしょう打数（ふわふわに当てた回数、スタートの1打もふくむ）／クリア回数
//  ぜんぶ：ふわふわに当てた回数の累計／ふわふわが進んだ距離の累計
//  距離はコースに挑戦中（タイムが動いている間）だけ、しばらくごとの「横にまっすぐ進んだ分」を足す
//  （てじゃ「竜巻とかで稼ぐのも防ぎたいな」：同じ所をぐるぐる・上下するだけでは増えない。ワープなど大きく飛んだ分は足さない）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaStats : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("コースの並び（courseStarts / courseKeys / courseNamesJa）を借りる。ステージ1 → 2 の順")]
    public FuwaRecords[] records;
    [Tooltip("PlayerData に保存する（公開前にオンにする。FuwaRecords.savePlayerData と同じ時に）")]
    public bool savePlayerData = false;
    [Tooltip("距離を測る間隔(秒)。この間に横にまっすぐ進んだ分だけ足す")]
    public float distInterval = 2f;
    [Tooltip("1回の間隔でこれより大きく動いたら（ワープ・リスポーン）足さない(m)")]
    public float distMaxStep = 40f;
    // 2026-11-19 てじゃ「あるていどでカンストにしておきたい」：ここで止まる（表示もそのまま）
    [Tooltip("さいしょうアウト・さいしょう打数・クリア回数（コースごと）のカンスト")]
    public int maxCount = 9999;
    [Tooltip("ふわふわに当てた回数（ぜんぶ）のカンスト")]
    public int maxTotalHits = 999999;
    [Tooltip("進んだきょり（ぜんぶ）のカンスト(m)。9999.99 km")]
    public float maxDist = 9999990f;

    private int[] _minOuts, _minHits, _clears;
    private int _totalHits;
    private float _totalDist;
    private bool _dirty;
    private float _nextSave, _nextDist;
    private Vector3 _lastPos;
    private bool _hasLast;

    private void Start() { Init(); }

    private void Init()
    {
        int n = CourseCount();
        if (_minOuts != null && _minOuts.Length == n) return;
        _minOuts = new int[n]; _minHits = new int[n]; _clears = new int[n];
        for (int i = 0; i < n; i++) { _minOuts[i] = -1; _minHits[i] = -1; }
    }

    public int CourseCount()
    {
        int n = 0;
        if (records != null) for (int r = 0; r < records.Length; r++) if (records[r] != null && records[r].courseStarts != null) n += records[r].courseStarts.Length;
        return n;
    }

    // 全体の通し番号 → そのコースの PlayerData のキー
    public string KeyOf(int idx)
    {
        int k = idx;
        for (int r = 0; r < records.Length; r++)
        {
            FuwaRecords rec = records[r]; if (rec == null || rec.courseStarts == null) continue;
            if (k < rec.courseStarts.Length) return (rec.courseKeys != null && k < rec.courseKeys.Length) ? rec.courseKeys[k] : ("c" + idx);
            k -= rec.courseStarts.Length;
        }
        return "c" + idx;
    }

    public string NameOf(int idx) { return NameIn(idx, false); }

    // 2026-11-19 てじゃ「全部英語版もほしいな」：en＝英語のコース名（courseNamesEn）
    public string NameIn(int idx, bool en)
    {
        int k = idx;
        for (int r = 0; r < records.Length; r++)
        {
            FuwaRecords rec = records[r]; if (rec == null || rec.courseStarts == null) continue;
            string[] names = en ? rec.courseNamesEn : rec.courseNamesJa;
            if (k < rec.courseStarts.Length) return (names != null && k < names.Length) ? names[k] : "";
            k -= rec.courseStarts.Length;
        }
        return "";
    }

    private int IndexOf(Transform start)
    {
        int b = 0;
        for (int r = 0; r < records.Length; r++)
        {
            FuwaRecords rec = records[r]; if (rec == null || rec.courseStarts == null) continue;
            for (int i = 0; i < rec.courseStarts.Length; i++) if (rec.courseStarts[i] == start) return b + i;
            b += rec.courseStarts.Length;
        }
        return -1;
    }

    // ゴールした時（FuwaBall.ReachGoal、タイムのあるコースだけ）
    public void ReportRun(Transform start, int outs, int hits)
    {
        Init();
        int i = IndexOf(start); if (i < 0) return;
        if (_clears[i] < maxCount) _clears[i]++;
        outs = Mathf.Min(outs, maxCount); hits = Mathf.Min(hits, maxCount);
        if (_minOuts[i] < 0 || outs < _minOuts[i]) _minOuts[i] = outs;
        if (_minHits[i] < 0 || hits < _minHits[i]) _minHits[i] = hits;
        SaveCourse(i);
        SaveTotals();
    }

    // 銃の風がふわふわに当たった時（FuwaBall.NoteShotCharge）
    public void AddHit() { if (_totalHits >= maxTotalHits) return; _totalHits++; _dirty = true; }

    // テストプレイ用（2026-11-19 てじゃ「クリアフラグオンにしたときに、仮の記録いれてほしい」）。公開前に消す。全コース・累計をこの値で上書きして保存
    public void FakeAll(int outs, int hits, int clears, int totalHits, float totalDist)
    {
        Init();
        for (int i = 0; i < _minOuts.Length; i++)
        {
            _minOuts[i] = outs; _minHits[i] = hits; _clears[i] = clears;
        }
        _totalHits = totalHits; _totalDist = totalDist;
        ClampAll();
        for (int i = 0; i < _minOuts.Length; i++) SaveCourse(i);
        SaveTotals();
    }

    public int GetMinOuts(int i) { Init(); return (i >= 0 && i < _minOuts.Length) ? _minOuts[i] : -1; }
    public int GetMinHits(int i) { Init(); return (i >= 0 && i < _minHits.Length) ? _minHits[i] : -1; }
    public int GetClears(int i) { Init(); return (i >= 0 && i < _clears.Length) ? _clears[i] : 0; }
    public int GetTotalHits() { return _totalHits; }
    public float GetTotalDist() { return _totalDist; }

    private void Update()
    {
        float now = Time.time;
        if (now >= _nextDist)
        {
            _nextDist = now + distInterval;
            bool on = ball != null && ball.IsRunning() && !ball.IsHidden();
            if (on)
            {
                Vector3 p = ball.transform.position;
                if (_hasLast)
                {
                    Vector3 d = p - _lastPos; d.y = 0f;   // 横に進んだ分だけ（竜巻で上下・ぐるぐるしても増えない）
                    float m = d.magnitude;
                    if (m < distMaxStep && _totalDist < maxDist) { _totalDist = Mathf.Min(_totalDist + m, maxDist); _dirty = true; }
                }
                _lastPos = p; _hasLast = true;
            }
            else _hasLast = false;
        }
        if (_dirty && now >= _nextSave) { _nextSave = now + 15f; SaveTotals(); }
    }

    private void SaveCourse(int i)
    {
        if (!savePlayerData) return;
        string key = KeyOf(i);
        PlayerData.SetInt("st_out_" + key, _minOuts[i]);
        PlayerData.SetInt("st_hit_" + key, _minHits[i]);
        PlayerData.SetInt("st_clr_" + key, _clears[i]);
    }

    private void SaveTotals()
    {
        _dirty = false;
        if (!savePlayerData) return;
        PlayerData.SetInt("st_hits", _totalHits);
        PlayerData.SetFloat("st_dist", _totalDist);
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || !savePlayerData) return;
        Init();
        for (int i = 0; i < _minOuts.Length; i++)
        {
            string key = KeyOf(i);
            if (PlayerData.HasKey(player, "st_out_" + key)) _minOuts[i] = PlayerData.GetInt(player, "st_out_" + key);
            if (PlayerData.HasKey(player, "st_hit_" + key)) _minHits[i] = PlayerData.GetInt(player, "st_hit_" + key);
            if (PlayerData.HasKey(player, "st_clr_" + key)) _clears[i] = PlayerData.GetInt(player, "st_clr_" + key);
        }
        if (PlayerData.HasKey(player, "st_hits")) _totalHits = PlayerData.GetInt(player, "st_hits");
        if (PlayerData.HasKey(player, "st_dist")) _totalDist = PlayerData.GetFloat(player, "st_dist");
        ClampAll();
    }

    // カンストより大きい値（前に保存した物・テストの仮の記録）をそろえる
    private void ClampAll()
    {
        for (int i = 0; i < _minOuts.Length; i++)
        {
            if (_minOuts[i] > maxCount) _minOuts[i] = maxCount;
            if (_minHits[i] > maxCount) _minHits[i] = maxCount;
            if (_clears[i] > maxCount) _clears[i] = maxCount;
        }
        if (_totalHits > maxTotalHits) _totalHits = maxTotalHits;
        if (_totalDist > maxDist) _totalDist = maxDist;
    }
}
