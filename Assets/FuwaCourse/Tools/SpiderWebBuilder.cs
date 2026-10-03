using UnityEngine;

// クモの巣を作るための設定（エディタ専用。アップロード時には外れる）。
// 巣の当たり判定（FuwaHazard）が付いている物体に付けると、インスペクターで値を変えるたびに
// 巣の見た目（FuwaCourse/SpiderWeb シェーダーのマテリアル）・左右の木や岩への支えの糸・
// 見た目の形に合わせた当たり判定 を作り直す。作る処理は Editor/SpiderWebBuild.cs。
// 座標はすべてメートルで、この物体の中心・向きが基準（Z軸が巣の面に垂直）。
[DisallowMultipleComponent]
public class SpiderWebBuilder : MonoBehaviour, VRC.SDKBase.IEditorOnly
{
    public enum AnchorType { None, Tree, Rock }

    [System.Serializable]
    public class Side
    {
        [Tooltip("この側を何に留めるか")]
        public AnchorType anchor = AnchorType.Tree;

        [Header("木")]
        [Tooltip("巣のフチから幹までの距離(m)")]
        public float treeGap = 0.5f;
        [Tooltip("木の大きさの倍率（1＝巣の高さに合わせた標準）")]
        public float treeScale = 1f;
        [Tooltip("木の高さの足し引き(m)")]
        public float treeExtraHeight = 0f;
        [Tooltip("葉っぱの大きさの倍率（幹はそのまま）")]
        public float crownScale = 1f;

        [Header("岩")]
        [Tooltip("巣のフチから、糸が岩に刺さる所までの距離(m)")]
        public float rockDepth = 0.12f;

        [Header("糸の本数")]
        [Tooltip("巣の上のほう → 枝")]
        [Range(0, 5)] public int upperThreads = 2;
        [Tooltip("巣の横 → 幹（岩の場合は岩）")]
        [Range(0, 8)] public int sideThreads = 2;
        [Tooltip("巣の下の角 → 幹の下のほう")]
        [Range(0, 3)] public int bottomThreads = 2;
    }

    [Header("巣の形")]
    [Tooltip("巣の板の大きさ（幅, 高さ）m")]
    public Vector2 size = new Vector2(9.6f, 6f);
    [Tooltip("板の中心から見た穴の中心（x, y）m")]
    public Vector2 holeCenter = new Vector2(0f, -1.3f);
    [Tooltip("穴の半径 m（FuwaWebHole があればそちらの値を使う）")]
    public float holeRadius = 0.62f;
    [Tooltip("放射の糸の本数")]
    [Range(8, 48)] public int spokes = 28;
    [Tooltip("形のばらつきの種。変えると外形や糸の揺らぎが変わる")]
    public float seed = 11f;
    [Tooltip("外周の糸の位置：板のフチまでの何割（小さい側）")]
    [Range(0.3f, 1f)] public float outerMin = 0.84f;
    [Tooltip("外周の糸の位置：板のフチまでの何割（大きい側）")]
    [Range(0.3f, 1f)] public float outerMax = 0.98f;
    [Tooltip("角の丸さ（大きいほど四角に近い）")]
    public float cornerRound = 1.1f;
    [Tooltip("外形を、まっすぐ張った枠の糸の多角形にする（角から支えへ係留の糸を張る）。小さい巣向け")]
    public bool polygonFrame = false;
    [Tooltip("枠の角の数")]
    [Range(3, 8)] public int frameCorners = 5;
    [Tooltip("木のない側の下の角を地面へつなぐ時、地面を探す深さ m")]
    public float groundSearch = 6f;
    [Tooltip("最初の輪までの間隔 m")]
    public float ringStart = 0.12f;
    [Tooltip("輪の間隔の広がり方")]
    public float ringGrow = 0.17f;
    [Tooltip("糸の太さ m")]
    public float lineWidth = 0.0165f;
    public Color color = new Color(0.42f, 0.40f, 0.52f, 0.94f);

    [Header("つなぐ物")]
    [Tooltip("巣を描く板（Quad）")]
    public Renderer webRenderer;
    [Tooltip("穴の中心に置く物（FuwaHazard の holeCenter・金の輪）。なければ空でOK")]
    public Transform hole;

    [Header("当たり判定")]
    [Tooltip("見た目の外形に合わせた当たり判定にする（この物体の BoxCollider は MeshCollider に置き換わる）")]
    public bool fitCollider = true;
    [Tooltip("当たり判定の厚み m")]
    public float colliderDepth = 0.4f;

    [Header("支え")]
    public Side left = new Side();
    public Side right = new Side { anchor = AnchorType.Tree };
    [Tooltip("木が両側なら てっぺん中央→左右の枝先、片側なら 枝を巣の真上まで伸ばして まっすぐ吊る")]
    public bool topThread = true;
    [Tooltip("糸の垂れ具合（糸の長さに対する割合）")]
    [Range(0f, 0.2f)] public float sag = 0.08f;
    [Tooltip("木の根元に地面がない時は、小さい浮島を作る")]
    public bool autoIsland = true;
    [Tooltip("枯れ木にする（葉っぱなし、てっぺんに細い枝を足す、灰色っぽい幹）")]
    public bool bareTree = false;

    [HideInInspector] public string assetId = "";
}
