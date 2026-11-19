using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// あそびばのおもちゃ（コース2–2「ほのおのやま」のクリアで出る）：溶岩溜まり（2026-11-16 てじゃ「2-2はマグマ弾が真上に打ち上がる溶岩溜まりとか」「炎上ってできる？」）
// オンの間、interval 秒ごとにマグマ弾が池から真上へ height m 打ち上がって、池に落ちて戻る。いつ上がるかはサーバー時刻で全員同じ
// 弾に当たった同期ふわふわ：上へ吹っ飛んで燃えだす（FuwaSharedBall.Ignite、撃てば消える）。人：ちょっと跳ね飛ばされる（自分の画面）
// 持ち運び（FuwaMovableMushroom）から、持ってトリガーで Toggle
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaPlaygroundLavaPool : UdonSharpBehaviour
{
    public FuwaPlaygroundSpawner spawner;
    [Tooltip("溶岩の面の中心（弾はここから上がってここへ落ちる）")]
    public Transform surface;
    [Tooltip("マグマ弾の見た目（飛んでいない時は隠す）")]
    public GameObject bomb;
    public ParticleSystem trail;
    public float trailRate = 40f;
    [Tooltip("池のぶくぶく・しぶき（なくてもOK）")]
    public ParticleSystem splash;
    public AudioSource whoosh, toggleSound;
    public float interval = 2.5f, flightTime = 1.4f, height = 4f;
    public float hitRadius = 0.75f;
    [Tooltip("ふわふわ：上へ knockUp、弾から外へ knockSide")]
    public float knockUp = 7f, knockSide = 2f, ballCooldown = 0.8f;
    public float playerUp = 6f, playerSide = 3f, playerCooldown = 0.8f;
    [Tooltip("オンの間、池の上（半径 poolRadius・溶岩の面から poolDepth m 以内）に入ったふわふわも燃えだす")]
    public float poolRadius = 0.85f, poolDepth = 0.7f;

    [UdonSynced] private bool _on;
    [UdonSynced] private double _onAt;
    private int _lastCycle = -1;
    private bool _shown;
    private float[] _ballAt;
    private float _playerAt = -10f;

    private void Start()
    {
        int n = spawner != null && spawner.balls != null ? spawner.balls.Length : 0;
        _ballAt = new float[n];
        for (int i = 0; i < n; i++) _ballAt[i] = -10f;
        Show(false);
    }

    // 持ち運び（FuwaMovableMushroom）で離した時に呼ばれる。このおもちゃは何もしない（氷のおもちゃだけ当たりを合わせ直す）
    public void ReapplyAfterDrop() { }

    public void Toggle()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _on = !_on;
        _onAt = Networking.GetServerTimeInSeconds();
        RequestSerialization();
        if (toggleSound != null) toggleSound.Play();
    }
    public override void Interact() { Toggle(); }

    private void Show(bool on)
    {
        if (_shown == on) return;
        _shown = on;
        if (bomb != null) bomb.SetActive(on);
        if (trail != null) { var e = trail.emission; e.rateOverTime = on ? trailRate : 0f; }
    }

    private void Update()
    {
        if (!_on || surface == null) { Show(false); return; }
        BurnInPool();
        double t = Networking.GetServerTimeInSeconds() - _onAt - 0.6;   // オンにしてから少しおいて最初の1発
        if (t < 0.0) { Show(false); return; }
        int cycle = (int)(t / interval);
        float c = (float)(t - cycle * (double)interval);
        if (c >= flightTime) { Show(false); return; }
        if (cycle != _lastCycle)
        {
            _lastCycle = cycle;
            if (splash != null) splash.Emit(18);
            if (whoosh != null) whoosh.Play();
        }
        float u = c / flightTime;
        Vector3 p = surface.position + Vector3.up * (4f * height * u * (1f - u));
        Show(true);
        if (bomb != null) bomb.transform.position = p;
        HitBalls(p);
        HitPlayer(p);
    }

    private void BurnInPool()
    {
        if (spawner == null || spawner.balls == null) return;
        Vector3 c = surface.position;
        float r2 = poolRadius * poolRadius;
        for (int i = 0; i < spawner.balls.Length; i++)
        {
            FuwaSharedBall b = spawner.balls[i];
            if (b == null || !b.IsFlying() || b.IsBurning()) continue;
            Vector3 d = b.transform.position - c;
            if (d.y < -0.3f || d.y > poolDepth) continue;
            d.y = 0f;
            if (d.sqrMagnitude <= r2) b.Ignite();
        }
    }

    private void HitBalls(Vector3 p)
    {
        if (spawner == null || spawner.balls == null || _ballAt == null) return;
        int n = Mathf.Min(spawner.balls.Length, _ballAt.Length);
        float r2 = hitRadius * hitRadius;
        for (int i = 0; i < n; i++)
        {
            FuwaSharedBall b = spawner.balls[i];
            if (b == null || !b.IsFlying()) continue;
            if (Time.time - _ballAt[i] < ballCooldown) continue;
            Vector3 d = b.transform.position - p;
            if (d.sqrMagnitude > r2) continue;
            _ballAt[i] = Time.time;
            Vector3 side = d; side.y = 0f;
            side = side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.zero;
            b.Nudge(Vector3.up * knockUp + side * knockSide);
            b.Ignite();
        }
    }

    private void HitPlayer(Vector3 p)
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null || Time.time - _playerAt < playerCooldown) return;
        Vector3 feet = lp.GetPosition();
        Vector3 chest = feet + Vector3.up * 0.9f;
        Vector3 d = chest - p;
        // 体（足〜頭）を縦の線と見て、弾との近さ
        float dy = Mathf.Clamp(p.y, feet.y, feet.y + 1.7f);
        Vector3 q = new Vector3(feet.x, dy, feet.z);
        if ((q - p).sqrMagnitude > (hitRadius + 0.3f) * (hitRadius + 0.3f)) return;
        _playerAt = Time.time;
        Vector3 side = d; side.y = 0f;
        side = side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.forward;
        lp.SetVelocity(lp.GetVelocity() + side * playerSide + Vector3.up * playerUp);
    }
}
