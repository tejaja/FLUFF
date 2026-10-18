// 写真用の太陽（2026-10-18 てじゃ「縦看板の右上に、ちょっと見切れてる感じで太陽見せたいな。写真用に今だけ」）。
// Vket ブースの立体の太陽（VketWinter2026 の Custom/2562/Fuwa/Sun）と同じ見た目：ぷっくりした球＋先の丸いトゲ＋顔。
// 撮影カメラの子に置いて画面の右上に見せる（FuwaPhotoSun）。霧はかけない（遠くに置くので、霧があると空に溶ける）。
// 写真なので「ピョン」はなし、トゲの回る向きは _Spin（ラジアン）で止めて決める
Shader "FuwaCourse/PhotoSun"
{
    Properties
    {
        _SunColor ("Sun Color", Color) = (1, 0.97, 0.88, 1)
        _DiscColor ("Disc Color", Color) = (1, 0.93, 0.55, 1)
        _RayColor ("Ray / Rim Color", Color) = (1, 0.72, 0.3, 1)
        _Spin ("トゲの回転（ラジアン）", Float) = 0
        _Shade ("光の当たらない側の暗さ", Range(0, 0.5)) = 0.12
        _EyeColor ("Eye Color", Color) = (0.38, 0.22, 0.12, 1)
        _CheekColor ("Cheek Color (a=strength)", Color) = (1, 0.55, 0.62, 0.85)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _SunColor, _DiscColor, _RayColor, _EyeColor, _CheekColor;
            float _Spin, _Shade;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv1 : TEXCOORD1; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 objN : TEXCOORD1; float3 wpos : TEXCOORD2; float part : TEXCOORD3; };

            float2 rot(float2 p, float a) { float s, c; sincos(a, s, c); return float2(p.x * c - p.y * s, p.x * s + p.y * c); }

            v2f vert (appdata v)
            {
                v2f o;
                float3 p = v.vertex.xyz, n = v.normal;
                if (v.uv1.x > 0.5) { p.xy = rot(p.xy, _Spin); n.xy = rot(n.xy, _Spin); }
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.n = UnityObjectToWorldNormal(n);
                o.objN = v.normal;
                o.wpos = mul(unity_ObjectToWorld, float4(p, 1)).xyz;
                o.part = v.uv1.x;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float3 L = dot(_WorldSpaceLightPos0.xyz, _WorldSpaceLightPos0.xyz) > 1e-4 ? normalize(_WorldSpaceLightPos0.xyz) : float3(0, 1, 0);
                float shade = lerp(1 - _Shade, 1, saturate(dot(n, L) * 0.5 + 0.5));
                float3 col;
                if (i.part > 0.5) col = _RayColor.rgb;
                else
                {
                    float ndv = saturate(dot(n, v));
                    float aa = max(fwidth(ndv), 1e-4);
                    col = lerp(_DiscColor.rgb, _RayColor.rgb, 1 - smoothstep(0.54 - aa, 0.54 + aa, ndv));
                    float3 on = normalize(i.objN);
                    if (on.z > 0)
                    {
                        float2 fp = on.xy;
                        float fa = max(fwidth(length(fp)), 1e-4) * 1.2;
                        float2 e = float2(abs(fp.x) - 0.3, clamp(fp.y, 0.02, 0.3));
                        float eye = 1 - smoothstep(0.065 - fa, 0.065 + fa, length(float2(e.x, fp.y - e.y)));
                        float cheek = 1 - smoothstep(0.15 - fa, 0.15 + fa, length(float2(abs(fp.x) - 0.52, fp.y + 0.16)));
                        col = lerp(col, _CheekColor.rgb, cheek * _CheekColor.a);
                        col = lerp(col, _EyeColor.rgb, eye);
                    }
                }
                return fixed4(col * _SunColor.rgb * shade, 1);
            }
            ENDCG
        }
    }
}
