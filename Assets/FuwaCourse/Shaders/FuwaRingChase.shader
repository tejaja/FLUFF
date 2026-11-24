// チュートリアルの課題の輪（2026-11-24 てじゃ「クリア前リングに回転する光、クリア後は全部光る」）
//  輪のまわり（uv.x）を光の帯が回る。_Lit=1 で全体が光る（クリア後の材質）。ふつうの半ランバート＋霧。影は FallBack の ShadowCaster
//  2026-11-25 てじゃ「光ってるとこがのっぺり」→ A：光ってる所もネオン管みたいに芯が白く・ふちが色濃く＋陰影を少し残す
//                                              B：にせブルーム。輪をふくらませた殻を加算で重ねて、光ってる所のまわりをボヤっと（全機種で同じ見え方）
//  2026-11-25 てじゃ「まぶしい」→ 芯の白さ 0.55→0.3、にじみ 0.7→0.38
//  殻は空より後に描かないと空に消されるので Queue は Geometry+501（空の後）。本体は不透明のまま
Shader "FuwaCourse/RingChase"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.55, 0.45, 1)
        _Glow ("Glow", Color) = (1, 0.97, 0.8, 1)
        _Speed ("Speed (turns/sec)", Float) = 0.32
        _Count ("Bands", Float) = 2
        _Width ("Band width", Range(0.02, 0.5)) = 0.2
        _Lit ("All lit", Range(0, 1)) = 0
        _Pulse ("Pulse when lit", Range(0, 1)) = 0.25
        _Core ("Hot core (whiteness)", Range(0, 1)) = 0.3
        _Tube ("Tube radius (m)", Float) = 0.06
        _HaloSize ("Halo size (m)", Float) = 0.16
        _Halo ("Halo strength", Range(0, 2)) = 0.38
        _Tint ("Mix base color into glow", Range(0, 1)) = 0.35
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    fixed4 _Color, _Glow;
    float _Speed, _Count, _Width, _Lit, _Pulse, _Core, _Tube, _HaloSize, _Halo, _Tint;
    float4 _LightColor0;

    struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 wp : TEXCOORD2; UNITY_FOG_COORDS(3) UNITY_VERTEX_OUTPUT_STEREO };

    // 光っている量（0..1）：回る帯、クリア後は全体＋ゆっくり脈打つ
    float GlowAmount(float u)
    {
        float p = frac(u * _Count - _Time.y * _Speed * _Count);
        float d = min(p, 1.0 - p);
        float band = smoothstep(_Width, 0.0, d);
        float pulse = 1.0 - _Pulse * (0.5 + 0.5 * sin(_Time.y * 4.0));
        return max(band * (1.0 - _Lit), _Lit * pulse);
    }

    v2f vertInflate (appdata v, float inflate)
    {
        v2f o;
        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
        float4 p = v.vertex; p.xyz += v.normal * inflate;
        o.pos = UnityObjectToClipPos(p);
        o.wp = mul(unity_ObjectToWorld, p).xyz;
        o.uv = v.uv;
        o.n = UnityObjectToWorldNormal(v.normal);
        UNITY_TRANSFER_FOG(o, o.pos);
        return o;
    }
    ENDCG

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+501" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            v2f vert (appdata v) { return vertInflate(v, 0.0); }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 vd = normalize(_WorldSpaceCameraPos - i.wp);
                float nl = dot(n, normalize(_WorldSpaceLightPos0.xyz)) * 0.5 + 0.5;
                float ndv = saturate(dot(n, vd));
                float3 base = _Color.rgb * lerp(0.72, 1.06, nl);
                // ネオン管：正面（芯）は白っぽく明るく、ふちは色が濃く少し暗い。光の向きの陰影も少し残す
                float3 gc = lerp(_Glow.rgb, _Color.rgb, _Tint);   // 2026-11-25 てじゃ「光の部分も弱めたい、元の色足したら」
                float3 edge = gc * 0.74;
                float3 core = lerp(gc, float3(1, 1, 1), _Core);
                float3 hot = lerp(edge, core, pow(ndv, 1.4)) * lerp(0.86, 1.04, nl);
                float g = GlowAmount(i.uv.x);
                float3 col = lerp(base, hot, g);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, 1);
            }
            ENDCG
        }
        // にせブルーム：輪をふくらませた殻。正面ほど濃く、殻のふち（=光の外側）で 0 に。光っている所だけ
        Pass
        {
            Tags { "LightMode" = "Always" }
            Blend One One
            ZWrite Off
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            v2f vert (appdata v) { return vertInflate(v, _HaloSize); }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 vd = normalize(_WorldSpaceCameraPos - i.wp);
                float ndv = saturate(dot(n, vd));
                // 殻の上で、管の中心から見た横ずれ（0=まん中、1=殻のふち）。管の外から殻のふちまでをなめらかに消す
                float d = sqrt(saturate(1.0 - ndv * ndv));
                float k = _Tube / max(_Tube + _HaloSize, 1e-4);
                float t = saturate((1.0 - d) / max(1.0 - k, 1e-4));
                float a = t * t * (3.0 - 2.0 * t);
                a *= a;
                float g = GlowAmount(i.uv.x);
                float3 col = lerp(_Glow.rgb, _Color.rgb, _Tint) * (a * g * _Halo);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0, 0, 0, 0));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    FallBack "VertexLit"
}
