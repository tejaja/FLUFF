#ifndef HIKARI_STANDARD_URP_INCLUDED
#define HIKARI_STANDARD_URP_INCLUDED

#if defined(_GLOSSYREFLECTIONS_OFF)
#define _ENVIRONMENTREFLECTIONS_OFF 1
#endif

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ParallaxMapping.hlsl"
#include "HikariLightmapDecode.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
float4 _DetailAlbedoMap_ST;
float4 _HikariMaterialLightmapTransform;
half4 _Color;
half4 _EmissionColor;
half4 _HikariTransmissionColor;
half4 _HikariBakeTransmissionColor;
half4 _HikariVolumetricAbsorption;
half4 _HikariVolumetricEmission;
half _Cutoff;
half _Metallic;
half _Glossiness;
half _GlossMapScale;
half _SmoothnessTextureChannel;
half _BumpScale;
half _Parallax;
half _OcclusionStrength;
half _DetailNormalMapScale;
half _UVSec;
half _HikariStandardVariant;
half _HikariRefractionStrength;
half _HikariScreenSpaceReflection;
half _HikariSsrIntensity;
float _HikariSsrMaxDistance;
float _HikariSsrThickness;
float _HikariSsrSmoothness;
float _HikariSsrSteps;
half _HikariMonoSh;
half _HikariMaterialLightmapEnabled;
half _HikariMaterialLightmapMode;
half _HikariIor;
half _HikariAbbe;
half _HikariBakeTransmissionOverride;
half _HikariVolumetric;
half _HikariVolumetricScattering;
half _HikariTransparentTwoPass;
half _Mode;
half _SrcBlend;
half _DstBlend;
half _ZWrite;
half _Cull;
CBUFFER_END

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_MetallicGlossMap);
SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);
TEXTURE2D(_ParallaxMap);
SAMPLER(sampler_ParallaxMap);
TEXTURE2D(_OcclusionMap);
SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
TEXTURE2D(_DetailMask);
SAMPLER(sampler_DetailMask);
TEXTURE2D(_DetailAlbedoMap);
SAMPLER(sampler_DetailAlbedoMap);
TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_DetailNormalMap);
TEXTURE2D(_HikariTransmissionMap);
SAMPLER(sampler_HikariTransmissionMap);
TEXTURE2D(_HikariMaterialLightmap);
SAMPLER(sampler_HikariMaterialLightmap);
TEXTURE2D(_HikariMaterialLightmapDirection);
SAMPLER(sampler_HikariMaterialLightmapDirection);

struct HikariUrpSurfaceUv
{
    float2 main;
    float2 detail;
};

HikariUrpSurfaceUv HikariUrpApplyParallax(float4 texcoord, half3 viewDirTS)
{
    HikariUrpSurfaceUv result;
    result.main = texcoord.xy;
    result.detail = texcoord.zw;
#if defined(_PARALLAXMAP)
    float2 offset = ParallaxMapping(
        TEXTURE2D_ARGS(_ParallaxMap, sampler_ParallaxMap), viewDirTS, _Parallax, result.main);
    result.main += offset;
    result.detail += offset;
#endif
    return result;
}

void HikariUrpInitializeSurface(HikariUrpSurfaceUv texcoord, out SurfaceData surfaceData)
{
    surfaceData = (SurfaceData)0;
    half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, texcoord.main);
    half4 albedo = mainTex * _Color;
#if defined(_ALPHATEST_ON)
    clip(albedo.a - _Cutoff);
#endif

#if defined(_DETAIL_MULX2)
    half detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, texcoord.main).a;
    half3 detailAlbedo = SAMPLE_TEXTURE2D(
        _DetailAlbedoMap, sampler_DetailAlbedoMap, texcoord.detail).rgb;
    albedo.rgb *= LerpWhiteTo(detailAlbedo * 2.0h, detailMask);
#endif

    surfaceData.albedo = albedo.rgb;
    surfaceData.alpha = albedo.a;

#if defined(_METALLICGLOSSMAP)
    half4 metallicGloss = SAMPLE_TEXTURE2D(
        _MetallicGlossMap, sampler_MetallicGlossMap, texcoord.main);
    surfaceData.metallic = metallicGloss.r;
#else
    surfaceData.metallic = _Metallic;
#endif

#if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
    surfaceData.smoothness = mainTex.a * _GlossMapScale;
