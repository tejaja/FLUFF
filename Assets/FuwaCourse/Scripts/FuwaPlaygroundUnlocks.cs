using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// みんなでひろばのおもちゃの解放（2026-10-12 てじゃ「各コースをクリアフラグ立てた人がインスタンスにいると各コースのギミックが一個ずつ解放」）。
// このインスタンスで誰かがコースをクリアしたら（FuwaRecords → FuwaPlaygroundSpawner.Unlock、同期）、そのコースのおもちゃを出す。インスタンスが閉じるまで出たまま。
// 保存はしない（てじゃ「あえて保存なし、『竜巻解放してくるわ』みたいなことが起こる」）。まだの所は札（locked）を出す
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPlaygroundUnlocks : UdonSharpBehaviour
{
    public FuwaPlaygroundSpawner spawner;
    [Tooltip("コースの順（FuwaRecords.courseStarts と同じ）：そのコースがクリアされたら出すおもちゃ")]
    public GameObject[] toys;
    [Tooltip("toys と同じ順：まだの時に出す札")]
    public GameObject[] locked;
    [Tooltip("テスト用：全部出す")]
    public bool unlockAll;

    private float _next;
    private bool[] _shown = new bool[0];

    private void Start() { _next = 0f; }

    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + 1f;
        Refresh();
    }

    private void Refresh()
    {
        if (spawner == null || toys == null) return;
        int n = toys.Length;
        if (_shown.Length != n) { _shown = new bool[n]; for (int i = 0; i < n; i++) Set(i, false, true); }
        for (int i = 0; i < n; i++)
        {
            bool on = unlockAll || spawner.Unlocked(i);
            if (on != _shown[i]) Set(i, on, false);
        }
    }

    private void Set(int i, bool on, bool force)
    {
        _shown[i] = on;
        if (toys[i] != null && (force || toys[i].activeSelf != on)) toys[i].SetActive(on);
        if (locked != null && i < locked.Length && locked[i] != null) locked[i].SetActive(!on);
    }
}
