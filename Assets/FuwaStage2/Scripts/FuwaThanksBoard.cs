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
    // 2026-11-18 てじゃ「ステージ１のコースナンバーが1-1みたいになってない」「コースナンバーの縦列とタイムの縦列は揃えて」：
    //  列そろえ（左寄せのテキストに <pos> で列を置く）。番号に「–」がなければ numberPrefix をつける（ステージ1は "1–"）
    [Tooltip("列をそろえて書く（番号・名前・タイム）。テキストは左寄せにしておく")]
    public bool columns = false;
    public string numberPrefix = "";
    public string namePos = "15%", timePos = "67%";
    // 2026-11-19 てじゃ「最終タイム2枚のボードが英訳されてない」：英語の時はコース名を英語に（長いので少し小さく。FuwaRecords の看板と同じ）
    [Tooltip("言語の切りかえ（なければ日本語だけ）")]
    public FuwaLanguage language;
    public string titleEn = "";
    [Tooltip("英語のコース名の大きさ（長い名前が時間の列にかぶらないように）")]
    public string enNameSize = "80%";
    private int _lang = -1;
    private float _next;

    private void OnEnable() { Render(); }

    // 言語が切りかわったら書き直す（1秒ごとに見る）
    private void Update()
    {
        if (language == null || Time.time < _next) return;
        _next = Time.time + 1f;
        int l = language.IsEnglish() ? 1 : 0;
        if (l != _lang) Render();
    }

    public void Render()
    {
        if (text == null || records == null) return;
        // 見出し（title）が空なら見出しも空き行も入れない（2026-11-18 てじゃ「ステージ◯も消してもっと詰めよう」）
        bool en = language != null && language.IsEnglish();
        _lang = en ? 1 : 0;
        string tt = en && titleEn != null && titleEn.Length > 0 ? titleEn : title;
        string s = tt != null && tt.Length > 0 ? tt + "\n" : "";
        bool first = true;
        for (int r = 0; r < records.Length; r++)
        {
            FuwaRecords rec = records[r]; if (rec == null || rec.courseStarts == null) continue;
            for (int i = 0; i < rec.courseStarts.Length; i++)
            {
                string[] names = en ? rec.courseNamesEn : rec.courseNamesJa;
                string nm = (names != null && i < names.Length) ? names[i] : "";
                if (!first || s.Length > 0) s += "\n";
                first = false;
                string tm = "<mspace=0.55em>" + rec.FormatTimeOf(rec.GetMyBest(i)) + "</mspace>";
                if (!columns) { s += nm + "  " + tm; continue; }
                int sp = nm.IndexOf(' ');
                string num = sp > 0 ? nm.Substring(0, sp) : "" + (i + 1);
                string name = sp > 0 ? nm.Substring(sp + 1).Trim() : nm;
                if (num.IndexOf('–') < 0 && num.IndexOf('-') < 0) num = numberPrefix + num;
                if (en) name = "<size=" + enNameSize + ">" + name + "</size>";
                s += num + "<pos=" + namePos + ">" + name + "<pos=" + timePos + ">" + tm;
            }
        }
        text.text = s;
    }
}
