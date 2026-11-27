// あそびばのたね（竜巻・重力）のオフの時のオーラ（2026-11-27 てじゃ「オフ時は球の周囲に球近い色のオーラ」）
// 球のまわりのふんわりした光：ふち（視線と面が平行な所）ほど濃く、ゆっくり息をする。_Alpha でフェード（FuwaSeedToggle が書く）
Shader "Fuwa/SeedAura"
{
    Properties
    {
        _Color ("Color", Color) = (0.6, 0.95, 0.9, 1)
        _Alpha ("Alpha（フェード）", Range(0, 1)) = 1
        _Rim ("ふちの濃さ", Range(0, 2)) = 0.9
        _Center ("まん中の濃さ", Range(0, 1)) = 0.12
        _Power ("ふちのしぼり", Range(0.5, 6)) = 2.2
        _Pulse ("息の大きさ", Range(0, 1)) = 0.25
        _PulseSpeed ("息の速さ", Float) = 2.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            fixed4 _Color; half _Alpha, _Rim, _Center, _Power, _Pulse, _PulseSpeed;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata i)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(i); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 wp = mul(unity_ObjectToWorld, i.vertex).xyz;
                o.pos = UnityWorldToClipPos(wp);
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = _WorldSpaceCameraPos - wp;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float nv = saturate(dot(normalize(i.n), normalize(i.v)));
                float rim = pow(1 - nv, _Power) * _Rim + _Center * nv;
                float breathe = 1 + _Pulse * sin(_Time.y * _PulseSpeed);
                return fixed4(_Color.rgb, saturate(rim * breathe) * _Alpha);
            }
            ENDCG
        }
    }
}
