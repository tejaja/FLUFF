// クモの巣の真下の床に落とす、ぼんやりした影の帯（奥行きの目印）。uv.x = 帯の長さ方向 0〜1、uv.y = 幅方向 0〜1。
// 床にぴったり重ねるので、Offset で手前に寄せて Z ファイトしないようにする。
Shader "FuwaCourse/WebFloorShadow"
{
    Properties
    {
        _Color ("Color", Color) = (0.13, 0.07, 0.22, 0.6)
        _EndFade ("端のぼかし (0〜0.5)", Range(0, 0.5)) = 0.12
    }
    SubShader
    {
        Tags { "Queue"="Transparent-20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Offset -2, -2
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _EndFade;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float across = 1 - smoothstep(0.0, 0.5, abs(i.uv.y - 0.5));   // 真ん中が濃く、ふちはふわっと
                float along = smoothstep(0.0, _EndFade, i.uv.x) * smoothstep(0.0, _EndFade, 1 - i.uv.x);
                fixed4 c = fixed4(_Color.rgb, _Color.a * across * along);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
