// 蒸気の穴の奥がほんのり光る（地熱っぽく）。穴の口に寝かせた板に、中心ほど明るい丸を足し算で描き、ゆっくり脈打たせる
Shader "FuwaCourse/VentGlow"
{
    Properties
    {
        _Color ("Glow Color", Color) = (1, 0.55, 0.2, 1)
        _Intensity ("Intensity", Float) = 0.6
        _PulseSpeed ("Pulse Speed", Float) = 1.6
        _PulseAmount ("Pulse Amount (0-1)", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Intensity, _PulseSpeed, _PulseAmount;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float phase : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                // 穴ごとに脈打つタイミングをずらす（置いた場所から決める）
                float3 c = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                o.phase = frac(sin(dot(c.xz, float2(12.9898, 78.233))) * 43758.5453) * 6.2832;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv - 0.5) * 2;
                float g = saturate(1 - r);
                g = g * g;
                float pulse = 1 - _PulseAmount * (0.5 + 0.5 * sin(_Time.y * _PulseSpeed + i.phase));
                return fixed4(_Color.rgb * g * _Intensity * pulse, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
