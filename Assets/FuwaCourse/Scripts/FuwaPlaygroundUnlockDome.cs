using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// あそびばのまだ出ていないおもちゃのドーム（2026-11-16 てじゃ「クリアフラグ持ちはおもちゃ予定地のドームをUSEすると解放できる」
//  「一度解放したらそのインスタンスでは解放されっぱなし」「新インスタンスでもクリアフラグ持ちはUSEで解放できる」）
// さわった人がそのコースをクリアしたことがあれば（FuwaRecords.HasCleared、保存されたクリアの印）、このインスタンスでおもちゃを出す（FuwaPlaygroundSpawner.Unlock、同期）
// まだの人がさわった時は、ドームがぷるっとゆれる＋看板が少し光る
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPlaygroundUnlockDome : UdonSharpBehaviour
{
    public FuwaRecords records;
    [Tooltip("records の中のコースの番号")]
    public int courseIndex;
    public FuwaPlaygroundSpawner spawner;
    [Tooltip("おもちゃの番号（Unlock に渡す）")]
    public int toyIndex;
    public Transform dome;
    public AudioSource unlockSound, denySound;

    private float _wobbleAt = -10f;
    private Vector3 _domeScale;

    private void Start() { if (dome != null) _domeScale = dome.localScale; }

    public override void Interact()
    {
        if (records == null || spawner == null) return;
        if (records.HasCleared(courseIndex))
        {
            spawner.Unlock(toyIndex);
            if (unlockSound != null) unlockSound.Play();
        }
        else
        {
            _wobbleAt = Time.time;
            if (denySound != null) denySound.Play();
        }
    }

    private void Update()
    {
        if (dome == null) return;
        float t = Time.time - _wobbleAt;
        if (t > 0.6f) { if (dome.localScale != _domeScale) dome.localScale = _domeScale; return; }
        float k = Mathf.Sin(t * 30f) * (1f - t / 0.6f) * 0.08f;
        dome.localScale = new Vector3(_domeScale.x * (1f + k), _domeScale.y * (1f - k), _domeScale.z * (1f + k));
    }
}
