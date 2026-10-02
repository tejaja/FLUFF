// 近づくと見える「通れない壁」の目印（赤い斜めじま＋ふちの光）。
// カメラ（見ている人）との距離で濃さが変わる：_FadeFar より遠いと見えない、_FadeNear で最大。
Shader "FuwaCourse/ProximityBarrier"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.25, 0.25, 1)
        _MaxAlpha ("Max Alpha", Range(0, 1)) = 0.7
        _FadeNear ("Fade Near (m)", Float) = 1.2
        _FadeFar ("Fade Far (m)", Float) = 4.0
        _StripeWidth ("Stripe Width (m)", Float) = 0.18
        _StripeSpeed ("Stripe Speed", Float) = 0.25
        _Edge ("Edge Glow Width (uv)", Range(0, 0.5)) = 0.08
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
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
            float _MaxAlpha, _FadeNear, _FadeFar, _StripeWidth, _StripeSpeed, _Edge;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = distance(_WorldSpaceCameraPos, i.wpos);
                float near = 1.0 - saturate((d - _FadeNear) / max(0.01, _FadeFar - _FadeNear));
                if (near <= 0.001) discard;
                // 斜めじま（ワールド座標で幅が一定）
                float s = (i.wpos.x + i.wpos.y + i.wpos.z) / max(0.01, _StripeWidth) + _Time.y * _StripeSpeed * 4.0;
                float stripe = smoothstep(0.35, 0.5, abs(frac(s) - 0.5));
                // ふち（uvの端）を明るく
                float2 e = min(i.uv, 1.0 - i.uv);
                float edge = 1.0 - smoothstep(0.0, _Edge, min(e.x, e.y));
                float a = (0.25 + 0.55 * stripe + 0.6 * edge) * near * _MaxAlpha;
                return fixed4(_Color.rgb + edge * 0.25, saturate(a));
            }
            ENDCG
        }
    }
}
