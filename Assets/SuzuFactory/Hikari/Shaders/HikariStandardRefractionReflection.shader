Shader "Hidden/SuzuFactory/Hikari/Standard (Refraction + Reflection)"
{
    Properties
    {
        _Color("Color", Color) = (1, 1, 1, 1)
        _MainTex("Albedo", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5

        [Gamma] _Metallic("Metallic", Range(0, 1)) = 0
        _Glossiness("Smoothness", Range(0, 1)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0, 1)) = 1
        [Enum(Metallic Alpha,0,Albedo Alpha,1)] _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0
        [NoScaleOffset] _MetallicGlossMap("Metallic (R) Smoothness (A)", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _GlossyReflections("Glossy Reflections", Float) = 1.0

        [NoScaleOffset][Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1
        [NoScaleOffset] _ParallaxMap("Height Map (G)", 2D) = "black" {}
        _Parallax("Height Scale", Range(0.005, 0.08)) = 0.02
        [NoScaleOffset] _OcclusionMap("Occlusion (G)", 2D) = "white" {}
        _OcclusionStrength("Occlusion Strength", Range(0, 1)) = 1

        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}
        [NoScaleOffset] _DetailMask("Detail Mask (A)", 2D) = "white" {}
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "grey" {}
        [NoScaleOffset][Normal] _DetailNormalMap("Detail Normal Map", 2D) = "bump" {}
        _DetailNormalMapScale("Detail Normal Scale", Float) = 1
        [Enum(UV0,0,UV1,1)] _UVSec("UV Set for secondary textures", Float) = 0

        _HikariRefractionStrength("Refraction Strength", Range(-1, 1)) = 0.1
        _HikariTransmissionColor("Transmission", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _HikariTransmissionMap("Transmission", 2D) = "white" {}
        [HideInInspector][Enum(Standard,0,Refraction,1,Transmission,2)] _HikariStandardVariant("Variant", Float) = 1
        [HideInInspector] _HikariScreenSpaceReflection("Screen Space Reflection", Float) = 1
        _HikariSsrIntensity("SSR Intensity", Range(0, 1)) = 1
        _HikariSsrMaxDistance("SSR Max Distance", Range(0.1, 100)) = 20
        _HikariSsrThickness("SSR Thickness", Range(0.001, 1)) = 0.1
        _HikariSsrSmoothness("SSR Smoothness", Range(0, 1)) = 1
        [IntRange] _HikariSsrSteps("SSR Max Steps", Range(1, 64)) = 32

        [HideInInspector] _HikariMonoSh("MonoSH Directional Lightmaps", Float) = 0
        [HideInInspector][Toggle(_HIKARI_MATERIAL_LIGHTMAP)] _HikariMaterialLightmapEnabled("Material Lightmap", Float) = 0
        [HideInInspector][NoScaleOffset] _HikariMaterialLightmap("Material Lightmap Color", 2D) = "black" {}
        [HideInInspector][NoScaleOffset] _HikariMaterialLightmapDirection("Material Lightmap Direction", 2D) = "black" {}
        [HideInInspector] _HikariMaterialLightmapTransform("Material Lightmap Transform", Vector) = (1, 1, 0, 0)
        [HideInInspector] _HikariMaterialLightmapMode("Material Lightmap Mode", Float) = 0
        _HikariIor("Index of Refraction (Hikari)", Range(0.001, 3.0)) = 1
        _HikariAbbe("Abbe Number (Hikari, 0 = No Dispersion)", Range(0, 100)) = 0
        [Toggle] _HikariBakeTransmissionOverride("Override Bake Transmission", Float) = 0
        _HikariBakeTransmissionColor("Bake Transmission", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _HikariBakeTransmissionMap("Bake Transmission", 2D) = "white" {}
        [Toggle] _HikariVolumetric("Volumetric", Float) = 0
        _HikariVolumetricAbsorption("Volumetric Absorption", Color) = (1, 1, 1, 1)
        _HikariVolumetricScattering("Volumetric Scattering", Float) = 0
        [HDR] _HikariVolumetricEmission("Volumetric Emission", Color) = (0, 0, 0, 1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
        [Toggle] _HikariTransparentTwoPass("2 Pass", Float) = 0

        [HideInInspector] _Mode("Rendering Mode", Float) = 0
        [HideInInspector] _HikariZWriteMode("Z Write Mode", Float) = 0
        [HideInInspector] _SrcBlend("Source Blend", Float) = 1
        [HideInInspector] _DstBlend("Destination Blend", Float) = 0
        [HideInInspector] _ZWrite("Z Write", Float) = 1
        _ColorMask("Color Mask", Float) = 15
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 4
        [Toggle] _ZClip("ZClip", Float) = 1
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
        PackageRequirements { "com.unity.render-pipelines.universal": "14.0" }
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Stencil
        {
            Ref [_StencilRef]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
            Comp [_StencilComp]
            Pass [_StencilPass]
            Fail [_StencilFail]
            ZFail [_StencilZFail]
        }
        ColorMask [_ColorMask]
        ZTest [_ZTest]
        ZClip [_ZClip]

        UsePass "Hidden/SuzuFactory/Hikari/Standard URP Passes/UniversalForward"
        UsePass "Hidden/SuzuFactory/Hikari/Standard URP Passes/UniversalShadowCaster"
        UsePass "Hidden/SuzuFactory/Hikari/Standard URP Passes/UniversalDepthOnly"
        UsePass "Hidden/SuzuFactory/Hikari/Standard URP Passes/UniversalDepthNormals"
        UsePass "Hidden/SuzuFactory/Hikari/Standard URP Passes/UniversalMeta"
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "PerformanceChecks" = "False" }
        LOD 300
        Cull [_Cull]

        Stencil
        {
            Ref [_StencilRef]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
            Comp [_StencilComp]
            Pass [_StencilPass]
            Fail [_StencilFail]
            ZFail [_StencilZFail]
        }
        ColorMask [_ColorMask]
        ZTest [_ZTest]
        ZClip [_ZClip]

        GrabPass { "_HikariScreenColorRefractionReflectionTexture" }

        UsePass "Hidden/SuzuFactory/Hikari/Standard Built-in Reflection Passes/FORWARD"
        UsePass "Hidden/SuzuFactory/Hikari/Standard Built-in Reflection Passes/FORWARD_DELTA"
        UsePass "Hidden/SuzuFactory/Hikari/Standard Built-in Reflection Passes/ShadowCaster"
        UsePass "Hidden/SuzuFactory/Hikari/Standard Built-in Reflection Passes/Meta"
    }

    FallBack "Standard"
    CustomEditor "SuzuFactory.Hikari.Editor.HikariStandardShaderGUI"
}
