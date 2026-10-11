using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

// 名場面アルバム（自分にだけ、同期なし）。2026-10-11 てじゃ「遊んでるところのスクショを自動でいい感じに撮ってロビーで見れるやつ」
// ・自分がゴールした瞬間（少し遅れて、花火が上がるあたり）に、見えないカメラ（shotCam）が
//   自分とふわふわが両方入る斜め前へ回り込んで1枚撮る。地面や壁で隠れる角度はよける
// ・写真はテクスチャ（photos）に描いて、ロビーの額縁（frames）に新しい順に並べる。下にコース名とタイム
// ・VRChatの手持ちカメラを額縁に近づけると、VRChatカメラの画面だけその写真に差し替える（シャッターで自分のアルバムに保存できる）
//   （Fuwa/PhotoAlbumFeed の板をレンズの前に置く。FuwaFollowCam と同じやり方）
// 画像はネットで送れないので、撮るのも見るのも自分の画面だけ。ワールドを出たら消える
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaPhotoAlbum : UdonSharpBehaviour
{
    [Header("撮影")]
    [Tooltip("撮る用のカメラ（ふだんは止めておく。撮る時だけ Render する）")]
    public Camera shotCam;
    [Tooltip("ゴールしてから撮るまでの秒数")]
    public float shotDelay = 0.6f;
    [Tooltip("写真の大きさ（横）。縦は16:9")]
    public int photoWidth = 1280;
    [Tooltip("撮る時に隠すもの（HUDの文字など）")]
    public Renderer[] hideWhileShooting;
    [Tooltip("カメラと写す物の間に地面や壁があったら、その角度はやめる")]
    public LayerMask obstacleMask = (1 << 0) | (1 << 11);

    [Header("飾る所")]
    [Tooltip("額縁の写真の面（新しい順）")]
    public Renderer[] frames;
    [Tooltip("frames と同じ順で、下の文字（コース名とタイム）")]
    public TextMeshPro[] captions;
    [Tooltip("まだ写真がない額縁に出す物（「ゴールすると写真が飾られるよ」など）。写真が入ると消す")]
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
    private float[] _timeOf;
    private int _count;

    private FuwaBall _ball;   // 自分のふわふわ（タイムの書き方もこれに聞く）
    private float _pendingTime;
    private float _shootAt = -1f;
    private Material _feedMat;
    private int _feedShown = -1;

    private void Start()
    {
        int n = frames != null ? frames.Length : 0;
        _photos = new RenderTexture[n];
        _courseOf = new int[n];
        _timeOf = new float[n];
        if (shotCam != null) { shotCam.enabled = false; shotCam.depthTextureMode = DepthTextureMode.Depth; }   // うそ影（デカール）は深度を使う
        if (feedRenderer != null) { _feedMat = feedRenderer.material; feedRenderer.enabled = false; }
        for (int i = 0; i < n; i++) if (frames[i] != null) frames[i].enabled = false;
        RefreshCaptions();
    }

    // 自分のふわふわがゴールした時に FuwaGoal から呼ばれる
    public void OnGoal(FuwaBall ball)
    {
        if (ball == null || shotCam == null) return;
        _ball = ball;
        _pendingTime = ball.lastGoalTime;
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
            captions[i].text = _timeOf[i] > 0f && _ball != null ? name + "  " + _ball.FormatTimeText(_timeOf[i]) : name;
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
        if (lp == null || !lp.IsValid() || _ball == null || frames == null || frames.Length == 0) return;

        // 写す物：自分の胸とふわふわ
        float eye = lp.GetAvatarEyeHeightAsMeters();
        Vector3 chest = lp.GetBonePosition(HumanBodyBones.Chest);
        if (chest == Vector3.zero) chest = lp.GetPosition() + Vector3.up * (eye * 0.6f);
        Vector3 head = lp.GetBonePosition(HumanBodyBones.Head);
        if (head == Vector3.zero) head = lp.GetPosition() + Vector3.up * eye;
        Vector3 ballPos = _ball.transform.position;
        if (!PlaceCamera(lp, chest, head, ballPos, eye)) return;

        // いちばん古い写真を押し出して、先頭に入れる
        int n = frames.Length;
        RenderTexture rt = _photos[n - 1];
        if (rt == null)
        {
            rt = new RenderTexture(photoWidth, photoWidth * 9 / 16, 24);
            rt.Create();
        }
        for (int i = n - 1; i > 0; i--) { _photos[i] = _photos[i - 1]; _courseOf[i] = _courseOf[i - 1]; _timeOf[i] = _timeOf[i - 1]; }
        _photos[0] = rt;
        _courseOf[0] = CourseIndex(_ball.startPoint);
        _timeOf[0] = _pendingTime;
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

    // 自分とふわふわが両方入って、間に何もない角度を探す。顔が写るよう、自分の前側の角度から順に試す
    private bool PlaceCamera(VRCPlayerApi lp, Vector3 chest, Vector3 head, Vector3 ballPos, float eye)
    {
        Vector3 mid = (chest + ballPos) * 0.5f;
        Vector3 toBall = ballPos - chest; toBall.y = 0f;
        float sep = toBall.magnitude;
        Vector3 fwd = lp.GetRotation() * Vector3.forward; fwd.y = 0f;
        fwd = fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
        // ふわふわが遠い時は、ふわふわ寄りを見る（自分が小さくなりすぎないよう、少しだけ）
        float halfFov = shotCam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float scale = Mathf.Max(0.6f, eye / 1.6f);
        float dist = Mathf.Clamp((sep * 0.5f + 1.0f * scale) / Mathf.Tan(halfFov * 1.6f), 2.0f * scale, 9f);
        float[] yaws = { 35f, -35f, 60f, -60f, 0f, 90f, -90f, 130f, -130f, 180f };
        for (int k = 0; k < yaws.Length; k++)
        {
            Vector3 dir = Quaternion.Euler(0f, yaws[k], 0f) * fwd;
            Vector3 pos = mid + dir * dist + Vector3.up * (0.6f * scale);
            if (Physics.CheckSphere(pos, 0.25f, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
            // 手前に物があって顔やふわふわが隠れる角度もやめる（少し太い線で調べる）
            if (Blocked(pos, head)) continue;
            if (Blocked(pos, chest)) continue;
            if (Blocked(pos, ballPos)) continue;
            shotCam.transform.position = pos;
            shotCam.transform.rotation = Quaternion.LookRotation(mid + Vector3.up * (0.1f * scale) - pos, Vector3.up);
            return true;
        }
        return false;
    }

    private bool Blocked(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        float len = d.magnitude - 0.35f;   // 写す物そのもの（自分の体・ふわふわ）には当たらないよう、手前で止める
        if (len <= 0f) return false;
        RaycastHit hit;
        return Physics.SphereCast(from, 0.12f, d / d.magnitude, out hit, len, obstacleMask, QueryTriggerInteraction.Ignore);
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
