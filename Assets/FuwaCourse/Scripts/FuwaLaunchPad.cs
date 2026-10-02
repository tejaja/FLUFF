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

    private float _readyAt;

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || Time.time < _readyAt) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;
        _readyAt = Time.time + cooldown;
        Vector3 vel = transform.TransformDirection(localDirection).normalized * speed;
        Color glow = customGlow ? glowColor : ball.launchGlowColor;
        if (flightSeconds > 0f) ball.LaunchFlightTinted(vel, flightSeconds, flightDrag, glow);
        else ball.LaunchTinted(vel, glow);
        if (launchEffect != null) launchEffect.Play();
        if (launchSound != null) launchSound.Play();
    }
}

