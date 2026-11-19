// Vketブース版 Fur（元は FLUFF の FuwaCourse/Fur）。キーワードなし（霧なし）、光は FuwaBooth.cginc の固定の太陽と空の色
// FurOnePass ＋ 顔のパス（2026-10-30）。顔のパスのぶん SetPass が1つ増えるので、顔を出す物だけこっち
Shader "Custom/2562/Fuwa/FurOnePassFace"
{
    // 毛玉（シェルファー）の1パス版：層を重ねたメッシュ（FuwaBoothSetPassReduce が作る、uv4.x＝層の高さ）を1回で描く。見た目は Fur と同じ
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
        _WindFlutter ("毛先が風にそよぐ量 (0=なし)", Float) = 0
        _Squash ("つぶし（xyz=ワールドの向き、w=量）", Vector) = (0, 0, 0, 0)
        _SquashWobble ("ポヨポヨ（x=ゆれ幅、y=速さ）", Vector) = (0, 0, 0, 0)
        [Toggle] _FaceOn ("Face", Float) = 0
        _FaceFwd ("Face Forward (object)", Vector) = (0, 0, 1, 0)
        _FaceTilt ("Face Tilt (deg)", Float) = 0
        _FaceSize ("Face Size (sin)", Range(0.2, 1)) = 0.62
        _FaceLift ("Face Lift (x fur)", Range(0, 0.5)) = 0.08
        _FaceDepth ("Face Sphere (x fur)", Range(0, 1)) = 0.5
        [Enum(Lines,0,Smile,1,Dots,2,DotsMouth,3)] _FaceStyle ("Face Style", Float) = 3
        _FaceCheekSize ("Face Cheek Size", Range(0, 1.5)) = 1
        [Toggle] _FaceHit ("Face Hit", Float) = 0
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
        #define FUR_ONE_PASS
        #include "FuwaFur.cginc"
        ENDCG }
        // 顔（2026-10-30）：毛を描いたあとに、毛先のちょっと外側に半透明で
        Pass { Tags { "LightMode"="ForwardBase" } Blend SrcAlpha OneMinusSrcAlpha ZWrite Off CGPROGRAM
        #pragma vertex vertFace
        #pragma fragment fragFace
        #include "FuwaFurFace.cginc"
        ENDCG }
    }
}
