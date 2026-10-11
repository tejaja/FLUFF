using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

// 名場面アルバム（自分にだけ、同期なし）。2026-10-11 てじゃ「遊んでるところのスクショを自動でいい感じに撮ってロビーで見れるやつ」
// ・各コースにフォトポイント（FuwaPhotoPoint）が3か所。自分のふわふわが通ったら、見えないカメラ（shotCam）で2枚撮る：
//     すぐに「ふわふわ主役」1枚、少し後に「自分に寄った」1枚。→ 1コースで6枚
//   角度はポイントごとに思いっきり変える（似た角度ばかりだと単調。てじゃ）
//     ふわふわ主役：①斜め前の低め ②真横 ③上から見下ろし
//     自分に寄る　：①顔の正面 ②横の低い所からあおり ③肩ごしに後ろから（飛んでいくふわふわが見える）
//   どの角度も地面や物で隠れる時は、左右を入れ替え→別の角度→ポイントに置いたカメラ（camPose）の順に試す
//   （最初はゴールの瞬間→全コース同じ絵。次にポイントの固定カメラ→遠くて迫力がない。てじゃ）
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
    [Tooltip("フォトポイントに入ってから「自分に寄った」写真を撮るまでの秒数")]
    public float playerShotDelay = 1.0f;
    [Tooltip("ふわふわからカメラまでの距離のめやす(m)")]
    public float shotDistance = 2.4f;
    [Tooltip("カメラを置けるか調べる時に、ぶつかったらダメな物")]
    public LayerMask obstacleMask = (1 << 0) | (1 << 11);
    [Tooltip("写真の大きさ（横）。縦は16:9")]
    public int photoWidth = 1280;
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
    [TextArea] public string titleJa = "さっきの名場面";
    [TextArea] public string titleEn = "Recent Moments";

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

    private RenderTexture[] _photos;
    private bool[] _has;
    private FuwaPhotoPoint[] _pointOf;
    private int _boardCourse = -1;
    private float _boardRun = -1f;

    private FuwaBall _ball;
    private FuwaPhotoPoint _ballPoint;
    private FuwaPhotoPoint _playerPoint;
    private float _ballShotAt = -1f;
    private float _playerShotAt = -1f;
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
        RefreshCaptions();
    }

    // 自分のふわふわがフォトポイントに入った時に FuwaPhotoPoint から呼ばれる
    public void OnPhotoPoint(FuwaPhotoPoint point, FuwaBall ball)
    {
        if (point == null || ball == null || shotCam == null || point.camPose == null) return;
        _ball = ball;
        _ballPoint = point;
        _playerPoint = point;
        _ballShotAt = Time.time + shotDelay;
        _playerShotAt = Time.time + playerShotDelay;
    }

    // 言語が切り替わった時に FuwaLanguage から呼ばれる（呼ばれなくても、次に撮った時に直る）
    public void RefreshCaptions()
    {
        bool en = language != null && language.IsEnglish();
        bool any = false;
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
            string t = en ? titleEn : titleJa;
            string c = CourseName(_boardCourse, en);
            titleText.text = any && c != "" ? t + "<size=70%>  ―  " + c + "</size>" : t;
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
        if (_ballShotAt >= 0f && Time.time >= _ballShotAt) { _ballShotAt = -1f; Shoot(_ballPoint, false); }
        if (_playerShotAt >= 0f && Time.time >= _playerShotAt) { _playerShotAt = -1f; Shoot(_playerPoint, true); }
    }

    private void Shoot(FuwaPhotoPoint point, bool ofPlayer)
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null || !lp.IsValid() || _ball == null || point == null || frames == null || frames.Length == 0) return;
        int order = Mathf.Clamp(point.order, 0, 2);
        int slot = order * 2 + (ofPlayer ? 1 : 0);
        if (slot >= frames.Length) return;

        // 別のコース・別の挑戦の写真なら、ボードを消して並べ直す
        float run = _ball.GetRunStartTime();
        if (run != _boardRun)
        {
            _boardRun = run;
            _boardCourse = CourseIndex(_ball.startPoint);
            for (int i = 0; i < _has.Length; i++) { _has[i] = false; _pointOf[i] = null; if (frames[i] != null) frames[i].enabled = false; }
        }

        float eye = lp.GetAvatarEyeHeightAsMeters();
        Vector3 chest = lp.GetBonePosition(HumanBodyBones.Chest);
        if (chest == Vector3.zero) chest = lp.GetPosition() + Vector3.up * (eye * 0.6f);
        Vector3 head = lp.GetBonePosition(HumanBodyBones.Head);
        if (head == Vector3.zero) head = lp.GetPosition() + Vector3.up * eye;
        Vector3 ball = _ball.transform.position;
        bool placed = ofPlayer ? PlacePlayerShot(lp, order, chest, head, ball, eye) : PlaceBallShot(point, order, ball, chest);
        if (!placed && ofPlayer) placed = PlaceBallShot(point, order, ball, chest);
        if (!placed)
        {
            // どこも置けない時は、ポイントに置いたカメラから
            Transform pose = point.camPose;
            shotCam.transform.position = pose.position;
            Vector3 to = ball - pose.position;
            shotCam.transform.rotation = to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to, Vector3.up) : pose.rotation;
            shotCam.fieldOfView = point.fieldOfView;
        }

        RenderTexture rt = _photos[slot];
        if (rt == null)
        {
            rt = new RenderTexture(photoWidth, photoWidth * 9 / 16, 24);
            rt.Create();
            _photos[slot] = rt;
        }

        // HUDなどを隠して1枚だけ描く
        bool[] was = new bool[hideWhileShooting != null ? hideWhileShooting.Length : 0];
        for (int i = 0; i < was.Length; i++) { Renderer r = hideWhileShooting[i]; if (r != null) { was[i] = r.enabled; r.enabled = false; } }
        shotCam.targetTexture = rt;
        shotCam.aspect = 16f / 9f;
        shotCam.Render();
        shotCam.targetTexture = null;
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

    // ふわふわ主役：ふわふわの飛んでいく向きを基準に、ポイントごとに違う角度から（後ろの自分も入れば入れる）
    private bool PlaceBallShot(FuwaPhotoPoint point, int order, Vector3 ball, Vector3 chest)
    {
        Rigidbody rb = _ball.GetComponent<Rigidbody>();
        Vector3 fwd = rb != null ? rb.velocity : Vector3.zero;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.04f) { fwd = ball - chest; fwd.y = 0f; }
        fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, fwd);
        float pref = Vector3.Dot(point.camPose.position - ball, side) >= 0f ? 1f : -1f;
        float d = shotDistance;
        // 前・横・上（ふわふわからの距離の割合）。先頭2つがこのポイントの角度（決めた側・反対側）、あとは予備
        float[] ax; float[] sx; float[] ux;
        if (order == 1) { ax = new float[] { 0.05f, 0.05f, 0.5f, 0.5f, 0.85f, 0.85f }; sx = new float[] { 1f, -1f, 0.85f, -0.85f, 0.55f, -0.55f }; ux = new float[] { 0.2f, 0.2f, 0.35f, 0.35f, 0.3f, 0.3f }; }
        else if (order == 2) { ax = new float[] { 0.45f, 0.45f, 0.2f, 0.2f, 0.85f, 0.85f }; sx = new float[] { 0.25f, -0.25f, 0.5f, -0.5f, 0.55f, -0.55f }; ux = new float[] { 1.05f, 1.05f, 0.9f, 0.9f, 0.3f, 0.3f }; }
        else { ax = new float[] { 0.9f, 0.9f, 0.55f, 0.55f, 1.2f, 1.2f }; sx = new float[] { 0.5f, -0.5f, 0.85f, -0.85f, 0.35f, -0.35f }; ux = new float[] { 0.08f, 0.08f, 0.3f, 0.3f, 0.2f, 0.2f }; }
        bool playerNear = (chest - ball).magnitude < 9f;
        for (int k = 0; k < ax.Length; k++)
        {
            Vector3 pos = ball + (fwd * ax[k] + side * (sx[k] * pref) + Vector3.up * ux[k]) * d;
            if (Physics.CheckSphere(pos, 0.3f, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
            if (Blocked(pos, ball)) continue;
            Vector3 dB = (ball - pos).normalized;
            Vector3 dir = dB;
            float fov = 45f;
            if (playerNear && !Blocked(pos, chest))
            {
                Vector3 dC = (chest - pos).normalized;
                float ang = Vector3.Angle(dB, dC);
                if (ang < 70f)
                {
                    // ふわふわ寄りの真ん中を向いて、両方が入る画角に（ふわふわを主役に少し大きく）
                    dir = (dB * 0.6f + dC * 0.4f).normalized;
                    fov = Mathf.Clamp(ang * 1.1f + 14f, 32f, 62f);
                }
            }
            Aim(pos, dir, fov);
            return true;
        }
        return false;
    }

    // 自分に寄る：顔・体の向きを基準に、ポイントごとに違う角度から。アバターの大きさに合わせて距離を変える
    private bool PlacePlayerShot(VRCPlayerApi lp, int order, Vector3 chest, Vector3 head, Vector3 ball, float eye)
    {
        float s = Mathf.Clamp(eye / 1.3f, 0.5f, 2.5f);
        Vector3 f = lp.GetRotation() * Vector3.forward; f.y = 0f;
        f = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
        Vector3 r = Vector3.Cross(Vector3.up, f);
        for (int k = 0; k < 2; k++)
        {
            float m = k == 0 ? 1f : -1f;   // 2回目は左右を入れ替える
            Vector3 pos; Vector3 look; float fov; Vector3 subject;
            if (order == 1)
            {
                // 横の低い所からあおり
                pos = chest + (r * (1.7f * m) + f * 0.6f + Vector3.up * -0.75f) * s;
                look = chest + Vector3.up * (0.15f * s); fov = 48f; subject = chest;
            }
            else if (order == 2)
            {
                // 肩ごしに後ろから、飛んでいくふわふわの方を見る
                pos = head + (-f * 1.25f + r * (0.6f * m) + Vector3.up * 0.3f) * s;   // 頭で画面がふさがらないよう少し離す
                look = (ball - head).sqrMagnitude < 400f ? ball : head + f * 5f; fov = 55f; subject = head;
            }
            else
            {
                // 顔の正面（少し横から）
                pos = chest + (f * 1.6f + r * (0.45f * m) + Vector3.up * 0.25f) * s;
                look = Vector3.Lerp(chest, head, 0.6f); fov = 40f; subject = head;
            }
            if (Physics.CheckSphere(pos, 0.2f, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
            if (Blocked(pos, subject)) continue;
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

    // VRChatカメラが額縁に近づいたら、レンズの前に写真の板を置く
    public override void PostLateUpdate()
    {
        if (feedRenderer == null || feed == null) return;
        VRCCameraSettings photo = VRCCameraSettings.PhotoCamera;
        int hit = -1;
        if (photo != null && photo.Active)
        {
            float best = feedRange;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] == null || !_has[i] || _photos[i] == null) continue;
                Transform fr = frames[i].transform;
                Vector3 d = fr.position - photo.Position;
                float dist = d.magnitude;
                if (dist >= best || Vector3.Dot(photo.Forward, d) < dist * 0.6f) continue;
                best = dist;
                hit = i;
            }
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
