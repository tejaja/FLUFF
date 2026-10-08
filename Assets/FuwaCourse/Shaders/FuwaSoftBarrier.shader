// チュートリアルの「ふわふわだけ止める」半透明の壁。人は通り抜けられる（当たり判定は Walkthrough レイヤー）。
// 下のふちと左右のふちがほんのり濃く、上へいくほど消える。ゆっくり流れる縦のしま、見る角度で少し虹色。
// 近づくと（通り抜ける時に）薄くなる。
Shader "FuwaCourse/SoftBarrier"
{
    Properties
    {
        _Color ("Color", Color) = (0.85, 0.95, 1, 0.35)
        _EdgeColor ("Edge Color", Color) = (1, 1, 1, 0.6)
        _EdgeWidth ("Edge Width (UV)", Range(0.005, 0.2)) = 0.04
        _FadeTop ("Fade Top (0-1 of height)", Range(0.1, 1)) = 0.85
        _StripeFreq ("Stripe Freq", Float) = 9
        _StripeSpeed ("Stripe Speed", Float) = 0.25
        _NearFade ("Near Fade Distance", Float) = 1.2
        _Iri ("Iridescence", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
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

            fixed4 _Color, _EdgeColor;
            float _EdgeWidth, _FadeTop, _StripeFreq, _StripeSpeed, _NearFade, _Iri;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float3 wn : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wn = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // 上へいくほど消える
                float vfade = 1 - smoothstep(0, _FadeTop, uv.y);
                // 下と左右のふち
                float edge = max(1 - smoothstep(0, _EdgeWidth, uv.y), max(1 - smoothstep(0, _EdgeWidth, uv.x), 1 - smoothstep(0, _EdgeWidth, 1 - uv.x)));
                // 左右のふちは上まで少し残す
                float side = max(1 - smoothstep(0, _EdgeWidth * 1.5, uv.x), 1 - smoothstep(0, _EdgeWidth * 1.5, 1 - uv.x));
                // ゆっくり上へ流れる縦のしま
                float stripe = 0.5 + 0.5 * sin((uv.x * _StripeFreq + uv.y * 2 - _Time.y * _StripeSpeed) * 6.2831);
                stripe = smoothstep(0.55, 1, stripe) * 0.5;
                float3 vdir = normalize(_WorldSpaceCameraPos - i.wpos);
                float ndv = abs(dot(normalize(i.wn), vdir));
                // 見る角度で少し虹色
                float3 iri = 0.5 + 0.5 * cos(6.2831 * (ndv * 0.8 + float3(0, 0.33, 0.67)));
                float3 col = lerp(_Color.rgb, _Color.rgb * (0.7 + 0.6 * iri), _Iri);
                float a = _Color.a * (0.35 + stripe) * vfade;
                a = max(a, _EdgeColor.a * edge * (0.4 + 0.6 * vfade));
                a = max(a, _EdgeColor.a * 0.6 * side * vfade);
                col = lerp(col, _EdgeColor.rgb, saturate(edge + side * 0.5));
                // 斜めから見るほど少し濃く（シャボンの膜っぽく）
                a *= lerp(1.3, 0.8, ndv);
                // 近いと薄く（通り抜ける時に視界をふさがない）
                float d = distance(_WorldSpaceCameraPos, i.wpos);
                a *= smoothstep(0.3, _NearFade, d);
                return fixed4(col, saturate(a));
            }
            ENDCG
        }
    }
}
