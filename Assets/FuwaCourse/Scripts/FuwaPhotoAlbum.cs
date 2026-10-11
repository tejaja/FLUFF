using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

// 名場面アルバム（自分にだけ、同期なし）。2026-10-11 てじゃ「遊んでるところのスクショを自動でいい感じに撮ってロビーで見れるやつ」
// ・自分のふわふわがコースのフォトポイント（FuwaPhotoPoint）を通ったら、見えないカメラ（shotCam）を
//   そのポイントのカメラ位置に置いて、ふわふわ（と後ろの自分）の方へ向けて1枚撮る
//   （最初はゴールの瞬間を撮っていたが、全コース同じ絵になるのでやめた。てじゃ）
// ・写真はテクスチャ（photos）に描いて、ロビーの額縁（frames）に新しい順に並べる。下にコース名とポイント名
// ・VRChatの手持ちカメラを額縁に近づけると、VRChatカメラの画面だけその写真に差し替える（シャッターで自分のアルバムに保存できる）
//   （Fuwa/PhotoAlbumFeed の板をレンズの前に置く。FuwaFollowCam と同じやり方）
// 画像はネットで送れないので、撮るのも見るのも自分の画面だけ。ワールドを出たら消える
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPhotoAlbum : UdonSharpBehaviour
{
    [Header("撮影")]
    [Tooltip("撮る用のカメラ（ふだんは止めておく。撮る時だけ Render する）")]
    public Camera shotCam;
    [Tooltip("フォトポイントに入ってから撮るまでの秒数")]
    public float shotDelay = 0.2f;
    [Tooltip("カメラの向きを、ポイントの決めた向きからふわふわの方へどれだけ向け直すか（0=決めた向きのまま、1=ふわふわを真ん中に）")]
    [Range(0f, 1f)] public float followBall = 0.75f;
    [Tooltip("狙う所を、ふわふわから自分の方へどれだけ寄せるか（後ろで構える自分も入るように）")]
    [Range(0f, 1f)] public float aimTowardPlayer = 0.25f;
    [Tooltip("写真の大きさ（横）。縦は16:9")]
    public int photoWidth = 1280;
    [Tooltip("撮る時に隠すもの（HUDの文字など）")]
    public Renderer[] hideWhileShooting;

    [Header("飾る所")]
    [Tooltip("額縁の写真の面（新しい順）")]
    public Renderer[] frames;
    [Tooltip("frames と同じ順で、下の文字（コース名とポイント名）")]
    public TextMeshPro[] captions;
    [Tooltip("まだ写真がない額縁に出す物。写真が入ると消す")]
    public GameObject[] emptyMarks;
    [Tooltip("ボードの見出し（言語で切り替える）")]
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
    private int[] _courseOf;
    private FuwaPhotoPoint[] _pointOf;
    private int _count;

    private FuwaBall _ball;
    private FuwaPhotoPoint _point;
    private float _shootAt = -1f;
    private Material _feedMat;
    private int _feedShown = -1;

    private void Start()
    {
        int n = frames != null ? frames.Length : 0;
        _photos = new RenderTexture[n];
        _courseOf = new int[n];
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
        _point = point;
        _shootAt = Time.time + shotDelay;
    }

    // 言語が切り替わった時に FuwaLanguage から呼ばれる（呼ばれなくても、次に撮った時に直る）
    public void RefreshCaptions()
    {
        if (captions == null) return;
        bool en = language != null && language.IsEnglish();
        for (int i = 0; i < captions.Length; i++)
        {
            if (captions[i] == null) continue;
            if (i >= _count) { captions[i].text = ""; continue; }
            string name = CourseName(_courseOf[i], en);
            FuwaPhotoPoint pt = _pointOf[i];
            string where = pt != null ? (en ? pt.nameEn : pt.nameJa) : "";
            captions[i].text = where != "" ? name + "  /  " + where : name;
        }
        if (titleText != null) titleText.text = en ? titleEn : titleJa;
        if (emptyMarks != null)
            for (int i = 0; i < emptyMarks.Length; i++) if (emptyMarks[i] != null) emptyMarks[i].SetActive(i >= _count);
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
        if (_shootAt < 0f || Time.time < _shootAt) return;
        _shootAt = -1f;
        Shoot();
    }

    private void Shoot()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null || !lp.IsValid() || _ball == null || _point == null || frames == null || frames.Length == 0) return;

        // カメラはポイントの位置。向きは、決めた向きからふわふわ（少し自分寄り）の方へ向け直す
        Vector3 chest = lp.GetBonePosition(HumanBodyBones.Chest);
        if (chest == Vector3.zero) chest = lp.GetPosition() + Vector3.up * (lp.GetAvatarEyeHeightAsMeters() * 0.6f);
        Vector3 aim = Vector3.Lerp(_ball.transform.position, chest, aimTowardPlayer);
        Transform pose = _point.camPose;
        Vector3 pos = pose.position;
        Quaternion rot = pose.rotation;
        Vector3 toAim = aim - pos;
        if (toAim.sqrMagnitude > 0.01f) rot = Quaternion.Slerp(rot, Quaternion.LookRotation(toAim, Vector3.up), followBall);
        // 傾きは消す（地平線がまっすぐの写真に）
        Vector3 f = rot * Vector3.forward;
        if (Mathf.Abs(f.y) < 0.99f) rot = Quaternion.LookRotation(f, Vector3.up);
        shotCam.transform.position = pos;
        shotCam.transform.rotation = rot;
        shotCam.fieldOfView = _point.fieldOfView;

        // いちばん古い写真を押し出して、先頭に入れる
        int n = frames.Length;
        RenderTexture rt = _photos[n - 1];
        if (rt == null)
        {
            rt = new RenderTexture(photoWidth, photoWidth * 9 / 16, 24);
            rt.Create();
        }
        for (int i = n - 1; i > 0; i--) { _photos[i] = _photos[i - 1]; _courseOf[i] = _courseOf[i - 1]; _pointOf[i] = _pointOf[i - 1]; }
        _photos[0] = rt;
        _courseOf[0] = CourseIndex(_ball.startPoint);
        _pointOf[0] = _point;
        if (_count < n) _count++;

        // HUDなどを隠して1枚だけ描く
        bool[] was = new bool[hideWhileShooting != null ? hideWhileShooting.Length : 0];
        for (int i = 0; i < was.Length; i++) { Renderer r = hideWhileShooting[i]; if (r != null) { was[i] = r.enabled; r.enabled = false; } }
        shotCam.targetTexture = rt;
        shotCam.aspect = 16f / 9f;
        shotCam.Render();
        shotCam.targetTexture = null;
        for (int i = 0; i < was.Length; i++) { Renderer r = hideWhileShooting[i]; if (r != null) r.enabled = was[i]; }

        for (int i = 0; i < n; i++)
        {
            if (frames[i] == null) continue;
            bool has = i < _count && _photos[i] != null;
            frames[i].enabled = has;
            if (has) frames[i].material.SetTexture("_MainTex", _photos[i]);
        }
        _feedShown = -1;
        RefreshCaptions();
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
            for (int i = 0; i < _count && i < frames.Length; i++)
            {
                if (frames[i] == null || _photos[i] == null) continue;
                Transform f = frames[i].transform;
                Vector3 d = f.position - photo.Position;
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
