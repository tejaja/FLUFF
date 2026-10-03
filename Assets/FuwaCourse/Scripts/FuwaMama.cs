using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// まもののすの奥の大きい巣で寝ている「ママけだま」（自分の画面だけ、同期なし）。
// 近くに来ると、ときどき「は…は…」とためてから「はっくしょん！」。前（このオブジェクトの forward）の円すいの中にいる
// ふわふわと自分を、くしゃみの風で押し戻す。くしゃみの合間に通り抜ける。倒せない・突進しない。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaMama : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("見た目（呼吸・ためでふくらむ）")]
    public Transform visual;
    [Tooltip("目（寝てる時・ためてる時は細く）")]
    public Transform eyes;
    [Tooltip("くしゃみの風が出る所（口）。なければこのオブジェクト")]
    public Transform mouth;

    [Header("黒目（動く）")]
    [Tooltip("左右の黒目（白目の前の面に沿って動く）")]
    public Transform pupilL;
    public Transform pupilR;
    [Tooltip("黒目が中心から動ける距離（目のローカル）")]
    public float pupilReach = 0.036f;
    [Tooltip("白目の前の面のふくらみ（黒目を白目の面に沿わせる）")]
    public float pupilDepth = 0.04f;

    [Header("くしゃみのリズム")]
    [Tooltip("くしゃみの間の秒数（ため時間は含まない）")]
    public float interval = 3.2f;
    public float intervalJitter = 0.8f;
    [Tooltip("「は…は…」とためる秒数（予告）")]
    public float telegraph = 1.4f;
    [Tooltip("ふわふわか自分がこの距離にいる時だけくしゃみする(m)")]
    public float activeRange = 13f;

    [Header("くしゃみの風")]
    public float range = 7.5f;
    [Tooltip("風の円すいの半分の角度(度)")]
    public float coneAngle = 38f;
    public float ballPush = 4.2f;
    public float ballUp = 1.6f;
    public float playerPush = 4.5f;
    public float playerUp = 2.2f;

    [Header("見た目・音")]
    public Transform zzz;
    public Transform warnText;
    public Transform sneezeText;
    public ParticleSystem sneezeFx;
    public AudioSource inhaleSound;
    public AudioSource sneezeSound;

    // 0=寝てる 1=待ち 2=ため 3=くしゃみ直後
    private int _state;
    private float _t;
    private float _wait;
    private Vector3 _visualScale = Vector3.one;
    private Vector3 _visualBase;
    private Vector3 _eyesScale = Vector3.one;
    private Vector3 _zzzBase;

    private void Start()
    {
        if (visual != null) { _visualScale = visual.localScale; _visualBase = visual.localPosition; }
        if (eyes != null) _eyesScale = eyes.localScale;
        InitPupils();
        if (zzz != null) _zzzBase = zzz.localPosition;
        if (warnText != null) warnText.gameObject.SetActive(false);
        if (sneezeText != null) sneezeText.gameObject.SetActive(false);
        _wait = interval;
    }

    private void SetState(int s) { _state = s; _t = 0f; }

    private void Update()
    {
        float dt = Time.deltaTime;
        _t += dt;
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local == null || !local.IsValid()) return;
        bool near = (local.GetPosition() - transform.position).magnitude < activeRange
                 || (ball != null && ball.gameObject.activeInHierarchy && (ball.transform.position - transform.position).magnitude < activeRange);

        UpdatePupils(_state != 0, local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position, dt);
        float swell = 0f, squint = 1f, shake = 0f;
        if (_state == 0)
        {
            squint = 0.12f;
            if (near) { _wait = interval * 0.6f; SetState(1); }
        }
        else if (_state == 1)
        {
            squint = 0.55f;
            if (!near && _t > 2f) SetState(0);
            else if (_t > _wait)
            {
                SetState(2);
                if (warnText != null) warnText.gameObject.SetActive(true);
                if (inhaleSound != null) inhaleSound.Play();
            }
        }
        else if (_state == 2)
        {
            float k = Mathf.Clamp01(_t / telegraph);
            swell = 0.16f * k;                 // 息を吸ってふくらむ
            shake = 0.02f * k;
            squint = Mathf.Lerp(0.55f, 0.08f, k);
            if (warnText != null) warnText.localScale = Vector3.one * (0.85f + 0.3f * k);
            if (_t > telegraph) Sneeze();
        }
        else if (_state == 3)
        {
            float k = Mathf.Clamp01(_t / 0.5f);
            swell = -0.12f * Mathf.Sin(k * Mathf.PI);   // くしゃみでぺこっ
            squint = 0.08f;
            if (_t > 0.9f)
            {
                if (sneezeText != null) sneezeText.gameObject.SetActive(false);
                _wait = interval + Random.Range(-intervalJitter, intervalJitter);
                SetState(1);
            }
        }

        // 見た目：呼吸＋ため
        float br = Mathf.Sin(Time.time * (_state == 0 ? 1.2f : 2f)) * 0.03f;
        if (visual != null)
        {
            float s = 1f + swell + br;
            visual.localScale = new Vector3(_visualScale.x * s, _visualScale.y * (1f + swell * 0.7f - br), _visualScale.z * s);
            visual.localPosition = _visualBase + (shake > 0f ? Random.insideUnitSphere * shake : Vector3.zero);
        }
        if (eyes != null) eyes.localScale = new Vector3(_eyesScale.x, _eyesScale.y * squint, _eyesScale.z);
        if (zzz != null)
        {
            zzz.gameObject.SetActive(_state == 0);
            float u = (Time.time * 0.4f) % 1f;
            zzz.localPosition = _zzzBase + new Vector3(0.15f * Mathf.Sin(u * 6.28f), 0.5f * u, 0f);
            zzz.localScale = Vector3.one * (0.7f + 0.6f * u) * (u < 0.85f ? 1f : (1f - u) / 0.15f);
        }
    }

    private void Sneeze()
    {
        SetState(3);
        if (warnText != null) warnText.gameObject.SetActive(false);
        if (sneezeText != null) sneezeText.gameObject.SetActive(true);
        if (sneezeFx != null) sneezeFx.Play();
        if (sneezeSound != null) sneezeSound.Play();
        Transform m = mouth != null ? mouth : transform;
        Vector3 fwd = m.forward; fwd.y = 0f; fwd.Normalize();

        if (ball != null && ball.gameObject.activeInHierarchy)
        {
            float f = Falloff(m.position, fwd, ball.transform.position);
            if (f > 0f) ball.Blow(fwd * ballPush * f + Vector3.up * ballUp * f);
        }
        VRCPlayerApi local = Networking.LocalPlayer;
        if (local != null && local.IsValid())
        {
            float f = Falloff(m.position, fwd, local.GetPosition() + Vector3.up * 0.8f);
            if (f > 0f) local.SetVelocity(local.GetVelocity() * 0.3f + fwd * playerPush * f + Vector3.up * playerUp * f);
        }
    }

    // 円すいの中なら、近いほど強い（1〜0.45）。外なら0
    private float Falloff(Vector3 origin, Vector3 fwd, Vector3 p)
    {
        Vector3 to = p - origin;
        Vector3 flat = to; flat.y = 0f;
        float d = flat.magnitude;
        if (d > range || d < 0.01f) return d < 0.01f ? 1f : 0f;
        if (Mathf.Abs(to.y) > 2.5f) return 0f;
        if (Vector3.Angle(fwd, flat) > coneAngle) return 0f;
        return Mathf.Lerp(1f, 0.45f, d / range);
    }

    private Vector3 _pupilBaseL, _pupilBaseR;
    private Vector2 _look, _lookGoal;
    private float _nextGlance;

    private void InitPupils()
    {
        if (pupilL != null) _pupilBaseL = pupilL.localPosition;
        if (pupilR != null) _pupilBaseR = pupilR.localPosition;
    }

    // 黒目：見る相手がいればそっち、いなければときどき、きょろきょろ
    private void UpdatePupils(bool watch, Vector3 target, float dt)
    {
        if (pupilL == null || pupilR == null) return;
        Transform basis = pupilL.parent;
        if (watch && basis != null)
        {
            Vector3 d = basis.InverseTransformDirection(target - pupilL.position);
            Vector2 g = new Vector2(d.x, d.y) / Mathf.Max(0.05f, Mathf.Abs(d.z) + 0.3f);
            _lookGoal = Vector2.ClampMagnitude(g * 1.6f, 1f);
        }
        else if (Time.time > _nextGlance)
        {
            _lookGoal = Random.insideUnitCircle * 0.9f;
            _nextGlance = Time.time + Random.Range(0.7f, 2.0f);
        }
        _look = Vector2.Lerp(_look, _lookGoal, 1f - Mathf.Exp(-12f * dt));
        SetPupil(pupilL, _pupilBaseL);
        SetPupil(pupilR, _pupilBaseR);
    }

    private void SetPupil(Transform p, Vector3 b)
    {
        float r = Mathf.Clamp01(_look.magnitude);
        p.localPosition = b + new Vector3(_look.x * pupilReach, _look.y * pupilReach, -pupilDepth * (1f - Mathf.Sqrt(Mathf.Max(0f, 1f - r * r * 0.8f))));
    }
}
