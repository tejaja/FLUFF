using UdonSharp;
using UnityEngine;

// 2026-11-17 てじゃ「全クリしたあとになんかほしい」「2-5 のミギがあいてる、ワープして島を置く、島はクリア前は何もない、クリア後だけ各種報酬が見える」
//  ごほうびの島：自分が全コース（records の全部）をクリアしていたら showIfClear を出す（自分の画面だけ）。
//  クリアの記録は PlayerData から少し遅れて届くので、ときどき見直す（checkEvery 秒ごと）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaAllClearReveal : UdonSharpBehaviour
{
    public FuwaRecords[] records;
    [Tooltip("全クリの時だけ出す物")]
    public GameObject[] showIfClear;
    [Tooltip("全クリの時は消す物（クリア前の看板など）")]
    public GameObject[] hideIfClear;
    public float checkEvery = 2f;
    private float _next;
    private int _state = -1;

    private void Start() { Apply(IsAllClear()); }

    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + checkEvery;
        Apply(IsAllClear());
    }

    public bool IsAllClear()
    {
        if (records == null || records.Length == 0) return false;
        for (int r = 0; r < records.Length; r++)
        {
            FuwaRecords rec = records[r];
            if (rec == null || rec.courseStarts == null) return false;
            for (int i = 0; i < rec.courseStarts.Length; i++) if (!rec.HasCleared(i)) return false;
        }
        return true;
    }

    private void Apply(bool clear)
    {
        int s = clear ? 1 : 0;
        if (s == _state) return;
        _state = s;
        if (showIfClear != null) for (int i = 0; i < showIfClear.Length; i++) if (showIfClear[i] != null) showIfClear[i].SetActive(clear);
        if (hideIfClear != null) for (int i = 0; i < hideIfClear.Length; i++) if (hideIfClear[i] != null) hideIfClear[i].SetActive(!clear);
    }
}
