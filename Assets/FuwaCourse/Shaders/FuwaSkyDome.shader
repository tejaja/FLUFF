Shader "FuwaCourse/SkyDome"
{
    // 空と同じグラデーションを描く「内側から見る球」。中にいる人から外の景色を隠す（空に見える）。
    // 色の計算は FuwaCourse/GradientSky と同じなので、本物の空とつなぎ目なく見える。
    Properties
    {
        _TopColor ("Top Color", Color) = (0.36, 0.62, 0.98, 1)
        _HorizonColor ("Horizon Color", Color) = (0.86, 0.94, 1.0, 1)
        _BottomColor ("Bottom Color", Color) = (0.98, 0.86, 0.92, 1)
        _TopExponent ("Top Exponent", Range(0.1, 5)) = 0.8
        _BottomExponent ("Bottom Exponent", Range(0.1, 5)) = 0.6
        _SunColor ("Sun Color", Color) = (1, 0.97, 0.88, 1)
        _SunSize ("Sun Size", Range(0, 0.2)) = 0.035
        _SunGlow ("Sun Glow", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" }
        Cull Front

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _TopColor, _HorizonColor, _BottomColor, _SunColor;
            half _TopExponent, _BottomExponent, _SunSize, _SunGlow;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 worldPos : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 見ている向き（カメラから画素へ）で空の色を決める
                float3 d = normalize(i.worldPos - _WorldSpaceCameraPos);
                float y = d.y;
                float3 col = y >= 0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(y), _TopExponent))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, pow(saturate(-y), _BottomExponent));
                float3 sunDir = normalize(_WorldSpaceLightPos0.xyz);
                float c = saturate(dot(d, sunDir));
                float disk = smoothstep(1 - _SunSize, 1 - _SunSize * 0.6, c);
                float glow = pow(c, 64) * _SunGlow;
                col += _SunColor.rgb * (disk + glow);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
