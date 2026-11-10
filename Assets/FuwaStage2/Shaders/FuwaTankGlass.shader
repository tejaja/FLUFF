// 2–4 駆動歯車の上の回るタンクのガラス（2026-11-10 てじゃ「この歯車の上あたりに、なんか速く回ってるタンク」）。
// うすい色＋ふちほど白く（フレネル）＋たての光の筋。裏（奥の面）→表の順に描いて、中の綿あめが透けて見える
Shader "FuwaCourse/TankGlass"
{
    Properties
    {
        _Tint ("ガラスの色", Color) = (0.85, 0.95, 1, 1)
        _Alpha ("まん中の濃さ", Range(0,1)) = 0.08
        _RimAlpha ("ふちの濃さ", Range(0,1)) = 0.55
        _RimPow ("ふちの細さ", Range(0.5,8)) = 2.5
        _Streak ("光の筋の強さ", Range(0,1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        CGINCLUDE
        #include "UnityCG.cginc"
        half4 _Tint; half _Alpha, _RimAlpha, _RimPow, _Streak;
        struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct v2f { float4 pos : SV_POSITION; float3 wn : TEXCOORD0; float3 wv : TEXCOORD1; float3 on : TEXCOORD2; UNITY_FOG_COORDS(3) UNITY_VERTEX_OUTPUT_STEREO };
        v2f vert (appdata v)
        {
            v2f o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex);
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.wn = UnityObjectToWorldNormal(v.normal);
            o.wv = _WorldSpaceCameraPos - wp;
            o.on = v.normal;
            UNITY_TRANSFER_FOG(o, o.pos);
            return o;
        }
        half4 shade (v2f i, half back)
        {
            float3 n = normalize(i.wn) * (back > 0.5 ? -1 : 1), vd = normalize(i.wv);
            half f = pow(1 - saturate(abs(dot(n, vd))), _RimPow);
            // 筒の向き（物の中の法線の角度）で決まる、たての筋2本（タンクと一緒に回る → 回っているのが見える）
            float ang = atan2(i.on.z, i.on.x);
            half s = smoothstep(0.85, 1, cos(ang * 2 + 0.6)) * (1 - abs(i.on.y)) * _Streak;
            half a = saturate(lerp(_Alpha, _RimAlpha, f) + s * 0.6) * (back > 0.5 ? 0.6 : 1);
            half3 c = lerp(_Tint.rgb, half3(1, 1, 1), saturate(f + s));
            half4 col = half4(c, a);
            UNITY_APPLY_FOG(i.fogCoord, col);
            return col;
        }
        ENDCG
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull Front ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            half4 frag (v2f i) : SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return shade(i, 1); }
            ENDCG
        }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Cull Back ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            half4 frag (v2f i) : SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return shade(i, 0); }
            ENDCG
        }
    }
    FallBack Off
}
