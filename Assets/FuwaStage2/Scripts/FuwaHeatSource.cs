using UdonSharp;
using UnityEngine;

// かがり火の熱の範囲（トリガー）：ふわふわが入ると FuwaHeat を満タンに（2026-10-28 試作）
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class FuwaHeatSource : UdonSharpBehaviour
{
    public FuwaHeat heat;
    [Tooltip("温まった時の音（なくてもOK）")]
    public AudioSource heatSound;
    private float _lastSound = -10f;

    private void OnTriggerEnter(Collider other) { Touch(other, true); }
    private void OnTriggerStay(Collider other) { Touch(other, false); }

    private void Touch(Collider other, bool enter)
    {
        if (heat == null || other == null) return;
        if (other.GetComponent<FuwaBall>() == null) return;
        heat.Heat();
        if (enter && heatSound != null && heatSound.clip != null && Time.time - _lastSound > 0.5f) { _lastSound = Time.time; heatSound.PlayOneShot(heatSound.clip); }
    }
}
