using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// ワープを使わずに（VRChatで飛んで）別のコースやロビーに降りた時、そこに入った扱いにする（2026-10-14 てじゃ「ロビーから飛んでコースに入った時、いいかんじにコースに入った扱いに」）。自分の画面だけ
// ・足元の床がどのコースの物か（床のいちばん上の親）で調べる。飛んで通り過ぎるだけなら反応しないよう、地面に立って dwell 秒たったら
// ・切り替えはそのコースのワープ（ロビーのポータル）と同じ：BGM・軽い版・リスポーン先・ロビーに戻った時の向き。プレイヤーは動かさない
// ・ふわふわはそのコースのスタートから（タイムも計る。2026-10-14 てじゃ「どこに立ってもスタートからやらないとだめ」）
// ・ひとりでモード（areaVisibility の soloArea）の時だけ
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

    private float _next;
    private int _cand = -1;
    private float _candSince;

    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + interval;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null || ball == null || roots == null) return;
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
}
