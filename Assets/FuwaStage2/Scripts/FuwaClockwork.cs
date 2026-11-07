using UdonSharp;
using UnityEngine;

// 2–4 はぐるまのとう：時計塔が動かす、道ごとの自動の歯車じかけ（2026-11-07 てじゃ「各道に、なんかしら自動の歯車の仕掛けを追加しよう」）
// ・塔から空中の軸がのびて、かさ歯車でたての軸 → 歯車 → クランク → 押し出し壁・回るアーム・シャッター
// ・元の角度 a = speed × 時間（Time.time。だれの画面でもいつも同じリズムになるわけではないが、自分の画面では一定。同期なし）
//   歯車 i の角度（local Y まわり、2D 反時計回り、度）= ratio[i] × a + phase[i]。Unity の Y 回転はマイナス（ほかの歯車と同じ）
// ・クランクとスライダー：クランクの歯車 sliderGear[k] の local (pinR, 0, 0) がピン。スライダー（ピンの所が原点）は
//   クランクの中心から axis の向きの線の上で、ピンから rodLen の所。棒（rod）はピンとスライダーの間（local Z が棒の向き、長さ 1 の箱を rodLen にのばしておく）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaClockwork : UdonSharpBehaviour
{
    public float speed = 10f;
    public float wrap = 36000f;
    public Transform[] gears;
    public float[] ratio, phase;

    public int[] sliderGear;
    public Transform[] sliders;
    public Vector3[] sliderAxis;    // クランクの歯車の親の中での向き
    public float[] pinR, pinY, rodLen;   // ピンはクランクの歯車の local (pinR, pinY, 0)、線の中心は local (0, pinY, 0)
    public Transform[] rods;

    private void Update()
    {
        float a = Mathf.Repeat(speed * Time.time, wrap);
        if (gears != null && ratio != null && phase != null)
            for (int i = 0; i < gears.Length; i++)
                if (gears[i] != null) gears[i].localRotation = Quaternion.Euler(0f, -Mathf.Repeat(ratio[i] * a + phase[i], 360f), 0f);
        if (sliders == null || sliderGear == null) return;
        for (int k = 0; k < sliders.Length; k++)
        {
            Transform s = sliders[k]; if (s == null) continue;
            Transform g = gears[sliderGear[k]]; if (g == null) continue;
            float py = pinY != null && k < pinY.Length ? pinY[k] : 0f;
            Vector3 c = g.TransformPoint(new Vector3(0f, py, 0f));
            Vector3 pin = g.TransformPoint(new Vector3(pinR[k], py, 0f));
            Vector3 ax = g.parent.TransformDirection(sliderAxis[k]).normalized;
            Vector3 d = pin - c;
            float along = Vector3.Dot(d, ax);
            Vector3 side = d - ax * along;
            float L = rodLen[k];
            float sq = L * L - side.sqrMagnitude;
            Vector3 sp = c + ax * (along + Mathf.Sqrt(Mathf.Max(0f, sq)));
            s.position = sp;
            if (rods != null && k < rods.Length && rods[k] != null)
            {
                rods[k].position = (pin + sp) * 0.5f;
                rods[k].rotation = Quaternion.LookRotation((sp - pin).normalized, g.up);
            }
        }
    }
}
