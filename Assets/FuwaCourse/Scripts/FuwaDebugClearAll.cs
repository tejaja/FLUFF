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

    public void Press()
    {
        if (records != null) for (int i = 0; i < records.Length; i++) if (records[i] != null) records[i].ClearAllMine();
        _doneAt = Time.time;
    }

    private void Update()
    {
        if (label == null) return;
        bool en = language != null && language.IsEnglish();
        int key = (Time.time - _doneAt <= 2f ? 2 : 0) + (en ? 1 : 0);
        if (key == _shown) return;
        _shown = key;
        // 文言は「クリアフラグON」（てじゃ「全部クリアだと分かりづらいから、クリアフラグON でいいよ」）
        if (key >= 2) label.text = en ? "Flags set!" : "ONにしたよ";
        else label.text = en ? "Clear flags ON" : "クリアフラグON";
    }
}
