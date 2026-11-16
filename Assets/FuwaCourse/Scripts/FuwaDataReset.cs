using TMPro;
using UdonSharp;
using UnityEngine;

// タイトルの下の「データリセット」（2026-11-16 てじゃ「クリアフラグと自己ベストを消す、データリセット」）。公開前に消す。
// 自分の分だけ。まちがって押しても消えないように、1回目で「もういちど押すと消えるよ」、3秒以内にもう1回で消す
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaDataReset : UdonSharpBehaviour
{
    public FuwaRecords[] records;
    public TextMeshProUGUI label;
    public FuwaLanguage language;
    public float confirmTime = 3f;

    private float _armedAt = -100f, _doneAt = -100f;
    private int _shown = -1;

    public void Press()
    {
        if (Time.time - _armedAt > confirmTime) { _armedAt = Time.time; _doneAt = -100f; return; }
        _armedAt = -100f;
        if (records != null) for (int i = 0; i < records.Length; i++) if (records[i] != null) records[i].ResetMine();
        _doneAt = Time.time;
    }

    private void Update()
    {
        if (label == null) return;
        bool en = language != null && language.IsEnglish();
        int state = Time.time - _armedAt <= confirmTime ? 1 : Time.time - _doneAt <= 2f ? 2 : 0;
        int key = state * 2 + (en ? 1 : 0);
        if (key == _shown) return;
        _shown = key;
        if (state == 1) label.text = en ? "Press again to erase" : "もういちど押すと消えるよ";
        else if (state == 2) label.text = en ? "Data erased" : "消したよ";
        else label.text = en ? "Reset Data" : "データリセット";
    }
}
