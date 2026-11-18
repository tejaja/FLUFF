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
    [UnityEngine.Serialization.FormerlySerializedAs("versusObjects")] public GameObject[] playgroundObjects;
    [Tooltip("デバッグエリア（ギミックを並べたテスト用。完成したら消す）")]
    public GameObject[] debugObjects;
    [Tooltip("ワールドに入った時のエリア（スポーン地点＝入口）")]
    public int startArea = 0;
    [Tooltip("ステージ2（つぎのしま、ステージ1の真下）。ひとりでモード(1)の続きで、ひとりでの間は今いるステージだけ表示する（soloObjects がステージ1）。2026-10-17")]
    public GameObject[] stage2Objects;
    [Tooltip("おまけ本島（記念の島、area 4）。おまけのゴールのワープから。2026-11-18")]
    public GameObject[] bonusObjects;
    [Tooltip("ステージ1（ひとりで）とおまけ本島の両方で出す、遠くのけしき（雲・遠くの浮島・軽い版の入れ物）。2026-11-18 てじゃ「（おまけ本島から）遠景みえててほしいな」")]
    public GameObject[] bonusFarObjects;
    [Tooltip("おまけ本島にいる時だけ出す物（ステージ1の各コースの軽い版。ふだんは FuwaCourseLod が出し入れする）")]
    public GameObject[] bonusLites;

    private int _area = -1;
    private int _stage = 1;

    private void Start()
    {
        ShowArea(startArea);
    }

    public int GetArea() { return _area; }
    public int GetStage() { return _stage; }

    public void ShowArea(int area)
    {
        if (area < 0 || area == _area) return;
        _area = area;
        Refresh();
    }

    // ひとりでモードの中のステージ（1＝ステージ1、2＝つぎのしま）。ワープ（FuwaCourseWarp.stage）から
    public void ShowStage(int stage)
    {
        if (stage <= 0 || stage == _stage) return;
        _stage = stage;
        if (_area >= 0) Refresh();
    }

    private void Refresh()
    {
        SetAll(hubObjects, _area == 0);
        SetAll(soloObjects, _area == 1 && _stage == 1);
        SetAll(stage2Objects, _area == 1 && _stage == 2);
        SetAll(playgroundObjects, _area == 2);
        SetAll(debugObjects, _area == 3);
        SetAll(bonusObjects, _area == 4);
        // 両方で出す物は最後に（soloObjects で消されても出し直す）
        SetAll(bonusFarObjects, (_area == 1 && _stage == 1) || _area == 4);
        SetAll(bonusLites, _area == 4);
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
