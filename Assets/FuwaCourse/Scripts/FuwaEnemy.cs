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

    [Header("黒目（動く）")]
    [Tooltip("左右の黒目（白目の前の面に沿って動く）")]
    public Transform pupilL;
    public Transform pupilR;
    [Tooltip("黒目が中心から動ける距離（目のローカル）")]
    public float pupilReach = 0.036f;
    [Tooltip("白目の前の面のふくらみ（黒目を白目の面に沿わせる）")]
    public float pupilDepth = 0.04f;

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

    [Header("巣の中にかくれる（まもののす）")]
    [Tooltip("ONなら、ふだんは巣の床の下にかくれていて、近づくとぴょこっと出てくる")]
    public bool hideInNest = false;
    [Tooltip("この距離まで近づくと出てくる(m)")]
    public float popRange = 4.5f;
    public float hideDepth = 0.6f;

    [Header("寝てる（まもののす）")]
    [Tooltip("ONなら、ふだんは寝ている。走って近づく・エアガンの音・さわると起きる")]
    public bool sleeping = false;
    [Tooltip("この距離の中を走ると起きる(m)")]
    public float wakeRange = 3.5f;
    [Tooltip("この速さより速く動くと「走ってる」(m/s)")]
    public float sneakSpeed = 2.4f;
    [Tooltip("エアガンを撃つ音が聞こえる距離(m)")]
    public float hearRange = 6f;
    [Tooltip("寝てる間に出す「Zzz」")]
    public Transform sleepFx;
    [Tooltip("起きた時に出す「！」")]
    public Transform alertFx;
    public AudioSource wakeSound;

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

    // 0=うろうろ 1=溜め 2=突進 3=戻る 4=ひるみ 5=落下中 6=消えてる 7=かくれてる 8=出てくる 9=もぐる 10=寝てる 11=起きた（！）
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
        if (eyes != null) _eyesScale = eyes.localScale;
        InitPupils();
        if (sleepFx != null) _sleepFxBase = sleepFx.localPosition;
        if (alertFx != null) alertFx.gameObject.SetActive(false);
        EnterIdleMode();
    }

    private Vector3 _eyesScale = Vector3.one;
    private Vector3 _sleepFxBase;
    private float _farSince = -1f;

    // ふだんの状態（かくれてる／寝てる／うろうろ）に戻す
    private void EnterIdleMode()
    {
        if (hideInNest)
        {
            transform.position = _home + Vector3.down * hideDepth;
            SetVisible(false);
            SetState(7);
        }
        else if (sleeping)
        {
            transform.position = _home;
            SetState(10);
        }
        if (sleepFx != null) sleepFx.gameObject.SetActive(sleeping && !hideInNest);
        if (eyes != null) eyes.localScale = _eyesScale;
    }

    // エアガンを撃った音（FuwaBlower から）。寝てたら起きる
    public void Hear(Vector3 pos)
    {
        if (_state == 10 && (pos - transform.position).magnitude < hearRange) Wake();
    }

    private void Wake()
    {
        if (_state != 10) return;
        if (sleepFx != null) sleepFx.gameObject.SetActive(false);
        if (alertFx != null) alertFx.gameObject.SetActive(true);
        if (eyes != null) eyes.localScale = _eyesScale;
        if (wakeSound != null) wakeSound.Play();
        SetState(11);
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

        // 黒目：近くにいれば自分（プレイヤーの頭）を見る
        UpdatePupils(_state != 10 && toPlayer.magnitude < detectRange * 1.3f, local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position, dt);
        // 倒されたら、プレイヤーがやられる／リスポーンするまで復活しない（Revive()で戻る）
        if (_state == 6) return;

        if (_state == 7) // かくれてる：近づいたら出てくる
        {
            if (toPlayer.magnitude < popRange) { SetVisible(true); SetState(8); }
            return;
        }
        if (_state == 8) // 出てくる：床の下からぴょこっ（少し跳ねて着地）
        {
            float x = Mathf.Clamp01(_stateTime / 0.45f);
            transform.position = _home + Vector3.up * (-hideDepth * (1f - x) * (1f - x) + 0.45f * Mathf.Sin(x * Mathf.PI));
            if (visual != null)
            {
                float w = Mathf.Sin(x * Mathf.PI) * 0.3f;
                visual.localPosition = _visualBase;
                visual.localScale = new Vector3(_visualScale.x * (1f - w * 0.5f), _visualScale.y * (1f + w), _visualScale.z * (1f - w * 0.5f));
            }
            if (x >= 1f) { _cooldownUntil = Time.time + 0.35f; _farSince = -1f; SetState(0); }
            return;
        }
        if (_state == 9) // もぐる
        {
            float x = Mathf.Clamp01(_stateTime / 0.4f);
            transform.position = _home + Vector3.down * hideDepth * x * x;
            if (x >= 1f) { SetVisible(false); SetState(7); }
            return;
        }
        if (_state == 10) // 寝てる：すーすー。走って近づく・さわると起きる
        {
            transform.position = _home;
            Vector3 v = local.GetVelocity(); v.y = 0f;
            if (toPlayer.magnitude < wakeRange && v.magnitude > sneakSpeed) Wake();
            float br = Mathf.Sin(Time.time * 1.6f + _phase);
            if (visual != null)
            {
                visual.localPosition = _visualBase;
                visual.localScale = new Vector3(_visualScale.x * (1.08f + 0.04f * br), _visualScale.y * (0.84f - 0.04f * br), _visualScale.z * (1.08f + 0.04f * br));
            }
            if (eyes != null) eyes.localScale = new Vector3(_eyesScale.x, _eyesScale.y * 0.12f, _eyesScale.z);
            if (sleepFx != null)
            {
                float u = (Time.time * 0.5f + _phase) % 1f;
                sleepFx.localPosition = _sleepFxBase + new Vector3(0.08f * Mathf.Sin(u * 6.28f), 0.25f * u, 0f);
                sleepFx.localScale = Vector3.one * (0.6f + 0.6f * u) * (u < 0.85f ? 1f : (1f - u) / 0.15f);
            }
            return;
        }
        if (_state == 11) // 起きた：「！」でぴょんと跳ねてから溜めへ
        {
            float x = Mathf.Clamp01(_stateTime / 0.5f);
            transform.position = _home + Vector3.up * 0.35f * Mathf.Sin(x * Mathf.PI);
            if (alertFx != null) alertFx.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(x * Mathf.PI));
            if (x >= 1f)
            {
                if (alertFx != null) alertFx.gameObject.SetActive(false);
                _cooldownUntil = 0f;
                SetState(1);
            }
            return;
        }

        if (_state == 0) // うろうろ（地面を這う）
        {
            float t = Time.time * 0.6f + _phase;
            Vector3 target = _home + new Vector3(Mathf.Sin(t) * wanderRadius, 0f, Mathf.Cos(t * 0.8f) * wanderRadius * 0.5f);
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-3f * dt));
            if (toPlayer.magnitude < detectRange && Time.time > _cooldownUntil && HasGroundBelow()) SetState(1);
            // かくれる・寝る敵は、プレイヤーが離れてしばらくしたら元に戻る
            else if ((hideInNest || sleeping) && toPlayer.magnitude > Mathf.Max(popRange, wakeRange) * 1.6f)
            {
                if (_farSince < 0f) _farSince = Time.time;
                else if (Time.time - _farSince > 2.5f)
                {
                    _farSince = -1f;
                    if (hideInNest) SetState(9);
                    else EnterIdleMode();
                }
            }
            else _farSince = -1f;
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
        if (player == null || !player.isLocal || _state == 5 || _state == 6 || _state == 7) return;
        if (_state == 10) { Wake(); return; }
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
        if (alertFx != null) alertFx.gameObject.SetActive(false);
        EnterIdleMode();
    }

    private void SetVisible(bool on)
    {
        if (visual != null) visual.gameObject.SetActive(on);
        if (eyes != null) eyes.gameObject.SetActive(on);
    }

    public bool IsActive() { return _state != 5 && _state != 6 && _state != 7; }

    private Vector3 _pupilBaseL, _pupilBaseR;
    private Vector2 _look, _lookGoal;
    private float _nextGlance;

    private void InitPupils()
    {
        if (pupilL != null) _pupilBaseL = pupilL.localPosition;
        if (pupilR != null) _pupilBaseR = pupilR.localPosition;
    }

    // 黒目：見る相手がいればそっち、いなければときどき、きょろきょろ
    private void UpdatePupils(bool watch, Vector3 target, float dt)
    {
        if (pupilL == null || pupilR == null) return;
        Transform basis = pupilL.parent;
        if (watch && basis != null)
        {
            Vector3 d = basis.InverseTransformDirection(target - pupilL.position);
            Vector2 g = new Vector2(d.x, d.y) / Mathf.Max(0.05f, Mathf.Abs(d.z) + 0.3f);
            _lookGoal = Vector2.ClampMagnitude(g * 1.6f, 1f);
        }
        else if (Time.time > _nextGlance)
        {
            _lookGoal = Random.insideUnitCircle * 0.9f;
            _nextGlance = Time.time + Random.Range(0.7f, 2.0f);
        }
        _look = Vector2.Lerp(_look, _lookGoal, 1f - Mathf.Exp(-12f * dt));
        SetPupil(pupilL, _pupilBaseL);
        SetPupil(pupilR, _pupilBaseR);
    }

    private void SetPupil(Transform p, Vector3 b)
    {
        float r = Mathf.Clamp01(_look.magnitude);
        p.localPosition = b + new Vector3(_look.x * pupilReach, _look.y * pupilReach, -pupilDepth * (1f - Mathf.Sqrt(Mathf.Max(0f, 1f - r * r * 0.8f))));
    }
}
