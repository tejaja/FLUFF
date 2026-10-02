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
        _Bob ("Bob Height (m, 0=off)", Float) = 0
        _BobSpeed ("Bob Speed", Float) = 0.25
        _FallSpeed ("Water Flow Speed", Float) = 1.6
        _FallLen ("Waterfall Length (uv)", Float) = 1
    }
    SubShader
    {
        // 島ごとの原点でゆらすので、まとめ描き（バッチング）は禁止
        Tags { "RenderType"="Opaque" "DisableBatching"="True" }
        Cull Off
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Haze, _LightColor0;
            float _HazeStart, _HazeEnd, _HazeMax, _Shade;

            float _FallSpeed, _FallLen, _Bob, _BobSpeed;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float3 wn : TEXCOORD0; float3 wp : TEXCOORD1; float2 uv : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                // ほんのりふわふわ：島ごとにずれた周期で上下＋少し横に
                float3 origin = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float ph = dot(origin, float3(0.071, 0.0, 0.113));
                float t = _Time.y * _BobSpeed;
                wp += float3(sin(t * 0.6 + ph * 1.7) * 0.4, sin(t + ph), cos(t * 0.5 + ph * 2.3) * 0.4) * _Bob;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                o.color = v.color;
                o.uv = v.uv;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = wp;
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
                // 水（頂点アルファ 0）：川→ふち→滝の1本の帯。すじ模様が加速しながら流れ、下は白い霧になってちぎれて消える
                if (i.color.a < 0.75)
                {
                    float2 uv = i.uv;
                    float t = saturate(uv.y / _FallLen);   // 0＝ふち（川はマイナス→0） 1＝いちばん下
                    // 落ちるほど速くなる：流れの座標を下へ行くほど引きのばす（すじが縦に伸びて加速して見える）
                    float fy = uv.y < 0 ? uv.y : uv.y + uv.y * uv.y * 1.6;
                    float tm = _Time.y * _FallSpeed;
                    float streak = vnoise(float2(uv.x * 7, fy * 3.0 - tm));
                    streak = streak * 0.55 + vnoise(float2(uv.x * 16 + 7, fy * 6.5 - tm * 1.35)) * 0.45;
                    // 白いすじと青い地のコントラストを強めに
                    float3 deep = i.color.rgb * float3(0.62, 0.78, 0.92);
                    col = lerp(deep, float3(1, 1, 1), smoothstep(0.42, 0.7, streak));
                    // 横切る白い泡の帯が流れ落ちる
                    float band = vnoise(float2(uv.x * 2.5 + 3, fy * 1.6 - tm * 0.9));
                    col = lerp(col, float3(1, 1, 1), smoothstep(0.62, 0.8, band) * 0.6);
                    // 両わきは白いしぶき
                    col = lerp(col, float3(1, 1, 1), smoothstep(0.32, 0.5, abs(uv.x - 0.5)) * 0.45);
                    // 下の方：だんだん白い霧になって、ちぎれて消える
                    float fray = vnoise(float2(uv.x * 5 + 11, fy * 4 - tm * 1.2)) * 0.6 + vnoise(float2(uv.x * 11, fy * 9 - tm * 1.5)) * 0.4;
                    clip(fray - smoothstep(0.3, 1.0, t) * 1.0 - 0.001);
                    col = lerp(col, float3(1, 1, 1), smoothstep(0.25, 0.85, t) * 0.75);
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
