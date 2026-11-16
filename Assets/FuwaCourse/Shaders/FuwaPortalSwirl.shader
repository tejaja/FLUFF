// 2026-11-16 あそびばのポータル（コース2–5 のおもちゃ）：穴の中のうずまき。uv は円の中心 (0.5,0.5)、ふちほど濃い。ゆっくり回る
Shader "Fuwa/PortalSwirl"
{
    Properties
    {
        _Color ("色", Color) = (0.45, 0.8, 1, 1)
        _Core ("まん中の色", Color) = (1, 1, 1, 1)
        _Alpha ("濃さ", Range(0, 1)) = 0.8
        _Arms ("うずの数", Float) = 3
        _Twist ("ねじれ", Float) = 6
        _Speed ("回る速さ", Float) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off ZWrite Off Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Color, _Core; half _Alpha; float _Arms, _Twist, _Speed;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; UNITY_TRANSFER_FOG(o, o.pos); return o;
            }
            half4 frag (v2f i) : SV_Target
            {
                float2 q = i.uv * 2 - 1;
                float r = length(q), a = atan2(q.y, q.x);
                float sw = 0.5 + 0.5 * sin(a * _Arms + r * _Twist - _Time.y * _Speed * 3.0);
                float edge = smoothstep(1.0, 0.92, r);
                float rim = smoothstep(0.55, 0.98, r);
                float core = exp(-r * r * 6.0);
                half a0 = _Alpha * edge * (0.35 + 0.45 * sw * (0.3 + rim) + 0.3 * rim);
                half3 col = lerp(_Color.rgb, _Core.rgb, saturate(core * 0.8 + sw * 0.15));
                half4 c = half4(col * a0, a0);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, half4(0, 0, 0, 0));
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
