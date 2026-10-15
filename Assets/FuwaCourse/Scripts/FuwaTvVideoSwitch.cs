using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

// あそびばの観戦リビングのテレビ：おっかけカメラ ⇄ 動画（TejaVideoPlayer）の切りかえ（自分の画面だけ）。
// ・見出しの横の丸ボタンで手動切りかえ
// ・誰かが動画を再生したら（再開も）自動で動画に、■停止・最後まで再生・動画なしになったら自動でカメラに戻す。一時停止は動画のまま
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

    private bool _video;
    private bool _wasPlaying;
    private bool _wasIdle = true;
    private bool _started;
    private float _next;

    private void OnEnable()
    {
        // エリアに戻ってきた時：今の再生の状態に合わせて始める
        _started = false;
    }

    public void ToggleMode()
    {
        SetMode(!_video);
    }

    private void SetMode(bool v)
    {
        _video = v;
        if (videoScreen != null && videoScreen.activeSelf != v) videoScreen.SetActive(v);
        if (videoControls != null && videoControls.activeSelf != v) videoControls.SetActive(v);
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
            SetMode(playing || (!idle && _video));
            return;
        }
        if (playing && !_wasPlaying) SetMode(true);   // だれかが再生した・再開した
        if (idle && !_wasIdle) SetMode(false);        // 停止・おわり・動画なし
        _wasPlaying = playing; _wasIdle = idle;
        ApplyTitle();   // 言語の切りかえで見出しが書きかわっても、今のモードの見出しに戻す
    }
}
