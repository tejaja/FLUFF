using UdonSharp;
using UnityEngine;

// ふわふわが入ると、決まった向きにビュンっと打ち出す。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaLaunchPad : UdonSharpBehaviour
{
    [Tooltip("打ち出す向き（このオブジェクトのローカル座標）")]
    public Vector3 localDirection = new Vector3(0f, 4f, 6f);
    [Tooltip("打ち出す速さ(m/s)")]
    public float speed = 7f;
    [Tooltip("一度打ち出したら、次に反応するまでの秒数")]
    public float cooldown = 1f;
    [Tooltip("0より大きいと、打ち出してからこの秒数だけ空気抵抗を弱くして遠くへ飛ばす（谷越え用）")]
    public float flightSeconds = 0f;
    [Tooltip("遠くへ飛ばしている間の空気抵抗（ふだんは1.2）")]
    public float flightDrag = 0.3f;
    [Tooltip("ONなら打ち出した時のふわふわの光を glowColor にする（OFFならふつうのオレンジ）")]
    public bool customGlow = false;
    public Color glowColor = new Color(0.45f, 1f, 0.55f, 1f);
    public ParticleSystem launchEffect;
    public AudioSource launchSound;

    [Header("どこで当たっても同じ場所へ落とす（なくてもOK）")]
    [Tooltip("ここへ落ちるように、当たった位置から横の速さを計算する。上の速さは localDirection のまま")]
    public Transform target;
    [Tooltip("ふわふわの落ちる加速度・ふだんの空気抵抗・落ちる速さの上限（FuwaBall と同じ値）")]
    public float ballFall = 1.5f, ballDrag = 1.2f, ballMaxFall = 0.7f;

    private float _readyAt;

    // 上の速さを決めて落ちるまでを歩かせ、横の速さ 1m/s で進む距離を出して、target までの距離から逆算する（空気抵抗は速さに比例なので横と縦は別に計算できる）
    private Vector3 AimedVelocity(Vector3 from, float up)
    {
        Vector3 d = target.position - from; float targetY = d.y; d.y = 0f;
        const float dt = 0.02f;
        float vy = up, y = 0f, keep = 1f, reach = 0f, t = 0f;
        while (t < 8f)
        {
            float drag = t < flightSeconds ? flightDrag : ballDrag;
            vy = (vy - ballFall * dt) * (1f - drag * dt);
            if (t >= flightSeconds && vy < -ballMaxFall) vy = -ballMaxFall;
            y += vy * dt;
            keep *= 1f - drag * dt;
            reach += keep * dt;
            t += dt;
            if (vy < 0f && y <= targetY) break;
        }
        Vector3 v = reach > 0.01f ? d / reach : Vector3.zero;
        v.y = up;
        return v;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || Time.time < _readyAt) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;
        _readyAt = Time.time + cooldown;
        Vector3 vel = transform.TransformDirection(localDirection).normalized * speed;
        if (target != null) vel = AimedVelocity(other.transform.position, vel.y);
        Color glow = customGlow ? glowColor : ball.launchGlowColor;
        if (flightSeconds > 0f) ball.LaunchFlightTinted(vel, flightSeconds, flightDrag, glow);
        else ball.LaunchTinted(vel, glow);
        if (launchEffect != null) launchEffect.Play();
        if (launchSound != null) launchSound.Play();
    }
}

