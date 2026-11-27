using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 浅瀬（ステージ2「みずのたに」、2026-10-17 てじゃ「浅瀬はプレイヤー減速（ジャンプ初速も下がる、高さは同じ）」）。
// 水の入れ物のトリガー（FuwaWater と同じ物）に付ける。自分の足が水の中にある間：
//   歩く・走る・横の速さ × moveMul、ジャンプの初速 × jumpMul、重力 × jumpMul²（＝跳べる高さは同じで、ふわっとゆっくり）。
//   水から跳んで出ても、着地するまではそのまま（空中で急に重くならないように）。
// 入った瞬間に「じゃぶ」の音と水しぶき（ほかの人が入った時も、その人の所で鳴る）
// 判定はトリガーの出入りの通知ではなく、毎フレーム足の位置で見る（蓮の葉が沈んで水の中に入った時も拾える。通知は取りこぼすこともあるので）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaShallowWater : UdonSharpBehaviour
{
    [Range(0.1f, 1f)] public float moveMul = 0.3f;   // 2026-10-19 てじゃ「水の減速を強めに、プレイヤーは水に極力入りたくない」0.55→0.3（ジャンプはそのまま＝岸に上がれる）。
    // 2026-10-25 「減速がけっこうなストレス」で一度なしにしたが、「減速有りで飛び石かな」→ 0.3 のまま、川の渡り場に飛び石（FuwaCourse2_1Builder.CrossingStones）
    [Range(0.1f, 1f)] public float jumpMul = 1f;   // 2026-10-23 てじゃ「ジャンプ力は下げなくていい」0.7→1（前は跳べる高さは同じで、ふわっとゆっくり）
    [Tooltip("水面からこの高さまでに足があれば水の中（蓮の葉が水面すれすれまで沈んだ時も入るように）")]
    public float surfaceAbove = 0.03f;
    [Tooltip("着地しないまま水から出て、これだけたったら戻す（念のため）")]
    public float restoreTimeout = 3f;

    [Header("元の値（VRChatの既定値。今の値を覚えると、ほかの減速中の値を覚えて戻らなくなるので決まった値で）")]
    public float baseWalk = 2f, baseRun = 4f, baseStrafe = 2f, baseJump = 3f, baseGravity = 1f;

    [Header("音・しぶき")]
    [Tooltip("鳴らす場所（クリップは下の2つ）。clip には強い方（ザブン）を入れておく")]
    public AudioSource splashSound;
    [Tooltip("歩いて入る・軽く触れた時の小さい「ちゃぷ」（2026-10-17 てじゃ「軽くふれてもザブンなるのが違和感」）")]
    public AudioClip softClip;
    [Tooltip("下向きにこの速さ(m/s)より速く落ちてきたら「ザブン」")]
    public float strongFall = 2.5f;
    public FuwaWaterFx fx;
    // 曲がった川・池（2–1）：向きのついた箱をいくつも並べた水は、ここに全部入れて1つで見る（箱ごとに置くと境目で「ちゃぷ」が鳴り続ける）。
    // 空なら自分のコライダーの外接箱で見る（まっすぐな池・テスト場）
    public BoxCollider[] areas;
    public float splashStrength = 1.2f;
    [Header("川底で見る（2–1。入っていれば areas より優先）")]
    [Tooltip("この水の川底のコライダー（Bed・Body）。足の真下がこれで、水面より下なら水の中")]
    public Collider[] floors;
    [Tooltip("水面の高さ（ワールド）")]
    public float surfaceY;
    [Tooltip("足の下を見る光線が当たるレイヤー（プレイヤーが立てる物）")]
    public LayerMask floorMask = ~0;

    private Collider _col;
    private bool _in;
    private bool _slowed;
    private float _leftAt;
    private float _lastSplash = -10f;

    // 2026-11-17 軽くする：水全体を囲む箱（少し広め）を先に作っておき、その外にいる間は何もしない
    //（ステージ2のロビーで、2–1 の浅瀬9つが毎フレーム水の箱を全部調べて約0.9ms かかっていた）
    private Vector3 _bbMin, _bbMax;
    private bool _bbReady;

    private void Start()
    {
        _col = GetComponent<Collider>();
    }

    private void MakeBox()
    {
        bool any = false; Bounds b = new Bounds();
        if (floors != null)
            for (int i = 0; i < floors.Length; i++)
            {
                if (floors[i] == null) continue;
                if (!any) { b = floors[i].bounds; any = true; } else b.Encapsulate(floors[i].bounds);
            }
        if (!any && areas != null)
            for (int i = 0; i < areas.Length; i++)
            {
                if (areas[i] == null) continue;
                if (!any) { b = areas[i].bounds; any = true; } else b.Encapsulate(areas[i].bounds);
            }
        if (!any && _col != null) { b = _col.bounds; any = true; }
        if (!any) return;
        // コライダーが無効だと bounds が 0 になるので、その時は作らない（毎フレーム調べる今まで通り）
        if (b.size.sqrMagnitude < 1e-4f) return;
        _bbMin = b.min - new Vector3(2f, 2f, 2f);
        _bbMax = b.max + new Vector3(2f, surfaceAbove + 2f, 2f);
        _bbReady = true;
    }

    private float _hitTop;

    private bool FeetInWater(Vector3 f)
    {
        // 2026-11-28 てじゃ「川の上を減速なしで走れる／3連滝の道の端で水判定」→ 箱をやめて、足の真下が川底（floors）かを光線で直接見る。
        //  （曲がった川に合わせた箱は道の端にはみ出し、川底が一段深い所は箱の外になっていた。道・石・岩の上なら水ではない）
        if (floors != null && floors.Length > 0)
        {
            if (f.y > surfaceY + surfaceAbove) return false;
            RaycastHit hit;
            if (!Physics.Raycast(f + new Vector3(0f, 0.3f, 0f), Vector3.down, out hit, 3f, floorMask, QueryTriggerInteraction.Ignore)) return false;
            Collider c = hit.collider;
            for (int i = 0; i < floors.Length; i++) if (floors[i] == c) { _hitTop = surfaceY; return true; }
            return false;
        }
        if (areas != null && areas.Length > 0)
        {
            for (int i = 0; i < areas.Length; i++)
            {
                BoxCollider a = areas[i];
                if (a == null) continue;
                Transform t = a.transform;
                Vector3 lp = t.InverseTransformPoint(f) - a.center;
                Vector3 h = a.size * 0.5f;
                float top = t.TransformPoint(a.center + new Vector3(0f, h.y, 0f)).y;
                if (Mathf.Abs(lp.x) < h.x && Mathf.Abs(lp.z) < h.z && f.y < top + surfaceAbove && f.y > top - a.size.y * t.lossyScale.y - 0.3f) { _hitTop = top; return true; }
            }
            return false;
        }
        if (_col == null) return false;
        _hitTop = _col.bounds.max.y;
        Bounds b = _col.bounds;
        return f.x > b.min.x && f.x < b.max.x && f.z > b.min.z && f.z < b.max.z && f.y < b.max.y + surfaceAbove && f.y > b.min.y - 0.3f;
    }

    private void Update()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || !p.IsValid()) return;
        Vector3 f = p.GetPosition();
        if (!_bbReady) MakeBox();
        if (_bbReady && !_in && !_slowed
            && (f.x < _bbMin.x || f.y < _bbMin.y || f.z < _bbMin.z || f.x > _bbMax.x || f.y > _bbMax.y || f.z > _bbMax.z)) return;
        bool inside = FeetInWater(f);
        if (inside && !_in) Splash(new Vector3(f.x, _hitTop, f.z), p.GetVelocity());
        if (!inside && _in) _leftAt = Time.time;
        _in = inside;
        if (inside)
        {
            // 重なったほかの水やイバラが速さを戻しても、中にいる間は遅くし直す
            if (!_slowed || p.GetWalkSpeed() > baseWalk * moveMul + 0.01f || p.GetJumpImpulse() > baseJump * jumpMul + 0.01f) Apply(p);
            // ジャンプ中は歩く速さを変えても勢いが残るので、空中でも横の速さを抑える
            if (!p.IsPlayerGrounded())
            {
                Vector3 v = p.GetVelocity(); Vector3 h = new Vector3(v.x, 0f, v.z);
                float lim = baseRun * moveMul;
                if (h.magnitude > lim) { h = h.normalized * lim; p.SetVelocity(new Vector3(h.x, v.y, h.z)); }
            }
            return;
        }
        if (_slowed && (p.IsPlayerGrounded() || Time.time - _leftAt > restoreTimeout)) Restore(p);
    }

    private void Apply(VRCPlayerApi p)
    {
        p.SetWalkSpeed(baseWalk * moveMul);
        p.SetRunSpeed(baseRun * moveMul);
        p.SetStrafeSpeed(baseStrafe * moveMul);
        p.SetJumpImpulse(baseJump * jumpMul);
        p.SetGravityStrength(baseGravity * jumpMul * jumpMul);
        if (!_slowed)
        {
            // 入った瞬間の勢いも水にからめ取られる
            Vector3 v = p.GetVelocity();
            p.SetVelocity(new Vector3(v.x * moveMul, v.y > 0f ? v.y * jumpMul : v.y * 0.6f, v.z * moveMul));
        }
        _slowed = true;
    }

    // そのコースを出て止まる前（FuwaCourseLod）：水の中で遅くしていたら戻す
    public void LodStop()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (_slowed && p != null && p.IsValid()) Restore(p);
        _in = false;
    }

    private void Restore(VRCPlayerApi p)
    {
        p.SetWalkSpeed(baseWalk); p.SetRunSpeed(baseRun); p.SetStrafeSpeed(baseStrafe);
        p.SetJumpImpulse(baseJump); p.SetGravityStrength(baseGravity);
        _slowed = false;
    }

    // ほかの人が入った時（自分は Update の足の位置で見る）
    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || player.isLocal || _col == null) return;
        Vector3 f = player.GetPosition();
        Splash(new Vector3(f.x, _col.bounds.max.y, f.z), player.GetVelocity());
    }

    // 入り方で音を分ける：落ちてきた（下向きが速い）→ザブン、歩いて入る・軽く触れる→小さい「ちゃぷ」。勢いで音量も少し変える
    private void Splash(Vector3 at, Vector3 vel)
    {
        if (Time.time - _lastSplash < 0.25f) return;
        _lastSplash = Time.time;
        float fall = -vel.y;
        bool strong = fall > strongFall || softClip == null;
        if (splashSound != null)
        {
            splashSound.transform.position = at; splashSound.pitch = Random.Range(0.92f, 1.08f);
            AudioClip c = strong ? splashSound.clip : softClip;
            float vol = strong ? Mathf.Lerp(0.7f, 1f, Mathf.InverseLerp(strongFall, strongFall * 2.5f, fall))
                               : Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(new Vector3(vel.x, 0f, vel.z).magnitude / 4f + Mathf.Max(0f, fall) / strongFall));
            if (c != null) splashSound.PlayOneShot(c, vol);
        }
        if (fx != null) fx.Visual(at, strong ? splashStrength : splashStrength * 0.35f);
    }

    private void OnDisable()
    {
        if (!_slowed) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p != null && p.IsValid()) Restore(p);
        _in = false;
    }
}
