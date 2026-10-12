// いわのみちのまわりの浮島の温泉のお湯（2026-10-12 てじゃ「浮島に噴気孔とか、湯気でてる温泉とか」）
// ・まん中ほど濃い水色、ふちへいくほど明るい水色。ふちに白っぽい泡の線
// ・ゆっくり外へ広がる波紋と、ちらちら光るきらめき
// メッシュの uv.x＝まん中0〜ふち1、uv.y＝ぐるっと一周の位置（0〜1）。光は当てない（いつも明るいお湯）
Shader "FuwaCourse/HotSpring"
{
    Properties
    {
        _Deep ("Center Color", Color) = (0.30, 0.78, 0.86, 1)
        _Shallow ("Edge Color", Color) = (0.62, 0.93, 0.92, 1)
        _Foam ("Foam Color", Color) = (0.95, 1, 0.98, 1)
        _FoamWidth ("Foam Width (0-1)", Range(0, 0.4)) = 0.1
        _RippleFreq ("Ripple Count", Float) = 5
        _RippleSpeed ("Ripple Speed", Float) = 0.25
        _RippleStrength ("Ripple Strength", Range(0, 1)) = 0.35
        _Sparkle ("Sparkle", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _Deep, _Shallow, _Foam;
            float _FoamWidth, _RippleFreq, _RippleSpeed, _RippleStrength, _Sparkle;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; UNITY_FOG_COORDS(2) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = saturate(i.uv.x);
                float3 col = lerp(_Deep.rgb, _Shallow.rgb, smoothstep(0.0, 1.0, r));
                // 波紋：外へゆっくり広がる細い明るい輪（ふちに近いほど薄く）
                float w = frac(r * _RippleFreq - _Time.y * _RippleSpeed);
                float ring = smoothstep(0.0, 0.08, w) * (1 - smoothstep(0.08, 0.22, w));
                col = lerp(col, _Foam.rgb, ring * _RippleStrength * (1 - r * 0.6));
                // きらめき：ワールド座標の細かいマスごとに、たまにぴかっと
                float2 cell = floor(i.wp.xz * 6.0);
                float h = Hash(cell);
                float tw = saturate(sin(_Time.y * (1.5 + h * 2.0) + h * 40.0) * 8.0 - 7.0);
                float2 f = frac(i.wp.xz * 6.0) - 0.5;
                float dot_ = 1 - smoothstep(0.05, 0.2, length(f));
                col = lerp(col, _Foam.rgb, tw * dot_ * _Sparkle * step(0.6, h));
                // ふちの泡（ぐるっと一周で太さが少し変わる）
                float fw = _FoamWidth * (0.75 + 0.25 * sin(i.uv.y * 6.2832 * 7 + _Time.y * 0.8));
                float foam = smoothstep(1 - fw, 1 - fw * 0.4, r);
                col = lerp(col, _Foam.rgb, foam);
                fixed4 c = fixed4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
