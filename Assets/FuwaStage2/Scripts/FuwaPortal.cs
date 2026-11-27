using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2026-11-10 2–5 そらのいせき：壁に開いたポータル（てじゃ「ふわふわも人もそこを通って進む」）
//  entry（この面の手前＝+forward から奥へ）をくぐったら exit へ。位置・向き・速さは entry → exit の変換（exit の forward が出ていく向き）でそのまま運ぶ
//   変換：p' = exit · R180y · entry⁻¹ · p（entry の奥へ進むと exit の手前へ出る）
//  人：頭（VR の目）が面をこえた瞬間に体ごと移す（体で判定すると、頭が先に向こう側の「何もない所」を見てしまう）
//  ふわふわ：まん中が面をこえた瞬間に移す（ふわふわは自分の画面だけの物なので、ここで動かしてよい）。FuwaBall.PortalMoved で「落ちたらアウト」の基準も動かす
//  穴の大きさ：entry の local で |x| <= halfW、0 <= y <= height（y=0 が床）
//  向こうが見えるのは見た目だけの仕組み（Fuwa/PortalMask ＋ 行き先のコピー）。
//  ふわふわのうつし（ghost）：ふわふわが exit の手前（向こう側）にいる間、entry の奥の同じ所に、穴の中だけに見えるうつしを出す（てじゃ「でもふわふわ見えないか」）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPortal : UdonSharpBehaviour
{
    public Transform entry, exit;
    public float halfW = 1.1f, height = 3f;
    public FuwaBall ball;
    [Tooltip("ふわふわの半径（穴のふちに当たる分だけ内がわで判定）")]
    public float ballRadius = 0.35f;
    [Tooltip("人を出す時、足が出口の面より最低これだけ前に出るように（0=押し出さない。出口の裏に見えない床がある時）")]
    public float footAhead = 0f;
    private Vector3 _pendingVel; private int _velFrames;
    [Header("ふわふわのうつし")]
    public MeshRenderer ghost;
    [Tooltip("ふわふわが出口の面をまたいでいる間、入口の手前に出ている分（2026-11-10 てじゃ「通る一瞬ふわふわが欠ける」）。ステンシルなし・入口の面の奥を切る")]
    public MeshRenderer nearGhost;
    public float ghostRange = 40f;
    private Rigidbody _rb;
    private MeshRenderer _ballR;
    private MaterialPropertyBlock _mpb;
    // ふわふわの毛の材質は FuwaBall が自分用に複製して、つぶれ・光り・顔などを直接入れている → うつしにも毎フレーム写す（てじゃ「ポータル越しだとつぶれるアニメーションとかはでない」）
    private Material _bm, _gm, _nm;
    private float _prevHead = 1f, _prevBall = 1f;
    private bool _init;
    // 2026-11-27 てじゃ「2–5 の最初のポータルでふわふわがどっか見えないとこいっちゃった（再現なし）」：
    //  前は「面の手前→奥に移った」＋「移った後の点が穴の幅（x・y だけ、奥行きなし）の中」で判定していたので、
    //  ふわふわ・人が一瞬で遠くへ移った時（アウトで戻る・出し直し・呼び出し・テレポート）に、移った先がたまたま面の奥の「穴の延長」だとくぐった扱いになっていた。
    //  → 前の位置と今の位置を結んだ線が面を通る点で穴の中か見る。1フレームで maxStep 以上動いた時はくぐった扱いにしない
    [Tooltip("1フレームでこれより大きく動いた時は、くぐったとみなさない（m）")]
    public float maxStep = 2f;
    private Vector3 _prevHeadPos, _prevBallPos;
    private bool Crossed(Vector3 prev, float sPrev, Vector3 cur, float sCur, float margin)
    {
        if (!(sPrev > 0f && sCur <= 0f)) return false;
        if ((cur - prev).sqrMagnitude > maxStep * maxStep) return false;
        float t = sPrev / Mathf.Max(1e-5f, sPrev - sCur);
        return InHole(Vector3.Lerp(prev, cur, t), margin);
    }

    // 2026-11-17 コースにいない間は止められる（FuwaCourseLod の courseOnlyBehaviours）。くぐった直後の勢いの残りは捨てる
    public void LodStop() { _velFrames = 0; _init = false; }

    private void Start()
    {
        if (ball != null) { _rb = ball.GetComponent<Rigidbody>(); _ballR = ball.GetComponent<MeshRenderer>(); }
        _mpb = new MaterialPropertyBlock();
        if (ghost != null) { ghost.enabled = false; _gm = ghost.material; }
        if (nearGhost != null) { nearGhost.enabled = false; _nm = nearGhost.material; }
    }

    private void CopyFur(Material dst)
    {
        if (dst == null) return;
        if (_bm == null && ball != null && ball.furRenderer != null) _bm = ball.furRenderer.material;
        if (_bm == null) return;
        dst.SetColor("_Color", _bm.GetColor("_Color")); dst.SetColor("_TipColor", _bm.GetColor("_TipColor"));
        dst.SetColor("_GlowColor", _bm.GetColor("_GlowColor")); dst.SetVector("_GlowDir", _bm.GetVector("_GlowDir"));
        dst.SetVector("_Drag", _bm.GetVector("_Drag")); dst.SetVector("_Squash", _bm.GetVector("_Squash"));
        dst.SetFloat("_FaceHit", _bm.GetFloat("_FaceHit")); dst.SetFloat("_FaceOn", _bm.GetFloat("_FaceOn"));
        dst.SetFloat("_FurLength", _bm.GetFloat("_FurLength"));
    }

    private Vector3 MapPoint(Vector3 p)
    {
        Vector3 l = entry.InverseTransformPoint(p);
        l = new Vector3(-l.x, l.y, -l.z);   // y まわり 180°
        return exit.TransformPoint(l);
    }
    public Vector3 GhostPos(Vector3 ballPos) { return UnmapPoint(ballPos); }
    // 銃（origin）から、穴ごしに向こうのふわふわが見えているか：ふわふわが出口の手前の近くにいて、origin → うつしの線が入口の穴を手前から奥へ通る
    public bool SeesBall(Vector3 origin, Vector3 ballPos)
    {
        if (entry == null || exit == null) return false;
        if (Vector3.Dot(ballPos - exit.position, exit.forward) < -ballRadius || (ballPos - exit.position).sqrMagnitude > ghostRange * ghostRange) return false;
        Vector3 g = UnmapPoint(ballPos);
        float so = Side(origin), sg = Side(g);
        if (so <= 0f || sg >= 0f) return false;
        Vector3 hit = origin + (g - origin) * (so / (so - sg));
        return InHole(hit, 0f);
    }
    private Vector3 UnmapPoint(Vector3 p)
    {
        Vector3 l = exit.InverseTransformPoint(p);
        l = new Vector3(-l.x, l.y, -l.z);
        return entry.TransformPoint(l);
    }
    public Vector3 MapDir(Vector3 d)
    {
        Vector3 l = entry.InverseTransformDirection(d);
        l = new Vector3(-l.x, l.y, -l.z);
        return exit.TransformDirection(l);
    }
    private Quaternion MapRot(Quaternion q)
    {
        return exit.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(entry.rotation) * q;
    }
    private Quaternion UnmapRot(Quaternion q)
    {
        return entry.rotation * Quaternion.Euler(0f, 180f, 0f) * Quaternion.Inverse(exit.rotation) * q;
    }
    private float Side(Vector3 p) { return Vector3.Dot(p - entry.position, entry.forward); }
    private bool InHole(Vector3 p, float margin)
    {
        Vector3 l = entry.InverseTransformPoint(p);
        return Mathf.Abs(l.x) <= halfW - margin && l.y >= -0.6f && l.y <= height - margin;
    }

    private void Update()
    {
        if (entry == null || exit == null) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p != null)
        {
            Vector3 head = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            float s = Side(head);
            if (_init && Crossed(_prevHeadPos, _prevHead, head, s, 0f))
            {
                // 2026-11-10 てじゃ「歩いてはいったときに一瞬カクっとなる」→ プレイスペースの原点ごと動かす（頭の位置・向きがぴったり続く）、勢いは次のフレームでもどす
                Vector3 vel = p.GetVelocity();
                VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
                Vector3 no = MapPoint(o.position);
                if (footAhead > 0f)
                {
                    float fz = Vector3.Dot(MapPoint(p.GetPosition()) - exit.position, exit.forward);
                    if (fz < footAhead) no += exit.forward * (footAhead - fz);
                }
                p.TeleportTo(no, MapRot(o.rotation), VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, false);
                _pendingVel = MapDir(vel); _velFrames = 2;
                s = 1f;   // 向こうへ出たので、こちらの面の記録はもどす（出口の面は手前にいるのでくぐり返さない）
            }
            else if (_velFrames > 0) { _velFrames--; p.SetVelocity(_pendingVel); }
            _prevHead = s; _prevHeadPos = head;
        }
        if (_rb != null)
        {
            Vector3 c = _rb.position;
            float s = Side(c);
            if (_init && Crossed(_prevBallPos, _prevBall, c, s, ballRadius * 0.6f))
            {
                Vector3 np = MapPoint(c), nv = MapDir(_rb.velocity);
                _rb.position = np; ball.transform.position = np; _rb.velocity = nv;
                _rb.rotation = MapRot(_rb.rotation);
                ball.PortalMoved(np - c);
                s = 1f; c = np;
            }
            _prevBall = s; _prevBallPos = c;
            // うつし：ふわふわが出口の手前（向こう側）の近くにいて見えている間
            if (ghost != null)
            {
                Vector3 bp = _rb.position;
                bool show = _ballR != null && _ballR.enabled && ball.gameObject.activeInHierarchy
                    && Vector3.Dot(bp - exit.position, exit.forward) > -ballRadius && (bp - exit.position).sqrMagnitude < ghostRange * ghostRange;
                if (show)
                {
                    Transform g = ghost.transform;
                    g.position = UnmapPoint(bp); g.rotation = UnmapRot(ball.transform.rotation); g.localScale = ball.transform.lossyScale;
                    _ballR.GetPropertyBlock(_mpb); ghost.SetPropertyBlock(_mpb); CopyFur(_gm);
                }
                if (ghost.enabled != show) ghost.enabled = show;
                // 出口の面をまたいでいる（後ろ半分が出口の面の奥）間は、その分を入口の手前にも出す
                if (nearGhost != null)
                {
                    float d = Vector3.Dot(bp - exit.position, exit.forward);
                    bool near = show && d < ballRadius * 1.6f;
                    if (near)
                    {
                        Transform g = nearGhost.transform;
                        g.position = ghost.transform.position; g.rotation = ghost.transform.rotation; g.localScale = ghost.transform.localScale;
                        nearGhost.SetPropertyBlock(_mpb); CopyFur(_nm);
                    }
                    if (nearGhost.enabled != near) nearGhost.enabled = near;
                }
            }
        }
        _init = true;
    }
}
