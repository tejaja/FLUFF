using UdonSharp;
using UnityEngine;

// 2–4 はぐるまのとう：まん中の雲のバルーン（2026-11-08 てじゃ「白パイプだけにして、上にあげてでっかいバルーン状の機械に接続、上から雲がぽわぽわ」）
// ・白いパイプ（FuwaSugarPipe）からふくらみが着くたびに Puff() → バルーンが少しふくらんで戻る＋てっぺんから雲がぽわっ
// ・いつもの雲（ゆっくり）は別の粒が出しっぱなし。息をするようにゆっくり大きさが変わる
// 自分の画面だけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaCloudBalloon : UdonSharpBehaviour
{
    public Transform body;
    public ParticleSystem burst;
    public int burstCount = 7;
    public float swell = 0.05f, breathe = 0.015f, breathePeriod = 5f;

    private float _kick;

    public void Puff()
    {
        _kick = 1f;
        if (burst != null) burst.Emit(burstCount);
    }

    private void Update()
    {
        if (body == null) return;
        _kick = Mathf.Max(0f, _kick - Time.deltaTime * 1.4f);
        float k = _kick * _kick * (3f - 2f * _kick);   // ふわっとふくらんで、ゆっくり戻る
        float s = 1f + swell * k + breathe * Mathf.Sin(Time.time * 2f * Mathf.PI / breathePeriod);
        body.localScale = new Vector3(s, 1f + (s - 1f) * 0.6f, s);
    }
}
