Shader "FuwaCourse/FurGhost"
{
    // 他人の玉（ゴースト）用の半透明の毛玉。人数分出るので層は8枚に減らして軽くしてある
    Properties
    {
        _Color ("Root Color", Color) = (0.8, 0.9, 1, 1)
        _TipColor ("Tip Color", Color) = (0.9, 0.95, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.45
        _FurLength ("Fur Length", Range(0, 0.5)) = 0.2
        _Density ("Density", Range(5, 200)) = 38
        _Thinness ("Thinness", Range(0.1, 1)) = 0.62
        _Shadow ("Root Shadow", Range(0, 1)) = 0.2
        _Gravity ("Gravity", Range(0, 0.3)) = 0.03
        _Drag ("Drag", Vector) = (0, 0, 0, 0)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #define FUR_H 0.0
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #define FUR_H 0.125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #define FUR_H 0.25
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #define FUR_H 0.375
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #define FUR_H 0.5
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #define FUR_H 0.625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #define FUR_H 0.75
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #define FUR_H 0.875
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #define FUR_H 1.0
        #include "FuwaFur.cginc"
        ENDCG }
    }
    Fallback Off
}
