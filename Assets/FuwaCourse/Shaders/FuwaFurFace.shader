Shader "FuwaCourse/FurFace"
{
    // 毛玉（シェルファー）＋顔。FuwaCourse/Fur と同じ毛のあと、最後のパスで顔を描く
    Properties
    {
        _Color ("Root Color", Color) = (1, 0.97, 0.94, 1)
        _TipColor ("Tip Color", Color) = (1, 1, 1, 1)
        _FurLength ("Fur Length", Range(0, 0.5)) = 0.14
        _EyeDirL ("Eye Dir L (object)", Vector) = (0, 0, 1, 0)
        _EyeDirR ("Eye Dir R (object)", Vector) = (0, 0, 1, 0)
        _EyeMask ("Eye Mask (x=inner cos, y=outer cos, 0=off)", Vector) = (0, 0, 0, 0)
        _EyeMinLen ("Eye Fur Length (ratio)", Range(0, 1)) = 0
        _EyeTint ("Eye Fur Tint", Range(0, 1)) = 0
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
        _SwayAmp ("Sway (m, 0=off)", Float) = 0
        _SwaySpeed ("Sway Speed", Float) = 1.2
        _SwayDir ("Sway Direction (world)", Vector) = (1, 0, 0, 0)
        [Toggle] _FaceOn ("Face", Float) = 1
        [Enum(LookAtCamera,0,Rotate,1)] _FaceMode ("Face Mode", Float) = 0
        _FaceSize ("Face Size (sin)", Range(0.2, 1)) = 0.62
        _FaceLift ("Face Lift (x fur)", Range(0, 0.5)) = 0.08
        _FaceDepth ("Face Sphere (x fur)", Range(0, 1)) = 0.5
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

        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.0
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.0625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.1875
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.25
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.3125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.375
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.4375
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.5
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.5625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.6875
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.75
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.8125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.875
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.9375
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 1.0
        #include "FuwaFur.cginc"
        ENDCG }
        // 顔：毛を全部描いたあとに、毛先のちょっと外側に半透明で描く
        Pass { Tags { "LightMode"="ForwardBase" } Blend SrcAlpha OneMinusSrcAlpha ZWrite Off CGPROGRAM
        #pragma vertex vert
        #pragma fragment fragFace
        #pragma multi_compile_fog
        #include "FuwaFurFace.cginc"
        ENDCG }
    }
    Fallback "Diffuse"
}
