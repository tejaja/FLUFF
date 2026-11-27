using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// あそびばのおもちゃ（コース2–4「はぐるまのしま」のクリアで出る）：プロペラ＋歯車（2026-11-16 てじゃ「2-4 プロペラ+回る歯車」）
// 柱の上のプロペラをエアブラスターで撃つと回る（FuwaBlower → Blown。撃つほど速く、ほっとくとゆっくり止まる）。
// プロペラとつながった床の歯車（大・小）がいっしょに回る。歯車に乗った人はいっしょに運ばれる（自分の画面で）、ふわふわはまさつで運ばれる
// 回り方はみんなに同期：撃った人が持ち主になって「その時の角度・速さ・時刻」を送り、みんな同じ式（指数で減る速さ）で先を計算する
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaPlaygroundWindmill : UdonSharpBehaviour
{
    [Tooltip("プロペラ（ローカル Y 軸で回る）")]
    public Transform rotor;
    [Tooltip("床の歯車（ローカル Y 軸で回る）。gearRatio＝プロペラ 1 度あたりの回る度（向きは符号）")]
    public Transform[] gears;
    public float[] gearRatio;
    public float[] gearPhase;
    [Tooltip("当たり（FuwaBlower が見る）：コース2–4 の羽根車と同じ。羽根の円（横 hitRadius、たて ±hitHalfHeight）を風の細い芯（hitCone 度）が通ったら")]
    public float hitRadius = 1f, hitHalfHeight = 0.55f, hitCone = 3f;
    public float spinPerShot = 360f, maxSpin = 720f;
    [Tooltip("回る速さの減り方（1秒あたり）")]
    public float drag = 0.35f;
    public AudioSource clickSound;
    [Tooltip("乗っている人を見るレイの長さ")]
    public float rideRay = 0.4f;

    [UdonSynced] private float _turn0, _vel0;
    [UdonSynced] private double _t0;
    private float _turn, _vel;
    private Rigidbody[] _rbs;
    private Quaternion[] _base;
    private float _lastTurn; private bool _init;

    private void Start()
    {
        int n = gears != null ? gears.Length : 0;
        _rbs = new Rigidbody[n]; _base = new Quaternion[n];
        for (int i = 0; i < n; i++) if (gears[i] != null) { _rbs[i] = gears[i].GetComponent<Rigidbody>(); _base[i] = gears[i].localRotation; }
        Eval();
        Apply();
    }

    private void Eval()
    {
        float dt = (float)(Networking.GetServerTimeInSeconds() - _t0);
        if (dt < 0f) dt = 0f;
        float e = Mathf.Exp(-drag * dt);
        _vel = _vel0 * e;
        _turn = _turn0 + (drag > 0.0001f ? _vel0 * (1f - e) / drag : _vel0 * dt);
    }

    // 2026-11-16 てじゃ「はね、それじゃないやつ」→ コース2–4 の回転床の羽根車（たての羽根、横から撃つ）。撃った所と向きで回る向きが決まる（FuwaTurnTable.Shot と同じ）
    public void Shot(Vector3 origin, Vector3 aim, float power)
    {
        if (rotor == null) return;
        Vector3 o = origin - rotor.position; o.y = 0f;
        Vector3 a = aim; a.y = 0f;
        if (a.sqrMagnitude < 1e-4f) return;
        a.Normalize();
        Vector3 c = o - a * Vector3.Dot(o, a);
        float lever = Mathf.Clamp01(c.magnitude / Mathf.Max(0.01f, hitRadius));
        float cy = Vector3.Cross(c.normalized, a).y;   // + なら Unity の Y 回転の +（上から見て時計回り）
        Eval();
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _turn0 = Mathf.Repeat(_turn, 360000f);
        _vel0 = Mathf.Clamp(_vel + cy * spinPerShot * power * lever, -maxSpin, maxSpin);
        _t0 = Networking.GetServerTimeInSeconds();
        RequestSerialization();
        if (clickSound != null) clickSound.Play();
    }

    private void Update()
    {
        Eval();
        if (rotor != null) rotor.localRotation = Quaternion.Euler(0f, _turn, 0f);
        Carry();
    }

    private void FixedUpdate()
    {
        Eval();
        Apply();
    }

    private void Apply()
    {
        if (gears == null || gearRatio == null) return;
        for (int i = 0; i < gears.Length; i++)
        {
            if (gears[i] == null) continue;
            float a = gearRatio[i] * _turn + (gearPhase != null && i < gearPhase.Length ? gearPhase[i] : 0f);
            Quaternion q = gears[i].parent != null ? gears[i].parent.rotation * Quaternion.Euler(0f, -a, 0f) : Quaternion.Euler(0f, -a, 0f);
            if (_rbs[i] != null) _rbs[i].MoveRotation(q); else gears[i].rotation = q;
        }
    }

    // 歯車に乗っている自分を、その歯車の回りに合わせて運ぶ（横だけ）
    private void Carry()
    {
        float turn = _turn;
        if (!_init) { _lastTurn = turn; _init = true; return; }
        float d = turn - _lastTurn; _lastTurn = turn;
        if (Mathf.Abs(d) < 0.0001f || gears == null) return;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        Vector3 pos = p.GetPosition();
        RaycastHit hit;
        if (!Physics.Raycast(pos + Vector3.up * 0.3f, Vector3.down, out hit, 0.3f + rideRay, ~((1 << 9) | (1 << 10)), QueryTriggerInteraction.Ignore)) return;
        for (int i = 0; i < gears.Length; i++)
        {
            if (gears[i] == null || hit.collider == null || !hit.collider.transform.IsChildOf(gears[i])) continue;
            Vector3 c = gears[i].position;
            Quaternion r = Quaternion.AngleAxis(-gearRatio[i] * d, gears[i].parent != null ? gears[i].parent.up : Vector3.up);
            Vector3 delta = c + r * (pos - c) - pos; delta.y = 0f;
            if (delta.sqrMagnitude < 1e-10f || delta.sqrMagnitude > 1f) return;
            VRCPlayerApi.TrackingData o = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Origin);
            p.TeleportTo(o.position + delta, o.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignRoomWithSpawnPoint, true);
            return;
        }
    }
}
