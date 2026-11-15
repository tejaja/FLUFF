// 2026-11-15 2–5 クリスタルの広場：ためで空を少し暗く（てじゃ「チャージで空もちょっと暗くするとか」）
//  広場のまわりの大きい球（半径400m）の内がわ。スカイボックスのあと（Transparent-100）に深度テストつきで描く
//  → 400m より手前の景色には負けるので、暗くなるのは空（と遠い景色）だけ。_Alpha はスクリプト（FuwaShockwave）が MPB で
Shader "Fuwa/SkyDim"
{
    Properties
    {
        _Color ("色", Color) = (0.08, 0.03, 0.14, 1)
        _Alpha ("濃さ（スクリプト）", Range(0, 1)) = 0
        _TopBoost ("上ほど濃く", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-100" "IgnoreProjector"="True" "PreviewType"="Skybox" }
        Pass
        {
            Cull Front ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            half4 _Color; half _TopBoost;
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float, _Alpha)
            UNITY_INSTANCING_BUFFER_END(Props)
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            half4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float a = UNITY_ACCESS_INSTANCED_PROP(Props, _Alpha);
                float3 d = normalize(i.wp - _WorldSpaceCameraPos);
                a *= lerp(1 - _TopBoost, 1, saturate(d.y * 0.5 + 0.5));
                return half4(_Color.rgb, saturate(a));
            }
            ENDCG
        }
    }
    FallBack Off
}
