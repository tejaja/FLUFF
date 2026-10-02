// Standard（不透明）と同じ見た目で、カメラのすぐ近くだけ消える版。クモの巣の穴の輪っか用。
// 半透明にすると輪っか自身の前後が崩れるので、不透明のまま細かい網目（ディザ）で抜いて消していく。
// カメラから _FadeNear 以内は見えず、_FadeFar までにだんだん見えてくる。
Shader "FuwaCourse/NearFadeOpaque"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0, 1)) = 0.4
        _Metallic ("Metallic", Range(0, 1)) = 0
        _FadeNear ("Camera Fade: invisible within (m)", Float) = 0.45
        _FadeFar ("Camera Fade: fully visible beyond (m)", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        CGPROGRAM
        #pragma surface surf Standard addshadow
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness, _Metallic;
        float _FadeNear, _FadeFar;

        struct Input { float2 uv_MainTex; float3 worldPos; float4 screenPos; };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float d = distance(IN.worldPos, _WorldSpaceCameraPos);
            float k = smoothstep(_FadeNear, max(_FadeFar, _FadeNear + 0.01), d);
            // 4x4 のディザで k の割合だけ残す
            float2 px = floor(IN.screenPos.xy / max(IN.screenPos.w, 1e-5) * _ScreenParams.xy);
            static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            int2 m = (int2)fmod(px, 4.0);
            float dither = (bayer[m.y * 4 + m.x] + 0.5) / 16.0;
            clip(k - dither);
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
