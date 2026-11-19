using UdonSharp;
using UnityEngine;
using TMPro;

// 2026-11-18 おまけ本島の隅の「あしあと」の看板。FuwaStats の自分の記録を書く（出ている間、ときどき書き直す）
// 2026-11-19 てじゃ「全部英語版もほしいな」：language が英語の時は、タイトル・見出し・コース名・下の2行を英語で（列の位置も言葉ごと）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaStatsBoard : UdonSharpBehaviour
{
    public FuwaStats stats;
    public TextMeshPro text;
    [Tooltip("言語の切りかえ（なければ日本語だけ）")]
    public FuwaLanguage language;
    public string title = "<size=125%>あしあと</size>";
    public string titleEn = "<size=125%>Footprints</size>";
    public string namePos = "13%", outPos = "52%", hitPos = "70%", clearPos = "86%";
    [Tooltip("英語の時の列の位置")]
    public string namePosEn = "13%", outPosEn = "52%", hitPosEn = "70%", clearPosEn = "86%";
    [Tooltip("板（言葉ごとに幅を変える。なくてもOK）")]
    public SpriteRenderer panel;
    [Tooltip("文字の欄の幅（日本語／英語）。板はこれ＋panelPad。0なら変えない")]
    public float textW = 0f, textWEn = 0f;
    public float panelPad = 0.4f;
    [Tooltip("text の RectTransform（Udon では text.rectTransform が使えないので）")]
    public RectTransform textRect;
    // 2026-11-19 てじゃ「行ごとに色を交互につけよう」：コースの行の帯（幅を言葉で合わせる）
    public SpriteRenderer[] stripes;
    public float stripePad = 0.08f;
    public float interval = 1f;
    private float _next;
    private int _lang = -1;

    private void OnEnable() { _next = 0f; }

    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + interval;
        Render();
    }

    public void Render()
    {
        if (text == null || stats == null) return;
        bool en = language != null && language.IsEnglish();
        if ((en ? 1 : 0) != _lang) Resize(en);
        string nP = en ? namePosEn : namePos, oP = en ? outPosEn : outPos, hP = en ? hitPosEn : hitPos, cP = en ? clearPosEn : clearPos;
        string s = (en ? titleEn : title) + "\n<size=80%>";
        if (en) s += "<pos=" + oP + ">Fewest<pos=" + hP + ">Fewest\n<pos=" + oP + ">Outs<pos=" + hP + ">Shots<pos=" + cP + ">Clears</size>";
        else s += "<pos=" + oP + ">さいしょう<pos=" + hP + ">さいしょう<pos=" + cP + ">クリア\n<pos=" + oP + ">アウト<pos=" + hP + ">打数<pos=" + cP + ">回数</size>";
        int n = stats.CourseCount();
        for (int i = 0; i < n; i++)
        {
            string nm = stats.NameIn(i, en);
            int sp = nm.IndexOf(' ');
            string num = sp > 0 ? nm.Substring(0, sp) : "" + (i + 1);
            string name = sp > 0 ? nm.Substring(sp + 1).Trim() : nm;
            if (num.IndexOf('–') < 0 && num.IndexOf('-') < 0) num = (i < 5 ? "1–" : "2–") + num;
            int o = stats.GetMinOuts(i), h = stats.GetMinHits(i), c = stats.GetClears(i);
            string os = o < 0 ? "--" : (o == 0 ? (en ? "No Miss" : "ノーミス") : o.ToString());
            string hs = h < 0 ? "--" : h.ToString();
            s += "\n" + num + "<pos=" + nP + ">" + name + "<pos=" + oP + ">" + os + "<pos=" + hP + ">" + hs + "<pos=" + cP + ">" + c;
        }
        float d = stats.GetTotalDist();
        string ds = d >= 1000f ? (d / 1000f).ToString("0.00") + " km" : Mathf.FloorToInt(d) + " m";
        if (en) s += "\n\nTimes you hit fluffs<pos=" + hP + ">" + stats.GetTotalHits() + "\nDistance fluffs traveled<pos=" + hP + ">" + ds;
        else s += "\n\nふわふわに当てた回数<pos=" + hP + ">" + stats.GetTotalHits() + "\nふわふわが進んだきょり<pos=" + hP + ">" + ds;
        text.text = s;
    }

    // 日本語と英語で文字の幅がちがうので、文字の欄と板の幅を合わせる（どちらもまん中を中心に）
    private void Resize(bool en)
    {
        _lang = en ? 1 : 0;
        float w = en ? textWEn : textW;
        if (w <= 0f) return;
        if (textRect != null) { Vector2 sd = textRect.sizeDelta; sd.x = w; textRect.sizeDelta = sd; }
        if (panel != null)
        {
            Vector3 ps = panel.transform.localScale;
            Vector2 sz = panel.size; sz.x = (w + panelPad) / ps.x; panel.size = sz;
        }
        if (stripes != null)
        {
            for (int i = 0; i < stripes.Length; i++)
            {
                SpriteRenderer sr = stripes[i]; if (sr == null) continue;
                Vector2 sz = sr.size; sz.x = (w + stripePad * 2f) / sr.transform.localScale.x; sr.size = sz;
            }
        }
    }
}
