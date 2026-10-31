// 2–2 の火の玉（2026-10-31）。光を受けず自分で光る。まん中は黄色、ふちは赤。模様は上へ流れて、輪郭が炎のようにゆらゆらする。
// 大きさは 1 の球（Unity の Sphere）のオブジェクト座標で作るので、まとめ描き（ダイナミックバッチング）を切っている。当たりは見た目のゆらゆらに関係なくスクリプトの球。
Shader "FuwaCourse/Fireball"
{
    Properties
    {
        [NoScaleOffset] _FlowTex ("溶岩の模様 (R 明暗 G 筋 B かさぶた、継ぎ目なし)", 2D) = "gray" {}
        [HDR] _Hot ("まん中の色", Color) = (1.4, 1.0, 0.4, 1)
        [HDR] _Mid ("ふつうの色", Color) = (1.25, 0.5, 0.08, 1)
        [HDR] _Edge ("ふちの色", Color) = (0.95, 0.16, 0.04, 1)
        _Scale ("模様の数（球の直径あたり）", Float) = 1.1
        _Speed ("上へ流れる速さ", Float) = 0.9
        _Wobble ("輪郭のゆらぎ（球の半径の割合に近い）", Range(0,0.5)) = 0.17
        _Fresnel ("ふちの色の広さ（大きいほど細い）", Range(0.5,6)) = 1.7
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

            sampler2D _FlowTex;
            half4 _Hot, _Mid, _Edge;
            float _Scale, _Speed, _Wobble;
            half _Fresnel;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 op : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; UNITY_FOG_COORDS(3) };

            v2f vert (appdata v)
            {
                v2f o;
                float t = _Time.y;
                float2 a = float2(v.vertex.x + v.vertex.z * 0.7, v.vertex.y) * _Scale + float2(0.13, -t * _Speed * 0.6);
                float n = tex2Dlod(_FlowTex, float4(a, 0, 0)).r;
                // 上の方ほど大きくゆらす（炎の先のように）
                float up = saturate(v.normal.y * 0.5 + 0.6);
                float3 p = v.vertex.xyz + v.normal * (n - 0.42) * _Wobble * (0.5 + up);
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.op = v.vertex.xyz;
                o.wn = UnityObjectToWorldNormal(v.normal);
                o.wp = mul(unity_ObjectToWorld, float4(p, 1)).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                // 2 方向から取って混ぜる（球のどこでも模様がのびない）
                float3 an = abs(normalize(i.op)); an = an / (an.x + an.y + an.z);
                float2 down = float2(0, -t * _Speed);
                half r1 = tex2D(_FlowTex, i.op.xy * _Scale + down).r;
                half r2 = tex2D(_FlowTex, i.op.zy * _Scale + down + 0.37).r;
                half r3 = tex2D(_FlowTex, i.op.xz * _Scale + float2(t * 0.2, t * 0.13)).r;
                half h = r1 * an.z + r2 * an.x + r3 * an.y;
                half q = h * 3; h = (floor(q) + smoothstep(0.3, 0.7, frac(q))) / 3;
                float3 v = normalize(_WorldSpaceCameraPos - i.wp);
                half facing = saturate(dot(normalize(i.wn), v));
                // まん中ほど熱い
                half k = saturate(pow(facing, _Fresnel) * 1.1 + (h - 0.5) * 0.7);
                half3 c = k < 0.5 ? lerp(_Edge.rgb, _Mid.rgb, k * 2) : lerp(_Mid.rgb, _Hot.rgb, (k - 0.5) * 2);
                fixed4 o = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, o);
                return o;
            }
            ENDCG
        }
        UsePass "FuwaCourse/ToonLit/SHADOWCASTER"
    }
    FallBack Off
}
