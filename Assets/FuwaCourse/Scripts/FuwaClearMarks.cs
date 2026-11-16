using UdonSharp;
using UnityEngine;

// ロビーのコースへのポータルのまわりのミニキノコ（2026-11-16 てじゃ「クリアフラグの印もほしいな」「各コースのロビーのポータル、クリア済みならまわりにミニキノコ生やしてみよ」「道のところは無し」「ローカルね」）
// 自分がそのコースをクリアしたことがあれば（FuwaRecords.HasCleared、保存されたクリアの印）、marks[i] を出す。自分の画面だけ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaClearMarks : UdonSharpBehaviour
{
    public FuwaRecords[] records;
    public int[] courseIndex;
    public GameObject[] marks;
    public float interval = 1f;

    private float _next;

    private void Start() { for (int i = 0; i < marks.Length; i++) if (marks[i] != null) marks[i].SetActive(false); }

    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + interval;
        if (marks == null || records == null || courseIndex == null) return;
        int n = Mathf.Min(marks.Length, Mathf.Min(records.Length, courseIndex.Length));
        for (int i = 0; i < n; i++)
        {
            if (marks[i] == null || records[i] == null) continue;
            bool on = records[i].HasCleared(courseIndex[i]);
            if (marks[i].activeSelf != on) marks[i].SetActive(on);
        }
    }
}
