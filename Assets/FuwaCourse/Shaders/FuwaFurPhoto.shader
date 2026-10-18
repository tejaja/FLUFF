Shader "FuwaCourse/FurPhoto"
{
    // 撮影用の毛玉（FuwaPhotoSet）。FuwaCourse/Fur と同じ毛で、層を 64 枚に（近くで大きく写しても層の点々が見えないように）。ゲームでは使わない
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
        _Squash ("Squash (world dir xyz, amount w)", Vector) = (0, 0, 1, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest" }
        Cull Back
        // ステンシルの64番のビットに「ふわふわ」の印。うそ影のデカール（FuwaBlobDecal）がふわふわ自身に乗らないように（2026-10-15 地面近くで玉の下の方に影のふちが出ていた）
        Stencil { Ref 64 ReadMask 64 WriteMask 64 Comp Always Pass Replace }

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
        #define FUR_H 0.015625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.03125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.046875
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
        #define FUR_H 0.078125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.09375
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.109375
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
        #define FUR_H 0.140625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.15625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.171875
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
        #define FUR_H 0.203125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.21875
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.234375
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
        #define FUR_H 0.265625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.28125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.296875
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
        #define FUR_H 0.328125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.34375
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.359375
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
        #define FUR_H 0.390625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.40625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.421875
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
        #define FUR_H 0.453125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.46875
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.484375
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
        #define FUR_H 0.515625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.53125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.546875
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
        #define FUR_H 0.578125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.59375
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.609375
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
        #define FUR_H 0.640625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.65625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.671875
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
        #define FUR_H 0.703125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.71875
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.734375
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
        #define FUR_H 0.765625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.78125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.796875
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
        #define FUR_H 0.828125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.84375
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.859375
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
        #define FUR_H 0.890625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.90625
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.921875
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
        #define FUR_H 0.953125
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.96875
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 0.984375
        #include "FuwaFur.cginc"
        ENDCG }
        Pass { Tags { "LightMode"="ForwardBase" } CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile_fog
        #define FUR_H 1.0
        #include "FuwaFur.cginc"
        ENDCG }
    }
    Fallback "Diffuse"
}
