using UdonSharp;
using UnityEngine;

// 2–4 の最後の坂（2026-11-17 てじゃ「最後の坂になんかほしい、上下させる」→ 波の階段は「相性わるい」→ B「坂ごとシーソー」「鉄骨トラスつきのまま、上を縞鋼板」）
//  坂（板＋下のトラス）を、まん中の横の軸（この transform の local X）でゆっくり上下にかたむける。角度 = amp × sin(2π t / period)（自分の画面だけ、同期なし）
//  板の両はしは道とすき間をあけてあり、まん中（0°）の時だけ両はしの道とそろう
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSeesaw : UdonSharpBehaviour
{
    public float amp = 7f, period = 6f;
    public Quaternion baseRot;

    private void Update()
    {
        float a = amp * Mathf.Sin(Time.time * 6.2831853f / period);
        transform.localRotation = baseRot * Quaternion.Euler(a, 0f, 0f);
    }
}
