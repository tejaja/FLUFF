using UdonSharp;
using UnityEngine;

// ひとりでモードで、自分がいるコース以外のコースを「床だけの軽い版」に切り替える（自分の画面だけ）。
// ・どのコースにいるかは、ふわふわのスタート地点（ball.startPoint）が starts[i] のどれかで決める（BGM と同じ判定）
// ・ロビーにいる間（どれにも当てはまらない）は全部ふつうに表示
// ・他の人のアバターは消せないので、軽い版で床だけは見せて「空中に浮いて見える」のを防ぐ
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

    private float _next;
    private int _shown = -2;   // いま反映している状態（-1=全部ふつう、i=コースiにいる）
    private int _area = -1;

    private void Update()
    {
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
        if (cur == _shown && area == _area) return;
        _shown = cur; _area = area;
        Apply(area == soloArea, cur);
    }

    private void Apply(bool inSolo, int cur)
    {
        int n = fullRoots != null ? fullRoots.Length : 0;
        for (int i = 0; i < n; i++)
        {
            bool full = inSolo && (cur < 0 || cur == i);
            bool lite = inSolo && cur >= 0 && cur != i;
            if (fullRoots[i] != null && inSolo && fullRoots[i].activeSelf != full) fullRoots[i].SetActive(full);
            if (liteRoots != null && i < liteRoots.Length && liteRoots[i] != null && liteRoots[i].activeSelf != lite) liteRoots[i].SetActive(lite);
        }
    }
}
