#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace SuzuFactory.Hikari
{
    public enum HikariLightingMode : uint
    {
        FullLighting = 0,
        Indirect = 1,
        Shadowmask = 2,
        Subtractive = 3
    }

    public enum HikariDirectionalMode : uint
    {
        NonDirectional = 0,
        DominantDirection = 1,
        MonoSH = 2
    }

    public enum HikariCausticMode : uint
    {
        Off = 0,
        Forward = 3,
    }

    public enum HikariDenoiseMode : uint
    {
        Off = 0,
        NLMeans = 1
    }

    public enum HikariGpuPriority : uint
    {
        Low = 0,
        Normal = 1,
        High = 2
    }

    public enum HikariOutputFolderMode : uint
    {
        Fixed = 0,
        NextToScene = 1
    }

    public enum HikariExrBitDepth : uint
    {
        [InspectorName("16-bit (half)")] Half = 0,
        [InspectorName("32-bit (float)")] Float = 1
    }

    public sealed class HikariAtlasSizePopupAttribute : PropertyAttribute
    {
    }

    [Serializable]
    public sealed class HikariBakeSettings
    {
        public HikariOutputFolderMode OutputFolderMode = HikariBakeDefaults.OutputFolderMode;
        public string OutputFolder = HikariBakeDefaults.OutputFolder;
        public bool IncludeInactive = HikariBakeDefaults.IncludeInactive;
        [HikariAtlasSizePopup] public int MaxAtlasSize = HikariBakeDefaults.MaxAtlasSize;
        [Min(0)] public int AtlasMarginPixels = HikariBakeDefaults.AtlasMarginPixels;
        [Min(0)] public int MipmapLevels = HikariBakeDefaults.MipmapLevels;
        [Min(0.0f)] public float TexelsPerUnit = HikariBakeDefaults.TexelsPerUnit;
        [HikariAtlasSizePopup] public int EnvironmentCubemapFaceSize = HikariBakeDefaults.EnvironmentCubemapFaceSize;
        public bool LowDiscrepancySampling = HikariBakeDefaults.LowDiscrepancySampling;
        public bool EnvironmentImportanceSampling = HikariBakeDefaults.EnvironmentImportanceSampling;
        public bool LightBvhSampling = HikariBakeDefaults.LightBvhSampling;
        [Min(0)] public int FinalGatherIterations = HikariBakeDefaults.FinalGatherIterations;
        [Min(1)] public int FinalGatherTaps = HikariBakeDefaults.FinalGatherTaps;
        [Min(1)] public int EnvNeeSamples = HikariBakeDefaults.EnvNeeSamples;
        [Min(1)] public int EmissiveNeeSamples = HikariBakeDefaults.EmissiveNeeSamples;
        public bool StitchSeams = HikariBakeDefaults.StitchSeams;
        public float SeamGradientWeight = HikariBakeDefaults.SeamGradientWeight;
        public int DirectionalSmoothRadius = HikariBakeDefaults.DirectionalSmoothRadius;
        public HikariDenoiseMode Denoise = HikariBakeDefaults.Denoise;
        [Range(0.0f, 4.0f)] public float DenoiseH = HikariBakeDefaults.DenoiseH;
        [HikariAtlasSizePopup] public int MaterialProbeSize = HikariBakeDefaults.MaterialProbeSize;
        [HikariAtlasSizePopup] public int NormalProbeSize = HikariBakeDefaults.NormalProbeSize;
        public TextureImporterCompression LightmapCompression = HikariBakeDefaults.LightmapCompression;
        public TextureImporterFormat ColorFormat = HikariBakeDefaults.ColorFormat;
        public TextureImporterFormat DirectionalFormat = HikariBakeDefaults.DirectionalFormat;
        public TextureImporterFormat ShadowMaskFormat = HikariBakeDefaults.ShadowMaskFormat;
        public HikariExrBitDepth ExrBitDepth = HikariBakeDefaults.ExrBitDepth;
        public HikariDirectionalMode DirectionalMode = HikariBakeDefaults.DirectionalMode;
        public HikariCausticMode CausticMode = HikariBakeDefaults.CausticMode;
        public bool SpectralDispersion = HikariBakeDefaults.SpectralDispersion;
        public HikariLightingMode LightingMode = HikariBakeDefaults.LightingMode;
        public bool ExcludeHiddenTexels = HikariBakeDefaults.ExcludeHiddenTexels;
        [Min(0.0f)] public float HiddenTexelContactDistance = HikariBakeDefaults.HiddenTexelContactDistance;
        [Range(0.0f, 90.0f)] public float HiddenTexelAngle = HikariBakeDefaults.HiddenTexelAngle;
        public bool ExcludeBackfaceContact = HikariBakeDefaults.ExcludeBackfaceContact;
        [Min(0.0f)] public float BackfaceContactTexels = HikariBakeDefaults.BackfaceContactTexels;
        [Min(0.0f)] public float IndirectMultiplier = HikariBakeDefaults.IndirectMultiplier;
        [Min(0.0f)] public float AoWeight = HikariBakeDefaults.AoWeight;
        [Min(0.0f)] public float AoRadius = HikariBakeDefaults.AoRadius;
        [Min(1)] public int AoSamples = HikariBakeDefaults.AoSamples;
        [Min(1)] public int LightProbeSamples = HikariBakeDefaults.LightProbeSamples;
        public bool ExcludeLightProbeL2 = HikariBakeDefaults.ExcludeLightProbeL2;
        public bool ProjectLightProbesOutOfSurfaces = HikariBakeDefaults.ProjectLightProbesOutOfSurfaces;
        [Min(0.0f)] public float LightProbeSurfaceSearchDistance = HikariBakeDefaults.LightProbeSurfaceSearchDistance;
        [Min(1)] public int SamplesPerTexel = HikariBakeDefaults.SamplesPerTexel;
        [Min(1)] public int MaxBounces = HikariBakeDefaults.MaxBounces;
        public int RandomSeed = HikariBakeDefaults.RandomSeed;
        [Min(0)] public int RussianRouletteMinBounce = HikariBakeDefaults.RussianRouletteMinBounce;
        [Range(0.01f, 1.0f)] public float RussianRouletteSurvival = HikariBakeDefaults.RussianRouletteSurvival;
        public bool FilterRadianceCache = HikariBakeDefaults.FilterRadianceCache;
    }
}
#endif