#elif defined(_METALLICGLOSSMAP)
    surfaceData.smoothness = metallicGloss.a * _GlossMapScale;
#else
    surfaceData.smoothness = _Glossiness;
#endif

#if defined(_NORMALMAP)
    half3 normalTS = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, texcoord.main), _BumpScale);
#else
    half3 normalTS = half3(0.0h, 0.0h, 1.0h);
#endif
#if defined(_DETAIL_MULX2)
    half3 detailNormalTS = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, texcoord.detail),
        _DetailNormalMapScale);
    normalTS = lerp(normalTS, BlendNormalRNM(normalTS, detailNormalTS), detailMask);
#endif
    surfaceData.normalTS = normalTS;

    half occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, texcoord.main).g;
    surfaceData.occlusion = LerpWhiteTo(occlusion, _OcclusionStrength);

#if defined(_EMISSION)
    surfaceData.emission =
        SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, texcoord.main).rgb * _EmissionColor.rgb;
#else
    surfaceData.emission = 0.0h;
#endif
    surfaceData.specular = 0.0h;
    surfaceData.clearCoatMask = 0.0h;
    surfaceData.clearCoatSmoothness = 0.0h;
}

half HikariUrpTransmissionCoverage(SurfaceData surfaceData)
{
#if defined(_ALPHAPREMULTIPLY_ON)
    half oneMinusReflectivity = OneMinusReflectivityMetallic(surfaceData.metallic);
    return 1.0h - oneMinusReflectivity + surfaceData.alpha * oneMinusReflectivity;
#elif defined(_ALPHABLEND_ON)
    return surfaceData.alpha;
#else
    return 1.0h;
#endif
}

#if defined(HIKARI_URP_FORWARD_PASS)

