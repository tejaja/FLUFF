using UdonSharp;
using UnityEngine;
using TMPro;

// 2026-11-18 おまけ本島の隅の「あしあと」の看板。FuwaStats の自分の記録を書く（出ている間、ときどき書き直す）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaStatsBoard : UdonSharpBehaviour
{
    public FuwaStats stats;
    public TextMeshPro text;
    public string title = "<size=125%>あしあと</size>";
    public string namePos = "13%", outPos = "52%", hitPos = "70%", clearPos = "86%";
    public float interval = 1f;
    private float _next;

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
        string s = title + "\n<size=80%><pos=" + outPos + ">さいしょう<pos=" + hitPos + ">さいしょう<pos=" + clearPos + ">クリア\n<pos=" + outPos + ">アウト<pos=" + hitPos + ">打数<pos=" + clearPos + ">回数</size>";
        int n = stats.CourseCount();
        for (int i = 0; i < n; i++)
        {
            string nm = stats.NameOf(i);
            int sp = nm.IndexOf(' ');
            string num = sp > 0 ? nm.Substring(0, sp) : "" + (i + 1);
            string name = sp > 0 ? nm.Substring(sp + 1).Trim() : nm;
            if (num.IndexOf('–') < 0 && num.IndexOf('-') < 0) num = (i < 5 ? "1–" : "2–") + num;
            int o = stats.GetMinOuts(i), h = stats.GetMinHits(i), c = stats.GetClears(i);
            string os = o < 0 ? "--" : (o == 0 ? "ノーミス" : o.ToString());
            string hs = h < 0 ? "--" : h.ToString();
            s += "\n" + num + "<pos=" + namePos + ">" + name + "<pos=" + outPos + ">" + os + "<pos=" + hitPos + ">" + hs + "<pos=" + clearPos + ">" + c;
        }
        float d = stats.GetTotalDist();
        string ds = d >= 1000f ? (d / 1000f).ToString("0.00") + " km" : Mathf.FloorToInt(d) + " m";
        s += "\n\nふわふわに当てた回数<pos=" + hitPos + ">" + stats.GetTotalHits() + "\nふわふわが進んだきょり<pos=" + hitPos + ">" + ds;
        text.text = s;
    }
}
