// やわらかいトゥーン寄りのライティング（Standard の代わり、軽い）。
// ・光の当たり方はハーフランバートを少しだけ段にして、ふんわり明暗
// ・影はグレーではなく _ShadowTint（紫寄り）に寄せる。太陽の影（シャドウ）も同じ色で受ける
// ・まわりの明るさは環境光（空・地平線・地面の3色）から
// ・ふちをほんのり光らせる（リム）。光の当たっている側だけ少し強め
Shader "FuwaCourse/ToonLit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (optional)", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        [Header(Light)]
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.55, 0.78, 1)
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 0.55
        _Wrap ("Wrap (half lambert)", Range(0,1)) = 0.6
        _Steps ("Soft Band Sharpness", Range(0,1)) = 0.35
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.6
        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1, 0.97, 0.92, 1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.18
        _RimPower ("Rim Power", Range(0.5,8)) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _ShadowTint, _RimColor;
            half _ShadowStrength, _Wrap, _Steps, _AmbientStrength, _RimStrength, _RimPower;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half4, _EmissionColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wn : TEXCOORD1;
                float3 wp : TEXCOORD2;
                half3 amb : TEXCOORD3;
                SHADOW_COORDS(4)
                UNITY_FOG_COORDS(5)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.amb = ShadeSH9(float4(o.wn, 1));
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col = tex2D(_MainTex, i.uv) * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float3 n = normalize(i.wn);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);

                // ふんわり明暗：ハーフランバートを、少しだけ段っぽく（_Steps で境目の締まり）
                half ndl = dot(n, l);
                half wrap = saturate((ndl + _Wrap) / (1 + _Wrap));
                half band = smoothstep(0.5 - 0.5 * (1 - _Steps), 0.5 + 0.5 * (1 - _Steps), wrap);
                half lit = lerp(wrap, band, 0.5);
                UNITY_LIGHT_ATTENUATION(atten, i, i.wp);
                lit *= lerp(1, atten, 0.85);

                // 影側はグレーではなく紫寄りの色に
                half3 shade = lerp(1, _ShadowTint.rgb, _ShadowStrength);
                half3 light = lerp(shade, 1, lit) * _LightColor0.rgb;
                half3 c = col.rgb * (light + i.amb * _AmbientStrength * shade);

                // ふちの光（光の当たる側は少し強め）
                half rim = pow(1 - saturate(dot(n, v)), _RimPower) * _RimStrength * (0.5 + 0.5 * lit);
                c += _RimColor.rgb * rim;
                c += UNITY_ACCESS_INSTANCED_PROP(Props, _EmissionColor).rgb;

                fixed4 o = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
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
    FallBack "Diffuse"
}
