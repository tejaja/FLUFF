using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;

// あそびばの跳ねるキノコ（コース5 きのこのたに を誰かがクリアすると出てくる）。持って動かせて、いつも水平のまま（2026-10-13 てじゃ「水平固定で動かせるように」）。
// あそびばのイバラ（コース4のおもちゃ）も同じ仕組みで持って動かす。
// このスクリプトは持つところ（VRCPickup＋VRCObjectSync、見た目なし）に付ける。見た目・傘の当たり・ジャンプ台（body）は持つところの子にしない
// （Rigidbody の子になるとジャンプ台のトリガーの知らせが届かなくなる。竜巻のたねと同じ）ので、毎フレーム持つところの位置と水平の向きへ合わせる。
// 傾けて置くと「横を向いた傘に当たっても真上へ跳ねる」変な動きになるので、傾きは見た目にも出さない
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaMovableMushroom : UdonSharpBehaviour
{
    public Transform body;
    [Tooltip("手を離した時に立てる地面のレイヤー")]
    public LayerMask ground = 1 << 11;
    [Tooltip("下にこれ以上地面がなければ、離したその場に浮かべる")]
    public float maxDrop = 6f;
    [Tooltip("持っている間だけ（自分の画面だけ）body の見た目をこの半透明の材質にする（2026-11-16 てじゃ「デカいから岩みたいに半透明化」「きのこ台にも半透明化」）。空なら変えない")]
    public Material heldMaterial;
    [Tooltip("持ってトリガー（Use）を押した時にこの UdonBehaviour へ送るイベント（あそびばの貝・氷のオン/オフ。2026-11-16 てじゃ「B」）")]
    public UdonSharpBehaviour useTarget;
    public string useEvent = "Toggle";
    public override void OnPickupUseDown() { if (useTarget != null) { useTarget.SendCustomEvent(useEvent); StartFlash(); } }
    // 2026-11-19 てじゃ「オンオフ切り替え式のおもちゃをクリックでオンオフしたとき、一瞬白っぽく明滅するようにしよ」
    //  → 同日「フワッと光るのを一回でいいよ」：オン/オフした時、自分の画面で body の見た目を白くフワッと光らせて、flashTime 秒でゆっくり消す
    //  （ToonLit の _EmissionColor を MaterialPropertyBlock で。ほかのシェーダーは光らない）
    [Tooltip("フワッと光る色・強さ・消えるまでの秒")]
    public Color flashColor = new Color(1f, 0.97f, 0.92f, 1f);
    public float flashStrength = 0.8f, flashTime = 0.5f;
    [Tooltip("（前の版の白い材質。今は使わない）")]
    public Material flashMaterial;
    private float _flashAt = -10f;
    private bool _flashing;
    private MaterialPropertyBlock _mpb;
    private void StartFlash()
    {
        if (body == null) return;
        CacheVis();
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        _flashAt = Time.time; _flashing = true;
    }
    private void UpdateFlash()
    {
        if (!_flashing || _vis == null) return;
        float u = (Time.time - _flashAt) / Mathf.Max(0.01f, flashTime);
        // 出だしはすぐ明るく、あとはゆっくり消える
        float k = u >= 1f ? 0f : (u < 0.12f ? u / 0.12f : 1f - (u - 0.12f) / 0.88f);
        k = k * k * (3f - 2f * k);
        Color c = flashColor * (flashStrength * k);
        for (int i = 0; i < _vis.Length; i++)
        {
            if (_vis[i] == null) continue;
            _vis[i].GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", c);
            _vis[i].SetPropertyBlock(_mpb);
        }
        if (u >= 1f) _flashing = false;
    }
    private Renderer[] _vis;
    private Material[][] _visMats;

    private Vector3 _lastPos = new Vector3(0f, -9999f, 0f);
    private float _lastYaw;

    // 持たれている間は、傘の当たり・ジャンプ台・イバラの判定を切る（全員の画面で）。
    // 乗ったまま持つと、持ち上げた傘に押し上げられて手も上がり、どんどん上へ飛んでいった（2026-10-13 てじゃ）
    private VRCPickup _pickup;
    private Collider[] _cols;
    private GameObject[] _boosts;
    private bool _inited;
    private bool _shownHeld;

    private void Init()
    {
        if (_inited) return;
        _inited = true;
        _pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        if (body == null) { _cols = new Collider[0]; _boosts = new GameObject[0]; return; }
        _cols = body.GetComponentsInChildren<Collider>(true);
        FuwaJumpBoost[] jb = body.GetComponentsInChildren<FuwaJumpBoost>(true);
        _boosts = new GameObject[jb.Length];
        for (int i = 0; i < jb.Length; i++) _boosts[i] = jb[i].gameObject;
    }

    public bool IsHeldNow() { return _pickup != null && _pickup.IsHeld; }

    private void UpdateHeld()
    {
        Init();
        bool held = _pickup != null && _pickup.IsHeld;
        // 2026-11-19 てじゃ「上に乗れる系のおもちゃ（歯車・氷）、持ってる間は乗れないようにしたい。ピックアップで自分が乗ると無限に飛んじゃう」：
        //  おもちゃのスクリプトが当たりをオンに戻すことがある（氷を持ったままオンにすると、広がる氷の当たりがオンになる）ので、持っている間は毎フレーム切る
        if (held) for (int i = 0; i < _cols.Length; i++) if (_cols[i] != null && _cols[i].enabled && (_boosts.Length == 0 || _cols[i].GetComponent<FuwaJumpBoost>() == null)) _cols[i].enabled = false;
        if (held == _shownHeld) return;
        _shownHeld = held;
        // ジャンプ台はオブジェクトごと止める（OnDisable で上がったジャンプ力が元に戻る。判定だけ切ると出た知らせが来ず戻らない）
        for (int i = 0; i < _boosts.Length; i++) if (_boosts[i] != null) _boosts[i].SetActive(!held);
        for (int i = 0; i < _cols.Length; i++) if (_cols[i] != null && (_boosts.Length == 0 || _cols[i].GetComponent<FuwaJumpBoost>() == null)) _cols[i].enabled = !held;
        // 離したら、おもちゃ自身に当たりのオン/オフを決め直してもらう（氷が出ていない時の氷の当たりなど）
        if (!held && useTarget != null) useTarget.SendCustomEvent("ReapplyAfterDrop");
    }

    public override void OnPickup() { SetGhost(true); }

    private void CacheVis()
    {
        if (_vis != null || body == null) return;
        // 見た目（メッシュ）だけ。粒（ParticleSystemRenderer）は変えない
        MeshRenderer[] mr = body.GetComponentsInChildren<MeshRenderer>(true);
        _vis = new Renderer[mr.Length]; _visMats = new Material[mr.Length][];
        for (int i = 0; i < mr.Length; i++) { _vis[i] = mr[i]; _visMats[i] = mr[i].sharedMaterials; }
    }

    private void SetAll(Material m)
    {
        CacheVis();
        if (_vis == null) return;
        for (int i = 0; i < _vis.Length; i++)
        {
            if (_vis[i] == null) continue;
            Material[] g = new Material[_visMats[i].Length];
            for (int k = 0; k < g.Length; k++) g[k] = m;
            _vis[i].sharedMaterials = g;
        }
    }

    private void SetGhost(bool on)
    {
        if (body == null) return;
        if (on) { if (heldMaterial != null) SetAll(heldMaterial); return; }
        CacheVis();
        if (_vis == null) return;
        for (int i = 0; i < _vis.Length; i++) if (_vis[i] != null) _vis[i].sharedMaterials = _visMats[i];
    }

    // 手を離したら、その場の地面にまっすぐ立てる（持っていた人の画面で。位置は VRCObjectSync が配る）
    public override void OnDrop()
    {
        SetGhost(false);
        Vector3 p = transform.position;
        RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * 0.3f, Vector3.down, maxDrop + 0.3f, ground, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Vector3 at = p;
        for (int i = 0; i < hits.Length; i++)
        {
            // 自分の傘には立たない
            if (body != null && hits[i].collider.transform.IsChildOf(body)) continue;
            if (hits[i].distance < best) { best = hits[i].distance; at = hits[i].point; }
        }
        Vector3 f = transform.forward; f.y = 0f;
        Quaternion rot = f.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(f.normalized) : Quaternion.identity;
        transform.SetPositionAndRotation(at, rot);
        Follow();
    }

    // 持っている手の動きは PostLateUpdate の時に反映されているので、そのあとで合わせる
    public override void PostLateUpdate() { UpdateHeld(); Follow(); UpdateFlash(); }

    private void Follow()
    {
        if (body == null) return;
        Vector3 p = transform.position;
        float yaw = transform.eulerAngles.y;
        // 動いていない時は当たり判定を動かさない（止まっている当たり判定を毎フレーム動かすと重い）
        if ((p - _lastPos).sqrMagnitude < 0.000001f && Mathf.Abs(Mathf.DeltaAngle(yaw, _lastYaw)) < 0.05f) return;
        _lastPos = p; _lastYaw = yaw;
        body.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
    }
}
