using UdonSharp;
using UnityEngine;
using TMPro;

// 2026-11-17 てじゃ「ゴール演出が出るけど GOAL じゃなくて Thank you for playing! ってだしながら、各コースの自己ベストタイム一覧がドンとボードに出てくる」
//  ゴールの ClearBanner の子に置く。ClearBanner が出た時（OnEnable）に、全コースの自己ベストを書く
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaThanksBoard : UdonSharpBehaviour
{
    public FuwaRecords[] records;
    public TextMeshPro text;
    public string title = "<size=120%>じこベスト</size>";

    private void OnEnable() { Render(); }

    public void Render()
    {
        if (text == null || records == null) return;
        string s = title + "\n";
        for (int r = 0; r < records.Length; r++)
        {
            FuwaRecords rec = records[r]; if (rec == null || rec.courseStarts == null) continue;
            for (int i = 0; i < rec.courseStarts.Length; i++)
            {
                string nm = (rec.courseNamesJa != null && i < rec.courseNamesJa.Length) ? rec.courseNamesJa[i] : "";
                s += "\n" + nm + "  <mspace=0.55em>" + rec.FormatTimeOf(rec.GetMyBest(i)) + "</mspace>";
            }
        }
        text.text = s;
    }
}
