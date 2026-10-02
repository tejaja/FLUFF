// ふわふわの足元の影（本物の影の代わりに床に描くぼかした丸＝ブロブシャドウ）。洞窟の中の暗い所でも出る。
// _Alpha はスクリプトから（高いほど薄く）。
Shader "FuwaCourse/GroundMarker"
{
    Properties
    {
        _Color ("Color", Color) = (0.12, 0.08, 0.2, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.6
        _RingWidth ("Ring Width (uv)", Range(0.01, 0.5)) = 0.12
        _Fill ("Inner Fill", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "Queue"="Transparent+5" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -2, -2

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Alpha, _RingWidth, _Fill;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv - 0.5) * 2.0;            // 0=中心 1=ふち
                if (r > 1.0) discard;
                // 中心は濃く、ふちに向かってなめらかに薄く
                float a = (1.0 - smoothstep(0.35, 1.0, r)) * _Alpha;
                return fixed4(_Color.rgb, a);
            }
            ENDCG
        }
    }
}
