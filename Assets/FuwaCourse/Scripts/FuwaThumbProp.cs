using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// サムネ撮影用の小道具（2026-11-19 てじゃ「サムネ取り直したい。空気が発射されてるように見える銃のピックアップと、ちょっと潰れて表情が変わってるふわふわのピックアップがほしい。見えるのはTejaのみ」）
//  ownerNames の表示名の人の画面にだけ出る（ほかの人の画面では最初に自分ごと消える）。同期なし：位置も自分の画面だけ。
//  持ってはなすと、その場の空中に止まる（Rigidbody は isKinematic）。
//  銃：トリガー（Use）で、空気の粒を一時停止／再開（いい瞬間で止めて撮る）。
//  ふわふわ：トリガー（Use）で表情を切りかえ（毛の材質の _FaceHit：0 ふつう／1 ＞＜／2 こまり顔）。つぶれ方は squashed の大きさで決まる。
//  公開前に消す（FuwaSystem/ThumbProps を消すだけ）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaThumbProp : UdonSharpBehaviour
{
    [Tooltip("この表示名の人にだけ見える")]
    public string[] ownerNames;
    [Header("銃")]
    public ParticleSystem[] particles;
    [Header("ふわふわ")]
    public Renderer fur;
    public int face = 1;
    public int faceCount = 3;

    private Material _mat;
    private bool _paused;

    private void Start()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        bool ok = false;
        if (p != null && ownerNames != null)
        {
            string n = p.displayName;
            for (int i = 0; i < ownerNames.Length; i++) if (ownerNames[i] == n) ok = true;
        }
        if (!ok) { gameObject.SetActive(false); return; }
        if (fur != null) { _mat = fur.material; _mat.SetFloat("_FaceHit", face); }
    }

    public override void OnPickupUseDown()
    {
        if (_mat != null)
        {
            face = (face + 1) % Mathf.Max(1, faceCount);
            _mat.SetFloat("_FaceHit", face);
        }
        if (particles != null && particles.Length > 0)
        {
            _paused = !_paused;
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] == null) continue;
                if (_paused) particles[i].Pause(); else particles[i].Play();
            }
        }
    }
}
