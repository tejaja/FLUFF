using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// あそびばの観戦リビングのテレビ：おっかけカメラ ⇄ 動画（TejaVideoPlayer）の切りかえ（自分の画面だけ）。
// ・見出しの横の丸ボタンで手動切りかえ
// ・だれかが動画を流しても、テレビは勝手に切りかえない（おっかけカメラを見たい人もいるので。音は流れる）。
//   代わりに切りかえボタンに「動画やってるよ」の印（コーラルの点がぽわぽわ）を出す（2026-10-15 てじゃ「勝手に動画切り替わるのきになる」）
// ・■停止・最後まで再生・動画なしになったら、動画を見ていた人のテレビはカメラに戻す。一時停止は動画のまま
// ・動画の時：テレビの画面の手前に動画の画面を出し、下の ME／CAM ボタンの所を動画の操作パネルに入れかえ、見出しを「どうが」に
// （2026-10-15 てじゃ「おすすめでいこう」＝ボタンで切りかえ＋再生で自動）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaTvVideoSwitch : UdonSharpBehaviour
{
    public TejaVideoPlayer video;
    [Tooltip("動画の画面（テレビの画面のすぐ手前に重ねた板）")]
    public GameObject videoScreen;
    [Tooltip("動画の操作パネル（動画の時だけ出す）")]
    public GameObject videoControls;
    [Tooltip("おっかけカメラの時だけ出す物（ME／CAM のボタン）")]
    public GameObject[] cameraOnly;
    public TextMeshPro title;
    public FuwaLanguage language;
    public string camTitleJa = "<color=#FF6B5C>おっかけカメラ</color>";
    public string camTitleEn = "<color=#FF6B5C>Chase Cam</color>";
    public string videoTitleJa = "<color=#FF6B5C>どうが</color>";
    public string videoTitleEn = "<color=#FF6B5C>Video</color>";
    [Header("切りかえボタンの絵")]
    public Image toggleIcon;
    [Tooltip("カメラの時に出す絵（押すと動画へ）")]
    public Sprite iconToVideo;
    [Tooltip("動画の時に出す絵（押すとカメラへ）")]
    public Sprite iconToCam;

    [Tooltip("カメラを見ている間に動画が流れていたら出す印（切りかえボタンの右上の点）")]
    public GameObject playingBadge;

    [Header("操作パネルを隠すボタン（動画の時だけ出す。画面とパネルのすき間の真ん中）")]
    public GameObject hideButton;
    public Text hideLabel;

    private bool _video;
    private bool _panelHidden;   // 動画の操作パネルを隠している（自分の画面だけ。2026-10-15 てじゃ「UI隠しボタンもどっかにほしい」）
    private bool _wasPlaying;
    private bool _wasIdle = true;
    private bool _started;
    private float _next;

    private void OnEnable()
    {
        // エリアに戻ってきた時：今の再生の状態に合わせて始める
        _started = false;
    }

    public void TogglePanel()
    {
        _panelHidden = !_panelHidden;
        SetMode(_video);
    }

    public void ToggleMode()
    {
        SetMode(!_video);
    }

    private void SetMode(bool v)
    {
        _video = v;
        if (videoScreen != null && videoScreen.activeSelf != v) videoScreen.SetActive(v);
        bool ctrl = v && !_panelHidden;
        if (videoControls != null && videoControls.activeSelf != ctrl) videoControls.SetActive(ctrl);
        if (hideButton != null && hideButton.activeSelf != v) hideButton.SetActive(v);
        if (hideLabel != null) hideLabel.text = _panelHidden ? "▼" : "▲";   // 出ている時▲（しまう）、隠れている時▼（出す）（2026-10-15 てじゃ「イメージ逆」）
        if (cameraOnly != null) for (int i = 0; i < cameraOnly.Length; i++) if (cameraOnly[i] != null && cameraOnly[i].activeSelf == v) cameraOnly[i].SetActive(!v);
        if (toggleIcon != null) toggleIcon.sprite = v ? iconToCam : iconToVideo;
        ApplyTitle();
    }

    private void ApplyTitle()
    {
        if (title == null) return;
        bool en = language != null && language.IsEnglish();
        string t = _video ? (en ? videoTitleEn : videoTitleJa) : (en ? camTitleEn : camTitleJa);
        if (title.text != t) title.text = t;
    }

    private void Update()
    {
        if (Time.time < _next) return;
        _next = Time.time + 0.25f;
        if (video == null) return;
        bool playing = video.IsPlayingNow();
        bool idle = video.IsIdle();
        if (!_started)
        {
            _started = true;
            _wasPlaying = playing; _wasIdle = idle;
            SetMode(!idle && _video);
            return;
        }
        if (idle && !_wasIdle) SetMode(false);        // 停止・おわり・動画なし
        _wasPlaying = playing; _wasIdle = idle;
        ApplyTitle();   // 言語の切りかえで見出しが書きかわっても、今のモードの見出しに戻す
        _badge = playing && !_video;
        if (playingBadge != null && playingBadge.activeSelf != _badge) playingBadge.SetActive(_badge);
    }

    private bool _badge;
    // 印をぽわぽわさせる（出ている時だけ）
    private void LateUpdate()
    {
        if (!_badge || playingBadge == null) return;
        float k = 1f + 0.18f * Mathf.Sin(Time.time * 4f);
        playingBadge.transform.localScale = new Vector3(k, k, 1f);
    }
}
