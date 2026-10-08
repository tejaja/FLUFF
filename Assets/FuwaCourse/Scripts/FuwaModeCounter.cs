using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// モードの看板に「今そのモードに何人いるか」を出す（自分の画面だけ、1秒ごと）。
// 各プレイヤーの今いるエリアは、その人の分身（FuwaGhost）に同期されている。
// 看板の文字はこのスクリプトが言語に合わせて書く（FuwaLanguage の配列には入れない）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaModeCounter : UdonSharpBehaviour
{
    public FuwaAreaVisibility areaVisibility;
    public FuwaLanguage language;
    [Tooltip("人数を出す看板の文字")]
    public TextMeshPro[] labels;
    [Tooltip("各看板が数えるエリア（1=ぼうけん / 2=たいせん）")]
    public int[] labelAreas;
    [TextArea] public string[] labelsJa;
    [TextArea] public string[] labelsEn;
    [Header("UIボタン（レーザーで押す版）に人数を出す")]
    public TextMeshProUGUI[] uiLabels;
    public int[] uiLabelAreas;
    [TextArea] public string[] uiLabelsJa;
    [TextArea] public string[] uiLabelsEn;
    [Tooltip("UIボタンの人数の色（ボタンの地の色の上で読める色）")]
    public string uiCountColor = "#FFFFFFCC";
    [Tooltip("文字ごとの人数の色（空なら uiCountColor）")]
    public string[] uiCountColors;

    private float _next;

    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + 1f;

        // 自分のいるエリアを自分の分身に書く（他の人の画面で数えられるように）
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null || !local.IsValid()) return;
        FuwaGhost mine = GhostOf(local);
        if (mine != null && areaVisibility != null) mine.SetArea(areaVisibility.GetArea());

        int adventure = 0, versus = 0;
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);
        foreach (VRCPlayerApi p in players)
        {
            if (p == null || !p.IsValid()) continue;
            int area;
            if (p.isLocal && areaVisibility != null) area = areaVisibility.GetArea();
            else
            {
                FuwaGhost g = GhostOf(p);
                area = g != null ? g.GetArea() : 0;
            }
            if (area == 1) adventure++;
            else if (area == 2) versus++;
        }

        bool en = language != null && language.IsEnglish();
        if (labels != null)
        {
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] == null) continue;
                int a = (labelAreas != null && i < labelAreas.Length) ? labelAreas[i] : 1;
                string s = (en ? Get(labelsEn, i) : Get(labelsJa, i)) + "\n<size=55%><color=#FF8A7A>" + CountText(a == 2 ? versus : adventure) + "</color></size>";
                if (labels[i].text != s) labels[i].text = s;
            }
        }
        if (uiLabels != null)
        {
            for (int i = 0; i < uiLabels.Length; i++)
            {
                if (uiLabels[i] == null) continue;
                int a = (uiLabelAreas != null && i < uiLabelAreas.Length) ? uiLabelAreas[i] : 1;
                string s = (en ? Get(uiLabelsEn, i) : Get(uiLabelsJa, i)) + "  <size=70%><color=" + ((uiCountColors != null && i < uiCountColors.Length && !string.IsNullOrEmpty(uiCountColors[i])) ? uiCountColors[i] : uiCountColor) + ">" + CountText(a == 2 ? versus : adventure) + "</color></size>";
                if (uiLabels[i].text != s) uiLabels[i].text = s;
            }
        }
    }

    // 人のマーク＋人数（言語に関係なく同じ）。マークは各文字の Sprite Asset（PersonIcon）の0番を文字色で塗る
    private string CountText(int n)
    {
        return "<sprite=0 tint=1>" + n;
    }

    private string Get(string[] arr, int i)
    {
        return (arr != null && i < arr.Length && arr[i] != null) ? arr[i] : "";
    }

    private FuwaGhost GhostOf(VRCPlayerApi p)
    {
        GameObject[] objs = Networking.GetPlayerObjects(p);
        if (objs == null) return null;
        foreach (GameObject o in objs)
        {
            if (o == null) continue;
            FuwaGhost g = o.GetComponent<FuwaGhost>();
            if (g != null) return g;
        }
        return null;
    }
}
