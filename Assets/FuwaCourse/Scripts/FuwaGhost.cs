using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 自分の玉の「分身（ゴースト）」。PlayerObjectで1人1個配られる。
// 持ち主は毎フレーム自分の玉の位置をコピーし、動いている間だけ「位置・速さ・向き・時刻」をときどき送る（Manual同期）。
// 他の人の画面では、届いた点を速さを使ったなめらかな曲線でつないで動かす（半透明の毛玉）。自分の分身は自分には見せない。
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaGhost : UdonSharpBehaviour
{
    public FuwaBall ball;
    public Renderer ghostRenderer;
    public TrailRenderer trail;
    [Tooltip("人ごとの色の鮮やかさ")]
    [Range(0f, 1f)] public float saturation = 0.35f;

    private bool _isMine;
    // 持ち主の胞子がキノコの中に隠れている間は、分身も隠す（半透明だとキノコからはみ出て見えるため）
    [UdonSynced] private bool _hidden;
    // 吹かれるのを空中で待っている（チェックポイント・スタートで浮いている）。自分の目では分身を出さないが、おっかけカメラには映す
    [UdonSynced] private bool _waiting;
    private bool _pWaiting;
    private bool _shownHidden;

    [Header("対戦アイテム（なくてもOK）")]
    [Tooltip("アイテムの管理（シーンに1つ）。相手から届いた効果をここへ渡す")]
    public FuwaItems items;
    [Tooltip("クモの糸の見た目（持ち主が置いた場所に全員の画面で出る）")]
    public Transform trap;
    [Tooltip("シャボン玉の見た目（持ち主のふわふわを包む）")]
    public GameObject shield;
    [UdonSynced] private Vector3 _trapPos;
    [UdonSynced] private int _trapEndMs;      // サーバー時刻。これを過ぎたら消える（0=なし）
    [UdonSynced] private bool _shielded;
    [UdonSynced] private int _area;          // 持ち主が今いるエリア（0=入口 1=ぼうけん 2=たいせん）。モードの看板の人数表示用

    [Header("同期（持ち主が送る・見る側がなめらかにつなぐ）")]
    [Tooltip("動いている間に送る間隔(秒)")]
    public float sendInterval = 0.2f;
    [Tooltip("速さがこれ以上急に変わったら（撃たれた・ぶつかった）すぐ送る(m/s)")]
    public float sendOnVelocityChange = 1.5f;
    [Tooltip("見る側で、届いた時刻からこれだけ遅らせて表示する(秒)。小さいほど遅れが減る")]
    public float viewDelay = 0.08f;
    [Tooltip("これ以上離れていたら、つながずにその場へ移す(m)")]
    public float snapDistance = 4f;
    [UdonSynced] private Vector3 _sPos;
    [UdonSynced] private Vector3 _sVel;
    [UdonSynced] private Quaternion _sRot = Quaternion.identity;
    [UdonSynced] private double _sTime;

    // 持ち主
    private Vector3 _prevPos;
    private Vector3 _vel;
    private Vector3 _lastSentVel;
    private float _nextSend;
    private bool _moving;
    private bool _pHidden, _pShielded;
    private int _pTrapEnd, _pArea;
    // 見る側：A（受け取った時に表示していた状態）から B（届いた点）へつなぐ
    private Vector3 _aPos, _aVel, _bPos, _bVel;
    private Quaternion _aRot = Quaternion.identity, _bRot = Quaternion.identity;
    private double _aTime, _bTime;
    private bool _hasSample;
    private bool _sentOnce;   // 持ち主：入った直後に一度は送る（動かないと送らないので、見る側に位置が届かなかった）

    public void SetArea(int area) { if (_isMine) _area = area; }
    public int GetArea() { return _area; }

    // ===== 持ち主がセットする =====
    public void PlaceTrap(Vector3 pos, float seconds)
    {
        if (!_isMine) return;
        _trapPos = pos;
        _trapEndMs = Networking.GetServerTimeInMilliseconds() + Mathf.RoundToInt(seconds * 1000f);
    }

    public void SetShielded(bool on)
    {
        if (!_isMine) return;
        _shielded = on;
    }

    // ===== 同期 =====
    public override void OnPreSerialization()
    {
        _sPos = transform.position;
        _sVel = _vel;
        _sRot = transform.rotation;
        _sTime = Networking.GetServerTimeInSeconds();
    }

    public override void OnDeserialization()
    {
        if (_isMine) return;
        double now = Networking.GetServerTimeInSeconds() - viewDelay;
        if (!_hasSample || (transform.position - _sPos).magnitude > snapDistance || _hidden)
        {
            _hasSample = true;
            _aPos = _sPos; _aVel = _sVel; _aRot = _sRot; _aTime = now;
            transform.SetPositionAndRotation(_sPos, _sRot);
            if (trail != null) trail.Clear();
        }
        else
        {
            // 今表示している所から、届いた点へつなぎ直す（途切れない）
            _aPos = transform.position; _aVel = _dispVel; _aRot = transform.rotation; _aTime = now;
        }
        _bPos = _sPos; _bVel = _sVel; _bRot = _sRot;
        // 届いた点の時刻は少なくとも少し先にする（遅れて届いた時は、速さで先読みした位置を目標にする）
        double tb = _sTime;
        if (tb < now + 0.05)
        {
            float late = (float)(now + 0.05 - tb);
            _bPos = _sPos + _sVel * Mathf.Min(late, 0.5f);
            tb = now + 0.05;
        }
        _bTime = tb;
    }

    private Vector3 _dispVel;

    private void ViewUpdate()
    {
        if (!_hasSample) return;
        double now = Networking.GetServerTimeInSeconds() - viewDelay;
        float span = (float)(_bTime - _aTime);
        Vector3 pos; Quaternion rot;
        if (now < _bTime && span > 0.0001f)
        {
            // エルミート曲線：両はしの位置と速さが合うようにつなぐ
            float t = Mathf.Clamp01((float)(now - _aTime) / span);
            float t2 = t * t, t3 = t2 * t;
            Vector3 m0 = _aVel * span, m1 = _bVel * span;
            pos = (2f * t3 - 3f * t2 + 1f) * _aPos + (t3 - 2f * t2 + t) * m0 + (-2f * t3 + 3f * t2) * _bPos + (t3 - t2) * m1;
            _dispVel = ((6f * t2 - 6f * t) * _aPos + (3f * t2 - 4f * t + 1f) * m0 + (-6f * t2 + 6f * t) * _bPos + (3f * t2 - 2f * t) * m1) / span;
            rot = Quaternion.Slerp(_aRot, _bRot, t);
        }
        else
        {
            // 次が届くまでは、最後の速さで少しだけ先へ進める（止まっている時は動かない）
            float over = Mathf.Min((float)(now - _bTime), 0.4f);
            pos = _bPos + _bVel * over;
            _dispVel = _bVel;
            rot = _bRot;
        }
        transform.SetPositionAndRotation(pos, rot);
    }

    private void OwnerUpdate()
    {
        transform.SetPositionAndRotation(ball.transform.position, ball.transform.rotation);
        // 吹かれるのを空中で待っている間（チェックポイントやスタートで浮いている時）は、他の人の目には分身を出さない
        // （チェックポイントで他の人のふわふわと重なって見えた。とくにチュートリアル。2026-10-12 てじゃ）。おっかけカメラには映す
        _hidden = ball.IsHidden();
        _waiting = ball.IsWaiting();
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 rawVel = (transform.position - _prevPos) / dt;
        _prevPos = transform.position;
        if (rawVel.magnitude > 30f) rawVel = Vector3.zero;   // ワープした時
        _vel = Vector3.Lerp(_vel, rawVel, 1f - Mathf.Exp(-20f * dt));

        bool moving = _vel.magnitude > 0.05f;
        bool stateChanged = _hidden != _pHidden || _shielded != _pShielded || _trapEndMs != _pTrapEnd || _area != _pArea || _waiting != _pWaiting;
        bool kick = (_vel - _lastSentVel).magnitude > sendOnVelocityChange;
        bool due = moving && Time.time >= _nextSend;
        bool stopped = !moving && _moving;   // 止まった瞬間にもう一度送って、止まった位置をそろえる
        _moving = moving;
        if (stateChanged || kick || due || stopped || !_sentOnce)
        {
            _sentOnce = true;
            _pHidden = _hidden; _pShielded = _shielded; _pTrapEnd = _trapEndMs; _pArea = _area; _pWaiting = _waiting;
            _lastSentVel = _vel;
            _nextSend = Time.time + sendInterval;
            RequestSerialization();
        }
    }

    public bool TrapActive() { return _trapEndMs != 0 && Networking.GetServerTimeInMilliseconds() < _trapEndMs; }
    public Vector3 GetTrapPos() { return _trapPos; }
    public int GetTrapEnd() { return _trapEndMs; }

    public bool IsMine() { return _isMine; }
    // おっかけカメラに映すか（隠れていない・位置が届いている。浮いて待っている間も映す）
    public bool MonitorVisible() { return !_isMine && !_hidden && _hasSample && gameObject.activeInHierarchy; }

    // ===== 相手から届く（持ち主のPCで実行される） =====
    public void HitHeavy() { if (items != null) items.ReceiveHeavy(); }
    public void HitFog() { if (items != null) items.ReceiveFog(); }

    private void Start()
    {
        _isMine = Networking.IsOwner(gameObject);
        ApplyOwnerLook();
    }

    public override void OnOwnershipTransferred(VRCPlayerApi player)
    {
        _isMine = Networking.IsOwner(gameObject);
        ApplyOwnerLook();
    }

    private void ApplyOwnerLook()
    {
        // 見る側は、最初の位置が届くまで出さない（誰かが入ってきた時、届く前の分身がロビーの真ん中に一瞬出ていた。てじゃ）
        _shownHidden = !(!_isMine && !_hidden && !_waiting && _hasSample);
        if (ghostRenderer != null) ghostRenderer.enabled = !_shownHidden;
        if (trail != null) trail.emitting = !_shownHidden;
        if (_isMine || ghostRenderer == null) return;

        // 持ち主のプレイヤーIDから色を決める（人ごとに違う淡い色）
        VRCPlayerApi owner = Networking.GetOwner(gameObject);
        int id = owner != null ? owner.playerId : 0;
        float hue = Mathf.Repeat(id * 0.618034f, 1f);
        Color tip = Color.HSVToRGB(hue, saturation, 1f);
        Color root = Color.HSVToRGB(hue, saturation * 0.8f, 0.9f);
        Material m = ghostRenderer.material;
        m.SetColor("_TipColor", tip);
        m.SetColor("_Color", root);
        if (trail != null)
        {
            trail.startColor = new Color(tip.r, tip.g, tip.b, 0.35f);
            trail.endColor = new Color(tip.r, tip.g, tip.b, 0f);
        }
    }

    private void Update()
    {
        // クモの糸とシャボン玉の見た目（全員の画面）
        if (trap != null)
        {
            bool on = _trapEndMs != 0 && Networking.GetServerTimeInMilliseconds() < _trapEndMs;
            if (trap.gameObject.activeSelf != on) trap.gameObject.SetActive(on);
            // 分身は玉と一緒に回るので、糸は向きを固定して置いた場所に出す
            if (on) trap.SetPositionAndRotation(_trapPos, Quaternion.identity);
        }
        if (shield != null && shield.activeSelf != _shielded) shield.SetActive(_shielded);

        if (_isMine && ball != null) OwnerUpdate();
        else if (!_isMine) ViewUpdate();
        if (!_isMine)
        {
            bool hide = _hidden || _waiting || !_hasSample;
            if (hide != _shownHidden)
            {
                _shownHidden = hide;
                if (ghostRenderer != null) ghostRenderer.enabled = !hide;
                if (trail != null) { trail.Clear(); trail.emitting = !hide; }
            }
        }
    }
}
