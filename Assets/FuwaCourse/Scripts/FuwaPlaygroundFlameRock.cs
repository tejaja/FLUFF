using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// あそびばのおもちゃ（コース2–2「ほのおのやま」のクリアで出る）：火炎放射の岩（2026-11-16 てじゃ「2-2 火炎放射出す岩 トリガーでオンオフ」）
// 岩をクリック（Interact）でオン／オフ（みんなに同期）。オンの間、穴から炎（コース2–2 の火炎放射と同じ FlameMesh）が吹き出し続ける。
// 炎に入った同期ふわふわは吹っ飛ぶ・人は押される（FuwaPlaygroundFlameZone）。止めると炎は根元から燃えつきて消える
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaPlaygroundFlameRock : UdonSharpBehaviour
{
    [Tooltip("炎の本体（+Y が炎の向き。長さは localScale.y で伸びる）")]
    public Transform flameBody;
    public Vector3 bodyScale = new Vector3(0.22f, 0.3f, 0.22f);
    public float bodySpeed = 5f, bodyAfter = 0.6f;
    public AudioSource roar, toggleSound;

    [UdonSynced] private bool _on;
    private float _body, _burn = 1f;
    private Material _mat;

    public bool IsOn() { return _on && _body > 0.5f; }

    public override void Interact()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _on = !_on;
        RequestSerialization();
        if (toggleSound != null) toggleSound.Play();
    }

    private void Update()
    {
        if (flameBody == null) return;
        if (_on) { _body = Mathf.MoveTowards(_body, 1f, bodySpeed * Time.deltaTime); _burn = 0f; }
        else if (_body > 0f && _burn < 1f) { _burn = Mathf.Min(1f, _burn + Time.deltaTime / Mathf.Max(0.05f, bodyAfter)); if (_burn >= 1f) _body = 0f; }
        else { _body = 0f; _burn = 1f; }
        bool show = _body > 0.001f;
        if (flameBody.gameObject.activeSelf != show) flameBody.gameObject.SetActive(show);
        if (show)
        {
            flameBody.localScale = new Vector3(bodyScale.x * Mathf.Lerp(0.6f, 1f, _body), bodyScale.y * _body, bodyScale.z * Mathf.Lerp(0.6f, 1f, _body));
            if (_mat == null) _mat = flameBody.GetComponent<Renderer>().material;
            _mat.SetFloat("_Burn", _burn);
        }
        if (roar != null)
        {
            if (_on && !roar.isPlaying) roar.Play();
            else if (!_on && roar.isPlaying) roar.Stop();
        }
    }
}
