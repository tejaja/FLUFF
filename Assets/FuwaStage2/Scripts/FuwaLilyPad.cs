using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 蓮の葉の足場（ステージ2「みずのたに」、2026-10-17）。水に浮いていて、いつもゆらゆら。人が乗ると少し沈んで、乗った側へ少し傾く。
// 自分が乗った時だけ動かす（自分の画面だけ。ほかの人の葉っぱは揺れているだけ）。当たり判定ごと動くので、乗ったまま上下する
// この物に：歩ける当たり（ふつうのコライダー）＋乗っているかを見るトリガー（上に薄く）。見た目もこの物か子に
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaLilyPad : UdonSharpBehaviour
{
    [Tooltip("乗った時に沈む深さ(m)")]
    public float sinkDepth = 0.045f;   // 水面より下に沈むと水の板に隠れて見えなくなるので、浮かせて置いて沈みは浅め
    [Tooltip("沈む・戻る速さ（1/s）")]
    public float sinkSpeed = 4f;
    [Tooltip("乗った側へ傾く角度（ふちに立った時、度）")]
    public float tiltDeg = 5f;
    [Tooltip("葉っぱの半径(m)（傾きの計算用）")]
    public float radius = 0.9f;
    [Tooltip("いつものゆらゆら：上下の幅(m)と速さ")]
    public float bobAmp = 0.012f;
    public float bobSpeed = 1.1f;
    [Tooltip("乗った瞬間の音（なくてもOK）")]
    public AudioSource stepSound;
    [Header("長く乗っていると沈む（2026-10-17 てじゃ「傾くの体感できないかも、長く乗ってると沈んで浅瀬扱いに」）")]
    [Tooltip("乗ってからこの秒数で沈みはじめる")]
    public float deepDelay = 2f;
    [Tooltip("沈む深さ(m)。水面より下に足が入って浅瀬（FuwaShallowWater）扱いになる深さ")]
    public float deepDepth = 0.15f;
    [Tooltip("沈む速さ・浮かぶ速さ(m/s)")]
    public float deepSinkSpeed = 0.12f;
    public float deepRiseSpeed = 0.2f;
    private float _onTime;
    private float _deep;

    private Vector3 _basePos;
    private Quaternion _baseRot;
    private bool _on;
    private float _sink;
    private Vector2 _tilt;
    private float _phase;
    private float _kick;

    private void Start()
    {
        _basePos = transform.localPosition;
        _baseRot = transform.localRotation;
        _phase = (transform.position.x * 0.37f + transform.position.z * 0.61f) % 6.28f;
    }

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        _on = true;
        _kick = 1f;   // 乗った瞬間だけ少し深く沈む
        if (stepSound != null) { stepSound.pitch = Random.Range(0.92f, 1.08f); stepSound.Play(); }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        _on = false;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        _kick = Mathf.MoveTowards(_kick, 0f, dt * 3f);
        float target = _on ? sinkDepth * (1f + 0.5f * _kick) : 0f;
        _sink = Mathf.Lerp(_sink, target, 1f - Mathf.Exp(-sinkSpeed * dt));
        Vector2 tiltTarget = Vector2.zero;
        if (_on)
        {
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (lp != null)
            {
                Vector3 d = lp.GetPosition() - transform.position;
                // 乗っている場所（真ん中からのずれ）の向きへ傾く（ふちほど大きく）
                tiltTarget = Vector2.ClampMagnitude(new Vector2(d.x, d.z) / Mathf.Max(0.1f, radius), 1f) * tiltDeg;
            }
        }
        _tilt = Vector2.Lerp(_tilt, tiltTarget, 1f - Mathf.Exp(-sinkSpeed * dt));
        // 長く乗っていると、じわっと沈む。降りたら少し待ってから浮いてくる
        _onTime = _on ? _onTime + dt : Mathf.Max(0f, _onTime - dt * 3f);
        bool deep = _onTime > deepDelay;
        _deep = Mathf.MoveTowards(_deep, deep ? deepDepth : 0f, (deep ? deepSinkSpeed : deepRiseSpeed) * dt);
        float bob = Mathf.Sin(Time.time * bobSpeed + _phase) * bobAmp * (1f - Mathf.Clamp01(_deep / Mathf.Max(0.001f, deepDepth)));
        Vector3 p = _basePos; p.y += bob - _sink - _deep;
        transform.localPosition = p;
        // 世界のx方向のずれ→z軸まわり（-）、z方向のずれ→x軸まわり（+）に傾ける
        transform.localRotation = Quaternion.Euler(_tilt.y, 0f, -_tilt.x) * _baseRot;
    }
}
