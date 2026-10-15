using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;

// 「みんなで」ロビーの、全員で同じものを吹いて遊ぶふわふわ。
// 持ち主は1人（撃っても交代しない。ただし持ち主がロビーにいない＝状態が届かなくなったら、ロビーにいる人が引き継ぐ）。
// 持ち主の画面の動きが正解で、持ち主が「位置・速さ・時刻」をみんなに送る。
// 持ち主でない人が撃った時は、自分の画面ではすぐ動かし（遅れなし）、同時に「この強さで撃った」を持ち主に送る。
// 持ち主は届いた撃ちを自分の玉に反映して配り直す。撃った本人の画面は、持ち主がその撃ちを反映した状態が届くまでは自分の予測を優先する。
// （以前は撃った人が持ち主になっていたが、2人で連打すると持ち主が入れ替わり続けて、画面ごとの位置がずれたまま戻らなかった）
// あとはそれぞれの画面で同じ決まりで動かし（なめらか）、持ち主がときどき送る位置へそっと寄せる。
// 最初は真ん中のキノコの上で浮いて待っていて、誰かが吹くと飛び出す。床に落ちたら弾けて、キノコの上に戻る。
// （VRCObjectSync は使わない。同期モードは Manual）
// あそびばの玉は FuwaSystem の下に置く（エリアごと隠れる物の中に置くと、隠れている間の同期を取りこぼし、入り直した人に一部の玉が見えなかった。2026-10-14 てじゃ）。
// 自分がそのエリア（areas / myArea）にいない間は、自分の画面では止めて隠す（床が消えるので落ちないように）。持ち主がいない間は「いない」を送り、エリアにいる人が引き継ぐ
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

    [Header("柵の上で止まらない（FuwaBall と同じ。2026-10-12 てじゃ「一応つけとこ」）")]
    [Tooltip("名前にこれを含む物（FenceCollider など）の上に乗って止まったら、まわりの一番高い床の方へ転がす")]
    public string perchName = "Fence";
    public float perchDelay = 0.35f;
    public float perchPush = 0.9f;
    public float perchProbe = 0.8f;
    private float _perchSeen = -1f, _perchStart = -1f;

    [Header("撃った直後は風の効きが弱まる（FuwaBall と同じ。つむじ風から抜けやすく）")]
    [Range(0f, 1f)] public float shotWindDampMin = 0.35f;
    [Range(0f, 1f)] public float shotWindDampMax = 0.85f;
    public float shotWindDampHoldMax = 0.4f;
    public float shotWindDampTimeMin = 0.3f;
    public float shotWindDampTimeMax = 0.55f;

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
    [Tooltip("毛オフ（軽量）の時の材質（FuwaCourse/FurPlain）。あそびばの設定スイッチで切り替え（FuwaPlaygroundSpawner → SetLook）。なければ毛オフなし")]
    public Material plainMaterial;
    [Tooltip("毛オフの時の球のメッシュ（毛ありの玉のメッシュは粗くて、毛がないと輪郭がカクカクに見えるので細かい球に替える）。なければそのまま")]
    public Mesh plainMesh;
    [Tooltip("撃たれた時に顔が 😣（ぎゅっと閉じた目＋への字口）になっている秒数（顔を出している時だけ見える）")]
    public float hitFaceTime = 0.6f;
    [Tooltip("遠くの玉は自動で毛オフ：自分の頭からの距離 ÷ 玉の直径 がこれを超えたら毛なし（0.3mの玉なら 27×0.3＝約8m。でっかい玉ほど遠くまで毛あり）。0=自動なし（2026-10-15 てじゃ「距離で自動切りかえ」）")]
    public float lodRatio = 27f;

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
    [Tooltip("待っている間・弾けている間も、持ち主がこの間隔(秒)で今の状態を送り直す（画面ごとに状態がずれたままにならないように）")]
    public float heartbeatInterval = 1.5f;

    [Tooltip("自分が撃った後、持ち主がその撃ちを反映した状態が届くまで、自分の画面の予測を優先する最長の秒数")]
    public float pendingTimeout = 1.2f;
    [Tooltip("持ち主からの状態（待っている間も heartbeatInterval ごとに来る）がこの秒数途切れたら、持ち主がロビーにいない（ひとりでへ行った等）とみなして、ロビーにいる人が持ち主を引き継ぐ")]
    public float ownerSilentTimeout = 3.5f;
    private float _lastHeard;
    private float _takeoverJitter;

    [Header("エリア（あそびば用。なければいつもエリアにいる扱い）")]
    [Tooltip("自分が myArea にいない間は、自分の画面では玉を止めて隠す")]
    public FuwaAreaVisibility areas;
    public int myArea = 2;
    [UdonSynced] private bool _sAway;    // 持ち主がエリアにいない（エリアにいる人が引き継ぐ）
    private bool _here = true;
    private float _awaySince = -1f;

    [Header("みんなでの会場用（ロビーのふわふわは触らない）")]
    [Tooltip("撃たれた時の効きの倍率（会場のふわふわは重くするので1より小さく）")]
    public float shotScale = 1f;
    [Tooltip("会場の真ん中。ここから arenaRadius より外へは出ず、内側へはじき返す（全員の画面で同じ計算）")]
    public Transform arenaCenter;
    public float arenaRadius = 0f;
    [Tooltip("膜に当たった時の、はね返りの強さ（入ってきた速さの何倍で返すか）")]
    public float barrierBounce = 1.1f;
    [Tooltip("膜に当たった時、最低でもこの速さで内側へ返す")]
    public float barrierMinInward = 3.5f;
    [Tooltip("試合の前後などで撃っても動かない（FuwaMatch が全員の画面でそれぞれ切り替える）")]
    [System.NonSerialized] public bool locked;

    [Header("チルルーム用：出すたびに大きさ・色・地面ではじけるかを変える（FuwaPlaygroundSpawner が決める）")]
    [Tooltip("オンなら、下の同期した大きさ・色・はじけ方を使う（持ち主が SpawnVariant で決めて配る）")]
    public bool variant;
    [UdonSynced] private float _vSize = 0.3f;
    [UdonSynced] private Color _vColor = Color.white;
    [Tooltip("チルルーム用：「地面に落ちたら消えるか」はこの係の今の設定を見る（切り替えたら、床に転がっている玉もすぐ従う）")]
    [UnityEngine.Serialization.FormerlySerializedAs("chill")] public FuwaPlaygroundSpawner playground;
    [UdonSynced] private float _vShotScale = 1f;
    [UdonSynced] private double _vTime;
    private float _shownSize = -1f;
    private Color _shownColor = new Color(-1f, 0f, 0f, 0f);
    private Material _selfMat;

    // 画面ごとの状態（持ち主ならこれが正解。持ち主でなければ、届いた状態をここへ写す）
    private bool _waiting = true;
    private bool _popped;
    // 送る用（持ち主が OnPreSerialization で詰める）
    [UdonSynced] private bool _nWaiting = true;
    [UdonSynced] private bool _nPopped;
    [UdonSynced] private Vector3 _sPos;
    [UdonSynced] private Vector3 _sVel;
    [UdonSynced] private Vector3 _sAng;
    [UdonSynced] private double _sTime;
    [UdonSynced] private int _sShot;
    // 最後の撃ち（溜め具合・向き・サーバー時刻）。つむじ風が「溜めて撃たれたら放す」・撃った直後は風が効きにくい、を全員の画面でそろえる（2026-10-13）
    [UdonSynced] private float _sShotCharge;
    [UdonSynced] private Vector3 _sShotDir;
    [UdonSynced] private double _sShotAt;
    private float _shotCharge;
    private Vector3 _shotDir;
    private double _shotAt = -100.0;

    private Rigidbody _rb;
    private Vector3 _homePos;
    private Quaternion _homeRot;
    private float _respawnAt = -1f;
    private Material _furMat;
    private Material _plainMat;   // 毛オフの材質（自分用の複製）。色・ポヨン・なびきは毛ありと両方に入れておく
    private bool _plain;
    private MeshFilter _furMeshFilter;
    private Mesh _furMesh;
    private float _hitFaceUntil;
    private bool _forcePlain;   // スイッチで「け: なし」
    private bool _lodPlain;     // 遠いので自動で毛なし
    private bool _hitFaceShown;
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
    // 持ち主でない人が撃った後：持ち主に反映されるのを待っている撃ちの数と、その時点で見えていた撃ちの番号
    private int _pending;
    private int _pendBase;
    private float _pendUntil;

    private bool _inited;

    private void Start() { Init(); }

    // ロビーが表示された（自分がみんなでロビーに来た・戻ってきた）
    private void OnEnable()
    {
        Init();
        _lastHeard = Time.time;   // 来たばかりの人がすぐ持ち主を取らないように、まず届くのを待つ
        _takeoverJitter = Random.Range(0f, 1.5f);   // ロビーにいる何人かが同時に引き継ごうとしないように、少しずつずらす
        // 持ち主のまま戻ってきた：自分の画面の玉はロビーを離れた時のまま止まっていたので、その古い状態を配らずにキノコの上へ戻す
        // （2026-10-09 てじゃ実機：持ち主がひとりでへ行って戻ると、みんなの画面でふわふわが一瞬ワープした）
        if (Networking.IsOwner(gameObject) && !_waiting) SetWaiting();
    }

    // Start より先に呼ばれることがある（隠れていたエリアが出た直後に撃たれた・届いた時）ので、使う前に必ず通す
    private void Init()
    {
        if (_inited) return;
        _inited = true;
        _rb = GetComponent<Rigidbody>();
        _selfRenderer = GetComponent<Renderer>();
        _trail = GetComponent<TrailRenderer>();
        _col = GetComponent<Collider>();
        if (furRenderer != null) _furMat = furRenderer.material;
        if (variant && _selfRenderer != null) _selfMat = _selfRenderer.material;
        // 毛オフの材質の複製を作って、毛ありに戻しておく（先に毛ありの複製 _furMat/_selfMat を取ってから）
        if (plainMaterial != null && furRenderer != null)
        {
            furRenderer.sharedMaterial = plainMaterial;
            _plainMat = furRenderer.material;
            furRenderer.sharedMaterial = _furMat;
            _furMeshFilter = furRenderer.GetComponent<MeshFilter>();
            if (_furMeshFilter != null) _furMesh = _furMeshFilter.sharedMesh;
        }
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
    // 弾けて戻るのを待っている間も含めて「出ていない」（チルルームで次に出す玉を選ぶ時に使う）
    public bool IsIdle() { Init(); return _waiting || _popped; }
    // 出した時刻（チルルームで上限を超えた時に、いちばん古いのから戻す）
    public double SpawnTime() { return _vTime; }

    // チルルーム：持ち主になった人の画面で呼ぶ。大きさ・色・地面ではじけるか・撃たれた時の効きを決めて、キノコから撃った向きへ出す
    public void SpawnVariant(Vector3 shotDir, float size, Color color, float shotK)
    {
        Init();
        if (!Networking.IsOwner(gameObject)) return;
        _pending = 0;
        if (!_waiting || _popped) SetWaiting();   // 出ている玉を使い回す時は、いったんキノコへ戻す
        _vSize = size; _vColor = color; _vShotScale = shotK;
        _vTime = Networking.GetServerTimeInSeconds();
        ApplyVariant();
        ReleaseFromMushroom(shotDir);
    }

    // 持ち主の画面で：キノコへ戻す（上限を減らした・使い回す時）
    public void Recall()
    {
        Init();
        if (!Networking.IsOwner(gameObject)) return;
        _pending = 0;
        SetWaiting();
    }

    // 同期した大きさ・色を見た目と当たりに反映（全員）
    private void ApplyVariant()
    {
        if (!variant) return;
        if (_vSize != _shownSize)
        {
            _shownSize = _vSize;
            transform.localScale = Vector3.one * _vSize;
            float r = _vSize * 0.5f;
            hitPadding = Mathf.Max(0f, r - 0.15f);   // 大きい玉はふちに当たった風も効くように
            releasePush = Mathf.Lerp(2.4f, 1.2f, Mathf.InverseLerp(0.2f, 1.4f, _vSize));
            releaseUp = Mathf.Lerp(2.1f, 1.6f, Mathf.InverseLerp(0.2f, 1.4f, _vSize));
        }
        shotScale = _vShotScale;
        if (_vColor != _shownColor)
        {
            _shownColor = _vColor;
            if (_furMat != null)
            {
                _furMat.SetColor("_Color", _vColor);
                _furMat.SetColor("_TipColor", Color.Lerp(_vColor, Color.white, 0.55f));
            }
            if (_selfMat != null) _selfMat.SetColor("_Color", _vColor);
            if (_plainMat != null)
            {
                _plainMat.SetColor("_Color", _vColor);
                _plainMat.SetColor("_TipColor", Color.Lerp(_vColor, Color.white, 0.55f));
            }
        }
    }

    public bool IsFlying() { Init(); return !_waiting && !_popped && _here; }

    // 毛ありと毛オフの両方の材質に入れる（今どっちを使っていても切り替えた瞬間に同じ見た目になるように）
    private void FurVec(string n, Vector4 v)
    {
        if (_furMat != null) _furMat.SetVector(n, v);
        if (_plainMat != null) _plainMat.SetVector(n, v);
    }

    // あそびばの設定スイッチ（FuwaPlaygroundSpawner）から：顔（一緒に回る）と毛オフ（軽量）。自分の画面だけの見た目
    public void SetLook(bool face, bool plain)
    {
        Init();
        float f = face ? 1f : 0f;
        if (_furMat != null) _furMat.SetFloat("_FaceOn", f);
        if (_plainMat != null) _plainMat.SetFloat("_FaceOn", f);
        _forcePlain = plain;
        ApplyPlain();
    }

    private void ApplyPlain()
    {
        bool plain = (_forcePlain || _lodPlain) && _plainMat != null && furRenderer != null;
        if (plain == _plain) return;
        _plain = plain;
        furRenderer.sharedMaterial = plain ? _plainMat : _furMat;
        if (_furMeshFilter != null && plainMesh != null) _furMeshFilter.sharedMesh = plain ? plainMesh : _furMesh;
    }

    // 遠くの玉は毛なしに（自分の画面だけ）。切り替わりの境目でちらつかないよう、戻る時は1割近くまで来てから
    private void UpdateLod()
    {
        if (_plainMat == null || lodRatio <= 0f || _shownHidden) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        float d = Vector3.Distance(lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position, transform.position);
        float r = d / Mathf.Max(0.05f, transform.lossyScale.x);
        bool far = _lodPlain ? r > lodRatio * 0.9f : r > lodRatio;
        if (far == _lodPlain) return;
        _lodPlain = far;
        ApplyPlain();
    }

    // 追っかけカメラがほかのエリアからあそびばを映している間は、エリアにいる扱いにして動かして見せる（FuwaFollowCam が切り替える）
    [System.NonSerialized] public bool showAway;
    private bool Here() { return areas == null || areas.GetArea() == myArea || showAway; }
    private bool ShouldHide() { return _popped || _waiting || !_here; }

    // エリアに来た・離れたの切り替え（自分の画面だけ）
    private void UpdateHere()
    {
        bool h = Here();
        if (h == _here) return;
        _here = h;
        if (!_waiting && !_popped)
        {
            if (h)
            {
                // 戻った：止めていた玉を、今の位置（いない間も届いた状態で動かしてある）から動かし始める
                _rb.isKinematic = false;
                _rb.velocity = Networking.IsOwner(gameObject) ? Vector3.zero : _predVel;
                _posErr = Vector3.zero;
            }
            else _rb.isKinematic = true;   // 離れた：床が消えるので止める
        }
        if (Networking.IsOwner(gameObject)) _sendSoon = true;   // 「いる／いない」をすぐ知らせる
        ApplyHiddenVisual();
    }

    // 持ち主の画面で：キノコの上に戻す（ゴールに入った・試合が始まった・終わった時）
    public void ForceHome()
    {
        Init();
        if (!Networking.IsOwner(gameObject)) return;
        _pending = 0;
        SetWaiting();
    }

    // 持ち主：キノコの上に戻して、吹かれるまで止めて待つ
    private void SetWaiting()
    {
        _shotCharge = 0f; _shotDir = Vector3.zero; _shotAt = -100.0;
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

    // エアガンから呼ばれる（吹いた人の画面で）。自分の画面ではすぐ動かし、持ち主でなければ持ち主にも伝える。charge＝溜め具合（0〜1）
    public void BlowShared(Vector3 velocityChange, float charge)
    {
        Init();
        if (_popped || _waiting || locked) return;   // キノコに隠れている間は、傘を撃たないと出てこない
        Shoot(velocityChange * shotScale, false, charge);
    }

    // スタートのキノコの傘を撃った人の画面で呼ばれる：撃った向きへポンと飛び出す
    public void ReleaseFromMushroom(Vector3 shotDir)
    {
        Init();
        if (_popped || !_waiting || locked) return;
        Vector3 flat = new Vector3(shotDir.x, 0f, shotDir.z);
        flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.zero;
        Shoot(flat * releasePush + Vector3.up * releaseUp, true, 0f);
    }

    private void Shoot(Vector3 velocityChange, bool release, float charge)
    {
        Launch(velocityChange);
        NoteShot(velocityChange, charge, Networking.GetServerTimeInSeconds());
        float k = Mathf.Clamp01(velocityChange.magnitude / Mathf.Max(0.01f, squashFullAt));
        PlayPuff(k);   // 自分の画面ではすぐ
        SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(PlayPuff), k);
        if (Networking.IsOwner(gameObject))
        {
            _sShot++;
            _seenShot = _sShot;
            SendState();
        }
        else
        {
            // 持ち主に伝える。持ち主がこの撃ちを反映した状態（撃ちの番号が進んだ状態）が届くまでは、自分の画面の予測を優先する
            if (_pending == 0) _pendBase = _seenShot;
            _pending++;
            _pendUntil = Time.time + pendingTimeout;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(RemoteShot), velocityChange, release, charge);
        }
    }

    // 持ち主の画面で：ほかの人が撃った分を反映して、みんなに配る
    [NetworkCallable(30)]
    public void RemoteShot(Vector3 velocityChange, bool release, float charge)
    {
        Init();
        if (!Networking.IsOwner(gameObject) || _popped || locked) return;
        if (_waiting && !release) return;   // 持ち主の画面ではまだキノコの中：傘を撃った分だけ受け付ける
        Launch(velocityChange);
        NoteShot(velocityChange, charge, Networking.GetServerTimeInSeconds());
        _sShot++;
        _seenShot = _sShot;
        SendState();
    }

    private void NoteShot(Vector3 velocityChange, float charge, double at)
    {
        _shotCharge = charge;
        _shotDir = velocityChange.sqrMagnitude > 0.000001f ? velocityChange.normalized : Vector3.zero;
        _shotAt = at;
    }

    public float LastShotCharge() { return _shotCharge; }
    public float LastShotAge() { return (float)(Networking.GetServerTimeInSeconds() - _shotAt); }
    public Vector3 LastShotDir() { return _shotDir; }

    // 撃った直後の風の弱まりの残り（FuwaBall と同じ決まり。保っている間は1、戻る間に1→0）
    private float WindDampLeft()
    {
        if (_shotCharge <= 0f && _shotDir == Vector3.zero) return 0f;
        float hold = shotWindDampHoldMax * _shotCharge * _shotCharge;
        float time = Mathf.Lerp(shotWindDampTimeMin, shotWindDampTimeMax, _shotCharge);
        float t = LastShotAge() - hold;
        if (time <= 0f || t >= time) return 0f;
        return 1f - Mathf.Max(0f, t) / time;
    }

    // 撃った向きと同じ向きの風はそのまま、横向き・逆向きの分だけ弱める
    private Vector3 DampWind(Vector3 acc)
    {
        float left = WindDampLeft();
        if (left <= 0f) return acc;
        float k = 1f - Mathf.Lerp(shotWindDampMin, shotWindDampMax, _shotCharge) * left;
        float along = Vector3.Dot(acc, _shotDir);
        Vector3 keep = along > 0f ? _shotDir * along : Vector3.zero;
        return keep + (acc - keep) * k;
    }

    // 玉に撃った速さを足す（自分の画面の玉に。持ち主かどうかに関係なく）
    private void Launch(Vector3 velocityChange)
    {
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
        _posErr = Vector3.zero;   // 撃った直後は、自分の見ている位置から動かす
        ApplyHiddenVisual();
    }

    // 持ち主：今の位置・速さ・時刻を送る（中身は OnPreSerialization で詰める）
    private void SendState()
    {
        // 床に止まっている間（チルルームで「消えない」の時）は送る間隔をゆっくりに
        bool resting = _rb.isKinematic || _rb.velocity.sqrMagnitude < 0.0025f;
        _nextSend = Time.time + (resting ? heartbeatInterval : correctionInterval);
        _sendSoon = false;
        RequestSerialization();
    }

    public override void OnPreSerialization()
    {
        Init();
        _nWaiting = _waiting;
        _nPopped = _popped;
        _sPos = _rb.position;
        _sVel = _rb.isKinematic ? Vector3.zero : _rb.velocity;
        _sAng = _rb.isKinematic ? Vector3.zero : _rb.angularVelocity;
        _sTime = Networking.GetServerTimeInSeconds();
        _sShotCharge = _shotCharge;
        _sShotDir = _shotDir;
        _sShotAt = _shotAt;
        _sAway = !_here;
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
        Init();
        _lastHeard = Time.time;
        if (Networking.IsOwner(gameObject)) return;
        UpdateHere();
        if (_sAway) { if (_awaySince < 0f) _awaySince = Time.time; } else _awaySince = -1f;
        if (variant) ApplyVariant();
        // 自分が撃った分がまだ持ち主に反映されていない間は、届いた状態より自分の予測を優先する（弾けた知らせだけはすぐ受け取る）
        if (_pending > 0)
        {
            if (_sShot >= _pendBase + _pending || Time.time > _pendUntil) _pending = 0;
            else if (!_nPopped) { _seenShot = _sShot; return; }
        }
        _waiting = _nWaiting;
        _popped = _nPopped;
        if (_popped)
        {
            _rb.isKinematic = true;
            _pending = 0;   // 弾けた：持ち主はもう撃ちを受け付けないので待たない
            _seenShot = _sShot;
        }
        else if (_waiting)
        {
            if (!_shownWaiting) GoHome();
            _seenShot = _sShot;
        }
        else
        {
            float late = (float)(Networking.GetServerTimeInSeconds() - _sTime);
            Vector3 target;
            if (_sVel.sqrMagnitude < 0.0025f) { target = _sPos; _predVel = Vector3.zero; }   // 床に止まっている：先回りすると床にめり込む方へ寄せてしまう
            else target = Predict(_sPos, _sVel, late);
            bool newShot = _sShot != _seenShot;
            _seenShot = _sShot;
            if (newShot) { _shotCharge = _sShotCharge; _shotDir = _sShotDir; _shotAt = _sShotAt; }
            if (!_here)
            {
                // エリアにいない：止めたまま、届いた位置へ置いておく（戻った時にそこから動かす）
                _shownWaiting = false;
                _rb.isKinematic = true;
                _rb.position = target;
                transform.position = target;
                _posErr = Vector3.zero;
            }
            else if (_shownWaiting || _rb.isKinematic)
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
            if (!_rb.isKinematic)
            {
                _rb.velocity = _predVel;
                if (newShot) _rb.angularVelocity = _sAng;
            }
        }
        if (_shownHidden != ShouldHide()) ApplyHiddenVisual();
    }

    // 持ち主が入れ替わった（前の持ち主が抜けた時など）：新しい持ち主が、今の自分の状態をすぐみんなに送る
    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        Init();
        _lastHeard = Time.time;
        if (player == null || !player.isLocal) return;
        if (_popped && _respawnAt < 0f) _respawnAt = Time.time + respawnDelay;
        _posErr = Vector3.zero;
        _pending = 0;
        SendState();
    }

    // 撃たれた時（全員）：強さ k(0〜1) に合わせて、綿毛を散らして、ボヨンと鳴らして、ポヨンとつぶれる
    [NetworkCallable(30)]
    public void PlayPuff(float k)
    {
        if (shotPuff != null) shotPuff.Emit(Mathf.RoundToInt(Mathf.Lerp(shotPuffMin, shotPuffMax, k)));
        if (sfxBoing != null && sfxBoing.clip != null)
        {
            sfxBoing.pitch = Mathf.Lerp(boingPitchWeak, boingPitchStrong, k) * Random.Range(0.96f, 1.04f);
            sfxBoing.PlayOneShot(sfxBoing.clip, Mathf.Lerp(boingVolumeWeak, boingVolumeStrong, k));
        }
        StartSquash(k);
        _hitFaceUntil = Time.time + hitFaceTime;
    }

    // 撃たれた顔 😣 の出し入れ（顔の材質の _FaceHit。毛あり・毛オフの両方）
    private void UpdateHitFace()
    {
        bool hit = Time.time < _hitFaceUntil;
        if (hit == _hitFaceShown) return;
        _hitFaceShown = hit;
        float v = hit ? 1f : 0f;
        if (_furMat != null) _furMat.SetFloat("_FaceHit", v);
        if (_plainMat != null) _plainMat.SetFloat("_FaceHit", v);
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
            FurVec("_Squash", Vector4.zero);
            return;
        }
        float w = _squashPower * env * Mathf.Cos(t * squashFrequency * 2f * Mathf.PI);
        FurVec("_Squash", new Vector4(0f, 1f, 0f, w));
    }

    public void PlayPop()
    {
        if (popEffect != null) { popEffect.transform.position = transform.position; popEffect.Play(); }
        if (sfxPop != null) sfxPop.Play();
    }

    private Vector3 _wind;
    // 風のギミックから物理の1回ごとに呼ばれる（OnTriggerStay）。次の FixedUpdate で加速度として足す
    public void AddWind(Vector3 accel) { _wind += accel; }

    // 重くなるエリア（FuwaGravityArea）から毎フレーム：次の物理更新だけ落ちる速さ・落ちる上限が scale 倍（FuwaBall と同じ）
    private float _gravScale;
    public void SetGravityScale(float scale) { if (scale > _gravScale) _gravScale = scale; }

    // 全員の画面で同じように起きる当たり（みんなでひろばの転がる岩など）：通信せずに、それぞれの画面の玉の速さを変える。持ち主はすぐ送り直す
    public void Nudge(Vector3 velocityChange)
    {
        Init();
        if (_waiting || _popped || _rb.isKinematic) return;
        _rb.velocity = _rb.velocity + velocityChange;
        PlayPuff(Mathf.Clamp01(velocityChange.magnitude / Mathf.Max(0.01f, squashFullAt)));
        if (Networking.IsOwner(gameObject)) _sendSoon = true;
    }

    private void FixedUpdate()
    {
        UpdateHere();   // エリアを離れた直後の物理の1回で、消えた床を抜けて落ちないように
        if (_waiting || _popped || _rb.isKinematic) { _wind = Vector3.zero; return; }
        // 全員の画面で同じ決まりで動かす（持ち主でなくても）
        float g = _gravScale > 0f ? _gravScale : 1f;
        _gravScale = 0f;
        _rb.AddForce(Vector3.down * fallAcceleration * g, ForceMode.Acceleration);
        // 風のギミック（うちわ・扇風機など）から足された加速度（全員の画面で同じ風を当てるので、ずれにくい）
        if (_wind.sqrMagnitude > 0f) { _rb.AddForce(DampWind(_wind), ForceMode.Acceleration); _wind = Vector3.zero; }
        Vector3 v = _rb.velocity;
        if (v.y < -maxFallSpeed * g) v.y = -maxFallSpeed * g;
        if (v.magnitude > maxSpeed) v = v.normalized * maxSpeed;
        // 会場のまわりの膜：外へ向かっていたら内側へはじき返す（全員の画面で同じ計算なので、ずれにくい）
        if (arenaCenter != null && arenaRadius > 0f)
        {
            Vector3 off = _rb.position - arenaCenter.position; off.y = 0f;
            float d = off.magnitude;
            float lim = arenaRadius - transform.lossyScale.x * 0.5f;
            if (d > lim && d > 0.001f)
            {
                Vector3 n = off / d;
                float vn = Vector3.Dot(v, n);
                if (vn > -barrierMinInward) v -= n * (vn + Mathf.Max(barrierMinInward, vn * barrierBounce));
                _rb.position -= n * (d - lim);
                if (Networking.IsOwner(gameObject)) _sendSoon = true;
            }
        }
        _rb.velocity = v;
        UpdatePerch();
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
        UpdateHere();
        bool owner = Networking.IsOwner(gameObject);
        Vector3 vel = _rb.isKinematic ? Vector3.zero : _rb.velocity;

        if (_waiting && _shownWaiting) transform.Rotate(new Vector3(0.3f, 1f, 0.15f), idleSpin * Time.deltaTime, Space.World);
        if (owner)
        {
            if (_here && !_waiting && !_popped && transform.position.y < _homePos.y - fallLimit) SetWaiting();
            if (_popped && _respawnAt > 0f && Time.time >= _respawnAt) SetWaiting();
            // チルルーム：床に転がっている間に「消える」へ切り替えた（ぶつかった瞬間はもう過ぎている）ので、床に触れていたらはじける
            if (_here && playground != null && !_waiting && !_popped && !_rb.isKinematic && playground.PopGround() && TouchingPopGround()) Pop();
            // 弾けたまま戻す予定が無い（弾けている間に持ち主が入れ替わった）なら、ここで戻す予定を立てる
            if (_popped && _respawnAt < 0f) _respawnAt = Time.time + respawnDelay;
            // 飛んでいる間はときどき今の位置を送る（壁にぶつかった後はすぐ送る）
            if (!_waiting && !_popped && (_sendSoon || Time.time >= _nextSend)) SendState();
            // 待っている間・弾けている間も、ゆっくり送り直す（ほぼ同時に2人が撃った時などに、画面ごとの状態がずれたままにならないように）
            else if ((_waiting || _popped) && Time.time >= _nextSend) { _nextSend = Time.time + heartbeatInterval; RequestSerialization(); }
        }
        else if (_here && ((_sAway && _awaySince >= 0f && Time.time - _awaySince > 0.3f + _takeoverJitter) || Time.time - _lastHeard > ownerSilentTimeout + _takeoverJitter))
        {
            // 持ち主がエリアにいない（「いない」が届いた）か、持ち主からしばらく何も届かない：エリアにいる自分が引き継ぐ
            _awaySince = -1f;
            // 持ち主からしばらく何も届かない＝持ち主の画面ではロビーが非表示（ひとりで・入口へ行った）。ロビーにいる自分が引き継ぐ
            _lastHeard = Time.time;
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        }
        if (_shownHidden != ShouldHide()) ApplyHiddenVisual();
        if (_trail != null) _trail.emitting = !ShouldHide();

        // 壁や柵にぶつかった時のポヨン：急に速さが変わったのを、それぞれの画面で見て鳴らさずにつぶすだけ
        if (!_waiting && !_popped && Time.time > _bumpCooldown && (vel - _prevVel).magnitude > bumpDetect && _prevVel.magnitude > 1f)
        {
            StartSquash(Mathf.Clamp01((vel - _prevVel).magnitude / Mathf.Max(0.01f, squashFullAt)) * 0.7f);
            _bumpCooldown = Time.time + 0.25f;
        }
        _prevVel = vel;
        UpdateSquash();
        UpdateHitFace();
        UpdateLod();

        if (_furMat != null)
        {
            Vector3 target = _waiting ? Vector3.zero : Vector3.ClampMagnitude(-vel * furDragAmount, furDragMax);
            _furDrag = Vector3.Lerp(_furDrag, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            FurVec("_Drag", new Vector4(_furDrag.x, _furDrag.y, _furDrag.z, 0f));
        }
    }

    // 柵の上の面に乗っている間（持ち主だけが見て、転がしたら送る）
    private void OnCollisionStay(Collision collision)
    {
        if (collision == null || collision.gameObject == null || collision.contactCount == 0) return;
        if (perchName == "" || !collision.gameObject.name.Contains(perchName)) return;
        if (collision.GetContact(0).normal.y < 0.6f) return;   // 横に当たっているだけの時は数えない
        if (_perchStart < 0f || Time.time - _perchSeen > 0.2f) _perchStart = Time.time;
        _perchSeen = Time.time;
    }

    // 柵のてっぺん（平らな当たり判定）に乗ってほとんど止まったら、まわりで一番高い床の方へそっと転がす
    private void UpdatePerch()
    {
        if (_perchStart < 0f) return;
        if (Time.time - _perchSeen > 0.2f) { _perchStart = -1f; return; }   // もう乗っていない
        if (_rb.velocity.sqrMagnitude > 0.25f) { _perchStart = Time.time; return; }   // まだ動いている
        if (Time.time - _perchStart < perchDelay) return;
        _perchStart = -1f;
        if (!Networking.IsOwner(gameObject)) return;
        Vector3 p = transform.position;
        Vector3 best = Vector3.zero; float bestY = -1e9f;
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 0.25f;
            Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            RaycastHit hit;
            if (!Physics.Raycast(p + d * perchProbe + Vector3.up * 0.3f, Vector3.down, out hit, 12f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (hit.collider.gameObject.name.Contains(perchName) || hit.collider.gameObject == gameObject) continue;   // 柵そのもの・自分は床じゃない
            if (hit.point.y > bestY) { bestY = hit.point.y; best = d; }
        }
        if (best == Vector3.zero)
        {
            best = _homePos - p; best.y = 0f;   // まわりに床がない：出てきた所の方へ
            if (best.sqrMagnitude < 0.0001f) return;
            best.Normalize();
        }
        _rb.velocity = best * perchPush + Vector3.up * 0.3f;
        _sendSoon = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.collider == null) return;
        // 跳ねるキノコ（FuwaBounce）：全員の画面で同じように跳ねる（みんなでひろばの跳ねるキノコ。FuwaBall と同じ決まり）
        if (!_waiting && !_popped)
        {
            FuwaBounce bo = collision.collider.GetComponent<FuwaBounce>();
            if (bo != null) { BounceOff(bo); return; }
        }
        if (!Networking.IsOwner(gameObject) || _waiting || _popped) return;
        int popMask = playground != null && !playground.PopGround() ? 0 : popLayers.value;   // あそびばで「地面に落ちても消えない」の時は床ではじけない
        if ((popMask & (1 << collision.collider.gameObject.layer)) == 0)
        {
            _sendSoon = true;   // 壁や柵で跳ね返った：みんなの画面のズレを早めに直す
            return;
        }
        Pop();
    }

    // 床（popLayers）に触れているか。跳ねるキノコの傘（FuwaBounce）は床ではない（同じレイヤーなので、乗った瞬間にはじけていた。2026-10-13）
    private bool TouchingPopGround()
    {
        Collider[] cols = Physics.OverlapSphere(transform.position + Vector3.down * 0.05f, transform.lossyScale.x * 0.5f, popLayers.value, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null || cols[i].gameObject == gameObject) continue;
            if (cols[i].GetComponent<FuwaBounce>() != null) continue;
            return true;
        }
        return false;
    }

    // あそびばのイバラ（FuwaSpike）に入ったら、はじける（コースと同じ。2026-10-13 てじゃ）。はじけるかは持ち主の画面で決める
    private void OnTriggerEnter(Collider other)
    {
        if (other == null || _waiting || _popped || !Networking.IsOwner(gameObject)) return;
        if (other.GetComponent<FuwaSpike>() != null) Pop();
    }

    private void BounceOff(FuwaBounce bo)
    {
        Vector3 n = bo.GetNormal(transform.position);
        Vector3 v = _rb.velocity;
        float vn = Vector3.Dot(v, n);
        Vector3 vt = v - n * vn;
        _rb.velocity = vt * bo.keepTangent + n * Mathf.Max(bo.bounceSpeed, -vn * 0.5f);
        PlayPuff(0.6f);
        if (Networking.IsOwner(gameObject)) _sendSoon = true;
    }

    // 持ち主の画面で：床に落ちてはじける
    private void Pop()
    {
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
        _shownHidden = ShouldHide();
        if (_selfRenderer != null) _selfRenderer.enabled = !_shownHidden;
        if (furRenderer != null) furRenderer.enabled = !_shownHidden;
        if (_col != null) _col.enabled = !_shownHidden;
        if (_trail != null) _trail.Clear();
        if (_shownHidden && shotPuff != null) shotPuff.Clear();
    }
}
