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
    [Tooltip("人を出す時、足が出口の面より最低これだけ前に出るように")]
    public float footAhead = 0.35f;
    [Header("ふわふわのうつし")]
    public MeshRenderer ghost;
    public float ghostRange = 40f;
    private Rigidbody _rb;
    private MeshRenderer _ballR;
    private MaterialPropertyBlock _mpb;
    private float _prevHead = 1f, _prevBall = 1f;
    private bool _init;

    private void Start()
    {
        if (ball != null) { _rb = ball.GetComponent<Rigidbody>(); _ballR = ball.GetComponent<MeshRenderer>(); }
        _mpb = new MaterialPropertyBlock();
        if (ghost != null) ghost.enabled = false;
    }

    private Vector3 MapPoint(Vector3 p)
    {
        Vector3 l = entry.InverseTransformPoint(p);
        l = new Vector3(-l.x, l.y, -l.z);   // y まわり 180°
        return exit.TransformPoint(l);
    }
    private Vector3 UnmapPoint(Vector3 p)
    {
        Vector3 l = exit.InverseTransformPoint(p);
        l = new Vector3(-l.x, l.y, -l.z);
        return entry.TransformPoint(l);
    }
    private Vector3 MapDir(Vector3 d)
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
            if (_init && _prevHead > 0f && s <= 0f && InHole(head, 0f))
            {
                Vector3 pos = p.GetPosition(), vel = p.GetVelocity();
                Quaternion rot = p.GetRotation();
                // 頭でくぐるので、足はまだ面の手前（出口では面の奥＝何もない所）にいる → 出口の面より footAhead m 前へ出す
                Vector3 np = MapPoint(pos);
                float fz = Vector3.Dot(np - exit.position, exit.forward);
                if (fz < footAhead) np += exit.forward * (footAhead - fz);
                p.TeleportTo(np, MapRot(rot), VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, false);
                p.SetVelocity(MapDir(vel));
                s = 1f;   // 向こうへ出たので、こちらの面の記録はもどす（出口の面は手前にいるのでくぐり返さない）
            }
            _prevHead = s;
        }
        if (_rb != null)
        {
            Vector3 c = _rb.position;
            float s = Side(c);
            if (_init && _prevBall > 0f && s <= 0f && InHole(c, ballRadius * 0.6f))
            {
                Vector3 np = MapPoint(c), nv = MapDir(_rb.velocity);
                _rb.position = np; ball.transform.position = np; _rb.velocity = nv;
                _rb.rotation = MapRot(_rb.rotation);
                ball.PortalMoved(np - c);
                s = 1f;
            }
            _prevBall = s;
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
                    _ballR.GetPropertyBlock(_mpb); ghost.SetPropertyBlock(_mpb);
                }
                if (ghost.enabled != show) ghost.enabled = show;
            }
        }
        _init = true;
    }
}
