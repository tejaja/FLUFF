using UdonSharp;
using UnityEngine;

// スタートのキノコ。エアガンで撃つと傘がぷるんと揺れて、中にしまってある胞子（けだま）が飛び出す
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaStartMushroom : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("このキノコのコースのスタート位置（胞子がここのスタートで隠れている時だけ反応）")]
    public Transform courseStart;
    [Tooltip("揺らす傘")]
    public Transform cap;
    public ParticleSystem burst;
    public AudioSource popSound;
    [Tooltip("撃たれるたびに鳴る「ボヨヨン」（胞子が出ない時も鳴る）")]
    public AudioSource boingSound;
    [Tooltip("みんなでのロビー用：ここに隠れている同期ふわふわ（ball の代わり）")]
    public FuwaSharedBall sharedBall;
    [Tooltip("ふわふわがこのキノコに隠れている間だけ出す▼（飛び出したら消える）")]
    public GameObject startArrow;
    [Tooltip("チルルーム用：撃つたびに今の設定のふわふわを1個出す（ball・sharedBall の代わり）")]
    public FuwaChillSpawner spawner;

    private float _wobbleStart = -1f;
    private Vector3 _capScale;

    private void Start()
    {
        if (cap != null) _capScale = cap.localScale;
    }

    // エアガンから呼ばれる（shotDir＝撃った向き。胞子はその向きに飛び出す）
    public void Hit(Vector3 shotDir)
    {
        _wobbleStart = Time.time;
        if (boingSound != null && boingSound.clip != null)
        {
            boingSound.pitch = Random.Range(0.95f, 1.05f);
            boingSound.PlayOneShot(boingSound.clip);
        }
        if (spawner != null)
        {
            spawner.Spawn(shotDir);
            if (burst != null) burst.Play();
            if (popSound != null) popSound.Play();
            return;
        }
        if (sharedBall != null)
        {
            if (!sharedBall.IsWaiting()) return;
            sharedBall.ReleaseFromMushroom(shotDir);
            if (burst != null) burst.Play();
            if (popSound != null) popSound.Play();
            return;
        }
        if (ball == null || !ball.IsHidden() || ball.startPoint != courseStart) return;
        ball.ReleaseFromMushroom(shotDir);
        if (burst != null) burst.Play();
        if (popSound != null) popSound.Play();
    }

    // ふわふわがこのキノコに隠れている（＝撃てば出てくる）か
    private bool BallInside()
    {
        if (spawner != null) return true;   // チルルーム：いつでも出せる
        if (sharedBall != null) return sharedBall.IsWaiting();
        return ball != null && ball.IsHidden() && ball.startPoint == courseStart;
    }

    private void Update()
    {
        if (startArrow != null)
        {
            bool show = BallInside();
            if (startArrow.activeSelf != show) startArrow.SetActive(show);
        }
        if (_wobbleStart < 0f || cap == null) return;
        float t = Time.time - _wobbleStart;
        if (t > 0.8f) { cap.localScale = _capScale; _wobbleStart = -1f; return; }
        float k = Mathf.Sin(t * 28f) * Mathf.Exp(-t * 5f) * 0.12f;
        cap.localScale = new Vector3(_capScale.x * (1f + k), _capScale.y * (1f - k), _capScale.z * (1f + k));
    }
}
