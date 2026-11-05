#ifndef HIKARI_SSR_SHARED_INCLUDED
#define HIKARI_SSR_SHARED_INCLUDED

struct HikariSsrResult
{
    half3 color;
    half confidence;
};

float hash14(float4 v)
{
    const uint4 mult = uint4(0xae3cc725, 0x9fe72885, 0xae36bfb5, 0x82c1fcad);

    uint4 u = asuint(v);
    u = u * mult;
    u ^= u.wxyz ^ u >> 13;

    uint r = dot(u, mult);

    r ^= r >> 11;
    r = (r * r) ^ r;

    return r * 2.3283064365386962890625e-10;
}

float HikariSsrLinearEyeDepth(float raw_depth, float2 viewport_uv)
{
#if defined(UNITY_REVERSED_Z)
    raw_depth = 1.0 - raw_depth;
    if (raw_depth >= 1.0)
    {
        return _ProjectionParams.z;
    }
#endif
    float4 clip_position = float4(viewport_uv, raw_depth, 1.0);
    clip_position.xyz = 2.0 * clip_position.xyz - 1.0;
    float4 view_position = mul(unity_CameraInvProjection, clip_position);
    return -view_position.z / view_position.w;
}

float4 HikariSsrViewportScaleOffset()
{
#if defined(UNITY_SINGLE_PASS_STEREO)
    return unity_StereoScaleOffset[unity_StereoEyeIndex];
#else
    return float4(1.0, 1.0, 0.0, 0.0);
#endif
}

float3 HikariSsrLowDiscrepancySample(float2 pixel, float screen_width)
{
    uint pixel_index = (uint)pixel.x + (uint)pixel.y * (uint)screen_width;
    uint temporal_index = (uint)floor(_Time.y * 60.0);
    uint sequence_index = (pixel_index + temporal_index) & 0xffffu;
    float2 tile = floor(pixel * 0.125);
    float3 rotation = float3(
        hash14(float4(tile, 3.0, 0.0)),
        hash14(float4(tile, 4.0, 0.0)),
        hash14(float4(tile, 5.0, 0.0)));
    return frac(
        ((float)sequence_index + 0.5) * float3(0.819172513, 0.671043607, 0.549700478) +
        rotation);
}

float HikariSsrProjectedThickness(
    float minimum_thickness,
    float eye_depth,
    float2 projection_scale,
    float2 screen_extent)
{
    float projected_depth = lerp(
        max(eye_depth, 1.0e-4),
        1.0,
        saturate(unity_OrthoParams.w));
    float2 pixel_extent =
        2.0 * projected_depth /
        max(abs(projection_scale) * screen_extent, 1.0e-4);
    return max(minimum_thickness, max(pixel_extent.x, pixel_extent.y));
}

float3 HikariSsrSampleMicrofacetNormal(
    float3 view_direction,
    float3 surface_normal,
    float smoothness,
    float2 random_sample)
{
    float3 reference_axis = abs(surface_normal.z) < 0.999
        ? float3(0.0, 0.0, 1.0)
        : float3(0.0, 1.0, 0.0);
    float3 tangent = normalize(cross(reference_axis, surface_normal));
    float3 bitangent = cross(surface_normal, tangent);
    float3 view_local = float3(
        dot(view_direction, tangent),
        dot(view_direction, bitangent),
        max(dot(view_direction, surface_normal), 1.0e-4));
    float perceptual_roughness = 1.0 - saturate(smoothness);
    float alpha = perceptual_roughness * perceptual_roughness;
    float3 stretched_view = normalize(float3(alpha * view_local.xy, view_local.z));
    float lensq = dot(stretched_view.xy, stretched_view.xy);
    float3 stretched_tangent = lensq > 0.0
        ? float3(-stretched_view.y, stretched_view.x, 0.0) * rsqrt(lensq)
        : float3(1.0, 0.0, 0.0);
    float3 stretched_bitangent = cross(stretched_view, stretched_tangent);
    float radius = sqrt(random_sample.x);
    float angle = 6.2831853 * random_sample.y;
    float sine;
    float cosine;
    sincos(angle, sine, cosine);
    float t1 = radius * cosine;
    float t2 = radius * sine;
    float blend = 0.5 * (1.0 + stretched_view.z);
    t2 = lerp(sqrt(saturate(1.0 - t1 * t1)), t2, blend);
    float3 stretched_normal =
        t1 * stretched_tangent +
        t2 * stretched_bitangent +
        sqrt(saturate(1.0 - t1 * t1 - t2 * t2)) * stretched_view;
    float3 microfacet_normal_local = normalize(float3(
        alpha * stretched_normal.xy,
        max(stretched_normal.z, 0.0)));
    return normalize(
        tangent * microfacet_normal_local.x +
        bitangent * microfacet_normal_local.y +
        surface_normal * microfacet_normal_local.z);
}

static const float kHikariSsrSmoothnessMin = 0.35;
static const float kHikariSsrSmoothnessRange = 0.45;

#endif
