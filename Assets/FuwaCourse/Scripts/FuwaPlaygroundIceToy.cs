using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// あそびばのおもちゃ（コース2–3「こおりのみち」のクリアで出る）：氷のたね（2026-11-16 てじゃ「2-3 滑る氷の床エリア トリガーでオンすると周囲の地面が氷に」）
// 氷の柱をクリック（Interact）でオン／オフ（みんなに同期）。オンでまわりの床に氷がひろがり（円がのびる）、オフで縮んで消える。
// 氷の上の人はコース2–3 と同じく滑る（FuwaIceSkate、ice＝この氷の当たり）。氷の当たりはつるつるの物理材質（ふわふわもすべる）
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaPlaygroundIceToy : UdonSharpBehaviour
{
    [Tooltip("氷の円（見た目と当たり。localScale の x・z で広がる）")]
    public Transform disc;
    public Collider discCollider;
    [Tooltip("広がる・縮む速さ（1/秒）")]
    public float spreadSpeed = 0.9f;
    public AudioSource toggleSound;

    [UdonSynced] private bool _on;
    private float _k;

    // 2026-11-16 持ち運べるように：持ってトリガーで Toggle（FuwaMovableMushroom.useTarget）。クリックは切る
    [Tooltip("クリック（Interact）でもオン/オフするか（持ち運べるようにした時は切る）")]
    public bool clickToggle = true;
    private void Start() { if (!clickToggle) DisableInteractive = true; Apply(); }

    public override void Interact() { Toggle(); }
    public void Toggle()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _on = !_on;
        RequestSerialization();
        if (toggleSound != null) toggleSound.Play();
    }

    private void Update()
    {
        float target = _on ? 1f : 0f;
        if (Mathf.Approximately(_k, target)) return;
        _k = Mathf.MoveTowards(_k, target, spreadSpeed * Time.deltaTime);
        Apply();
    }

    // 持ち運び（FuwaMovableMushroom）で離した時：当たりを今の広がりに合わせ直す（持っている間は当たりを全部切っている）
    public void ReapplyAfterDrop() { Apply(); }

    private void Apply()
    {
        if (disc == null) return;
        float e = _k * _k * (3f - 2f * _k);   // ゆっくり出てゆっくり止まる
        bool vis = e > 0.001f;
        if (disc.gameObject.activeSelf != vis) disc.gameObject.SetActive(vis);
        disc.localScale = new Vector3(e, 1f, e);
        if (discCollider != null) discCollider.enabled = e > 0.2f;
    }
}
