#if UNITY_EDITOR
using UnityEngine;

namespace SuzuFactory.Hikari
{
    public enum HikariLightBakeMode : uint
    {
        Baked = 0,
        Mixed = 1,
        Realtime = 2
    }

    public enum HikariIesAxis
    {
        Auto = 0,
        Down = 1,
        Forward = 2
    }

    public static class HikariLightOverrideLimits
    {
        public const int MinDistributionSize = 8;
        public const int MaxDistributionSize = 2048;
        public const int DefaultDistributionSize = 256;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hikari/Hikari Light Override")]
    [HelpURL("https://doc.suzufactory.com/Hikari/component-light-override")]
    public sealed class HikariLightOverride : MonoBehaviour
    {
        public bool OverrideColor;
        public Color Color = UnityEngine.Color.white;

        public bool OverrideIntensity;
        [Min(0.0f)]
        public float Intensity = 1.0f;

        public bool OverrideColorTemperature;
        public bool UseColorTemperature;
        [Range(1000.0f, 20000.0f)]
        public float ColorTemperature = 6570.0f;

        public bool OverrideRange;
        [Min(0.0f)]
        public float Range = 10.0f;

        public bool OverrideSpotAngle;
        [Range(1.0f, 179.0f)]
        public float SpotAngle = 30.0f;
        [Range(0.0f, 179.0f)]
        public float InnerSpotAngle = 21.80208f;

        public bool OverrideAreaSize;
        public Vector2 AreaSize = Vector2.one;

        public bool OverrideShadowRadius;
        [Min(0.0f)]
        public float ShadowRadius;

        public bool OverrideShadowAngle;
        [Min(0.0f)]
        public float ShadowAngle;

        public bool OverrideBakeMode;
        public HikariLightBakeMode BakeMode = HikariLightBakeMode.Baked;

        public bool OverrideCookie;
        public Texture Cookie;
        public float CookieSize = 10.0f;

        public bool OverrideIes;
        public Object IesFile;
        public HikariIesAxis IesAxis = HikariIesAxis.Auto;

        [Range(HikariLightOverrideLimits.MinDistributionSize, HikariLightOverrideLimits.MaxDistributionSize)]
        public int DistributionSize = HikariLightOverrideLimits.DefaultDistributionSize;

        public bool HasAnyOverride =>
            OverrideColor || OverrideIntensity || OverrideColorTemperature ||
            OverrideRange || OverrideSpotAngle || OverrideAreaSize ||
            OverrideShadowRadius || OverrideShadowAngle || OverrideBakeMode ||
            OverrideCookie || OverrideIes;

        public Light Light => TryGetComponent(out Light light) ? light : null;

        public bool AppliesTo(Light light)
        {
            return light != null && enabled && HasAnyOverride && light.gameObject == gameObject;
        }

        public static HikariLightOverride FindFor(Light light)
        {
            if (light == null || !light.TryGetComponent(out HikariLightOverride light_override))
            {
                return null;
            }

            return light_override.AppliesTo(light) ? light_override : null;
        }

        public static HikariLightBakeMode ToBakeMode(LightmapBakeType bakeType)
        {
            switch (bakeType)
            {
                case LightmapBakeType.Mixed:
                    return HikariLightBakeMode.Mixed;
                case LightmapBakeType.Realtime:
                    return HikariLightBakeMode.Realtime;
                default:
                    return HikariLightBakeMode.Baked;
            }
        }

        public static LightmapBakeType ToLightmapBakeType(HikariLightBakeMode bakeMode)
        {
            switch (bakeMode)
            {
                case HikariLightBakeMode.Mixed:
                    return LightmapBakeType.Mixed;
                case HikariLightBakeMode.Realtime:
                    return LightmapBakeType.Realtime;
                default:
                    return LightmapBakeType.Baked;
            }
        }

        private void Reset()
        {
            if (!TryGetComponent(out Light light))
            {
                return;
            }

            Color = light.color;
            Intensity = light.intensity;
            UseColorTemperature = light.useColorTemperature;
            ColorTemperature = light.colorTemperature;
            Range = light.range;
            SpotAngle = light.spotAngle;
            InnerSpotAngle = light.innerSpotAngle;
            AreaSize = light.areaSize;
            ShadowRadius = Mathf.Max(0.0f, light.shadowRadius);
            ShadowAngle = Mathf.Max(0.0f, light.shadowAngle);
            BakeMode = ToBakeMode(light.lightmapBakeType);
            Cookie = light.cookie;
            CookieSize = light.cookieSize;
        }
    }
}
#endif
