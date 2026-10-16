// たんぽぽの軽い版の絵を撮る時だけ使う：頂点カラーをそのまま（光なし）。軽い版のシェーダーで光を当て直すので、絵には陰を入れない
Shader "Hidden/FuwaBakeUnlitVC"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; };
            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; return o; }
            fixed4 frag(v2f i) : SV_Target { return fixed4(GammaToLinearSpace(i.color.rgb), 1); }
            ENDCG
        }
    }
}
