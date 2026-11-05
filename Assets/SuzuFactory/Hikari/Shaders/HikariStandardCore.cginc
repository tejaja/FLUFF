#ifndef HIKARI_STANDARD_CORE_INCLUDED
#define HIKARI_STANDARD_CORE_INCLUDED

#include "UnityCG.cginc"

#if defined(_HIKARI_SCREEN_SPACE_REFLECTION)
#define HIKARI_SCREEN_SPACE_REFLECTION 1
#define HIKARI_SCREEN_SPACE_TRANSMISSION 1
#endif

fixed4 _Color;
sampler2D _MainTex;
float4 _MainTex_ST;
half _Cutoff;
half _Metallic;
half _Glossiness;
sampler2D _MetallicGlossMap;
half _GlossMapScale;
sampler2D _BumpMap;
half _BumpScale;
sampler2D _ParallaxMap;
half _Parallax;
sampler2D _OcclusionMap;
half _OcclusionStrength;
fixed4 _EmissionColor;
sampler2D _EmissionMap;
sampler2D _DetailMask;
sampler2D _DetailAlbedoMap;
float4 _DetailAlbedoMap_ST;
sampler2D _DetailNormalMap;
half _DetailNormalMapScale;
half _UVSec;
sampler2D _HikariMaterialLightmap;
sampler2D _HikariMaterialLightmapDirection;
float4 _HikariMaterialLightmapTransform;
half _HikariMaterialLightmapMode;
half _HikariStandardVariant;

#if defined(HIKARI_TRANSMISSION_BLEND) || defined(HIKARI_SCREEN_SPACE_TRANSMISSION)
fixed4 _HikariTransmissionColor;
sampler2D _HikariTransmissionMap;
#endif

#if defined(HIKARI_SCREEN_SPACE_TRANSMISSION)
#define HIKARI_GRAB_TRANSMISSION 1
#endif

#if defined(HIKARI_SCREEN_SPACE_REFLECTION) || defined(HIKARI_SCREEN_SPACE_TRANSMISSION)
#if defined(HIKARI_SCREEN_SPACE_REFLECTION)
UNITY_DECLARE_SCREENSPACE_TEXTURE(_HikariScreenColorReflectionTexture);
UNITY_DECLARE_SCREENSPACE_TEXTURE(_HikariScreenColorRefractionReflectionTexture);
UNITY_DECLARE_SCREENSPACE_TEXTURE(_HikariScreenColorTransmissionReflectionTexture);
#else
UNITY_DECLARE_SCREENSPACE_TEXTURE(_HikariScreenColorRefractionTexture);
#endif
#endif

#if defined(HIKARI_SCREEN_SPACE_REFLECTION)
UNITY_DECLARE_SCREENSPACE_TEXTURE(_CameraDepthTexture);
half _HikariSsrIntensity;
float _HikariSsrMaxDistance;
float _HikariSsrThickness;
float _HikariSsrSmoothness;
float _HikariSsrSteps;
#endif

#if defined(HIKARI_SCREEN_SPACE_TRANSMISSION)
half _HikariRefractionStrength;
#endif

bool HikariUsesGrabTransmission()
{
#if defined(HIKARI_SCREEN_SPACE_TRANSMISSION)
    return _HikariStandardVariant > 0.5;
#else
    return false;
#endif
}

#if defined(HIKARI_SCREEN_SPACE_REFLECTION) || defined(HIKARI_SCREEN_SPACE_TRANSMISSION)
half3 HikariSampleGrabTexture(float4 grab_position)
{
    float2 grab_uv = grab_position.xy / grab_position.w;
#if defined(HIKARI_SCREEN_SPACE_REFLECTION)
    if (_HikariStandardVariant < 0.5)
    {
        return UNITY_SAMPLE_SCREENSPACE_TEXTURE(
            _HikariScreenColorReflectionTexture,
            grab_uv).rgb;
    }
    if (_HikariStandardVariant < 1.5)
    {
        return UNITY_SAMPLE_SCREENSPACE_TEXTURE(
            _HikariScreenColorRefractionReflectionTexture,
            grab_uv).rgb;
    }
    return UNITY_SAMPLE_SCREENSPACE_TEXTURE(
        _HikariScreenColorTransmissionReflectionTexture,
        grab_uv).rgb;
#else
    return UNITY_SAMPLE_SCREENSPACE_TEXTURE(
        _HikariScreenColorRefractionTexture,
        grab_uv).rgb;
#endif
}
#endif

struct HikariSurfaceUv
{
    float2 uv;
    float2 detail_uv;
};

