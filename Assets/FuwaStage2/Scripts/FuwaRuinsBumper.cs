using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2026-11-14 2–5 上の段（てじゃ「回る石の腕と飛び出す石ブロックの複合」「当たったら弾かれるくらいでいい」）
//  mode 0：回る石の腕。この物体のたて軸まわりに degPerSec で回る（子の腕のトリガーで当たり）
//  mode 1：飛び出す石ブロック。a → b（親の中の位置）へ outTime で突き出して holdOut 止まり、backTime でもどる（period ごと）
//  全員同じ動き（サーバー時刻）。当たったふわふわ・自分を弾く（アウトにはしない）。同期なし
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaRuinsBumper : UdonSharpBehaviour
{
    public int mode = 0;
    [Header("回る腕")]
    public float degPerSec = 45f;
    public float phaseDeg = 0f;
    [Header("飛び出すブロック")]
    public Vector3 a, b;
    public float period = 3.2f, phase = 0f, outTime = 0.25f, holdOut = 0.8f, backTime = 1.0f;
    [Header("弾く強さ")]
    public float ballPush = 5f, ballUp = 2f;
    public float playerPush = 5.5f, playerUp = 3f;
    public AudioSource hitSound;

    private Quaternion _base;
    private float _ballCool, _playerCool;

    private void Start()
    {
        _base = transform.localRotation;
    }

    private void Update()
    {
        int ms = Networking.GetServerTimeInMilliseconds();
        if (mode == 0)
        {
            float deg = Mathf.Abs(degPerSec);
            if (deg < 0.01f) return;
            int per = Mathf.Max(1, Mathf.RoundToInt(360f / deg * 1000f));
            int m = ms % per; if (m < 0) m += per;
            float ang = m / (float)per * 360f * Mathf.Sign(degPerSec) + phaseDeg;
            transform.localRotation = _base * Quaternion.Euler(0f, ang, 0f);
        }
        else
        {
            int per = Mathf.Max(1, Mathf.RoundToInt(period * 1000f));
            int m = (ms + Mathf.RoundToInt(phase * 1000f)) % per; if (m < 0) m += per;
            float t = m / 1000f, u;
            if (t < outTime) { float x = t / outTime; u = 1f - (1f - x) * (1f - x); }
            else if (t < outTime + holdOut) u = 1f;
            else if (t < outTime + holdOut + backTime) { float x = (t - outTime - holdOut) / backTime; u = 1f - x * x * (3f - 2f * x); }
            else u = 0f;
            transform.localPosition = Vector3.Lerp(a, b, u);
        }
    }

    // p（ワールド）にいる物を弾く向き
    private Vector3 PushDir(Vector3 p)
    {
        Vector3 d;
        if (mode == 0)
        {
            Vector3 axis = transform.up;
            Vector3 r = p - transform.position; r -= axis * Vector3.Dot(r, axis);
            d = Vector3.Cross(axis, r) * Mathf.Sign(degPerSec);
            if (d.sqrMagnitude < 0.04f) d = r;
            d = d.normalized + r.normalized * 0.35f;
        }
        else
        {
            Transform par = transform.parent;
            d = par != null ? par.TransformDirection(b - a) : (b - a);
        }
        d.y = 0f;
        return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || Time.time < _ballCool) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null) return;
        _ballCool = Time.time + 0.35f;
        ball.Launch(PushDir(other.transform.position) * ballPush + Vector3.up * ballUp);
        if (hitSound != null) hitSound.Play();
    }

    private void OnTriggerStay(Collider other)
    {
        OnTriggerEnter(other);
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || Time.time < _playerCool) return;
        _playerCool = Time.time + 0.35f;
        player.SetVelocity(PushDir(player.GetPosition()) * playerPush + Vector3.up * playerUp);
        if (hitSound != null) hitSound.Play();
    }

    public override void OnPlayerTriggerStay(VRCPlayerApi player)
    {
        OnPlayerTriggerEnter(player);
    }
}