struct HikariUrpForwardAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct HikariUrpForwardVaryings
{
    float4 texcoord : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
#if defined(_HIKARI_MATERIAL_LIGHTMAP) || defined(LIGHTMAP_ON)
    float2 lightmapUV : TEXCOORD4;
#else
    half3 vertexSH : TEXCOORD4;
#endif
    float4 shadowCoord : TEXCOORD5;
    half4 fogAndVertexLight : TEXCOORD6;
    float4 screenPosition : TEXCOORD7;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

HikariUrpForwardVaryings HikariUrpForwardVertex(HikariUrpForwardAttributes input)
{
    HikariUrpForwardVaryings output = (HikariUrpForwardVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    half tangentSign = input.tangentOS.w * GetOddNegativeScale();

    output.texcoord.xy = input.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;
    float2 detailSource = _UVSec < 0.5h ? input.uv0 : input.uv1;
    output.texcoord.zw = detailSource * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = half4(normalInputs.tangentWS, tangentSign);
#if defined(_HIKARI_MATERIAL_LIGHTMAP)
    output.lightmapUV =
        input.uv1 * _HikariMaterialLightmapTransform.xy + _HikariMaterialLightmapTransform.zw;
#elif defined(LIGHTMAP_ON)
    OUTPUT_LIGHTMAP_UV(input.uv1, unity_LightmapST, output.lightmapUV);
#else
    OUTPUT_SH(output.normalWS, output.vertexSH);
#endif
    output.shadowCoord = GetShadowCoord(positionInputs);
    output.fogAndVertexLight = half4(
        ComputeFogFactor(positionInputs.positionCS.z),
        VertexLighting(positionInputs.positionWS, normalInputs.normalWS));
    output.screenPosition = ComputeScreenPos(positionInputs.positionCS);
    output.positionCS = positionInputs.positionCS;
    return output;
}

#if defined(LIGHTMAP_ON)
half3 HikariUrpSampleMonoSh(float2 staticLightmapUV, half3 normalWS)
{
#ifdef UNITY_LIGHTMAP_FULL_HDR
    bool encodedLightmap = false;
#else
    bool encodedLightmap = true;
#endif
    half4 decodeInstructions = half4(
        LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h);
    half4 transformCoords = half4(1.0h, 1.0h, 0.0h, 0.0h);
    half3 baked = SampleSingleLightmap(
        TEXTURE2D_LIGHTMAP_ARGS(LIGHTMAP_NAME, LIGHTMAP_SAMPLER_NAME),
        LIGHTMAP_SAMPLE_EXTRA_ARGS, transformCoords, encodedLightmap, decodeInstructions);
    half4 direction = SAMPLE_TEXTURE2D_LIGHTMAP(
        LIGHTMAP_INDIRECTION_NAME, LIGHTMAP_SAMPLER_NAME, LIGHTMAP_SAMPLE_EXTRA_ARGS);
    float3 nl1 = direction.rgb * 2.0 - 1.0;
    return baked * HikariMonoShGain(nl1, normalWS);
}
#endif

half3 HikariUrpSampleMaterialLightmap(float2 uv, half3 normalWS)
{
#ifdef UNITY_LIGHTMAP_FULL_HDR
    bool encodedLightmap = false;
#else
    bool encodedLightmap = true;
#endif
    half4 decodeInstructions = half4(
        LIGHTMAP_HDR_MULTIPLIER, LIGHTMAP_HDR_EXPONENT, 0.0h, 0.0h);
    half4 encoded = SAMPLE_TEXTURE2D(
        _HikariMaterialLightmap, sampler_HikariMaterialLightmap, uv);
    half3 baked = encodedLightmap ? DecodeLightmap(encoded, decodeInstructions) : encoded.rgb;
    half4 direction = SAMPLE_TEXTURE2D(
        _HikariMaterialLightmapDirection, sampler_HikariMaterialLightmapDirection, uv);
    return HikariDecodeDirectionalLightmap(
        baked, direction, normalWS, _HikariMaterialLightmapMode);
}

#if defined(_HIKARI_SCREEN_SPACE_REFLECTION) && !defined(_GLOSSYREFLECTIONS_OFF) && !defined(HIKARI_URP_TWO_PASS_BACK)

#include "HikariSsrShared.hlsl"

HikariSsrResult HikariUrpTraceScreenSpaceReflection(
    float3 positionWS,
    half3 normalWS,
    half smoothness)
{
    HikariSsrResult result;
    result.color = 0.0h;
    result.confidence = 0.0h;

    if (_HikariSsrIntensity <= 0.0h)
    {
        return result;
    }

    if (smoothness <= kHikariSsrSmoothnessMin)
    {
        return result;
    }

    int stepCount = clamp((int)_HikariSsrSteps, 1, 64);
    float maxDistance = max(_HikariSsrMaxDistance, 1.0e-3);
    float thickness = max(_HikariSsrThickness, 1.0e-4);
    float3 positionVS = TransformWorldToView(positionWS);
    float3 normalVS = normalize(TransformWorldToViewDir(normalWS, true));
    float3 viewRayVS = normalize(positionVS);
    float4x4 projection = GetViewToHClipMatrix();
    float4 originPositionCS = mul(projection, float4(positionVS, 1.0));
    float4 originScreenPosition = ComputeScreenPos(originPositionCS);
    float2 originUv = originScreenPosition.xy / originScreenPosition.w;
    float4 viewportScaleOffset = HikariSsrViewportScaleOffset();
    float2 viewportScale = max(viewportScaleOffset.xy, 1.0e-6);
    float2 viewportUvMin = viewportScaleOffset.zw;
    float2 viewportUvMax = viewportUvMin + viewportScale;
    float2 viewportExtent = max(_ScreenParams.xy * viewportScale, 1.0);
    float2 originViewportUv = (originUv - viewportUvMin) / viewportScale;
    float2 originPixel = floor(originViewportUv * viewportExtent);
    float3 lowDiscrepancySample = HikariSsrLowDiscrepancySample(
        originPixel, viewportExtent.x);
    float2 randomSample = lowDiscrepancySample.xy;
    float ddaJitter = 1.0 - lowDiscrepancySample.z;
    float3 microfacetNormalVS = HikariSsrSampleMicrofacetNormal(
        -viewRayVS,
        normalVS,
        _HikariSsrSmoothness,
        randomSample);
    float3 reflectedVS = normalize(reflect(viewRayVS, microfacetNormalVS));
    float minimumDistance = max(thickness * 2.0, maxDistance / (float)stepCount * 0.25);
    float endDistance = minimumDistance + maxDistance;
    float nearPlaneZ = -_ProjectionParams.y * 1.01;
    if (reflectedVS.z > 0.0)
    {
        endDistance = min(endDistance, (nearPlaneZ - positionVS.z) / reflectedVS.z);
    }
    if (endDistance <= minimumDistance)
    {
        return result;
    }

    float3 startPositionVS = positionVS + reflectedVS * minimumDistance;
    float3 endPositionVS = positionVS + reflectedVS * endDistance;
    float4 startPositionCS = mul(projection, float4(startPositionVS, 1.0));
    float4 endPositionCS = mul(projection, float4(endPositionVS, 1.0));
    float startK = rcp(startPositionCS.w);
    float endK = rcp(endPositionCS.w);
    float3 startQ = startPositionVS * startK;
    float3 endQ = endPositionVS * endK;
    float4 startScreenPosition = ComputeScreenPos(startPositionCS);
    float4 endScreenPosition = ComputeScreenPos(endPositionCS);
    float2 startPixel = startScreenPosition.xy / startScreenPosition.w * _ScreenParams.xy;
    float2 endPixel = endScreenPosition.xy / endScreenPosition.w * _ScreenParams.xy;
    float2 screenMin = viewportUvMin * _ScreenParams.xy + 0.5;
    float2 screenMax = viewportUvMax * _ScreenParams.xy - 0.5;
    if (any(startPixel < screenMin) || any(startPixel > screenMax))
    {
        return result;
    }

    float2 pixelDelta = endPixel - startPixel;
    float clipFraction = 1.0;
    if (pixelDelta.x > 0.0)
    {
        clipFraction = min(clipFraction, (screenMax.x - startPixel.x) / pixelDelta.x);
    }
    else if (pixelDelta.x < 0.0)
    {
        clipFraction = min(clipFraction, (screenMin.x - startPixel.x) / pixelDelta.x);
    }
    if (pixelDelta.y > 0.0)
    {
        clipFraction = min(clipFraction, (screenMax.y - startPixel.y) / pixelDelta.y);
    }
    else if (pixelDelta.y < 0.0)
    {
        clipFraction = min(clipFraction, (screenMin.y - startPixel.y) / pixelDelta.y);
    }
    clipFraction = saturate(clipFraction);
    endPixel = lerp(startPixel, endPixel, clipFraction);
    endQ = lerp(startQ, endQ, clipFraction);
    endK = lerp(startK, endK, clipFraction);

    bool permute = abs(endPixel.x - startPixel.x) < abs(endPixel.y - startPixel.y);
    float2 ddaStart = permute ? startPixel.yx : startPixel;
    float2 ddaEnd = permute ? endPixel.yx : endPixel;
    float2 ddaDelta = ddaEnd - ddaStart;
    float majorLength = abs(ddaDelta.x);
    if (majorLength < 1.0e-4)
    {
        return result;
    }

    float stride = max(1.0, majorLength / (float)stepCount);
    float direction = ddaDelta.x >= 0.0 ? 1.0 : -1.0;
    float inverseMajorLength = rcp(majorLength);
    float2 pixelStep = float2(direction, ddaDelta.y * inverseMajorLength) * stride;
    float3 qStep = (endQ - startQ) * (stride * inverseMajorLength);
    float kStep = (endK - startK) * (stride * inverseMajorLength);
    float2 ddaPixel = ddaStart + pixelStep * ddaJitter;
    float3 ddaQ = startQ + qStep * ddaJitter;
    float ddaK = startK + kStep * ddaJitter;
    float traversedPixels = stride * ddaJitter;
    float2 projectionScale = float2(projection[0][0], projection[1][1]);
    float previousDistance = minimumDistance;
    float previousRayDepth = -startPositionVS.z;
    float previousDelta = -HikariSsrProjectedThickness(
        thickness,
        previousRayDepth,
        projectionScale,
        viewportExtent);
    float hitDistance = 0.0;
    float2 hitUv = 0.0;
    bool hit = false;

    [loop]
    for (int step = 1; step <= 64; ++step)
    {
        if (step > stepCount)
        {
            break;
        }
        if (traversedPixels > majorLength + 1.0e-4)
        {
            break;
        }

        float2 samplePixel = permute ? ddaPixel.yx : ddaPixel;
        float2 sampleUv = samplePixel / _ScreenParams.xy;
        float3 samplePositionVS = ddaQ / ddaK;
        float distanceAlongRay = dot(samplePositionVS - positionVS, reflectedVS);
        if (samplePositionVS.z >= -_ProjectionParams.y)
        {
            break;
        }
        if (any(sampleUv <= viewportUvMin) ||
            any(sampleUv >= viewportUvMax))
        {
            break;
        }

        float sceneDepth = HikariSsrLinearEyeDepth(
            SampleSceneDepth(sampleUv), (sampleUv - viewportUvMin) / viewportScale);
        float rayDepth = -samplePositionVS.z;
        float delta = rayDepth - sceneDepth;
        float projectedThickness = HikariSsrProjectedThickness(
            thickness,
            sceneDepth,
            projectionScale,
            viewportExtent);
        float rayDepthMin = min(previousRayDepth, rayDepth);
        float rayDepthMax = max(previousRayDepth, rayDepth);
        if (rayDepthMax + projectedThickness >= sceneDepth &&
            rayDepthMin - projectedThickness <= sceneDepth)
        {
            float low = previousDistance;
            float high = distanceAlongRay;
            float lowDelta = previousDelta;
            float bestDistance = distanceAlongRay;
            float2 bestUv = sampleUv;
            float bestError = abs(delta);
            float bestThickness = projectedThickness;
            [unroll]
            for (int refinement = 0; refinement < 5; ++refinement)
            {
                float middle = (low + high) * 0.5;
                float3 refinedPositionVS = positionVS + reflectedVS * middle;
                float4 refinedPositionCS = mul(projection, float4(refinedPositionVS, 1.0));
                float4 refinedScreenPosition = ComputeScreenPos(refinedPositionCS);
                float2 refinedUv = refinedScreenPosition.xy / refinedScreenPosition.w;
                float refinedSceneDepth = HikariSsrLinearEyeDepth(
                    SampleSceneDepth(refinedUv), (refinedUv - viewportUvMin) / viewportScale);
                float middleDelta = -refinedPositionVS.z - refinedSceneDepth;
                float middleThickness = HikariSsrProjectedThickness(
                    thickness,
                    refinedSceneDepth,
                    projectionScale,
                    viewportExtent);
                float middleError = abs(middleDelta);
                if (middleError < bestError)
                {
                    bestDistance = middle;
                    bestUv = refinedUv;
                    bestError = middleError;
                    bestThickness = middleThickness;
                }
                if ((lowDelta <= 0.0 && middleDelta >= 0.0) ||
                    (lowDelta >= 0.0 && middleDelta <= 0.0))
                {
                    high = middle;
                }
                else
                {
                    low = middle;
                    lowDelta = middleDelta;
                }
            }

            if (bestError <= bestThickness)
            {
                hit = true;
                hitDistance = bestDistance;
                hitUv = bestUv;
                break;
            }
        }

        previousDistance = distanceAlongRay;
        previousRayDepth = rayDepth;
        previousDelta = delta;
        ddaPixel += pixelStep;
        ddaQ += qStep;
        ddaK += kStep;
        traversedPixels += stride;
    }

    if (hit)
    {
        float2 hitViewportUv = (hitUv - viewportUvMin) / viewportScale;
        float edge = min(
            min(hitViewportUv.x, hitViewportUv.y),
            min(1.0 - hitViewportUv.x, 1.0 - hitViewportUv.y));
        float edgeConfidence = saturate(edge * 20.0);
        float distanceConfidence = saturate(1.0 - hitDistance / maxDistance);
        float smoothnessConfidence =
            saturate((smoothness - kHikariSsrSmoothnessMin) / kHikariSsrSmoothnessRange);
        result.color = SampleSceneColor(hitUv);
        result.confidence = edgeConfidence * distanceConfidence * smoothnessConfidence;
    }
    return result;
}

#endif

half4 HikariUrpForwardFragment(HikariUrpForwardVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half3 normalWS = NormalizeNormalPerVertex(input.normalWS);
    half3 tangentWS = normalize(input.tangentWS.xyz);
    half3 bitangentWS = input.tangentWS.w * cross(normalWS, tangentWS);
    half3x3 tangentToWorld = half3x3(tangentWS, bitangentWS, normalWS);
    half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirectionTS = mul(transpose(tangentToWorld), viewDirectionWS);

    HikariUrpSurfaceUv texcoord = HikariUrpApplyParallax(input.texcoord, viewDirectionTS);
    SurfaceData surfaceData;
    HikariUrpInitializeSurface(texcoord, surfaceData);
    normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(surfaceData.normalTS, tangentToWorld));

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalWS = normalWS;
    inputData.viewDirectionWS = viewDirectionWS;
    inputData.shadowCoord = input.shadowCoord;
    inputData.fogCoord = input.fogAndVertexLight.x;
    inputData.vertexLighting = input.fogAndVertexLight.yzw;
#if defined(_HIKARI_MATERIAL_LIGHTMAP)
    inputData.bakedGI = HikariUrpSampleMaterialLightmap(input.lightmapUV, normalWS);
#elif defined(LIGHTMAP_ON)
#if defined(_HIKARI_MONOSH) && defined(DIRLIGHTMAP_COMBINED)
    inputData.bakedGI = HikariUrpSampleMonoSh(input.lightmapUV, normalWS);
#else
    inputData.bakedGI = SAMPLE_GI(input.lightmapUV, half3(0.0h, 0.0h, 0.0h), normalWS);
#endif
#else
    inputData.bakedGI = SAMPLE_GI(float2(0.0, 0.0), input.vertexSH, normalWS);
#endif
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);
    inputData.tangentToWorld = tangentToWorld;

