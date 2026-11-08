using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2–4 はぐるまのとう：歯車に巻き込まれた時のめりこみ対策（2026-11-08 てじゃ「プレイヤが歯車に巻き込まれて潰される感じになると、めり込んでちょっと変」）
// ・運ぶ（CarryPlayer）：FuwaGearRide・FuwaClockwork が乗っている自分を運ぶ時に呼ぶ。行き先で体が壁・ほかの物に当たるなら運ばない
//   （運ぶのはワープなので当たりを無視して、壁やほかの歯車の中まで入っていた）
// ・押し出す（LateUpdate）：動く物（movers：歯車・回転床・せり出す橋・振り子のおもり）が体に食いこんだら、食いこまない近くの所へずらす
//   （VRChat の人は動く当たりに押されない）。8 方向 × 近い順の距離で、足もとに床がある所 → 床がなくても空いている所 → 上（乗れる高さの上の面）
// 体は足もとから footClear 上〜height のカプセル（床や小さい段は数えない）。自分の画面だけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGearGuard : UdonSharpBehaviour
{
    [Tooltip("動く物の当たり（食いこんだら押し出す）")]
    public Collider[] movers;
    [Tooltip("movers と同じ並びで、いっしょに動くまとまり（歯車など）。足もとと同じまとまりの物は食いこみに数えない（いっしょに動くので押されない）")]
    public Transform[] moverGroup;
    [Tooltip("体が当たる物のレイヤー（道・壁）")]
    public LayerMask mask = 1 | (1 << 11);
    [Tooltip("食いこみを調べる体の半径（VRChat の体より少し細く：かべぎわに立っているだけで押し出さないように）と、逃がす先を調べる半径")]
    public float radius = 0.18f, freeRadius = 0.22f;
    public float footClear = 0.3f, height = 1.5f;
    [Tooltip("上の面へ逃がす時の、足もとからの高さの上限")]
    public float maxStepUp = 1.5f;
    public bool debugLog = false;

    private Vector3[] _dirs;
    private float[] _steps;

    private void Start()
    {
        _dirs = new Vector3[8];
        for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4f; _dirs[k] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)); }
        _steps = new float[] { 0.06f, 0.12f, 0.2f, 0.3f, 0.45f, 0.65f, 0.9f };
    }

    private int MoverIndex(Collider c)
    {
        if (c == null || movers == null) return -1;
        for (int i = 0; i < movers.Length; i++) if (movers[i] == c) return i;
        return -1;
    }

    // pos に立った時の体が、ignore（の子）以外の物に当たるか
    private bool Blocked(Vector3 pos, Transform ignore)
    {
        Collider[] hits = Physics.OverlapCapsule(pos + Vector3.up * (footClear + freeRadius), pos + Vector3.up * (height - freeRadius), freeRadius, mask, QueryTriggerInteraction.Ignore);
        if (hits == null) return false;
        for (int i = 0; i < hits.Length; i++)
        {
            Collider h = hits[i];
            if (h == null) continue;
            if (ignore != null && h.transform.IsChildOf(ignore)) continue;
            return true;
        }
        return false;
    }

    private bool HasFloor(Vector3 pos)
    {
        return Physics.Raycast(pos + Vector3.up * footClear, Vector3.down, footClear + 0.5f, mask, QueryTriggerInteraction.Ignore);
    }

    private void Shift(VRCPlayerApi p, Vector3 d)
    {
        VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
        p.TeleportTo(o.position + d, o.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
    }

    // 乗っている歯車 riding の回りに合わせて delta だけ運ぶ（行き先がふさがっていたら運ばない。今もう食いこんでいる時は運ぶ＝押し出しにまかせる）
    public void CarryPlayer(Vector3 delta, Transform riding)
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        Vector3 pos = p.GetPosition();
        if (Blocked(pos + delta, riding) && !Blocked(pos, riding)) return;
        Shift(p, delta);
    }

    private void LateUpdate()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || _dirs == null) return;
        Vector3 pos = p.GetPosition();
        Collider[] hits = Physics.OverlapCapsule(pos + Vector3.up * (footClear + radius), pos + Vector3.up * (height - radius), radius, mask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;
        // 足もとの物のまとまり（同じまとまりは、いっしょに動くので数えない）
        Transform groundGroup = null;
        RaycastHit gh;
        if (Physics.Raycast(pos + Vector3.up * footClear, Vector3.down, out gh, footClear + 0.6f, mask, QueryTriggerInteraction.Ignore))
        {
            int gi = MoverIndex(gh.collider);
            if (gi >= 0 && moverGroup != null && gi < moverGroup.Length) groundGroup = moverGroup[gi];
        }
        bool mover = false;
        for (int i = 0; i < hits.Length; i++)
        {
            int mi = MoverIndex(hits[i]);
            if (mi < 0) continue;
            if (groundGroup != null && moverGroup != null && mi < moverGroup.Length && moverGroup[mi] == groundGroup) continue;
            mover = true; break;
        }
        if (!mover) return;

        // 近い順に、床のある空いている所
        Vector3 best = Vector3.zero; bool found = false;
        for (int pass = 0; pass < 2 && !found; pass++)
            for (int s = 0; s < _steps.Length && !found; s++)
                for (int k = 0; k < _dirs.Length && !found; k++)
                {
                    Vector3 d = _dirs[k] * _steps[s];
                    if (Blocked(pos + d, null)) continue;
                    if (pass == 0 && !HasFloor(pos + d)) continue;
                    best = d; found = true;
                }
        if (!found)
        {
            // 横はどこもふさがっている（はさまれた）：上の面へ
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * (maxStepUp + 0.3f), Vector3.down, out hit, maxStepUp + 0.3f, mask, QueryTriggerInteraction.Ignore)
                && hit.point.y - pos.y > 0.05f && !Blocked(hit.point, null))
            { best = hit.point - pos + Vector3.up * 0.02f; found = true; }
        }
        if (!found) return;
        if (debugLog) Debug.Log("[FuwaGearGuard] push " + best.ToString("F2"));
        Shift(p, best);
    }
}
