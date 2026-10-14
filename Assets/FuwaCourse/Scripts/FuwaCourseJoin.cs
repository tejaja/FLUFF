using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// ワープを使わずに（VRChatで飛んで）別のコースやロビーに降りた時、そこに入った扱いにする（2026-10-14 てじゃ「ロビーから飛んでコースに入った時、いいかんじにコースに入った扱いに」）。自分の画面だけ
// ・足元の床がどのコースの物か（床のいちばん上の親）で調べる。飛んで通り過ぎるだけなら反応しないよう、地面に立って dwell 秒たったら
// ・切り替えはそのコースのワープ（ロビーのポータル）と同じ：BGM・軽い版・リスポーン先・ロビーに戻った時の向き。プレイヤーは動かさない
// ・ふわふわはそのコースのスタートから（タイムも計る。2026-10-14 てじゃ「どこに立ってもスタートからやらないとだめ」）
// ・ひとりでモード（areaVisibility の soloArea）の時だけ
// ・エリア（入口・ひとりで・あそびば）の間を飛んでいった時は、着く前に（areaMin〜areaMax に入ったら）そのエリアに切り替えて見えるようにする
//  （2026-10-14 てじゃ「強引に飛んでいった時、近づいたら切り替わって見えるように」）。切り替えはそのエリアへのワープと同じ（プレイヤーは動かさない）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaCourseJoin : UdonSharpBehaviour
{
    public FuwaBall ball;
    public FuwaAreaVisibility areaVisibility;
    public int soloArea = 1;
    [Tooltip("各コース（とロビー）の床のいちばん上の親")]
    public Transform[] roots;
    [Tooltip("roots と同じ順：軽い版のいちばん上の親（なくてもOK）")]
    public Transform[] liteRoots;
    [Tooltip("roots と同じ順：そこへ入るワープ（ロビーならコースからロビーへのワープ）")]
    public FuwaCourseWarp[] warps;
    [Tooltip("地面に立ってこの秒数たったら入った扱い")]
    public float dwell = 1f;
    public float interval = 0.25f;

    [Header("エリアの間を飛んだ時")]
    [Tooltip("各エリアの範囲（少し広げてある）の小さい角")]
    public Vector3[] areaMin;
    [Tooltip("各エリアの範囲の大きい角")]
    public Vector3[] areaMax;
    [Tooltip("areaMin と同じ順：FuwaAreaVisibility のエリア番号")]
    public int[] areaIds;
    [Tooltip("areaMin と同じ順：そのエリアへ行くワープ")]
    public FuwaCourseWarp[] areaWarps;
    [Tooltip("範囲に入ってこの秒数たったら切り替える")]
    public float areaDwell = 0.5f;
    private int _areaCand = -1;
    private float _areaCandSince;

    private float _next;
    private int _cand = -1;
    private float _candSince;

    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + interval;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null || ball == null || roots == null) return;
        if (CheckArea(lp.GetPosition())) { _cand = -1; return; }
        if (areaVisibility != null && areaVisibility.GetArea() != soloArea) { _cand = -1; return; }
        int c = -1;
        if (lp.IsPlayerGrounded())
        {
            RaycastHit hit;
            Vector3 pos = lp.GetPosition();
            int mask = ~((1 << 9) | (1 << 10) | (1 << 18));   // Player / PlayerLocal / MirrorReflection
            if (Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out hit, 1.5f, mask, QueryTriggerInteraction.Ignore) && hit.collider != null)
            {
                Transform t = hit.collider.transform;
                Transform r = t.root;
                for (int i = 0; i < roots.Length; i++)
                    if (r == roots[i] || (liteRoots != null && i < liteRoots.Length && liteRoots[i] != null && t.IsChildOf(liteRoots[i]))) { c = i; break; }
            }
        }
        if (c < 0 || warps[c] == null || warps[c].courseStart == ball.startPoint) { _cand = -1; return; }
        if (c != _cand) { _cand = c; _candSince = Time.time; return; }
        if (Time.time - _candSince < dwell) return;
        _cand = -1;
        warps[c].JoinWithoutWarp();
    }

    // 今と違うエリアの範囲に入っていたら切り替える。切り替えたら true
    private bool CheckArea(Vector3 p)
    {
        if (areaVisibility == null || areaMin == null) return false;
        int cur = areaVisibility.GetArea();
        int k = -1;
        for (int i = 0; i < areaMin.Length; i++)
        {
            Vector3 a = areaMin[i], b = areaMax[i];
            if (p.x < a.x || p.y < a.y || p.z < a.z || p.x > b.x || p.y > b.y || p.z > b.z) continue;
            k = i; break;
        }
        if (k < 0 || areaIds[k] == cur || areaWarps[k] == null) { _areaCand = -1; return false; }
        if (k != _areaCand) { _areaCand = k; _areaCandSince = Time.time; return false; }
        if (Time.time - _areaCandSince < areaDwell) return false;
        _areaCand = -1;
        areaWarps[k].JoinWithoutWarp();
        return true;
    }
}
