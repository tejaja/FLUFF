#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace SuzuFactory.Hikari
{
    public enum HikariMaterialOverrideTargets : uint
    {
        AllMaterials = 0,
        SelectedMaterials = 1,
        SelectedShaders = 2
    }

    public enum HikariBakeEmissionOverride : uint
    {
        Unchanged = 0,
        Enabled = 1,
        Disabled = 2
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hikari/Hikari Material Override")]
    [HelpURL("https://doc.suzufactory.com/Hikari/component-material-override")]
    public sealed class HikariMaterialOverride : MonoBehaviour
    {
        [Tooltip("Which materials under this object the override applies to.")]
        public HikariMaterialOverrideTargets Targets = HikariMaterialOverrideTargets.AllMaterials;

        [Tooltip("The override applies only to these materials.")]
        public List<Material> Materials = new List<Material>();

        [Tooltip("The override applies only to materials that use these shaders.")]
        public List<Shader> Shaders = new List<Shader>();

        public bool OverrideAlbedo;
        public Color AlbedoColor = Color.white;
        public Texture AlbedoTexture;

        public bool OverrideEmission;
        [ColorUsage(false, true)]
        public Color EmissionColor = Color.black;
        public Texture EmissionTexture;

        public bool OverrideEmissionMultiplier;
        [Tooltip("Scale only this material's emission by this factor (1 = unchanged, higher = brighter emission).")]
        [Min(0.0f)]
        public float EmissionMultiplier = 1.0f;

        [Tooltip("Override whether this material's emission is baked into GI (lightmap and light probes). Unchanged keeps the material's own setting; Enabled forces it into GI; Disabled excludes it from GI while the surface still shows its emission in the render.")]
        public HikariBakeEmissionOverride BakeEmission = HikariBakeEmissionOverride.Unchanged;

        public bool OverrideTransmission;
        public Color TransmissionColor = Color.white;
        public Texture TransmissionTexture;

        [Tooltip("Treat the closed mesh volume as a homogeneous participating medium during baking. This does not change the material's Transmission.")]
        public bool Volumetric;

        [Tooltip("Color remaining after light travels one world-space unit through the medium. Red preserves red while absorbing green and blue.")]
        [ColorUsage(false, false)]
        public Color VolumetricAbsorption = Color.white;

        [Tooltip("Isotropic scattering coefficient per world-space unit.")]
        [Min(0.0f)]
        public float VolumetricScattering;

        [Tooltip("Radiance emitted by the medium per world-space unit.")]
        [ColorUsage(false, true)]
        public Color VolumetricEmission = Color.black;

        public bool OverrideMetallic;
        [Range(0.0f, 1.0f)]
        public float Metallic;

        public bool OverrideSmoothness;
        [Range(0.0f, 1.0f)]
        public float Smoothness;

        public bool OverrideIor;
        [Min(0.001f)]
        public float Ior = 1.0f;

        public bool OverrideAbbe;
        [Tooltip("Abbe number (dispersion). 0 = no dispersion; lower = stronger rainbow (typical glass 20-70).")]
        [Min(0.0f)]
        public float Abbe = 0.0f;

        public bool OverrideNormalMap;
        [Tooltip("Tangent-space normal map used for baking. None removes the material's normal map.")]
        public Texture NormalMapTexture;
        [Tooltip("Strength of the normal map (matches the material's _BumpScale).")]
        public float NormalMapScale = 1.0f;
        public Vector2 NormalMapTiling = Vector2.one;
        public Vector2 NormalMapOffset = Vector2.zero;

        public bool OverrideDetailNormalMap;
        [Tooltip("Tangent-space detail normal map used for baking. None removes the material's detail normal map.")]
        public Texture DetailNormalMapTexture;
        [Tooltip("Strength of the detail normal map (matches the material's _DetailNormalMapScale).")]
        public float DetailNormalMapScale = 1.0f;
        public Vector2 DetailNormalMapTiling = Vector2.one;
        public Vector2 DetailNormalMapOffset = Vector2.zero;

        public bool OverrideUvRange;
        [Tooltip("Explicit UV range captured by the material probe. Leave off to measure it automatically from the mesh UVs. Set to (0,0)-(1,1) to force the classic single-tile probe.")]
        public Vector2 UvRangeMin = Vector2.zero;
        public Vector2 UvRangeMax = Vector2.one;

        public bool OverrideMaterialProbeSize;
        [Tooltip("Material probe resolution used for this override.")]
        [HikariAtlasSizePopup]
        public int MaterialProbeSize = HikariBakeDefaults.MaterialProbeSize;

        public bool OverrideNormalProbeSize;
        [Tooltip("Maximum exported normal map resolution used for this override.")]
        [HikariAtlasSizePopup]
        public int NormalProbeSize = HikariBakeDefaults.NormalProbeSize;

        public bool HasAnyOverride =>
            OverrideAlbedo || OverrideEmission || OverrideTransmission ||
            Volumetric ||
            OverrideMetallic || OverrideSmoothness || OverrideIor || OverrideAbbe ||
            OverrideEmissionMultiplier || BakeEmission != HikariBakeEmissionOverride.Unchanged ||
            OverrideMaterialProbeSize || OverrideNormalProbeSize ||
            OverrideNormalMap || OverrideDetailNormalMap ||
            OverrideUvRange;

        public bool AppliesToMaterial(Material material)
        {
            if (!HasAnyOverride)
            {
                return false;
            }

            switch (Targets)
            {
                case HikariMaterialOverrideTargets.SelectedMaterials:
                    return material != null && Materials.Contains(material);
                case HikariMaterialOverrideTargets.SelectedShaders:
                    return material != null && material.shader != null && Shaders.Contains(material.shader);
                default:
                    return true;
            }
        }

        public static HikariMaterialOverride FindFor(GameObject gameObject)
        {
            for (Transform transform = gameObject != null ? gameObject.transform : null;
                 transform != null;
                 transform = transform.parent)
            {
                if (transform.TryGetComponent(out HikariMaterialOverride material_override) &&
                    material_override.enabled)
                {
                    return material_override;
                }
            }

            return null;
        }
    }
}
#endif
