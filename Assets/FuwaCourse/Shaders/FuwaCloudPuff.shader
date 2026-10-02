// もこもこ雲：やわらかい陰影（日なたは白、日かげは水色）＋ふちが少し明るい。
// 頂点シェーダーで、雲ごとにずれた周期でゆっくり漂う（Udon不要）。
Shader "FuwaCourse/CloudPuff"
{
    Properties
    {
        _Color ("Lit Color", Color) = (1, 1, 1, 1)
        _Shade ("Shade Color", Color) = (0.72, 0.83, 0.97, 1)
        _Rim ("Rim", Range(0, 1)) = 0.35
        _Drift ("Drift Distance (m)", Float) = 3
        _DriftSpeed ("Drift Speed", Float) = 0.04
        _Bob ("Bob Height (m)", Float) = 0.4
        _Haze ("Haze Color", Color) = (0.6, 0.83, 1, 1)
        _HazeStart ("Haze Start (m)", Float) = 300
        _HazeEnd ("Haze End (m)", Float) = 1000
        _HazeMax ("Haze Max", Range(0, 1)) = 0.55
    }
    SubShader
    {
        // まとめ描き（バッチング）されると雲ごとの原点が取れず、漂う位置が急に飛ぶので禁止
        Tags { "RenderType"="Opaque" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color, _Shade, _LightColor0, _Haze;
            float _Rim, _Drift, _DriftSpeed, _Bob, _HazeStart, _HazeEnd, _HazeMax;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wn : TEXCOORD0; float3 wv : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 origin = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float ph = dot(origin, float3(0.131, 0.0, 0.173));
                float t = _Time.y;
                wp += float3(sin(t * _DriftSpeed + ph) * _Drift, sin(t * _DriftSpeed * 3.1 + ph * 2.0) * _Bob, cos(t * _DriftSpeed * 0.8 + ph * 1.3) * _Drift);
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wv = _WorldSpaceCameraPos - wp;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.wn);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float d = saturate((dot(n, l) + 0.5) / 1.5);
                float3 col = lerp(_Shade.rgb, _Color.rgb, d);
                float rim = pow(1 - saturate(dot(n, normalize(i.wv))), 3) * _Rim;
                col += rim;
                float3 tint = saturate(_LightColor0.rgb * 0.85 + 0.15);
                col *= tint;   // 太陽の色・明るさについてくる（やみのもりでは暗い紫に）
                // 遠い雲ほど空の色にかすむ（地平線の遠い雲用。近くの雲は変わらない）
                float h = saturate((length(i.wv) - _HazeStart) / max(1, _HazeEnd - _HazeStart)) * _HazeMax;
                col = lerp(col, _Haze.rgb * tint, h);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
