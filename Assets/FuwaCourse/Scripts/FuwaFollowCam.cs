using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.SDKBase;

// ふわふわ追っかけカメラ＝写真館のモニター（自分にだけ、同期なし、保存なし）。2026-10-11 てじゃの仕様で作り直し：
// ・写真館のモニターに「選んだ人（自分か、ひとりでモードにいる人）」のふわふわを追う映像を映す
// ・CAMボタンで OFF → 上 → 右横 → 左横 → 後ろ。OFF の時はモニターも消える（映像を止める）。最初は OFF
// ・モニターの横の半透明の球（zone）に VRChatカメラ（写真・ストリーム）を入れている間だけ、カメラの映像＝モニターの映像
//   （ワールドからはVRChatカメラを動かせないので、映像をテクスチャに描いてレンズの前の板 feed（Fuwa/PhotoCamFeed）で画面いっぱいに貼る）
// ・他の人のふわふわは、自分の画面では半透明の分身（FuwaGhost）。モニターのカメラには分身を写さず（分身は別レイヤー）、
//   かわりに本物そっくりの毛玉（monitorFluff、自分の目には写らないレイヤー）を分身の位置に置いて写す
// ・映すのは、モニターの近く（renderRange 以内）にいる時か、球にカメラを入れている時だけ（軽くするため）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaFollowCam : UdonSharpBehaviour
{
    [Tooltip("追っかけ用のカメラ（Target Texture に feedTexture を入れておく）")]
    public Camera cam;
    [Tooltip("VRChatカメラのレンズの前に置く板（Fuwa/PhotoCamFeed のマテリアル）")]
    public Transform feed;
    public Renderer feedRenderer;
    [Tooltip("板をレンズからどれだけ前に置くか(m)")]
    public float feedDistance = 0.3f;

    [Tooltip("追いかける物（ひとりで用のふわふわ、みんなで用のふわふわ…）。見えていて近いものを追う")]
    public Transform[] targets;
    [Tooltip("targets と同じ順で、その物の見た目。消えている（隠れている）時は追わない")]
    public Renderer[] targetRenderers;
    [Tooltip("これより遠いふわふわは追わない（自分だけ映す）")]
    public float maxTargetDistance = 25f;

    [Header("切り替え")]
    public KeyCode toggleKey = KeyCode.B;
    [Tooltip("ボタンの今のモードの文字（各ロビーのボタン）")]
    public TextMeshProUGUI[] modeTexts;
    [Tooltip("modeTexts と同じ順で、文字の後ろに出す白い札")]
    public UnityEngine.UI.Image[] modePills;
    [Tooltip("ONの時の文字の色（ボタンの色）")]
    public Color activeTextColor = new Color(0.45f, 0.78f, 0.62f, 1f);
    [Tooltip("OFFの時の文字の色（白い札の上なので灰色）")]
    public Color idleTextColor = new Color(0.55f, 0.55f, 0.6f, 1f);

    [Header("カメラの動き")]
    [Tooltip("位置の追いつく時間(秒)。大きいほどふわっと遅れてついてくる")]
    public float posSmoothTime = 0.35f;
    [Tooltip("向きの追いつく速さ")]
    public float lookSpeed = 6f;
    [Tooltip("進む向き（自分→ふわふわ）の変わる速さ")]
    public float dirSpeed = 2.5f;
    [Tooltip("自分とふわふわの間のどこを見るか（0=自分、1=ふわふわ）")]
    [Range(0f, 1f)] public float focusBlend = 0.55f;
    [Tooltip("カメラと見る所の間に地面や壁があったら手前に寄せる")]
    public LayerMask obstacleMask = (1 << 0) | (1 << 11);
    [Tooltip("当たり判定のない飾り（吹き流し・風車など）。カメラがこの中に入らないよう手前に寄せる（見た目の箱で判定）")]
    public Renderer[] camBlockers;
    [Tooltip("飾りの箱をどれだけ広げてよけるか(m)")]
    public float blockerMargin = 0.35f;
    [Tooltip("テスト用：モニターから遠くても追っかけカメラを動かす")]
    public bool runWithoutPhotoCamera = false;

    [Header("モニター")]
    [Tooltip("モニターの画面（映像を貼る板）")]
    public Renderer monitorScreen;
    [Tooltip("モニターが OFF の時に出す物（暗い画面・「OFF」の文字など）")]
    public GameObject monitorOffMark;
    [Tooltip("モニター用の映像（16:9、軽め）")]
    public RenderTexture monitorTexture;
    [Tooltip("VRChatカメラに出す時の映像（高画質）")]
    public RenderTexture feedTexture;
    [Tooltip("モニターからこの距離(m)以内にいる時だけ映す")]
    public float renderRange = 14f;
    [Tooltip("モニターだけの時の画角")]
    public float monitorFov = 55f;
    [Tooltip("VRChatカメラを入れる球（中心）")]
    public Transform zone;
    [Tooltip("球の半径(m)")]
    public float zoneRadius = 0.45f;
    [Tooltip("球の見た目（カメラが入っている時は _Glow=1）")]
    public Renderer zoneRenderer;

    [Header("映す人")]
    [Tooltip("ひとりでモードの番号（分身に同期されているエリア）")]
    public int soloArea = 1;
    [Tooltip("今映している人の名前を出す文字（人物ボタン）")]
    public TextMeshProUGUI[] targetTexts;
    [Tooltip("モニターの画面の上に出す名前（他の人を映している時だけ）")]
    public TextMeshPro monitorName;
    [Tooltip("他の人を映す時に、分身の位置に置く本物そっくりの毛玉（モニター専用レイヤー）")]
    public Transform monitorFluff;
    public TrailRenderer monitorFluffTrail;

    [Header("ほかの場所のモニター（みんなでひろば等。2026-10-12 てじゃ「こっちでも観戦できる」）")]
    [Tooltip("同じ映像を貼る画面。どれかの近くにいれば映す")]
    public Renderer[] extraScreens;
    [Tooltip("extraScreens と同じ順で、OFF の時に出す物")]
    public GameObject[] extraOffMarks;
    [Tooltip("extraScreens と同じ順で、画面の上の名前")]
    public TextMeshPro[] extraNames;
    [Tooltip("VRChatカメラを入れる球（ほかの場所の分）。どれかに入れればよい")]
    public Transform[] extraZones;
    [Tooltip("extraZones と同じ順で、球の見た目")]
    public Renderer[] extraZoneRenderers;

    private int _mode;   // 0=OFF 1=上 2=右横 3=左横 4=後ろ（てじゃ「TOPとBACKの順番入れ替え」）
    private int _targetId = -1;   // 映す人の playerId（-1＝自分）
    private float _nextList;
    private int[] _ids = new int[0];
    private bool _feedOn;
    private FuwaGhost _watchedGhost;
    private bool _inZone;
    private int _zoneIdx = -1;   // カメラが入っている球（-1=なし、0=写真館、1〜=extraZones）
    private Vector3 _dir = Vector3.forward;
    private Vector3 _focus;
    private Vector3 _vel;
    private bool _snap;
    private bool _running;

    private void Start()
    {
        if (cam != null) { cam.enabled = false; if (monitorTexture != null) cam.targetTexture = monitorTexture; }
        if (feedRenderer != null) feedRenderer.enabled = false;
        if (monitorScreen != null) monitorScreen.enabled = false;
        if (monitorOffMark != null) monitorOffMark.SetActive(true);
        SetExtra(false);
        if (extraScreens != null && monitorTexture != null) foreach (Renderer r in extraScreens) if (r != null) r.material.SetTexture("_MainTex", monitorTexture);
        if (monitorFluff != null) monitorFluff.gameObject.SetActive(false);
        UpdateLabels();
        UpdateTargetLabels();
    }

    // ---- 映す人の切り替え（人物ボタン）----
    public void NextTarget() { StepTarget(1); }
    public void PrevTarget() { StepTarget(-1); }

    private void StepTarget(int step)
    {
        RefreshList();
        int n = _ids.Length;   // 0 番は自分（-1）
        int cur = 0;
        for (int i = 0; i < n; i++) if (_ids[i] == _targetId) { cur = i; break; }
        cur = ((cur + step) % n + n) % n;
        _targetId = _ids[cur];
        _snap = true;
        UpdateTargetLabels();
    }

    // 自分＋ひとりでモードにいる人の一覧（playerId、自分は -1）
    private void RefreshList()
    {
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);
        int[] tmp = new int[players.Length + 1];
        int n = 0;
        tmp[n++] = -1;
        foreach (VRCPlayerApi p in players)
        {
            if (p == null || !p.IsValid() || p.isLocal) continue;
            FuwaGhost g = GhostOf(p);
            if (g != null && g.GetArea() == soloArea) tmp[n++] = p.playerId;
        }
        _ids = new int[n];
        for (int i = 0; i < n; i++) _ids[i] = tmp[i];
        // 映していた人がいなくなった（モードを出た・退出）ら自分に戻す
        bool found = false;
        for (int i = 0; i < n; i++) if (_ids[i] == _targetId) found = true;
        if (!found) { _targetId = -1; _snap = true; }
    }

    private FuwaGhost GhostOf(VRCPlayerApi p)
    {
        if (p == null || !p.IsValid()) return null;
        GameObject[] objs = Networking.GetPlayerObjects(p);
        if (objs == null) return null;
        foreach (GameObject o in objs)
        {
            if (o == null) continue;
            FuwaGhost g = o.GetComponent<FuwaGhost>();
            if (g != null) return g;
        }
        return null;
    }

    private void UpdateTargetLabels()
    {
        // 自分の時も名前（てじゃ「自分も名前取得しよ」）
        string s = "";
        VRCPlayerApi p = _targetId >= 0 ? VRCPlayerApi.GetPlayerById(_targetId) : Networking.LocalPlayer;
        if (p != null && p.IsValid())
        {
            s = p.displayName;
            if (s.Length > 20) s = s.Substring(0, 19) + "…";
        }
        if (targetTexts != null) foreach (TextMeshProUGUI t in targetTexts) if (t != null && t.text != s) t.text = s;
        string m = _targetId >= 0 ? s : "";
        if (monitorName != null && monitorName.text != m) monitorName.text = m;
        if (extraNames != null) foreach (TextMeshPro t in extraNames) if (t != null && t.text != m) t.text = m;
    }

    // 球の光（idx：0=写真館、1〜=extraZones）
    private void ZoneGlow(int idx, float v)
    {
        Renderer r = null;
        if (idx == 0) r = zoneRenderer;
        else if (idx > 0 && extraZoneRenderers != null && idx - 1 < extraZoneRenderers.Length) r = extraZoneRenderers[idx - 1];
        if (r != null) r.material.SetFloat("_Glow", v);
    }

    // ほかの場所のモニターを ON/OFF
    private void SetExtra(bool on)
    {
        if (extraScreens != null) foreach (Renderer r in extraScreens) if (r != null) r.enabled = on;
        if (extraOffMarks != null) foreach (GameObject g in extraOffMarks) if (g != null) g.SetActive(!on);
    }

    // どれかのモニターの近くにいるか
    private bool NearAnyMonitor(Vector3 pos)
    {
        if (monitorScreen != null && Vector3.Distance(pos, monitorScreen.transform.position) < renderRange) return true;
        if (extraScreens != null) foreach (Renderer r in extraScreens) if (r != null && r.gameObject.activeInHierarchy && Vector3.Distance(pos, r.transform.position) < renderRange) return true;
        return false;
    }

    // 設定のカメラボタン（レーザーで押すUIボタン）から呼ばれる。自分だけ
    public void CycleMode()
    {
        _mode = (_mode + 1) % 5;
        _snap = true;
        UpdateLabels();
    }

    // ボタンの◀側（左の部分）から呼ばれる：ひとつ前のモードへ
    public void CycleModeBack()
    {
        _mode = (_mode + 4) % 5;
        _snap = true;
        UpdateLabels();
    }

    public void TurnOff()
    {
        _mode = 0;
        UpdateLabels();
    }

    // 今映している人（-1＝自分）
    public int GetTargetId() { return _targetId; }

    private void UpdateLabels()
    {
        string s = _mode == 1 ? "TOP" : _mode == 2 ? "SIDE R" : _mode == 3 ? "SIDE L" : _mode == 4 ? "BACK" : "OFF";
        if (modeTexts != null)
            foreach (TextMeshProUGUI t in modeTexts) if (t != null) { t.text = s; t.color = _mode != 0 ? activeTextColor : idleTextColor; }
        if (modePills != null)
            foreach (UnityEngine.UI.Image im in modePills) if (im != null) im.enabled = true;   // 札はいつも出す（今のモードが書いてある）
    }

    private void Update()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        if (!lp.IsUserInVR() && Input.GetKeyDown(toggleKey)) CycleMode();
    }

    // VRChatカメラが動いた後に、板をレンズの前に置き直す。追っかけカメラもここで動かす
    public override void PostLateUpdate()
    {
        if (cam == null) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        if (Time.time >= _nextList)
        {
            _nextList = Time.time + 1f;
            RefreshList();
            UpdateTargetLabels();   // 人が抜けた時・名前が後から届いた時も合わせる
        }

        VRCCameraSettings photo = VRCCameraSettings.PhotoCamera;
        bool photoOn = photo != null && photo.Active;
        int zi = -1;
        if (photoOn)
        {
            if (zone != null && Vector3.Distance(photo.Position, zone.position) < zoneRadius) zi = 0;
            else if (extraZones != null)
                for (int i = 0; i < extraZones.Length; i++)
                    if (extraZones[i] != null && extraZones[i].gameObject.activeInHierarchy && Vector3.Distance(photo.Position, extraZones[i].position) < zoneRadius) { zi = i + 1; break; }
        }
        bool inZone = zi >= 0;
        if (zi != _zoneIdx)
        {
            ZoneGlow(_zoneIdx, 0f);
            _zoneIdx = zi;
            ZoneGlow(zi, 1f);
        }
        _inZone = inZone;
        bool near = NearAnyMonitor(lp.GetPosition());
        bool run = _mode != 0 && (near || inZone || runWithoutPhotoCamera);
        bool feedOn = run && inZone;
        if (run != _running)
        {
            _running = run;
            cam.enabled = run;
            if (run) _snap = true;
            if (monitorScreen != null) monitorScreen.enabled = run;
            if (monitorOffMark != null) monitorOffMark.SetActive(!run);
            SetExtra(run);
        }
        if (feedOn != _feedOn)
        {
            _feedOn = feedOn;
            // VRChatカメラに出す間は高画質の映像に描いて、モニターにも同じものを貼る
            RenderTexture rt = feedOn && feedTexture != null ? feedTexture : monitorTexture;
            if (rt != null)
            {
                cam.targetTexture = rt;
                if (monitorScreen != null) monitorScreen.material.SetTexture("_MainTex", rt);
                if (extraScreens != null) foreach (Renderer r in extraScreens) if (r != null) r.material.SetTexture("_MainTex", rt);
                if (feedRenderer != null) feedRenderer.material.SetTexture("_MainTex", rt);
            }
        }
        if (feedRenderer != null && feedRenderer.enabled != feedOn) feedRenderer.enabled = feedOn;
        if (!run)
        {
            if (_watchedGhost != null) { _watchedGhost.watched = false; _watchedGhost = null; }
            if (monitorFluff != null && monitorFluff.gameObject.activeSelf) monitorFluff.gameObject.SetActive(false);
            return;
        }

        if (feedOn)
        {
            cam.fieldOfView = photo.FieldOfView;
            if (photo.Aspect > 0.01f) cam.aspect = photo.Aspect;
            if (feed != null)
            {
                feed.position = photo.Position + photo.Forward * feedDistance;
                feed.rotation = photo.Rotation;
            }
        }
        else
        {
            cam.fieldOfView = monitorFov;
            cam.aspect = 16f / 9f;
        }
        MoveCamera();
    }

    private void MoveCamera()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (lp == null) return;
        // 映す人：自分（-1）か、ひとりでモードにいる他の人
        VRCPlayerApi who = lp;
        FuwaGhost ghost = null;
        if (_targetId >= 0)
        {
            VRCPlayerApi o = VRCPlayerApi.GetPlayerById(_targetId);
            if (o != null && o.IsValid()) { who = o; ghost = GhostOf(o); }
        }

        float eye = who.GetAvatarEyeHeightAsMeters();
        Vector3 p = who.GetPosition() + Vector3.up * (eye * 0.6f);

        // 映している人の分身は、見えていなくても毎フレーム動かしてもらう
        if (ghost != _watchedGhost)
        {
            if (_watchedGhost != null) _watchedGhost.watched = false;
            _watchedGhost = ghost;
            if (ghost != null) ghost.watched = true;
        }
        Transform tgt = null;
        bool ghostShown = false;
        if (who == lp) tgt = FindTarget(p);
        else if (ghost != null && ghost.MonitorVisible()) { tgt = ghost.transform; ghostShown = true; }   // 浮いて待っている間も映す
        // 他の人のふわふわ：分身の位置に本物そっくりの毛玉（モニターのカメラにだけ写る）
        if (monitorFluff != null)
        {
            if (monitorFluff.gameObject.activeSelf != ghostShown) monitorFluff.gameObject.SetActive(ghostShown);
            if (ghostShown)
            {
                if (monitorFluffTrail != null && (monitorFluff.position - tgt.position).sqrMagnitude > 9f) monitorFluffTrail.Clear();
                monitorFluff.SetPositionAndRotation(tgt.position, tgt.rotation);
            }
        }
        Vector3 b;
        if (tgt != null) b = tgt.position;
        else
        {
            Vector3 hf = who.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
            hf.y = 0f;
            if (hf.sqrMagnitude < 0.0001f) hf = _dir;
            b = p + hf.normalized * 1.5f;
        }

        // 進む向き：自分→ふわふわ（横だけ）。急に振り向かないようにゆっくり回す
        Vector3 d = b - p;
        d.y = 0f;
        if (d.sqrMagnitude > 0.04f)
        {
            d.Normalize();
            _dir = _snap ? d : Vector3.Slerp(_dir, d, 1f - Mathf.Exp(-dirSpeed * Time.deltaTime)).normalized;
        }
        float sep = Vector3.Distance(p, b);
        Vector3 focus = Vector3.Lerp(p, b, focusBlend);
        Vector3 right = Vector3.Cross(Vector3.up, _dir).normalized;

        Vector3 want;
        if (_mode == 4) want = p - _dir * (2.6f + sep * 0.25f) + Vector3.up * (1.2f + sep * 0.12f);
        else if (_mode == 2 || _mode == 3)
        {
            float side = _mode == 2 ? 1f : -1f;
            want = focus + right * side * (3.0f + sep * 0.6f) + Vector3.up * 0.7f - _dir * 0.5f;
        }
        else want = focus - _dir * (2.0f + sep * 0.3f) + Vector3.up * (5.0f + sep * 0.4f);

        // 地面や壁にめり込まないように、見る所からカメラまでの間に何かあれば手前に
        Vector3 toCam = want - focus;
        float len = toCam.magnitude;
        RaycastHit hit;
        if (len > 0.01f && Physics.Raycast(focus, toCam / len, out hit, len, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            want = hit.point + hit.normal * 0.25f;
        }
        // 当たり判定のない飾り（吹き流しなど）も、見た目の箱でよける（カメラが中に入って画面がふさがらないように）
        if (camBlockers != null && len > 0.01f)
        {
            Vector3 dir = toCam / len;
            float near = (want - focus).magnitude;
            Ray ray = new Ray(focus, dir);
            bool blocked = false;
            for (int i = 0; i < camBlockers.Length; i++)
            {
                Renderer br = camBlockers[i];
                if (br == null || !br.enabled || !br.gameObject.activeInHierarchy) continue;
                Bounds bb = br.bounds;
                bb.Expand(blockerMargin * 2f);
                if (bb.Contains(focus)) continue;   // 見る所が飾りの中なら、よけようがないので無視
                float bd;
                if (bb.IntersectRay(ray, out bd) && bd < near) { near = bd; blocked = true; }
            }
            if (blocked) want = focus + dir * Mathf.Max(0.3f, near - 0.1f);
        }

        Transform ct = cam.transform;
        if (_snap)
        {
            ct.position = want;
            _focus = focus;
            _vel = Vector3.zero;
            ct.rotation = Quaternion.LookRotation(focus - want, Vector3.up);
            _snap = false;
            return;
        }
        // SmoothDamp と同じばね（Udon で ref 引数を避けるため自前で）
        float omega = 2f / Mathf.Max(0.01f, posSmoothTime);
        float x = omega * Time.deltaTime;
        float ex = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
        Vector3 change = ct.position - want;
        Vector3 temp = (_vel + omega * change) * Time.deltaTime;
        _vel = (_vel - omega * temp) * ex;
        ct.position = want + (change + temp) * ex;
        _focus = Vector3.Lerp(_focus, focus, 1f - Mathf.Exp(-lookSpeed * 1.5f * Time.deltaTime));
        Vector3 look = _focus - ct.position;
        if (look.sqrMagnitude > 0.0001f)
        {
            Quaternion r = Quaternion.LookRotation(look, Vector3.up);
            ct.rotation = Quaternion.Slerp(ct.rotation, r, 1f - Mathf.Exp(-lookSpeed * Time.deltaTime));
        }
    }

    private Transform FindTarget(Vector3 p)
    {
        if (targets == null) return null;
        Transform best = null;
        float bestD = maxTargetDistance;
        for (int i = 0; i < targets.Length; i++)
        {
            Transform t = targets[i];
            if (t == null || !t.gameObject.activeInHierarchy) continue;
            if (targetRenderers != null && i < targetRenderers.Length)
            {
                Renderer r = targetRenderers[i];
                if (r != null && !r.enabled) continue;
            }
            float dd = Vector3.Distance(p, t.position);
            if (dd < bestD) { bestD = dd; best = t; }
        }
        return best;
    }
}
