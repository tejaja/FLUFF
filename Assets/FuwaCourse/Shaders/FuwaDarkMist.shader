// 遠くから見たやみのもりを包む「暗いもや」（2026-10-08）。だ円体のメッシュ1つで、視線がもやの中を通る長さで濃さを決める。
// ふちは通る距離が短いので自然に薄く、まん中ほど濃い。下ほど少し濃く、ゆっくりゆらぐムラをつける。
// 外から見る用（表の面だけ描く）。中にいる時は遠景ごと消えるので考えない。
Shader "FuwaCourse/DarkMist"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.19, 0.29, 1)
        _Density ("Density (per m)", Float) = 0.035
        _MaxAlpha ("Max Alpha", Range(0, 1)) = 0.9
        _LowBoost ("Lower part extra", Range(0, 2)) = 0.6
        _Wobble ("Wobble", Range(0, 1)) = 0.25
        _EdgeSoft ("Edge Softness", Range(0.01, 1)) = 0.8
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Back ZWrite Off ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _Color; float _Density, _MaxAlpha, _LowBoost, _Wobble, _EdgeSoft;
            struct v2f { float4 pos : SV_POSITION; float3 opos : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata_base v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.opos = v.vertex.xyz; return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                // オブジェクト空間（半径1の球。組み込みの Sphere メッシュ）で視線と球の交わりを求め、通る長さをワールドの距離に直す
                float3 camO = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos, 1)).xyz;
                float3 d = normalize(i.opos - camO);
                float b = dot(camO, d), c = dot(camO, camO) - 1.0;
                float h = b * b - c;
                if (h <= 0) discard;
                h = sqrt(h);
                float t0 = max(-b - h, 0), t1 = -b + h;
                float3 segW = mul((float3x3)unity_ObjectToWorld, d * (t1 - t0));
                float len = length(segW);
                // 通る所の高さ（オブジェクト空間の中点の y）：下ほど濃く
                float3 mid = camO + d * (t0 + t1) * 0.5;
                float low = saturate(0.5 - mid.y * 0.5);
                // ゆっくり動くムラ（安いサイン波の重ね）
                float3 wp = mul(unity_ObjectToWorld, float4(mid, 1)).xyz;
                float wob = sin(wp.x * 0.11 + _Time.y * 0.15) * sin(wp.z * 0.09 - _Time.y * 0.12) * 0.5 + 0.5;
                float dens = _Density * (1 + low * _LowBoost) * lerp(1, 0.6 + wob * 0.8, _Wobble);
                float a = (1 - exp(-len * dens)) * _MaxAlpha;
                // ふちをふわっと：視線が中心から遠いほど（h が小さいほど）薄く
                a *= smoothstep(0, _EdgeSoft, h);
                return fixed4(_Color.rgb, a * _Color.a);
            }
            ENDCG
        }
    }
}
