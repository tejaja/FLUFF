using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 坂の上から転がってくる岩（自分の画面だけ・同期なし）。
// path の点をなぞって転がり、終点で地面に沈んで消える → 周期ごとに上から出直す。
// ふわふわに当たると坂の下へはじき返す。プレイヤーに当たると坂の下へ押し飛ばす。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaBoulder : UdonSharpBehaviour
{
    [Tooltip("転がる道すじ（ワールド座標、上→下）。岩の中心が通る点")]
    public Vector3[] path;
    [Tooltip("見た目（回転させる）")]
    public Transform visual;
    public float radius = 0.6f;
    [Tooltip("転がる速さ m/s")]
    public float speed = 4f;
    [Tooltip("出直す周期（秒）")]
    public float period = 6f;
    [Tooltip("周期のずれ（秒）")]
    public float phaseOffset = 0f;
    [Tooltip("上に出てくる時間（秒）")]
    public float emergeTime = 0.5f;
    [Tooltip("道の端から落ちていく時間（秒）。この間は重力で落ちて、最後に消える")]
    public float fallTime = 2.0f;
    [Tooltip("落ちる時に前へ進む速さの割合（穴の向こう側の壁にぶつからないように）")]
    public float fallCarry = 0.5f;

    [Header("当たった時")]
    public float ballPush = 4.5f;
    public float ballUp = 2.5f;
    public float playerPush = 4f;
    public float playerUp = 4.5f;
    public AudioSource hitSound;
    public AudioSource rollSound;

    private float[] _cum;
    private float _length;
    private bool _active;
    private Collider _col;
    private Vector3 _dir;
    private float _rolled;
    private Vector3 _baseScale;

    private void Start()
    {
        _col = GetComponent<Collider>();
        if (visual != null) _baseScale = visual.localScale;
        int n = path != null ? path.Length : 0;
        _cum = new float[n];
        for (int i = 1; i < n; i++) _cum[i] = _cum[i - 1] + Vector3.Distance(path[i - 1], path[i]);
        _length = n > 0 ? _cum[n - 1] : 0f;
    }

    private void Update()
    {
        if (path == null || path.Length < 2 || _length <= 0f) return;
        float travel = _length / speed;
        float t = Mathf.Repeat(Time.time + phaseOffset, period);
        // 0〜emerge：奥から出てくる / 〜travel：転がる / 〜+fall：道の端から落ちていく / 残り：いない
        float total = emergeTime + travel + fallTime;
        if (t >= total) { SetActive(false); return; }
        SetActive(true);

        float d; float scale = 1f;
        Vector3 pos;
        if (t < emergeTime) { d = 0f; scale = Mathf.SmoothStep(0f, 1f, t / emergeTime); pos = Sample(0f, out _dir); }
        else if (t < emergeTime + travel) { d = (t - emergeTime) * speed; pos = Sample(d, out _dir); }
        else
        {
            // 最後の向きのまま飛び出して、重力で落ちる
            float k = t - emergeTime - travel;
            pos = Sample(_length, out _dir);
            Vector3 flat = _dir; flat.y = 0f;
            pos += flat * speed * fallCarry * k + Vector3.down * (0.5f * 9.8f * k * k);
            d = _length + speed * fallCarry * k;
            if (k > fallTime - 0.3f) scale = Mathf.Max(0.01f, (fallTime - k) / 0.3f);
        }
        transform.position = pos;
        if (visual != null)
        {
            visual.localScale = _baseScale * Mathf.Max(0.01f, scale);
            // 転がった距離ぶん回す（進む向きに対して横軸まわり）
            Vector3 axis = Vector3.Cross(Vector3.up, _dir);
            if (axis.sqrMagnitude > 1e-4f)
            {
                float delta = d - _rolled;
                if (delta < 0f) delta = 0f;
                visual.rotation = Quaternion.AngleAxis(delta / radius * Mathf.Rad2Deg, axis.normalized) * visual.rotation;
            }
        }
        _rolled = d;
        // 沈んでいる間・出てくる途中は当たらない
        if (_col != null) _col.enabled = t >= emergeTime * 0.6f && t < emergeTime + travel + 0.15f;
    }

    private void SetActive(bool on)
    {
        if (_active == on) return;
        _active = on;
        if (visual != null) visual.gameObject.SetActive(on);
        if (_col != null) _col.enabled = on;
        if (rollSound != null) { if (on) rollSound.Play(); else rollSound.Stop(); }
        if (!on) _rolled = 0f;
    }

    private Vector3 Sample(float d, out Vector3 dir)
    {
        int n = path.Length;
        for (int i = 1; i < n; i++)
        {
            if (d <= _cum[i] || i == n - 1)
            {
                float seg = _cum[i] - _cum[i - 1];
                float k = seg > 0f ? Mathf.Clamp01((d - _cum[i - 1]) / seg) : 0f;
                dir = (path[i] - path[i - 1]).normalized;
                return Vector3.Lerp(path[i - 1], path[i], k);
            }
        }
        dir = Vector3.forward;
        return path[0];
    }

    private Vector3 Downhill()
    {
        Vector3 f = _dir; f.y = 0f;
        return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.back;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;
        Vector3 away = other.transform.position - transform.position; away.y = 0f;
        Vector3 push = Downhill() * ballPush + (away.sqrMagnitude > 1e-4f ? away.normalized * 1.0f : Vector3.zero) + Vector3.up * ballUp;
        ball.Launch(push);
        if (hitSound != null) hitSound.Play();
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        player.SetVelocity(Downhill() * playerPush + Vector3.up * playerUp);
        if (hitSound != null) hitSound.Play();
    }
}
