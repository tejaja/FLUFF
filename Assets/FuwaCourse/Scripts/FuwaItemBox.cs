using UdonSharp;
using UnityEngine;

// アイテムボックス（？の胞子カプセル）。自分のふわふわが触るとアイテムがもらえる。
// 取ったボックスは自分の画面でだけ少しの間消える（他の人は取れる）。
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaItemBox : UdonSharpBehaviour
{
    public FuwaItems items;
    [Tooltip("見た目（消したり回したりする）")]
    public Transform visual;
    public float respawnSeconds = 3f;
    public float spinSpeed = 60f;
    public float bobHeight = 0.08f;

    private float _hiddenUntil;
    private Vector3 _basePos;

    private void Start()
    {
        if (visual != null) _basePos = visual.localPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time < _hiddenUntil || items == null || other == null) return;
        FuwaBall ball = other.GetComponent<FuwaBall>();
        if (ball == null || !items.CanReceive()) return;
        items.GiveRandom();
        _hiddenUntil = Time.time + respawnSeconds;
        if (visual != null) visual.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (visual == null) return;
        bool show = Time.time >= _hiddenUntil;
        if (visual.gameObject.activeSelf != show) visual.gameObject.SetActive(show);
        if (!show) return;
        visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        visual.localPosition = _basePos + Vector3.up * Mathf.Sin(Time.time * 2f + _basePos.x) * bobHeight;
    }
}
