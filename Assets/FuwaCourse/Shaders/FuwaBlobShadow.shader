// プレイヤーなど動く物の足元の丸い影（ブロブシャドウ）。まん中が濃く、ふちへなめらかに消える。
// 色は影の紫寄り。_Alpha はスクリプトから（床から離れるほど薄く）
Shader "FuwaCourse/BlobShadow"
{
    Properties
    {
        _Color ("Color", Color) = (0.22, 0.16, 0.32, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.45
        _Softness ("Softness", Range(0.05, 1)) = 0.7
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -2, -2

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Alpha, _Softness;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2 - 1;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float r = length(i.uv);
                float a = 1 - smoothstep(1 - _Softness, 1, r);
                fixed4 c = fixed4(_Color.rgb, a * a * _Alpha);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(_Color.rgb, 0));
                return c;
            }
            ENDCG
        }
    }
}
