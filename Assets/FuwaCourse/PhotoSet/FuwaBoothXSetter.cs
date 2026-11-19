using UnityEngine;

// 撮影セット（EditorOnly）の Vket ブースの景色用（2026-11-19 てじゃ「タンポポの揺れのむきもへんだ」）。
// ブース版シェーダー（Assets/2562）は「ブースの +X」を合体メッシュの接線から取るが、撮影セットは合体前の元のメッシュなので、
// この物の +X（＝撮影セットの Booth の向き）を全体の _FuwaPhotoBoothX に入れて、揺れる向きをブースとそろえる（ふきながしの向きと一致を確認）
[ExecuteAlways]
public class FuwaBoothXSetter : MonoBehaviour
{
    void OnEnable() { Apply(); }
    void Update() { Apply(); }
    void OnDisable() { Shader.SetGlobalVector("_FuwaPhotoBoothX", Vector4.zero); Shader.SetGlobalVector("_FuwaBoothX", Vector4.zero); }
    void Apply()
    {
        Vector3 x = transform.right; x.y = 0f;
        if (x.sqrMagnitude < 1e-6f) return;
        x.Normalize();
        Shader.SetGlobalVector("_FuwaPhotoBoothX", new Vector4(x.x, 0f, x.z, 1f));
        // 太陽の向きもブースの向きに合わせる（会場でブースを回した時と同じ仕組み）
        Shader.SetGlobalVector("_FuwaBoothX", new Vector4(x.x, 0f, x.z, 0f));
    }
}
