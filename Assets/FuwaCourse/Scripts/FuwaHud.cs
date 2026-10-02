using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Rendering;

// タイマーやメッセージの表示（同期なし、自分にだけ見える）。
// デスクトップ: 銃に関係なく、いつも視界の下の方に固定。
// VR: 銃を持っている間は銃の横（右手なら右側、左手なら左側）に浮かび、自分の頭の方を向く。持っていない時は道具の少し上。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaHud : UdonSharpBehaviour
{
    [Header("デスクトップ")]
    [Tooltip("頭から見た位置")]
    public Vector3 offset = new Vector3(0f, 0.05f, 0.7f);
    [Tooltip("追従のなめらかさ（大きいほどピッタリ付いてくる）")]
    public float followSpeed = 12f;

    [Header("VR")]
    [Tooltip("道具からどれだけ上に浮かせるか（世界の上方向）")]
    public float vrHeightAboveTool = 0.18f;
    [Tooltip("VRでの文字の大きさ（デスクトップ=1）")]
    public float vrScale = 0.6f;

    [Header("銃を持っている間（銃の横に出す）")]
    [Tooltip("銃から文字の端までのすき間(m)")]
    public float besideGap = 0.08f;
    [Tooltip("銃の横に出す時の文字の大きさ（デスクトップ=1）")]
    public float besideScale = 0.45f;
    [Tooltip("銃より少し上にずらす量(m)")]
    public float besideUp = 0.03f;

    [Header("文字の後ろの背景（なくてもOK）")]
    public TextMeshPro statusText;
    public SpriteRenderer statusBg;
    public TextMeshPro timerText;
    public SpriteRenderer timerBg;
    [Tooltip("文字の左右・上下に足す余白")]
    public Vector2 bgPadding = new Vector2(0.08f, 0.04f);
    [Tooltip("背景を文字の中心から上下にずらす量。文字の影（アンダーレイ）が右下に出る分、見た目の中心が下がるので少し下げる")]
    public float bgOffsetY = 0f;

    [Header("空気ゲージ（銃を持っている間だけ、タイムの下に出す）")]
    public GameObject airGauge;
    public SpriteRenderer airFill;
    [Tooltip("カプセル型ゲージ（Fuwa/HudGauge の板）。あればこちらを使う")]
    public Renderer airGaugeRenderer;
    private Material _gaugeMat;
    [Tooltip("ゲージの大きさ（HUDの中の単位）")]
    public Vector2 airGaugeSize = new Vector2(0.24f, 0.03f);
    public Color airColorFull = new Color(0.55f, 0.9f, 0.6f, 0.95f);
    public Color airColorLow = new Color(0.95f, 0.45f, 0.4f, 0.95f);

    [Tooltip("フルチャージの時にゲージが光る色")]
    public Color airColorCharged = new Color(1f, 0.85f, 0.35f, 1f);
    private float _charge;
    private float _air = 1f;
    private float _airShownValue = -1f;
    private bool _airEmpty;
    private bool _airHeld;

    private VRCPlayerApi _local;
    private Transform _anchor;
    private Vector3 _baseScale;
    private bool _held;
    private bool _leftHand;
    private int _align = 0;
    private string _lastStatus = "";
    private string _lastTimer = "";
    private MeshRenderer _statusMr;
    private MeshRenderer _timerMr;

    private void Start()
    {
        _local = Networking.LocalPlayer;
        if (statusText != null) _statusMr = statusText.GetComponent<MeshRenderer>();
        if (timerText != null) _timerMr = timerText.GetComponent<MeshRenderer>();
        _baseScale = transform.localScale;
        if (airGaugeRenderer != null) _gaugeMat = airGaugeRenderer.material;
    }

    // 自分の風の道具から呼ばれる
    public void SetAnchor(Transform anchor)
    {
        _anchor = anchor;
    }

    // 自分の銃を持った／離した時に銃から呼ばれる
    public void SetHeld(bool held, bool leftHand)
    {
        _held = held;
        _leftHand = leftHand;
    }

    // 自分の銃から毎フレーム呼ばれる（空気の残り、ちょん押しできないほど少ないか、持っているか）
    public void SetAir(float air, bool empty, bool held)
    {
        _air = air;
        _airEmpty = empty;
        _airHeld = held;
    }

    // 自分の銃から毎フレーム呼ばれる（今の溜め具合 0〜1、溜めていなければ0）
    public void SetCharge(float charge)
    {
        _charge = charge;
    }

    private void UpdateAirGauge()
    {
        if (airGauge == null) return;
        // デスクトップは画面下のタイムの下、VRは銃の横のタイムの下に出る（HUDごと動くので）
        // VRでは銃の側面のゲージを使うので、画面側のゲージは出さない
        bool show = _airHeld && !(_local != null && _local.IsValid() && _local.IsUserInVR());
        if (airGauge.activeSelf != show) airGauge.SetActive(show);
        if (!show || (airFill == null && _gaugeMat == null)) return;
        Color c = GetGaugeColor();
        if (_gaugeMat != null)
        {
            _gaugeMat.SetColor("_FillColor", c);
            _gaugeMat.SetFloat("_Fill", _air);
            return;
        }
        airFill.color = c;
        if (Mathf.Abs(_air - _airShownValue) < 0.001f) return;
        _airShownValue = _air;
        float h = airGaugeSize.y;
        float w = Mathf.Max(h, airGaugeSize.x * _air);
        airFill.enabled = _air > 0.01f;
        float sc = h * 2f;
        airFill.transform.localScale = new Vector3(sc, sc, 1f);
        airFill.size = new Vector2(w, h) / sc;
        Vector3 p = airFill.transform.localPosition;
        p.x = -airGaugeSize.x * 0.5f + w * 0.5f;
        airFill.transform.localPosition = p;
    }

    // ゲージの色（銃の側面のゲージもこれを使う）
    public Color GetGaugeColor()
    {
        Color c = _airEmpty
            ? Color.Lerp(airColorLow, new Color(airColorLow.r, airColorLow.g, airColorLow.b, 0.35f), 0.5f + 0.5f * Mathf.Sin(Time.time * 14f))
            : Color.Lerp(airColorLow, airColorFull, Mathf.Clamp01(_air * 2.5f));   // 4割を切るまでは緑のまま
        if (_charge <= 0f) return c;
        // 溜めている間：ゲージの色が徐々に黄色（金色）に。フルチャージでキラキラ脈打つ
        if (_charge >= 1f)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
            return Color.Lerp(airColorCharged, Color.white, pulse * 0.6f);
        }
        // 水色と黄色をそのまま混ぜると途中がくすんだ緑になるので、途中は白っぽく明るくしながら黄色へ
        Color k = Color.Lerp(c, airColorCharged, _charge * _charge);
        return Color.Lerp(k, Color.white, 0.45f * Mathf.Sin(_charge * Mathf.PI));
    }

    public override void PostLateUpdate()
    {
        UpdateAirGauge();
        // 文字が変わった瞬間は、まだ文字の形（メッシュ）が古いまま（描画の直前に作り直される）。
        // そのフレームは文字と枠を隠して、次のフレームで正しい大きさ・位置になってから見せる
        // （TextMeshProのForceMeshUpdateはUdonで使えないため）
        bool statusChanged = statusText != null && statusText.text != _lastStatus;
        bool timerChanged = timerText != null && timerText.text != _lastTimer;
        if (statusChanged) _lastStatus = statusText.text;
        // タイムは毎フレーム数字が変わるので、空⇔表示の切り替わりの時だけ隠す
        if (timerChanged) { timerChanged = string.IsNullOrEmpty(_lastTimer) || string.IsNullOrEmpty(timerText.text); _lastTimer = timerText.text; }
        SetTextVisible(statusText, statusBg, !statusChanged);
        SetTextVisible(timerText, timerBg, !timerChanged);
        if (!statusChanged) FitBackground(statusText, statusBg);
        if (!timerChanged) FitBackground(timerText, timerBg);
        if (_local == null || !_local.IsValid()) return;
        VRCPlayerApi.TrackingData head = _local.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);

        // 銃の横に出すのはVRだけ（デスクトップは銃が視界の端で文字が見えないので、画面下に出す）
        if (_held && _anchor != null && _local.IsUserInVR())
        {
            // 銃の外側へ。向きは頭から見た左右（銃をどう傾けても読める向き）
            Vector3 side = head.rotation * Vector3.right;
            side.y = 0f;
            side = side.sqrMagnitude > 0.0001f ? side.normalized : Vector3.right;
            float sign = _leftHand ? -1f : 1f;
            float sc = besideScale;
            Vector3 pos = _anchor.position + side * sign * besideGap + Vector3.up * besideUp;
            // 文字（背景の枠）の銃側の端がそろうように、それぞれ枠の幅の半分だけ外側へずらす
            ShiftOutward(statusText, statusBg, sign);
            ShiftOutward(timerText, timerBg, sign);
            ShiftGauge(sign);
            Vector3 toText = pos - head.position;
            transform.position = pos;
            if (toText.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(toText, Vector3.up);
            transform.localScale = _baseScale * sc;
            return;
        }
        ShiftOutward(statusText, statusBg, 0f);
        ShiftOutward(timerText, timerBg, 0f);
        ShiftGauge(0f);

        if (_local.IsUserInVR() && _anchor != null)
        {
            Vector3 pos = _anchor.position + Vector3.up * vrHeightAboveTool;
            Vector3 toText = pos - head.position;
            transform.position = pos;
            // TextMeshProは「forwardが見る人の視線と同じ向き」の時に正しく読める
            if (toText.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(toText, Vector3.up);
            transform.localScale = _baseScale * vrScale;
            return;
        }

        // デスクトップは視界にピッタリ固定（遅れてついてくると文字が揺れて読みにくいので）
        // 頭ではなく画面のカメラ基準にする（三人称視点だとカメラが頭の後ろにあって、頭基準だとアバターの体に重なるため）
        transform.localScale = _baseScale;
        VRCCameraSettings cam = VRCCameraSettings.ScreenCamera;
        if (cam != null)
        {
            Quaternion r = cam.Rotation;
            transform.position = cam.Position + r * offset;
            transform.rotation = r;
        }
        else
        {
            transform.position = head.position + head.rotation * offset;
            transform.rotation = head.rotation;
        }
    }

    // 文字の長さに合わせて背景の大きさを変える。文字が空なら背景も消す
    private void FitBackground(TextMeshPro text, SpriteRenderer bg)
    {
        if (text == null || bg == null) return;
        bool on = !string.IsNullOrEmpty(text.text);
        if (bg.enabled != on) bg.enabled = on;
        if (!on) return;
        // 文字のメッシュの大きさ（TextMeshProのpreferredWidthはUdonで使えないため）
        MeshFilter mf = text.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;
        Vector3 ext = mf.sharedMesh.bounds.size;
        Vector2 size = new Vector2(ext.x + bgPadding.x * 2f, ext.y + bgPadding.y * 2f);
        size.x = Mathf.Max(size.x, size.y);
        // 数字が変わるたびに文字の形で数mmずつ幅が変わってピクピクするので、小さな変化は無視する
        // 位置も同じ：数字の形で文字のメッシュの中心が数mmずれるので、小さな変化の時は枠を動かさない
        // （前はここで中心だけ毎フレーム合わせ直していて、枠が左右にプルプルしてた）
        Vector2 cur = bg.size * bg.transform.localScale.x;
        if (Mathf.Abs(size.x - cur.x) < 0.03f && Mathf.Abs(size.y - cur.y) < 0.03f) return;
        // 文字寄せの側の端をそろえて置く（左寄せなら左端、右寄せなら右端が動かない）
        Bounds b = mf.sharedMesh.bounds;
        float cx = b.center.x;
        if (_align > 0) cx = b.min.x - bgPadding.x + size.x * 0.5f;
        else if (_align < 0) cx = b.max.x + bgPadding.x - size.x * 0.5f;
        bg.transform.localPosition = new Vector3(cx, b.center.y + bgOffsetY, 0.005f);
        // 角丸スプライトの角の半径は大きさ1の時0.25。高さの半分がちょうど角の半径になるように
        // 縮小して置く（両端がきれいな半円のカプセル型になる）
        float sc = Mathf.Max(0.0001f, size.y * 2f);
        bg.transform.localScale = new Vector3(sc, sc, 1f);
        bg.size = size / sc;
    }

    // 文字を横にずらす（sign=1なら右へ、-1なら左へ、0なら真ん中）
    private void ShiftOutward(TextMeshPro text, SpriteRenderer bg, float sign)
    {
        // TextMeshProの.transformはUdonで使えないので、子の背景から親（＝文字）をたどる
        if (text == null || bg == null) return;
        Transform t = bg.transform.parent;
        if (t == null) return;
        float half = bg.enabled ? bg.size.x * bg.transform.localScale.x * 0.5f : 0f;
        Vector3 p = t.localPosition;
        p.x = sign * half;
        t.localPosition = p;
    }

    // ゲージも文字と同じく、銃側の端がそろうように外側へずらす
    private void ShiftGauge(float sign)
    {
        if (airGauge == null) return;
        Transform t = airGauge.transform;
        Vector3 p = t.localPosition;
        p.x = sign * (airGaugeSize.x * 0.5f + 0.01f);
        t.localPosition = p;
    }

    private void SetTextVisible(TextMeshPro text, SpriteRenderer bg, bool visible)
    {
        if (text == null) return;
        MeshRenderer mr = text == statusText ? _statusMr : _timerMr;
        if (mr != null && mr.enabled != visible) mr.enabled = visible;
        if (!visible && bg != null) bg.enabled = false;
    }
}
