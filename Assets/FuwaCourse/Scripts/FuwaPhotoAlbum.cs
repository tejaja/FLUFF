using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

// 名場面アルバム（自分にだけ、同期なし）。2026-10-11 てじゃ「遊んでるところのスクショを自動でいい感じに撮ってロビーで見れるやつ」
// ・各コースにフォトポイント（FuwaPhotoPoint）が3か所。自分のふわふわが通ったら、見えないカメラ（shotCam）で2枚撮る → 1コースで6枚
//   6枚は「遠くから2・近くから2・撃った瞬間2」（てじゃ：追いかけてる絵ばかりだと単調）。角度もポイントごとに思いっきり変える
//     ポイント1：遠（横の高い所から景色ごと）　＋ 撃った瞬間（斜め前から、銃口と風がこっちへ）
//     ポイント2：近（ふわふわの斜め前の低め）　＋ 撃った瞬間（真横から、風がまっすぐ飛んでいく）
//     ポイント3：遠（後ろの高い所から追いかけ）＋ 近（ふわふわを上から見下ろし）
//   1枚目は通ってすぐ。「撃った瞬間」は通った後に次に撃った時（銃 FuwaBlower から OnFire）。3秒撃たなければ近くからの1枚にする
//   どの角度も地面や物で隠れる時は、左右を入れ替え→予備の角度→ポイントに置いたカメラ（camPose）の順に試す
//   （最初はゴールの瞬間→全コース同じ絵。次にポイントの固定カメラ→遠くて迫力がない。てじゃ）
// ・ゴールした時は、ゴールの奥の空に同じ6枚の板（goalBoard）を出す（結果発表。板がふわっと出て、写真が1枚ずつぽんぽん出る）。
//   次の挑戦を始めた時・コースを離れた時に消える
// ・ボードは「1コース・1回の挑戦」ぶんの6枚。別のコースで（同じコースのやり直しでも）1枚撮ったら、消して並べ直す
//   並びは通った順（ポイント1のふわふわ・自分、ポイント2の…）。見出しにコース名、写真の下にポイント名
// ・VRChatの手持ちカメラを額縁に近づけると、VRChatカメラの画面だけその写真に差し替える（シャッターで自分のアルバムに保存できる）
//   （Fuwa/PhotoAlbumFeed の板をレンズの前に置く。FuwaFollowCam と同じやり方）
// 画像はネットで送れないので、撮るのも見るのも自分の画面だけ。ワールドを出たら消える
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPhotoAlbum : UdonSharpBehaviour
{
    [Header("撮影")]
    [Tooltip("撮る用のカメラ（ふだんは止めておく。撮る時だけ Render する）")]
    public Camera shotCam;
    [Tooltip("フォトポイントに入ってから「ふわふわ主役」を撮るまでの秒数")]
    public float shotDelay = 0.2f;
    [Tooltip("2枚目が「近く」の時、フォトポイントに入ってから撮るまでの秒数")]
    public float secondShotDelay = 1.0f;
    [Tooltip("「撃った瞬間」を待つ秒数（これを過ぎたら近くからの1枚にする）")]
    public float fireWait = 3f;
    [Tooltip("撃ってから撮るまでの秒数（風の輪が出たところ）")]
    public float fireShotDelay = 0.15f;
    [Tooltip("近くから撮る時の、ふわふわからカメラまでの距離(m)")]
    public float closeDistance = 1.9f;
    [Tooltip("遠くから撮る時の、ふわふわからカメラまでの距離(m)")]
    public float farDistance = 6.5f;
    [Tooltip("カメラを置けるか調べる時に、ぶつかったらダメな物")]
    public LayerMask obstacleMask = (1 << 0) | (1 << 11);
    [Tooltip("写真の大きさ（横）。縦は16:9")]
    public int photoWidth = 1920;   // 2026-10-12 1280→1920（PCのみ。Quest対応する時は Android ビルドで下げる）
    [Tooltip("撮る時に隠すもの（HUDの文字など）")]
    public Renderer[] hideWhileShooting;

    [Header("飾る所")]
    [Tooltip("額縁の写真の面（通った順。ポイント1のふわふわ・自分、ポイント2の…）")]
    public Renderer[] frames;
    [Tooltip("frames と同じ順で、下の文字（ポイント名）")]
    public TextMeshPro[] captions;
    [Tooltip("まだ写真がない額縁に出す物。写真が入ると消す")]
    public GameObject[] emptyMarks;
    [Tooltip("ボードの見出し（言語で切り替える。後ろにコース名がつく）")]
    public TextMeshPro titleText;
    [TextArea] public string titleJa = "かってにスクショ";   // 2026-10-11 てじゃ「ギャグっぽく、かってにスクショ」（元「さっきの名場面」）
    [TextArea] public string titleEn = "Sneaky Shots";

    [Header("ゴールの結果発表")]
    [Tooltip("ゴールの奥に出す写真の板（ふだんは非表示）")]
    public Transform goalBoard;
    [Tooltip("goalBoard の写真の面（frames と同じ順）")]
    public Renderer[] goalFrames;
    [Tooltip("goalBoard の、まだ写真がない所に出す物")]
    public GameObject[] goalEmptyMarks;
    [Tooltip("ゴールの中心から、奥（戻る地点と反対側）へどれだけ離すか(m)")]
    public float goalBoardBack = 5.5f;
    [Tooltip("ゴールの中心からの高さ(m)")]
    public float goalBoardUp = 4.3f;
    [Tooltip("板が出てくるまでの秒数（ゴールしてから）")]
    public float goalBoardDelay = 1.6f;
    [Tooltip("自分がこれより離れたら消す(m)")]
    public float goalBoardHideDistance = 60f;

    [Header("コース名（FuwaRecords と同じ並び）")]
    public Transform[] courseStarts;
    public string[] courseNamesJa;
    public string[] courseNamesEn;
    public FuwaLanguage language;

    [Header("VRChatカメラへの差し替え")]
    [Tooltip("VRChatカメラのレンズの前に置く板（Fuwa/PhotoAlbumFeed のマテリアル）")]
    public Transform feed;
    public Renderer feedRenderer;
    public float feedDistance = 0.3f;
    [Tooltip("レンズが額縁からこの距離(m)以内で、額縁の方を向いていたら差し替える")]
    public float feedRange = 0.9f;
    [Tooltip("レンズが写真の面より奥へこの距離(m)まで入っても差し替えたまま（ぶつけるくらい近づけた時）")]
    public float feedBehind = 0.3f;
    [Tooltip("写真の外側にこの幅(m)はみ出していても当たりにする")]
    public float feedMargin = 0.15f;

    private RenderTexture[] _photos;
    private RenderTexture _renderRT;   // 描く用（奥行きつき、共用の1枚）
    private bool[] _has;
    private FuwaPhotoPoint[] _pointOf;
    private int _boardCourse = -1;
    private float _boardRun = -1f;

    private FuwaBall _ball;
    private Vector3 _goalBoardScale;
    private float _goalShowAt = -1f;
    private float _goalRun = -1f;
    private bool _goalShown;
    private FuwaPhotoPoint _firstPoint;
    private FuwaPhotoPoint _secondPoint;
    private float _firstShotAt = -1f;
    private float _secondShotAt = -1f;
    private float _fireWaitUntil = -1f;
    private bool _secondIsFire;
    private Vector3 _fireOrigin;
    private Vector3 _fireAim;

    // 撮り方
    private const int KindFar = 0;
    private const int KindClose = 1;
    private const int KindFire = 2;
    private Material _feedMat;
    private int _feedShown = -1;

    private void Start()
    {
        int n = frames != null ? frames.Length : 0;
        _photos = new RenderTexture[n];
        _has = new bool[n];
        _pointOf = new FuwaPhotoPoint[n];
        if (shotCam != null) { shotCam.enabled = false; shotCam.depthTextureMode = DepthTextureMode.Depth; }   // うそ影（デカール）は深度を使う
        if (feedRenderer != null) { _feedMat = feedRenderer.material; feedRenderer.enabled = false; }
        for (int i = 0; i < n; i++) if (frames[i] != null) frames[i].enabled = false;
        if (goalBoard != null) { _goalBoardScale = goalBoard.localScale; goalBoard.gameObject.SetActive(false); }
        RefreshCaptions();
    }

    // 自分のふわふわがゴールした時に FuwaGoal から呼ばれる。ゴールの奥の空に、この挑戦の写真の板を出す
    public void OnGoal(FuwaBall ball, Transform goal, Transform returnPoint)
    {
        if (goalBoard == null || ball == null || goal == null) return;
        _ball = ball;
        Vector3 away = returnPoint != null ? goal.position - returnPoint.position : goal.forward;
        away.y = 0f;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
        goalBoard.position = goal.position + away * goalBoardBack + Vector3.up * goalBoardUp;
        goalBoard.rotation = Quaternion.LookRotation(away, Vector3.up);   // 戻る地点（ゴールの手前）から読める向き
        _goalRun = ball.GetRunStartTime();
        _goalShowAt = Time.time + goalBoardDelay;
        _goalShown = false;
        goalBoard.gameObject.SetActive(false);
    }

    // ゴールの板の出し入れと、ぽんっと出る動き
    private void UpdateGoalBoard()
    {
        if (goalBoard == null || _goalShowAt < 0f) return;
        // 次の挑戦を始めた・コースを離れた・遠く離れたら消す
        bool gone = _ball == null || _ball.GetRunStartTime() != _goalRun;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!gone && lp != null && lp.IsValid() && (lp.GetPosition() - goalBoard.position).sqrMagnitude > goalBoardHideDistance * goalBoardHideDistance) gone = true;
        if (gone)
        {
            _goalShowAt = -1f;
            goalBoard.gameObject.SetActive(false);
            return;
        }
        float t = Time.time - _goalShowAt;
        if (t < 0f) return;
        if (!_goalShown)
        {
            _goalShown = true;
            goalBoard.gameObject.SetActive(true);
            for (int i = 0; i < goalFrames.Length; i++)
            {
                if (goalFrames[i] == null) continue;
                bool has = i < _has.Length && _has[i] && _photos[i] != null;
                goalFrames[i].enabled = has;
                if (has) goalFrames[i].material.SetTexture("_MainTex", _photos[i]);
            }
        }
        // 板はふわっと（少し行き過ぎて戻る）、写真は 0.12 秒ずつずらしてぽんっ
        goalBoard.localScale = _goalBoardScale * Pop(t / 0.4f);
        for (int i = 0; i < goalFrames.Length; i++)
        {
            if (goalFrames[i] == null) continue;
            bool has = i < _has.Length && _has[i] && _photos[i] != null;
            if (has && !goalFrames[i].enabled)
            {
                goalFrames[i].enabled = true;   // ゴールの後に撮れた写真（ゴール前の2枚目など）
                goalFrames[i].material.SetTexture("_MainTex", _photos[i]);
            }
            Transform slot = goalFrames[i].transform.parent;
            if (slot != null) slot.localScale = Vector3.one * Pop((t - 0.3f - i * 0.12f) / 0.3f);
            if (goalEmptyMarks != null && i < goalEmptyMarks.Length && goalEmptyMarks[i] != null && goalEmptyMarks[i].activeSelf == has) goalEmptyMarks[i].SetActive(!has);
        }
    }

    // 0→1 で、少し行き過ぎてから 1 に落ち着く大きさ
    private float Pop(float x)
    {
        if (x <= 0f) return 0f;
        if (x >= 1f) return 1f;
        return Mathf.Sin(x * Mathf.PI * 0.5f) * (1f + 0.18f * Mathf.Sin(x * Mathf.PI));
    }

    // 自分のふわふわがフォトポイントに入った時に FuwaPhotoPoint から呼ばれる
    public void OnPhotoPoint(FuwaPhotoPoint point, FuwaBall ball)
    {
        if (point == null || ball == null || shotCam == null || point.camPose == null) return;
        _ball = ball;
        if (point.takeFirst) { _firstPoint = point; _firstShotAt = Time.time + shotDelay; }
        if (!point.takeSecond) return;
        _secondPoint = point;
        _secondIsFire = point.order <= 1;
        if (_secondIsFire) { _fireWaitUntil = Time.time + fireWait; _secondShotAt = -1f; }
        else { _fireWaitUntil = -1f; _secondShotAt = Time.time + secondShotDelay; }
    }

    // 自分の銃が撃った時に FuwaBlower から呼ばれる（origin＝銃口、aim＝撃った向き）
    public void OnFire(Vector3 origin, Vector3 aim)
    {
        if (_fireWaitUntil < 0f || Time.time > _fireWaitUntil) return;
        _fireWaitUntil = -1f;
        _fireOrigin = origin;
        _fireAim = aim;
        _secondShotAt = Time.time + fireShotDelay;
    }

    // 言語が切り替わった時に FuwaLanguage から呼ばれる（呼ばれなくても、次に撮った時に直る）
    public void RefreshCaptions()
    {
        bool en = language != null && language.IsEnglish();
        // 写真が1枚でもあればコース名を出す（写真の下の名前をなくした時に、ここで判定していたせいでコース名まで消えていた）
        bool any = false;
        if (_has != null) for (int i = 0; i < _has.Length; i++) if (_has[i]) any = true;
        if (captions != null)
        {
            for (int i = 0; i < captions.Length; i++)
            {
                if (captions[i] == null) continue;
                FuwaPhotoPoint pt = i < _pointOf.Length && _has[i] ? _pointOf[i] : null;
                captions[i].text = pt != null ? (en ? pt.nameEn : pt.nameJa) : "";
                if (pt != null) any = true;
            }
        }
        if (titleText != null)
        {
            string t = "<color=#FF6B5C>" + (en ? titleEn : titleJa) + "</color>";   // 見出しはあそびかたの看板と同じオレンジ（コース名はふつうの色）
            string c = CourseName(_boardCourse, en);
            titleText.text = any && c != "" ? t + "<space=0.8em><size=58%><color=#222222>" + c + "</color></size>" : t;   // 見出しとコース名の間をあけて、コース名は黒（てじゃ）
        }
        if (emptyMarks != null)
            for (int i = 0; i < emptyMarks.Length; i++) if (emptyMarks[i] != null) emptyMarks[i].SetActive(!(i < _has.Length && _has[i]));
    }

    private string CourseName(int course, bool en)
    {
        string[] names = en ? courseNamesEn : courseNamesJa;
        return names != null && course >= 0 && course < names.Length ? names[course] : "";
    }

    private int CourseIndex(Transform start)
    {
        if (courseStarts == null || start == null) return -1;
        for (int i = 0; i < courseStarts.Length; i++) if (courseStarts[i] == start) return i;
        return -1;
    }

    private void Update()
    {
        UpdateGoalBoard();
        if (_firstShotAt >= 0f && Time.time >= _firstShotAt)
        {
            _firstShotAt = -1f;
            int o = _firstPoint != null ? _firstPoint.order : 0;
            Shoot(_firstPoint, 0, o == 1 ? KindClose : KindFar);
        }
        // 撃たないまま待ち時間が過ぎたら、近くからの1枚にする
        if (_fireWaitUntil >= 0f && Time.time > _fireWaitUntil)
        {
            _fireWaitUntil = -1f;
            _secondIsFire = false;
            _secondShotAt = Time.time;
        }
        if (_secondShotAt >= 0f && Time.time >= _secondShotAt)
        {
            _secondShotAt = -1f;
            Shoot(_secondPoint, 1, _secondIsFire ? KindFire : KindClose);
        }
    }

    private void Shoot(FuwaPhotoPoint point, int which, int kind)
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null || !lp.IsValid() || _ball == null || point == null || frames == null || frames.Length == 0) return;
        int order = Mathf.Clamp(point.order, 0, 2);
        int slot = order * 2 + which;
        if (slot >= frames.Length) return;

        // 別のコース・別の挑戦の写真なら、ボードを消して並べ直す
        float run = _ball.GetRunStartTime();
        if (run != _boardRun)
        {
            _boardRun = run;
            _boardCourse = CourseIndex(_ball.startPoint);
            for (int i = 0; i < _has.Length; i++) { _has[i] = false; _pointOf[i] = null; if (frames[i] != null) frames[i].enabled = false; }
            if (goalBoard != null) { _goalShowAt = -1f; goalBoard.gameObject.SetActive(false); }
        }

        float eye = lp.GetAvatarEyeHeightAsMeters();
        Vector3 chest = lp.GetBonePosition(HumanBodyBones.Chest);
        if (chest == Vector3.zero) chest = lp.GetPosition() + Vector3.up * (eye * 0.6f);
        Vector3 head = lp.GetBonePosition(HumanBodyBones.Head);
        if (head == Vector3.zero) head = lp.GetPosition() + Vector3.up * eye;
        Vector3 ball = _ball.transform.position;
        bool placed;
        if (kind == KindFire) placed = PlaceFireShot(order, chest, head, eye) || PlaceAround(point, order, KindClose, ball, chest);
        else placed = PlaceAround(point, order, kind, ball, chest);
        if (!placed)
        {
            // どこも置けない時は、ポイントに置いたカメラから
            Transform pose = point.camPose;
            shotCam.transform.position = pose.position;
            Vector3 to = ball - pose.position;
            shotCam.transform.rotation = to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to, Vector3.up) : pose.rotation;
            shotCam.fieldOfView = point.fieldOfView;
        }

        // 写真は色だけ（奥行きなし）で持つ。描く時だけ、奥行きつきの共用の1枚に描いてから写す（メモリ半分）
        int ph = photoWidth * 9 / 16;
        RenderTexture rt = _photos[slot];
        if (rt == null)
        {
            rt = new RenderTexture(photoWidth, ph, 0);
            rt.Create();
            _photos[slot] = rt;
        }
        if (_renderRT == null)
        {
            _renderRT = new RenderTexture(photoWidth, ph, 24);
            _renderRT.Create();
        }

        // HUDなどを隠して1枚だけ描く
        bool[] was = new bool[hideWhileShooting != null ? hideWhileShooting.Length : 0];
        for (int i = 0; i < was.Length; i++) { Renderer r = hideWhileShooting[i]; if (r != null) { was[i] = r.enabled; r.enabled = false; } }
        shotCam.targetTexture = _renderRT;
        shotCam.aspect = 16f / 9f;
        shotCam.Render();
        shotCam.targetTexture = null;
        VRCGraphics.Blit(_renderRT, rt);
        for (int i = 0; i < was.Length; i++) { Renderer r = hideWhileShooting[i]; if (r != null) r.enabled = was[i]; }

        _has[slot] = true;
        _pointOf[slot] = point;
        if (frames[slot] != null)
        {
            frames[slot].enabled = true;
            frames[slot].material.SetTexture("_MainTex", rt);
        }
        _feedShown = -1;
        RefreshCaptions();
    }

    // 遠く・近く：ふわふわの飛んでいく向きを基準に、ポイントごとに違う角度から。後ろの自分も入れば入れる
    private bool PlaceAround(FuwaPhotoPoint point, int order, int kind, Vector3 ball, Vector3 chest)
    {
        Rigidbody rb = _ball.GetComponent<Rigidbody>();
        Vector3 fwd = rb != null ? rb.velocity : Vector3.zero;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.04f) { fwd = ball - chest; fwd.y = 0f; }
        fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, fwd);
        float pref = Vector3.Dot(point.camPose.position - ball, side) >= 0f ? 1f : -1f;
        // 前・横・上（基準からの距離の割合）。先頭2つがこの撮り方の角度（決めた側・反対側）、あとは予備
        float[] ax; float[] sx; float[] ux;
        float d;
        Vector3 basePos = ball;
        if (kind == KindFar && order == 2)
        {
            // 後ろの高い所から追いかけ（自分の後ろ上から、前を飛ぶふわふわを見る）
            d = farDistance; basePos = chest;
            ax = new float[] { -0.55f, -0.55f, -0.35f, -0.35f, 0.3f, 0.3f }; sx = new float[] { 0.25f, -0.25f, 0.5f, -0.5f, 0.9f, -0.9f }; ux = new float[] { 0.45f, 0.45f, 0.6f, 0.6f, 0.4f, 0.4f };
        }
        else if (kind == KindFar)
        {
            // 横の高い所から景色ごと
            d = farDistance;
            ax = new float[] { 0.3f, 0.3f, 0.65f, 0.65f, -0.2f, -0.2f }; sx = new float[] { 0.95f, -0.95f, 0.7f, -0.7f, 0.9f, -0.9f }; ux = new float[] { 0.45f, 0.45f, 0.35f, 0.35f, 0.55f, 0.55f };
        }
        else if (order == 2)
        {
            // 上から見下ろし
            d = closeDistance;
            ax = new float[] { 0.45f, 0.45f, 0.2f, 0.2f, 0.85f, 0.85f }; sx = new float[] { 0.25f, -0.25f, 0.5f, -0.5f, 0.55f, -0.55f }; ux = new float[] { 1.05f, 1.05f, 0.9f, 0.9f, 0.3f, 0.3f };
        }
        else
        {
            // 斜め前の低め
            d = closeDistance;
            ax = new float[] { 0.9f, 0.9f, 0.55f, 0.55f, 1.2f, 1.2f }; sx = new float[] { 0.5f, -0.5f, 0.85f, -0.85f, 0.35f, -0.35f }; ux = new float[] { 0.08f, 0.08f, 0.3f, 0.3f, 0.2f, 0.2f };
        }
        bool playerNear = (chest - ball).magnitude < 12f;
        for (int k = 0; k < ax.Length; k++)
        {
            Vector3 pos = basePos + (fwd * ax[k] + side * (sx[k] * pref) + Vector3.up * ux[k]) * d;
            if (Physics.CheckSphere(pos, 0.3f, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
            if (Blocked(pos, ball)) continue;
            Vector3 dB = (ball - pos).normalized;
            Vector3 dir = dB;
            float fov = kind == KindFar ? 50f : 45f;
            if (playerNear && !Blocked(pos, chest))
            {
                Vector3 dC = (chest - pos).normalized;
                float ang = Vector3.Angle(dB, dC);
                if (ang < 70f)
                {
                    // 両方が入る向き・画角に（近くはふわふわを主役に、遠くは真ん中）
                    float wB = kind == KindFar ? 0.5f : 0.6f;
                    dir = (dB * wB + dC * (1f - wB)).normalized;
                    fov = Mathf.Clamp(ang * 1.1f + (kind == KindFar ? 20f : 14f), 32f, 62f);
                }
            }
            Aim(pos, dir, fov);
            return true;
        }
        return false;
    }

    // 撃った瞬間：銃口と撃った向きを基準に。ポイント1は斜め前から（風がこっちへ）、ポイント2は真横から（風がまっすぐ飛んでいく）
    private bool PlaceFireShot(int order, Vector3 chest, Vector3 head, float eye)
    {
        float s = Mathf.Clamp(eye / 1.3f, 0.5f, 2.5f);
        Vector3 m0 = _fireOrigin;
        Vector3 a = _fireAim.sqrMagnitude > 0.0001f ? _fireAim.normalized : Vector3.forward;
        Vector3 f = a; f.y = 0f;
        f = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        Vector3 r = Vector3.Cross(Vector3.up, f);
        Vector3 mid = Vector3.Lerp(chest, m0, 0.5f);
        for (int k = 0; k < 2; k++)
        {
            float mm = k == 0 ? 1f : -1f;   // 2回目は左右を入れ替える
            Vector3 pos; Vector3 look; float fov;
            if (order == 1)
            {
                pos = mid + (r * (2.1f * mm) + f * 0.9f + Vector3.up * -0.05f) * s;
                look = m0 + a * (0.9f * s); fov = 52f;
            }
            else
            {
                pos = m0 + (f * 1.3f + r * (0.85f * mm) + Vector3.up * 0.2f) * s;
                look = Vector3.Lerp(head, m0, 0.6f); fov = 44f;
            }
            if (Physics.CheckSphere(pos, 0.2f, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
            if (Blocked(pos, chest) || Blocked(pos, m0)) continue;
            Vector3 dir = look - pos;
            if (dir.sqrMagnitude < 0.0001f) continue;
            Aim(pos, dir.normalized, fov);
            return true;
        }
        return false;
    }

    private void Aim(Vector3 pos, Vector3 dir, float fov)
    {
        shotCam.transform.position = pos;
        shotCam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        shotCam.fieldOfView = fov;
    }

    private bool Blocked(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float len = d.magnitude - 0.4f;   // 写す物そのもの（ふわふわ・自分の体）には当たらないよう、手前で止める
        if (len <= 0f) return false;
        RaycastHit hit;
        return Physics.SphereCast(from, 0.1f, d / d.magnitude, out hit, len, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    // 額縁（ロビーのボード・ゴールの板）のうち、VRChatカメラが近くで向いている写真の番号。なければ -1
    // 写真の面の座標で判定する：レンズが写真の範囲（少し広め）の手前 feedRange 以内〜面の奥 feedBehind まで、写真の方を向いていれば当たり
    // （2026-10-12 てじゃ「ぶつかるくらいまで近づけると解除されちゃう」：前は写真の中心への角度で見ていたので、近すぎると外れた）
    private int FeedTarget(Renderer[] list, Vector3 camPos, Vector3 camFwd, ref float best)
    {
        int hit = -1;
        if (list == null) return -1;
        for (int i = 0; i < list.Length && i < _has.Length; i++)
        {
            if (list[i] == null || !list[i].enabled || !_has[i] || _photos[i] == null) continue;
            Transform t = list[i].transform;
            Vector3 n = t.forward;                       // 写真の面の奥向き（見る人は -forward 側）
            Vector3 d = camPos - t.position;
            float depth = -Vector3.Dot(d, n);            // 面の手前が＋、奥が－
            if (depth > feedRange || depth < -feedBehind) continue;
            if (Vector3.Dot(camFwd, n) < 0.3f) continue; // 写真の方を向いている
            Vector3 lossy = t.lossyScale;
            float hx = Mathf.Abs(lossy.x) * 0.5f + feedMargin, hy = Mathf.Abs(lossy.y) * 0.5f + feedMargin;
            float lx = Vector3.Dot(d, t.right), ly = Vector3.Dot(d, t.up);
            if (Mathf.Abs(lx) > hx || Mathf.Abs(ly) > hy) continue;
            float score = Mathf.Abs(depth) + new Vector2(lx, ly).magnitude * 0.5f;
            if (score >= best) continue;
            best = score;
            hit = i;
        }
        return hit;
    }

    // VRChatカメラが額縁に近づいたら、レンズの前に写真の板を置く
    public override void PostLateUpdate()
    {
        if (feedRenderer == null || feed == null) return;
        VRCCameraSettings photo = VRCCameraSettings.PhotoCamera;
        int hit = -1;
        if (photo != null && photo.Active)
        {
            float best = feedRange;
            int h1 = FeedTarget(frames, photo.Position, photo.Forward, ref best);
            int h2 = FeedTarget(goalFrames, photo.Position, photo.Forward, ref best);
            hit = h2 >= 0 ? h2 : h1;
            if (hit >= 0)
            {
                feed.position = photo.Position + photo.Forward * feedDistance;
                feed.rotation = photo.Rotation;
            }
        }
        if (hit != _feedShown)
        {
            _feedShown = hit;
            feedRenderer.enabled = hit >= 0;
            if (hit >= 0 && _feedMat != null) _feedMat.SetTexture("_MainTex", _photos[hit]);
        }
    }
}
