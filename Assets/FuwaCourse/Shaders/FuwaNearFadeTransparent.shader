// Standard（Fadeモード）と同じ見た目で、カメラのすぐ近くの部分だけスゥっと消える版。小さいクモの巣用。
// カメラから _FadeNear 以内は見えず、_FadeFar までにだんだん見えてくる（通り抜ける時に視界をふさがない）。
Shader "FuwaCourse/NearFadeTransparent"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo (RGBA)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.2
        _Metallic ("Metallic", Range(0, 1)) = 0
        _FadeNear ("Camera Fade: invisible within (m)", Float) = 0.45
        _FadeFar ("Camera Fade: fully visible beyond (m)", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness, _Metallic;
        float _FadeNear, _FadeFar;

        struct Input { float2 uv_MainTex; float3 worldPos; };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            float d = distance(IN.worldPos, _WorldSpaceCameraPos);
            o.Alpha = c.a * smoothstep(_FadeNear, max(_FadeFar, _FadeNear + 0.01), d);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
