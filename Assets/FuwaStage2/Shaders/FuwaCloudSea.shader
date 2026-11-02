// ステージ2の雲海（2026-11-02 てじゃ「雲、ちょっとうねうねさせたりできないかな」）。CloudPuff の色の付け方に、頂点でゆっくりうねる波を足した物。
// 波はワールド座標の sin の重ね。法線も同じ波の傾きぶん倒して、陰影も一緒に動く。カメラから遠いと揺れを弱める
Shader "FuwaCourse/CloudSea"
{
    Properties
    {
        _Color ("Lit Color", Color) = (1, 1, 1, 1)
        _Shade ("Shade Color", Color) = (0.55, 0.72, 0.95, 1)
        _Rim ("Rim", Range(0, 1)) = 0.12
        _ShadeContrast ("陰影のはっきりさ", Range(1, 12)) = 5
        _WaveHeight ("うねりの高さ(m)", Float) = 7
        _WaveLength ("うねりの長さ(m)", Float) = 150
        _WaveSpeed ("うねりの速さ", Float) = 0.28
        _Haze ("Haze Color", Color) = (0.6, 0.83, 1, 1)
        _HazeStart ("Haze Start (m)", Float) = 180
        _HazeEnd ("Haze End (m)", Float) = 1000
        _HazeMax ("Haze Max", Range(0, 1)) = 0.9
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color, _Shade, _LightColor0, _Haze;
            float _UdonSkyDim;
            float _Rim, _ShadeContrast, _WaveHeight, _WaveLength, _WaveSpeed, _HazeStart, _HazeEnd, _HazeMax;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wn : TEXCOORD0; float3 wv : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            // 方向の違う 3 つの波。戻り値 x が高さ、yz が xz 方向の傾き
            float3 Wave(float2 p, float t)
            {
                float k = 6.2831853 / _WaveLength;
                float a1 = dot(p, float2(0.8, 0.6)) * k + t;
                float a2 = dot(p, float2(-0.5, 0.86)) * k * 1.7 - t * 1.3;
                float a3 = dot(p, float2(0.2, -0.98)) * k * 2.9 + t * 0.7;
                float h = sin(a1) + 0.55 * sin(a2) + 0.25 * sin(a3);
                float2 d = float2(0.8, 0.6) * k * cos(a1) + float2(-0.5, 0.86) * k * 1.7 * 0.55 * cos(a2) + float2(0.2, -0.98) * k * 2.9 * 0.25 * cos(a3);
                return float3(h, d) * _WaveHeight;
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 w = Wave(wp.xz, _Time.y * _WaveSpeed);
                wp.y += w.x;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                float3 n = UnityObjectToWorldNormal(v.normal);
                n.xz -= w.yz;
                o.wn = n;
                o.wv = _WorldSpaceCameraPos - wp;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float3 n = normalize(i.wn);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float d = pow(saturate((dot(n, l) + 0.5) / 1.5), _ShadeContrast);
                float3 col = lerp(_Shade.rgb, _Color.rgb, d);
                col += pow(1 - saturate(dot(n, normalize(i.wv))), 3) * _Rim;
                float3 tint = saturate(_LightColor0.rgb * 0.85 + 0.15);
                col *= tint;
                float h = saturate((length(i.wv) - _HazeStart) / max(1, _HazeEnd - _HazeStart)) * _HazeMax;
                col = lerp(col, _Haze.rgb * tint, h);
                col = lerp(col, saturate(col) * float3(0.11, 0.11, 0.2), saturate(_UdonSkyDim));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
