#if UNITY_EDITOR
using UnityEditor;

namespace SuzuFactory.Hikari
{
    public static class HikariBakeDefaults
    {
        public const string OutputFolder = "Assets/HikariLightmaps";
        public const HikariOutputFolderMode OutputFolderMode = HikariOutputFolderMode.Fixed;
        public const bool IncludeInactive = false;
        public const int MaxAtlasSize = 4096;
        public const int AtlasMarginPixels = 2;
        public const int MipmapLevels = 0;
        public const float TexelsPerUnit = 40.0f;
        public const int EnvironmentCubemapFaceSize = 128;
        public const float EnvironmentCubemapIntensity = 1.0f;
        public const int SamplesPerTexel = 64;
        public const int MaxBounces = 5;
        public const int TileSize = 16;
        public const int RandomSeed = 1;
        public const int MaxActiveGpus = 1;
        public const int RussianRouletteMinBounce = 3;
        public const float RussianRouletteSurvival = 0.85f;
        public const bool LowDiscrepancySampling = true;
        public const bool EnvironmentImportanceSampling = true;
        public const bool LightBvhSampling = true;
        public const int FinalGatherIterations = 1;
        public const int FinalGatherTaps = 16;
        public const int EnvNeeSamples = 4;
        public const int EmissiveNeeSamples = 4;
        public const bool FilterRadianceCache = true;
        public const HikariDenoiseMode Denoise = HikariDenoiseMode.NLMeans;
        public const float DenoiseH = 2.0f;
        public const bool SaveDebugData = false;
        public const bool RunBakeInBackground = true;
        public const bool BakeCompleteSound = false;
        public const string DebugOutputPath = "Temp/Hikari/DebugData";
        public const int MaterialProbeSize = 64;
        public const int NormalProbeSize = 1024;
        public const bool UseSoftwareBvh = false;
        public const HikariGpuPriority GpuPriority = HikariGpuPriority.Low;
        public const float GpuUsagePercent = 90.0f;
        public const bool ExcludeHiddenTexels = false;
        public const float HiddenTexelContactDistance = 0.001f;
        public const float HiddenTexelAngle = 5.0f;
        public const bool ExcludeBackfaceContact = true;
        public const float BackfaceContactTexels = 1.0f;
        public const float IndirectMultiplier = 1.0f;
        public const float AoWeight = 0.0f;
        public const float AoRadius = 1.0f;
        public const int AoSamples = 16;
        public const TextureImporterCompression LightmapCompression = TextureImporterCompression.Compressed;
        public const TextureImporterFormat ColorFormat = TextureImporterFormat.RGB9E5;
        public const TextureImporterFormat DirectionalFormat = TextureImporterFormat.Automatic;
        public const TextureImporterFormat ShadowMaskFormat = TextureImporterFormat.Automatic;
        public const HikariExrBitDepth ExrBitDepth = HikariExrBitDepth.Half;
        public const HikariDirectionalMode DirectionalMode = HikariDirectionalMode.NonDirectional;
        public const HikariCausticMode CausticMode = HikariCausticMode.Forward;
        public const bool SpectralDispersion = false;
        public const HikariLightingMode LightingMode = HikariLightingMode.FullLighting;
        public const int LightProbeSamples = 1024;
        public const bool ExcludeLightProbeL2 = false;
        public const bool ProjectLightProbesOutOfSurfaces = true;
        public const float LightProbeSurfaceSearchDistance = 1.0f;
        public const bool OutputShadowMask = false;
        public const bool BakeLightmaps = true;
        public const bool BakeLightProbes = true;
        public const bool StitchSeams = true;
        public const float SeamGradientWeight = 0.0f;
        public const float SeamGradientC1Weight = 0.5f;
        public const int DirectionalSmoothRadius = 1;
    }
}
#endif
