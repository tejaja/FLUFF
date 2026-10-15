Shader "FuwaCourse/FurPlain"
{
    // ふわふわの毛なし版（軽量）。毛の層を描かずに、毛の分だけふくらませた球1枚＋顔。
    // プロパティは FuwaCourse/Fur・FurFace と同じ名前（色・ポヨン・なびき・光をスクリプトからそのまま入れられる）
    Properties
    {
        _Color ("Root Color", Color) = (1, 0.97, 0.94, 1)
        _TipColor ("Tip Color", Color) = (1, 1, 1, 1)
        _FurLength ("Fur Length", Range(0, 0.5)) = 0.14
        _PlainH ("Plain Sphere (x fur)", Range(0, 1)) = 0.6
        _PlainTip ("Plain Color (0=root, 1=tip)", Range(0, 1)) = 0.7
        _Density ("Density", Range(5, 200)) = 60
        _Shadow ("Root Shadow", Range(0, 1)) = 0.35
        _Gravity ("Gravity", Range(0, 0.3)) = 0.03
        _Drag ("Drag (set by script)", Vector) = (0, 0, 0, 0)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.35
        [HideInInspector] _Alpha ("Alpha", Float) = 1
        _GlowColor ("Glow (set by script, a=amount)", Color) = (0, 0, 0, 0)
        _GlowDir ("Glow Direction (set by script)", Vector) = (0, 1, 0, 0)
        [Toggle] _FaceOn ("Face", Float) = 0
        [Enum(LookAtCamera,0,Rotate,1)] _FaceMode ("Face Mode", Float) = 1
        _FaceSize ("Face Size (sin)", Range(0.2, 1)) = 0.62
        _FaceLift ("Face Lift (x fur)", Range(0, 0.5)) = 0.08
        _FaceDepth ("Face Sphere (x fur, = Plain Sphere)", Range(0, 1)) = 0.6
        [Enum(Lines,0,Smile,1,Dots,2,DotsMouth,3)] _FaceStyle ("Face Style", Float) = 3
        _FaceCheekSize ("Face Cheek Size", Range(0, 1.5)) = 1
        [Toggle] _FaceHit ("Face Hit (set by script)", Float) = 0
        _FaceEyeColor ("Face Eye Color", Color) = (0.28, 0.17, 0.12, 1)
        _FaceCheekColor ("Face Cheek Color (a=strength)", Color) = (1, 0.55, 0.6, 0.75)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest" }
        Cull Back

        // 球：毛の _PlainH 倍の高さまでふくらませる（毛ありの見た目の大きさに合わせる）
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragPlain
        #pragma multi_compile_fog
        float _PlainH, _PlainTip;
        #define FUR_H _PlainH
        #include "FuwaFur.cginc"
        fixed4 fragPlain (v2f i) : SV_Target
        {
            float3 n = normalize(i.worldNormal);
            float3 l = normalize(_WorldSpaceLightPos0.xyz);
            float ndl = saturate(dot(n, l) * 0.6 + 0.4);
            float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
            float rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimStrength;
            // 毛並みの名残：細かいまだら（毛ありの毛の長さのばらつきと同じマス目）
            float fuzz = hash3(floor(i.objPos * _Density));
            float ao = lerp(1.0 - _Shadow, 1.0, _PlainTip) * (0.93 + 0.07 * fuzz);
            float3 col = lerp(_Color.rgb, _TipColor.rgb, _PlainTip);
            col = col * (ndl * _LightColor0.rgb + ShadeSH9(float4(n, 1))) * ao + rim * _TipColor.rgb;
            if (_GlowColor.a > 0.001)
            {
                float3 center = unity_ObjectToWorld._m03_m13_m23;
                float d = dot(n, normalize(_GlowDir.xyz));
                float grad = saturate(d * 0.5 + 0.5);
                float band = frac(d * 1.2 - _Time.y * 2.2);
                band = smoothstep(0.0, 0.25, band) * (1.0 - smoothstep(0.35, 0.7, band));
                float glow = _GlowColor.a * (0.25 + 0.75 * grad) * (0.7 + 0.6 * band) * 0.9;
                col = lerp(col, _GlowColor.rgb * 1.2, saturate(glow)) + _GlowColor.rgb * glow * 0.3;
            }
            fixed4 c = fixed4(col, 1);
            UNITY_APPLY_FOG(i.fogCoord, c);
            return c;
        }
        ENDCG }

        // 顔（FurFace と同じ。球の少し外側に描く）
        Pass { Tags { "LightMode"="ForwardBase" } Blend SrcAlpha OneMinusSrcAlpha ZWrite Off CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragFace
        #pragma multi_compile_fog
        float _PlainH, _PlainTip;
        #define FUR_H (_PlainH + _FaceLift)
        #include "FuwaFurFace.cginc"
        ENDCG }
    }
    Fallback "Diffuse"
}
