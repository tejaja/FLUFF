using UdonSharp;
using UnityEngine;

// 2–4 はぐるまのとう：エアガンで撃つと回る風車（2026-11-06 お試し）
// ・風車 → 歯車のつながり → 跳ね橋（扇形の歯車）。撃つと回って橋が下りる、撃たないとおもりでじわじわ戻る
// ・てじゃ「同期させるとソロ攻略としておかしくなるからローカルでいい」→ 自分の画面だけで動く（同期なし）
// ・歯車の角度は「風車の回った量 turn」から全部決める（角度 = ratio × u + phase、u = -turn / ratio[0]）。どれだけ回っても歯はずれない
// ・角度は歯車の 2D の向き（度、反時計回り）。Unity のローカル Y 回転には符号を反転して入れる
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaWindmill : UdonSharpBehaviour
{
    [Tooltip("羽根（ローカル Y 軸で回る）")]
    public Transform rotor;
    [Tooltip("歯車。0 番目が風車と同じ軸の歯車")]
    public Transform[] gears;
    public float[] gearRatio;
    public float[] gearPhase;
    [Tooltip("当たり：風の円すいがこの半径の球にかすれば当たり")]
    public float hitRadius = 1.3f;
    [Tooltip("フルパワーで撃った時に足す回る速さ（度/秒）")]
    public float spinPerShot = 420f;
    public float maxSpin = 900f;
    [Tooltip("回る速さの減り方（1秒あたり）")]
    public float drag = 1.2f;
    [Tooltip("おもりで戻る速さ（風車の度/秒）と、そこまでの加速")]
    public float returnSpeed = 45f;     // 2026-11-06 てじゃ「もどるのがはやい」120→45（橋が上がりきるまで 約19秒）
    public float returnAccel = 25f;
    [Tooltip("最後に撃ってから戻り始めるまで（秒）")]
    public float holdTime = 2.5f;
    [Tooltip("いちばん回した時の turn（度）。橋が下りきる所")]
    public float maxTurn = 900f;
    [Tooltip("風車の軸の向き（ワールド）。風がこの向きに近いほどよく回る")]
    public Transform axisRef;
    public AudioSource clickSound;

    private float _turn, _vel, _lastHit = -100f;
    private int _lastTooth;

    public void Blown(Vector3 aim, float power)
    {
        float k = 1f;
        if (axisRef != null) k = Mathf.Lerp(0.35f, 1f, Mathf.Abs(Vector3.Dot(aim.normalized, axisRef.up)));
        _vel = Mathf.Min(maxSpin, Mathf.Max(_vel, 0f) + spinPerShot * power * k);
        _lastHit = Time.time;
    }

    public float GetOpen() { return maxTurn > 0f ? _turn / maxTurn : 0f; }

    private void Start() { Apply(); }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        // 速さの減り（空気・まさつ）＋おもりで逆向きへ
        _vel *= Mathf.Exp(-drag * dt);
        if (_turn > 0f && Time.time > _lastHit + holdTime) _vel = Mathf.Max(_vel - returnAccel * dt, -returnSpeed);
        _turn += _vel * dt;
        if (_turn <= 0f) { _turn = 0f; if (_vel < 0f) _vel = 0f; }
        if (_turn >= maxTurn) { _turn = maxTurn; if (_vel > 0f) _vel = 0f; }
        Apply();
    }

    private void Apply()
    {
        if (rotor != null) rotor.localRotation = Quaternion.Euler(0f, _turn, 0f);
        if (gears == null || gearRatio == null || gearPhase == null || gears.Length == 0) return;
        float r0 = gearRatio[0];
        float u = r0 != 0f ? -_turn / r0 : 0f;
        for (int i = 0; i < gears.Length; i++)
        {
            if (gears[i] == null) continue;
            float a = gearRatio[i] * u + gearPhase[i];
            gears[i].localRotation = Quaternion.Euler(0f, -a, 0f);
        }
        // 歯が1本進むごとにカチッ（風車の歯車の歯数ぶんで1周）
        if (clickSound != null)
        {
            int tooth = Mathf.FloorToInt(_turn / 22.5f);
            if (tooth != _lastTooth) { _lastTooth = tooth; clickSound.pitch = 0.9f + Random.value * 0.2f; clickSound.Play(); }
        }
    }
}
