// 2–2 の溶岩（穴・みぞ・落ちる所。2026-10-31）。火山（FuwaCourse/Volcano）の溶岩と同じ色と模様で、光を受けず自分で光る。
// UV は使わずワールド座標で模様を取るので、箱や円盤でも伸びない。上向きの面はゆっくりうごめき、横向きの面は下へ流れる。Static にしても同じ。
Shader "FuwaCourse/Lava"
{
    Properties
    {
        [NoScaleOffset] _FlowTex ("溶岩の模様 (R 明暗 G 筋 B かさぶた、継ぎ目なし)", 2D) = "gray" {}
        [HDR] _LavaHot ("いちばん熱い色", Color) = (1.2, 0.3, 0.06, 1)
        [HDR] _LavaMid ("ふつうの色", Color) = (1.0, 0.085, 0.02, 1)
        [HDR] _LavaDark ("暗い色", Color) = (0.5, 0.025, 0.012, 1)
        _CrustColor ("かさぶたの色", Color) = (0.2, 0.07, 0.06, 1)
        _Crust ("かさぶたの濃さ（上向きの面だけ）", Range(0,1)) = 0.9
        _Scale ("模様の数（/m）", Float) = 0.28
        _PoolSpeed ("上向きの面の動き", Float) = 0.016
        _FallSpeed ("横向きの面の流れる速さ（模様 / 秒）", Float) = 0.5
        _Wave ("垂れる溶岩のゆれ（0 でなし。_WaveTop からの深さに比例）", Float) = 0
        _WaveSpeed ("ゆれの速さ", Float) = 0.9
        _WaveTop ("ゆれの根元の高さ（ワールド y）", Float) = 0
        _Fray ("先のこの割合でちぎれて消える（uv.x が根元 0〜先 1。0 でなし）", Range(0,1)) = 0
        _FrayColor ("ちぎれる所の色", Color) = (1, 0.6, 0.2, 1)
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
            #include "UnityCG.cginc"
            #include "FuwaFireFar.cginc"

            sampler2D _FlowTex;
            half4 _LavaHot, _LavaMid, _LavaDark;
            fixed4 _CrustColor;
            half _Crust;
            float _Scale, _PoolSpeed, _FallSpeed, _Wave, _WaveSpeed, _WaveTop, _Fray;
            fixed4 _FrayColor;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float3 wn : TEXCOORD1; UNITY_FOG_COORDS(2) float fray : TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.fray = v.uv.x;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                if (_Wave > 0.0001)
                {
                    // 根元は動かず、先へ行くほどゆっくり伸び縮みして横へゆれる。表と裏で同じだけ動くように法線は使わない
                    float d = max(0.0, _WaveTop - o.wp.y), t = _Time.y * _WaveSpeed;
                    float a = (o.wp.x + o.wp.z) * 0.9;
                    o.wp.y -= d * _Wave * (0.5 + 0.5 * sin(t + a));
                    o.wp.x += d * _Wave * 0.35 * sin(t * 1.7 + a * 1.8 + d * 1.2);
                    o.wp.z += d * _Wave * 0.35 * sin(t * 1.3 - a * 1.4 + d * 0.9);
                }
                o.pos = UnityWorldToClipPos(o.wp);
                o.wn = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float t = _Time.y;
                half up = step(0.5, abs(normalize(i.wn).y));
                fixed4 f1, f2;
                if (up > 0.5)
                {
                    float2 xz = i.wp.xz * _Scale;
                    f1 = tex2D(_FlowTex, xz + float2(t * _PoolSpeed, t * _PoolSpeed * 1.4));
                    f2 = tex2D(_FlowTex, xz * 1.9 + float2(-t * _PoolSpeed * 1.6, t * _PoolSpeed * 0.8));
                }
                else
                {
                    // 横向き：下へ流れる。縦に長い模様
                    float h = (i.wp.x + i.wp.z) * _Scale * 1.6;
                    f1 = tex2D(_FlowTex, float2(h, i.wp.y * _Scale * 0.45 + t * _FallSpeed));
                    f2 = tex2D(_FlowTex, float2(h * 2.0 + 0.37, i.wp.y * _Scale * 0.7 + t * _FallSpeed * 1.9));
                }
                half h2 = saturate(f1.r * 0.7 + f2.r * 0.32 + f2.g * 0.22 - 0.3);
                half hq = h2 * 3; h2 = (floor(hq) + smoothstep(0.3, 0.7, frac(hq))) / 3;
                half3 c = h2 < 0.5 ? lerp(_LavaDark.rgb, _LavaMid.rgb, h2 * 2) : lerp(_LavaMid.rgb, _LavaHot.rgb, (h2 - 0.5) * 2);
                half crust = up * smoothstep(0.5, 0.65, f1.b) * smoothstep(0.62, 0.42, f1.r) * (1 - f2.g);
                c = lerp(c, _CrustColor.rgb, crust * _Crust);
                if (_Fray > 0.001)
                {
                    // 水の滝（FuwaStage2/Waterfall の _FrayLen）と同じ消え方：先へ行くほど色が変わって、下へ流れるノイズで穴があいてちぎれる。テクスチャの R は 0.3〜0.7 くらいなので広げて使う
                    float tf = saturate((i.fray - (1 - _Fray)) / _Fray);
                    float h3 = (i.wp.x + i.wp.z) * _Scale * 7.0;
                    fixed4 f3 = tex2D(_FlowTex, float2(h3, i.wp.y * _Scale * 1.2 + t * _FallSpeed * 1.3));
                    float nf = saturate((f1.r * 0.45 + f2.r * 0.25 + f3.r * 0.3 - 0.33) / 0.34);
                    clip(nf - smoothstep(0.1, 1.0, tf) * 1.05 + 0.001);   // + にしないと、消え始める前の所にも点の穴があいた
                    c = lerp(c, _FrayColor.rgb, smoothstep(0.05, 0.75, tf) * 0.85);
                }
                c = FuwaLavaCalm(c, FuwaFireFar(i.wp));
                fixed4 o = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
        // 深度テクスチャに入れる（入らないと、深度で描くふわふわのウソ影が溶岩の上に出ない。2026-10-31 てじゃ「影がないから高く見える」）
        UsePass "FuwaCourse/ToonLit/SHADOWCASTER"
    }
    FallBack Off
}
