using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 自分の玉の「分身（ゴースト）」。PlayerObjectで1人1個配られる。
// 持ち主は毎フレーム自分の玉の位置をコピーし（VRCObjectSyncで他の人へ送られる）、
// 他の人の画面では半透明の毛玉として見える。自分の分身は自分には見せない。
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
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

    public bool TrapActive() { return _trapEndMs != 0 && Networking.GetServerTimeInMilliseconds() < _trapEndMs; }
    public Vector3 GetTrapPos() { return _trapPos; }
    public int GetTrapEnd() { return _trapEndMs; }

    public bool IsMine() { return _isMine; }

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
        _shownHidden = _hidden;
        if (ghostRenderer != null) ghostRenderer.enabled = !_isMine && !_hidden;
        if (trail != null) trail.emitting = !_isMine && !_hidden;
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

        if (_isMine && ball != null)
        {
            transform.SetPositionAndRotation(ball.transform.position, ball.transform.rotation);
            _hidden = ball.IsHidden();
        }
        if (_hidden != _shownHidden && !_isMine)
        {
            _shownHidden = _hidden;
            if (ghostRenderer != null) ghostRenderer.enabled = !_hidden;
            if (trail != null) { trail.Clear(); trail.emitting = !_hidden; }
        }
    }
}
