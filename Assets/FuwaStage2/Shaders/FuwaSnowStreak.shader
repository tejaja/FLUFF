// 氷の上のスケートの跡（2–3 最初の島。2026-11-24 てじゃ「流れのスジつけて誘導」→「スケートの跡」）。動きなし。
// メッシュ側：uv.x＝線の頭からの長さ(m)、uv.y＝横 0〜1、uv2.x＝線ごとのずらし、color.a＝はしの薄まり。
Shader "FuwaCourse/SnowStreak"
{
    Properties
    {
        _Color ("色", Color) = (0.97, 0.99, 1, 1)
        _Base ("線の濃さ", Range(0, 1)) = 0.7
        _Grain ("削れたざらつき", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -2, -2

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Base, _Grain;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 uv : TEXCOORD0; fixed edge : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = float3(v.uv, v.uv2.x);
                o.edge = v.color.a;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float hash11(float p) { p = frac(p * 0.1031); p *= p + 33.33; p *= p + p; return frac(p); }

            fixed4 frag (v2f i) : SV_Target
            {
                // 横：まん中が濃くて、ふちはふわっと消える
                float across = abs(i.uv.y * 2 - 1);
                float side = 1 - smoothstep(0.15, 1.0, across);
                // 削れたムラ：長さ方向の細かい濃い薄い（止まっている）
                float gx = floor(i.uv.x * 9 + i.uv.z * 57);
                float gy = floor(i.uv.y * 3);
                float grain = lerp(1, 0.45 + 0.9 * hash11(gx * 7.13 + gy * 19.7), _Grain);
                float a = _Base * side * i.edge * grain;
                fixed4 c = fixed4(_Color.rgb, saturate(a) * _Color.a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
