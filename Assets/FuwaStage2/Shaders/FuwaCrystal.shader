// 2026-11-15 2–5 クリスタルの広場：結晶のシェーダー（てじゃ「見た目クオリティあげてく」）
//  不透明（並び順の心配なし）。面ごとに明るさが変わる（平らな法線＋太陽の向き）、ふちが光る（フレネル）、
//  正面ほど奥の濃い色（中が深い感じ）、面ごとの乱数でたまにきらっ。_EmissionColor はスクリプト（FuwaShockwave・FuwaShieldGate）が MPB で動かす
Shader "Fuwa/Crystal"
{
    Properties
    {
        _Color ("明るい色（ふち・光の当たる面）", Color) = (0.62, 0.95, 1, 1)
        _Deep ("奥の色", Color) = (0.12, 0.38, 0.75, 1)
        _RimPow ("ふちの細さ", Range(0.5, 6)) = 2.2
        _Spark ("きらめきの強さ", Range(0, 3)) = 1.2
        _EmissionColor ("光（スクリプト）", Color) = (0.2, 0.45, 0.5, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Color, _Deep; half _RimPow, _Spark;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wn : TEXCOORD0; float3 wp : TEXCOORD1; float3 on : TEXCOORD2; UNITY_FOG_COORDS(3) UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.on = v.normal;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            float Hash3(float3 p) { return frac(sin(dot(p, float3(12.9898, 78.233, 37.719))) * 43758.5453); }
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.wn);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                float3 l = normalize(_WorldSpaceLightPos0.xyz + float3(0, 1e-4, 0));
                float ndv = saturate(dot(n, v));
                float fres = pow(1 - ndv, _RimPow);
                // 面ごとの乱数（平らな法線なので面の中で同じ値）
                float fh = Hash3(floor(i.on * 37.0 + 0.5));
                // 正面ほど奥の色、ななめほど明るい色。面ごとに少しずらす
                float depthK = saturate(ndv * (0.8 + 0.4 * fh));
                half3 col = lerp(_Color.rgb, _Deep.rgb, depthK * 0.85);
                // 光の当たる面は明るく（やわらかく）
                float ndl = dot(n, l) * 0.5 + 0.5;
                col *= 0.65 + 0.5 * ndl;
                // ふち
                col += _Color.rgb * fres * 0.9;
                // きらっ：反射の向きが太陽に近い面＋時間でゆれる
                float3 h = normalize(l + v);
                float sp = pow(saturate(dot(n, h)), 60) * 2.0;
                float tw = saturate(sin(_Time.y * (1.5 + fh * 2.5) + fh * 40) * 3 - 2.2);
                col += (sp + tw * 0.9 * fh) * _Spark * half3(1, 1, 1);
                // 光（ためるほど明るく）：ふちと奥の両方に乗せる
                half3 em = UNITY_ACCESS_INSTANCED_PROP(Props, _EmissionColor).rgb;
                col += em * (0.55 + 0.6 * fres + 0.35 * (1 - depthK));
                half4 c = half4(col, 1);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
