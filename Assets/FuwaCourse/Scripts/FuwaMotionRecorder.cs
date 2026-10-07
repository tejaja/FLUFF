using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

// お手本アニメ用のモーション録画（開発用・2026-10-07）。公開アップロードの前に外すこと。
// 録画中は一定間隔で「自分のアバターの骨の位置・頭の向き・両手のトラッキング・銃・ふわふわ」を Debug.Log で VRChat のログ（output_log）に書き出す。
// 骨は向きではなく位置で録る（アバターごとに骨の軸の決まりが違うため。位置なら別の人形にも移せる）。
// Unity側の「FuwaCourse/モーション録画/ログから取り込む」でアニメに変換する。
// 開始/停止：このボタンをInteract、デスクトップは toggleKey、VRは両手を頭の上に上げて1秒。開始はカウントダウン付き。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaMotionRecorder : UdonSharpBehaviour
{
    [Tooltip("自分の銃とふわふわを見つけるため")]
    public FuwaRespawnManager manager;
    [Tooltip("ボタンの上の表示")]
    public TextMeshPro panelText;
    [Tooltip("視界の上に出す表示（カウントダウン・録画中）")]
    public TextMeshPro headText;

    [Tooltip("1秒あたりの記録回数")]
    public float sampleRate = 30f;
    [Tooltip("開始までのカウントダウン(秒)")]
    public float countdown = 3f;
    [Tooltip("これを超えたら自動で止める(秒)")]
    public float maxSeconds = 90f;
    [Tooltip("デスクトップで開始/停止するキー")]
    public KeyCode toggleKey = KeyCode.Alpha0;
    [Tooltip("VR：両手を頭の上に上げ続けたら開始/停止(秒)")]
    public float gestureHold = 1f;

    private const string Tag = "[FMR] ";
    private int[] _bones;
    private string[] _parts;
    private int _state;           // 0=待機 1=カウントダウン 2=録画中
    private float _stateAt;
    private float _nextSample;
    private int _take;
    private int _frames;
    private float _gestureSince = -1f;
    private bool _gestureLatch;
    private VRCPickup _gunPickup;
    private FuwaBlower _gun;

    private void Start()
    {
        // HumanBodyBones の番号（Udonでは HumanBodyBones の配列が使えないので int で持つ）
        // 腰・背骨・胸・上胸・首・頭 / 左肩・左上腕・左前腕・左手 / 右も同じ / 左もも・左すね・左足・左つま先 / 右も同じ
        // 手の向き用に 左右の人差し指・中指・小指の付け根
        _bones = new int[]
        {
            0, 7, 8, 54, 9, 10,
            11, 13, 15, 17,
            12, 14, 16, 18,
            1, 3, 5, 19,
            2, 4, 6, 20,
            27, 30, 36,
            42, 45, 51,
        };
        if (headText != null) headText.gameObject.SetActive(false);
        SetPanel("モーション録画\n<size=60%>押す / [0]キー / 両手を頭の上に1秒</size>");
    }

    public override void Interact()
    {
        Toggle("button");
    }

    private void Toggle(string why)
    {
        if (_state == 0) { _state = 1; _stateAt = Time.time; }
        else if (_state == 1) { _state = 0; SetPanel("キャンセル"); ShowHead(false); }
        else Stop(why);
    }

    private void Update()
    {
        var me = Networking.LocalPlayer;
        if (me == null) return;

        if (Input.GetKeyDown(toggleKey)) Toggle("key");
        UpdateGesture(me);

        if (_state == 1)
        {
            float left = countdown - (Time.time - _stateAt);
            if (left <= 0f) Begin(me);
            else
            {
                string s = Mathf.CeilToInt(left).ToString();
                SetPanel("録画まで " + s);
                SetHead("<color=#ffcc33>" + s + "</color>");
            }
        }
        else if (_state == 2)
        {
            float t = Time.time - _stateAt;
            if (t >= maxSeconds) { Stop("max"); return; }
            if (Time.time >= _nextSample)
            {
                _nextSample += 1f / sampleRate;
                if (_nextSample < Time.time) _nextSample = Time.time + 1f / sampleRate;
                Sample(me, t);
            }
            string sec = Mathf.FloorToInt(t).ToString();
            SetHead("<color=#ff4040>●</color> REC " + sec);
            SetPanel("録画中 " + sec + "秒");
        }
    }

    // VR：両手が頭より上に gestureHold 秒あったら切り替え（一度切り替えたら手を下ろすまで再反応しない）
    private void UpdateGesture(VRCPlayerApi me)
    {
        if (!me.IsUserInVR()) return;
        float hy = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position.y;
        bool up = me.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position.y > hy + 0.08f
               && me.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position.y > hy + 0.08f;
        if (!up) { _gestureSince = -1f; _gestureLatch = false; return; }
        if (_gestureLatch) return;
        if (_gestureSince < 0f) _gestureSince = Time.time;
        else if (Time.time - _gestureSince >= gestureHold) { _gestureLatch = true; Toggle("gesture"); }
    }

    private void Begin(VRCPlayerApi me)
    {
        _state = 2;
        _stateAt = Time.time;
        _nextSample = Time.time;
        _frames = 0;
        _take++;
        _gun = manager != null ? manager.GetMyBlower() : null;
        _gunPickup = _gun != null ? (VRCPickup)_gun.GetComponent(typeof(VRCPickup)) : null;

        // 骨の名前の並び（フレームの数字の順番）
        string names = "";
        for (int i = 0; i < _bones.Length; i++) names += (i == 0 ? "" : ",") + _bones[i];
        Debug.Log(Tag + "B take=" + _take + " rate=" + sampleRate + " eye=" + Mm(me.GetAvatarEyeHeightAsMeters())
            + " vr=" + (me.IsUserInVR() ? 1 : 0) + " bones=" + names);
        Debug.Log(Tag + "L fields=t|bones(pos mm)|head(pos,rot)|lhand(pos,rot)|rhand(pos,rot)|gun(held,hand,pos,rot,scale,charging,chargeStart,lastShot)|ball(pos,rot)");
        if (_parts == null || _parts.Length != 2 + _bones.Length * 3 + 7 * 3 + 2 + 7 + 4 + 7)
            _parts = new string[2 + _bones.Length * 3 + 7 * 3 + 2 + 7 + 4 + 7];
    }

    private void Stop(string why)
    {
        float t = Time.time - _stateAt;
        Debug.Log(Tag + "E take=" + _take + " frames=" + _frames + " len=" + Mm(t) + " why=" + why);
        _state = 0;
        SetPanel("録画した：テイク" + _take + "（" + Mathf.RoundToInt(t) + "秒）\n<size=60%>押す / [0]キー / 両手を頭の上に1秒</size>");
        ShowHead(false);
    }

    private void Sample(VRCPlayerApi me, float t)
    {
        int k = 0;
        _parts[k++] = "F";
        _parts[k++] = Mathf.RoundToInt(t * 1000f).ToString();
        for (int i = 0; i < _bones.Length; i++) k = PutPos(k, me.GetBonePosition((HumanBodyBones)_bones[i]));
        var h = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        k = PutPos(k, h.position); k = PutRot(k, h.rotation);
        var l = me.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand);
        k = PutPos(k, l.position); k = PutRot(k, l.rotation);
        var r = me.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand);
        k = PutPos(k, r.position); k = PutRot(k, r.rotation);

        bool held = _gunPickup != null && _gunPickup.IsHeld;
        _parts[k++] = held ? "1" : "0";
        _parts[k++] = held ? ((int)_gunPickup.currentHand).ToString() : "0";
        if (_gun != null)
        {
            // 見た目の部分（visualRoot）を録る。デスクトップでは本体ではなく見た目だけが手のひら＋視線の向きに動かされるため
            var gt = _gun.visualRoot != null ? _gun.visualRoot.transform : _gun.transform;
            k = PutPos(k, gt.position); k = PutRot(k, gt.rotation);
            _parts[k++] = Mm(gt.lossyScale.x);
            object c = _gun.GetProgramVariable("_charging");
            object cs = _gun.GetProgramVariable("_chargeStart");
            object ls = _gun.GetProgramVariable("_lastShotTime");
            _parts[k++] = (c != null && (bool)c) ? "1" : "0";
            _parts[k++] = cs != null ? Mm((float)cs - _stateAt) : "0";
            _parts[k++] = ls != null ? Mm((float)ls - _stateAt) : "0";
        }
        else
        {
            for (int i = 0; i < 11; i++) _parts[k++] = "0";
        }
        Transform b = (manager != null && manager.ball != null) ? manager.ball.transform : null;
        if (b != null) { k = PutPos(k, b.position); k = PutRot(k, b.rotation); }
        else { for (int i = 0; i < 7; i++) _parts[k++] = "0"; }

        Debug.Log(Tag + string.Join(" ", _parts));
        _frames++;
    }

    private int PutPos(int k, Vector3 p)
    {
        _parts[k] = Mm(p.x); _parts[k + 1] = Mm(p.y); _parts[k + 2] = Mm(p.z);
        return k + 3;
    }

    private int PutRot(int k, Quaternion q)
    {
        _parts[k] = Mathf.RoundToInt(q.x * 10000f).ToString();
        _parts[k + 1] = Mathf.RoundToInt(q.y * 10000f).ToString();
        _parts[k + 2] = Mathf.RoundToInt(q.z * 10000f).ToString();
        _parts[k + 3] = Mathf.RoundToInt(q.w * 10000f).ToString();
        return k + 4;
    }

    private string Mm(float v)
    {
        return Mathf.RoundToInt(v * 1000f).ToString();
    }

    public override void PostLateUpdate()
    {
        if (headText == null || !headText.gameObject.activeSelf) return;
        var me = Networking.LocalPlayer;
        if (me == null) return;
        var h = me.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        float s = Mathf.Max(0.3f, me.GetAvatarEyeHeightAsMeters() / 1.6f);
        // TextMeshPro の .transform はUdonで使えないので gameObject 経由
        var ht = headText.gameObject.transform;
        ht.SetPositionAndRotation(h.position + h.rotation * new Vector3(0f, 0.16f, 0.6f) * s, h.rotation);
        ht.localScale = Vector3.one * s;
    }

    private void SetPanel(string s)
    {
        if (panelText != null && panelText.text != s) panelText.text = s;
    }

    private void SetHead(string s)
    {
        if (headText == null) return;
        ShowHead(true);
        if (headText.text != s) headText.text = s;
    }

    private void ShowHead(bool on)
    {
        if (headText != null && headText.gameObject.activeSelf != on) headText.gameObject.SetActive(on);
    }
}