    half4 color = UniversalFragmentPBR(inputData, surfaceData);

#if defined(_HIKARI_SCREEN_SPACE_REFLECTION) && !defined(_GLOSSYREFLECTIONS_OFF) && !defined(HIKARI_URP_TWO_PASS_BACK)
    HikariSsrResult ssr = HikariUrpTraceScreenSpaceReflection(
        input.positionWS, normalWS, surfaceData.smoothness);
    half ssrConfidence = saturate(ssr.confidence * _HikariSsrIntensity);
    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);
    half3 reflectVector = reflect(-viewDirectionWS, normalWS);
    half fresnelTerm = Pow4(1.0h - saturate(dot(normalWS, viewDirectionWS)));
    half3 probe = GlossyEnvironmentReflection(
        reflectVector,
        input.positionWS,
        brdfData.perceptualRoughness,
        1.0h,
        inputData.normalizedScreenSpaceUV);
    half3 probeContribution = EnvironmentBRDF(brdfData, 0.0h, probe, fresnelTerm);
    half3 ssrContribution = EnvironmentBRDF(
        brdfData, 0.0h, lerp(probe, ssr.color, ssrConfidence), fresnelTerm);
    color.rgb += (ssrContribution - probeContribution) * surfaceData.occlusion;
#endif

#if !defined(HIKARI_URP_TWO_PASS_FRONT)
    if (_HikariStandardVariant > 0.5h)
    {
        float4 scenePosition = input.screenPosition;
        if (_HikariStandardVariant < 1.5h)
        {
            float3 positionVS = TransformWorldToView(input.positionWS);
            float3 normalVS = TransformWorldToViewDir(normalWS, true);
            float3 viewRayVS = normalize(positionVS);
            float3 lateral = normalVS - viewRayVS * dot(normalVS, viewRayVS);
            float4x4 projection = GetViewToHClipMatrix();
            scenePosition.xy += 0.5 * float2(
                projection[0][0] * lateral.x,
                projection[1][1] * lateral.y) * _HikariRefractionStrength;
        }
        float2 sceneUv = scenePosition.xy / scenePosition.w;
        half3 transmission = _HikariTransmissionColor.rgb * SAMPLE_TEXTURE2D(
            _HikariTransmissionMap, sampler_HikariTransmissionMap, texcoord.main).rgb;
        half3 refracted = SampleSceneColor(sceneUv) * transmission;
        half coverage = HikariUrpTransmissionCoverage(surfaceData);
#if defined(_ALPHABLEND_ON)
        color.rgb *= surfaceData.alpha;
#endif
        color.rgb += refracted * (1.0h - coverage);
        color.a = 1.0h;
    }
