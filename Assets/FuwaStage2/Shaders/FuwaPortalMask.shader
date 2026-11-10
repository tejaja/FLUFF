// 2026-11-10 2–5 そらのいせき：壁に開いたポータルの穴（てじゃ「ポータルガンのポータルみたいな、壁に開いてて向こう側がみえてるタイプ」）
//  色は何も描かない。穴の見えている所にステンシル _StencilRef を書く。そのあと（キュー 2002〜）行き先の島のコピー（FuwaCourse/ToonLit の Comp=Equal）が穴の中だけに描かれる
//  キューは 2001：ふつうの物（2000）のあと。手前の物（ポータルの枠など）にかくれた所には書かない（ZTest LEqual、ZWrite Off）
Shader "Fuwa/PortalMask"
{
    Properties
    {
        _StencilRef ("Stencil Ref", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" "IgnoreProjector"="True" "DisableBatching"="True" }
        Pass
        {
            ColorMask 0
            ZWrite Off
            ZTest LEqual
            Cull Back
            Stencil { Ref [_StencilRef] Comp Always Pass Replace }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); return o;
            }
            fixed4 frag (v2f i) : SV_Target { return 0; }
            ENDCG
        }
    }
}
