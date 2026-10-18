Shader "Hidden/FuwaPhotoDepth"
{
    // 撮影セットのぼかし用（FuwaPhotoSet）：カメラからの奥行き（ビュー空間の z、m）を色に書く置きかえシェーダー。
    // 不透明と、抜き（_MainTex のアルファ×_Cutoff）だけ。半透明（粒・エフェクト）は書かない＝後ろの物の奥行きになる
    Properties { _MainTex ("", 2D) = "white" {} _Cutoff ("", Float) = 0.5 }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex; float4 _MainTex_ST; float _Cutoff;
    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float z : TEXCOORD1; };
    v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.texcoord, _MainTex); o.z = -UnityObjectToViewPos(v.vertex).z; return o; }
    float4 fragOpaque (v2f i) : SV_Target { return i.z; }
    float4 fragCut (v2f i) : SV_Target { clip(tex2D(_MainTex, i.uv).a - _Cutoff); return i.z; }
    ENDCG
    SubShader { Tags { "RenderType"="Opaque" } Pass { CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragOpaque
        ENDCG } }
    SubShader { Tags { "RenderType"="TransparentCutout" } Pass { Cull Off CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragCut
        ENDCG } }
}
