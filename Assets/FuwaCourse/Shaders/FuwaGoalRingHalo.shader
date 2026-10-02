// ゴールのリングが光った時の、まわりのぼんやりした光（加算）。
// メッシュの頂点アルファ（リングの中心線で1、外側で0）でぼかす。FuwaGoal が _Glow を動かす。
Shader "FuwaCourse/GoalRingHalo"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.8, 0.9, 1)
        _Glow ("Glow", Range(0, 4)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent+1" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Off
            Offset -1, -1
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Glow;

            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; fixed a : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.a = v.color.a;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half a = i.a * i.a * _Glow * 0.5;
                return fixed4(_Color.rgb * a, 0);
            }
            ENDCG
        }
    }
}
