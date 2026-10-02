using UdonSharp;
using UnityEngine;

// 自分が今いるエリア（0=入口 / 1=ひとりモード / 2=たいせんモード / 3=デバッグ）だけを表示する（自分の画面だけ）。
// 他のエリアのオブジェクトは丸ごと非表示にするので、同期するもの（レース・最速タイム・分身など）は
// ここで消すものの中に置かないこと（FuwaSystem に置く）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaAreaVisibility : UdonSharpBehaviour
{
    public GameObject[] hubObjects;
    public GameObject[] soloObjects;
    public GameObject[] versusObjects;
    [Tooltip("デバッグエリア（ギミックを並べたテスト用。完成したら消す）")]
    public GameObject[] debugObjects;
    [Tooltip("ワールドに入った時のエリア（スポーン地点＝入口）")]
    public int startArea = 0;

    private int _area = -1;

    private void Start()
    {
        ShowArea(startArea);
    }

    public int GetArea() { return _area; }

    public void ShowArea(int area)
    {
        if (area < 0 || area == _area) return;
        _area = area;
        SetAll(hubObjects, area == 0);
        SetAll(soloObjects, area == 1);
        SetAll(versusObjects, area == 2);
        SetAll(debugObjects, area == 3);
    }

    private void SetAll(GameObject[] objs, bool on)
    {
        if (objs == null) return;
        foreach (GameObject g in objs)
        {
            if (g != null && g.activeSelf != on) g.SetActive(on);
        }
    }
}
