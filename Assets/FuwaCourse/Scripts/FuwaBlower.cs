using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

// 風の道具（ピックアップ）。PlayerObjectで1人1本ずつ配られる。
// 持ってUse（トリガー/左クリック）でフッと風を出す。風が効くのは自分のPCのふわふわだけ。
// 他人の道具は「その人が持っている間だけ」見える。拾えない。
// VRCObjectSyncと同じオブジェクトなのでContinuous（Manualは併用できない）
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class FuwaBlower : UdonSharpBehaviour
{
    public FuwaBall targetBall;
    [Tooltip("みんなで吹く同期ふわふわ（FuwaSharedBall）。自分のふわふわと同じ風で吹ける")]
    public FuwaSharedBall[] sharedBalls;
    [Tooltip("風が出る口。forward（青い矢印）が風の向き")]
    public Transform nozzle;
    public ParticleSystem puffEffect;
    public AudioSource puffSound;
    [Tooltip("他人の道具を隠す時にまとめてON/OFFする見た目")]
    public GameObject visualRoot;
    [Tooltip("VRで頭から取り出す準備中だけ表示する、何も描かれないダミー（水色の枠を出さないため）")]
    public Renderer highlightDummy;
    [Tooltip("VRで頭から取り出す準備中の、掴む判定の箱の大きさ(m)")]
    public float summonGrabSize = 0.3f;
    [Tooltip("VRでタイマー表示をこの道具の上に出すためのHUD")]
    public FuwaHud hud;
    [Tooltip("自分の道具を登録しておく先（ワープ時や台から呼び寄せるため）")]
    public FuwaRespawnManager manager;
    [Tooltip("操作表示（発射・エアガン）の言語を合わせるため")]
    public FuwaLanguage language;

    [Header("風の強さ")]
    [Tooltip("届く距離(m)")]
    public float range = 3.5f;
    [Tooltip("風の広がり（中心からの角度）")]
    public float coneAngle = 45f;
    [Tooltip("風の広がりの上側だけの角度（狙った向きより上にある物はこの角度まで。横〜下は Cone Angle）")]
    public float coneAngleTop = 38f;
    [Tooltip("前（向いてる水平方向）に押す速度(m/s)")]
    public float pushPower = 2.6f;
    [Tooltip("必ず上に持ち上げる速度(m/s)。見下ろして吹いても玉は浮く")]
    public float liftPower = 2.2f;
    [Tooltip("見上げて吹いた時に追加で持ち上げる量(m/s)")]
    public float aimLiftBonus = 1.4f;
    [Tooltip("遠くでもこの割合は残る")]
    [Range(0f, 1f)] public float minFalloff = 0.3f;
    [Tooltip("すぐ近くで撃った時の強さの倍率（近いほど強い）")]
    public float nearBoost = 1.3f;
    [Tooltip("ちょん押しの射程（rangeに対する割合）")]
    [Range(0f, 1f)] public float minRangeRatio = 0.5f;
    [Tooltip("満タンの射程（rangeに対する割合）")]
    [Range(0f, 1f)] public float maxRangeRatio = 0.75f;
    [Tooltip("風の中心線へ胞子を吸い寄せる強さ（ずれた胞子も気流に乗る）")]
    public float suction = 2.0f;
    [Tooltip("撃ったあと次に溜め始められるまで(秒)")]
    public float cooldown = 0.1f;

    [Header("チャージ（押してる間溜めて、離すと発射）")]
    [Tooltip("押してから溜め始めるまでの時間(秒)。これより早く離すとちょん押し扱いで溜めの演出も出ない")]
    public float chargeDelay = 0.12f;
    [Tooltip("溜め始めてから最大まで溜まる時間(秒)")]
    public float chargeTime = 0.4f;
    [Tooltip("ちょん押しでの胞子への強さ（最大溜め=1）")]
    [Range(0f, 1f)] public float minBallPower = 0.35f;
    [Tooltip("満タンでの胞子への強さ（1より大きいと、溜めるほど遠くへ飛ばせる）")]
    public float maxBallPower = 1.6f;
    [Tooltip("ちょん押しでのモンスターへの強さ（最大溜め=1）")]
    [Range(0f, 1f)] public float minEnemyPower = 0.1f;
    [Tooltip("この溜め以上でモンスターがひるむ（突進キャンセル）。0ならちょん押しでもひるむ")]
    [Range(0f, 1f)] public float enemyStunCharge = 0f;
    [Tooltip("フルチャージでモンスターを撃った時のノックバックの倍率")]
    public float fullChargeEnemyBoost = 1.25f;
    [Tooltip("溜め中に銃口でふくらむ玉（自分にだけ見える）")]
    public Transform chargeOrb;
    public float orbMaxScale = 0.07f;
    public Renderer chargeOrbRenderer;
    [Tooltip("溜め中に銃口へ吸い込まれる粒")]
    public ParticleSystem chargeParticles;
    [Tooltip("満タンになった瞬間のきらきら")]
    public ParticleSystem fullBurst;
    [Tooltip("満タンで撃った時に追加で出す風の粒の数")]
    public int strongPuffExtra = 40;
    public AudioSource chargeSound;
    public AudioSource chargedSound;
    public AudioSource puffBigSound;
    [Tooltip("銃の後ろの赤いポンプ。溜めると膨らみ、撃つとしぼむ")]
    public Transform pumpBulb;
    [Tooltip("満タンでどれだけ膨らむか（1=2倍）")]
    public float bulbMaxInflate = 0.45f;
    [Tooltip("ちょん押しで一瞬膨らむ量")]
    public float bulbTapInflate = 0.18f;

    [Header("空気ゲージ（ちょん押し連射で減り、撃たない・溜めると回復。自分の銃だけ）")]
    [Tooltip("1発で使う空気（満タン=1）。ちょん押しも溜め撃ちも同じ")]
    public float airPerTap = 1f / 6f;
    [Tooltip("撃ってからこの秒数たつと自然回復が始まる")]
    public float airRegenDelay = 0.5f;
    [Tooltip("自然回復で空から満タンまでの秒数")]
    public float airRegenTime = 1.5f;
    [Tooltip("溜めている間、空から満タンまで回復する秒数（ポンプで空気を入れる）。chargeTime と同じにすると、空から溜めてもフルチャージの瞬間に満タン")]
    public float airChargeRefillTime = 0.4f;
    [Tooltip("空気が足りない時の空振り音")]
    public AudioSource emptySound;
    [Tooltip("空気が空の時のポンプの大きさ（満タン=1）")]
    public float bulbEmptyScale = 0.65f;
    public Color bulbEmptyColor = new Color(0.55f, 0.6f, 0.55f, 1f);
    public Color bulbWarnColor = new Color(0.95f, 0.42f, 0.38f, 1f);
    [Tooltip("銃の側面に貼った空気ゲージ（左右。自分の銃だけ表示。色はHUDと同じ）")]
    public Renderer[] gunGauges;
    private Material[] _gunGaugeMats;

    [Tooltip("デスクトップでは道具の向きではなく視線の方向に吹く")]
    public bool desktopAimWithView = true;
    [Tooltip("デスクトップでは見た目をアバターの右手の位置に持ってくる")]
    public bool desktopSnapToHand = true;
    [Tooltip("右手からのずらし（視線基準: x=右 y=上 z=前）")]
    public Vector3 desktopHandOffset = Vector3.zero;

    [Tooltip("持ち主のアバターの大きさ（目の高さ）に合わせて銃の大きさを変える。風の届く距離や強さは変えない")]
    public bool autoScale = true;
    [Tooltip("この目の高さ(m)のアバターで等倍")]
    public float scaleBaseEyeHeight = 1.6f;
    public float minAutoScale = 0.5f;
    public float maxAutoScale = 1.6f;
    private float _autoScale = 1f;

    [UdonSynced] private bool _held;
    // 空気の残り（ポンプの見た目を他の人にも見せるため。多少遅れてもいいのでContinuousの定期送信に乗せる）
    [UdonSynced(UdonSyncMode.Smooth)] private float _airSync = 1f;

    private VRCPickup _pickup;
    private float _nextPuff;
    private bool _charging;
    private bool _chargeFxSent;
    private float _chargeStart;
    private float _puffBaseVolume = 1f;
    private float _puffBasePitch = 1f;
    private Material _orbMat;
    private float _puffBaseSpeed = 1f;
    private Vector3 _bulbBaseScale;
    private Vector3 _bulbBasePos;
    private float _bulbInflate;       // 今の膨らみ（0=普通）
    private float _bulbShotTime = -10f;
    private float _bulbShotFrom;
    private bool _bulbTap;
    // 見た目・音用のチャージ状態（ネットワークイベントで全員に届く）
    private bool _fxCharging;
    private float _fxStart;
    private bool _fxFull;
    private bool _isMine;
    private Quaternion _visualLocalRot;
    private Quaternion _nozzleLocalRot;
    private Vector3 _visualLocalPos;
    private Vector3 _nozzleLocalPos;
    private bool _aligned;
    private bool _summonHidden;   // VRで頭から取り出す準備中（手の中に隠して置いてある）
    private Renderer[] _visualRenderers;
    private bool[] _visualWasOn;
    private bool _grabBoxBig;
    private Vector3 _grabBoxSize, _grabBoxCenter;
    private float _air = 1f;
    private float _lastShotTime = -10f;
    private float _bulbAirScale = 1f;
    private Material _bulbMat;
    private Color _bulbBaseColor = Color.white;
    private Color _bulbLastColor;

    private void Start()
    {
        if (visualRoot != null)
        {
            _visualLocalRot = visualRoot.transform.localRotation;
            _visualLocalPos = visualRoot.transform.localPosition;
        }
        if (nozzle != null)
        {
            _nozzleLocalRot = nozzle.localRotation;
            _nozzleLocalPos = nozzle.localPosition;
        }
        if (puffSound != null) { _puffBaseVolume = puffSound.volume; _puffBasePitch = puffSound.pitch; }
        if (chargeOrbRenderer != null) _orbMat = chargeOrbRenderer.material;
        if (puffEffect != null) _puffBaseSpeed = puffEffect.main.startSpeedMultiplier;
        if (pumpBulb != null) { _bulbBaseScale = pumpBulb.localScale; _bulbBasePos = pumpBulb.localPosition; }
        SetOrb(0f);
        _pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        _isMine = Networking.IsOwner(gameObject);
        SetupBulbMaterial();
        if (!_isMine && _pickup != null) _pickup.pickupable = false;
        if (_isMine && hud != null) hud.SetAnchor(visualRoot != null ? visualRoot.transform : transform);
        if (_isMine && manager != null) manager.RegisterMyBlower(this);
        if (_isMine && language != null) language.RegisterBlower(this);
        UpdateVisibility();
    }

    // FuwaLanguageから呼ばれる（自分の銃だけ）
    public void ApplyLanguage(bool english)
    {
        if (_pickup == null) _pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        if (_pickup == null) return;
        _pickup.UseText = english ? "Shoot" : "ショット";   // チャージの説明は看板のあそびかたに任せる
        _pickup.InteractionText = english ? "Air Gun" : "エアガン";
    }

    public bool IsHeld() { return _held; }

    // VRの取り出し：掴むまでは見えないようにしておく（FuwaGunSummon から）
    public void SetSummonHidden(bool hidden)
    {
        if (hidden == _summonHidden) return;
        _summonHidden = hidden;
        ApplySummonHidden();
    }

    // 見た目だけ消す。VRChatは「近づけた時の枠線」を、掴める物とその子の表示中のレンダラーの形で描き、
    // 表示中のレンダラーが1つも無いと、代わりに当たり判定の箱を水色の枠で描いてしまう。
    // なので銃の見た目のレンダラーは切りつつ、形が点しかない（何も描かれない）ダミーのレンダラーだけ表示しておく。
    private void ApplySummonHidden()
    {
        if (visualRoot == null) return;
        if (_visualRenderers == null) _visualRenderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        bool hide = _summonHidden && !_held;
        if (hide && _visualWasOn == null)
        {
            _visualWasOn = new bool[_visualRenderers.Length];
            for (int i = 0; i < _visualRenderers.Length; i++)
            {
                Renderer r = _visualRenderers[i];
                if (r == null) continue;
                _visualWasOn[i] = r.enabled;
                r.enabled = false;
            }
        }
        else if (!hide && _visualWasOn != null)
        {
            for (int i = 0; i < _visualRenderers.Length; i++)
                if (_visualRenderers[i] != null) _visualRenderers[i].enabled = _visualWasOn[i];
            _visualWasOn = null;
        }
        if (highlightDummy != null) highlightDummy.enabled = hide;

        // 隠してる間は掴む判定を大きめの箱にして、手の向きに関係なく掴めるようにする
        BoxCollider box = (BoxCollider)GetComponent(typeof(BoxCollider));
        if (box != null)
        {
            if (hide && !_grabBoxBig)
            {
                _grabBoxSize = box.size; _grabBoxCenter = box.center;
                box.size = Vector3.one * summonGrabSize;
                box.center = Vector3.zero;
                _grabBoxBig = true;
            }
            else if (!hide && _grabBoxBig)
            {
                box.size = _grabBoxSize; box.center = _grabBoxCenter;
                _grabBoxBig = false;
            }
        }
    }

    // 持っていなければ、指定の台の上へ移動する（自分の道具にだけ呼ばれる）
    public void MoveToStand(Transform spot)
    {
        if (spot == null || _held || !Networking.IsOwner(gameObject)) return;
        VRCObjectSync sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        if (sync != null) sync.TeleportTo(spot);
        else transform.SetPositionAndRotation(spot.position, spot.rotation);
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        _isMine = Networking.IsOwner(gameObject);
        SetupBulbMaterial();
        if (_pickup != null) _pickup.pickupable = _isMine;
        if (_isMine && hud != null) hud.SetAnchor(visualRoot != null ? visualRoot.transform : transform);
        if (_isMine && manager != null) manager.RegisterMyBlower(this);
        if (_isMine && language != null) language.RegisterBlower(this);
        UpdateVisibility();
    }

    public override void OnPickup()
    {
        if (hud != null && _pickup != null) hud.SetHeld(true, _pickup.currentHand == VRC_Pickup.PickupHand.Left);
        _held = true;
        if (_summonHidden) { _summonHidden = false; ApplySummonHidden(); }
        RequestSerialization();
        UpdateVisibility();
    }

    public override void OnDrop()
    {
        if (hud != null) hud.SetHeld(false, false);
        if (_charging && _chargeFxSent) SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChargeCancelFx));
        _charging = false;
        _held = false;
        RequestSerialization();
        UpdateVisibility();

        // デスクトップでは見た目だけ視線の向きにしているので、離すと本体（変な角度で持たれてる）の向きに戻ってしまう。
        // 離した時に見えていた位置へ、水平で前向きの角度で置き直す（VRChatが離した処理を終えた次のフレームで）
        if (_aligned && visualRoot != null)
        {
            Vector3 fwd = visualRoot.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = transform.forward;
            _dropPos = visualRoot.transform.position - visualRoot.transform.rotation * Vector3.Scale(_visualLocalPos, transform.lossyScale);
            _dropRot = Quaternion.LookRotation(fwd.normalized, Vector3.up) * Quaternion.Inverse(_visualLocalRot);
            _dropPending = true;
            SendCustomEventDelayedFrames(nameof(_ApplyDropPose), 1);
        }
    }

    private Vector3 _dropPos;
    private Quaternion _dropRot;
    private bool _dropPending;   // 置き直すまでは見た目を戻さない（1フレーム変な角度が見えないように）

    public void _ApplyDropPose()
    {
        _dropPending = false;
        if (_held || !Networking.IsOwner(gameObject)) return;
        transform.SetPositionAndRotation(_dropPos, _dropRot);
        Rigidbody rb = (Rigidbody)GetComponent(typeof(Rigidbody));
        if (rb != null && !rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        VRCObjectSync sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        if (sync != null) sync.FlagDiscontinuity();
    }

    public override void OnDeserialization()
    {
        UpdateVisibility();
    }

    // デスクトップで持っている間だけ、見た目と風の口をアバターの右手の位置へ移し、視線の方向に向ける
    // （VRは手の向きそのまま）
    // 他人の道具も、持ち主がデスクトップなら同じ計算をする。持ち主の手のボーンと頭の向きは
    // VRChatが同期しているので、追加の同期なしで全員に同じ見た目になる。
    public override void PostLateUpdate()
    {
        VRCPlayerApi holder = Networking.GetOwner(gameObject);
        UpdateAutoScale(holder);
        bool align = desktopAimWithView && _held && holder != null && holder.IsValid() && !holder.IsUserInVR();
        if (align)
        {
            Quaternion head = holder.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
            Vector3 basePos = transform.position;
            if (desktopSnapToHand)
            {
                // アバターに右手のボーンが無い時は (0,0,0) が返るので、その時は元の位置のまま
                Vector3 hand = holder.GetBonePosition(HumanBodyBones.RightHand);
                if (hand.sqrMagnitude > 0.0001f)
                {
                    // 手のボーンは手首にあるので、中指の付け根の方へ寄せて手のひらの中心にする
                    Vector3 finger = holder.GetBonePosition(HumanBodyBones.RightMiddleProximal);
                    Vector3 palm = finger.sqrMagnitude > 0.0001f ? Vector3.Lerp(hand, finger, 0.7f) : hand;
                    basePos = palm + head * (desktopHandOffset * _autoScale);
                }
            }
            if (visualRoot != null) visualRoot.transform.SetPositionAndRotation(basePos, head);
            if (nozzle != null) nozzle.SetPositionAndRotation(basePos + head * (_nozzleLocalPos * _autoScale), head);
            _aligned = true;
        }
        else if (_aligned && !_dropPending)
        {
            if (visualRoot != null)
            {
                visualRoot.transform.localPosition = _visualLocalPos;
                visualRoot.transform.localRotation = _visualLocalRot;
            }
            if (nozzle != null)
            {
                nozzle.localPosition = _nozzleLocalPos;
                nozzle.localRotation = _nozzleLocalRot;
            }
            _aligned = false;
        }
    }

    // 持ち主の目の高さから銃の大きさを決める。目の高さはVRChatが同期しているので、全員で同じ大きさになる。
    // 風の口（ショットの判定の起点）は見た目の銃口について行く。届く距離・広がり・強さは大きさに関係なく固定。
    // 撃った風の粒は大きさを打ち消して等倍のまま、溜めの見た目（光る玉・溜めの粒）は銃と一緒に伸び縮みさせる
    private void UpdateAutoScale(VRCPlayerApi owner)
    {
        float s = 1f;
        if (autoScale && owner != null && owner.IsValid() && scaleBaseEyeHeight > 0.01f)
            s = Mathf.Clamp(owner.GetAvatarEyeHeightAsMeters() / scaleBaseEyeHeight, minAutoScale, maxAutoScale);
        if (Mathf.Abs(s - _autoScale) < 0.005f) return;
        _autoScale = s;
        transform.localScale = Vector3.one * s;
        if (nozzle != null)
        {
            nozzle.localScale = Vector3.one / s;
            if (!_aligned) nozzle.localPosition = _nozzleLocalPos;
        }
        if (!_chargeFxPosSaved)
        {
            _chargeFxPosSaved = true;
            if (chargeOrb != null) _orbPos = chargeOrb.localPosition;
            if (chargeParticles != null) _chargePsPos = chargeParticles.transform.localPosition;
            if (fullBurst != null) _burstPsPos = fullBurst.transform.localPosition;
        }
        if (chargeOrb != null) chargeOrb.localPosition = _orbPos * s;
        if (chargeParticles != null) { chargeParticles.transform.localScale = Vector3.one * s; chargeParticles.transform.localPosition = _chargePsPos * s; }
        if (fullBurst != null) { fullBurst.transform.localScale = Vector3.one * s; fullBurst.transform.localPosition = _burstPsPos * s; }
    }

    private Vector3 _orbPos, _chargePsPos, _burstPsPos;
    private bool _chargeFxPosSaved;

    public float GetAutoScale() { return _autoScale; }

    private void UpdateVisibility()
    {
        if (visualRoot != null) visualRoot.SetActive(_isMine || _held);
    }

    // 押してから少し(chargeDelay)待ってから溜め始め、離した時の溜め具合で撃つ。
    // 連打（すぐ離す）の時は溜めの演出が出ないように、待ち時間の間は演出を始めない
    // 押した瞬間はクールダウン中でも受け付ける（連射中に押すと、その押しが丸ごと無視されて
    // 長押ししても溜まらないことがあった）。クールダウンは撃つ時(Fire)だけで見る
    public override void OnPickupUseDown()
    {
        _charging = true;
        _chargeStart = Time.time;
        _chargeFxSent = false;
    }

    public override void OnPickupUseUp()
    {
        if (!_charging) return;
        _charging = false;
        float charge = GetCharge();
        if (Time.time < _nextPuff) return;
        // どの撃ち方でも1発分の空気を使う（溜めている間は回復するので、溜め撃ちは実質プラス）。足りなければ空振り
        float cost = airPerTap;
        if (_air + 0.0001f < cost)
        {
            if (_chargeFxSent) SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChargeCancelFx));
            if (emptySound != null) emptySound.Play();
            return;
        }
        _air = Mathf.Max(0f, _air - cost);
        Fire(charge);
    }

    [Tooltip("溜めている間、風の範囲にいるふわふわを銃口へ引き寄せる強さ（加速度 m/s²、満タンでこの値）。ささやかに")]
    public float chargePull = 0.5f;
    [Tooltip("銃口からこの距離(m)より近いふわふわは引き寄せない（くっつかないように）")]
    public float chargePullMinDist = 0.6f;
    [Tooltip("引き寄せが届く距離(m)。撃った時の射程より短くする（遠くのふわふわまで寄せると便利すぎるので）")]
    public float chargePullRange = 1.5f;

    // 溜めている間のささやかな引き寄せ（自分の銃だけ）。範囲は撃った時と同じ（その時の溜めの射程）
    private void UpdateChargePull()
    {
        if (!_isMine || !_charging || targetBall == null || chargePull <= 0f) return;
        if (Time.time - _chargeStart < chargeDelay) return;
        float charge = GetCharge();
        float reach = Mathf.Min(range * Mathf.Lerp(minRangeRatio, maxRangeRatio, charge), chargePullRange);
        Transform n = nozzle != null ? nozzle : transform;
        Vector3 origin = n.position;
        Vector3 aim = n.forward;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (desktopAimWithView && local != null && !local.IsUserInVR())
            aim = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
        Vector3 to = targetBall.transform.position - origin;
        if (to.magnitude < chargePullMinDist || !InShot(origin, aim, reach, to)) return;
        // 引くのは水平方向だけ（上下に引くと、浮かせ続けたり床に引きずり下ろしたりできてしまうので）
        Vector3 flat = new Vector3(-to.x, 0f, -to.z);
        if (flat.sqrMagnitude < 0.0001f) return;
        targetBall.SetChargePull(flat.normalized * chargePull * Mathf.Lerp(0.3f, 1f, charge));
    }

    // 空気の回復（自分の銃だけ）。溜めている間はポンプで入れる感じで早く回復、撃たずにいると自然回復
    private void UpdateAir()
    {
        if (!_isMine) return;
        UpdateChargePull();
        if (_charging && Time.time - _chargeStart >= chargeDelay)
        {
            if (airChargeRefillTime > 0.001f) _air += Time.deltaTime / airChargeRefillTime;
        }
        else if (Time.time - _lastShotTime >= airRegenDelay)
        {
            if (airRegenTime > 0.001f) _air += Time.deltaTime / airRegenTime;
        }
        _air = Mathf.Clamp01(_air);
        _airSync = _air;
        if (hud != null)
        {
            hud.SetAir(_air, _air + 0.0001f < airPerTap, _held);
            hud.SetCharge(_charging ? GetCharge() : 0f);
        }
        UpdateGunGauge();
    }

    private float ShownAir()
    {
        return _isMine ? _air : Mathf.Clamp01(_airSync);
    }

    // 銃の側面の空気ゲージ
    private void UpdateGunGauge()
    {
        if (gunGauges == null) return;
        // 自分が持っている時だけ（VRもデスクトップも。デスクトップは画面下のゲージと両方出る）
        bool show = _isMine && _held;
        if (_gunGaugeMats == null) _gunGaugeMats = new Material[gunGauges.Length];
        Color c = hud != null ? hud.GetGaugeColor() : Color.white;
        for (int i = 0; i < gunGauges.Length; i++)
        {
            Renderer r = gunGauges[i];
            if (r == null) continue;
            if (r.enabled != show) r.enabled = show;
            if (!show) continue;
            if (_gunGaugeMats[i] == null) _gunGaugeMats[i] = r.material;
            _gunGaugeMats[i].SetFloat("_Fill", _air);
            _gunGaugeMats[i].SetColor("_FillColor", c);
        }
    }

    private void SetupBulbMaterial()
    {
        if (_bulbMat != null || pumpBulb == null) return;
        Renderer r = pumpBulb.GetComponent<Renderer>();
        if (r == null) return;
        _bulbMat = r.material;
        _bulbBaseColor = _bulbMat.color;
        _bulbLastColor = _bulbBaseColor;
    }

    private float GetCharge()
    {
        float held = Time.time - _chargeStart - chargeDelay;
        if (held <= 0f) return 0f;
        return chargeTime > 0.001f ? Mathf.Clamp01(held / chargeTime) : 1f;
    }

    // 溜めの見た目と音（持ち主以外にも見える）
    public void ChargeStartFx()
    {
        _fxCharging = true;
        _fxStart = Time.time;
        _fxFull = false;
        if (chargeSound != null) chargeSound.Play();
        if (chargeParticles != null) chargeParticles.Play();
    }

    public void ChargeCancelFx()
    {
        StopChargeFx();
    }

    private void StopChargeFx()
    {
        _fxCharging = false;
        _fxFull = false;
        if (chargeSound != null) chargeSound.Stop();
        if (chargeParticles != null) chargeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetOrb(0f);
    }

    private void Update()
    {
        // 自分が押しっぱなしで待ち時間を過ぎたら、溜めの演出を始める（全員に）
        if (_charging && !_chargeFxSent && Time.time - _chargeStart >= chargeDelay)
        {
            _chargeFxSent = true;
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(ChargeStartFx));
        }
        UpdateAir();
        UpdateBulb();
        if (!_fxCharging) return;
        float c = chargeTime > 0.001f ? Mathf.Clamp01((Time.time - _fxStart) / chargeTime) : 1f;
        // 持ち主が撃たずに手を離した等で発射イベントが来ない時の保険
        if (Time.time - _fxStart > 8f) { StopChargeFx(); return; }
        if (c >= 1f && !_fxFull)
        {
            _fxFull = true;
            if (chargedSound != null) chargedSound.Play();
            if (fullBurst != null) fullBurst.Play();
        }
        SetOrb(c);
    }

    // ポンプの膨らみ。溜め中は溜めに合わせて膨らみ、撃った直後はしぼむ（ちょん押しは小さく一瞬ふくらむ）
    private void UpdateBulb()
    {
        if (pumpBulb == null) return;
        float inflate = 0f;
        float since = Time.time - _bulbShotTime;
        if (_fxCharging)
        {
            float c = chargeTime > 0.001f ? Mathf.Clamp01((Time.time - _fxStart) / chargeTime) : 1f;
            float ease = 1f - (1f - c) * (1f - c);
            inflate = bulbMaxInflate * ease;
            if (c >= 1f) inflate += 0.04f * Mathf.Sin(Time.time * 28f);   // 満タンでぷるぷる
        }
        else if (_bulbTap && since < 0.16f)
        {
            inflate = bulbTapInflate * Mathf.Sin(Mathf.PI * since / 0.16f);
        }
        else if (!_bulbTap && since < 0.3f)
        {
            float t = since / 0.3f;
            // 一気にしぼんで、少し凹んでから戻る
            inflate = _bulbShotFrom * Mathf.Max(0f, 1f - t * 3f) - 0.12f * Mathf.Sin(Mathf.PI * t);
        }
        // 空気の残りでポンプの大きさと色が変わる（空気ゲージ）。他人の銃は同期された値で
        float airScale = Mathf.Lerp(bulbEmptyScale, 1f, ShownAir());
        UpdateBulbColor();
        if (Mathf.Abs(inflate - _bulbInflate) < 0.0001f && inflate == 0f && Mathf.Abs(airScale - _bulbAirScale) < 0.0001f) return;
        _bulbInflate = inflate;
        _bulbAirScale = airScale;
        // 手に当たらないよう、下端を固定して上側へ膨らむ
        float up = 1f + inflate * 1.3f;
        Vector3 bs = _bulbBaseScale * airScale;
        pumpBulb.localScale = new Vector3(bs.x * (1f + inflate * 0.9f), bs.y * up, bs.z * (1f + inflate * 0.8f));
        pumpBulb.localPosition = _bulbBasePos + Vector3.up * (0.5f * _bulbBaseScale.y * (airScale * up - 1f));
    }

    // 空気が減るとくすんだ色に、ちょん押しできない量まで減ると赤っぽく点滅
    private void UpdateBulbColor()
    {
        if (_bulbMat == null) return;
        float air = ShownAir();
        Color c = Color.Lerp(bulbEmptyColor, _bulbBaseColor, air);
        if (air + 0.0001f < airPerTap)
        {
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
            c = Color.Lerp(bulbEmptyColor, bulbWarnColor, blink);
        }
        if (c == _bulbLastColor) return;
        _bulbLastColor = c;
        _bulbMat.color = c;
    }

    private void SetOrb(float charge)
    {
        if (chargeOrb == null) return;
        bool on = charge > 0f;
        if (chargeOrb.gameObject.activeSelf != on) chargeOrb.gameObject.SetActive(on);
        if (!on) return;
        // 溜まるほど震えが速くなり、満タンで大きく脈打つ
        float wobble = charge >= 1f ? 1f + 0.2f * Mathf.Sin(Time.time * 32f) : 1f + 0.06f * Mathf.Sin(Time.time * (20f + 40f * charge));
        chargeOrb.localScale = Vector3.one * orbMaxScale * Mathf.Lerp(0.2f, 1f, charge) * wobble * _autoScale;
        if (_orbMat != null)
        {
            // 水色 → 白 → 満タンで金色
            Color c = charge < 1f
                ? Color.Lerp(new Color(0.55f, 0.85f, 1f, 0.4f), new Color(1f, 1f, 1f, 0.9f), charge)
                : Color.Lerp(new Color(1f, 0.85f, 0.35f, 0.95f), new Color(1f, 1f, 0.9f, 1f), 0.5f + 0.5f * Mathf.Sin(Time.time * 32f));
            _orbMat.color = c;
        }
    }

    // テストや他のスクリプト用：最大溜めで撃つ
    public void Puff()
    {
        Fire(1f);
    }

    private void Fire(float charge)
    {
        if (Time.time < _nextPuff) return;
        _nextPuff = Time.time + cooldown;
        _lastShotTime = Time.time;

        SendCustomNetworkEvent(NetworkEventTarget.All, nameof(PlayShotEffect), charge);
        float ballPower = Mathf.Lerp(minBallPower, maxBallPower, charge);
        float enemyPower = Mathf.Lerp(minEnemyPower, 1f, charge);
        float reach = range * Mathf.Lerp(minRangeRatio, maxRangeRatio, charge);

        Transform n = nozzle != null ? nozzle : transform;
        Vector3 origin = n.position;
        Vector3 aim = n.forward;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (desktopAimWithView && local != null && !local.IsUserInVR())
        {
            aim = local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
        }

        // フルチャージの時だけ、モンスターを少し遠くまで押し飛ばす
        if (charge >= 0.99f) enemyPower *= fullChargeEnemyBoost;
        PushEnemies(origin, aim, reach, enemyPower, charge >= enemyStunCharge);
        if (targetBall != null && targetBall.gameObject.activeInHierarchy)
        {
            Vector3 vb = ShotVelocity(origin, aim, reach, ballPower, targetBall.transform.position);
            if (vb != Vector3.zero) targetBall.Blow(vb);
        }
        if (sharedBalls == null) return;
        for (int i = 0; i < sharedBalls.Length; i++)
        {
            FuwaSharedBall sb = sharedBalls[i];
            if (sb == null || !sb.gameObject.activeInHierarchy) continue;
            Vector3 vs = ShotVelocity(origin, aim, reach, ballPower, sb.transform.position);
            if (vs != Vector3.zero) sb.BlowShared(vs);
        }
    }

    // 風が当たっていれば、ふわふわに与える速度変化（当たっていなければ zero）
    private Vector3 ShotVelocity(Vector3 origin, Vector3 aim, float reach, float ballPower, Vector3 ballPos)
    {
        Vector3 toBall = ballPos - origin;
        if (!InShot(origin, aim, reach, toBall)) return Vector3.zero;
        // 銃口より後ろ（0距離の筒）にいる時は距離0扱い＝いちばん強く
        float dist = Vector3.Dot(toBall, aim) > 0f ? Mathf.Min(toBall.magnitude, reach) : 0f;

        // 近いほど強く、遠いほど弱い
        float falloff = Mathf.Lerp(nearBoost, minFalloff, dist / reach) * ballPower;
        // 風は「向いてる水平方向に押す」＋「必ず上に持ち上げる」の合成。
        // 下の玉を狙っても床に押し付けず、見上げて吹くほど高く上がる。
        // 見上げるほど横に押す分は減り、真下から撃つと真上に飛ぶ。
        Vector3 flatRaw = Vector3.ProjectOnPlane(aim, Vector3.up);
        float side = aim.y > 0f ? flatRaw.magnitude : 1f;
        Vector3 flat = flatRaw.sqrMagnitude > 0.0001f ? flatRaw.normalized : Vector3.zero;
        float lift = liftPower + Mathf.Max(0f, aim.y) * aimLiftBonus;
        // 風の中心線から外れている分だけ、中心線の方へ吸い寄せる（気流に乗る感じ）
        Vector3 offAxis = toBall - aim * Vector3.Dot(toBall, aim);
        Vector3 pull = -offAxis * suction;
        pull.y = Mathf.Min(pull.y, 0f) * 0.5f;   // 下向きの吸い寄せは弱めに（床に叩きつけない）
        Vector3 v = (flat * pushPower * side + Vector3.up * lift) * falloff + pull * ballPower;
        return v == Vector3.zero ? Vector3.up * 0.0001f : v;
    }

    // 風の広がりの中か。狙った向きより上にずれている分だけ、角度の上限を coneAngle → coneAngleTop へ狭める
    // （真上にずれてたら coneAngleTop、真横〜下は coneAngle、その間はなめらかに）
    private bool InCone(Vector3 aim, Vector3 to)
    {
        float angle = Vector3.Angle(aim, to);
        if (angle <= Mathf.Min(coneAngle, coneAngleTop)) return true;
        Vector3 upPerp = Vector3.ProjectOnPlane(Vector3.up, aim);
        Vector3 off = Vector3.ProjectOnPlane(to, aim);
        float upness = 0f;
        if (upPerp.sqrMagnitude > 0.0001f && off.sqrMagnitude > 0.0001f)
            upness = Mathf.Max(0f, Vector3.Dot(off.normalized, upPerp.normalized));
        return angle <= Mathf.Lerp(coneAngle, coneAngleTop, upness);
    }

    [Tooltip("超至近距離の救済：銃口から後ろ（手元側）へこの長さ(m)の筒の中も当たりにする")]
    public float nearBackLength = 0.4f;
    [Tooltip("その筒の太さ（半径m）")]
    public float nearRadius = 0.35f;

    // 当たり判定：風の広がり（円すい）の中か、銃口から後ろへ伸ばした筒（0距離の部分）の中
    private bool InShot(Vector3 origin, Vector3 aim, float reach, Vector3 to)
    {
        if (to.magnitude <= reach && InCone(aim, to)) return true;
        if (nearBackLength <= 0f || nearRadius <= 0f) return false;
        float along = Vector3.Dot(to, aim);   // 銃口より前ならプラス
        if (along < -nearBackLength) return false;   // 筒の後ろの端は平ら（手元より後ろまでは広げない）
        float t = Mathf.Clamp(along, -nearBackLength, 0f);
        return (to - aim * t).sqrMagnitude <= nearRadius * nearRadius;
    }

    // 風の届く範囲にいる敵を押し返す
    private void PushEnemies(Vector3 origin, Vector3 aim, float reach, float power, bool stun)
    {
        // 銃口の後ろの筒（0距離の部分）まで入るように少し大きめに拾う
        Collider[] hits = Physics.OverlapSphere(origin + aim * (reach - nearBackLength) * 0.5f, Mathf.Max(reach * 0.6f, (reach + nearBackLength) * 0.5f + nearRadius), -1, QueryTriggerInteraction.Collide);
        Vector3 flat = Vector3.ProjectOnPlane(aim, Vector3.up).normalized;
        foreach (Collider c in hits)
        {
            if (c == null) continue;
            Vector3 to = c.transform.position - origin;
            if (!InShot(origin, aim, reach, to)) continue;
            float d = Vector3.Dot(to, aim) > 0f ? Mathf.Min(to.magnitude, reach) : 0f;
            FuwaEnemy e = c.GetComponent<FuwaEnemy>();
            if (e != null) e.Blown(flat * pushPower * Mathf.Lerp(nearBoost, minFalloff, d / reach) * power, stun);
            FuwaStartMushroom m = c.GetComponent<FuwaStartMushroom>();
            if (m != null) m.Hit(aim);
        }
    }

    [Tooltip("風の粒の飛距離＝速さ×寿命。これが射程と同じになるように速さを変える")]
    public float puffBaseDistance = 1.75f;

    // 撃った時の見た目と音（全員）。風の粒は、その溜めの射程と同じ所まで飛ぶ
    [NetworkCallable]
    public void PlayShotEffect(float charge)
    {
        _bulbShotTime = Time.time;
        _bulbTap = charge < 0.2f;
        _bulbShotFrom = _bulbInflate;
        if (puffEffect != null)
        {
            float reach = range * Mathf.Lerp(minRangeRatio, maxRangeRatio, Mathf.Clamp01(charge));
            ParticleSystem.MainModule main = puffEffect.main;
            main.startSpeedMultiplier = _puffBaseSpeed * (puffBaseDistance > 0.001f ? reach / puffBaseDistance : 1f);
        }
        if (charge >= 1f) PlayPuffEffectStrong();
        else PlayPuffEffect();
    }

    public void PlayPuffEffect()
    {
        StopChargeFx();
        if (puffEffect != null) puffEffect.Play();
        if (puffSound != null)
        {
            puffSound.volume = _puffBaseVolume * 0.6f;
            puffSound.pitch = _puffBasePitch * 1.15f;
            puffSound.Play();
        }
    }

    // 最大溜めの時：低めで大きい音
    public void PlayPuffEffectStrong()
    {
        StopChargeFx();
        if (puffEffect != null) { puffEffect.Play(); puffEffect.Emit(strongPuffExtra); }
        if (fullBurst != null) fullBurst.Play();
        if (puffBigSound != null) puffBigSound.Play();
        else if (puffSound != null)
        {
            puffSound.volume = _puffBaseVolume;
            puffSound.pitch = _puffBasePitch * 0.9f;
            puffSound.Play();
        }
    }
}
