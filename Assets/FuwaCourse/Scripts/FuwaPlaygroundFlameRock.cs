using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// あそびばのおもちゃ（コース2–2「ほのおのやま」のクリアで出る）：火炎放射の岩（2026-11-16 てじゃ「2-2 火炎放射出す岩 トリガーでオンオフ」）
// 岩をクリック（Interact）でオン／オフ（みんなに同期）。オンの間、穴から炎（コース2–2 の火炎放射と同じ FlameMesh）が吹き出し続ける。
// 炎に入った同期ふわふわは吹っ飛ぶ・人は押される（FuwaPlaygroundFlameZone）。止めると炎は根元から燃えつきて消える
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaPlaygroundFlameRock : UdonSharpBehaviour
{
    [Tooltip("炎の本体（+Y が炎の向き。長さは localScale.y で伸びる）。2026-11-16 てじゃ「3列にしよ」→ 上中下の3本（rowDelay 秒ずつずれて出る）")]
    public Transform[] flameBodies;
    public float rowDelay = 0.08f;
    public Vector3 bodyScale = new Vector3(0.22f, 0.3f, 0.22f);
    public float bodySpeed = 5f, bodyAfter = 0.6f;
    public AudioSource roar, toggleSound;

    [UdonSynced] private bool _on;
    private float[] _bodies, _burns;
    private Material[] _mats;
    private float _onAt = -10f, _offAt = -10f;
    private bool _lastOn;

    public bool IsOn() { return _on && _bodies != null && _bodies.Length > 0 && _bodies[0] > 0.5f; }

    public override void Interact()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _on = !_on;
        RequestSerialization();
        if (toggleSound != null) toggleSound.Play();
    }

    private void Update()
    {
        if (flameBodies == null) return;
        int n = flameBodies.Length;
        if (_bodies == null || _bodies.Length != n) { _bodies = new float[n]; _burns = new float[n]; _mats = new Material[n]; for (int i = 0; i < n; i++) _burns[i] = 1f; }
        if (_on != _lastOn) { _lastOn = _on; if (_on) _onAt = Time.time; else _offAt = Time.time; }
        for (int i = 0; i < n; i++)
        {
            Transform b = flameBodies[i]; if (b == null) continue;
            bool on = _on && Time.time - _onAt >= rowDelay * i;
            if (on) { _bodies[i] = Mathf.MoveTowards(_bodies[i], 1f, bodySpeed * Time.deltaTime); _burns[i] = 0f; }
            else if (_bodies[i] > 0f && _burns[i] < 1f) { _burns[i] = Mathf.Min(1f, _burns[i] + Time.deltaTime / Mathf.Max(0.05f, bodyAfter)); if (_burns[i] >= 1f) _bodies[i] = 0f; }
            else { _bodies[i] = 0f; _burns[i] = 1f; }
            float k = _bodies[i];
            bool show = k > 0.001f;
            if (b.gameObject.activeSelf != show) b.gameObject.SetActive(show);
            if (!show) continue;
            b.localScale = new Vector3(bodyScale.x * Mathf.Lerp(0.6f, 1f, k), bodyScale.y * k, bodyScale.z * Mathf.Lerp(0.6f, 1f, k));
            if (_mats[i] == null) _mats[i] = b.GetComponent<Renderer>().material;
            _mats[i].SetFloat("_Burn", _burns[i]);
        }
        if (roar != null)
        {
            if (_on && !roar.isPlaying) roar.Play();
            else if (!_on && roar.isPlaying) roar.Stop();
        }
    }
}
