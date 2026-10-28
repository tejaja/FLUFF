using UdonSharp;
using UnityEngine;

// ステージ2「ほのおのやま」の熱（2026-10-28 試作）：かがり火（FuwaHeatSource）・火柱（FuwaFireJet）を通ったふわふわが温まって、
// 少し上がった所でふわっと止まって浮いたまま待つ（熱気球みたいに）。次に撃たれるまで浮いたまま（冷めない）。
// 2026-10-28 てじゃ「推進力がないから活用がむずかしい」「とどまるのを利用する手もある、タイミング図るのムズイポイント」「冷めるのはいらんかも」
// → 浮いて止まっている間に、向きを変えたり（曲がり角）タイミングを見て撃つ遊び。温まっている間はオレンジに光る。
// てじゃ「あまりに長時間浮かせてると燃えるとかもおもろい、厳しすぎずに」「燃えてもすぐショットすれば消える」
// → burnAfter 秒浮いていると燃えだす（赤く点滅＋炎）。burnGrace 秒以内に撃てば消える、撃たなければ燃え尽きてアウト
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
    public float burnGrace = 3f;
    public Color glowColor = new Color(1f, 0.55f, 0.15f);
    public Color burnColor = new Color(1f, 0.15f, 0.05f);
    [Tooltip("燃えている時にふわふわについていく炎（なくてもOK）")]
    public ParticleSystem burnFx;
    public AudioSource burnSound;

    private bool _hover, _burning;
    private float _heatAt = -100f;
    private Vector3 _anchor;

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
        _hover = true; _burning = false; _heatAt = Time.time;
        _anchor = anchor;
    }
    public bool IsHovering() { return _hover; }

    private void Stop()
    {
        _hover = false; _burning = false;
        if (burnFx != null) { var e = burnFx.emission; e.rateOverTime = 0f; }
    }

    private void Update()
    {
        if (burnFx != null && _burning && ball != null) burnFx.transform.position = ball.transform.position;
    }

    private void FixedUpdate()
    {
        if (ball == null || !_hover) return;
        // 撃たれたら（温まったあとに撃った）おしまい＝燃えていても消える。はなれすぎてもおしまい
        if (ball.LastShotAge() < Time.time - _heatAt - 0.05f) { Stop(); return; }
        Vector3 p = ball.transform.position;
        if ((p - _anchor).magnitude > maxLeash) { Stop(); return; }
        float age = Time.time - _heatAt;
        // 燃える
        if (age > burnAfter + burnGrace) { Stop(); ball.Fail(); return; }
        if (age > burnAfter && !_burning)
        {
            _burning = true;
            if (burnFx != null) { var e = burnFx.emission; e.rateOverTime = 40f; burnFx.transform.position = p; }
            if (burnSound != null && burnSound.clip != null) burnSound.PlayOneShot(burnSound.clip);
        }
        Vector3 v = ball.GetVelocity();
        float ty = _anchor.y + Mathf.Sin(age * Mathf.PI * 2f / Mathf.Max(0.1f, bobPeriod)) * bobAmp;
        float ay = ball.GetFallAcceleration() + (ty - p.y) * spring - v.y * damping;
        // 横も止まる所へ（ばねは縦より弱め）
        Vector3 a = new Vector3((_anchor.x - p.x) * sideSpring - v.x * sideDamping, ay, (_anchor.z - p.z) * sideSpring - v.z * sideDamping);
        ball.AddHeat(a);
        if (_burning)
        {
            // 燃え尽きに近いほど速く点滅
            float k = (age - burnAfter) / Mathf.Max(0.1f, burnGrace);
            float blink = Mathf.Sin(age * Mathf.PI * 2f * Mathf.Lerp(3f, 9f, k)) * 0.5f + 0.5f;
            ball.Glow(Color.Lerp(glowColor, burnColor, blink), Vector3.up, 1f, 0.05f);
        }
        else ball.Glow(glowColor, Vector3.up, 0.8f, 0.05f);
    }
}
