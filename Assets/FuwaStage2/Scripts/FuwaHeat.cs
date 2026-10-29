using UdonSharp;
using UnityEngine;

// ステージ2「ほのおのやま」の熱（2026-10-28 試作）：かがり火（FuwaHeatSource）・火柱（FuwaFireJet）を通ったふわふわが温まって、
// 少し上がった所でふわっと止まって浮いたまま待つ（熱気球みたいに）。次に撃たれるまで浮いたまま（冷めない）。
// 2026-10-28 てじゃ「推進力がないから活用がむずかしい」「とどまるのを利用する手もある、タイミング図るのムズイポイント」「冷めるのはいらんかも」
// → 浮いて止まっている間に、向きを変えたり（曲がり角）タイミングを見て撃つ遊び。温まっている間はオレンジに光る。
// てじゃ「あまりに長時間浮かせてると燃えるとかもおもろい、厳しすぎずに」「燃えてもすぐショットすれば消える」
// → burnAfter 秒浮いていると燃えだす（赤く点滅＋炎）。燃えたまま飛んでいく。burnGrace 秒以内に撃って消す（ためなし3回／半分ため2回／フルため1回）、消せないと燃え尽きてアウト
// 2026-10-29：燃え尽きたら黒焦げ→一瞬止まる→アウト（FuwaBall.FailByBurn）。火の玉・ほのおのまもの（Ignite）、水（Douse）、火炎放射（BurnOut）
// 1つだけ置いて、かがり火から Heat() を呼ぶ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaHeat : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("温まった所から、どれだけ上で止まるか(m)")]
    public float hoverRise = 0.8f;
    [Tooltip("止まる所へ引っぱるばねの強さと、ゆれをおさえる強さ")]
    public float spring = 9f, damping = 4.5f;
    [Tooltip("横の動きをおさえる強さ（1/s）")]
    public float sideDamping = 3f;
    [Tooltip("横に止まる所へ引っぱるばねの強さ")]
    public float sideSpring = 4f;
    [Tooltip("ゆらゆら：上下の幅(m)と周期(秒)")]
    public float bobAmp = 0.08f, bobPeriod = 2.2f;
    [Tooltip("撃ってからこの秒数は温まり直さない（火の中から撃ってもすぐ止まらないように）")]
    public float reheatDelay = 0.6f;
    [Tooltip("止まる所からこれ以上はなれたら（スタートに戻ったなど）浮くのをやめる(m)")]
    public float maxLeash = 6f;
    [Tooltip("この秒数浮いていると燃えだす")]
    public float burnAfter = 6f;
    [Tooltip("燃えだしてから燃え尽きる（アウト）までの秒数。この間に撃てば消える")]
    public float burnGrace = 3.5f;
    [Tooltip("消すのに要る撃ちの量：ためなし1回の量（ためるほど 1 に近づく）。ためなし3回・半分ため2回・フルため1回で消える（てじゃ 2026-10-28）")]
    public float douseTap = 0.34f, douseHalf = 0.5f;
    public Color glowColor = new Color(1f, 0.55f, 0.15f);
    public Color burnColor = new Color(1f, 0.15f, 0.05f);
    [Tooltip("燃えている時にふわふわについていく炎（なくてもOK）")]
    public ParticleSystem burnFx;
    public AudioSource burnSound;

    private bool _hover, _burning;
    private float _heatAt = -100f, _burnAt = -100f, _douse, _lastShotT = -100f;
    private Vector3 _anchor, _prevPos;

    public void Heat()
    {
        if (ball == null) return;
        HeatAt(ball.transform.position + Vector3.up * hoverRise);
    }
    // 止まる所を火の側で決める（2026-10-28 てじゃ「ホバーに入ったときの高さによってホバーする高さがかわるのが気になる」）
    public void HeatAt(Vector3 anchor)
    {
        if (ball == null || _hover) return;
        if (ball.LastShotAge() < reheatDelay) return;
        _hover = true; _heatAt = Time.time;
        _anchor = anchor;
    }
    public bool IsHovering() { return _hover; }

    // 火の玉・ほのおのまものに当たった：浮かずに、すぐ燃えだす（撃てば消える。2026-10-29 てじゃの 2–2 の流れ）
    public void Ignite()
    {
        if (ball == null || _burning || ball.IsWaitingOrHidden()) return;
        StartBurn(ball.transform.position);
    }
    // 水に入った：燃えていたら消える（浮いているのもおしまい）
    public void Douse()
    {
        _hover = false;
        if (_burning) { StopBurn(); if (ball != null) ball.Glow(new Color(0.85f, 0.9f, 1f), Vector3.up, 1f, 0.3f); }
    }
    // 火炎放射に当たった：その場で黒焦げ→アウト
    public void BurnOut()
    {
        if (ball == null || ball.IsWaitingOrHidden()) return;
        _hover = false; StopBurn();
        CharFx();
        ball.FailByBurn();
    }
    private float _charUntil;
    private void CharFx()
    {
        // 黒焦げの間は、黒い煙っぽく少しだけ炎を残す
        if (burnFx == null || ball == null) return;
        _charUntil = Time.time + ball.charFreezeTime + 0.2f;
        var e = burnFx.emission; e.rateOverTime = 15f;
        burnFx.transform.position = ball.transform.position;
    }
    private void StartBurn(Vector3 p)
    {
        _burning = true; _burnAt = Time.time; _douse = 0f;
        if (burnFx != null) { var e = burnFx.emission; e.rateOverTime = 40f; burnFx.transform.position = p; }
        if (burnSound != null && burnSound.clip != null) burnSound.PlayOneShot(burnSound.clip);
    }
    public bool IsBurning() { return _burning; }

    private void StopBurn()
    {
        _burning = false;
        if (burnFx != null) { var e = burnFx.emission; e.rateOverTime = 0f; }
    }

    private void Update()
    {
        if (burnFx != null && (_burning || _charUntil > 0f) && ball != null) burnFx.transform.position = ball.transform.position;
        if (_charUntil > 0f && Time.time > _charUntil) { _charUntil = 0f; if (!_burning && burnFx != null) { var e = burnFx.emission; e.rateOverTime = 0f; } }
    }

    // 溜め具合 → 消す量（0＝ためなし douseTap、0.5＝douseHalf、1＝1）
    private float DouseOf(float charge)
    {
        return charge <= 0.5f ? Mathf.Lerp(douseTap, douseHalf, charge / 0.5f) : Mathf.Lerp(douseHalf, 1f, (charge - 0.5f) / 0.5f);
    }

    private void FixedUpdate()
    {
        if (ball == null) return;
        Vector3 p = ball.transform.position;
        // スタートやチェックポイントへ戻った（一瞬で大きく動いた）らおしまい
        if ((p - _prevPos).sqrMagnitude > 25f) { _hover = false; StopBurn(); }
        _prevPos = p;
        float shotT = Time.time - ball.LastShotAge();
        bool newShot = shotT > _lastShotT + 0.01f;
        if (newShot) _lastShotT = shotT;

        // 燃えている：撃たれるたびに消える量をためる。たまったら消える、時間切れで燃え尽きてアウト
        if (_burning)
        {
            if (newShot)
            {
                _douse += DouseOf(ball.LastShotCharge());
                if (_douse >= 0.999f) { StopBurn(); ball.Glow(new Color(0.85f, 0.9f, 1f), Vector3.up, 1f, 0.3f); }
            }
            if (_burning && Time.time - _burnAt > burnGrace) { _hover = false; StopBurn(); CharFx(); ball.FailByBurn(); return; }
        }

        if (_hover)
        {
            // 撃たれたら（温まったあとに撃った）浮くのはおしまい。はなれすぎてもおしまい
            if (newShot && shotT > _heatAt + 0.05f) _hover = false;
            else if ((p - _anchor).magnitude > maxLeash) _hover = false;
        }
        if (_hover)
        {
            float age = Time.time - _heatAt;
            if (age > burnAfter && !_burning) StartBurn(p);
            Vector3 v = ball.GetVelocity();
            float ty = _anchor.y + Mathf.Sin(age * Mathf.PI * 2f / Mathf.Max(0.1f, bobPeriod)) * bobAmp;
            float ay = ball.GetFallAcceleration() + (ty - p.y) * spring - v.y * damping;
            // 横も止まる所へ（ばねは縦より弱め）
            Vector3 a = new Vector3((_anchor.x - p.x) * sideSpring - v.x * sideDamping, ay, (_anchor.z - p.z) * sideSpring - v.z * sideDamping);
            ball.AddHeat(a);
        }
        if (_burning)
        {
            // 燃え尽きに近いほど速く点滅
            float k = (Time.time - _burnAt) / Mathf.Max(0.1f, burnGrace);
            float blink = Mathf.Sin(Time.time * Mathf.PI * 2f * Mathf.Lerp(3f, 9f, k)) * 0.5f + 0.5f;
            ball.Glow(Color.Lerp(glowColor, burnColor, blink), Vector3.up, 1f, 0.05f);
        }
        else if (_hover) ball.Glow(glowColor, Vector3.up, 0.8f, 0.05f);
    }
}
