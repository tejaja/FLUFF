// 道のふちの飾り（草・花・小石・ツタ）用：両面描画、裏から見た時は法線を反転してふつうに明るく。
Shader "FuwaCourse/Deco"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Wrap ("Soft Light (wrap)", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Wrapped fullforwardshadows addshadow
        #pragma target 3.0
        fixed4 _Color;
        half _Wrap;
        struct Input { float facing : VFACE; };

        half4 LightingWrapped(SurfaceOutput s, half3 lightDir, half atten)
        {
            half d = (dot(s.Normal, lightDir) + _Wrap) / (1 + _Wrap);
            half4 c; c.rgb = s.Albedo * _LightColor0.rgb * saturate(d) * atten; c.a = s.Alpha; return c;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            o.Albedo = _Color.rgb;
            o.Normal = IN.facing < 0 ? half3(0, 0, -1) : half3(0, 0, 1);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
