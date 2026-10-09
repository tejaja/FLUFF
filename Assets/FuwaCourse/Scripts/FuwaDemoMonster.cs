using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// チュートリアルの「まもの」のお手本：見た目だけのまもの（コースの FuwaEnemy の体・目・音のコピー）が、
// となりのお手本台（FuwaDemoStand）のふわふわへ寄っていって、ぷるぷる溜めて体当たりする。
// stand の銃が溜めの間に撃つ（stand の shotTimes に入れておく）と、押し返されて目を回す（ふわふわは無事）。
// 撃たれなければ体当たりでふわふわを吹っ飛ばす（本物の FuwaEnemy と同じ。台の外まで飛んで落ちたらはじける）。1回分の長さ・時計の区切りは stand と同じ（同じ時刻にそろう）。
// 自分の画面だけ・同期なし。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaDemoMonster : UdonSharpBehaviour
{
    [Tooltip("いっしょに動くお手本台（ふわふわ・銃・1回分の長さを使う）")]
    public FuwaDemoStand stand;
    [Tooltip("体（FuwaEnemy の Body のコピー）")]
    public Transform visual;
    [Tooltip("目（FuwaEnemy の Eyes のコピー。ふわふわの方を向く）")]
    public Transform eyes;
    [Tooltip("出てくる所")]
    public Transform home;
    [Tooltip("ぷるぷる溜める所（ふわふわの手前）")]
    public Transform windupSpot;
    public AudioSource windupSound;
    public AudioSource dashSound;
    public AudioSource blownSound;
    [Tooltip("ふわふわに当たった時の音")]
    public AudioSource hitSound;

    [Header("時刻（1回分の始まりからの秒）")]
    public float approachFrom = 0.4f;
    public float windupFrom = 1.8f;
    public float dashAt = 2.6f;
    [Tooltip("撃たれる時刻（stand の銃が撃つ時刻と同じにする）。0未満なら撃たれない＝体当たりする")]
    public float shotAt = -1f;

    [Header("動き")]
    [Header("体当たり（本物の FuwaEnemy と同じ値）")]
    public float dashSpeed = 5.3f;
    public float hopHeight = 1f;
    public float maxHopHeight = 2.2f;
    public float ballOvershoot = 0.6f;
    public float ballHitRadius = 0.5f;
    public float ballKnockback = 5.5f;
    public float ballKnockUp = 1.2f;
    public Color ballHitGlow = new Color(0.75f, 0.35f, 1f);
    private float _dashTime = 0.3f, _dashHop = 1f;
    [Tooltip("撃たれた時に押される速さ(m/s)と、止まるまでの減り方")]
    public float pushSpeed = 3.2f;
    public float pushDamping = 3f;
    [Tooltip("撃たれた時にピョンと浮く上向きの速さ・重力・浮いてる間の横の減り方・着地で残す横の速さ（本物の FuwaEnemy と同じ動き。0なら前のようにすべる）")]
    public float blownHop = 2.6f;
    public float blownGravity = 12f;
    public float airDamping = 0.8f;
    public float landKeep = 0.3f;
    private float _vy, _groundY, _squashUntil;
    private bool _airborne;
    public float stunTime = 1.1f;
    public float returnSpeed = 1.2f;
    public float bobHeight = 0.08f;
    public float activeRange = 14f;

    private float _cycleStart = -999f;
    private int _state;   // 0=まだ 1=寄る 2=溜め 3=体当たり 4=押された 5=戻る 6=止まる
    private float _stateTime;
    private Vector3 _pos, _vel, _dashFrom, _dashTo;
    private bool _active;
    private float _nextRangeCheck;
    private Vector3 _visualBase, _visualScale = Vector3.one, _eyesBase, _eyesScale = Vector3.one;
    private bool _hit;

    private void Start()
    {
        if (visual != null) { _visualBase = visual.localPosition; _visualScale = visual.localScale; }
        if (eyes != null) { _eyesBase = eyes.localPosition; _eyesScale = eyes.localScale; }
        SetShown(false);
    }

    private void SetShown(bool on)
    {
        if (visual != null) visual.gameObject.SetActive(on);
        if (eyes != null) eyes.gameObject.SetActive(on);
    }

    private void Update()
    {
        if (Time.time >= _nextRangeCheck)
        {
            _nextRangeCheck = Time.time + 0.5f;
            VRCPlayerApi lp = Networking.LocalPlayer;
            bool near = lp != null && lp.IsValid() && (lp.GetPosition() - transform.position).sqrMagnitude <= activeRange * activeRange;
            if (near != _active) { _active = near; SetShown(near); _cycleStart = -999f; }
        }
        if (!_active || stand == null || home == null || windupSpot == null) return;

        float len = Mathf.Max(0.1f, stand.cycleLength);
        float cs = Mathf.Floor((Time.time - stand.timeOffset) / len) * len + stand.timeOffset;
        if (cs != _cycleStart) { _cycleStart = cs; Begin(); }
        float t = Time.time - _cycleStart;

        Vector3 ballPos = stand.ball != null ? stand.ball.position : windupSpot.position;
        float wob = 0f, squash = 0f;
        switch (_state)
        {
            case 0:
                _pos = home.position;
                if (t >= approachFrom) SetState(1);
                break;
            case 1:
            {
                // ゆらゆら寄っていく
                float k = Mathf.Clamp01((t - approachFrom) / Mathf.Max(0.1f, windupFrom - approachFrom));
                k = k * k * (3f - 2f * k);
                _pos = Vector3.Lerp(home.position, windupSpot.position, k);
                if (t >= windupFrom) { SetState(2); if (windupSound != null) windupSound.Play(); }
                break;
            }
            case 2:
                // ぷるぷる（溜め）。だんだん速く・強く
                _pos = windupSpot.position;
                float w = Mathf.Clamp01((t - windupFrom) / Mathf.Max(0.1f, dashAt - windupFrom));
                wob = (0.015f + 0.03f * w) * Mathf.Sin(Time.time * (40f + 30f * w));
                squash = 0.08f * w;
                if (shotAt >= 0f && t >= shotAt)
                {
                    if (windupSound != null) windupSound.Stop();
                    if (blownSound != null) blownSound.Play();
                    Vector3 d = windupSpot.position - stand.nozzle.position; d.y = 0f;
                    _vel = (d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward) * pushSpeed;
                    _groundY = _pos.y; _vy = blownHop; _airborne = blownHop > 0f;
                    SetState(4);
                }
                else if (shotAt < 0f && t >= dashAt)
                {
                    if (windupSound != null) windupSound.Stop();
                    if (dashSound != null) dashSound.Play();
                    // 本物と同じ：ふわふわの少し先に着地するように跳ぶ。跳ねの高さは、ふわふわの所でその高さになるように
                    _dashFrom = _pos;
                    Vector3 d = ballPos - _pos; d.y = 0f;
                    float along = Mathf.Max(0.5f, d.magnitude);
                    _dashTo = _pos + (d.sqrMagnitude > 0.0001f ? d.normalized : transform.forward) * (along + ballOvershoot);
                    _dashTo.y = home.position.y;
                    _dashTime = Mathf.Max(0.2f, (along + ballOvershoot) / Mathf.Max(0.1f, dashSpeed));
                    float xb = Mathf.Clamp(along / (along + ballOvershoot), 0.2f, 0.9f);
                    float need = ballPos.y - _pos.y;
                    _dashHop = Mathf.Clamp(need / (4f * xb * (1f - xb)), hopHeight * 0.6f, maxHopHeight);
                    SetState(3);
                }
                break;
            case 3:
            {
                // 体当たり（ぴょんと跳んで、ふわふわの少し先に着地）。当たったらふわふわを吹っ飛ばす
                float k = Mathf.Clamp01((Time.time - _stateTime) / _dashTime);
                _pos = Vector3.Lerp(_dashFrom, _dashTo, k) + Vector3.up * (_dashHop * 4f * k * (1f - k));
                if (!_hit && stand.ball != null && (stand.ball.position - _pos).magnitude < ballHitRadius)
                {
                    _hit = true;
                    Vector3 dir = _dashTo - _dashFrom; dir.y = 0f; dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : transform.forward;
                    stand.KnockBall(dir * ballKnockback + Vector3.up * ballKnockUp, ballHitGlow);
                    if (hitSound != null) hitSound.Play();
                }
                if (k >= 1f) SetState(6);
                break;
            }
            case 4:
            {
                // 押された：ピョンと浮いて飛んで、着地してちょっと転がって止まる。目を回す（目を細めてくるくる）
                float dt = Time.deltaTime;
                if (_airborne)
                {
                    _vy -= blownGravity * dt;
                    _vel *= Mathf.Exp(-airDamping * dt);
                    _pos += (_vel + Vector3.up * _vy) * dt;
                    if (_vy < 0f && _pos.y <= _groundY)
                    {
                        _pos.y = _groundY; _airborne = false; _vel *= landKeep; _squashUntil = Time.time + 0.3f;
                    }
                }
                else
                {
                    _vel *= Mathf.Clamp01(1f - pushDamping * dt);
                    _pos += _vel * dt;
                }
                float u = Time.time - _stateTime;
                wob = 0.04f * Mathf.Sin(u * 18f) * Mathf.Clamp01(1f - u / stunTime);
                if (u >= stunTime) SetState(5);
                break;
            }
            case 5:
            {
                // 元の場所へゆっくり戻る
                Vector3 to = home.position - _pos; to.y = 0f;
                float step = returnSpeed * Time.deltaTime;
                if (to.magnitude <= step) _pos = new Vector3(home.position.x, _pos.y, home.position.z); else _pos += to.normalized * step;
                break;
            }
        }

        // 位置・ふわふわ浮き・ぷるぷる
        float bob = (_state == 3 || (_state == 4 && _airborne)) ? 0f : bobHeight * Mathf.Sin(Time.time * 3.1f);
        // 浮いている間は縦にのびて、着地でぷにっとつぶれる（本物と同じ）
        if (_state == 4 && _airborne) squash = -Mathf.Clamp(_vy / 4f, -0.3f, 1f) * 0.14f;
        else if (Time.time < _squashUntil) squash = Mathf.Sin(Mathf.Clamp01((_squashUntil - Time.time) / 0.3f) * Mathf.PI) * 0.3f;
        transform.position = _pos + Vector3.up * bob;
        // 向き：ふわふわの方（押されている間・戻る間は向きを変えない）
        if (_state <= 3)
        {
            Vector3 look = ballPos - transform.position; look.y = 0f;
            if (look.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look.normalized, Vector3.up), 1f - Mathf.Exp(-8f * Time.deltaTime));
        }
        // 出てくる時ふくらみ、1回分の終わりぎわに縮んで消える
        float appear = Mathf.Clamp01(t / 0.3f);
        float vanish = Mathf.Clamp01((len - t) / 0.35f);
        float sc = Mathf.Min(1f - (1f - appear) * (1f - appear), vanish);
        if (visual != null)
        {
            visual.localPosition = _visualBase + new Vector3(wob, 0f, 0f);
            visual.localScale = new Vector3(_visualScale.x * (1f + squash), _visualScale.y * (1f - squash), _visualScale.z * (1f + squash)) * Mathf.Max(0.001f, sc);
        }
        if (eyes != null)
        {
            bool dizzy = _state == 4;
            eyes.localPosition = _eyesBase + new Vector3(wob, 0f, 0f);
            Vector3 es = _eyesScale * Mathf.Max(0.001f, sc);
            if (dizzy) es.y *= 0.35f;   // 目を細める
            eyes.localScale = es;
            eyes.localRotation = dizzy ? Quaternion.Euler(0f, 0f, 25f * Mathf.Sin((Time.time - _stateTime) * 14f)) : Quaternion.identity;
        }
    }

    private void Begin()
    {
        _state = 0;
        _stateTime = Time.time;
        _hit = false;
        _vel = Vector3.zero;
        _airborne = false; _vy = 0f;
        _pos = home != null ? home.position : transform.position;
        if (windupSound != null) windupSound.Stop();
    }

    private void SetState(int s)
    {
        _state = s;
        _stateTime = Time.time;
    }
}
