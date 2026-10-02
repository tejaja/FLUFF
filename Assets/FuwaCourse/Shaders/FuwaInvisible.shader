// 何も描かないシェーダー。物体は「ある」まま（掴める・大きさもそのまま）見た目だけ消したい時に使う。
Shader "FuwaCourse/Invisible"
{
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Pass
        {
            ColorMask 0
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata_base v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target { return 0; }
            ENDCG
        }
    }
}
