using UdonSharp;
using UnityEngine;

// 蓄音機のボタン（◀ ▶ ・レバー）：クリックで FuwaGramophone にイベントを送る。押すと少しへこむ
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaGramophoneButton : UdonSharpBehaviour
{
    public FuwaGramophone target;
    public string eventName = "Next";
    public Transform pressPart;
    public Vector3 pressOffset = new Vector3(0f, 0f, 0.012f);
    private Vector3 _base;
    private float _press;

    private void Start() { if (pressPart != null) _base = pressPart.localPosition; }

    public override void Interact()
    {
        if (target != null) target.SendCustomEvent(eventName);
        _press = 1f;
    }

    private void Update()
    {
        if (pressPart == null || _press <= 0f) return;
        _press = Mathf.Max(0f, _press - Time.deltaTime * 5f);
        pressPart.localPosition = _base + pressOffset * Mathf.Sin(_press * Mathf.PI);
    }
}
