// Vketブース版 VentHoleMask（元は FLUFF の FuwaCourse/VentHoleMask。エアガンの銃口の穴の口）。multi_compile なし。ステンシル 37（規定は200以下）。
// 見えている所だけステンシルに印をつけて、そこの奥行きを「いちばん遠く」に戻す。色は描かない。
// このあと FuwaVentShaft が、印の付いた所だけに縦穴の内側を描く
// ※奥行きを書き換えるパスは ZTest が書き換えた後の奥行きで判定されてしまうので、「印をつける」と「奥行きを戻す」を2パスに分ける
Shader "Custom/2562/Fuwa/VentHoleMask"
{
    Properties { }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        CGINCLUDE
        #include "UnityCG.cginc"
        struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct v2f { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
        v2f vert(appdata v)
        {
            v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            o.pos = UnityObjectToClipPos(v.vertex); return o;
        }
        ENDCG
        // 1) 見えている穴の口に印
        Pass
        {
            Stencil { Ref 37 Comp Always Pass Replace }
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i) : SV_Target { return 0; }
            ENDCG
        }
        // 2) 印の所の奥行きを、いちばん遠くに戻す
        Pass
        {
            Stencil { Ref 37 Comp Equal }
            ColorMask 0
            ZWrite On
            ZTest Always
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag(v2f i, out float depth : SV_Depth) : SV_Target
            {
            #if defined(UNITY_REVERSED_Z)
                depth = 0;
            #else
                depth = 1;
            #endif
                return 0;
            }
            ENDCG
        }
    }
}
