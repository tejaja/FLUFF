// 2–2 の焚き火と燃える島の炎（2026-11-01）。板をやめて、しずく形のかたまりを重ねた立体の炎。どこから見ても板に見えない（てじゃ「板感がすごい」、カメラに向けるのは VR で気持ち悪い）。
// 表面の模様を上へ流して形をふくらませ、先ほど大きくゆらす。正面は黄色、ふちと先は赤。光を受けず自分で光る。uv.x はかたまりごとのずれ、uv.y は下 0〜先 1
// 大きさはオブジェクト座標で作るので、まとめ描き（ダイナミックバッチング）を切っている
Shader "FuwaCourse/Flame3D"
{
    Properties
    {
        [NoScaleOffset] _FlowTex ("上へ流す模様（R）", 2D) = "gray" {}
        [HDR] _Hot ("内側の色", Color) = (1.2, 0.86, 0.3, 1)
        [HDR] _Mid ("まん中の色", Color) = (1.2, 0.46, 0.07, 1)
        [HDR] _Tip ("ふちの色", Color) = (1, 0.21, 0.055, 1)
        _Speed ("動きの速さ", Float) = 1
        _FlowSpeed ("模様が上へ流れる速さ", Float) = 0.7
        _FlowScale ("模様の細かさ", Float) = 0.9
        _Bulge ("表面のふくらみ（オブジェクトの長さ）", Float) = 0.1
        _Wag ("先のゆれ（オブジェクトの長さ）", Float) = 0.1
        _Stretch ("高さののびちぢみ", Range(0,0.5)) = 0.14
        _Soft ("色の境目のぼかし", Range(0.001,0.3)) = 0.06
        _Fresnel ("ふちの色の広さ（大きいほど太い）", Range(0.5,5)) = 1.6
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
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "FuwaFireFar.cginc"

            sampler2D _FlowTex;
            half4 _Hot, _Mid, _Tip;
            float _Speed, _FlowSpeed, _FlowScale, _Bulge, _Wag, _Stretch;
            half _Soft, _Fresnel;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float y : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; float n : TEXCOORD3; UNITY_FOG_COORDS(4) };

            v2f vert (appdata v)
            {
                v2f o;
                float t = _Time.y * _Speed, y = v.uv.y, ph = v.uv.x;
                float2 a = float2(v.vertex.x + v.vertex.z * 0.7 + ph, v.vertex.y) * _FlowScale + float2(0, -t * _FlowSpeed);
                float n = tex2Dlod(_FlowTex, float4(a, 0, 0)).r;
                float3 p = v.vertex.xyz + v.normal * (n - 0.42) * _Bulge * (0.35 + y * 1.3);
                p.y *= 1.0 + _Stretch * (sin(t * 3.1 + ph * 2.0) * 0.6 + sin(t * 5.3 + ph * 3.7) * 0.4);
                float hh = y * y;
                p.x += (sin(t * 2.3 + ph + y * 2.5) + 0.5 * sin(t * 4.1 + ph * 1.7 + y * 4.0)) * _Wag * hh;
                p.z += (sin(t * 1.9 + ph * 1.3 + y * 2.5 + 2.0) + 0.5 * sin(t * 3.7 + ph * 2.1 + y * 4.0)) * _Wag * hh;
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.y = y; o.n = n;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, float4(p, 1)).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half facing = saturate(dot(normalize(i.wn), normalize(_WorldSpaceCameraPos - i.wp)));
                // 正面ほど熱く、上ほど赤く
                half k = pow(facing, _Fresnel) * 1.15 + (i.n - 0.5) * 0.5 - i.y * 0.55;
                half3 c = lerp(_Tip.rgb, _Mid.rgb, smoothstep(0.2 - _Soft, 0.2 + _Soft, k));
                c = lerp(c, _Hot.rgb, smoothstep(0.52 - _Soft, 0.52 + _Soft, k));
                c = FuwaFireCalm(c, FuwaFireFar(i.wp));
                fixed4 o = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
    }
    FallBack Off
}
