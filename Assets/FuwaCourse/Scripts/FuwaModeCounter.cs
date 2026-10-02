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
        if (labels == null) return;
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null) continue;
            int a = (labelAreas != null && i < labelAreas.Length) ? labelAreas[i] : 1;
            int n = a == 2 ? versus : adventure;
            string baseText = en ? Get(labelsEn, i) : Get(labelsJa, i);
            string count = en ? (n + (n == 1 ? " player" : " players") + " now") : ("いま " + n + "人");
            string s = baseText + "\n<size=55%><color=#FF8A7A>● " + count + "</color></size>";
            if (labels[i].text != s) labels[i].text = s;
        }
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