HikariSurfaceUv HikariApplyParallax(float4 tex, half3 tangent_view_dir)
{
    HikariSurfaceUv result;
    result.uv = tex.xy;
    result.detail_uv = tex.zw;
#if defined(_PARALLAXMAP)
    half height = tex2D(_ParallaxMap, tex.xy).g;
    float2 parallax_offset = ParallaxOffset(height, _Parallax, tangent_view_dir);
    result.uv += parallax_offset;
    result.detail_uv += parallax_offset;
#endif
    return result;
}

float2 HikariDetailTexcoord(float2 uv0, float2 uv1)
{
    float2 detail_source = (_UVSec == 0.0) ? uv0 : uv1;
    return detail_source * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
}

#if defined(HIKARI_TRANSMISSION_BLEND)

#include "UnityStandardUtils.cginc"

struct HikariVaryingsTransmission
{
    UNITY_POSITION(pos);
    float4 tex : TEXCOORD0;
#if defined(_PARALLAXMAP)
    half3 tangent_view_dir : TEXCOORD1;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

HikariVaryingsTransmission HikariVertTransmission(appdata_full v)
{
    UNITY_SETUP_INSTANCE_ID(v);
    HikariVaryingsTransmission o;
    UNITY_INITIALIZE_OUTPUT(HikariVaryingsTransmission, o);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.pos = UnityObjectToClipPos(v.vertex);
    o.tex.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
    o.tex.zw = 0.0;
#if defined(_PARALLAXMAP)
    TANGENT_SPACE_ROTATION;
    o.tangent_view_dir = mul(rotation, ObjSpaceViewDir(v.vertex));
#endif
    return o;
}

half4 HikariFragTransmission(HikariVaryingsTransmission i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

#if defined(_PARALLAXMAP)
    half3 tangent_view_dir = normalize(i.tangent_view_dir);
#else
    half3 tangent_view_dir = half3(0.0, 0.0, 1.0);
#endif
    HikariSurfaceUv uv = HikariApplyParallax(i.tex, tangent_view_dir);

#if defined(HIKARI_SCREEN_SPACE_TRANSMISSION_PASS)
    if (_HikariStandardVariant < 0.5)
    {
        return half4(1.0, 1.0, 1.0, 1.0);
    }
#endif

    fixed4 main_tex = tex2D(_MainTex, uv.uv);
    half alpha = main_tex.a * _Color.a;
#if defined(_ALPHATEST_ON)
    clip(alpha - _Cutoff);
#endif

    half3 transmission = _HikariTransmissionColor.rgb * tex2D(_HikariTransmissionMap, uv.uv).rgb;
    return half4(transmission, 1.0);
}

#endif

#if defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_FORWARDADD) || defined(UNITY_PASS_META)

#include "UnityPBSLighting.cginc"
#include "UnityStandardUtils.cginc"

void HikariSurface(HikariSurfaceUv IN, inout SurfaceOutputStandard o)
{
    fixed4 main_tex = tex2D(_MainTex, IN.uv);
    fixed4 albedo = main_tex * _Color;
#if defined(_ALPHATEST_ON)
    clip(albedo.a - _Cutoff);
#endif

#if defined(_DETAIL_MULX2)
    half detail_mask = tex2D(_DetailMask, IN.uv).a;
    half3 detail_albedo = tex2D(_DetailAlbedoMap, IN.detail_uv).rgb;
    albedo.rgb *= LerpWhiteTo(detail_albedo * unity_ColorSpaceDouble.rgb, detail_mask);
#endif
    o.Albedo = albedo.rgb;
    o.Alpha = albedo.a;

#if defined(_METALLICGLOSSMAP)
    o.Metallic = tex2D(_MetallicGlossMap, IN.uv).r;
#else
    o.Metallic = _Metallic;
#endif
#if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
    o.Smoothness = main_tex.a * _GlossMapScale;
#elif defined(_METALLICGLOSSMAP)
    o.Smoothness = tex2D(_MetallicGlossMap, IN.uv).a * _GlossMapScale;
#else
    o.Smoothness = _Glossiness;
#endif

#if defined(_NORMALMAP)
    half3 normal_tangent = UnpackScaleNormal(tex2D(_BumpMap, IN.uv), _BumpScale);
#else
    half3 normal_tangent = half3(0.0, 0.0, 1.0);
#endif
#if defined(_DETAIL_MULX2)
    half3 detail_normal = UnpackScaleNormal(tex2D(_DetailNormalMap, IN.detail_uv), _DetailNormalMapScale);
    normal_tangent = lerp(normal_tangent, BlendNormals(normal_tangent, detail_normal), detail_mask);
#endif
    o.Normal = normal_tangent;

    o.Occlusion = LerpOneTo(tex2D(_OcclusionMap, IN.uv).g, _OcclusionStrength);

#if defined(_EMISSION)
    o.Emission = tex2D(_EmissionMap, IN.uv).rgb * _EmissionColor.rgb;
#endif
}

#endif

#if defined(UNITY_PASS_FORWARDBASE) || defined(UNITY_PASS_FORWARDADD)

#include "AutoLight.cginc"
#include "HikariLightmapDecode.hlsl"

half3 HikariSampleMaterialLightmap(float2 uv, half3 normal)
{
    half3 baked = DecodeLightmap(tex2D(_HikariMaterialLightmap, uv));
    half4 direction = tex2D(_HikariMaterialLightmapDirection, uv);
    return HikariDecodeDirectionalLightmap(
        baked, direction, normal, _HikariMaterialLightmapMode);
}

inline half4 LightingHikariStandard(SurfaceOutputStandard s, float3 viewDir, UnityGI gi)
{
    half4 color = LightingStandard(s, viewDir, gi);
#if defined(_ALPHABLEND_ON)
    #if defined(UNITY_PASS_FORWARDADD)
    color.rgb *= s.Alpha;
    #elif defined(HIKARI_GRAB_TRANSMISSION)
    if (HikariUsesGrabTransmission())
    {
        color.rgb *= s.Alpha;
        #if !defined(HIKARI_TRANSPARENT_TWO_PASS_FRONT)
        color.a = 1.0;
        #endif
    }
    #endif
#elif defined(_ALPHAPREMULTIPLY_ON)
    #if defined(HIKARI_GRAB_TRANSMISSION) && !defined(UNITY_PASS_FORWARDADD) && !defined(HIKARI_TRANSPARENT_TWO_PASS_FRONT)
    if (HikariUsesGrabTransmission())
    {
        color.a = 1.0;
    }
    #endif
#else
    color.a = 1.0;
#endif
    return color;
}

inline void LightingHikariStandard_GI(SurfaceOutputStandard s, UnityGIInput data, inout UnityGI gi)
{
    LightingStandard_GI(s, data, gi);
#if defined(_HIKARI_MATERIAL_LIGHTMAP)
    gi.indirect.diffuse = HikariSampleMaterialLightmap(data.lightmapUV.xy, s.Normal) * s.Occlusion;
#elif defined(_HIKARI_MONOSH) && defined(LIGHTMAP_ON) && defined(DIRLIGHTMAP_COMBINED)
    half4 baked_tex = UNITY_SAMPLE_TEX2D(unity_Lightmap, data.lightmapUV.xy);
    half3 baked = DecodeLightmap(baked_tex);
    half4 dir_tex = UNITY_SAMPLE_TEX2D_SAMPLER(unity_LightmapInd, unity_Lightmap, data.lightmapUV.xy);
    float3 nl1 = dir_tex.rgb * 2.0 - 1.0;
    gi.indirect.diffuse = baked * HikariMonoShGain(nl1, s.Normal) * s.Occlusion;
#endif
}

#endif

#if defined(UNITY_PASS_FORWARDBASE)

#if defined(HIKARI_SCREEN_SPACE_REFLECTION) && !defined(_GLOSSYREFLECTIONS_OFF) && !defined(HIKARI_TRANSPARENT_TWO_PASS_BACK)

#include "HikariSsrShared.hlsl"

HikariSsrResult HikariTraceScreenSpaceReflection(
    float3 world_position,
    float3 world_normal,
    half smoothness)
{
    HikariSsrResult result;
    result.color = 0.0;
    result.confidence = 0.0;

    if (_HikariSsrIntensity <= 0.0)
    {
        return result;
    }

    if (smoothness <= kHikariSsrSmoothnessMin)
    {
        return result;
    }

    int step_count = clamp((int)_HikariSsrSteps, 1, 64);
    float max_distance = max(_HikariSsrMaxDistance, 1.0e-3);
    float thickness = max(_HikariSsrThickness, 1.0e-4);
    float3 view_position = mul(UNITY_MATRIX_V, float4(world_position, 1.0)).xyz;
    float3 view_normal = normalize(mul((float3x3)UNITY_MATRIX_V, world_normal));
    float3 view_ray = normalize(view_position);
    float4 origin_clip = mul(UNITY_MATRIX_P, float4(view_position, 1.0));
    float4 origin_screen = ComputeScreenPos(origin_clip);
    float2 origin_uv = origin_screen.xy / origin_screen.w;
    float4 viewport_scale_offset = HikariSsrViewportScaleOffset();
    float2 viewport_scale = max(viewport_scale_offset.xy, 1.0e-6);
    float2 viewport_uv_min = viewport_scale_offset.zw;
    float2 viewport_uv_max = viewport_uv_min + viewport_scale;
    float2 viewport_extent = max(_ScreenParams.xy * viewport_scale, 1.0);
    float2 origin_viewport_uv =
        (origin_uv - viewport_uv_min) / viewport_scale;
    float2 origin_pixel = floor(origin_viewport_uv * viewport_extent);
    float3 low_discrepancy_sample = HikariSsrLowDiscrepancySample(
        origin_pixel, viewport_extent.x);
    float2 random_sample = low_discrepancy_sample.xy;
    float dda_jitter = 1.0 - low_discrepancy_sample.z;
    float3 microfacet_normal = HikariSsrSampleMicrofacetNormal(
        -view_ray,
        view_normal,
        _HikariSsrSmoothness,
        random_sample);
    float3 reflected = normalize(reflect(view_ray, microfacet_normal));
    float minimum_distance = max(thickness * 2.0, max_distance / (float)step_count * 0.25);
    float end_distance = minimum_distance + max_distance;
    float near_plane_z = -_ProjectionParams.y * 1.01;
    if (reflected.z > 0.0)
    {
        end_distance = min(end_distance, (near_plane_z - view_position.z) / reflected.z);
    }
    if (end_distance <= minimum_distance)
    {
        return result;
    }

    float3 start_position = view_position + reflected * minimum_distance;
    float3 end_position = view_position + reflected * end_distance;
    float4 start_clip = mul(UNITY_MATRIX_P, float4(start_position, 1.0));
    float4 end_clip = mul(UNITY_MATRIX_P, float4(end_position, 1.0));
    float start_k = rcp(start_clip.w);
    float end_k = rcp(end_clip.w);
    float3 start_q = start_position * start_k;
    float3 end_q = end_position * end_k;
    float4 start_screen = ComputeScreenPos(start_clip);
    float4 end_screen = ComputeScreenPos(end_clip);
    float2 start_pixel = start_screen.xy / start_screen.w * _ScreenParams.xy;
    float2 end_pixel = end_screen.xy / end_screen.w * _ScreenParams.xy;
    float2 screen_min = viewport_uv_min * _ScreenParams.xy + 0.5;
    float2 screen_max = viewport_uv_max * _ScreenParams.xy - 0.5;
    if (any(start_pixel < screen_min) || any(start_pixel > screen_max))
    {
        return result;
    }

    float2 pixel_delta = end_pixel - start_pixel;
    float clip_fraction = 1.0;
    if (pixel_delta.x > 0.0)
    {
        clip_fraction = min(clip_fraction, (screen_max.x - start_pixel.x) / pixel_delta.x);
    }
    else if (pixel_delta.x < 0.0)
    {
        clip_fraction = min(clip_fraction, (screen_min.x - start_pixel.x) / pixel_delta.x);
    }
    if (pixel_delta.y > 0.0)
    {
        clip_fraction = min(clip_fraction, (screen_max.y - start_pixel.y) / pixel_delta.y);
    }
    else if (pixel_delta.y < 0.0)
    {
        clip_fraction = min(clip_fraction, (screen_min.y - start_pixel.y) / pixel_delta.y);
    }
    clip_fraction = saturate(clip_fraction);
    end_pixel = lerp(start_pixel, end_pixel, clip_fraction);
    end_q = lerp(start_q, end_q, clip_fraction);
    end_k = lerp(start_k, end_k, clip_fraction);

    bool permute = abs(end_pixel.x - start_pixel.x) < abs(end_pixel.y - start_pixel.y);
    float2 dda_start = permute ? start_pixel.yx : start_pixel;
    float2 dda_end = permute ? end_pixel.yx : end_pixel;
    float2 dda_delta = dda_end - dda_start;
    float major_length = abs(dda_delta.x);
    if (major_length < 1.0e-4)
    {
        return result;
    }

    float stride = max(1.0, major_length / (float)step_count);
    float direction = dda_delta.x >= 0.0 ? 1.0 : -1.0;
    float inverse_major_length = rcp(major_length);
    float2 pixel_step = float2(direction, dda_delta.y * inverse_major_length) * stride;
    float3 q_step = (end_q - start_q) * (stride * inverse_major_length);
    float k_step = (end_k - start_k) * (stride * inverse_major_length);
    float2 dda_pixel = dda_start + pixel_step * dda_jitter;
    float3 dda_q = start_q + q_step * dda_jitter;
    float dda_k = start_k + k_step * dda_jitter;
    float traversed_pixels = stride * dda_jitter;
    float2 projection_scale = float2(UNITY_MATRIX_P[0][0], UNITY_MATRIX_P[1][1]);
    float previous_distance = minimum_distance;
    float previous_ray_depth = -start_position.z;
    float previous_delta = -HikariSsrProjectedThickness(
        thickness,
        previous_ray_depth,
        projection_scale,
        viewport_extent);
    float hit_distance = 0.0;
    float4 hit_clip = 0.0;
    bool hit = false;

    [loop]
    for (int step = 1; step <= 64; ++step)
    {
        if (step > step_count)
        {
            break;
        }
        if (traversed_pixels > major_length + 1.0e-4)
        {
            break;
        }

        float2 sample_pixel = permute ? dda_pixel.yx : dda_pixel;
        float2 sample_uv = sample_pixel / _ScreenParams.xy;
        float3 sample_position = dda_q / dda_k;
        float distance_along_ray = dot(sample_position - view_position, reflected);
        if (sample_position.z >= -_ProjectionParams.y)
        {
            break;
        }
        if (any(sample_uv <= viewport_uv_min) ||
            any(sample_uv >= viewport_uv_max))
        {
            break;
        }

        float scene_depth = HikariSsrLinearEyeDepth(
            UNITY_SAMPLE_SCREENSPACE_TEXTURE(_CameraDepthTexture, sample_uv).r,
            (sample_uv - viewport_uv_min) / viewport_scale);
        float ray_depth = -sample_position.z;
        float delta = ray_depth - scene_depth;
        float projected_thickness = HikariSsrProjectedThickness(
            thickness,
            scene_depth,
            projection_scale,
            viewport_extent);
        float ray_depth_min = min(previous_ray_depth, ray_depth);
        float ray_depth_max = max(previous_ray_depth, ray_depth);
        if (ray_depth_max + projected_thickness >= scene_depth &&
            ray_depth_min - projected_thickness <= scene_depth)
        {
            float low = previous_distance;
            float high = distance_along_ray;
            float low_delta = previous_delta;
            float best_distance = distance_along_ray;
            float4 best_clip = mul(UNITY_MATRIX_P, float4(sample_position, 1.0));
            float best_error = abs(delta);
            float best_thickness = projected_thickness;
            [unroll]
            for (int refinement = 0; refinement < 5; ++refinement)
            {
                float middle = (low + high) * 0.5;
                float3 refined_position = view_position + reflected * middle;
                float4 middle_clip = mul(UNITY_MATRIX_P, float4(refined_position, 1.0));
                float4 refined_screen = ComputeScreenPos(middle_clip);
                float2 refined_uv = refined_screen.xy / refined_screen.w;
                float refined_scene_depth = HikariSsrLinearEyeDepth(
                    UNITY_SAMPLE_SCREENSPACE_TEXTURE(
                        _CameraDepthTexture, refined_uv).r,
                    (refined_uv - viewport_uv_min) / viewport_scale);
                float middle_delta = -refined_position.z - refined_scene_depth;
                float middle_thickness = HikariSsrProjectedThickness(
                    thickness,
                    refined_scene_depth,
                    projection_scale,
                    viewport_extent);
                float middle_error = abs(middle_delta);
                if (middle_error < best_error)
                {
                    best_distance = middle;
                    best_clip = middle_clip;
                    best_error = middle_error;
                    best_thickness = middle_thickness;
                }
                if ((low_delta <= 0.0 && middle_delta >= 0.0) ||
                    (low_delta >= 0.0 && middle_delta <= 0.0))
                {
                    high = middle;
                }
                else
                {
                    low = middle;
                    low_delta = middle_delta;
                }
            }

            if (best_error <= best_thickness)
            {
                hit = true;
                hit_distance = best_distance;
                hit_clip = best_clip;
                break;
            }
        }

        previous_distance = distance_along_ray;
        previous_ray_depth = ray_depth;
        previous_delta = delta;
        dda_pixel += pixel_step;
        dda_q += q_step;
        dda_k += k_step;
        traversed_pixels += stride;
    }

    if (hit)
    {
        float4 grab_position = ComputeGrabScreenPos(hit_clip);
        float2 hit_uv = grab_position.xy / grab_position.w;
        float2 hit_viewport_uv =
            (hit_uv - viewport_uv_min) / viewport_scale;
        float edge = min(
            min(hit_viewport_uv.x, hit_viewport_uv.y),
            min(1.0 - hit_viewport_uv.x, 1.0 - hit_viewport_uv.y));
        float edge_confidence = saturate(edge * 20.0);
        float distance_confidence = saturate(1.0 - hit_distance / max_distance);
        float smoothness_confidence =
            saturate((smoothness - kHikariSsrSmoothnessMin) / kHikariSsrSmoothnessRange);
        result.color = HikariSampleGrabTexture(grab_position);
        result.confidence = edge_confidence * distance_confidence * smoothness_confidence;
    }
    return result;
}

#endif

struct HikariVaryingsBase
{
    UNITY_POSITION(pos);
    float4 tex : TEXCOORD0;
    float3 world_pos : TEXCOORD1;
    float3 world_normal : TEXCOORD2;
#if defined(_NORMALMAP) || defined(_DETAIL_MULX2)
    float4 world_tangent : TEXCOORD3;
#endif
#if defined(_PARALLAXMAP)
    half3 tangent_view_dir : TEXCOORD4;
#endif
    half4 ambient_or_lightmap_uv : TEXCOORD5;
    UNITY_LIGHTING_COORDS(6, 7)
    UNITY_FOG_COORDS(8)
#if (defined(HIKARI_GRAB_TRANSMISSION) && !defined(HIKARI_TRANSPARENT_TWO_PASS_FRONT)) || defined(HIKARI_SCREEN_SPACE_REFLECTION)
    float4 grab_pos : TEXCOORD9;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

HikariVaryingsBase HikariVertBase(appdata_full v)
{
    UNITY_SETUP_INSTANCE_ID(v);
    HikariVaryingsBase o;
    UNITY_INITIALIZE_OUTPUT(HikariVaryingsBase, o);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.pos = UnityObjectToClipPos(v.vertex);
    o.tex.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
    o.tex.zw = HikariDetailTexcoord(v.texcoord.xy, v.texcoord1.xy);
    o.world_pos = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.world_normal = UnityObjectToWorldNormal(v.normal);
#if defined(_NORMALMAP) || defined(_DETAIL_MULX2)
    o.world_tangent = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
#endif
#if defined(_PARALLAXMAP)
    TANGENT_SPACE_ROTATION;
    o.tangent_view_dir = mul(rotation, ObjSpaceViewDir(v.vertex));
#endif

#if defined(_HIKARI_MATERIAL_LIGHTMAP)
    o.ambient_or_lightmap_uv.xy =
        v.texcoord1.xy * _HikariMaterialLightmapTransform.xy + _HikariMaterialLightmapTransform.zw;
#elif defined(LIGHTMAP_ON)
    o.ambient_or_lightmap_uv.xy = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
#elif UNITY_SHOULD_SAMPLE_SH
    half3 ambient = 0.0;
    #if defined(VERTEXLIGHT_ON)
    ambient = Shade4PointLights(
        unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
        unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
        unity_4LightAtten0, o.world_pos, o.world_normal);
    #endif
    o.ambient_or_lightmap_uv.rgb = ShadeSHPerVertex(o.world_normal, ambient);
#endif

    UNITY_TRANSFER_LIGHTING(o, v.texcoord1.xy);
    UNITY_TRANSFER_FOG(o, o.pos);
#if (defined(HIKARI_GRAB_TRANSMISSION) && !defined(HIKARI_TRANSPARENT_TWO_PASS_FRONT)) || defined(HIKARI_SCREEN_SPACE_REFLECTION)
    o.grab_pos = ComputeGrabScreenPos(o.pos);
#endif
    return o;
}

half4 HikariFragBase(HikariVaryingsBase i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

#if defined(_PARALLAXMAP)
    half3 tangent_view_dir = normalize(i.tangent_view_dir);
#else
    half3 tangent_view_dir = half3(0.0, 0.0, 1.0);
#endif
    HikariSurfaceUv uv = HikariApplyParallax(i.tex, tangent_view_dir);

    SurfaceOutputStandard s = (SurfaceOutputStandard)0;
    HikariSurface(uv, s);

#if defined(_NORMALMAP) || defined(_DETAIL_MULX2)
    float3 world_normal = normalize(i.world_normal);
    float3 world_tangent = normalize(i.world_tangent.xyz);
    float3 world_binormal = cross(world_normal, world_tangent) * (i.world_tangent.w * unity_WorldTransformParams.w);
    float3 normal_world = normalize(
        world_tangent * s.Normal.x + world_binormal * s.Normal.y + world_normal * s.Normal.z);
#else
    float3 normal_world = normalize(i.world_normal);
#endif
    s.Normal = normal_world;

    float3 world_view_dir = normalize(UnityWorldSpaceViewDir(i.world_pos));

    UNITY_LIGHT_ATTENUATION(atten, i, i.world_pos);

    UnityGI gi = (UnityGI)0;
    gi.light.color = _LightColor0.rgb;
    gi.light.dir = _WorldSpaceLightPos0.xyz;

    UnityGIInput gi_input = (UnityGIInput)0;
    gi_input.light = gi.light;
    gi_input.worldPos = i.world_pos;
    gi_input.worldViewDir = world_view_dir;
    gi_input.atten = atten;
#if defined(_HIKARI_MATERIAL_LIGHTMAP) || defined(LIGHTMAP_ON)
    gi_input.lightmapUV = i.ambient_or_lightmap_uv;
#elif UNITY_SHOULD_SAMPLE_SH
    gi_input.ambient = i.ambient_or_lightmap_uv.rgb;
#endif
    gi_input.probeHDR[0] = unity_SpecCube0_HDR;
    gi_input.probeHDR[1] = unity_SpecCube1_HDR;
#if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
    gi_input.boxMin[0] = unity_SpecCube0_BoxMin;
#endif
#if defined(UNITY_SPECCUBE_BOX_PROJECTION)
    gi_input.boxMax[0] = unity_SpecCube0_BoxMax;
    gi_input.probePosition[0] = unity_SpecCube0_ProbePosition;
    gi_input.boxMax[1] = unity_SpecCube1_BoxMax;
    gi_input.boxMin[1] = unity_SpecCube1_BoxMin;
    gi_input.probePosition[1] = unity_SpecCube1_ProbePosition;
#endif

    LightingHikariStandard_GI(s, gi_input, gi);

#if defined(HIKARI_SCREEN_SPACE_REFLECTION) && !defined(_GLOSSYREFLECTIONS_OFF) && !defined(HIKARI_TRANSPARENT_TWO_PASS_BACK)
    HikariSsrResult ssr = HikariTraceScreenSpaceReflection(i.world_pos, normal_world, s.Smoothness);
    gi.indirect.specular = lerp(
        gi.indirect.specular,
        ssr.color,
        saturate(ssr.confidence * _HikariSsrIntensity));
#endif

    half4 color = LightingHikariStandard(s, world_view_dir, gi);
    color.rgb += s.Emission;

#if defined(HIKARI_GRAB_TRANSMISSION)
#if !defined(HIKARI_TRANSPARENT_TWO_PASS_FRONT)
    half3 refracted = 0.0;
    if (HikariUsesGrabTransmission())
    {
        float4 grab_position = i.grab_pos;
        float refraction_strength = 0.0;
        #if defined(HIKARI_SCREEN_SPACE_TRANSMISSION)
        refraction_strength = _HikariStandardVariant < 1.5 ? _HikariRefractionStrength : 0.0;
        #endif
        float3 view_pos = mul(UNITY_MATRIX_V, float4(i.world_pos, 1.0)).xyz;
        float3 view_normal = mul((float3x3)UNITY_MATRIX_V, normal_world);
        float3 view_ray = normalize(view_pos);
        float3 lateral = view_normal - view_ray * dot(view_normal, view_ray);
        grab_position.xy += 0.5 * float2(UNITY_MATRIX_P[0][0] * lateral.x,
                                         UNITY_MATRIX_P[1][1] * lateral.y) * refraction_strength;
        half3 grabbed = HikariSampleGrabTexture(grab_position);
        refracted = grabbed * _HikariTransmissionColor.rgb * tex2D(_HikariTransmissionMap, uv.uv).rgb;
    }
#endif
#if defined(_ALPHAPREMULTIPLY_ON)
    half one_minus_reflectivity = OneMinusReflectivityFromMetallic(s.Metallic);
    half coverage = 1.0 - one_minus_reflectivity + s.Alpha * one_minus_reflectivity;
#elif defined(_ALPHABLEND_ON)
    half coverage = s.Alpha;
#else
    half coverage = 1.0;
#endif
#if defined(HIKARI_TRANSPARENT_TWO_PASS_FRONT)
    if (HikariUsesGrabTransmission())
    {
        color.a = coverage;
    }
#else
    if (HikariUsesGrabTransmission())
    {
        color.rgb += refracted * (1.0 - coverage);
    }
#endif
#endif

    UNITY_APPLY_FOG(i.fogCoord, color.rgb);
    return color;
}

#endif

#if defined(UNITY_PASS_FORWARDADD)

struct HikariVaryingsAdd
{
    UNITY_POSITION(pos);
    float4 tex : TEXCOORD0;
    float3 world_pos : TEXCOORD1;
    float3 world_normal : TEXCOORD2;
#if defined(_NORMALMAP) || defined(_DETAIL_MULX2)
    float4 world_tangent : TEXCOORD3;
#endif
#if defined(_PARALLAXMAP)
    half3 tangent_view_dir : TEXCOORD4;
#endif
    UNITY_LIGHTING_COORDS(5, 6)
    UNITY_FOG_COORDS(7)
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

HikariVaryingsAdd HikariVertAdd(appdata_full v)
{
    UNITY_SETUP_INSTANCE_ID(v);
    HikariVaryingsAdd o;
    UNITY_INITIALIZE_OUTPUT(HikariVaryingsAdd, o);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    o.pos = UnityObjectToClipPos(v.vertex);
    o.tex.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
    o.tex.zw = HikariDetailTexcoord(v.texcoord.xy, v.texcoord1.xy);
    o.world_pos = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.world_normal = UnityObjectToWorldNormal(v.normal);
#if defined(_NORMALMAP) || defined(_DETAIL_MULX2)
    o.world_tangent = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
#endif
#if defined(_PARALLAXMAP)
    TANGENT_SPACE_ROTATION;
    o.tangent_view_dir = mul(rotation, ObjSpaceViewDir(v.vertex));
#endif

    UNITY_TRANSFER_LIGHTING(o, v.texcoord1.xy);
    UNITY_TRANSFER_FOG(o, o.pos);
    return o;
}

half4 HikariFragAdd(HikariVaryingsAdd i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

#if defined(_PARALLAXMAP)
    half3 tangent_view_dir = normalize(i.tangent_view_dir);
#else
    half3 tangent_view_dir = half3(0.0, 0.0, 1.0);
#endif
    HikariSurfaceUv uv = HikariApplyParallax(i.tex, tangent_view_dir);

    SurfaceOutputStandard s = (SurfaceOutputStandard)0;
    HikariSurface(uv, s);

#if defined(_NORMALMAP) || defined(_DETAIL_MULX2)
    float3 world_normal = normalize(i.world_normal);
    float3 world_tangent = normalize(i.world_tangent.xyz);
    float3 world_binormal = cross(world_normal, world_tangent) * (i.world_tangent.w * unity_WorldTransformParams.w);
    float3 normal_world = normalize(
        world_tangent * s.Normal.x + world_binormal * s.Normal.y + world_normal * s.Normal.z);
#else
    float3 normal_world = normalize(i.world_normal);
#endif
    s.Normal = normal_world;

    float3 world_view_dir = normalize(UnityWorldSpaceViewDir(i.world_pos));

    UNITY_LIGHT_ATTENUATION(atten, i, i.world_pos);

    UnityGI gi = (UnityGI)0;
    gi.light.color = _LightColor0.rgb * atten;
    gi.light.dir = normalize(UnityWorldSpaceLightDir(i.world_pos));

    half4 color = LightingHikariStandard(s, world_view_dir, gi);
    UNITY_APPLY_FOG_COLOR(i.fogCoord, color.rgb, fixed4(0.0, 0.0, 0.0, 0.0));
    return color;
}

#endif

#if defined(UNITY_PASS_SHADOWCASTER)

struct HikariVaryingsShadow
{
    V2F_SHADOW_CASTER;
#if defined(_ALPHATEST_ON)
    float2 tex : TEXCOORD1;
#endif
};

HikariVaryingsShadow HikariVertShadow(appdata_base v)
{
    UNITY_SETUP_INSTANCE_ID(v);
    HikariVaryingsShadow o;
    UNITY_INITIALIZE_OUTPUT(HikariVaryingsShadow, o);
    TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
#if defined(_ALPHATEST_ON)
    o.tex = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
#endif
    return o;
}

float4 HikariFragShadow(HikariVaryingsShadow i) : SV_Target
{
#if defined(_ALPHATEST_ON)
    fixed alpha = tex2D(_MainTex, i.tex).a * _Color.a;
    clip(alpha - _Cutoff);
#endif
    SHADOW_CASTER_FRAGMENT(i)
}

#endif

#if defined(UNITY_PASS_META)

#include "UnityMetaPass.cginc"

struct HikariVaryingsMeta
{
    float4 pos : SV_POSITION;
    float4 tex : TEXCOORD0;
};

HikariVaryingsMeta HikariVertMeta(appdata_full v)
{
    HikariVaryingsMeta o;
    o.pos = UnityMetaVertexPosition(v.vertex, v.texcoord1.xy, v.texcoord2.xy, unity_LightmapST, unity_DynamicLightmapST);
    o.tex.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
    o.tex.zw = HikariDetailTexcoord(v.texcoord.xy, v.texcoord1.xy);
    return o;
}

float4 HikariFragMeta(HikariVaryingsMeta i) : SV_Target
{
    HikariSurfaceUv uv;
    uv.uv = i.tex.xy;
    uv.detail_uv = i.tex.zw;

    SurfaceOutputStandard s = (SurfaceOutputStandard)0;
    HikariSurface(uv, s);

    UnityMetaInput meta;
    UNITY_INITIALIZE_OUTPUT(UnityMetaInput, meta);
    meta.Albedo = s.Albedo;
    meta.Emission = s.Emission;
    return UnityMetaFragment(meta);
}

#endif

#endif
