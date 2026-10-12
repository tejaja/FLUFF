using UdonSharp;
using UnityEngine;

// チルルームのスイッチ（FuwaChillSpawner の設定を1つ進める）。kind：0=サイズ 1=カラー 2=上限数 3=地面に落ちたら消えるか
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaChillButton : UdonSharpBehaviour
{
    public FuwaChillSpawner spawner;
    public int kind;
    public Transform press;   // 押した時にへこませる物（なくてもOK）
    private float _pressAt = -1f;
    private Vector3 _pressPos;

    private void Start() { if (press != null) _pressPos = press.localPosition; }

    public override void Interact()
    {
        if (spawner == null) return;
        if (kind == 0) spawner.NextSize();
        else if (kind == 1) spawner.NextColor();
        else if (kind == 2) spawner.NextLimit();
        else spawner.TogglePop();
        _pressAt = Time.time;
    }

    private void Update()
    {
        if (_pressAt < 0f || press == null) return;
        float t = (Time.time - _pressAt) / 0.25f;
        if (t >= 1f) { press.localPosition = _pressPos; _pressAt = -1f; return; }
        press.localPosition = _pressPos + Vector3.down * 0.03f * Mathf.Sin(t * Mathf.PI);
    }
}
