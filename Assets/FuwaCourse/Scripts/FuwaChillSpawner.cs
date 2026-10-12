using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 「みんなで」のチルルーム（2026-10-12 てじゃ「みんなでをチルルームにする方向性で固めよう」）。
// 真ん中のキノコを撃つと、今の設定のふわふわが1個出る（FuwaSharedBall を balls 個まで使い回す）。
// スイッチ4つ（FuwaChillButton）で設定を切り替える：サイズ／カラー／出せる上限数／地面に落ちたら消えるか。設定はみんなで共通（同期）。
// 上限を超えたら、いちばん前に出したのをキノコへ戻して使う。
// 同期は Manual（押した人が持ち主になって設定を送る）。エリアの表示切り替えで隠れない FuwaSystem の下に置く
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaChillSpawner : UdonSharpBehaviour
{
    public FuwaSharedBall[] balls;
    public FuwaLanguage language;

    [Header("サイズ（玉の大きさ・撃たれた時の効き）")]
    public string[] sizeJa = { "ちび", "ふつう", "おおきめ", "巨大" };
    public string[] sizeEn = { "Tiny", "Normal", "Big", "Giant" };
    public float[] sizeScale = { 0.2f, 0.3f, 0.55f, 1.4f };
    public float[] sizeShot = { 1.3f, 1f, 0.6f, 0.3f };

    [Header("カラー（最後は「ランダム」）")]
    public string[] colorJa = { "しろ", "ピンク", "みずいろ", "きいろ", "みどり", "むらさき", "ランダム" };
    public string[] colorEn = { "White", "Pink", "Sky", "Yellow", "Green", "Purple", "Random" };
    public Color[] colors =
    {
        new Color(0.98f, 0.95f, 0.93f), new Color(1f, 0.62f, 0.78f), new Color(0.55f, 0.82f, 1f),
        new Color(1f, 0.9f, 0.45f), new Color(0.6f, 0.92f, 0.6f), new Color(0.78f, 0.62f, 1f),
    };
    [Tooltip("スイッチの文字の色（colors を少し濃くしたもの。最後はランダム）")]
    public string[] colorHex = { "9C8F86", "E0507F", "3A8FD6", "C79A10", "3E9E48", "8A5CD6", "FF6B5C" };

    [Header("上限数")]
    public int[] limits = { 1, 3, 6, 10 };

    [Header("スイッチの文字（なくてもOK）")]
    public TextMeshPro sizeLabel;
    public TextMeshPro colorLabel;
    public TextMeshPro limitLabel;
    public TextMeshPro popLabel;

    [UdonSynced] private int _size = 1;
    [UdonSynced] private int _color = 0;
    [UdonSynced] private int _limit = 2;
    [UdonSynced] private bool _popGround = true;

    private bool _shownEn;
    private float _nextSpawn;
    private bool _dirty = true;

    private void Start() { _dirty = true; }

    // キノコ（FuwaStartMushroom）から：撃った人の画面で呼ばれる
    public void Spawn(Vector3 shotDir)
    {
        if (balls == null || balls.Length == 0 || Time.time < _nextSpawn) return;
        _nextSpawn = Time.time + 0.35f;   // 連打で一気に出ないように
        int max = Mathf.Clamp(limits[Mathf.Clamp(_limit, 0, limits.Length - 1)], 1, balls.Length);
        // 使う玉：上限の数までの中で出ていない玉。なければ、いちばん前に出した玉を使い回す
        FuwaSharedBall pick = null;
        int active = 0;
        FuwaSharedBall oldest = null; double oldestT = double.MaxValue;
        for (int i = 0; i < balls.Length; i++)
        {
            FuwaSharedBall b = balls[i];
            if (b == null) continue;
            if (b.IsIdle()) { if (pick == null && i < max) pick = b; continue; }
            active++;
            if (b.SpawnTime() < oldestT) { oldestT = b.SpawnTime(); oldest = b; }
        }
        if (active >= max || pick == null) pick = oldest;
        if (pick == null) return;
        // 上限を減らした後で出ている数が多すぎる時は、古いのからキノコへ戻す
        TrimTo(max - 1, pick);

        int ci = Mathf.Clamp(_color, 0, colorJa.Length - 1);
        Color c = ci >= colors.Length ? colors[Random.Range(0, colors.Length)] : colors[ci];
        int si = Mathf.Clamp(_size, 0, sizeScale.Length - 1);
        Networking.SetOwner(Networking.LocalPlayer, pick.gameObject);
        pick.SpawnVariant(shotDir, sizeScale[si], c, sizeShot[si]);
    }

    // 出ている玉を keep 個まで減らす（古いのから戻す。except は除く）
    private void TrimTo(int keep, FuwaSharedBall except)
    {
        for (int guard = 0; guard < balls.Length; guard++)
        {
            int active = 0; FuwaSharedBall oldest = null; double oldestT = double.MaxValue;
            for (int i = 0; i < balls.Length; i++)
            {
                FuwaSharedBall b = balls[i];
                if (b == null || b == except || b.IsIdle()) continue;
                active++;
                if (b.SpawnTime() < oldestT) { oldestT = b.SpawnTime(); oldest = b; }
            }
            if (active <= keep || oldest == null) return;
            Networking.SetOwner(Networking.LocalPlayer, oldest.gameObject);
            oldest.Recall();
        }
    }

    // スイッチ（FuwaChillButton）から：押した人の画面で呼ばれる
    public void NextSize() { Take(); _size = (_size + 1) % sizeScale.Length; Send(); }
    public void NextColor() { Take(); _color = (_color + 1) % colorJa.Length; Send(); }
    public void NextLimit()
    {
        Take(); _limit = (_limit + 1) % limits.Length; Send();
        TrimTo(Mathf.Clamp(limits[_limit], 1, balls.Length), null);
    }
    public bool PopGround() { return _popGround; }
    public void TogglePop() { Take(); _popGround = !_popGround; Send(); }

    private void Take() { if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject); }
    private void Send() { RequestSerialization(); _dirty = true; }

    public override void OnDeserialization() { _dirty = true; }

    private void Update()
    {
        bool en = language != null && language.IsEnglish();
        if (!_dirty && en == _shownEn) return;
        _dirty = false; _shownEn = en;
        int si = Mathf.Clamp(_size, 0, sizeJa.Length - 1), ci = Mathf.Clamp(_color, 0, colorJa.Length - 1), li = Mathf.Clamp(_limit, 0, limits.Length - 1);
        string colHex = colorHex[Mathf.Clamp(ci, 0, colorHex.Length - 1)];
        if (sizeLabel != null) sizeLabel.text = (en ? "Size: " : "サイズ：") + "<color=#FF6B5C>" + (en ? sizeEn[si] : sizeJa[si]) + "</color>";
        if (colorLabel != null) colorLabel.text = (en ? "Color: " : "カラー：") + "<color=#" + colHex + ">" + (en ? colorEn[ci] : colorJa[ci]) + "</color>";
        if (limitLabel != null) limitLabel.text = (en ? "Max: " : "上限：") + "<color=#FF6B5C>" + limits[li] + "</color>";
        if (popLabel != null) popLabel.text = en ? ("On landing: <color=#FF6B5C>" + (_popGround ? "Pop" : "Stay") + "</color>")
                                                 : ("落ちたら：<color=#FF6B5C>" + (_popGround ? "消える" : "消えない") + "</color>");
    }
}
