// やわらかいトゥーン寄りのライティング（Standard の代わり、軽い）。
// ・光の当たり方はハーフランバートを少しだけ段にして、ふんわり明暗
// ・影はグレーではなく _ShadowTint（紫寄り）に寄せる。太陽の影（シャドウ）も同じ色で受ける
// ・まわりの明るさは環境光（空・地平線・地面の3色）から
// ・ふちをほんのり光らせる（リム）。光の当たっている側だけ少し強め
// ・岩の情報量（どれも 0 で無効）：ワールド座標のノイズで色ムラ、横向きの地層の縞、面ごとのカクカク（フラット）、
//   上向きの面だけ別の色（苔・砂）
// ・根元のなじませ（つららなど）：オブジェクトの y=0（根元）から下へ _BaseFadeLength の間、色を _BaseFadeColor に、
//   向きを真下（天井と同じ）に寄せて、天井との境目をぼかす。0 なら何もしない
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
        [Header(Base Fade)]
        _BaseFadeColor ("Base Fade Color", Color) = (0.66, 0.6, 0.68, 1)
        _BaseFadeLength ("Base Fade Length (object units, 0=off)", Float) = 0
        _BaseFadeNormal ("Base Fade Normal (toward down)", Range(0,1)) = 1
        [Header(Rock Detail)]
        _NoiseStrength ("Color Noise Strength", Range(0,0.5)) = 0
        _NoiseScale ("Color Noise Scale (per m)", Float) = 0.6
        _StrataStrength ("Strata Strength", Range(0,1)) = 0
        _StrataScale ("Strata Layers per m", Float) = 1.2
        _StrataA ("Strata Tint A", Color) = (1.06, 0.98, 0.94, 1)
        _StrataB ("Strata Tint B", Color) = (0.93, 0.95, 1.06, 1)
        _StrataC ("Strata Tint C", Color) = (0.90, 0.87, 0.93, 1)
        _Facet ("Facet (flat shading)", Range(0,1)) = 0
        _TopColor ("Top Color", Color) = (0.62, 0.72, 0.52, 1)
        _TopStrength ("Top Strength", Range(0,1)) = 0
        _TopMin ("Top Start (normal.y)", Range(0,1)) = 0.55
        [Toggle] _PatternFromBase ("Pattern From Base (stalactite)", Float) = 0
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
            fixed4 _BaseFadeColor; float _BaseFadeLength; half _BaseFadeNormal;
            half _NoiseStrength, _StrataStrength, _Facet, _TopStrength, _TopMin; float _NoiseScale, _StrataScale; fixed4 _TopColor;
            half4 _StrataA, _StrataB, _StrataC; half _PatternFromBase;
            float Hash1(float x) { return frac(sin(x * 127.1 + 311.7) * 43758.5453); }
            // 層ごとの色味：層の番号からA/B/C（と元の色）を選ぶ。となりと同じ色になることもあるので、層の厚みがバラバラに見える
            half3 LayerTint(float id)
            {
                float h = Hash1(id);
                half3 t = h < 0.3 ? _StrataA.rgb : (h < 0.55 ? _StrataB.rgb : (h < 0.75 ? _StrataC.rgb : half3(1,1,1)));
                return t;
            }

            float Hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float VNoise(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash3(i), Hash3(i + float3(1,0,0)), f.x), lerp(Hash3(i + float3(0,1,0)), Hash3(i + float3(1,1,0)), f.x), f.y),
                            lerp(lerp(Hash3(i + float3(0,0,1)), Hash3(i + float3(1,0,1)), f.x), lerp(Hash3(i + float3(0,1,1)), Hash3(i + float3(1,1,1)), f.x), f.y), f.z);
            }
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
                half fade : TEXCOORD6;
                float3 pp : TEXCOORD7;   // 色ムラ・層の模様を取る位置（つららは根元＝天井の位置で取って、天井と同じ層の色にする）
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
                o.fade = _BaseFadeLength > 0 ? smoothstep(-_BaseFadeLength, 0, v.vertex.y) : 0;
                o.wn = lerp(o.wn, float3(0, -1, 0), o.fade * _BaseFadeNormal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pp = _PatternFromBase > 0.5 ? mul(unity_ObjectToWorld, float4(v.vertex.x, 0, v.vertex.z, 1)).xyz : o.wp;
                o.amb = ShadeSH9(float4(o.wn, 1));
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 col = tex2D(_MainTex, i.uv) * UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                col.rgb = lerp(col.rgb, _BaseFadeColor.rgb, i.fade);
                float3 n = normalize(i.wn);
                // 面ごとのカクカク：画面上の位置の変化から面の向きを出して混ぜる
                if (_Facet > 0)
                {
                    float3 fn = normalize(cross(ddy(i.wp), ddx(i.wp)));
                    fn *= sign(dot(fn, n) + 1e-4);
                    n = normalize(lerp(n, fn, _Facet * (1 - i.fade)));
                }
                // 色ムラ・地層の縞・上向きの面の色（ワールド座標なので、つながった岩どうしで模様がそろう）
                if (_NoiseStrength > 0 || _StrataStrength > 0)
                {
                    float3 q = i.pp * _NoiseScale;
                    float nz = 0.65 * VNoise(q) + 0.35 * VNoise(q * 2.3 + 7.1);
                    half mul = 1 + _NoiseStrength * (nz * 2 - 1);
                    col.rgb *= mul;
                    if (_StrataStrength > 0)
                    {
                        // 色味の違う層：高さを低い周波数のノイズで少し波打たせて、層の番号ごとに色を変える（境目は少しだけぼかす）
                        float wob = VNoise(float3(i.pp.x, 0, i.pp.z) * 0.18) * 1.2 + nz * 0.15;
                        float y = i.pp.y * _StrataScale + wob;
                        float id = floor(y), f = frac(y);
                        half3 t0 = LayerTint(id), t1 = LayerTint(id + 1), tm = LayerTint(id - 1);
                        half3 tint = f > 0.5 ? lerp(t0, t1, 0.5 * smoothstep(0.95, 1.0, f)) : lerp(tm, t0, smoothstep(0.0, 0.05, f) * 0.5 + 0.5);
                        col.rgb *= lerp(half3(1,1,1), tint, _StrataStrength);
                    }
                }
                if (_TopStrength > 0) col.rgb = lerp(col.rgb, _TopColor.rgb, _TopStrength * smoothstep(_TopMin, 1, n.y));
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
                rim *= 1 - i.fade;   // 根元のなじませ部分はふちを光らせない（境目が浮かないように）
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
