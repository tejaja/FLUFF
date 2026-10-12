using UdonSharp;
using UnityEngine;

// みんなでひろばの「竜巻のたね」（コース1 かぜのおか を誰かがクリアすると出てくる。2026-10-12 てじゃ「竜巻を設置できる何か」）。
// たねは持って動かせて、手を離すとその場に浮いて止まる（VRCObjectSync）。竜巻（FuwaWhirlwind）はたねの真下の地面に立つ（持ったまま歩くと竜巻もついてくる）。
// 竜巻はたねの子にしない（たねの Rigidbody の子になるとトリガーの知らせが竜巻のスクリプトに届かなくなる）。それぞれの画面で同じ計算で置く
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaTornadoSeed : UdonSharpBehaviour
{
    public Transform tornado;
    [Tooltip("竜巻を立てる地面のレイヤー")]
    public LayerMask ground = 1 << 11;
    [Tooltip("下にこれ以上地面がなければ、たねのすぐ下に浮かべる")]
    public float maxDrop = 6f;
    [Tooltip("回して見せる物（なくてもOK）")]
    public Transform spinner;

    private void Update()
    {
        if (spinner != null) spinner.Rotate(0f, 240f * Time.deltaTime, 0f, Space.World);
        if (tornado == null) return;
        Vector3 p = transform.position;
        RaycastHit hit;
        Vector3 at = Physics.Raycast(p, Vector3.down, out hit, maxDrop, ground, QueryTriggerInteraction.Ignore) ? hit.point : p + Vector3.down * 0.4f;
        Vector3 fwd = transform.forward; fwd.y = 0f;
        Quaternion rot = fwd.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(fwd.normalized) : Quaternion.identity;
        if ((tornado.position - at).sqrMagnitude > 0.000001f) tornado.SetPositionAndRotation(at, rot);
    }
}
