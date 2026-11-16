using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// あそびばのおもちゃ（コース2–1「みずのこみち」のクリアで出る）：泡が出る貝（2026-11-16 てじゃ「2-1 泡が出る貝 トリガーでオンオフ」）
// 貝をクリック（Interact）でオン／オフ（みんなに同期）。オンの間、ふたが開いて口から泡が立ちのぼる。
// 泡の柱（FuwaPlaygroundBubbleZone、子のトリガー）に入った同期ふわふわは、ゆっくり上へ運ばれて、てっぺんで島の真ん中の方へ送り出される
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaPlaygroundBubbleClam : UdonSharpBehaviour
{
    [Tooltip("ふたの付け根（ローカル X 軸まわりに開く）")]
    public Transform lid;
    public float openAngle = 42f;
    [Tooltip("開く・閉じる速さ（度/秒）")]
    public float openSpeed = 110f;
    [Tooltip("泡の出どころ（貝の口）")]
    public Transform mouth;
    [Tooltip("見た目の泡（球）")]
    public Transform[] bubbles;
    public float columnHeight = 4.2f;
    public float bubbleRise = 0.9f;
    public float bubbleSizeMin = 0.12f, bubbleSizeMax = 0.42f;
    public float wobble = 0.25f;
    public AudioSource toggleSound, loopSound;

    [UdonSynced] private bool _on;
    private float _ang, _show;
    private float[] _seed;

    private void Start()
    {
        int n = bubbles != null ? bubbles.Length : 0;
        _seed = new float[n];
        for (int i = 0; i < n; i++) { _seed[i] = (i + 0.37f * Random.value) / Mathf.Max(1, n); if (bubbles[i] != null) bubbles[i].gameObject.SetActive(false); }
        Apply();
    }

    public bool IsOn() { return _on; }

    public override void Interact()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _on = !_on;
        RequestSerialization();
        Apply();
        if (toggleSound != null) toggleSound.Play();
    }

    public override void OnDeserialization() { Apply(); }

    private void Apply()
    {
        if (loopSound == null) return;
        if (_on && !loopSound.isPlaying) loopSound.Play();
        else if (!_on && loopSound.isPlaying) loopSound.Stop();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _ang = Mathf.MoveTowards(_ang, _on ? openAngle : 0f, openSpeed * dt);
        if (lid != null) lid.localRotation = Quaternion.Euler(-_ang, 0f, 0f);
        // 泡：開いている分だけ出る（閉じる時はふわっと消える）
        _show = Mathf.MoveTowards(_show, _on && _ang > openAngle * 0.6f ? 1f : 0f, dt * 1.5f);
        if (bubbles == null || mouth == null) return;
        float t = Time.time;
        for (int i = 0; i < bubbles.Length; i++)
        {
            Transform b = bubbles[i]; if (b == null) continue;
            bool vis = _show > 0.01f;
            if (b.gameObject.activeSelf != vis) b.gameObject.SetActive(vis);
            if (!vis) continue;
            float p = Mathf.Repeat(t * bubbleRise / columnHeight + _seed[i], 1f);
            float ph = _seed[i] * 37f;
            Vector3 off = new Vector3(Mathf.Sin(t * 1.7f + ph), 0f, Mathf.Cos(t * 1.3f + ph * 0.7f)) * wobble * (0.3f + p);
            b.position = mouth.position + Vector3.up * (p * columnHeight) + off;
            float s = Mathf.Lerp(bubbleSizeMin, bubbleSizeMax, Mathf.Repeat(_seed[i] * 7.3f, 1f)) * Mathf.Clamp01(p * 6f) * Mathf.Clamp01((1f - p) * 8f) * _show;
            b.localScale = Vector3.one * s;
        }
    }
}
