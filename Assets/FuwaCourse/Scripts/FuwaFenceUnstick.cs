using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 丸い柵の上に乗ってしまったら、柵の外か内側へ押し出す（柵の上で動けなくなる対策）。
// このオブジェクトの位置＝柵の円の中心。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaFenceUnstick : UdonSharpBehaviour
{
    [Tooltip("柵の半径")]
    public float radius = 8f;
    [Tooltip("柵の上とみなす幅（半径±）")]
    public float band = 0.35f;
    [Tooltip("このオブジェクトの高さから何m上より上にいたら「柵の上」")]
    public float minHeight = 0.35f;
    public float maxHeight = 1.4f;
    public float pushSpeed = 2.5f;
    public float pushUp = 1.5f;

    private void Update()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        Vector3 p = lp.GetPosition();
        Vector3 c = transform.position;
        float h = p.y - c.y;
        if (h < minHeight || h > maxHeight) return;
        Vector3 d = p - c; d.y = 0f;
        float r = d.magnitude;
        if (r < 0.01f || Mathf.Abs(r - radius) > band) return;
        if (!lp.IsPlayerGrounded()) return;
        // 少しでも外側にいたら外へ、内側なら内へ（ちょうど真上なら外へ＝飛び降りたい人向け）
        Vector3 dir = (r >= radius ? d : -d) / r;
        Vector3 v = dir * pushSpeed;
        v.y = pushUp;
        lp.SetVelocity(v);
    }
}
