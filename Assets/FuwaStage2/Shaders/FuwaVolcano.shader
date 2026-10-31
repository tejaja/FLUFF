// 2–2 の火山（2026-10-31）。光り方は FuwaCourse/ToonLit と同じ（ふんわり明暗・紫寄りの影・リム）。
// テクスチャ（Tools/stage2_volcano.py）：RGB が岩の色、A が光：0 なし、0.25〜0.7 割れ目・熱で焼けた所（ゆっくり明滅）、0.75 以上 流れる溶岩。
// 溶岩は UV の v にそって下へ流れる（uv.y が減る向き）。池（uv.y が大きい所）は放射状に伸びないように xz で模様を取る。
Shader "FuwaCourse/Volcano"
{
    Properties
    {
        _MainTex ("色(RGB) 光(A)", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        [NoScaleOffset] _FlowTex ("溶岩の模様 (R 明暗 G 筋 B かさぶた、継ぎ目なし)", 2D) = "gray" {}
        [Header(Lava)]
        [HDR] _LavaHot ("溶岩：いちばん熱い色", Color) = (1.3, 0.92, 0.32, 1)
        [HDR] _LavaMid ("溶岩：ふつうの色", Color) = (1.15, 0.42, 0.07, 1)
        [HDR] _LavaDark ("溶岩：暗い色", Color) = (0.78, 0.15, 0.04, 1)
        _CrustColor ("かさぶたの色", Color) = (0.2, 0.07, 0.06, 1)
        _FlowU ("模様の数（角度 1 周）", Float) = 34
        _FlowV ("模様の数（v 1）", Float) = 7
        _FlowSpeed ("流れる速さ（模様 / 秒）", Float) = 0.22
        _PondScale ("池の模様の大きさ（/m）", Float) = 0.11
        _PondV ("池の範囲 uv.y がこれより大きい", Float) = 0.93
        [Header(Glow)]
        [HDR] _CrackColor ("割れ目の光", Color) = (1.1, 0.32, 0.06, 1)
        _PulseSpeed ("明滅の速さ", Float) = 0.9
        [Header(Light)]
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.55, 0.78, 1)
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 0.55
        _Wrap ("Wrap (half lambert)", Range(0,1)) = 0.6
        _Steps ("Soft Band Sharpness", Range(0,1)) = 0.35
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.6
        _RimColor ("Rim Color", Color) = (1, 0.72, 0.55, 1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.14
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
            #include "UnityCG.cginc"
            #include "FuwaFireFar.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex, _FlowTex; float4 _MainTex_ST;
            fixed4 _Color, _ShadowTint, _RimColor, _CrustColor;
            half4 _LavaHot, _LavaMid, _LavaDark, _CrackColor;
            float _FlowU, _FlowV, _FlowSpeed, _PondScale, _PondV, _PulseSpeed;
            half _ShadowStrength, _Wrap, _Steps, _AmbientStrength, _RimStrength, _RimPower;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wn : TEXCOORD1;
                float3 wp : TEXCOORD2;
                half3 amb : TEXCOORD3;
                UNITY_SHADOW_COORDS(4)
                UNITY_FOG_COORDS(5)
                float3 op : TEXCOORD6;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.op = v.vertex.xyz;
                o.amb = ShadeSH9(float4(o.wn, 1));
                UNITY_TRANSFER_SHADOW(o, v.uv1);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed3 col = tex.rgb * _Color.rgb;
                float3 n = normalize(i.wn);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                half ndl = dot(n, l);
                half wrap = saturate((ndl + _Wrap) / (1 + _Wrap));
                half band = smoothstep(0.5 - 0.5 * (1 - _Steps), 0.5 + 0.5 * (1 - _Steps), wrap);
                half lit = lerp(wrap, band, 0.5);
                UNITY_LIGHT_ATTENUATION(atten, i, i.wp);
                lit *= lerp(1, atten, 0.85);
                half3 shade = lerp(1, _ShadowTint.rgb, _ShadowStrength);
                half3 light = lerp(shade, 1, lit) * _LightColor0.rgb;
                half3 c = col * (light + i.amb * _AmbientStrength * shade);
                half rim = pow(1 - saturate(dot(n, v)), _RimPower) * _RimStrength * (0.5 + 0.5 * lit);
                c += _RimColor.rgb * rim;

                float t = _Time.y;
                // 割れ目・焼けた所：ゆっくり明滅（場所で少しずらす）
                half glow = smoothstep(0.18, 0.7, tex.a);
                half pulse = 0.82 + 0.18 * sin(t * _PulseSpeed + i.op.y * 0.35 + i.op.x * 0.2);
                half fireFar = FuwaFireFar(i.wp) * 0.6;   // 火山はコースの目玉で、遠くから見るのがふつうなので控えめに
                c += _CrackColor.rgb * glow * glow * pulse * (1 - fireFar * 0.6);

                // 溶岩
                // 遠くの小さいミップは圧縮ブロックで階段になるので、そこではふちをなめらかに
                half lw = clamp(fwidth(tex.a) * 1.5, 0.03, 0.22);
                half lava = smoothstep(0.75 - lw, 0.75 + lw, tex.a);
                half pond = 0;
                if (lava > 0.001)
                {
                    pond = smoothstep(_PondV - 0.01, _PondV + 0.01, i.uv.y);
                    float2 fuv = float2(i.uv.x * _FlowU, i.uv.y * _FlowV + t * _FlowSpeed);
                    float2 fuv2 = float2(i.uv.x * _FlowU * 2.0 + 0.37, i.uv.y * _FlowV * 1.6 + t * _FlowSpeed * 1.9);
                    fixed4 f1 = tex2D(_FlowTex, fuv), f2 = tex2D(_FlowTex, fuv2);
                    if (pond > 0.001)
                    {
                        // 池：ふたつの模様がゆっくり別の向きへ動く
                        float2 xz = i.op.xz * _PondScale;
                        fixed4 p1 = tex2D(_FlowTex, xz + float2(t * 0.012, t * 0.017));
                        fixed4 p2 = tex2D(_FlowTex, xz * 1.9 + float2(-t * 0.02, t * 0.009));
                        f1 = lerp(f1, p1, pond); f2 = lerp(f2, p2, pond);
                    }
                    half h = saturate(f1.r * 0.7 + f2.r * 0.32 + f2.g * 0.22 - 0.3);
                    // トゥーンっぽく：明るさをやわらかい段に
                    half hq = h * 3; h = (floor(hq) + smoothstep(0.3, 0.7, frac(hq))) / 3;
                    half3 lc = h < 0.5 ? lerp(_LavaDark.rgb, _LavaMid.rgb, h * 2) : lerp(_LavaMid.rgb, _LavaHot.rgb, (h - 0.5) * 2);
                    // かさぶた：池に浮かぶ大きい板だけ。筋（f2.g）は光らせて割れ目に見せる
                    half crust = pond * smoothstep(0.5, 0.65, f1.b) * smoothstep(0.62, 0.42, f1.r) * (1 - f2.g);
                    lc = lerp(lc, _CrustColor.rgb, crust * 0.9);
                    c = lerp(c, FuwaFireCalm(lc, fireFar), lava);
                }
                fixed4 o = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
        UsePass "FuwaCourse/ToonLit/FORWARD_ADD"
        UsePass "FuwaCourse/ToonLit/SHADOWCASTER"
    }
    FallBack "Diffuse"
}
