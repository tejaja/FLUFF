#ifndef HIKARI_LIGHTMAP_DECODE_INCLUDED
#define HIKARI_LIGHTMAP_DECODE_INCLUDED

float HikariMonoShGain(float3 nl1, float3 normal)
{
    float directionality = length(nl1);
    if (directionality <= 1.0e-4)
    {
        return 1.0;
    }
    float3 dominant = nl1 / directionality;
    float q = dot(dominant, normal) * 0.5 + 0.5;
    float p = 1.0 + 2.0 * directionality;
    float a = (1.0 - directionality) / (1.0 + directionality);
    float g = a + (1.0 - a) * (p + 1.0) * pow(max(q, 0.0), p);
    return max(g, 1.0e-4);
}

half3 HikariDecodeDirectionalLightmap(half3 baked, half4 direction, half3 normal, float mode)
{
    if (mode > 1.5)
    {
        return baked * HikariMonoShGain(direction.rgb * 2.0 - 1.0, normal);
    }
    if (mode > 0.5)
    {
        half half_lambert = dot(normal, direction.rgb - 0.5) + 0.5;
        return baked * (half_lambert / max(1.0e-4, direction.a));
    }
    return baked;
}

#endif
