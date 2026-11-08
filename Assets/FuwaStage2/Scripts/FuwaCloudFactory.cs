using UdonSharp;
using UnityEngine;

// 2–4 はぐるまのとう：雲づくり工場の機械 1台（2026-11-08 てじゃ「歯車のしごとやろか」→ A 雲づくり工場）
// ・しかけのたての軸についた大きい歯車（bigGear、軸と一緒に回る）→ となりの小さい歯車（pinion、ここで回す）で速める
//   角度は frame（動かない、上＝軸）の中の 2D（x→z 反時計回り、度）。pinion の角度 = ratio × 大きい歯車の角度 + phase
// ・mode 0 ふいご：pinion の local (pinR, pinY, 0) のピン → 棒 → ふいごの板（frame の中の axisDir の向きに動く）。
//   板と動かない板の間をじゃばら（rings）でつなぐ。いちばん押しこんだ所で煙突から雲（puffs）
// ・mode 1 臼：pinion についたらせんのカム（1周で steps 回、lift だけ上がって落ちる）で臼（hammer）を持ち上げて落とす。落ちた所で雲
// ・mode 2 糸巻き：pinion が回るだけ（糸巻きは pinion の子）
// 自分の画面だけ（同期なし）。回り方は軸を回すスクリプト（FuwaClockwork・FuwaGearRide）の結果を読むだけ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaCloudFactory : UdonSharpBehaviour
{
    public int mode = 0;
    public Transform frame, bigGear, pinion;
    [Tooltip("整数にする（大きい歯車の角度が巻き戻った時にずれないように）")]
    public float ratio = -8f, phase = 0f;

    [Header("ふいご")]
    public float pinR = 0.35f, pinY = 0.1f, rodLen = 1.1f;
    [Tooltip("ふいごごとの、板が動く向き（frame の中、長さ 1）")]
    public Vector3[] axisDir;
    [Tooltip("動く板（ピンの所が原点）")]
    public Transform[] plates;
    public Transform[] rods;
    [Tooltip("動かない板の、pinion の軸からの距離（ふいごごと）")]
    public float[] fixedDist;
    [Tooltip("じゃばらの輪（ringOwner のふいごの、動く板と動かない板の間に等間隔）")]
    public Transform[] rings;
    public int[] ringOwner;
    public float[] ringT;
    [Tooltip("中の革（x の長さを板の間にのばす。長さ 1 の箱）")]
    public Transform[] skins;
    public ParticleSystem[] puffs;
    public int puffCount = 6;
    [Tooltip("いちばん押しこんだ時にざらめを送るパイプ（ふいご・ポンプごと、2026-11-08 色ざらめパイプ）")]
    public FuwaSugarPipe[] pipes;

    [Header("臼")]
    public Transform hammer;
    public float hammerBaseY = 0f, lift = 0.8f;
    public int steps = 2;
    public float camSign = 1f;
    public ParticleSystem hammerPuff;

    private float[] _lastD; private float[] _lastDir;
    private float _lastU = -1f;

    private void Start()
    {
        int n = axisDir != null ? axisDir.Length : 0;
        _lastD = new float[n]; _lastDir = new float[n];
    }

    private float Angle2D(Transform t)
    {
        Vector3 x = Quaternion.Inverse(frame.rotation) * (t.rotation * Vector3.right);
        return Mathf.Atan2(x.z, x.x) * Mathf.Rad2Deg;
    }

    private void Update()
    {
        if (frame == null || bigGear == null || pinion == null) return;
        // 大きい歯車の角度（−180〜180 で巻き戻る）。ratio は整数（歯数比 64:8 など）なので、巻き戻っても pinion の角度は 360 の倍数ずれるだけ
        float th = Angle2D(bigGear);
        float s = Mathf.Repeat(ratio * th + phase, 360f);
        pinion.localRotation = Quaternion.Euler(0f, -s, 0f);

        if (mode == 0) Bellows();
        else if (mode == 1) Hammer(s);
    }

    private void Bellows()
    {
        if (axisDir == null) return;
        float sc = frame.lossyScale.x;   // 機械ごと大きくしている時（長さは frame の中の値）
        float L = rodLen * sc;
        Vector3 pin = pinion.TransformPoint(new Vector3(pinR, pinY, 0f));
        Vector3 c = pinion.TransformPoint(new Vector3(0f, pinY, 0f));
        for (int k = 0; k < axisDir.Length; k++)
        {
            Vector3 ax = frame.TransformDirection(axisDir[k]);
            Vector3 d = pin - c;
            float along = Vector3.Dot(d, ax);
            Vector3 side = d - ax * along;
            float dist = along + Mathf.Sqrt(Mathf.Max(0f, L * L - side.sqrMagnitude));
            Vector3 sp = c + ax * dist;
            if (plates != null && k < plates.Length && plates[k] != null) plates[k].position = sp;
            if (rods != null && k < rods.Length && rods[k] != null)
            {
                rods[k].position = (pin + sp) * 0.5f;
                rods[k].rotation = Quaternion.LookRotation((sp - pin).normalized, frame.up);
            }
            // じゃばら：動く板から動かない板まで
            float fd = fixedDist[k] * sc;
            if (rings != null)
                for (int i = 0; i < rings.Length; i++)
                    if (ringOwner[i] == k && rings[i] != null) rings[i].position = c + ax * Mathf.Lerp(dist, fd, ringT[i]);
            if (skins != null && k < skins.Length && skins[k] != null)
            {
                skins[k].position = c + ax * ((dist + fd) * 0.5f);
                Vector3 ls = skins[k].localScale; ls.x = Mathf.Max(0.01f, (fd - dist) / sc); skins[k].localScale = ls;
            }
            // いちばん押しこんだ所（板が動かない板にいちばん近づいて戻りはじめた）で雲
            float dir = dist - _lastD[k];
            if (_lastDir[k] > 0f && dir < 0f)
            {
                if (puffs != null && k < puffs.Length && puffs[k] != null) puffs[k].Emit(puffCount);
                if (pipes != null && k < pipes.Length && pipes[k] != null) pipes[k].Send();
            }
            if (Mathf.Abs(dir) > 1e-5f) _lastDir[k] = dir;
            _lastD[k] = dist;
        }
    }

    private void Hammer(float s)
    {
        if (hammer == null) return;
        float u = Mathf.Repeat(camSign * s * steps / 360f, 1f);
        Vector3 p = hammer.localPosition; p.y = hammerBaseY + lift * u; hammer.localPosition = p;
        if (_lastU >= 0f && u < _lastU - 0.5f && hammerPuff != null) hammerPuff.Emit(puffCount);   // 落ちた
        _lastU = u;
    }
}
