// ブースの遊べるエアガン用の粒（2026-10-19）。FLUFF の Legacy Shaders/Particles（Alpha Blended / Additive）と
// Particles/Standard Unlit の代わり。色＝頂点の色×_Color×絵×_Mul（Legacy は 2 倍なので _Mul=2）。
// 足し算にする時は _DstBlend=One（Additive）。キーワードなし・霧なし・ライトなし・ソフトパーティクルなし（ブースの規定に合わせる）
// キューは会場のメニューより手前に出ないよう 3000 未満（Transparent-8。reference_vket_ui_render_queue）
Shader "Custom/2562/Fuwa/Particle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0.5, 0.5, 0.5, 0.5)
        _Mul ("明るさの倍率（Legacy の粒は 2）", Float) = 2
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend（10=ふつう、1=足し算）", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent-8" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4 _Color; float _Mul;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = _Mul * i.color * _Color * tex2D(_MainTex, i.uv);
                c.a = saturate(c.a);
                return c;
            }
            ENDCG
        }
    }
}
