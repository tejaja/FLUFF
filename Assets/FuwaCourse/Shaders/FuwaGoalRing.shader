// ゴールのリング。普段はピンクにうっすら光る輪。
// ゴールした瞬間に FuwaGoal が _Glow を上げてパッと光らせ、_Alpha を下げてフェードアウトさせる。
Shader "FuwaCourse/GoalRing"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.55, 0.8, 1)
        _Emission ("Emission", Range(0, 2)) = 0.5
        _GlowColor ("Glow Color", Color) = (1, 0.95, 0.75, 1)
        _Glow ("Glow", Range(0, 4)) = 0
        _Alpha ("Alpha", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "DisableBatching"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Emission;
            fixed4 _GlowColor;
            half _Glow;
            half _Alpha;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 上から光が当たった程度のやわらかい陰影
                half shade = 0.7 + 0.3 * saturate(normalize(i.n).y * 0.5 + 0.5);
                half3 c = _Color.rgb * (shade + _Emission);
                c = lerp(c, _GlowColor.rgb * (1 + _Glow), saturate(_Glow * 0.5));
                return fixed4(c, _Alpha);
            }
            ENDCG
        }
    }
}
