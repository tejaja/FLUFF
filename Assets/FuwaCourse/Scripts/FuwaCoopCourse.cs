using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 「みんなで」協力のランダムコース（2026-10-12 試作。てじゃ「組み合わせでランダムで変わる感じもおもろそう」）
// ・パーツ（pieces）は最初から全部置いておいて、ラウンドのたびに並べ替える（その場で作らない）。
//   パーツは「入口＝自分の位置・向き（+Z へ進む）」「出口＝子の Exit」。前のパーツの出口に次のパーツの入口を合わせてつなぐ
// ・並びは種（_seed）1つで決まる。種だけ同期すれば全員同じコース（持ち主が決める）
// ・同じ種類は2回まで・同じ種類を続けない。曲がりは「最初の向きから左右90°まで」（コースが戻ってきて重ならないように）
// ・最後の出口にゴールの島（goalRoot）を置く
// ・同期する物はエリアの表示切り替えで隠れると届かないので、FuwaSystem の下に置く（パーツは隠れてもよい）
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaCoopCourse : UdonSharpBehaviour
{
    [Tooltip("並べるパーツ（同じ種類は2つずつ）")]
    public Transform[] pieces;
    [Tooltip("パーツの種類（pieces と同じ順）。0=まっすぐ 1=左カーブ 2=右カーブ 3=細い橋 4=向かい風")]
    public int[] pieceType;
    [Tooltip("曲がる角度（左カーブ +90、右カーブ -90、ほかは 0）。pieces と同じ順")]
    public float[] pieceTurn;
    [Tooltip("スタートの島の出口（ここから1つめのパーツをつなぐ）")]
    public Transform startExit;
    [Tooltip("ゴールの島（最後のパーツの出口に置く。自分の位置＝入口）")]
    public Transform goalRoot;
    public int count = 5;
    [Tooltip("並べ替えたら巨大ふわふわをキノコへ戻す")]
    public FuwaCoopGiant giant;

    [UdonSynced] private int _seed = 1;
    private int _shown = -1;
    private int _rnd;

    private void Start() { Arrange(); }

    public override void OnDeserialization() { Arrange(); }

    // 「コースを変える」ボタン（FuwaCoopButton から）
    public void Reroll()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _seed = Random.Range(1, 1000000);
        RequestSerialization();
        Arrange();
    }

    private int Next(int n)
    {
        _rnd = (_rnd * 1103515245 + 12345) & 0x7fffffff;   // Udon は uint の割り算が使えないので int で
        return (_rnd >> 8) % n;
    }

    private void Arrange()
    {
        if (pieces == null || startExit == null || _shown == _seed) return;
        _shown = _seed;
        _rnd = (_seed * 1103515245 + 12345) & 0x7fffffff;
        int n = pieces.Length;
        bool[] used = new bool[n];
        for (int i = 0; i < n; i++) if (pieces[i] != null) pieces[i].gameObject.SetActive(false);
        Vector3 pos = startExit.position; Quaternion rot = startExit.rotation;
        float heading = 0f; int lastType = -1;
        int[] typeCount = new int[8];
        for (int k = 0; k < count; k++)
        {
            // 候補：まだ使っていない・前と同じ種類でない・同じ種類2回まで・曲がりすぎない
            int[] cand = new int[n]; int cn = 0;
            for (int i = 0; i < n; i++)
            {
                if (pieces[i] == null || used[i]) continue;
                int t = pieceType[i];
                if (t == lastType || typeCount[t] >= 2) continue;
                if (k == 0 && t == 4) continue;   // 1つめが向かい風だと、島から出たとたん押し戻されてコースに入れない（2026-10-12 てじゃ）
                float h = heading + pieceTurn[i];
                if (h > 90.5f || h < -90.5f) continue;
                // 同じ種類のパーツが複数あっても、選ばれやすさは種類ごとに同じに（最初の1つだけ候補にする）
                bool dup = false;
                for (int j = 0; j < cn; j++) if (pieceType[cand[j]] == t) dup = true;
                if (dup) continue;
                cand[cn++] = i;
            }
            if (cn == 0) break;
            int pick = cand[Next(cn)];
            used[pick] = true; typeCount[pieceType[pick]]++; lastType = pieceType[pick]; heading += pieceTurn[pick];
            Transform p = pieces[pick];
            p.SetPositionAndRotation(pos, rot);
            p.gameObject.SetActive(true);
            Transform exit = p.Find("Exit");
            if (exit != null) { pos = exit.position; rot = exit.rotation; }
        }
        if (goalRoot != null) goalRoot.SetPositionAndRotation(pos, rot);
        if (giant != null) giant.ResetGiant();
    }
}

