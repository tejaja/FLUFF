Shader "Hidden/SuzuFactory/Hikari/Standard Built-in Reflection Passes"
{
    Properties
    {
        _SrcBlend("Source Blend", Float) = 1
        _DstBlend("Destination Blend", Float) = 0
        _ZWrite("Z Write", Float) = 1
        _Cull("Cull", Float) = 2
        [IntRange] _StencilRef("Stencil Reference", Range(0, 255)) = 0
        [IntRange] _StencilReadMask("Stencil Read Mask", Range(0, 255)) = 255
        [IntRange] _StencilWriteMask("Stencil Write Mask", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Compare", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass("Stencil Pass", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail("Stencil Fail", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail("Stencil ZFail", Float) = 0
    }

    SubShader
    {
        LOD 300
        Cull [_Cull]

        Pass
        {
            Name "TRANSMISSION"
            Tags { "LightMode" = "Always" }
            Blend Zero SrcColor
            ZWrite Off
            ColorMask RGB

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex HikariVertTransmission
            #pragma fragment HikariFragTransmission
            #pragma shader_feature _PARALLAXMAP
            #pragma shader_feature _ _ALPHATEST_ON
            #pragma multi_compile_instancing
            #define HIKARI_TRANSMISSION_BLEND 1
            #include "HikariStandardCore.cginc"
            ENDCG
        }

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex HikariVertBase
            #pragma fragment HikariFragBase
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _PARALLAXMAP
            #pragma shader_feature _DETAIL_MULX2
            #pragma shader_feature _EMISSION
            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _GLOSSYREFLECTIONS_OFF
            #pragma shader_feature _HIKARI_MONOSH
            #pragma shader_feature _HIKARI_MATERIAL_LIGHTMAP
            #pragma shader_feature _HIKARI_SCREEN_SPACE_REFLECTION
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma skip_variants DYNAMICLIGHTMAP_ON
            #ifndef UNITY_PASS_FORWARDBASE
            #define UNITY_PASS_FORWARDBASE
            #endif
            #define HIKARI_SCREEN_SPACE_TRANSMISSION 1
            #include "HikariStandardCore.cginc"
            ENDCG
        }

        Pass
        {
            Name "FORWARD_DELTA"
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            ZWrite Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex HikariVertAdd
            #pragma fragment HikariFragAdd
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _PARALLAXMAP
            #pragma shader_feature _DETAIL_MULX2
            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #ifndef UNITY_PASS_FORWARDADD
            #define UNITY_PASS_FORWARDADD
            #endif
            #include "HikariStandardCore.cginc"
            ENDCG
        }

        Pass
        {
            Name "FORWARD_BACK"
            Tags { "LightMode" = "ForwardBase" }
            Cull Front
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex HikariVertBase
            #pragma fragment HikariFragBase
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _PARALLAXMAP
            #pragma shader_feature _DETAIL_MULX2
            #pragma shader_feature _EMISSION
            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _GLOSSYREFLECTIONS_OFF
            #pragma shader_feature _HIKARI_MONOSH
            #pragma shader_feature _HIKARI_MATERIAL_LIGHTMAP
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma skip_variants DYNAMICLIGHTMAP_ON
            #ifndef UNITY_PASS_FORWARDBASE
            #define UNITY_PASS_FORWARDBASE
            #endif
            #include "HikariStandardCore.cginc"
            ENDCG
        }

        Pass
        {
            Name "REFRACTION_FORWARD_BACK"
            Tags { "LightMode" = "ForwardBase" }
            Cull Front
            Blend One OneMinusSrcAlpha
            ZWrite [_ZWrite]

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex HikariVertBase
            #pragma fragment HikariFragBase
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _PARALLAXMAP
            #pragma shader_feature _DETAIL_MULX2
            #pragma shader_feature _EMISSION
            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _GLOSSYREFLECTIONS_OFF
            #pragma shader_feature _HIKARI_MONOSH
            #pragma shader_feature _HIKARI_MATERIAL_LIGHTMAP
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma skip_variants DYNAMICLIGHTMAP_ON
            #ifndef UNITY_PASS_FORWARDBASE
            #define UNITY_PASS_FORWARDBASE
            #endif
            #define HIKARI_TRANSPARENT_TWO_PASS_BACK 1
            #define HIKARI_SCREEN_SPACE_TRANSMISSION 1
            #include "HikariStandardCore.cginc"
            ENDCG
        }

        Pass
        {
            Name "TRANSMISSION_BACK"
            Tags { "LightMode" = "Always" }
            Cull Front
            Blend Zero SrcColor
            ZWrite Off
            ColorMask RGB

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex HikariVertTransmission
            #pragma fragment HikariFragTransmission
            #pragma shader_feature _PARALLAXMAP
            #pragma shader_feature _ _ALPHATEST_ON
            #pragma multi_compile_instancing
            #define HIKARI_TRANSMISSION_BLEND 1
            #include "HikariStandardCore.cginc"
            ENDCG
        }

        Pass
        {
            Name "TRANSMISSION_FRONT"
            Tags { "LightMode" = "Always" }
            Cull Back
            Blend Zero SrcColor
            ZWrite Off
            ColorMask RGB

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex HikariVertTransmission
            #pragma fragment HikariFragTransmission
            #pragma shader_feature _PARALLAXMAP
            #pragma shader_feature _ _ALPHATEST_ON
            #pragma multi_compile_instancing
            #define HIKARI_TRANSMISSION_BLEND 1
            #define HIKARI_SCREEN_SPACE_TRANSMISSION_PASS 1
            #include "HikariStandardCore.cginc"
            ENDCG
        }

        Pass
        {
            Name "FORWARD_FRONT"
            Tags { "LightMode" = "ForwardBase" }
            Cull Back
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex HikariVertBase
            #pragma fragment HikariFragBase
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _PARALLAXMAP
            #pragma shader_feature _DETAIL_MULX2
            #pragma shader_feature _EMISSION
            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _GLOSSYREFLECTIONS_OFF
            #pragma shader_feature _HIKARI_MONOSH
            #pragma shader_feature _HIKARI_MATERIAL_LIGHTMAP
            #pragma shader_feature _HIKARI_SCREEN_SPACE_REFLECTION
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma skip_variants DYNAMICLIGHTMAP_ON
            #ifndef UNITY_PASS_FORWARDBASE
            #define UNITY_PASS_FORWARDBASE
            #endif
            #include "HikariStandardCore.cginc"
            ENDCG
        }

        Pass
        {
            Name "REFRACTION_FORWARD_FRONT"
            Tags { "LightMode" = "ForwardBase" }
            Cull Back
            Blend One OneMinusSrcAlpha
            ZWrite [_ZWrite]

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex HikariVertBase
            #pragma fragment HikariFragBase
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _PARALLAXMAP
            #pragma shader_feature _DETAIL_MULX2
            #pragma shader_feature _EMISSION
            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _GLOSSYREFLECTIONS_OFF
            #pragma shader_feature _HIKARI_MONOSH
            #pragma shader_feature _HIKARI_MATERIAL_LIGHTMAP
            #pragma shader_feature _HIKARI_SCREEN_SPACE_REFLECTION
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma skip_variants DYNAMICLIGHTMAP_ON
            #ifndef UNITY_PASS_FORWARDBASE
            #define UNITY_PASS_FORWARDBASE
            #endif
            #define HIKARI_SCREEN_SPACE_TRANSMISSION 1
            #define HIKARI_TRANSPARENT_TWO_PASS_FRONT 1
            #include "HikariStandardCore.cginc"
            ENDCG
        }

        Pass
        {
            Name "FORWARD_DELTA_BACK"
            Tags { "LightMode" = "ForwardAdd" }
            Cull Front
            Blend One One
            ZWrite Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex HikariVertAdd
            #pragma fragment HikariFragAdd
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _PARALLAXMAP
            #pragma shader_feature _DETAIL_MULX2
            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #ifndef UNITY_PASS_FORWARDADD
            #define UNITY_PASS_FORWARDADD
            #endif
            #include "HikariStandardCore.cginc"
            ENDCG
        }

        Pass
        {
            Name "FORWARD_DELTA_FRONT"
            Tags { "LightMode" = "ForwardAdd" }
            Cull Back
            Blend One One
            ZWrite Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex HikariVertAdd
            #pragma fragment HikariFragAdd
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _PARALLAXMAP
            #pragma shader_feature _DETAIL_MULX2
            #pragma shader_feature _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #ifndef UNITY_PASS_FORWARDADD
            #define UNITY_PASS_FORWARDADD
            #endif
            #include "HikariStandardCore.cginc"
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"

            Stencil
            {
                Ref 0
                ReadMask 255
                WriteMask 0
                Comp Always
                Pass Keep
                Fail Keep
                ZFail Keep
            }
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask RGBA
            ZClip On

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex HikariVertShadow
            #pragma fragment HikariFragShadow
            #pragma shader_feature _ _ALPHATEST_ON
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing
            #ifndef UNITY_PASS_SHADOWCASTER
            #define UNITY_PASS_SHADOWCASTER
            #endif
            #include "HikariStandardCore.cginc"
            ENDCG
        }

        Pass
        {
            Name "Meta"

            Stencil
            {
                Ref 0
                ReadMask 255
                WriteMask 0
                Comp Always
                Pass Keep
                Fail Keep
                ZFail Keep
            }
            Tags { "LightMode" = "Meta" }
            Cull Off
            ColorMask RGBA
            ZTest LEqual
            ZClip On

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex HikariVertMeta
            #pragma fragment HikariFragMeta
            #pragma shader_feature _ _ALPHATEST_ON
            #pragma shader_feature _DETAIL_MULX2
            #pragma shader_feature _EMISSION
            #ifndef UNITY_PASS_META
            #define UNITY_PASS_META
            #endif
            #include "HikariStandardCore.cginc"
            ENDCG
        }
    }
}
