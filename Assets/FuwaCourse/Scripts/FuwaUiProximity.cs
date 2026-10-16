using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// レーザーで押すUI（言語・BGMボタン、音量バー、スタートボタン）を、近くにいる時だけ押せるようにする。
// 遠いとUIの当たり判定（VRCUiShape の BoxCollider）を切るので、レーザーが当たらない。見た目はそのまま。同期なし
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaUiProximity : UdonSharpBehaviour
{
    [Tooltip("レーザーで押すUIの当たり判定（各 Canvas の BoxCollider）")]
    public Collider[] uiColliders;
    [Tooltip("この距離(m)より近い時だけ押せる（頭からUIまで）")]
    public float range = 3f;
    [Tooltip("uiColliders と同じ順：そのUIだけの距離（0なら range）。ロビーの看板のボタンは銃の台・練習のきのこから4.4〜5mにあって、5mだと銃を取る・撃つ時に押してしまったので4m（2026-10-16 てじゃ）")]
    public float[] ranges;
    [Tooltip("何秒ごとに距離を見るか")]
    public float interval = 0.2f;

    private float _next;

    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + interval;
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null || !p.IsValid() || uiColliders == null) return;
        Vector3 head = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
        float r2 = range * range;
        int nr = ranges != null ? ranges.Length : 0;
        for (int i = 0; i < uiColliders.Length; i++)
        {
            Collider c = uiColliders[i];
            if (c == null) continue;
            float ri = i < nr && ranges[i] > 0f ? ranges[i] * ranges[i] : r2;
            // 当たり判定を切ると bounds が取れなくなるので、UIの位置（中心）で測る
            bool near = (c.transform.position - head).sqrMagnitude < ri;
            if (c.enabled != near) c.enabled = near;
        }
    }
}
