using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 乗るとワープし、玉をワープ先のスタートに移して、リスポーン地点もワープ先にする。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaCourseWarp : UdonSharpBehaviour
{
    public FuwaBall ball;
    public FuwaRespawnManager respawnManager;
    [Tooltip("ワープ先の、玉のスタート位置")]
    public Transform courseStart;
    [Tooltip("ワープ先の、人の立ち位置（向きも使う）。リスポーン地点にもなる")]
    public Transform playerSpot;
    [Tooltip("ワープ先でタイムを計るか（ロビーはOFF）")]
    public bool timed = true;
    [Tooltip("ワープ先の銃の台の上（持っていない銃をここへ移す）")]
    public Transform gunSpot;
    [Tooltip("ワープ時に鳴らす音（自分にだけ聞こえる。なくてもOK）")]
    public AudioSource warpSound;
    [Tooltip("ワープしたら隠すもの（ゴールのワープ自身など。なくてもOK）")]
    public GameObject hideOnWarp;
    [Tooltip("0=なにもしない / 1=ここ（モードのロビー）を手動リスポーンの戻り先にする / 2=戻り先を消す（入口ロビーへ戻る時）")]
    public int homeAction = 0;
    [Tooltip("ワープ先のエリア（0=入口 / 1=ひとり / 2=たいせん / -1=切り替えない）。そのエリアだけ表示する")]
    public int area = -1;
    public FuwaAreaVisibility areaVisibility;
    [Tooltip("コースへのワープ用：コース中に手動リスポーンしてロビーへ戻った時の立ち位置（そのコースのポータルの方を向いた場所。なくてもOK）")]
    public Transform lobbyReturnSpot;

    [Header("暗転")]
    [Tooltip("暗転（なくてもOK。無ければすぐワープ）")]
    public FuwaFade fade;
    [Tooltip("暗くなるまでの秒数（真っ暗になった所でワープ）")]
    public float fadeOutTime = 0.25f;
    [Tooltip("真っ暗のままの秒数")]
    public float fadeHold = 0.15f;
    [Tooltip("明けるまでの秒数")]
    public float fadeInTime = 0.35f;
    private float _busyUntil;

    [Header("乗って少し待つとワープ（うっかり防止。0なら乗った瞬間）")]
    [Tooltip("乗ってからワープするまでの秒数。途中で降りたらキャンセル")]
    public float dwellTime = 0f;
    [Tooltip("床の「たまっていく輪っか」（FuwaCourse/ProgressRing。なくてもOK）")]
    public Renderer progressRing;
    [Tooltip("たまっている間の音（なくてもOK）")]
    public AudioSource chargeSound;
    [Tooltip("乗った瞬間の音（chargeSound の音源で鳴らす。なくてもOK）。2026-10-12 てじゃ「入った！ってのをわかりやすく」")]
    public AudioClip enterClip;
    [Tooltip("輪っかから上へ伸びて体を包む光の幕（FuwaCourse/WarpVeil。なくてもOK）")]
    public Renderer progressVeil;
    [Tooltip("目の前に出す丸いゲージ（FuwaCourse/ProgressRing の縦の板。なくてもOK）。乗った瞬間の向きで、目の高さ・正面に置く（視界には張り付かない）")]
    public Renderer gauge;
    [Tooltip("ゲージを頭からどれだけ前に置くか(m)")]
    public float gaugeDistance = 0.7f;
    [Tooltip("ゲージの大きさ(m)")]
    public float gaugeSize = 0.32f;
    [Tooltip("正面の幕が立ち上がる時間(秒)")]
    public float frontRiseTime = 0.3f;
    private float _dwellStart = -1f;
    private Material _gaugeMat;
    private float _front;
    private float _shownFill = 0f;
    private float _holdFullUntil = 0f;
    private Material _ringMat;
    private Material _veilMat;

    public override void OnPlayerTriggerEnter(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal) return;
        if (dwellTime <= 0f) { WarpLocalPlayer(); return; }
        _dwellStart = Time.time;
        SetStartFromView(player);
        PlaceGauge(player);
        if (chargeSound != null)
        {
            chargeSound.Stop();
            if (enterClip != null) chargeSound.PlayOneShot(enterClip);
            chargeSound.Play();
        }
    }

    public override void OnPlayerTriggerExit(VRCPlayerApi player)
    {
        if (player == null || !player.isLocal || dwellTime <= 0f) return;
        CancelDwell();
    }

    // 途中で降りたら、Update で輪と幕がスッと戻る
    private void CancelDwell()
    {
        _dwellStart = -1f;
        if (chargeSound != null) chargeSound.Stop();
    }

    // 輪と幕のたまり始めを、乗った時に向いていた方向（目の前）にする
    private void SetStartFromView(VRCPlayerApi player)
    {
        Transform basis = progressRing != null ? progressRing.transform : (progressVeil != null ? progressVeil.transform : null);
        if (basis == null) return;
        Vector3 fwd = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
        Vector3 d = basis.InverseTransformDirection(fwd);
        float a = Mathf.Atan2(-d.x, d.y) / (Mathf.PI * 2f);
        if (a < 0f) a += 1f;
        if (progressRing != null)
        {
            if (_ringMat == null) _ringMat = progressRing.material;
            _ringMat.SetFloat("_Start", a);
        }
        if (progressVeil != null)
        {
            if (_veilMat == null) _veilMat = progressVeil.material;
            _veilMat.SetFloat("_Start", a);
        }
    }

    // 目の前のゲージを、乗った瞬間の頭の位置・向きで置く（その後は動かさない）
    private void PlaceGauge(VRCPlayerApi player)
    {
        if (gauge == null) return;
        VRCPlayerApi.TrackingData head = player.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 fwd = head.rotation * Vector3.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();
        Transform g = gauge.transform;
        g.position = head.position + fwd * gaugeDistance + Vector3.down * 0.05f;
        g.rotation = Quaternion.LookRotation(fwd, Vector3.up);   // 板の表（-z）が自分の方を向く
        g.localScale = Vector3.zero;
        if (_gaugeMat == null) _gaugeMat = gauge.material;
        _gaugeMat.SetFloat("_Fill", 0f);
        gauge.enabled = true;
    }

    private void SetFill(float f)
    {
        _shownFill = f;
        if (progressRing != null)
        {
            if (_ringMat == null) _ringMat = progressRing.material;
            _ringMat.SetFloat("_Fill", f);
        }
        if (progressVeil != null)
        {
            if (_veilMat == null) _veilMat = progressVeil.material;
            _veilMat.SetFloat("_Fill", f);
            progressVeil.enabled = f > 0.001f || _front > 0.001f;
        }
        if (gauge != null && _gaugeMat != null) _gaugeMat.SetFloat("_Fill", f);
    }

    // 正面の幕（乗った瞬間にふわっと立ち上がる）とゲージの出し入れ
    private void SetFront(float v)
    {
        _front = v;
        if (progressVeil != null)
        {
            if (_veilMat == null) _veilMat = progressVeil.material;
            _veilMat.SetFloat("_Front", v);
            progressVeil.enabled = v > 0.001f || _shownFill > 0.001f;
        }
        if (gauge != null)
        {
            float s = gaugeSize * Pop(v);
            gauge.transform.localScale = new Vector3(-s, s, s);   // 左右反転：見る人から見て時計回りに満ちる
            gauge.enabled = v > 0.001f;
        }
    }

    private float Pop(float x)
    {
        if (x <= 0f) return 0f;
        if (x >= 1f) return 1f;
        return Mathf.Sin(x * Mathf.PI * 0.5f) * (1f + 0.15f * Mathf.Sin(x * Mathf.PI));
    }

    private void Update()
    {
        if (_dwellStart < 0f)
        {
            if (_shownFill > 0f && Time.time > _holdFullUntil) SetFill(Mathf.Max(0f, _shownFill - Time.deltaTime * 3f));
            if (_front > 0f && Time.time > _holdFullUntil) SetFront(Mathf.Max(0f, _front - Time.deltaTime * 4f));
            return;
        }
        SetFront(Mathf.Clamp01((Time.time - _dwellStart) / Mathf.Max(0.01f, frontRiseTime)));
        float p = (Time.time - _dwellStart) / Mathf.Max(0.01f, dwellTime);
        SetFill(Mathf.Clamp01(p));
        if (p < 1f) return;
        _dwellStart = -1f;
        _holdFullUntil = Time.time + fadeOutTime + fadeHold;   // 真っ暗になるまで満タンの幕のまま
        WarpLocalPlayer();
    }

    private void OnEnable()
    {
        _dwellStart = -1f;
        SetFill(0f);
        SetFront(0f);
    }

    private void OnDisable()
    {
        _dwellStart = -1f;
        if (chargeSound != null) chargeSound.Stop();
    }

    // 自分をワープ先へ（FuwaRespawnManagerの手動リスポーン時にも呼ばれる）。
    // その場で動けなくして暗転し、真っ暗になったらワープ、明けたら動けるように戻す。
    public void WarpLocalPlayer()
    {
        if (Time.time < _busyUntil) return;
        if (fade == null) { DoWarp(); return; }
        _busyUntil = Time.time + fadeOutTime + fadeHold;
        // 動けなくする・戻すのは暗転側に任せる（ワープ後にこの物体が非表示になることがあるので）
        fade.FreezeFor(fadeOutTime + fadeHold + fadeInTime * 0.5f);
        fade.Play(0f, fadeOutTime, fadeHold, fadeInTime);
        SendCustomEventDelayedSeconds(nameof(DoWarp), fadeOutTime);
    }

    public void DoWarp()
    {
        VRCPlayerApi player = Networking.LocalPlayer;
        if (player == null) return;
        if (playerSpot != null) player.TeleportTo(playerSpot.position, playerSpot.rotation);
        if (respawnManager != null)
        {
            respawnManager.SetRespawnSpot(playerSpot);
            respawnManager.BringGun(gunSpot);
            if (homeAction == 1) respawnManager.SetHomeWarp(this);
            else if (homeAction == 2) respawnManager.SetHomeWarp(null);
            // モードのロビーに入り直したら、手動リスポーンの向きはふつう（説明ボード向き）に戻す
            if (homeAction != 0) respawnManager.SetLobbyReturnSpot(null);
            if (lobbyReturnSpot != null) respawnManager.SetLobbyReturnSpot(lobbyReturnSpot);
        }
        if (ball != null && courseStart != null) ball.StartCourse(courseStart, timed);
        if (warpSound != null) warpSound.Play();
        if (hideOnWarp != null) hideOnWarp.SetActive(false);
        // 最後にエリアの表示を切り替える（このワープ自身が非表示になることがあるので最後に）
        if (areaVisibility != null && area >= 0) areaVisibility.ShowArea(area);
    }
}
