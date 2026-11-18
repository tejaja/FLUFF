using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

// 2026-11-19 てじゃ「金ピカフワフワはやめて、ピックアップできるただのフワフワにしよう。USE で表情かえてポヨンってなるだけ。撮影用。離すと空中に止まるタイプ」
//  おまけ本島の撮影用ふわふわ。持って USE で表情を切りかえる（ふつう→＞＜→こまり顔→…）＋ぽよん。位置（VRCObjectSync）と表情はみんなに同期。
//  同期する物は隠れるエリアに置けないので FuwaSystem に置き、本島にいない間は見た目・当たり・持つのを止める（FuwaAreaShow と同じ）
[UdonBehaviourSyncMode(BehaviourSyncMode.Continuous)]
public class FuwaPhotoFluff : UdonSharpBehaviour
{
    public Renderer fur;
    public VRCPickup pickup;
    public Collider[] colliders;
    public FuwaAreaVisibility areaVisibility;
    public int area = 4;
    [Tooltip("表情の数（毛の材質の _FaceHit：0 ふつう／1 ＞＜／2 こまり顔）")]
    public int faceCount = 3;
    public float bounceTime = 0.5f, bounceAmount = 0.18f;
    public AudioSource boing;

    [UdonSynced] private int _face;
    private int _faceShown = -1;
    private Material _mat;
    private Vector3 _baseScale;
    private float _bounceAt = -10f;
    private int _shown = -1;
    private float _next;

    private void Start()
    {
        _baseScale = transform.localScale;
        if (fur != null) _mat = fur.material;
        ApplyFace();
        ApplyArea(false); _shown = 0;
    }

    public override void OnPickupUseDown()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        _face = (_face + 1) % Mathf.Max(1, faceCount);
        RequestSerialization();
        ApplyFace();
        Bounce();
    }

    public override void OnDeserialization()
    {
        if (_face != _faceShown) { ApplyFace(); Bounce(); }
    }

    private void ApplyFace()
    {
        _faceShown = _face;
        if (_mat != null) _mat.SetFloat("_FaceHit", _face);
    }

    private void Bounce()
    {
        _bounceAt = Time.time;
        if (boing != null && boing.gameObject.activeInHierarchy) { boing.pitch = Random.Range(0.95f, 1.08f); boing.Play(); }
    }

    private void Update()
    {
        // ぽよん：縦につぶれて横にふくらみ、ゆれながら戻る
        float u = (Time.time - _bounceAt) / Mathf.Max(0.05f, bounceTime);
        if (u >= 0f && u < 1f)
        {
            float w = -bounceAmount * Mathf.Sin(u * Mathf.PI * 3f) * (1f - u);
            transform.localScale = Vector3.Scale(_baseScale, new Vector3(1f - w * 0.5f, 1f + w, 1f - w * 0.5f));
        }
        else if (u >= 1f && _bounceAt > 0f) { transform.localScale = _baseScale; _bounceAt = -10f; }

        if (Time.time < _next) return;
        _next = Time.time + 0.25f;
        bool on = areaVisibility != null && areaVisibility.GetArea() == area;
        int s = on ? 1 : 0;
        if (s == _shown) return;
        _shown = s;
        ApplyArea(on);
    }

    private void ApplyArea(bool on)
    {
        if (fur != null) fur.enabled = on;
        if (colliders != null) for (int i = 0; i < colliders.Length; i++) if (colliders[i] != null) colliders[i].enabled = on;
        if (pickup != null) { if (!on) pickup.Drop(); pickup.pickupable = on; }
    }
}
