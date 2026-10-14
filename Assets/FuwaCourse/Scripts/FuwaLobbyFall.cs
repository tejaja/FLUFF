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

    private float _busyUntil;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || returnWarp == null) return;
        if (Time.time < _busyUntil) return;
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
