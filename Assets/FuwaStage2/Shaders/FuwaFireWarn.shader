// 火炎放射が当たる所を地面に見せる赤いグラデーション（2026-11-02 てじゃ）。uv.x が横（まん中が濃い）、uv.y が炎の根元→先。
// 道と重なってチラつかないよう、少し持ち上げた上で Offset をかけて、深さは書かない
Shader "FuwaCourse/FireWarn"
{
    Properties
    {
        _Color ("色", Color) = (1, 0.1, 0.05, 1)
        _Root ("根元の濃さ", Range(0,1)) = 0.75
        _Tip ("先の濃さ", Range(0,1)) = 0.4
        _Alpha ("出ている量（スクリプトが動かす）", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Geometry+20" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Offset -2, -2
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Root, _Tip, _Alpha;

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
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                half side = 1 - abs(i.uv.x * 2 - 1);
                half a = smoothstep(0, 0.55, side) * lerp(_Root, _Tip, i.uv.y) * smoothstep(1, 0.88, i.uv.y) * _Alpha;
                fixed4 c = fixed4(_Color.rgb, a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
