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
    [Tooltip("オフにした時の音（なければ toggleSound）")]
    public AudioSource offSound;

    // 2026-11-27 てじゃ「展開後は小さくなって、かつぶつからないように」：氷が出ている間は結晶を小さく（足もとが原点なので床にのったまま縮む）、結晶の当たりも切る（滑っていてぶつからない）
    [Tooltip("氷が出ている間に縮める結晶（null で縮めない）")]
    public Transform crystal;
    public Collider crystalCollider;
    [Tooltip("氷が出きった時の結晶の大きさ（倍）")]
    public float onScale = 0.45f;
    [Tooltip("持つ判定（Grab）の大きい当たり・小さい当たり（onScale 倍）。縮んでいる間は小さい方に切りかえる（ハイライトも小さく）")]
    public Collider grabFull, grabSmall;

    [UdonSynced] private bool _on;
    private float _k;

    // 2026-11-16 持ち運べるように：持ってトリガーで Toggle（FuwaMovableMushroom.useTarget）。クリックは切る
    [Tooltip("クリック（Interact）でもオン/オフするか（持ち運べるようにした時は切る）")]
    public bool clickToggle = true;
    private void Start() { if (!clickToggle) DisableInteractive = true; FitHeight(); Apply(); }

    // 2026-11-23 てじゃ「氷を芝に展開すると木の板の下にいっちゃうのがやだ」：芝は木の床（デッキ）より少し低いので、芝に置くと氷の円がデッキの下にもぐっていた
    //  → 置いた所のまわり（氷が広がる円の中）の床の高さを調べて、いちばん高い床の上に氷をのせる（ふちは厚みがあるので、低い所でも浮いて見えない）
    [Tooltip("床の高さを調べるレイヤー（道・Default）")]
    public LayerMask floorMask = 1 | (1 << 11);
    [Tooltip("氷の円の半径（m、調べる範囲）・持ち上げる上限（m）")]
    public float discRadius = 3f, maxLift = 0.12f;   // 柵・ふちの岩（0.2m〜）は床じゃないので数えない。デッキは芝より 6cm 高い
    public void FitHeight()
    {
        if (disc == null) return;
        Transform body = disc.parent;
        if (body == null) return;
        bool ce = discCollider != null && discCollider.enabled;
        if (discCollider != null) discCollider.enabled = false;   // 自分の氷には当てない
        Vector3 c = body.position; float best = 0f;
        for (int ring = 0; ring < 3; ring++)
        {
            float r = discRadius * (ring == 0 ? 0f : (ring == 1 ? 0.55f : 0.95f));
            int n = ring == 0 ? 1 : 16;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 2f * i / n;
                Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 1.5f, Mathf.Sin(a) * r);
                RaycastHit h;
                if (Physics.Raycast(p, Vector3.down, out h, 3f, floorMask, QueryTriggerInteraction.Ignore))
                {
                    if (h.collider != null && h.collider.transform.IsChildOf(body)) continue;   // おもちゃ自身（結晶）は無視
                    float dy = h.point.y - c.y; if (dy > best && dy < maxLift) best = dy;
                }
            }
        }
        if (discCollider != null) discCollider.enabled = ce;
        Vector3 lp = disc.localPosition; lp.y = best; disc.localPosition = lp;
    }

    public override void Interact() { Toggle(); }
    public void Toggle()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _on = !_on;
        RequestSerialization();
        // 2026-11-19 てじゃ「SEは扇風機のを使いまわそ」：オンは toggleSound、オフは offSound（なければ toggleSound）
        AudioSource snd = (!_on && offSound != null) ? offSound : toggleSound;
        if (snd != null) snd.Play();
    }

    private void Update()
    {
        float target = _on ? 1f : 0f;
        if (Mathf.Approximately(_k, target)) return;
        _k = Mathf.MoveTowards(_k, target, spreadSpeed * Time.deltaTime);
        Apply();
    }

    // 持ち運び（FuwaMovableMushroom）で離した時：当たりを今の広がりに合わせ直す（持っている間は当たりを全部切っている）
    public void ReapplyAfterDrop() { FitHeight(); Apply(); }

    private void Apply()
    {
        if (disc == null) return;
        float e = _k * _k * (3f - 2f * _k);   // ゆっくり出てゆっくり止まる
        bool vis = e > 0.001f;
        if (disc.gameObject.activeSelf != vis) disc.gameObject.SetActive(vis);
        disc.localScale = new Vector3(e, 1f, e);
        if (discCollider != null) discCollider.enabled = e > 0.2f;
        if (crystal != null) crystal.localScale = Vector3.one * Mathf.Lerp(1f, onScale, e);
        if (crystalCollider != null) crystalCollider.enabled = e < 0.05f;
        // 2026-11-27 てじゃ「縮んだあとにインタラクト時のハイライトも縮んで欲しい」
        if (grabFull != null && grabSmall != null) { bool small = e >= 0.5f; if (grabSmall.enabled != small) grabSmall.enabled = small; if (grabFull.enabled == small) grabFull.enabled = !small; }
    }
}
