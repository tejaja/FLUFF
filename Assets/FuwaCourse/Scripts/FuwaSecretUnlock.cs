using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// ひみつの小島のきのこ（2026-10-16 てじゃ「おもちゃ全解放スイッチ、ウラワザみたいな感じでどっかに置いておくのもいい」）。
// ロビーの写真館のボードを突っ切って飛び降りた先の小島にある。クリックすると、あそびばのおもちゃを全部出す（コースをクリアした時と同じく、このインスタンスの全員に）。
// 押した人の画面だけ、キラキラと音と、きのこの上に浮かぶ文字（出ているかどうかは同期の FuwaPlaygroundSpawner が持つので、ここは同期なし）。
// もう全部出ていた時（全コースクリア後など）は「もう ぜんぶ でてるよ」（2026-10-16 てじゃ「全コースクリア後の人がおしたときによくわかんないことになる」）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaSecretUnlock : UdonSharpBehaviour
{
    public FuwaPlaygroundSpawner spawner;
    public FuwaLanguage language;
    [Tooltip("押した時に出す粒")]
    public ParticleSystem burst;
    [Tooltip("新しく出した時の音")]
    public AudioSource sound;
    [Tooltip("もう全部出ていた時の音")]
    public AudioSource soundAgain;
    [Tooltip("押した時にぽよんとはねる傘")]
    public Transform cap;
    [Tooltip("きのこの上に浮かぶ文字（ふだんは非表示）")]
    public TextMeshPro popup;
    [Tooltip("popup の物（TextMeshPro の transform は Udon から触れないので別に持つ）")]
    public Transform popupRoot;
    [Tooltip("文字の後ろの板（一緒に消す）")]
    public SpriteRenderer popupPanel;
    public float popupTime = 4f;
    public float popupRise = 0.25f;
    public int toyCount = 5;

    private float _boingT = -1f;
    private Vector3 _capScale;
    private float _popT = -1f;
    private Vector3 _popBase;
    private Color _popColor, _panelColor;

    private void Start()
    {
        if (cap != null) _capScale = cap.localScale;
        if (popup != null) _popColor = popup.color;
        if (popupRoot != null) { _popBase = popupRoot.localPosition; popupRoot.gameObject.SetActive(false); }
        if (popupPanel != null) _panelColor = popupPanel.color;
    }

    public override void Interact()
    {
        bool already = true;
        if (spawner != null) for (int i = 0; i < toyCount; i++) if (!spawner.Unlocked(i)) already = false;
        if (spawner != null) for (int i = 0; i < toyCount; i++) spawner.Unlock(i);
        bool en = language != null && language.IsEnglish();

        if (already)
        {
            ShowPopup(en ? "All playground toys\nare already out!" : "あそびばの おもちゃは\nもう ぜんぶ でてるよ");
            if (soundAgain != null) { soundAgain.Stop(); soundAgain.Play(); }
        }
        else
        {
            ShowPopup(en ? "All playground toys\nare out now!" : "あそびばの おもちゃが\nぜんぶ でたよ!");
            if (burst != null) burst.Play();
            if (sound != null) { sound.Stop(); sound.Play(); }
        }
        _boingT = 0f;
    }

    private void ShowPopup(string text)
    {
        if (popup == null || popupRoot == null) return;
        popup.text = text;
        popupRoot.gameObject.SetActive(true);
        _popT = 0f;
    }

    private void Update()
    {
        if (_boingT >= 0f && cap != null)
        {
            _boingT += Time.deltaTime;
            const float dur = 0.45f;
            if (_boingT >= dur) { cap.localScale = _capScale; _boingT = -1f; }
            else
            {
                // つぶれてからふくらんで戻る
                float s = Mathf.Sin(_boingT / dur * Mathf.PI * 2f) * Mathf.Exp(-_boingT * 5f) * 0.25f;
                cap.localScale = new Vector3(_capScale.x * (1f + s), _capScale.y * (1f - s), _capScale.z * (1f + s));
            }
        }

        if (_popT >= 0f && popup != null && popupRoot != null)
        {
            _popT += Time.deltaTime;
            if (_popT >= popupTime) { popupRoot.gameObject.SetActive(false); _popT = -1f; return; }
            float k = _popT / popupTime;
            // ふわっと出て（0.25秒で大きく→ちょうど）、ゆっくり上がり、最後の0.8秒で消える
            float pop = _popT < 0.25f ? Mathf.SmoothStep(0.6f, 1f, _popT / 0.25f) : 1f;
            popupRoot.localPosition = _popBase + Vector3.up * (popupRise * k);
            popupRoot.localScale = Vector3.one * pop;
            float fade = Mathf.Clamp01((popupTime - _popT) / 0.8f);
            Color c = _popColor; c.a *= fade; popup.color = c;
            if (popupPanel != null) { Color pc = _panelColor; pc.a *= fade; popupPanel.color = pc; }
            // 自分の方を向く（横だけ）
            VRCPlayerApi lp = Networking.LocalPlayer;
            if (lp != null && lp.IsValid())
            {
                Vector3 d = popupRoot.position - lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.0001f) popupRoot.rotation = Quaternion.LookRotation(d, Vector3.up);
            }
        }
    }
}
