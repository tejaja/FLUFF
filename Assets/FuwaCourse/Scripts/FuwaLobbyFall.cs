using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// ロビーの下に置く薄い板状のトリガー。ロビーから飛び降りたら、少し落ちたところで暗転してロビーへ戻す（自分だけ）。
// 飛び降り自体は楽しめるけど、下のコース（入口の小島や橋）に降りて直接コースへ入ることはできないようにする。
// 戻し先は、そのロビーへ行くワープ（FuwaCourseWarp）の DoWarp をそのまま使う（位置・胞子・銃・エリア表示もワープと同じ）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaLobbyFall : UdonSharpBehaviour
{
    [Tooltip("このロビーへ行くワープ（戻す時に DoWarp を呼ぶ）")]
    public FuwaCourseWarp returnWarp;
    public FuwaFade fade;
    [Tooltip("板を通ってから暗転し始めるまで、そのまま落ちていられる秒数（ここで下の小島に着いても、必ずロビーへ戻る）")]
    public float fallDelay = 0.45f;
    public float fadeOutTime = 0.4f;
    public float fadeHold = 0.15f;
    public float fadeInTime = 0.35f;
    [Tooltip("落ちた時の音（任意）")]
    public AudioSource fallSound;

    [Tooltip("ON なら、ストンと落ちている時（重力で下向きにどんどん速くなっている時）だけ戻す。飛んで降りている人（速さがほぼ一定）は通す（2026-10-14 てじゃ：飛んでエリアの間を移動できるように。飛行はけっこう速いので速さでは見分けない）")]
    public bool onlyFreeFall = true;

    private float _busyUntil;
    // 少し前の下向きの速さ（加速しているかを見る）。A が新しい方、B が 0.1〜0.2 秒前
    private float _vyA, _tA = -1f, _vyB, _tB = -1f;

    private void Update()
    {
        if (!onlyFreeFall) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        if (Time.time - _tA < 0.1f) return;
        _vyB = _vyA; _tB = _tA;
        _vyA = lp.GetVelocity().y; _tA = Time.time;
    }

    // 重力で落ちているか：0.1〜0.3秒の間に、重力の半分以上の勢いで下向きに速くなっている
    private bool IsFreeFalling(VRCPlayerApi lp)
    {
        if (!onlyFreeFall) return true;
        float dt = Time.time - _tB;
        if (_tB < 0f || dt < 0.05f || dt > 0.5f) return true;   // 測れていない時は今まで通り戻す
        float g = -Physics.gravity.y * lp.GetGravityStrength();
        float accel = (lp.GetVelocity().y - _vyB) / dt;
        return accel < -0.5f * g;
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || returnWarp == null) return;
        if (Time.time < _busyUntil) return;
        if (!IsFreeFalling(player)) return;
        _busyUntil = Time.time + fallDelay + fadeOutTime + fadeHold + fadeInTime;
        if (fallSound != null) { fallSound.Stop(); fallSound.Play(); }
        if (fade == null) { SendCustomEventDelayedSeconds(nameof(_Return), fallDelay); return; }
        // 暗転は落ちている途中から。動けなくするのは暗転し始めてから（落ちている間に止めると空中で止まって見える）
        fade.Play(fallDelay, fadeOutTime, fadeHold, fadeInTime);
        SendCustomEventDelayedSeconds(nameof(_StartFreeze), fallDelay);
        SendCustomEventDelayedSeconds(nameof(_Return), fallDelay + fadeOutTime);
    }

    public void _StartFreeze()
    {
        if (fade != null) fade.FreezeFor(fadeOutTime + fadeHold + fadeInTime * 0.5f);
    }

    public void _Return()
    {
        if (returnWarp != null) returnWarp.DoWarp();
    }
}
