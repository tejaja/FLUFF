using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// ひみつの小島のきのこ（2026-10-16 てじゃ「おもちゃ全解放スイッチ、ウラワザみたいな感じでどっかに置いておくのもいい」）。
// ロビーの写真館のボードを突っ切って飛び降りた先の小島にある。クリックすると、あそびばのおもちゃを全部出す（コースをクリアした時と同じく、このインスタンスの全員に）。
// 押した人の画面だけ、キラキラと音（出ているかどうかは同期の FuwaPlaygroundSpawner が持つので、ここは同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSecretUnlock : UdonSharpBehaviour
{
    public FuwaPlaygroundSpawner spawner;
    [Tooltip("押した時に出す粒")]
    public ParticleSystem burst;
    public AudioSource sound;
    [Tooltip("押した時にぽよんとはねる傘")]
    public Transform cap;
    public int toyCount = 5;

    private float _boingT = -1f;
    private Vector3 _capScale;

    private void Start()
    {
        if (cap != null) _capScale = cap.localScale;
    }

    public override void Interact()
    {
        if (spawner != null) for (int i = 0; i < toyCount; i++) spawner.Unlock(i);
        if (burst != null) burst.Play();
        if (sound != null) { sound.Stop(); sound.Play(); }
        _boingT = 0f;
    }

    private void Update()
    {
        if (_boingT < 0f || cap == null) return;
        _boingT += Time.deltaTime;
        const float dur = 0.45f;
        if (_boingT >= dur) { cap.localScale = _capScale; _boingT = -1f; return; }
        // つぶれてからふくらんで戻る
        float s = Mathf.Sin(_boingT / dur * Mathf.PI * 2f) * Mathf.Exp(-_boingT * 5f) * 0.25f;
        cap.localScale = new Vector3(_capScale.x * (1f + s), _capScale.y * (1f - s), _capScale.z * (1f + s));
    }
}
