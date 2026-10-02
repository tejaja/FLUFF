Shader "FuwaCourse/GradientSky"
{
    // 上・地平線・下の3色グラデーションの空。地平線より下には、ゆっくり流れる雲（下に雲の層を見下ろす感じ。_CloudAlpha=0で消える）
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

        [Header(Clouds below)]
        _CloudAlpha ("Cloud Alpha (0=off)", Range(0, 1)) = 0
        _CloudColor ("Cloud Color", Color) = (1, 1, 1, 1)
        _CloudShade ("Cloud Shade Color", Color) = (0.74, 0.84, 0.97, 1)
        _CloudScale ("Cloud Scale", Float) = 0.6
        _CloudCover ("Cloud Cover", Range(0, 1)) = 0.5
        _CloudSoft ("Cloud Softness", Range(0.01, 0.5)) = 0.14
        _CloudSpeed ("Cloud Speed (xz)", Vector) = (0.01, 0, 0.005, 0)
        _CloudHorizonFade ("Horizon Fade", Range(0.001, 0.5)) = 0.07
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor, _HorizonColor, _BottomColor, _SunColor, _CloudColor, _CloudShade;
            half _TopExponent, _BottomExponent, _SunSize, _SunGlow;
            float _CloudAlpha, _CloudScale, _CloudCover, _CloudSoft, _CloudHorizonFade;
            float4 _CloudSpeed;

            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3 - 2 * f);
                float a = hash(i), b = hash(i + float2(1, 0)), c = hash(i + float2(0, 1)), d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }
            float fbm(float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { s += vnoise(p) * a; p = p * 2.03 + float2(17.1, 9.2); a *= 0.5; }
                return s;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float3 col = y >= 0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(y), _TopExponent))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, pow(saturate(-y), _BottomExponent));
                float3 sunDir = normalize(_WorldSpaceLightPos0.xyz);

                // 下の雲：見下ろした向きを、下にある平らな雲の層に投影して、ゆっくり流す
                if (y < 0 && _CloudAlpha > 0)
                {
                    float t = 1.0 / max(-y, 0.03);
                    float2 p = d.xz * t * _CloudScale + _Time.y * _CloudSpeed.xz;
                    float n = fbm(p);
                    float dens = smoothstep(_CloudCover, _CloudCover + _CloudSoft, n);
                    // 太陽側を明るく、反対側を少し影色に（もこもこ感）
                    float n2 = fbm(p + sunDir.xz * 0.12);
                    float lit = saturate(0.65 + (n - n2) * 5);
                    float3 cc = lerp(_CloudShade.rgb, _CloudColor.rgb, lit);
                    // 地平線ぎわは細かすぎてチラつくので、空の色に溶かす
                    float fade = smoothstep(0.0, _CloudHorizonFade, -y);
                    col = lerp(col, cc, dens * _CloudAlpha * fade);
                }

                // 太陽（シーンのディレクショナルライトの向き）
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
