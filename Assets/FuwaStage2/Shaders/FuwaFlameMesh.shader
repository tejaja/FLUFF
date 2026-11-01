// 2–2 の焚き火と燃える島の炎（2026-11-01）。FlameVolume の見た目のまま軽くした版（てじゃ「重くなるのはやだ」）。ふつうのメッシュを頂点で動かすだけで、色はオレンジと黄色の 2 色
// uv.x：0 = 外のたまご、1 = 中の黄色、2 = ちぎれて飛ぶかたまり。uv.y：かたまりごとのずれ。ノイズを読むのは頂点だけ（上のほうだけピクセルで 1 回、穴をあける）
// 黄色は深さだけ手前にずらして外から透けて見せる（描く位置は変えない）
Shader "FuwaCourse/FlameMesh"
{
    Properties
    {
        [NoScaleOffset] _Noise ("立体ノイズ（R、タイル）", 3D) = "gray" {}
        _Orange ("外の色", Color) = (0.93, 0.43, 0, 1)
        _Yellow ("中の色", Color) = (0.98, 1, 0.04, 1)
        _Speed ("動きの速さ", Float) = 1
        _FlowSpeed ("模様が上へ流れる速さ（タイル／秒）", Float) = 0.75
        _NoiseScale ("模様の細かさ（タイル／オブジェクトの長さ）", Float) = 0.75
        _AmpBase ("根元のでこぼこ", Float) = 0.1
        _AmpTop ("先のでこぼこ", Float) = 0.42
        _Tear ("先の穴のあき具合", Range(0,1.5)) = 0.75
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "DisableBatching"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull Off
            ZWrite On
            AlphaToMask On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "FuwaFireFar.cginc"
            #define BOX_H 1.6

            sampler3D _Noise;
            half4 _Orange, _Yellow;
            float _Speed, _FlowSpeed, _NoiseScale, _AmpBase, _AmpTop, _Tear;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 np : TEXCOORD0;     // 穴をあけるノイズの座標（流れ込み）
                half3 col : TEXCOORD1;
                half tear : TEXCOORD2;     // 0 なら穴なし
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float NoiseAt(float3 w, float t)
            {
                float3 a = w * _NoiseScale; a.y -= frac(t * _FlowSpeed);
                float3 b = w * _NoiseScale * 1.7 + float3(0.37, 0, 0.61); b.y -= frac(t * _FlowSpeed * 1.45);
                float n = tex3Dlod(_Noise, float4(a, 0)).r * 0.66 + tex3Dlod(_Noise, float4(b, 0)).r * 0.34;
                return (n - 0.5) * 2.0;
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float t = _Time.y * _Speed, layer = v.uv.x, seed = v.uv.y;
                float2 sway = float2((sin(t * 2.3) + 0.5 * sin(t * 3.9)) * 0.05, (sin(t * 1.9 + 2.0) + 0.5 * sin(t * 3.3)) * 0.05);
                float3 p = v.vertex.xyz, nor = v.normal;
                float yellow = layer > 0.5 && layer < 1.5 ? 1 : 0;
                if (layer > 1.5)
                {
                    // ちぎれて飛ぶかたまり：上がりながらふくらんで、消える
                    float ph = frac(t * (0.55 + seed * 0.35) + seed * 7.13);
                    float size = (0.05 + 0.055 * frac(seed * 3.7)) * pow(sin(ph * 3.14159), 0.7);
                    float3 c = float3(sin(seed * 41.0) * 0.14, lerp(0.92, 1.5, ph), cos(seed * 29.0) * 0.14);
                    c.xz += sway * c.y / BOX_H * 1.6 + float2(sin(ph * 4.0 + seed * 9.0), cos(ph * 3.3 + seed * 5.0)) * 0.05;
                    float n = NoiseAt(c + p * 0.2, t);
                    p = c + p * size * (1.0 + n * 0.7);
                }
                else
                {
                    float k = saturate(p.y / BOX_H);
                    p.xz += sway * k;
                    float amp = lerp(_AmpBase, _AmpTop, k) * (yellow > 0.5 ? 0.9 : 1.0);
                    p += nor * NoiseAt(p, t) * amp;
                    o.tear = yellow > 0.5 ? 0 : smoothstep(0.42, 0.95, k) * _Tear;
                }
                float3 wp = mul(unity_ObjectToWorld, float4(p, 1)).xyz;
                o.pos = UnityWorldToClipPos(wp);
                if (yellow > 0.5)
                {
                    // 深さだけ手前へ（根元は少しだけ。多いと穴のふちを越えて見える）
                    float len = length(unity_ObjectToWorld[0].xyz);
                    float bias = lerp(0.12, 0.46, saturate(p.y / 0.42)) * len;
                    float4 near = UnityWorldToClipPos(wp + normalize(_WorldSpaceCameraPos - wp) * bias);
                    o.pos.z = near.z / near.w * o.pos.w;
                }
                float3 a = p * _NoiseScale * 1.35; a.y -= frac(t * _FlowSpeed * 1.2);
                o.np = a;
                half3 c = yellow > 0.5 ? _Yellow.rgb : _Orange.rgb;
                o.col = FuwaFireCalm(c, FuwaFireFar(wp));
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half cover = 1;
                if (i.tear > 0.001)
                {
                    float d = tex3D(_Noise, i.np).r - i.tear * 0.5;   // tear が大きいほど穴が増える
                    cover = saturate(d / max(fwidth(d), 1e-4) + 0.5);
                    clip(cover - 0.01);
                }
                fixed4 o = fixed4(i.col, cover);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
