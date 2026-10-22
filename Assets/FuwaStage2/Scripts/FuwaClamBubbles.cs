using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 貝の道（コース2–1「みずのさと」の後半、2026-10-21）。道の両わきの貝が、左右交互にパカッと開いて道を横切る泡を吹く。
// ふわふわが泡に触れると包まれていっしょに流される。一定時間で割れてその場にぽとっと落ちる。
// 割れる前に撃つと、泡ごと撃った向きへ押せる（道の上に戻して割れるのを待てばセーフ）。放っておくと道の外の滝へ運ばれて落下アウト。
// てじゃ案「道の脇に一定間隔で泡を出してくる生物…回避可能な風ギミックに近いイメージ、そのかわり当たると風より厄介」「貝だと出す時に開く」。
// 泡の出るタイミングはサーバーの時刻でそろえる（みんな同じリズムに見える）。包む・押すのは自分のふわふわだけ（同期なし）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaClamBubbles : UdonSharpBehaviour
{
    public FuwaBall ball;

    [Header("貝（clams[i] の +Z が泡を吹く向き。lids[i] はふたの付け根で、ローカル X 軸まわりに開く）")]
    public Transform[] clams;
    public Transform[] lids;
    public Transform[] mouths;
    [Tooltip("貝ごとのずらし（秒）")]
    public float[] phases;
    [Tooltip("泡を吹く間隔（秒）")]
    public float period = 2.8f;
    [Tooltip("ふたが開きはじめてから泡が出るまで（秒）＝予備動作")]
    public float openLead = 0.55f;
    [Tooltip("泡が出てからふたが閉じ終わるまで（秒）")]
    public float closeAfter = 0.45f;
    public float lidOpenAngle = 38f;

    [Header("泡")]
    [Tooltip("泡（球）。貝ごとに bubblesPerClam 個ずつ並べる。ふだんは隠しておく")]
    public Transform[] bubbles;
    public int bubblesPerClam = 3;
    public float bubbleSize = 0.62f;
    [Tooltip("泡の進む速さ(m/s)と上がる速さ(m/s)")]
    public float bubbleSpeed = 1.2f;
    public float bubbleRise = 0.22f;
    [Tooltip("泡の上下のゆれ（m、片側）と速さ（rad/s）。2026-10-22 てじゃ「上下の揺れも追加」（前は 0.05m のふわふわだけ）。包む判定もゆれた位置で")]
    public float bobAmp = 0.35f;
    public float bobSpeed = 2.2f;
    [Tooltip("吹き出し口から横にこれだけ進んだら割れる(m)＝反対側の滝のあたり。包んでいても割れる")]
    public float bubbleRange = 6.2f;

    [Header("包まれた時")]
    [Tooltip("包まれてから割れるまで（秒）")]
    public float holdTime = 3f;
    [Tooltip("包む距離（泡の中心とふわふわの中心）")]
    public float captureRadius = 0.5f;
    [Tooltip("泡の速さに寄せる強さ（1/s）")]
    public float holdResponse = 5f;
    [Tooltip("撃った時に泡が進む速さ(m/s、横)。ためるほど少し速く")]
    public float shotSpeed = 2.4f;
    public float shotSpeedCharged = 3.4f;
    // 撃った時（2026-10-22 てじゃ「撃った方向にまっすぐ気味に、ただし元の泡の移動の慣性も残す」「ぐにゃって変な方向に飛ぶはず、それが狙い」）：
    // 撃った勢い（上下も）と、それまでの泡の動き（慣性）を別々に持って足す。撃った勢いは早く、慣性はゆっくり弱まるので、
    // はじめは撃った向きにまっすぐ → だんだん元の泡の流れの向きへぐにゃっと曲がる
    [Tooltip("撃った勢いが弱まる強さ（1/s）")]
    public float shotDrag = 1.2f;
    [Tooltip("撃った時に、それまでの泡の動きをどれだけ残すか（慣性）")]
    [Range(0f, 1f)] public float shotKeep = 0.7f;   // 0.5→0.7（2026-10-22 てじゃ「もっと元の慣性残して」）
    [Tooltip("撃ったあと、残した慣性が弱まる強さ（1/s）。shotDrag より小さいと、あとから慣性の向きへ曲がる")]
    public float driftDrag = 0.45f;
    [Tooltip("下向きに撃った時の下向きの分をこの倍率に弱める（道にたたきつけてアウトになりにくく）")]
    [Range(0f, 1f)] public float shotDownScale = 0.3f;
    [Tooltip("割れたあと、また包まれるまでの秒数")]
    public float recaptureDelay = 0.8f;

    [Header("音・粒")]
    public ParticleSystem popFx;
    public AudioSource popSound;
    public AudioSource captureSound;
    [Tooltip("貝が泡を吹いた音（貝の位置で鳴らす。なくてもOK）")]
    public AudioSource blowSound;

    private int _n;
    private int[] _lastCycle;
    private Quaternion[] _lidRest;
    private bool[] _alive;
    private Vector3[] _pos, _vel, _from, _dir;
    private float[] _born;
    private int _held = -1;
    private float _heldAt, _noCatchUntil, _shotSeen;
    private bool _heldShot;
    private Vector3 _shotVel;   // 包んでいる泡の、撃った勢いの分（_vel[_held] は慣性＋浮き上がり）

    private void Start()
    {
        _n = clams != null ? clams.Length : 0;
        _lastCycle = new int[_n];
        _lidRest = new Quaternion[_n];
        for (int i = 0; i < _n; i++)
        {
            _lastCycle[i] = int.MinValue;
            if (lids != null && i < lids.Length && lids[i] != null) _lidRest[i] = lids[i].localRotation;
        }
        int nb = bubbles != null ? bubbles.Length : 0;
        _alive = new bool[nb]; _pos = new Vector3[nb]; _vel = new Vector3[nb]; _from = new Vector3[nb]; _dir = new Vector3[nb]; _born = new float[nb];
        for (int b = 0; b < nb; b++) if (bubbles[b] != null) bubbles[b].gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        // ステージを出た・エリアが隠れた：泡は全部消して、包んでいたら放す
        if (_alive == null) return;
        for (int b = 0; b < _alive.Length; b++) { _alive[b] = false; if (bubbles[b] != null) bubbles[b].gameObject.SetActive(false); }
        _held = -1;
    }

    private void Update()
    {
        if (_n == 0) return;
        float now = Time.time;
        double server = Networking.GetServerTimeInSeconds();
        for (int i = 0; i < _n; i++)
        {
            float ph = phases != null && i < phases.Length ? phases[i] : 0f;
            double tt = server + ph;
            int cyc = (int)System.Math.Floor(tt / period);
            float inCyc = (float)(tt - (double)cyc * period);   // 0〜period。openLead の所で泡が出る
            // ふた：openLead 前から開いて、泡が出たあと closeAfter で閉じる
            if (lids != null && i < lids.Length && lids[i] != null)
            {
                float open;
                if (inCyc < openLead) open = Smooth(inCyc / openLead);
                else if (inCyc < openLead + closeAfter) open = 1f - Smooth((inCyc - openLead) / closeAfter);
                else open = 0f;
                lids[i].localRotation = _lidRest[i] * Quaternion.Euler(-lidOpenAngle * open, 0f, 0f);
            }
            if (inCyc >= openLead && _lastCycle[i] != cyc)
            {
                bool first = _lastCycle[i] == int.MinValue;
                _lastCycle[i] = cyc;
                // 入った時（最初の1回）は、もう出ていたはずの泡は出さない（途中から急に出てくると変なので）
                if (!first || inCyc < openLead + 0.1f) Spawn(i);
            }
        }

        float dt = Time.deltaTime;
        int nb = _alive.Length;
        bool ballOk = ball != null && !ball.IsWaitingOrHidden() && ball.gameObject.activeInHierarchy;
        Vector3 bp = ballOk ? ball.transform.position : Vector3.zero;
        for (int b = 0; b < nb; b++)
        {
            if (!_alive[b] || b == _held) continue;
            _pos[b] += _vel[b] * dt;
            if (Vector3.Dot(_pos[b] - _from[b], _dir[b]) > bubbleRange) { Pop(b, false); continue; }
            float w = now - _born[b];
            float s = bubbleSize * Mathf.Min(1f, 0.35f + w * 2.2f);   // 吹き出した直後はふくらむ
            // 出た瞬間は 0 から（ゆれ始めでパッと動かないように）。貝ごとに向きを逆にする
            float bob = bobAmp * Mathf.Sin(w * bobSpeed) * Mathf.Min(1f, w * 1.5f) * ((b / Mathf.Max(1, bubblesPerClam)) % 2 == 0 ? 1f : -1f);
            bubbles[b].position = _pos[b] + Vector3.up * (bob + 0.05f * Mathf.Sin(w * 3.1f + b));
            bubbles[b].localScale = new Vector3(s * (1f + 0.04f * Mathf.Sin(w * 7f + b)), s, s * (1f - 0.04f * Mathf.Sin(w * 7f + b)));
            if (ballOk && _held < 0 && now >= _noCatchUntil && w > 0.25f && (bubbles[b].position - bp).magnitude < captureRadius) Capture(b);
        }

        if (_held >= 0)
        {
            int h = _held;
            if (!ballOk) { Pop(h, true); return; }
            // 割れる前に撃った：泡ごと撃った向きへ
            float age = ball.LastShotAge();
            float shotAt = now - age;
            if (age < 0.15f && shotAt > _heldAt + 0.05f && Mathf.Abs(shotAt - _shotSeen) > 0.01f)
            {
                _shotSeen = shotAt;
                // 撃った向きにまっすぐ（上下も）＋それまでの泡の速さを shotKeep だけ残す。前は水平だけで、上に撃っても高さが変わらなかった。下向きは弱める
                Vector3 d = ball.LastShotDir();
                if (d.sqrMagnitude > 1e-4f)
                {
                    d.Normalize();
                    if (d.y < 0f) d.y *= shotDownScale;
                    float sp = Mathf.Lerp(shotSpeed, shotSpeedCharged, Mathf.Clamp01(ball.LastShotCharge()));
                    Vector3 rise0 = Vector3.up * bubbleRise;
                    _vel[h] = rise0 + (_vel[h] + _shotVel - rise0) * shotKeep;   // 前に撃った勢いも慣性に入れる
                    _shotVel = d * sp;
                    _heldShot = true;
                }
            }
            if (_heldShot)
            {
                // 撃った勢いは早く、慣性はゆっくり弱める（慣性はふつうの浮き上がり＝真上に bubbleRise へ戻る）
                _shotVel *= Mathf.Exp(-shotDrag * Time.deltaTime);
                Vector3 rise = Vector3.up * bubbleRise;
                _vel[h] = rise + (_vel[h] - rise) * Mathf.Exp(-driftDrag * Time.deltaTime);
            }
            Vector3 hv = _vel[h] + _shotVel;
            Vector3 v = ball.GetVelocity();
            // 空気の抵抗（drag）のぶんも足して、泡の速さにちゃんと乗せる（足さないと6割くらいの速さにしかならなかった）
            float dr = ball.GetDrag();
            float ay = ball.GetFallAcceleration() + (hv.y - v.y) * holdResponse + hv.y * dr;
            Vector3 ah = new Vector3(hv.x - v.x, 0f, hv.z - v.z) * holdResponse + new Vector3(hv.x, 0f, hv.z) * dr;
            ball.AddWater(new Vector3(ah.x, ay, ah.z));
            float hw = now - _heldAt;
            float s = bubbleSize * (1f + 0.05f * Mathf.Sin(hw * 9f));
            bubbles[h].position = ball.transform.position;
            bubbles[h].localScale = new Vector3(s * (1f + 0.03f * Mathf.Sin(hw * 7f)), s, s * (1f - 0.03f * Mathf.Sin(hw * 7f)));
            // 道の外まで運ばれた（吹いた向きに bubbleRange 以上＝反対側の滝、または貝の後ろへ押し戻しすぎ）：泡が割れて落ちる
            float across = Vector3.Dot(ball.transform.position - _from[h], _dir[h]);
            if (hw > holdTime || across > bubbleRange || across < -1.2f) Pop(h, true);
        }
    }

    private void Spawn(int clam)
    {
        if (mouths == null || clam >= mouths.Length || mouths[clam] == null) return;
        int k0 = clam * bubblesPerClam, slot = -1;
        for (int k = 0; k < bubblesPerClam; k++) { int b = k0 + k; if (b < _alive.Length && !_alive[b]) { slot = b; break; } }
        if (slot < 0) return;
        Vector3 dir = clams[clam].forward; dir.y = 0f; dir.Normalize();
        _alive[slot] = true;
        _pos[slot] = mouths[clam].position; _from[slot] = _pos[slot]; _dir[slot] = dir;
        _vel[slot] = dir * bubbleSpeed + Vector3.up * bubbleRise;
        _born[slot] = Time.time;
        bubbles[slot].position = _pos[slot]; bubbles[slot].localScale = Vector3.one * (bubbleSize * 0.35f);
        bubbles[slot].gameObject.SetActive(true);
        if (blowSound != null) { blowSound.transform.position = _pos[slot]; blowSound.pitch = Random.Range(0.92f, 1.08f); blowSound.Play(); }
    }

    private void Capture(int b)
    {
        _held = b; _heldAt = Time.time; _heldShot = false; _shotVel = Vector3.zero;
        // 包んだ時のふわふわの勢いは少し残す（泡の向きとまぜる）
        Vector3 v = ball.GetVelocity(); v.y = 0f;
        Vector3 bv = _vel[b]; bv.y = 0f;
        _vel[b] = Vector3.Lerp(bv, v, 0.15f) + Vector3.up * bubbleRise * 0.5f;
        if (captureSound != null) { captureSound.transform.position = ball.transform.position; captureSound.pitch = Random.Range(0.95f, 1.08f); captureSound.Play(); }
    }

    private void Pop(int b, bool wasHeld)
    {
        _alive[b] = false;
        if (wasHeld || b == _held) { _held = -1; _noCatchUntil = Time.time + recaptureDelay; }
        Vector3 at = bubbles[b].position;
        bubbles[b].gameObject.SetActive(false);
        if (popFx != null) { popFx.transform.position = at; popFx.Emit(wasHeld ? 18 : 10); }
        // 音はふわふわが入っていた泡だけ（空の泡はだまって消える。2026-10-22 てじゃ「ふわふわが入ってない泡が割れる音もいらない」）
        if (wasHeld && popSound != null) { popSound.transform.position = at; popSound.pitch = Random.Range(0.95f, 1.12f); popSound.Play(); }
    }

    private float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
}
