using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2026-11-14 2–5 上の段（てじゃ「柱が倒れてくギミック」）：倒れてくる柱（自分の画面だけ・同期なし。転がる岩 FuwaBoulder と同じ）
//  ・プレイヤーかふわふわが arm のまわり armRadius に入ると、グラグラ（wobbleTime 秒）→ バタンと倒れる
//  ・このスクリプトの物体が「ちょうつがい」。親の座標で +z が倒れる向き、ちょうつがいの X 軸まわりに fallAngle 度まわる
//  ・倒れる最後のほうで、ちょうつがいの位置を startPos → endPos へずらす（道のすき間にはまる・階段にささる用。ふつうは同じ所）
//  ・倒れている間だけ hit（このGameObjectのトリガー）が有効：当たったふわふわ・プレイヤーを倒れる向きへ大きく吹っ飛ばす
//  ・lieTime > 0：倒れて lieTime 秒したら起き上がる（じゃまな柱）。0：倒れたまま（橋・坂になる柱）
//  ・プレイヤーもふわふわも resetFar より遠くなったら、立った状態にもどす（コースをやり直した時用）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaFallingPillar : UdonSharpBehaviour
{
    public Transform arm;
    public float armRadius = 5f;
    public Transform ball;
    public float wobbleTime = 1.1f;
    public float wobbleDeg = 3.5f;
    public float fallTime = 0.75f;
    public float fallAngle = 90f;
    public Vector3 startPos, endPos;
    [Tooltip("ちょうつがいを endPos へずらし始める、倒れる進み（0〜1）")]
    public float slideFrom = 0.75f;
    public float lieTime = 0f;
    public float riseTime = 1.4f;
    public float resetFar = 45f;

    [Header("当たった時（倒れる向き＝親の +z）")]
    public float ballPush = 9f, ballUp = 4f;
    public float playerPush = 10f, playerUp = 6f;

    [Tooltip("柱の当たり（倒れている間だけ、ふつうのレイヤーにする：道と同じレイヤーだとふわふわが触れた瞬間アウトになって、吹っ飛ばない）")]
    public GameObject body;
    public int bodyLayer = 11;

    [Header("音")]
    public AudioSource rumble;
    public AudioSource thud;

    private Collider _hit;
    private int _state;        // 0 立ってる / 1 グラグラ / 2 倒れる / 3 倒れた / 4 起き上がる
    private float _t0;
    private bool _armed = true;
    private Quaternion _rot0;

    private void Start()
    {
        _hit = GetComponent<Collider>();
        _rot0 = Quaternion.identity;
        ResetStand();
    }

    private void OnEnable()
    {
        if (_hit != null) ResetStand();
    }

    private void ResetStand()
    {
        _state = 0; _armed = true;
        transform.localPosition = startPos;
        transform.localRotation = _rot0;
        if (_hit != null) _hit.enabled = false;
        if (body != null) body.layer = bodyLayer;
        if (rumble != null) rumble.Stop();
    }

    private bool Near(Vector3 c, float r)
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p != null && (p.GetPosition() - c).sqrMagnitude < r * r) return true;
        if (ball != null && ball.gameObject.activeInHierarchy && (ball.position - c).sqrMagnitude < r * r) return true;
        return false;
    }

    private void Update()
    {
        if (arm == null) return;
        Vector3 c = arm.position;
        float now = Time.time;
        if (_state != 0 && !Near(transform.parent.position, resetFar)) { ResetStand(); return; }
        if (_state == 0)
        {
            bool near = Near(c, armRadius);
            if (!_armed) { if (!near) _armed = true; return; }
            if (near) { _state = 1; _t0 = now; if (rumble != null) rumble.Play(); }
            return;
        }
        float k = now - _t0;
        if (_state == 1)
        {
            // グラグラ：だんだん大きく、最後は倒れる向きへ少しかたむく
            float a = Mathf.Clamp01(k / wobbleTime);
            float w = Mathf.Sin(k * 38f) * wobbleDeg * (0.4f + 0.6f * a) + a * a * 2.5f;
            float s = Mathf.Sin(k * 29f + 1.3f) * wobbleDeg * 0.5f * a;
            transform.localRotation = Quaternion.Euler(w, 0f, s);
            if (k >= wobbleTime) { _state = 2; _t0 = now; if (_hit != null) _hit.enabled = true; if (body != null) body.layer = 0; }
            return;
        }
        if (_state == 2)
        {
            float u = Mathf.Clamp01(k / fallTime);
            Pose(u * u);
            if (u >= 1f)
            {
                _state = 3; _t0 = now;
                if (_hit != null) _hit.enabled = false;
                if (body != null) body.layer = bodyLayer;
                if (rumble != null) rumble.Stop();
                if (thud != null) thud.Play();
            }
            return;
        }
        if (_state == 3)
        {
            // ドスン：ちょっとはねる
            float b = k < 0.3f ? Mathf.Sin(k / 0.3f * Mathf.PI) * 2.2f * (1f - k / 0.3f) : 0f;
            Pose(1f, b);
            if (lieTime > 0f && k >= lieTime) { _state = 4; _t0 = now; }
            return;
        }
        if (_state == 4)
        {
            float u = Mathf.Clamp01(k / riseTime);
            Pose(1f - Mathf.SmoothStep(0f, 1f, u));
            if (u >= 1f) { _state = 0; _armed = false; transform.localRotation = _rot0; transform.localPosition = startPos; }
        }
    }

    // f：倒れる進み（0 立ってる〜1 倒れた）。bounce：はね返りの角度
    private void Pose(float f, float bounce = 0f)
    {
        transform.localRotation = Quaternion.Euler(fallAngle * f - bounce, 0f, 0f);
        float g = slideFrom < 1f ? Mathf.Clamp01((f - slideFrom) / (1f - slideFrom)) : 0f;
        transform.localPosition = Vector3.Lerp(startPos, endPos, g * g * (3f - 2f * g));
    }

    private Vector3 Away()
    {
        Vector3 f = transform.parent != null ? transform.parent.forward : Vector3.forward;
        f.y = 0f;
        return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_state != 2 || other == null) return;
        FuwaBall b = other.GetComponent<FuwaBall>();
        if (b == null) return;
        b.Launch(Away() * ballPush + Vector3.up * ballUp);
    }

    private void OnTriggerStay(Collider other)
    {
        OnTriggerEnter(other);
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (_state != 2 || player == null || !player.isLocal) return;
        player.SetVelocity(Away() * playerPush + Vector3.up * playerUp);
    }

    public override void OnPlayerTriggerStay(VRCPlayerApi player)
    {
        OnPlayerTriggerEnter(player);
    }
}
