using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 1回目の渡り場の先の岩の列の番をする魚（コース2–1「みずのこみち」、2026-10-27）。
// 岩の列の上の空中（川幅いっぱい）をふわふわが通ろうとすると、岩の下流側の水面から魚が飛び出して、ふわふわめがけて跳ねる。
// 当たったら川上へポーンと押し返す（2回目の渡り場の FuwaRiverFish と同じ飛ばし方）。
// てじゃ「岩の上をふわふわが通ると魚が飛び出して押し返すとか」（岩の列を飛び越えて川で2回目の渡り場まで行けてしまうのを防ぐ。見通しは悪くしない）。
// 川に浮いて岩で止まっているふわふわ（minHeight より下）には反応しない。跳んでいる魚がプレイヤーに当たったら、プレイヤーも川上へ（てじゃ「プレイヤにあたってもいいかも」）。
// 当たるのは自分のふわふわ・自分のプレイヤーだけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGuardFish : UdonSharpBehaviour
{
    public FuwaBall ball;
    public FuwaWaterFx fx;
    public Transform fish;

    [Header("見張る所（line：岩の列のまん中の水面。+Z が川上、+X が川の右岸の向き）")]
    public Transform line;
    public float halfWidth = 3f;
    [Tooltip("岩の列から川上へ・川下へ、どこまでを見張るか(m)")]
    public float zoneUp = 1.0f;
    public float zoneDown = 3.0f;
    [Tooltip("川下へこの速さ(m/s)以上で動いているふわふわだけ（すくい上げて左岸へ運ぶ動きでは出てこない）")]
    public float minDownSpeed = 0.8f;
    [Tooltip("水面からの高さ：これより下は川に浮いているふわふわなので見ない")]
    public float minHeight = 0.9f;
    public float maxHeight = 14f;
    // 岩の列：上の面の高さ（水面から）と、前後の半分の厚み(m)。岩の上に乗っているふわふわ（ゆっくり転がして越えようとする）は速さに関係なく見張る
    //（2026-10-27 てじゃ「やさしく岩に乗せて転がして進めるとすり抜けられる」）。岩の川上がわのふち（rockOnTop より川上）にいる時は見ない（岩に止まったふわふわをすくい上げる時）
    public float rockTop = 0.7f;
    public float rockHalf = 0.45f;
    public float rockOnTop = 0.15f;

    [Header("跳び方")]
    [Tooltip("魚が出てくる所：ふわふわの真下から川下へ何mずれた水面か")]
    public float startDown = 1.2f;
    [Tooltip("水面からふわふわに届くまで(秒)。ふわふわが動いても追いかける")]
    public float riseTime = 0.3f;
    [Tooltip("当たったあと水に戻るまで(秒)")]
    public float fallTime = 0.8f;
    [Tooltip("跳ぶ前の合図（ぶくぶく）の長さ(秒)")]
    public float warnLead = 0.1f;

    [Tooltip("魚の体の半分くらい（岩の上を通る時のすき間）")]
    public float fishClear = 0.55f;

    [Header("当たった時")]
    public float hitRadius = 1.0f;
    [Tooltip("川上へ飛ばす速さ(m/s)・上へ(m/s)・川のまん中へ寄せる強さ（まん中からのずれ1mあたり m/s）")]
    public float knockBack = 4.5f;
    public float knockUp = 3.0f;
    public float knockCenter = 0.8f;
    public Color knockGlow = new Color(1f, 0.55f, 0.35f, 1f);

    [Header("プレイヤーに当たった時")]
    [Tooltip("体（足から playerTop m までの縦の線）とこの距離まで近づいたら当たり")]
    public float playerHitRadius = 0.8f;
    public float playerTop = 1.5f;
    public float playerKnockBack = 4.5f;
    public float playerKnockUp = 4f;

    [Header("音")]
    public AudioSource jumpSound;
    public AudioSource hitSound;

    // 0＝待つ、1＝合図、2＝上がる、3＝落ちる
    private int _state;
    private float _t0;
    private Vector3 _from, _hitAt, _land, _prev;
    private bool _hasPrev, _playerHit;

    private void Start()
    {
        if (fish != null) fish.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        _state = 0;
        if (fish != null) fish.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (line == null || fish == null) return;
        bool ballOk = ball != null && !ball.IsWaitingOrHidden() && ball.gameObject.activeInHierarchy;
        Vector3 bp = ballOk ? ball.transform.position : Vector3.zero;
        Vector3 f = line.forward; f.y = 0f; f.Normalize();
        Vector3 r = line.right; r.y = 0f; r.Normalize();
        float now = Time.time;
        // 川下へ動く速さ（前のフレームとの差）
        float downSpeed = 0f;
        if (ballOk && _hasPrev && Time.deltaTime > 1e-4f) downSpeed = -Vector3.Dot((bp - _prev) / Time.deltaTime, f);
        _prev = bp; _hasPrev = ballOk;
        if (_state == 0)
        {
            if (!ballOk) return;
            bool overRocks = OnRocks(bp, f, r);
            if (!overRocks && (downSpeed < minDownSpeed || !InZone(bp, f, r))) return;
            // 出てくる所：ふわふわの真下から川下へ、水面
            Vector3 d = bp - line.position;
            float lat = Mathf.Clamp(Vector3.Dot(d, r), -halfWidth + 0.3f, halfWidth - 0.3f);
            _from = line.position + r * lat - f * startDown;
            _state = 1; _t0 = now;
            if (fx != null) fx.Visual(_from + Vector3.up * 0.05f, 0.35f);
            return;
        }
        if (_state == 1)
        {
            if (now - _t0 < warnLead) return;
            _state = 2; _t0 = now;
            fish.gameObject.SetActive(true);
            fish.position = _from; _playerHit = false;
            if (fx != null) fx.Visual(_from + Vector3.up * 0.05f, 0.7f);
            if (jumpSound != null) { jumpSound.transform.position = _from; jumpSound.pitch = Random.Range(0.9f, 1.1f); jumpSound.Play(); }
            return;
        }
        if (_state == 2)
        {
            // ふわふわを追いかけて上がる（ふわふわが見張る所から出たら、その場で上がりきって落ちる）
            Vector3 target = ballOk && InZoneLoose(bp, f, r) ? bp : _from + Vector3.up * 2f - f * 0.5f;
            float t = Mathf.Clamp01((now - _t0) / riseTime);
            float e = 1f - (1f - t) * (1f - t);
            Vector3 p = AboveRocks(Vector3.Lerp(_from, target, e), f);
            Vector3 tan = target - _from;
            fish.position = p;
            if (tan.sqrMagnitude > 1e-4f) fish.rotation = Quaternion.LookRotation(tan.normalized, Vector3.up);
            CheckPlayer(p, f);
            if (ballOk && (p - bp).magnitude < hitRadius) { Knock(p, f, r); StartFall(p, f); return; }
            if (t >= 1f) StartFall(p, f);
            return;
        }
        if (_state == 3)
        {
            float t = (now - _t0) / fallTime;
            if (t >= 1f)
            {
                _state = 0;
                fish.gameObject.SetActive(false);
                if (fx != null) fx.Visual(_land + Vector3.up * 0.05f, 0.5f);
                return;
            }
            // 当たった所から、川上の水面へ弧をえがいて落ちる
            float h = 0.6f;
            Vector3 p = AboveRocks(Vector3.Lerp(_hitAt, _land, t) + Vector3.up * (4f * h * t * (1f - t)), f);
            Vector3 tan = (_land - _hitAt) + Vector3.up * (4f * h * (1f - 2f * t));
            fish.position = p;
            if (tan.sqrMagnitude > 1e-4f) fish.rotation = Quaternion.LookRotation(tan.normalized, Vector3.up);
            if (t < 0.9f) CheckPlayer(p, f);
        }
    }

    // 岩の上に乗っている・岩のすぐ上（川上がわのふちは除く）
    private bool OnRocks(Vector3 p, Vector3 f, Vector3 r)
    {
        Vector3 d = p - line.position;
        float along = Vector3.Dot(d, f), lat = Vector3.Dot(d, r);
        return along < rockOnTop && along > -rockHalf - 0.6f && Mathf.Abs(lat) < halfWidth && d.y > rockTop - 0.1f && d.y < maxHeight;
    }

    // 魚が岩にめりこまないように、岩の列の上では岩の上の面より上を通す（2026-10-27 てじゃ「ギリギリを超えると岩に魚がめりこむ」）
    private Vector3 AboveRocks(Vector3 p, Vector3 f)
    {
        Vector3 d = p - line.position;
        float along = Mathf.Abs(Vector3.Dot(d, f));
        float edge = rockHalf + fishClear;
        if (along >= edge) return p;
        // 岩のふちから外へなだらかに下げる
        float k = along < rockHalf ? 1f : 1f - (along - rockHalf) / fishClear;
        float minY = line.position.y + rockTop + fishClear * k;
        if (p.y < minY) p.y = minY;
        return p;
    }

    private bool InZone(Vector3 p, Vector3 f, Vector3 r)
    {
        Vector3 d = p - line.position;
        float along = Vector3.Dot(d, f), lat = Vector3.Dot(d, r);
        return along < zoneUp && along > -zoneDown && Mathf.Abs(lat) < halfWidth && d.y > minHeight && d.y < maxHeight;
    }

    // 上がっている間は少し広めに追いかける
    private bool InZoneLoose(Vector3 p, Vector3 f, Vector3 r)
    {
        Vector3 d = p - line.position;
        float along = Vector3.Dot(d, f), lat = Vector3.Dot(d, r);
        return along < zoneUp + 1.5f && along > -zoneDown - 2f && Mathf.Abs(lat) < halfWidth + 1f && d.y > minHeight - 0.4f && d.y < maxHeight + 2f;
    }

    private void StartFall(Vector3 p, Vector3 f)
    {
        _state = 3; _t0 = Time.time; _hitAt = p;
        _land = new Vector3(p.x, line.position.y, p.z) + f * 1.2f;
    }

    // 自分のプレイヤーに当たったら川上へ（1回跳ぶあいだに1回だけ）
    private void CheckPlayer(Vector3 p, Vector3 f)
    {
        if (_playerHit) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        Vector3 pp = lp.GetPosition();
        float y = Mathf.Clamp(p.y, pp.y, pp.y + playerTop);
        if ((p - new Vector3(pp.x, y, pp.z)).magnitude >= playerHitRadius) return;
        _playerHit = true;
        lp.SetVelocity(f * playerKnockBack + Vector3.up * playerKnockUp);
        if (hitSound != null) { hitSound.transform.position = p; hitSound.pitch = Random.Range(0.8f, 0.9f); hitSound.Play(); }
    }

    private void Knock(Vector3 at, Vector3 f, Vector3 r)
    {
        float lat = Vector3.Dot(ball.transform.position - line.position, r);
        Vector3 v = Vector3.up * knockUp + f * knockBack - r * Mathf.Clamp(lat * knockCenter, -1.5f, 1.5f);
        ball.LaunchTinted(v, knockGlow);
        if (hitSound != null) { hitSound.transform.position = at; hitSound.pitch = Random.Range(0.95f, 1.1f); hitSound.Play(); }
    }
}
