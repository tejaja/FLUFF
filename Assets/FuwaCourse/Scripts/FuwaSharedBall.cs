using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;

// 「みんなで」ロビーの、全員で同じものを吹いて遊ぶふわふわ。
// 吹いた人がその場で持ち主になり、撃った瞬間の「位置・速さ・時刻」を全員に送る。
// あとはそれぞれの画面で同じ決まりで動かし（なめらか）、持ち主がときどき送る位置へそっと寄せる。
// 最初は真ん中のキノコの上で浮いて待っていて、誰かが吹くと飛び出す。床に落ちたら弾けて、キノコの上に戻る。
// （VRCObjectSync は使わない。同期モードは Manual）
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaSharedBall : UdonSharpBehaviour
{
    [Header("ふわふわの動き（FuwaBall と同じ考え方）")]
    public float fallAcceleration = 1.5f;
    public float maxFallSpeed = 0.7f;
    public float maxSpeed = 7f;
    public float airDrag = 1.2f;
    public float spinPerPuff = 4f;
    public float spinRandom = 1.5f;
    public float spinDamping = 0.6f;
    [Tooltip("待機中にゆっくり回る速さ(度/秒)")]
    public float idleSpin = 25f;

    [Header("落ちたら")]
    [Tooltip("このレイヤーの物（ロビーの床など）に触れたら弾けて戻る")]
    public LayerMask popLayers = 1;
    [Tooltip("弾けてから戻るまでの秒数")]
    public float respawnDelay = 1.2f;
    [Tooltip("待機位置からこれだけ下に落ちたら戻す(m)")]
    public float fallLimit = 6f;

    [Header("キノコから飛び出す（待っている間はキノコの中に隠れている）")]
    [Tooltip("傘を撃って飛び出す時の、撃った向きへの速さ")]
    public float releasePush = 2.2f;
    [Tooltip("傘を撃って飛び出す時の、上への速さ")]
    public float releaseUp = 2.0f;

    [Header("見た目（なくてもOK）")]
    public ParticleSystem popEffect;
    public ParticleSystem shotPuff;
    public AudioSource sfxPop;
    public AudioSource sfxBoing;
    public Renderer furRenderer;
    public float furDragAmount = 0.025f;
    public float furDragMax = 0.12f;

    [Header("撃たれた時のポヨンと綿毛（全員の画面で、FuwaBall と同じ）")]
    public float squashAmount = 0.4f;
    [Tooltip("この速度変化で最大の潰れ・綿毛の数になる")]
    public float squashFullAt = 6f;
    public float squashFrequency = 2.6f;
    public float squashDamping = 2.6f;
    public int shotPuffMin = 5;
    public int shotPuffMax = 16;
    public float boingPitchWeak = 1.35f;
    public float boingPitchStrong = 0.8f;
    public float boingVolumeWeak = 0.35f;
    public float boingVolumeStrong = 1f;
    [Tooltip("この速さ以上に急に向きが変わったら「ぶつかった」とみなしてポヨン（それぞれの画面で）")]
    public float bumpDetect = 2.2f;
    [Tooltip("撃った風の当たり判定を、玉の中心からこの分だけ風の中心線側に広げる(m)。玉を大きくした分のゆとり")]
    public float hitPadding = 0f;
    private float _squashStart = -10f;
    private float _squashPower;
    private bool _squashActive;
    private Vector3 _prevVel;
    private float _bumpCooldown;

    [Header("同期（全員の画面でそれぞれ同じ計算をして、持ち主の位置にそっと寄せる）")]
    [Tooltip("飛んでいる間、持ち主が今の位置と速さを送る間隔(秒)")]
    public float correctionInterval = 0.35f;
    [Tooltip("ズレを寄せる速さ（大きいほど早く寄る）")]
    public float correctionRate = 5f;
    [Tooltip("ズレがこれ以上なら寄せずにその場へ移す(m)")]
    public float snapDistance = 2.5f;

    [UdonSynced] private bool _waiting = true;
    [UdonSynced] private bool _popped;
    [UdonSynced] private Vector3 _sPos;
    [UdonSynced] private Vector3 _sVel;
    [UdonSynced] private Vector3 _sAng;
    [UdonSynced] private double _sTime;
    [UdonSynced] private int _sShot;

    private Rigidbody _rb;
    private Vector3 _homePos;
    private Quaternion _homeRot;
    private float _respawnAt = -1f;
    private Material _furMat;
    private Vector3 _furDrag;
    private bool _shownHidden;
    private Collider _col;
    private bool _shownWaiting = true;
    private Renderer _selfRenderer;
    private TrailRenderer _trail;
    private int _seenShot;
    private Vector3 _posErr;        // 持ち主の位置とのズレ。少しずつ寄せていく
    private Vector3 _predVel;
    private float _nextSend;
    private bool _sendSoon;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _selfRenderer = GetComponent<Renderer>();
        _trail = GetComponent<TrailRenderer>();
        _col = GetComponent<Collider>();
        if (furRenderer != null) _furMat = furRenderer.material;
        _rb.useGravity = false;
        _rb.drag = airDrag;
        _rb.angularDrag = spinDamping;
        _rb.maxAngularVelocity = 12f;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _homePos = transform.position;
        _homeRot = transform.rotation;
        _rb.isKinematic = true;
        if (Networking.IsOwner(gameObject)) SetWaiting();
        ApplyHiddenVisual();
    }

    public bool IsWaiting() { return _waiting && !_popped; }

    // 持ち主：キノコの上に戻して、吹かれるまで止めて待つ
    private void SetWaiting()
    {
        _waiting = true;
        _popped = false;
        _respawnAt = -1f;
        GoHome();
        RequestSerialization();
        ApplyHiddenVisual();
    }

    private void GoHome()
    {
        _rb.isKinematic = true;
        _rb.position = _homePos;
        _rb.rotation = _homeRot;
        transform.SetPositionAndRotation(_homePos, _homeRot);
        _posErr = Vector3.zero;
        _shownWaiting = true;
        if (_trail != null) _trail.Clear();
    }

    // エアガンから呼ばれる（吹いた人の画面で）。その人が持ち主になって、自分の画面ではすぐ動かす
    public void BlowShared(Vector3 velocityChange)
    {
        if (_popped || _waiting) return;   // キノコに隠れている間は、傘を撃たないと出てこない
        Launch(velocityChange);
    }

    // スタートのキノコの傘を撃った人の画面で呼ばれる：撃った向きへポンと飛び出す
    public void ReleaseFromMushroom(Vector3 shotDir)
    {
        if (_popped || !_waiting) return;
        Vector3 flat = new Vector3(shotDir.x, 0f, shotDir.z);
        flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.zero;
        Launch(flat * releasePush + Vector3.up * releaseUp);
    }

    private void Launch(Vector3 velocityChange)
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        Vector3 v = _rb.isKinematic ? Vector3.zero : _rb.velocity;
        if (_waiting)
        {
            _waiting = false;
            v = Vector3.zero;
        }
        _shownWaiting = false;
        _rb.isKinematic = false;
        // 落ちてる最中に下から吹かれたら、いったん落下を止めてから持ち上げる（FuwaBall と同じ）
        if (v.y < 0f && velocityChange.y > 0f) v.y = 0f;
        _rb.velocity = v + velocityChange;
        Vector3 flat = new Vector3(velocityChange.x, 0f, velocityChange.z);
        Vector3 axis = flat.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.zero;
        _rb.angularVelocity = axis * spinPerPuff + Random.insideUnitSphere * spinRandom;
        _posErr = Vector3.zero;   // 自分が撃った玉は、自分の見ている位置が正解
        _sShot++;
        _seenShot = _sShot;
        SendState();
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(PlayPuff), Mathf.Clamp01(velocityChange.magnitude / Mathf.Max(0.01f, squashFullAt)));
    }

    // 持ち主：今の位置・速さ・時刻を送る（中身は OnPreSerialization で詰める）
    private void SendState()
    {
        _nextSend = Time.time + correctionInterval;
        _sendSoon = false;
        RequestSerialization();
    }

    public override void OnPreSerialization()
    {
        _sPos = _rb.position;
        _sVel = _rb.isKinematic ? Vector3.zero : _rb.velocity;
        _sAng = _rb.isKinematic ? Vector3.zero : _rb.angularVelocity;
        _sTime = Networking.GetServerTimeInSeconds();
    }

    // 送られた時刻から今までの動きを、ふわふわの決まり（落ちる速さ・空気抵抗・上限）で先回りして計算する
    private Vector3 Predict(Vector3 pos, Vector3 vel, float t)
    {
        t = Mathf.Clamp(t, 0f, 1.5f);
        while (t > 0f)
        {
            float dt = Mathf.Min(0.02f, t);
            vel += Vector3.down * fallAcceleration * dt;
            vel *= Mathf.Clamp01(1f - airDrag * dt);
            if (vel.y < -maxFallSpeed) vel.y = -maxFallSpeed;
            if (vel.magnitude > maxSpeed) vel = vel.normalized * maxSpeed;
            pos += vel * dt;
            t -= dt;
        }
        _predVel = vel;
        return pos;
    }

    public override void OnDeserialization()
    {
        if (Networking.IsOwner(gameObject)) return;
        if (_popped)
        {
            _rb.isKinematic = true;
        }
        else if (_waiting)
        {
            if (!_shownWaiting) GoHome();
        }
        else
        {
            float late = (float)(Networking.GetServerTimeInSeconds() - _sTime);
            Vector3 target = Predict(_sPos, _sVel, late);
            bool newShot = _sShot != _seenShot;
            _seenShot = _sShot;
            if (_shownWaiting || _rb.isKinematic)
            {
                // 待っていた玉が飛び出した：その場へ置いて動かし始める
                _shownWaiting = false;
                _rb.isKinematic = false;
                _rb.position = target;
                transform.position = target;
                _posErr = Vector3.zero;
            }
            else
            {
                Vector3 err = target - _rb.position;
                if (err.magnitude > snapDistance) { _rb.position = target; transform.position = target; _posErr = Vector3.zero; }
                else _posErr = err;
            }
            _rb.velocity = _predVel;
            if (newShot) _rb.angularVelocity = _sAng;
        }
        if (_shownHidden != (_popped || _waiting)) ApplyHiddenVisual();
    }

    // 撃たれた時（全員）：強さ k(0〜1) に合わせて、綿毛を散らして、ボヨンと鳴らして、ポヨンとつぶれる
    [NetworkCallable]
    public void PlayPuff(float k)
    {
        if (shotPuff != null) shotPuff.Emit(Mathf.RoundToInt(Mathf.Lerp(shotPuffMin, shotPuffMax, k)));
        if (sfxBoing != null && sfxBoing.clip != null)
        {
            sfxBoing.pitch = Mathf.Lerp(boingPitchWeak, boingPitchStrong, k) * Random.Range(0.96f, 1.04f);
            sfxBoing.PlayOneShot(sfxBoing.clip, Mathf.Lerp(boingVolumeWeak, boingVolumeStrong, k));
        }
        StartSquash(k);
    }

    private void StartSquash(float k)
    {
        float p = squashAmount * (0.5f + 0.5f * Mathf.Clamp01(k));
        float left = _squashActive ? _squashPower * Mathf.Exp(-squashDamping * (Time.time - _squashStart)) : 0f;
        if (p < left) return;
        _squashPower = p;
        _squashStart = Time.time;
        _squashActive = true;
    }

    private void UpdateSquash()
    {
        if (!_squashActive || _furMat == null) return;
        float t = Time.time - _squashStart;
        float env = Mathf.Exp(-squashDamping * t);
        if (env < 0.01f)
        {
            _squashActive = false;
            _furMat.SetVector("_Squash", Vector4.zero);
            return;
        }
        float w = _squashPower * env * Mathf.Cos(t * squashFrequency * 2f * Mathf.PI);
        _furMat.SetVector("_Squash", new Vector4(0f, 1f, 0f, w));
    }

    public void PlayPop()
    {
        if (popEffect != null) { popEffect.transform.position = transform.position; popEffect.Play(); }
        if (sfxPop != null) sfxPop.Play();
    }

    private void FixedUpdate()
    {
        if (_waiting || _popped || _rb.isKinematic) return;
        // 全員の画面で同じ決まりで動かす（持ち主でなくても）
        _rb.AddForce(Vector3.down * fallAcceleration, ForceMode.Acceleration);
        Vector3 v = _rb.velocity;
        if (v.y < -maxFallSpeed) v.y = -maxFallSpeed;
        if (v.magnitude > maxSpeed) v = v.normalized * maxSpeed;
        _rb.velocity = v;
        // 持ち主とのズレを少しずつ寄せる
        if (_posErr.sqrMagnitude > 0.000001f)
        {
            Vector3 d = _posErr * (1f - Mathf.Exp(-correctionRate * Time.fixedDeltaTime));
            _rb.position += d;
            _posErr -= d;
        }
    }

    private void Update()
    {
        bool owner = Networking.IsOwner(gameObject);
        Vector3 vel = _rb.isKinematic ? Vector3.zero : _rb.velocity;

        if (_waiting && _shownWaiting) transform.Rotate(new Vector3(0.3f, 1f, 0.15f), idleSpin * Time.deltaTime, Space.World);
        if (owner)
        {
            if (!_waiting && !_popped && transform.position.y < _homePos.y - fallLimit) SetWaiting();
            if (_popped && _respawnAt > 0f && Time.time >= _respawnAt) SetWaiting();
            // 飛んでいる間はときどき今の位置を送る（壁にぶつかった後はすぐ送る）
            if (!_waiting && !_popped && (_sendSoon || Time.time >= _nextSend)) SendState();
        }
        if (_shownHidden != (_popped || _waiting)) ApplyHiddenVisual();
        if (_trail != null) _trail.emitting = !_popped && !_waiting;

        // 壁や柵にぶつかった時のポヨン：急に速さが変わったのを、それぞれの画面で見て鳴らさずにつぶすだけ
        if (!_waiting && !_popped && Time.time > _bumpCooldown && (vel - _prevVel).magnitude > bumpDetect && _prevVel.magnitude > 1f)
        {
            StartSquash(Mathf.Clamp01((vel - _prevVel).magnitude / Mathf.Max(0.01f, squashFullAt)) * 0.7f);
            _bumpCooldown = Time.time + 0.25f;
        }
        _prevVel = vel;
        UpdateSquash();

        if (_furMat != null)
        {
            Vector3 target = _waiting ? Vector3.zero : Vector3.ClampMagnitude(-vel * furDragAmount, furDragMax);
            _furDrag = Vector3.Lerp(_furDrag, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            _furMat.SetVector("_Drag", new Vector4(_furDrag.x, _furDrag.y, _furDrag.z, 0f));
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!Networking.IsOwner(gameObject) || _waiting || _popped || collision == null || collision.collider == null) return;
        if ((popLayers.value & (1 << collision.collider.gameObject.layer)) == 0)
        {
            _sendSoon = true;   // 壁や柵で跳ね返った：みんなの画面のズレを早めに直す
            return;
        }
        // 床に落ちたかどうかは持ち主の画面で決める
        _popped = true;
        _respawnAt = Time.time + respawnDelay;
        _rb.isKinematic = true;
        RequestSerialization();
        ApplyHiddenVisual();
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(PlayPop));
    }

    // 弾けている間とキノコに隠れている間は見えなくする（全員：同期された _popped / _waiting で）
    private void ApplyHiddenVisual()
    {
        _shownHidden = _popped || _waiting;
        if (_selfRenderer != null) _selfRenderer.enabled = !_shownHidden;
        if (furRenderer != null) furRenderer.enabled = !_shownHidden;
        if (_col != null) _col.enabled = !_shownHidden;
        if (_trail != null) _trail.Clear();
        if (_shownHidden && shotPuff != null) shotPuff.Clear();
    }
}
