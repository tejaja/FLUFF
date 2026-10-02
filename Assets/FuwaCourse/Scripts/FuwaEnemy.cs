using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// けだまの天敵。自分のPCだけで動く（自分を狙う敵は自分にしか見えない）

// うろうろ → 近づくと溜め（ぷるぷる）→ 突進。人に当たると吹っ飛ばし、けだまに当たるとアウト。
// エアガンで押し返せて、溜め中に当てるとひるむ。浮いてるので道の外に出しても落ちない。
// 倒せるのはトゲに当てた時だけ（しばらくして元の場所に復活）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaEnemy : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("見た目（溜め中に震わせる）")]
    public Transform visual;
    [Tooltip("目（プレイヤーの方を向く）")]
    public Transform eyes;

    [Header("うろうろ")]
    public float wanderRadius = 1.2f;
    public float bobHeight = 0.15f;

    [Header("突進")]
    public float detectRange = 7f;
    public float windupTime = 0.8f;
    public float dashSpeed = 7f;
    public float dashTime = 0.8f;
    public float cooldown = 1.6f;
    public float returnSpeed = 2f;
    [Tooltip("突進の時に跳ねる高さ(m)")]
    public float hopHeight = 1.0f;

    [Header("当たった時")]
    public float playerHitRadius = 0.75f;
    public float ballHitRadius = 0.5f;
    public float knockback = 7f;
    public float knockUp = 3f;
    [Tooltip("頭の上どこまで当たりにするか(m)")]
    public float hitAboveHead = 0.6f;
    [Tooltip("プレイヤーの何m先に着地するように跳ぶか")]
    public float dashOvershoot = 0.3f;
    [Tooltip("突進してない時に触れた時の跳ね返し")]
    public float bumpRadius = 0.5f;
    public float bumpPower = 3f;
    public float bumpUp = 1.8f;
    private float _bumpUntil;
    private float _squashUntil;

    [Header("エアガンで押された時")]
    public float pushScale = 1.6f;
    public float pushDamping = 3f;
    public float stunTime = 0.7f;

    [Header("落下・復活")]
    public float gravity = 12f;
    public float fallDepth = 8f;
    [Tooltip("足元の道を探すレイヤー（11=Environment）")]
    public int groundLayer = 11;

    [Tooltip("トゲで倒れた時に弾け飛ぶ毛（なくてもOK）")]
    public ParticleSystem popEffect;
    public AudioSource popSound;
    public AudioSource windupSound;
    public AudioSource dashSound;
    [Tooltip("突進が当たった時の音")]
    public AudioSource hitSound;
    [Tooltip("ポヨンの時の音")]
    public AudioSource bumpSound;
    [Tooltip("エアガンで撃たれた時の音（溜め撃ちで大きく）")]
    public AudioSource blownSound;
    [Tooltip("道から落ちる時の「ヒューン」")]
    public AudioSource fallSound;

    // 0=うろうろ 1=溜め 2=突進 3=戻る 4=ひるみ 5=落下中 6=消えてる
    private int _state;
    private float _stateTime;
    private float _cooldownUntil;
    private Vector3 _home;
    private Vector3 _vel;
    private Vector3 _dashDir;
    private float _phase;
    private Vector3 _visualBase;
    private Vector3 _visualScale = Vector3.one;
    private bool _hitThisDash;
    private float _groundY;
    private float _dashDuration = 0.8f;

    private void Start()
    {
        _home = transform.position;
        _groundY = _home.y;
        _phase = Random.Range(0f, 10f);
        if (visual != null)
        {
            _visualBase = visual.localPosition;
            _visualScale = visual.localScale;
        }
    }

    private void SetState(int s)
    {
        // 溜めをやめた（プレイヤーが離れた・撃たれた）時は溜め音も止める
        if (_state == 1 && s != 1 && windupSound != null) windupSound.Stop();
        if (s == 5 && _state != 5 && fallSound != null) fallSound.Play();
        _state = s;
        _stateTime = 0f;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _stateTime += dt;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null || !local.IsValid()) return;
        Vector3 chest = local.GetPosition() + Vector3.up * 1.0f;
        Vector3 toPlayer = chest - transform.position;

        // 倒されたら、プレイヤーがやられる／リスポーンするまで復活しない（Revive()で戻る）
        if (_state == 6) return;

        if (_state == 0) // うろうろ（地面を這う）
        {
            float t = Time.time * 0.6f + _phase;
            Vector3 target = _home + new Vector3(Mathf.Sin(t) * wanderRadius, 0f, Mathf.Cos(t * 0.8f) * wanderRadius * 0.5f);
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-3f * dt));
            if (toPlayer.magnitude < detectRange && Time.time > _cooldownUntil && HasGroundBelow()) SetState(1);
        }
        else if (_state == 1) // 溜め
        {
            if (toPlayer.magnitude > detectRange * 1.3f) { SetState(0); }
            else if (_stateTime > windupTime)
            {
                Vector3 d = local.GetPosition() - transform.position; d.y = 0f;
                _dashDir = d.sqrMagnitude > 0.001f ? d.normalized : transform.forward;
                // プレイヤーまでの距離に合わせて跳ぶ長さを決める（少し行き過ぎるくらい）
                _dashDuration = Mathf.Clamp((d.magnitude + dashOvershoot) / dashSpeed, 0.3f, dashTime);
                _groundY = transform.position.y;
                _hitThisDash = false;
                if (dashSound != null) dashSound.Play();
                SetState(2);
            }
            else if (_stateTime < dt * 1.5f && windupSound != null) windupSound.Play();
        }
        else if (_state == 2) // 突進（ぴょーんと跳ねて飛びかかる）
        {
            Vector3 p = transform.position + _dashDir * dashSpeed * dt;
            float x = Mathf.Clamp01(_stateTime / _dashDuration);
            p.y = _groundY + hopHeight * 4f * x * (1f - x);
            transform.position = p;
            if (!_hitThisDash) CheckHits(local);
            if (_stateTime > _dashDuration)
            {
                // 着地先に道がなければ落ちる
                if (!HasGroundBelow()) { _vel = _dashDir * dashSpeed * 0.3f; SetState(5); }
                else { _cooldownUntil = Time.time + cooldown; SetState(3); }
            }
        }
        else if (_state == 3) // 戻る
        {
            transform.position = Vector3.MoveTowards(transform.position, _home, returnSpeed * dt);
            if ((transform.position - _home).sqrMagnitude < 0.04f) SetState(0);
        }
        else if (_state == 4) // ひるみ（押されて滑る）
        {
            Vector3 p = transform.position + _vel * dt;
            p.y = Mathf.MoveTowards(p.y, _groundY, 4f * dt);
            transform.position = p;
            _vel *= Mathf.Exp(-pushDamping * dt);
            // 道の外に押し出されたら落ちる（しばらくして復活）
            if (!HasGroundBelow()) SetState(5);
            else if (_stateTime > stunTime) { _cooldownUntil = Time.time + cooldown * 0.5f; SetState(3); }
        }
        else if (_state == 5) // 落下
        {
            _vel += Vector3.down * gravity * dt;
            transform.position += _vel * dt;
            if (transform.position.y < _home.y - fallDepth) { SetVisible(false); SetState(6); }
        }

        // 突進してない時も、触れたら「ポヨン」と軽く跳ね返す
        if (_state != 2 && _state != 5 && _state != 6 && Time.time > _bumpUntil)
        {
            Vector3 feet = local.GetPosition();
            float headY = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position.y - feet.y;
            if (headY < 0.5f) headY = 1.2f;
            Vector3 closest = feet + Vector3.up * Mathf.Clamp(transform.position.y - feet.y, 0f, headY);
            if ((closest - transform.position).magnitude < bumpRadius) Bump(local);
        }

        // 見た目：プレイヤーの方を向く、溜め中は震える
        if (toPlayer.sqrMagnitude > 0.01f && _state != 5)
        {
            Vector3 flat = toPlayer; flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 1f - Mathf.Exp(-8f * dt));
        }
        if (visual != null)
        {
            if (Time.time < _squashUntil)
            {
                // ポヨンと跳ね返した時：ぷにっとつぶれる
                float s = Mathf.Clamp01((_squashUntil - Time.time) / 0.35f);
                float w = Mathf.Sin(s * Mathf.PI) * 0.45f;
                visual.localPosition = _visualBase;
                visual.localScale = new Vector3(_visualScale.x * (1f + w), _visualScale.y * (1f - w), _visualScale.z * (1f + w));
            }
            else if (_state == 1)
            {
                // 溜め：ぷるぷる震えながら、つぶれて力をためる
                float k = _stateTime / windupTime;
                visual.localPosition = _visualBase + Random.insideUnitSphere * 0.025f * (0.5f + k);
                visual.localScale = new Vector3(_visualScale.x * (1f + 0.15f * k), _visualScale.y * (1f - 0.25f * k), _visualScale.z * (1f + 0.15f * k));
            }
            else if (_state == 0 || _state == 3)
            {
                // 這ってる：のびちぢみ
                float w = Mathf.Sin(Time.time * 9f + _phase) * 0.08f;
                visual.localPosition = _visualBase;
                visual.localScale = new Vector3(_visualScale.x * (1f - w * 0.5f), _visualScale.y * (1f + w), _visualScale.z * (1f - w * 0.5f));
            }
            else
            {
                visual.localPosition = _visualBase;
                visual.localScale = _visualScale;
            }
        }
    }

    private void CheckHits(VRCPlayerApi local)
    {
        Vector3 p = transform.position;
        Vector3 feet = local.GetPosition();
        // 人：縦長の円柱で判定（横の距離＋足元の少し下〜頭の上まで）。上を飛び越えても当たる
        float headY = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position.y - feet.y;
        if (headY < 0.5f) headY = 1.2f;
        Vector3 flat = p - feet; float dy = flat.y; flat.y = 0f;
        if (flat.magnitude < playerHitRadius && dy > -0.3f && dy < headY + hitAboveHead) DashHit(local);
    }

    private void DashHit(VRCPlayerApi local)
    {
        if (_hitThisDash) return;
        local.SetVelocity(_dashDir * knockback + Vector3.up * knockUp);
        _hitThisDash = true;
        _squashUntil = Time.time + 0.35f;
        if (hitSound != null) hitSound.Play();
    }

    private void Bump(VRCPlayerApi local)
    {
        if (Time.time < _bumpUntil) return;
        Vector3 away = local.GetPosition() - transform.position; away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
        local.SetVelocity(away * bumpPower + Vector3.up * bumpUp);
        _bumpUntil = Time.time + 0.4f;
        _squashUntil = Time.time + 0.25f;
        if (bumpSound != null) bumpSound.Play();
    }

    // VRChat側のプレイヤーの当たり判定（体のカプセル）が触れた時。アバターの大きさに関係なく確実に反応する
    public override void OnPlayerTriggerEnter(VRCPlayerApi player) { OnPlayerTouch(player); }
    public override void OnPlayerTriggerStay(VRCPlayerApi player) { OnPlayerTouch(player); }

    private void OnPlayerTouch(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || _state == 5 || _state == 6) return;
        if (_state == 2) DashHit(player);
        else Bump(player);
    }

    // トゲに当たったら倒れる（消えて、しばらくしたら復活）
    // うろうろ・戻り中にトゲに触れても倒れない（放置で勝手に自滅しないように）。
    // 押されて滑っている時と、突進中だけ倒れる。トゲの上で押されても倒れるようにStayで見る
    private void OnTriggerStay(Collider other)
    {
        if (other == null || (_state != 2 && _state != 4)) return;
        if (other.GetComponent<FuwaSpike>() == null) return;
        if (popEffect != null)
        {
            popEffect.transform.position = transform.position + Vector3.up * 0.15f;
            popEffect.Play();
        }
        if (popSound != null) popSound.Play();
        SetVisible(false);
        SetState(6);
    }

    // エアガンから呼ばれる。stun=false（溜めが足りない）なら、突進は止まらず少しずれるだけ
    public void Blown(Vector3 impulse, bool stun)
    {
        if (_state == 5 || _state == 6) return;
        if (blownSound != null && blownSound.clip != null)
        {
            // 強く押されたほど（溜め撃ち・近い）低く大きく
            float k = Mathf.Clamp01(impulse.magnitude / 3.4f);
            blownSound.pitch = Mathf.Lerp(1.2f, 0.85f, k) * Random.Range(0.95f, 1.05f);
            blownSound.PlayOneShot(blownSound.clip, Mathf.Lerp(0.45f, 1f, k));
        }
        if (!stun)
        {
            if (_state == 2) return;
            Vector3 nudge = impulse * pushScale * 0.15f; nudge.y = 0f;
            transform.position += nudge;
            return;
        }
        _vel = impulse * pushScale;
        _vel.y = 0f;
        SetState(4);
    }

    private bool HasGroundBelow()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 6f, 1 << groundLayer);
    }

    // 倒されていたら元の場所で復活する（プレイヤーが落ちた・トゲ・リスポーン・コース開始時に呼ばれる）
    public void Revive()
    {
        if (_state == 5 || _state == 6) Respawn();
    }

    private void Respawn()
    {
        transform.position = _home;
        _vel = Vector3.zero;
        _cooldownUntil = Time.time + cooldown;
        SetVisible(true);
        SetState(0);
    }

    private void SetVisible(bool on)
    {
        if (visual != null) visual.gameObject.SetActive(on);
        if (eyes != null) eyes.gameObject.SetActive(on);
    }

    public bool IsActive() { return _state != 5 && _state != 6; }
}
