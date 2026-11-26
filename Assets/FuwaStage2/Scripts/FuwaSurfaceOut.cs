using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 2026-11-26 てじゃ「2-2の途中、まものエリア付近でデカい山のほうに落下すると、山に乗っちゃう」：
// 当たり判定のある飾り（2–2 の火山）に付ける。自分がその上に「立った」ら落ちた扱い（FuwaPlayerCatcher と同じ「落ちた！」）。
// ・てじゃ「まったくのれないのもやだな」→ 足が maxFeetY より下（道より下のすそ）の時だけ
// ・道のふちで山肌にかすっただけではアウトにしないよう、足の真下の一番近い当たりがこの物の時だけ
// ・OnPlayerCollision〜は立っているだけだと来なかった（ClientSim）ので、毎フレーム見る。高さと箱の外ならすぐ戻るので軽い
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSurfaceOut : UdonSharpBehaviour
{
    public FuwaBall ball;
    [Tooltip("足（GetPosition）がこの高さ（ワールド y）より下の時だけアウト")]
    public float maxFeetY = 100000f;
    [Tooltip("足の真下を調べる長さ(m)")]
    public float probe = 1.2f;
    [Tooltip("この秒数つづけて乗っていたらアウト（一瞬かすっただけは無視）")]
    public float holdTime = 0.15f;

    private Collider _col;
    private Bounds _b;
    private float _since = -1f;
    private int _mask;

    private void Start()
    {
        _col = GetComponent<Collider>();
        if (_col != null) { _b = _col.bounds; _b.Expand(2f); }
        // プレイヤーの層（9 Player, 10 PlayerLocal）とトリガーは見ない
        _mask = ~((1 << 9) | (1 << 10));
    }

    private void Update()
    {
        if (_col == null || ball == null) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        Vector3 p = lp.GetPosition();
        if (p.y >= maxFeetY || !_b.Contains(p)) { _since = -1f; return; }
        RaycastHit hit;
        bool onMe = Physics.Raycast(p + Vector3.up * 0.4f, Vector3.down, out hit, probe, _mask, QueryTriggerInteraction.Ignore) && hit.collider == _col;
        if (!onMe) { _since = -1f; return; }
        if (_since < 0f) { _since = Time.time; return; }
        if (Time.time - _since < holdTime) return;
        _since = -1f;
        ball.BeginPlayerMiss("落ちた！", "You fell!");
    }
}
