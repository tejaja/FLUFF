// 2026-11-15 2–5 クリスタルのため（てじゃ「クリスタル本体にもエネルギーためてる感じ」）：クリスタルのまわりのやわらかい光（足し算）
//  球にかける。正面ほど明るく、ふちへ消える。_Alpha（スクリプト）で明るさ、_Flicker でちらつき
Shader "Fuwa/EnergyGlow"
{
    Properties
    {
        _Color ("色", Color) = (0.45, 0.9, 1, 1)
        _Pow ("ふちへの消え方", Range(0.5, 8)) = 2.2
        _Alpha ("明るさ（スクリプト）", Range(0, 2)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "IgnoreProjector"="True" }
        Pass
        {
            Cull Back ZWrite Off Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half _Pow;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Alpha)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wn : TEXCOORD0; float3 wp : TEXCOORD1; float3 on : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.on = normalize(v.vertex.xyz);
                return o;
            }
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float4 col = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float a = UNITY_ACCESS_INSTANCED_PROP(Props, _Alpha);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                float f = pow(saturate(dot(normalize(i.wn), v)), _Pow);
                // ゆらぐすじ（エネルギーが流れこむ感じ）
                float sw = 0.8 + 0.2 * sin(i.on.y * 14 - _Time.y * 9 + sin(atan2(i.on.z, i.on.x) * 5) * 2);
                return half4(col.rgb * f * sw * a, 0);
            }
            ENDCG
        }
    }
    FallBack Off
}
