Shader "FuwaCourse/Fur"
{
    // 毛玉（シェルファー）。根元の球＋16枚の毛の層
    Properties
    {
        _Color ("Root Color", Color) = (1, 0.97, 0.94, 1)
        _TipColor ("Tip Color", Color) = (1, 1, 1, 1)
        _FurLength ("Fur Length", Range(0, 0.5)) = 0.14
        _Density ("Density", Range(5, 200)) = 60
        _Thinness ("Thinness", Range(0.1, 1)) = 0.55
        _Shadow ("Root Shadow", Range(0, 1)) = 0.35
        _Gravity ("Gravity", Range(0, 0.3)) = 0.03
        _Drag ("Drag (set by script)", Vector) = (0, 0, 0, 0)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.5
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.35
        [HideInInspector] _Alpha ("Alpha", Float) = 1
        _GlowColor ("Glow (set by script, a=amount)", Color) = (0, 0, 0, 0)
        _GlowDir ("Glow Direction (set by script)", Vector) = (0, 1, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest" }
        Cull Back

        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #define FUR_H 0.0
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #define FUR_H 0.0625
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
        #define FUR_H 0.1875
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
        #define FUR_H 0.3125
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
        #define FUR_H 0.4375
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
        #define FUR_H 0.5625
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
        #define FUR_H 0.6875
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
        #define FUR_H 0.8125
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
        #define FUR_H 0.9375
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #define FUR_H 1.0
        #include "FuwaFur.cginc"
        ENDCG }
    }
    Fallback "Diffuse"
}
