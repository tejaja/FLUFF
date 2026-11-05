Shader "Hidden/SuzuFactory/Hikari/Standard URP Passes"
{
    Properties
    {
        _Color("Color", Color) = (1, 1, 1, 1)
        _MainTex("Albedo", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _Metallic("Metallic", Range(0, 1)) = 0
        _Glossiness("Smoothness", Range(0, 1)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0, 1)) = 1
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0
        _MetallicGlossMap("Metallic Smoothness", 2D) = "white" {}
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1
        _ParallaxMap("Height Map", 2D) = "black" {}
        _Parallax("Height Scale", Range(0.005, 0.08)) = 0.02
        _OcclusionMap("Occlusion", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1
        _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionMap("Emission Map", 2D) = "white" {}
        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMap("Detail Albedo", 2D) = "grey" {}
        _DetailNormalMap("Detail Normal Map", 2D) = "bump" {}
        _DetailNormalMapScale("Detail Normal Scale", Float) = 1
        _UVSec("UV Set", Float) = 0
        _HikariStandardVariant("Variant", Float) = 0
        _HikariRefractionStrength("Refraction Strength", Range(-1, 1)) = 0.1
        _HikariScreenSpaceReflection("Screen Space Reflection", Float) = 0
        _HikariSsrIntensity("SSR Intensity", Range(0, 1)) = 1
        _HikariSsrMaxDistance("SSR Max Distance", Range(0.1, 100)) = 20
        _HikariSsrThickness("SSR Thickness", Range(0.001, 1)) = 0.1
        _HikariSsrSmoothness("SSR Smoothness", Range(0, 1)) = 1
        [IntRange] _HikariSsrSteps("SSR Max Steps", Range(1, 64)) = 32
        _HikariTransmissionColor("Transmission", Color) = (1, 1, 1, 1)
        _HikariTransmissionMap("Transmission", 2D) = "white" {}
        _HikariMonoSh("MonoSH", Float) = 0
        _HikariMaterialLightmapEnabled("Material Lightmap", Float) = 0
        _HikariMaterialLightmap("Material Lightmap Color", 2D) = "black" {}
        _HikariMaterialLightmapDirection("Material Lightmap Direction", 2D) = "black" {}
        _HikariMaterialLightmapTransform("Material Lightmap Transform", Vector) = (1, 1, 0, 0)
        _HikariMaterialLightmapMode("Material Lightmap Mode", Float) = 0
        _HikariIor("Index of Refraction", Float) = 1
        _HikariAbbe("Abbe Number", Float) = 0
        _HikariBakeTransmissionOverride("Override Bake Transmission", Float) = 0
        _HikariBakeTransmissionColor("Bake Transmission", Color) = (1, 1, 1, 1)
        _HikariVolumetric("Volumetric", Float) = 0
        _HikariVolumetricAbsorption("Volumetric Absorption", Color) = (1, 1, 1, 1)
        _HikariVolumetricScattering("Volumetric Scattering", Float) = 0
        _HikariVolumetricEmission("Volumetric Emission", Color) = (0, 0, 0, 1)
        _HikariTransparentTwoPass("2 Pass", Float) = 0
        _Mode("Rendering Mode", Float) = 0
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
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "14.0"
        }
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HikariUrpForwardVertex
            #pragma fragment HikariUrpForwardFragment
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #define HIKARI_URP_FORWARD_PASS 1
            #include "HikariStandardUrp.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "UniversalForwardBack"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Front

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HikariUrpForwardVertex
            #pragma fragment HikariUrpForwardFragment
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #define HIKARI_URP_FORWARD_PASS 1
            #define HIKARI_URP_TWO_PASS_BACK 1
            #include "HikariStandardUrp.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "UniversalForwardFront"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HikariUrpForwardVertex
            #pragma fragment HikariUrpForwardFragment
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #define HIKARI_URP_FORWARD_PASS 1
            #define HIKARI_URP_TWO_PASS_FRONT 1
            #include "HikariStandardUrp.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "UniversalShadowCaster"

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
            ColorMask 0
            Cull [_Cull]
            ZClip On

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex HikariUrpShadowVertex
            #pragma fragment HikariUrpShadowFragment
            #pragma shader_feature _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #define HIKARI_URP_SHADOW_PASS 1
            #include "HikariStandardUrp.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "UniversalDepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex HikariUrpDepthVertex
            #pragma fragment HikariUrpDepthFragment
            #pragma shader_feature _ALPHATEST_ON
            #pragma multi_compile_instancing
            #define HIKARI_URP_DEPTH_PASS 1
            #include "HikariStandardUrp.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "UniversalDepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }
            ZWrite On
            ColorMask RGBA
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex HikariUrpDepthNormalsVertex
            #pragma fragment HikariUrpDepthNormalsFragment
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _PARALLAXMAP
            #pragma shader_feature _DETAIL_MULX2
            #pragma shader_feature _ALPHATEST_ON
            #pragma multi_compile _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #define HIKARI_URP_DEPTH_NORMALS_PASS 1
            #include "HikariStandardUrp.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "UniversalMeta"

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

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex HikariUrpMetaVertex
            #pragma fragment HikariUrpMetaFragment
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature _DETAIL_MULX2
            #pragma shader_feature _EMISSION
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature EDITOR_VISUALIZATION
            #define HIKARI_URP_META_PASS 1
            #include "HikariStandardUrp.hlsl"
            ENDHLSL
        }
    }

    SubShader
    {
        Pass
        {
            Name "Fallback"
            ColorMask 0

            CGPROGRAM
            #pragma vertex HikariFallbackVertex
            #pragma fragment HikariFallbackFragment
            #include "UnityCG.cginc"

            float4 HikariFallbackVertex(float4 positionOS : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(positionOS);
            }

            fixed4 HikariFallbackFragment() : SV_Target
            {
                return 0;
            }
            ENDCG
        }
    }
}