#endif

#if !defined(_ALPHABLEND_ON) && !defined(_ALPHAPREMULTIPLY_ON)
    color.a = 1.0h;
#endif
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    return color;
}

#endif

#if defined(HIKARI_URP_SHADOW_PASS)

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct HikariUrpShadowAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv0 : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct HikariUrpShadowVaryings
{
    float2 uv : TEXCOORD0;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

HikariUrpShadowVaryings HikariUrpShadowVertex(HikariUrpShadowAttributes input)
{
    HikariUrpShadowVaryings output = (HikariUrpShadowVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    output.positionCS = TransformWorldToHClip(
        ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if UNITY_REVERSED_Z
    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    output.uv = input.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;
    return output;
}

half4 HikariUrpShadowFragment(HikariUrpShadowVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
#if defined(_ALPHATEST_ON)
    half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _Color.a;
    clip(alpha - _Cutoff);
#endif
    return 0.0h;
}

#endif

#if defined(HIKARI_URP_DEPTH_PASS)

struct HikariUrpDepthAttributes
{
    float4 positionOS : POSITION;
    float2 uv0 : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct HikariUrpDepthVaryings
{
    float2 uv : TEXCOORD0;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

HikariUrpDepthVaryings HikariUrpDepthVertex(HikariUrpDepthAttributes input)
{
    HikariUrpDepthVaryings output = (HikariUrpDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.uv = input.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    return output;
}

half HikariUrpDepthFragment(HikariUrpDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(_ALPHATEST_ON)
    half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _Color.a;
    clip(alpha - _Cutoff);
#endif
    return input.positionCS.z;
}

#endif

#if defined(HIKARI_URP_DEPTH_NORMALS_PASS)

struct HikariUrpDepthNormalsAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct HikariUrpDepthNormalsVaryings
{
    float4 texcoord : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half4 tangentWS : TEXCOORD3;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

HikariUrpDepthNormalsVaryings HikariUrpDepthNormalsVertex(
    HikariUrpDepthNormalsAttributes input)
{
    HikariUrpDepthNormalsVaryings output = (HikariUrpDepthNormalsVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
    output.texcoord.xy = input.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;
    float2 detailSource = _UVSec < 0.5h ? input.uv0 : input.uv1;
    output.texcoord.zw = detailSource * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = normalInputs.normalWS;
    output.tangentWS = half4(
        normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
    output.positionCS = positionInputs.positionCS;
    return output;
}

half4 HikariUrpDepthNormalsFragment(HikariUrpDepthNormalsVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half3 normalWS = NormalizeNormalPerVertex(input.normalWS);
    half3 tangentWS = normalize(input.tangentWS.xyz);
    half3 bitangentWS = input.tangentWS.w * cross(normalWS, tangentWS);
    half3x3 tangentToWorld = half3x3(tangentWS, bitangentWS, normalWS);
    half3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    half3 viewDirectionTS = mul(transpose(tangentToWorld), viewDirectionWS);
    HikariUrpSurfaceUv texcoord = HikariUrpApplyParallax(input.texcoord, viewDirectionTS);
    SurfaceData surfaceData;
    HikariUrpInitializeSurface(texcoord, surfaceData);
    normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(surfaceData.normalTS, tangentToWorld));
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormal = PackNormalOctQuadEncode(normalWS);
    half3 packedNormal = PackFloat2To888(saturate(octNormal * 0.5 + 0.5));
    return half4(packedNormal, 0.0h);
#else
    return half4(normalWS, 0.0h);
#endif
}

#endif

#if defined(HIKARI_URP_META_PASS)

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

struct HikariUrpMetaAttributes
{
    float4 positionOS : POSITION;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct HikariUrpMetaVaryings
{
    float4 positionCS : SV_POSITION;
    float4 texcoord : TEXCOORD0;
#ifdef EDITOR_VISUALIZATION
    float2 visualizationUv : TEXCOORD1;
    float4 lightCoord : TEXCOORD2;
#endif
};

HikariUrpMetaVaryings HikariUrpMetaVertex(HikariUrpMetaAttributes input)
{
    HikariUrpMetaVaryings output = (HikariUrpMetaVaryings)0;
    output.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
    output.texcoord.xy = input.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;
    float2 detailSource = _UVSec < 0.5h ? input.uv0 : input.uv1;
    output.texcoord.zw = detailSource * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
#ifdef EDITOR_VISUALIZATION
    UnityEditorVizData(
        input.positionOS.xyz,
        input.uv0,
        input.uv1,
        input.uv2,
        output.visualizationUv,
        output.lightCoord);
#endif
    return output;
}

half4 HikariUrpMetaFragment(HikariUrpMetaVaryings input) : SV_Target
{
    HikariUrpSurfaceUv texcoord;
    texcoord.main = input.texcoord.xy;
    texcoord.detail = input.texcoord.zw;
    SurfaceData surfaceData;
    HikariUrpInitializeSurface(texcoord, surfaceData);
    BRDFData brdfData;
    InitializeBRDFData(surfaceData, brdfData);
    MetaInput metaInput = (MetaInput)0;
    metaInput.Albedo = brdfData.diffuse + brdfData.specular * brdfData.roughness * 0.5h;
    metaInput.Emission = surfaceData.emission;
#ifdef EDITOR_VISUALIZATION
    metaInput.VizUV = input.visualizationUv;
    metaInput.LightCoord = input.lightCoord;
#endif
    return UnityMetaFragment(metaInput);
}

#endif

#endif
