using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// ロビーの「あそびかた」看板をたたむ（2026-10-12 てじゃ「常時展開やめて、デフォは『あそびかた』だけの細い状態、レーザーで押すと展開（アニメーションで伸びる）、戻すには右上に×」「デフォの位置は今の中央くらいで、上下に伸びる」）。
// たたんだ時：板（Panel・Border）は真ん中の細い帯、見出し（title）だけが帯のまん中に出る。
// 開くと：板が上下に伸びて、見出しは上のはしへ動き、本文（body、見出しの行も含む元の文字）がふわっと出る。最後に見出しは本文の見出しと入れ替わる。
// 自分の画面だけ（同期なし）。開く・閉じるはレーザーで押すボタン（Open / Close を呼ぶ）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaHowToFold : UdonSharpBehaviour
{
    public SpriteRenderer panel;
    public SpriteRenderer border;
    public TextMeshPro body;
    public TextMeshPro title;
    public Transform titleT;     // title の Transform（TMP からは transform を取れないので）
    public GameObject titleGo;
    public GameObject bodyGo;
    public FuwaLanguage language;
    [Tooltip("たたんだ時だけ出す物（開くボタン）")]
    public GameObject whenClosed;
    [Tooltip("開いた時だけ出す物（×ボタン）")]
    public GameObject whenOpen;
    [Tooltip("たたんだ時の板の高さ（Panel の size.y）")]
    public float closedHeight = 0.62f;
    [Tooltip("ふち（Border）の拡大率")]
    public float borderScale = 1.28f;
    [Tooltip("見出しの位置：たたんだ時（板のまん中）と開いた時（本文の見出しの所）。看板のローカルの y")]
    public float titleClosedY = 1.96f;
    public float titleOpenY = 3.556f;
    public float duration = 0.45f;
    [Tooltip("伸びきってから本文がふわっと出るまでの秒数")]
    public float fadeIn = 0.35f;
    [Tooltip("開いている時、看板（板のまん中）からこれより離れたら自動で閉じる（m、水平距離）。0 で閉じない")]
    public float autoCloseDistance = 8f;
    public string titleJa = "あそびかた";
    public string titleEn = "How to Play";

    private float _p;        // 0=たたむ 1=開く
    private float _target;
    private float _panelFull, _borderFull;
    private Color _bodyColor = Color.white;
    private bool _inited;
    private bool _shownEn;
    private float _openedAt = -10f;

    private void Start() { Init(); Apply(); }

    private void Init()
    {
        if (_inited) return;
        _inited = true;
        if (panel != null) _panelFull = panel.size.y;
        if (border != null) _borderFull = border.size.y;
        if (body != null) _bodyColor = body.color;
        _shownEn = !(language != null && language.IsEnglish());   // 最初の Update で必ず見出しを書く
    }

    // 看板ごと非表示になって（コースへ移動など）また出てきた時は、たたんだ状態から始める
    private void OnEnable()
    {
        if (!_inited) return;
        _target = 0f; _p = 0f;
        Apply();
    }

    public void Open() { Init(); _target = 1f; }
    public void Close() { Init(); _target = 0f; }

    private void Update()
    {
        Init();
        bool en = language != null && language.IsEnglish();
        if (title != null && en != _shownEn)
        {
            _shownEn = en;
            title.text = "<size=150%><color=#FF6B5C>" + (en ? titleEn : titleJa) + "</color></size>";
        }
        // 離れたら閉じる（2026-10-13 てじゃ「離れても閉じるようにしよう」）
        if (_target > 0.5f && autoCloseDistance > 0f && panel != null)
        {
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (lp != null)
            {
                Vector3 d = lp.GetPosition() - panel.transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > autoCloseDistance * autoCloseDistance) Close();
            }
        }
        bool fading = _target >= 1f && _p >= 1f && Time.time - _openedAt < fadeIn + 0.05f;
        if (_p == _target && !fading) return;
        float before = _p;
        _p = Mathf.MoveTowards(_p, _target, Time.deltaTime / Mathf.Max(0.01f, duration));
        if (before < 1f && _p >= 1f) _openedAt = Time.time;
        Apply();
    }

    private void Apply()
    {
        // ぽよんと伸びる（行き過ぎて少し戻る）
        float e = _p >= 1f ? 1f : (_p <= 0f ? 0f : 1f - Mathf.Pow(1f - _p, 3f) + 0.08f * Mathf.Sin(_p * Mathf.PI));
        float k = Mathf.Lerp(closedHeight / Mathf.Max(0.01f, _panelFull), 1f, e);
        if (panel != null) panel.size = new Vector2(panel.size.x, _panelFull * k);
        // ふちは板より一回り大きいだけ（ふちの太さを上下左右そろえる。比で縮めると上下だけ細くなって変な形だった。2026-10-12 てじゃ）
        if (border != null) border.size = new Vector2(border.size.x, _borderFull - _panelFull * (1f - k) / Mathf.Max(0.01f, borderScale));
        // 見出し：前半で上へ動く。本文：後半でふわっと出る（出きったら見出しは本文の見出しと入れ替わる）
        float mv = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_p / 0.85f));
        float fade = _target > 0.5f ? (_p >= 1f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.time - _openedAt) / fadeIn)) : 0f) : Mathf.Clamp01((_p - 0.7f) / 0.3f);   // 開く時は伸びきってからふわっと、閉じる時はすぐ消える
        if (title != null)
        {
            Vector3 lp = titleT.localPosition;
            titleT.localPosition = new Vector3(lp.x, Mathf.Lerp(titleClosedY, titleOpenY, mv), lp.z);
            bool showTitle = _p < 1f || Time.time - _openedAt < fadeIn;   // 本文の見出しが出きるまでは重ねておく
            if (titleGo.activeSelf != showTitle) titleGo.SetActive(showTitle);
        }
        if (body != null)
        {
            bool showBody = fade > 0.001f;
            if (bodyGo.activeSelf != showBody) bodyGo.SetActive(showBody);
            body.color = new Color(_bodyColor.r, _bodyColor.g, _bodyColor.b, _bodyColor.a * fade);
        }
        bool open = _p >= 1f, closed = _p <= 0f;
        if (whenOpen != null && whenOpen.activeSelf != open) whenOpen.SetActive(open);
        if (whenClosed != null && whenClosed.activeSelf != closed) whenClosed.SetActive(closed);
    }
}
