// 2026-11-15 2–5 ポータル：石の枠なしで「穴だけが浮いている」見た目（てじゃ「石の枠なくして、ポータル自体が浮いてる感じのほうが映えるかも」「ワープしてる感うすい」）
//  _Mode 0：穴のふちのまわりの帯（uv.x＝ふちに沿った位置 0〜1、uv.y＝0 穴のふち → 1 外）。細く明るい線＋外へ広がる光＋流れるすじ（炎のようなゆらぎ）
//  _Mode 1：穴の下の地面の光のたまり（uv 0〜1 の板、まん中が明るい）＋ゆっくり広がる波紋
//  どちらも加算（Blend One One）、両面
Shader "Fuwa/PortalRim"
{
    Properties
    {
        _Color ("光の色", Color) = (0.45, 0.85, 1, 1)
        _Core ("ふちの線の色", Color) = (0.9, 0.98, 1, 1)
        _Strength ("強さ", Float) = 1
        _Mode ("0＝ふちの帯 1＝地面の光", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off Blend One OneMinusSrcAlpha   // ぼわっとした光はふつうの重ね（明るい空でも白く飛ばない）、線とすじは加算
            Offset -2, -2   // 穴のふちの上に描く（穴と同じ面なので）
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Color, _Core; half _Strength, _Mode;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            float H(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float VN(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(H(i), H(i + float2(1, 0)), f.x), lerp(H(i + float2(0, 1)), H(i + float2(1, 1)), f.x), f.y);
            }
            half4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                half3 col;
                if (_Mode < 0.5)
                {
                    float u = i.uv.x, v = i.uv.y;
                    // ゆらぐ炎のような光：ふちに沿って流れるノイズで、外への伸びをゆらす（u は輪なので 1 周で模様がつながる数にする）
                    float n = VN(float2(u * 28.0 - t * 1.6, v * 2.5 - t * 0.9)) * 0.6 + VN(float2(u * 61.0 + t * 2.3, v * 5.0 - t * 1.7)) * 0.4;
                    float reach = 0.35 + 0.55 * n;                       // この外側までのびる
                    float glow = exp(-v * 3.2 / reach) * (1 - smoothstep(0.8, 1.0, v));
                    // ふちのすぐ外の細く明るい線（少しまたたく）
                    float core = exp(-pow((v - 0.06) / 0.035, 2.0)) * (0.85 + 0.15 * sin(t * 3.1 + u * 157.0));
                    // うずを巻くすじ（ふちに沿って回る）
                    float s = frac(u * 9.0 + v * 0.8 - t * 0.35);
                    float streak = smoothstep(0.0, 0.08, s) * smoothstep(0.32, 0.08, s) * exp(-v * 2.5) * 0.55;
                    float ga = glow * 0.5 * _Strength;
                    half4 o0 = half4(_Color.rgb * ga + (_Color.rgb * streak * 0.8 + _Core.rgb * core * 1.1) * _Strength, ga * 0.85);
                    UNITY_APPLY_FOG_COLOR(i.fogCoord, o0, half4(0, 0, 0, 0));
                    return o0;
                }
                else
                {
                    float2 p = i.uv * 2 - 1; float r = length(p);
                    float pool = exp(-r * r * 3.5) * (0.92 + 0.08 * sin(t * 1.7));
                    float rr = frac(t * 0.35);
                    float ring = exp(-pow((r - rr) / 0.06, 2.0)) * (1 - rr) * 0.55;
                    col = _Color.rgb * (pool * 0.55 + ring) * (1 - smoothstep(0.85, 1.0, r));
                }
                col *= _Strength;
                half4 o = half4(col, 0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, o, half4(0, 0, 0, 0));
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
