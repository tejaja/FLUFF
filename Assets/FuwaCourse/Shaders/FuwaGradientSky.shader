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

        [Header(Cartoon sun)]
        [ToggleUI] _SunStyle ("Cartoon Sun", Float) = 1
        _SunRadius ("Disc Radius (deg)", Range(0.5, 20)) = 5
        _SunDiscColor ("Disc Color", Color) = (1, 0.93, 0.55, 1)
        _SunRayColor ("Ray / Rim Color", Color) = (1, 0.72, 0.3, 1)
        _SunRayCount ("Ray Count", Float) = 10
        _SunRayLen ("Ray Length (x radius)", Range(1.2, 3)) = 1.75
        _SunRayWidth ("Ray Base Half Width (x radius)", Range(0.05, 0.6)) = 0.3
        _SunRaySpin ("Ray Spin Speed", Float) = 0.08
        _SunHalo ("Halo", Range(0, 2)) = 0.6
        [ToggleUI] _SunFace ("Face", Float) = 1
        _SunEyeColor ("Eye Color", Color) = (0.38, 0.22, 0.12, 1)
        _SunCheekColor ("Cheek Color (a=strength)", Color) = (1, 0.55, 0.62, 0.85)

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
            fixed4 _SunDiscColor, _SunRayColor, _SunEyeColor, _SunCheekColor;
            float _SunFace, _SunStyle, _SunRadius, _SunRayCount, _SunRayLen, _SunRayWidth, _SunRaySpin, _SunHalo;

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
                if (_SunStyle < 0.5)
                {
                    float disk = smoothstep(1 - _SunSize, 1 - _SunSize * 0.6, c);
                    float glow = pow(c, 64) * _SunGlow;
                    col += _SunColor.rgb * (disk + glow);
                    return fixed4(col, 1);
                }

                // デフォルメ太陽：まるい円＋すき間＋ぷっくりした三角のトゲがゆっくり回る
                float rho = degrees(acos(c));                       // 太陽の中心からの角度(度)
                float3 hint = abs(sunDir.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
                float3 bu = normalize(cross(hint, sunDir)), bw = cross(sunDir, bu);
                float phi = atan2(dot(d, bw), dot(d, bu)) + _Time.y * _SunRaySpin;
                float R = _SunRadius;
                float aa = max(fwidth(rho), 1e-4) * 1.2;
                // ほんのり後光
                col += _SunColor.rgb * _SunHalo * exp(-rho / (R * 1.6)) * 0.35;
                // トゲ：円のすぐ外から R*_SunRayLen まで、根元が太く先が丸い三角
                float r0 = R * 1.18, r1 = R * _SunRayLen;
                float sector = 6.2831853 / _SunRayCount;
                float a = (frac(phi / sector) - 0.5) * sector;      // トゲの中心線からの角度
                float arc = abs(a) * rho;                           // 横方向の距離(度)
                float tr = saturate((rho - r0) / (r1 - r0));
                float hw = lerp(R * _SunRayWidth, R * 0.06, tr);    // 先ほど細く（先は少し丸く残す）
                float ray = (1 - smoothstep(hw - aa, hw + aa, arc))
                          * smoothstep(r0 - aa, r0 + aa, rho) * (1 - smoothstep(r1 - aa, r1 + aa, rho));
                // 先っちょを丸める
                float tipD = length(float2(arc, rho - r1));
                ray = max(ray, (1 - smoothstep(R * 0.06 - aa, R * 0.06 + aa, tipD)) * step(r0, rho));
                col = lerp(col, _SunRayColor.rgb * _SunColor.rgb, ray);
                // 円：ふちは少し濃い色、中は明るく
                float disc = 1 - smoothstep(R - aa, R + aa, rho);
                float rim = smoothstep(R * 0.84 - aa, R * 0.84 + aa, rho);
                float3 dc = lerp(_SunDiscColor.rgb, _SunRayColor.rgb, rim);
                col = lerp(col, dc * _SunColor.rgb, disc);
                // 顔：見ている人のカメラの上方向に合わせて、いつも正立して見える
                if (_SunFace > 0.5 && disc > 0)
                {
                    float3 camUp = UNITY_MATRIX_V[1].xyz, camRight = UNITY_MATRIX_V[0].xyz;
                    float3 fu = camUp - sunDir * dot(camUp, sunDir);
                    float3 fr = camRight - sunDir * dot(camRight, sunDir);
                    fu = normalize(fu + 1e-5); fr = normalize(fr + 1e-5);
                    float2 fp = float2(dot(d, fr), dot(d, fu)) * 57.29578 / R;   // 円の半径=1
                    float fa = aa / R;
                    // 棒の目（縦長のカプセル）
                    float2 e = float2(abs(fp.x) - 0.3, clamp(fp.y, 0.02, 0.3));
                    float eye = 1 - smoothstep(0.065 - fa, 0.065 + fa, length(float2(e.x, fp.y - e.y)));
                    // ぴんくの丸ほっぺ
                    float cheek = 1 - smoothstep(0.15 - fa, 0.15 + fa, length(float2(abs(fp.x) - 0.52, fp.y + 0.16)));
                    col = lerp(col, _SunCheekColor.rgb * _SunColor.rgb, cheek * _SunCheekColor.a * disc);
                    col = lerp(col, _SunEyeColor.rgb * _SunColor.rgb, eye * disc);
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
