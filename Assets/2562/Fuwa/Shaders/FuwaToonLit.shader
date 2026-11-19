// Vketブース版 ToonLit（元は FLUFF の FuwaCourse/ToonLit）。
// ・光は FuwaBooth.cginc の固定の「にせの太陽」と空の色（会場のライトは使わない）
// ・規定のため multi_compile 系なし：霧・インスタンシング・ライトマップ・リアルタイムの影・点光源はなし
// ・見た目の機能（ふんわり明暗、紫寄りの影色、リム、岩の色ムラ・地層、芝の模様、下向きの面の色、根元のなじませ、光り方）はそのまま
// ・_VertexAO：頂点カラーの R を「影のかかり具合」として使う（1=日なた、0=影）。影がないぶん、接地の影や物かげを頂点に描き込む用。0 で無効
Shader "Custom/2562/Fuwa/ToonLit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        [Toggle] _UseTint ("色を頂点の uv6 から（材質まとめ用）", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        _MainTex ("Albedo (optional)", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        [Toggle] _EmissionGradient ("Emission: グラデーションで光る（足し算で白飛びしない）", Float) = 0
        _EmissionFull ("Emission: いちばん光った時の強さ（この明るさで色が全部光の色になる）", Float) = 1.6
        [Header(Light)]
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.55, 0.78, 1)
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 0.55
        _Wrap ("Wrap (half lambert)", Range(0,1)) = 0.6
        _Steps ("Soft Band Sharpness", Range(0,1)) = 0.35
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 0.6
        _VertexAO ("頂点カラーRの影（0=使わない）", Range(0,1)) = 0
        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1, 0.97, 0.92, 1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.18
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        [Header(Base Fade)]
        _BaseFadeColor ("Base Fade Color", Color) = (0.66, 0.6, 0.68, 1)
        _BaseFadeLength ("Base Fade Length (object units, 0=off)", Float) = 0
        _BaseFadeNormal ("Base Fade Normal (toward down)", Range(0,1)) = 1
        [Header(Under Tint)]
        _UnderColor ("下向きの面の色（かける）", Color) = (0.72, 0.84, 1, 1)
        _UnderStrength ("下向きの面の色の強さ (0=off)", Range(0,1)) = 0
        _UnderGrad ("UV の y がこの値より下も色をかける（下ほど濃く。0=off）", Range(0,1)) = 0
        [Header(Rock Detail)]
        _NoiseStrength ("Color Noise Strength", Range(0,0.5)) = 0
        _NoiseScale ("Color Noise Scale (per m)", Float) = 0.6
        _StrataStrength ("Strata Strength", Range(0,1)) = 0
        _StrataScale ("Strata Layers per m", Float) = 1.2
        _StrataA ("Strata Tint A", Color) = (1.06, 0.98, 0.94, 1)
        _StrataB ("Strata Tint B", Color) = (0.93, 0.95, 1.06, 1)
        _StrataC ("Strata Tint C", Color) = (0.90, 0.87, 0.93, 1)
        _Facet ("Facet (flat shading)", Range(0,1)) = 0
        // 材質まとめ（_UseTint）で、模様の強さだけ違う材質も1つにする（2026-10-19 SetPass 対策：島の側面の岩2材質）。
        // uv6.w が 2 か 3 の頂点は上の値の代わりにこっちを使う（1 はふつう＝上の値）
        _FxA2 ("模様2: 色ムラ強さ, 色ムラ大きさ, 地層強さ, 地層の数", Vector) = (0, 0.6, 0, 1.2)
        _FxB2 ("模様2: カクカク, リム強さ", Vector) = (0, 0.18, 0, 0)
        _FxA3 ("模様3: 色ムラ強さ, 色ムラ大きさ, 地層強さ, 地層の数", Vector) = (0, 0.6, 0, 1.2)
        _FxB3 ("模様3: カクカク, リム強さ", Vector) = (0, 0.18, 0, 0)
        _TopColor ("Top Color", Color) = (0.62, 0.72, 0.52, 1)
        _TopStrength ("Top Strength", Range(0,1)) = 0
        _TopMin ("Top Start (normal.y)", Range(0,1)) = 0.55
        [Toggle] _PatternFromBase ("Pattern From Base (stalactite)", Float) = 0
        [Header(Ground Detail)]
        [NoScaleOffset] _GroundTex ("Ground Detail (R=細かい草 G=大きいムラ B=中くらい、継ぎ目なし)", 2D) = "gray" {}
        _GroundPatch ("大きいムラの強さ (0=off)", Range(0,1)) = 0
        _GroundPatchScale ("大きいムラの大きさ (m)", Float) = 11
        _GroundMidScale ("中くらいのかたまりの大きさ (m)", Float) = 3.2
        _GroundTintA ("明るい所の色味", Color) = (1.1, 1.08, 0.86, 1)
        _GroundTintB ("濃い所の色味", Color) = (0.8, 0.92, 0.84, 1)
        _GroundSteps ("ムラを段にする数（トゥーンっぽく、0=なめらか）", Float) = 0
        _GroundFine ("細かい草の強さ (0=off)", Range(0,1)) = 0
        _GroundFineScale ("細かい草の模様の大きさ (m)", Float) = 0.9
        _GroundFineFade ("細かい草が消える距離 (m)", Float) = 18
        _GroundFineOffset ("細かい草の模様をずらす (m, xy)", Vector) = (0, 0, 0, 0)
        _BakedShadow ("焼いた地面の影（FuwaBoothShadow。受けるのは uv7.z=1 の面だけ）", 2D) = "black" {}
        _BakedShadowStrength ("焼いた影の濃さ", Range(0,1)) = 0.85
        _BakedShadowColor ("焼いた影の色（かける）", Color) = (0.7, 0.66, 0.86, 1)
        _GroundFineCrisp ("草の筋をくっきり（トゥーンっぽく、0=やわらか）", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "FuwaBooth.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color; half4 _EmissionColor;
            fixed4 _ShadowTint, _RimColor;
            half _ShadowStrength, _Wrap, _Steps, _AmbientStrength, _RimStrength, _RimPower, _VertexAO, _UseTint;
            fixed4 _BaseFadeColor; float _BaseFadeLength; half _BaseFadeNormal;
            half _NoiseStrength, _StrataStrength, _Facet, _TopStrength, _TopMin; float _NoiseScale, _StrataScale; fixed4 _TopColor;
            half4 _StrataA, _StrataB, _StrataC; half _PatternFromBase;
            float4 _FxA2, _FxB2, _FxA3, _FxB3;
            half _EmissionGradient, _EmissionFull;
            fixed4 _UnderColor; half _UnderStrength, _UnderGrad;
            sampler2D _BakedShadow; half _BakedShadowStrength; fixed4 _BakedShadowColor;
            sampler2D _GroundTex; half _GroundPatch, _GroundFine, _GroundSteps, _GroundFineCrisp; float _GroundPatchScale, _GroundMidScale, _GroundFineScale, _GroundFineFade; half4 _GroundTintA, _GroundTintB; float4 _GroundFineOffset;
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

            // sh（uv7）：影を受ける面は xy＝影テクスチャの位置・z=1。ミニきのこのぴょこぴょこは z=0 で x＝ずらす時間・w＝跳ねる高さ(m、根元0〜てっぺん)（FuwaBoothCombine）
            //（はじめ uv5 に入れたら、uv5 を持っていない物でも値が入って全部跳ねた。2026-10-18）
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; fixed4 color : COLOR; float4 tint : TEXCOORD6; float4 sh : TEXCOORD7; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wn : TEXCOORD1;
                float3 wp : TEXCOORD2;
                half ao : TEXCOORD3;
                half fade : TEXCOORD4;
                float3 pp : TEXCOORD5;   // 色ムラ・層の模様を取る位置（つららは根元＝天井の位置で取って、天井と同じ層の色にする）
                half3 tint : TEXCOORD6;  // 材質まとめ（2026-10-17）：色ちがいの材質を1つにして、色は頂点の uv6（リニアの色）
                float3 sh : TEXCOORD7;   // 焼いた地面の影：xy＝影テクスチャの位置、z＝受ける面なら1（2026-10-17）
                float4 fxA : TEXCOORD8;  // 色ムラ強さ, 色ムラ大きさ, 地層強さ, 地層の数（uv6.w で選ぶ）
                half2 fxB : TEXCOORD9;   // カクカク, リム強さ
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // ぴょこぴょこ：3.7秒ごとに、ぴょんと伸びて、ぷるんと戻る（太陽のトゲの「ピョン」と同じ形）
                if (v.sh.z < 0.5 && v.sh.w > 0 && v.sh.w < 0.2)
                {
                    float x = frac((_Time.y + v.sh.x) / 3.7) * 3.7 / 0.75;
                    float b = x < 1 ? (1 - x) * (1 - x) * sin(x * 9.4) : 0;
                    v.vertex.y += v.sh.w * b;
                }
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.fade = _BaseFadeLength > 0 ? smoothstep(-_BaseFadeLength, 0, v.vertex.y) : 0;
                o.wn = lerp(o.wn, float3(0, -1, 0), o.fade * _BaseFadeNormal);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pp = _PatternFromBase > 0.5 ? mul(unity_ObjectToWorld, float4(v.vertex.x, 0, v.vertex.z, 1)).xyz : o.wp;
                o.ao = lerp(1, v.color.r, _VertexAO);
                o.tint = _UseTint > 0.5 ? v.tint.rgb : half3(1, 1, 1);
                o.sh = v.sh.xyz;
                float k = _UseTint > 0.5 ? v.tint.w : 1;
                o.fxA = k > 2.5 ? _FxA3 : (k > 1.5 ? _FxA2 : float4(_NoiseStrength, _NoiseScale, _StrataStrength, _StrataScale));
                o.fxB = k > 2.5 ? _FxB3.xy : (k > 1.5 ? _FxB2.xy : half2(_Facet, _RimStrength));
                return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color; col.rgb *= i.tint;
                col.rgb = lerp(col.rgb, _BaseFadeColor.rgb, i.fade);
                float3 n = normalize(i.wn) * (facing > 0 ? 1 : -1);   // 両面描画の裏側は法線を反転
                // 面ごとのカクカク：画面上の位置の変化から面の向きを出して混ぜる
                half facet = i.fxB.x;
                if (facet > 0.001)
                {
                    float3 fn = normalize(cross(ddy(i.wp), ddx(i.wp)));
                    fn *= sign(dot(fn, n) + 1e-4);
                    n = normalize(lerp(n, fn, facet * (1 - i.fade)));
                }
                // 色ムラ・地層の縞・上向きの面の色（ワールド座標なので、つながった岩どうしで模様がそろう）
                half noiseStr = i.fxA.x, strataStr = i.fxA.z;
                if (noiseStr > 0.001 || strataStr > 0.001)
                {
                    float3 q = i.pp * i.fxA.y;
                    float nz = 0.65 * VNoise(q) + 0.35 * VNoise(q * 2.3 + 7.1);
                    half mul = 1 + noiseStr * (nz * 2 - 1);
                    col.rgb *= mul;
                    if (strataStr > 0.001)
                    {
                        float wob = VNoise(float3(i.pp.x, 0, i.pp.z) * 0.18) * 1.2 + nz * 0.15;
                        float y = i.pp.y * i.fxA.w + wob;
                        float id = floor(y), f = frac(y);
                        half3 t0 = LayerTint(id), t1 = LayerTint(id + 1), tm = LayerTint(id - 1);
                        half3 tint = f > 0.5 ? lerp(t0, t1, 0.5 * smoothstep(0.95, 1.0, f)) : lerp(tm, t0, smoothstep(0.0, 0.05, f) * 0.5 + 0.5);
                        col.rgb *= lerp(half3(1,1,1), tint, strataStr);
                    }
                }
                if (_TopStrength > 0) col.rgb = lerp(col.rgb, _TopColor.rgb, _TopStrength * smoothstep(_TopMin, 1, n.y));
                // 芝の地面の模様（上向きの面だけ、ワールドの xz で取る）
                if (_GroundPatch > 0 || _GroundFine > 0)
                {
                    half up = smoothstep(0.45, 0.85, n.y);
                    // 影を受ける面（島の上、uv7.z=1）はブースの座標で取る（ワールドの xz だと会場の置き場所で模様の並びが変わる。2026-10-18）
                    // 小さい浮島の上（uv7.z=2）は xy＝模様の位置そのもの（島の大きさに合わせて細かく。2026-10-20 てじゃ「ちっちゃい島の芝模様がスカスカ」）
                    float2 xz = i.sh.z > 1.5 ? i.sh.xy : (i.sh.z > 0.5 ? i.sh.xy * 4 - 2 : i.wp.xz);
                    half pg = tex2D(_GroundTex, xz / _GroundPatchScale).g;
                    half pb = tex2D(_GroundTex, xz / _GroundMidScale + 0.37).b;
                    half p = saturate(pg * 0.65 + pb * 0.35);
                    if (_GroundSteps > 0.5)
                    {
                        half q = p * _GroundSteps;
                        p = (floor(q) + smoothstep(0.42, 0.58, frac(q))) / _GroundSteps;
                    }
                    half3 tint = p > 0.5 ? lerp(half3(1,1,1), _GroundTintA.rgb, (p - 0.5) * 2) : lerp(_GroundTintB.rgb, half3(1,1,1), p * 2);
                    col.rgb *= lerp(half3(1,1,1), tint, _GroundPatch * up);
                    half fine = tex2D(_GroundTex, (xz + _GroundFineOffset.xy) / _GroundFineScale).r;
                    half fineFade = 1 - smoothstep(_GroundFineFade * 0.45, _GroundFineFade, distance(_WorldSpaceCameraPos, i.wp));
                    half fd = (fine - 0.5) * 2;
                    if (_GroundFineCrisp > 0)
                    {
                        half w = max(fwidth(fd) * 1.5, 0.02);
                        half crisp = sign(fd) * smoothstep(0.22 - w, 0.22 + w, abs(fd)) * 0.6;
                        fd = lerp(fd, crisp, _GroundFineCrisp);
                    }
                    col.rgb *= 1 + fd * _GroundFine * fineFade * up;
                }
                // 下向きの面だけ色をかける
                if (_UnderStrength > 0)
                {
                    half under = smoothstep(-0.15, 0.75, -n.y);
                    if (_UnderGrad > 0) under = max(under, 0.75 * smoothstep(_UnderGrad, 0, i.uv.y));
                    col.rgb *= lerp(1, _UnderColor.rgb, _UnderStrength * under);
                }
                float3 l = FUWA_BOOTH_SUN_DIR;
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);

                // ふんわり明暗：ハーフランバートを、少しだけ段っぽく（_Steps で境目の締まり）
                half ndl = dot(n, l);
                half wrap = saturate((ndl + _Wrap) / (1 + _Wrap));
                half band = smoothstep(0.5 - 0.5 * (1 - _Steps), 0.5 + 0.5 * (1 - _Steps), wrap);
                half lit = lerp(wrap, band, 0.5);
                lit *= lerp(1, i.ao, 0.85);   // 頂点に描いた影（元の太陽の影と同じ効き方）
                // 焼いた地面の影（物の影）：光の当たり具合を下げて影側の色に＋環境光のぶんも少し落とす（光だけだと薄かった）
                half bs = (i.sh.z > 0.5 && i.sh.z < 1.5) ? tex2D(_BakedShadow, i.sh.xy).r * _BakedShadowStrength : 0;
                lit *= 1 - bs;

                // 影側はグレーではなく紫寄りの色に
                half3 shade = lerp(1, _ShadowTint.rgb, _ShadowStrength);
                half3 light = lerp(shade, 1, lit) * FUWA_BOOTH_SUN_COLOR;
                half3 c = col.rgb * (light + FuwaBoothAmbient(n) * _AmbientStrength * shade);
                c *= lerp(half3(1, 1, 1), _BakedShadowColor.rgb, bs);

                // ふちの光（光の当たる側は少し強め）
                half rim = pow(1 - saturate(dot(n, v)), _RimPower) * i.fxB.y * (0.5 + 0.5 * lit);
                rim *= 1 - i.fade;
                c += _RimColor.rgb * rim;
                half3 em = _EmissionColor.rgb;
                if (_EmissionGradient > 0.5)
                {
                    // 光の色へ「塗り替える」光り方：下向き・ふちほど強く、てっぺんは控えめ（グラデーション）
                    half s = max(em.r, max(em.g, em.b));
                    half3 hue = em / max(s, 1e-4);
                    half k = saturate(s / max(_EmissionFull, 1e-3));
                    half g = lerp(0.3, 1.0, saturate(0.5 - 0.5 * n.y));
                    g = saturate(g + pow(1 - saturate(dot(n, v)), 2) * 0.5);
                    half3 glow = hue * lerp(0.5, 1.2, g);
                    c = lerp(c, glow, k * lerp(0.2, 1.0, g * g));
                }
                else c += em;

                return fixed4(c, 1);
            }
            ENDCG
        }

        // 奥行き（会場のカメラの深度テクスチャ）用。影を落とす相手のライトは無いが、深度には入れておく
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull]
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { V2F_SHADOW_CASTER; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); TRANSFER_SHADOW_CASTER_NORMALOFFSET(o) return o; }
            float4 frag (v2f i) : SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
}
