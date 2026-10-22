// コースの軽い版（FuwaCourseLite）用：頂点カラー（sRGBで書いた色）＋ ToonLit と同じ光り方。
// 前はヒトヨタケ用の InkCap を借りていて、影が紫寄りにならず本体より黒っぽく見えた（2026-10-22 てじゃ「軽量版の影色ってなんかほかのより黒っぽく」）。
// 数値の初期値はコースの材質（道・地面・きのこ）と同じ。影は受けない・落とさない（軽い版は遠くに見せるだけなので）
Shader "FuwaCourse/LiteVC"
{
    Properties
    {
        _ShadowTint ("Shadow Tint", Color) = (0.55, 0.48, 0.75, 1)
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 0.75
        _Wrap ("Wrap (half lambert)", Range(0,1)) = 0.35
        _Steps ("Soft Band Sharpness", Range(0,1)) = 0.75
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.55
        _RimColor ("Rim Color", Color) = (1, 0.97, 0.92, 1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.25
        _RimPower ("Rim Power", Range(0.5,8)) = 3
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
            #include "Lighting.cginc"

            fixed4 _ShadowTint, _RimColor;
            half _ShadowStrength, _Wrap, _Steps, _AmbientStrength, _RimStrength, _RimPower;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float3 wn : TEXCOORD0;
                float3 wp : TEXCOORD1;
                half3 amb : TEXCOORD2;
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.amb = ShadeSH9(float4(o.wn, 1));
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 col = GammaToLinearSpace(i.color.rgb);
                float3 n = normalize(i.wn);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                // ToonLit と同じ：ハーフランバートを少しだけ段に、影側は紫寄りの色に
                half wrap = saturate((dot(n, l) + _Wrap) / (1 + _Wrap));
                half band = smoothstep(0.5 - 0.5 * (1 - _Steps), 0.5 + 0.5 * (1 - _Steps), wrap);
                half lit = lerp(wrap, band, 0.5);
                half3 shade = lerp(1, _ShadowTint.rgb, _ShadowStrength);
                half3 light = lerp(shade, 1, lit) * _LightColor0.rgb;
                half3 c = col * (light + i.amb * _AmbientStrength * shade);
                c += _RimColor.rgb * pow(1 - saturate(dot(n, v)), _RimPower) * _RimStrength * (0.5 + 0.5 * lit);
                fixed4 o = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
        // 深度テクスチャ（ふわふわのうそ影など）に入るように
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { V2F_SHADOW_CASTER; };
            v2f vert (appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); TRANSFER_SHADOW_CASTER_NORMALOFFSET(o) return o; }
            float4 frag (v2f i) : SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
}
