using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// あそびばのおもちゃ（コース2–1「みずのこみち」のクリアで出る）：泡が出る貝（2026-11-16 てじゃ「2-1 泡が出る貝 トリガーでオンオフ」）
// 貝をクリック（Interact）でオン／オフ（みんなに同期）。オンの間、コース2–1 の貝と同じように一定の間隔でパカッと開いて、大きい泡を吹く
//  （てじゃ「泡はコースのみたいなの、出してほしい」）。泡は島の真ん中の方へ、ゆらゆら上下しながらゆっくり進む
// 泡にふれた同期ふわふわ（FuwaSharedBall）は包まれていっしょに運ばれ、holdTime で割れてぽとっと落ちる。撃つと割れて出られる
// 泡の出るタイミング・位置はサーバーの時刻で決める（みんな同じに見える）。包んで運ぶのは全員の画面で同じ風（AddWind）
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaPlaygroundBubbleClam : UdonSharpBehaviour
{
    public FuwaPlaygroundSpawner spawner;
    [Tooltip("ふたの付け根（ローカル X 軸まわりに開く）")]
    public Transform lid;
    public float openAngle = 40f;
    [Tooltip("泡の出どころ。+Z が泡の進む向き")]
    public Transform mouth;
    [Tooltip("泡（球）。ふだんは隠しておく")]
    public Transform[] bubbles;
    public float bubbleSize = 0.95f;
    [Tooltip("泡を吹く間隔（秒）")]
    public float period = 2.6f;
    public float openLead = 0.55f, closeAfter = 0.45f;
    [Tooltip("泡の進む速さ(m/s)・上がる速さ(m/s)・上下のゆれ（m、片側）とその速さ（rad/s）")]
    public float bubbleSpeed = 1.1f, bubbleRise = 0.25f, bobAmp = 0.3f, bobSpeed = 2.2f;
    [Tooltip("ここまで進んだら割れる(m)")]
    public float bubbleRange = 6f;
    [Tooltip("包まれてから割れるまで（秒）")]
    public float holdTime = 3f;
    [Tooltip("包む距離（泡の中心とふわふわの中心）")]
    public float captureRadius = 0.55f;
    [Tooltip("ふつうの玉の大きさ。これより大きい玉は、包む距離・泡の大きさを同じ倍率で大きく")]
    public float refBallSize = 0.3f;
    [Tooltip("泡を置いておく、動かない親（空なら貝の親の親）")]
    public Transform bubbleRoot;
    [Tooltip("泡の位置へ寄せる強さ（1/s²）と動きをそろえる強さ（1/s）")]
    public float holdPull = 30f, holdDamp = 6f;
    [Tooltip("割れたあと、また包まれるまで（秒）")]
    public float recaptureDelay = 1.5f;
    public AudioSource toggleSound, blowSound, popSound, captureSound;
    [Tooltip("オフにした時の音（なければ toggleSound）")]
    public AudioSource offSound;
    public ParticleSystem popFx;

    [UdonSynced] private bool _on;
    [UdonSynced] private double _onAt;
    private float _ang;
    private int[] _slotIdx;        // その泡の場所に今いる泡の番号
    private bool[] _popped;
    private int[] _held;           // 包んでいるふわふわ（spawner.balls の番号、-1 なし）
    private double[] _heldAt;
    private float[] _freeUntil;    // ふわふわごと：また包まれてよい時刻（Time.time）
    private int _lastBlow = -1;

    private void Start()
    {
        if (!clickToggle) DisableInteractive = true;
        int n = bubbles != null ? bubbles.Length : 0;
        _slotIdx = new int[n]; _popped = new bool[n]; _held = new int[n]; _heldAt = new double[n]; _org = new Vector3[n]; _dir = new Vector3[n];
        for (int i = 0; i < n; i++) { _slotIdx[i] = -999; _held[i] = -1; if (bubbles[i] != null) bubbles[i].gameObject.SetActive(false); }
        // 2026-11-28 てじゃ「貝を動かすと泡がプルプル、貝の子だとダメ」：泡は動かせる貝（Body）の子なので、持って動かすと親といっしょに動いてから位置を置き直して震えていた
        //  → 動かない所（bubbleRoot、なければ Body のさらに親）へ移す。泡の位置はワールドで毎フレーム決めているので見た目は同じ
        Transform holder = bubbleRoot;
        if (holder == null && transform.parent != null) holder = transform.parent.parent;
        if (holder != null) for (int i = 0; i < n; i++) if (bubbles[i] != null) bubbles[i].SetParent(holder, true);
        int nb = spawner != null && spawner.balls != null ? spawner.balls.Length : 0;
        _freeUntil = new float[nb];
    }

    public bool IsOn() { return _on; }

    // 2026-11-16 てじゃ「おもちゃも持ち運んで移動したい」→ B「おもちゃ全体を掴める。オン/オフは持ってトリガー」：持ち手（FuwaMovableMushroom.useTarget）から Toggle を呼ぶ。クリックは切る
    [Tooltip("クリック（Interact）でもオン/オフするか（持ち運べるようにした時は切る）")]
    public bool clickToggle = true;
    public override void Interact() { Toggle(); }
    // 持ち運び（FuwaMovableMushroom）で離した時に呼ばれる。このおもちゃは何もしない（氷のおもちゃだけ当たりを合わせ直す）
    public void ReapplyAfterDrop() { }

    public void Toggle()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _on = !_on;
        _onAt = Networking.GetServerTimeInSeconds();
        RequestSerialization();
        // 2026-11-19 てじゃ「SEは扇風機のを使いまわそ」：オンは toggleSound、オフは offSound（なければ toggleSound）
        AudioSource snd = (!_on && offSound != null) ? offSound : toggleSound;
        if (snd != null) snd.Play();
    }

    // 泡 idx の、吹いてからの時間 age での位置（吹いた時の mouth 基準）
    // 2026-11-27 てじゃ「貝の泡が、貝を運ぶといっしょに動いちゃう」：毎フレームの mouth から出していたので、貝を運ぶと飛んでいる泡もついてきた
    //  → 泡ごとに吹いた時の出どころと向きを覚えて（_org/_dir）、そこから進める
    private Vector3[] _org, _dir;
    private Vector3 BubblePos(int s, int idx, double age)
    {
        float a = (float)age;
        float d = a * bubbleSpeed;
        float ph = Mathf.Repeat(idx * 1.37f, 6.28f);
        return _org[s] + _dir[s] * d + Vector3.up * (a * bubbleRise + Mathf.Sin(a * bobSpeed + ph) * bobAmp * Mathf.Clamp01(a * 1.5f));
    }

    private void Update()
    {
        if (mouth == null || bubbles == null) return;
        double t = Networking.GetServerTimeInSeconds();
        int k = (int)System.Math.Floor(t / period);
        int first = (int)System.Math.Ceiling(_onAt / period);   // オンにしてから最初の泡
        // ふた：吹く少し前に開いて、吹いたら閉じる
        float target = 0f;
        if (_on)
        {
            double toNext = (k + 1) * period - t, since = t - k * period;
            if ((toNext < openLead && k + 1 >= first) || (since < closeAfter && k >= first)) target = openAngle;
        }
        _ang = Mathf.MoveTowards(_ang, target, Time.deltaTime * openAngle / 0.25f);
        if (lid != null) lid.localRotation = Quaternion.Euler(-_ang, 0f, 0f);
        if (_on && k >= first && k != _lastBlow) { _lastBlow = k; if (blowSound != null) blowSound.Play(); }
        float life = bubbleRange / Mathf.Max(0.05f, bubbleSpeed);
        int n = bubbles.Length;
        for (int s = 0; s < n; s++)
        {
            // この場所（s）に今いるはずの泡：k 以下で s と同じ余りの一番新しい番号
            int idx = k - (((k - s) % n) + n) % n;
            Transform b = bubbles[s]; if (b == null) continue;
            if (_slotIdx[s] != idx) { if (_held[s] >= 0) Release(s); _slotIdx[s] = idx; _popped[s] = false; _org[s] = mouth.position; _dir[s] = mouth.forward; }
            double age = t - idx * period;
            bool alive = _on && idx >= first && age >= 0 && age < life && !_popped[s];
            if (alive && _held[s] >= 0 && t - _heldAt[s] > holdTime) { Pop(s); alive = false; }
            if (!alive) { if (b.gameObject.activeSelf) b.gameObject.SetActive(false); if (_held[s] >= 0) Release(s); continue; }
            if (age > life - 0.05) { Pop(s); continue; }
            if (!b.gameObject.activeSelf) b.gameObject.SetActive(true);
            b.position = BubblePos(s, idx, age);
            float grow = Mathf.Clamp01((float)age * 3f);
            // 2026-11-28 てじゃ「泡つつみもふわふわの大きさに」：包んでいる玉がふつう（refBallSize）より大きければ、泡もその倍率に
            float bk = 1f;
            if (_held[s] >= 0 && spawner != null && spawner.balls != null && _held[s] < spawner.balls.Length && spawner.balls[_held[s]] != null)
                bk = Mathf.Max(1f, spawner.balls[_held[s]].transform.lossyScale.x / Mathf.Max(0.01f, refBallSize));
            b.localScale = Vector3.one * bubbleSize * (0.4f + 0.6f * grow) * bk;
        }
    }

    private void FixedUpdate()
    {
        if (!_on || spawner == null || spawner.balls == null || bubbles == null || mouth == null) return;
        double t = Networking.GetServerTimeInSeconds();
        // 見張り：自分が持ち主の玉が「包まれている」になっているのに、どの泡も包んでいない（持ち主が入れ替わった等）なら外す
        for (int i = 0; i < spawner.balls.Length; i++)
        {
            FuwaSharedBall bb = spawner.balls[i];
            if (bb != null && bb.BubbledSynced() && Networking.IsOwner(bb.gameObject) && !IsHeldElsewhere(i)) bb.SetBubbled(false);
        }
        for (int s = 0; s < bubbles.Length; s++)
        {
            Transform b = bubbles[s];
            if (b == null || !b.gameObject.activeSelf || _popped[s]) continue;
            Vector3 bp = b.position;
            // 泡の動き（つぎの位置との差）
            double age = t - _slotIdx[s] * period;
            Vector3 bv = (BubblePos(s, _slotIdx[s], age + 0.05) - BubblePos(s, _slotIdx[s], age)) / 0.05f;
            if (_held[s] < 0)
            {
                for (int i = 0; i < spawner.balls.Length; i++)
                {
                    FuwaSharedBall ball = spawner.balls[i];
                    if (ball == null || !ball.IsFlying() || IsHeldElsewhere(i)) continue;
                    bool mine = Networking.IsOwner(ball.gameObject);
                    // 包むかは玉の持ち主の画面だけで決める。ほかの画面は「持ち主の画面で包まれている」と届いた玉を、いちばん近い泡で運ぶ
                    if (mine ? Time.time < _freeUntil[i] : !ball.BubbledSynced()) continue;
                    float cr = captureRadius * Mathf.Max(1f, ball.transform.lossyScale.x / Mathf.Max(0.01f, refBallSize));
                    if (!mine) cr *= 3f;   // 位置が少しずれていても拾う
                    if ((ball.transform.position - bp).sqrMagnitude < cr * cr) { if (mine) ball.SetBubbled(true); _held[s] = i; _heldAt[s] = t; if (captureSound != null) { captureSound.transform.position = bp; captureSound.Play(); } break; }
                }
            }
            int hi = _held[s]; if (hi < 0) continue;
            FuwaSharedBall hb = spawner.balls[hi];
            if (hb == null || !hb.IsFlying()) { _held[s] = -1; continue; }
            bool hmine = Networking.IsOwner(hb.gameObject);
            if (!hmine && !hb.BubbledSynced()) { Pop(s); continue; }   // 持ち主の画面で泡から出た
            // 撃たれたら割れて出られる（持ち主の画面で決める）
            if (hmine && hb.LastShotAge() < 0.15f && t - _heldAt[s] > 0.2) { Pop(s); continue; }
            Rigidbody rb = hb.GetComponent<Rigidbody>(); if (rb == null) continue;
            Vector3 acc = (bp - rb.position) * holdPull + (bv - rb.velocity) * holdDamp + Vector3.up * hb.fallAcceleration;
            hb.AddWind(acc);
        }
    }

    private bool IsHeldElsewhere(int ballIdx)
    {
        for (int s = 0; s < _held.Length; s++) if (_held[s] == ballIdx) return true;
        return false;
    }

    private void Release(int s)
    {
        int hi = _held[s];
        if (hi >= 0 && spawner != null && spawner.balls != null && hi < spawner.balls.Length && spawner.balls[hi] != null) spawner.balls[hi].SetBubbled(false);
        if (hi >= 0 && _freeUntil != null && hi < _freeUntil.Length) _freeUntil[hi] = Time.time + recaptureDelay;
        _held[s] = -1;
    }

    private void Pop(int s)
    {
        if (_popped[s]) return;
        _popped[s] = true;
        Transform b = bubbles[s];
        if (b != null)
        {
            if (popFx != null) { popFx.transform.position = b.position; popFx.Emit(14); }
            if (popSound != null) { popSound.transform.position = b.position; popSound.Play(); }
            b.gameObject.SetActive(false);
        }
        Release(s);
    }
}
