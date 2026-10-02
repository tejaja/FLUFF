// Fuwa/PanelSprite の曲げ版（_BendRadius、文字の Fuwa/TMP Curved と同じ曲げ方）。
// 看板・ラベルの角丸パネル用。半透明だけど奥行き（Zバッファ）を書くので、
// 手前のパネルの後ろにある別の看板の文字が透けて見えない。
// 文字(TextMeshPro, Queue 3000)より先に描くため Queue は 2990。
Shader "Fuwa/PanelSprite Curved"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff (これ以下は奥行きも書かない)", Range(0,1)) = 0.3
        _BendRadius ("Bend Radius (0=曲げない)", Float) = 0
        _BendOffset ("Bend Offset (文字と中心を合わせる, xyz)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "DisableBatching"="True" "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            fixed _Cutoff;
            float _BendRadius;
            float4 _BendOffset;
            #include "Assets/FuwaCourse/Shaders/FuwaBend.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                UNITY_FOG_COORDS(1)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // 親（文字）の座標に直してから曲げて戻す（パネルは文字から少しずれた位置にあるため）
                float3 p = FuwaBend(v.vertex.xyz + _BendOffset.xyz, _BendRadius) - _BendOffset.xyz;
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.uv = v.uv;
                o.color = v.color * _Color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                // 角丸の外側（ほぼ透明な所）は捨てて、奥行きも書かない
                clip(c.a - _Cutoff);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
