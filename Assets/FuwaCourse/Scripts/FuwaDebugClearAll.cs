using TMPro;
using UdonSharp;
using UnityEngine;

// テストプレイ用の「ぜんぶクリア」（2026-11-17 てじゃ「テストプレイ用に、デバッグスイッチのとこに全コースクリアフラグONにするボタン」）。公開前に消す。
// 自分の分だけ、全コース（ステージ1・2・チュートリアル）のクリアの印をつけて保存する。自己ベストは変えない。
// 押したあと2秒「つけたよ」と出す
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaDebugClearAll : UdonSharpBehaviour
{
    public FuwaRecords[] records;
    public TextMeshProUGUI label;
    public FuwaLanguage language;

    private float _doneAt = -100f;
    private int _shown = -1;

    // 2026-11-19 てじゃ「クリアフラグオンにしたときに、仮の記録いれてほしい。タイムは99分59秒99、最小ミス100回、クリア回数100、射撃回数1000、飛距離1000」
    //  タイムはステージ1・2のコースだけ（チュートリアルはタイムなし＝stats.records にあるものだけ）。今の記録より悪くても上書き
    [Tooltip("あしあと（仮の記録を入れる先。なくてもOK）")]
    public FuwaStats stats;
    public float fakeTime = 5999.995f;   // 99:59.99（表示は1/100秒で切り捨てなので、ちょうど 5999.99 だと 99:59.98 になることがある）
    public int fakeOuts = 100, fakeHits = 1000, fakeClears = 100, fakeTotalHits = 1000;
    public float fakeDist = 1000f;

    public void Press()
    {
        if (records != null) for (int i = 0; i < records.Length; i++) if (records[i] != null) records[i].ClearAllMine();
        if (stats != null)
        {
            if (stats.records != null) for (int i = 0; i < stats.records.Length; i++) if (stats.records[i] != null) stats.records[i].FakeBestMine(fakeTime);
            stats.FakeAll(fakeOuts, fakeHits, fakeClears, fakeTotalHits, fakeDist);
        }
        _doneAt = Time.time;
    }

    private void Update()
    {
        if (label == null) return;
        bool en = language != null && language.IsEnglish();
        int key = (Time.time - _doneAt <= 2f ? 2 : 0) + (en ? 1 : 0);
        if (key == _shown) return;
        _shown = key;
        // 文言は「クリアフラグON」（てじゃ「全部クリアだと分かりづらいから、クリアフラグON でいいよ」「押してでる表示もクリアフラグONでいいよ」）
        label.text = en ? "Clear flags ON" : "クリアフラグON";
    }
}
