using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 「みんなで」のチルルーム（2026-10-12 てじゃ「みんなでをチルルームにする方向性で固めよう」）。
// 真ん中のキノコを撃つと、今の設定のふわふわが1個出る（FuwaSharedBall を balls 個まで使い回す）。
// スイッチ4つ（レーザーで押すボタン → NextSize 等。2026-10-12 てじゃ「押すもの全部レーザー式に」）で設定を切り替える：サイズ／カラー／出せる上限数／地面に落ちたら消えるか。設定はみんなで共通（同期）。
// 上限を超えたら、いちばん前に出したのをキノコへ戻して使う。
// 同期は Manual（押した人が持ち主になって設定を送る）。エリアの表示切り替えで隠れない FuwaSystem の下に置く
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class FuwaPlaygroundSpawner : UdonSharpBehaviour
{
    public FuwaSharedBall[] balls;
    public FuwaLanguage language;

    [Header("サイズ（玉の大きさ・撃たれた時の効き。名前の最後は「ランダム」＝出すたびに sizeScale から選ぶ。2026-10-14 てじゃ）")]
    public string[] sizeJa = { "ちび", "ふつう", "おおきめ", "でっかい", "ランダム" };   // ボタンのフォント（だるまドロップ）には漢字が無いので、ひらがなで（2026-10-13）
    public string[] sizeEn = { "Tiny", "Normal", "Big", "Giant", "Random" };
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
    public string[] colorHex = { "FFFFFF", "FFB3CF", "9AD4FF", "FFE680", "A8F0A8", "D3B8FF", "FFFFFF" };

    [Header("上限数")]
    public int[] limits = { 1, 3, 6, 10 };

    [Header("スイッチ（レーザーで押すボタン）の文字（なくてもOK）")]
    public TextMeshProUGUI sizeLabel;
    public TextMeshProUGUI colorLabel;
    [Tooltip("カラーのスイッチの、紺の縁どりつきの文字（colorLabel と同じ場所に重ねた別の文字。「しろ」の時だけこちらを出す。Udon からは材質を替えられないので2つ用意）")]
    public TextMeshProUGUI colorLabelOutlined;
    [Tooltip("カラーの名前の文字の色（colorJa と同じ順。白いボタンの上で読めるよう、ふわふわの色より少し濃い）。空なら colorHex")]
    public string[] colorTextHex = { "FFFFFF", "FF8FB8", "5FB4F5", "E8B520", "52C46E", "A57FEA", "" };
    public TextMeshProUGUI limitLabel;
    public TextMeshProUGUI popLabel;
    [Tooltip("スイッチの文字の頭を固定する左の余白（<space=..em> のタグ。サイズ・カラー・かず・おちたら の順、ビルダーが一番長い値で真ん中になるように計算。2026-10-14 てじゃ「切り替えると文字の位置がずれる、左側は固定で値だけ変わるように」）")]
    public string[] padJa = new string[4];
    public string[] padEn = new string[4];

    [UdonSynced] private int _size = 1;
    [UdonSynced] private int _color = 0;
    [UdonSynced] private int _limit = 2;
    [UdonSynced] private bool _popGround = true;
    // おもちゃの解放（コース i をこのインスタンスで誰かがクリアしたら bit i）。保存はしない（2026-10-12 てじゃ「竜巻解放してくるわ、みたいなことが起こる」）
    [UdonSynced] private int _unlocked;

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
        int si = Mathf.Clamp(_size, 0, sizeJa.Length - 1);
        if (si >= sizeScale.Length) si = Random.Range(0, sizeScale.Length);   // ランダム
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
    public void NextSize() { Take(); _size = (_size + 1) % sizeJa.Length; Send(); }
    public void NextColor() { Take(); _color = (_color + 1) % colorJa.Length; Send(); }
    public void NextLimit()
    {
        Take(); _limit = (_limit + 1) % limits.Length; Send();
        TrimTo(Mathf.Clamp(limits[_limit], 1, balls.Length), null);
    }
    public bool PopGround() { return _popGround; }
    public bool Unlocked(int i) { return (_unlocked & (1 << i)) != 0; }
    // FuwaRecords から：自分がコース i をクリアした
    public void Unlock(int i)
    {
        if (i < 0 || i > 30 || Unlocked(i)) return;
        Take(); _unlocked |= 1 << i; Send();
    }
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
        // ボタンはオレンジなので、値は白で太く（色は●で見せる）
        // スイッチは白いボタン＋オレンジの枠、文字は紺。今の値はオレンジ（2026-10-13 てじゃ）
        string V = "<color=#F2786B>"; string VE = "</color>";
        // カラーは色見本の丸をやめて、名前の文字そのものをその色に（2026-10-13 てじゃ「色見本やめて文字の色をかえよう、白は紺のアウトラインつき」）。
        // ランダムは1文字ずつ色を変える
        string cname = en ? colorEn[ci] : colorJa[ci];
        string th = (colorTextHex != null && ci < colorTextHex.Length) ? colorTextHex[ci] : colHex;
        string colored;
        if (th == "")
        {
            colored = "";
            for (int k = 0; k < cname.Length; k++) colored += "<color=#" + colorTextHex[1 + (k % 5)] + ">" + cname.Substring(k, 1) + "</color>";
        }
        else colored = "<color=#" + th + ">" + cname + "</color>";

        if (sizeLabel != null) sizeLabel.text = Pad(0, en) + (en ? "Size: " : "サイズ: ") + V + (en ? sizeEn[si] : sizeJa[si]) + VE;
        string ctext = Pad(1, en) + (en ? "Color: " : "カラー: ") + colored;
        bool outlined = th == "FFFFFF" && colorLabelOutlined != null;
        if (colorLabel != null) { colorLabel.text = ctext; if (colorLabel.gameObject.activeSelf == outlined) colorLabel.gameObject.SetActive(!outlined); }
        if (colorLabelOutlined != null) { colorLabelOutlined.text = ctext; if (colorLabelOutlined.gameObject.activeSelf != outlined) colorLabelOutlined.gameObject.SetActive(outlined); }
        if (limitLabel != null) limitLabel.text = Pad(2, en) + (en ? "Max: " : "かず: ") + V + limits[li] + VE;
        if (popLabel != null) popLabel.text = Pad(3, en) + (en ? ("On landing: " + V + (_popGround ? "Pop" : "Stay") + VE)
                                                 : ("おちたら: " + V + (_popGround ? "きえる" : "きえない") + VE));
    }

    private string Pad(int k, bool en)
    {
        string[] a = en ? padEn : padJa;
        return (a != null && k < a.Length && a[k] != null) ? a[k] : "";
    }
}
