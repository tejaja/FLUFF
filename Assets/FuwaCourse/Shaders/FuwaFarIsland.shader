// 遠景の浮島：頂点カラー＋やわらかい陰影。カメラから遠いほど空の色(_Haze)にかすむ。
// 太陽の色・明るさについてくる（やみのもりでは暗い紫に）。滝の帯が裏から見えるように両面。
Shader "FuwaCourse/FarIsland"
{
    Properties
    {
        _Haze ("Haze Color", Color) = (0.62, 0.83, 1, 1)
        _HazeStart ("Haze Start (m)", Float) = 150
        _HazeEnd ("Haze End (m)", Float) = 900
        _HazeMax ("Haze Max", Range(0, 1)) = 0.75
        _Shade ("Shade Strength", Range(0, 1)) = 0.35
        _FallSpeed ("Water Flow Speed", Float) = 0.9
        _FallLen ("Waterfall Length (uv)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Haze, _LightColor0;
            float _HazeStart, _HazeEnd, _HazeMax, _Shade;

            float _FallSpeed, _FallLen;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float3 wn : TEXCOORD0; float3 wp : TEXCOORD1; float2 uv : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                float3 n = normalize(i.wn) * (facing > 0 ? 1 : -1);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float d = saturate(dot(n, l) * 0.5 + 0.5);
                float3 col = i.color.rgb * lerp(1 - _Shade, 1, d);
                // 水（頂点アルファ 0＝滝 / 0.5＝上の川）：すじ模様が流れる。滝の下はちぎれて消える
                if (i.color.a < 0.75)
                {
                    bool fall = i.color.a < 0.25;
                    float2 uv = i.uv;
                    float speed = fall ? _FallSpeed : _FallSpeed * 0.35;
                    float streak = vnoise(float2(uv.x * 6, uv.y * 2.4 - _Time.y * speed));
                    streak = streak * 0.6 + vnoise(float2(uv.x * 13 + 7, uv.y * 5 - _Time.y * speed * 1.3)) * 0.4;
                    col = lerp(i.color.rgb * 0.82, float3(1, 1, 1), smoothstep(0.45, 0.8, streak));
                    // 両わきを少し白く（しぶき）
                    col = lerp(col, float3(1, 1, 1), smoothstep(0.3, 0.5, abs(uv.x - 0.5)) * 0.35);
                    if (fall)
                    {
                        float t = uv.y / _FallLen;   // 0＝ふち 1＝いちばん下
                        float fray = vnoise(float2(uv.x * 8, uv.y * 6 - _Time.y * speed * 1.1));
                        clip(fray - smoothstep(0.45, 1.0, t) * 1.05);
                        col = lerp(col, float3(1, 1, 1), smoothstep(0.5, 1.0, t) * 0.5);
                    }
                }
                float3 tint = saturate(_LightColor0.rgb * 0.85 + 0.15);
                col *= tint;
                float dist = distance(i.wp, _WorldSpaceCameraPos);
                float h = saturate((dist - _HazeStart) / max(1, _HazeEnd - _HazeStart)) * _HazeMax;
                col = lerp(col, _Haze.rgb * tint, h);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
