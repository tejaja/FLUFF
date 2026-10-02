using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.Udon.Common.Interfaces;

// 「みんなで」ロビーの、全員で同じものを吹いて遊ぶふわふわ。
// 吹いた人がその場で持ち主になって物理を動かし、位置・回転は VRCObjectSync で全員に届く。
// 最初は真ん中のキノコの上で浮いて待っていて、誰かが吹くと飛び出す。床に落ちたら弾けて、キノコの上に戻る。
// （VRCObjectSync と同じ物に付けるので、同期モードは Continuous）
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
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

    [Header("見た目（なくてもOK）")]
    public ParticleSystem popEffect;
    public ParticleSystem shotPuff;
    public AudioSource sfxPop;
    public AudioSource sfxBoing;
    public Renderer furRenderer;
    public float furDragAmount = 0.025f;
    public float furDragMax = 0.12f;

    [UdonSynced] private bool _waiting = true;
    [UdonSynced] private bool _popped;

    private Rigidbody _rb;
    private VRCObjectSync _sync;
    private Vector3 _homePos;
    private Quaternion _homeRot;
    private float _respawnAt = -1f;
    private Vector3 _lastPos;
    private Vector3 _estVel;      // 持ち主でない時、届いた位置から見積もった速度（持ち主を引き継いだ瞬間に使う）
    private Material _furMat;
    private Vector3 _furDrag;
    private bool _shownPopped;
    private Renderer _selfRenderer;
    private TrailRenderer _trail;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        _selfRenderer = GetComponent<Renderer>();
        _trail = GetComponent<TrailRenderer>();
        if (furRenderer != null) _furMat = furRenderer.material;
        _rb.useGravity = false;
        _rb.drag = airDrag;
        _rb.angularDrag = spinDamping;
        _rb.maxAngularVelocity = 12f;
        _homePos = transform.position;
        _homeRot = transform.rotation;
        _lastPos = transform.position;
        if (Networking.IsOwner(gameObject)) SetWaiting();
    }

    // 持ち主：キノコの上に戻して、吹かれるまで止めて待つ
    private void SetWaiting()
    {
        _waiting = true;
        _popped = false;
        _respawnAt = -1f;
        if (_sync != null) { _sync.FlagDiscontinuity(); _sync.SetKinematic(true); }
        _rb.isKinematic = true;
        transform.SetPositionAndRotation(_homePos, _homeRot);
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        RequestSerialization();
        ApplyPoppedVisual();
    }

    // エアガンから呼ばれる（吹いた人の画面で）。その人が持ち主になって、すぐ動かす
    public void BlowShared(Vector3 velocityChange)
    {
        if (_popped) return;
        Vector3 v = _estVel;
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        else v = _rb.isKinematic ? Vector3.zero : _rb.velocity;
        if (_waiting)
        {
            _waiting = false;
            v = Vector3.zero;
        }
        if (_sync != null) _sync.SetKinematic(false);
        _rb.isKinematic = false;
        // 落ちてる最中に下から吹かれたら、いったん落下を止めてから持ち上げる（FuwaBall と同じ）
        if (v.y < 0f && velocityChange.y > 0f) v.y = 0f;
        _rb.velocity = v + velocityChange;
        Vector3 flat = new Vector3(velocityChange.x, 0f, velocityChange.z);
        Vector3 axis = flat.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, flat.normalized) : Vector3.zero;
        _rb.angularVelocity = axis * spinPerPuff + Random.insideUnitSphere * spinRandom;
        RequestSerialization();
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(PlayPuff));
    }

    public void PlayPuff()
    {
        if (shotPuff != null) shotPuff.Play();
        if (sfxBoing != null) sfxBoing.Play();
    }

    public void PlayPop()
    {
        if (popEffect != null) { popEffect.transform.position = transform.position; popEffect.Play(); }
        if (sfxPop != null) sfxPop.Play();
    }

    private void FixedUpdate()
    {
        if (!Networking.IsOwner(gameObject) || _waiting || _popped || _rb.isKinematic) return;
        _rb.AddForce(Vector3.down * fallAcceleration, ForceMode.Acceleration);
        Vector3 v = _rb.velocity;
        if (v.y < -maxFallSpeed) v.y = -maxFallSpeed;
        if (v.magnitude > maxSpeed) v = v.normalized * maxSpeed;
        _rb.velocity = v;
    }

    private void Update()
    {
        bool owner = Networking.IsOwner(gameObject);
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        if (!owner) _estVel = Vector3.Lerp(_estVel, (transform.position - _lastPos) / dt, 1f - Mathf.Exp(-10f * dt));
        Vector3 vel = owner && !_rb.isKinematic ? _rb.velocity : _estVel;
        _lastPos = transform.position;

        if (owner)
        {
            if (_waiting) transform.Rotate(new Vector3(0.3f, 1f, 0.15f), idleSpin * Time.deltaTime, Space.World);
            if (!_waiting && !_popped && transform.position.y < _homePos.y - fallLimit) SetWaiting();
            if (_popped && _respawnAt > 0f && Time.time >= _respawnAt) SetWaiting();
        }
        if (_shownPopped != _popped) ApplyPoppedVisual();
        if (_trail != null) _trail.emitting = !_popped && !_waiting;

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
        if ((popLayers.value & (1 << collision.collider.gameObject.layer)) == 0) return;
        _popped = true;
        _respawnAt = Time.time + respawnDelay;
        if (_sync != null) _sync.SetKinematic(true);
        _rb.isKinematic = true;
        RequestSerialization();
        ApplyPoppedVisual();
        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(PlayPop));
    }

    // 弾けている間は見えなくする（全員：同期された _popped で）
    private void ApplyPoppedVisual()
    {
        _shownPopped = _popped;
        if (_selfRenderer != null) _selfRenderer.enabled = !_popped;
        if (_trail != null) _trail.Clear();
    }

    public override void OnDeserialization()
    {
        if (_shownPopped != _popped) ApplyPoppedVisual();
    }
}
